using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Microsoft.Win32;

namespace Loka.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IPhpService _phpService;
    private readonly IRuntimeDetector _detector;
    private readonly IDatabaseService _databaseService;
    private readonly IServiceManager _serviceManager;
    private readonly IApacheService _apacheService;
    private readonly INginxService _nginxService;
    private readonly ISslCertificateService _sslService;
    private readonly ILogService _logger;

    // 1. General
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _closeToTray = true;

    [ObservableProperty]
    private string _defaultTerminal = "PowerShell";

    // 2. Environment
    [ObservableProperty]
    private string _rootPath = @"C:\Loka";

    [ObservableProperty]
    private string _projectsPath = @"C:\Loka\projects";

    [ObservableProperty]
    private string _binPath = @"C:\Loka\bin";

    [ObservableProperty]
    private string _logsPath = @"C:\Loka\logs";

    [ObservableProperty]
    private string _backupsPath = @"C:\Loka\backups";

    [ObservableProperty]
    private string _internalEnvPathPreview = string.Empty;

    [ObservableProperty]
    private bool _allowExternalRuntimes;

    // 3. PHP
    [ObservableProperty]
    private string _activePhpVersion = "8.4";

    [ObservableProperty]
    private string _activePhpBinary = string.Empty;

    // 4. Web Server
    [ObservableProperty]
    private string _preferredWebServer = "Apache";

    [ObservableProperty]
    private int _apachePort = 80;

    [ObservableProperty]
    private int _apacheSslPort = 443;

    [ObservableProperty]
    private int _nginxPort = 80;

    [ObservableProperty]
    private bool _autoStartApache;

    [ObservableProperty]
    private bool _autoStartNginx;

    // 5. Database
    [ObservableProperty]
    private string _preferredDatabase = string.Empty;

    [ObservableProperty]
    private int _mySqlPort = 3306;

    [ObservableProperty]
    private int _mariaDbPort = 3307;

    [ObservableProperty]
    private int _postgresPort = 5432;

    [ObservableProperty]
    private string _databaseHost = "127.0.0.1";

    [ObservableProperty]
    private string _databaseRootUser = "root";

    [ObservableProperty]
    private string _databaseRootPassword = string.Empty;

    [ObservableProperty]
    private string _phpMyAdminUrl = "http://localhost/phpmyadmin";

    [ObservableProperty]
    private bool _autoStartMySql;

    [ObservableProperty]
    private bool _autoStartMariaDb;

    [ObservableProperty]
    private bool _autoStartPostgres;

    // 6. Appearance
    [ObservableProperty]
    private string _selectedTheme = "Light";

    // 7. SSL & HTTPS
    [ObservableProperty]
    private bool _isRootCaTrusted;

    [ObservableProperty]
    private string _rootCaStatusText = "Checking...";

    [ObservableProperty]
    private bool _isSslBusy;

    [ObservableProperty]
    private string _statusMessage = "Settings loaded.";

    public ObservableCollection<string> ThemeOptions { get; } = new() { "Light", "Dark", "System" };
    public ObservableCollection<string> WebServerOptions { get; } = new() { "Apache", "Nginx" };
    public ObservableCollection<string> DatabaseOptions { get; } = new() { "MySQL", "MariaDB", "PostgreSQL" };
    public ObservableCollection<string> TerminalOptions { get; } = new() { "PowerShell", "Command Prompt" };
    public ObservableCollection<string> PhpVersionOptions { get; } = new() { "8.5", "8.4", "8.3" };

    public SettingsViewModel(
        IConfigurationService configService,
        IPhpService phpService,
        IRuntimeDetector detector,
        IDatabaseService databaseService,
        IServiceManager serviceManager,
        IApacheService apacheService,
        INginxService nginxService,
        ISslCertificateService sslService,
        ILogService logger)
    {
        _configService = configService;
        _phpService = phpService;
        _detector = detector;
        _databaseService = databaseService;
        _serviceManager = serviceManager;
        _apacheService = apacheService;
        _nginxService = nginxService;
        _sslService = sslService;
        _logger = logger;

        LoadSettings();
    }

    public async Task InitializeAsync()
    {
        LoadSettings();
        await Task.CompletedTask;
    }

    public void LoadSettings()
    {
        var cfg = _configService.Config;

        // General
        StartWithWindows = cfg.General.StartWithWindows;
        MinimizeToTray = cfg.General.MinimizeToTray;
        CloseToTray = cfg.General.CloseToTray;
        SelectedTheme = string.IsNullOrEmpty(cfg.General.Theme) ? "Light" : cfg.General.Theme;

        // Environment Paths
        RootPath = cfg.Paths.RootPath;
        ProjectsPath = cfg.Paths.ProjectsPath;
        BinPath = cfg.Paths.BinPath;
        LogsPath = cfg.Paths.LogsPath;
        BackupsPath = cfg.Paths.BackupsPath;
        AllowExternalRuntimes = cfg.Paths.AllowExternalRuntimes;
        InternalEnvPathPreview = _configService.GetInternalEnvironmentPath();

        // PHP
        ActivePhpVersion = string.IsNullOrEmpty(cfg.Php.ActiveVersion) ? "8.4" : cfg.Php.ActiveVersion;
        ActivePhpBinary = _configService.GetActivePhpBinary();

        // Web Server
        PreferredWebServer = cfg.Services.PreferredWebServer;
        ApachePort = cfg.Services.ApachePort;
        ApacheSslPort = cfg.Services.ApacheSslPort;
        NginxPort = cfg.Services.NginxPort;
        AutoStartApache = cfg.Services.AutoStartApache;
        AutoStartNginx = cfg.Services.AutoStartNginx;

        // Database
        PreferredDatabase = cfg.Services.PreferredDatabase;
        MySqlPort = cfg.Services.MySqlPort;
        MariaDbPort = cfg.Services.MariaDbPort;
        PostgresPort = cfg.Services.PostgresPort;
        DatabaseHost = cfg.Database.Host;
        DatabaseRootUser = cfg.Database.RootUser;
        DatabaseRootPassword = cfg.Database.RootPassword ?? "";
        PhpMyAdminUrl = cfg.Database.PhpMyAdminUrl;
        AutoStartMySql = cfg.Services.AutoStartMySql;
        AutoStartMariaDb = cfg.Services.AutoStartMariaDb;
        AutoStartPostgres = cfg.Services.AutoStartPostgres;

        // Root CA & SSL
        var sslDir = Path.Combine(cfg.Paths.ConfigPath, "ssl");
        IsRootCaTrusted = _sslService.IsRootCertificateTrusted(sslDir);
        RootCaStatusText = IsRootCaTrusted ? "Trusted (Valid & Active in Windows)" : "Not Trusted";

        StatusMessage = "Settings loaded.";
    }

    [RelayCommand]
    public async Task TrustRootCertificateAsync()
    {
        if (IsSslBusy) return;
        IsSslBusy = true;
        StatusMessage = "Installing Loka Root CA into Windows Certificate Store...";

        try
        {
            var sslDir = Path.Combine(_configService.Config.Paths.ConfigPath, "ssl");
            await _sslService.EnsureRootCertificateAuthorityAsync(sslDir);
            await _sslService.TrustRootCertificateAsync(sslDir);
            int count = await _sslService.RegenerateAllCertificatesAsync(sslDir);

            IsRootCaTrusted = _sslService.IsRootCertificateTrusted(sslDir);
            RootCaStatusText = IsRootCaTrusted ? "Trusted (Valid & Active in Windows)" : "Not Trusted";
            StatusMessage = IsRootCaTrusted 
                ? $"Root CA installed successfully! {count} domain certificates updated."
                : "Certificate installation request sent.";

            try
            {
                await _serviceManager.RestartServiceAsync(Loka.Core.Enums.ServiceType.Apache);
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError("Settings", "Failed trusting Root CA", ex);
            StatusMessage = $"Failed to install Root CA: {ex.Message}";
        }
        finally
        {
            IsSslBusy = false;
        }
    }

    [RelayCommand]
    public async Task ApplyDatabasePasswordAsync()
    {
        try
        {
            var pass = DatabaseRootPassword?.Trim() ?? "";
            StatusMessage = string.IsNullOrEmpty(pass) ? "Clearing MySQL password..." : "Applying MySQL password...";
            await _databaseService.SetRootPasswordAsync(pass);
            StatusMessage = string.IsNullOrEmpty(pass)
                ? "MySQL password cleared successfully (no password)."
                : "MySQL password updated and saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to set MySQL password: {ex.Message}";
            _logger.LogError("Settings", "Failed setting MySQL password", ex);
        }
    }

    [RelayCommand]
    public async Task ClearDatabasePasswordAsync()
    {
        try
        {
            DatabaseRootPassword = string.Empty;
            StatusMessage = "Clearing MySQL password...";
            await _databaseService.SetRootPasswordAsync(string.Empty);
            StatusMessage = "MySQL password cleared successfully (no password).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear MySQL password: {ex.Message}";
            _logger.LogError("Settings", "Failed clearing MySQL password", ex);
        }
    }

    [RelayCommand]
    public void BrowseRootPath()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Loka Root Environment Directory",
            InitialDirectory = Directory.Exists(RootPath) ? RootPath : @"C:\"
        };
        if (fbd.ShowDialog() == true)
        {
            RootPath = fbd.FolderName;
            ProjectsPath = Path.Combine(RootPath, "projects");
            BinPath = Path.Combine(RootPath, "bin");
            LogsPath = Path.Combine(RootPath, "logs");
            BackupsPath = Path.Combine(RootPath, "backups");
        }
    }

    [RelayCommand]
    public void BrowseProjectsPath()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Projects Directory",
            InitialDirectory = Directory.Exists(ProjectsPath) ? ProjectsPath : @"C:\"
        };
        if (fbd.ShowDialog() == true)
        {
            ProjectsPath = fbd.FolderName;
        }
    }

    [RelayCommand]
    public void ReinitializeDirectories()
    {
        _configService.EnsureDirectories();
        InternalEnvPathPreview = _configService.GetInternalEnvironmentPath();
        StatusMessage = "Internal environment directories verified and created.";
    }

    [RelayCommand]
    public void OpenPhpMyAdminInBrowser()
    {
        var url = PhpMyAdminUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            url = ApachePort == 80 ? "http://localhost/phpmyadmin" : $"http://localhost:{ApachePort}/phpmyadmin";
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
            StatusMessage = $"Opening phpMyAdmin at {url}...";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open browser: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        try
        {
            if (Directory.Exists(LogsPath))
            {
                var files = Directory.GetFiles(LogsPath, "*.log", SearchOption.AllDirectories);
                foreach (var f in files)
                {
                    try { File.WriteAllText(f, string.Empty); } catch { }
                }
                StatusMessage = "All log files cleared successfully.";
            }
            else
            {
                StatusMessage = "Logs directory does not exist.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear logs: {ex.Message}";
        }
    }



    [RelayCommand]
    public async Task SaveSettingsAsync()
    {
        if (ApachePort < 1 || ApachePort > 65535 || MySqlPort < 1 || MySqlPort > 65535 || NginxPort < 1 || NginxPort > 65535 || MariaDbPort < 1 || MariaDbPort > 65535 || PostgresPort < 1 || PostgresPort > 65535)
        {
            StatusMessage = "Validation error: Network ports must be integers between 1 and 65535.";
            return;
        }

        if (string.IsNullOrWhiteSpace(RootPath) || string.IsNullOrWhiteSpace(ProjectsPath))
        {
            StatusMessage = "Validation error: Root and Projects paths cannot be empty.";
            return;
        }

        var cfg = _configService.Config;

        // General
        cfg.General.StartWithWindows = StartWithWindows;
        cfg.General.MinimizeToTray = MinimizeToTray;
        cfg.General.CloseToTray = CloseToTray;
        cfg.General.Theme = SelectedTheme;

        // Paths
        cfg.Paths.RootPath = RootPath.Trim();
        cfg.Paths.ProjectsPath = ProjectsPath.Trim();
        cfg.Paths.BinPath = BinPath.Trim();
        cfg.Paths.LogsPath = LogsPath.Trim();
        cfg.Paths.BackupsPath = BackupsPath.Trim();
        cfg.Paths.AllowExternalRuntimes = AllowExternalRuntimes;

        // PHP
        cfg.Php.ActiveVersion = ActivePhpVersion;

        // Web Server
        cfg.Services.PreferredWebServer = PreferredWebServer;
        cfg.Services.ApachePort = ApachePort;
        cfg.Services.ApacheSslPort = ApacheSslPort;
        cfg.Services.NginxPort = NginxPort;
        cfg.Services.AutoStartApache = AutoStartApache;
        cfg.Services.AutoStartNginx = AutoStartNginx;

        // Database
        cfg.Services.PreferredDatabase = PreferredDatabase;
        cfg.Services.MySqlPort = MySqlPort;
        cfg.Services.MariaDbPort = MariaDbPort;
        cfg.Services.PostgresPort = PostgresPort;
        cfg.Database.Host = DatabaseHost.Trim();
        cfg.Database.RootUser = DatabaseRootUser.Trim();
        cfg.Database.RootPassword = DatabaseRootPassword.Trim();
        cfg.Database.PhpMyAdminUrl = PhpMyAdminUrl.Trim();
        cfg.Services.AutoStartMySql = AutoStartMySql;
        cfg.Services.AutoStartMariaDb = AutoStartMariaDb;
        cfg.Services.AutoStartPostgres = AutoStartPostgres;

        try
        {
            // 1. Save configuration to loka.json
            await _configService.SaveAsync();
            _configService.EnsureDirectories();
            InternalEnvPathPreview = _configService.GetInternalEnvironmentPath();
            ActivePhpBinary = _configService.GetActivePhpBinary();

            // 2. Apply PHP Active Version to system
            if (!string.IsNullOrWhiteSpace(ActivePhpVersion))
            {
                await _phpService.SetActivePhpVersionAsync(ActivePhpVersion);
            }

            // 3. Update Apache port in httpd.conf
            await _apacheService.UpdatePortConfigurationAsync(ApachePort, ApacheSslPort);

            // 5. Update Nginx port in nginx.conf
            await _nginxService.UpdatePortConfigurationAsync(NginxPort);

            // Stop inactive web server to ensure clean mutual exclusion on port 80/443
            if (string.Equals(PreferredWebServer, "Apache", StringComparison.OrdinalIgnoreCase))
            {
                var ng = _serviceManager.GetService(Loka.Core.Enums.ServiceType.Nginx);
                if (ng != null && ng.IsRunning)
                {
                    await _serviceManager.StopServiceAsync(Loka.Core.Enums.ServiceType.Nginx);
                }
            }
            else if (string.Equals(PreferredWebServer, "Nginx", StringComparison.OrdinalIgnoreCase))
            {
                var ap = _serviceManager.GetService(Loka.Core.Enums.ServiceType.Apache);
                if (ap != null && ap.IsRunning)
                {
                    await _serviceManager.StopServiceAsync(Loka.Core.Enums.ServiceType.Apache);
                }
            }

            // 6. Update ServiceManager live service ports
            var apService = _serviceManager.GetService(Loka.Core.Enums.ServiceType.Apache);
            if (apService != null) apService.Port = ApachePort;
            var myService = _serviceManager.GetService(Loka.Core.Enums.ServiceType.MySQL);
            if (myService != null) myService.Port = MySqlPort;
            var ngService = _serviceManager.GetService(Loka.Core.Enums.ServiceType.Nginx);
            if (ngService != null) ngService.Port = NginxPort;
            var maService = _serviceManager.GetService(Loka.Core.Enums.ServiceType.MariaDB);
            if (maService != null) maService.Port = MariaDbPort;
            var pgService = _serviceManager.GetService(Loka.Core.Enums.ServiceType.PostgreSQL);
            if (pgService != null) pgService.Port = PostgresPort;

            // 7. Update Windows startup registry key
            try
            {
                using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (runKey != null)
                {
                    if (StartWithWindows)
                    {
                        var exePath = Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath))
                        {
                            runKey.SetValue("Loka", $"\"{exePath}\"");
                        }
                    }
                    else
                    {
                        runKey.DeleteValue("Loka", false);
                    }
                }
            }
            catch (Exception regEx)
            {
                _logger.LogWarning("Settings", $"Could not update Windows startup registry: {regEx.Message}");
            }

            // 8. Refresh all services runtime detection
            await _serviceManager.RefreshAllAsync();

            StatusMessage = "Settings saved and successfully applied to the system.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save error: {ex.Message}";
            _logger.LogError("Settings", "Failed saving settings", ex);
        }
    }
}
