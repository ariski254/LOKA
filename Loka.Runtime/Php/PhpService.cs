using System.Text.RegularExpressions;
using Loka.Core.Exceptions;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.Infrastructure.FileSystem;

namespace Loka.Runtime.Php;

public class PhpService : IPhpService
{
    private readonly IConfigurationService _configService;
    private readonly IRuntimeDetector _detector;
    private readonly IProcessManager _processManager;
    private readonly ILogService _logger;

    public event Action<string>? ActivePhpVersionChanged;

    public PhpService(
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

    public async Task<IReadOnlyList<string>> GetAvailablePhpVersionsAsync()
    {
        var info = await _detector.DetectPhpAsync();
        var versions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (info != null && info.IsInstalled)
        {
            if (!string.IsNullOrEmpty(info.Version))
            {
                var m = System.Text.RegularExpressions.Regex.Match(info.Version, @"^([0-9]+\.[0-9]+)");
                if (m.Success && Version.TryParse(m.Groups[1].Value, out var v) && v >= new Version(8, 3))
                {
                    versions.Add(m.Groups[1].Value);
                }
            }

            foreach (var v in info.AvailableVersions)
            {
                if (!string.IsNullOrEmpty(v) && Version.TryParse(v, out var parsed) && parsed >= new Version(8, 3))
                {
                    versions.Add(v);
                }
            }
        }

        foreach (var supported in _configService.Config.Php.AvailableVersions)
        {
            if (Version.TryParse(supported, out var parsed) && parsed >= new Version(8, 3))
            {
                var candidate = Path.Combine(_configService.Config.Paths.BinPath, "php", supported, "php.exe");
                if (File.Exists(candidate))
                {
                    versions.Add(supported);
                }
            }
        }

        return versions.OrderBy(v => v).ToList();
    }

    public async Task<bool> SetActivePhpVersionAsync(string version)
    {
        if (string.IsNullOrWhiteSpace(version)) return false;

        string previousVersion = _configService.Config.Php.ActiveVersion;
        _logger.LogInfo("PHP", $"Request to switch active PHP version from '{previousVersion}' to '{version}'...");

        // 1. Locate the candidate binary in C:\Loka\bin\php\{version}\php.exe
        var candidateDir = Path.Combine(_configService.Config.Paths.BinPath, "php", version);
        var candidateBinary = Path.Combine(candidateDir, "php.exe");

        if (!File.Exists(candidateBinary))
        {
            var phpBase = Path.Combine(_configService.Config.Paths.BinPath, "php");
            if (Directory.Exists(phpBase))
            {
                var match = Directory.GetDirectories(phpBase)
                    .FirstOrDefault(d => Path.GetFileName(d).StartsWith(version, StringComparison.OrdinalIgnoreCase));
                if (match != null && File.Exists(Path.Combine(match, "php.exe")))
                {
                    candidateDir = match;
                    candidateBinary = Path.Combine(match, "php.exe");
                }
            }
        }

        if (!File.Exists(candidateBinary))
        {
            _logger.LogError("PHP", $"Cannot switch to PHP {version}: Executable not found at '{candidateBinary}'. Retaining active version '{previousVersion}'.");
            return false;
        }

        // 2. Validate candidate execution (Rule 12)
        var testResult = await _processManager.ExecuteAsync(candidateBinary, "-v");
        if (!testResult.Success)
        {
            _logger.LogError("PHP", $"Validation failed for PHP {version} at '{candidateBinary}': {testResult.StandardError}. Retaining active version '{previousVersion}'.");
            return false;
        }

        // 3. Mirror candidate version to root C:\Loka\bin\php\ so global path, CLI and composer run this version (Rule 7)
        try
        {
            var rootPhpDir = Path.Combine(_configService.Config.Paths.BinPath, "php");
            if (Directory.Exists(rootPhpDir))
            {
                foreach (var exe in Directory.GetFiles(candidateDir, "*.exe"))
                {
                    var dest = Path.Combine(rootPhpDir, Path.GetFileName(exe));
                    try { File.Copy(exe, dest, overwrite: true); } catch { }
                }

                foreach (var dll in Directory.GetFiles(candidateDir, "*.dll"))
                {
                    var dest = Path.Combine(rootPhpDir, Path.GetFileName(dll));
                    try { File.Copy(dll, dest, overwrite: true); } catch { }
                }

                var candidateIni = Path.Combine(candidateDir, "php.ini");
                if (File.Exists(candidateIni))
                {
                    try { File.Copy(candidateIni, Path.Combine(rootPhpDir, "php.ini"), overwrite: true); } catch { }
                }

                var candidateExt = Path.Combine(candidateDir, "ext");
                var rootExt = Path.Combine(rootPhpDir, "ext");
                if (Directory.Exists(candidateExt))
                {
                    if (!Directory.Exists(rootExt)) Directory.CreateDirectory(rootExt);
                    foreach (var extFile in Directory.GetFiles(candidateExt, "*.dll"))
                    {
                        try { File.Copy(extFile, Path.Combine(rootExt, Path.GetFileName(extFile)), overwrite: true); } catch { }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PHP", $"Notice while mirroring PHP {version} files to root: {ex.Message}");
        }

        // 4. Update Composer wrapper script to use the active version (Rule 9)
        try
        {
            var composerBat = Path.Combine(_configService.Config.Paths.BinPath, "composer", "composer.bat");
            if (File.Exists(composerBat))
            {
                var content = $"@echo off\r\n\"%~dp0..\\php\\{version}\\php.exe\" \"%~dp0composer.phar\" %*\r\n";
                await File.WriteAllTextAsync(composerBat, content);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PHP", $"Notice updating composer.bat: {ex.Message}");
        }

        // 5. Update configuration and persist
        _configService.Config.Php.ActiveVersion = version;
        await _configService.SaveAsync();

        // 6. Terminate previous php-cgi daemon and immediately spawn the new version on port 9000
        try
        {
            try { await _processManager.StopMonitoredProcessAsync("PHP-CGI"); } catch { }
            var cgiProcesses = System.Diagnostics.Process.GetProcessesByName("php-cgi");
            foreach (var p in cgiProcesses)
            {
                try { p.Kill(entireProcessTree: true); } catch { }
            }

            var candidateCgi = Path.Combine(candidateDir, "php-cgi.exe");
            if (File.Exists(candidateCgi))
            {
                var env = new Dictionary<string, string>
                {
                    { "PHP_FCGI_MAX_REQUESTS", "10000" },
                    { "PHP_FCGI_CHILDREN", "4" },
                    { "PHPRC", candidateDir },
                    { "PHP_INI_SCAN_DIR", "" }
                };
                await _processManager.StartMonitoredProcessAsync(
                    "PHP-CGI",
                    candidateCgi,
                    "-b 127.0.0.1:9000",
                    workingDirectory: candidateDir,
                    environmentVariables: env);
                _logger.LogInfo("PHP", $"Spawned FastCGI daemon ({candidateCgi}) on 127.0.0.1:9000.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("PHP", $"Notice spawning FastCGI daemon for PHP {version}: {ex.Message}");
        }

        _logger.LogInfo("PHP", $"Active PHP successfully switched to {version}: {testResult.StandardOutput.Split('\n').FirstOrDefault()}");
        ActivePhpVersionChanged?.Invoke(version);
        return true;
    }

    public async Task<(string VersionOutput, string IniOutput, string ModulesOutput)> GetPhpCliDetailsAsync(string? version = null)
    {
        var binary = GetActivePhpBinary();
        if (!string.IsNullOrEmpty(version))
        {
            var specific = Path.Combine(_configService.Config.Paths.BinPath, "php", version, "php.exe");
            if (File.Exists(specific)) binary = specific;
        }

        if (!File.Exists(binary))
        {
            return ("PHP executable not found", "php.ini not found", "No modules loaded");
        }

        var vRes = await _processManager.ExecuteAsync(binary, "-v");
        var iniRes = await _processManager.ExecuteAsync(binary, "--ini");
        var mRes = await _processManager.ExecuteAsync(binary, "-m");

        return (
            vRes.Success ? vRes.StandardOutput.Trim() : vRes.StandardError.Trim(),
            iniRes.Success ? iniRes.StandardOutput.Trim() : iniRes.StandardError.Trim(),
            mRes.Success ? mRes.StandardOutput.Trim() : mRes.StandardError.Trim()
        );
    }

    public async Task<IReadOnlyList<PhpExtensionInfo>> GetExtensionsAsync()
    {
        var iniPath = GetActivePhpIniPath();
        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath))
        {
            // Default common PHP extensions list
            return GetDefaultExtensionsList();
        }

        try
        {
            var lines = await File.ReadAllLinesAsync(iniPath);
            var extensions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            var extRegex = new Regex(@"^\s*(;?)\s*extension\s*=\s*(?:php_)?([a-zA-Z0-9_\-]+)(?:\.dll)?", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                var match = extRegex.Match(line);
                if (match.Success)
                {
                    bool isCommented = match.Groups[1].Value.Contains(';');
                    string extName = match.Groups[2].Value.ToLowerInvariant();

                    if (!isCommented)
                    {
                        // Active uncommented extension line
                        extensions[extName] = true;
                    }
                    else if (!extensions.ContainsKey(extName))
                    {
                        // Only mark as disabled if not already seen as enabled elsewhere in php.ini
                        extensions[extName] = false;
                    }
                }
            }

            var commonList = GetDefaultExtensionsList();
            foreach (var ext in commonList)
            {
                if (extensions.TryGetValue(ext.Name, out var isEnabled))
                {
                    ext.IsEnabled = isEnabled;
                }
            }

            return commonList;
        }
        catch (Exception ex)
        {
            _logger.LogError("PHP", "Failed to parse php.ini extensions", ex);
            return GetDefaultExtensionsList();
        }
    }

    public async Task<bool> SetExtensionEnabledAsync(string extensionName, bool enable)
    {
        var iniPath = GetActivePhpIniPath();
        if (string.IsNullOrEmpty(iniPath) || !File.Exists(iniPath))
        {
            _logger.LogError("PHP", $"php.ini not found to toggle extension '{extensionName}'.");
            return false;
        }

        _logger.LogInfo("PHP", $"{(enable ? "Enabling" : "Disabling")} PHP extension '{extensionName}' in '{iniPath}'...");

        try
        {
            await FileBackupHelper.SafeModifyAsync(iniPath, async content =>
            {
                var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList();
                var extPattern = $@"^\s*;?\s*extension\s*=\s*(php_)?{Regex.Escape(extensionName)}(\.dll)?\s*$";
                var regex = new Regex(extPattern, RegexOptions.IgnoreCase);

                bool firstHandled = false;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (regex.IsMatch(lines[i]))
                    {
                        if (enable)
                        {
                            if (!firstHandled)
                            {
                                lines[i] = $"extension={extensionName}";
                                firstHandled = true;
                            }
                            else
                            {
                                // Ensure duplicate entries are commented out to avoid PHP 'already loaded' warnings
                                lines[i] = $";extension={extensionName}";
                            }
                        }
                        else
                        {
                            lines[i] = $";extension={extensionName}";
                            firstHandled = true;
                        }
                    }
                }

                if (!firstHandled && enable)
                {
                    lines.Add($"extension={extensionName}");
                }

                return await Task.FromResult(string.Join(Environment.NewLine, lines));
            }, _logger);

            // Validate with php -v specifically for the extension being enabled
            if (enable)
            {
                var phpBinary = GetActivePhpBinary();
                if (File.Exists(phpBinary))
                {
                    var result = await _processManager.ExecuteAsync(phpBinary, "-v");
                    if (result.StandardError.Contains($"Unable to load dynamic library '{extensionName}'", StringComparison.OrdinalIgnoreCase) ||
                        result.StandardError.Contains($"php_{extensionName}.dll", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogError("PHP", $"PHP extension '{extensionName}' could not be loaded: {result.StandardError}");
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError("PHP", $"Failed to toggle extension '{extensionName}'", ex);
            return false;
        }
    }

    public string GetActivePhpBinary()
    {
        return _configService.GetActivePhpBinary();
    }

    public string? GetActivePhpIniPath()
    {
        var binary = GetActivePhpBinary();
        if (!File.Exists(binary)) return null;

        var dir = Path.GetDirectoryName(binary);
        if (string.IsNullOrEmpty(dir)) return null;

        var candidates = new[]
        {
            Path.Combine(dir, "php.ini"),
            Path.Combine(dir, "php.ini-development"),
            Path.Combine(dir, "php.ini-production")
        };

        foreach (var cand in candidates)
        {
            if (File.Exists(cand)) return cand;
        }

        return null;
    }

    private static List<PhpExtensionInfo> GetDefaultExtensionsList()
    {
        return new List<PhpExtensionInfo>
        {
            new() { Name = "curl", IsEnabled = true, Description = "Client URL Library" },
            new() { Name = "fileinfo", IsEnabled = true, Description = "File Information" },
            new() { Name = "mbstring", IsEnabled = true, Description = "Multibyte String" },
            new() { Name = "openssl", IsEnabled = true, Description = "OpenSSL cryptography" },
            new() { Name = "pdo_mysql", IsEnabled = true, Description = "MySQL PDO Driver" },
            new() { Name = "mysqli", IsEnabled = true, Description = "Improved MySQL Extension" },
            new() { Name = "zip", IsEnabled = true, Description = "Zip compression" },
            new() { Name = "gd", IsEnabled = false, Description = "Image processing" },
            new() { Name = "redis", IsEnabled = false, Description = "Redis key-value store" },
            new() { Name = "sqlite3", IsEnabled = true, Description = "SQLite 3 Database" },
            new() { Name = "pdo_sqlite", IsEnabled = true, Description = "SQLite PDO Driver" },
            new() { Name = "soap", IsEnabled = false, Description = "SOAP Web Services" },
            new() { Name = "intl", IsEnabled = false, Description = "Internationalization" },
            new() { Name = "sodium", IsEnabled = true, Description = "Modern Cryptography" },
            new() { Name = "exif", IsEnabled = false, Description = "Exchangeable Image Information" }
        };
    }
}
