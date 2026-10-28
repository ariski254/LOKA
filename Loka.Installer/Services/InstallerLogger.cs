using System.IO;

namespace Loka.Installer.Services;

public class InstallerLogger
{
    private static readonly object _lock = new();
    private static string? _logFilePath;

    public static string LogFilePath
    {
        get
        {
            if (_logFilePath == null)
            {
                var dir = Path.Combine(Path.GetTempPath(), "Loka");
                Directory.CreateDirectory(dir);
                _logFilePath = Path.Combine(dir, "installer.log");
            }
            return _logFilePath;
        }
    }

    public static void Log(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level.ToUpper()}] {message}";
        try
        {
            lock (_lock)
            {
                File.AppendAllText(LogFilePath, line + Environment.NewLine);
            }
        }
        catch { }
    }

    public static void Info(string message) => Log("INFO", message);
    public static void Warning(string message) => Log("WARN", message);
    public static void Error(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} (Exception: {ex.Message})" : message;
        Log("ERROR", msg);
    }
}
