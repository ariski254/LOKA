using System.Diagnostics;
using System.IO;

namespace Loka.Installer.Services;

public class UninstallerEngine
{
    public string InstallPath { get; set; } = @"C:\Program Files\Loka";
    public string EnvironmentPath { get; set; } = @"C:\Loka";
    public bool RemoveEnvironmentData { get; set; } = true;

    public async Task<bool> UninstallAsync(Action<int, string> onProgress)
    {
        InstallerLogger.Info("========== Starting LOKA Uninstallation ==========");
        InstallerLogger.Info($"Install Directory: {InstallPath}");
        InstallerLogger.Info($"Environment Directory: {EnvironmentPath}");
        InstallerLogger.Info($"Remove Environment Data: {RemoveEnvironmentData}");

        try
        {
            // 1. Terminate all Loka processes and stack services
            onProgress(10, "Stopping all LOKA application and service processes...");
            TerminateAllLokaProcesses();
            await Task.Delay(300);

            // 2. Remove Windows Registry Keys
            onProgress(25, "Removing Windows Registry entries...");
            WindowsRegistryService.UnregisterApplication();
            await Task.Delay(200);

            // 3. Remove Start Menu and Desktop shortcuts
            onProgress(40, "Removing Start Menu and Desktop shortcuts...");
            RemoveShortcuts();
            await Task.Delay(200);

            // 4. Remove User AppData
            onProgress(55, "Cleaning local log data and temporary files...");
            RemoveAppData();
            await Task.Delay(200);

            // 5. Completely delete Environment Directory (including the C:\Loka folder itself)
            if (RemoveEnvironmentData && !string.IsNullOrWhiteSpace(EnvironmentPath))
            {
                onProgress(70, $"Removing environment directory '{EnvironmentPath}'...");
                ForceDeleteDirectory(EnvironmentPath);
                await Task.Delay(200);
            }
            else
            {
                InstallerLogger.Info($"Environment directory '{EnvironmentPath}' preserved safely.");
            }

            // 6. Completely delete Application Directory (including the C:\Program Files\Loka folder itself)
            if (!string.IsNullOrWhiteSpace(InstallPath))
            {
                onProgress(85, $"Removing installation directory '{InstallPath}'...");
                ForceDeleteDirectory(InstallPath);
                await Task.Delay(200);
            }

            // 7. Clean up User Environment PATH
            RemoveUserEnvironmentPath();

            // 8. Schedule post-exit cleanup
            SchedulePostExitCleanup();

            onProgress(100, "LOKA uninstallation completed successfully.");
            InstallerLogger.Info("========== LOKA Uninstallation Completed Successfully ==========");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Uninstallation failed", ex);
            throw;
        }
    }

    private void TerminateAllLokaProcesses()
    {
        InstallerLogger.Info("Terminating all LOKA application and service processes...");

        string[] processNames = new[]
        {
            "Loka", "Loka.App", "Loka-Setup",
            "nginx", "httpd", "mariadbd", "mysqld",
            "postgres", "initdb", "pg_ctl",
            "php-cgi", "php",
            "redis-server", "mailpit"
        };

        foreach (var name in processNames)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    try
                    {
                        if (proc.Id != Environment.ProcessId)
                        {
                            proc.Kill();
                            proc.WaitForExit(1000);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (proc.Id == Environment.ProcessId) continue;

                    string? exePath = null;
                    try { exePath = proc.MainModule?.FileName; } catch { }

                    if (!string.IsNullOrEmpty(exePath))
                    {
                        bool matchesInstall = !string.IsNullOrEmpty(InstallPath) &&
                            exePath.StartsWith(InstallPath, StringComparison.OrdinalIgnoreCase);
                        bool matchesEnv = !string.IsNullOrEmpty(EnvironmentPath) &&
                            exePath.StartsWith(EnvironmentPath, StringComparison.OrdinalIgnoreCase);

                        if (matchesInstall || matchesEnv)
                        {
                            InstallerLogger.Info($"Killing locking process '{proc.ProcessName}' ({proc.Id}) at '{exePath}'...");
                            proc.Kill();
                            proc.WaitForExit(1000);
                        }
                    }
                }
                catch { }
            }
        }
        catch { }

        Thread.Sleep(300);
    }

    private void RemoveShortcuts()
    {
        TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "Loka"));
        TryDeleteDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Loka"));
        TryDeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Loka.lnk"));
        TryDeleteFile(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "Loka.lnk"));
    }

    private void RemoveAppData()
    {
        try
        {
            var localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loka");
            ForceDeleteDirectory(localAppData);

            var roamingAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Loka");
            ForceDeleteDirectory(roamingAppData);
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Notice during AppData removal: {ex.Message}");
        }
    }

    public static void ForceDeleteDirectory(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            return;

        InstallerLogger.Info($"Force deleting directory and all contents: '{dirPath}'...");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (!Directory.Exists(dirPath)) break;

                var rootDir = new DirectoryInfo(dirPath);
                ClearAttributesRecursive(rootDir);

                foreach (var file in Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                        File.Delete(file);
                    }
                    catch { }
                }

                foreach (var sub in Directory.GetDirectories(dirPath, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                {
                    try
                    {
                        File.SetAttributes(sub, FileAttributes.Normal);
                        Directory.Delete(sub, recursive: true);
                    }
                    catch { }
                }

                File.SetAttributes(dirPath, FileAttributes.Normal);
                Directory.Delete(dirPath, recursive: true);

                if (!Directory.Exists(dirPath))
                {
                    InstallerLogger.Info($"Successfully deleted directory and folder: '{dirPath}'");
                    return;
                }
            }
            catch (Exception ex)
            {
                InstallerLogger.Warning($"Attempt {attempt} standard delete failed for '{dirPath}': {ex.Message}");
                Thread.Sleep(200);
            }
        }

        if (Directory.Exists(dirPath))
        {
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rd /s /q \"{dirPath}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                proc?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                InstallerLogger.Warning($"cmd rd /s /q failed for '{dirPath}': {ex.Message}");
            }
        }

        if (!Directory.Exists(dirPath))
        {
            InstallerLogger.Info($"Directory successfully removed via cmd fallback: '{dirPath}'");
        }
        else
        {
            InstallerLogger.Warning($"Directory could not be completely removed immediately: '{dirPath}'");
        }
    }

    private static void ClearAttributesRecursive(DirectoryInfo directory)
    {
        try
        {
            directory.Attributes = FileAttributes.Normal;
            foreach (var sub in directory.GetDirectories())
            {
                ClearAttributesRecursive(sub);
            }
            foreach (var file in directory.GetFiles())
            {
                try { file.Attributes = FileAttributes.Normal; } catch { }
            }
        }
        catch { }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                ForceDeleteDirectory(path);
                InstallerLogger.Info($"Deleted directory: '{path}'");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Failed removing directory '{path}': {ex.Message}");
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
                InstallerLogger.Info($"Deleted file: '{path}'");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Failed removing file '{path}': {ex.Message}");
        }
    }

    private void SchedulePostExitCleanup()
    {
        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "Loka_Uninstaller");
            var cmdArgs = $"/c ping 127.0.0.1 -n 3 > NUL";

            if (!string.IsNullOrWhiteSpace(InstallPath))
                cmdArgs += $" & if exist \"{InstallPath}\" rd /s /q \"{InstallPath}\"";

            if (RemoveEnvironmentData && !string.IsNullOrWhiteSpace(EnvironmentPath))
                cmdArgs += $" & if exist \"{EnvironmentPath}\" rd /s /q \"{EnvironmentPath}\"";

            cmdArgs += $" & if exist \"{tempDir}\" rd /s /q \"{tempDir}\"";

            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdArgs,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false
            });

            InstallerLogger.Info("Scheduled background post-exit directory cleanup.");
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Could not schedule post-exit cleanup: {ex.Message}");
        }
    }

    private void RemoveUserEnvironmentPath()
    {
        try
        {
            var current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
            var segments = current.Split(';', StringSplitOptions.RemoveEmptyEntries).ToList();
            int removed = segments.RemoveAll(p =>
                (!string.IsNullOrWhiteSpace(InstallPath) && p.StartsWith(InstallPath, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(EnvironmentPath) && p.StartsWith(EnvironmentPath, StringComparison.OrdinalIgnoreCase)));

            if (removed > 0)
            {
                Environment.SetEnvironmentVariable("PATH", string.Join(";", segments), EnvironmentVariableTarget.User);
                InstallerLogger.Info("Removed Loka and stack paths from user environment PATH.");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Could not clean user environment PATH: {ex.Message}");
        }
    }
}
