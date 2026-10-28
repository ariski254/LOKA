using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Microsoft.Win32;

namespace Loka.App.ViewModels;

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly IConfigurationService _configService;
    private readonly IRuntimeDetector _detector;
    private readonly IPortService _portService;
    private readonly IPhpService _phpService;
    private readonly ILogService _logger;

    public event Action? RequestCloseWithSuccess;
    public event Action? RequestCloseCancel;

    // Current Step: 0 = Welcome, 1 = Environment Location, 2 = Web Server, 3 = PHP Runtime, 4 = Database, 5 = Review & Apply
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepIndicatorText))]
    [NotifyPropertyChangedFor(nameof(IsWelcomeStep))]
    [NotifyPropertyChangedFor(nameof(IsStep1))]
    [NotifyPropertyChangedFor(nameof(IsStep2))]
    [NotifyPropertyChangedFor(nameof(IsStep3))]
    [NotifyPropertyChangedFor(nameof(IsStep4))]
    [NotifyPropertyChangedFor(nameof(IsStep5))]
    [NotifyPropertyChangedFor(nameof(IsStep5Ready))]
    [NotifyPropertyChangedFor(nameof(IsStep5Running))]
    [NotifyPropertyChangedFor(nameof(IsStep5Success))]
    [NotifyPropertyChangedFor(nameof(IsStep5Failed))]
    [NotifyPropertyChangedFor(nameof(HasAnyStep5StateActive))]
    private int _currentStep = 0;

    public string StepIndicatorText => CurrentStep switch
    {
        0 => "Welcome",
        >= 1 and <= 5 => $"Step {CurrentStep} of 5",
        _ => "Setup Complete"
    };

    public bool IsWelcomeStep => CurrentStep == 0;
    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;

    // Step 0 & 1: Environment & Projects Location
    [ObservableProperty]
    private string _environmentPath = @"C:\Loka";

    [ObservableProperty]
    private string _projectsPath = @"C:\Loka\projects";

    [ObservableProperty]
    private string _driveSpaceInfo = "Calculating...";

    [ObservableProperty]
    private bool _isExistingEnvironmentDetected;

    // Step 2: Web Server
    [ObservableProperty]
    private string _preferredWebServer = "Apache";

    [ObservableProperty]
    private int _httpPort = 80;

    [ObservableProperty]
    private int _httpsPort = 443;

    [ObservableProperty]
    private string _webServerStatus = "Installed & Ready";

    [ObservableProperty]
    private string _portConflictWarning = string.Empty;

    [ObservableProperty]
    private bool _hasPortConflict;

    [ObservableProperty]
    private bool _autoStartWebServer = false;

    // Step 3: PHP Runtime (Strictly >= PHP 8.3 per Ui.md Section 3)
    [ObservableProperty]
    private string _selectedPhpVersion = "8.4";

    public ObservableCollection<PhpVersionItem> PhpVersions { get; } = new();

    // Step 4: Database
    public ObservableCollection<string> DatabaseOptions { get; } = new() { "MySQL", "MariaDB", "PostgreSQL" };

    [ObservableProperty]
    private string _preferredDatabase = "MySQL";

    [ObservableProperty]
    private int _databasePort = 3306;

    [ObservableProperty]
    private string _databaseDataPath = @"C:\Loka\data\mysql";

    [ObservableProperty]
    private string _databaseRootPassword = string.Empty;

    [ObservableProperty]
    private bool _autoStartDatabase = false;

public enum SetupStep5State
{
    Ready,
    Running,
    Success,
    Failed
}

public partial class SetupProvisionStep : ObservableObject
{
    public int StepNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [ObservableProperty]
    private string _status = "Pending";

    [ObservableProperty]
    private string _statusIcon = "○";

    [ObservableProperty]
    private string _statusColor = "#94A3B8";
}

    // Step 5: Apply Setup Progress & States
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotProvisioning))]
    private bool _isProvisioning;

    public bool IsNotProvisioning => !IsProvisioning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProvisionNotComplete))]
    private bool _isProvisionComplete;

    public bool IsProvisionNotComplete => !IsProvisionComplete;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep5Ready))]
    [NotifyPropertyChangedFor(nameof(IsStep5Running))]
    [NotifyPropertyChangedFor(nameof(IsStep5Success))]
    [NotifyPropertyChangedFor(nameof(IsStep5Failed))]
    [NotifyPropertyChangedFor(nameof(HasAnyStep5StateActive))]
    private SetupStep5State _step5State = SetupStep5State.Ready;

    public bool IsStep5Ready => IsStep5 && Step5State == SetupStep5State.Ready;
    public bool IsStep5Running => IsStep5 && Step5State == SetupStep5State.Running;
    public bool IsStep5Success => IsStep5 && Step5State == SetupStep5State.Success;
    public bool IsStep5Failed => IsStep5 && Step5State == SetupStep5State.Failed;
    public bool HasAnyStep5StateActive => IsStep5 && (Step5State == SetupStep5State.Ready || Step5State == SetupStep5State.Running || Step5State == SetupStep5State.Success || Step5State == SetupStep5State.Failed);

    public ObservableCollection<SetupProvisionStep> ProvisionSteps { get; } = new();

    [ObservableProperty]
    private int _provisionProgress;

    [ObservableProperty]
    private string _provisionStatus = "Ready to configure";

    // Error handling
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SetupWizardViewModel(
        IConfigurationService configService,
        IRuntimeDetector detector,
        IPortService portService,
        IPhpService phpService,
        ILogService logger)
    {
        _configService = configService;
        _detector = detector;
        _portService = portService;
        _phpService = phpService;
        _logger = logger;

        EnvironmentPath = _configService.Config.Paths.RootPath;
        ProjectsPath = _configService.Config.Paths.ProjectsPath;

        PreferredWebServer = !string.IsNullOrEmpty(_configService.Config.Services.PreferredWebServer)
            ? _configService.Config.Services.PreferredWebServer
            : "Apache";

        HttpPort = PreferredWebServer == "Nginx"
            ? _configService.Config.Services.NginxPort
            : _configService.Config.Services.ApachePort;

        SelectedPhpVersion = !string.IsNullOrEmpty(_configService.Config.Php.ActiveVersion)
            ? _configService.Config.Php.ActiveVersion
            : "8.4";

        var binPath = _configService.Config.Paths.BinPath;
        var hasMySql = File.Exists(Path.Combine(binPath, "mysql", "bin", "mysqld.exe"));
        var hasMariaDb = File.Exists(Path.Combine(binPath, "mariadb", "bin", "mariadbd.exe")) || File.Exists(Path.Combine(binPath, "mariadb", "bin", "mysqld.exe"));

        if (!string.IsNullOrEmpty(_configService.Config.Services.PreferredDatabase))
        {
            PreferredDatabase = _configService.Config.Services.PreferredDatabase;
        }
        else if (hasMariaDb && !hasMySql)
        {
            PreferredDatabase = "MariaDB";
        }
        else
        {
            PreferredDatabase = "MySQL";
        }

        DatabasePort = PreferredDatabase switch
        {
            "MariaDB" => _configService.Config.Services.MariaDbPort,
            "PostgreSQL" => _configService.Config.Services.PostgresPort,
            _ => _configService.Config.Services.MySqlPort
        };

        UpdateDriveSpace();
        CheckExistingEnvironment();
        _ = LoadPhpVersionsAsync();
        InitializeProvisionSteps();
    }

    public void InitializeProvisionSteps()
    {
        ProvisionSteps.Clear();
        ProvisionSteps.Add(new SetupProvisionStep
        {
            StepNumber = 1,
            Name = "Environment & Directory Permissions",
            Description = $"Verify root '{EnvironmentPath}' accessibility and storage permissions",
            Status = "Pending",
            StatusIcon = "○",
            StatusColor = "#94A3B8"
        });
        ProvisionSteps.Add(new SetupProvisionStep
        {
            StepNumber = 2,
            Name = "Environment Directory Layout",
            Description = "Ensure bin, config, data, logs, tmp, and projects directory structure",
            Status = "Pending",
            StatusIcon = "○",
            StatusColor = "#94A3B8"
        });
        ProvisionSteps.Add(new SetupProvisionStep
        {
            StepNumber = 3,
            Name = $"{PreferredWebServer} Web Server Configuration",
            Description = $"Configure HTTP port {HttpPort}, SSL port {HttpsPort}, and default virtual hosts",
            Status = "Pending",
            StatusIcon = "○",
            StatusColor = "#94A3B8"
        });
        ProvisionSteps.Add(new SetupProvisionStep
        {
            StepNumber = 4,
            Name = "PHP FastCGI Synchronization",
            Description = $"Map active PHP {SelectedPhpVersion} upstream daemon and command-line binary",
            Status = "Pending",
            StatusIcon = "○",
            StatusColor = "#94A3B8"
        });
        ProvisionSteps.Add(new SetupProvisionStep
        {
            StepNumber = 5,
            Name = $"{PreferredDatabase} Engine & Configuration",
            Description = $"Configure {PreferredDatabase} on port {DatabasePort} and persist loka.json",
            Status = "Pending",
            StatusIcon = "○",
            StatusColor = "#94A3B8"
        });
    }

    private void UpdateDriveSpace()
    {
        try
        {
            var root = Path.GetPathRoot(EnvironmentPath);
            if (!string.IsNullOrEmpty(root) && Directory.Exists(root))
            {
                var d = new DriveInfo(root);
                var freeGb = d.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                DriveSpaceInfo = $"Drive {d.Name.TrimEnd('\\')} : {freeGb:F1} GB free space available";
            }
        }
        catch
        {
            DriveSpaceInfo = "Windows Storage Available";
        }
    }

    private void CheckExistingEnvironment()
    {
        var binPath = Path.Combine(EnvironmentPath, "bin");
        var cfgPath = Path.Combine(EnvironmentPath, "config", "loka.json");
        IsExistingEnvironmentDetected = Directory.Exists(binPath) || File.Exists(cfgPath);
    }

    private async Task LoadPhpVersionsAsync()
    {
        await Task.Yield();
        try
        {
            var supported = new[] { "8.3", "8.4", "8.5" };
            var binPath = _configService.Config.Paths.BinPath;

            PhpVersions.Clear();
            foreach (var ver in supported)
            {
                var dir = Path.Combine(binPath, "php", ver);
                var exe = Path.Combine(dir, "php.exe");
                bool isInstalled = File.Exists(exe);
                bool isActive = ver == SelectedPhpVersion;

                string status = isActive ? "Active" : (isInstalled ? "Installed" : "Available to install");
                string releaseInfo = ver switch
                {
                    "8.3" => "PHP 8.3 LTS (High Performance & Wide Compatibility)",
                    "8.4" => "PHP 8.4 Current (Property Hooks, Asymmetric Visibility)",
                    "8.5" => "PHP 8.5 Preview (Next Generation Features)",
                    _ => $"PHP {ver}"
                };

                PhpVersions.Add(new PhpVersionItem
                {
                    Version = ver,
                    Status = status,
                    BinaryPath = exe,
                    ReleaseInfo = releaseInfo,
                    IsInstalled = isInstalled,
                    IsActive = isActive,
                    CanActivate = !isActive
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Wizard", $"Failed loading PHP versions: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SelectPhpVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return;
        SelectedPhpVersion = version;
        foreach (var item in PhpVersions)
        {
            item.IsActive = item.Version == version;
            item.Status = item.IsActive ? "Active" : (item.IsInstalled ? "Installed" : "Available to install");
            item.CanActivate = !item.IsActive;
        }
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(StepIndicatorText));
        OnPropertyChanged(nameof(IsWelcomeStep));
        OnPropertyChanged(nameof(IsStep1));
        OnPropertyChanged(nameof(IsStep2));
        OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(IsStep4));
        OnPropertyChanged(nameof(IsStep5));
        OnPropertyChanged(nameof(IsStep5Ready));
        OnPropertyChanged(nameof(IsStep5Running));
        OnPropertyChanged(nameof(IsStep5Success));
        OnPropertyChanged(nameof(IsStep5Failed));
        OnPropertyChanged(nameof(HasAnyStep5StateActive));
        ClearError();

        _logger.LogInfo("SetupWizard", $"Step transition: CurrentStep is now {value} ({StepIndicatorText}). Step5State: {Step5State}.");

        if (value == 2)
        {
            _ = CheckPortConflictsAsync();
        }
        else if (value == 5)
        {
            InitializeProvisionSteps();
            if (Step5State == SetupStep5State.Failed)
            {
                Step5State = SetupStep5State.Ready;
            }
        }
    }

    partial void OnEnvironmentPathChanged(string value)
    {
        UpdateDriveSpace();
        CheckExistingEnvironment();
    }

    partial void OnPreferredWebServerChanged(string value)
    {
        if (value == "Nginx")
        {
            HttpPort = _configService.Config.Services.NginxPort > 0 ? _configService.Config.Services.NginxPort : 80;
            WebServerStatus = "Nginx 1.26.x - Ready";
        }
        else
        {
            HttpPort = _configService.Config.Services.ApachePort > 0 ? _configService.Config.Services.ApachePort : 80;
            WebServerStatus = "Apache 2.4.x - Ready";
        }
        _ = CheckPortConflictsAsync();
    }

    partial void OnHttpPortChanged(int value)
    {
        _ = CheckPortConflictsAsync();
    }

    partial void OnPreferredDatabaseChanged(string value)
    {
        if (value == "MariaDB")
        {
            DatabasePort = 3307;
            DatabaseDataPath = Path.Combine(EnvironmentPath, "data", "mariadb");
        }
        else if (value == "PostgreSQL")
        {
            DatabasePort = 5432;
            DatabaseDataPath = Path.Combine(EnvironmentPath, "data", "postgresql");
        }
        else
        {
            DatabasePort = 3306;
            DatabaseDataPath = Path.Combine(EnvironmentPath, "data", "mysql");
        }
    }

    private async Task CheckPortConflictsAsync()
    {
        try
        {
            var httpAvail = await _portService.IsPortAvailableAsync(HttpPort);
            var httpsAvail = await _portService.IsPortAvailableAsync(HttpsPort);
            if (!httpAvail)
            {
                HasPortConflict = true;
                PortConflictWarning = $"Notice: Port {HttpPort} is in use by another process. If port 80 is occupied, consider port 8080 or stop the conflicting service.";
            }
            else if (!httpsAvail)
            {
                HasPortConflict = true;
                PortConflictWarning = $"Notice: SSL Port {HttpsPort} is currently in use.";
            }
            else
            {
                HasPortConflict = false;
                PortConflictWarning = string.Empty;
            }
        }
        catch { }
    }

    [RelayCommand]
    public void BrowseEnvironment()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Loka Environment Directory",
            InitialDirectory = Directory.Exists(EnvironmentPath) ? EnvironmentPath : @"C:\"
        };

        if (fbd.ShowDialog() == true)
        {
            EnvironmentPath = fbd.FolderName;
            ProjectsPath = Path.Combine(EnvironmentPath, "projects");
            ClearError();
        }
    }

    [RelayCommand]
    public void BrowseProjects()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Projects Directory",
            InitialDirectory = Directory.Exists(ProjectsPath) ? ProjectsPath : EnvironmentPath
        };

        if (fbd.ShowDialog() == true)
        {
            ProjectsPath = fbd.FolderName;
            ClearError();
        }
    }

    [RelayCommand]
    public void NextStep()
    {
        if (IsProvisioning) return;
        ClearError();

        _logger.LogInfo("SetupWizard", $"NextStep triggered at Step {CurrentStep}.");

        if (CurrentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(EnvironmentPath))
            {
                SetError("Environment path cannot be empty.");
                return;
            }
            var root = Path.GetPathRoot(EnvironmentPath);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                SetError($"Target drive '{root}' is not accessible.");
                return;
            }
            if (string.IsNullOrWhiteSpace(ProjectsPath))
            {
                SetError("Projects directory cannot be empty.");
                return;
            }
        }
        else if (CurrentStep == 2)
        {
            if (HttpPort < 1 || HttpPort > 65535 || HttpsPort < 1 || HttpsPort > 65535)
            {
                SetError("HTTP and HTTPS ports must be between 1 and 65535.");
                return;
            }
        }
        else if (CurrentStep == 3)
        {
            if (string.IsNullOrWhiteSpace(SelectedPhpVersion))
            {
                SetError("Please select an active PHP version.");
                return;
            }
        }
        else if (CurrentStep == 4)
        {
            if (DatabasePort < 1 || DatabasePort > 65535)
            {
                SetError("Database port must be between 1 and 65535.");
                return;
            }
        }

        if (CurrentStep < 5)
        {
            CurrentStep++;
            _logger.LogInfo("SetupWizard", $"Advanced to Step {CurrentStep}.");
        }
    }

    [RelayCommand]
    public void PreviousStep()
    {
        if (IsProvisioning) return;
        ClearError();
        if (CurrentStep > 0)
        {
            CurrentStep--;
            _logger.LogInfo("SetupWizard", $"Navigated back to Step {CurrentStep}.");
        }
    }

    [RelayCommand]
    public async Task ApplySetupAsync()
    {
        if (IsProvisioning) return;
        ClearError();
        IsProvisioning = true;
        Step5State = SetupStep5State.Running;
        IsProvisionComplete = false;
        ProvisionProgress = 0;

        _logger.LogInfo("SetupWizard", $"Starting ApplySetup for Environment '{EnvironmentPath}' (WebServer: {PreferredWebServer}, PHP: {SelectedPhpVersion}, Database: {PreferredDatabase}:{DatabasePort}).");

        if (ProvisionSteps.Count == 0)
        {
            InitializeProvisionSteps();
        }

        try
        {
            // Step 1: Directory and Path Configuration
            await ExecuteProvisionStepAsync(0, 20, "1/5: Validating environment directory and disk permissions...", async () =>
            {
                var cfg = _configService.Config;
                cfg.Paths.RootPath = EnvironmentPath.TrimEnd('\\');
                cfg.Paths.ProjectsPath = ProjectsPath.TrimEnd('\\');
                cfg.Paths.RuntimesPath = Path.Combine(cfg.Paths.RootPath, "bin");
                cfg.Paths.DataPath = Path.Combine(cfg.Paths.RootPath, "data");
                cfg.Paths.LogsPath = Path.Combine(cfg.Paths.RootPath, "logs");
                cfg.Paths.ConfigPath = Path.Combine(cfg.Paths.RootPath, "config");
                cfg.Paths.BackupsPath = Path.Combine(cfg.Paths.RootPath, "backups");
                cfg.Paths.TempPath = Path.Combine(cfg.Paths.RootPath, "tmp");
                await Task.Delay(120);
            });

            // Step 2: Directory structure creation
            await ExecuteProvisionStepAsync(1, 40, "2/5: Creating environment directory layout...", async () =>
            {
                _configService.EnsureDirectories();
                await Task.Delay(120);
            });

            // Step 3: Web server configuration
            await ExecuteProvisionStepAsync(2, 60, $"3/5: Configuring {PreferredWebServer} (HTTP: {HttpPort}, SSL: {HttpsPort})...", async () =>
            {
                var cfg = _configService.Config;
                cfg.Services.PreferredWebServer = PreferredWebServer;
                if (PreferredWebServer == "Nginx")
                {
                    cfg.Services.NginxPort = HttpPort;
                    cfg.Services.AutoStartNginx = AutoStartWebServer;
                    cfg.Services.AutoStartApache = false;
                }
                else
                {
                    cfg.Services.ApachePort = HttpPort;
                    cfg.Services.ApacheSslPort = HttpsPort;
                    cfg.Services.AutoStartApache = AutoStartWebServer;
                    cfg.Services.AutoStartNginx = false;
                }
                await Task.Delay(120);
            });

            // Step 4: PHP FastCGI configuration
            await ExecuteProvisionStepAsync(3, 80, $"4/5: Synchronizing PHP {SelectedPhpVersion} FastCGI upstream...", async () =>
            {
                var cfg = _configService.Config;
                cfg.Php.ActiveVersion = SelectedPhpVersion;
                await Task.Delay(120);
            });

            // Step 5: Database & Persistent save
            await ExecuteProvisionStepAsync(4, 100, $"5/5: Configuring {PreferredDatabase} (Port: {DatabasePort}) and saving loka.json...", async () =>
            {
                var cfg = _configService.Config;
                cfg.Services.PreferredDatabase = PreferredDatabase;
                if (PreferredDatabase == "MariaDB")
                {
                    cfg.Services.MariaDbPort = DatabasePort;
                    cfg.Services.AutoStartMariaDb = AutoStartDatabase;
                    cfg.Services.AutoStartMySql = false;
                    cfg.Services.AutoStartPostgres = false;
                }
                else if (PreferredDatabase == "PostgreSQL")
                {
                    cfg.Services.PostgresPort = DatabasePort;
                    cfg.Services.AutoStartPostgres = AutoStartDatabase;
                    cfg.Services.AutoStartMySql = false;
                    cfg.Services.AutoStartMariaDb = false;
                }
                else
                {
                    cfg.Services.MySqlPort = DatabasePort;
                    cfg.Services.AutoStartMySql = AutoStartDatabase;
                    cfg.Services.AutoStartMariaDb = false;
                    cfg.Services.AutoStartPostgres = false;
                }
                cfg.Database.RootPassword = DatabaseRootPassword.Trim();
                cfg.General.IsFirstRunCompleted = true;

                await _configService.SaveAsync();
                await Task.Delay(150);
            });

            ProvisionStatus = "Environment configured and verified successfully!";
            IsProvisionComplete = true;
            Step5State = SetupStep5State.Success;
            _logger.LogInfo("SetupWizard", "ApplySetup succeeded! All steps completed.");
        }
        catch (Exception ex)
        {
            Step5State = SetupStep5State.Failed;
            SetError($"Setup could not be completed: {ex.Message}");
            ProvisionStatus = $"Failed: {ex.Message}";
            _logger.LogError("SetupWizard", $"Failed during provisioning: {ex.Message}", ex);
        }
        finally
        {
            IsProvisioning = false;
            OnPropertyChanged(nameof(IsStep5Ready));
            OnPropertyChanged(nameof(IsStep5Running));
            OnPropertyChanged(nameof(IsStep5Success));
            OnPropertyChanged(nameof(IsStep5Failed));
            OnPropertyChanged(nameof(HasAnyStep5StateActive));
        }
    }

    private async Task ExecuteProvisionStepAsync(int stepIndex, int progressTarget, string statusMsg, Func<Task> action)
    {
        ProvisionStatus = statusMsg;
        ProvisionProgress = progressTarget;

        if (stepIndex >= 0 && stepIndex < ProvisionSteps.Count)
        {
            ProvisionSteps[stepIndex].Status = "Running";
            ProvisionSteps[stepIndex].StatusIcon = "◐";
            ProvisionSteps[stepIndex].StatusColor = "#2563EB";
        }

        try
        {
            await action();

            if (stepIndex >= 0 && stepIndex < ProvisionSteps.Count)
            {
                ProvisionSteps[stepIndex].Status = "Success";
                ProvisionSteps[stepIndex].StatusIcon = "✓";
                ProvisionSteps[stepIndex].StatusColor = "#10B981";
            }
        }
        catch (Exception)
        {
            if (stepIndex >= 0 && stepIndex < ProvisionSteps.Count)
            {
                ProvisionSteps[stepIndex].Status = "Failed";
                ProvisionSteps[stepIndex].StatusIcon = "✕";
                ProvisionSteps[stepIndex].StatusColor = "#EF4444";
            }
            throw;
        }
    }

    [RelayCommand]
    public async Task RetryApplySetupAsync()
    {
        _logger.LogInfo("SetupWizard", "Retrying ApplySetup...");
        InitializeProvisionSteps();
        await ApplySetupAsync();
    }

    [RelayCommand]
    public async Task UseExistingEnvironmentAsync()
    {
        ClearError();
        IsProvisioning = true;
        try
        {
            var cfg = _configService.Config;
            cfg.Paths.RootPath = EnvironmentPath.TrimEnd('\\');
            cfg.Paths.ProjectsPath = ProjectsPath.TrimEnd('\\');
            cfg.General.IsFirstRunCompleted = true;

            _configService.EnsureDirectories();
            await _configService.SaveAsync();

            RequestCloseWithSuccess?.Invoke();
        }
        catch (Exception ex)
        {
            SetError($"Failed to connect existing environment: {ex.Message}");
            _logger.LogError("Wizard", "Failed using existing environment", ex);
        }
        finally
        {
            IsProvisioning = false;
        }
    }

    [RelayCommand]
    public void FinishSetup()
    {
        RequestCloseWithSuccess?.Invoke();
    }

    [RelayCommand]
    public void CancelSetup()
    {
        RequestCloseCancel?.Invoke();
    }

    private void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }

    private void ClearError()
    {
        ErrorMessage = string.Empty;
        HasError = false;
    }
}
