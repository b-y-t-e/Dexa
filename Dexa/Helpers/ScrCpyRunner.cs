using System.Diagnostics;
using System.IO;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public class ScrCpyRunner : IDisposable
{
    private Process? _scrCpyProcess;
    private IntPtr _scrCpyHwnd;
    private Orientation? _scrCpyLastOrientation;
    private bool _isFullscreen;
    private bool _isGameMode;
    private bool _isRecording;
    private string? _recordFile;

    public bool IsRunning { get; private set; }
    public Device Device { get; }

    public ScrCpyRunner(Device device)
    {
        Device = device;
        LogMessage("Podpięto obsługę zdarzeń klawiatury");
        Start();
    }

    private void OnKeyboardEvent(KeyboardInterceptorWinForms.KeyboardEventArgs e)
    {
        LogMessage($"Otrzymano zdarzenie klawisza {e.KeyCode} dla okna 0x{e.WindowHandle.ToInt64():X}");

        if (!IsKeypressForScrCpyWindow(e.WindowHandle) || !e.IsKeyDown)
            return;

        HandleKeypress(e.KeyCode);
    }

    private bool IsKeypressForScrCpyWindow(IntPtr windowHandle)
    {
        return _scrCpyHwnd != IntPtr.Zero && windowHandle == _scrCpyHwnd;
    }

    private void HandleKeypress(int keyCode)
    {
        switch (keyCode)
        {
            case KeyboardInterceptorWinForms.VirtualKeys.F10:
                ToggleDeviceOrientation();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F11:
                ToggleFullscreenMode();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F9:
                ToggleGameMode();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F5:
                ToggleRecording();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F1:
                ShowHelp();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.ESCAPE:
                CloseApplication();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.HOME:
                ShowDesktop();
                break;

            default:
                LogMessage($"Otrzymano nieobsługiwany klawisz o kodzie {keyCode}");
                break;
        }
    }

    private void ToggleDeviceOrientation()
    {
        LogMessage("Przełączanie orientacji ekranu");
        ScrCpy.ToggleScreenOrientation(Device.Name);
    }

    private void ToggleFullscreenMode()
    {
        LogMessage("Przełączanie trybu pełnoekranowego");
        _isFullscreen = !_isFullscreen;
        DisposeProcess();
    }

    private void ToggleGameMode()
    {
        LogMessage("Przełączanie trybu gamingowego");
        _isGameMode = !_isGameMode;
        DisposeProcess();
    }

    private void ToggleRecording()
    {
        if (_isRecording)
        {
            DisposeProcess();
        }
        else
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Title = "Zapisz plik",
                    FileName =
                        $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{string.Join("_", Device.FriendlyName.Split(Path.GetInvalidFileNameChars()))}.mp4",
                    DefaultExt = ".mp4",
                    Filter = "Pliki wideo|*.mp4;*.mkv|Wszystkie pliki|*.*"
                };

                var result = saveFileDialog.ShowDialog();
                if (result != DialogResult.OK)
                    return;

                this._recordFile = saveFileDialog.FileName;
                this._isRecording = true;
                DisposeProcess();
            });
        }
    }

    private void StopRecording()
    {
        this._recordFile = null;
        this._isRecording = false;
    }

    private void ShowHelp()
    {
        LogMessage("Wyświetlanie pomocy");
        // Implementacja wyświetlania pomocy
    }

    private void CloseApplication()
    {
        LogMessage("Zamykanie aplikacji");
        // Implementacja zamykania aplikacji
    }

    private void ShowDesktop()
    {
        LogMessage("Wyświetlanie pulpitu na urządzeniu");
        ScrCpy.ExecuteHome(Device.Name);
    }

    public void Start()
    {
        if (IsRunning)
            return;
        IsRunning = true;
        StartScrcpyThread();
    }

    private void StartScrcpyThread()
    {
        var thread = new Thread(RunScrcpyProcess);
        thread.IsBackground = false;
        thread.Start();
    }

    private void RunScrcpyProcess(object state)
    {
        try
        {
            while (IsRunning)
            {
                if (Device?.IsRunning != true)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                try
                {
                    CleanupPreviousProcess();
                    StartNewScrcpyProcess();
                    ZmieńIkonęProcesu();
                    AudioResume();
                    WaitForProcessExit(() =>
                    {
                        CheckOrientation();
                        UpdateWindowInfo();
                    });

                    if (CheckIfScrCpyIsClosed())
                        break;
                }
                catch (Exception ex)
                {
                    LogMessage(ex.Message);
                    DisposeProcess();
                }
            }
        }
        finally
        {
            DisposeProcess();
        }
    }

    private bool CheckIfScrCpyIsClosed()
    {
        if (_scrCpyProcess != null &&
            _scrCpyProcess.HasExited == true)
        {
            Device.IsRunning = false;
            DeviceRepository.UpdateRunData(Device);
            return true;
        }

        return false;
    }

    private void ZmieńIkonęProcesu()
    {
        if (_scrCpyHwnd == IntPtr.Zero)
            return;

        // Zmiana ikony okna procesu
        var nowaIkona = IconHelper.GetAppIcon();
        IconHelper.ZmieńIkonęOkna(_scrCpyHwnd, nowaIkona);
    }

    private void CleanupPreviousProcess()
    {
        if (_scrCpyProcess != null)
            DisposeProcess();
    }

    private void StartNewScrcpyProcess()
    {
        LogMessage("Uruchamianie procesu scrcpy");
        CreateScrCpyIcon();
        var startInfo = CreateProcessStartInfo();

        StartProcess(startInfo);

        WaitForWindowHandle();

        if (IsValidWindowHandle())
        {
            RegisterWindowForKeyboardEvents();
            ActivateWindow();
            ConfigureWindow();
            ChangeScrCpyWindowName($"Dexa ⫽ {Device.FriendlyName}");
        }
        else
        {
            DisposeProcess();
        }
    }

    private void StartProcess(ProcessStartInfo startInfo)
    {
        _scrCpyProcess = Process.Start(startInfo);
        try
        {
            _scrCpyProcess.PriorityClass = ProcessPriorityClass.AboveNormal;
        }
        catch
        {
        }
    }

    private static void CreateScrCpyIcon()
    {
        IconHelper.UstawIkonęWMenuStart(@"scrcpy\scrcpy.exe");
    }

    private ProcessStartInfo CreateProcessStartInfo()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = @"scrcpy\scrcpy.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            Arguments = ScrCpy.BuildScrcpyArguments(
                Device,
                _recordFile,
                _isFullscreen,
                _isGameMode)
        };

        if (!_isFullscreen)
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

        return startInfo;
    }

    private void WaitForWindowHandle()
    {
        var stopwatch = Stopwatch.StartNew();
        const int maxWaitTimeSeconds = 30;

        _scrCpyLastOrientation = null;

        while (_scrCpyHwnd == IntPtr.Zero &&
               !_scrCpyProcess.HasExited &&
               stopwatch.Elapsed < TimeSpan.FromSeconds(maxWaitTimeSeconds))
        {
            _scrCpyHwnd = _scrCpyProcess.MainWindowHandle;
            Thread.Sleep(5);
        }
    }

    private bool IsValidWindowHandle()
    {
        return _scrCpyHwnd != IntPtr.Zero && !_scrCpyProcess.HasExited;
    }

    private void RegisterWindowForKeyboardEvents()
    {
        KeyboardInterceptorWinForms.AddWindowHandle(_scrCpyHwnd);
        KeyboardInterceptorWinForms.KeyboardEvent += OnKeyboardEvent;
    }

    private void ActivateWindow()
    {
        LogMessage($"Aktywacja okna scrcpy (hwnd: 0x{_scrCpyHwnd.ToInt64():X})");

        // Przywróć i pokaż okno
        NativeWindowHelper.ShowWindow(_scrCpyHwnd, NativeWindowHelper.SW_RESTORE);
        NativeWindowHelper.ShowWindow(_scrCpyHwnd, NativeWindowHelper.SW_SHOW);

        // Próba ustawienia na pierwszym planie
        if (NativeWindowHelper.SetForegroundWindow(_scrCpyHwnd))
        {
            LogMessage("Okno scrcpy zostało aktywowane pomyślnie");
        }
        else
        {
            LogMessage("Nie udało się aktywować okna scrcpy");
            TryAlternativeWindowActivation();
        }
    }

    private void TryAlternativeWindowActivation()
    {
        if (NativeWindowHelper.ShowWindowAsync(_scrCpyHwnd, NativeWindowHelper.SW_SHOWNORMAL))
        {
            LogMessage("Alternatywna metoda aktywacji okna zakończona sukcesem");
            Thread.Sleep(100);
            NativeWindowHelper.SetForegroundWindow(_scrCpyHwnd);
        }
    }

    private void ChangeScrCpyWindowName(string nowyTytuł)
    {
        if (_scrCpyHwnd == IntPtr.Zero)
            return;

        LogMessage($"Zmiana tytułu okna na: {nowyTytuł}");
        NativeWindowHelper.SetWindowText(_scrCpyHwnd, nowyTytuł);
    }

    private void ConfigureWindow()
    {
        if (_isFullscreen)
            return;

        if (this.Device?.DeviceWindow != null)
            WindowInfoUtils.SetWindowInfo(_scrCpyHwnd, new WindowInfo()
            {
                State = this.Device.DeviceWindow.WindowState,
                Bounds = new Rectangle(
                    (int)this.Device.DeviceWindow.X,
                    (int)this.Device.DeviceWindow.Y,
                    (int)this.Device.DeviceWindow.Width,
                    (int)this.Device.DeviceWindow.Height)
            });
    }

    private void WaitForProcessExit(Action action)
    {
        while (true)
        {
            Thread.Sleep(10);

            if (_scrCpyProcess == null ||
                _scrCpyProcess.HasExited ||
                !IsRunning)
            {
                LogMessage("Proces scrcpy zakończył działanie");
                //AudioPause(_scrCpyProcess);
                break;
            }

            action();
        }
    }

    private void UpdateWindowInfo()
    {
        if (_isFullscreen)
            return;

        if (!IsRunning)
            return;

        if (Device?.IsRunning != true)
            return;

        if (_scrCpyProcess?.HasExited != false)
            return;

        var newWindowInfo = WindowInfoUtils.GetWindowInfo(_scrCpyHwnd);
        if (newWindowInfo?.State == FormWindowState.Normal)
        {
            if (this.Device.DeviceWindow == null)
                this.Device.DeviceWindow = new DeviceWindow();

            this.Device.DeviceWindow.X = newWindowInfo.Bounds.X;
            this.Device.DeviceWindow.Y = newWindowInfo.Bounds.Y;
            this.Device.DeviceWindow.Width = newWindowInfo.Bounds.Width;
            this.Device.DeviceWindow.Height = newWindowInfo.Bounds.Height;
            this.Device.DeviceWindow.WindowState = newWindowInfo.State;

            DeviceRepository.UpdateRunData(this.Device);
        }
    }

    private void CheckOrientation()
    {
        if (_scrCpyHwnd == IntPtr.Zero)
            return;

        if (_scrCpyProcess?.HasExited != false)
            return;

        try
        {
            var windowSize = WindowInfoUtils.GetWindowInfo(_scrCpyHwnd);
            if (windowSize != null)
                UpdateDeviceOrientation(windowSize);
        }
        catch (Exception ex)
        {
            LogMessage($"Błąd monitorowania orientacji: {ex.Message}");
        }
    }

    private void UpdateDeviceOrientation(WindowInfo windowSize)
    {
        const int minSize = 40;
        bool permissionsExecuted = false;

        if (windowSize.Width <= minSize || windowSize.Height <= minSize ||
            windowSize.State == FormWindowState.Minimized)
            return;

        Orientation currentOrientation = DetermineOrientation(windowSize.Width, windowSize.Height);

        if (_scrCpyLastOrientation == currentOrientation)
            return;

        SetDeviceOrientation(currentOrientation, !permissionsExecuted);
        _scrCpyLastOrientation = currentOrientation;
    }

    private Orientation DetermineOrientation(double width, double height)
    {
        return width > height ? Orientation.Horizontal : Orientation.Vertical;
    }

    private void SetDeviceOrientation(Orientation orientation, bool executePermissions)
    {
        if (orientation == Orientation.Horizontal)
        {
            ScrCpy.SetScreenOrientationHorizontal(Device.Name, executePermissions);
        }
        else
        {
            ScrCpy.SetScreenOrientationVertical(Device.Name, executePermissions);
        }
    }

    private void DisposeProcess()
    {
        var (scrCpyHwnd, scrCpyProcess) = RemoveReferencesToProcess();
        StopRecording();
        AudioPause(scrCpyProcess);
        UnregisterWindowFromKeyboardEvents(scrCpyHwnd);
        CloseScrcpyProcess(scrCpyProcess);
    }

    private (IntPtr scrCpyHwnd, Process? scrCpyProcess) RemoveReferencesToProcess()
    {
        var scrCpyHwnd = _scrCpyHwnd;
        var scrCpyProcess = _scrCpyProcess;
        _scrCpyLastOrientation = null;
        _scrCpyHwnd = IntPtr.Zero;
        _scrCpyProcess = null;
        return (scrCpyHwnd, scrCpyProcess);
    }

    private void AudioPause(Process? scrCpyProcess)
    {
        if (scrCpyProcess == null)
            return;

        var isMediaPlaying = ScrCpy.IsMediaPlay(this.Device?.Name);
        Device?.UpdateMediaPlaying(isMediaPlaying);
        DeviceRepository.UpdateRunData(this.Device);

        if (isMediaPlaying)
            ScrCpy.MediaPause(this.Device?.Name);
    }

    private void AudioResume()
    {
        if (Device.DeviceMedia?.WasMediaPlaying == true &&
            Device.DeviceMedia?.GetTimeSinceMediaPaused() < TimeSpan.FromMinutes(5))
        {
            ScrCpy.MediaPlay(this.Device?.Name);
            Device.ResetMedia();
            DeviceRepository.UpdateRunData(Device);
        }
    }

    private void UnregisterWindowFromKeyboardEvents(IntPtr scrCpyHwnd)
    {
        if (scrCpyHwnd != IntPtr.Zero)
        {
            KeyboardInterceptorWinForms.RemoveWindowHandle(scrCpyHwnd);
            KeyboardInterceptorWinForms.KeyboardEvent -= OnKeyboardEvent;
        }
    }

    private void CloseScrcpyProcess(Process? _scrCpyProcess)
    {
        if (_scrCpyProcess == null)
            return;

        LogMessage("Zamykanie procesu scrcpy");

        try
        {
            CloseMainWindow(_scrCpyProcess);
            EnsureProcessTerminated(_scrCpyProcess);
        }
        catch (Exception ex)
        {
            LogMessage($"Wyjątek podczas zamykania procesu: {ex.Message}");
        }
        finally
        {
            _scrCpyProcess?.Dispose();
            _scrCpyProcess = null;
        }
    }

    private void CloseMainWindow(Process _scrCpyProcess)
    {
        _scrCpyProcess.CloseMainWindow();
    }

    private void EnsureProcessTerminated(Process _scrCpyProcess)
    {
        if (!_scrCpyProcess.HasExited)
        {
            _scrCpyProcess.Kill(true);
            _scrCpyProcess.WaitForExit2(200);
        }
    }

    private void LogMessage(string message)
    {
        Console.WriteLine($"{DateTime.Now.TimeOfDay} - ScrCpyRunner: {message}");
    }

    public void Dispose()
    {
        StopRunning();
        UnregisterEvents();
    }

    private void StopRunning()
    {
        IsRunning = false;
    }

    private void UnregisterEvents()
    {
        KeyboardInterceptorWinForms.KeyboardEvent -= OnKeyboardEvent;
    }
}
