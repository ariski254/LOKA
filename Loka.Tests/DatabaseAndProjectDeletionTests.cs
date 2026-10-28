using System.Diagnostics;
using System.IO;
using System.Text;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Loka.Database;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Logging;
using Loka.Project;
using Xunit;

namespace Loka.Tests;

public class DatabaseAndProjectDeletionTests : IDisposable
{
    private readonly string _testSandbox;
    private readonly LogService _logger;
    private readonly ConfigurationService _configService;

    public DatabaseAndProjectDeletionTests()
    {
        _testSandbox = Path.Combine(Path.GetTempPath(), "loka_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testSandbox);

        _logger = new LogService();
        _configService = new ConfigurationService(_logger);

        // Point config paths to sandbox for isolation
        _configService.Config.Paths.RootPath = Path.Combine(_testSandbox, "Loka");
        _configService.Config.Paths.ProjectsPath = Path.Combine(_testSandbox, "Loka", "projects");
        _configService.Config.Paths.ConfigPath = Path.Combine(_testSandbox, "Loka", "config");
        _configService.Config.Paths.EtcPath = Path.Combine(_testSandbox, "Loka", "etc");
        _configService.Config.Paths.BinPath = Path.Combine(_testSandbox, "Loka", "bin");
        _configService.Config.Paths.DataPath = Path.Combine(_testSandbox, "Loka", "data");
        _configService.Config.Paths.LogsPath = Path.Combine(_testSandbox, "Loka", "logs");

        Directory.CreateDirectory(_configService.Config.Paths.ProjectsPath);
        Directory.CreateDirectory(Path.Combine(_configService.Config.Paths.ConfigPath, "apache", "sites-enabled"));
        Directory.CreateDirectory(Path.Combine(_configService.Config.Paths.ConfigPath, "nginx", "sites-enabled"));
        Directory.CreateDirectory(Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin"));
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testSandbox))
            {
                // Clear any read-only attributes before deleting sandbox
                foreach (var file in Directory.EnumerateFiles(_testSandbox, "*", SearchOption.AllDirectories))
                {
                    try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
                }
                Directory.Delete(_testSandbox, recursive: true);
            }
        }
        catch { }
    }

    private ProjectService CreateProjectService()
    {
        var mockApache = new MockApacheService();
        var mockNginx = new MockNginxService();
        var mockHosts = new MockHostsService();
        var mockSsl = new MockSslCertificateService();
        var mockProcess = new MockProcessManager();

        return new ProjectService(
            _configService,
            mockApache,
            mockNginx,
            mockHosts,
            mockSsl,
            _logger,
            mockProcess);
    }

    private DatabaseService CreateDatabaseService(IPortService? portService = null, IProcessManager? processManager = null)
    {
        var detector = new MockRuntimeDetector();
        var pm = processManager ?? new MockProcessManager();
        return new DatabaseService(_configService, detector, pm, _logger, portService);
    }

    #region BUG 1: PROJECT DELETION TESTS (Scenarios 1-4)

    [Fact]
    public async Task Test01_DeleteProjectAndFiles_PermanentlyDeletesPhysicalFolder()
    {
        // 1. Delete Project and Files menghapus folder fisik
        var ps = CreateProjectService();
        var projectDir = Path.Combine(_configService.Config.Paths.ProjectsPath, "my-app");
        Directory.CreateDirectory(projectDir);
        var subDir = Path.Combine(projectDir, "src");
        Directory.CreateDirectory(subDir);
        var testFile = Path.Combine(subDir, "index.php");
        await File.WriteAllTextAsync(testFile, "<?php echo 'hello';");

        // Make one file readonly to test attribute clearing
        var readOnlyFile = Path.Combine(projectDir, "README.md");
        await File.WriteAllTextAsync(readOnlyFile, "# My App");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        var project = new ProjectInfo
        {
            Name = "my-app",
            Path = projectDir,
            Domain = "my-app.test"
        };

        var result = await ps.DeleteProjectAsync(project, deleteFiles: true);

        Assert.True(result);
        Assert.False(Directory.Exists(projectDir), "Project folder must be permanently deleted from disk.");
        Assert.False(File.Exists(testFile));
        Assert.False(File.Exists(readOnlyFile));
    }

    [Fact]
    public async Task Test02_RemoveFromLoka_KeepsPhysicalFolderIntact()
    {
        // 2. Remove from Loka tidak menghapus folder
        var ps = CreateProjectService();
        var projectDir = Path.Combine(_configService.Config.Paths.ProjectsPath, "safe-project");
        Directory.CreateDirectory(projectDir);
        var codeFile = Path.Combine(projectDir, "app.js");
        await File.WriteAllTextAsync(codeFile, "console.log('safe');");

        var project = new ProjectInfo
        {
            Name = "safe-project",
            Path = projectDir,
            Domain = "safe-project.test"
        };

        var result = await ps.DeleteProjectAsync(project, deleteFiles: false);

        Assert.True(result);
        Assert.True(Directory.Exists(projectDir), "Physical directory must remain 100% intact.");
        Assert.True(File.Exists(codeFile), "Code files must remain 100% intact.");
        Assert.True(File.Exists(Path.Combine(projectDir, ".loka-ignore")), ".loka-ignore marker must be written.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Program Files")]
    public void Test03_PathValidation_RejectsDangerousPaths(string dangerousPath)
    {
        // 3. Path berbahaya ditolak
        var ps = CreateProjectService();
        var isValid = ps.ValidateProjectPathForDeletion(dangerousPath, out var error);

        Assert.False(isValid, $"Path '{dangerousPath}' should be rejected as dangerous.");
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void Test03b_PathValidation_RejectsLokaCoreAndProjectsRoot()
    {
        var ps = CreateProjectService();

        // Projects root directory itself must never be deleted
        var isProjectsRootValid = ps.ValidateProjectPathForDeletion(_configService.Config.Paths.ProjectsPath, out var err1);
        Assert.False(isProjectsRootValid);
        Assert.Contains("Security violation", err1);

        // Loka Root directory itself must never be deleted
        var isRootValid = ps.ValidateProjectPathForDeletion(_configService.Config.Paths.RootPath, out var err2);
        Assert.False(isRootValid);
        Assert.Contains("Security violation", err2);
    }

    [Fact]
    public async Task Test04_LockedFile_HandlesDeletionFailureSafely()
    {
        // 4. Penghapusan gagal karena permission atau file terkunci
        var ps = CreateProjectService();
        var projectDir = Path.Combine(_configService.Config.Paths.ProjectsPath, "locked-project");
        Directory.CreateDirectory(projectDir);
        var lockedFile = Path.Combine(projectDir, "locked.db");

        // Hold an exclusive open file handle
        using var fs = new FileStream(lockedFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        fs.Write(new byte[] { 1, 2, 3 });
        fs.Flush();

        var project = new ProjectInfo
        {
            Name = "locked-project",
            Path = projectDir,
            Domain = "locked-project.test"
        };

        var result = await ps.DeleteProjectAsync(project, deleteFiles: true);

        // Must report false and NOT false success
        Assert.False(result);
        Assert.True(Directory.Exists(projectDir), "Directory must remain when deletion fails.");
    }

    #endregion

    #region BUG 2: CURRENT SERVER & PHPMYADMIN TESTS (Scenarios 5-15)

    [Fact]
    public async Task Test05_Condition1_OnlyMySqlRunning_ResolvesMySql()
    {
        // 5. Hanya MySQL berjalan
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1234 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "1\n1");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);

        var resolution = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MySQL", resolution.ActiveServerName);
        Assert.Equal(3306, resolution.ActivePort);
        Assert.Equal(1, resolution.PhpMyAdminServerIndex);
        Assert.True(resolution.MySqlStatus.IsHealthy);
        Assert.False(resolution.MariaDbStatus.IsHealthy);

        // Verify config.inc.php
        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        Assert.True(File.Exists(pmaConfig));
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 1;", content);
    }

    [Fact]
    public async Task Test06_Condition2_OnlyMariaDbRunning_ResolvesMariaDb_NoFallbackToStoppedMySql()
    {
        // 6. Hanya MariaDB berjalan
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 5678 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "1\n1");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);

        var resolution = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MariaDB", resolution.ActiveServerName);
        Assert.Equal(3307, resolution.ActivePort);
        Assert.Equal(2, resolution.PhpMyAdminServerIndex);
        Assert.False(resolution.MySqlStatus.IsHealthy);
        Assert.True(resolution.MariaDbStatus.IsHealthy);

        // Verify config.inc.php directs to MariaDB
        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        Assert.True(File.Exists(pmaConfig));
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 2;", content);
        Assert.DoesNotContain("$cfg['ServerDefault'] = 1;", content);
    }

    [Fact]
    public async Task Test07_Condition3_BothRunning_RespectsPreferredChoice()
    {
        // 7. MySQL dan MariaDB berjalan bersamaan
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1111 };
        mockPortService.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 2222 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "1\n1");
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "1\n1");

        _configService.Config.Services.PreferredDatabase = "MariaDB";
        var dbService = CreateDatabaseService(mockPortService, mockProcess);

        var resolution = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MariaDB", resolution.ActiveServerName);
        Assert.Equal(2, resolution.PhpMyAdminServerIndex);
        Assert.True(resolution.MySqlStatus.IsHealthy);
        Assert.True(resolution.MariaDbStatus.IsHealthy);

        // Verify config has ServerDefault = 2 and lists both servers
        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 2;", content);
        Assert.Contains("MySQL", content);
        Assert.Contains("MariaDB", content);
    }

    [Fact]
    public async Task Test08_CurrentServerMariaDb_PersistsAcrossReload()
    {
        // 8. Current Server MariaDB tetap benar setelah reload
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 2222 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "1\n1");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);
        await dbService.SetCurrentDatabaseServerAsync("MariaDB");

        // Simulate reload 1
        var res1 = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MariaDB", res1.ActiveServerName);
        Assert.Equal(2, res1.PhpMyAdminServerIndex);

        // Simulate reload 2
        var res2 = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MariaDB", res2.ActiveServerName);
        Assert.Equal(2, res2.PhpMyAdminServerIndex);

        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 2;", content);
    }

    [Fact]
    public async Task Test09_CurrentServerMySql_PersistsAcrossReload()
    {
        // 9. Current Server MySQL tetap benar setelah reload
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1111 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "1\n1");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);
        await dbService.SetCurrentDatabaseServerAsync("MySQL");

        // Simulate reload
        var res = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MySQL", res.ActiveServerName);
        Assert.Equal(1, res.PhpMyAdminServerIndex);

        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 1;", content);
    }

    [Fact]
    public async Task Test10_Condition5_ActiveServerStops_FallsBackToAlternativeHealthyServer()
    {
        // 10. Database berhenti ketika sedang dipilih (Fallback test)
        var mockPortService = new MockPortService();
        // MySQL is stopped, but MariaDB is running
        mockPortService.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 3333 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "1\n1");

        // User preference was MySQL
        _configService.Config.Services.PreferredDatabase = "MySQL";

        var dbService = CreateDatabaseService(mockPortService, mockProcess);
        var resolution = await dbService.ResolveCurrentDatabaseServerAsync();

        // Must automatically fall back to MariaDB
        Assert.Equal("MariaDB", resolution.ActiveServerName);
        Assert.Equal(2, resolution.PhpMyAdminServerIndex);
        Assert.Contains("Fallback", resolution.Message);
    }

    [Fact]
    public async Task Test11_DynamicCustomPorts_RespectedInConfig()
    {
        // 11. Port konfigurasi berbeda dari port default
        _configService.Config.Services.MySqlPort = 3316;
        _configService.Config.Services.MariaDbPort = 3317;

        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3316] = new PortInfo { Port = 3316, IsOccupied = true, ProcessName = "mysqld", ProcessId = 4444 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3316", "1\n1");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);
        var res = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal(3316, res.ActivePort);

        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("'port'] = '3316';", content);
        Assert.Contains("'port'] = '3317';", content);
    }

    [Fact]
    public async Task Test12_ForeignProcessOnDatabasePort_IsNotMarkedAsDatabase()
    {
        // 12. Port dipakai oleh proses yang bukan database
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "python.exe", ProcessId = 9999 };

        var dbService = CreateDatabaseService(mockPortService);
        var probe = await dbService.ProbeDatabaseServerAsync("MySQL");

        Assert.False(probe.IsHealthy);
        Assert.Contains("busy (python.exe)", probe.StatusText);
    }

    [Fact]
    public async Task Test13_InvalidCredentials_ReportsAuthFailed()
    {
        // 13. Kredensial salah dan koneksi ditolak
        var mockPortService = new MockPortService();
        mockPortService.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1234 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterFailure("mysql.exe", "-h 127.0.0.1 -P 3306", "ERROR 1045 (28000): Access denied for user 'root'@'localhost' (using password: NO)");

        var dbService = CreateDatabaseService(mockPortService, mockProcess);
        var probe = await dbService.ProbeDatabaseServerAsync("MySQL");

        Assert.Equal("Authentication Failed", probe.StatusText);
        Assert.False(probe.IsConnectionSuccessful);
    }

    [Fact]
    public async Task Test14_PhpMyAdminConfig_IsValidSyntaxAndStructure()
    {
        // 14. Konfigurasi phpMyAdmin valid setelah diperbarui
        var dbService = CreateDatabaseService();
        var success = await dbService.SyncPhpMyAdminConfigAsync("MySQL");

        Assert.True(success);
        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);

        Assert.StartsWith("<?php", content.TrimStart());
        Assert.Contains("$cfg['blowfish_secret']", content);
        Assert.Contains("$cfg['ServerDefault'] = 1;", content);
        Assert.Contains("$cfg['Servers'][$i]['auth_type'] = 'config';", content);
        Assert.Contains("$cfg['Servers'][$i]['connect_type'] = 'tcp';", content);
    }

    [Fact]
    public async Task Test15_AtomicConfigReplacement_PreservesBackup()
    {
        // 15. Perubahan konfigurasi gagal dan pemulihan berjalan dengan aman
        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        await File.WriteAllTextAsync(pmaConfig, "<?php /* custom user config */ ?>");

        var dbService = CreateDatabaseService();
        await dbService.SyncPhpMyAdminConfigAsync("MariaDB");

        // .bak file must be created to protect user settings
        var backupFile = pmaConfig + ".bak";
        Assert.True(File.Exists(backupFile));
        var backupContent = await File.ReadAllTextAsync(backupFile);
        Assert.Contains("custom user config", backupContent);

        // active config updated atomically
        var activeContent = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("$cfg['ServerDefault'] = 2;", activeContent);
    }

    #endregion

    #region AGENT.MD SECTION 6: MANDATORY DATABASE DETECTION TESTS (Scenarios 1-9)

    [Fact]
    public async Task AgentScenario1_MySqlAlone_DetectedAsMySql_WithVendorIdentity()
    {
        // 1. MySQL berjalan sendiri -> terdeteksi sebagai MySQL
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1888 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "ver\tcomment\n8.4.0\tMySQL Community Server - GPL");

        var dbService = CreateDatabaseService(mockPort, mockProcess);
        var res = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MySQL", res.ActiveServerName);
        Assert.Equal(3306, res.ActivePort);
        Assert.Equal(1, res.PhpMyAdminServerIndex);
        Assert.True(res.MySqlStatus.IsHealthy);
        Assert.Equal("MySQL", res.MySqlStatus.DetectedVendor);
        Assert.True(res.MySqlStatus.IsVerifiedVendor);
        Assert.Contains("8.4.0", res.MySqlStatus.Version);
        Assert.False(res.MariaDbStatus.IsHealthy);
        Assert.Equal("Stopped", res.MariaDbStatus.StatusText);
    }

    [Fact]
    public async Task AgentScenario2_MariaDbAlone_DetectedAsMariaDb_WithVendorIdentity()
    {
        // 2. MariaDB berjalan sendiri -> terdeteksi sebagai MariaDB
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 2888 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "ver\tcomment\n11.8.9-MariaDB\tMariaDB Server");

        var dbService = CreateDatabaseService(mockPort, mockProcess);
        var res = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MariaDB", res.ActiveServerName);
        Assert.Equal(3307, res.ActivePort);
        Assert.Equal(2, res.PhpMyAdminServerIndex);
        Assert.False(res.MySqlStatus.IsHealthy);
        Assert.True(res.MariaDbStatus.IsHealthy);
        Assert.Equal("MariaDB", res.MariaDbStatus.DetectedVendor);
        Assert.True(res.MariaDbStatus.IsVerifiedVendor);
        Assert.Contains("11.8.9-MariaDB", res.MariaDbStatus.Version);
    }

    [Fact]
    public async Task AgentScenario3_BothRunning_MappingCorrect_And_RespectsUserChoice()
    {
        // 3. Keduanya berjalan -> mapping masing-masing tetap benar
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1100 };
        mockPort.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 2200 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "ver\tcomment\n8.4.0\tMySQL Community Server - GPL");
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "ver\tcomment\n11.8.9-MariaDB\tMariaDB Server");

        var dbService = CreateDatabaseService(mockPort, mockProcess);

        // User sets MySQL
        await dbService.SetCurrentDatabaseServerAsync("MySQL");
        var resMySql = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MySQL", resMySql.ActiveServerName);
        Assert.Equal(3306, resMySql.ActivePort);
        Assert.Equal(1, resMySql.PhpMyAdminServerIndex);
        Assert.True(resMySql.MySqlStatus.IsHealthy);
        Assert.True(resMySql.MariaDbStatus.IsHealthy);

        // User switches to MariaDB
        await dbService.SetCurrentDatabaseServerAsync("MariaDB");
        var resMaria = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MariaDB", resMaria.ActiveServerName);
        Assert.Equal(3307, resMaria.ActivePort);
        Assert.Equal(2, resMaria.PhpMyAdminServerIndex);
    }

    [Fact]
    public async Task AgentScenario4_MySqlOnNonDefaultPort_CorrectlyDetected()
    {
        // 4. MySQL berjalan pada port non-default -> tetap terdeteksi benar
        _configService.Config.Services.MySqlPort = 3316;

        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3316] = new PortInfo { Port = 3316, IsOccupied = true, ProcessName = "mysqld", ProcessId = 4400 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3316", "ver\tcomment\n8.4.4\tMySQL Community Server");

        var dbService = CreateDatabaseService(mockPort, mockProcess);
        var res = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MySQL", res.ActiveServerName);
        Assert.Equal(3316, res.ActivePort);
        Assert.Equal(1, res.PhpMyAdminServerIndex);
        Assert.True(res.MySqlStatus.IsHealthy);

        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("'port'] = '3316';", content);
        Assert.Contains("$cfg['ServerDefault'] = 1;", content);
    }

    [Fact]
    public async Task AgentScenario5_MariaDbOnNonDefaultPort_CorrectlyDetected()
    {
        // 5. MariaDB berjalan pada port non-default -> tetap terdeteksi benar
        _configService.Config.Services.MariaDbPort = 3317;

        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3317] = new PortInfo { Port = 3317, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 5500 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3317", "ver\tcomment\n11.4.2-MariaDB\tMariaDB Server");

        var dbService = CreateDatabaseService(mockPort, mockProcess);
        var res = await dbService.ResolveCurrentDatabaseServerAsync();

        Assert.Equal("MariaDB", res.ActiveServerName);
        Assert.Equal(3317, res.ActivePort);
        Assert.Equal(2, res.PhpMyAdminServerIndex);
        Assert.True(res.MariaDbStatus.IsHealthy);

        var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
        var content = await File.ReadAllTextAsync(pmaConfig);
        Assert.Contains("'port'] = '3317';", content);
        Assert.Contains("$cfg['ServerDefault'] = 2;", content);
    }

    [Fact]
    public async Task AgentScenario6_ServiceStops_StatusUpdatedAndFallbackOrUnavailable()
    {
        // 6. Service berhenti -> status diperbarui dengan benar
        var mockPort = new MockPortService();
        var mockProcess = new MockProcessManager();

        var dbService = CreateDatabaseService(mockPort, mockProcess);

        // Neither is running
        var resStopped = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Null(resStopped.ActiveServerName);
        Assert.Equal(0, resStopped.ActivePort);
        Assert.Contains("Not available", resStopped.Message);
        Assert.False(resStopped.MySqlStatus.IsHealthy);
        Assert.False(resStopped.MariaDbStatus.IsHealthy);

        // Start MySQL
        mockPort.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1234 };
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "ver\tcomment\n8.4.0\tMySQL");
        var resMySqlRunning = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MySQL", resMySqlRunning.ActiveServerName);

        // Stop MySQL and start MariaDB (Simulate Stop + Fallback)
        mockPort.OccupiedPorts.Remove(3306);
        mockPort.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mariadbd", ProcessId = 5678 };
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "ver\tcomment\n11.8.9-MariaDB\tMariaDB");
        var resMariaFallback = await dbService.ResolveCurrentDatabaseServerAsync();
        Assert.Equal("MariaDB", resMariaFallback.ActiveServerName);
        Assert.Equal(3307, resMariaFallback.ActivePort);
        Assert.False(resMariaFallback.MySqlStatus.IsHealthy);
        Assert.True(resMariaFallback.MariaDbStatus.IsHealthy);
    }

    [Fact]
    public async Task AgentScenario7_PortOccupiedByForeignProcess_NotTreatedAsDatabase()
    {
        // 7. Port dipakai proses lain -> tidak dianggap sebagai database
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "nginx.exe", ProcessId = 7777 };

        var dbService = CreateDatabaseService(mockPort);
        var probe = await dbService.ProbeDatabaseServerAsync("MySQL");

        Assert.False(probe.IsHealthy);
        Assert.Contains("busy (nginx.exe)", probe.StatusText);
        Assert.False(probe.IsVerifiedVendor);
    }

    [Fact]
    public async Task AgentScenario8_ReloadPhpMyAdmin_EndpointsNotCrossed()
    {
        // 8. Reload phpMyAdmin -> endpoint tidak tertukar
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3306] = new PortInfo { Port = 3306, IsOccupied = true, ProcessName = "mysqld", ProcessId = 1111 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3306", "ver\tcomment\n8.4.0\tMySQL");

        var dbService = CreateDatabaseService(mockPort, mockProcess);

        // Reload loop 5 times
        for (int i = 0; i < 5; i++)
        {
            var res = await dbService.ResolveCurrentDatabaseServerAsync();
            Assert.Equal("MySQL", res.ActiveServerName);
            Assert.Equal(3306, res.ActivePort);
            Assert.Equal(1, res.PhpMyAdminServerIndex);

            var pmaConfig = Path.Combine(_configService.Config.Paths.EtcPath, "phpmyadmin", "config.inc.php");
            var content = await File.ReadAllTextAsync(pmaConfig);
            Assert.Contains("$cfg['ServerDefault'] = 1;", content);
            Assert.DoesNotContain("$cfg['ServerDefault'] = 2;", content);
        }
    }

    [Fact]
    public async Task AgentScenario9_PortVendorMismatch_RejectsWrongDatabaseIdentity()
    {
        // 9. Vendor Mismatch rejection: MariaDB port responding with MySQL banner
        var mockPort = new MockPortService();
        mockPort.OccupiedPorts[3307] = new PortInfo { Port = 3307, IsOccupied = true, ProcessName = "mysqld", ProcessId = 9999 };

        var mockProcess = new MockProcessManager();
        mockProcess.RegisterSuccess("mysql.exe", "-h 127.0.0.1 -P 3307", "ver\tcomment\n8.4.0\tMySQL Community Server - GPL");

        var dbService = CreateDatabaseService(mockPort, mockProcess);
        var probeMaria = await dbService.ProbeDatabaseServerAsync("MariaDB");

        // MariaDB probe must reject MySQL identity
        Assert.False(probeMaria.IsHealthy);
        Assert.Equal("MySQL", probeMaria.DetectedVendor);
        Assert.False(probeMaria.IsVerifiedVendor);
        Assert.Contains("Wrong Server", probeMaria.StatusText);
    }

    #endregion
}

#region Mock Services for Testing

class MockPortService : IPortService
{
    public Dictionary<int, PortInfo> OccupiedPorts { get; } = new();

    public Task<PortInfo> CheckPortAsync(int port)
    {
        if (OccupiedPorts.TryGetValue(port, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new PortInfo
        {
            Port = port,
            IsOccupied = false
        });
    }

    public Task<IReadOnlyList<PortInfo>> GetPortStatusesAsync(IEnumerable<int> ports)
    {
        var list = ports.Select(p => CheckPortAsync(p).GetAwaiter().GetResult()).ToList();
        return Task.FromResult<IReadOnlyList<PortInfo>>(list);
    }

    public Task<bool> IsPortAvailableAsync(int port) => Task.FromResult(!OccupiedPorts.ContainsKey(port));
    public Task<bool> KillProcessUsingPortAsync(int port) => Task.FromResult(true);
}

class MockProcessManager : IProcessManager
{
    private readonly Dictionary<string, ProcessExecutionResult> _responses = new();

    public void RegisterSuccess(string exe, string argsSubstr, string stdout)
    {
        _responses[exe + "|" + argsSubstr] = new ProcessExecutionResult
        {
            ExitCode = 0,
            StandardOutput = stdout,
            StandardError = ""
        };
    }

    public void RegisterFailure(string exe, string argsSubstr, string stderr)
    {
        _responses[exe + "|" + argsSubstr] = new ProcessExecutionResult
        {
            ExitCode = 1,
            StandardOutput = "",
            StandardError = stderr
        };
    }

    public Task<ProcessExecutionResult> ExecuteAsync(
        string executable,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default,
        Action<string>? onOutputLine = null)
    {
        foreach (var kvp in _responses)
        {
            var parts = kvp.Key.Split('|');
            if (executable.Contains(parts[0], StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrEmpty(parts[1]) || arguments.Contains(parts[1], StringComparison.OrdinalIgnoreCase)))
            {
                return Task.FromResult(kvp.Value);
            }
        }

        return Task.FromResult(new ProcessExecutionResult
        {
            ExitCode = 0,
            StandardOutput = "1\n1",
            StandardError = ""
        });
    }

    private readonly Dictionary<string, MockMonitoredProcess> _monitoredProcesses = new(StringComparer.OrdinalIgnoreCase);

    public Task<IMonitoredProcess> StartMonitoredProcessAsync(
        string name,
        string executable,
        string arguments,
        string? workingDirectory = null,
        IDictionary<string, string>? environmentVariables = null)
    {
        var proc = new MockMonitoredProcess
        {
            Name = name,
            ExecutablePath = executable,
            IsRunning = true,
            ProcessId = new Random().Next(1000, 9999)
        };
        _monitoredProcesses[name] = proc;
        return Task.FromResult<IMonitoredProcess>(proc);
    }

    public IMonitoredProcess? GetMonitoredProcess(string name)
    {
        return _monitoredProcesses.TryGetValue(name, out var p) ? p : null;
    }

    public Task<bool> StopMonitoredProcessAsync(string name, int timeoutMilliseconds = 5000)
    {
        if (_monitoredProcesses.TryGetValue(name, out var p))
        {
            _ = p.KillAsync();
            _monitoredProcesses.Remove(name);
            return Task.FromResult(true);
        }
        return Task.FromResult(true);
    }

    public IReadOnlyList<IMonitoredProcess> GetRunningProcesses() => _monitoredProcesses.Values.ToList();
    public event EventHandler<ProcessUnexpectedExitEventArgs>? ProcessExitedUnexpectedly;
}

public class MockMonitoredProcess : IMonitoredProcess
{
    public string Name { get; set; } = "";
    public int ProcessId { get; set; } = 1234;
    public bool IsRunning { get; set; } = true;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public string ExecutablePath { get; set; } = "";
    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public event Action<int>? Exited;

    public Task KillAsync()
    {
        IsRunning = false;
        Exited?.Invoke(0);
        return Task.CompletedTask;
    }
}

class MockRuntimeDetector : IRuntimeDetector
{
    public string MySqlExePath { get; set; } = "mysql.exe";
    public string MariaDbExePath { get; set; } = "mariadbd.exe";

    public Task<IReadOnlyList<RuntimeInfo>> DetectAllRuntimesAsync() => Task.FromResult<IReadOnlyList<RuntimeInfo>>(Array.Empty<RuntimeInfo>());
    public Task<RuntimeInfo?> DetectPhpAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectNodeAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectGitAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectComposerAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectApacheAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectMySqlAsync() => Task.FromResult<RuntimeInfo?>(new RuntimeInfo { Name = "MySQL", IsInstalled = true, ExecutablePath = MySqlExePath, DirectoryPath = Path.GetDirectoryName(MySqlExePath) ?? "C:\\Loka\\bin\\mysql\\bin" });
    public Task<RuntimeInfo?> DetectMariaDbAsync() => Task.FromResult<RuntimeInfo?>(new RuntimeInfo { Name = "MariaDB", IsInstalled = true, ExecutablePath = MariaDbExePath, DirectoryPath = Path.GetDirectoryName(MariaDbExePath) ?? "C:\\Loka\\bin\\mariadb\\bin" });
    public Task<RuntimeInfo?> DetectPostgresAsync() => Task.FromResult<RuntimeInfo?>(null);
    public Task<RuntimeInfo?> DetectNginxAsync() => Task.FromResult<RuntimeInfo?>(null);
}

class MockHostsService : IHostsService
{
    public Task<bool> HasHostEntryAsync(string domain, string ip = "127.0.0.1") => Task.FromResult(true);
    public Task<bool> AddHostEntryAsync(string domain, string ip = "127.0.0.1") => Task.FromResult(true);
    public Task<bool> RemoveHostEntryAsync(string domain) => Task.FromResult(true);
    public Task<bool> SyncHostsAsync(IEnumerable<string> domains, string ip = "127.0.0.1") => Task.FromResult(true);
    public Task<IReadOnlyList<string>> GetAllManagedHostsAsync() => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

class MockApacheService : IApacheService
{
    public Task<(bool IsValid, string Output)> ValidateConfigurationAsync() => Task.FromResult((true, "Syntax OK"));
    public Task<bool> GenerateVHostConfigAsync(ProjectInfo project) => Task.FromResult(true);
    public Task<bool> UpdatePortConfigurationAsync(int port, int sslPort) => Task.FromResult(true);
    public Task<bool> ReloadConfigurationAsync() => Task.FromResult(true);
}

class MockNginxService : INginxService
{
    public Task<(bool IsValid, string Output)> ValidateConfigurationAsync() => Task.FromResult((true, "Syntax OK"));
    public Task<bool> GenerateServerBlockAsync(ProjectInfo project) => Task.FromResult(true);
    public Task<bool> UpdatePortConfigurationAsync(int port) => Task.FromResult(true);
    public Task<bool> ReloadConfigurationAsync() => Task.FromResult(true);
}

class MockPhpService : IPhpService
{
    public Task<IReadOnlyList<string>> GetAvailablePhpVersionsAsync() => Task.FromResult<IReadOnlyList<string>>(new[] { "8.3" });
    public Task<bool> SetActivePhpVersionAsync(string version) => Task.FromResult(true);
    public Task<IReadOnlyList<PhpExtensionInfo>> GetExtensionsAsync() => Task.FromResult<IReadOnlyList<PhpExtensionInfo>>(Array.Empty<PhpExtensionInfo>());
    public Task<bool> SetExtensionEnabledAsync(string extensionName, bool enable) => Task.FromResult(true);
    public string GetActivePhpBinary() => "php.exe";
    public Task<(string VersionOutput, string IniOutput, string ModulesOutput)> GetPhpCliDetailsAsync(string? version = null) => Task.FromResult(("8.3", "", ""));
    public event Action<string>? ActivePhpVersionChanged;
}

class MockSslCertificateService : ISslCertificateService
{
    public Task<string> EnsureLocalSslCertificateAsync(string domain, string outputDir) => Task.FromResult(string.Empty);
    public Task<bool> EnsureRootCertificateAuthorityAsync(string sslDir) => Task.FromResult(true);
    public Task<bool> TrustRootCertificateAsync(string sslDir) => Task.FromResult(true);
    public bool IsRootCertificateTrusted(string sslDir) => true;
    public Task<int> RegenerateAllCertificatesAsync(string sslDir) => Task.FromResult(0);
}

#endregion
