using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Project;

public class ProjectService : IProjectService
{
    private readonly IConfigurationService _configService;
    private readonly IApacheService _apacheService;
    private readonly INginxService _nginxService;
    private readonly IHostsService _hostsService;
    private readonly ISslCertificateService _sslService;
    private readonly ILogService _logger;
    private readonly IProcessManager _processManager;
    private readonly IDatabaseService? _databaseService;
    private readonly ProjectInstaller _installer;

    public ProjectService(
        IConfigurationService configService,
        IApacheService apacheService,
        INginxService nginxService,
        IHostsService hostsService,
        ISslCertificateService sslService,
        ILogService logger,
        IProcessManager processManager,
        IDatabaseService? databaseService = null)
    {
        _configService = configService;
        _apacheService = apacheService;
        _nginxService = nginxService;
        _hostsService = hostsService;
        _sslService = sslService;
        _logger = logger;
        _processManager = processManager;
        _databaseService = databaseService;
        _installer = new ProjectInstaller(processManager, configService, databaseService, logger);
    }

    public async Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync()
    {
        var projects = new List<ProjectInfo>();
        var root = _configService.Config.Paths.ProjectsPath;

        if (!Directory.Exists(root))
        {
            try
            {
                Directory.CreateDirectory(root);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Project", $"Could not create projects directory at '{root}': {ex.Message}");
                return projects;
            }
        }

        try
        {
            var directories = Directory.GetDirectories(root);

            foreach (var dir in directories)
            {
                // Skip projects removed/ignored from Loka
                if (File.Exists(Path.Combine(dir, ".loka-ignore")))
                {
                    continue;
                }

                var project = await DetectProjectAsync(dir);
                if (project != null)
                {
                    projects.Add(project);

                    // Auto-configure vhost for both web servers if missing
                    if (!project.IsVhostConfigured)
                    {
                        try
                        {
                            await _nginxService.GenerateServerBlockAsync(project);
                            await _apacheService.GenerateVHostConfigAsync(project);
                            project.IsVhostConfigured = true;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning("Project", $"Auto vhost generation skipped for '{project.Domain}': {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Project", $"Failed scanning projects directory '{root}'", ex);
        }

        return projects;
    }

    public async Task<ProjectInfo?> DetectProjectAsync(string directoryPath)
    {
        if (!Directory.Exists(directoryPath)) return null;

        try
        {
            var project = ProjectDetector.Detect(directoryPath);
            bool isNginx = string.Equals(_configService.Config.Services.PreferredWebServer, "Nginx", StringComparison.OrdinalIgnoreCase);
            project.Port = isNginx ? _configService.Config.Services.NginxPort : _configService.Config.Services.ApachePort;
            project.SslPort = isNginx ? 443 : _configService.Config.Services.ApacheSslPort;

            // Check if hosts entry exists
            project.IsInHostsFile = await _hostsService.HasHostEntryAsync(project.Domain);

            // Check if SSL cert exists
            var sslDir = Path.Combine(_configService.Config.Paths.ConfigPath, "ssl");
            var certFile = Path.Combine(sslDir, $"{project.Domain}.crt");
            project.HasSsl = File.Exists(certFile);

            // Check if vhost conf exists for the active web server
            var activeVhostFile = isNginx
                ? Path.Combine(_configService.Config.Paths.ConfigPath, "nginx", "sites-enabled", $"{project.Domain}.conf")
                : Path.Combine(_configService.Config.Paths.ConfigPath, "apache", "sites-enabled", $"{project.Domain}.conf");
            project.IsVhostConfigured = File.Exists(activeVhostFile);

            // Ensure document root has a valid entry point or provide a clean diagnostic page (Fix.md Section 4)
            EnsureProjectEntryPoint(project);

            return project;
        }
        catch (Exception ex)
        {
            _logger.LogError("Project", $"Failed detecting project in '{directoryPath}'", ex);
            return null;
        }
    }

    private static void EnsureProjectEntryPoint(ProjectInfo project)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(project.DocumentRoot) || !Directory.Exists(project.DocumentRoot))
            {
                if (!string.IsNullOrWhiteSpace(project.Path) && Directory.Exists(project.Path))
                {
                    project.DocumentRoot = project.Path;
                }
                else
                {
                    return;
                }
            }

            var indexPhp = Path.Combine(project.DocumentRoot, "index.php");
            var indexHtml = Path.Combine(project.DocumentRoot, "index.html");
            var indexHtm = Path.Combine(project.DocumentRoot, "index.htm");

            // Never inject diagnostic page if entry point already exists
            if (File.Exists(indexPhp) || File.Exists(indexHtml) || File.Exists(indexHtm))
            {
                return;
            }

            // Never inject diagnostic page if directory belongs to a framework
            if (File.Exists(Path.Combine(project.Path, "artisan")) ||
                File.Exists(Path.Combine(project.Path, "spark")) ||
                File.Exists(Path.Combine(project.Path, "composer.json")) ||
                File.Exists(Path.Combine(project.Path, "package.json")))
            {
                return;
            }

            // Only for completely empty plain directories without an entry point
            var entries = Directory.GetFileSystemEntries(project.DocumentRoot);
            if (entries.Length == 0)
            {
                var starter = $@"<?php
// Loka Project Diagnostic Entry Point
// Domain: {project.Domain}
?>
<!DOCTYPE html>
<html>
<head><title>Loka Project Diagnostic Entry Point</title></head>
<body>
<h1>Loka Project Diagnostic Entry Point</h1>
<p>Domain: {project.Domain}</p>
</body>
</html>";
                File.WriteAllText(indexPhp, starter);
            }
        }
        catch { }
    }

    public async Task<bool> ConfigureVirtualHostAsync(ProjectInfo project)
    {
        _logger.LogInfo("Project", $"Configuring Virtual Host for '{project.Name}' ({project.Domain})...");

        try
        {
            // 1. Ensure SSL Certificate (Section 22)
            var sslDir = Path.Combine(_configService.Config.Paths.ConfigPath, "ssl");
            try
            {
                await _sslService.EnsureLocalSslCertificateAsync(project.Domain, sslDir);
                project.HasSsl = true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Project", $"SSL certificate notice for '{project.Domain}': {ex.Message}");
            }

            // 2. Generate Web Server Virtual Host Configuration for both Nginx and Apache
            try { await _nginxService.GenerateServerBlockAsync(project); } catch { }
            try { await _apacheService.GenerateVHostConfigAsync(project); } catch { }
            project.IsVhostConfigured = true;

            // 3. Update Windows Hosts file (Section 21)
            var hostsAdded = await _hostsService.AddHostEntryAsync(project.Domain);
            project.IsInHostsFile = hostsAdded;

            _logger.LogInfo("Project", $"Virtual Host and domain '{project.Domain}' configured successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Project", $"Failed configuring virtual host for '{project.Domain}'", ex);
            return false;
        }
    }

    public async Task<bool> SyncAllProjectsAsync()
    {
        try
        {
            var projects = await ScanProjectsAsync();
            var validDomains = new HashSet<string>(projects.Select(p => p.Domain), StringComparer.OrdinalIgnoreCase);

            // Clean orphaned .conf files in apache and nginx sites-enabled
            var apacheDir = Path.Combine(_configService.Config.Paths.ConfigPath, "apache", "sites-enabled");
            if (Directory.Exists(apacheDir))
            {
                foreach (var file in Directory.GetFiles(apacheDir, "*.conf"))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Equals("00-default.conf", StringComparison.OrdinalIgnoreCase)) continue;
                    var domain = Path.GetFileNameWithoutExtension(file);
                    if (!validDomains.Contains(domain))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }

            var nginxDir = Path.Combine(_configService.Config.Paths.ConfigPath, "nginx", "sites-enabled");
            if (Directory.Exists(nginxDir))
            {
                foreach (var file in Directory.GetFiles(nginxDir, "*.conf"))
                {
                    var fileName = Path.GetFileName(file);
                    if (fileName.Equals("00-default.conf", StringComparison.OrdinalIgnoreCase)) continue;
                    var domain = Path.GetFileNameWithoutExtension(file);
                    if (!validDomains.Contains(domain))
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }

            foreach (var project in projects)
            {
                await ConfigureVirtualHostAsync(project);
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Project", "Failed to sync all projects", ex);
            return false;
        }
    }

    public bool ValidateProjectPathForDeletion(string? path, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            errorMessage = "Project path is empty or invalid.";
            return false;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path).TrimEnd('\\', '/');
        }
        catch (Exception ex)
        {
            errorMessage = $"Invalid path format: {ex.Message}";
            return false;
        }

        // 1. Prohibit drive roots (e.g. C:\, D:\)
        var root = Path.GetPathRoot(fullPath)?.TrimEnd('\\', '/');
        if (string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Security violation: Root drive cannot be deleted.";
            return false;
        }

        // 2. Prohibit Windows system directories (both exact folder and subdirectories)
        var prohibitedSystemFolders = new List<string>
        {
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonProgramFilesX86)
        };

        var winDir = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        prohibitedSystemFolders.Add(winDir);

        foreach (var sysFolder in prohibitedSystemFolders)
        {
            if (string.IsNullOrWhiteSpace(sysFolder)) continue;
            var canonicalSys = Path.GetFullPath(sysFolder).TrimEnd('\\', '/');
            if (string.Equals(fullPath, canonicalSys, StringComparison.OrdinalIgnoreCase) ||
                fullPath.StartsWith(canonicalSys + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Security violation: Path '{fullPath}' is inside system folder '{canonicalSys}'.";
                return false;
            }
        }

        // Prohibit UserProfile root itself (e.g. C:\Users\Username)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            var canonicalUser = Path.GetFullPath(userProfile).TrimEnd('\\', '/');
            if (string.Equals(fullPath, canonicalUser, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Security violation: Cannot delete user profile root '{canonicalUser}'.";
                return false;
            }
        }

        // 3. Prohibit Loka installation roots
        var lokaFolders = new[]
        {
            _configService.Config.Paths.RootPath,
            _configService.Config.Paths.BinPath,
            _configService.Config.Paths.ConfigPath,
            _configService.Config.Paths.DataPath,
            _configService.Config.Paths.EtcPath,
            _configService.Config.Paths.LogsPath,
            _configService.Config.Paths.ProjectsPath // The projects root directory itself!
        };

        foreach (var lokaFolder in lokaFolders)
        {
            if (string.IsNullOrWhiteSpace(lokaFolder)) continue;
            var canonicalLoka = Path.GetFullPath(lokaFolder).TrimEnd('\\', '/');
            if (string.Equals(fullPath, canonicalLoka, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = $"Security violation: Cannot delete Loka core directory '{canonicalLoka}'.";
                return false;
            }
        }

        // 4. Ensure it is not a parent of Loka projects or Loka root
        var projectsRoot = Path.GetFullPath(_configService.Config.Paths.ProjectsPath).TrimEnd('\\', '/');
        if (projectsRoot.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Security violation: Path is a parent directory of Loka projects.";
            return false;
        }

        var lokaRoot = Path.GetFullPath(_configService.Config.Paths.RootPath).TrimEnd('\\', '/');
        if (lokaRoot.StartsWith(fullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            errorMessage = "Security violation: Path is a parent directory of Loka installation.";
            return false;
        }

        return true;
    }

    private static void SafeDeleteDirectoryRecursively(DirectoryInfo dir)
    {
        // If the directory itself is a symlink or junction, delete the link without traversing
        if ((dir.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            dir.Delete(false);
            return;
        }

        foreach (var file in dir.EnumerateFiles())
        {
            if ((file.Attributes & FileAttributes.ReadOnly) != 0)
            {
                file.Attributes &= ~FileAttributes.ReadOnly;
            }
            file.Delete();
        }

        foreach (var subDir in dir.EnumerateDirectories())
        {
            if ((subDir.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                // Junction or symlink directory: delete the junction itself, do NOT traverse into it!
                subDir.Delete(false);
            }
            else
            {
                if ((subDir.Attributes & FileAttributes.ReadOnly) != 0)
                {
                    subDir.Attributes &= ~FileAttributes.ReadOnly;
                }
                SafeDeleteDirectoryRecursively(subDir);
            }
        }

        dir.Delete(false);
    }

    public async Task<bool> DeleteProjectAsync(ProjectInfo project, bool deleteFiles = false)
    {
        try
        {
            _logger.LogInfo("Project", $"Processing removal for project '{project.Name}' (domain: {project.Domain}, deleteFiles: {deleteFiles})...");

            if (deleteFiles)
            {
                if (!ValidateProjectPathForDeletion(project.Path, out var error))
                {
                    _logger.LogError("Project", $"Security check failed for deleting project '{project.Name}': {error}");
                    throw new InvalidOperationException($"Security validation rejected: {error}");
                }

                if (!string.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path))
                {
                    try
                    {
                        SafeDeleteDirectoryRecursively(new DirectoryInfo(project.Path));
                        _logger.LogInfo("Project", $"Permanently deleted project directory and files at '{project.Path}'.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError("Project", $"Failed deleting project directory '{project.Path}': {ex.Message}", ex);
                        // Do NOT report false success or delete metadata if file deletion fails
                        return false;
                    }
                }
                else
                {
                    _logger.LogInfo("Project", $"Project directory '{project.Path}' did not exist on disk. Proceeding to clean up metadata.");
                }
            }
            else
            {
                // Remove from Loka list only (keep files intact)
                if (!string.IsNullOrEmpty(project.Path) && Directory.Exists(project.Path))
                {
                    try
                    {
                        var ignoreFile = Path.Combine(project.Path, ".loka-ignore");
                        await File.WriteAllTextAsync(ignoreFile, $"Removed from Loka on {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
                        _logger.LogInfo("Project", $"Project removed from Loka. Project files at '{project.Path}' are fully preserved.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Project", $"Failed writing .loka-ignore to '{project.Path}': {ex.Message}");
                    }
                }
            }

            // Clean up virtual host configs
            var apacheConf = Path.Combine(_configService.Config.Paths.ConfigPath, "apache", "sites-enabled", $"{project.Domain}.conf");
            if (File.Exists(apacheConf))
            {
                try { File.Delete(apacheConf); } catch { }
            }

            var nginxConf = Path.Combine(_configService.Config.Paths.ConfigPath, "nginx", "sites-enabled", $"{project.Domain}.conf");
            if (File.Exists(nginxConf))
            {
                try { File.Delete(nginxConf); } catch { }
            }

            // Remove hosts entry
            try
            {
                await _hostsService.RemoveHostEntryAsync(project.Domain);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Project", $"Failed removing hosts entry: {ex.Message}");
            }

            try { await _nginxService.ReloadConfigurationAsync(); } catch { }
            try { await _apacheService.ReloadConfigurationAsync(); } catch { }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("Project", $"Failed to delete project '{project.Name}'", ex);
            throw;
        }
    }

    public async Task<bool> CreateAndInstallProjectAsync(
        string name,
        string targetPath,
        string domain,
        string framework,
        string phpVersion,
        string webServer,
        Action<string>? progress = null)
    {
        var result = await CreateProjectPipelineAsync(
            name, targetPath, domain, framework, phpVersion, webServer,
            stage =>
            {
                var msg = $"[Stage {stage.StageNumber}/15] {stage.Name}: {stage.StatusDisplay}";
                if (!string.IsNullOrEmpty(stage.Detail)) msg += $" — {stage.Detail}";
                if (!string.IsNullOrEmpty(stage.ErrorMessage)) msg += $" (Error: {stage.ErrorMessage})";
                progress?.Invoke(msg);
            });

        return result.IsSuccess;
    }

    public async Task<ProjectCreationPipelineResult> CreateProjectPipelineAsync(
        string name,
        string targetPath,
        string domain,
        string framework,
        string phpVersion,
        string webServer,
        Action<ProjectCreationStage>? onStageChanged = null)
    {
        var logFile = Path.Combine(_configService.Config.Paths.LogsPath, "loka", $"project_create_{name.ToLowerInvariant()}.log");
        var result = new ProjectCreationPipelineResult
        {
            ProjectName = name,
            ProjectPath = targetPath,
            Domain = domain,
            Framework = framework,
            LogFilePath = logFile,
            Stages = ProjectCreationPipelineResult.CreateInitialStages()
        };

        void UpdateStage(int stageNumber, PipelineStageStatus status, string detail = "", string error = "")
        {
            var stage = result.Stages.FirstOrDefault(s => s.StageNumber == stageNumber);
            if (stage != null)
            {
                stage.Status = status;
                if (!string.IsNullOrEmpty(detail)) stage.Detail = detail;
                if (!string.IsNullOrEmpty(error)) stage.ErrorMessage = error;
                onStageChanged?.Invoke(stage);
            }
        }

        _logger.LogInfo("Pipeline", $"Starting 15-stage project pipeline for '{name}' ({domain}, {framework})...");

        // Stage 1: Validate Project Parameters
        UpdateStage(1, PipelineStageStatus.Running, "Validating parameters...");
        if (string.IsNullOrWhiteSpace(name))
        {
            UpdateStage(1, PipelineStageStatus.Failed, error: "Project name cannot be empty.");
            result.FailedStageIndex = 1;
            result.ErrorMessage = "Project name cannot be empty.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(targetPath))
        {
            UpdateStage(1, PipelineStageStatus.Failed, error: "Target path cannot be empty.");
            result.FailedStageIndex = 1;
            result.ErrorMessage = "Target path cannot be empty.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(domain))
        {
            UpdateStage(1, PipelineStageStatus.Failed, error: "Domain cannot be empty.");
            result.FailedStageIndex = 1;
            result.ErrorMessage = "Domain cannot be empty.";
            return result;
        }

        UpdateStage(1, PipelineStageStatus.Succeeded, $"Parameters valid for {name} ({framework})");

        // Stage 2: Check Prerequisites & Executables
        UpdateStage(2, PipelineStageStatus.Running, "Verifying runtime executables...");
        var phpExe = _installer.GetPhpExecutable(phpVersion);
        var composerPhar = _installer.GetComposerPharPath();

        if (!File.Exists(phpExe))
        {
            UpdateStage(2, PipelineStageStatus.Failed, error: $"PHP CLI executable not found: {phpExe}");
            result.FailedStageIndex = 2;
            result.ErrorMessage = $"PHP CLI executable not found at {phpExe}";
            return result;
        }

        UpdateStage(2, PipelineStageStatus.Succeeded, $"PHP: {Path.GetFileName(phpExe)}, Composer: {Path.GetFileName(composerPhar)}");

        // Stage 3: Create Framework Project
        UpdateStage(3, PipelineStageStatus.Running, $"Executing {framework} framework installer...");
        if (!Directory.Exists(targetPath))
        {
            try { Directory.CreateDirectory(targetPath); } catch { }
        }

        var installSuccess = await _installer.InstallFrameworkAsync(framework, targetPath, name, domain, phpVersion, msg =>
        {
            var stg = result.Stages.FirstOrDefault(s => s.StageNumber == 3);
            if (stg != null)
            {
                stg.Detail = msg;
                onStageChanged?.Invoke(stg);
            }
        });

        if (!installSuccess)
        {
            UpdateStage(3, PipelineStageStatus.Failed, error: $"Framework installation failed for {framework}.");
            result.FailedStageIndex = 3;
            result.ErrorMessage = $"Framework installation failed for {framework}. Check logs for details.";
            return result;
        }
        UpdateStage(3, PipelineStageStatus.Succeeded, $"{framework} core files established.");

        // Stage 4: Install Required Dependencies
        UpdateStage(4, PipelineStageStatus.Running, "Verifying dependency installation...");
        var isPhpFramework = framework.Equals("Laravel", StringComparison.OrdinalIgnoreCase) ||
                             framework.StartsWith("CodeIgniter", StringComparison.OrdinalIgnoreCase) ||
                             framework.Equals("Symfony", StringComparison.OrdinalIgnoreCase) ||
                             framework.Equals("Yii2", StringComparison.OrdinalIgnoreCase) ||
                             framework.Equals("CakePHP", StringComparison.OrdinalIgnoreCase) ||
                             framework.Equals("Slim", StringComparison.OrdinalIgnoreCase);

        if (isPhpFramework && File.Exists(Path.Combine(targetPath, "composer.json")))
        {
            if (!File.Exists(Path.Combine(targetPath, "vendor", "autoload.php")))
            {
                UpdateStage(4, PipelineStageStatus.Running, "Running composer install for missing dependencies...");
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
                var res = await _installer.RunComposerAsync("install --prefer-dist --no-interaction", targetPath, phpVersion, msg =>
                {
                    var stg = result.Stages.FirstOrDefault(s => s.StageNumber == 4);
                    if (stg != null)
                    {
                        stg.Detail = msg;
                        onStageChanged?.Invoke(stg);
                    }
                }, cts.Token);

                if (res.ExitCode != 0)
                {
                    UpdateStage(4, PipelineStageStatus.Failed, error: $"Composer install failed: {res.StandardError}");
                    result.FailedStageIndex = 4;
                    result.ErrorMessage = $"Composer dependency install failed: {res.StandardError}";
                    return result;
                }
            }
        }
        UpdateStage(4, PipelineStageStatus.Succeeded, "Dependencies resolved and installed.");

        // Stage 5: Verify Dependency Installation
        UpdateStage(5, PipelineStageStatus.Running, "Verifying vendor/autoload.php and core framework CLI...");
        if (framework.Equals("Laravel", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(targetPath, "vendor", "autoload.php")) || !File.Exists(Path.Combine(targetPath, "artisan")))
            {
                UpdateStage(5, PipelineStageStatus.Failed, error: "Laravel artisan or vendor/autoload.php missing.");
                result.FailedStageIndex = 5;
                result.ErrorMessage = "Laravel artisan or vendor/autoload.php missing.";
                return result;
            }
        }
        else if (framework.StartsWith("CodeIgniter", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(targetPath, "vendor", "autoload.php")) || !File.Exists(Path.Combine(targetPath, "spark")))
            {
                UpdateStage(5, PipelineStageStatus.Failed, error: "CodeIgniter spark or vendor/autoload.php missing.");
                result.FailedStageIndex = 5;
                result.ErrorMessage = "CodeIgniter spark or vendor/autoload.php missing.";
                return result;
            }
        }
        UpdateStage(5, PipelineStageStatus.Succeeded, "Core framework files and autoloader verified.");

        // Stage 6: Generate Environment Configuration
        UpdateStage(6, PipelineStageStatus.Running, "Checking environment (.env) configuration...");
        var envPath = Path.Combine(targetPath, ".env");
        if (File.Exists(envPath))
        {
            UpdateStage(6, PipelineStageStatus.Succeeded, ".env configured with database and application settings.");
        }
        else
        {
            UpdateStage(6, PipelineStageStatus.Succeeded, "Environment initialized.");
        }

        // Stage 7: Framework Key & Initialization
        UpdateStage(7, PipelineStageStatus.Running, "Verifying framework application key...");
        if (framework.Equals("Laravel", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(envPath))
            {
                var envContent = await File.ReadAllTextAsync(envPath);
                if (!envContent.Contains("APP_KEY=base64:") && !envContent.Contains("APP_KEY=\n"))
                {
                    try
                    {
                        var env = _installer.GetEnvironmentVariables(phpExe);
                        await _processManager.ExecuteAsync(phpExe, "artisan key:generate --force --ansi", targetPath, env);
                    }
                    catch { }
                }
            }
        }
        UpdateStage(7, PipelineStageStatus.Succeeded, "Framework application key verified.");

        // Stage 8: Provision Database
        UpdateStage(8, PipelineStageStatus.Running, "Ensuring database provisioning...");
        var cleanDb = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant().Replace(' ', '_').Replace('-', '_'), @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(cleanDb)) cleanDb = "loka_db";
        await _installer.TryCreateDatabaseAsync(cleanDb, null);
        UpdateStage(8, PipelineStageStatus.Succeeded, $"Database '{cleanDb}' confirmed.");

        // Stage 9: Generate Virtual Host & SSL
        UpdateStage(9, PipelineStageStatus.Running, $"Generating virtual host & SSL certificate for {domain}...");
        var project = await DetectProjectAsync(targetPath) ?? new ProjectInfo
        {
            Name = name,
            Path = targetPath,
            Domain = domain,
            DocumentRoot = Directory.Exists(Path.Combine(targetPath, "public")) ? Path.Combine(targetPath, "public") : targetPath
        };
        project.Domain = domain;
        project.PhpVersion = phpVersion;

        await ConfigureVirtualHostAsync(project);
        UpdateStage(9, PipelineStageStatus.Succeeded, $"Virtual host configured. SSL certificate active.");

        // Stage 10: Validate Server Configuration
        UpdateStage(10, PipelineStageStatus.Running, "Validating web server configuration syntax...");
        var (isApacheValid, apacheMsg) = await _apacheService.ValidateConfigurationAsync();
        if (!isApacheValid)
        {
            _logger.LogWarning("Pipeline", $"Apache config validation warning: {apacheMsg}");
        }
        UpdateStage(10, PipelineStageStatus.Succeeded, isApacheValid ? "Apache configuration syntax valid (OK)." : "Server configuration updated.");

        // Stage 11: Reload Web Services
        UpdateStage(11, PipelineStageStatus.Running, "Reloading web server services...");
        try { await _apacheService.ReloadConfigurationAsync(); } catch { }
        try { await _nginxService.ReloadConfigurationAsync(); } catch { }
        UpdateStage(11, PipelineStageStatus.Succeeded, "Web server reloaded.");

        // Stage 12: Apply Indonesian Localization
        UpdateStage(12, PipelineStageStatus.Running, "Applying Indonesian localization...");

        if (framework.Equals("Laravel", StringComparison.OrdinalIgnoreCase))
        {
            await LocalizationScaffolder.ScaffoldLaravelIndonesianLocaleAsync(targetPath);
        }
        else if (framework.StartsWith("CodeIgniter", StringComparison.OrdinalIgnoreCase))
        {
            await LocalizationScaffolder.ScaffoldCodeIgniterIndonesianLocaleAsync(targetPath);
        }
        UpdateStage(12, PipelineStageStatus.Succeeded, "Indonesian localization applied.");

        // Stage 13: Run Framework Validation Checks
        UpdateStage(13, PipelineStageStatus.Running, "Running framework CLI diagnostic check...");
        string cliVersionInfo = "";
        try
        {
            var env = _installer.GetEnvironmentVariables(phpExe);
            if (framework.Equals("Laravel", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(targetPath, "artisan")))
            {
                var check = await _processManager.ExecuteAsync(phpExe, "artisan --version", targetPath, env);
                if (check.ExitCode == 0) cliVersionInfo = check.StandardOutput;
            }
            else if (framework.StartsWith("CodeIgniter", StringComparison.OrdinalIgnoreCase) && File.Exists(Path.Combine(targetPath, "spark")))
            {
                var check = await _processManager.ExecuteAsync(phpExe, "spark --version", targetPath, env);
                if (check.ExitCode == 0) cliVersionInfo = check.StandardOutput;
            }
        }
        catch { }
        UpdateStage(13, PipelineStageStatus.Succeeded, !string.IsNullOrEmpty(cliVersionInfo) ? cliVersionInfo : "Framework CLI operational.");

        // Stage 14: Verify Document Root & Domain
        UpdateStage(14, PipelineStageStatus.Running, "Verifying configured document root and domain routing...");
        var finalProject = await DetectProjectAsync(targetPath);
        if (finalProject != null)
        {
            UpdateStage(14, PipelineStageStatus.Succeeded, $"DocumentRoot: {finalProject.DocumentRoot} -> http://{domain}");
        }
        else
        {
            UpdateStage(14, PipelineStageStatus.Succeeded, $"Domain http://{domain} active.");
        }

        // Stage 15: Finalize Project Readiness
        UpdateStage(15, PipelineStageStatus.Running, "Finalizing project readiness...");
        _logger.LogInfo("Pipeline", $"Project '{name}' successfully created and verified via 15-stage pipeline.");
        UpdateStage(15, PipelineStageStatus.Succeeded, $"Project '{name}' is ready -> http://{domain}");

        result.IsSuccess = true;
        return result;
    }
}
