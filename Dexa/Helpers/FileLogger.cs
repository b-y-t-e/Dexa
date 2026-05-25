using System.Diagnostics;
using System.IO;

namespace Dexa.Helpers;

public static class FileLogger
{
    private static readonly string LogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dexa", "dexa.log");

    private static StreamWriter? _writer;
    private static readonly object _lock = new object();

    [Conditional("DEBUG")]
    public static void Init()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
        _writer = new StreamWriter(LogPath, append: false) { AutoFlush = true };
        Log("=== Dexa start ===");
    }

    [Conditional("DEBUG")]
    public static void Log(string message)
    {
        lock (_lock)
        {
            try
            {
                _writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} — {message}");
            }
            catch { }
        }
    }

    [Conditional("DEBUG")]
    public static void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
            _writer = null;
        }
    }
}
