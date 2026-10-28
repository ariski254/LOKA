using System.Diagnostics;
using Loka.Core.Enums;
using Loka.Core.Exceptions;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Runtime.Services;

public class ServiceManager : IServiceManager
{
    private readonly IProcessManager _processManager;
    private readonly IPortService _portService;
    private readonly IConfigurationService _configService;
    private readonly IRuntimeDetector _detector;
    private readonly IApacheService _apacheService;
    private readonly INginxService _nginxService;
    private readonly IMySqlService _mySqlService;
    private readonly ILogService _logger;

    private readonly Dictionary<ServiceType, ServiceInfo> _services = new();

    public event Action<ServiceInfo>? ServiceStateChanged;

    public ServiceManager(
        IProcessManager processManager,
        IPortService portService,
        IConfigurationService configService,
        IRuntimeDetector detector,
        IApacheService apacheService,
        INginxService nginxService,
        IMySqlService mySqlService,
        ILogService logger)
    {
        _processManager = processManager;
        _portService = portService;
        _configService = configService;
        _detector = detector;
        _apacheService = apacheService;
        _nginxService = nginxService;
        _mySqlService = mySqlService;
        _logger = logger;

        InitializeServiceDefinitions();

        _processManager.ProcessExitedUnexpectedly += OnProcessExitedUnexpectedly;
    }

    private void InitializeServiceDefinitions()
    {
        _services[ServiceType.Apache] = new ServiceInfo
        {
            Type = ServiceType.Apache,
            Name = "Apache",
            Port = _configService.Config.Services.ApachePort,
            State = ServiceState.Stopped,
            AutoStart = _configService.Config.Services.AutoStartApache
        };

        _services[ServiceType.MySQL] = new ServiceInfo
        {
            Type = ServiceType.MySQL,
            Name = "MySQL",
            Port = _configService.Config.Services.MySqlPort,
            State = ServiceState.Stopped,
            AutoStart = _configService.Config.Services.AutoStartMySql
        };

        _services[ServiceType.Nginx] = new ServiceInfo
        {
            Type = ServiceType.Nginx,
            Name = "Nginx",
            Port = _configService.Config.Services.NginxPort,
            State = ServiceState.Stopped,
            AutoStart = _configService.Config.Services.AutoStartNginx
        };

        _services[ServiceType.MariaDB] = new ServiceInfo
        {
            Type = ServiceType.MariaDB,
            Name = "MariaDB",
            Port = _configService.Config.Services.MariaDbPort,
            State = ServiceState.Stopped
        };

        _services[ServiceType.PostgreSQL] = new ServiceInfo
        {
            Type = ServiceType.PostgreSQL,
            Name = "PostgreSQL",
            Port = _configService.Config.Services.PostgresPort,
            State = ServiceState.Stopped
        };
    }

    public IReadOnlyList<ServiceInfo> GetAllServices() => _services.Values.ToList();

    public ServiceInfo? GetService(ServiceType type)
    {
        _services.TryGetValue(type, out var service);
        return service;
    }

    public async Task<bool> StartServiceAsync(ServiceType type)
    {
        if (!_services.TryGetValue(type, out var service))
        {
            return false;
        }

        if (service.State == ServiceState.Running)
        {
            return true;
        }

        service.State = ServiceState.Starting;
        service.LastError = null;
        NotifyStateChanged(service);

        try
        {
            // Mutual exclusion for web servers (Fix.md Section 6)
            if (type == ServiceType.Apache)
            {
                var ngService = GetService(ServiceType.Nginx);
                if (ngService != null && (ngService.IsRunning || Process.GetProcessesByName("nginx").Length > 0))
                {
                    _logger.LogInfo(service.Name, "Stopping Nginx before starting Apache to ensure Apache exclusively owns web traffic...");
                    await StopServiceAsync(ServiceType.Nginx);
                    await Task.Delay(300);
                }
            }
            else if (type == ServiceType.Nginx)
            {
                var apService = GetService(ServiceType.Apache);
                if (apService != null && (apService.IsRunning || Process.GetProcessesByName("httpd").Length > 0))
                {
                    _logger.LogInfo(service.Name, "Stopping Apache before starting Nginx to ensure Nginx exclusively owns web traffic...");
                    await StopServiceAsync(ServiceType.Apache);
                    await Task.Delay(300);
                }
            }

            // 1. Port conflict pre-check
            var portStatus = await _portService.CheckPortAsync(service.Port);
            if (portStatus.IsOccupied)
            {
                if (IsProcessMatchingService(type, portStatus.ProcessName, portStatus.ProcessPath))
                {
                    service.State = ServiceState.Running;
                    service.ProcessId = portStatus.ProcessId;
                    service.StartedAt ??= DateTime.Now;
                    service.LastError = null;
                    NotifyStateChanged(service);
                    _logger.LogInfo(service.Name, $"{service.Name} is already running on port {service.Port} (PID: {portStatus.ProcessId}).");

                    if (type == ServiceType.Apache || type == ServiceType.Nginx)
                    {
                        await EnsurePhpCgiRunningAsync();
                    }
                    return true;
                }

                if ((type == ServiceType.Apache && portStatus.ProcessName.Contains("nginx", StringComparison.OrdinalIgnoreCase)) ||
                    (type == ServiceType.Nginx && (portStatus.ProcessName.Contains("httpd", StringComparison.OrdinalIgnoreCase) || portStatus.ProcessName.Contains("apache", StringComparison.OrdinalIgnoreCase))))
                {
                    _logger.LogInfo(service.Name, $"Port {service.Port} is occupied by conflicting web server '{portStatus.ProcessName}'. Freeing port {service.Port} for {service.Name}...");
                    foreach (var p in Process.GetProcessesByName("nginx"))
                    {
                        try { p.Kill(entireProcessTree: true); } catch { }
                    }
                    foreach (var p in Process.GetProcessesByName("httpd"))
                    {
                        try { p.Kill(entireProcessTree: true); } catch { }
                    }
                    await _portService.KillProcessUsingPortAsync(service.Port);
                    await Task.Delay(400);

                    // Check if port is still occupied after kill attempt (e.g. system service like IIS)
                    var checkAfterKill = await _portService.CheckPortAsync(service.Port);
                    if (checkAfterKill.IsOccupied)
                    {
                        var killErrMsg = $"Port {service.Port} is already in use by external service '{checkAfterKill.ProcessName}' (PID: {checkAfterKill.ProcessId?.ToString() ?? "Unknown"}). Please free port {service.Port} or change port in Settings.";
                        service.State = ServiceState.Error;
                        service.LastError = killErrMsg;
                        NotifyStateChanged(service);
                        _logger.LogWarning(service.Name, killErrMsg);
                        return false;
                    }
                }
                var isDbConflict = (type == ServiceType.MySQL && portStatus.ProcessName.Contains("mariadb", StringComparison.OrdinalIgnoreCase)) ||
                                   (type == ServiceType.MariaDB && portStatus.ProcessName.Contains("mysql", StringComparison.OrdinalIgnoreCase));
                var errMsg = isDbConflict
                    ? $"Port {service.Port} is already in use by '{portStatus.ProcessName}' (PID: {portStatus.ProcessId?.ToString() ?? "Unknown"}). Both MySQL and MariaDB cannot run on the same port simultaneously. Please assign distinct ports in Settings."
                    : $"Port {service.Port} is already in use by external service '{portStatus.ProcessName}' (PID: {portStatus.ProcessId?.ToString() ?? "Unknown"}). Please free port {service.Port} or change port in Settings.";
                service.State = ServiceState.Error;
                service.LastError = errMsg;
                NotifyStateChanged(service);
                _logger.LogWarning(service.Name, errMsg);
                return false;
            }

            // 2. Specific Service Pre-flight & Launch
            switch (type)
            {
                case ServiceType.Apache:
                    await StartApacheInternalAsync(service);
                    break;
                case ServiceType.MySQL:
                    await StartMySqlInternalAsync(service);
                    break;
                case ServiceType.Nginx:
                    await StartNginxInternalAsync(service);
                    break;
                case ServiceType.MariaDB:
                    await StartMariaDbInternalAsync(service);
                    break;
                case ServiceType.PostgreSQL:
                    await StartPostgresInternalAsync(service);
                    break;
                default:
                    throw new NotSupportedException($"Service type {type} is not yet configured for automatic execution.");
            }

            // Verify process running
            var proc = _processManager.GetMonitoredProcess(service.Name);
            if (proc != null && proc.IsRunning)
            {
                service.State = ServiceState.Running;
                service.ProcessId = proc.ProcessId;
                service.StartedAt = DateTime.Now;
                service.LastError = null;
                NotifyStateChanged(service);
                _logger.LogInfo(service.Name, $"{service.Name} started successfully on port {service.Port} (PID: {proc.ProcessId}).");
                return true;
            }

            // Nginx on Windows forks into background worker processes, so check system processes
            if (type == ServiceType.Nginx)
            {
                var nginxProcs = Process.GetProcessesByName("nginx");
                if (nginxProcs.Length > 0)
                {
                    service.State = ServiceState.Running;
                    service.ProcessId = nginxProcs[0].Id;
                    service.StartedAt = DateTime.Now;
                    service.LastError = null;
                    NotifyStateChanged(service);
                    _logger.LogInfo(service.Name, $"{service.Name} started successfully (PID: {nginxProcs[0].Id}).");
                    return true;
                }
            }

            // PostgreSQL started via pg_ctl runs in background workers
            if (type == ServiceType.PostgreSQL)
            {
                var pgProcs = Process.GetProcessesByName("postgres");
                if (pgProcs.Length > 0)
                {
                    service.State = ServiceState.Running;
                    service.ProcessId = pgProcs[0].Id;
                    service.StartedAt = DateTime.Now;
                    service.LastError = null;
                    NotifyStateChanged(service);
                    _logger.LogInfo(service.Name, $"{service.Name} started successfully (PID: {pgProcs[0].Id}).");
                    return true;
                }
            }

            // Check if process is running via port
            var portCheck = await _portService.CheckPortAsync(service.Port);
            if (portCheck.IsOccupied && IsProcessMatchingService(type, portCheck.ProcessName, portCheck.ProcessPath))
            {
                service.State = ServiceState.Running;
                service.ProcessId = portCheck.ProcessId;
                service.StartedAt = DateTime.Now;
                service.LastError = null;
                NotifyStateChanged(service);
                _logger.LogInfo(service.Name, $"{service.Name} started successfully on port {service.Port} (PID: {portCheck.ProcessId}).");
                return true;
            }

            service.State = ServiceState.Error;
            service.LastError = "Process failed to stay running.";
            NotifyStateChanged(service);
            return false;
        }
        catch (FileNotFoundException)
        {
            service.State = ServiceState.Stopped;
            service.LastError = $"{service.Name} is not installed (executable not found).";
            NotifyStateChanged(service);
            _logger.LogWarning(service.Name, service.LastError);
            return false;
        }
        catch (Exception ex)
        {
            service.State = ServiceState.Error;
            service.LastError = ex.Message;
            NotifyStateChanged(service);
            _logger.LogError(service.Name, $"Failed to start {service.Name}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> StopServiceAsync(ServiceType type)
    {
        if (!_services.TryGetValue(type, out var service))
        {
            return false;
        }

        var matchingProcs = GetProcessesForService(type);
        var portStatusBeforeStop = await _portService.CheckPortAsync(service.Port);

        if (service.State == ServiceState.Stopped && matchingProcs.Count == 0 && !portStatusBeforeStop.IsOccupied)
        {
            return true;
        }

        service.State = ServiceState.Stopping;
        NotifyStateChanged(service);

        try
        {
            if (type == ServiceType.Nginx)
            {
                var info = await _detector.DetectNginxAsync();
                if (info != null && File.Exists(info.ExecutablePath))
                {
                    try { await _processManager.ExecuteAsync(info.ExecutablePath, $"-p \"{info.DirectoryPath}\" -s stop", workingDirectory: info.DirectoryPath); } catch { }
                }
                ForceKillProcessesByName("nginx");
                var apache = GetService(ServiceType.Apache);
                if (apache == null || !apache.IsRunning)
                {
                    try { await _processManager.StopMonitoredProcessAsync("PHP-CGI"); } catch { }
                    ForceKillProcessesByName("php-cgi");
                }
            }
            else if (type == ServiceType.Apache)
            {
                ForceKillProcessesByName("httpd");
                var nginx = GetService(ServiceType.Nginx);
                if (nginx == null || !nginx.IsRunning)
                {
                    try { await _processManager.StopMonitoredProcessAsync("PHP-CGI"); } catch { }
                    ForceKillProcessesByName("php-cgi");
                }
            }
            else if (type == ServiceType.PostgreSQL)
            {
                var dataDir = Path.Combine(_configService.Config.Paths.DataPath, "postgresql").Replace('\\', '/');
                var info = await _detector.DetectPostgresAsync();
                if (info != null && File.Exists(info.ExecutablePath))
                {
                    var pgCtl = Path.Combine(info.DirectoryPath, "pg_ctl.exe");
                    if (File.Exists(pgCtl))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = pgCtl,
                            Arguments = $"stop -D \"{dataDir}\" -m fast",
                            WorkingDirectory = info.DirectoryPath,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        try
                        {
                            using var p = Process.Start(psi);
                            if (p != null) await p.WaitForExitAsync();
                        }
                        catch { }
                    }
                }
                try { foreach (var p in Process.GetProcessesByName("postgres")) { p.Kill(entireProcessTree: true); } } catch { }
                var pidFile = Path.Combine(dataDir, "postmaster.pid");
                if (File.Exists(pidFile)) { try { File.Delete(pidFile); } catch { } }
            }

            var mon = _processManager.GetMonitoredProcess(service.Name);
            if (mon != null)
            {
                await _processManager.StopMonitoredProcessAsync(service.Name);
            }
            else if (service.ProcessId.HasValue)
            {
                await _portService.KillProcessUsingPortAsync(service.Port);
            }

            service.State = ServiceState.Stopped;
            service.ProcessId = null;
            service.StartedAt = null;
            service.LastError = null;
            NotifyStateChanged(service);
            _logger.LogInfo(service.Name, $"{service.Name} stopped.");
            return true;
        }
        catch (Exception ex)
        {
            service.State = ServiceState.Error;
            service.LastError = ex.Message;
            NotifyStateChanged(service);
            _logger.LogError(service.Name, $"Failed to stop {service.Name}: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> RestartServiceAsync(ServiceType type)
    {
        _logger.LogInfo(type.ToString(), $"Initiating restart sequence for {type}...");

        // 1. Stop service
        await StopServiceAsync(type);

        // 2. Wait until old process / port is completely terminated
        if (_services.TryGetValue(type, out var service))
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3500)
            {
                var portStatus = await _portService.CheckPortAsync(service.Port);
                if (!portStatus.IsOccupied) break;
                await Task.Delay(250);
            }
        }
        else
        {
            await Task.Delay(500);
        }

        // 3. Start service
        var started = await StartServiceAsync(type);
        if (started)
        {
            _logger.LogInfo(type.ToString(), $"{type} restarted successfully and is running on port {service?.Port}.");
        }
        else
        {
            _logger.LogError(type.ToString(), $"Restart failed for {type}: {service?.LastError ?? "Process failed to stay running."} (Log: {service?.LogPath})");
        }

        return started;
    }

    public async Task RefreshAllAsync()
    {
        var runtimes = await _detector.DetectAllRuntimesAsync();

        foreach (var kvp in _services)
        {
            var service = kvp.Value;
            var runtime = runtimes.FirstOrDefault(r => r.Name.Equals(service.Name, StringComparison.OrdinalIgnoreCase));

            if (runtime != null && runtime.IsInstalled)
            {
                service.Version = runtime.Version;
                service.ExecutablePath = runtime.ExecutablePath;
            }
            else
            {
                service.Version = "Not installed";
                service.ExecutablePath = string.Empty;
                if (service.State == ServiceState.Error)
                {
                    service.State = ServiceState.Stopped;
                    service.LastError = null;
                }
            }

            var mon = _processManager.GetMonitoredProcess(service.Name);
            if (mon != null && mon.IsRunning)
            {
                service.State = ServiceState.Running;
                service.ProcessId = mon.ProcessId;
                service.LastError = null;
            }
            else if (kvp.Key == ServiceType.Nginx && Process.GetProcessesByName("nginx").Length > 0)
            {
                service.State = ServiceState.Running;
                service.ProcessId = Process.GetProcessesByName("nginx")[0].Id;
                service.LastError = null;
            }
            else if (kvp.Key == ServiceType.PostgreSQL && Process.GetProcessesByName("postgres").Length > 0)
            {
                service.State = ServiceState.Running;
                service.ProcessId = Process.GetProcessesByName("postgres")[0].Id;
                service.LastError = null;
            }
            else
            {
                var portStatus = await _portService.CheckPortAsync(service.Port);
                if (portStatus.IsOccupied && IsProcessMatchingService(kvp.Key, portStatus.ProcessName, portStatus.ProcessPath))
                {
                    service.State = ServiceState.Running;
                    service.ProcessId = portStatus.ProcessId;
                    service.LastError = null;
                }
                else if (service.State == ServiceState.Running)
                {
                    service.State = ServiceState.Stopped;
                    service.ProcessId = null;
                }
            }

            NotifyStateChanged(service);
        }

        // Verify PHP-CGI is alive if any web server is running
        var isWebServerRunning = _services.Values.Any(s => (s.Type == ServiceType.Apache || s.Type == ServiceType.Nginx) && s.IsRunning)
                                 || Process.GetProcessesByName("nginx").Length > 0
                                 || Process.GetProcessesByName("httpd").Length > 0;
        if (isWebServerRunning)
        {
            var p9000 = await _portService.CheckPortAsync(9000);
            if (!p9000.IsOccupied)
            {
                await EnsurePhpCgiRunningAsync();
            }
        }
    }

    private static bool IsProcessMatchingService(ServiceType type, string processName, string? processPath = null)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        return type switch
        {
            ServiceType.MySQL => (processName.Contains("mysqld", StringComparison.OrdinalIgnoreCase) || processName.Equals("mysql", StringComparison.OrdinalIgnoreCase))
                                 && !processName.Contains("mariadb", StringComparison.OrdinalIgnoreCase)
                                 && (string.IsNullOrEmpty(processPath) || !processPath.Contains("mariadb", StringComparison.OrdinalIgnoreCase)),
            ServiceType.MariaDB => (processName.Contains("mariadbd", StringComparison.OrdinalIgnoreCase) || processName.Contains("mariadb", StringComparison.OrdinalIgnoreCase))
                                   || (processName.Contains("mysqld", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(processPath) && processPath.Contains("mariadb", StringComparison.OrdinalIgnoreCase)),
            ServiceType.PostgreSQL => processName.Contains("postgres", StringComparison.OrdinalIgnoreCase),
            ServiceType.Apache => processName.Contains("httpd", StringComparison.OrdinalIgnoreCase) || processName.Contains("apache", StringComparison.OrdinalIgnoreCase),
            ServiceType.Nginx => processName.Contains("nginx", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private async Task StartApacheInternalAsync(ServiceInfo service)
    {
        var info = await _detector.DetectApacheAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            throw new FileNotFoundException("Apache executable (httpd.exe) was not found on the system.");
        }

        service.ExecutablePath = info.ExecutablePath;
        service.Version = info.Version;

        // Automatically resolve port conflict (e.g. Nginx or stale process on port 80)
        try
        {
            var portStatus = await _portService.CheckPortAsync(service.Port);
            if (portStatus.IsOccupied)
            {
                _logger.LogInfo(service.Name, $"Port {service.Port} is occupied by '{portStatus.ProcessName}'. Freeing port {service.Port} for Apache...");
                foreach (var p in Process.GetProcessesByName("nginx"))
                {
                    try { p.Kill(entireProcessTree: true); } catch { }
                }
                await _portService.KillProcessUsingPortAsync(service.Port);
                await Task.Delay(400);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(service.Name, $"Port resolution notice: {ex.Message}");
        }

        // Sync port configuration to httpd.conf before test & start
        await _apacheService.UpdatePortConfigurationAsync(service.Port, _configService.Config.Services.ApacheSslPort);

        // Configuration syntax test before start (Section 10)
        var (isApacheValid, apacheOutput) = await _apacheService.ValidateConfigurationAsync();
        if (!isApacheValid)
        {
            throw new InvalidOperationException($"Apache configuration test failed: {apacheOutput}");
        }

        // Ensure FastCGI PHP daemon is running on port 9000 with active PHP version
        await EnsurePhpCgiRunningAsync();

        // Start Apache process with explicit root directory (-d)
        var apacheDir = Directory.GetParent(info.DirectoryPath)?.FullName ?? info.DirectoryPath;
        var args = $"-d \"{apacheDir.Replace('\\', '/')}\"";
        await _processManager.StartMonitoredProcessAsync(
            service.Name,
            info.ExecutablePath,
            args,
            workingDirectory: apacheDir);
    }

    private async Task StartMySqlInternalAsync(ServiceInfo service)
    {
        var info = await _detector.DetectMySqlAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            throw new FileNotFoundException("MySQL executable (mysqld.exe) was not found on the system.");
        }

        service.ExecutablePath = info.ExecutablePath;
        service.Version = info.Version;

        // Initialize datadir if needed
        await _mySqlService.InitializeDataDirectoryAsync();

        var dataDir = Path.Combine(_configService.Config.Paths.DataPath, "mysql").Replace('\\', '/');
        var args = $"--datadir=\"{dataDir}\" --port={service.Port} --console";

        await _processManager.StartMonitoredProcessAsync(
            service.Name,
            info.ExecutablePath,
            args,
            workingDirectory: info.DirectoryPath);
    }

    private async Task StartMariaDbInternalAsync(ServiceInfo service)
    {
        var info = await _detector.DetectMariaDbAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            throw new FileNotFoundException("MariaDB executable (mariadbd.exe/mysqld.exe) was not found on the system.");
        }

        service.ExecutablePath = info.ExecutablePath;
        service.Version = info.Version;

        var dataDir = Path.Combine(_configService.Config.Paths.DataPath, "mariadb").Replace('\\', '/');
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

        var mysqlDbDir = Path.Combine(dataDir, "mysql");
        if (!Directory.Exists(mysqlDbDir))
        {
            var installDbExe = Path.Combine(info.DirectoryPath, "mysql_install_db.exe");
            if (File.Exists(installDbExe))
            {
                _logger.LogInfo(service.Name, "Initializing MariaDB data directory with mysql_install_db...");
                await _processManager.ExecuteAsync(installDbExe, $"--datadir=\"{dataDir}\"", workingDirectory: info.DirectoryPath);
            }
        }

        var baseDir = Path.GetFullPath(Path.Combine(info.DirectoryPath, "..")).Replace('\\', '/');
        var args = $"--no-defaults --basedir=\"{baseDir}\" --datadir=\"{dataDir}\" --port={service.Port} --console";
        await _processManager.StartMonitoredProcessAsync(
            service.Name,
            info.ExecutablePath,
            args,
            workingDirectory: info.DirectoryPath);
    }

    private async Task StartPostgresInternalAsync(ServiceInfo service)
    {
        var info = await _detector.DetectPostgresAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            throw new FileNotFoundException("PostgreSQL executable (postgres.exe) was not found on the system.");
        }

        service.ExecutablePath = info.ExecutablePath;
        service.Version = info.Version;

        var dataDir = Path.Combine(_configService.Config.Paths.DataPath, "postgresql").Replace('\\', '/');
        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

        // Stale postmaster.pid lock cleanup
        var pidFile = Path.Combine(dataDir, "postmaster.pid");
        if (File.Exists(pidFile))
        {
            try
            {
                var pidContent = await File.ReadAllLinesAsync(pidFile);
                if (pidContent.Length > 0 && int.TryParse(pidContent[0].Trim(), out int pid))
                {
                    try
                    {
                        var runningProc = Process.GetProcessById(pid);
                        if (!runningProc.ProcessName.Contains("postgres", StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(pidFile);
                            _logger.LogInfo(service.Name, "Cleaned up stale postmaster.pid lock file.");
                        }
                    }
                    catch (ArgumentException)
                    {
                        File.Delete(pidFile);
                        _logger.LogInfo(service.Name, "Cleaned up stale postmaster.pid lock file (orphaned lock).");
                    }
                }
                else
                {
                    File.Delete(pidFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(service.Name, $"Stale lock check warning: {ex.Message}");
            }
        }

        // Initialize cluster if missing
        if (!File.Exists(Path.Combine(dataDir, "PG_VERSION")))
        {
            var initDbExe = Path.Combine(info.DirectoryPath, "initdb.exe");
            if (File.Exists(initDbExe))
            {
                _logger.LogInfo(service.Name, $"Initializing PostgreSQL cluster at {dataDir}...");
                await _processManager.ExecuteAsync(initDbExe, $"-D \"{dataDir}\" -U postgres -A trust -E UTF8", workingDirectory: info.DirectoryPath);
            }
        }

        // Use pg_ctl to start PostgreSQL under unprivileged/restricted token on Windows
        var pgCtl = Path.Combine(info.DirectoryPath, "pg_ctl.exe");
        var logDir = Path.Combine(_configService.Config.Paths.LogsPath, "postgresql");
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
        var logFile = Path.Combine(logDir, "postgresql.log").Replace('\\', '/');

        if (File.Exists(pgCtl))
        {
            _logger.LogInfo(service.Name, "Starting PostgreSQL service via pg_ctl with unprivileged token...");
            var psi = new ProcessStartInfo
            {
                FileName = pgCtl,
                Arguments = $"start -D \"{dataDir}\" -l \"{logFile}\" -o \"-p {service.Port}\"",
                WorkingDirectory = info.DirectoryPath,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p != null)
            {
                await p.WaitForExitAsync();
            }

            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 3500)
            {
                var portStatus = await _portService.CheckPortAsync(service.Port);
                if (portStatus.IsOccupied) break;
                await Task.Delay(250);
            }
        }
        else
        {
            var args = $"-D \"{dataDir}\" -p {service.Port}";
            await _processManager.StartMonitoredProcessAsync(
                service.Name,
                info.ExecutablePath,
                args,
                workingDirectory: info.DirectoryPath);
        }
    }

    private async Task StartNginxInternalAsync(ServiceInfo service)
    {
        var info = await _detector.DetectNginxAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            throw new FileNotFoundException("Nginx executable (nginx.exe) was not found on the system.");
        }

        service.ExecutablePath = info.ExecutablePath;
        service.Version = info.Version;

        // Automatically resolve port conflict on port 80 (virtual hosts) and service.Port
        foreach (var checkPort in new[] { 80, service.Port }.Distinct())
        {
            try
            {
                var portStatus = await _portService.CheckPortAsync(checkPort);
                if (portStatus.IsOccupied && !IsProcessMatchingService(ServiceType.Nginx, portStatus.ProcessName, portStatus.ProcessPath))
                {
                    _logger.LogInfo(service.Name, $"Port {checkPort} is occupied by '{portStatus.ProcessName}'. Freeing port {checkPort} for Nginx...");
                    foreach (var p in Process.GetProcessesByName("httpd"))
                    {
                        try { p.Kill(entireProcessTree: true); } catch { }
                    }
                    await _portService.KillProcessUsingPortAsync(checkPort);
                    await Task.Delay(400);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(service.Name, $"Port resolution notice on port {checkPort}: {ex.Message}");
            }
        }

        // Sync port configuration to nginx.conf before test & start
        await _nginxService.UpdatePortConfigurationAsync(service.Port);

        // Ensure FastCGI PHP daemon is running on port 9000 with active PHP version
        await EnsurePhpCgiRunningAsync();

        var (isNginxValid, nginxOutput) = await _nginxService.ValidateConfigurationAsync();
        if (!isNginxValid)
        {
            throw new InvalidOperationException($"Nginx configuration test failed: {nginxOutput}");
        }

        var confPath = Path.Combine(_configService.Config.Paths.EtcPath, "nginx", "nginx.conf");
        var confArg = File.Exists(confPath) ? $" -c \"{confPath}\"" : "";
        var args = $"-p \"{info.DirectoryPath}\"{confArg}";
        await _processManager.StartMonitoredProcessAsync(
            service.Name,
            info.ExecutablePath,
            args,
            workingDirectory: info.DirectoryPath);
    }

    public async Task<bool> EnsurePhpCgiRunningAsync()
    {
        try
        {
            var portStatus = await _portService.CheckPortAsync(9000);
            if (portStatus.IsOccupied)
            {
                if (portStatus.ProcessName.Contains("php-cgi", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInfo("PHP-CGI", $"PHP-CGI FastCGI daemon is already running on port 9000 (PID: {portStatus.ProcessId}).");
                    return true;
                }

                _logger.LogWarning("PHP-CGI", $"Port 9000 is occupied by non-php-cgi process '{portStatus.ProcessName}'. Freeing port 9000 for PHP-CGI...");
                await _portService.KillProcessUsingPortAsync(9000);
                await Task.Delay(300);
            }

            var activeVersion = _configService.Config.Php.ActiveVersion;
            var binPath = _configService.Config.Paths.BinPath;
            string? phpCgiPath = null;

            if (!string.IsNullOrEmpty(activeVersion))
            {
                var candidate = Path.Combine(binPath, "php", activeVersion, "php-cgi.exe");
                if (File.Exists(candidate)) phpCgiPath = candidate;
            }

            if (phpCgiPath == null)
            {
                var lokaPhp = Path.Combine(binPath, "php");
                if (Directory.Exists(lokaPhp))
                {
                    var candidates = Directory.GetFiles(lokaPhp, "php-cgi.exe", SearchOption.AllDirectories);
                    phpCgiPath = candidates.OrderByDescending(f => f).FirstOrDefault();
                }
            }

            if (phpCgiPath != null && File.Exists(phpCgiPath))
            {
                _logger.LogInfo("PHP-CGI", $"Starting PHP FastCGI daemon ({phpCgiPath}) on 127.0.0.1:9000...");
                var phpDir = Path.GetDirectoryName(phpCgiPath) ?? "";
                var env = new Dictionary<string, string>
                {
                    { "PHP_FCGI_MAX_REQUESTS", "10000" },
                    { "PHP_FCGI_CHILDREN", "4" },
                    // Ensure php-cgi loads the correct php.ini from its own directory
                    { "PHPRC", phpDir },
                    { "PHP_INI_SCAN_DIR", "" }  // Disable extra ini scan dirs to prevent conflicts
                };
                await _processManager.StartMonitoredProcessAsync(
                    "PHP-CGI",
                    phpCgiPath,
                    "-b 127.0.0.1:9000",
                    workingDirectory: phpDir,
                    environmentVariables: env);

                // Health check verification that FastCGI backend is listening on port 9000 (Fix.md Section 7)
                var sw = Stopwatch.StartNew();
                bool isPortReady = false;
                while (sw.ElapsedMilliseconds < 3500)
                {
                    var check = await _portService.CheckPortAsync(9000);
                    if (check.IsOccupied && check.ProcessName.Contains("php-cgi", StringComparison.OrdinalIgnoreCase))
                    {
                        isPortReady = true;
                        break;
                    }
                    await Task.Delay(200);
                }

                if (!isPortReady)
                {
                    _logger.LogWarning("PHP-CGI", "PHP-CGI daemon process was started but port 9000 is not yet responsive.");
                }
                else
                {
                    _logger.LogInfo("PHP-CGI", "PHP-CGI FastCGI daemon health check passed on port 9000.");
                }
                return isPortReady;
            }
            else
            {
                _logger.LogWarning("PHP-CGI", "php-cgi.exe not found to start FastCGI daemon on port 9000.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("PHP-CGI", $"Error starting PHP FastCGI: {ex.Message}", ex);
            return false;
        }
    }

    public async Task<bool> RestartPhpCgiAsync()
    {
        try
        {
            _logger.LogInfo("PHP-CGI", "Restarting PHP FastCGI daemon to apply updated PHP configuration...");
            try { await _processManager.StopMonitoredProcessAsync("PHP-CGI"); } catch { }
            try { foreach (var p in Process.GetProcessesByName("php-cgi")) { p.Kill(entireProcessTree: true); } } catch { }
            await Task.Delay(300);
            return await EnsurePhpCgiRunningAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("PHP-CGI", $"Failed restarting PHP FastCGI daemon: {ex.Message}", ex);
            return false;
        }
    }

    private void OnProcessExitedUnexpectedly(object? sender, ProcessUnexpectedExitEventArgs e)
    {
        if (e.ProcessName.Equals("PHP-CGI", StringComparison.OrdinalIgnoreCase))
        {
            var isWebRunning = _services.Values.Any(s => (s.Type == ServiceType.Apache || s.Type == ServiceType.Nginx) && s.IsRunning)
                               || Process.GetProcessesByName("nginx").Length > 0
                               || Process.GetProcessesByName("httpd").Length > 0;
            if (isWebRunning)
            {
                _logger.LogInfo("PHP-CGI", "PHP-CGI exited. Auto-restarting PHP FastCGI daemon...");
                _ = Task.Run(EnsurePhpCgiRunningAsync);
            }
            return;
        }

        var service = _services.Values.FirstOrDefault(s => s.Name.Equals(e.ProcessName, StringComparison.OrdinalIgnoreCase));
        if (service != null)
        {
            if (service.Type == ServiceType.Nginx && Process.GetProcessesByName("nginx").Length > 0)
            {
                // Nginx worker is alive in background, not an unexpected exit
                return;
            }

            service.State = ServiceState.Error;
            service.LastError = $"{service.Name} stopped unexpectedly with exit code {e.ExitCode}. Reason: {e.LastErrorOutput ?? "See error log"}";
            NotifyStateChanged(service);
            _logger.LogError(service.Name, service.LastError);
        }
    }

    public async Task<bool> StartAllAsync()
    {
        _logger.LogInfo("ServiceManager", "Executing sequenced Start All workflow (Fitur.md Section 7)...");

        // 1. Ensure FastCGI PHP daemon is running on port 9000
        await EnsurePhpCgiRunningAsync();

        // 2. Start Database Engines (independent, non-conflicting)
        bool dbStarted = false;
        if (_configService.Config.Services.AutoStartMySql || _configService.Config.Services.AutoStartMariaDb || _configService.Config.Services.AutoStartPostgres)
        {
            if (_configService.Config.Services.AutoStartMySql)
            {
                var ok = await StartServiceAsync(ServiceType.MySQL);
                if (ok) dbStarted = true;
            }
            if (_configService.Config.Services.AutoStartMariaDb)
            {
                var ok = await StartServiceAsync(ServiceType.MariaDB);
                if (ok) dbStarted = true;
            }
            if (_configService.Config.Services.AutoStartPostgres)
            {
                var ok = await StartServiceAsync(ServiceType.PostgreSQL);
                if (ok) dbStarted = true;
            }
        }
        else
        {
            // Default Start All behavior: Start MySQL, and start MariaDB if configured on a distinct port
            var mySqlOk = await StartServiceAsync(ServiceType.MySQL);
            if (mySqlOk) dbStarted = true;

            if (_configService.Config.Services.MariaDbPort != _configService.Config.Services.MySqlPort)
            {
                var mariaOk = await StartServiceAsync(ServiceType.MariaDB);
                if (mariaOk) dbStarted = true;
            }
        }

        // 3. Start chosen Web Server (Apache or Nginx)
        var webType = string.Equals(_configService.Config.Services.PreferredWebServer, "Nginx", StringComparison.OrdinalIgnoreCase)
            ? ServiceType.Nginx
            : ServiceType.Apache;

        var webStarted = await StartServiceAsync(webType);

        return dbStarted && webStarted;
    }

    public async Task<bool> StopAllAsync()
    {
        _logger.LogInfo("ServiceManager", "Executing sequenced Stop All workflow (Fitur.md Section 7)...");

        // 1. Stop Web Servers
        await StopServiceAsync(ServiceType.Apache);
        await StopServiceAsync(ServiceType.Nginx);

        // 2. Stop Database Engines
        await StopServiceAsync(ServiceType.MySQL);
        await StopServiceAsync(ServiceType.MariaDB);
        await StopServiceAsync(ServiceType.PostgreSQL);

        // 3. Stop PHP-CGI FastCGI daemon
        try { await _processManager.StopMonitoredProcessAsync("PHP-CGI"); } catch { }
        try { foreach (var p in Process.GetProcessesByName("php-cgi")) { p.Kill(entireProcessTree: true); } } catch { }

        return true;
    }

    private void NotifyStateChanged(ServiceInfo info)
    {
        ServiceStateChanged?.Invoke(info);
    }

    private static IReadOnlyList<Process> GetProcessesForService(ServiceType type)
    {
        return type switch
        {
            ServiceType.Apache => Process.GetProcessesByName("httpd"),
            ServiceType.Nginx => Process.GetProcessesByName("nginx"),
            ServiceType.MySQL => Process.GetProcessesByName("mysqld").Where(p => {
                try { return !p.MainModule?.FileName?.Contains("mariadb", StringComparison.OrdinalIgnoreCase) ?? true; } catch { return true; }
            }).ToArray(),
            ServiceType.MariaDB => Process.GetProcessesByName("mariadbd").Concat(
                Process.GetProcessesByName("mysqld").Where(p => {
                    try { return p.MainModule?.FileName?.Contains("mariadb", StringComparison.OrdinalIgnoreCase) ?? false; } catch { return false; }
                })
            ).ToArray(),
            ServiceType.PostgreSQL => Process.GetProcessesByName("postgres"),
            _ => Array.Empty<Process>()
        };
    }

    private static void ForceKillProcessesByName(string processName)
    {
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try
            {
                p.Kill(entireProcessTree: true);
            }
            catch
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"(Get-CimInstance Win32_Process -Filter \\\"ProcessId = {p.Id}\\\") | Invoke-CimMethod -MethodName Terminate\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(2000);
                }
                catch { }
            }
        }
    }
}
