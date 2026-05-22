using System.Diagnostics;
using System.IO;
using Dexa.Helpers;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public class ScrCpyRunner : IDisposable
{
    private Process? _scrCpyProcess;
    private IntPtr _scrCpyHwnd;
    private bool _isFullscreen;
    private bool _isGameMode;
    private bool _isRecording;
    private string? _recordFile;
    private Stopwatch? _stopwatch;

    private int _lastSavedWindowX;
    private int _lastSavedWindowY;
    private int _lastSavedWindowWidth;
    private int _lastSavedWindowHeight;
    private DateTime _lastWindowStableTime = DateTime.MinValue;
    private static readonly TimeSpan WindowSaveDelay = TimeSpan.FromMilliseconds(800);

    private int _lastWindowWidth;
    private int _lastWindowHeight;
    private DateTime _lastWindowSizeChangeTime = DateTime.MinValue;
    private Orientation? _scrCpyLastOrientation;
    private readonly bool _isAspectRatioUnlocked;
    private const double OrientationThresholdRatio = 0.15;
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(1);

    public bool IsRunning { get; private set; }
    public Device Device { get; }

    public ScrCpyRunner(Device device)
    {
        Device = device;
        _isAspectRatioUnlocked = AppSettings.Load().IsAspectRatioUnlocked;
        LogMessage("Podpięto obsługę zdarzeń klawiatury");
        Start();
    }

    private void OnKeyboardEvent(KeyboardInterceptorWinForms.KeyboardEventArgs e)
    {
        LogMessage($"Otrzymano zdarzenie klawisza {e.KeyCode} dla okna 0x{e.WindowHandle.ToInt64():X}");

        if (!IsKeypressForScrCpyWindow(e.WindowHandle) || !e.IsKeyDown)
            return;

        HandleKeypress(e.KeyCode);
    }

    private bool IsKeypressForScrCpyWindow(IntPtr windowHandle)
    {
        return _scrCpyHwnd != IntPtr.Zero && windowHandle == _scrCpyHwnd;
    }

    private void HandleKeypress(int keyCode)
    {
        switch (keyCode)
        {
            case KeyboardInterceptorWinForms.VirtualKeys.F10:
                ToggleDeviceOrientation();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F11:
                ToggleFullscreenMode();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F9:
                ToggleGameMode();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F5:
                ToggleRecording();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F1:
                ShowHelp();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.ESCAPE:
                CloseApplication();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.HOME:
                ShowDesktop();
                break;

            default:
                LogMessage($"Otrzymano nieobsługiwany klawisz o kodzie {keyCode}");
                break;
        }
    }

    private void ToggleDeviceOrientation()
    {
        LogMessage("Przełączanie orientacji ekranu");
        Task.Run(() => ScrCpy.ToggleScreenOrientation(Device.Name));
    }

    private void ToggleFullscreenMode()
    {
        LogMessage("Przełączanie trybu pełnoekranowego");
        _isFullscreen = !_isFullscreen;
        DisposeProcess();
    }

    private void ToggleGameMode()
    {
        LogMessage("Przełączanie trybu gamingowego");
        _isGameMode = !_isGameMode;
        DisposeProcess();
    }

    private void ToggleRecording()
    {
        if (_isRecording)
        {
            this._recordFile = null;
            this._isRecording = false;
            DisposeProcess(stopRecording: false);
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Zapisz plik",
                    FileName =
                        $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{string.Join("_", Device.FriendlyName.Split(Path.GetInvalidFileNameChars()))}.mp4",
                    DefaultExt = ".mp4",
                    Filter = "Pliki wideo|*.mp4;*.mkv|Wszystkie pliki|*.*"
                };

                var result = saveFileDialog.ShowDialog();
                if (result != DialogResult.OK)
                    return;

                this._recordFile = saveFileDialog.FileName;
                this._isRecording = true;
                DisposeProcess(stopRecording: false);
            });
        }
    }

    private void StopRecording()
    {
        this._recordFile = null;
        this._isRecording = false;
    }

    private void ShowHelp()
    {
        LogMessage("Wyświetlanie pomocy");
        // Implementacja wyświetlania pomocy
    }

    private void CloseApplication()
    {
        LogMessage("Zamykanie aplikacji");
        // Implementacja zamykania aplikacji
    }

    private void ShowDesktop()
    {
        LogMessage("Wyświetlanie pulpitu na urządzeniu");
        ScrCpy.ExecuteHome(Device.Name);
    }

    public void Start()
    {
        if (IsRunning)
            return;
        IsRunning = true;
        StartScrcpyThread();
    }

    private void StartScrcpyThread()
    {
        var thread = new Thread(RunScrcpyProcess);
        thread.IsBackground = true;
        thread.Start();
    }

    private void RunScrcpyProcess(object state)
    {
        try
        {
            var maxRetries = 5;
            var retryCount = 0;

            while (IsRunning)
            {
                if (Device?.IsRunning != true)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                try
                {
                    CleanupPreviousProcess();
                    if (!StartNewScrcpyProcess())
                    {
                        retryCount++;
                        if (maxRetries == retryCount)
                        {
                            Stop();
                            break;
                        }
                    }
                    else
                    {
                        retryCount = 0;
                    }

                    ZmieńIkonęProcesu();
                    AudioResume();
                    WaitForProcessExit(() =>
                    {
                        var windowInfo = _scrCpyHwnd != IntPtr.Zero
                            ? WindowInfoUtils.GetWindowInfo(_scrCpyHwnd)
                            : null;
                        CheckOrientation(windowInfo);
                        UpdateWindowInfo(windowInfo);
                        NotifyDeviceStatus();
                    });

                    if (CheckIfScrCpyIsClosed())
                        break;
                }
                catch (Exception ex)
                {
                    LogMessage(ex.Message);
                    DisposeProcess();
                }
            }
        }
        finally
        {
            IsRunning = false;
            DisposeProcess();
        }
    }

    private void NotifyDeviceStatus()
    {
        if (_stopwatch == null ||
            _stopwatch.Elapsed > TimeSpan.FromSeconds(30))
        {
            Device.InformRunning();
            DeviceRepository.UpdateRunData(Device);
            _stopwatch = Stopwatch.StartNew();
        }
    }

    private bool CheckIfScrCpyIsClosed()
    {
        if (_scrCpyProcess != null &&
            _scrCpyProcess.HasExited == true)
        {
            Stop();
            return true;
        }

        return false;
    }

    private void Stop()
    {
        Device.Disable();
        DeviceRepository.UpdateRunData(Device);
    }

    private void ZmieńIkonęProcesu()
    {
        if (_scrCpyHwnd == IntPtr.Zero)
            return;

        // Zmiana ikony okna procesu
        var nowaIkona = IconHelper.GetAppIcon();
        IconHelper.ZmieńIkonęOkna(_scrCpyHwnd, nowaIkona);
    }

    private void CleanupPreviousProcess()
    {
        if (_scrCpyProcess != null)
            DisposeProcess();
    }

    private bool StartNewScrcpyProcess()
    {
        LogMessage("Uruchamianie procesu scrcpy");
        CreateScrCpyIcon();
        var startInfo = CreateProcessStartInfo();

        StartProcess(startInfo);

        WaitForWindowHandle();

        if (IsValidWindowHandle())
        {
            RegisterWindowForKeyboardEvents();
            ActivateWindow();
            ConfigureWindow();
            ChangeScrCpyWindowName($"Dexa ⫽ {Device.FriendlyName}");
            return true;
        }
        else
        {
            DisposeProcess();
            return false;
        }
    }

    private void StartProcess(ProcessStartInfo startInfo)
    {
        _scrCpyProcess = Process.Start(startInfo);
        if (_scrCpyProcess == null) return;
        _scrCpyProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                LogMessage($"scrcpy stderr: {e.Data}");
        };
        _scrCpyProcess.BeginErrorReadLine();
        try
        {
            _scrCpyProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch
        {
        }
    }

    private static void CreateScrCpyIcon()
    {
        IconHelper.UstawIkonęWMenuStart(@"scrcpy\scrcpy.exe");
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = @"scrcpy\scrcpy.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            Arguments = ScrCpy.BuildScrcpyArguments(
                Device,
                _recordFile,
                _isFullscreen,
                _isGameMode)
        };

        if (!_isFullscreen)
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

        return startInfo;
    }

    private void WaitForWindowHandle()
    {
        var stopwatch = Stopwatch.StartNew();
        const int maxWaitTimeSeconds = 30;

        _scrCpyLastOrientation = ScrCpy.GetDeviceOrientation(Device.Name);
        _lastWindowWidth = 0;
        _lastWindowHeight = 0;
        _lastWindowSizeChangeTime = DateTime.MinValue;

        while (_scrCpyHwnd == IntPtr.Zero &&
               _scrCpyProcess?.HasExited == false &&
               stopwatch.Elapsed < TimeSpan.FromSeconds(maxWaitTimeSeconds))
        {
            _scrCpyHwnd = _scrCpyProcess?.MainWindowHandle ?? IntPtr.Zero;
            Thread.Sleep(5);
        }
    }

    private bool IsValidWindowHandle()
    {
        return _scrCpyHwnd != IntPtr.Zero && !_scrCpyProcess.HasExited;
    }

    private void RegisterWindowForKeyboardEvents()
    {
        KeyboardInterceptorWinForms.AddWindowHandle(_scrCpyHwnd);
        KeyboardInterceptorWinForms.KeyboardEvent += OnKeyboardEvent;
    }

    private void ActivateWindow()
    {
        LogMessage($"Aktywacja okna scrcpy (hwnd: 0x{_scrCpyHwnd.ToInt64():X})");

        // Przywróć i pokaż okno
        NativeWindowHelper.ShowWindow(_scrCpyHwnd, NativeWindowHelper.SW_RESTORE);
        NativeWindowHelper.ShowWindow(_scrCpyHwnd, NativeWindowHelper.SW_SHOW);

        // Próba ustawienia na pierwszym planie
        if (NativeWindowHelper.SetForegroundWindow(_scrCpyHwnd))
        {
            LogMessage("Okno scrcpy zostało aktywowane pomyślnie");
        }
        else
        {
            LogMessage("Nie udało się aktywować okna scrcpy");
            TryAlternativeWindowActivation();
        }
    }

    private void TryAlternativeWindowActivation()
    {
        if (NativeWindowHelper.ShowWindowAsync(_scrCpyHwnd, NativeWindowHelper.SW_SHOWNORMAL))
        {
            LogMessage("Alternatywna metoda aktywacji okna zakończona sukcesem");
            Thread.Sleep(100);
            NativeWindowHelper.SetForegroundWindow(_scrCpyHwnd);
        }
    }

    private void ChangeScrCpyWindowName(string nowyTytuł)
    {
        if (_scrCpyHwnd == IntPtr.Zero)
            return;

        LogMessage($"Zmiana tytułu okna na: {nowyTytuł}");
        NativeWindowHelper.SetWindowText(_scrCpyHwnd, nowyTytuł);
    }

    private void ConfigureWindow()
    {
        if (_isFullscreen)
            return;

        if (this.Device != null &&
            this.Device.DeviceWindow == null)
        {
            var deviceWithBounds = DeviceRepository
                .GetDevices()
                .OrderByDescending(x => x.LastUsage)
                .FirstOrDefault(x => x.DeviceWindow?.HasBounds() == true);

            this.Device.DeviceWindow = deviceWithBounds?.DeviceWindow;
        }

        if (this.Device?.DeviceWindow != null)
            WindowInfoUtils.SetWindowInfo(_scrCpyHwnd, new WindowInfo()
            {
                State = this.Device.DeviceWindow.WindowState,
                Bounds = new Rectangle(
                    (int)this.Device.DeviceWindow.X,
                    (int)this.Device.DeviceWindow.Y,
                    (int)this.Device.DeviceWindow.Width,
                    (int)this.Device.DeviceWindow.Height)
            });
    }

    private void WaitForProcessExit(Action action)
    {
        while (true)
        {
            Thread.Sleep(10);

            if (_scrCpyProcess == null ||
                _scrCpyProcess.HasExited ||
                !IsRunning)
            {
                LogMessage("Proces scrcpy zakończył działanie");
                //AudioPause(_scrCpyProcess);
                break;
            }

            action();
        }
    }

    private void UpdateWindowInfo(WindowInfo? windowInfo)
    {
        if (_isFullscreen || !IsRunning || Device?.IsRunning != true || _scrCpyProcess?.HasExited != false)
            return;

        if (windowInfo?.State != FormWindowState.Normal)
            return;

        int x = windowInfo.Bounds.X;
        int y = windowInfo.Bounds.Y;
        int w = windowInfo.Bounds.Width;
        int h = windowInfo.Bounds.Height;

        bool boundsChanged = x != _lastSavedWindowX || y != _lastSavedWindowY ||
                             w != _lastSavedWindowWidth || h != _lastSavedWindowHeight;

        if (boundsChanged)
        {
            _lastSavedWindowX = x;
            _lastSavedWindowY = y;
            _lastSavedWindowWidth = w;
            _lastSavedWindowHeight = h;
            _lastWindowStableTime = DateTime.UtcNow;

            if (this.Device.DeviceWindow == null)
                this.Device.DeviceWindow = new DeviceWindow();

            this.Device.DeviceWindow.X = x;
            this.Device.DeviceWindow.Y = y;
            this.Device.DeviceWindow.Width = w;
            this.Device.DeviceWindow.Height = h;
            this.Device.DeviceWindow.WindowState = windowInfo.State;
            return;
        }

        // Zapisz na dysk dopiero gdy okno jest stabilne przez WindowSaveDelay
        if (_lastWindowStableTime == DateTime.MinValue) return;
        if (DateTime.UtcNow - _lastWindowStableTime < WindowSaveDelay) return;

        _lastWindowStableTime = DateTime.MinValue;
        LogMessage($"Zapis pozycji okna: {w}x{h} @ {x},{y}");
        DeviceRepository.UpdateRunData(this.Device);
    }

    private void CheckOrientation(WindowInfo? windowInfo)
    {
        if (!_isAspectRatioUnlocked) return;
        if (_scrCpyHwnd == IntPtr.Zero) return;
        if (_scrCpyProcess?.HasExited != false) return;

        try
        {
            if (windowInfo == null || windowInfo.State != FormWindowState.Normal) return;

            int w = (int)windowInfo.Width;
            int h = (int)windowInfo.Height;

            if (w != _lastWindowWidth || h != _lastWindowHeight)
            {
                _lastWindowWidth = w;
                _lastWindowHeight = h;
                _lastWindowSizeChangeTime = DateTime.UtcNow;
                return;
            }

            if (_lastWindowSizeChangeTime == DateTime.MinValue) return;
            if (DateTime.UtcNow - _lastWindowSizeChangeTime < DebounceDelay) return;

            _lastWindowSizeChangeTime = DateTime.MinValue;
            LogMessage($"Debounce wyzwolony: klasyfikuję orientację {w}x{h}");
            UpdateDeviceOrientation(w, h);
        }
        catch (Exception ex)
        {
            LogMessage($"Błąd monitorowania orientacji: {ex.Message}");
        }
    }

    private void UpdateDeviceOrientation(int windowWidth, int windowHeight)
    {
        var orientation = ClassifyWindowOrientation(windowWidth, windowHeight);
        if (orientation == null) return;
        if (_scrCpyLastOrientation == orientation.Value) return;

        LogMessage($"Zmiana orientacji: {_scrCpyLastOrientation} → {orientation.Value} ({windowWidth}x{windowHeight})");
        _scrCpyLastOrientation = orientation.Value;
        SetDeviceOrientation(orientation.Value, executePermissions: true);
    }

    private Orientation? ClassifyWindowOrientation(double width, double height)
    {
        double larger = Math.Max(width, height);
        double smaller = Math.Min(width, height);
        if ((larger - smaller) / larger < OrientationThresholdRatio) return null;
        return width > height ? Orientation.Horizontal : Orientation.Vertical;
    }

    private void SetDeviceOrientation(Orientation orientation, bool executePermissions)
    {
        if (orientation == Orientation.Horizontal)
            ScrCpy.SetScreenOrientationHorizontal(Device.Name, executePermissions);
        else
            ScrCpy.SetScreenOrientationVertical(Device.Name, executePermissions);
    }

    private void DisposeProcess(bool stopRecording = true)
    {
        var (scrCpyHwnd, scrCpyProcess) = RemoveReferencesToProcess();
        if (stopRecording)
            StopRecording();
        AudioPause(scrCpyProcess);
        UnregisterWindowFromKeyboardEvents(scrCpyHwnd);
        CloseScrcpyProcess(scrCpyProcess, stopRecording);
    }

    private (IntPtr scrCpyHwnd, Process? scrCpyProcess) RemoveReferencesToProcess()
    {
        var scrCpyHwnd = _scrCpyHwnd;
        var scrCpyProcess = _scrCpyProcess;
        _scrCpyLastOrientation = null;
        _lastWindowSizeChangeTime = DateTime.MinValue;
        _lastWindowStableTime = DateTime.MinValue;
        _lastWindowWidth = 0;
        _lastWindowHeight = 0;
        _scrCpyHwnd = IntPtr.Zero;
        _scrCpyProcess = null;
        return (scrCpyHwnd, scrCpyProcess);
    }

    private void AudioPause(Process? scrCpyProcess)
    {
        if (scrCpyProcess == null)
            return;

        var isMediaPlaying = ScrCpy.IsMediaPlay(this.Device?.Name);
        Device?.UpdateMediaPlaying(isMediaPlaying);
        DeviceRepository.UpdateRunData(this.Device);

        if (isMediaPlaying)
            ScrCpy.MediaPause(this.Device?.Name);
    }

    private void AudioResume()
    {
        if (Device.DeviceMedia?.WasMediaPlaying == true &&
            Device.DeviceMedia?.GetTimeSinceMediaPaused() < TimeSpan.FromMinutes(5))
        {
            ScrCpy.MediaPlay(this.Device?.Name);
            Device.ResetMedia();
            DeviceRepository.UpdateRunData(Device);
        }
    }

    private void UnregisterWindowFromKeyboardEvents(IntPtr scrCpyHwnd)
    {
        if (scrCpyHwnd != IntPtr.Zero)
        {
            KeyboardInterceptorWinForms.RemoveWindowHandle(scrCpyHwnd);
            KeyboardInterceptorWinForms.KeyboardEvent -= OnKeyboardEvent;
        }
    }

    private void CloseScrcpyProcess(Process? _scrCpyProcess, Boolean waitForExit = true)
    {
        if (_scrCpyProcess == null)
            return;

        LogMessage("Zamykanie procesu scrcpy");

        try
        {
            CloseMainWindow(_scrCpyProcess);
            if (waitForExit)
                EnsureProcessTerminated(_scrCpyProcess);
            else
                WaitProcessTerminated(_scrCpyProcess);
        }
        catch (Exception ex)
        {
            LogMessage($"Wyjątek podczas zamykania procesu: {ex.Message}");
        }
        finally
        {
            _scrCpyProcess?.Dispose();
            _scrCpyProcess = null;
        }
    }

    private void CloseMainWindow(Process _scrCpyProcess)
    {
        _scrCpyProcess.CloseMainWindow();
    }

    private void EnsureProcessTerminated(Process _scrCpyProcess)
    {
        if (!_scrCpyProcess.HasExited)
        {
            _scrCpyProcess.Kill(true);
            _scrCpyProcess.WaitForExit2(200);
        }
    }
    private void WaitProcessTerminated(Process _scrCpyProcess)
    {
        if (!_scrCpyProcess.HasExited)
        {
            _scrCpyProcess.WaitForExit2(99999);
        }
    }

    private void LogMessage(string message)
    {
        FileLogger.Log($"ScrCpyRunner [{Device?.Name}]: {message}");
    }

    public void RestartProcess()
    {
        DisposeProcess();
    }

    public void Dispose()
    {
        DisposeProcess();
        Stop();
        UnregisterEvents();
    }


    private void UnregisterEvents()
    {
        KeyboardInterceptorWinForms.KeyboardEvent -= OnKeyboardEvent;
    }
}
