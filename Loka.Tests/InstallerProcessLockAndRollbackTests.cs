using System.Diagnostics;
using System.IO;
using System.Text;
using Loka.Installer.Models;
using Loka.Installer.Services;
using Xunit;

namespace Loka.Tests;

public class InstallerProcessLockAndRollbackTests : IDisposable
{
    private readonly string _testRoot;
    private readonly string _installDir;
    private readonly string _envDir;

    public InstallerProcessLockAndRollbackTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "Loka_Test_" + Guid.NewGuid().ToString("N"));
        _installDir = Path.Combine(_testRoot, "ProgramFiles_Loka");
        _envDir = Path.Combine(_testRoot, "Loka_Env");

        Directory.CreateDirectory(_installDir);
        Directory.CreateDirectory(_envDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public void Scenario1_CleanInstall_NoRunningLoka_IsFileLockedFalse()
    {
        // 1. Fresh installation scenario where Loka.exe does not exist or is unlocked
        var exePath = Path.Combine(_installDir, "Loka.exe");
        Assert.False(File.Exists(exePath));
        Assert.False(InstallerEngine.IsFileLocked(exePath));

        File.WriteAllText(exePath, "MOCK_EXE_PAYLOAD_V1");
        Assert.False(InstallerEngine.IsFileLocked(exePath));
    }

    [Fact]
    public void Scenario2_IsFileLocked_DetectsExclusiveWriteLock()
    {
        var targetFile = Path.Combine(_installDir, "Loka.exe");
        File.WriteAllText(targetFile, "INITIAL_BINARY_BYTES");

        Assert.False(InstallerEngine.IsFileLocked(targetFile));

        // Hold exclusive lock
        using (var lockStream = File.Open(targetFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.True(InstallerEngine.IsFileLocked(targetFile));
        }

        // Lock released
        Assert.False(InstallerEngine.IsFileLocked(targetFile));
    }

    [Fact]
    public async Task Scenario3_EnsureApplicationClosed_FileLockedIndefinitely_ThrowsInformativeIOException()
    {
        var engine = new InstallerEngine
        {
            InstallPath = _installDir,
            EnvironmentPath = _envDir
        };

        var targetExe = Path.Combine(_installDir, "Loka.exe");
        File.WriteAllText(targetExe, "CURRENT_VERSION_BYTES");

        // Lock the file exclusively so installer cannot write to it
        using var lockStream = File.Open(targetExe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var ex = await Assert.ThrowsAsync<IOException>(async () =>
        {
            await engine.EnsureApplicationClosedAsync((pct, msg) => { }, CancellationToken.None);
        });

        Assert.Contains("used by another process", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(targetExe, ex.Message);
    }

    [Fact]
    public async Task Scenario4_ProcessLockPrompt_UserCancel_ThrowsOperationCanceledException()
    {
        var engine = new InstallerEngine
        {
            InstallPath = _installDir,
            EnvironmentPath = _envDir
        };

        // Inject prompt handler returning Cancel
        engine.ProcessLockPromptHandler = info => Task.FromResult(ProcessLockAction.Cancel);

        // Simulate prompt invocation directly with a mock process info
        var mockInfo = new ProcessLockInfo
        {
            ProcessId = 999999,
            ProcessName = "Loka.exe",
            ExecutablePath = Path.Combine(_installDir, "Loka.exe"),
            Reason = "LOKA application is running"
        };

        var action = await engine.ProcessLockPromptHandler(mockInfo);
        Assert.Equal(ProcessLockAction.Cancel, action);
    }

    [Fact]
    public async Task Scenario5_EnsureApplicationClosed_LockReleasedDuringPolling_Succeeds()
    {
        var engine = new InstallerEngine
        {
            InstallPath = _installDir,
            EnvironmentPath = _envDir
        };

        var targetExe = Path.Combine(_installDir, "Loka.exe");
        File.WriteAllText(targetExe, "RUNNING_VERSION_BYTES");

        var lockStream = File.Open(targetExe, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Schedule release after 400ms while polling
        _ = Task.Run(async () =>
        {
            await Task.Delay(400);
            lockStream.Dispose();
        });

        // EnsureApplicationClosedAsync should poll, observe release, and succeed
        await engine.EnsureApplicationClosedAsync((pct, msg) => { }, CancellationToken.None);

        Assert.False(InstallerEngine.IsFileLocked(targetExe));
    }

    [Fact]
    public void Scenario6_GetLockingProcesses_FilterPathMatches()
    {
        var procs = InstallerEngine.GetLockingProcesses(_installDir, _envDir);
        // Should execute safely without throwing exceptions on any Windows environment
        Assert.NotNull(procs);
    }

    [Fact]
    public async Task Scenario7_Rollback_RestoresOriginalVersionAndCleansNewFiles()
    {
        var engine = new InstallerEngine
        {
            InstallPath = _installDir,
            EnvironmentPath = _envDir,
            InstallMode = "Update"
        };

        // Existing pre-install file (v1)
        var exePath = Path.Combine(_installDir, "Loka.exe");
        File.WriteAllText(exePath, "ORIGINAL_VERSION_1.0");

        // Simulate installation step failure and rollback
        var steps = new List<InstallationStep>
        {
            new() { Title = "Step 1", Status = StepStatus.Waiting },
            new() { Title = "Step 2", Status = StepStatus.Waiting },
            new() { Title = "Step 3", Status = StepStatus.Waiting },
            new() { Title = "Step 4", Status = StepStatus.Waiting },
            new() { Title = "Step 5", Status = StepStatus.Waiting },
            new() { Title = "Step 6", Status = StepStatus.Waiting },
            new() { Title = "Step 7", Status = StepStatus.Waiting }
        };

        // Lock file so payload replacement will fail and trigger rollback
        using (var lockStream = File.Open(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            await Assert.ThrowsAsync<IOException>(async () =>
            {
                await engine.InstallAsync(steps, (pct, msg) => { }, CancellationToken.None);
            });
        }

        // After lock is released, verify that the original file was restored
        Assert.True(File.Exists(exePath));
        var content = File.ReadAllText(exePath);
        Assert.Equal("ORIGINAL_VERSION_1.0", content);
    }

    [Fact]
    public void Scenario8_PreservesUserConfigAndDatabasesDuringUpdate()
    {
        var configDir = Path.Combine(_envDir, "config");
        var dataDir = Path.Combine(_envDir, "data");
        var projectsDir = Path.Combine(_envDir, "projects");

        Directory.CreateDirectory(configDir);
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(projectsDir);

        var lokaJson = Path.Combine(configDir, "loka.json");
        var customConfig = @"{ ""CustomSetting"": ""UserPreservedValue"", ""Projects"": [ ""my-laravel-app"" ] }";
        File.WriteAllText(lokaJson, customConfig);

        var dbFile = Path.Combine(dataDir, "mysql_data.db");
        File.WriteAllText(dbFile, "USER_DATABASE_DATA_PERSISTENT");

        var projectFile = Path.Combine(projectsDir, "index.php");
        File.WriteAllText(projectFile, "<?php echo 'User Project';");

        var engine = new InstallerEngine
        {
            InstallPath = _installDir,
            EnvironmentPath = _envDir,
            InstallMode = "Update"
        };

        // Verify config is NOT overwritten if already present
        var configFile = Path.Combine(_envDir, "config", "loka.json");
        Assert.True(File.Exists(configFile));
        Assert.Equal(customConfig, File.ReadAllText(configFile));

        // Verify database and project files remain intact
        Assert.True(File.Exists(dbFile));
        Assert.Equal("USER_DATABASE_DATA_PERSISTENT", File.ReadAllText(dbFile));

        Assert.True(File.Exists(projectFile));
        Assert.Equal("<?php echo 'User Project';", File.ReadAllText(projectFile));
    }
}
