using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.App.Common;
using Microsoft.Win32;

namespace Loka.App.ViewModels;

public partial class ProjectsViewModel : ObservableObject
{
    private readonly IProjectService _projectService;
    private readonly IServiceManager _serviceManager;
    private readonly IConfigurationService _configService;
    private readonly ILogService _logger;

    [ObservableProperty]
    private bool _isLoading;

    public bool IsNotLoading => !IsLoading;
    public string CreateButtonText => IsLoading ? "Installing..." : "Create Project";

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotLoading));
        OnPropertyChanged(nameof(CreateButtonText));
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // Add Project Form State (Section 4)
    [ObservableProperty]
    private bool _isAddProjectVisible;

    [ObservableProperty]
    private bool _isCustomPathVisible;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _newProjectPath = string.Empty;

    [ObservableProperty]
    private string _newProjectFramework = "Laravel";

    [ObservableProperty]
    private string _newProjectPhpVersion = "8.4";

    [ObservableProperty]
    private string _newProjectWebServer = "Apache";

    [ObservableProperty]
    private string _newProjectDomain = string.Empty;

    public ObservableCollection<string> FrameworkOptions { get; } = new()
    {
        "Laravel", "Kyro", "WordPress", "Symfony", "CodeIgniter", "Yii2", "CakePHP", "Slim", "Node.js", "Next.js", "Nuxt.js", "Vite", "React", "Vue", "Plain PHP", "Static HTML"
    };

    public ObservableCollection<string> WebServerOptions { get; } = new()
    {
        "Apache", "Nginx"
    };

    public ObservableCollection<string> PhpVersionOptions { get; } = new()
    {
        "8.5", "8.4", "8.3", "Default"
    };

    public ObservableCollection<ProjectInfo> Projects { get; } = new();

    public ProjectsViewModel(
        IProjectService projectService,
        IServiceManager serviceManager,
        IConfigurationService configService,
        ILogService logger)
    {
        _projectService = projectService;
        _serviceManager = serviceManager;
        _configService = configService;
        _logger = logger;

        _configService.ConfigurationChanged += OnConfigurationChanged;
    }

    private void OnConfigurationChanged(LokaConfig config)
    {
        UiDispatcher.Run(() =>
        {
            _ = LoadProjectsAsync();
        });
    }

    public async Task InitializeAsync()
    {
        await LoadProjectsAsync();
    }

    partial void OnNewProjectNameChanged(string value)
    {
        var raw = value ?? string.Empty;
        var clean = raw.Trim().ToLowerInvariant().Replace(" ", "-");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^a-z0-9\-_\.]", "");

        if (!string.IsNullOrEmpty(clean))
        {
            NewProjectDomain = $"{clean}.test";
            if (!IsCustomPathVisible)
            {
                NewProjectPath = Path.Combine(_configService.Config.Paths.ProjectsPath, clean);
            }
        }
        else
        {
            NewProjectDomain = string.Empty;
            if (!IsCustomPathVisible)
            {
                NewProjectPath = string.Empty;
            }
        }
    }

    [RelayCommand]
    public void ToggleCustomPath()
    {
        IsCustomPathVisible = !IsCustomPathVisible;
    }

    [RelayCommand]
    public void ToggleAddProject()
    {
        IsAddProjectVisible = !IsAddProjectVisible;
        if (IsAddProjectVisible)
        {
            NewProjectName = string.Empty;
            NewProjectDomain = string.Empty;
            NewProjectPath = string.Empty;
            IsCustomPathVisible = false;
        }
    }

    [RelayCommand]
    public void BrowseProjectPath()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Project Root Folder",
            InitialDirectory = Directory.Exists(_configService.Config.Paths.ProjectsPath) 
                ? _configService.Config.Paths.ProjectsPath 
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (fbd.ShowDialog() == true)
        {
            NewProjectPath = fbd.FolderName;
            var dirName = Path.GetFileName(fbd.FolderName);
            if (!string.IsNullOrEmpty(dirName))
            {
                NewProjectName = dirName;
                NewProjectDomain = $"{dirName.ToLowerInvariant().Replace(" ", "-")}.test";
            }
        }
    }

    [RelayCommand]
    public async Task CreateProjectAsync()
    {
        if (IsLoading) return;

        var rawName = NewProjectName?.Trim();
        if (string.IsNullOrWhiteSpace(rawName))
        {
            StatusMessage = "Project name cannot be empty.";
            return;
        }

        var clean = rawName.ToLowerInvariant().Replace(" ", "-");
        clean = System.Text.RegularExpressions.Regex.Replace(clean, @"[^a-z0-9\-_\.]", "");

        if (string.IsNullOrWhiteSpace(clean))
        {
            StatusMessage = "Invalid characters in project name.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewProjectDomain))
        {
            NewProjectDomain = $"{clean}.test";
        }

        if (string.IsNullOrWhiteSpace(NewProjectPath))
        {
            NewProjectPath = Path.Combine(_configService.Config.Paths.ProjectsPath, clean);
        }

        StatusMessage = $"Preparing project installation '{rawName}' ({NewProjectFramework})...";
        IsLoading = true;

        try
        {
            var success = await _projectService.CreateAndInstallProjectAsync(
                rawName,
                NewProjectPath,
                NewProjectDomain,
                NewProjectFramework,
                NewProjectPhpVersion,
                NewProjectWebServer,
                msg => UiDispatcher.Run(() => StatusMessage = msg));

            if (success)
            {
                // Auto restart web server so virtual host is immediately live
                var webServerType = string.Equals(NewProjectWebServer, "Nginx", StringComparison.OrdinalIgnoreCase)
                    ? ServiceType.Nginx
                    : ServiceType.Apache;
                var ws = _serviceManager.GetService(webServerType);
                if (ws != null && ws.IsRunning)
                {
                    StatusMessage = $"Restarting web server ({webServerType})...";
                    await _serviceManager.RestartServiceAsync(webServerType);
                }

                StatusMessage = $"Project '{rawName}' ({NewProjectFramework}) created and installed successfully -> http://{NewProjectDomain}";
                IsAddProjectVisible = false;
                NewProjectName = string.Empty;
                NewProjectPath = string.Empty;
                NewProjectDomain = string.Empty;
                IsCustomPathVisible = false;

                await LoadProjectsAsync();
            }
            else
            {
                StatusMessage = $"Failed to create project '{rawName}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Create project error: {ex.Message}";
            _logger.LogError("Projects", "Failed creating project", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadProjectsAsync()
    {
        IsLoading = true;
        StatusMessage = "Scanning projects directory...";

        try
        {
            var list = await _projectService.ScanProjectsAsync();
            Projects.Clear();
            foreach (var p in list)
            {
                Projects.Add(p);
            }
            StatusMessage = $"Found {Projects.Count} project(s) in {_configService.Config.Paths.ProjectsPath}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Scan error: {ex.Message}";
            _logger.LogError("Projects", "Error scanning projects", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task SyncAllDomainsAsync()
    {
        IsLoading = true;
        StatusMessage = "Syncing all project domains to Windows hosts...";

        try
        {
            var success = await _projectService.SyncAllProjectsAsync();
            if (success)
            {
                StatusMessage = "All .test domains successfully synced and configured!";
            }
            else
            {
                StatusMessage = "Domain sync finished. If UAC prompt appeared, please allow to grant hosts permission.";
            }
            await LoadProjectsAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sync error: {ex.Message}";
            _logger.LogError("Projects", "Failed syncing domains", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task StartProjectAsync(ProjectInfo? project)
    {
        if (project == null) return;
        StatusMessage = $"Starting services for '{project.Name}'...";
        try
        {
            var webServerType = string.Equals(_configService.Config.Services.PreferredWebServer, "Nginx", StringComparison.OrdinalIgnoreCase)
                ? ServiceType.Nginx
                : ServiceType.Apache;

            var ok = await _serviceManager.StartServiceAsync(webServerType);
            if (ok)
            {
                project.IsRunning = true;
                StatusMessage = $"Project '{project.Name}' is active at http://{project.Domain}";
            }
            else
            {
                var ws = _serviceManager.GetService(webServerType);
                StatusMessage = ws?.LastError ?? $"Failed to start web server for '{project.Name}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed starting service: {ex.Message}";
        }
    }

    [RelayCommand]
    public void StopProject(ProjectInfo? project)
    {
        if (project == null) return;
        project.IsRunning = false;
        StatusMessage = $"Project '{project.Name}' stopped.";
    }

    [RelayCommand]
    public async Task ConfigureVirtualHostAsync(ProjectInfo? project)
    {
        if (project == null) return;
        StatusMessage = $"Configuring domain '{project.Domain}'...";
        try
        {
            bool ok = await _projectService.ConfigureVirtualHostAsync(project);
            if (ok)
            {
                StatusMessage = $"Virtual host configured for {project.Domain}.";
                await LoadProjectsAsync();
            }
            else
            {
                StatusMessage = $"Failed configuring virtual host for {project.Domain}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RemoveProjectAsync(ProjectInfo? project)
    {
        if (project == null) return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to remove project '{project.Name}' ({project.Domain}) from Loka?\n\n(Note: Your project files and folders on disk will remain 100% safe and will NOT be deleted).",
            "Remove Project from Loka",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = $"Removing project '{project.Name}' from Loka...";

        try
        {
            var success = await _projectService.DeleteProjectAsync(project, deleteFiles: false);
            if (success)
            {
                Projects.Remove(project);
                StatusMessage = $"Project '{project.Name}' was removed from Loka (files preserved).";
                await LoadProjectsAsync();
            }
            else
            {
                StatusMessage = $"Failed to remove project '{project.Name}' from Loka.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to remove project: {ex.Message}";
            _logger.LogError("Projects", $"Error removing project '{project.Name}'", ex);
        }
    }

    [RelayCommand]
    public async Task DeleteProjectAndFilesAsync(ProjectInfo? project)
    {
        if (project == null) return;

        var confirm = MessageBox.Show(
            $"Are you sure you want to PERMANENTLY DELETE project '{project.Name}' and ALL its files from disk?\n\n" +
            $"Target Directory: {project.Path}\n\n" +
            "WARNING: This action CANNOT be undone! All source code and files inside this directory will be permanently erased.",
            "Delete Project and Files",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        StatusMessage = $"Permanently deleting project and files for '{project.Name}'...";

        try
        {
            var success = await _projectService.DeleteProjectAsync(project, deleteFiles: true);
            if (success)
            {
                Projects.Remove(project);
                StatusMessage = $"Project '{project.Name}' and its physical files were permanently deleted.";
                await LoadProjectsAsync();
            }
            else
            {
                StatusMessage = $"Failed to delete physical folder for '{project.Name}'. File or directory may be locked or in use.";
                MessageBox.Show(
                    $"Failed to delete project directory:\n{project.Path}\n\nThe directory or a file may be locked by another program or you lack permissions.\nProject was not deleted.",
                    "Deletion Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error deleting project: {ex.Message}";
            _logger.LogError("Projects", $"Error permanently deleting project '{project.Name}'", ex);
            MessageBox.Show(
                $"Error deleting project '{project.Name}':\n\n{ex.Message}",
                "Deletion Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public void OpenInBrowser(ProjectInfo? project)
    {
        if (project == null) return;
        // Default clean HTTP URL (e.g. http://project.test or http://kyro.test) per Ui.md
        var url = project.LocalUrl;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Projects", $"Failed to open URL '{url}'", ex);
        }
    }

    [RelayCommand]
    public void OpenFolder(ProjectInfo? project)
    {
        if (project == null) return;
        var targetPath = !string.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path) 
            ? project.Path 
            : _configService.Config.Paths.ProjectsPath;

        try
        {
            if (Directory.Exists(targetPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetPath,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{targetPath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Projects", $"Failed opening folder '{targetPath}'", ex);
        }
    }

    [RelayCommand]
    public void OpenTerminal(ProjectInfo? project)
    {
        if (project == null || !Directory.Exists(project.Path)) return;
        try
        {
            var lokaPath = _configService.GetInternalEnvironmentPath();
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                WorkingDirectory = project.Path,
                UseShellExecute = true
            };

            if (!string.IsNullOrEmpty(lokaPath))
            {
                psi.Arguments = $"-NoExit -Command \"$env:PATH='{lokaPath};' + $env:PATH; Write-Host 'Loka Developer Environment Active' -ForegroundColor Cyan\"";
            }

            Process.Start(psi);
        }
        catch (Exception ex)
        {
            _logger.LogError("Projects", $"Failed opening terminal in '{project.Path}'", ex);
        }
    }
}
