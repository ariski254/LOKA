using System.IO;
using Microsoft.Win32;

namespace Loka.Installer.Services;

public static class WindowsRegistryService
{
    private const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\Loka";

    public static bool RegisterInstalledApplication(string installDir, string version, string exeName, string uninstallerPath)
    {
        try
        {
            var mainExe = Path.Combine(installDir, exeName);
            long estimatedSizeKb = 0;
            try
            {
                var dirInfo = new DirectoryInfo(installDir);
                estimatedSizeKb = dirInfo.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) / 1024;
            }
            catch { }

            // Prefer HKLM if running with administrator rights, otherwise fallback to HKCU
            RegistryKey? baseKey = null;
            try
            {
                baseKey = Registry.LocalMachine.CreateSubKey(UninstallKeyPath, writable: true);
            }
            catch
            {
                baseKey = Registry.CurrentUser.CreateSubKey(UninstallKeyPath, writable: true);
            }

            if (baseKey == null)
            {
                InstallerLogger.Warning("Could not create registry uninstall key.");
                return false;
            }

            using (baseKey)
            {
                baseKey.SetValue("DisplayName", "LOKA Local Development Environment");
                baseKey.SetValue("DisplayVersion", version);
                baseKey.SetValue("Publisher", "Loka Dev Team");
                baseKey.SetValue("DisplayIcon", $"{mainExe},0");
                baseKey.SetValue("InstallLocation", installDir);
                baseKey.SetValue("UninstallString", $"\"{uninstallerPath}\"");
                baseKey.SetValue("QuietUninstallString", $"\"{uninstallerPath}\" --quiet");
                baseKey.SetValue("EstimatedSize", (int)Math.Max(estimatedSizeKb, 10240), RegistryValueKind.DWord);
                baseKey.SetValue("NoModify", 1, RegistryValueKind.DWord);
                baseKey.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                baseKey.SetValue("HelpLink", "https://github.com/loka/loka");
                baseKey.SetValue("URLInfoAbout", "https://github.com/loka/loka");
            }

            InstallerLogger.Info($"Registered LOKA in Windows Installed Apps at '{UninstallKeyPath}'.");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Failed registering LOKA in Windows Registry", ex);
            return false;
        }
    }

    public static bool UnregisterApplication()
    {
        try
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false); } catch { }
            InstallerLogger.Info("Removed LOKA registry entries from Windows Installed Apps.");
            return true;
        }
        catch (Exception ex)
        {
            InstallerLogger.Error("Failed deleting LOKA registry keys during uninstallation", ex);
            return false;
        }
    }

    public static string? GetInstallLocation()
    {
        try
        {
            using var lmKey = Registry.LocalMachine.OpenSubKey(UninstallKeyPath);
            var loc = lmKey?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(loc)) return loc;

            using var cuKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath);
            loc = cuKey?.GetValue("InstallLocation") as string;
            if (!string.IsNullOrWhiteSpace(loc)) return loc;
        }
        catch { }
        return null;
    }
}
