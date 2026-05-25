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
using Dexa.Helpers;
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

            FileLogger.Init();
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
                    DeviceRepository.Update(deviceFromRepo, save: false);
                }
                else
                {
                    var newDevice = Device.Create(adbDevice);
                    if (isPingable) newDevice.MakeAvailable();
                    else newDevice.MakeNotAvailable();
                    DeviceRepository.Add(newDevice, save: false);
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

                DeviceRepository.Update(deviceFromRepo, save: false);
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
                var reply = ping.Send(ipAddress, 300);
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

            _trayWindow.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Render, () =>
                {
                    // Use the screen where the cursor is (the monitor with the tray icon),
                    // not SystemParameters.WorkArea which always returns the primary monitor.
                    var cursorPos = System.Windows.Forms.Cursor.Position;
                    var screen = System.Windows.Forms.Screen.FromPoint(cursorPos);
                    var workArea = screen.WorkingArea;

                    // Screen.WorkingArea is in physical pixels; convert to WPF DIPs.
                    var source = PresentationSource.FromVisual(_trayWindow);
                    double scaleX = 1.0, scaleY = 1.0;
                    if (source?.CompositionTarget != null)
                    {
                        scaleX = source.CompositionTarget.TransformFromDevice.M11;
                        scaleY = source.CompositionTarget.TransformFromDevice.M22;
                    }

                    _trayWindow.Left = workArea.Right * scaleX - _trayWindow.ActualWidth;
                    _trayWindow.Top = workArea.Bottom * scaleY - _trayWindow.ActualHeight;
                    _trayWindow.Opacity = 1;
                    _trayWindow.Activate();
                });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _isAppClosed = true;
            _trayIcon?.Dispose();
            ScrCpyRunners.TurnOff();

            KeyboardInterceptorWinForms.Stop();

            try { _mutex?.ReleaseMutex(); } catch { }
            FileLogger.Dispose();

            base.OnExit(e);
        }
    }
}
