using System.IO;

namespace Loka.Installer.Services;

public static class WindowsShortcutService
{
    public static bool CreateShortcut(string shortcutPath, string targetPath, string workingDirectory, string? iconPath = null, string? description = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(shortcutPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null)
            {
                InstallerLogger.Warning("WScript.Shell COM type not available on system.");
                return false;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetPath;
            shortcut.WorkingDirectory = workingDirectory;
            if (!string.IsNullOrEmpty(description)) shortcut.Description = description;
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath)) shortcut.IconLocation = iconPath;
            shortcut.Save();

            InstallerLogger.Info($"Shortcut created successfully: '{shortcutPath}' -> '{targetPath}'");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error($"Failed creating shortcut at '{shortcutPath}'", ex);
            return false;
        }
    }
}
