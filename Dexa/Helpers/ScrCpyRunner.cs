using System.Diagnostics;
using System.IO;
using System.Threading;
using Dexa.Helpers;
using Else.PhoneMirror.Repositories;
using Else.PhoneMirror.ViewModels;

namespace Dexa;

public class ScrCpyRunner : IDisposable
{
    private Process? _scrCpyProcess;
    private IntPtr _scrCpyHwnd;
    private int _restartPending;
    private volatile bool _skipPositionSave;
    private string? _recordFile;
    private Stopwatch? _stopwatch;

    public bool IsRunning { get; private set; }
    public Device Device { get; }

    public ScrCpyRunner(Device device)
    {
        Device = device;
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
                ToggleOrientation();
                break;

            case KeyboardInterceptorWinForms.VirtualKeys.F11:
                ToggleFullscreen();
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

    public void ToggleOrientation()
    {
        LogMessage("Przełączanie orientacji ekranu");
        Task.Run(() => ScrCpy.ToggleScreenOrientation(Device.Name));
    }

    public void ToggleFullscreen()
    {
        LogMessage("Przełączanie trybu pełnoekranowego");
        if (Device.IsFullscreen)
            _skipPositionSave = true;
        Device.IsFullscreen = !Device.IsFullscreen;
        ScheduleRestart();
    }

    public void ToggleGameMode()
    {
        LogMessage("Przełączanie trybu gamingowego");
        Device.IsGameMode = !Device.IsGameMode;
        ScheduleRestart();
    }

    private void ScheduleRestart()
    {
        if (Interlocked.CompareExchange(ref _restartPending, 1, 0) != 0)
            return;
        Task.Run(() =>
        {
            try { DisposeProcess(clearRecording: false, forceKill: true); }
            finally
            {
                _skipPositionSave = false;
                Interlocked.Exchange(ref _restartPending, 0);
            }
        });
    }

    public void ToggleRecording()
    {
        if (Device.IsRecording)
        {
            this._recordFile = null;
            Device.IsRecording = false;
            DisposeProcess(clearRecording: false, forceKill: false);
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
                Device.IsRecording = true;
                Task.Run(() => DisposeProcess(clearRecording: false, forceKill: false));
            });
        }
    }

    private void StopRecording()
    {
        this._recordFile = null;
        Device.IsRecording = false;
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

    public void ShowDesktop()
    {
        LogMessage("Wyświetlanie pulpitu na urządzeniu");
        Task.Run(() => ScrCpy.ExecuteHome(Device.Name));
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
        thread.IsBackground = true;
        thread.Start();
    }

    private void RunScrcpyProcess(object state)
    {
        try
        {
            var maxRetries = 5;
            var retryCount = 0;

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
                    if (!StartNewScrcpyProcess())
                    {
                        retryCount++;
                        if (maxRetries == retryCount)
                        {
                            Stop();
                            break;
                        }
                    }
                    else
                    {
                        retryCount = 0;
                    }

                    ZmieńIkonęProcesu();
                    AudioResume();
                    WaitForProcessExit(() =>
                    {
                        NotifyDeviceStatus();
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
            IsRunning = false;
            DisposeProcess();
        }
    }

    private void NotifyDeviceStatus()
    {
        if (_stopwatch == null ||
            _stopwatch.Elapsed > TimeSpan.FromSeconds(30))
        {
            Device.InformRunning();
            DeviceRepository.UpdateRunData(Device);
            _stopwatch = Stopwatch.StartNew();
        }
    }

    private bool CheckIfScrCpyIsClosed()
    {
        if (_scrCpyProcess != null &&
            _scrCpyProcess.HasExited == true)
        {
            Stop();
            return true;
        }

        return false;
    }

    private void Stop()
    {
        Device.Disable();
        DeviceRepository.UpdateRunData(Device);
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

    private bool StartNewScrcpyProcess()
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
            return true;
        }
        else
        {
            DisposeProcess();
            return false;
        }
    }

    private void StartProcess(ProcessStartInfo startInfo)
    {
        _scrCpyProcess = Process.Start(startInfo);
        if (_scrCpyProcess == null) return;
        _scrCpyProcess.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                LogMessage($"scrcpy stderr: {e.Data}");
        };
        _scrCpyProcess.BeginErrorReadLine();
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
        var args = ScrCpy.BuildScrcpyArguments(
            Device,
            Device.DeviceWindow,
            _recordFile,
            Device.IsFullscreen,
            Device.IsGameMode);
        LogMessage($"scrcpy args: {args}");

        var startInfo = new ProcessStartInfo
        {
            FileName = @"scrcpy\scrcpy.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            Arguments = args
        };

        if (!Device.IsFullscreen)
            startInfo.WindowStyle = ProcessWindowStyle.Minimized;

        return startInfo;
    }

    private void WaitForWindowHandle()
    {
        var stopwatch = Stopwatch.StartNew();
        const int maxWaitTimeSeconds = 30;

        while (_scrCpyHwnd == IntPtr.Zero &&
               _scrCpyProcess?.HasExited == false &&
               stopwatch.Elapsed < TimeSpan.FromSeconds(maxWaitTimeSeconds))
        {
            _scrCpyHwnd = _scrCpyProcess?.MainWindowHandle ?? IntPtr.Zero;
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

    private void WaitForProcessExit(Action action)
    {
        while (true)
        {
            Thread.Sleep(500);

            if (_scrCpyProcess == null ||
                _scrCpyProcess.HasExited ||
                !IsRunning)
            {
                LogMessage("Proces scrcpy zakończył działanie");
                break;
            }

            // Track position in memory every 500ms so DisposeProcess has the latest value.
            // Disk write is throttled inside action() to every 30s.
            SaveWindowPosition(_scrCpyHwnd);
            action();
        }
    }

    private void DisposeProcess(bool clearRecording = true, bool forceKill = true)
    {
        var (scrCpyHwnd, scrCpyProcess) = RemoveReferencesToProcess();
        SaveWindowPosition(scrCpyHwnd);
        DeviceRepository.UpdateRunData(Device);
        if (clearRecording)
            StopRecording();
        AudioPause(scrCpyProcess);
        UnregisterWindowFromKeyboardEvents(scrCpyHwnd);
        CloseScrcpyProcess(scrCpyProcess, forceKill);
    }

    private void SaveWindowPosition(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || Device.IsFullscreen || _skipPositionSave) return;
        var windowInfo = WindowInfoUtils.GetWindowInfo(hwnd);
        if (windowInfo?.State != FormWindowState.Normal) return;

        Device.DeviceWindow = new DeviceWindow
        {
            X = windowInfo.Bounds.X,
            Y = windowInfo.Bounds.Y,
            Width = windowInfo.Bounds.Width,
            Height = windowInfo.Bounds.Height,
            WindowState = windowInfo.State
        };
        LogMessage($"Zapis pozycji okna: {windowInfo.Bounds.Width}x{windowInfo.Bounds.Height} @ {windowInfo.Bounds.X},{windowInfo.Bounds.Y}");
    }

    private (IntPtr scrCpyHwnd, Process? scrCpyProcess) RemoveReferencesToProcess()
    {
        var scrCpyHwnd = _scrCpyHwnd;
        var scrCpyProcess = _scrCpyProcess;
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

    private void CloseScrcpyProcess(Process? _scrCpyProcess, Boolean waitForExit = true)
    {
        if (_scrCpyProcess == null)
            return;

        LogMessage("Zamykanie procesu scrcpy");

        try
        {
            CloseMainWindow(_scrCpyProcess);
            if (waitForExit)
                EnsureProcessTerminated(_scrCpyProcess);
            else
                WaitProcessTerminated(_scrCpyProcess);
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
    private void WaitProcessTerminated(Process _scrCpyProcess)
    {
        if (!_scrCpyProcess.HasExited)
        {
            _scrCpyProcess.WaitForExit2(99999);
        }
    }

    private void LogMessage(string message)
    {
        FileLogger.Log($"ScrCpyRunner [{Device?.Name}]: {message}");
    }

    public void RestartProcess()
    {
        DisposeProcess();
    }

    public void Dispose()
    {
        DisposeProcess();
        Stop();
        UnregisterEvents();
    }


    private void UnregisterEvents()
    {
        KeyboardInterceptorWinForms.KeyboardEvent -= OnKeyboardEvent;
    }
}
