using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IServiceManager _serviceManager;
    private readonly IRuntimeDetector _detector;
    private readonly IProjectService _projectService;
    private readonly IConfigurationService _configService;
    private readonly IPhpService _phpService;
    private readonly ISslCertificateService _sslService;
    private readonly ILogService _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isRootCaTrusted = true;

    public bool IsRootCaUntrusted => !IsRootCaTrusted;

    [ObservableProperty]
    private bool _isSslBusy;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // System Information (Section 3)
    [ObservableProperty]
    private string _windowsVersion = RuntimeInformation.OSDescription;

    [ObservableProperty]
    private string _cpuInfo = $"{Environment.ProcessorCount} Logical Cores ({RuntimeInformation.ProcessArchitecture})";

    [ObservableProperty]
    private string _ramInfo = GetSystemMemorySummary();

    [ObservableProperty]
    private string _architectureInfo = RuntimeInformation.OSArchitecture.ToString();

    [ObservableProperty]
    private string _lokaVersion = "1.0.0 (Production Release)";

    [ObservableProperty]
    private string _environmentPath = @"C:\Loka";

    [ObservableProperty]
    private string _greeting = "Good day";

    [ObservableProperty]
    private string _activePhpVersion = "8.4";

    // Project Summary (Section 3)
    [ObservableProperty]
    private int _totalProjectsCount;

    [ObservableProperty]
    private int _activeProjectsCount;

    [ObservableProperty]
    private string _lastUsedProjectName = "-";

    [ObservableProperty]
    private string _projectsDirectory = string.Empty;

    // Runtime Summary (Section 3)
    [ObservableProperty]
    private string _activePhpSummary = "Not installed";

    [ObservableProperty]
    private string _nodeSummary = "Not installed";

    [ObservableProperty]
    private string _npmSummary = "Not installed";

    [ObservableProperty]
    private string _composerSummary = "Not installed";

    [ObservableProperty]
    private string _gitSummary = "Not installed";

    // Database Server / Current Server (Agent.md Section 2)
    [ObservableProperty]
    private string _currentServerName = "Not available";

    [ObservableProperty]
    private string _currentServerPort = "-";

    [ObservableProperty]
    private bool _isCurrentServerRunning;

    [ObservableProperty]
    private string _currentServerStatus = "Not available";

    [ObservableProperty]
    private string _currentServerWarning = string.Empty;

    [ObservableProperty]
    private bool _hasCurrentServerWarning;

    [ObservableProperty]
    private bool _canSwitchDatabaseServer;

    [ObservableProperty]
    private string _preferredDatabase = "MySQL";

    public ObservableCollection<ServiceInfo> Services { get; } = new();
    public ObservableCollection<ServiceInfo> WebServers { get; } = new();
    public ObservableCollection<ServiceInfo> DatabaseServers { get; } = new();
    public ObservableCollection<RuntimeInfo> Runtimes { get; } = new();
    public ObservableCollection<LogEntry> RecentLogs { get; } = new();

    private readonly IDatabaseService? _databaseService;

    public DashboardViewModel(
        IServiceManager serviceManager,
        IRuntimeDetector detector,
        IProjectService projectService,
        IConfigurationService configService,
        IPhpService phpService,
        ISslCertificateService sslService,
        ILogService logger,
        IDatabaseService? databaseService = null)
    {
        _serviceManager = serviceManager;
        _detector = detector;
        _projectService = projectService;
        _configService = configService;
        _phpService = phpService;
        _sslService = sslService;
        _logger = logger;
        _databaseService = databaseService;

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;
        _configService.ConfigurationChanged += OnConfigurationChanged;
        _phpService.ActivePhpVersionChanged += HandleActivePhpVersionChanged;
        _logger.LogAdded += OnLogAdded;

        foreach (var log in _logger.GetLogs(limit: 60))
        {
            RecentLogs.Add(log);
        }
    }

    private bool _isInitialized;

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            await RefreshDynamicStateAsync();
            return;
        }
        _isInitialized = true;
        await RefreshAsync();
    }

    public async Task RefreshDynamicStateAsync()
    {
        try
        {
            await _serviceManager.RefreshAllAsync();
            var projects = await _projectService.ScanProjectsAsync();
            TotalProjectsCount = projects.Count;
            ActiveProjectsCount = projects.Count(p => p.IsRunning);
            LastUsedProjectName = projects.OrderByDescending(p => p.LastModified).FirstOrDefault()?.Name ?? "-";
            await UpdateCurrentDatabaseResolutionAsync();
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
        catch { }
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
            await _sslService.RegenerateAllCertificatesAsync(sslDir);

            IsRootCaTrusted = _sslService.IsRootCertificateTrusted(sslDir);
            OnPropertyChanged(nameof(IsRootCaUntrusted));

            StatusMessage = IsRootCaTrusted 
                ? "Loka Root CA installed and trusted successfully by Windows!"
                : "Certificate installation request submitted.";

            try
            {
                await _serviceManager.RestartServiceAsync(ServiceType.Apache);
            }
            catch { }
        }
        catch (Exception ex)
        {
            _logger.LogError("Dashboard", "Failed trusting Root CA", ex);
            StatusMessage = $"Failed to install Root CA: {ex.Message}";
        }
        finally
        {
            IsSslBusy = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        StatusMessage = "Refreshing environment status...";

        try
        {
            EnvironmentPath = _configService.Config.Paths.RootPath;

            var sslDir = Path.Combine(_configService.Config.Paths.ConfigPath, "ssl");
            IsRootCaTrusted = _sslService.IsRootCertificateTrusted(sslDir);
            OnPropertyChanged(nameof(IsRootCaUntrusted));

            var hour = DateTime.Now.Hour;
            Greeting = hour < 12 ? "Good morning" : (hour < 18 ? "Good afternoon" : "Good evening");
            ActivePhpVersion = !string.IsNullOrEmpty(_configService.Config.Php.ActiveVersion) ? _configService.Config.Php.ActiveVersion : "8.4";

            // 1. Refresh Services
            await _serviceManager.RefreshAllAsync();
            Services.Clear();
            WebServers.Clear();
            DatabaseServers.Clear();
            foreach (var svc in _serviceManager.GetAllServices().OrderBy(s => GetServiceSortOrder(s.Type)))
            {
                if (string.IsNullOrEmpty(svc.ConfigPath))
                {
                    svc.ConfigPath = svc.Type switch
                    {
                        ServiceType.Apache => Path.Combine(_configService.Config.Paths.EtcPath, "apache", "httpd.conf"),
                        ServiceType.Nginx => Path.Combine(_configService.Config.Paths.BinPath, "nginx", "conf", "nginx.conf"),
                        ServiceType.MySQL => Path.Combine(_configService.Config.Paths.EtcPath, "mysql", "my.ini"),
                        ServiceType.MariaDB => Path.Combine(_configService.Config.Paths.EtcPath, "mariadb", "my.ini"),
                        ServiceType.PostgreSQL => Path.Combine(_configService.Config.Paths.EtcPath, "postgresql", "postgresql.conf"),
                        _ => Path.Combine(_configService.Config.Paths.EtcPath, $"{svc.Name.ToLowerInvariant()}.conf")
                    };
                }

                if (string.IsNullOrEmpty(svc.LogPath))
                {
                    svc.LogPath = svc.Type switch
                    {
                        ServiceType.Apache => Path.Combine(_configService.Config.Paths.LogsPath, "apache", "apache_error.log"),
                        ServiceType.Nginx => Path.Combine(_configService.Config.Paths.LogsPath, "nginx", "error.log"),
                        ServiceType.MySQL => Path.Combine(_configService.Config.Paths.LogsPath, "mysql", "mysql_error.log"),
                        ServiceType.MariaDB => Path.Combine(_configService.Config.Paths.LogsPath, "mariadb", "mariadb_error.log"),
                        ServiceType.PostgreSQL => Path.Combine(_configService.Config.Paths.LogsPath, "postgresql", "postgresql.log"),
                        _ => Path.Combine(_configService.Config.Paths.LogsPath, $"{svc.Name.ToLowerInvariant()}.log")
                    };
                }

                if (svc.Type == ServiceType.Apache || svc.Type == ServiceType.Nginx)
                {
                    WebServers.Add(svc);
                }
                else if (svc.Type == ServiceType.MySQL || svc.Type == ServiceType.MariaDB || svc.Type == ServiceType.PostgreSQL)
                {
                    svc.ConnectionStatus = svc.IsRunning ? "Connected" : (svc.State == ServiceState.Error ? "Error" : "Stopped");
                    DatabaseServers.Add(svc);
                }

                Services.Add(svc);
            }

            // 2. Refresh Runtimes
            var detectedRuntimes = await _detector.DetectAllRuntimesAsync();
            Runtimes.Clear();
            foreach (var rt in detectedRuntimes)
            {
                Runtimes.Add(rt);
            }

            // Runtime Summaries
            var php = detectedRuntimes.FirstOrDefault(r => r.Name.Equals("PHP", StringComparison.OrdinalIgnoreCase));
            ActivePhpSummary = php != null && php.IsInstalled ? $"PHP {php.Version}" : "Not installed";

            var node = detectedRuntimes.FirstOrDefault(r => r.Name.Equals("Node.js", StringComparison.OrdinalIgnoreCase));
            NodeSummary = node != null && node.IsInstalled ? $"Node.js {node.Version}" : "Not installed";

            var composer = detectedRuntimes.FirstOrDefault(r => r.Name.Equals("Composer", StringComparison.OrdinalIgnoreCase));
            ComposerSummary = composer != null && composer.IsInstalled ? $"Composer {composer.Version}" : "Not installed";

            var git = detectedRuntimes.FirstOrDefault(r => r.Name.Equals("Git", StringComparison.OrdinalIgnoreCase));
            GitSummary = git != null && git.IsInstalled ? $"Git {git.Version}" : "Not installed";

            // Check npm
            NpmSummary = node != null && node.IsInstalled ? "npm (active)" : "Not installed";

            // 3. Refresh Projects
            ProjectsDirectory = _configService.Config.Paths.ProjectsPath;
            var projects = await _projectService.ScanProjectsAsync();
            TotalProjectsCount = projects.Count;
            ActiveProjectsCount = projects.Count(p => p.IsRunning);
            LastUsedProjectName = projects.OrderByDescending(p => p.LastModified).FirstOrDefault()?.Name ?? "-";
            await UpdateCurrentDatabaseResolutionAsync();

            StatusMessage = "Environment overview updated.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError("Dashboard", "Failed to refresh dashboard", ex);
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }

    [RelayCommand]
    public async Task StartServiceAsync(ServiceInfo? service)
    {
        if (service == null || service.IsBusy) return;
        service.IsBusy = true;
        StatusMessage = $"Starting {service.Name}...";
        try
        {
            var ok = await _serviceManager.StartServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} started." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to start {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start {service.Name}: {ex.Message}";
        }
        finally
        {
            service.IsBusy = false;
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }

    [RelayCommand]
    public async Task StopServiceAsync(ServiceInfo? service)
    {
        if (service == null || service.IsBusy) return;
        service.IsBusy = true;
        StatusMessage = $"Stopping {service.Name}...";
        try
        {
            var ok = await _serviceManager.StopServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} stopped." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to stop {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to stop {service.Name}: {ex.Message}";
        }
        finally
        {
            service.IsBusy = false;
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }

    [RelayCommand]
    public async Task RestartServiceAsync(ServiceInfo? service)
    {
        if (service == null || service.IsBusy) return;
        service.IsBusy = true;
        StatusMessage = $"Restarting {service.Name}...";
        try
        {
            var ok = await _serviceManager.RestartServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} restarted." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to restart {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restart {service.Name}: {ex.Message}";
        }
        finally
        {
            service.IsBusy = false;
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }

    public bool HasRunningServices => Services.Any(s => s.IsRunning);
    public string GlobalActionText => HasRunningServices ? "Stop All" : "Start All";

    [RelayCommand]
    public async Task ToggleAllServicesAsync()
    {
        if (HasRunningServices)
        {
            await StopAllAsync();
        }
        else
        {
            await StartAllAsync();
        }
    }

    [RelayCommand]
    public async Task ToggleServiceAsync(ServiceInfo? service)
    {
        if (service == null || service.IsBusy) return;
        if (service.IsRunning)
        {
            await StopServiceAsync(service);
        }
        else
        {
            await StartServiceAsync(service);
        }
        OnPropertyChanged(nameof(HasRunningServices));
        OnPropertyChanged(nameof(GlobalActionText));
    }

    [RelayCommand]
    public async Task StartAllAsync()
    {
        StatusMessage = "Starting configured Web, PHP-CGI, and Database services...";
        try
        {
            var ok = await _serviceManager.StartAllAsync();
            StatusMessage = ok 
                ? "Configured services started successfully in proper sequence."
                : "One or more services encountered an issue during startup. Check services log.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Start All error: {ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }

    [RelayCommand]
    public async Task StopAllAsync()
    {
        StatusMessage = "Stopping all services...";
        try
        {
            await _serviceManager.StopAllAsync();
            StatusMessage = "All services stopped.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Stop All error: {ex.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        }
    }


    [RelayCommand]
    public void OpenConfig(ServiceInfo? service)
    {
        if (service == null) return;
        try
        {
            var dir = Path.GetDirectoryName(service.ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(service.ConfigPath))
            {
                File.WriteAllText(service.ConfigPath, $"# Loka Configuration for {service.Name}\n# Port: {service.Port}\n");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{service.ConfigPath}\"",
                UseShellExecute = true
            });
            StatusMessage = $"Opened configuration for {service.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening config: {ex.Message}";
            _logger.LogError("Dashboard", $"Failed opening config for {service.Name}", ex);
        }
    }

    [RelayCommand]
    public void OpenLogs(ServiceInfo? service)
    {
        if (service == null) return;
        try
        {
            var dir = Path.GetDirectoryName(service.LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (!File.Exists(service.LogPath))
            {
                File.WriteAllText(service.LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] [{service.Name}] Log initialized by Loka.\n");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{service.LogPath}\"",
                UseShellExecute = true
            });
            StatusMessage = $"Opened log file for {service.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening log: {ex.Message}";
            _logger.LogError("Dashboard", $"Failed opening log for {service.Name}", ex);
        }
    }

    [RelayCommand]
    public void OpenConfigFolder(ServiceInfo? service)
    {
        if (service == null) return;
        var dir = !string.IsNullOrEmpty(service.ConfigPath) ? Path.GetDirectoryName(service.ConfigPath) : null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            dir = _configService.Config.Paths.EtcPath;
        }
        OpenFolderSafe(dir);
    }

    [RelayCommand]
    public void OpenLogFolder(ServiceInfo? service)
    {
        if (service == null) return;
        var dir = !string.IsNullOrEmpty(service.LogPath) ? Path.GetDirectoryName(service.LogPath) : null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            dir = _configService.Config.Paths.LogsPath;
        }
        OpenFolderSafe(dir);
    }

    [RelayCommand]
    public void OpenServiceFolder(ServiceInfo? service)
    {
        if (service == null) return;
        var dir = !string.IsNullOrEmpty(service.ExecutablePath) ? Path.GetDirectoryName(service.ExecutablePath) : null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            dir = Path.Combine(_configService.Config.Paths.BinPath, service.Name.ToLowerInvariant());
        }
        OpenFolderSafe(dir);
    }

    private void OpenFolderSafe(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        try
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Dashboard", $"Failed opening folder '{path}'", ex);
        }
    }

    [RelayCommand]
    public void ClearLogs()
    {
        RecentLogs.Clear();
        _logger.Clear();
        StatusMessage = "Activity logs cleared.";
    }

    private void OnLogAdded(LogEntry log)
    {
        UiDispatcher.Run(() =>
        {
            RecentLogs.Add(log);
            while (RecentLogs.Count > 100)
            {
                RecentLogs.RemoveAt(0);
            }
        });
    }

    private static int GetServiceSortOrder(ServiceType type) => type switch
    {
        ServiceType.Apache => 1,
        ServiceType.MySQL => 2,
        ServiceType.MariaDB => 3,
        ServiceType.PostgreSQL => 4,
        ServiceType.Nginx => 5,
        _ => 99
    };

    [RelayCommand]
    public async Task NavigateToPhpMyAdminAsync()
    {
        StatusMessage = "Checking phpMyAdmin prerequisites...";
        try
        {
            int serverIndex = 1;
            if (_databaseService != null)
            {
                var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
                serverIndex = resolution.PhpMyAdminServerIndex;

                if (string.IsNullOrEmpty(resolution.ActiveServerName))
                {
                    var dbType = string.Equals(_configService.Config.Services.PreferredDatabase, "MariaDB", StringComparison.OrdinalIgnoreCase)
                        ? ServiceType.MariaDB
                        : ServiceType.MySQL;

                    bool shouldStartDb = false;
                    if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                    {
                        var confirm = System.Windows.MessageBox.Show(
                            $"{dbType} is currently stopped. phpMyAdmin requires a database server to be running.\n\nWould you like to start {dbType} now?",
                            $"Start {dbType} for phpMyAdmin",
                            System.Windows.MessageBoxButton.YesNo,
                            System.Windows.MessageBoxImage.Question);
                        shouldStartDb = confirm == System.Windows.MessageBoxResult.Yes;
                    }

                    if (shouldStartDb)
                    {
                        StatusMessage = $"Starting {dbType} for phpMyAdmin...";
                        var okDb = await _serviceManager.StartServiceAsync(dbType);
                        if (!okDb)
                        {
                            StatusMessage = $"Could not start {dbType}. Check database logs.";
                            return;
                        }
                        await Task.Delay(400);
                        resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
                        serverIndex = resolution.PhpMyAdminServerIndex;
                    }
                    else
                    {
                        StatusMessage = "Database server is stopped. Please start MySQL or MariaDB to use phpMyAdmin.";
                        return;
                    }
                }
            }

            // Ensure PHP CGI
            await _serviceManager.EnsurePhpCgiRunningAsync();

            // Step 3: Determine and ensure active/configured web server (Apache or Nginx)
            var apSvc = _serviceManager.GetService(ServiceType.Apache);
            var ngSvc = _serviceManager.GetService(ServiceType.Nginx);
            bool isApRunning = apSvc != null && apSvc.IsRunning;
            bool isNgRunning = ngSvc != null && ngSvc.IsRunning;

            var preferredWeb = _configService.Config.Services?.PreferredWebServer;
            bool preferApache = string.Equals(preferredWeb, "Apache", StringComparison.OrdinalIgnoreCase);

            ServiceType targetWebType;
            if (isApRunning && !isNgRunning)
            {
                targetWebType = ServiceType.Apache;
            }
            else if (isNgRunning && !isApRunning)
            {
                targetWebType = ServiceType.Nginx;
            }
            else if (isApRunning && isNgRunning)
            {
                targetWebType = preferApache ? ServiceType.Apache : ServiceType.Nginx;
            }
            else
            {
                targetWebType = preferApache ? ServiceType.Apache : ServiceType.Nginx;
            }

            var webService = _serviceManager.GetService(targetWebType);
            var serverDisplayName = targetWebType == ServiceType.Nginx ? "Nginx" : "Apache";

            if (webService == null || !webService.IsRunning)
            {
                bool shouldStart = false;
                if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                {
                    var confirm = System.Windows.MessageBox.Show(
                        $"{serverDisplayName} web server is stopped. phpMyAdmin requires a web server to be running.\n\nWould you like to start {serverDisplayName} now?",
                        $"Start {serverDisplayName} for phpMyAdmin",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    shouldStart = confirm == System.Windows.MessageBoxResult.Yes;
                }

                if (shouldStart)
                {
                    StatusMessage = $"Starting {serverDisplayName} for phpMyAdmin...";
                    var okWeb = await _serviceManager.StartServiceAsync(targetWebType);
                    if (!okWeb)
                    {
                        var lastErr = webService?.LastError ?? "Port conflict or startup failure";
                        StatusMessage = $"Could not start {serverDisplayName}: {lastErr}.";
                        return;
                    }
                    webService = _serviceManager.GetService(targetWebType);
                }
                else
                {
                    StatusMessage = $"{serverDisplayName} is stopped. phpMyAdmin requires {serverDisplayName} web server to run.";
                    return;
                }
            }

            var port = webService?.Port ?? (targetWebType == ServiceType.Nginx
                ? (_configService.Config.Services?.NginxPort ?? 80)
                : (_configService.Config.Services?.ApachePort ?? 80));
            if (port <= 0) port = 80;

            var url = port == 80 
                ? $"http://localhost/phpmyadmin/index.php?route=/&server={serverIndex}" 
                : $"http://localhost:{port}/phpmyadmin/index.php?route=/&server={serverIndex}";

            // Test HTTP response before launching
            StatusMessage = "Verifying phpMyAdmin HTTP response...";
            using (var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) })
            {
                try
                {
                    var response = await httpClient.GetAsync(url);
                    if ((int)response.StatusCode >= 400)
                    {
                        StatusMessage = $"phpMyAdmin returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Check server configuration.";
                        _logger.LogError("Dashboard", $"HTTP check on {url} returned {(int)response.StatusCode}: {response.ReasonPhrase}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Dashboard", $"HTTP check notice on {url}: {ex.Message}");
                    if (System.Windows.Application.Current?.MainWindow != null && System.Windows.Application.Current.MainWindow.IsVisible)
                    {
                        StatusMessage = $"phpMyAdmin connection refused or timed out: {ex.Message}";
                        _logger.LogError("Dashboard", $"HTTP check failed on {url}", ex);
                        return;
                    }
                }
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Dashboard", $"Could not launch browser: {ex.Message}");
            }
            StatusMessage = $"phpMyAdmin launched at {url}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open phpMyAdmin: {ex.Message}";
            _logger.LogError("Dashboard", "Failed opening phpMyAdmin", ex);
        }
    }

    public void NavigateToPhpMyAdmin()
    {
        _ = NavigateToPhpMyAdminAsync();
    }

    public async Task UpdateCurrentDatabaseResolutionAsync()
    {
        if (_databaseService == null) return;
        try
        {
            var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
            void Apply()
            {
                if (!string.IsNullOrEmpty(resolution.ActiveServerName))
                {
                    CurrentServerName = resolution.ActiveServerName;
                    IsCurrentServerRunning = true;
                    CurrentServerStatus = "Running";
                    CurrentServerPort = resolution.ActivePort > 0 ? resolution.ActivePort.ToString() : "-";
                }
                else
                {
                    CurrentServerName = "Not available";
                    IsCurrentServerRunning = false;
                    CurrentServerStatus = "Not available";
                    CurrentServerPort = "-";
                }

                CanSwitchDatabaseServer = resolution.MySqlStatus.IsHealthy && resolution.MariaDbStatus.IsHealthy;
                PreferredDatabase = _configService.Config.Services.PreferredDatabase ?? "MySQL";

                CurrentServerWarning = string.Empty;
                HasCurrentServerWarning = false;
            }

            UiDispatcher.Run(Apply);
        }
        catch (Exception ex)
        {
            _logger.LogError("Dashboard", "Error resolving current database server", ex);
        }
    }

    [RelayCommand]
    public async Task SetCurrentServerAsync(string? serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName) || _databaseService == null) return;

        StatusMessage = $"Switching active database server to {serverName}...";
        try
        {
            await _databaseService.SetCurrentDatabaseServerAsync(serverName);
            PreferredDatabase = serverName;
            if (_configService.Config.Services.PreferredDatabase != serverName)
            {
                _configService.Config.Services.PreferredDatabase = serverName;
                await _configService.SaveAsync();
            }
            await UpdateCurrentDatabaseResolutionAsync();
            StatusMessage = $"Active database server set to {serverName}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to switch server: {ex.Message}";
            _logger.LogError("Dashboard", $"Failed to set active database server to {serverName}", ex);
        }
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        UiDispatcher.Run(() =>
        {
            void UpdateInCollection(ObservableCollection<ServiceInfo> collection)
            {
                var existing = collection.FirstOrDefault(s => s.Type == info.Type);
                if (existing != null)
                {
                    existing.State = info.State;
                    existing.Port = info.Port;
                    existing.ProcessId = info.ProcessId;
                    existing.LastError = info.LastError;
                    existing.Version = info.Version;
                    existing.ConnectionStatus = info.IsRunning ? "Connected" : (info.State == ServiceState.Error ? "Error" : "Stopped");
                }
            }

            UpdateInCollection(Services);
            UpdateInCollection(WebServers);
            UpdateInCollection(DatabaseServers);

            if (!string.IsNullOrEmpty(info.LastError))
            {
                StatusMessage = info.LastError;
            }
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        });

        if (info.Type == ServiceType.MySQL || info.Type == ServiceType.MariaDB)
        {
            _ = UpdateCurrentDatabaseResolutionAsync();
        }
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            EnvironmentPath = config.Paths.RootPath;
            ProjectsDirectory = config.Paths.ProjectsPath;
            ActivePhpVersion = !string.IsNullOrEmpty(config.Php.ActiveVersion) ? config.Php.ActiveVersion : "8.4";
        });
        _ = UpdateCurrentDatabaseResolutionAsync();
    }

    private void HandleActivePhpVersionChanged(string newVersion)
    {
        UiDispatcher.Run(() =>
        {
            ActivePhpVersion = newVersion;
            ActivePhpSummary = $"PHP {newVersion}";
            var phpRt = Runtimes.FirstOrDefault(r => r.Name.Equals("PHP", StringComparison.OrdinalIgnoreCase));
            if (phpRt != null)
            {
                phpRt.Version = newVersion;
            }
        });
    }

    private static string GetSystemMemorySummary()
    {
        try
        {
            var memInfo = GC.GetGCMemoryInfo();
            if (memInfo.TotalAvailableMemoryBytes > 0)
            {
                var gb = memInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
                return $"{gb:F1} GB Total Available";
            }
        }
        catch { }
        return "Windows System Memory";
    }
}
