using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using GalaSoft.MvvmLight.Command;
using Dexa.Views;
using Dexa.Helpers;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa.ViewModels
{
    public class TrayWindowViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<Device> _devices;

        public ObservableCollection<Device> Devices
        {
            get => _devices;
            set
            {
                SetProperty(ref _devices, value);
                IsDeviceListEmpty = value.Count == 0;
            }
        }

        private Device? _selectedDevice;

        public Device? SelectedDevice
        {
            get => _selectedDevice;
            set => SetProperty(ref _selectedDevice, value);
        }

        private bool _isDeviceListEmpty;

        public bool IsDeviceListEmpty
        {
            get => _isDeviceListEmpty;
            set => SetProperty(ref _isDeviceListEmpty, value);
        }

        private AppSettings _settings;

        private bool _isKeyboardEnabled;
        public bool IsKeyboardEnabled
        {
            get => _isKeyboardEnabled;
            set
            {
                SetProperty(ref _isKeyboardEnabled, value);
                _settings.IsKeyboardEnabled = value;
                _settings.Save();
            }
        }

        private bool _isScreenOffEnabled;
        public bool IsScreenOffEnabled
        {
            get => _isScreenOffEnabled;
            set
            {
                SetProperty(ref _isScreenOffEnabled, value);
                _settings.IsScreenOffEnabled = value;
                _settings.Save();
            }
        }

        public TrayWindowViewModel()
        {
            // Load settings
            _settings = AppSettings.Load();
            _isKeyboardEnabled = _settings.IsKeyboardEnabled;
            _isScreenOffEnabled = _settings.IsScreenOffEnabled;

            _devices = new ObservableCollection<Device>(DeviceRepository.GetDevices());
            _devices.CollectionChanged += Devices_CollectionChanged;
            IsDeviceListEmpty = _devices.Count == 0;

            RefreshDevicesCommand = new RelayCommand(RefreshDevices);
            // ShowHelpCommand = new RelayCommand(ShowHelp);
            ExitApplicationCommand = new RelayCommand(ExitApplication);
            TurnOnDeviceCommand = new RelayCommand<Device>(TurnOnDevice);
            ConnectWirelessCommand = new RelayCommand<Device>(ConnectWireless);
            DisconnectWirelessCommand = new RelayCommand<Device>(DisconnectWireless);
            ToggleKeyboardCommand = new RelayCommand(ToggleKeyboard);
            ToggleScreenCommand = new RelayCommand(ToggleScreen);
        }

        private void Devices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            IsDeviceListEmpty = Devices.Count == 0;
        }

        private void RefreshDevices()
        {
            Devices = new ObservableCollection<Device>(DeviceRepository.GetDevices());
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

        public RelayCommand RefreshDevicesCommand { get; private set; }
        public RelayCommand ToggleKeyboardCommand { get; private set; }
        public RelayCommand ToggleScreenCommand { get; private set; }

        // public RelayCommand ShowHelpCommand { get; private set; }

        // private void ShowHelp()
        // {
        //     var helpWindow = new HelpWindow
        //     {
        //         DataContext = new HelpWindowViewModel()
        //     };
        //     helpWindow.Show();
        // }

        public RelayCommand ExitApplicationCommand { get; private set; }

        private void ExitApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void ToggleKeyboard()
        {
            IsKeyboardEnabled = !IsKeyboardEnabled;
        }

        private void ToggleScreen()
        {
            IsScreenOffEnabled = !IsScreenOffEnabled;
        }

        public RelayCommand<Device> TurnOnDeviceCommand { get; private set; }

        private void TurnOnDevice(Device device)
        {
            if (device != null)
            {
                if (device.IsRemoteConnection)
                {
                    ScrCpy.ConnectWirelessTo(device.Name, device.IpAddress);
                }

                ScrCpyRunners.TurnOn(device);
                System.Windows.Application.Current.Windows[0].Hide(); // Close the tray window
            }
        }

        public RelayCommand<Device> ConnectWirelessCommand { get; private set; }

        private void ConnectWireless(Device device)
        {
            System.Windows.Application.Current.Windows[0].Hide(); // Close the tray window
            if (device != null && !string.IsNullOrEmpty(device.Name) && !string.IsNullOrEmpty(device.IpAddress))
            {
                ScrCpy.ConnectWireless(device.Name);
                ScrCpy.ConnectWirelessTo(device.Name, device.IpAddress);
            }
        }

        public RelayCommand<Device> DisconnectWirelessCommand { get; private set; }

        private void DisconnectWireless(Device device)
        {
            System.Windows.Application.Current.Windows[0].Hide(); // Close the tray window
            if (device != null && !string.IsNullOrEmpty(device.Name))
            {
                ScrCpy.DisconnectWireless(device.Name);
                DeviceRepository.Remove(device.Name);

                foreach (var x in Devices.ToArray())
                    Devices.Remove(x);
                foreach (var x in DeviceRepository.GetDevices())
                    Devices.Add(x);
            }
        }
    }
}
