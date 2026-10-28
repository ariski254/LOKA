using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Loka.Core.Exceptions;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Infrastructure.Process;

public class ProcessManager : IProcessManager
{
    private readonly ConcurrentDictionary<string, MonitoredProcess> _runningProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogService _logger;

    public event EventHandler<ProcessUnexpectedExitEventArgs>? ProcessExitedUnexpectedly;

    public ProcessManager(ILogService logger)
    {
        _logger = logger;
    }

    public async Task<ProcessExecutionResult> ExecuteAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default,
        Action<string>? onOutputLine = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? (Path.GetDirectoryName(executable) ?? Directory.GetCurrentDirectory()),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                try { onOutputLine?.Invoke(e.Data); } catch { }
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                try { onOutputLine?.Invoke(e.Data); } catch { }
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);
            stopwatch.Stop();

            return new ProcessExecutionResult
            {
                ExitCode = process.ExitCode,
                StandardOutput = stdoutBuilder.ToString().Trim(),
                StandardError = stderrBuilder.ToString().Trim(),
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError("ProcessManager", $"Failed to execute '{executable} {arguments}'", ex);
            throw new ProcessExecutionException(executable, -1, ex.Message);
        }
    }

    public Task<IMonitoredProcess> StartMonitoredProcessAsync(
        string name,
        string executable,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null)
    {
        if (_runningProcesses.TryGetValue(name, out var existing) && existing.IsRunning)
        {
            _logger.LogWarning("ProcessManager", $"Process '{name}' is already running with PID {existing.ProcessId}.");
            return Task.FromResult<IMonitoredProcess>(existing);
        }

        if (!File.Exists(executable))
        {
            throw new FileNotFoundException($"Executable not found at '{executable}'", executable);
        }

        var psi = new ProcessStartInfo
        {
            FileName = executable,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? (Path.GetDirectoryName(executable) ?? Directory.GetCurrentDirectory()),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (environmentVariables != null)
        {
            foreach (var kvp in environmentVariables)
            {
                psi.Environment[kvp.Key] = kvp.Value;
            }
        }

        var process = new System.Diagnostics.Process { StartInfo = psi };
        process.Start();

        var monitored = new MonitoredProcess(name, process, executable, _logger);
        monitored.UnexpectedlyExited += (s, e) =>
        {
            _runningProcesses.TryRemove(name, out _);
            ProcessExitedUnexpectedly?.Invoke(this, e);
        };
        monitored.Exited += exitCode =>
        {
            _runningProcesses.TryRemove(name, out var _);
        };

        _runningProcesses[name] = monitored;
        _logger.LogInfo("ProcessManager", $"Started '{name}' (PID: {process.Id}) -> {executable} {arguments}");

        return Task.FromResult<IMonitoredProcess>(monitored);
    }

    public async Task<bool> StopMonitoredProcessAsync(string name, int timeoutMilliseconds = 5000)
    {
        if (_runningProcesses.TryRemove(name, out var monitored))
        {
            monitored.MarkManualStop();
            await monitored.KillAsync();
            _logger.LogInfo("ProcessManager", $"Stopped '{name}' successfully.");
            return true;
        }

        return false;
    }

    public IMonitoredProcess? GetMonitoredProcess(string name)
    {
        _runningProcesses.TryGetValue(name, out var proc);
        return proc;
    }

    public IReadOnlyList<IMonitoredProcess> GetRunningProcesses()
    {
        return _runningProcesses.Values.Where(p => p.IsRunning).ToList();
    }
}
