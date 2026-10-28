using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using Loka.Installer.Models;

namespace Loka.Installer.Services;

public class InstallerEngine
{
    public string InstallPath { get; set; } = @"C:\Program Files\Loka";
    public string EnvironmentPath { get; set; } = @"C:\Loka";
    public string InstallMode { get; set; } = "Recommended"; // "Recommended", "Custom", "Repair", "Update"
    public List<string> SelectedComponents { get; set; } = new();
    public bool CreateStartMenuShortcut { get; set; } = true;
    public bool CreateDesktopShortcut { get; set; } = true;
    public bool LaunchAfterInstall { get; set; } = true;

    /// <summary>
    /// Optional handler to prompt user when a running LOKA instance is detected and refuses to close gracefully.
    /// </summary>
    public Func<ProcessLockInfo, Task<ProcessLockAction>>? ProcessLockPromptHandler { get; set; }

    private readonly List<string> _createdFiles = new();
    private readonly List<string> _newlyCreatedFiles = new();
    private readonly Dictionary<string, string> _backedUpFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<string> _createdDirectories = new();
    private string? _activeStagingDir;
    private string? _activeBackupDir;

    public async Task<bool> InstallAsync(
        IList<InstallationStep> steps,
        Action<int, string> onProgress,
        CancellationToken cancellationToken = default)
    {
        InstallerLogger.Info("========== Starting LOKA Windows Installation ==========");
        InstallerLogger.Info($"Install Directory: {InstallPath}");
        InstallerLogger.Info($"Environment Directory: {EnvironmentPath}");

        try
        {
            // Step 1: Checking system & compatibility
            SetStep(steps, 0, StepStatus.InProgress);
            onProgress(5, "Checking Windows system compatibility...");
            await Task.Delay(200, cancellationToken);

            if (!CheckSystemCompatibility(InstallPath, out var compError))
            {
                SetStep(steps, 0, StepStatus.Failed);
                throw new InvalidOperationException($"Windows compatibility check failed: {compError}");
            }
            SetStep(steps, 0, StepStatus.Completed);

            // Step 2: Preparing directories & closing any running LOKA instance
            SetStep(steps, 1, StepStatus.InProgress);
            onProgress(10, "Checking for active LOKA processes and preparing directories...");
            await Task.Delay(200, cancellationToken);

            await EnsureApplicationClosedAsync(onProgress, cancellationToken);

            CreateDirectoryTracked(InstallPath);
            SetStep(steps, 1, StepStatus.Completed);

            // Step 3: Installing application files with staging and rollback protection
            SetStep(steps, 2, StepStatus.InProgress);
            onProgress(30, "Installing LOKA core application files...");
            await ExtractApplicationPayloadAsync(onProgress, cancellationToken);
            SetStep(steps, 2, StepStatus.Completed);

            // Step 4: Setting up Loka environment structure & stack components
            SetStep(steps, 3, StepStatus.InProgress);
            onProgress(50, "Installing development stack components (Nginx, Apache, PHP 8.4, PHP 8.5, MariaDB, MySQL, PostgreSQL, Node.js, Git, Composer, phpMyAdmin)...");
            await SetupEnvironmentAndStackAsync(onProgress, cancellationToken);
            SetStep(steps, 3, StepStatus.Completed);

            // Step 5: Registering Windows application & Uninstaller
            SetStep(steps, 4, StepStatus.InProgress);
            onProgress(85, "Registering LOKA in Windows Installed Apps...");
            await SetupUninstallerAndRegistryAsync(cancellationToken);
            SetStep(steps, 4, StepStatus.Completed);

            // Step 6: Creating shortcuts
            SetStep(steps, 5, StepStatus.InProgress);
            onProgress(92, "Creating Start Menu and Desktop shortcuts...");
            CreateShortcuts();
            SetStep(steps, 5, StepStatus.Completed);

            // Step 7: Finalizing
            SetStep(steps, 6, StepStatus.InProgress);
            onProgress(100, "Finalizing LOKA installation...");
            await Task.Delay(300, cancellationToken);
            SetStep(steps, 6, StepStatus.Completed);

            InstallerLogger.Info("========== LOKA Installation Completed Successfully ==========");
            return true;
        }
        catch (OperationCanceledException)
        {
            InstallerLogger.Warning("Installation cancelled by user.");
            await RollbackAsync();
            throw;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Installation failed. Initiating rollback...", ex);
            await RollbackAsync();
            throw;
        }
        finally
        {
            CleanupStagingAndBackup();
        }
    }

    public async Task EnsureApplicationClosedAsync(Action<int, string> onProgress, CancellationToken cancellationToken)
    {
        var targetExe = Path.Combine(InstallPath, "Loka.exe");
        if (!File.Exists(targetExe))
        {
            targetExe = Path.Combine(InstallPath, "Loka.App.exe");
        }

        var lockingProcs = GetLockingProcesses(InstallPath, EnvironmentPath);

        if (lockingProcs.Count == 0 && (!File.Exists(targetExe) || !IsFileLocked(targetExe)))
        {
            return;
        }

        InstallerLogger.Info($"Detected {lockingProcs.Count} running process(es) that may lock installation files.");

        foreach (var info in lockingProcs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Process? proc = null;
            try
            {
                proc = Process.GetProcessById(info.ProcessId);
            }
            catch
            {
                continue; // Process already terminated
            }

            if (proc == null || proc.HasExited) continue;

            InstallerLogger.Info($"Requesting graceful shutdown for process '{info.ProcessName}' (PID: {info.ProcessId}, Path: '{info.ExecutablePath}')...");
            onProgress(12, $"Closing {info.ProcessName} (PID {info.ProcessId})...");

            // 1. Try graceful shutdown via WM_CLOSE
            try
            {
                proc.CloseMainWindow();
            }
            catch { }

            // Wait up to 3000ms for graceful exit
            int waitMs = 0;
            while (!proc.HasExited && waitMs < 3000)
            {
                await Task.Delay(200, cancellationToken);
                waitMs += 200;
            }

            // 2. If still running, prompt user or terminate
            if (!proc.HasExited)
            {
                if (ProcessLockPromptHandler != null)
                {
                    var action = await ProcessLockPromptHandler(info);
                    if (action == ProcessLockAction.CloseAutomatically)
                    {
                        InstallerLogger.Info($"Closing '{info.ProcessName}' (PID {info.ProcessId}) automatically...");
                        try
                        {
                            proc.Kill();
                            proc.WaitForExit(3000);
                        }
                        catch { }
                    }
                    else if (action == ProcessLockAction.Retry)
                    {
                        InstallerLogger.Info($"Retrying wait for '{info.ProcessName}' (PID {info.ProcessId})...");
                        await Task.Delay(1000, cancellationToken);
                    }
                    else
                    {
                        InstallerLogger.Warning($"User cancelled installation due to active process '{info.ProcessName}' (PID {info.ProcessId}).");
                        throw new OperationCanceledException($"Installation was cancelled by user because {info.ProcessName} is currently running.");
                    }
                }
                else
                {
                    // Silent / non-interactive: terminate process
                    InstallerLogger.Info($"Non-interactive mode: Terminating process '{info.ProcessName}' (PID {info.ProcessId})...");
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch { }
                }
            }
        }

        // 3. Verify file lock release with limited retries (avoid indefinite loops)
        if (File.Exists(targetExe))
        {
            int retries = 0;
            const int maxRetries = 15;
            while (IsFileLocked(targetExe) && retries < maxRetries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                retries++;
                InstallerLogger.Info($"Waiting for file lock on '{targetExe}' to be released (attempt {retries}/{maxRetries})...");
                onProgress(14, $"Waiting for {Path.GetFileName(targetExe)} to be unlocked ({retries}/{maxRetries})...");
                await Task.Delay(300, cancellationToken);
            }

            if (IsFileLocked(targetExe))
            {
                var lockEx = new IOException($"The process cannot access the file '{targetExe}' because it is being used by another process. Please close any running LOKA instance and try again.");
                InstallerLogger.Error($"File '{targetExe}' remains locked after {maxRetries} retry attempts.", lockEx);
                throw lockEx;
            }

            InstallerLogger.Info($"File lock on '{targetExe}' released successfully.");
        }
    }

    public static List<ProcessLockInfo> GetLockingProcesses(string installPath, string environmentPath)
    {
        var result = new List<ProcessLockInfo>();
        var currentPid = Environment.ProcessId;
        var inspectedPids = new HashSet<int>();

        // 1. Process name matches (Loka, Loka.App)
        string[] targetNames = new[] { "Loka", "Loka.App" };
        foreach (var name in targetNames)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    if (proc.Id == currentPid || !inspectedPids.Add(proc.Id)) continue;

                    string? exePath = TryGetProcessPath(proc);
                    result.Add(new ProcessLockInfo
                    {
                        ProcessName = proc.ProcessName,
                        ProcessId = proc.Id,
                        ExecutablePath = exePath ?? "Loka Application",
                        Reason = "LOKA application executable or UI process is running"
                    });
                }
            }
            catch { }
        }

        // 2. Scan all processes for path matches within installPath or environmentPath
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                if (proc.Id == currentPid || inspectedPids.Contains(proc.Id)) continue;

                string? exePath = TryGetProcessPath(proc);
                if (string.IsNullOrEmpty(exePath)) continue;

                bool isUnderInstall = !string.IsNullOrWhiteSpace(installPath) &&
                    exePath.StartsWith(installPath, StringComparison.OrdinalIgnoreCase);

                bool isUnderEnv = !string.IsNullOrWhiteSpace(environmentPath) &&
                    exePath.StartsWith(environmentPath, StringComparison.OrdinalIgnoreCase);

                if (isUnderInstall || isUnderEnv)
                {
                    inspectedPids.Add(proc.Id);
                    result.Add(new ProcessLockInfo
                    {
                        ProcessName = proc.ProcessName,
                        ProcessId = proc.Id,
                        ExecutablePath = exePath,
                        Reason = isUnderInstall 
                            ? "Process running from Loka installation directory" 
                            : "Background service running from Loka environment directory"
                    });
                }
            }
        }
        catch { }

        return result;
    }

    public static bool IsFileLocked(string filePath)
    {
        if (!File.Exists(filePath)) return false;
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? TryGetProcessPath(Process proc)
    {
        try
        {
            return proc.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static bool CheckSystemCompatibility(string installPath, out string error)
    {
        if (!OperatingSystem.IsWindows())
        {
            error = "LOKA only supports the Windows operating system.";
            return false;
        }

        if (Environment.Is64BitOperatingSystem == false)
        {
            error = "LOKA requires a 64-bit Windows operating system (x64).";
            return false;
        }

        try
        {
            var targetPath = Path.GetPathRoot(Path.GetFullPath(installPath)) ?? @"C:\";
            var drive = new DriveInfo(targetPath);
            double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            InstallerLogger.Info($"Drive '{drive.Name}' free disk space: {freeGb:F2} GB");

            if (drive.AvailableFreeSpace < 500 * 1024 * 1024) // 500 MB minimum
            {
                error = $"Available disk space on drive {drive.Name} is only {freeGb:F2} GB (minimum 500 MB required).";
                return false;
            }
        }
        catch { }

        error = string.Empty;
        return true;
    }

    public static string GetDiskSpaceSummary(string path)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? @"C:\";
            var drive = new DriveInfo(root);
            double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
            return $"{freeGb:F1} GB free on {drive.Name}";
        }
        catch
        {
            return "Space verified";
        }
    }

    private void CreateDirectoryTracked(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
            _createdDirectories.Add(path);
            InstallerLogger.Info($"Created directory: {path}");
        }
    }

    private async Task ExtractApplicationPayloadAsync(Action<int, string> onProgress, CancellationToken cancellationToken)
    {
        await Task.Yield();
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("app_payload.zip", StringComparison.OrdinalIgnoreCase));

        _activeStagingDir = Path.Combine(Path.GetTempPath(), "Loka_Install_Staging_" + Guid.NewGuid().ToString("N"));
        _activeBackupDir = Path.Combine(Path.GetTempPath(), "Loka_Install_Backup_" + Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(_activeStagingDir);
            Directory.CreateDirectory(_activeBackupDir);

            // Step 1: Extract payload into temporary staging directory
            if (resourceName != null)
            {
                InstallerLogger.Info($"Extracting embedded payload '{resourceName}' to staging directory '{_activeStagingDir}'...");
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    archive.ExtractToDirectory(_activeStagingDir, overwriteFiles: true);
                }
            }
            else
            {
                // Development fallback: Copy files from local build/dist folder if payload is not embedded
                var localSourcePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "bin", "Debug", "net8.0-windows"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Loka.App", "bin", "Release", "net8.0-windows")
                };

                string? validSource = localSourcePaths.FirstOrDefault(Directory.Exists);
                if (validSource != null)
                {
                    InstallerLogger.Info($"Copying dev files from '{validSource}' to staging '{_activeStagingDir}'...");
                    foreach (var file in Directory.GetFiles(validSource, "*.*", SearchOption.AllDirectories))
                    {
                        var rel = Path.GetRelativePath(validSource, file);
                        var target = Path.Combine(_activeStagingDir, rel);
                        var dir = Path.GetDirectoryName(target);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        File.Copy(file, target, overwrite: true);
                    }
                }
                else
                {
                    throw new InvalidOperationException("LOKA application installation files (payload) not found.");
                }
            }

            // Verify staging contents
            var stagedExe = Path.Combine(_activeStagingDir, "Loka.exe");
            if (!File.Exists(stagedExe))
            {
                stagedExe = Path.Combine(_activeStagingDir, "Loka.App.exe");
            }
            if (!File.Exists(stagedExe) || new FileInfo(stagedExe).Length == 0)
            {
                throw new InvalidOperationException("Staging verification failed: Application executable is missing or zero bytes in payload.");
            }

            InstallerLogger.Info($"Staging verified: Payload unpacked successfully ({new DirectoryInfo(_activeStagingDir).EnumerateFiles("*", SearchOption.AllDirectories).Count()} files).");

            // Step 2: Safe atomic replacement into InstallPath with rollback backup
            var stagedFiles = Directory.GetFiles(_activeStagingDir, "*.*", SearchOption.AllDirectories);
            int total = stagedFiles.Length;
            int count = 0;

            foreach (var file in stagedFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relPath = Path.GetRelativePath(_activeStagingDir, file);
                var targetFile = Path.Combine(InstallPath, relPath);
                var targetDir = Path.GetDirectoryName(targetFile);
                if (!string.IsNullOrEmpty(targetDir)) CreateDirectoryTracked(targetDir);

                // Backup existing file if present
                if (File.Exists(targetFile))
                {
                    var backupFile = Path.Combine(_activeBackupDir, relPath);
                    var backupFileDir = Path.GetDirectoryName(backupFile);
                    if (!string.IsNullOrEmpty(backupFileDir)) Directory.CreateDirectory(backupFileDir);
                    File.Copy(targetFile, backupFile, overwrite: true);
                    _backedUpFiles[targetFile] = backupFile;
                }
                else
                {
                    _newlyCreatedFiles.Add(targetFile);
                }

                // Copy with limited retries to handle transient file lock
                bool copied = false;
                Exception? lastCopyEx = null;
                for (int attempt = 1; attempt <= 10; attempt++)
                {
                    try
                    {
                        File.Copy(file, targetFile, overwrite: true);
                        copied = true;
                        _createdFiles.Add(targetFile);
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        lastCopyEx = ioEx;
                        InstallerLogger.Warning($"File '{targetFile}' locked during copy (attempt {attempt}/10): {ioEx.Message}");
                        await Task.Delay(300, cancellationToken);
                    }
                }

                if (!copied)
                {
                    throw new IOException($"Failed to replace '{targetFile}' because it is in use by another process. Details: {lastCopyEx?.Message}", lastCopyEx);
                }

                count++;
                int pct = 30 + (int)((float)count / total * 35);
                onProgress(pct, $"Installing: {Path.GetFileName(file)}");
            }

            // Step 3: Verify installed executable
            var installedExe = Path.Combine(InstallPath, "Loka.exe");
            if (!File.Exists(installedExe))
            {
                installedExe = Path.Combine(InstallPath, "Loka.App.exe");
            }
            if (!File.Exists(installedExe) || new FileInfo(installedExe).Length == 0)
            {
                throw new InvalidOperationException("Verification failed: Installed executable 'Loka.exe' is missing or zero bytes.");
            }

            InstallerLogger.Info($"Verified installed executable '{installedExe}' ({new FileInfo(installedExe).Length} bytes).");

            // Clean up staging after successful replacement (backup remains for possible rollback)
            try
            {
                if (!string.IsNullOrEmpty(_activeStagingDir) && Directory.Exists(_activeStagingDir))
                {
                    Directory.Delete(_activeStagingDir, recursive: true);
                    _activeStagingDir = null;
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("File replacement failed during payload installation. Restoring previous version from backup...", ex);

            // Restore from backupDir immediately
            foreach (var kvp in _backedUpFiles)
            {
                var targetFile = kvp.Key;
                var backupFile = kvp.Value;
                try
                {
                    if (File.Exists(backupFile))
                    {
                        File.Copy(backupFile, targetFile, overwrite: true);
                        InstallerLogger.Info($"Restored previous file: '{targetFile}'");
                    }
                }
                catch (Exception restoreEx)
                {
                    InstallerLogger.Warning($"Could not restore '{targetFile}': {restoreEx.Message}");
                }
            }

            throw;
        }
    }

    private void CleanupStagingAndBackup()
    {
        try
        {
            if (!string.IsNullOrEmpty(_activeStagingDir) && Directory.Exists(_activeStagingDir))
            {
                Directory.Delete(_activeStagingDir, recursive: true);
            }
        }
        catch { }

        try
        {
            if (!string.IsNullOrEmpty(_activeBackupDir) && Directory.Exists(_activeBackupDir))
            {
                Directory.Delete(_activeBackupDir, recursive: true);
            }
        }
        catch { }

        _activeStagingDir = null;
        _activeBackupDir = null;
    }

    private async Task SetupEnvironmentAndStackAsync(Action<int, string> onProgress, CancellationToken cancellationToken)
    {
        CreateDirectoryTracked(EnvironmentPath);

        // 1. Provision stack binaries (PHP 8.4, 8.5, Nginx, Apache, MariaDB, MySQL, Postgres, Node, Git, Composer, phpMyAdmin)
        var stackService = new StackProvisioningService();
        bool isRepair = string.Equals(InstallMode, "Repair", StringComparison.OrdinalIgnoreCase);
        bool isUpdate = string.Equals(InstallMode, "Update", StringComparison.OrdinalIgnoreCase);
        await stackService.ProvisionAsync(
            EnvironmentPath,
            onProgress,
            SelectedComponents.Count > 0 ? SelectedComponents : null,
            isRepair,
            isUpdate,
            cancellationToken);

        // 2. Initialize default configuration
        await SetupDefaultConfigAsync(cancellationToken);

        // 3. Register user environment PATH
        ConfigureUserEnvironmentPath();
    }

    private void ConfigureUserEnvironmentPath()
    {
        try
        {
            var pathsToAdd = new List<string>
            {
                InstallPath,
                Path.Combine(EnvironmentPath, "bin"),
                Path.Combine(EnvironmentPath, "bin", "php"),
                Path.Combine(EnvironmentPath, "bin", "composer"),
                Path.Combine(EnvironmentPath, "bin", "nodejs"),
                Path.Combine(EnvironmentPath, "bin", "git"),
                Path.Combine(EnvironmentPath, "bin", "mariadb", "bin"),
                Path.Combine(EnvironmentPath, "bin", "mysql", "bin"),
                Path.Combine(EnvironmentPath, "bin", "postgresql", "bin")
            };

            var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var segments = current.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            bool modified = false;

            foreach (var p in pathsToAdd)
            {
                if (!segments.Any(s => s.Equals(p, StringComparison.OrdinalIgnoreCase)))
                {
                    segments.Add(p);
                    modified = true;
                }
            }

            if (modified)
            {
                Environment.SetEnvironmentVariable("PATH", string.Join(";", segments), EnvironmentVariableTarget.User);
                InstallerLogger.Info("Configured user environment PATH with Loka and stack runtimes.");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Could not configure user environment PATH: {ex.Message}");
        }
    }

    private async Task SetupDefaultConfigAsync(CancellationToken cancellationToken)
    {
        var configFile = Path.Combine(EnvironmentPath, "config", "loka.json");
        if (!File.Exists(configFile))
        {
            var initialJson = @"{
  ""Paths"": {
    ""RootPath"": """ + EnvironmentPath.Replace("\\", "\\\\") + @""",
    ""BinPath"": """ + Path.Combine(EnvironmentPath, "bin").Replace("\\", "\\\\") + @""",
    ""RuntimesPath"": """ + Path.Combine(EnvironmentPath, "bin").Replace("\\", "\\\\") + @""",
    ""EtcPath"": """ + Path.Combine(EnvironmentPath, "etc").Replace("\\", "\\\\") + @""",
    ""DataPath"": """ + Path.Combine(EnvironmentPath, "data").Replace("\\", "\\\\") + @""",
    ""ProjectsPath"": """ + Path.Combine(EnvironmentPath, "projects").Replace("\\", "\\\\") + @""",
    ""LogsPath"": """ + Path.Combine(EnvironmentPath, "logs").Replace("\\", "\\\\") + @""",
    ""ConfigPath"": """ + Path.Combine(EnvironmentPath, "config").Replace("\\", "\\\\") + @""",
    ""BackupsPath"": """ + Path.Combine(EnvironmentPath, "backups").Replace("\\", "\\\\") + @""",
    ""TmpPath"": """ + Path.Combine(EnvironmentPath, "tmp").Replace("\\", "\\\\") + @""",
    ""TempPath"": """ + Path.Combine(EnvironmentPath, "tmp").Replace("\\", "\\\\") + @""",
    ""SslPath"": """ + Path.Combine(EnvironmentPath, "ssl").Replace("\\", "\\\\") + @""",
    ""AdditionalRuntimeSearchPaths"": [
      ""C:\\laragon\\bin"",
      ""C:\\xampp"",
      ""C:\\Program Files\\nodejs"",
      ""C:\\Program Files\\Git\\cmd""
    ],
    ""AllowExternalRuntimes"": false
  },
  ""Services"": {
    ""ApachePort"": 80,
    ""ApacheSslPort"": 443,
    ""NginxPort"": 80,
    ""MySqlPort"": 3306,
    ""MariaDbPort"": 3307,
    ""PostgresPort"": 5432,
    ""PreferredWebServer"": ""Nginx"",
    ""PreferredDatabase"": """",
    ""AutoStartApache"": false,
    ""AutoStartMySql"": false,
    ""AutoStartNginx"": false,
    ""AutoStartMariaDb"": false,
    ""AutoStartPostgres"": false
  },
  ""General"": {
    ""StartWithWindows"": false,
    ""MinimizeToTray"": true,
    ""CloseToTray"": true,
    ""Theme"": ""Light"",
    ""Language"": ""English"",
    ""IsFirstRunCompleted"": false
  },
  ""Php"": {
    ""ActiveVersion"": ""8.4"",
    ""AvailableVersions"": [ ""8.3"", ""8.4"", ""8.5"" ],
    ""EnabledExtensions"": [ ""curl"", ""fileinfo"", ""mbstring"", ""openssl"", ""pdo_mysql"", ""mysqli"", ""zip"", ""pdo_pgsql"", ""pgsql"", ""pdo_sqlite"", ""sqlite3"" ]
  },
  ""Database"": {
    ""RootUser"": ""root"",
    ""RootPassword"": """",
    ""Host"": ""127.0.0.1"",
    ""PhpMyAdminUrl"": ""http://localhost/phpmyadmin""
  }
}";
            var configDir = Path.GetDirectoryName(configFile)!;
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            await File.WriteAllTextAsync(configFile, initialJson, cancellationToken);
            _createdFiles.Add(configFile);
            InstallerLogger.Info($"Created initial configuration file at '{configFile}'.");
        }
    }

    private async Task SetupUninstallerAndRegistryAsync(CancellationToken cancellationToken)
    {
        // Deploy Uninstaller executable
        var uninstallerPath = Path.Combine(InstallPath, "Uninstall-Loka.exe");
        try
        {
            var currentExe = Environment.ProcessPath;
            if (File.Exists(currentExe))
            {
                File.Copy(currentExe, uninstallerPath, overwrite: true);
                _createdFiles.Add(uninstallerPath);
                InstallerLogger.Info($"Copied uninstaller to '{uninstallerPath}'.");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Could not copy uninstaller binary: {ex.Message}");
        }

        // Register in Windows Installed Apps
        WindowsRegistryService.RegisterInstalledApplication(InstallPath, "1.0.0", "Loka.exe", uninstallerPath);
        await Task.CompletedTask;
    }

    private void CreateShortcuts()
    {
        var mainExe = Path.Combine(InstallPath, "Loka.exe");
        if (!File.Exists(mainExe))
        {
            mainExe = Path.Combine(InstallPath, "Loka.App.exe");
        }

        var iconPath = Path.Combine(InstallPath, "Resources", "loka.ico");
        if (!File.Exists(iconPath))
        {
            iconPath = mainExe;
        }

        if (CreateStartMenuShortcut)
        {
            var startMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Loka");
            CreateDirectoryTracked(startMenuDir);

            var appShortcut = Path.Combine(startMenuDir, "Loka.lnk");
            WindowsShortcutService.CreateShortcut(appShortcut, mainExe, InstallPath, iconPath, "Loka Local Development Environment");

            var uninstallerPath = Path.Combine(InstallPath, "Uninstall-Loka.exe");
            if (File.Exists(uninstallerPath))
            {
                var uninstShortcut = Path.Combine(startMenuDir, "Uninstall Loka.lnk");
                WindowsShortcutService.CreateShortcut(uninstShortcut, uninstallerPath, InstallPath, uninstallerPath, "Uninstall Loka");
            }
        }

        if (CreateDesktopShortcut)
        {
            var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var desktopShortcut = Path.Combine(desktopDir, "Loka.lnk");
            WindowsShortcutService.CreateShortcut(desktopShortcut, mainExe, InstallPath, iconPath, "Loka Local Development Environment");
        }
    }

    public async Task RollbackAsync()
    {
        InstallerLogger.Warning("Executing installation rollback...");

        // Restore backed up files if any
        foreach (var kvp in _backedUpFiles)
        {
            var targetFile = kvp.Key;
            var backupFile = kvp.Value;
            try
            {
                if (File.Exists(backupFile))
                {
                    File.Copy(backupFile, targetFile, overwrite: true);
                    InstallerLogger.Info($"Rollback restored file: '{targetFile}'");
                }
            }
            catch { }
        }

        // Delete newly created files only (never existing files that were updated)
        foreach (var file in _newlyCreatedFiles)
        {
            try
            {
                if (File.Exists(file)) File.Delete(file);
            }
            catch { }
        }

        bool isUpdateOrRepair = string.Equals(InstallMode, "Update", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(InstallMode, "Repair", StringComparison.OrdinalIgnoreCase);

        if (!isUpdateOrRepair)
        {
            WindowsRegistryService.UnregisterApplication();
        }

        foreach (var dir in _createdDirectories.OrderByDescending(d => d.Length))
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir);
                }
            }
            catch { }
        }

        await Task.CompletedTask;
    }

    private static void SetStep(IList<InstallationStep> steps, int index, StepStatus status)
    {
        if (index >= 0 && index < steps.Count)
        {
            steps[index].Status = status;
        }
    }
}
