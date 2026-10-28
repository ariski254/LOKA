using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Loka.Core.Interfaces;

namespace Loka.Infrastructure.Windows;

public class HostsService : IHostsService
{
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string BeginMarker = "# --- LOKA MANAGED HOSTS BEGIN ---";
    private const string EndMarker = "# --- LOKA MANAGED HOSTS END ---";
    private static readonly string BackupDir = @"C:\Loka\backups\hosts";

    private readonly ILogService _logger;

    public HostsService(ILogService logger)
    {
        _logger = logger;
    }

    public async Task<bool> HasHostEntryAsync(string domain, string ip = "127.0.0.1")
    {
        if (!File.Exists(HostsFilePath)) return false;

        try
        {
            var content = await File.ReadAllTextAsync(HostsFilePath);
            var pattern = $@"(?m)^\s*{Regex.Escape(ip)}\s+{Regex.Escape(domain)}\b";
            return Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase);
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", $"Failed to check hosts file for '{domain}'", ex);
            return false;
        }
    }

    public async Task<bool> AddHostEntryAsync(string domain, string ip = "127.0.0.1")
    {
        return await SyncHostsAsync(new[] { domain }, ip);
    }

    public async Task<bool> SyncHostsAsync(IEnumerable<string> domains, string ip = "127.0.0.1")
    {
        if (!File.Exists(HostsFilePath))
        {
            _logger.LogError("HostsService", $"Hosts file does not exist at '{HostsFilePath}'.");
            return false;
        }

        try
        {
            var domainList = domains.Where(d => !string.IsNullOrWhiteSpace(d)).Select(d => d.Trim()).Distinct().ToList();
            if (domainList.Count == 0) return true;

            var content = await File.ReadAllTextAsync(HostsFilePath);
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();

            int beginIdx = lines.FindIndex(l => l.Trim().Equals(BeginMarker, StringComparison.OrdinalIgnoreCase));
            int endIdx = lines.FindIndex(l => l.Trim().Equals(EndMarker, StringComparison.OrdinalIgnoreCase));

            var currentManaged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (beginIdx >= 0 && endIdx >= beginIdx)
            {
                for (int i = beginIdx + 1; i < endIdx; i++)
                {
                    var line = lines[i].Trim();
                    if (!string.IsNullOrEmpty(line) && !line.StartsWith("#"))
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            currentManaged.Add(parts[1]);
                        }
                    }
                }
            }

            // Union with incoming domains
            bool hasChange = false;
            foreach (var d in domainList)
            {
                if (currentManaged.Add(d))
                {
                    hasChange = true;
                }
            }

            if (!hasChange && beginIdx >= 0 && endIdx >= beginIdx)
            {
                // All domains are already present in hosts file. No update needed.
                return true;
            }

            var managedEntries = currentManaged.Select(d => $"{ip} {d}").ToList();

            if (beginIdx >= 0 && endIdx >= beginIdx)
            {
                // Remove old block inside markers
                lines.RemoveRange(beginIdx + 1, endIdx - beginIdx - 1);
                // Insert updated entries
                lines.InsertRange(beginIdx + 1, managedEntries);
            }
            else
            {
                // Add new managed block at the end
                lines.Add("");
                lines.Add(BeginMarker);
                lines.AddRange(managedEntries);
                lines.Add(EndMarker);
                lines.Add("");
            }

            var newContent = string.Join(Environment.NewLine, lines);
            var success = await WriteHostsFileWithElevationFallbackAsync(newContent);
            if (success)
            {
                FlushDns();
                _logger.LogInfo("HostsService", $"Successfully synced {domainList.Count} domain(s) in hosts file.");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", "Failed to sync hosts file", ex);
            return false;
        }
    }

    public async Task<bool> RemoveHostEntryAsync(string domain)
    {
        if (!File.Exists(HostsFilePath)) return true;

        try
        {
            var content = await File.ReadAllTextAsync(HostsFilePath);
            var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
            int beginIdx = lines.FindIndex(l => l.Trim().Equals(BeginMarker, StringComparison.OrdinalIgnoreCase));
            int endIdx = lines.FindIndex(l => l.Trim().Equals(EndMarker, StringComparison.OrdinalIgnoreCase));

            if (beginIdx >= 0 && endIdx >= beginIdx)
            {
                for (int i = endIdx - 1; i > beginIdx; i--)
                {
                    var line = lines[i].Trim();
                    if (Regex.IsMatch(line, $@"\b{Regex.Escape(domain)}\b", RegexOptions.IgnoreCase))
                    {
                        lines.RemoveAt(i);
                    }
                }
            }

            var newContent = string.Join(Environment.NewLine, lines);
            var success = await WriteHostsFileWithElevationFallbackAsync(newContent);
            if (success)
            {
                FlushDns();
                _logger.LogInfo("HostsService", $"Removed '{domain}' from hosts file.");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", $"Failed to remove host entry for '{domain}'", ex);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetAllManagedHostsAsync()
    {
        var result = new List<string>();
        if (!File.Exists(HostsFilePath)) return result;

        try
        {
            var lines = await File.ReadAllLinesAsync(HostsFilePath);
            bool inBlock = false;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Equals(BeginMarker, StringComparison.OrdinalIgnoreCase))
                {
                    inBlock = true;
                    continue;
                }
                if (trimmed.Equals(EndMarker, StringComparison.OrdinalIgnoreCase))
                {
                    inBlock = false;
                    continue;
                }

                if (inBlock && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("#"))
                {
                    var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2)
                    {
                        result.Add(parts[1]);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", "Failed to read managed hosts entries", ex);
        }

        return result;
    }

    private async Task<bool> WriteHostsFileWithElevationFallbackAsync(string newContent)
    {
        CreateSafeBackup();

        // 1. Try direct write first
        try
        {
            await File.WriteAllTextAsync(HostsFilePath, newContent, Encoding.UTF8);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("HostsService", "Permission denied writing directly to hosts file. Attempting elevated write with UAC...");
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", "Unexpected error writing to hosts file", ex);
            return false;
        }

        // 2. Fallback: Write via elevated PowerShell process
        try
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"loka_hosts_{Guid.NewGuid():N}.tmp");
            await File.WriteAllTextAsync(tempFile, newContent, Encoding.UTF8);

            var escapedTemp = tempFile.Replace("'", "''");
            var escapedHosts = HostsFilePath.Replace("'", "''");

            var script = $"Copy-Item -Path '{escapedTemp}' -Destination '{escapedHosts}' -Force; " +
                         $"icacls '{escapedHosts}' /grant 'BUILTIN\\Users:(M)'; " +
                         $"Remove-Item -Path '{escapedTemp}' -Force -ErrorAction SilentlyContinue; " +
                         $"ipconfig /flushdns";

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
                Verb = "runas",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync();
                if (process.ExitCode == 0)
                {
                    _logger.LogInfo("HostsService", "Elevated write and Users:(M) permission grant completed successfully.");
                    return true;
                }
            }

            _logger.LogError("HostsService", "Elevated process failed to update hosts file.");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("HostsService", "Failed during elevated hosts file modification. Administrator approval is required.", ex);
            return false;
        }
    }

    private void CreateSafeBackup()
    {
        try
        {
            if (!Directory.Exists(BackupDir))
            {
                Directory.CreateDirectory(BackupDir);
            }
            if (File.Exists(HostsFilePath))
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupPath = Path.Combine(BackupDir, $"hosts_{timestamp}.bak");
                File.Copy(HostsFilePath, backupPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("HostsService", $"Could not create backup of hosts file: {ex.Message}");
        }
    }

    private void FlushDns()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "ipconfig.exe",
                Arguments = "/flushdns",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            using var proc = System.Diagnostics.Process.Start(psi);
            proc?.WaitForExit(3000);
        }
        catch { }
    }
}
