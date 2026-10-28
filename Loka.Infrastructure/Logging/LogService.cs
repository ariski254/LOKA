using System.Collections.Concurrent;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Infrastructure.Logging;

public class LogService : ILogService
{
    private readonly ConcurrentQueue<LogEntry> _inMemoryLogs = new();
    private readonly object _fileLock = new();
    private readonly string _logsDirectory;
    private const int MaxMemoryLogs = 1000;

    public event Action<LogEntry>? LogAdded;

    public LogService(string? customLogsDirectory = null)
    {
        _logsDirectory = customLogsDirectory ?? @"C:\Loka\logs";
        try
        {
            if (!Directory.Exists(_logsDirectory))
            {
                Directory.CreateDirectory(_logsDirectory);
            }
        }
        catch
        {
            // Fallback to local appdata if C:\Loka is not yet accessible
            _logsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loka", "logs");
            Directory.CreateDirectory(_logsDirectory);
        }
    }

    public void Log(LogSeverity severity, string source, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Severity = severity,
            Source = source,
            Message = message
        };

        _inMemoryLogs.Enqueue(entry);
        while (_inMemoryLogs.Count > MaxMemoryLogs && _inMemoryLogs.TryDequeue(out _)) { }

        // Write to log file asynchronously in background
        Task.Run(() => WriteToFile(entry));

        LogAdded?.Invoke(entry);
    }

    public void LogInfo(string source, string message) => Log(LogSeverity.Information, source, message);
    public void LogWarning(string source, string message) => Log(LogSeverity.Warning, source, message);
    public void LogError(string source, string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message} (Exception: {ex.Message})" : message;
        Log(LogSeverity.Error, source, msg);
    }

    public IReadOnlyList<LogEntry> GetLogs(string? sourceFilter = null, int limit = 500)
    {
        var list = _inMemoryLogs.ToList();
        if (!string.IsNullOrWhiteSpace(sourceFilter) && !sourceFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            list = list.Where(l => l.Source.Equals(sourceFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return list.TakeLast(limit).ToList();
    }

    public void Clear()
    {
        while (_inMemoryLogs.TryDequeue(out _)) { }
    }

    private void WriteToFile(LogEntry entry)
    {
        try
        {
            var fileName = $"loka_{DateTime.Now:yyyyMMdd}.log";
            var fullPath = Path.Combine(_logsDirectory, fileName);
            lock (_fileLock)
            {
                File.AppendAllText(fullPath, entry.ToString() + Environment.NewLine);
            }
        }
        catch
        {
            // Never crash the application on file logging failure
        }
    }
}
