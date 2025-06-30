namespace Else.PhoneMirror.ViewModels;

public class AdbDevice
{
    public static AdbDevice Create(string deviceName, string hardwareName, string ipAddress, bool isRemoteConnection, bool isEmulator,
        bool isNetworkVisible, string friendlyName, bool canBeRemoteConnected)
    {
        return new AdbDevice
        {
            Name = deviceName,
            HardwareName = hardwareName,
            IpAddress = ipAddress,
            IsRemoteConnection = isRemoteConnection,
            IsEmulator = isEmulator,
            IsNetworkVisible = isNetworkVisible,
            FriendlyName = friendlyName,
            CanBeRemoteConnected = canBeRemoteConnected
        };
    }

    public string Name { get; set; }

    public string HardwareName { get; set; }

    public string IpAddress { get; set; }

    public bool IsRemoteConnection { get; set; }

    public bool IsEmulator { get; set; }

    public bool IsNetworkVisible { get; set; }

    public string FriendlyName { get; set; }

    public bool CanBeRemoteConnected { get; set; }
}
