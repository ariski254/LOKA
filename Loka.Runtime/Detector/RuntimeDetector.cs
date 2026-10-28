using System.Diagnostics;
using System.Text.RegularExpressions;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Runtime.Detector;

public class RuntimeDetector : IRuntimeDetector
{
    private readonly IConfigurationService _configService;
    private readonly ILogService _logger;

    public RuntimeDetector(IConfigurationService configService, ILogService logger)
    {
        _configService = configService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RuntimeInfo>> DetectAllRuntimesAsync()
    {
        var runtimes = new List<RuntimeInfo>();

        var php = await DetectPhpAsync();
        if (php != null) runtimes.Add(php);

        var node = await DetectNodeAsync();
        if (node != null) runtimes.Add(node);

        var composer = await DetectComposerAsync();
        if (composer != null) runtimes.Add(composer);

        var git = await DetectGitAsync();
        if (git != null) runtimes.Add(git);

        var apache = await DetectApacheAsync();
        if (apache != null) runtimes.Add(apache);

        var mysql = await DetectMySqlAsync();
        if (mysql != null) runtimes.Add(mysql);

        var mariadb = await DetectMariaDbAsync();
        if (mariadb != null) runtimes.Add(mariadb);

        var postgres = await DetectPostgresAsync();
        if (postgres != null) runtimes.Add(postgres);

        var nginx = await DetectNginxAsync();
        if (nginx != null) runtimes.Add(nginx);

        return runtimes;
    }

    public async Task<RuntimeInfo?> DetectPhpAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidatePaths = new List<string>();

        // 1. Check internal C:\Loka\bin\php
        var lokaPhp = Path.Combine(binPath, "php");
        if (Directory.Exists(lokaPhp))
        {
            // Active version prioritized
            var activeVer = _configService.Config.Php.ActiveVersion;
            if (!string.IsNullOrEmpty(activeVer))
            {
                var directActive = Path.Combine(lokaPhp, activeVer, "php.exe");
                if (File.Exists(directActive)) candidatePaths.Add(directActive);
            }

            var direct = Path.Combine(lokaPhp, "php.exe");
            if (File.Exists(direct)) candidatePaths.Add(direct);

            candidatePaths.AddRange(Directory.GetFiles(lokaPhp, "php.exe", SearchOption.AllDirectories));
        }

        // 2. Fallback to external paths only if configured or internal not found
        if (!candidatePaths.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            foreach (var dir in _configService.Config.Paths.AdditionalRuntimeSearchPaths)
            {
                if (Directory.Exists(dir))
                {
                    var direct = Path.Combine(dir, "php.exe");
                    if (File.Exists(direct)) candidatePaths.Add(direct);

                    var phpFolder = Path.Combine(dir, "php");
                    if (Directory.Exists(phpFolder))
                    {
                        try { candidatePaths.AddRange(Directory.GetFiles(phpFolder, "php.exe", SearchOption.AllDirectories)); } catch { }
                    }
                }
            }

            var fromPath = FindExecutableInPath("php.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidatePaths.Add(fromPath);
        }

        candidatePaths = candidatePaths.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists).ToList();

        if (!candidatePaths.Any())
        {
            return new RuntimeInfo { Name = "PHP", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var activePath = candidatePaths.First();
        var (version, _) = await GetExecutableVersionAsync(activePath, "-v", @"PHP\s+([0-9]+\.[0-9]+\.[0-9]+)");

        var allVersions = new List<string>();
        foreach (var path in candidatePaths)
        {
            var dirName = Path.GetFileName(Path.GetDirectoryName(path) ?? "");
            if (Regex.IsMatch(dirName, @"^[0-9]+\.[0-9]+"))
            {
                if (Version.TryParse(dirName, out var parsedVer) && parsedVer >= new Version(8, 3))
                {
                    allVersions.Add(dirName);
                }
            }
        }

        string source = DetermineSource(activePath);
        return new RuntimeInfo
        {
            Name = "PHP",
            Version = version,
            ExecutablePath = activePath,
            DirectoryPath = Path.GetDirectoryName(activePath) ?? "",
            IsInstalled = true,
            IsActive = true,
            Status = source.StartsWith("Internal") ? "Installed (Bundled)" : "Installed (External)",
            Source = source,
            AvailableVersions = allVersions.Distinct().ToList()
        };
    }

    public async Task<RuntimeInfo?> DetectNodeAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\nodejs
        var lokaNode = Path.Combine(binPath, "nodejs");
        if (Directory.Exists(lokaNode))
        {
            var direct = Path.Combine(lokaNode, "node.exe");
            if (File.Exists(direct)) candidates.Add(direct);
            try { candidates.AddRange(Directory.GetFiles(lokaNode, "node.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var nodePath = FindExecutableInPath("node.exe");
            if (!string.IsNullOrEmpty(nodePath)) candidates.Add(nodePath);

            var common = @"C:\Program Files\nodejs\node.exe";
            if (File.Exists(common)) candidates.Add(common);
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "Node.js", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "-v", @"v?([0-9]+\.[0-9]+\.[0-9]+)");
        string source = DetermineSource(found);
        return new RuntimeInfo
        {
            Name = "Node.js",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = true,
            Status = source.StartsWith("Internal") ? "Installed (Bundled)" : "Installed (External)",
            Source = source
        };
    }

    public async Task<RuntimeInfo?> DetectComposerAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\composer
        var lokaComposer = Path.Combine(binPath, "composer");
        if (Directory.Exists(lokaComposer))
        {
            var bat = Path.Combine(lokaComposer, "composer.bat");
            if (File.Exists(bat)) candidates.Add(bat);
            var phar = Path.Combine(lokaComposer, "composer.phar");
            if (File.Exists(phar)) candidates.Add(phar);
            var exe = Path.Combine(lokaComposer, "composer.exe");
            if (File.Exists(exe)) candidates.Add(exe);
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var composerPath = FindExecutableInPath("composer.bat");
            if (string.IsNullOrEmpty(composerPath)) composerPath = FindExecutableInPath("composer");
            if (!string.IsNullOrEmpty(composerPath)) candidates.Add(composerPath);
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "Composer", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "--version", @"Composer\s+(?:version\s+)?([0-9]+\.[0-9]+\.[0-9]+)");
        string source = DetermineSource(found);
        return new RuntimeInfo
        {
            Name = "Composer",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = true,
            Status = source.StartsWith("Internal") ? "Installed (Bundled)" : "Installed (External)",
            Source = source
        };
    }

    public async Task<RuntimeInfo?> DetectGitAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\git
        var lokaGit = Path.Combine(binPath, "git");
        if (Directory.Exists(lokaGit))
        {
            var cmd = Path.Combine(lokaGit, "cmd", "git.exe");
            if (File.Exists(cmd)) candidates.Add(cmd);
            var bin = Path.Combine(lokaGit, "bin", "git.exe");
            if (File.Exists(bin)) candidates.Add(bin);
            var exe = Path.Combine(lokaGit, "git.exe");
            if (File.Exists(exe)) candidates.Add(exe);
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var gitPath = FindExecutableInPath("git.exe");
            if (!string.IsNullOrEmpty(gitPath)) candidates.Add(gitPath);

            var common = @"C:\Program Files\Git\cmd\git.exe";
            if (File.Exists(common)) candidates.Add(common);
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "Git", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "--version", @"git\s+version\s+([0-9]+\.[0-9]+\.[0-9]+)");
        string source = DetermineSource(found);
        return new RuntimeInfo
        {
            Name = "Git",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = true,
            Status = source.StartsWith("Internal") ? "Installed (Bundled)" : "Installed (External)",
            Source = source
        };
    }

    public async Task<RuntimeInfo?> DetectApacheAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\apache
        var lokaApache = Path.Combine(binPath, "apache");
        if (Directory.Exists(lokaApache))
        {
            var direct = Path.Combine(lokaApache, "bin", "httpd.exe");
            if (File.Exists(direct)) candidates.Add(direct);
            try { candidates.AddRange(Directory.GetFiles(lokaApache, "httpd.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var fromPath = FindExecutableInPath("httpd.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidates.Add(fromPath);

            candidates.Add(@"C:\Apache24\bin\httpd.exe");
            candidates.Add(@"C:\xampp\apache\bin\httpd.exe");
            candidates.Add(@"C:\laragon\bin\apache\httpd-2.4.54-win64-VS16\bin\httpd.exe");

            if (Directory.Exists(@"C:\laragon\bin\apache"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\laragon\bin\apache", "httpd.exe", SearchOption.AllDirectories)); } catch { }
            }
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "Apache", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "-v", @"Apache/([0-9]+\.[0-9]+\.[0-9]+)");
        bool isRunning = Process.GetProcessesByName("httpd").Any();
        return new RuntimeInfo
        {
            Name = "Apache",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = isRunning,
            Status = isRunning ? "Running" : "Stopped",
            Source = DetermineSource(found)
        };
    }

    public async Task<RuntimeInfo?> DetectMySqlAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\mysql
        var lokaMySql = Path.Combine(binPath, "mysql");
        if (Directory.Exists(lokaMySql))
        {
            var direct = Path.Combine(lokaMySql, "bin", "mysqld.exe");
            if (File.Exists(direct)) candidates.Add(direct);
            try { candidates.AddRange(Directory.GetFiles(lokaMySql, "mysqld.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var fromPath = FindExecutableInPath("mysqld.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidates.Add(fromPath);

            candidates.Add(@"C:\Program Files\MySQL\MySQL Server 8.4\bin\mysqld.exe");
            candidates.Add(@"C:\Program Files\MySQL\MySQL Server 8.0\bin\mysqld.exe");
            candidates.Add(@"C:\xampp\mysql\bin\mysqld.exe");
            if (Directory.Exists(@"C:\laragon\bin\mysql"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\laragon\bin\mysql", "mysqld.exe", SearchOption.AllDirectories)); } catch { }
            }
        }

        string? found = null;
        string version = "Unknown";
        foreach (var cand in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            var (ver, outStr) = await GetExecutableVersionAsync(cand, "--version", @"(?:Ver|mysqld)\s+([0-9]+\.[0-9]+\.[0-9]+)");
            if (outStr.Contains("MariaDB", StringComparison.OrdinalIgnoreCase))
            {
                continue; // Skip MariaDB binary
            }

            found = cand;
            version = ver;
            break;
        }

        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "MySQL", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        bool isRunning = IsServiceProcessRunning("mysqld", excludedPathSubstring: "mariadb");
        return new RuntimeInfo
        {
            Name = "MySQL",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = isRunning,
            Status = isRunning ? "Running" : "Stopped",
            Source = DetermineSource(found)
        };
    }

    public async Task<RuntimeInfo?> DetectMariaDbAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\mariadb
        var lokaMaria = Path.Combine(binPath, "mariadb");
        if (Directory.Exists(lokaMaria))
        {
            var directD = Path.Combine(lokaMaria, "bin", "mariadbd.exe");
            if (File.Exists(directD)) candidates.Add(directD);
            var directM = Path.Combine(lokaMaria, "bin", "mysqld.exe");
            if (File.Exists(directM)) candidates.Add(directM);
            try { candidates.AddRange(Directory.GetFiles(lokaMaria, "mariadbd.exe", SearchOption.AllDirectories)); } catch { }
            try { candidates.AddRange(Directory.GetFiles(lokaMaria, "mysqld.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var fromPath = FindExecutableInPath("mariadbd.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidates.Add(fromPath);

            if (Directory.Exists(@"C:\Program Files\MariaDB"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\Program Files\MariaDB", "mariadbd.exe", SearchOption.AllDirectories)); } catch { }
                try { candidates.AddRange(Directory.GetFiles(@"C:\Program Files\MariaDB", "mysqld.exe", SearchOption.AllDirectories)); } catch { }
            }

            if (File.Exists(@"C:\xampp\mysql\bin\mysqld.exe"))
            {
                candidates.Add(@"C:\xampp\mysql\bin\mysqld.exe");
            }
            if (Directory.Exists(@"C:\laragon\bin\mysql"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\laragon\bin\mysql", "mysqld.exe", SearchOption.AllDirectories)); } catch { }
            }
        }

        string? found = null;
        string version = "Unknown";
        foreach (var cand in candidates.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists))
        {
            var (ver, outStr) = await GetExecutableVersionAsync(cand, "--version", @"(?:mariadbd|Ver)\s+([0-9]+\.[0-9]+\.[0-9]+)");
            bool isMaria = cand.Contains("mariadb", StringComparison.OrdinalIgnoreCase) ||
                           outStr.Contains("MariaDB", StringComparison.OrdinalIgnoreCase);

            if (isMaria)
            {
                found = cand;
                version = ver;
                break;
            }
        }

        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "MariaDB", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        bool isRunning = Process.GetProcessesByName("mariadbd").Any() || IsServiceProcessRunning("mysqld", requiredPathSubstring: "mariadb");
        return new RuntimeInfo
        {
            Name = "MariaDB",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = isRunning,
            Status = isRunning ? "Running" : "Stopped",
            Source = DetermineSource(found)
        };
    }

    public async Task<RuntimeInfo?> DetectPostgresAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\postgresql
        var lokaPg = Path.Combine(binPath, "postgresql");
        if (Directory.Exists(lokaPg))
        {
            var direct = Path.Combine(lokaPg, "bin", "postgres.exe");
            if (File.Exists(direct)) candidates.Add(direct);
            try { candidates.AddRange(Directory.GetFiles(lokaPg, "postgres.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var fromPath = FindExecutableInPath("postgres.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidates.Add(fromPath);

            if (Directory.Exists(@"C:\Program Files\PostgreSQL"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\Program Files\PostgreSQL", "postgres.exe", SearchOption.AllDirectories)); } catch { }
            }
            if (Directory.Exists(@"C:\PostgreSQL"))
            {
                try { candidates.AddRange(Directory.GetFiles(@"C:\PostgreSQL", "postgres.exe", SearchOption.AllDirectories)); } catch { }
            }
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "PostgreSQL", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "--version", @"(?:postgres\s+\(PostgreSQL\)|PostgreSQL)\s+([0-9]+(?:\.[0-9]+)+)");
        bool isRunning = Process.GetProcessesByName("postgres").Any();
        return new RuntimeInfo
        {
            Name = "PostgreSQL",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = isRunning,
            Status = isRunning ? "Running" : "Stopped",
            Source = DetermineSource(found)
        };
    }

    public async Task<RuntimeInfo?> DetectNginxAsync()
    {
        var binPath = _configService.Config.Paths.BinPath;
        var candidates = new List<string>();

        // 1. Internal C:\Loka\bin\nginx
        var lokaNginx = Path.Combine(binPath, "nginx");
        if (Directory.Exists(lokaNginx))
        {
            var direct = Path.Combine(lokaNginx, "nginx.exe");
            if (File.Exists(direct)) candidates.Add(direct);
            try { candidates.AddRange(Directory.GetFiles(lokaNginx, "nginx.exe", SearchOption.AllDirectories)); } catch { }
        }

        // 2. Fallback
        if (!candidates.Any(File.Exists) || _configService.Config.Paths.AllowExternalRuntimes)
        {
            var fromPath = FindExecutableInPath("nginx.exe");
            if (!string.IsNullOrEmpty(fromPath)) candidates.Add(fromPath);

            candidates.Add(@"C:\Program Files\Herd\resources\app.asar.unpacked\resources\bin\nginx\nginx.exe");
            candidates.Add(@"C:\nginx\nginx.exe");
            candidates.Add(@"C:\laragon\bin\nginx\nginx.exe");
            candidates.Add(@"C:\tools\nginx\nginx.exe");
        }

        var found = candidates.Distinct(StringComparer.OrdinalIgnoreCase).FirstOrDefault(File.Exists);
        if (string.IsNullOrEmpty(found))
        {
            return new RuntimeInfo { Name = "Nginx", IsInstalled = false, Status = "Available to install", CanInstall = true };
        }

        var (version, _) = await GetExecutableVersionAsync(found, "-v", @"nginx/([0-9]+\.[0-9]+\.[0-9]+)");
        bool isRunning = Process.GetProcessesByName("nginx").Any();
        return new RuntimeInfo
        {
            Name = "Nginx",
            Version = version,
            ExecutablePath = found,
            DirectoryPath = Path.GetDirectoryName(found) ?? "",
            IsInstalled = true,
            IsActive = isRunning,
            Status = isRunning ? "Running" : "Stopped",
            Source = DetermineSource(found)
        };
    }

    private static bool IsServiceProcessRunning(string processName, string? requiredPathSubstring = null, string? excludedPathSubstring = null)
    {
        try
        {
            var procs = Process.GetProcessesByName(processName);
            if (!procs.Any()) return false;
            if (string.IsNullOrEmpty(requiredPathSubstring) && string.IsNullOrEmpty(excludedPathSubstring)) return true;

            foreach (var p in procs)
            {
                try
                {
                    var mod = p.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(mod))
                    {
                        if (!string.IsNullOrEmpty(excludedPathSubstring) && mod.Contains(excludedPathSubstring, StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (!string.IsNullOrEmpty(requiredPathSubstring) && !mod.Contains(requiredPathSubstring, StringComparison.OrdinalIgnoreCase))
                            continue;
                        return true;
                    }
                }
                catch
                {
                    // Access denied or architecture mismatch
                }
            }
            return string.IsNullOrEmpty(excludedPathSubstring);
        }
        catch
        {
            return false;
        }
    }

    private static string? FindExecutableInPath(string exeName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in paths)
        {
            try
            {
                var full = Path.Combine(p, exeName);
                if (File.Exists(full)) return full;
            }
            catch { }
        }

        return null;
    }

    private static string DetermineSource(string path)
    {
        if (path.Contains(@"\Loka\bin\", StringComparison.OrdinalIgnoreCase)) return "Internal (Loka)";
        if (path.Contains(@"\Loka\", StringComparison.OrdinalIgnoreCase)) return "Internal (Loka)";
        if (path.Contains(@"\laragon\", StringComparison.OrdinalIgnoreCase)) return "External (Laragon)";
        if (path.Contains(@"\xampp\", StringComparison.OrdinalIgnoreCase)) return "External (XAMPP)";
        if (path.Contains(@"\herd", StringComparison.OrdinalIgnoreCase)) return "External (Herd)";
        return "External (System)";
    }

    private async Task<(string Version, string Output)> GetExecutableVersionAsync(string executable, string args, string pattern)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var stdoutTask = proc.StandardOutput.ReadToEndAsync();
                var stderrTask = proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync();

                var output = (await stdoutTask) + " " + (await stderrTask);
                var match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return (match.Groups[1].Value.Trim(), output);
                }

                return (output.Trim().Split('\n').FirstOrDefault()?.Trim() ?? "Detected", output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Detector", $"Failed to get version from '{executable}'", ex);
        }

        return ("Unknown", "");
    }
}
