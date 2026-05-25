using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public static class ScrCpyRunners
{
    static private readonly object _lock = new object();
    static private readonly List<ScrCpyRunner> _deviceRunners = new List<ScrCpyRunner>();
    static private volatile bool _isShuttingDown;

    public static int ActiveCount
    {
        get { lock (_lock) return _deviceRunners.Count; }
    }

    public static void Apply(List<Device> devices)
    {
        List<ScrCpyRunner> toDispose;
        List<Device> toAdd;
        lock (_lock)
        {
            toDispose = _deviceRunners
                .Where(runner => !runner.IsRunning || !devices.Any(d => d.IsRunning && d.Name == runner.Device.Name))
                .ToList();

            foreach (var r in toDispose)
                _deviceRunners.Remove(r);

            toAdd = devices
                .Where(d => d.IsRunning && !_deviceRunners.Any(r => r.Device.Name == d.Name))
                .ToList();
        }

        var newRunners = toAdd.Select(d => new ScrCpyRunner(d)).ToList();
        lock (_lock)
        {
            if (_isShuttingDown)
            {
                foreach (var runner in newRunners)
                    runner.Dispose();
                return;
            }
            foreach (var runner in newRunners)
            {
                if (!_deviceRunners.Any(r => r.Device.Name == runner.Device.Name))
                    _deviceRunners.Add(runner);
                else
                    runner.Dispose();
            }
        }

        foreach (var r in toDispose)
            r.Dispose();
    }

    public static void TurnOff()
    {
        List<ScrCpyRunner> runners;
        lock (_lock)
        {
            _isShuttingDown = true;
            runners = _deviceRunners.ToList();
            _deviceRunners.Clear();
        }

        foreach (var r in runners)
            r.Dispose();
    }

    public static void RestartAll()
    {
        List<ScrCpyRunner> runners;
        lock (_lock)
            runners = _deviceRunners.ToList();

        foreach (var r in runners)
            r.RestartProcess();
    }

    public static void TurnOn(Device device)
    {
        lock (_lock)
        {
            if (_deviceRunners.Any(r => r.Device.Name == device.Name))
                return;

            device.Enable();
            DeviceRepository.UpdateRunData(device);
            _deviceRunners.Add(new ScrCpyRunner(device));
        }
    }

    public static ScrCpyRunner? GetRunner(Device device)
    {
        lock (_lock)
            return _deviceRunners.FirstOrDefault(r => r.Device.Name == device.Name);
    }

    public static void ToggleFullscreen(Device d)    => GetRunner(d)?.ToggleFullscreen();
    public static void ToggleGameMode(Device d)      => GetRunner(d)?.ToggleGameMode();
    public static void ToggleRecording(Device d)     => GetRunner(d)?.ToggleRecording();
    public static void ToggleOrientation(Device d)   => GetRunner(d)?.ToggleOrientation();
    public static void ShowDesktop(Device d)         => GetRunner(d)?.ShowDesktop();
    public static void ToggleAlwaysOnTop(Device d)   => GetRunner(d)?.ToggleAlwaysOnTop();
}
