using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Loka.App.ViewModels;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Logging;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Loka.Tests;

public class MasterPromptAuditTests : IDisposable
{
    private readonly string _testSandbox;
    private readonly LogService _logger;
    private readonly ConfigurationService _configService;

    public MasterPromptAuditTests()
    {
        _testSandbox = Path.Combine(Path.GetTempPath(), "loka_audit_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testSandbox);

        _logger = new LogService();
        _configService = new ConfigurationService(_logger, Path.Combine(_testSandbox, "test_config.json"));

        _configService.Config.Paths.RootPath = Path.Combine(_testSandbox, "Loka");
        _configService.Config.Paths.ProjectsPath = Path.Combine(_testSandbox, "Loka", "projects");
        _configService.Config.Paths.ConfigPath = Path.Combine(_testSandbox, "Loka", "config");
        _configService.Config.Paths.EtcPath = Path.Combine(_testSandbox, "Loka", "etc");
        _configService.Config.Paths.BinPath = Path.Combine(_testSandbox, "Loka", "bin");
        _configService.Config.Paths.DataPath = Path.Combine(_testSandbox, "Loka", "data");
        _configService.Config.Paths.LogsPath = Path.Combine(_testSandbox, "Loka", "logs");

        Directory.CreateDirectory(_configService.Config.Paths.ProjectsPath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSandbox))
            {
                Directory.Delete(_testSandbox, recursive: true);
            }
        }
        catch { }
    }

    private (MainViewModel mainVM, TerminalViewModel terminalVM, DiagnosticsViewModel diagVM, DatabaseViewModel dbVM, RuntimesViewModel runtimesVM) CreateMainViewModel(
        IServiceManager? svcManager = null,
        IDatabaseService? dbService = null,
        IProcessManager? procManager = null)
    {
        var portService = new MockPortService();
        var processManager = procManager ?? new MockProcessManager();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();
        var phpService = new MockPhpService();
        var sslService = new MockSslCertificateService();
        var hostsService = new MockHostsService();

        var serviceManager = svcManager ?? new Loka.Runtime.Services.ServiceManager(
            processManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var databaseService = dbService ?? new Loka.Database.DatabaseService(_configService, runtimeDetector, processManager, _logger, portService);
        var projectService = new Loka.Project.ProjectService(_configService, apacheService, nginxService, hostsService, sslService, _logger, processManager, databaseService);
        var diagnosticsService = new MockDiagnosticsService();

        var dashboardVM = new DashboardViewModel(serviceManager, runtimeDetector, projectService, _configService, phpService, sslService, _logger, databaseService);
        var projectsVM = new ProjectsViewModel(projectService, serviceManager, _configService, _logger);
        var databaseVM = new DatabaseViewModel(databaseService, _configService, serviceManager, portService, _logger);
        var runtimesVM = new RuntimesViewModel(runtimeDetector, phpService, serviceManager, nginxService, _configService, _logger, processManager);
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, databaseService, _logger);
        var diagVM = new DiagnosticsViewModel(diagnosticsService);
        var terminalVM = new TerminalViewModel(processManager, _configService, phpService, projectService);
        var settingsVM = new SettingsViewModel(_configService, phpService, runtimeDetector, databaseService, serviceManager, apacheService, nginxService, sslService, _logger);

        var mainVM = new MainViewModel(
            serviceManager, _configService, _logger,
            dashboardVM, projectsVM, databaseVM, runtimesVM,
            pmaVM, diagVM, terminalVM, settingsVM);

        return (mainVM, terminalVM, diagVM, databaseVM, runtimesVM);
    }

    // --- Navigation Tests: 7 Menu Final ---

    [Fact]
    public void Audit01_MainViewModel_SevenMenusOnly_OverviewIsDefault()
    {
        var (mainVM, _, _, _, _) = CreateMainViewModel();

        // Default state
        Assert.True(mainVM.IsDashboardActive);
        Assert.Equal("Overview", mainVM.PageTitle);
        Assert.IsType<DashboardViewModel>(mainVM.CurrentViewModel);
    }

    [Fact]
    public async Task Audit02_MainViewModel_ProjectsNavigation_RoutesToProjectsViewModel()
    {
        var (mainVM, _, _, _, _) = CreateMainViewModel();

        await mainVM.NavigateAsync("Projects");

        Assert.True(mainVM.IsProjectsActive);
        Assert.Equal("Projects", mainVM.PageTitle);
        Assert.False(mainVM.IsDashboardActive);
    }

    [Fact]
    public async Task Audit03_MainViewModel_PHPToolsNavigation_RoutesToRuntimesViewModel()
    {
        var (mainVM, _, _, _, runtimesVM) = CreateMainViewModel();

        await mainVM.NavigateAsync("PHPTools");

        Assert.True(mainVM.IsPHPToolsActive);
        Assert.Equal("PHP & Tools", mainVM.PageTitle);
        Assert.Same(runtimesVM, mainVM.CurrentViewModel);
    }

    [Fact]
    public async Task Audit04_MainViewModel_DatabaseNavigation_RoutesToDatabaseViewModel()
    {
        var (mainVM, _, _, dbVM, _) = CreateMainViewModel();

        await mainVM.NavigateAsync("Database");

        Assert.True(mainVM.IsDatabasesActive);
        Assert.Equal("Database", mainVM.PageTitle);
        Assert.Same(dbVM, mainVM.CurrentViewModel);
    }

    [Fact]
    public async Task Audit05_MainViewModel_TerminalNavigation_RoutesToTerminalViewModel()
    {
        var (mainVM, terminalVM, _, _, _) = CreateMainViewModel();

        await mainVM.NavigateAsync("Terminal");

        Assert.True(mainVM.IsTerminalActive);
        Assert.Equal("Terminal", mainVM.PageTitle);
        Assert.Same(terminalVM, mainVM.CurrentViewModel);
    }

    [Fact]
    public async Task Audit06_MainViewModel_DiagnosticsNavigation_RoutesToDiagnosticsViewModel()
    {
        var (mainVM, _, diagVM, _, _) = CreateMainViewModel();

        await mainVM.NavigateAsync("Diagnostics");

        Assert.True(mainVM.IsDiagnosticsActive);
        Assert.Equal("Diagnostics", mainVM.PageTitle);
        Assert.Same(diagVM, mainVM.CurrentViewModel);
    }

    [Fact]
    public async Task Audit07_MainViewModel_SettingsNavigation_WorksCorrectly()
    {
        var (mainVM, _, _, _, _) = CreateMainViewModel();

        await mainVM.NavigateAsync("Settings");

        Assert.True(mainVM.IsSettingsActive);
        Assert.Equal("Settings", mainVM.PageTitle);
    }

    [Fact]
    public async Task Audit08_MainViewModel_AllSevenMenus_CanBeNavigatedStrictly()
    {
        var (mainVM, terminalVM, diagVM, dbVM, runtimesVM) = CreateMainViewModel();

        // 1. Overview
        await mainVM.NavigateAsync("Dashboard");
        Assert.True(mainVM.IsDashboardActive);
        Assert.Equal("Overview", mainVM.PageTitle);

        // 2. Projects
        await mainVM.NavigateAsync("Projects");
        Assert.True(mainVM.IsProjectsActive);
        Assert.Equal("Projects", mainVM.PageTitle);

        // 3. PHP & Tools
        await mainVM.NavigateAsync("PHPTools");
        Assert.True(mainVM.IsPHPToolsActive);
        Assert.Equal("PHP & Tools", mainVM.PageTitle);
        Assert.Same(runtimesVM, mainVM.CurrentViewModel);

        // 4. Database (includes phpMyAdmin access)
        await mainVM.NavigateAsync("Database");
        Assert.True(mainVM.IsDatabasesActive);
        Assert.Equal("Database", mainVM.PageTitle);
        Assert.Same(dbVM, mainVM.CurrentViewModel);

        // 5. Terminal
        await mainVM.NavigateAsync("Terminal");
        Assert.True(mainVM.IsTerminalActive);
        Assert.Equal("Terminal", mainVM.PageTitle);
        Assert.Same(terminalVM, mainVM.CurrentViewModel);

        // 6. Diagnostics
        await mainVM.NavigateAsync("Diagnostics");
        Assert.True(mainVM.IsDiagnosticsActive);
        Assert.Equal("Diagnostics", mainVM.PageTitle);
        Assert.Same(diagVM, mainVM.CurrentViewModel);

        // 7. Settings
        await mainVM.NavigateAsync("Settings");
        Assert.True(mainVM.IsSettingsActive);
        Assert.Equal("Settings", mainVM.PageTitle);
    }

    [Fact]
    public async Task Audit09_MainViewModel_ServicesNotAMenu_NoActiveProperty()
    {
        var (mainVM, _, _, _, _) = CreateMainViewModel();

        // "Services" must not be a routable page; navigating to it keeps current view unchanged
        await mainVM.NavigateAsync("Dashboard");
        var vmBefore = mainVM.CurrentViewModel;

        // There is no IsServicesActive property — sidebar has been removed
        // Attempting to navigate to "Services" should be a no-op (no matching case)
        await mainVM.NavigateAsync("Services");

        // ViewModel does not change to a ServicesViewModel (Services is not a menu)
        Assert.Equal("Services", mainVM.ActiveView); // ActiveView may be set but no routing
        Assert.Null(mainVM.CurrentViewModel as ServicesViewModel);
    }

    [Fact]
    public async Task Audit10_MainViewModel_PhpMyAdminNotAMenu_DatabaseContainsIt()
    {
        var (mainVM, _, _, dbVM, _) = CreateMainViewModel();

        // Navigate to Database, which now hosts phpMyAdmin
        await mainVM.NavigateAsync("Database");
        Assert.Same(dbVM, mainVM.CurrentViewModel);
        Assert.Equal("Database", mainVM.PageTitle);

        // Verify dbVM exposes phpMyAdmin properties
        Assert.NotNull(dbVM.PhpMyAdminUrl);
        Assert.NotEmpty(dbVM.PhpMyAdminUrl);
    }

    [Fact]
    public async Task Audit11_TerminalViewModel_RunPresetCommand_ExecutesAndAppendsOutput()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterSuccess("powershell", "php -v", "PHP 8.4.1 (cli) (built: Oct 2026)");

        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        await terminalVM.RunPresetAsync("php -v");

        Assert.Contains(terminalVM.OutputLines, line => line.Contains("PHP 8.4.1"));
        Assert.Contains(terminalVM.OutputLines, line => line.Contains("php -v"));
    }

    [Fact]
    public async Task Audit12_TerminalViewModel_ExitCode_IsReportedWhenFailed()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterFailure("powershell", "unknown_cmd", "Command not found");

        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        await terminalVM.RunPresetAsync("unknown_cmd");

        Assert.Contains(terminalVM.OutputLines, line => line.Contains("Process exited with code 1"));
        Assert.Contains(terminalVM.OutputLines, line => line.Contains("Command not found"));
        Assert.DoesNotContain(terminalVM.OutputLines, line => line.StartsWith("ERR:"));

        Assert.Single(terminalVM.CommandBlocks);
        var block = terminalVM.CommandBlocks[0];
        Assert.Equal(1, block.ExitCode);
        Assert.True(block.IsFailure);
        Assert.Contains("Command not found", block.FullOutput);
        Assert.DoesNotContain("ERR:", block.FullOutput);
    }

    [Fact]
    public async Task Audit13_DiagnosticsViewModel_RunAndRefresh_PopulateReport()
    {
        var mockDiag = new MockDiagnosticsService();
        var vm = new DiagnosticsViewModel(mockDiag);

        await vm.RunDiagnosticsAsync();

        Assert.Equal(2, vm.Items.Count);
        Assert.True(vm.IsHealthy);

        await vm.RefreshAsync();
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public async Task Audit14_DatabaseViewModel_InitializeAsync_AutoSelectsActiveDatabase()
    {
        var mockDbService = new MockDatabaseServiceForAudit();
        mockDbService.ResolvedServer = "MySQL";

        var portService = new MockPortService();
        var processManager = new MockProcessManager();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();
        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            processManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        _configService.Config.Services.PreferredDatabase = "MariaDB";

        var dbVM = new DatabaseViewModel(mockDbService, _configService, serviceManager, portService, _logger);

        await dbVM.InitializeAsync();

        Assert.Equal("MySQL", dbVM.SelectedEngine);
    }

    [Fact]
    public void Audit15_TerminalViewModel_ProjectSelection_UpdatesWorkingDirectory()
    {
        var mockProcessManager = new MockProcessManager();
        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        var testProjectDir = Path.Combine(_configService.Config.Paths.ProjectsPath, "test-app");
        Directory.CreateDirectory(testProjectDir);

        var project = new ProjectInfo
        {
            Name = "test-app",
            Path = testProjectDir,
            Domain = "test-app.test"
        };

        terminalVM.SelectedProject = project;

        Assert.Equal(testProjectDir, terminalVM.WorkingDirectory);
    }

    [Fact]
    public async Task Audit16_RuntimesViewModel_RunUtility_ExecutesAndOutputs()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterSuccess("powershell", "php -v", "PHP 8.4.1 (cli) (built: Oct 2026)");

        var runtimeDetector = new MockRuntimeDetector();
        var phpService = new MockPhpService();
        var portService = new MockPortService();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var runtimesVM = new RuntimesViewModel(runtimeDetector, phpService, serviceManager, nginxService, _configService, _logger, mockProcessManager);

        await runtimesVM.RunUtilityAsync("php -v");

        Assert.Contains("PHP 8.4.1", runtimesVM.UtilityOutput);
    }

    [Fact]
    public async Task Audit17_DashboardViewModel_CurrentServerDatabase_ResolvesAndSwitches()
    {
        var mockProcessManager = new MockProcessManager();
        var runtimeDetector = new MockRuntimeDetector();
        var phpService = new MockPhpService();
        var portService = new MockPortService();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();
        var sslService = new MockSslCertificateService();
        var hostsService = new MockHostsService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var mockDbService = new MockDatabaseServiceForAudit();
        var projectService = new Loka.Project.ProjectService(_configService, apacheService, nginxService, hostsService, sslService, _logger, mockProcessManager, mockDbService);

        var dashboardVM = new DashboardViewModel(serviceManager, runtimeDetector, projectService, _configService, phpService, sslService, _logger, mockDbService);

        await dashboardVM.UpdateCurrentDatabaseResolutionAsync();
        Assert.Equal("MySQL", dashboardVM.CurrentServerName);
        Assert.Equal("3306", dashboardVM.CurrentServerPort);

        await dashboardVM.SetCurrentServerAsync("MariaDB");
        Assert.Equal("MariaDB", mockDbService.ResolvedServer);
        Assert.Equal("MariaDB", dashboardVM.CurrentServerName);
    }

    [Fact]
    public async Task Audit18_RuntimesViewModel_ComposerSupport_DetectsAndRunsInProject()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterSuccess("powershell", "composer diagnose", "Checking composer.json: OK");

        var runtimeDetector = new MockRuntimeDetector();
        var phpService = new MockPhpService();
        var portService = new MockPortService();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();
        var hostsService = new MockHostsService();
        var sslService = new MockSslCertificateService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var mockDbService = new MockDatabaseServiceForAudit();
        var projectService = new Loka.Project.ProjectService(_configService, apacheService, nginxService, hostsService, sslService, _logger, mockProcessManager, mockDbService);

        var runtimesVM = new RuntimesViewModel(runtimeDetector, phpService, serviceManager, nginxService, _configService, _logger, mockProcessManager, projectService);

        await runtimesVM.RefreshRuntimesAsync();
        Assert.NotEmpty(runtimesVM.AvailableProjects);

        await runtimesVM.RunComposerCommandAsync("diagnose");
        Assert.Contains("Checking composer.json: OK", runtimesVM.UtilityOutput);
    }

    [Fact]
    public void Audit19_TerminalView_ResolvesInverseBoolToVisConverter()
    {
        var conv = new Loka.App.Converters.InverseBooleanToVisibilityConverter();
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(true, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(false, typeof(System.Windows.Visibility), null, System.Globalization.CultureInfo.InvariantCulture));

        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "Views", "TerminalView.xaml");
        if (File.Exists(xamlPath))
        {
            var content = File.ReadAllText(xamlPath);
            Assert.Contains("InverseBoolToVis", content);
            Assert.Contains("UniversalBooleanToVisibilityConverter", content);
        }
    }

    [Fact]
    public async Task Audit20_PhpMyAdmin_Scenario1_ApacheRunning_NginxStopped_DoesNotStartNginx()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        // Scenario 1: Apache running, Nginx stopped
        var apSvc = serviceManager.GetService(ServiceType.Apache)!;
        apSvc.State = ServiceState.Running;
        apSvc.Port = 8080;
        var ngSvc = serviceManager.GetService(ServiceType.Nginx)!;
        ngSvc.State = ServiceState.Stopped;

        portService.OccupiedPorts[9000] = new PortInfo { Port = 9000, IsOccupied = true, ProcessName = "php-cgi" };
        portService.OccupiedPorts[8080] = new PortInfo { Port = 8080, IsOccupied = true, ProcessName = "httpd" };

        var mockDbService = new MockDatabaseServiceForAudit();
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, mockDbService, _logger);

        await pmaVM.OpenPhpMyAdminAsync();

        Assert.True(apSvc.IsRunning);
        Assert.False(ngSvc.IsRunning);
    }

    [Fact]
    public async Task Audit21_PhpMyAdmin_Scenario2_ApacheRunning_NginxRunning_DoesNotStopNginx()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        // Scenario 2: Apache running, Nginx running
        var apSvc = serviceManager.GetService(ServiceType.Apache)!;
        apSvc.State = ServiceState.Running;
        apSvc.Port = 8080;
        var ngSvc = serviceManager.GetService(ServiceType.Nginx)!;
        ngSvc.State = ServiceState.Running;
        ngSvc.Port = 80;

        portService.OccupiedPorts[9000] = new PortInfo { Port = 9000, IsOccupied = true, ProcessName = "php-cgi" };
        portService.OccupiedPorts[8080] = new PortInfo { Port = 8080, IsOccupied = true, ProcessName = "httpd" };
        portService.OccupiedPorts[80] = new PortInfo { Port = 80, IsOccupied = true, ProcessName = "nginx" };

        var mockDbService = new MockDatabaseServiceForAudit();
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, mockDbService, _logger);

        await pmaVM.OpenPhpMyAdminAsync();

        Assert.True(apSvc.IsRunning);
        Assert.True(ngSvc.IsRunning);
    }

    [Fact]
    public async Task Audit22_PhpMyAdmin_Scenario3_ApacheStopped_NginxStopped_DoesNotStartNginx()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        // Scenario 3: Apache stopped, Nginx stopped (Preferred: Apache)
        _configService.Config.Services.PreferredWebServer = "Apache";
        var apSvc = serviceManager.GetService(ServiceType.Apache)!;
        apSvc.State = ServiceState.Stopped;
        var ngSvc = serviceManager.GetService(ServiceType.Nginx)!;
        ngSvc.State = ServiceState.Stopped;

        var mockDbService = new MockDatabaseServiceForAudit();
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, mockDbService, _logger);

        await pmaVM.OpenPhpMyAdminAsync();

        Assert.False(ngSvc.IsRunning);
        Assert.Contains("Apache is stopped", pmaVM.StatusMessage);
    }

    [Fact]
    public async Task Audit22b_PhpMyAdmin_Scenario4_NginxPreferred_Stopped_DoesNotStartApache()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        _configService.Config.Services.PreferredWebServer = "Nginx";
        var apSvc = serviceManager.GetService(ServiceType.Apache)!;
        apSvc.State = ServiceState.Stopped;
        var ngSvc = serviceManager.GetService(ServiceType.Nginx)!;
        ngSvc.State = ServiceState.Stopped;

        var mockDbService = new MockDatabaseServiceForAudit();
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, mockDbService, _logger);

        await pmaVM.OpenPhpMyAdminAsync();

        Assert.False(apSvc.IsRunning);
        Assert.False(ngSvc.IsRunning);
        Assert.Contains("Nginx is stopped", pmaVM.StatusMessage);
        Assert.DoesNotContain("Apache", pmaVM.StatusMessage);
    }

    [Fact]
    public async Task Audit22c_PhpMyAdmin_Scenario5_NginxRunning_ApacheStopped_DoesNotPromptOrStartApache()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var apSvc = serviceManager.GetService(ServiceType.Apache)!;
        apSvc.State = ServiceState.Stopped;
        var ngSvc = serviceManager.GetService(ServiceType.Nginx)!;
        ngSvc.State = ServiceState.Running;
        ngSvc.Port = 80;

        portService.OccupiedPorts[9000] = new PortInfo { Port = 9000, IsOccupied = true, ProcessName = "php-cgi" };
        portService.OccupiedPorts[80] = new PortInfo { Port = 80, IsOccupied = true, ProcessName = "nginx" };

        var mockDbService = new MockDatabaseServiceForAudit();
        var pmaVM = new PhpMyAdminViewModel(serviceManager, _configService, portService, mockDbService, _logger);

        await pmaVM.OpenPhpMyAdminAsync();

        Assert.True(ngSvc.IsRunning);
        Assert.False(apSvc.IsRunning);
        Assert.DoesNotContain("Apache", pmaVM.StatusMessage);
    }

    [Fact]
    public async Task Audit23_DatabaseViewModel_SupportsThreeEngines_AndTestConnection()
    {
        var mockProcessManager = new MockProcessManager();
        var portService = new MockPortService();
        var runtimeDetector = new MockRuntimeDetector();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var mockDbService = new MockDatabaseServiceForAudit();
        var dbVM = new DatabaseViewModel(mockDbService, _configService, serviceManager, portService, _logger);

        // 1. Available engines contains all 3
        Assert.Contains("MySQL", dbVM.AvailableEngines);
        Assert.Contains("MariaDB", dbVM.AvailableEngines);
        Assert.Contains("PostgreSQL", dbVM.AvailableEngines);

        // 2. Switching to PostgreSQL configures pg port and user
        dbVM.SelectedEngine = "PostgreSQL";
        Assert.Equal(5432, dbVM.ConnectionPort);
        Assert.Equal("postgres", dbVM.ConnectionUser);

        // 3. phpMyAdmin informs it does not support PostgreSQL
        await dbVM.OpenPhpMyAdminAsync();
        Assert.Contains("phpMyAdmin supports MySQL and MariaDB only", dbVM.PhpMyAdminStatus);

        // 4. TestConnection command works
        await dbVM.TestConnectionAsync();
        Assert.NotNull(dbVM.ConnectionStatus);
    }

    [Fact]
    public void Audit24_DashboardView_NoDatabaseSwitcherButtons_InXaml()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "Views", "DashboardView.xaml");
        if (File.Exists(xamlPath))
        {
            var content = File.ReadAllText(xamlPath);
            Assert.DoesNotContain("CommandParameter=\"MySQL\"", content);
            Assert.DoesNotContain("CommandParameter=\"MariaDB\"", content);
            Assert.DoesNotContain("Switch:", content);
        }
    }

    [Fact]
    public void Audit25_RuntimesView_HasOnlyPhpManagerAndPhpExtensions_InXaml()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "Views", "RuntimesView.xaml");
        if (File.Exists(xamlPath))
        {
            var content = File.ReadAllText(xamlPath);
            Assert.Contains("PHP MANAGER", content);
            Assert.Contains("PHP EXTENSIONS", content);
            Assert.DoesNotContain("COMPOSER (DEPENDENCY MANAGER)", content);
            Assert.DoesNotContain("composer install", content);
        }
    }

    [Fact]
    public async Task Audit26_Overview_HasNeutralDatabaseServers_AndNoPreferredMariaDb()
    {
        var xamlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "Views", "DashboardView.xaml");
        if (File.Exists(xamlPath))
        {
            var content = File.ReadAllText(xamlPath);
            Assert.Contains("DATABASE SERVERS", content);
            Assert.Contains("WEB SERVERS", content);
            Assert.DoesNotContain("CURRENT DATABASE SERVER", content);
            Assert.DoesNotContain("Preferred: MariaDB", content);
            Assert.DoesNotContain("(Preferred:", content);
        }

        var mockProcessManager = new MockProcessManager();
        var runtimeDetector = new MockRuntimeDetector();
        var phpService = new MockPhpService();
        var portService = new MockPortService();
        var apacheService = new MockApacheService();
        var nginxService = new MockNginxService();
        var mySqlService = new MockMySqlService();
        var sslService = new MockSslCertificateService();
        var hostsService = new MockHostsService();

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            mockProcessManager, portService, _configService, runtimeDetector,
            apacheService, nginxService, mySqlService, _logger);

        var mockDbService = new MockDatabaseServiceForAudit();
        var projectService = new Loka.Project.ProjectService(_configService, apacheService, nginxService, hostsService, sslService, _logger, mockProcessManager, mockDbService);

        var dashboardVM = new DashboardViewModel(serviceManager, runtimeDetector, projectService, _configService, phpService, sslService, _logger, mockDbService);
        await dashboardVM.RefreshAsync();

        Assert.Equal(2, dashboardVM.WebServers.Count);
        Assert.Equal(3, dashboardVM.DatabaseServers.Count);
        Assert.Contains(dashboardVM.DatabaseServers, s => s.Name == "MySQL");
        Assert.Contains(dashboardVM.DatabaseServers, s => s.Name == "MariaDB");
        Assert.Contains(dashboardVM.DatabaseServers, s => s.Name == "PostgreSQL");

        // Changing SelectedEngine on DatabaseViewModel should not alter global PreferredDatabase
        var origPref = _configService.Config.Services.PreferredDatabase;
        var dbVM = new DatabaseViewModel(mockDbService, _configService, serviceManager, portService, _logger);
        dbVM.SelectedEngine = "PostgreSQL";
        Assert.Equal(origPref, _configService.Config.Services.PreferredDatabase);
        dbVM.SelectedEngine = "MySQL";
        Assert.Equal(origPref, _configService.Config.Services.PreferredDatabase);
    }

    [Fact]
    public async Task Audit27_DatabaseServers_IndependentStartAndPortConflictHandling()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_audit_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var mysqlExe = Path.Combine(tempDir, "mysqld.exe");
        var mariadbExe = Path.Combine(tempDir, "mariadbd.exe");
        File.WriteAllText(mysqlExe, "");
        File.WriteAllText(mariadbExe, "");

        try
        {
            var mockProcessManager = new MockProcessManager();
            mockProcessManager.RegisterSuccess("mysqld.exe", "--port=3306", "MySQL started");
            mockProcessManager.RegisterSuccess("mariadbd.exe", "--port=3307", "MariaDB started");

            var portService = new MockPortService();
            var runtimeDetector = new MockRuntimeDetector
            {
                MySqlExePath = mysqlExe,
                MariaDbExePath = mariadbExe
            };
            var apacheService = new MockApacheService();
            var nginxService = new MockNginxService();
            var mySqlService = new MockMySqlService();

            _configService.Config.Services.MySqlPort = 3306;
            _configService.Config.Services.MariaDbPort = 3307;

            var serviceManager = new Loka.Runtime.Services.ServiceManager(
                mockProcessManager, portService, _configService, runtimeDetector,
                apacheService, nginxService, mySqlService, _logger);

            // Start MySQL on 3306
            var mySqlSvc = serviceManager.GetService(Loka.Core.Enums.ServiceType.MySQL)!;
            var startMySql = await serviceManager.StartServiceAsync(Loka.Core.Enums.ServiceType.MySQL);
            Assert.True(startMySql);
            Assert.True(mySqlSvc.IsRunning);

            // Start MariaDB on 3307 -> both running independently
            var mariaSvc = serviceManager.GetService(Loka.Core.Enums.ServiceType.MariaDB)!;
            var startMaria = await serviceManager.StartServiceAsync(Loka.Core.Enums.ServiceType.MariaDB);
            Assert.True(startMaria);
            Assert.True(mariaSvc.IsRunning);
            Assert.True(mySqlSvc.IsRunning);

            // Stop MySQL -> MariaDB remains running
            await serviceManager.StopServiceAsync(Loka.Core.Enums.ServiceType.MySQL);
            Assert.False(mySqlSvc.IsRunning);
            Assert.True(mariaSvc.IsRunning);

            // Stop MariaDB
            await serviceManager.StopServiceAsync(Loka.Core.Enums.ServiceType.MariaDB);
            Assert.False(mariaSvc.IsRunning);

            // Port conflict test: If MariaDB is configured on same port (3306) occupied by mysqld, fail with clear message without killing mysqld
            portService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 9999 };
            mariaSvc.Port = 3306;
            var mariaConflict = await serviceManager.StartServiceAsync(Loka.Core.Enums.ServiceType.MariaDB);
            Assert.False(mariaConflict);
            Assert.Contains("Port 3306 is already in use", mariaSvc.LastError);
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public async Task Audit28_Terminal_DoesNotAddErrPrefixOnStderr_WhenExitCodeZero()
    {
        var mockProcessManager = new MockProcessManager();
        // Simulate composer -V where composer prints PHP info to stderr with ExitCode 0
        mockProcessManager.RegisterSuccess(
            "powershell", 
            "composer -V", 
            "Composer version 2.10.3 2026-08-27 13:34:23\n__LOKA_PWD__:C:\\Loka\\projects");

        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        await terminalVM.RunPresetAsync("composer -V");

        Assert.Single(terminalVM.CommandBlocks);
        var block = terminalVM.CommandBlocks[0];

        Assert.Equal(0, block.ExitCode);
        Assert.True(block.IsSuccess);
        Assert.False(block.IsFailure);
        Assert.Contains("Composer version 2.10.3", block.OutputText);
        Assert.DoesNotContain("ERR:", block.FullOutput);
        Assert.DoesNotContain(terminalVM.OutputLines, line => line.StartsWith("ERR:"));
    }

    [Fact]
    public async Task Audit29_Terminal_ContinuousTerminalText_AndManualTextSelectionBuffer()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterSuccess("powershell", "php -v", "PHP 8.4.26 (cli) (built: Sep 2026)");

        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        await terminalVM.RunPresetAsync("php -v");

        // Unified continuous buffer for mouse dragging and selection
        Assert.NotEmpty(terminalVM.TerminalText);
        Assert.Contains("Loka Developer Shell", terminalVM.TerminalText);
        Assert.Contains("php -v", terminalVM.TerminalText);
        Assert.Contains("PHP 8.4.26", terminalVM.TerminalText);
        Assert.DoesNotContain("ERR:", terminalVM.TerminalText);

        // Verify Clear cleans buffer
        terminalVM.ClearOutputCommand.Execute(null);
        Assert.Empty(terminalVM.TerminalText);
        Assert.Empty(terminalVM.OutputLines);
    }

    [Fact]
    public async Task Audit30_Terminal_PreservesWorkingDirectoryOnDirectoryChange()
    {
        var targetDir = Path.Combine(Path.GetTempPath(), $"loka_term_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(targetDir);

        try
        {
            var mockProcessManager = new MockProcessManager();
            mockProcessManager.RegisterSuccess(
                "powershell",
                "cd " + targetDir,
                $"__LOKA_PWD__:{targetDir}");

            var phpService = new MockPhpService();
            var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

            await terminalVM.RunPresetAsync("cd " + targetDir);

            // Working directory must be updated to targetDir
            Assert.Equal(targetDir, terminalVM.WorkingDirectory);
            var block = terminalVM.CommandBlocks[0];
            // __LOKA_PWD__ metadata must be stripped from visible output
            Assert.DoesNotContain("__LOKA_PWD__", block.OutputText);
        }
        finally
        {
            try { Directory.Delete(targetDir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void Audit29_TerminalView_LoadsWithoutStaticResourceExceptions()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    var app = new System.Windows.Application();
                    var colorsDict = new System.Windows.ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/Loka;component/Themes/Colors.xaml", UriKind.Absolute)
                    };
                    var stylesDict = new System.Windows.ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/Loka;component/Themes/Styles.xaml", UriKind.Absolute)
                    };
                    app.Resources.MergedDictionaries.Add(colorsDict);
                    app.Resources.MergedDictionaries.Add(stylesDict);
                }

                var view = new Loka.App.Views.TerminalView();
                Assert.NotNull(view);
                var outputBox = view.FindName("TerminalOutputBox") as System.Windows.Controls.TextBox;
                var inputBox = view.FindName("CommandInputBox") as System.Windows.Controls.TextBox;
                Assert.NotNull(outputBox);
                Assert.NotNull(inputBox);
                Assert.NotNull(outputBox.ContextMenu);
                Assert.NotNull(inputBox.ContextMenu);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw new Exception("TerminalView failed to instantiate due to XAML/StaticResource exception: " + threadEx.ToString(), threadEx);
        }
    }

    [Fact]
    public void Audit30_ColorsDictionary_DeclaresCardBackgroundBrush()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                if (System.Windows.Application.Current == null)
                {
                    new System.Windows.Application();
                }

                var colorsDict = new System.Windows.ResourceDictionary
                {
                    Source = new Uri("pack://application:,,,/Loka;component/Themes/Colors.xaml", UriKind.Absolute)
                };

                Assert.True(colorsDict.Contains("CardBackgroundBrush"), "Colors.xaml must declare CardBackgroundBrush");
                Assert.True(colorsDict.Contains("CardBrush"), "Colors.xaml must declare CardBrush");
                Assert.True(colorsDict.Contains("PopupBackgroundBrush"), "Colors.xaml must declare PopupBackgroundBrush");
                Assert.True(colorsDict.Contains("PopupBorderBrush"), "Colors.xaml must declare PopupBorderBrush");
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (threadEx != null)
        {
            throw threadEx;
        }
    }

    [Fact]
    public async Task Audit31_TerminalViewModel_PresetsRunWithoutErrPrefix()
    {
        var mockProcessManager = new MockProcessManager();
        mockProcessManager.RegisterSuccess("powershell", "php -v", "PHP 8.3.6 (cli) (built: Apr 11 2024 20:53:39) (NTS Visual C++ 2019 x64)\r\n__LOKA_PWD__:C:\\Loka\\projects");
        mockProcessManager.RegisterSuccess("powershell", "composer -V", "Composer version 2.7.2 2024-03-11 17:12:18\r\n__LOKA_PWD__:C:\\Loka\\projects");
        mockProcessManager.RegisterSuccess("powershell", "node -v", "v20.12.2\r\n__LOKA_PWD__:C:\\Loka\\projects");
        mockProcessManager.RegisterSuccess("powershell", "npm -v", "10.5.0\r\n__LOKA_PWD__:C:\\Loka\\projects");
        mockProcessManager.RegisterSuccess("powershell", "git --version", "git version 2.44.0.windows.1\r\n__LOKA_PWD__:C:\\Loka\\projects");
        mockProcessManager.RegisterSuccess("powershell", "git status", "On branch main\nnothing to commit, working tree clean\r\n__LOKA_PWD__:C:\\Loka\\projects");

        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(mockProcessManager, _configService, phpService);

        var presets = new[] { "php -v", "composer -V", "node -v", "npm -v", "git --version", "git status" };
        foreach (var cmd in presets)
        {
            await terminalVM.RunPresetAsync(cmd);
            Assert.Contains(cmd, terminalVM.TerminalText);
            Assert.DoesNotContain("ERR:", terminalVM.TerminalText);
        }
    }

    [Fact]
    public async Task Audit32_Terminal_OutputTextRendering_And_CommandExecution()
    {
        var pm = new Loka.Infrastructure.Process.ProcessManager(_logger);
        var phpService = new MockPhpService();
        var terminalVM = new TerminalViewModel(pm, _configService, phpService);

        // 1. Write-Output with double quotes
        await terminalVM.RunPresetAsync("Write-Output \"LOKA_TERMINAL_OUTPUT_TEST\"");
        Assert.Contains("LOKA_TERMINAL_OUTPUT_TEST", terminalVM.TerminalText);
        Assert.DoesNotContain("ERR:", terminalVM.TerminalText);
        Assert.DoesNotContain("MissingMandatoryParameter", terminalVM.TerminalText);
        Assert.DoesNotContain("CommandNotFoundException", terminalVM.TerminalText);

        // 2. Clear output
        terminalVM.ClearOutputCommand.Execute(null);
        Assert.Empty(terminalVM.TerminalText);
    }
}

class MockDiagnosticsService : IDiagnosticsService
{
    public Task<IReadOnlyList<DiagnosticItem>> RunDiagnosticsAsync()
    {
        var list = new List<DiagnosticItem>
        {
            new DiagnosticItem { Title = "Apache", IsPassed = true, Details = "Apache 2.4.66" },
            new DiagnosticItem { Title = "MySQL", IsPassed = true, Details = "MySQL 8.4.0" }
        };
        return Task.FromResult<IReadOnlyList<DiagnosticItem>>(list);
    }
}

class MockDatabaseServiceForAudit : IDatabaseService
{
    public string ResolvedServer { get; set; } = "MySQL";

    public Task<CurrentDatabaseResolution> ResolveCurrentDatabaseServerAsync()
    {
        var res = new CurrentDatabaseResolution
        {
            ActiveServerName = ResolvedServer,
            ActivePort = ResolvedServer == "MySQL" ? 3306 : 3307,
            PhpMyAdminServerIndex = ResolvedServer == "MySQL" ? 1 : 2
        };
        return Task.FromResult(res);
    }

    public Task<DatabaseServerStatus> ProbeDatabaseServerAsync(string serverName)
    {
        return Task.FromResult(new DatabaseServerStatus
        {
            Name = serverName,
            IsProcessRunning = true,
            IsPortListening = true,
            IsConnectionSuccessful = true,
            IsVerifiedVendor = true,
            DetectedVendor = serverName,
            ActualPort = serverName == "MySQL" ? 3306 : 3307
        });
    }

    public Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(string? engine = null, int? port = null) => Task.FromResult<IReadOnlyList<DatabaseInfo>>(Array.Empty<DatabaseInfo>());
    public Task<IReadOnlyList<DatabaseTableInfo>> GetTablesAsync(string databaseName, string? engine = null, int? port = null) => Task.FromResult<IReadOnlyList<DatabaseTableInfo>>(Array.Empty<DatabaseTableInfo>());
    public Task<bool> CreateDatabaseAsync(string databaseName, string? engine = null, int? port = null) => Task.FromResult(true);
    public Task<bool> DropDatabaseAsync(string databaseName, string? engine = null, int? port = null) => Task.FromResult(true);
    public Task<SqlQueryResult> ExecuteQueryAsync(string databaseName, string sql, string? engine = null, int? port = null) => Task.FromResult(new SqlQueryResult { IsSuccess = true });
    public Task<bool> ExportDatabaseAsync(string databaseName, string destinationFilePath, Action<string>? progressCallback = null) => Task.FromResult(true);
    public Task<bool> ImportDatabaseAsync(string databaseName, string sourceFilePath, Action<string>? progressCallback = null) => Task.FromResult(true);
    public Task<bool> SetRootPasswordAsync(string newPassword) => Task.FromResult(true);
    public Task<bool> SyncPhpMyAdminConfigAsync(string? activeServerName = null) => Task.FromResult(true);
    public Task<bool> SetCurrentDatabaseServerAsync(string serverName)
    {
        ResolvedServer = serverName;
        return Task.FromResult(true);
    }
}

class MockMySqlService : IMySqlService
{
    public Task<bool> InitializeDataDirectoryAsync() => Task.FromResult(true);
}
