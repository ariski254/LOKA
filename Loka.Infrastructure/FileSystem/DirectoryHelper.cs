using System.Diagnostics;
using System.IO;
using Loka.Core.Interfaces;

namespace Loka.Infrastructure.FileSystem;

public static class DirectoryHelper
{
    /// <summary>
    /// Completely deletes the specified directory including all files, subdirectories, and the directory itself.
    /// Strips ReadOnly/Hidden attributes and uses cmd rd /s /q as a resilient fallback on Windows.
    /// </summary>
    public static bool ForceDeleteDirectory(string dirPath, ILogService? logger = null)
    {
        if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            return true;

        logger?.LogInfo("FileSystem", $"Force deleting directory and folder: '{dirPath}'");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (!Directory.Exists(dirPath)) return true;

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
                    logger?.LogInfo("FileSystem", $"Successfully removed directory and folder: '{dirPath}'");
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning("FileSystem", $"Attempt {attempt} standard delete failed for '{dirPath}': {ex.Message}");
                Thread.Sleep(150);
            }
        }

        // Fallback for Windows: cmd.exe /c rd /s /q
        if (Directory.Exists(dirPath) && OperatingSystem.IsWindows())
        {
            try
            {
                using var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c rd /s /q \"{dirPath}\"",
                    CreateNoWindow = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
                proc?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                logger?.LogWarning("FileSystem", $"cmd rd /s /q failed for '{dirPath}': {ex.Message}");
            }
        }

        bool deleted = !Directory.Exists(dirPath);
        if (deleted)
        {
            logger?.LogInfo("FileSystem", $"Directory successfully deleted via cmd fallback: '{dirPath}'");
        }
        else
        {
            logger?.LogWarning("FileSystem", $"Directory could not be deleted immediately: '{dirPath}'");
        }

        return deleted;
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
}
