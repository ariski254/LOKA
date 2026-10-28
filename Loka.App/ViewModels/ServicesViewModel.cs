using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class ServicesViewModel : ObservableObject
{
    private readonly IServiceManager _serviceManager;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public ObservableCollection<ServiceInfo> Services { get; } = new();

    public ServicesViewModel(
        IServiceManager serviceManager,
        IConfigurationService configService,
        ILogService logger)
    {
        _serviceManager = serviceManager;
        _configService = configService;
        _logger = logger;

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;
        _configService.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            _ = RefreshServicesAsync();
        });
    }

    public async Task InitializeAsync()
    {
        await RefreshServicesAsync();
    }

    [RelayCommand]
    public async Task RefreshServicesAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking service processes...";

        try
        {
            await _serviceManager.RefreshAllAsync();
            Services.Clear();
            foreach (var svc in _serviceManager.GetAllServices().OrderBy(s => GetServiceSortOrder(s.Type)))
            {
                // Populate default config/log paths if empty
                if (string.IsNullOrEmpty(svc.ConfigPath))
                {
                    svc.ConfigPath = svc.Type switch
                    {
                        ServiceType.Apache => Path.Combine(_configService.Config.Paths.EtcPath, "apache", "httpd.conf"),
                        ServiceType.Nginx => Path.Combine(_configService.Config.Paths.EtcPath, "nginx", "nginx.conf"),
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
                        ServiceType.Nginx => Path.Combine(_configService.Config.Paths.LogsPath, "nginx", "nginx_error.log"),
                        ServiceType.MySQL => Path.Combine(_configService.Config.Paths.LogsPath, "mysql", "mysql_error.log"),
                        ServiceType.MariaDB => Path.Combine(_configService.Config.Paths.LogsPath, "mariadb", "mariadb_error.log"),
                        ServiceType.PostgreSQL => Path.Combine(_configService.Config.Paths.LogsPath, "postgresql", "postgresql.log"),
                        _ => Path.Combine(_configService.Config.Paths.LogsPath, $"{svc.Name.ToLowerInvariant()}.log")
                    };
                }

                Services.Add(svc);
            }

            var runningCount = Services.Count(s => s.IsRunning);
            StatusMessage = $"Monitored {Services.Count} services ({runningCount} active).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Refresh error: {ex.Message}";
            _logger.LogError("Services", "Failed to refresh services", ex);
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
            bool ok = await _serviceManager.StartServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} started successfully." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to start {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error starting {service.Name}: {ex.Message}";
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
            bool ok = await _serviceManager.StopServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} stopped." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to stop {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error stopping {service.Name}: {ex.Message}";
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
            bool ok = await _serviceManager.RestartServiceAsync(service.Type);
            StatusMessage = ok 
                ? $"{service.Name} restarted." 
                : (!string.IsNullOrEmpty(service.LastError) ? service.LastError : $"Failed to restart {service.Name}.");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error restarting {service.Name}: {ex.Message}";
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
            _logger.LogError("Services", $"Failed opening config for {service.Name}", ex);
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
            _logger.LogError("Services", $"Failed opening log for {service.Name}", ex);
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
            _logger.LogError("Services", $"Failed opening folder '{path}'", ex);
        }
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        UiDispatcher.Run(() =>
        {
            var existing = Services.FirstOrDefault(s => s.Type == info.Type);
            if (existing != null)
            {
                existing.State = info.State;
                existing.Port = info.Port;
                existing.ProcessId = info.ProcessId;
                existing.LastError = info.LastError;
                existing.Version = info.Version;
            }
            if (!string.IsNullOrEmpty(info.LastError))
            {
                StatusMessage = info.LastError;
            }
            OnPropertyChanged(nameof(HasRunningServices));
            OnPropertyChanged(nameof(GlobalActionText));
        });
    }
}
