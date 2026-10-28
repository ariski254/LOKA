using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Runtime.MySql;

public class MySqlService : IMySqlService
{
    private readonly IConfigurationService _configService;
    private readonly IRuntimeDetector _detector;
    private readonly IProcessManager _processManager;
    private readonly ILogService _logger;

    public MySqlService(
        IConfigurationService configService,
        IRuntimeDetector detector,
        IProcessManager processManager,
        ILogService logger)
    {
        _configService = configService;
        _detector = detector;
        _processManager = processManager;
        _logger = logger;
    }

    public async Task<bool> InitializeDataDirectoryAsync()
    {
        var dataDir = Path.Combine(_configService.Config.Paths.DataPath, "mysql");
        if (!Directory.Exists(dataDir))
        {
            Directory.CreateDirectory(dataDir);
        }

        // If the directory already has mysql system tables, skip initialization
        if (Directory.Exists(Path.Combine(dataDir, "mysql")))
        {
            return true;
        }

        var info = await _detector.DetectMySqlAsync();
        if (info == null || !info.IsInstalled || !File.Exists(info.ExecutablePath))
        {
            _logger.LogWarning("MySQL", "MySQL binary not found; initialization skipped.");
            return false;
        }

        try
        {
            _logger.LogInfo("MySQL", $"Initializing fresh MySQL data directory at '{dataDir}'...");
            var args = $"--initialize-insecure --datadir=\"{dataDir}\"";
            var result = await _processManager.ExecuteAsync(info.ExecutablePath, args);

            if (result.Success)
            {
                _logger.LogInfo("MySQL", "MySQL data directory initialized successfully.");
                return true;
            }

            _logger.LogError("MySQL", $"MySQL initialization returned error: {result.StandardError}");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError("MySQL", "Failed to initialize MySQL data directory", ex);
            return false;
        }
    }
}
