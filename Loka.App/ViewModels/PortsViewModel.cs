using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class PortsViewModel : ObservableObject
{
    private readonly IPortService _portService;
    private readonly IConfigurationService _configService;
    private readonly IServiceManager _serviceManager;
    private readonly ILogService _logger;

    private readonly List<PortInfo> _allPorts = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    public ObservableCollection<PortInfo> Ports { get; } = new();

    public PortsViewModel(
        IPortService portService, 
        IConfigurationService configService, 
        IServiceManager serviceManager,
        ILogService logger)
    {
        _portService = portService;
        _configService = configService;
        _serviceManager = serviceManager;
        _logger = logger;

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;
        _configService.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        UiDispatcher.Run(() =>
        {
            _ = RefreshPortsAsync();
        });
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            _ = RefreshPortsAsync();
        });
    }

    public async Task InitializeAsync()
    {
        await RefreshPortsAsync();
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Ports.Clear();
        var q = SearchFilter.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q) 
            ? _allPorts 
            : _allPorts.Where(p => 
                p.Port.ToString().Contains(q) || 
                p.ServiceAssigned.ToLowerInvariant().Contains(q) || 
                p.ProcessName.ToLowerInvariant().Contains(q) ||
                (p.ProcessId.HasValue && p.ProcessId.Value.ToString().Contains(q))).ToList();

        foreach (var p in filtered)
        {
            Ports.Add(p);
        }
    }

    [RelayCommand]
    public async Task RefreshPortsAsync()
    {
        IsLoading = true;
        StatusMessage = "Checking port bindings...";

        try
        {
            var trackedPorts = new List<int>
            {
                80, 443, 3306, 3307, 8080, 8000, 5173, 5432, 9000, 6379, 21, 22, 25, 110,
                _configService.Config.Services.ApachePort,
                _configService.Config.Services.ApacheSslPort,
                _configService.Config.Services.MySqlPort,
                _configService.Config.Services.NginxPort
            }.Distinct().OrderBy(p => p).ToList();

            var statuses = await _portService.GetPortStatusesAsync(trackedPorts);

            _allPorts.Clear();
            foreach (var status in statuses)
            {
                if (status.Port == _configService.Config.Services.ApachePort) status.ServiceAssigned = "Apache (HTTP)";
                else if (status.Port == _configService.Config.Services.ApacheSslPort) status.ServiceAssigned = "Apache (HTTPS)";
                else if (status.Port == _configService.Config.Services.MySqlPort) status.ServiceAssigned = "MySQL";
                else if (status.Port == _configService.Config.Services.NginxPort) status.ServiceAssigned = "Nginx";
                else if (status.Port == 5173) status.ServiceAssigned = "Vite Dev Server";
                else if (status.Port == 8000) status.ServiceAssigned = "PHP Artisan";
                else if (status.Port == 6379) status.ServiceAssigned = "Redis";
                else if (status.Port == 5432) status.ServiceAssigned = "PostgreSQL";

                _allPorts.Add(status);
            }

            ApplyFilter();
            StatusMessage = $"Monitored {_allPorts.Count} development ports ({_allPorts.Count(p => p.IsOccupied)} occupied).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Port check error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task KillProcessAsync(PortInfo? portInfo)
    {
        if (portInfo == null || !portInfo.IsOccupied || !portInfo.ProcessId.HasValue) return;

        // Confirmation Dialog per Section 8
        var confirm = MessageBox.Show(
            $"Are you sure you want to terminate process '{portInfo.ProcessName}' (PID: {portInfo.ProcessId}) occupying port {portInfo.Port}?",
            "Confirm Kill Process",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = $"Terminating {portInfo.ProcessName} (PID: {portInfo.ProcessId})...";
        try
        {
            bool ok = await _portService.KillProcessUsingPortAsync(portInfo.Port);
            if (ok)
            {
                StatusMessage = $"Terminated process occupying port {portInfo.Port}.";
                await RefreshPortsAsync();
            }
            else
            {
                StatusMessage = "Failed to terminate process (permission denied or protected system process).";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }
}
