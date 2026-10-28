using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.App.ViewModels;

public partial class RuntimesViewModel : ObservableObject
{
    private readonly IRuntimeDetector _detector;
    private readonly IPhpService _phpService;
    private readonly IServiceManager _serviceManager;
    private readonly INginxService _nginxService;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logger;
    private readonly IProcessManager? _processManager;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private string _activePhpBinary = string.Empty;

    [ObservableProperty]
    private string _selectedPhpVersion = string.Empty;

    [ObservableProperty]
    private string _phpCliVersion = string.Empty;

    [ObservableProperty]
    private string _phpIniPath = string.Empty;

    [ObservableProperty]
    private string _phpLoadedModules = string.Empty;

    // Composer Section (Agent.md Section 4.C)
    [ObservableProperty]
    private bool _isComposerInstalled;

    [ObservableProperty]
    private string _composerVersion = "Not detected";

    [ObservableProperty]
    private string _composerPath = "-";

    [ObservableProperty]
    private string _composerStatus = "Checking...";

    [ObservableProperty]
    private string _selectedComposerProject = "(Default Projects Root)";

    [ObservableProperty]
    private string _customComposerCommand = "diagnose";

    [ObservableProperty]
    private string _utilityOutput = string.Empty;

    [ObservableProperty]
    private bool _isExecutingUtility;

    public ObservableCollection<RuntimeInfo> Runtimes { get; } = new();
    public ObservableCollection<string> AvailablePhpVersions { get; } = new();
    public ObservableCollection<PhpVersionItem> PhpVersions { get; } = new();
    public ObservableCollection<PhpExtensionInfo> Extensions { get; } = new();
    public ObservableCollection<string> AvailableProjects { get; } = new();

    private readonly IProjectService? _projectService;

    public RuntimesViewModel(
        IRuntimeDetector detector,
        IPhpService phpService,
        IServiceManager serviceManager,
        INginxService nginxService,
        IConfigurationService configService,
        ILogService logger,
        IProcessManager? processManager = null,
        IProjectService? projectService = null)
    {
        _detector = detector;
        _phpService = phpService;
        _serviceManager = serviceManager;
        _nginxService = nginxService;
        _configService = configService;
        _logger = logger;
        _processManager = processManager;
        _projectService = projectService;
    }

    public async Task InitializeAsync()
    {
        await RefreshRuntimesAsync();
    }

    [RelayCommand]
    public async Task RefreshRuntimesAsync()
    {
        IsLoading = true;
        StatusMessage = "Detecting installed development runtimes...";

        try
        {
            // 1. Detect all system and local runtimes
            var detected = await _detector.DetectAllRuntimesAsync();
            Runtimes.Clear();
            foreach (var r in detected)
            {
                Runtimes.Add(r);
            }

            // 2. Load available PHP versions (Strictly starting from PHP 8.3 per UX.md Section 2)
            var phpVersions = await _phpService.GetAvailablePhpVersionsAsync();
            AvailablePhpVersions.Clear();
            foreach (var v in phpVersions)
            {
                if (Version.TryParse(v, out var pv) && pv >= new Version(8, 3))
                {
                    AvailablePhpVersions.Add(v);
                }
            }

            ActivePhpBinary = _phpService.GetActivePhpBinary();
            SelectedPhpVersion = _configService.Config.Php.ActiveVersion;

            // Build detailed PHP version catalog with clear status differentiation
            var supportedVersions = new[] { "8.3", "8.4", "8.5" };
            var binPath = _configService.Config.Paths.BinPath;

            PhpVersions.Clear();
            foreach (var v in supportedVersions)
            {
                var candidateBinary = Path.Combine(binPath, "php", v, "php.exe");
                bool isInstalled = File.Exists(candidateBinary);
                bool isActive = isInstalled && string.Equals(SelectedPhpVersion, v, StringComparison.OrdinalIgnoreCase);

                string status;
                string releaseInfo;
                if (isActive)
                {
                    status = "Active";
                    releaseInfo = "Official NTS x64 (Currently Active Runtime)";
                }
                else if (isInstalled)
                {
                    status = "Installed";
                    releaseInfo = "Official NTS x64 (Ready to Activate)";
                }
                else
                {
                    status = "Available to install";
                    releaseInfo = "Official Release (windows.php.net)";
                }

                PhpVersions.Add(new PhpVersionItem
                {
                    Version = v,
                    Status = status,
                    BinaryPath = isInstalled ? candidateBinary : "Not installed",
                    ReleaseInfo = releaseInfo,
                    IsActive = isActive,
                    IsInstalled = isInstalled,
                    CanActivate = isInstalled && !isActive
                });
            }

            // 3. Load PHP Extensions
            var exts = await _phpService.GetExtensionsAsync();
            Extensions.Clear();
            foreach (var ext in exts)
            {
                Extensions.Add(ext);
            }

            // 4. Load PHP CLI inspection details (Rule 11)
            try
            {
                var (verOut, iniOut, modOut) = await _phpService.GetPhpCliDetailsAsync();
                PhpCliVersion = verOut.Split('\n').FirstOrDefault() ?? verOut;
                PhpIniPath = iniOut;
                PhpLoadedModules = modOut;
            }
            catch { }

            // 5. Detect Composer (Agent.md Section 4.C)
            try
            {
                var composer = await _detector.DetectComposerAsync();
                if (composer != null && composer.IsInstalled)
                {
                    IsComposerInstalled = true;
                    ComposerVersion = composer.Version ?? "Installed";
                    ComposerPath = composer.ExecutablePath ?? "composer";
                    ComposerStatus = composer.Status ?? "Installed";
                }
                else
                {
                    IsComposerInstalled = false;
                    ComposerVersion = "Not installed";
                    ComposerPath = "-";
                    ComposerStatus = "Not detected";
                }
            }
            catch
            {
                IsComposerInstalled = false;
                ComposerVersion = "Not installed";
                ComposerPath = "-";
                ComposerStatus = "Error detecting Composer";
            }

            // 6. Scan Projects for Composer Target Directory
            if (_projectService != null)
            {
                try
                {
                    var projs = await _projectService.ScanProjectsAsync();
                    AvailableProjects.Clear();
                    AvailableProjects.Add("(Default Projects Root)");
                    foreach (var p in projs)
                    {
                        AvailableProjects.Add(p.Name);
                    }
                    if (string.IsNullOrEmpty(SelectedComposerProject) || !AvailableProjects.Contains(SelectedComposerProject))
                    {
                        SelectedComposerProject = AvailableProjects.FirstOrDefault() ?? "(Default Projects Root)";
                    }
                }
                catch { }
            }
            else if (AvailableProjects.Count == 0)
            {
                AvailableProjects.Add("(Default Projects Root)");
                SelectedComposerProject = "(Default Projects Root)";
            }

            StatusMessage = $"PHP & Composer detection complete. PHP active: {SelectedPhpVersion}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Runtime scan error: {ex.Message}";
            _logger.LogError("Runtimes", "Failed detecting runtimes", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SetActivePhpVersionAsync(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return;

        var versionItem = PhpVersions.FirstOrDefault(p => p.Version == version);
        if (versionItem != null)
        {
            if (versionItem.IsBusy) return;
            versionItem.IsBusy = true;
        }

        StatusMessage = $"Switching active PHP version to {version}...";
        try
        {
            bool ok = await _phpService.SetActivePhpVersionAsync(version);
            if (ok)
            {
                SelectedPhpVersion = version;
                ActivePhpBinary = _phpService.GetActivePhpBinary();

                // Ensure FastCGI PHP-CGI daemon is restarted and listening on port 9000
                await _serviceManager.RestartPhpCgiAsync();

                // If Nginx is running, validate and reload Nginx
                var nginx = _serviceManager.GetService(Loka.Core.Enums.ServiceType.Nginx);
                if (nginx != null && nginx.IsRunning)
                {
                    var (isNginxValid, _) = await _nginxService.ValidateConfigurationAsync();
                    if (isNginxValid)
                    {
                        await _serviceManager.RestartServiceAsync(Loka.Core.Enums.ServiceType.Nginx);
                    }
                }

                StatusMessage = $"Active PHP version successfully set to {version}. Upstream FastCGI synchronized.";
                await RefreshRuntimesAsync();
            }
            else
            {
                StatusMessage = $"Failed to switch PHP to {version}. Retaining active version '{_configService.Config.Php.ActiveVersion}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            if (versionItem != null)
            {
                versionItem.IsBusy = false;
            }
        }
    }

    [RelayCommand]
    public async Task ToggleExtensionAsync(PhpExtensionInfo? ext)
    {
        if (ext == null || ext.IsBusy) return;

        var targetState = !ext.IsEnabled;
        ext.IsBusy = true;
        StatusMessage = $"Updating extension '{ext.Name}' to {(targetState ? "Enabled" : "Disabled")}...";

        try
        {
            bool ok = await _phpService.SetExtensionEnabledAsync(ext.Name, targetState);
            if (ok)
            {
                ext.IsEnabled = targetState;
                StatusMessage = $"PHP extension '{ext.Name}' is now {(targetState ? "enabled" : "disabled")}.";

                // Reload FastCGI daemon so the extension state takes effect immediately
                await _serviceManager.RestartPhpCgiAsync();
            }
            else
            {
                StatusMessage = $"Could not update '{ext.Name}'. Make sure php.ini is writable and extension DLL exists.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error updating '{ext.Name}': {ex.Message}";
            _logger.LogError("Runtimes", $"Failed toggling extension '{ext.Name}'", ex);
        }
        finally
        {
            ext.IsBusy = false;
        }
    }

    [RelayCommand]
    public void OpenRuntimeFolder(RuntimeInfo? runtime)
    {
        if (runtime == null || !Directory.Exists(runtime.DirectoryPath)) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = runtime.DirectoryPath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Runtimes", $"Failed opening runtime directory '{runtime.DirectoryPath}'", ex);
        }
    }

    [RelayCommand]
    public void OpenPhpIni()
    {
        var bin = _phpService.GetActivePhpBinary();
        if (File.Exists(bin))
        {
            var dir = Path.GetDirectoryName(bin) ?? "";
            var ini = Path.Combine(dir, "php.ini");
            if (File.Exists(ini))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{ini}\"",
                    UseShellExecute = true
                });
                StatusMessage = $"Opened {ini}";
                return;
            }
        }

        StatusMessage = "Active php.ini could not be found.";
    }

    [RelayCommand]
    public async Task RunComposerCommandAsync(string? subCommand)
    {
        if (string.IsNullOrWhiteSpace(subCommand)) return;
        var fullCommand = subCommand.StartsWith("composer", StringComparison.OrdinalIgnoreCase)
            ? subCommand
            : $"composer {subCommand}";

        string targetDir = _configService.Config.Paths.ProjectsPath;
        if (_projectService != null && !string.IsNullOrEmpty(SelectedComposerProject) && SelectedComposerProject != "(Default Projects Root)")
        {
            try
            {
                var projs = await _projectService.ScanProjectsAsync();
                var matched = projs.FirstOrDefault(p => p.Name.Equals(SelectedComposerProject, StringComparison.OrdinalIgnoreCase));
                if (matched != null && Directory.Exists(matched.Path))
                {
                    targetDir = matched.Path;
                }
            }
            catch { }
        }

        await ExecuteCommandInDirectoryAsync(fullCommand, targetDir);
    }

    [RelayCommand]
    public async Task RunCustomComposerCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CustomComposerCommand)) return;
        await RunComposerCommandAsync(CustomComposerCommand);
    }

    [RelayCommand]
    public async Task RunUtilityAsync(string? command)
    {
        if (string.IsNullOrWhiteSpace(command) || IsExecutingUtility) return;
        var workDir = Directory.Exists(_configService.Config.Paths.ProjectsPath) 
            ? _configService.Config.Paths.ProjectsPath 
            : @"C:\";
        await ExecuteCommandInDirectoryAsync(command, workDir);
    }

    public async Task ExecuteCommandInDirectoryAsync(string command, string workDir)
    {
        if (IsExecutingUtility) return;
        IsExecutingUtility = true;
        UtilityOutput = $"[Directory: {workDir}]\n$ {command}...\n";

        try
        {
            var lokaEnvPath = _configService.GetInternalEnvironmentPath();
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var env = new Dictionary<string, string>
            {
                ["LOKA_ENV_PATH"] = lokaEnvPath,
                ["PATH"] = string.IsNullOrEmpty(lokaEnvPath) ? existingPath : $"{lokaEnvPath};{existingPath}"
            };

            if (_processManager != null)
            {
                if (!Directory.Exists(workDir))
                {
                    workDir = @"C:\";
                }

                var result = await _processManager.ExecuteAsync(
                    "powershell.exe",
                    $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{command.Replace("\"", "\\\"")}\"",
                    workingDirectory: workDir,
                    environmentVariables: env);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[Directory: {workDir}]");
                sb.AppendLine($"$ {command}");
                if (!string.IsNullOrEmpty(result.StandardOutput))
                {
                    sb.AppendLine(result.StandardOutput.TrimEnd());
                }
                if (!string.IsNullOrEmpty(result.StandardError))
                {
                    sb.AppendLine($"[Error] {result.StandardError.TrimEnd()}");
                }
                if (result.ExitCode != 0)
                {
                    sb.AppendLine($"[Process exited with code {result.ExitCode}]");
                }
                UtilityOutput = sb.ToString();
            }
            else
            {
                UtilityOutput = "Process manager service not available.";
            }
        }
        catch (Exception ex)
        {
            UtilityOutput = $"Execution error: {ex.Message}";
        }
        finally
        {
            IsExecutingUtility = false;
        }
    }

    [RelayCommand]
    public void ClearUtilityOutput()
    {
        UtilityOutput = string.Empty;
    }
}
