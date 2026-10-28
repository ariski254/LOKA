using Loka.Core.Exceptions;
using Loka.Core.Interfaces;

namespace Loka.Infrastructure.FileSystem;

public static class FileBackupHelper
{
    public static string CreateBackup(string filePath, ILogService? logger = null)
    {
        if (!File.Exists(filePath))
        {
            return string.Empty;
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = $"{filePath}.loka_bak_{timestamp}";
            File.Copy(filePath, backupPath, overwrite: true);
            logger?.LogInfo("Backup", $"Created backup of '{filePath}' at '{backupPath}'");
            return backupPath;
        }
        catch (Exception ex)
        {
            logger?.LogError("Backup", $"Failed to create backup of '{filePath}'", ex);
            throw new LokaException($"Failed to create backup of '{filePath}': {ex.Message}", ex);
        }
    }

    public static void RestoreBackup(string originalPath, string backupPath, ILogService? logger = null)
    {
        if (!File.Exists(backupPath))
        {
            throw new LokaException($"Backup file '{backupPath}' not found for rollback.");
        }

        try
        {
            File.Copy(backupPath, originalPath, overwrite: true);
            logger?.LogWarning("Backup", $"Restored '{originalPath}' from backup '{backupPath}'");
        }
        catch (Exception ex)
        {
            logger?.LogError("Backup", $"Failed to restore '{originalPath}' from '{backupPath}'", ex);
            throw new LokaException($"Rollback failed for '{originalPath}': {ex.Message}", ex);
        }
    }

    public static async Task SafeModifyAsync(string filePath, Func<string, Task<string>> modifyFunc, ILogService? logger = null)
    {
        if (!File.Exists(filePath))
        {
            return;
        }

        var backupPath = CreateBackup(filePath, logger);
        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            var newContent = await modifyFunc(content);
            await File.WriteAllTextAsync(filePath, newContent);
            logger?.LogInfo("FileModify", $"Successfully updated '{filePath}'");
        }
        catch (Exception ex)
        {
            logger?.LogError("FileModify", $"Error modifying '{filePath}'. Initiating rollback...", ex);
            if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath))
            {
                RestoreBackup(filePath, backupPath, logger);
            }
            throw;
        }
    }
}
