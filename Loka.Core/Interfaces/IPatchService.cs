using Loka.Core.Models;

namespace Loka.Core.Interfaces;

public interface IPatchService
{
    string CurrentAppVersion { get; }
    string FeedUrl { get; set; }
    DateTime? LastCheckedTime { get; }
    IReadOnlyList<PatchInfo> InstalledPatches { get; }

    Task<PatchCheckResult> CheckForPatchesAsync(CancellationToken cancellationToken = default);
    Task<bool> DownloadPatchAsync(PatchInfo patch, IProgress<PatchProgress> progress, CancellationToken cancellationToken = default);
    Task<bool> VerifyPatchAsync(PatchInfo patch);
    Task<PatchInstallResult> InstallPatchAsync(PatchInfo patch, IProgress<string> progress, CancellationToken cancellationToken = default);
    Task<bool> RollbackPatchAsync(string backupDirectory);
    string GetLocalPatchFilePath(PatchInfo patch);
}
