namespace Loka.Installer.Models;

public class PackageInstallationException : Exception
{
    public string PackageName { get; }
    public string PackageVersion { get; }
    public string Phase { get; }
    public string Details { get; }
    public string SuggestedAction { get; }

    public PackageInstallationException(
        string packageName,
        string packageVersion,
        string phase,
        string details,
        string suggestedAction,
        Exception? innerException = null)
        : base($"Package '{packageName}' (v{packageVersion}) failed during {phase}: {details}\r\n\r\nSuggested Action: {suggestedAction}", innerException)
    {
        PackageName = packageName;
        PackageVersion = packageVersion;
        Phase = phase;
        Details = details;
        SuggestedAction = suggestedAction;
    }
}
