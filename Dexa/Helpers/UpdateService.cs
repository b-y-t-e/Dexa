using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace Dexa.Helpers;

/// <summary>
/// Auto-aktualizacja z GitHub Releases (Velopack). Co 10 minut sprawdza, pobiera w tle
/// i zgłasza <see cref="UpdateAvailable"/> — restart dopiero na żądanie użytkownika,
/// żeby nie przerwać trwającego mirroringu.
/// </summary>
public sealed class UpdateService : IDisposable
{
    private const string GithubRepo = "https://github.com/b-y-t-e/Dexa";
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(10);

    private readonly DispatcherTimer _timer;

    // Tworzony leniwie: UpdateManager rzuca wyjątek poza instalacją Velopacka (np. uruchomienie z bin\Debug).
    private UpdateManager? Manager
    {
        get
        {
            if (_mgr is not null || _managerUnavailable)
                return _mgr;

            try
            {
                _mgr = new UpdateManager(new GithubSource(GithubRepo, null, false));
            }
            catch (Exception ex)
            {
                _managerUnavailable = true;
                FileLogger.Log($"Aktualizacje niedostępne w tej instalacji: {ex.Message}");
            }
            return _mgr;
        }
    }

    private UpdateManager? _mgr;
    private bool _managerUnavailable;
    private volatile UpdateInfo? _pendingUpdate;
    private int _checking;

    public event Action? UpdateAvailable;
    public bool HasUpdate => _pendingUpdate != null;
    public string? NewVersion => _pendingUpdate?.TargetFullRelease.Version.ToString();

    public UpdateService()
    {
        _timer = new DispatcherTimer { Interval = CheckInterval };
        _timer.Tick += (_, _) => _ = Task.Run(CheckSilently);
    }

    public void StartPeriodicCheck()
    {
        _timer.Start();
        _ = Task.Run(CheckSilently);
    }

    private void CheckSilently()
    {
        if (_pendingUpdate != null) return;
        if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0) return;
        try
        {
            if (Manager is not { } manager) return;

            var info = manager.CheckForUpdates();
            if (info != null)
            {
                manager.DownloadUpdates(info);
                _pendingUpdate = info;
                FileLogger.Log($"Pobrano aktualizację {NewVersion}");
                System.Windows.Application.Current?.Dispatcher.BeginInvoke(() => UpdateAvailable?.Invoke());
            }
        }
        catch (Exception ex)
        {
            FileLogger.Log($"Sprawdzanie aktualizacji nie powiodło się: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _checking, 0);
        }
    }

    public void ApplyUpdate()
    {
        if (_pendingUpdate == null || Manager is not { } manager) return;

        // Zwykły Shutdown zamiast ApplyUpdatesAndRestart (Environment.Exit): OnExit musi zamknąć
        // procesy scrcpy/adb, inaczej blokują pliki w katalogu aplikacji podczas podmiany.
        manager.WaitExitThenApplyUpdates(_pendingUpdate.TargetFullRelease, silent: false, restart: true);
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
