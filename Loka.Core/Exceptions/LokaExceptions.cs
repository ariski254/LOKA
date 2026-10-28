namespace Loka.Core.Exceptions;

public class LokaException : Exception
{
    public LokaException(string message) : base(message) { }
    public LokaException(string message, Exception innerException) : base(message, innerException) { }
}

public class PortConflictException : LokaException
{
    public int Port { get; }
    public int? OccupyingProcessId { get; }
    public string OccupyingProcessName { get; }

    public PortConflictException(int port, int? processId, string processName)
        : base($"Port {port} is occupied by {processName} (PID: {processId?.ToString() ?? "Unknown"}).")
    {
        Port = port;
        OccupyingProcessId = processId;
        OccupyingProcessName = processName;
    }
}

public class ProcessExecutionException : LokaException
{
    public int ExitCode { get; }
    public string StandardError { get; }

    public ProcessExecutionException(string processName, int exitCode, string stdErr)
        : base($"Process '{processName}' failed with exit code {exitCode}: {stdErr}")
    {
        ExitCode = exitCode;
        StandardError = stdErr;
    }
}

public class ConfigurationException : LokaException
{
    public string ConfigPath { get; }

    public ConfigurationException(string configPath, string message)
        : base($"Configuration error in '{configPath}': {message}")
    {
        ConfigPath = configPath;
    }
}

public class ServiceOperationException : LokaException
{
    public string ServiceName { get; }

    public ServiceOperationException(string serviceName, string message)
        : base($"Failed operation on service '{serviceName}': {message}")
    {
        ServiceName = serviceName;
    }

    public ServiceOperationException(string serviceName, string message, Exception innerException)
        : base($"Failed operation on service '{serviceName}': {message}", innerException)
    {
        ServiceName = serviceName;
    }
}
