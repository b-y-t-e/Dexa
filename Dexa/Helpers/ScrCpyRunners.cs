using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public static class ScrCpyRunners
{
    // K1: lock for thread-safe access from DeviceWatcherThread and UI thread
    static private readonly object _lock = new object();
    static private readonly List<ScrCpyRunner> _deviceRunners = new List<ScrCpyRunner>();

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

        foreach (var device in toAdd)
        {
            var runner = new ScrCpyRunner(device);
            lock (_lock)
                _deviceRunners.Add(runner);
        }

        foreach (var r in toDispose)
            r.Dispose();
    }

    public static void TurnOff()
    {
        List<ScrCpyRunner> runners;
        lock (_lock)
        {
            runners = _deviceRunners.ToList();
            _deviceRunners.Clear();
        }

        foreach (var r in runners)
            r.Dispose();
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
}
