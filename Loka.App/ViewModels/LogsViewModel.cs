using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILogService _logService;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private string _selectedSource = "All";

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    public ObservableCollection<string> Sources { get; } = new()
    {
        "All", "Apache", "MySQL", "PHP", "Nginx", "Detector", "PortService", "Loka"
    };

    public ObservableCollection<LogEntry> DisplayedLogs { get; } = new();

    public LogsViewModel(ILogService logService, IConfigurationService configService)
    {
        _logService = logService;
        _configService = configService;

        _logService.LogAdded += OnLogAdded;
    }

    public void Initialize()
    {
        RefreshLogs();
    }

    partial void OnSelectedSourceChanged(string value)
    {
        RefreshLogs();
    }

    partial void OnSearchFilterChanged(string value)
    {
        RefreshLogs();
    }

    [RelayCommand]
    public void RefreshLogs()
    {
        var logs = _logService.GetLogs(SelectedSource, 500).ToList();

        // Also check if physical log file exists on disk to load real process output
        LoadPhysicalFileLogs(logs);

        if (!string.IsNullOrWhiteSpace(SearchFilter))
        {
            var q = SearchFilter.Trim().ToLowerInvariant();
            logs = logs.Where(l => l.Message.ToLowerInvariant().Contains(q) || l.Source.ToLowerInvariant().Contains(q)).ToList();
        }

        DisplayedLogs.Clear();
        foreach (var l in logs.OrderBy(l => l.Timestamp))
        {
            DisplayedLogs.Add(l);
        }
    }

    private void LoadPhysicalFileLogs(List<LogEntry> logs)
    {
        try
        {
            var logDir = _configService.Config.Paths.LogsPath;
            if (!Directory.Exists(logDir)) return;

            string? fileName = SelectedSource switch
            {
                "Apache" => "apache_error.log",
                "MySQL" => "mysql_error.log",
                "PHP" => "php_error.log",
                "Nginx" => "nginx_error.log",
                "Loka" => "loka.log",
                _ => null
            };

            if (fileName != null)
            {
                var filePath = Path.Combine(logDir, fileName);
                if (File.Exists(filePath))
                {
                    var lines = File.ReadLines(filePath).TakeLast(100).ToList();
                    foreach (var line in lines)
                    {
                        if (!string.IsNullOrWhiteSpace(line) && !logs.Any(l => l.Message.Equals(line, StringComparison.Ordinal)))
                        {
                            logs.Add(new LogEntry
                            {
                                Timestamp = File.GetLastWriteTime(filePath),
                                Source = SelectedSource,
                                Severity = line.Contains("error", StringComparison.OrdinalIgnoreCase) ? LogSeverity.Error : LogSeverity.Information,
                                Message = line
                            });
                        }
                    }
                }
            }
        }
        catch { }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        _logService.Clear();
        DisplayedLogs.Clear();
    }

    [RelayCommand]
    public void CopyLogs()
    {
        var all = string.Join(Environment.NewLine, DisplayedLogs.Select(l => l.ToString()));
        if (!string.IsNullOrEmpty(all))
        {
            try
            {
                Clipboard.SetDataObject(all, true);
            }
            catch { }
        }
    }

    [RelayCommand]
    public void OpenLogFile()
    {
        var logDir = _configService.Config.Paths.LogsPath;
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

        string fileName = SelectedSource switch
        {
            "Apache" => "apache_error.log",
            "MySQL" => "mysql_error.log",
            "PHP" => "php_error.log",
            "Nginx" => "nginx_error.log",
            _ => "loka.log"
        };

        var filePath = Path.Combine(logDir, fileName);
        if (!File.Exists(filePath))
        {
            File.WriteAllText(filePath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{SelectedSource}] Log file created by Loka.\n");
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    public void OpenLogsFolder()
    {
        var path = _configService.Config.Paths.LogsPath;
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void OnLogAdded(LogEntry entry)
    {
        UiDispatcher.Run(() =>
        {
            if (SelectedSource == "All" || entry.Source.Equals(SelectedSource, StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(SearchFilter) || 
                    entry.Message.Contains(SearchFilter, StringComparison.OrdinalIgnoreCase))
                {
                    DisplayedLogs.Add(entry);
                    if (DisplayedLogs.Count > 1000)
                    {
                        DisplayedLogs.RemoveAt(0);
                    }
                }
            }
        });
    }
}
