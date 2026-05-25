using System;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Dexa;
using Dexa.Helpers;

namespace Else.PhoneMirror.ViewModels;

public static class ScrCpy
{
    private static readonly IMemoryCache _ipCache = new MemoryCache(new MemoryCacheOptions());
    private static readonly TimeSpan _ipCacheDuration = TimeSpan.FromSeconds(20);

    private static readonly IMemoryCache _deviceCache = new MemoryCache(new MemoryCacheOptions());
    private static readonly TimeSpan _deviceCacheDuration = TimeSpan.FromSeconds(1);

    private static readonly IMemoryCache _deviceInfoCache = new MemoryCache(new MemoryCacheOptions());
    private static readonly TimeSpan _deviceInfoCacheDuration = TimeSpan.FromMinutes(5);

    public static List<AdbDevice> GetAllDevices()
    {
        if (_deviceCache.TryGetValue("", out var cachedDevices))
            return cachedDevices as List<AdbDevice>;

        var devices = RunAdb($"devices")
            .Split(new[] { '\n', '\r' })
            .Where((x, i) => !string.IsNullOrEmpty(x) && i > 0)
            .Select(x => x.IndexOf("\t") >= 0 ? x.Substring(0, x.IndexOf("\t")) : x)
            .ToList();

        var allDevices = devices
            .Select(deviceName =>
            {
                var ip = GetIp(deviceName);
                var isEmulator = deviceName.ToLower().Contains("emulator");
                var isWifi = IpHelper.IsIpAddress(deviceName);
                return AdbDevice.Create(
                    deviceName,
                    hardwareName: deviceName,
                    // isEnabled: !isEmulator,
                    ipAddress: ip,
                    isRemoteConnection: IpHelper.IsIpAddress(deviceName),
                    isEmulator: isEmulator,
                    isNetworkVisible: IpHelper.IsLocalIpAddress(ip),
                    friendlyName: GetDeviceFriendlyName(deviceName, isWifi),
                    canBeRemoteConnected: false);
            })
            .ToList();

        foreach (var device in allDevices)
        {
            if (!device.IsRemoteConnection)
                continue;

            var hardwareDevice = allDevices
                .FirstOrDefault(x => !x.IsRemoteConnection && x.IpAddress == device.IpAddress);

            if (hardwareDevice == null)
                continue;

            device.HardwareName = hardwareDevice.Name;
        }

        foreach (var device in allDevices)
        {
            if (device.IsRemoteConnection)
                continue;

            var wifiDevice = allDevices
                .FirstOrDefault(x => x.IsRemoteConnection && x.IpAddress == device.IpAddress);

            device.CanBeRemoteConnected = wifiDevice == null;
        }

        _deviceCache.Set("", allDevices, _deviceCacheDuration);

        return allDevices;
    }

    private static string GetDeviceFriendlyName(string deviceName, bool isWifi)
    {
        try
        {
            var deviceInfo = ScrCpy.GetDeviceInfo(deviceName);

            deviceInfo.TryGetValue("Model", out var model);
            var friendlyName = string.IsNullOrEmpty(model) ? deviceName : model;

            if (isWifi)
                friendlyName += " ⫽ WiFi";

            return friendlyName;
        }
        catch
        {
            return deviceName;
        }
    }

    public static void ConnectWireless(string hardwareDevice)
    {
        RunAdb($"-s {hardwareDevice} tcpip 5555");
    }

    public static void ConnectWirelessTo(string hardwareDevice, string ip)
    {
        RunAdb($"-s {hardwareDevice} connect {ip}:5555");
    }

    public static string GetIp(string hardwareDevice)
    {
        if (_ipCache.TryGetValue(hardwareDevice, out var responseIp))
            return (string)responseIp;

        // var hardwareDevice = GetHardwareDevices().FirstOrDefault();
        var input = "";
        var st = Stopwatch.StartNew();

        while (st.Elapsed < TimeSpan.FromMilliseconds(200))
        {
            input = RunAdb($"-s {hardwareDevice} exec-out ip -f inet addr show wlan0");
            Thread.Sleep(5);
            if (!String.IsNullOrEmpty(input))
                break;
        }

        // Regex: najpierw nazwa interfejsu (np. "wlan0" lub "rmnet_data0"), potem linia z inet,
        // walidacja oktetów w zakresie 0–255
        Regex regex = new Regex(
            @"^\s*\d+:\s*(?<iface>[\w@]+):.*\r?\n\s*inet\s+(?!127\.)" +
            @"(?<ip>(25[0-5]|2[0-4]\d|1?\d{1,2})(\.(25[0-5]|2[0-4]\d|1?\d{1,2})){3})",
            RegexOptions.Multiline);

        Match match = regex.Match(input);

        if (match.Success)
        {
            string iface = match.Groups["iface"].Value;
            string ip = match.Groups["ip"].Value;
            //Console.WriteLine($"Znaleziono interfejs: {iface}");
            //Console.WriteLine($"Znaleziony adres IP: {ip}");
            _ipCache.Set(hardwareDevice, ip, _ipCacheDuration);
            return ip;
        }
        else
        {
            _ipCache.Set(hardwareDevice, "", _ipCacheDuration);
            return "";
        }
    }

    public static void DisconnectWireless(string hardwareDevice)
    {
        RunAdb($"-s {hardwareDevice} disconnect");
    }

    public static string BuildScrcpyArguments(
        Device HardwareDevice,
        DeviceWindow? deviceWindow,
        string? RecordFilePath,
        bool isFullscreen,
        bool isGameMode)
    {
        if (HardwareDevice == null)
            return null;

        // Get settings from app
        var settings = AppSettings.Load();

        var friendlyTitle = $"Dexa ⯽ {HardwareDevice.FriendlyName}";

        var args = $" -s {HardwareDevice.Name}" +
                   $" --stay-awake" +
                   $" --shortcut-mod=rsuper" +
                   $" --window-title \"{friendlyTitle}\"";

        if (!isFullscreen && deviceWindow?.HasBounds() == true)
        {
            var bounds = WindowInfoUtils.ValidateWindowBounds(new Rectangle(
                (int)deviceWindow.X, (int)deviceWindow.Y,
                (int)deviceWindow.Width, (int)deviceWindow.Height));
            args += $" --window-x={bounds.X} --window-y={bounds.Y}" +
                    $" --window-width={bounds.Width} --window-height={bounds.Height}";
        }

        if (settings.IsAspectRatioUnlocked)
        {
            args += " --no-window-aspect-ratio-lock";
            args += " --no-auto-resize";
            args += " --auto-rotate-on-resize";
        }

        if (!settings.IsAudioEnabled)
            args += " --no-audio";

        if (!settings.IsKeyboardEnabled)
            args += " --gamepad=uhid --keyboard=uhid";

        if (!settings.IsScreenOffEnabled)
            args += " --turn-screen-off";

        if (isFullscreen)
            args += $" --fullscreen";

        if (HardwareDevice.IsRemoteConnection)
            args += $" -m 960" +
                    $" -b 4M" +
                    $" --max-fps=45";
        else if (isGameMode)
            args += $" -m 1024" +
                    $" -b 4M" +
                    $" --max-fps=60";
        else
            args += $" -m 1280" +
                    $" -b 6M" +
                    $" --max-fps=60";

        if (!string.IsNullOrEmpty(RecordFilePath))
            args += $" --record=\"{RecordFilePath}\"";
        return args;
    }

    public static void ShowDesktop()
    {
        RunAdb($"exec-out input keyevent 3");
    }

    public static int? GetVirtualActiveDisplayId(string input)
    {
        if (string.IsNullOrEmpty(input))
            return null;

        // Wzorzec:
        //  - zaczyna się od "DisplayViewport{"
        //  - w obrębie tego nawiasu szuka w dowolnej kolejności fragmentów:
        //      type=VIRTUAL, valid=true, isActive=true
        //  - a potem odszukuje displayId=<cyfraciąg> i łapie tę grupę.
        var pattern = @"DisplayViewport\{[^}]*?type=VIRTUAL[^}]*?valid=true[^}]*?isActive=true[^}]*?displayId=(\d+)";
        var match = Regex.Match(input, pattern);

        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int id))
                return id;
        }

        return null;
    }

    public static string RunAdb(String parameters, bool wait = true)
    {
        try
        {
            //Console.WriteLine($"adb {parameters}");
            string scrcpyPath = @"scrcpy\adb.exe"; // Upewnij się, że ścieżka jest poprawna
            string arguments = parameters;

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = scrcpyPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true, // ← dorzuć przekierowanie stdin
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var st = Stopwatch.StartNew();
            using (var process = Process.Start(psi))
            {
                process.StandardInput.Close();

                var reader = process.StandardOutput;
                var sw = Stopwatch.StartNew();
                var sb = new StringBuilder();

                if (wait)
                {
                    while (!reader.EndOfStream && sw.ElapsedMilliseconds < 30000)
                    {
                        sb.AppendLine(reader.ReadLine());
                    }

                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }

                return sb.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Wystąpił błąd: {ex.Message}");
            return "";
        }
    }

    public static Orientation? ToggleScreenOrientation(string? hardwareDeviceName)
    {
        if (hardwareDeviceName == null)
            return null;

        ScrCpy.RunAdb(
            $"-s {hardwareDeviceName} exec-out pm grant com.android.shell android.permission.WRITE_SECURE_SETTINGS");
        ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system accelerometer_rotation 0");

        var screenOrientation = ScrCpy.GetDeviceOrientation(hardwareDeviceName);
        if (screenOrientation == Orientation.Horizontal)
        {
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system user_rotation 0");
        }
        else
        {
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system user_rotation 1");
        }

        ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system accelerometer_rotation 1");
        return screenOrientation == Orientation.Horizontal ? Orientation.Vertical : Orientation.Horizontal;
    }

    public static void SetScreenOrientationVertical(string? hardwareDeviceName, bool executeSystemPermissions = true)
    {
        if (hardwareDeviceName == null)
            return;

        if (executeSystemPermissions)
        {
            ScrCpy.RunAdb(
                $"-s {hardwareDeviceName} exec-out pm grant com.android.shell android.permission.WRITE_SECURE_SETTINGS");
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system accelerometer_rotation 0");
            Thread.Sleep(1000);
        }

        var screenOrientation = ScrCpy.GetDeviceOrientation(hardwareDeviceName);
        if (screenOrientation == Orientation.Horizontal)
        {
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system user_rotation 0");
            // ScrCpy.RunAdb($"-s {hardwareDeviceName} shell settings put system accelerometer_rotation 1");
            Thread.Sleep(1000);
        }
    }

    public static void SetScreenOrientationHorizontal(string? hardwareDeviceName, bool executeSystemPermissions = true)
    {
        if (hardwareDeviceName == null)
            return;

        if (executeSystemPermissions)
        {
            ScrCpy.RunAdb(
                $"-s {hardwareDeviceName} exec-out pm grant com.android.shell android.permission.WRITE_SECURE_SETTINGS");
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system accelerometer_rotation 0");
            Thread.Sleep(1000);
        }

        var screenOrientation = ScrCpy.GetDeviceOrientation(hardwareDeviceName);
        if (screenOrientation == Orientation.Vertical)
        {
            ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system user_rotation 1");
            //ScrCpy.RunAdb($"-s {hardwareDeviceName} exec-out settings put system accelerometer_rotation 1");
            Thread.Sleep(1000);
        }
    }

    public static Dictionary<string, string> GetDeviceInfo(string deviceName)
    {
        string cacheKey = $"DeviceInfo_{deviceName}";

        // Sprawdź czy informacje są w pamięci podręcznej
        if (_deviceInfoCache.TryGetValue(cacheKey, out var cachedInfo))
            return (Dictionary<string, string>)cachedInfo;

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Pobieranie producenta
        var manufacturer = RunAdb($"-s {deviceName} exec-out getprop ro.product.manufacturer").Trim();
        if (!string.IsNullOrEmpty(manufacturer))
            result["Manufacturer"] = manufacturer;

        // Pobieranie modelu urządzenia
        var model = RunAdb($"-s {deviceName} exec-out getprop ro.product.model").Trim();
        if (!string.IsNullOrEmpty(model))
            result["Model"] = model;

        // Pobieranie nazwy urządzenia
        var deviceMakeName = RunAdb($"-s {deviceName} exec-out getprop ro.product.name").Trim();
        if (!string.IsNullOrEmpty(deviceMakeName))
            result["Name"] = deviceMakeName;

        // Pobieranie wersji Androida
        var androidVersion = RunAdb($"-s {deviceName} exec-out getprop ro.build.version.release").Trim();
        if (!string.IsNullOrEmpty(androidVersion))
            result["AndroidVersion"] = androidVersion;

        // Pobieranie API Androida
        var androidAPI = RunAdb($"-s {deviceName} exec-out getprop ro.build.version.sdk").Trim();
        if (!string.IsNullOrEmpty(androidAPI))
            result["AndroidAPI"] = androidAPI;

        // Pobieranie numeru seryjnego
        var serialNumber = RunAdb($"-s {deviceName} exec-out getprop ro.serialno").Trim();
        if (!string.IsNullOrEmpty(serialNumber))
            result["SerialNumber"] = serialNumber;

        // Pobieranie informacji o wyświetlaczu
        var density = RunAdb($"-s {deviceName} exec-out getprop ro.sf.lcd_density").Trim();
        if (!string.IsNullOrEmpty(density))
            result["DisplayDensity"] = density;

        // Pobieranie architektury CPU
        var cpuArch = RunAdb($"-s {deviceName} exec-out getprop ro.product.cpu.abi").Trim();
        if (!string.IsNullOrEmpty(cpuArch))
            result["CPUArchitecture"] = cpuArch;

        // Pobieranie czasu pracy urządzenia
        var uptime = RunAdb($"-s {deviceName} exec-out cat /proc/uptime").Trim().Split(' ').FirstOrDefault();
        if (!string.IsNullOrEmpty(uptime) && double.TryParse(uptime, out double uptimeSeconds))
            result["Uptime"] = TimeSpan.FromSeconds(uptimeSeconds).ToString();

        // Cache only if we got at least the model name; if model is missing the device
        // wasn't ready yet (ADB daemon warming up) — skip cache so next cycle retries.
        if (result.ContainsKey("Model"))
            _deviceInfoCache.Set($"DeviceInfo_{deviceName}", result, _deviceInfoCacheDuration);

        return result;
    }

    public static void MediaPause(string? hardwareDeviceName)
    {
        if (string.IsNullOrEmpty(hardwareDeviceName))
            return;
        RunAdb($"-s {hardwareDeviceName} exec-out input keyevent KEYCODE_MEDIA_PAUSE");
    }

    public static bool IsMediaPlay(string? hardwareDeviceName)
    {
        if (string.IsNullOrEmpty(hardwareDeviceName))
            return false;
        string output = RunAdb($"-s {hardwareDeviceName} exec-out dumpsys media_session");
        bool isPlaying = false;

        if (output != null && output.Contains("state=PlaybackState") &&
            (output.Contains("state=3") || output.Contains("state=PLAYING(3)")))
            isPlaying = true;

        return isPlaying;
    }

    public static void MediaPlay(string? hardwareDeviceName)
    {
        if (string.IsNullOrEmpty(hardwareDeviceName))
            return;
        RunAdb($"-s {hardwareDeviceName} exec-out input keyevent KEYCODE_MEDIA_PLAY");
    }

    public static void ExecuteHome(string? hardwareDeviceName)
    {
        if (string.IsNullOrEmpty(hardwareDeviceName))
            return;
        RunAdb($"-s {hardwareDeviceName} exec-out input keyevent 3");
    }

    public static Orientation? GetDeviceOrientation(string? hardwareDeviceName)
    {
        if (string.IsNullOrEmpty(hardwareDeviceName))
            return null;

        // 1) Spróbuj odczytać mCurrentOrientation z dumpsys display
        /*string output = RunAdb($"-s {hardwareDeviceName} exec-out dumpsys display");
        var m = Regex.Match(output, @"mCurrentOrientation=(\d)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out int rot))
        {
            switch (rot)
            {
                case 0:
                case 2:
                    return Orientation.Vertical;
                case 1:
                case 3:
                    return Orientation.Horizontal;
            }
        }*/

        var output = RunAdb($"-s {hardwareDeviceName} exec-out dumpsys window displays");
        foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Where(x => x.Contains("mCurrentRotation")))
        {
            if (line.Trim().EndsWith("_0") || line.Trim().EndsWith("_180"))
                return Orientation.Vertical;
            if (line.Trim().EndsWith("_90") || line.Trim().EndsWith("_270"))
                return Orientation.Horizontal;
        }

        return null;
    }
}

public static class ProcessEx
{
    public static bool WaitForExit2(this Process process, long milliseconds)
    {
        if (process == null || process.HasExited)
            return true;

        var st = Stopwatch.StartNew();
        while (st.ElapsedMilliseconds < milliseconds)
        {
            if (process.HasExited)
                break;
            Thread.Sleep(1);
        }

        return process.HasExited;
    }
}

public static class StringEx
{
    public static string FirstLetterCapital(this string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return input.First().ToString().ToUpper() + input.Substring(1);
    }
}
