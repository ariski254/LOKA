using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class PhpMyAdminViewModel : ObservableObject
{
    private readonly IServiceManager _serviceManager;
    private readonly IConfigurationService _configService;
    private readonly IPortService _portService;
    private readonly IDatabaseService _databaseService;
    private readonly ILogService _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Checking database service...";

    [ObservableProperty]
    private string _currentServerName = "None / Stopped";

    [ObservableProperty]
    private string _currentServerPort = "-";

    [ObservableProperty]
    private bool _isCurrentServerRunning;

    [ObservableProperty]
    private string _currentServerStatus = "Stopped";

    [ObservableProperty]
    private bool _canSwitchServer;

    [ObservableProperty]
    private bool _isMySqlRunning;

    [ObservableProperty]
    private string _mySqlStatus = "Checking...";

    [ObservableProperty]
    private string _mySqlPort = "3306";

    [ObservableProperty]
    private string _mySqlVersion = "-";

    [ObservableProperty]
    private bool _isMariaDbRunning;

    [ObservableProperty]
    private string _mariaDbStatus = "Checking...";

    [ObservableProperty]
    private string _mariaDbPort = "3307";

    [ObservableProperty]
    private string _mariaDbVersion = "-";

    [ObservableProperty]
    private bool _isPhpMyAdminAvailable;

    [ObservableProperty]
    private string _phpMyAdminUrl = "http://localhost/phpmyadmin";

    [ObservableProperty]
    private string _phpMyAdminStatus = "Checking...";

    [ObservableProperty]
    private string _databaseHost = "127.0.0.1";

    [ObservableProperty]
    private string _databaseRootUser = "root";

    [ObservableProperty]
    private string _databaseRootPassword = "(No password)";

    [ObservableProperty]
    private int _activePhpMyAdminServerIndex = 1;

    public PhpMyAdminViewModel(
        IServiceManager serviceManager,
        IConfigurationService configService,
        IPortService portService,
        IDatabaseService databaseService,
        ILogService logger)
    {
        _serviceManager = serviceManager;
        _configService = configService;
        _portService = portService;
        _databaseService = databaseService;
        _logger = logger;

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;

        UpdateCredentials();
    }

    private void UpdateCredentials()
    {
        var cfg = _configService.Config;
        var preferredWeb = cfg.Services?.PreferredWebServer;
        bool isNginxPreferred = string.Equals(preferredWeb, "Nginx", StringComparison.OrdinalIgnoreCase);

        var apacheSvc = _serviceManager.GetService(ServiceType.Apache);
        var nginxSvc = _serviceManager.GetService(ServiceType.Nginx);

        int webPort;
        if (apacheSvc != null && apacheSvc.IsRunning && (nginxSvc == null || !nginxSvc.IsRunning))
        {
            webPort = apacheSvc.Port > 0 ? apacheSvc.Port : (cfg.Services?.ApachePort ?? 80);
        }
        else if (nginxSvc != null && nginxSvc.IsRunning && (apacheSvc == null || !apacheSvc.IsRunning))
        {
            webPort = nginxSvc.Port > 0 ? nginxSvc.Port : (cfg.Services?.NginxPort ?? 80);
        }
        else
        {
            webPort = isNginxPreferred
                ? (nginxSvc?.Port > 0 ? nginxSvc.Port : (cfg.Services?.NginxPort ?? 80))
                : (apacheSvc?.Port > 0 ? apacheSvc.Port : (cfg.Services?.ApachePort ?? 80));
        }

        if (webPort != 80 && webPort > 0)
        {
            PhpMyAdminUrl = $"http://localhost:{webPort}/phpmyadmin";
        }
        else
        {
            PhpMyAdminUrl = "http://localhost/phpmyadmin";
        }

        DatabaseHost = cfg.Database?.Host ?? "127.0.0.1";
        DatabaseRootUser = cfg.Database?.RootUser ?? "root";
        DatabaseRootPassword = string.IsNullOrEmpty(cfg.Database?.RootPassword) ? "(None / No Password)" : cfg.Database.RootPassword;
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking database status...";
        try
        {
            UpdateCredentials();
            await _serviceManager.RefreshAllAsync();
            await UpdateDatabaseStatusesAsync();
            CheckPhpMyAdmin();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Initialization error", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task UpdateDatabaseStatusesAsync()
    {
        var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();

        // MySQL Status
        IsMySqlRunning = resolution.MySqlStatus.IsHealthy;
        MySqlStatus = resolution.MySqlStatus.StatusText;
        MySqlPort = resolution.MySqlStatus.ActualPort > 0 
            ? resolution.MySqlStatus.ActualPort.ToString() 
            : (_configService.Config.Services.MySqlPort > 0 ? _configService.Config.Services.MySqlPort.ToString() : "3306");
        var mySqlSvc = _serviceManager.GetService(ServiceType.MySQL);
        MySqlVersion = !string.IsNullOrEmpty(resolution.MySqlStatus.Version) && resolution.MySqlStatus.Version != "-"
            ? resolution.MySqlStatus.Version
            : (!string.IsNullOrEmpty(mySqlSvc?.Version) && mySqlSvc.Version != "Not installed" ? mySqlSvc.Version : "-");

        // MariaDB Status
        IsMariaDbRunning = resolution.MariaDbStatus.IsHealthy;
        MariaDbStatus = resolution.MariaDbStatus.StatusText;
        MariaDbPort = resolution.MariaDbStatus.ActualPort > 0 
            ? resolution.MariaDbStatus.ActualPort.ToString() 
            : (_configService.Config.Services.MariaDbPort > 0 ? _configService.Config.Services.MariaDbPort.ToString() : "3307");
        var mariaDbSvc = _serviceManager.GetService(ServiceType.MariaDB);
        MariaDbVersion = !string.IsNullOrEmpty(resolution.MariaDbStatus.Version) && resolution.MariaDbStatus.Version != "-"
            ? resolution.MariaDbStatus.Version
            : (!string.IsNullOrEmpty(mariaDbSvc?.Version) && mariaDbSvc.Version != "Not installed" ? mariaDbSvc.Version : "-");

        // Current Active Server
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

        ActivePhpMyAdminServerIndex = resolution.PhpMyAdminServerIndex;
        CanSwitchServer = resolution.MySqlStatus.IsHealthy && resolution.MariaDbStatus.IsHealthy;
        StatusMessage = resolution.Message;
    }

    [RelayCommand]
    public async Task SetCurrentServerAsync(string serverName)
    {
        IsLoading = true;
        StatusMessage = $"Switching active database server to {serverName}...";
        try
        {
            await _databaseService.SetCurrentDatabaseServerAsync(serverName);
            await UpdateDatabaseStatusesAsync();
            StatusMessage = $"Active server switched to {serverName}. phpMyAdmin reconfigured.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed switching server: {ex.Message}";
            _logger.LogError("PhpMyAdmin", $"Failed to set active server to {serverName}", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task StartMySqlAsync()
    {
        IsLoading = true;
        StatusMessage = "Starting MySQL...";
        try
        {
            await _serviceManager.StartServiceAsync(ServiceType.MySQL);
            await _serviceManager.RefreshAllAsync();
            await UpdateDatabaseStatusesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start MySQL: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Failed to start MySQL", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task StopMySqlAsync()
    {
        IsLoading = true;
        StatusMessage = "Stopping MySQL...";
        try
        {
            await _serviceManager.StopServiceAsync(ServiceType.MySQL);
            await _serviceManager.RefreshAllAsync();
            await UpdateDatabaseStatusesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to stop MySQL: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Failed to stop MySQL", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleMySqlAsync()
    {
        if (IsMySqlRunning)
            await StopMySqlAsync();
        else
            await StartMySqlAsync();
    }

    [RelayCommand]
    public async Task StartMariaDbAsync()
    {
        IsLoading = true;
        StatusMessage = "Starting MariaDB...";
        try
        {
            await _serviceManager.StartServiceAsync(ServiceType.MariaDB);
            await _serviceManager.RefreshAllAsync();
            await UpdateDatabaseStatusesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to start MariaDB: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Failed to start MariaDB", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task StopMariaDbAsync()
    {
        IsLoading = true;
        StatusMessage = "Stopping MariaDB...";
        try
        {
            await _serviceManager.StopServiceAsync(ServiceType.MariaDB);
            await _serviceManager.RefreshAllAsync();
            await UpdateDatabaseStatusesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to stop MariaDB: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Failed to stop MariaDB", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ToggleMariaDbAsync()
    {
        if (IsMariaDbRunning)
            await StopMariaDbAsync();
        else
            await StartMariaDbAsync();
    }

    [RelayCommand]
    public async Task OpenInBrowserAsync()
    {
        await OpenPhpMyAdminAsync();
    }

    [RelayCommand]
    public async Task OpenPhpMyAdminAsync()
    {
        IsLoading = true;
        try
        {
            // Step 1: Ensure at least one database server is running
            var resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
            if (string.IsNullOrEmpty(resolution.ActiveServerName))
            {
                var targetDb = string.Equals(_configService.Config.Services.PreferredDatabase, "MariaDB", StringComparison.OrdinalIgnoreCase)
                    ? ServiceType.MariaDB
                    : ServiceType.MySQL;

                bool shouldStartDb = false;
                if (IsGuiActive())
                {
                    var confirm = System.Windows.MessageBox.Show(
                        $"{targetDb} is currently stopped. phpMyAdmin requires a database server to be running.\n\nWould you like to start {targetDb} now?",
                        $"Start {targetDb} for phpMyAdmin",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);
                    shouldStartDb = confirm == System.Windows.MessageBoxResult.Yes;
                }

                if (shouldStartDb)
                {
                    StatusMessage = $"Starting {targetDb} for phpMyAdmin...";
                    _logger.LogInfo("PhpMyAdmin", $"Starting {targetDb} for phpMyAdmin...");
                    var startedDb = await _serviceManager.StartServiceAsync(targetDb);
                    if (!startedDb)
                    {
                        StatusMessage = $"Could not start {targetDb}. Check database logs.";
                        _logger.LogError("PhpMyAdmin", $"Database {targetDb} failed to start for phpMyAdmin.");
                        return;
                    }
                    await Task.Delay(500);
                    resolution = await _databaseService.ResolveCurrentDatabaseServerAsync();
                }
                else
                {
                    StatusMessage = "Database server is stopped. Please start MySQL or MariaDB to use phpMyAdmin.";
                    return;
                }
            }

            // Step 2: Check PHP & ensure FastCGI daemon is running on port 9000
            StatusMessage = "Checking PHP FastCGI daemon on port 9000...";
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
                if (IsGuiActive())
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
                    _logger.LogInfo("PhpMyAdmin", $"Starting {serverDisplayName} for phpMyAdmin...");
                    var startedWeb = await _serviceManager.StartServiceAsync(targetWebType);
                    if (!startedWeb)
                    {
                        var lastErr = webService?.LastError ?? "Port conflict or startup failure";
                        StatusMessage = $"Could not start {serverDisplayName}: {lastErr}.";
                        _logger.LogError("PhpMyAdmin", $"{serverDisplayName} failed to start for phpMyAdmin: {lastErr}");
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

            var targetPort = webService?.Port ?? (targetWebType == ServiceType.Nginx
                ? (_configService.Config.Services?.NginxPort ?? 80)
                : (_configService.Config.Services?.ApachePort ?? 80));
            if (targetPort <= 0) targetPort = 80;
            var sw = Stopwatch.StartNew();
            bool isPortListening = false;
            while (sw.ElapsedMilliseconds < 3000)
            {
                var check = await _portService.CheckPortAsync(targetPort);
                if (check.IsOccupied)
                {
                    isPortListening = true;
                    break;
                }
                await Task.Delay(200);
            }

            if (!isPortListening && webService?.IsRunning != true)
            {
                StatusMessage = $"{serverDisplayName} started but port {targetPort} is not accepting connections.";
                _logger.LogError("PhpMyAdmin", $"{serverDisplayName} port {targetPort} not accepting connections.");
                return;
            }

            // Step 5: Test real HTTP endpoint before launching browser
            var serverIndex = resolution.PhpMyAdminServerIndex;
            var testUrl = targetPort == 80 
                ? $"http://localhost/phpmyadmin/index.php?route=/&server={serverIndex}" 
                : $"http://localhost:{targetPort}/phpmyadmin/index.php?route=/&server={serverIndex}";

            bool isGuiActive = IsGuiActive();

            if (isGuiActive)
            {
                StatusMessage = "Verifying phpMyAdmin HTTP response...";
                using (var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) })
                {
                    try
                    {
                        var response = await httpClient.GetAsync(testUrl);
                        if ((int)response.StatusCode >= 400)
                        {
                            StatusMessage = $"phpMyAdmin returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). Check server configuration.";
                            _logger.LogError("PhpMyAdmin", $"HTTP check on {testUrl} returned {(int)response.StatusCode}: {response.ReasonPhrase}");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        StatusMessage = $"phpMyAdmin connection refused or timed out: {ex.Message}";
                        _logger.LogError("PhpMyAdmin", $"HTTP check failed on {testUrl}", ex);
                        return;
                    }
                }
            }

            // Launch browser directly targeting active server index
            PhpMyAdminUrl = testUrl;
            await UpdateDatabaseStatusesAsync();

            if (isGuiActive)
            {
                StatusMessage = $"Opening phpMyAdmin at {PhpMyAdminUrl}...";
                Process.Start(new ProcessStartInfo(PhpMyAdminUrl) { UseShellExecute = true });
            }
            StatusMessage = $"phpMyAdmin launched (Active: {resolution.ActiveServerName}).";
            _logger.LogInfo("PhpMyAdmin", $"phpMyAdmin launched successfully at {PhpMyAdminUrl}");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error opening phpMyAdmin: {ex.Message}";
            _logger.LogError("PhpMyAdmin", "Failed to launch phpMyAdmin workflow", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await InitializeAsync();
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        if (info.Type == ServiceType.MySQL || info.Type == ServiceType.MariaDB)
        {
            UiDispatcher.Run(() => { _ = UpdateDatabaseStatusesAsync(); });
        }
    }

    private void CheckPhpMyAdmin()
    {
        var url = PhpMyAdminUrl;
        IsPhpMyAdminAvailable = !string.IsNullOrWhiteSpace(url);
        PhpMyAdminStatus = IsPhpMyAdminAvailable ? $"Available at {url}" : "Not configured";
    }

    private static bool IsGuiActive()
    {
        var app = System.Windows.Application.Current;
        if (app == null) return false;
        var dispatcher = app.Dispatcher;
        if (dispatcher == null || !dispatcher.Thread.IsAlive || dispatcher.HasShutdownStarted) return false;

        if (dispatcher.CheckAccess())
        {
            return app.MainWindow != null && app.MainWindow.IsVisible;
        }

        try
        {
            var op = dispatcher.InvokeAsync(() => app.MainWindow != null && app.MainWindow.IsVisible);
            return op.Task.Wait(TimeSpan.FromMilliseconds(200)) && op.Task.Result;
        }
        catch
        {
            return false;
        }
    }
}
