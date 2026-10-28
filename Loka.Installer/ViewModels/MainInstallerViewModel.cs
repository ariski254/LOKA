using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Installer.Models;
using Loka.Installer.Services;

namespace Loka.Installer.ViewModels;

public partial class MainInstallerViewModel : ObservableObject
{
    private readonly InstallerEngine _installerEngine = new();
    private readonly UninstallerEngine _uninstallerEngine = new();

    [ObservableProperty]
    private string _activeView = "Welcome";

    [ObservableProperty]
    private string _installPath = @"C:\Program Files\Loka";

    [ObservableProperty]
    private string _environmentPath = @"C:\Loka";

    [ObservableProperty]
    private bool _createStartMenuShortcut = true;

    [ObservableProperty]
    private bool _createDesktopShortcut = true;

    [ObservableProperty]
    private bool _launchAfterInstall = true;

    [ObservableProperty]
    private bool _removeEnvironmentData = true;

    [ObservableProperty]
    private int _progressPercentage = 0;

    [ObservableProperty]
    private string _statusText = "Ready to install LOKA.";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRecommendedMode))]
    [NotifyPropertyChangedFor(nameof(IsCustomMode))]
    [NotifyPropertyChangedFor(nameof(IsRepairMode))]
    [NotifyPropertyChangedFor(nameof(IsUpdateMode))]
    [NotifyPropertyChangedFor(nameof(InstallActionText))]
    private string _installMode = "Recommended";

    public bool IsRecommendedMode
    {
        get => InstallMode == "Recommended";
        set { if (value) InstallMode = "Recommended"; }
    }

    public bool IsCustomMode
    {
        get => InstallMode == "Custom";
        set { if (value) InstallMode = "Custom"; }
    }

    public bool IsRepairMode
    {
        get => InstallMode == "Repair";
        set { if (value) InstallMode = "Repair"; }
    }

    public bool IsUpdateMode
    {
        get => InstallMode == "Update";
        set { if (value) InstallMode = "Update"; }
    }

    public string InstallActionText => InstallMode switch
    {
        "Repair" => "Repair LOKA",
        "Update" => "Update LOKA",
        _ => "Install LOKA"
    };

    public bool IsOfflineInstaller => StackProvisioningService.HasEmbeddedOfflinePayload;

    public string InstallerTypeTitle => "Loka Setup";

    public string InstallerTypeBadge => "Local Web Development Environment";

    public string InstallerTypeDescription =>
        "Set up your local development environment with Nginx, Apache, PHP 8.3+, MariaDB, MySQL, PostgreSQL, Node.js, and phpMyAdmin for Windows.";

    [ObservableProperty]
    private string _diskSpaceSummary = "";

    // Component toggles for Custom installation mode
    [ObservableProperty]
    private bool _installNginx = true;

    [ObservableProperty]
    private bool _installApache = true;

    [ObservableProperty]
    private bool _installPhp83 = true;

    [ObservableProperty]
    private bool _installPhp84 = true;

    [ObservableProperty]
    private bool _installPhp85 = true;

    [ObservableProperty]
    private bool _installMariaDb = true;

    [ObservableProperty]
    private bool _installMySql = true;

    [ObservableProperty]
    private bool _installPostgreSql = true;

    [ObservableProperty]
    private bool _installNodeJs = true;

    [ObservableProperty]
    private bool _installGit = true;

    [ObservableProperty]
    private bool _installComposer = true;

    [ObservableProperty]
    private bool _installPhpMyAdmin = true;

    public ObservableCollection<InstallationStep> Steps { get; } = new();

    public MainInstallerViewModel(bool isUninstallMode, string? originalDir = null, bool isRepairMode = false, bool isUpdateMode = false)
    {
        InitializeSteps();
        RefreshDiskSpaceSummary();

        if (isRepairMode)
        {
            InstallMode = "Repair";
        }
        else if (isUpdateMode)
        {
            InstallMode = "Update";
        }

        if (isUninstallMode)
        {
            ActiveView = "Uninstall";
            StatusText = "Choose uninstallation options for LOKA.";

            var regInstallDir = WindowsRegistryService.GetInstallLocation();
            if (!string.IsNullOrWhiteSpace(regInstallDir) && Directory.Exists(regInstallDir))
            {
                InstallPath = regInstallDir;
            }
            else if (!string.IsNullOrWhiteSpace(originalDir) && Directory.Exists(originalDir))
            {
                InstallPath = originalDir;
            }

            RemoveEnvironmentData = true;
        }
        else
        {
            ActiveView = "Welcome";
        }
    }

    partial void OnInstallPathChanged(string value)
    {
        RefreshDiskSpaceSummary();
    }

    public void RefreshDiskSpaceSummary()
    {
        DiskSpaceSummary = InstallerEngine.GetDiskSpaceSummary(InstallPath);
    }

    private void InitializeSteps()
    {
        Steps.Clear();
        Steps.Add(new InstallationStep { Title = "Checking system compatibility", Description = "Verifying Windows x64 architecture and disk space" });
        Steps.Add(new InstallationStep { Title = "Preparing application directory", Description = "Setting up installation directory C:\\Program Files\\Loka" });
        Steps.Add(new InstallationStep { Title = "Installing LOKA core files", Description = "Deploying Loka.exe and runtime libraries" });
        Steps.Add(new InstallationStep { Title = "Installing development stack components", Description = "Installing Nginx, Apache, PHP 8.4, PHP 8.5, MariaDB, MySQL, PostgreSQL, Node.js, Git, Composer, phpMyAdmin" });
        Steps.Add(new InstallationStep { Title = "Registering Windows application", Description = "Registering application and uninstaller in Windows" });
        Steps.Add(new InstallationStep { Title = "Creating Windows shortcuts", Description = "Creating Start Menu and Desktop shortcuts" });
        Steps.Add(new InstallationStep { Title = "Finalizing installation", Description = "Completing LOKA setup for first use" });
    }

    [RelayCommand]
    public void SelectAllComponents()
    {
        InstallNginx = true;
        InstallApache = true;
        InstallPhp83 = true;
        InstallPhp84 = true;
        InstallPhp85 = true;
        InstallMariaDb = true;
        InstallMySql = true;
        InstallPostgreSql = true;
        InstallNodeJs = true;
        InstallGit = true;
        InstallComposer = true;
        InstallPhpMyAdmin = true;
    }

    [RelayCommand]
    public void DeselectAllComponents()
    {
        InstallNginx = false;
        InstallApache = false;
        InstallPhp83 = false;
        InstallPhp84 = false;
        InstallPhp85 = false;
        InstallMariaDb = false;
        InstallMySql = false;
        InstallPostgreSql = false;
        InstallNodeJs = false;
        InstallGit = false;
        InstallComposer = false;
        InstallPhpMyAdmin = false;
    }

    [RelayCommand]
    public void BrowseFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select installation directory for LOKA",
            InitialDirectory = Directory.Exists(InstallPath) ? InstallPath : @"C:\Program Files"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            InstallPath = dialog.FolderName;
        }
    }

    [RelayCommand]
    public async Task StartInstallationAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ActiveView = "Installing";

        _installerEngine.InstallPath = InstallPath;
        _installerEngine.EnvironmentPath = EnvironmentPath;
        _installerEngine.InstallMode = InstallMode;
        _installerEngine.CreateStartMenuShortcut = CreateStartMenuShortcut;
        _installerEngine.CreateDesktopShortcut = CreateDesktopShortcut;
        _installerEngine.LaunchAfterInstall = LaunchAfterInstall;

        _installerEngine.SelectedComponents.Clear();
        if (InstallMode == "Recommended")
        {
            _installerEngine.SelectedComponents.Add("Nginx");
            _installerEngine.SelectedComponents.Add("Apache");
            _installerEngine.SelectedComponents.Add("PHP 8.3");
            _installerEngine.SelectedComponents.Add("PHP 8.4");
            _installerEngine.SelectedComponents.Add("PHP 8.5");
            _installerEngine.SelectedComponents.Add("MariaDB");
            _installerEngine.SelectedComponents.Add("MySQL");
            _installerEngine.SelectedComponents.Add("PostgreSQL");
            _installerEngine.SelectedComponents.Add("Node.js");
            _installerEngine.SelectedComponents.Add("Git");
            _installerEngine.SelectedComponents.Add("Composer");
            _installerEngine.SelectedComponents.Add("phpMyAdmin");
        }
        else if (InstallMode == "Custom")
        {
            if (InstallNginx) _installerEngine.SelectedComponents.Add("Nginx");
            if (InstallApache) _installerEngine.SelectedComponents.Add("Apache");
            if (InstallPhp83) _installerEngine.SelectedComponents.Add("PHP 8.3");
            if (InstallPhp84) _installerEngine.SelectedComponents.Add("PHP 8.4");
            if (InstallPhp85) _installerEngine.SelectedComponents.Add("PHP 8.5");
            if (InstallMariaDb) _installerEngine.SelectedComponents.Add("MariaDB");
            if (InstallMySql) _installerEngine.SelectedComponents.Add("MySQL");
            if (InstallPostgreSql) _installerEngine.SelectedComponents.Add("PostgreSQL");
            if (InstallNodeJs) _installerEngine.SelectedComponents.Add("Node.js");
            if (InstallGit) _installerEngine.SelectedComponents.Add("Git");
            if (InstallComposer) _installerEngine.SelectedComponents.Add("Composer");
            if (InstallPhpMyAdmin) _installerEngine.SelectedComponents.Add("phpMyAdmin");
        }

        _installerEngine.ProcessLockPromptHandler = (info) =>
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && dispatcher.Thread.IsAlive && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                if (dispatcher.CheckAccess())
                {
                    return Task.FromResult(ShowProcessLockPrompt(info));
                }

                return dispatcher.Invoke(() => Task.FromResult(ShowProcessLockPrompt(info)));
            }

            return Task.FromResult(ProcessLockAction.CloseAutomatically);
        };

        try
        {
            bool success = await _installerEngine.InstallAsync(
                Steps,
                (percent, status) =>
                {
                    SafeUiAction(() =>
                    {
                        ProgressPercentage = percent;
                        StatusText = status;
                    });
                });

            if (success)
            {
                ActiveView = "Finished";
            }
        }
        catch (OperationCanceledException)
        {
            InstallerLogger.Info("Installation was cancelled by user.");
            StatusText = "Installation cancelled.";
            MessageBox.Show(
                "Installation was cancelled because LOKA is currently running.\n\nPlease close LOKA before running setup.",
                "Installation Cancelled",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ActiveView = "Welcome";
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Fatal error during installation", ex);

            string userMessage;
            if (ex is PackageInstallationException pkgEx)
            {
                userMessage = $"LOKA installation failed on '{pkgEx.PackageName}':\n\n" +
                              $"• Component: {pkgEx.PackageName} (v{pkgEx.PackageVersion})\n" +
                              $"• Phase: {pkgEx.Phase}\n" +
                              $"• Details: {pkgEx.Details}\n\n" +
                              $"Suggested Action:\n{pkgEx.SuggestedAction}\n\n" +
                              $"Installation log saved at:\n{InstallerLogger.LogFilePath}";
            }
            else
            {
                userMessage = $"LOKA installation failed:\n{ex.Message}\n\nInstallation log saved at:\n{InstallerLogger.LogFilePath}";
            }

            MessageBox.Show(
                userMessage,
                "Installation Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            ActiveView = "Welcome";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Finish()
    {
        if (LaunchAfterInstall)
        {
            var exePath = Path.Combine(InstallPath, "Loka.exe");
            if (!File.Exists(exePath)) exePath = Path.Combine(InstallPath, "Loka.App.exe");

            if (File.Exists(exePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    InstallerLogger.Error($"Could not launch '{exePath}'", ex);
                }
            }
        }

        Application.Current.Shutdown();
    }

    [RelayCommand]
    public async Task StartUninstallationAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        ActiveView = "Uninstalling";

        _uninstallerEngine.InstallPath = InstallPath;
        _uninstallerEngine.EnvironmentPath = EnvironmentPath;
        _uninstallerEngine.RemoveEnvironmentData = RemoveEnvironmentData;

        try
        {
            bool success = await _uninstallerEngine.UninstallAsync((percent, status) =>
            {
                SafeUiAction(() =>
                {
                    ProgressPercentage = percent;
                    StatusText = status;
                });
            });

            if (success)
            {
                ActiveView = "UninstallFinished";
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Fatal error during uninstallation", ex);
            MessageBox.Show($"Uninstallation failed:\n{ex.Message}", "LOKA Uninstaller", MessageBoxButton.OK, MessageBoxImage.Error);
            Application.Current.Shutdown();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public void Cancel()
    {
        if (IsBusy)
        {
            var res = MessageBox.Show("Are you sure you want to cancel the process?", "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res != MessageBoxResult.Yes) return;
        }

        Application.Current?.Shutdown();
    }

    private static ProcessLockAction ShowProcessLockPrompt(ProcessLockInfo info)
    {
        var msg = $"LOKA is currently running (Process: {info.ProcessName}, PID: {info.ProcessId}).\n\n" +
                  $"Location: {info.ExecutablePath}\n\n" +
                  "The application must be closed before installation or update can continue.\n\n" +
                  "• Click 'Yes' to close LOKA automatically\n" +
                  "• Click 'No' to retry after closing it manually\n" +
                  "• Click 'Cancel' to abort installation";

        var res = MessageBox.Show(msg, "LOKA is Running", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
        return res switch
        {
            MessageBoxResult.Yes => ProcessLockAction.CloseAutomatically,
            MessageBoxResult.No => ProcessLockAction.Retry,
            _ => ProcessLockAction.Cancel
        };
    }

    private static void SafeUiAction(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || !dispatcher.Thread.IsAlive || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
        {
            action();
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }
}
