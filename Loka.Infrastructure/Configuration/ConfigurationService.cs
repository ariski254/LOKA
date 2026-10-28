using System.Text.Json;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.Infrastructure.FileSystem;

namespace Loka.Infrastructure.Configuration;

public class ConfigurationService : IConfigurationService
{
    private readonly ILogService _logger;
    private LokaConfig _config = new();
    private string _configFilePath;

    public LokaConfig Config => _config;

    public event Action<LokaConfig>? ConfigurationChanged;

    private readonly string? _customConfigPath;

    public ConfigurationService(ILogService logger, string? customConfigPath = null)
    {
        _logger = logger;
        _customConfigPath = customConfigPath;
        if (!string.IsNullOrEmpty(customConfigPath))
        {
            _configFilePath = customConfigPath;
        }
        else
        {
            var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loka");
            _configFilePath = Path.Combine(userConfigDir, "loka.json");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(_customConfigPath))
            {
                if (File.Exists(_customConfigPath))
                {
                    var json = await File.ReadAllTextAsync(_customConfigPath);
                    var loaded = JsonSerializer.Deserialize<LokaConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });
                    if (loaded != null)
                    {
                        _config = loaded;
                        return;
                    }
                }
                return;
            }

            EnsureDirectories();
            var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loka");
            var userConfigFile = Path.Combine(userConfigDir, "loka.json");
            var installConfigFile = Path.Combine(@"C:\Loka\config", "loka.json");

            // 1. Check primary user config (%LocalAppData%\Loka\loka.json)
            if (File.Exists(userConfigFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(userConfigFile);
                    var loaded = JsonSerializer.Deserialize<LokaConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });

                    // Only accept if the configured RootPath actually exists on disk
                    if (loaded != null && !string.IsNullOrWhiteSpace(loaded.Paths.RootPath) && Directory.Exists(loaded.Paths.RootPath))
                    {
                        _config = loaded;
                        _configFilePath = userConfigFile;
                        _logger.LogInfo("Config", $"Loaded primary user configuration from '{userConfigFile}'.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Config", $"Failed reading primary user config '{userConfigFile}': {ex.Message}");
                }
            }

            // 2. Check fallback installation config (C:\Loka\config\loka.json)
            if (File.Exists(installConfigFile))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(installConfigFile);
                    var loaded = JsonSerializer.Deserialize<LokaConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    });

                    if (loaded != null)
                    {
                        _config = loaded;
                        _configFilePath = installConfigFile;
                        _logger.LogInfo("Config", $"Loaded installation configuration from '{installConfigFile}'.");
                        // Sync to primary user config so user changes persist smoothly across sessions
                        await SaveAsync();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Config", $"Failed reading install config '{installConfigFile}': {ex.Message}");
                }
            }

            // 3. Create initial config if not found
            await SaveAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError("Config", "Failed to load config, using defaults", ex);
        }
        finally
        {
            ConfigurationChanged?.Invoke(_config);
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            EnsureDirectories();
            var userConfigDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loka");
            var userConfigFile = Path.Combine(userConfigDir, "loka.json");
            var installConfigFile = Path.Combine(_config.Paths.ConfigPath, "loka.json");

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(_config, options);

            // 1. Save to Primary User Config (%LocalAppData%\Loka\loka.json) via atomic write
            try
            {
                await AtomicWriteFileAsync(userConfigFile, json);
                _configFilePath = userConfigFile;
                _logger.LogInfo("Config", $"Saved primary user configuration to '{userConfigFile}'.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Config", $"Failed saving to primary user configuration '{userConfigFile}'", ex);
            }

            // 2. Sync to Installation Config (C:\Loka\config\loka.json) via atomic write if writable
            try
            {
                await AtomicWriteFileAsync(installConfigFile, json);
                _logger.LogInfo("Config", $"Synchronized configuration to installation path '{installConfigFile}'.");
            }
            catch (Exception ex)
            {
                // Silently log warning if installation path requires UAC elevation
                _logger.LogWarning("Config", $"Installation config sync skipped or non-writable: {ex.Message}");
            }

            ConfigurationChanged?.Invoke(_config);
        }
        catch (Exception ex)
        {
            _logger.LogError("Config", "Failed to save configuration", ex);
        }
    }

    private async Task AtomicWriteFileAsync(string targetPath, string content)
    {
        var targetDir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        var tempPath = targetPath + $".{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, content);

        if (File.Exists(targetPath))
        {
            FileBackupHelper.CreateBackup(targetPath, _logger);
            File.Move(tempPath, targetPath, overwrite: true);
        }
        else
        {
            File.Move(tempPath, targetPath, overwrite: true);
        }
    }

    public void EnsureDirectories()
    {
        var root = _config.Paths.RootPath;
        var dirs = new List<string>
        {
            root,
            // bin structure
            Path.Combine(root, "bin"),
            Path.Combine(root, "bin", "apache"),
            Path.Combine(root, "bin", "nginx"),
            Path.Combine(root, "bin", "mysql"),
            Path.Combine(root, "bin", "mariadb"),
            Path.Combine(root, "bin", "postgresql"),
            Path.Combine(root, "bin", "php"),
            Path.Combine(root, "bin", "php", "8.3"),
            Path.Combine(root, "bin", "nodejs"),
            Path.Combine(root, "bin", "composer"),
            Path.Combine(root, "bin", "phpmyadmin"),
            Path.Combine(root, "bin", "git"),
            // etc configuration structure
            Path.Combine(root, "etc"),
            Path.Combine(root, "etc", "apache"),
            Path.Combine(root, "etc", "nginx"),
            Path.Combine(root, "etc", "mysql"),
            Path.Combine(root, "etc", "mariadb"),
            Path.Combine(root, "etc", "postgresql"),
            Path.Combine(root, "etc", "php"),
            Path.Combine(root, "etc", "phpmyadmin"),
            Path.Combine(root, "etc", "hosts"),
            // data storage isolated per engine
            Path.Combine(root, "data"),
            Path.Combine(root, "data", "mysql"),
            Path.Combine(root, "data", "mariadb"),
            Path.Combine(root, "data", "postgresql"),
            // projects directory
            _config.Paths.ProjectsPath,
            // logs structure per engine
            Path.Combine(root, "logs"),
            Path.Combine(root, "logs", "apache"),
            Path.Combine(root, "logs", "nginx"),
            Path.Combine(root, "logs", "mysql"),
            Path.Combine(root, "logs", "mariadb"),
            Path.Combine(root, "logs", "postgresql"),
            Path.Combine(root, "logs", "loka"),
            // tmp, ssl, backups, config
            Path.Combine(root, "tmp"),
            Path.Combine(root, "ssl"),
            Path.Combine(root, "backups"),
            Path.Combine(root, "config"),
            Path.Combine(root, "config", "apache"),
            Path.Combine(root, "config", "apache", "sites-enabled"),
            Path.Combine(root, "config", "nginx"),
            Path.Combine(root, "config", "nginx", "sites-enabled"),
            Path.Combine(root, "etc", "apache", "sites-enabled"),
            Path.Combine(root, "etc", "nginx", "sites-enabled")
        };

        foreach (var dir in dirs)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Config", $"Could not create directory '{dir}': {ex.Message}");
            }
        }

        EnsureDefaultProjectsIndex();
    }

    private void EnsureDefaultProjectsIndex()
    {
        try
        {
            var indexFile = Path.Combine(_config.Paths.ProjectsPath, "index.php");
            if (!File.Exists(indexFile))
            {
                var content = @"<?php
/**
 * Loka Local Development Dashboard
 * Generated by Loka Environment
 */
$phpVersion = PHP_VERSION;
$webServer = $_SERVER['SERVER_SOFTWARE'] ?? 'Nginx';
$projectsDir = __DIR__;
$projects = [];

if (is_dir($projectsDir)) {
    $items = scandir($projectsDir);
    foreach ($items as $item) {
        if ($item === '.' || $item === '..' || !is_dir($projectsDir . '/' . $item)) {
            continue;
        }
        $domain = $item . '.test';
        $projects[] = [
            'name' => $item,
            'domain' => $domain,
            'url' => 'http://' . $domain
        ];
    }
}
?>
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Loka — Local Development Environment</title>
    <style>
        :root {
            --bg: #0f172a;
            --card: #1e293b;
            --card-border: #334155;
            --text-main: #f8fafc;
            --text-muted: #94a3b8;
            --primary: #3b82f6;
            --primary-hover: #2563eb;
            --accent: #10b981;
            --badge-bg: #1e3a8a;
            --badge-text: #93c5fd;
        }
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body {
            font-family: -apple-system, BlinkMacSystemFont, ""Segoe UI"", Roboto, Helvetica, Arial, sans-serif;
            background-color: var(--bg);
            color: var(--text-main);
            min-height: 100vh;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 40px 20px;
        }
        .container { width: 100%; max-width: 960px; }
        .header {
            display: flex; justify-content: space-between; align-items: center;
            border-bottom: 1px solid var(--card-border); padding-bottom: 24px; margin-bottom: 32px;
        }
        .logo-area { display: flex; align-items: center; gap: 16px; }
        .logo-icon {
            width: 48px; height: 48px;
            background: linear-gradient(135deg, #3b82f6, #8b5cf6);
            border-radius: 12px; display: flex; align-items: center; justify-content: center;
            font-size: 24px; font-weight: bold; color: white;
            box-shadow: 0 4px 12px rgba(59, 130, 246, 0.3);
        }
        .title h1 { font-size: 24px; font-weight: 700; letter-spacing: -0.5px; }
        .title p { color: var(--text-muted); font-size: 14px; margin-top: 4px; }
        .badges { display: flex; gap: 10px; flex-wrap: wrap; }
        .badge {
            background-color: var(--badge-bg); color: var(--badge-text);
            padding: 6px 14px; border-radius: 20px; font-size: 13px; font-weight: 600;
            display: inline-flex; align-items: center; gap: 6px;
        }
        .badge-green {
            background-color: rgba(16, 185, 129, 0.15); color: #34d399;
            border: 1px solid rgba(16, 185, 129, 0.3);
        }
        .quick-actions {
            display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
            gap: 16px; margin-bottom: 32px;
        }
        .action-card {
            background-color: var(--card); border: 1px solid var(--card-border);
            border-radius: 12px; padding: 20px; display: flex; align-items: center;
            justify-content: space-between; text-decoration: none; color: inherit;
            transition: all 0.2s ease;
        }
        .action-card:hover {
            transform: translateY(-2px); border-color: var(--primary);
            box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
        }
        .action-card h3 { font-size: 16px; font-weight: 600; }
        .action-card p { font-size: 13px; color: var(--text-muted); margin-top: 4px; }
        .section-title { font-size: 18px; font-weight: 600; margin-bottom: 16px; display: flex; align-items: center; gap: 8px; }
        .projects-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; }
        .project-card {
            background-color: var(--card); border: 1px solid var(--card-border);
            border-radius: 12px; padding: 20px; display: flex; flex-direction: column;
            justify-content: space-between; transition: all 0.2s ease;
        }
        .project-card:hover {
            transform: translateY(-2px); border-color: var(--primary);
            box-shadow: 0 6px 20px rgba(0, 0, 0, 0.25);
        }
        .project-header { display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px; }
        .project-name { font-size: 17px; font-weight: 600; color: var(--text-main); }
        .project-link { font-size: 14px; color: var(--primary); text-decoration: none; word-break: break-all; display: inline-block; margin-top: 6px; }
        .project-link:hover { text-decoration: underline; }
        .footer { margin-top: 48px; text-align: center; color: var(--text-muted); font-size: 13px; }
    </style>
</head>
<body>
    <div class=""container"">
        <header class=""header"">
            <div class=""logo-area"">
                <div class=""logo-icon"">L</div>
                <div class=""title"">
                    <h1>Loka Dashboard</h1>
                    <p>High-Performance Windows Development Environment</p>
                </div>
            </div>
            <div class=""badges"">
                <span class=""badge badge-green"">● Active</span>
                <span class=""badge"">PHP <?= htmlspecialchars($phpVersion) ?></span>
                <span class=""badge""><?= htmlspecialchars($webServer) ?></span>
            </div>
        </header>

        <div class=""quick-actions"">
            <a href=""/phpmyadmin/"" class=""action-card"">
                <div>
                    <h3>phpMyAdmin</h3>
                    <p>Manage MySQL & MariaDB databases</p>
                </div>
                <span style=""font-size: 20px; color: var(--primary);"">➔</span>
            </a>
            <div class=""action-card"" style=""cursor: default;"">
                <div>
                    <h3>Projects Root</h3>
                    <p><?= htmlspecialchars($projectsDir) ?></p>
                </div>
                <span style=""font-size: 14px; color: var(--accent);"">📁</span>
            </div>
        </div>

        <h2 class=""section-title"">Active Projects (<?= count($projects) ?>)</h2>
        <div class=""projects-grid"">
            <?php if (empty($projects)): ?>
                <p style=""color: var(--text-muted);"">No projects detected in <code><?= htmlspecialchars($projectsDir) ?></code>.</p>
            <?php else: ?>
                <?php foreach ($projects as $proj): ?>
                    <div class=""project-card"">
                        <div class=""project-header"">
                            <div>
                                <div class=""project-name""><?= htmlspecialchars($proj['name']) ?></div>
                                <a href=""<?= htmlspecialchars($proj['url']) ?>"" target=""_blank"" class=""project-link"">
                                    <?= htmlspecialchars($proj['domain']) ?> ↗
                                </a>
                            </div>
                        </div>
                    </div>
                <?php endforeach; ?>
            <?php endif; ?>
        </div>

        <footer class=""footer"">
            <p>Loka Environment &bull; Running on Windows &bull; All services operational</p>
        </footer>
    </div>
</body>
</html>";
                File.WriteAllText(indexFile, content);
                _logger.LogInfo("Config", $"Default projects dashboard generated at '{indexFile}'.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Config", $"Could not ensure default projects index: {ex.Message}");
        }
    }

    public string GetActivePhpBinary()
    {
        // 1. Check in internal LOKA bin directory for configured active version (Section 16 & 8)
        if (!string.IsNullOrEmpty(_config.Php.ActiveVersion))
        {
            var internalVersioned = Path.Combine(_config.Paths.BinPath, "php", _config.Php.ActiveVersion, "php.exe");
            if (File.Exists(internalVersioned)) return internalVersioned;

            // Also check partial match (e.g. 8.4 in 8.4.x)
            var phpBase = Path.Combine(_config.Paths.BinPath, "php");
            if (Directory.Exists(phpBase))
            {
                var match = Directory.GetDirectories(phpBase)
                    .FirstOrDefault(d => Path.GetFileName(d).StartsWith(_config.Php.ActiveVersion, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    var exe = Path.Combine(match, "php.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
        }

        // 2. Check general internal bin/php/php.exe
        var directInternal = Path.Combine(_config.Paths.BinPath, "php", "php.exe");
        if (File.Exists(directInternal)) return directInternal;

        // 3. Check any php.exe in internal bin/php subfolders
        var internalPhpDir = Path.Combine(_config.Paths.BinPath, "php");
        if (Directory.Exists(internalPhpDir))
        {
            var firstInternal = Directory.GetFiles(internalPhpDir, "php.exe", SearchOption.AllDirectories).FirstOrDefault();
            if (!string.IsNullOrEmpty(firstInternal) && File.Exists(firstInternal)) return firstInternal;
        }

        // 4. Secondary fallback: only if AllowExternalRuntimes is true
        if (_config.Paths.AllowExternalRuntimes)
        {
            foreach (var searchPath in _config.Paths.AdditionalRuntimeSearchPaths)
            {
                var check = Path.Combine(searchPath, "php.exe");
                if (File.Exists(check)) return check;

                var phpFolder = Path.Combine(searchPath, "php");
                if (Directory.Exists(phpFolder))
                {
                    var subPhp = Directory.GetFiles(phpFolder, "php.exe", SearchOption.AllDirectories).FirstOrDefault();
                    if (!string.IsNullOrEmpty(subPhp) && File.Exists(subPhp)) return subPhp;
                }
            }
        }

        return "php.exe";
    }

    public string GetInternalEnvironmentPath()
    {
        var binRoot = _config.Paths.BinPath;
        var paths = new List<string>();

        // 1. Active PHP
        var activePhp = GetActivePhpBinary();
        if (File.Exists(activePhp))
        {
            var phpDir = Path.GetDirectoryName(activePhp);
            if (!string.IsNullOrEmpty(phpDir)) paths.Add(phpDir);
        }
        else
        {
            var defaultPhpDir = Path.Combine(binRoot, "php", _config.Php.ActiveVersion);
            paths.Add(defaultPhpDir);
        }

        // 2. Node.js
        var nodeDir = Path.Combine(binRoot, "nodejs");
        if (Directory.Exists(nodeDir))
        {
            var subNode = Directory.GetDirectories(nodeDir).FirstOrDefault() ?? nodeDir;
            paths.Add(subNode);
        }
        else
        {
            paths.Add(nodeDir);
        }

        // 3. Composer
        var composerDir = Path.Combine(binRoot, "composer");
        paths.Add(composerDir);

        // 4. Git
        var gitDir = Path.Combine(binRoot, "git");
        if (Directory.Exists(gitDir))
        {
            var cmdDir = Path.Combine(gitDir, "cmd");
            if (Directory.Exists(cmdDir)) paths.Add(cmdDir);
            else paths.Add(gitDir);
        }
        else
        {
            paths.Add(gitDir);
        }

        // 5. Database engine bin paths (MySQL / MariaDB / PostgreSQL)
        var mysqlDir = Path.Combine(binRoot, "mysql");
        if (Directory.Exists(mysqlDir))
        {
            var sub = Directory.GetDirectories(mysqlDir).FirstOrDefault();
            if (sub != null && Directory.Exists(Path.Combine(sub, "bin"))) paths.Add(Path.Combine(sub, "bin"));
            else if (Directory.Exists(Path.Combine(mysqlDir, "bin"))) paths.Add(Path.Combine(mysqlDir, "bin"));
            else paths.Add(mysqlDir);
        }
        else
        {
            paths.Add(mysqlDir);
        }

        var mariaDir = Path.Combine(binRoot, "mariadb");
        if (Directory.Exists(mariaDir))
        {
            var sub = Directory.GetDirectories(mariaDir).FirstOrDefault();
            if (sub != null && Directory.Exists(Path.Combine(sub, "bin"))) paths.Add(Path.Combine(sub, "bin"));
            else if (Directory.Exists(Path.Combine(mariaDir, "bin"))) paths.Add(Path.Combine(mariaDir, "bin"));
            else paths.Add(mariaDir);
        }
        else
        {
            paths.Add(mariaDir);
        }

        var pgDir = Path.Combine(binRoot, "postgresql");
        if (Directory.Exists(pgDir))
        {
            var sub = Directory.GetDirectories(pgDir).FirstOrDefault();
            if (sub != null && Directory.Exists(Path.Combine(sub, "bin"))) paths.Add(Path.Combine(sub, "bin"));
            else if (Directory.Exists(Path.Combine(pgDir, "bin"))) paths.Add(Path.Combine(pgDir, "bin"));
            else paths.Add(pgDir);
        }
        else
        {
            paths.Add(pgDir);
        }

        // 6. Apache & Nginx
        var apacheDir = Path.Combine(binRoot, "apache");
        if (Directory.Exists(apacheDir))
        {
            var sub = Directory.GetDirectories(apacheDir).FirstOrDefault();
            if (sub != null && Directory.Exists(Path.Combine(sub, "bin"))) paths.Add(Path.Combine(sub, "bin"));
            else paths.Add(apacheDir);
        }
        else
        {
            paths.Add(apacheDir);
        }

        var nginxDir = Path.Combine(binRoot, "nginx");
        if (Directory.Exists(nginxDir))
        {
            var sub = Directory.GetDirectories(nginxDir).FirstOrDefault() ?? nginxDir;
            paths.Add(sub);
        }
        else
        {
            paths.Add(nginxDir);
        }

        return string.Join(";", paths.Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
