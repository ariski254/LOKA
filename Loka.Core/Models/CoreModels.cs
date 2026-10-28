using Loka.Core.Enums;

namespace Loka.Core.Models;

public class ProjectInfo : ObservableModel
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private ProjectFramework _framework = ProjectFramework.PlainPhp;
    private string _phpVersion = string.Empty;
    private string _nodeVersion = string.Empty;
    private string _domain = string.Empty;
    private int _port = 80;
    private int _sslPort = 443;
    private string _documentRoot = string.Empty;
    private bool _hasSsl;
    private bool _isVhostConfigured;
    private bool _isInHostsFile;
    private bool _isRunning;
    private string _gitBranch = string.Empty;
    private string _gitStatus = string.Empty;
    private DateTime _lastModified;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Path
    {
        get => _path;
        set => SetProperty(ref _path, value);
    }

    public ProjectFramework Framework
    {
        get => _framework;
        set
        {
            if (SetProperty(ref _framework, value))
            {
                OnPropertyChanged(nameof(FrameworkName));
            }
        }
    }

    public string FrameworkName => Framework switch
    {
        ProjectFramework.Laravel => "Laravel",
        ProjectFramework.Kyro => "Kyro",
        ProjectFramework.WordPress => "WordPress",
        ProjectFramework.Symfony => "Symfony",
        ProjectFramework.CodeIgniter => "CodeIgniter",
        ProjectFramework.Yii2 => "Yii2",
        ProjectFramework.CakePhp => "CakePHP",
        ProjectFramework.Slim => "Slim",
        ProjectFramework.NodeJs => "Node.js",
        ProjectFramework.NextJs => "Next.js",
        ProjectFramework.NuxtJs => "Nuxt.js",
        ProjectFramework.Express => "Express",
        ProjectFramework.Vite => "Vite",
        ProjectFramework.React => "React",
        ProjectFramework.Vue => "Vue",
        ProjectFramework.PlainPhp => "PHP",
        ProjectFramework.StaticHtml => "HTML",
        _ => "Custom"
    };

    public string PhpVersion
    {
        get => _phpVersion;
        set => SetProperty(ref _phpVersion, value);
    }

    public string NodeVersion
    {
        get => _nodeVersion;
        set => SetProperty(ref _nodeVersion, value);
    }

    public string Domain
    {
        get => _domain;
        set
        {
            if (SetProperty(ref _domain, value))
            {
                OnPropertyChanged(nameof(LocalUrl));
                OnPropertyChanged(nameof(LocalHttpsUrl));
            }
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                OnPropertyChanged(nameof(LocalUrl));
            }
        }
    }

    public int SslPort
    {
        get => _sslPort;
        set
        {
            if (SetProperty(ref _sslPort, value))
            {
                OnPropertyChanged(nameof(LocalHttpsUrl));
            }
        }
    }

    public string LocalUrl => Port == 80 || Port <= 0 ? $"http://{Domain}" : $"http://{Domain}:{Port}";
    public string LocalHttpsUrl => SslPort == 443 || SslPort <= 0 ? $"https://{Domain}" : $"https://{Domain}:{SslPort}";

    public string DocumentRoot
    {
        get => _documentRoot;
        set => SetProperty(ref _documentRoot, value);
    }

    public bool HasSsl
    {
        get => _hasSsl;
        set => SetProperty(ref _hasSsl, value);
    }

    public bool IsVhostConfigured
    {
        get => _isVhostConfigured;
        set => SetProperty(ref _isVhostConfigured, value);
    }

    public bool IsInHostsFile
    {
        get => _isInHostsFile;
        set => SetProperty(ref _isInHostsFile, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        set => SetProperty(ref _isRunning, value);
    }

    public string GitBranch
    {
        get => _gitBranch;
        set => SetProperty(ref _gitBranch, value);
    }

    public string GitStatus
    {
        get => _gitStatus;
        set => SetProperty(ref _gitStatus, value);
    }

    public DateTime LastModified
    {
        get => _lastModified;
        set => SetProperty(ref _lastModified, value);
    }
}

public class PortInfo : ObservableModel
{
    private int _port;
    private string _protocol = "TCP";
    private bool _isOccupied;
    private int? _processId;
    private string _processName = string.Empty;
    private string _processPath = string.Empty;
    private string _serviceAssigned = string.Empty;

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public string Protocol
    {
        get => _protocol;
        set => SetProperty(ref _protocol, value);
    }

    public bool IsOccupied
    {
        get => _isOccupied;
        set => SetProperty(ref _isOccupied, value);
    }

    public int? ProcessId
    {
        get => _processId;
        set => SetProperty(ref _processId, value);
    }

    public string ProcessName
    {
        get => _processName;
        set => SetProperty(ref _processName, value);
    }

    public string ProcessPath
    {
        get => _processPath;
        set => SetProperty(ref _processPath, value);
    }

    public string ServiceAssigned
    {
        get => _serviceAssigned;
        set => SetProperty(ref _serviceAssigned, value);
    }
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogSeverity Severity { get; set; } = LogSeverity.Information;
    public string Source { get; set; } = "Loka";
    public string Message { get; set; } = string.Empty;

    public override string ToString()
    {
        return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Severity.ToString().ToUpperInvariant()}] [{Source}] {Message}";
    }
}

public class DiagnosticItem
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsPassed { get; set; }
    public string Details { get; set; } = string.Empty;
    public string? RemediationSuggestion { get; set; }
}

public class DatabaseInfo : ObservableModel
{
    private string _name = string.Empty;
    private string _collation = "utf8mb4_unicode_ci";
    private int _tablesCount;
    private long _sizeBytes;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Collation
    {
        get => _collation;
        set => SetProperty(ref _collation, value);
    }

    public int TablesCount
    {
        get => _tablesCount;
        set => SetProperty(ref _tablesCount, value);
    }

    public long SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                OnPropertyChanged(nameof(FormattedSize));
            }
        }
    }

    public string FormattedSize => SizeBytes > 1024 * 1024 
        ? $"{SizeBytes / (1024.0 * 1024.0):F2} MB" 
        : $"{SizeBytes / 1024.0:F1} KB";
}

public class DatabaseTableInfo : ObservableModel
{
    private string _name = string.Empty;
    private long _rowsCount;
    private string _engine = "InnoDB";
    private long _dataLength;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public long RowsCount
    {
        get => _rowsCount;
        set => SetProperty(ref _rowsCount, value);
    }

    public string Engine
    {
        get => _engine;
        set => SetProperty(ref _engine, value);
    }

    public long DataLength
    {
        get => _dataLength;
        set => SetProperty(ref _dataLength, value);
    }
}

public class SqlQueryResult
{
    public bool IsSuccess { get; set; }
    public int RowsAffected { get; set; }
    public List<string> ColumnNames { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public double ExecutionTimeMs { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ProcessExecutionResult
{
    public int ExitCode { get; set; }
    public string StandardOutput { get; set; } = string.Empty;
    public string StandardError { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool Success => ExitCode == 0;
}

public class DatabaseServerStatus
{
    public string Name { get; set; } = string.Empty; // "MySQL" or "MariaDB"
    public string Host { get; set; } = "127.0.0.1";
    public int ConfiguredPort { get; set; }
    public int ActualPort { get; set; }
    public bool IsProcessRunning { get; set; }
    public bool IsPortListening { get; set; }
    public bool IsConnectionSuccessful { get; set; }
    public bool IsVerifiedVendor { get; set; }
    public string? DetectedVendor { get; set; } // "MySQL", "MariaDB", or "Unknown"
    public string StatusText { get; set; } = "Stopped";
    public string? ErrorDetails { get; set; }
    public int? ProcessId { get; set; }
    public string? ProcessPath { get; set; }
    public string Version { get; set; } = "-";

    public bool IsHealthy
    {
        get
        {
            if (!IsPortListening) return false;
            if (!string.IsNullOrEmpty(DetectedVendor) && !string.Equals(DetectedVendor, Name, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return (IsVerifiedVendor || IsConnectionSuccessful || IsProcessRunning);
        }
    }
}

public class CurrentDatabaseResolution
{
    public string? ActiveServerName { get; set; } // "MySQL", "MariaDB", or null / "Not available"
    public int ActivePort { get; set; }
    public int PhpMyAdminServerIndex { get; set; } = 1;
    public DatabaseServerStatus MySqlStatus { get; set; } = new();
    public DatabaseServerStatus MariaDbStatus { get; set; } = new();
    public string Message { get; set; } = string.Empty;
    public string DiagnosticReason { get; set; } = string.Empty;
}


