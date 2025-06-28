
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dexa.Views;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa.ViewModels
{
    public partial class TrayWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private ObservableCollection<Device> _devices;

        [ObservableProperty]
        private Device _selectedDevice;

        public TrayWindowViewModel()
        {
            Devices = new ObservableCollection<Device>(DeviceRepository.GetDevices());
        }

        [RelayCommand]
        private void ShowHelp()
        {
            var helpWindow = new HelpWindow
            {
                DataContext = new HelpWindowViewModel()
            };
            helpWindow.Show();
        }

        [RelayCommand]
        private void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }

        [RelayCommand]
        partial void OnSelectedDeviceChanged(Device value)
        {
            if (value != null)
            {
                ScrCpyRunners.TurnOn(value);
                System.Windows.Application.Current.Windows[0].Hide(); // Close the tray window
            }
        }

        [RelayCommand]
        private void ConnectWireless(Device device)
        {
            if (device != null && !string.IsNullOrEmpty(device.Name) && !string.IsNullOrEmpty(device.IpAddress))
            {
                ScrCpy.ConnectWireless(device.Name);
                ScrCpy.ConnectWirelessTo(device.Name, device.IpAddress);
            }
        }

        [RelayCommand]
        private void DisconnectWireless(Device device)
        {
            if (device != null && !string.IsNullOrEmpty(device.Name))
            {
                ScrCpy.DisconnectWireless(device.Name);
                DeviceRepository.Remove(device.Name);
            }
        }
    }
}
