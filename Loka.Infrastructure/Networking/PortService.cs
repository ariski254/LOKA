using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Infrastructure.Networking;

public class PortService : IPortService
{
    private readonly ILogService _logger;

    public PortService(ILogService logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<PortInfo>> GetPortStatusesAsync(IEnumerable<int> ports)
    {
        var occupiedMap = await GetOccupiedTcpPortsAsync();
        var results = new List<PortInfo>();

        foreach (var port in ports)
        {
            if (occupiedMap.TryGetValue(port, out var info))
            {
                results.Add(info);
            }
            else
            {
                results.Add(new PortInfo
                {
                    Port = port,
                    Protocol = "TCP",
                    IsOccupied = false,
                    ProcessName = "Available"
                });
            }
        }

        return results;
    }

    public async Task<PortInfo> CheckPortAsync(int port)
    {
        var statuses = await GetPortStatusesAsync(new[] { port });
        return statuses[0];
    }

    public async Task<bool> IsPortAvailableAsync(int port)
    {
        var status = await CheckPortAsync(port);
        return !status.IsOccupied;
    }

    public async Task<bool> KillProcessUsingPortAsync(int port)
    {
        var info = await CheckPortAsync(port);
        if (!info.IsOccupied || !info.ProcessId.HasValue)
        {
            return false;
        }

        var pid = info.ProcessId.Value;

        // Protection against killing system processes like System (PID 4) or idle (PID 0)
        if (pid <= 4)
        {
            _logger.LogWarning("PortService", $"Cannot kill system process (PID: {pid}) occupying port {port}.");
            return false;
        }

        try
        {
            var proc = System.Diagnostics.Process.GetProcessById(pid);
            _logger.LogInfo("PortService", $"Terminating process '{proc.ProcessName}' (PID: {pid}) occupying port {port}...");
            proc.Kill(entireProcessTree: true);
            await proc.WaitForExitAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("PortService", $"Failed to terminate process PID {pid} for port {port}: {ex.Message}", ex);
            return false;
        }
    }

    private async Task<Dictionary<int, PortInfo>> GetOccupiedTcpPortsAsync()
    {
        var map = new Dictionary<int, PortInfo>();

        try
        {
            // Execute netstat -ano -p tcp to get exact port to PID mapping on Windows
            var psi = new ProcessStartInfo
            {
                FileName = "netstat.exe",
                Arguments = "-ano -p tcp",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();

                // Regex matches lines like:  TCP    0.0.0.0:80    0.0.0.0:0    LISTENING    1234
                // or [::]:80
                var regex = new Regex(@"^\s*TCP\s+([^\s]+):(\d+)\s+[^\s]+\s+LISTENING\s+(\d+)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                var matches = regex.Matches(output);

                foreach (Match match in matches)
                {
                    if (int.TryParse(match.Groups[2].Value, out var port) &&
                        int.TryParse(match.Groups[3].Value, out var pid))
                    {
                        if (map.ContainsKey(port)) continue;

                        string procName = "Unknown";
                        string procPath = string.Empty;

                        if (pid > 0)
                        {
                            try
                            {
                                var p = System.Diagnostics.Process.GetProcessById(pid);
                                procName = p.ProcessName;
                                try { procPath = p.MainModule?.FileName ?? string.Empty; } catch { }
                            }
                            catch { }
                        }

                        map[port] = new PortInfo
                        {
                            Port = port,
                            Protocol = "TCP",
                            IsOccupied = true,
                            ProcessId = pid,
                            ProcessName = procName,
                            ProcessPath = procPath
                        };
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("PortService", "Failed to retrieve TCP port bindings via netstat", ex);
        }

        // Also cross-reference with IPGlobalProperties
        try
        {
            var ipProps = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = ipProps.GetActiveTcpListeners();
            foreach (var listener in listeners)
            {
                if (!map.ContainsKey(listener.Port))
                {
                    map[listener.Port] = new PortInfo
                    {
                        Port = listener.Port,
                        Protocol = "TCP",
                        IsOccupied = true,
                        ProcessName = "Active Listener"
                    };
                }
            }
        }
        catch { }

        return map;
    }
}
