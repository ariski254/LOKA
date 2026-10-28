namespace Loka.Installer.Models;

public enum ProcessLockAction
{
    Retry,
    CloseAutomatically,
    Cancel
}

public class ProcessLockInfo
{
    public string ProcessName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
