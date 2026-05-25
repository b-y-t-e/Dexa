using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace Else.PhoneMirror.ViewModels;

public class Device : INotifyPropertyChanged
{
    private bool _IsAvailable;

    public bool IsAvailable
    {
        get => _IsAvailable;
        set => SetProperty(ref _IsAvailable, value);
    }


    private string _friendlyName;

    public string FriendlyName
    {
        get => _friendlyName;
        set => SetProperty(ref _friendlyName, value);
    }

    private DateTime _LastUsage;

    public DateTime LastUsage
    {
        get => _LastUsage;
        set => SetProperty(ref _LastUsage, value);
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

    private bool _isFullscreen;
    [JsonIgnore]
    public bool IsFullscreen
    {
        get => _isFullscreen;
        set => SetProperty(ref _isFullscreen, value);
    }

    private bool _isGameMode;
    [JsonIgnore]
    public bool IsGameMode
    {
        get => _isGameMode;
        set => SetProperty(ref _isGameMode, value);
    }

    private bool _isRecording;
    [JsonIgnore]
    public bool IsRecording
    {
        get => _isRecording;
        set => SetProperty(ref _isRecording, value);
    }

    private bool _isAlwaysOnTop;
    [JsonIgnore]
    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set => SetProperty(ref _isAlwaysOnTop, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        var handler = PropertyChanged;
        if (handler == null) return;
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
            dispatcher.BeginInvoke(() => handler(this, new PropertyChangedEventArgs(propertyName)));
        else
            handler(this, new PropertyChangedEventArgs(propertyName));
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

    public static Device Create(AdbDevice device)
    {
        return new Device()
        {
            Name = device.Name,
            HardwareName = device.HardwareName,
            IsAvailable = true,
            IsRunning = !device.IsEmulator,
            IsEmulator = device.IsEmulator,
            IsNetworkVisible = device.IsNetworkVisible,
            IsRemoteConnection = device.IsRemoteConnection,
            CanBeRemoteConnected = device.CanBeRemoteConnected,
            FriendlyName = device.FriendlyName,
            IpAddress = device.IpAddress,
            LastUsage = DateTime.UtcNow
        };
    }

    public Device Update(AdbDevice updatedDevice)
    {
        if (updatedDevice == null)
            throw new ArgumentNullException(nameof(updatedDevice));

        if (updatedDevice.Name != Name)
            throw new ArgumentException("Device name cannot be changed");

        if (!string.IsNullOrEmpty(updatedDevice.HardwareName))
            HardwareName = updatedDevice.HardwareName;

        if (!string.IsNullOrEmpty(updatedDevice.FriendlyName))
            FriendlyName = updatedDevice.FriendlyName;

        if (!string.IsNullOrEmpty(updatedDevice.IpAddress))
            IpAddress = updatedDevice.IpAddress;

        IsRemoteConnection = updatedDevice.IsRemoteConnection;
        CanBeRemoteConnected = updatedDevice.CanBeRemoteConnected;
        IsEmulator = updatedDevice.IsEmulator;
        IsNetworkVisible = updatedDevice.IsNetworkVisible;

        if (LastUsage.Year < 2000)
            LastUsage = DateTime.UtcNow.AddDays(-1);

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

    public void Disable()
    {
        this.IsRunning = false;
    }

    public void InformRunning()
    {
        this.LastUsage = DateTime.UtcNow;
    }

    public void ResetMedia()
    {
        this.DeviceMedia = new DeviceMedia();
    }

    public void MakeAvailable()
    {
        this.IsAvailable = true;
    }

    public void MakeNotAvailable()
    {
        this.IsAvailable = false;
    }
}
