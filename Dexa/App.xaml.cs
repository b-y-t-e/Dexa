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
using Velopack;
using MessageBox = System.Windows.MessageBox;

namespace Dexa
{
    public partial class App : System.Windows.Application
    {
        [STAThread]
        private static void Main(string[] args)
        {
            try
            {
                VelopackApp.Build().Run();

                var app = new App();
                app.InitializeComponent();
                app.Run();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Unhandled exception: " + ex.ToString());
            }
        }

        private const string AppName = "Dexa";
        private Mutex _mutex;

        private TrayWindow? _trayWindow;
        private TaskbarIcon? _trayIcon;

        private volatile bool _isAppClosed;

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

            ThreadPool.QueueUserWorkItem(DeviceWatcherThread);

            Task.Run(UpdateMyApp);
        }

        private static void UpdateMyApp()
        {
            try
            {
                var mgr = new UpdateManager("https://else.net.pl/dexa/");
                var newVersion = mgr.CheckForUpdates();
                if (newVersion == null)
                    return;

                mgr.DownloadUpdates(newVersion);
                mgr.ApplyUpdatesAndRestart(newVersion);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Update check failed: {ex.Message}");
            }
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
            var currentDevices = ScrCpy.GetAllDevices().ToList();
            AddDevicesInRepository(currentDevices);
        }

        private void AddDevicesInRepository(List<AdbDevice> currentDevices)
        {
            var devicesFromRepo = DeviceRepository.GetDevices();

            foreach (var adbDevice in currentDevices)
            {
                var deviceFromRepo = devicesFromRepo.FirstOrDefault(d => d.Name == adbDevice.Name);
                var isPingable = CheckIsDevicePingable(adbDevice.IpAddress);

                if (deviceFromRepo != null)
                {
                    if (isPingable) deviceFromRepo.MakeAvailable();
                    else deviceFromRepo.MakeNotAvailable();
                    deviceFromRepo.Update(adbDevice);
                    DeviceRepository.Update(deviceFromRepo);
                }
                else
                {
                    var newDevice = Device.Create(adbDevice);
                    if (isPingable) newDevice.MakeAvailable();
                    else newDevice.MakeNotAvailable();
                    DeviceRepository.Add(newDevice);
                }
            }

            foreach (var deviceFromRepo in devicesFromRepo)
            {
                if (currentDevices.Any(d => d.Name == deviceFromRepo.Name))
                    continue;

                var isPingable = deviceFromRepo.IsRemoteConnection &&
                                 CheckIsDevicePingable(deviceFromRepo.IpAddress);
                if (isPingable)
                    deviceFromRepo.MakeAvailable();
                else
                    deviceFromRepo.MakeNotAvailable();

                DeviceRepository.Update(deviceFromRepo);
            }

            DeviceRepository.SaveToFile();
        }

        private static bool CheckIsDevicePingable(string ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
                return false;

            try
            {
                using var ping = new Ping();
                var reply = ping.Send(ipAddress, 1000);
                return reply?.Status == IPStatus.Success;
            }
            catch
            {
                return false;
            }
        }

        private void ShowTrayWindow()
        {
            if (_trayWindow == null) return;

            _trayWindow.Opacity = 0;
            _trayWindow.Show();
            _trayWindow.UpdateLayout();

            System.Drawing.Point cursorPosition =
                System.Windows.Forms.Cursor.Position + new System.Drawing.Size(15, -20);

            PresentationSource presentationSource = PresentationSource.FromVisual(_trayWindow);
            if (presentationSource?.CompositionTarget != null)
            {
                Matrix transform = presentationSource.CompositionTarget.TransformFromDevice;
                System.Windows.Point transformed =
                    transform.Transform(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));

                _trayWindow.Left = transformed.X - _trayWindow.ActualWidth;
                _trayWindow.Top = transformed.Y - _trayWindow.ActualHeight;
            }
            else
            {
                _trayWindow.Left = cursorPosition.X - _trayWindow.ActualWidth;
                _trayWindow.Top = cursorPosition.Y - _trayWindow.ActualHeight;
            }

            _trayWindow.Opacity = 1;
            _trayWindow.Activate();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isAppClosed = true;
            _trayIcon?.Dispose();
            ScrCpyRunners.TurnOff();

            KeyboardInterceptorWinForms.Stop();

            try { _mutex?.ReleaseMutex(); } catch { }

            base.OnExit(e);
        }
    }
}
