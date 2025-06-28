using System.Windows;
using System.Windows.Media.Imaging;
using Dexa.ViewModels;
using Dexa.Views;
using H.NotifyIcon;
using System.Windows.Forms;
using System.Windows.Media;
using System.Drawing;

namespace Dexa
{
    public partial class App : System.Windows.Application
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

            _trayWindow.Show();

            // Get cursor position using System.Windows.Forms.Cursor.Position
            System.Drawing.Point cursorPosition = System.Windows.Forms.Cursor.Position;

            // Convert screen coordinates to device-independent units
            PresentationSource presentationSource = PresentationSource.FromVisual(_trayWindow);
            if (presentationSource != null && presentationSource.CompositionTarget != null)
            {
                Matrix transform = presentationSource.CompositionTarget.TransformFromDevice;
                System.Windows.Point transformedCursorPosition = transform.Transform(new System.Windows.Point(cursorPosition.X, cursorPosition.Y));

                _trayWindow.Left = transformedCursorPosition.X - _trayWindow.ActualWidth / 2;
                _trayWindow.Top = transformedCursorPosition.Y - _trayWindow.ActualHeight;
            }
            else
            {
                // Fallback if PresentationSource is not available
                _trayWindow.Left = cursorPosition.X - _trayWindow.ActualWidth / 2;
                _trayWindow.Top = cursorPosition.Y - _trayWindow.ActualHeight;
            }

            _trayWindow.Activate();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _trayIcon?.Dispose();
            base.OnExit(e);
        }
    }
}
