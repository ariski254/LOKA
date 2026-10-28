using System.Text.Json.Serialization;

namespace Loka.Core.Models;

public enum PatchStatus
{
    Available,
    Downloading,
    Downloaded,
    Verifying,
    Verified,
    Installing,
    Installed,
    Failed
}

public class PatchFileInfo
{
    public string RelativePath { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string TargetRoot { get; set; } = "Environment"; // "App", "Environment", "Config"
}

public class PatchInfo
{
    public string Id { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ReleaseDate { get; set; } = DateTime.UtcNow;
    public string DownloadUrl { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsMandatory { get; set; } = false;
    public string MinAppVersion { get; set; } = "1.0.0";
    public string TargetAppVersion { get; set; } = "1.0.0";
    public List<string> Changelog { get; set; } = new();
    public List<PatchFileInfo> Files { get; set; } = new();

    [JsonIgnore]
    public PatchStatus Status { get; set; } = PatchStatus.Available;

    [JsonIgnore]
    public string FormattedSize => SizeBytes > 1024 * 1024
        ? $"{SizeBytes / (1024.0 * 1024.0):F2} MB"
        : $"{SizeBytes / 1024.0:F1} KB";

    [JsonIgnore]
    public string FormattedDate => ReleaseDate.ToString("yyyy-MM-dd");
}

public class PatchProgress
{
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public double Percentage { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public string Message { get; set; } = string.Empty;

    public string SpeedFormatted => SpeedBytesPerSecond switch
    {
        > 1024 * 1024 => $"{SpeedBytesPerSecond / (1024.0 * 1024.0):F2} MB/s",
        > 1024 => $"{SpeedBytesPerSecond / 1024.0:F1} KB/s",
        _ => $"{SpeedBytesPerSecond:F0} B/s"
    };

    public string BytesProgressFormatted => TotalBytes > 0
        ? $"{BytesDownloaded / (1024.0 * 1024.0):F2} MB / {TotalBytes / (1024.0 * 1024.0):F2} MB"
        : $"{BytesDownloaded / (1024.0 * 1024.0):F2} MB";
}

public class PatchCheckResult
{
    public bool IsUpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = "1.0.0";
    public string LatestVersion { get; set; } = "1.0.0";
    public List<PatchInfo> AvailablePatches { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

public class PatchInstallResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int InstalledFilesCount { get; set; }
    public string? BackupDirectory { get; set; }
}

public class PatchManifestFeed
{
    public string ManifestVersion { get; set; } = "1.0.0";
    public string LatestAppVersion { get; set; } = "1.0.1";
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public List<PatchInfo> Patches { get; set; } = new();
}
