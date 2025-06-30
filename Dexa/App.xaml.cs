using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using Dexa.ViewModels;
using Dexa.Views;
using H.NotifyIcon;
using System.Windows.Forms;
using System.Windows.Media;
using System.Drawing;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;
using Size = System.Windows.Size;
using System.Net.NetworkInformation;

namespace Dexa
{
    public partial class App : System.Windows.Application
    {
        private const string AppName = "Dexa";
        private Mutex _mutex;

        private TrayWindow? _trayWindow;
        private TaskbarIcon? _trayIcon;
        private bool _isAppClosed;

        protected override void OnStartup(StartupEventArgs e)
        {
            _mutex = new Mutex(true, AppName, out var createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show("An instance of the application is already running.",
                    "Application already running", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            _trayWindow = new TrayWindow
            {
                DataContext = new TrayWindowViewModel(),
                WindowStartupLocation = WindowStartupLocation.Manual
            };

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            if (_trayIcon != null)
            {
                try
                {
                    var iconUri = new System.Uri("pack://application:,,,/Icons/Dexa.ico");
                    _trayIcon.IconSource = new BitmapImage(iconUri);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading icon: {ex.Message}");
                    _trayIcon.Icon = SystemIcons.Application;
                }

                _trayIcon.ForceCreate(true);
                _trayIcon.TrayLeftMouseDown += (sender, args) => ShowTrayWindow();
                _trayIcon.TrayRightMouseDown += (sender, args) => ShowTrayWindow();
            }

            KeyboardInterceptorWinForms.InitializeHook();
            ThreadPool.QueueUserWorkItem(DeviceWatcherThread);
        }

        private void DeviceWatcherThread(object? _)
        {
            while (!_isAppClosed)
            {
                try
                {
                    UpdateNewDevicesInRepository();

                    if (_isAppClosed)
                        return;

                    var devices = DeviceRepository.GetDevices();

                    ScrCpyRunners.Apply(devices);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
                finally
                {
                    if (ScrCpyRunners.ActiveCount > 0)
                        Thread.Sleep(1500);
                    else
                        Thread.Sleep(500);
                }
            }
        }

        private void UpdateNewDevicesInRepository()
        {
            var currentDevices = ScrCpy
                .GetAllDevices()
                // .Where(x => !x.IsEmulator)
                .ToList();

            AddDevicesInRepository(currentDevices);
        }

        /// <summary>
        /// Aktualizuje repozytorium o nowe urządzenia lub aktualizuje istniejące
        /// </summary>
        /// <param name="currentDevices">Lista aktualnie wykrytych urządzeń</param>
        private void AddDevicesInRepository(List<AdbDevice> currentDevices)
        {
            // Pobierz zapisane urządzenia
            var devicesFromRepo = DeviceRepository.GetDevices();

            foreach (var adbDevice in currentDevices)
            {
                // Sprawdź czy urządzenie jest już w repozytorium
                var deviceFromRepo = devicesFromRepo
                    .FirstOrDefault(d => d.Name == adbDevice.Name);

                if (deviceFromRepo != null)
                {
                    deviceFromRepo.MakeAvailable();
                    deviceFromRepo.Update(adbDevice);
                    DeviceRepository.Update(deviceFromRepo);
                }
                else
                {
                    var newDevice = Device.Create(adbDevice);
                    newDevice.MakeAvailable();
                    DeviceRepository.Add(newDevice);
                }
            }

            foreach (var deviceFromRepo in devicesFromRepo)
            {
                var currentDevice = currentDevices
                    .FirstOrDefault(d => d.Name == deviceFromRepo.Name);

                if (currentDevice != null)
                    continue;

                var isPingable = deviceFromRepo.IsRemoteConnection &&
                                 CheckIsDevicePingable(deviceFromRepo);
                if (isPingable)
                {
                    deviceFromRepo.MakeAvailable();
                    DeviceRepository.Update(deviceFromRepo);
                }
                else
                {
                    deviceFromRepo.MakeNotAvailable();
                    DeviceRepository.Update(deviceFromRepo);
                }
            }

            // Zapisz zmiany
            DeviceRepository.SaveToFile();
        }

        private static bool CheckIsDevicePingable(Device deviceFromRepo)
        {
            bool isPingable = false;
            using (var ping = new Ping())
            {
                try
                {
                    var reply = ping.Send(deviceFromRepo.IpAddress, 1000);
                    isPingable = reply?.Status == IPStatus.Success;
                }
                catch
                {
                    isPingable = false;
                }
            }

            return isPingable;
        }

        private void ShowTrayWindow()
        {
            if (_trayWindow == null) return;

            // Make the window temporarily invisible to prevent flash
            _trayWindow.Opacity = 0;
            _trayWindow.Show();

            // Force layout update to get ActualWidth and ActualHeight
            _trayWindow.UpdateLayout();

            // Get cursor position using System.Windows.Forms.Cursor.Position
            System.Drawing.Point cursorPosition =
                System.Windows.Forms.Cursor.Position +
                new System.Drawing.Size(15, -20);

            // Convert screen coordinates to device-independent units
            PresentationSource presentationSource = PresentationSource.FromVisual(_trayWindow);
            if (presentationSource != null && presentationSource.CompositionTarget != null)
            {
                Matrix transform = presentationSource.CompositionTarget.TransformFromDevice;
                System.Windows.Point transformedCursorPosition =
                    transform.Transform(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));

                _trayWindow.Left = transformedCursorPosition.X - _trayWindow.ActualWidth;
                _trayWindow.Top = transformedCursorPosition.Y - _trayWindow.ActualHeight;
            }
            else
            {
                // Fallback if PresentationSource is not available
                _trayWindow.Left = cursorPosition.X - _trayWindow.ActualWidth;
                _trayWindow.Top = cursorPosition.Y - _trayWindow.ActualHeight;
            }

            // Make the window fully visible
            _trayWindow.Opacity = 1;
            _trayWindow.Activate();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isAppClosed = true;
            _trayIcon?.Dispose();
            ScrCpyRunners.TurnOff();
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
