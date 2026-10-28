using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceManager _serviceManager;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logger;

    [ObservableProperty]
    private string _activeView = "Dashboard";

    [ObservableProperty]
    private object _currentViewModel = null!;

    [ObservableProperty]
    private string _runningServicesStatus = "0 / 2 Running";

    [ObservableProperty]
    private bool _hasRunningServices;

    public string EnvironmentPath => _configService.Config.Paths.RootPath;

    // Navigation title
    public string PageTitle => ActiveView switch
    {
        "Dashboard" or "Overview" => "Overview",
        "Projects" => "Projects",
        "PHPTools" or "Runtimes" or "Tools" => "PHP & Tools",
        "Databases" or "Database" => "Database",
        "Terminal" => "Terminal",
        "Diagnostics" => "Diagnostics",
        "Settings" => "Settings",
        _ => ActiveView
    };

    public bool IsDashboardActive
    {
        get => ActiveView is "Dashboard" or "Overview";
        set { if (value && ActiveView != "Dashboard") _ = NavigateAsync("Dashboard"); }
    }

    public bool IsProjectsActive
    {
        get => ActiveView == "Projects";
        set { if (value && ActiveView != "Projects") _ = NavigateAsync("Projects"); }
    }

    public bool IsDatabasesActive
    {
        get => ActiveView is "Databases" or "Database";
        set { if (value && ActiveView != "Database") _ = NavigateAsync("Database"); }
    }

    public bool IsPHPToolsActive
    {
        get => ActiveView is "PHPTools" or "Runtimes" or "Tools";
        set { if (value && ActiveView != "PHPTools") _ = NavigateAsync("PHPTools"); }
    }

    public bool IsDiagnosticsActive
    {
        get => ActiveView == "Diagnostics";
        set { if (value && ActiveView != "Diagnostics") _ = NavigateAsync("Diagnostics"); }
    }

    public bool IsTerminalActive
    {
        get => ActiveView == "Terminal";
        set { if (value && ActiveView != "Terminal") _ = NavigateAsync("Terminal"); }
    }

    public bool IsSettingsActive
    {
        get => ActiveView == "Settings";
        set { if (value && ActiveView != "Settings") _ = NavigateAsync("Settings"); }
    }

    public DashboardViewModel DashboardVM { get; }
    public ProjectsViewModel ProjectsVM { get; }
    public DatabaseViewModel DatabaseVM { get; }
    public RuntimesViewModel RuntimesVM { get; }
    public DiagnosticsViewModel DiagnosticsVM { get; }
    public TerminalViewModel TerminalVM { get; }
    public SettingsViewModel SettingsVM { get; }

    // Kept internally for phpMyAdmin open action from DashboardVM
    private readonly PhpMyAdminViewModel _phpMyAdminVM;

    public MainViewModel(
        IServiceManager serviceManager,
        IConfigurationService configService,
        ILogService logger,
        DashboardViewModel dashboardVM,
        ProjectsViewModel projectsVM,
        DatabaseViewModel databaseVM,
        RuntimesViewModel runtimesVM,
        PhpMyAdminViewModel phpMyAdminVM,
        DiagnosticsViewModel diagnosticsVM,
        TerminalViewModel terminalVM,
        SettingsViewModel settingsVM)
    {
        _serviceManager = serviceManager;
        _configService = configService;
        _logger = logger;
        DashboardVM = dashboardVM;
        ProjectsVM = projectsVM;
        DatabaseVM = databaseVM;
        RuntimesVM = runtimesVM;
        _phpMyAdminVM = phpMyAdminVM;
        DiagnosticsVM = diagnosticsVM;
        TerminalVM = terminalVM;
        SettingsVM = settingsVM;

        CurrentViewModel = DashboardVM;

        _serviceManager.ServiceStateChanged += OnServiceStateChanged;
        _configService.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            OnPropertyChanged(nameof(EnvironmentPath));
        });
    }

    public async Task InitializeAsync()
    {
        CurrentViewModel = DashboardVM;
        UpdateActiveTabProperties();
        await DashboardVM.InitializeAsync();
        UpdateRunningServicesStatus();
    }

    private string _currentNavigatingView = string.Empty;

    [RelayCommand]
    public async Task NavigateAsync(string viewName)
    {
        if (string.IsNullOrWhiteSpace(viewName)) return;
        if (_currentNavigatingView == viewName) return;

        _currentNavigatingView = viewName;
        ActiveView = viewName;

        switch (viewName)
        {
            case "Dashboard":
            case "Overview":
                CurrentViewModel = DashboardVM;
                break;
            case "Projects":
                CurrentViewModel = ProjectsVM;
                break;
            case "Databases":
            case "Database":
                CurrentViewModel = DatabaseVM;
                break;
            case "PHPTools":
            case "Runtimes":
            case "Tools":
                CurrentViewModel = RuntimesVM;
                break;
            case "Diagnostics":
                CurrentViewModel = DiagnosticsVM;
                break;
            case "Terminal":
                CurrentViewModel = TerminalVM;
                break;
            case "Settings":
                CurrentViewModel = SettingsVM;
                break;
        }

        UpdateActiveTabProperties();

        try
        {
            switch (viewName)
            {
                case "Dashboard":
                case "Overview":
                    await DashboardVM.InitializeAsync();
                    break;
                case "Projects":
                    await ProjectsVM.InitializeAsync();
                    break;
                case "Databases":
                case "Database":
                    await DatabaseVM.InitializeAsync();
                    break;
                case "PHPTools":
                case "Runtimes":
                case "Tools":
                    await RuntimesVM.InitializeAsync();
                    break;
                case "Diagnostics":
                    await DiagnosticsVM.InitializeAsync();
                    break;
                case "Terminal":
                    await TerminalVM.InitializeAsync();
                    break;
                case "Settings":
                    await SettingsVM.InitializeAsync();
                    break;
            }
        }
        catch { }
        finally
        {
            _currentNavigatingView = string.Empty;
        }
    }

    [RelayCommand]
    public void OpenEnvironmentFolder()
    {
        var path = _configService.Config.Paths.RootPath;
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError("Main", "Failed opening environment folder", ex);
            }
        }
    }

    [RelayCommand]
    public async Task ToggleAllServicesAsync()
    {
        await DashboardVM.ToggleAllServicesAsync();
    }

    private void UpdateActiveTabProperties()
    {
        OnPropertyChanged(nameof(PageTitle));
        OnPropertyChanged(nameof(EnvironmentPath));
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsProjectsActive));
        OnPropertyChanged(nameof(IsDatabasesActive));
        OnPropertyChanged(nameof(IsPHPToolsActive));
        OnPropertyChanged(nameof(IsDiagnosticsActive));
        OnPropertyChanged(nameof(IsTerminalActive));
        OnPropertyChanged(nameof(IsSettingsActive));
    }

    private void OnServiceStateChanged(ServiceInfo info)
    {
        UiDispatcher.Run(UpdateRunningServicesStatus);
    }

    private void UpdateRunningServicesStatus()
    {
        var all = _serviceManager.GetAllServices();
        var running = all.Count(s => s.IsRunning);
        HasRunningServices = running > 0;
        RunningServicesStatus = $"{running} / {all.Count} Running";
    }
}
