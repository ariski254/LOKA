namespace Loka.Core.Models;

public class LokaConfig
{
    public PathsConfig Paths { get; set; } = new();
    public ServicesConfig Services { get; set; } = new();
    public GeneralConfig General { get; set; } = new();
    public PhpConfig Php { get; set; } = new();
    public DatabaseConfig Database { get; set; } = new();
    public UpdateConfig Updates { get; set; } = new();
}

public class UpdateConfig
{
    public bool CheckOnStartup { get; set; } = true;
    public string FeedUrl { get; set; } = "https://updates.loka.dev/patches/manifest.json";
    public bool AutoDownload { get; set; } = false;
    public string InstalledVersion { get; set; } = "1.0.0";
}

public class PathsConfig
{
    private string _rootPath = @"C:\Loka";

    public string RootPath
    {
        get => _rootPath;
        set
        {
            _rootPath = value;
            _binPath = Path.Combine(value, "bin");
            _etcPath = Path.Combine(value, "etc");
            _dataPath = Path.Combine(value, "data");
            _projectsPath = Path.Combine(value, "projects");
            _logsPath = Path.Combine(value, "logs");
            _tmpPath = Path.Combine(value, "tmp");
            _sslPath = Path.Combine(value, "ssl");
            _backupsPath = Path.Combine(value, "backups");
            _configPath = Path.Combine(value, "config");
        }
    }

    private string _binPath = @"C:\Loka\bin";
    public string BinPath
    {
        get => _binPath;
        set => _binPath = value;
    }

    // Alias for backward compatibility
    public string RuntimesPath
    {
        get => _binPath;
        set => _binPath = value;
    }

    private string _etcPath = @"C:\Loka\etc";
    public string EtcPath
    {
        get => _etcPath;
        set => _etcPath = value;
    }

    private string _dataPath = @"C:\Loka\data";
    public string DataPath
    {
        get => _dataPath;
        set => _dataPath = value;
    }

    private string _projectsPath = @"C:\Loka\projects";
    public string ProjectsPath
    {
        get => _projectsPath;
        set => _projectsPath = value;
    }

    private string _logsPath = @"C:\Loka\logs";
    public string LogsPath
    {
        get => _logsPath;
        set => _logsPath = value;
    }

    private string _configPath = @"C:\Loka\config";
    public string ConfigPath
    {
        get => _configPath;
        set => _configPath = value;
    }

    private string _backupsPath = @"C:\Loka\backups";
    public string BackupsPath
    {
        get => _backupsPath;
        set => _backupsPath = value;
    }

    private string _tmpPath = @"C:\Loka\tmp";
    public string TmpPath
    {
        get => _tmpPath;
        set => _tmpPath = value;
    }

    // Alias for backward compatibility
    public string TempPath
    {
        get => _tmpPath;
        set => _tmpPath = value;
    }

    private string _sslPath = @"C:\Loka\ssl";
    public string SslPath
    {
        get => _sslPath;
        set => _sslPath = value;
    }

    /// <summary>
    /// Optional external search paths if user opts into external detection.
    /// Default Loka operation strictly prioritizes internal Loka binaries.
    /// </summary>
    public List<string> AdditionalRuntimeSearchPaths { get; set; } = new()
    {
        @"C:\laragon\bin",
        @"C:\xampp",
        @"C:\Program Files\nodejs",
        @"C:\Program Files\Git\cmd"
    };

    public bool AllowExternalRuntimes { get; set; } = false;
}

public class ServicesConfig
{
    public int ApachePort { get; set; } = 80;
    public int ApacheSslPort { get; set; } = 443;
    public int NginxPort { get; set; } = 80;
    public int MySqlPort { get; set; } = 3306;
    public int MariaDbPort { get; set; } = 3307;
    public int PostgresPort { get; set; } = 5432;
    public string PreferredWebServer { get; set; } = "Nginx"; // "Apache" or "Nginx"
    public string PreferredDatabase { get; set; } = string.Empty; // Neutral: "MySQL", "MariaDB", or "PostgreSQL"
    public bool AutoStartApache { get; set; } = false;
    public bool AutoStartMySql { get; set; } = false;
    public bool AutoStartNginx { get; set; } = false;
    public bool AutoStartMariaDb { get; set; } = false;
    public bool AutoStartPostgres { get; set; } = false;
}

public class GeneralConfig
{
    public bool StartWithWindows { get; set; } = false;
    public bool MinimizeToTray { get; set; } = true;
    public bool CloseToTray { get; set; } = true;
    public string Theme { get; set; } = "Light";
    public string Language { get; set; } = "English";
    public bool IsFirstRunCompleted { get; set; } = false;
}

public class PhpConfig
{
    public string ActiveVersion { get; set; } = "8.4";
    public List<string> AvailableVersions { get; set; } = new() { "8.3", "8.4", "8.5" };
    public List<string> EnabledExtensions { get; set; } = new()
    {
        "curl", "fileinfo", "mbstring", "openssl", "pdo_mysql", "mysqli", "zip", "pdo_pgsql", "pgsql"
    };
}

public class DatabaseConfig
{
    public string RootUser { get; set; } = "root";
    public string RootPassword { get; set; } = "";
    public string Host { get; set; } = "127.0.0.1";
    public string PhpMyAdminUrl { get; set; } = "http://localhost/phpmyadmin";
}
