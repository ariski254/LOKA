using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Loka.Core.Models;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Logging;
using Loka.Infrastructure.Services;
using Xunit;

namespace Loka.Tests;

public class PatchServiceTests : IDisposable
{
    private readonly string _testRoot;
    private readonly LogService _logger = new();
    private readonly ConfigurationService _configService;
    private readonly PatchService _patchService;

    public PatchServiceTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "LokaPatchTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _configService = new ConfigurationService(_logger);
        _configService.Config.Paths.RootPath = _testRoot;
        _configService.EnsureDirectories();

        _patchService = new PatchService(_configService, _logger);
        _patchService.FeedUrl = string.Empty;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task CheckForPatchesAsync_WhenDefaultFeedGenerated_ShouldFindAvailablePatch()
    {
        var result = await _patchService.CheckForPatchesAsync();

        Assert.NotNull(result);
        Assert.Equal("1.0.0", result.CurrentVersion);
        Assert.True(result.IsUpdateAvailable);
        Assert.NotEmpty(result.AvailablePatches);

        var first = result.AvailablePatches.First();
        Assert.Equal("1.0.1", first.Version);
        Assert.NotEmpty(first.Changelog);
        Assert.True(first.SizeBytes > 0);
    }

    [Fact]
    public async Task VerifyPatchAsync_WithValidHash_ShouldReturnTrue()
    {
        var checkResult = await _patchService.CheckForPatchesAsync();
        var patch = checkResult.AvailablePatches.First();

        bool isValid = await _patchService.VerifyPatchAsync(patch);

        Assert.True(isValid);
        Assert.Equal(PatchStatus.Verified, patch.Status);
    }

    [Fact]
    public async Task VerifyPatchAsync_WithMismatchedHash_ShouldReturnFalse()
    {
        var checkResult = await _patchService.CheckForPatchesAsync();
        var patch = checkResult.AvailablePatches.First();
        patch.Sha256 = "INVALID_HASH_0123456789ABCDEF0123456789ABCDEF";

        bool isValid = await _patchService.VerifyPatchAsync(patch);

        Assert.False(isValid);
        Assert.Equal(PatchStatus.Failed, patch.Status);
    }

    [Fact]
    public async Task VerifyPatchAsync_WithPathTraversal_ShouldRejectMaliciousArchive()
    {
        string maliciousZip = Path.Combine(_configService.Config.Paths.TmpPath, "patches", "malicious.zip");
        var dir = Path.GetDirectoryName(maliciousZip)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        using (var zip = ZipFile.Open(maliciousZip, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("../../../Windows/System32/evil.dll");
            using var sw = new StreamWriter(entry.Open());
            sw.WriteLine("malicious");
        }

        using var sha = SHA256.Create();
        using var stream = File.OpenRead(maliciousZip);
        string hash = Convert.ToHexString(sha.ComputeHash(stream));

        var patch = new PatchInfo
        {
            Id = "malicious-patch",
            Version = "1.0.1",
            DownloadUrl = maliciousZip,
            Sha256 = hash
        };

        bool isValid = await _patchService.VerifyPatchAsync(patch);

        Assert.False(isValid);
        Assert.Equal(PatchStatus.Failed, patch.Status);
    }

    [Fact]
    public async Task DownloadPatchAsync_WhenLocalFile_ShouldTrackProgressAndComplete()
    {
        var checkResult = await _patchService.CheckForPatchesAsync();
        var patch = checkResult.AvailablePatches.First();

        var reports = new List<PatchProgress>();
        var progress = new Progress<PatchProgress>(reports.Add);

        bool success = await _patchService.DownloadPatchAsync(patch, progress);

        Assert.True(success);
        Assert.True(patch.Status is PatchStatus.Downloaded or PatchStatus.Verified);
        Assert.True(File.Exists(_patchService.GetLocalPatchFilePath(patch)));
    }

    [Fact]
    public async Task DownloadPatchAsync_WhenCancelled_ShouldAbort()
    {
        var checkResult = await _patchService.CheckForPatchesAsync();
        var patch = checkResult.AvailablePatches.First();

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancel immediately

        var progress = new Progress<PatchProgress>(_ => { });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _patchService.DownloadPatchAsync(patch, progress, cts.Token);
        });
    }

    [Fact]
    public async Task InstallPatchAsync_ShouldApplyFilesAndRecordHistory()
    {
        var checkResult = await _patchService.CheckForPatchesAsync();
        var patch = checkResult.AvailablePatches.First();

        var progressMsg = new List<string>();
        var progress = new Progress<string>(progressMsg.Add);

        var installResult = await _patchService.InstallPatchAsync(patch, progress);

        Assert.True(installResult.IsSuccess);
        Assert.True(installResult.InstalledFilesCount > 0);
        Assert.NotNull(installResult.BackupDirectory);
        Assert.True(Directory.Exists(installResult.BackupDirectory));

        // Verify installed history
        Assert.Contains(_patchService.InstalledPatches, p => p.Id == patch.Id);
    }

    [Fact]
    public async Task RollbackPatchAsync_ShouldRestorePreviousFiles()
    {
        string backupDir = Path.Combine(_configService.Config.Paths.BackupsPath, "patches", "test_rollback");
        Directory.CreateDirectory(backupDir);

        string testRelative = "config/test_backup.txt";
        string backupFile = Path.Combine(backupDir, testRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(backupFile)!);
        await File.WriteAllTextAsync(backupFile, "Original Content v1.0.0");

        string targetFile = Path.Combine(_configService.Config.Paths.RootPath, testRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
        await File.WriteAllTextAsync(targetFile, "Modified Content v1.0.1");

        bool rollbackSuccess = await _patchService.RollbackPatchAsync(backupDir);

        Assert.True(rollbackSuccess);
        Assert.True(File.Exists(targetFile));
        string restoredContent = await File.ReadAllTextAsync(targetFile);
        Assert.Equal("Original Content v1.0.0", restoredContent);
    }

    [Fact]
    public void UpdateConfig_ShouldHaveValidDefaultSettings()
    {
        var config = _configService.Config.Updates;

        Assert.NotNull(config);
        Assert.True(config.CheckOnStartup);
        Assert.NotEmpty(config.FeedUrl);
        Assert.False(config.AutoDownload);
        Assert.Equal("1.0.0", config.InstalledVersion);
    }
}
