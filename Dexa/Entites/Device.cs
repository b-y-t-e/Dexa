using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Else.PhoneMirror.ViewModels;

public class Device : INotifyPropertyChanged
{
    private string _friendlyName;
    public string FriendlyName
    {
        get => _friendlyName;
        set => SetProperty(ref _friendlyName, value);
    }

    private string _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    private string _hardwareName;
    public string HardwareName
    {
        get => _hardwareName;
        set => SetProperty(ref _hardwareName, value);
    }

    private string? _ipAddress;
    public string? IpAddress
    {
        get => _ipAddress;
        set => SetProperty(ref _ipAddress, value);
    }

    private bool _isRemoteConnection;
    public bool IsRemoteConnection
    {
        get => _isRemoteConnection;
        set => SetProperty(ref _isRemoteConnection, value);
    }

    private bool _isEmulator;
    public bool IsEmulator
    {
        get => _isEmulator;
        set => SetProperty(ref _isEmulator, value);
    }

    private bool _isNetworkVisible;
    public bool IsNetworkVisible
    {
        get => _isNetworkVisible;
        set => SetProperty(ref _isNetworkVisible, value);
    }

    private bool _canBeRemoteConnected;
    public bool CanBeRemoteConnected
    {
        get => _canBeRemoteConnected;
        set => SetProperty(ref _canBeRemoteConnected, value);
    }

    public DeviceWindow? DeviceWindow { get; set; }
    public DeviceMedia? DeviceMedia { get; set; }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    public static Device Create(
        string name,
        string hardwareName,
        bool isEnabled,
        string? ipAddress,
        bool isRemoteConnection,
        bool isEmulator, bool isNetworkVisible, string friendlyName,
        bool canBeRemoteConnected)
    {
        return new Device
        {
            IsRunning = isEnabled,
            Name = name,
            HardwareName = hardwareName,
            FriendlyName = friendlyName,
            IpAddress = ipAddress,
            IsRemoteConnection = isRemoteConnection,
            CanBeRemoteConnected = canBeRemoteConnected,
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

        FriendlyName = updatedDevice.FriendlyName;
        IpAddress = updatedDevice.IpAddress;
        IsRemoteConnection = updatedDevice.IsRemoteConnection;
        CanBeRemoteConnected = updatedDevice.CanBeRemoteConnected;
        IsEmulator = updatedDevice.IsEmulator;
        IsNetworkVisible = updatedDevice.IsNetworkVisible;
        HardwareName = updatedDevice.HardwareName;

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
