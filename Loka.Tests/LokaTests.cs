using System.IO;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using Loka.Core.Enums;
using Loka.Core.Models;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.FileSystem;
using Loka.Infrastructure.Logging;
using Loka.Infrastructure.Networking;
using Loka.Infrastructure.Process;
using Loka.Project;
using Xunit;

namespace Loka.Tests;

public class LokaTests
{
    private readonly LogService _logger = new();

    [Fact]
    public async Task ProcessManager_ShouldExecuteEchoCommandSuccessfully()
    {
        var pm = new ProcessManager(_logger);
        var result = await pm.ExecuteAsync("cmd.exe", "/c echo LokaEngineReady");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("LokaEngineReady", result.StandardOutput);
    }

    [Fact]
    public async Task PortService_ShouldDetectHighRandomPortAsFree()
    {
        var portService = new PortService(_logger);
        // Port 59871 is practically guaranteed to be free
        var isFree = await portService.IsPortAvailableAsync(59871);

        Assert.True(isFree);
    }

    [Fact]
    public void ConfigurationService_ShouldHaveSensibleDefaults()
    {
        var configService = new ConfigurationService(_logger);
        var config = configService.Config;

        Assert.Equal(@"C:\Loka", config.Paths.RootPath);
        Assert.Equal(80, config.Services.ApachePort);
        Assert.Equal(3306, config.Services.MySqlPort);
        Assert.Equal("Light", config.General.Theme);
        Assert.Contains("curl", config.Php.EnabledExtensions);
    }

    [Fact]
    public async Task FileBackupHelper_ShouldBackupAndRollbackSuccessfully()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"loka_test_{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(tempFile, "Original Content");

        try
        {
            var backupPath = FileBackupHelper.CreateBackup(tempFile, _logger);
            Assert.True(File.Exists(backupPath));

            // Modify original file
            await File.WriteAllTextAsync(tempFile, "Modified Content");
            Assert.Equal("Modified Content", await File.ReadAllTextAsync(tempFile));

            // Rollback
            FileBackupHelper.RestoreBackup(tempFile, backupPath, _logger);
            Assert.Equal("Original Content", await File.ReadAllTextAsync(tempFile));

            // Cleanup backup
            if (File.Exists(backupPath)) File.Delete(backupPath);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void ProjectDetector_ShouldDetectLaravelFramework()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_laravel_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "public"));
        File.WriteAllText(Path.Combine(tempDir, "artisan"), "#!/usr/bin/env php");

        try
        {
            var project = ProjectDetector.Detect(tempDir);
            Assert.Equal(ProjectFramework.Laravel, project.Framework);
            Assert.EndsWith("public", project.DocumentRoot);
            Assert.Contains(".test", project.Domain);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectDetector_ShouldDetectWordPressFramework()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_wp_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "wp-config.php"), "<?php // WordPress");

        try
        {
            var project = ProjectDetector.Detect(tempDir);
            Assert.Equal(ProjectFramework.WordPress, project.Framework);
            Assert.Contains(".test", project.Domain);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectDetector_ShouldDetectNextJsFramework()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_next_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "package.json"), "{\"dependencies\": {\"next\": \"14.0.0\"}}");

        try
        {
            var project = ProjectDetector.Detect(tempDir);
            Assert.Equal(ProjectFramework.NextJs, project.Framework);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LogService_ShouldStoreAndRetrieveLogsCorrectly()
    {
        var logService = new LogService();
        logService.LogInfo("TestSource", "Test information message");
        logService.LogError("TestSource", "Test error message");

        var allLogs = logService.GetLogs("All", 100);
        var sourceLogs = logService.GetLogs("TestSource", 100);

        Assert.NotEmpty(allLogs);
        Assert.True(sourceLogs.Count >= 2);
        Assert.Contains(sourceLogs, l => l.Message == "Test information message");
        Assert.Contains(sourceLogs, l => l.Message == "Test error message" && l.Severity == LogSeverity.Error);
    }

    [Fact]
    public void ConfigurationService_EnsureDirectories_CreatesAllSubfolders()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"loka_env_{Guid.NewGuid():N}");
        var configService = new ConfigurationService(_logger);
        configService.Config.Paths.RootPath = tempRoot;
        configService.Config.Paths.ProjectsPath = Path.Combine(tempRoot, "projects");

        try
        {
            configService.EnsureDirectories();

            Assert.True(Directory.Exists(Path.Combine(tempRoot, "bin", "apache")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "bin", "php")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "bin", "mysql")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "etc", "hosts")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "data", "mysql")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "logs", "apache")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "logs", "loka")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "tmp")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "ssl")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "config")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "backups")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PortService_GetPortStatusesAsync_ReturnsValidResults()
    {
        var portService = new PortService(_logger);
        var portsToCheck = new List<int> { 59872, 59873 };
        var results = await portService.GetPortStatusesAsync(portsToCheck);

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Port == 59872);
        Assert.Contains(results, r => r.Port == 59873);
    }

    [Fact]
    public async Task RuntimeDetector_ShouldDetectSystemBinariesSafely()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Runtime.Detector.RuntimeDetector(configService, _logger);
        var runtimes = await detector.DetectAllRuntimesAsync();

        Assert.NotNull(runtimes);
        Assert.NotEmpty(runtimes);
        Assert.Contains(runtimes, r => r.Name == "PHP");
        Assert.Contains(runtimes, r => r.Name == "Node.js");
        Assert.Contains(runtimes, r => r.Name == "MySQL");
        Assert.Contains(runtimes, r => r.Name == "MariaDB");
        Assert.Contains(runtimes, r => r.Name == "PostgreSQL");
    }

    [Fact]
    public async Task RuntimeDetector_ShouldDetectInternalStackComponents_WhenInstalled()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Runtime.Detector.RuntimeDetector(configService, _logger);

        var mariadb = await detector.DetectMariaDbAsync();
        Assert.NotNull(mariadb);
        Assert.True(mariadb.IsInstalled);
        Assert.Contains(@"bin\mariadb", mariadb.ExecutablePath);

        var php = await detector.DetectPhpAsync();
        Assert.NotNull(php);
        Assert.True(php.IsInstalled);
        Assert.Contains(@"bin\php", php.ExecutablePath);

        var nginx = await detector.DetectNginxAsync();
        Assert.NotNull(nginx);
        Assert.True(nginx.IsInstalled);
        Assert.Contains(@"bin\nginx", nginx.ExecutablePath);

        var composer = await detector.DetectComposerAsync();
        Assert.NotNull(composer);
        Assert.True(composer.IsInstalled);
        Assert.Contains(@"bin\composer", composer.ExecutablePath);

        var apache = await detector.DetectApacheAsync();
        Assert.NotNull(apache);
        Assert.True(apache.IsInstalled);
        Assert.Contains(@"bin\apache", apache.ExecutablePath);

        var postgres = await detector.DetectPostgresAsync();
        Assert.NotNull(postgres);
        Assert.True(postgres.IsInstalled);
        Assert.Contains(@"bin\postgresql", postgres.ExecutablePath);
    }

    [Fact]
    public void ConfigurationService_MultiDatabasePortsAndPaths_AreIsolated()
    {
        var configService = new ConfigurationService(_logger);
        var config = configService.Config;

        Assert.Equal(3306, config.Services.MySqlPort);
        Assert.Equal(3307, config.Services.MariaDbPort);
        Assert.Equal(5432, config.Services.PostgresPort);
        Assert.Equal(80, config.Services.ApachePort);
        Assert.Equal(80, config.Services.NginxPort);

        var envPath = configService.GetInternalEnvironmentPath();
        Assert.Contains(@"bin\php", envPath);
        Assert.Contains(@"bin\mysql", envPath);
        Assert.Contains(@"bin\mariadb", envPath);
        Assert.Contains(@"bin\postgresql", envPath);
        Assert.Contains(@"bin\composer", envPath);
        Assert.Contains(@"bin\git", envPath);
    }

    [Fact]
    public async Task DatabaseService_SetRootPassword_UpdatesConfigurationAndSaves()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Runtime.Detector.RuntimeDetector(configService, _logger);
        var pm = new ProcessManager(_logger);
        var dbService = new Database.DatabaseService(configService, detector, pm, _logger);

        var testPassword = "SecretLokaPassword123!";
        var result = await dbService.SetRootPasswordAsync(testPassword);

        Assert.True(result);
        Assert.Equal(testPassword, configService.Config.Database.RootPassword);

        // Reset to empty
        await dbService.SetRootPasswordAsync("");
        Assert.Equal("", configService.Config.Database.RootPassword);
    }

    [Fact]
    public void ProjectDetector_ShouldDetectKyroFramework()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_kyro_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        Directory.CreateDirectory(Path.Combine(tempDir, "public"));
        File.WriteAllText(Path.Combine(tempDir, "kyro"), "#!/usr/bin/env php");
        File.WriteAllText(Path.Combine(tempDir, "public", "index.php"), "<?php echo 'Kyro';");

        try
        {
            var project = ProjectDetector.Detect(tempDir);
            Assert.Equal(ProjectFramework.Kyro, project.Framework);
            Assert.Equal("Kyro", project.FrameworkName);
            Assert.EndsWith("public", project.DocumentRoot);
            Assert.Contains(".test", project.Domain);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectDetector_ShouldDetectCodeIgniterAndYiiAndCakeAndSlim()
    {
        // 1. CodeIgniter 4
        var ciDir = Path.Combine(Path.GetTempPath(), $"loka_ci_{Guid.NewGuid():N}");
        Directory.CreateDirectory(ciDir);
        Directory.CreateDirectory(Path.Combine(ciDir, "public"));
        File.WriteAllText(Path.Combine(ciDir, "spark"), "#!/usr/bin/env php");

        // 2. Yii2
        var yiiDir = Path.Combine(Path.GetTempPath(), $"loka_yii_{Guid.NewGuid():N}");
        Directory.CreateDirectory(yiiDir);
        Directory.CreateDirectory(Path.Combine(yiiDir, "web"));
        File.WriteAllText(Path.Combine(yiiDir, "yii"), "#!/usr/bin/env php");

        // 3. CakePHP
        var cakeDir = Path.Combine(Path.GetTempPath(), $"loka_cake_{Guid.NewGuid():N}");
        Directory.CreateDirectory(cakeDir);
        Directory.CreateDirectory(Path.Combine(cakeDir, "webroot"));
        Directory.CreateDirectory(Path.Combine(cakeDir, "bin"));
        File.WriteAllText(Path.Combine(cakeDir, "bin", "cake"), "#!/usr/bin/env php");

        // 4. Slim
        var slimDir = Path.Combine(Path.GetTempPath(), $"loka_slim_{Guid.NewGuid():N}");
        Directory.CreateDirectory(slimDir);
        Directory.CreateDirectory(Path.Combine(slimDir, "public"));
        File.WriteAllText(Path.Combine(slimDir, "composer.json"), "{\"require\": {\"slim/slim\": \"^4.0\"}}");

        try
        {
            var pCi = ProjectDetector.Detect(ciDir);
            Assert.Equal(ProjectFramework.CodeIgniter, pCi.Framework);
            Assert.EndsWith("public", pCi.DocumentRoot);

            var pYii = ProjectDetector.Detect(yiiDir);
            Assert.Equal(ProjectFramework.Yii2, pYii.Framework);
            Assert.EndsWith("web", pYii.DocumentRoot);

            var pCake = ProjectDetector.Detect(cakeDir);
            Assert.Equal(ProjectFramework.CakePhp, pCake.Framework);
            Assert.EndsWith("webroot", pCake.DocumentRoot);

            var pSlim = ProjectDetector.Detect(slimDir);
            Assert.Equal(ProjectFramework.Slim, pSlim.Framework);
            Assert.EndsWith("public", pSlim.DocumentRoot);
        }
        finally
        {
            if (Directory.Exists(ciDir)) Directory.Delete(ciDir, recursive: true);
            if (Directory.Exists(yiiDir)) Directory.Delete(yiiDir, recursive: true);
            if (Directory.Exists(cakeDir)) Directory.Delete(cakeDir, recursive: true);
            if (Directory.Exists(slimDir)) Directory.Delete(slimDir, recursive: true);
        }
    }

    [Fact]
    public void ProjectDetector_UniversalDocumentRoot_ResolvesCorrectPriority()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_universal_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var pub = Directory.CreateDirectory(Path.Combine(tempDir, "public"));
        File.WriteAllText(Path.Combine(pub.FullName, "index.php"), "<?php");

        try
        {
            var project = ProjectDetector.Detect(tempDir);
            Assert.EndsWith("public", project.DocumentRoot);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task SslCertificateService_ShouldGenerateCaAndSignedCertificate()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"loka_ssl_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var sslService = new Loka.Infrastructure.Windows.SslCertificateService(_logger);
            var certPath = await sslService.EnsureLocalSslCertificateAsync("unit-test.test", tempDir, trustInWindowsStore: false);

            Assert.True(File.Exists(certPath));
            var keyPath = Path.Combine(tempDir, "unit-test.test.key");
            Assert.True(File.Exists(keyPath));

            // Verify certificate
            using var cert = new System.Security.Cryptography.X509Certificates.X509Certificate2(certPath);
            Assert.Contains("CN=unit-test.test", cert.Subject);
            Assert.Contains("Loka Development Root CA", cert.Issuer);
            Assert.True(cert.NotAfter > DateTime.UtcNow);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ApacheService_ValidateConfigurationAsync_ReturnsValid()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Runtime.Detector.RuntimeDetector(configService, _logger);
        var processManager = new Loka.Infrastructure.Process.ProcessManager(_logger);
        var apacheService = new Loka.Runtime.Apache.ApacheService(configService, detector, processManager, _logger);

        var (isValid, output) = await apacheService.ValidateConfigurationAsync();
        Assert.True(isValid, $"Expected Apache configuration to be valid, but got: {output}");
    }

    [Fact]
    public async Task ServiceManager_ShouldStartAndStopApacheAndPostgres()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Runtime.Detector.RuntimeDetector(configService, _logger);
        var portService = new PortService(_logger);
        var processManager = new Loka.Infrastructure.Process.ProcessManager(_logger);
        var mySqlService = new Loka.Runtime.MySql.MySqlService(configService, detector, processManager, _logger);
        var apacheService = new Loka.Runtime.Apache.ApacheService(configService, detector, processManager, _logger);
        var nginxService = new Loka.Runtime.Nginx.NginxService(configService, detector, processManager, _logger);

        var serviceManager = new Loka.Runtime.Services.ServiceManager(
            processManager,
            portService,
            configService,
            detector,
            apacheService,
            nginxService,
            mySqlService,
            _logger);

        await serviceManager.RefreshAllAsync();

        // 1. Test Apache
        var apacheSvc = serviceManager.GetService(ServiceType.Apache);
        Assert.NotNull(apacheSvc);

        bool apacheStarted = await serviceManager.StartServiceAsync(ServiceType.Apache);
        Assert.True(apacheStarted, $"Apache should start successfully, error: '{apacheSvc.LastError}'");
        Assert.True(apacheSvc.IsRunning, "Apache status should be Running");

        bool apacheStopped = await serviceManager.StopServiceAsync(ServiceType.Apache);
        Assert.True(apacheStopped, "Apache should stop successfully");
        Assert.False(apacheSvc.IsRunning, "Apache status should be Stopped");

        // 2. Test PostgreSQL
        var pgSvc = serviceManager.GetService(ServiceType.PostgreSQL);
        Assert.NotNull(pgSvc);

        bool pgStarted = await serviceManager.StartServiceAsync(ServiceType.PostgreSQL);
        Assert.True(pgStarted, "PostgreSQL should start successfully");
        Assert.True(pgSvc.IsRunning, "PostgreSQL status should be Running");

        bool pgStopped = await serviceManager.StopServiceAsync(ServiceType.PostgreSQL);
        Assert.True(pgStopped, "PostgreSQL should stop successfully");
        Assert.False(pgSvc.IsRunning, "PostgreSQL status should be Stopped");
    }

    [Fact]
    public void DirectoryHelper_ShouldCompletelyDeleteDirectoryIncludingRootFolderAndReadOnlyFiles()
    {
        var testRootDir = Path.Combine(Path.GetTempPath(), $"loka_uninstall_test_{Guid.NewGuid():N}");
        var subDir = Path.Combine(testRootDir, "bin", "subfolder");
        Directory.CreateDirectory(subDir);

        var normalFile = Path.Combine(testRootDir, "test.txt");
        File.WriteAllText(normalFile, "Hello World");

        var readOnlyFile = Path.Combine(subDir, "readonly.dll");
        File.WriteAllText(readOnlyFile, "Read only binary data");
        File.SetAttributes(readOnlyFile, FileAttributes.ReadOnly);

        Assert.True(Directory.Exists(testRootDir));
        Assert.True(File.Exists(readOnlyFile));

        bool result = DirectoryHelper.ForceDeleteDirectory(testRootDir, _logger);

        Assert.True(result, "ForceDeleteDirectory should return true");
        Assert.False(Directory.Exists(testRootDir), "Root directory itself must be completely deleted from disk");
    }

    [Fact]
    public void ProjectInfo_LocalUrl_ShouldGenerateCleanUrlWithout8443()
    {
        var project = new ProjectInfo
        {
            Name = "kyro",
            Domain = "kyro.test",
            Port = 80,
            SslPort = 8443,
            HasSsl = true
        };

        // Rule 2: Clean http://{domain} default without :8443
        Assert.Equal("http://kyro.test", project.LocalUrl);
        Assert.DoesNotContain("8443", project.LocalUrl);
    }

    [Fact]
    public async Task PhpService_WhenSwitchingToNonExistentVersion_ShouldRetainPreviousActiveVersion()
    {
        var configService = new ConfigurationService(_logger);
        configService.Config.Php.ActiveVersion = "8.4";
        var detector = new Loka.Runtime.Detector.RuntimeDetector(configService, _logger);
        var pm = new ProcessManager(_logger);
        var phpService = new Loka.Runtime.Php.PhpService(configService, detector, pm, _logger);

        // Try switching to a non-existent PHP version 7.1
        bool success = await phpService.SetActivePhpVersionAsync("7.1");

        Assert.False(success, "Switching to non-existent PHP version should fail safely.");
        Assert.Equal("8.4", configService.Config.Php.ActiveVersion);
    }

    [Fact]
    public async Task ProjectService_EnsureProjectEntryPoint_GeneratesDiagnosticPageWhenMissing()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"loka_temp_{Guid.NewGuid():N}");
        var testDir = Path.Combine(tempRoot, "testapp");
        Directory.CreateDirectory(testDir);
        try
        {
            var project = new ProjectInfo
            {
                Name = "testapp",
                Domain = "testapp.test",
                DocumentRoot = testDir,
                Framework = ProjectFramework.PlainPhp
            };

            // Using ProjectService to verify entry point generation
            var configService = new ConfigurationService(_logger);
            var detector = new Loka.Runtime.Detector.RuntimeDetector(configService, _logger);
            var pm = new ProcessManager(_logger);
            var apacheService = new Loka.Runtime.Apache.ApacheService(configService, detector, pm, _logger);
            var nginxService = new Loka.Runtime.Nginx.NginxService(configService, detector, pm, _logger);
            var sslService = new Loka.Infrastructure.Windows.SslCertificateService(_logger);
            var hostsService = new Loka.Infrastructure.Windows.HostsService(_logger);
            var projService = new Loka.Project.ProjectService(configService, apacheService, nginxService, hostsService, sslService, _logger, pm);

            var entryFile = Path.Combine(testDir, "index.php");
            Assert.False(File.Exists(entryFile));

            // Run DetectProjectAsync on parent directory
            var parentDir = Path.GetDirectoryName(testDir)!;
            configService.Config.Paths.ProjectsPath = parentDir;
            var detected = await projService.DetectProjectAsync(testDir);

            Assert.NotNull(detected);
            Assert.True(File.Exists(entryFile), "Diagnostic index.php should be created for project without entry point.");
            var content = await File.ReadAllTextAsync(entryFile);
            Assert.Contains("Loka Project Diagnostic Entry Point", content);
            Assert.Contains("testapp.test", content);
        }
        finally
        {
            try { Directory.Delete(tempRoot, true); } catch { }
        }
    }

    [Fact]
    public async Task ConfigurationService_ShouldProperlyConfigureAndPersistDatabaseSetup()
    {
        var configService = new ConfigurationService(_logger);
        var originalDb = configService.Config.Services.PreferredDatabase;
        var originalPort = configService.Config.Services.MariaDbPort;
        try
        {
            configService.Config.Services.PreferredDatabase = "MariaDB";
            configService.Config.Services.MariaDbPort = 3307;
            configService.Config.Database.RootPassword = "secret_password";
            configService.Config.General.IsFirstRunCompleted = true;

            await configService.SaveAsync();

            var newConfigService = new ConfigurationService(_logger);
            await newConfigService.LoadAsync();

            Assert.Equal("MariaDB", newConfigService.Config.Services.PreferredDatabase);
            Assert.Equal(3307, newConfigService.Config.Services.MariaDbPort);
            Assert.Equal("secret_password", newConfigService.Config.Database.RootPassword);
            Assert.True(newConfigService.Config.General.IsFirstRunCompleted);
        }
        finally
        {
            configService.Config.Services.PreferredDatabase = originalDb;
            configService.Config.Services.MariaDbPort = originalPort;
            await configService.SaveAsync();
        }
    }

    [Fact]
    public void SetupWizardViewModel_Step4ToStep5Transition_ShouldActivateReadyStateAndPopulateProvisionSteps()
    {
        var configService = new ConfigurationService(_logger);
        var detector = new Loka.Runtime.Detector.RuntimeDetector(configService, _logger);
        var portService = new PortService(_logger);
        var pm = new ProcessManager(_logger);
        var phpService = new Loka.Runtime.Php.PhpService(configService, detector, pm, _logger);

        var vm = new Loka.App.ViewModels.SetupWizardViewModel(configService, detector, portService, phpService, _logger);

        // Track property change events to ensure WPF bindings update
        var changedProps = new HashSet<string>();
        vm.PropertyChanged += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.PropertyName))
                changedProps.Add(e.PropertyName);
        };

        // Navigate to Step 4 (Database Configuration)
        vm.CurrentStep = 4;
        Assert.True(vm.IsStep4);
        Assert.False(vm.IsStep5);

        changedProps.Clear();

        // Advance from Step 4 to Step 5
        vm.NextStep();

        // Verify Step 5 active state
        Assert.Equal(5, vm.CurrentStep);
        Assert.True(vm.IsStep5);
        Assert.False(vm.IsStep4);
        Assert.Equal("Step 5 of 5", vm.StepIndicatorText);

        // Verify state is Ready and NOT blank
        Assert.Equal(Loka.App.ViewModels.SetupWizardViewModel.SetupStep5State.Ready, vm.Step5State);
        Assert.True(vm.IsStep5Ready);
        Assert.False(vm.IsStep5Running);
        Assert.False(vm.IsStep5Success);
        Assert.False(vm.IsStep5Failed);
        Assert.True(vm.HasAnyStep5StateActive);

        // Verify 5 concrete provision steps populated
        Assert.Equal(5, vm.ProvisionSteps.Count);
        Assert.Equal("Environment & Directory Permissions", vm.ProvisionSteps[0].Name);
        Assert.Equal("Environment Directory Layout", vm.ProvisionSteps[1].Name);
        Assert.Contains("Web Server Configuration", vm.ProvisionSteps[2].Name);
        Assert.Equal("PHP FastCGI Synchronization", vm.ProvisionSteps[3].Name);
        Assert.Contains("Engine & Configuration", vm.ProvisionSteps[4].Name);

        // Verify PropertyChanged fired for all WPF UI binding properties
        Assert.Contains(nameof(vm.IsStep5), changedProps);
        Assert.Contains(nameof(vm.IsStep5Ready), changedProps);
        Assert.Contains(nameof(vm.HasAnyStep5StateActive), changedProps);
        Assert.Contains(nameof(vm.StepIndicatorText), changedProps);
    }

    [Fact]
    public async Task SetupWizardViewModel_ApplySetupExecution_ShouldTransitionToRunningThenSuccess()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"loka_wizard_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);

        try
        {
            var configService = new ConfigurationService(_logger);
            var detector = new Loka.Runtime.Detector.RuntimeDetector(configService, _logger);
            var portService = new PortService(_logger);
            var pm = new ProcessManager(_logger);
            var phpService = new Loka.Runtime.Php.PhpService(configService, detector, pm, _logger);

            var vm = new Loka.App.ViewModels.SetupWizardViewModel(configService, detector, portService, phpService, _logger)
            {
                EnvironmentPath = tempRoot,
                ProjectsPath = Path.Combine(tempRoot, "projects"),
                PreferredWebServer = "Apache",
                PreferredDatabase = "MySQL",
                SelectedPhpVersion = "8.4"
            };

            // Advance to Step 5
            vm.CurrentStep = 5;
            Assert.True(vm.IsStep5Ready);

            // Execute ApplySetup
            await vm.ApplySetupAsync();

            // Verify final success state
            Assert.Equal(Loka.App.ViewModels.SetupWizardViewModel.SetupStep5State.Success, vm.Step5State);
            Assert.True(vm.IsStep5Success);
            Assert.False(vm.IsStep5Running);
            Assert.False(vm.IsStep5Failed);
            Assert.True(vm.IsProvisionComplete);
            Assert.Equal(100, vm.ProvisionProgress);

            // Verify all 5 steps marked Success
            foreach (var step in vm.ProvisionSteps)
            {
                Assert.Equal("Success", step.Status);
                Assert.Equal("✓", step.StatusIcon);
            }

            // Verify directories were created in target environment
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "bin")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "config")));
            Assert.True(Directory.Exists(Path.Combine(tempRoot, "projects")));
        }
        finally
        {
            try { Directory.Delete(tempRoot, recursive: true); } catch { }
        }
    }
}


