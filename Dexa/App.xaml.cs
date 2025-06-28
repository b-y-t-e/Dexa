using System.Windows;
using System.Windows.Media.Imaging;
using Dexa.ViewModels;
using Dexa.Views;
using H.NotifyIcon;
using System.Runtime.InteropServices;
using System.Drawing;

namespace Dexa
{
    public partial class App : Application
    {
        private TrayWindow? _trayWindow;
        private TaskbarIcon? _trayIcon;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _trayWindow = new TrayWindow
            {
                DataContext = new TrayWindowViewModel()
            };

            _trayIcon = (TaskbarIcon)FindResource("TrayIcon");
            if (_trayIcon != null)
            {
                try
                {
                    var iconUri = new System.Uri("pack://application:,,,/icon.ico");
                    _trayIcon.IconSource = new BitmapImage(iconUri);
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading icon: {ex.Message}");
                    _trayIcon.Icon = SystemIcons.Application;
                }
                _trayIcon.ForceCreate(true);
                _trayIcon.TrayLeftMouseDown += (sender, args) => ShowTrayWindow();
            }
        }

        private void ShowTrayWindow()
        {
            if (_trayWindow == null) return;

            // Get cursor position using P/Invoke
            POINT lpPoint;
            GetCursorPos(out lpPoint);

            _trayWindow.Left = lpPoint.X - _trayWindow.Width / 2;
            _trayWindow.Top = lpPoint.Y - _trayWindow.Height;
            _trayWindow.Show();
            _trayWindow.Activate();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }
    }
}
