using System;

namespace Else.PhoneMirror.ViewModels;

public class DeviceWindow
{
    public FormWindowState WindowState { get; set; }
    public Double X { get; set; }
    public Double Y { get; set; }
    public Double Width { get; set; }
    public Double Height { get; set; }
}

public class DeviceMedia
{
    public Boolean WasMediaPlaying { get; set; }
    public DateTime? MediaPaused { get; set; }

    public TimeSpan? GetTimeSinceMediaPaused()
    {
        if (MediaPaused == null)
            return null;
        return DateTime.UtcNow - MediaPaused.Value;
    }
}

public class Device
{
    public string FriendlyName { get; set; }
    public string Name { get; set; }
    public string HardwareName { get; set; }
    public string? IpAddress { get; set; }
    public bool IsRemoteConnection { get; set; }
    public bool IsEmulator { get; set; }
    public bool IsNetworkVisible { get; set; }

    public DeviceWindow? DeviceWindow { get; set; }
    public DeviceMedia? DeviceMedia { get; set; }
    public bool IsRunning { get; set; }

    public static Device Create(
        string name,
        string hardwareName,
        bool isEnabled,
        string? ipAddress,
        bool isRemoteConnection,
        bool isEmulator, bool isNetworkVisible, string firendlyName)
    {
        return new Device
        {
            IsRunning = isEnabled,
            Name = name,
            HardwareName = hardwareName,
            FriendlyName = firendlyName,
            IpAddress = ipAddress,
            IsRemoteConnection = isRemoteConnection,
            IsEmulator = isEmulator,
            IsNetworkVisible = isNetworkVisible
        };
    }

    public Device Update(Device updatedDevice)
    {
        if (updatedDevice == null)
            throw new ArgumentNullException(nameof(updatedDevice));

        if (updatedDevice.Name != Name)
            throw new ArgumentException("Device name cannot be changed");

        FriendlyName = updatedDevice.FriendlyName ?? FriendlyName;
        IpAddress = updatedDevice.IpAddress ?? IpAddress;
        IsRemoteConnection = updatedDevice.IsRemoteConnection;
        IsEmulator = updatedDevice.IsEmulator;
        IsNetworkVisible = updatedDevice.IsNetworkVisible;
        HardwareName = updatedDevice.HardwareName ?? HardwareName;

        return this;
    }

    public void UpdateMediaPlaying(bool isMediaPlaying)
    {
        if (DeviceMedia == null)
            DeviceMedia = new DeviceMedia();

        DeviceMedia.WasMediaPlaying = isMediaPlaying;
        DeviceMedia.MediaPaused = isMediaPlaying ? DateTime.UtcNow : null;
    }

    public void Enable()
    {
        this.IsRunning = true;
    }

    public void ResetMedia()
    {
        this.DeviceMedia = new DeviceMedia();
    }
}
