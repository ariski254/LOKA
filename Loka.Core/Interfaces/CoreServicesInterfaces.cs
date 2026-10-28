using Loka.Core.Enums;
using Loka.Core.Models;

namespace Loka.Core.Interfaces;

public interface IPortService
{
    Task<IReadOnlyList<PortInfo>> GetPortStatusesAsync(IEnumerable<int> ports);
    Task<PortInfo> CheckPortAsync(int port);
    Task<bool> IsPortAvailableAsync(int port);
    Task<bool> KillProcessUsingPortAsync(int port);
}

public interface IConfigurationService
{
    LokaConfig Config { get; }
    Task LoadAsync();
    Task SaveAsync();
    void EnsureDirectories();
    string GetActivePhpBinary();
    string GetInternalEnvironmentPath();
    event Action<LokaConfig>? ConfigurationChanged;
}

public interface ILogService
{
    void Log(LogSeverity severity, string source, string message);
    void LogInfo(string source, string message);
    void LogWarning(string source, string message);
    void LogError(string source, string message, Exception? ex = null);
    IReadOnlyList<LogEntry> GetLogs(string? sourceFilter = null, int limit = 500);
    event Action<LogEntry>? LogAdded;
    void Clear();
}

public interface IHostsService
{
    Task<bool> HasHostEntryAsync(string domain, string ip = "127.0.0.1");
    Task<bool> AddHostEntryAsync(string domain, string ip = "127.0.0.1");
    Task<bool> RemoveHostEntryAsync(string domain);
    Task<bool> SyncHostsAsync(IEnumerable<string> domains, string ip = "127.0.0.1");
    Task<IReadOnlyList<string>> GetAllManagedHostsAsync();
}

public interface ISslCertificateService
{
    Task<string> EnsureLocalSslCertificateAsync(string domain, string outputDir);
    Task<bool> EnsureRootCertificateAuthorityAsync(string sslDir);
    Task<bool> TrustRootCertificateAsync(string sslDir);
    bool IsRootCertificateTrusted(string sslDir);
    Task<int> RegenerateAllCertificatesAsync(string sslDir);
}
