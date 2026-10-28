using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Infrastructure.Diagnostics;

public class DiagnosticsService : IDiagnosticsService
{
    private readonly IRuntimeDetector _detector;
    private readonly IPortService _portService;
    private readonly IApacheService _apacheService;
    private readonly IConfigurationService _configService;
    private readonly ISslCertificateService _sslService;
    private readonly ILogService _logger;

    public DiagnosticsService(
        IRuntimeDetector detector,
        IPortService portService,
        IApacheService apacheService,
        IConfigurationService configService,
        ISslCertificateService sslService,
        ILogService logger)
    {
        _detector = detector;
        _portService = portService;
        _apacheService = apacheService;
        _configService = configService;
        _sslService = sslService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<DiagnosticItem>> RunDiagnosticsAsync()
    {
        var items = new List<DiagnosticItem>();

        // 1. Environment (Section 43: Environment ✓ Ready)
        bool envReady = false;
        string envDetails = "";
        try
        {
            _configService.EnsureDirectories();
            var testFile = Path.Combine(_configService.Config.Paths.TmpPath, "write_test.tmp");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            envReady = true;
            envDetails = $"Workspace ready at '{_configService.Config.Paths.RootPath}' (Writable)";
        }
        catch (Exception ex)
        {
            envDetails = $"Workspace error: {ex.Message}";
        }

        items.Add(new DiagnosticItem
        {
            Title = "Environment",
            Description = "Self-contained Loka folder hierarchy and filesystem permissions.",
            IsPassed = envReady,
            Details = envDetails,
            RemediationSuggestion = envReady ? null : "Verify folder permissions or choose a different path in Settings."
        });

        // 2. Apache (Section 43: Apache ✓ Ready)
        var apache = await _detector.DetectApacheAsync();
        bool apacheOk = apache != null && apache.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "Apache",
            Description = "Apache HTTP Server binary and configuration check.",
            IsPassed = apacheOk,
            Details = apacheOk ? $"Apache {apache!.Version} ({apache.ExecutablePath})" : "Apache binary not installed in Loka.",
            RemediationSuggestion = apacheOk ? null : "Install Apache into C:\\Loka\\bin\\apache or use First Run Setup."
        });

        // 3. MySQL (Section 43: MySQL ✓ Ready)
        var mysql = await _detector.DetectMySqlAsync();
        bool mysqlOk = mysql != null && mysql.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "MySQL",
            Description = "MySQL Community Server engine daemon.",
            IsPassed = mysqlOk,
            Details = mysqlOk ? $"MySQL {mysql!.Version} ({mysql.ExecutablePath})" : "MySQL binary not installed in Loka.",
            RemediationSuggestion = mysqlOk ? null : "Install MySQL into C:\\Loka\\bin\\mysql or configure in Settings."
        });

        // 4. MariaDB (Section 43: MariaDB ✓ Ready)
        var mariadb = await _detector.DetectMariaDbAsync();
        bool mariaOk = mariadb != null && mariadb.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "MariaDB",
            Description = "MariaDB relational database management engine.",
            IsPassed = mariaOk,
            Details = mariaOk ? $"MariaDB {mariadb!.Version} ({mariadb.ExecutablePath})" : "MariaDB binary not installed in Loka.",
            RemediationSuggestion = mariaOk ? null : "MariaDB is optional. Install into C:\\Loka\\bin\\mariadb if needed."
        });

        // 5. PostgreSQL (Section 43: PostgreSQL ✓ Ready)
        var postgres = await _detector.DetectPostgresAsync();
        bool pgOk = postgres != null && postgres.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "PostgreSQL",
            Description = "PostgreSQL enterprise database engine daemon.",
            IsPassed = pgOk,
            Details = pgOk ? $"PostgreSQL {postgres!.Version} ({postgres.ExecutablePath})" : "PostgreSQL binary not installed in Loka.",
            RemediationSuggestion = pgOk ? null : "PostgreSQL is optional. Install into C:\\Loka\\bin\\postgresql if needed."
        });

        // 6. PHP (Section 43: PHP ✓ Ready)
        var php = await _detector.DetectPhpAsync();
        bool phpOk = php != null && php.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "PHP",
            Description = "PHP runtime executable for web services and CLI tooling.",
            IsPassed = phpOk,
            Details = phpOk ? $"PHP {php!.Version} (Active: {_configService.Config.Php.ActiveVersion}, Path: {php.ExecutablePath})" : "PHP runtime not found.",
            RemediationSuggestion = phpOk ? null : "Install PHP 8.3, 8.4, or 8.5 into C:\\Loka\\bin\\php."
        });

        // 7. Node.js (Section 43: Node.js ✓ Ready)
        var node = await _detector.DetectNodeAsync();
        bool nodeOk = node != null && node.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "Node.js",
            Description = "Node.js JavaScript runtime for frontend build pipelines.",
            IsPassed = nodeOk,
            Details = nodeOk ? $"Node.js {node!.Version} ({node.ExecutablePath})" : "Node.js engine not installed.",
            RemediationSuggestion = nodeOk ? null : "Install Node.js 22.x into C:\\Loka\\bin\\nodejs."
        });

        // 8. Composer (Section 43: Composer ✓ Ready)
        var composer = await _detector.DetectComposerAsync();
        bool composerOk = composer != null && composer.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "Composer",
            Description = "Composer dependency manager for PHP projects.",
            IsPassed = composerOk,
            Details = composerOk ? $"Composer {composer!.Version} ({composer.ExecutablePath})" : "Composer not found.",
            RemediationSuggestion = composerOk ? null : "Install composer.bat/phar into C:\\Loka\\bin\\composer."
        });

        // 9. Git (Section 43: Git ✓ Ready)
        var git = await _detector.DetectGitAsync();
        bool gitOk = git != null && git.IsInstalled;
        items.Add(new DiagnosticItem
        {
            Title = "Git",
            Description = "Git distributed version control system.",
            IsPassed = gitOk,
            Details = gitOk ? $"Git {git!.Version} ({git.ExecutablePath})" : "Git not found.",
            RemediationSuggestion = gitOk ? null : "Install Git into C:\\Loka\\bin\\git."
        });

        // 10. phpMyAdmin (Section 43: phpMyAdmin ✓ Ready)
        bool pmaOk = false;
        string pmaDetails = "";
        var pmaDir = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin");
        var pmaToolsDir = Path.Combine(_configService.Config.Paths.RootPath, "tools", "phpmyadmin");
        if (Directory.Exists(pmaDir) || Directory.Exists(pmaToolsDir) || !string.IsNullOrWhiteSpace(_configService.Config.Database.PhpMyAdminUrl))
        {
            pmaOk = true;
            pmaDetails = $"Available at {_configService.Config.Database.PhpMyAdminUrl}";
        }
        else
        {
            pmaDetails = "phpMyAdmin directory not found in C:\\Loka\\etc\\phpmyadmin.";
        }

        items.Add(new DiagnosticItem
        {
            Title = "phpMyAdmin",
            Description = "Browser-based MySQL/MariaDB database administration tool.",
            IsPassed = pmaOk,
            Details = pmaDetails,
            RemediationSuggestion = pmaOk ? null : "Extract phpMyAdmin to C:\\Loka\\etc\\phpmyadmin."
        });

        // 11. SSL Root Certificate Authority
        var sslDir = Path.Combine(_configService.Config.Paths.ConfigPath, "ssl");
        bool sslTrusted = _sslService.IsRootCertificateTrusted(sslDir);
        items.Add(new DiagnosticItem
        {
            Title = "SSL Root CA",
            Description = "Loka Development Root CA trusted in Windows Certificate Store.",
            IsPassed = sslTrusted,
            Details = sslTrusted
                ? "Loka Development Root CA is trusted in Windows (HTTPS secure)."
                : "Root CA is not trusted yet in Windows store.",
            RemediationSuggestion = sslTrusted
                ? null
                : "Run 'loka trust' in terminal or click 'Trust Root CA' in Settings."
        });

        return items;
    }
}
