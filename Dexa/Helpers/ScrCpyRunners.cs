using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public static class ScrCpyRunners
{
    static List<ScrCpyRunner> _deviceRunners = new List<ScrCpyRunner>();

    public static int ActiveCount
    {
        get => _deviceRunners.Count;
    }

    public static void Apply(List<Device> devices)
    {
        _deviceRunners.RemoveAll(runner =>
        {
            var shouldRemove = !runner.IsRunning ||
                               !devices.Any(d => d.IsRunning && d.Name == runner.Device.Name);
            if (shouldRemove)
                runner.Dispose();
            return shouldRemove;
        });

        foreach (var device in devices)
        {
            if (!device.IsRunning)
                continue;

            if (!_deviceRunners.Any(r => r.Device.Name == device.Name))
                _deviceRunners.Add(new ScrCpyRunner(device));
        }
    }

    public static void TurnOff()
    {
        foreach (var deviceRunner in _deviceRunners)
            deviceRunner.Dispose();
        _deviceRunners.Clear();
    }

    public static void TurnOn(Device device)
    {
        var thisDevice = _deviceRunners
            .FirstOrDefault(r => r.Device.Name == device.Name);
        if (thisDevice != null)
            return;

        device.Enable();
        DeviceRepository.UpdateRunData(device);
        _deviceRunners.Add(new ScrCpyRunner(device));
    }
}
