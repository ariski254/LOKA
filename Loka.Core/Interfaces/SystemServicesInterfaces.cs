using Loka.Core.Enums;
using Loka.Core.Models;

namespace Loka.Core.Interfaces;

public interface IRuntimeDetector
{
    Task<IReadOnlyList<RuntimeInfo>> DetectAllRuntimesAsync();
    Task<RuntimeInfo?> DetectPhpAsync();
    Task<RuntimeInfo?> DetectNodeAsync();
    Task<RuntimeInfo?> DetectGitAsync();
    Task<RuntimeInfo?> DetectComposerAsync();
    Task<RuntimeInfo?> DetectApacheAsync();
    Task<RuntimeInfo?> DetectMySqlAsync();
    Task<RuntimeInfo?> DetectMariaDbAsync();
    Task<RuntimeInfo?> DetectPostgresAsync();
    Task<RuntimeInfo?> DetectNginxAsync();
}

public interface IServiceManager
{
    IReadOnlyList<ServiceInfo> GetAllServices();
    ServiceInfo? GetService(ServiceType type);
    Task<bool> StartServiceAsync(ServiceType type);
    Task<bool> StopServiceAsync(ServiceType type);
    Task<bool> RestartServiceAsync(ServiceType type);
    Task<bool> EnsurePhpCgiRunningAsync();
    Task<bool> RestartPhpCgiAsync();
    Task<bool> StartAllAsync();
    Task<bool> StopAllAsync();
    Task RefreshAllAsync();
    event Action<ServiceInfo>? ServiceStateChanged;
}

public interface IProjectService
{
    Task<IReadOnlyList<ProjectInfo>> ScanProjectsAsync();
    Task<ProjectInfo?> DetectProjectAsync(string directoryPath);
    Task<bool> ConfigureVirtualHostAsync(ProjectInfo project);
    Task<bool> SyncAllProjectsAsync();
    Task<bool> DeleteProjectAsync(ProjectInfo project, bool deleteFiles = false);
    bool ValidateProjectPathForDeletion(string? path, out string errorMessage);
    Task<bool> CreateAndInstallProjectAsync(
        string name, 
        string targetPath, 
        string domain, 
        string framework, 
        string phpVersion, 
        string webServer, 
        Action<string>? progress = null);

    Task<ProjectCreationPipelineResult> CreateProjectPipelineAsync(
        string name,
        string targetPath,
        string domain,
        string framework,
        string phpVersion,
        string webServer,
        Action<ProjectCreationStage>? onStageChanged = null);
}

public interface IDatabaseService
{
    Task<IReadOnlyList<DatabaseInfo>> GetDatabasesAsync(string? engine = null, int? port = null);
    Task<bool> CreateDatabaseAsync(string databaseName, string? engine = null, int? port = null);
    Task<bool> DropDatabaseAsync(string databaseName, string? engine = null, int? port = null);
    Task<IReadOnlyList<DatabaseTableInfo>> GetTablesAsync(string databaseName, string? engine = null, int? port = null);
    Task<SqlQueryResult> ExecuteQueryAsync(string databaseName, string sql, string? engine = null, int? port = null);
    Task<bool> ExportDatabaseAsync(string databaseName, string targetFilePath, Action<string>? progress = null);
    Task<bool> ImportDatabaseAsync(string databaseName, string sourceFilePath, Action<string>? progress = null);
    Task<bool> SetRootPasswordAsync(string newPassword);
    Task<CurrentDatabaseResolution> ResolveCurrentDatabaseServerAsync();
    Task<bool> SetCurrentDatabaseServerAsync(string serverName);
    Task<bool> SyncPhpMyAdminConfigAsync(string? activeServerName = null);
    Task<DatabaseServerStatus> ProbeDatabaseServerAsync(string serverName);
}

public interface IDiagnosticsService
{
    Task<IReadOnlyList<DiagnosticItem>> RunDiagnosticsAsync();
}

public interface IPhpService
{
    Task<IReadOnlyList<string>> GetAvailablePhpVersionsAsync();
    Task<bool> SetActivePhpVersionAsync(string version);
    Task<IReadOnlyList<PhpExtensionInfo>> GetExtensionsAsync();
    Task<bool> SetExtensionEnabledAsync(string extensionName, bool enable);
    string GetActivePhpBinary();
    Task<(string VersionOutput, string IniOutput, string ModulesOutput)> GetPhpCliDetailsAsync(string? version = null);
    event Action<string>? ActivePhpVersionChanged;
}

public interface IApacheService
{
    Task<(bool IsValid, string Output)> ValidateConfigurationAsync();
    Task<bool> GenerateVHostConfigAsync(ProjectInfo project);
    Task<bool> UpdatePortConfigurationAsync(int port, int sslPort);
    Task<bool> ReloadConfigurationAsync();
}

public interface INginxService
{
    Task<(bool IsValid, string Output)> ValidateConfigurationAsync();
    Task<bool> GenerateServerBlockAsync(ProjectInfo project);
    Task<bool> UpdatePortConfigurationAsync(int port);
    Task<bool> ReloadConfigurationAsync();
}

public interface IMySqlService
{
    Task<bool> InitializeDataDirectoryAsync();
}
