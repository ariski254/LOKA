using System.Diagnostics;
using Loka.Core.Models;

namespace Loka.Core.Interfaces;

public interface IMonitoredProcess
{
    string Name { get; }
    int ProcessId { get; }
    bool IsRunning { get; }
    DateTime StartTime { get; }
    string ExecutablePath { get; }
    event Action<string>? OutputReceived;
    event Action<string>? ErrorReceived;
    event Action<int>? Exited;
    Task KillAsync();
}

public class ProcessUnexpectedExitEventArgs : EventArgs
{
    public string ProcessName { get; }
    public int ProcessId { get; }
    public int ExitCode { get; }
    public string? LastErrorOutput { get; }

    public ProcessUnexpectedExitEventArgs(string processName, int processId, int exitCode, string? lastErrorOutput)
    {
        ProcessName = processName;
        ProcessId = processId;
        ExitCode = exitCode;
        LastErrorOutput = lastErrorOutput;
    }
}

public interface IProcessManager
{
    Task<ProcessExecutionResult> ExecuteAsync(
        string executable, 
        string arguments, 
        string? workingDirectory = null, 
        IDictionary<string, string>? environmentVariables = null, 
        CancellationToken cancellationToken = default,
        Action<string>? onOutputLine = null);

    Task<IMonitoredProcess> StartMonitoredProcessAsync(
        string name, 
        string executable, 
        string arguments, 
        string? workingDirectory = null, 
        IDictionary<string, string>? environmentVariables = null);

    Task<bool> StopMonitoredProcessAsync(string name, int timeoutMilliseconds = 5000);

    IMonitoredProcess? GetMonitoredProcess(string name);

    IReadOnlyList<IMonitoredProcess> GetRunningProcesses();

    event EventHandler<ProcessUnexpectedExitEventArgs>? ProcessExitedUnexpectedly;
}
