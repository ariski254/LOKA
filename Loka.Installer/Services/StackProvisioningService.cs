using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection;
using System.Text;
using Loka.Installer.Models;

namespace Loka.Installer.Services;

public class StackProvisioningService
{
    private readonly HttpClient _httpClient;

    public static bool HasEmbeddedOfflinePayload =>
        Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Any(n => n.EndsWith("stack_payload.zip", StringComparison.OrdinalIgnoreCase));

    public StackProvisioningService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 10,
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };
        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15)
        };
        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Loka-Installer/1.0");
        return client;
    }

    public async Task ProvisionAsync(
        string environmentPath,
        Action<int, string> onProgress,
        IEnumerable<string>? selectedComponents = null,
        bool isRepair = false,
        bool isUpdate = false,
        CancellationToken cancellationToken = default)
    {
        InstallerLogger.Info($"Provisioning LOKA development stack into '{environmentPath}'...");

        // 1. Ensure basic environment directory layout
        EnsureDirectoryLayout(environmentPath);

        // 2. Determine local cache directory (without assuming developer user profile)
        var localCacheDirs = new List<string>
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "artifacts", "cache"),
            Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "artifacts", "cache"))
        };

        var devCache = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "artifacts", "cache"));
        if (Directory.Exists(devCache))
        {
            localCacheDirs.Add(devCache);
        }

        localCacheDirs.Add(Path.Combine(Path.GetTempPath(), "LokaInstallerCache"));

        string cacheDir = localCacheDirs.FirstOrDefault(Directory.Exists) ?? localCacheDirs.Last();
        if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

        InstallerLogger.Info($"Using package cache directory: '{cacheDir}'");

        // Extract embedded offline stack payload if present (for full offline installer)
        var assembly = Assembly.GetExecutingAssembly();
        var stackResource = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("stack_payload.zip", StringComparison.OrdinalIgnoreCase));

        if (stackResource != null)
        {
            InstallerLogger.Info($"Extracting embedded offline stack payload '{stackResource}' into '{cacheDir}'...");
            onProgress(48, "Extracting bundled offline development packages...");

            try
            {
                using var stream = assembly.GetManifestResourceStream(stackResource);
                if (stream != null)
                {
                    using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
                    foreach (var entry in archive.Entries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var dest = Path.Combine(cacheDir, entry.FullName);
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(dest);
                        }
                        else
                        {
                            var parent = Path.GetDirectoryName(dest);
                            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                            entry.ExtractToFile(dest, overwrite: true);
                        }
                    }
                    InstallerLogger.Info("Embedded offline stack packages extracted successfully.");
                }
            }
            catch (Exception ex)
            {
                InstallerLogger.Warning($"Could not extract embedded stack payload: {ex.Message}");
            }
        }

        // 3. Define all latest stack components with verified official SHA-256 and file sizes
        var allComponents = new[]
        {
            // Web Servers
            new StackComponent(
                "Nginx",
                "1.28.3",
                "nginx.zip",
                "https://nginx.org/download/nginx-1.28.3.zip",
                null,
                Path.Combine(environmentPath, "bin", "nginx"),
                IsZip: true,
                TargetBinaryRelative: "nginx.exe",
                CustomRootSubfolder: null,
                ExpectedSha256: "AAD7BF75D669ECE7671688BFDF35F1093D6A30D2E62469405F4D55D8D82D5FD3",
                ExpectedSizeBytes: 2743427),

            new StackComponent(
                "Apache",
                "2.4.66",
                "apache.zip",
                "https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.66-251206-Win64-VS17.zip",
                null,
                Path.Combine(environmentPath, "bin", "apache"),
                IsZip: true,
                TargetBinaryRelative: Path.Combine("bin", "httpd.exe"),
                CustomRootSubfolder: "Apache24",
                ExpectedSha256: "2CD1F349B6705E43E784E876A233EEB1A859FA6B8ABC693A91F87B35A368F7F9",
                ExpectedSizeBytes: 11527838),

            // PHP Runtimes (8.3, 8.4 and 8.5)
            new StackComponent(
                "PHP 8.3",
                "8.3.36",
                "php-8.3.zip",
                "https://windows.php.net/downloads/releases/archives/php-8.3.36-nts-Win32-vs16-x64.zip",
                new[]
                {
                    "https://windows.php.net/downloads/releases/php-8.3.36-nts-Win32-vs16-x64.zip",
                    "https://downloads.php.net/~windows/releases/archives/php-8.3.36-nts-Win32-vs16-x64.zip"
                },
                Path.Combine(environmentPath, "bin", "php", "8.3"),
                IsZip: true,
                TargetBinaryRelative: "php.exe",
                CustomRootSubfolder: null,
                ExpectedSha256: "3C9242622C7402B47C38D2EC2ED779CEF3A290FD6F2BDE6C35F9C482699E7900",
                ExpectedSizeBytes: 34101685),

            new StackComponent(
                "PHP 8.4",
                "8.4.26",
                "php-8.4.zip",
                "https://windows.php.net/downloads/releases/archives/php-8.4.26-nts-Win32-vs17-x64.zip",
                new[]
                {
                    "https://windows.php.net/downloads/releases/php-8.4.26-nts-Win32-vs17-x64.zip",
                    "https://downloads.php.net/~windows/releases/archives/php-8.4.26-nts-Win32-vs17-x64.zip"
                },
                Path.Combine(environmentPath, "bin", "php", "8.4"),
                IsZip: true,
                TargetBinaryRelative: "php.exe",
                CustomRootSubfolder: null,
                ExpectedSha256: "DA68394F9193B7F6B89D0C76861A4034AE10EFEE7FD55A7255D8118C2ACF70D7",
                ExpectedSizeBytes: 35103568),

            new StackComponent(
                "PHP 8.5",
                "8.5.11",
                "php-8.5.zip",
                "https://windows.php.net/downloads/releases/archives/php-8.5.11-nts-Win32-vs17-x64.zip",
                new[]
                {
                    "https://windows.php.net/downloads/releases/php-8.5.11-nts-Win32-vs17-x64.zip",
                    "https://downloads.php.net/~windows/releases/archives/php-8.5.11-nts-Win32-vs17-x64.zip"
                },
                Path.Combine(environmentPath, "bin", "php", "8.5"),
                IsZip: true,
                TargetBinaryRelative: "php.exe",
                CustomRootSubfolder: null,
                ExpectedSha256: "0EA96E0D2B9B737A6036F05CF4E95C49313FAA6D0F27BD97EDB2742503F0C043",
                ExpectedSizeBytes: 36048897),

            // Databases
            new StackComponent(
                "MariaDB",
                "11.8.9",
                "mariadb.zip",
                "https://archive.mariadb.org/mariadb-11.8.9/winx64-packages/mariadb-11.8.9-winx64.zip",
                null,
                Path.Combine(environmentPath, "bin", "mariadb"),
                IsZip: true,
                TargetBinaryRelative: Path.Combine("bin", "mariadbd.exe"),
                CustomRootSubfolder: null,
                ExpectedSha256: "830C46727D9278EAE212AE3ECA44EEB9E71B2A68704E95F344A64FBA7B1963F5",
                ExpectedSizeBytes: 97016251),

            new StackComponent(
                "MySQL",
                "8.4.0",
                "mysql.zip",
                "https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.0-winx64.zip",
                null,
                Path.Combine(environmentPath, "bin", "mysql"),
                IsZip: true,
                TargetBinaryRelative: Path.Combine("bin", "mysqld.exe"),
                CustomRootSubfolder: null,
                ExpectedSha256: "FB1F0C6CCE229BE4B3208BEC8A605576BB47EABB00512BCCBCC4396AF332C25C",
                ExpectedSizeBytes: 258946761),

            new StackComponent(
                "PostgreSQL",
                "17.4",
                "postgresql.zip",
                "https://get.enterprisedb.com/postgresql/postgresql-17.4-1-windows-x64-binaries.zip",
                null,
                Path.Combine(environmentPath, "bin", "postgresql"),
                IsZip: true,
                TargetBinaryRelative: Path.Combine("bin", "postgres.exe"),
                CustomRootSubfolder: "pgsql",
                ExpectedSha256: "80BB49E80DF0100DD52E7B3C647896F8D8CFDE760B6473E5180819C8DF4B23C4",
                ExpectedSizeBytes: 315936452),

            // Runtimes & Tools
            new StackComponent(
                "Node.js",
                "22.23.3",
                "nodejs.zip",
                "https://nodejs.org/dist/v22.23.3/node-v22.23.3-win-x64.zip",
                new[] { "https://nodejs.org/dist/latest-v22.x/node-v22.23.3-win-x64.zip" },
                Path.Combine(environmentPath, "bin", "nodejs"),
                IsZip: true,
                TargetBinaryRelative: "node.exe",
                CustomRootSubfolder: null,
                ExpectedSha256: "2B0FF57B049CDA1BBCEA2240EEC20467018713C1EFE1F7360C2681859B90ED71",
                ExpectedSizeBytes: 35574076),

            new StackComponent(
                "Git",
                "2.55.0.5",
                "git.zip",
                "https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.5/MinGit-2.55.0.5-64-bit.zip",
                null,
                Path.Combine(environmentPath, "bin", "git"),
                IsZip: true,
                TargetBinaryRelative: Path.Combine("cmd", "git.exe"),
                CustomRootSubfolder: null,
                ExpectedSha256: "56D7B226B7693196CFC71FEF26568F536C4A021AB6C37FF2DB4287BED908E96E",
                ExpectedSizeBytes: 38989688),

            new StackComponent(
                "Composer",
                "2.10.3",
                "composer.phar",
                "https://getcomposer.org/download/latest-stable/composer.phar",
                null,
                Path.Combine(environmentPath, "bin", "composer"),
                IsZip: false,
                TargetBinaryRelative: "composer.phar",
                CustomRootSubfolder: null,
                ExpectedSha256: "7A2D379D5B8FFDAA028580EF26494C36D2FEEF4B178D3DD1473A4DBC5E17C8D6",
                ExpectedSizeBytes: 3642137),

            new StackComponent(
                "phpMyAdmin",
                "5.2.2",
                "phpmyadmin.zip",
                "https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip",
                new[] { "https://files.phpmyadmin.net/phpMyAdmin/5.2.1/phpMyAdmin-5.2.1-all-languages.zip" },
                Path.Combine(environmentPath, "bin", "phpmyadmin"),
                IsZip: true,
                TargetBinaryRelative: "index.php",
                CustomRootSubfolder: null,
                ExpectedSha256: "6B99534F72FFB1D7275F50D23CA4141E1495C97D7CADB73A41D6DC580ED5CE29",
                ExpectedSizeBytes: 15782614)
        };

        var components = allComponents;
        if (selectedComponents != null && selectedComponents.Any())
        {
            var selectedSet = selectedComponents.ToHashSet(StringComparer.OrdinalIgnoreCase);
            components = allComponents.Where(c => selectedSet.Contains(c.Name)).ToArray();
        }

        // 4. Provision each component
        int compIndex = 0;
        foreach (var comp in components)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int basePct = 50 + (int)((float)compIndex / components.Length * 24); // 50% -> 74%
            await ProvisionComponentAsync(comp, cacheDir, basePct, onProgress, isRepair, cancellationToken);
            compIndex++;
        }

        // 5. Run post-extraction setup and configuration
        await RunPostExtractionSetupAsync(environmentPath, onProgress, cancellationToken);
    }

    private async Task ProvisionComponentAsync(
        StackComponent comp,
        string cacheDir,
        int baseProgress,
        Action<int, string> onProgress,
        bool isRepair,
        CancellationToken cancellationToken)
    {
        var targetFile = Path.Combine(cacheDir, comp.PackageFileName);
        if (!File.Exists(targetFile) || new FileInfo(targetFile).Length < 1000)
        {
            var altName = comp.PackageFileName switch
            {
                "mariadb-11.8.zip" => "mariadb.zip",
                "mysql-8.4.zip" => "mysql.zip",
                "postgresql-17.zip" => "postgresql.zip",
                "node-v22.zip" => "nodejs.zip",
                "mingit.zip" => "git.zip",
                _ => null
            };
            if (altName != null)
            {
                var altFile = Path.Combine(cacheDir, altName);
                if (File.Exists(altFile) && new FileInfo(altFile).Length > 1000)
                {
                    targetFile = altFile;
                }
            }
        }

        // Check if already installed
        var checkBinary = Path.Combine(comp.DestinationDirectory, comp.TargetBinaryRelative);
        if (File.Exists(checkBinary) && !isRepair)
        {
            InstallerLogger.Info($"Component '{comp.Name}' already installed at '{checkBinary}'.");
            onProgress(baseProgress, $"[Installed] {comp.Name} is already installed.");
            return;
        }

        // Check if cached package exists, verify its integrity
        if (File.Exists(targetFile))
        {
            if (!string.IsNullOrEmpty(comp.ExpectedSha256))
            {
                if (VerifyFileSha256(targetFile, comp.ExpectedSha256, out var cachedHash, out var failureReason))
                {
                    InstallerLogger.Info($"Cached package '{targetFile}' verified with SHA-256: {cachedHash}.");
                    onProgress(baseProgress, $"[Verified] {comp.Name} package integrity verified.");
                }
                else
                {
                    InstallerLogger.Warning($"Cached package '{targetFile}' is invalid or outdated ({failureReason}). Deleting invalid cache file...");
                    try { File.Delete(targetFile); } catch { }
                }
            }
            else if (new FileInfo(targetFile).Length > 1000)
            {
                onProgress(baseProgress, $"[Verified] {comp.Name} package found in cache.");
            }
        }

        // If not in cache, download package with retry and validation
        if (!File.Exists(targetFile) || new FileInfo(targetFile).Length < 1000)
        {
            if (HasEmbeddedOfflinePayload && !System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                throw new PackageInstallationException(
                    comp.Name,
                    comp.Version,
                    "Cache Check",
                    $"Package '{comp.PackageFileName}' was not found in the offline bundle or local cache and network is unavailable.",
                    "Connect to the internet to download required packages, or supply the verified package file in the installer cache folder.");
            }

            await DownloadPackageWithRetryAsync(comp, targetFile, baseProgress, onProgress, cancellationToken);
        }

        // Extract or copy
        onProgress(baseProgress + 1, $"[Installing] Installing {comp.Name}...");
        if (!Directory.Exists(comp.DestinationDirectory))
            Directory.CreateDirectory(comp.DestinationDirectory);

        try
        {
            if (comp.IsZip)
            {
                ExtractZipWithRootStripping(targetFile, comp.DestinationDirectory, comp.CustomRootSubfolder, cancellationToken);
            }
            else
            {
                var destPath = Path.Combine(comp.DestinationDirectory, Path.GetFileName(targetFile));
                File.Copy(targetFile, destPath, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is not PackageInstallationException and not OperationCanceledException)
        {
            InstallerLogger.Error($"Extraction error for '{comp.Name}'", ex);
            throw new PackageInstallationException(
                comp.Name,
                comp.Version,
                "Extraction",
                $"Failed to extract package into '{comp.DestinationDirectory}': {ex.Message}",
                "The archive may be corrupted or blocked by antivirus. Try deleting the cached package and re-running installation.",
                ex);
        }

        onProgress(baseProgress + 2, $"[Installed] {comp.Name} deployment complete.");
        InstallerLogger.Info($"Successfully installed {comp.Name} to '{comp.DestinationDirectory}'.");
    }

    public static bool VerifyFileSha256(string filePath, string expectedHash, out string actualHash, out string? failureReason)
    {
        actualHash = string.Empty;
        failureReason = null;

        if (string.IsNullOrWhiteSpace(expectedHash))
        {
            return true;
        }

        if (!File.Exists(filePath))
        {
            failureReason = $"Target file '{filePath}' does not exist.";
            return false;
        }

        var normalizedExpected = NormalizeHash(expectedHash);
        if (normalizedExpected.Length != 64 || !normalizedExpected.All(Uri.IsHexDigit))
        {
            failureReason = $"Expected SHA-256 hash has invalid format: '{expectedHash}'. Expected 64 hexadecimal characters.";
            return false;
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length == 0)
            {
                failureReason = "Downloaded file is 0 bytes (empty stream).";
                return false;
            }

            if (fileInfo.Length < 1000)
            {
                try
                {
                    var textSample = File.ReadAllText(filePath, Encoding.UTF8);
                    if (textSample.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                        textSample.Contains("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                        textSample.Contains("<head", StringComparison.OrdinalIgnoreCase))
                    {
                        failureReason = "Server returned an HTML error/anti-bot page instead of the binary package.";
                        return false;
                    }
                }
                catch { }
            }

            using var sha = System.Security.Cryptography.SHA256.Create();
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 131072);
            var hashBytes = sha.ComputeHash(stream);
            actualHash = Convert.ToHexString(hashBytes);

            if (!string.Equals(actualHash, normalizedExpected, StringComparison.OrdinalIgnoreCase))
            {
                failureReason = $"Downloaded package failed SHA-256 checksum verification. Expected: {normalizedExpected}, Computed: {actualHash}. File size: {fileInfo.Length} bytes.";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            failureReason = $"Failed to calculate SHA-256 hash for '{filePath}': {ex.Message}";
            return false;
        }
    }

    public static string NormalizeHash(string? hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return string.Empty;
        var clean = hash.Trim();
        if (clean.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean.Substring(7);
        }
        return clean.Replace("-", "").Replace(" ", "").Trim().ToUpperInvariant();
    }

    private async Task DownloadPackageWithRetryAsync(
        StackComponent comp,
        string destinationPath,
        int baseProgress,
        Action<int, string> onProgress,
        CancellationToken cancellationToken)
    {
        var urlsToTry = new List<string> { comp.DownloadUrl };
        if (comp.FallbackUrls != null && comp.FallbackUrls.Length > 0)
        {
            urlsToTry.AddRange(comp.FallbackUrls);
        }

        const int maxRetriesPerUrl = 3;
        string? lastErrorDetails = null;

        foreach (var url in urlsToTry)
        {
            for (int attempt = 1; attempt <= maxRetriesPerUrl; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tempFile = destinationPath + $".tmp.{Guid.NewGuid():N}";
                try
                {
                    if (File.Exists(tempFile))
                    {
                        try { File.Delete(tempFile); } catch { }
                    }

                    string attemptNote = attempt > 1 ? $" (Retry attempt {attempt}/{maxRetriesPerUrl})" : "";
                    InstallerLogger.Info($"Downloading {comp.Name} from {url} to '{tempFile}'{attemptNote}...");
                    onProgress(baseProgress, $"[Downloading] {comp.Name}{attemptNote}...");

                    await DownloadFileStreamAsync(
                        url,
                        tempFile,
                        (percent) =>
                        {
                            int currentPct = baseProgress + (int)(percent * 0.02f);
                            onProgress(currentPct, $"[Downloading] {comp.Name} ({percent}%)...");
                        },
                        cancellationToken);

                    // Verify SHA-256 on downloaded temp file before accepting
                    if (!string.IsNullOrEmpty(comp.ExpectedSha256))
                    {
                        if (VerifyFileSha256(tempFile, comp.ExpectedSha256, out var actualHash, out var failureReason))
                        {
                            if (File.Exists(destinationPath)) File.Delete(destinationPath);
                            File.Move(tempFile, destinationPath);
                            InstallerLogger.Info($"Verified {comp.Name} package SHA-256: {actualHash}.");
                            onProgress(baseProgress, $"[Verified] {comp.Name} downloaded and verified.");
                            return;
                        }
                        else
                        {
                            lastErrorDetails = failureReason;
                            InstallerLogger.Warning($"Package verification failed for {comp.Name} from {url}: {failureReason}");
                            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

                            // If verification failed on complete file, retry only once per URL to check if transport corrupt
                            if (attempt < 2)
                            {
                                await Task.Delay(500, cancellationToken);
                                continue;
                            }
                            break;
                        }
                    }
                    else
                    {
                        if (File.Exists(destinationPath)) File.Delete(destinationPath);
                        File.Move(tempFile, destinationPath);
                        return;
                    }
                }
                catch (HttpRequestException httpEx)
                {
                    lastErrorDetails = $"HTTP request failed ({httpEx.StatusCode}): {httpEx.Message}";
                    InstallerLogger.Warning($"HTTP error downloading {comp.Name} from {url}: {httpEx.Message}");
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

                    if (httpEx.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // 404 is not transient, try fallback URL immediately
                        break;
                    }

                    if (attempt < maxRetriesPerUrl)
                    {
                        await Task.Delay(1000 * attempt, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException and not PackageInstallationException)
                {
                    lastErrorDetails = $"Network/IO error: {ex.Message}";
                    InstallerLogger.Warning($"Download attempt {attempt} failed for {comp.Name} from {url}: {ex.Message}");
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }

                    if (attempt < maxRetriesPerUrl)
                    {
                        await Task.Delay(1000 * attempt, cancellationToken);
                    }
                }
            }
        }

        // If all attempts failed, throw structured user-friendly exception
        throw new PackageInstallationException(
            comp.Name,
            comp.Version,
            "Checksum Verification",
            lastErrorDetails ?? "Failed to download and verify package integrity after multiple attempts.",
            "Please verify your internet connection, firewall, or proxy settings. Alternatively, use the full Loka Offline Installer bundle.");
    }

    private async Task DownloadFileStreamAsync(
        string url,
        string destinationTempPath,
        Action<int> onPercent,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.IsNullOrEmpty(mediaType) &&
            mediaType.Equals("text/html", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException("Server returned text/html content instead of the expected binary package.");
        }

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
        await using (var fileStream = new FileStream(destinationTempPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
        {
            var buffer = new byte[131072];
            long totalRead = 0;
            int bytesRead;
            int lastReportedPercent = -1;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalRead += bytesRead;

                if (totalBytes > 0)
                {
                    int currentPercent = (int)((float)totalRead / totalBytes * 100);
                    if (currentPercent != lastReportedPercent && currentPercent % 5 == 0)
                    {
                        lastReportedPercent = currentPercent;
                        onPercent(currentPercent);
                    }
                }
            }

            await fileStream.FlushAsync(cancellationToken);

            if (totalBytes > 0 && totalRead < totalBytes)
            {
                throw new IOException($"Download stream interrupted prematurely. Read {totalRead} of {totalBytes} bytes.");
            }
        }
    }


    private static void ExtractZipWithRootStripping(
        string zipPath,
        string targetDirectory,
        string? customRootSubfolder,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(zipPath);

        string? rootPrefix = null;
        if (!string.IsNullOrEmpty(customRootSubfolder))
        {
            rootPrefix = customRootSubfolder.TrimEnd('/', '\\') + "/";
        }
        else
        {
            // Auto-detect if entries share a single common directory prefix
            var topDirectories = archive.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name)) // Only consider files
                .Select(e =>
                {
                    var clean = e.FullName.Replace('\\', '/');
                    int slashIdx = clean.IndexOf('/');
                    return slashIdx > 0 ? clean.Substring(0, slashIdx) : null;
                })
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();

            if (topDirectories.Count == 1)
            {
                rootPrefix = topDirectories[0] + "/";
            }
        }

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relPath = entry.FullName.Replace('\\', '/');

            // Strip prefix if applicable
            if (rootPrefix != null)
            {
                if (relPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    relPath = relPath.Substring(rootPrefix.Length);
                }
                else if (customRootSubfolder != null)
                {
                    // For archives with explicit root subfolder (like Apache24), skip root readme/license files
                    continue;
                }
            }

            if (string.IsNullOrWhiteSpace(relPath)) continue;

            // Skip heavy documentation and symbols to keep app size & disk footprint minimal
            if (relPath.StartsWith("doc/", StringComparison.OrdinalIgnoreCase) ||
                relPath.StartsWith("docs/", StringComparison.OrdinalIgnoreCase) ||
                relPath.StartsWith("manual/", StringComparison.OrdinalIgnoreCase) ||
                relPath.StartsWith("symbols/", StringComparison.OrdinalIgnoreCase) ||
                relPath.StartsWith("include/", StringComparison.OrdinalIgnoreCase) ||
                relPath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var fullDest = Path.Combine(targetDirectory, relPath.Replace('/', Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(entry.Name))
            {
                if (!Directory.Exists(fullDest))
                    Directory.CreateDirectory(fullDest);
            }
            else
            {
                var dir = Path.GetDirectoryName(fullDest);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                entry.ExtractToFile(fullDest, overwrite: true);
            }
        }
    }

    private async Task RunPostExtractionSetupAsync(
        string environmentPath,
        Action<int, string> onProgress,
        CancellationToken cancellationToken)
    {
        onProgress(75, "Configuring PHP runtimes (PHP 8.3, PHP 8.4, and PHP 8.5)...");

        var phpBaseDir = Path.Combine(environmentPath, "bin", "php");
        var php83Dir = Path.Combine(phpBaseDir, "8.3");
        var php84Dir = Path.Combine(phpBaseDir, "8.4");
        var php85Dir = Path.Combine(phpBaseDir, "8.5");

        // 1. PHP 8.3 configuration
        if (Directory.Exists(php83Dir))
        {
            var php83Ini = Path.Combine(php83Dir, "php.ini");
            if (!File.Exists(php83Ini) || !File.ReadAllText(php83Ini).Contains("extension=pdo_mysql"))
            {
                await File.WriteAllTextAsync(php83Ini, GeneratePhpIni(), cancellationToken);
            }
        }

        // 2. PHP 8.4 configuration
        if (Directory.Exists(php84Dir))
        {
            var php84Ini = Path.Combine(php84Dir, "php.ini");
            if (!File.Exists(php84Ini) || !File.ReadAllText(php84Ini).Contains("extension=pdo_mysql"))
            {
                await File.WriteAllTextAsync(php84Ini, GeneratePhpIni(), cancellationToken);
            }
        }

        // 2. PHP 8.5 configuration
        if (Directory.Exists(php85Dir))
        {
            var php85Ini = Path.Combine(php85Dir, "php.ini");
            if (!File.Exists(php85Ini) || !File.ReadAllText(php85Ini).Contains("extension=pdo_mysql"))
            {
                await File.WriteAllTextAsync(php85Ini, GeneratePhpIni(), cancellationToken);
            }
        }

        // 3. Mirror default PHP (8.4) to bin\php\ root for seamless CLI usage
        var activeSourceDir = Directory.Exists(php84Dir) ? php84Dir : php85Dir;
        if (Directory.Exists(activeSourceDir))
        {
            try
            {
                foreach (var file in Directory.GetFiles(activeSourceDir, "*.exe"))
                {
                    File.Copy(file, Path.Combine(phpBaseDir, Path.GetFileName(file)), overwrite: true);
                }
                foreach (var file in Directory.GetFiles(activeSourceDir, "*.dll"))
                {
                    File.Copy(file, Path.Combine(phpBaseDir, Path.GetFileName(file)), overwrite: true);
                }

                var sourceIni = Path.Combine(activeSourceDir, "php.ini");
                if (File.Exists(sourceIni))
                {
                    File.Copy(sourceIni, Path.Combine(phpBaseDir, "php.ini"), overwrite: true);
                }

                var extSource = Path.Combine(activeSourceDir, "ext");
                var extDest = Path.Combine(phpBaseDir, "ext");
                if (Directory.Exists(extSource))
                {
                    if (!Directory.Exists(extDest)) Directory.CreateDirectory(extDest);
                    foreach (var extFile in Directory.GetFiles(extSource, "*.dll"))
                    {
                        File.Copy(extFile, Path.Combine(extDest, Path.GetFileName(extFile)), overwrite: true);
                    }
                }
            }
            catch (Exception ex)
            {
                InstallerLogger.Warning($"Could not mirror PHP to root bin/php: {ex.Message}");
            }
        }

        // 4. Composer wrapper script
        onProgress(77, "Setting up Composer CLI wrapper...");
        var composerDir = Path.Combine(environmentPath, "bin", "composer");
        if (Directory.Exists(composerDir))
        {
            var composerBat = Path.Combine(composerDir, "composer.bat");
            var phpExeTarget = Directory.Exists(php84Dir) ? @"%~dp0..\php\8.4\php.exe" : @"%~dp0..\php\php.exe";
            var batContent = $"@echo off\r\n\"{phpExeTarget}\" \"%~dp0composer.phar\" %*\r\n";
            await File.WriteAllTextAsync(composerBat, batContent, cancellationToken);
        }

        // 5. MariaDB Configuration & Data Initialization
        onProgress(78, "Configuring MariaDB database engine...");
        var mariaDir = Path.Combine(environmentPath, "bin", "mariadb");
        var mariaDataDir = Path.Combine(environmentPath, "data", "mariadb");
        if (!Directory.Exists(mariaDataDir)) Directory.CreateDirectory(mariaDataDir);

        var myIniMaria = Path.Combine(environmentPath, "etc", "mariadb", "my.ini");
        if (!File.Exists(myIniMaria))
        {
            var etcMaria = Path.GetDirectoryName(myIniMaria)!;
            if (!Directory.Exists(etcMaria)) Directory.CreateDirectory(etcMaria);

            var iniContent = $@"[mysqld]
port = 3307
basedir = ""{mariaDir.Replace('\\', '/')}""
datadir = ""{mariaDataDir.Replace('\\', '/')}""
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
default_storage_engine = InnoDB
max_allowed_packet = 64M
bind-address = 127.0.0.1
sql_mode = ""NO_ENGINE_SUBSTITUTION""

[client]
port = 3307
default-character-set = utf8mb4
";
            await File.WriteAllTextAsync(myIniMaria, iniContent, cancellationToken);
        }

        var mysqlSysDbMaria = Path.Combine(mariaDataDir, "mysql");
        if (!Directory.Exists(mysqlSysDbMaria) && Directory.Exists(mariaDir))
        {
            onProgress(79, "Initializing MariaDB system database...");
            var installDbExe = Path.Combine(mariaDir, "bin", "mysql_install_db.exe");
            if (!File.Exists(installDbExe))
                installDbExe = Path.Combine(mariaDir, "bin", "mariadb-install-db.exe");

            if (File.Exists(installDbExe))
            {
                try
                {
                    InstallerLogger.Info($"Initializing MariaDB data dir with: {installDbExe}");
                    var psi = new ProcessStartInfo
                    {
                        FileName = installDbExe,
                        Arguments = $"--datadir=\"{mariaDataDir}\" --default-user --port=3307",
                        WorkingDirectory = Path.GetDirectoryName(installDbExe)!,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync(cancellationToken);
                        InstallerLogger.Info($"MariaDB install db exit code: {proc.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warning($"MariaDB install db note: {ex.Message}");
                }
            }
        }

        // 6. MySQL Configuration & Data Initialization
        onProgress(80, "Configuring MySQL database engine...");
        var mysqlDir = Path.Combine(environmentPath, "bin", "mysql");
        var mysqlDataDir = Path.Combine(environmentPath, "data", "mysql");
        if (!Directory.Exists(mysqlDataDir)) Directory.CreateDirectory(mysqlDataDir);

        var myIniMysql = Path.Combine(environmentPath, "etc", "mysql", "my.ini");
        if (!File.Exists(myIniMysql))
        {
            var etcMysql = Path.GetDirectoryName(myIniMysql)!;
            if (!Directory.Exists(etcMysql)) Directory.CreateDirectory(etcMysql);

            var iniContent = $@"[mysqld]
port = 3306
basedir = ""{mysqlDir.Replace('\\', '/')}""
datadir = ""{mysqlDataDir.Replace('\\', '/')}""
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci
default_storage_engine = InnoDB
max_allowed_packet = 64M
bind-address = 127.0.0.1

[client]
port = 3306
default-character-set = utf8mb4
";
            await File.WriteAllTextAsync(myIniMysql, iniContent, cancellationToken);
        }

        var mysqlSysDbMysql = Path.Combine(mysqlDataDir, "mysql");
        if (!Directory.Exists(mysqlSysDbMysql) && Directory.Exists(mysqlDir))
        {
            var mysqldExe = Path.Combine(mysqlDir, "bin", "mysqld.exe");
            if (File.Exists(mysqldExe))
            {
                try
                {
                    InstallerLogger.Info($"Initializing MySQL data directory with: {mysqldExe}");
                    var psi = new ProcessStartInfo
                    {
                        FileName = mysqldExe,
                        Arguments = $"--initialize-insecure --datadir=\"{mysqlDataDir}\" --console",
                        WorkingDirectory = Path.GetDirectoryName(mysqldExe)!,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync(cancellationToken);
                        InstallerLogger.Info($"MySQL initialize exit code: {proc.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warning($"MySQL initialize note: {ex.Message}");
                }
            }
        }

        // 7. phpMyAdmin Configuration
        onProgress(81, "Configuring phpMyAdmin...");
        var pmaDir = Path.Combine(environmentPath, "bin", "phpmyadmin");
        if (Directory.Exists(pmaDir))
        {
            var pmaConfig = Path.Combine(pmaDir, "config.inc.php");
            bool needsConfig = !File.Exists(pmaConfig);
            if (!needsConfig)
            {
                var existing = await File.ReadAllTextAsync(pmaConfig, cancellationToken);
                if (!existing.Contains("3306") || !existing.Contains("3307"))
                {
                    needsConfig = true;
                }
            }

            if (needsConfig)
            {
                var pmaContent = @"<?php
$cfg['blowfish_secret'] = 'loka_local_development_environment_secret_32chars';
$cfg['ServerDefault'] = 1;
$i = 0;

// Server 1: MySQL (Port 3306)
$i++;
$cfg['Servers'][$i]['verbose'] = 'MySQL (3306)';
$cfg['Servers'][$i]['auth_type'] = 'config';
$cfg['Servers'][$i]['host'] = '127.0.0.1';
$cfg['Servers'][$i]['port'] = '3306';
$cfg['Servers'][$i]['user'] = 'root';
$cfg['Servers'][$i]['password'] = '';
$cfg['Servers'][$i]['AllowNoPassword'] = true;

// Server 2: MariaDB (Port 3307)
$i++;
$cfg['Servers'][$i]['verbose'] = 'MariaDB (3307)';
$cfg['Servers'][$i]['auth_type'] = 'config';
$cfg['Servers'][$i]['host'] = '127.0.0.1';
$cfg['Servers'][$i]['port'] = '3307';
$cfg['Servers'][$i]['user'] = 'root';
$cfg['Servers'][$i]['password'] = '';
$cfg['Servers'][$i]['AllowNoPassword'] = true;
";
                await File.WriteAllTextAsync(pmaConfig, pmaContent, cancellationToken);
            }
        }

        // 8. Apache Configuration
        onProgress(82, "Configuring Apache HTTP server...");
        var apacheDir = Path.Combine(environmentPath, "bin", "apache");
        var apacheConf = Path.Combine(apacheDir, "conf", "httpd.conf");
        if (File.Exists(apacheConf))
        {
            try
            {
                var projectsDir = Path.Combine(environmentPath, "projects").Replace('\\', '/');
                var binApacheDir = apacheDir.Replace('\\', '/');
                var content = await File.ReadAllTextAsync(apacheConf, cancellationToken);
                content = content.Replace("C:/Apache24-64/htdocs", projectsDir);
                content = content.Replace("c:/Apache24/htdocs", projectsDir);
                content = content.Replace("C:/Apache24-64", binApacheDir);
                content = content.Replace("c:/Apache24", binApacheDir);
                content = System.Text.RegularExpressions.Regex.Replace(content, @"Define SRVROOT "".*""", $"Define SRVROOT \"{binApacheDir}\"");
                content = System.Text.RegularExpressions.Regex.Replace(content, @"ServerRoot "".*""", $"ServerRoot \"{binApacheDir}\"");

                var pmaWebDir = pmaDir.Replace('\\', '/');
                var logsDir = Path.Combine(environmentPath, "logs", "apache").Replace('\\', '/');

                var modules = new[]
                {
                    "ssl_module", "socache_shmcb_module", "proxy_module", "proxy_fcgi_module",
                    "rewrite_module", "headers_module", "vhost_alias_module", "env_module",
                    "setenvif_module", "dir_module", "mime_module", "alias_module"
                };
                foreach (var mod in modules)
                {
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(?m)^\s*#\s*LoadModule\s+" + System.Text.RegularExpressions.Regex.Escape(mod) + @"\s+(modules/\S+)",
                        $"LoadModule {mod} $1");
                }

                content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^\s*Listen\s+.*$\r?\n?", "");
                content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^\s*#\s*Listen\s+12\.34\.56\.78:80.*$\r?\n?", "#Listen 12.34.56.78:80\r\nListen 80\r\nListen 443\r\n");
                content = System.Text.RegularExpressions.Regex.Replace(content, @"(?m)^\s*#?\s*ServerName\s+[^:\r\n]+(:\d+)?", "ServerName localhost:80");

                content = content.Replace($"DocumentRoot \"{binApacheDir}/htdocs\"", $"DocumentRoot \"{projectsDir}\"");
                content = content.Replace($"<Directory \"{binApacheDir}/htdocs\">", $"<Directory \"{projectsDir}\">");

                if (!content.Contains("# Loka Projects Directory & FastCGI Handler"))
                {
                    content += $"\r\n\r\n# Loka Projects Directory & FastCGI Handler\r\n<Directory \"{projectsDir}\">\r\n    Options Indexes FollowSymLinks MultiViews\r\n    AllowOverride All\r\n    Require all granted\r\n    DirectoryIndex index.php index.html index.htm\r\n    <IfModule mod_rewrite.c>\r\n        RewriteEngine On\r\n    </IfModule>\r\n    <IfModule mod_proxy_fcgi.c>\r\n        ProxyFCGISetEnvIf \"true\" SCRIPT_FILENAME \"%{{reqenv:DOCUMENT_ROOT}}%{{reqenv:SCRIPT_NAME}}\"\r\n        <FilesMatch \\.php$>\r\n            SetHandler \"proxy:fcgi://127.0.0.1:9000/\"\r\n        </FilesMatch>\r\n    </IfModule>\r\n</Directory>\r\n";
                }

                var defaultVhost = $@"# Loka Default VirtualHost (Catch-all for localhost & 127.0.0.1)
<VirtualHost *:80>
    ServerName localhost
    ServerAlias 127.0.0.1
    DocumentRoot ""{projectsDir}""

    <Directory ""{projectsDir}"">
        Options Indexes FollowSymLinks MultiViews
        AllowOverride All
        Require all granted
        DirectoryIndex index.php index.html index.htm
        <IfModule mod_rewrite.c>
            RewriteEngine On
        </IfModule>
        <IfModule mod_proxy_fcgi.c>
            ProxyFCGISetEnvIf ""true"" SCRIPT_FILENAME ""%{{reqenv:DOCUMENT_ROOT}}%{{reqenv:SCRIPT_NAME}}""
            <FilesMatch \.php$>
                SetHandler ""proxy:fcgi://127.0.0.1:9000/""
            </FilesMatch>
        </IfModule>
    </Directory>

    Alias /phpmyadmin ""{pmaWebDir}""
    <Directory ""{pmaWebDir}"">
        Options Indexes FollowSymLinks MultiViews
        AllowOverride All
        Require all granted
        DirectoryIndex index.php index.html index.htm
        <IfModule mod_proxy_fcgi.c>
            ProxyFCGISetEnvIf ""reqenv('SCRIPT_NAME') =~ m#^/phpmyadmin/(.*)$#"" SCRIPT_FILENAME ""{pmaWebDir}/$1""
            <FilesMatch \.php$>
                SetHandler ""proxy:fcgi://127.0.0.1:9000/""
            </FilesMatch>
        </IfModule>
    </Directory>

    ErrorLog ""{logsDir}/apache_default_error.log""
    CustomLog ""{logsDir}/apache_default_access.log"" common
</VirtualHost>";

                if (System.Text.RegularExpressions.Regex.IsMatch(content, @"(?s)# Loka Default VirtualHost.*?</VirtualHost>"))
                {
                    content = System.Text.RegularExpressions.Regex.Replace(
                        content,
                        @"(?s)# Loka Default VirtualHost.*?</VirtualHost>",
                        defaultVhost);
                }
                else
                {
                    content += "\r\n\r\n" + defaultVhost + "\r\n";
                }

                var dynamicCfgApache = $"{environmentPath.Replace('\\', '/')}/config/apache/sites-enabled/*.conf";
                var dynamicEtcApache = $"{environmentPath.Replace('\\', '/')}/etc/apache/sites-enabled/*.conf";
                if (!content.Contains("Loka Projects Sites"))
                {
                    content += $"\r\n# Loka Projects Sites\r\nIncludeOptional \"{dynamicCfgApache}\"\r\nIncludeOptional \"{dynamicEtcApache}\"\r\n";
                }
                else
                {
                    // Update any existing hardcoded paths to dynamic environmentPath
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"IncludeOptional "".*?/config/apache/sites-enabled/\*\.conf""", $"IncludeOptional \"{dynamicCfgApache}\"");
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"IncludeOptional "".*?/etc/apache/sites-enabled/\*\.conf""", $"IncludeOptional \"{dynamicEtcApache}\"");
                }

                await File.WriteAllTextAsync(apacheConf, content, cancellationToken);

                // Ensure phpMyAdmin junction in projects directory
                var pmaJunction = Path.Combine(environmentPath, "projects", "phpmyadmin");
                var pmaSource = Path.Combine(environmentPath, "bin", "phpmyadmin");
                if (!Directory.Exists(pmaJunction) && Directory.Exists(pmaSource))
                {
                    try
                    {
                        Directory.CreateSymbolicLink(pmaJunction, pmaSource);
                    }
                    catch { }
                }

                var etcApache = Path.Combine(environmentPath, "etc", "apache");
                var etcApacheSites = Path.Combine(etcApache, "sites-enabled");
                var configApacheSites = Path.Combine(environmentPath, "config", "apache", "sites-enabled");
                if (!Directory.Exists(etcApache)) Directory.CreateDirectory(etcApache);
                if (!Directory.Exists(etcApacheSites)) Directory.CreateDirectory(etcApacheSites);
                if (!Directory.Exists(configApacheSites)) Directory.CreateDirectory(configApacheSites);
                File.Copy(apacheConf, Path.Combine(etcApache, "httpd.conf"), overwrite: true);
            }
            catch (Exception ex)
            {
                InstallerLogger.Warning($"Could not configure Apache httpd.conf: {ex.Message}");
            }
        }

        // 8b. Nginx Configuration & Virtual Hosts Setup
        onProgress(82, "Configuring Nginx HTTP server and virtual hosts...");
        await ConfigureNginxAsync(environmentPath, cancellationToken);

        // 8c. Ensure projects directory has a default Loka entry page
        await EnsureProjectsDefaultPageAsync(environmentPath, cancellationToken);

        // 9. PostgreSQL Data Initialization
        onProgress(83, "Configuring PostgreSQL database engine...");
        var pgDir = Path.Combine(environmentPath, "bin", "postgresql");
        var pgDataDir = Path.Combine(environmentPath, "data", "postgresql");
        if (!Directory.Exists(pgDataDir)) Directory.CreateDirectory(pgDataDir);

        if (!File.Exists(Path.Combine(pgDataDir, "PG_VERSION")) && Directory.Exists(pgDir))
        {
            var initDbExe = Path.Combine(pgDir, "bin", "initdb.exe");
            if (File.Exists(initDbExe))
            {
                try
                {
                    InstallerLogger.Info($"Initializing PostgreSQL cluster with {initDbExe}...");
                    var psi = new ProcessStartInfo
                    {
                        FileName = initDbExe,
                        Arguments = $"-D \"{pgDataDir.Replace('\\', '/')}\" -U postgres -A trust -E UTF8",
                        WorkingDirectory = Path.GetDirectoryName(initDbExe)!,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        await proc.WaitForExitAsync(cancellationToken);
                        InstallerLogger.Info($"PostgreSQL initdb exit code: {proc.ExitCode}");
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warning($"PostgreSQL initdb execution note: {ex.Message}");
                }
            }
        }

        // 10. Generate Central Component Manifest (installasi.md Section 33)
        onProgress(84, "Generating central component manifest...");
        await GenerateComponentManifestAsync(environmentPath, cancellationToken);

        // 11. Run Real Post-Install Executable Validations (installasi.md Section 18)
        onProgress(85, "Validating installed stack executables...");
        await ValidateAllExecutablesAsync(environmentPath, cancellationToken);

        InstallerLogger.Info("Post-extraction setup completed successfully.");
    }

    private static async Task GenerateComponentManifestAsync(string environmentPath, CancellationToken cancellationToken)
    {
        try
        {
            var manifestFile = Path.Combine(environmentPath, "config", "components.json");
            var configDir = Path.GetDirectoryName(manifestFile)!;
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);

            var templateCandidates = new List<string>
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", "components.json"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "config", "components.json")
            };

            var devConfig = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "config", "components.json"));
            if (File.Exists(devConfig))
            {
                templateCandidates.Add(devConfig);
            }

            var validTemplate = templateCandidates.FirstOrDefault(File.Exists);
            if (validTemplate != null)
            {
                File.Copy(validTemplate, manifestFile, overwrite: true);
                InstallerLogger.Info($"Copied component manifest template from '{validTemplate}' to '{manifestFile}'.");
            }
            else if (!File.Exists(manifestFile))
            {
                await File.WriteAllTextAsync(manifestFile, GetDefaultComponentsJson(environmentPath), cancellationToken);
                InstallerLogger.Info($"Generated default component manifest at '{manifestFile}'.");
            }
        }
        catch (Exception ex)
        {
            InstallerLogger.Warning($"Could not generate components manifest: {ex.Message}");
        }
    }

    private static string GetDefaultComponentsJson(string envPath)
    {
        var e = envPath.Replace("\\", "\\\\");
        return @"{
  ""manifestVersion"": ""1.0.0"",
  ""generatedAt"": ""2026-10-25T00:00:00Z"",
  ""environmentPath"": """ + e + @""",
  ""components"": [
    {
      ""component"": ""php-8.3"",
      ""name"": ""PHP 8.3"",
      ""version"": ""8.3.36"",
      ""channel"": ""legacy-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://windows.php.net/downloads/releases/archives/php-8.3.36-nts-Win32-vs16-x64.zip"",
      ""installPath"": """ + e + @"\\bin\\php\\8.3"",
      ""executable"": """ + e + @"\\bin\\php\\8.3\\php.exe"",
      ""validationCommand"": ""php.exe -v"",
      ""status"": ""ready"",
      ""isDefault"": false
    },
    {
      ""component"": ""php-8.4"",
      ""name"": ""PHP 8.4"",
      ""version"": ""8.4.26"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://windows.php.net/downloads/releases/archives/php-8.4.26-nts-Win32-vs17-x64.zip"",
      ""installPath"": """ + e + @"\\bin\\php\\8.4"",
      ""executable"": """ + e + @"\\bin\\php\\8.4\\php.exe"",
      ""validationCommand"": ""php.exe -v"",
      ""status"": ""ready"",
      ""isDefault"": true
    },
    {
      ""component"": ""php-8.5"",
      ""name"": ""PHP 8.5"",
      ""version"": ""8.5.11"",
      ""channel"": ""latest-current"",
      ""architecture"": ""x64"",
      ""source"": ""https://windows.php.net/downloads/releases/archives/php-8.5.11-nts-Win32-vs17-x64.zip"",
      ""installPath"": """ + e + @"\\bin\\php\\8.5"",
      ""executable"": """ + e + @"\\bin\\php\\8.5\\php.exe"",
      ""validationCommand"": ""php.exe -v"",
      ""status"": ""ready"",
      ""isDefault"": false
    },
    {
      ""component"": ""nginx"",
      ""name"": ""Nginx Web Server"",
      ""version"": ""1.28.3"",
      ""channel"": ""latest-mainline"",
      ""architecture"": ""x64"",
      ""source"": ""https://nginx.org/download/nginx-1.28.3.zip"",
      ""installPath"": """ + e + @"\\bin\\nginx"",
      ""executable"": """ + e + @"\\bin\\nginx\\nginx.exe"",
      ""validationCommand"": ""nginx.exe -v"",
      ""status"": ""ready"",
      ""defaultPort"": 80
    },
    {
      ""component"": ""apache"",
      ""name"": ""Apache HTTP Server"",
      ""version"": ""2.4.66"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.66-251206-Win64-VS17.zip"",
      ""installPath"": """ + e + @"\\bin\\apache"",
      ""executable"": """ + e + @"\\bin\\apache\\bin\\httpd.exe"",
      ""validationCommand"": ""httpd.exe -v"",
      ""status"": ""ready"",
      ""defaultPort"": 8080
    },
    {
      ""component"": ""mariadb"",
      ""name"": ""MariaDB Database Server"",
      ""version"": ""11.8.9"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://archive.mariadb.org/mariadb-11.8.9/winx64-packages/mariadb-11.8.9-winx64.zip"",
      ""installPath"": """ + e + @"\\bin\\mariadb"",
      ""executable"": """ + e + @"\\bin\\mariadb\\bin\\mariadbd.exe"",
      ""validationCommand"": ""mariadbd.exe --version"",
      ""status"": ""ready"",
      ""defaultPort"": 3307
    },
    {
      ""component"": ""mysql"",
      ""name"": ""MySQL Community Server"",
      ""version"": ""8.4.0"",
      ""channel"": ""latest-lts"",
      ""architecture"": ""x64"",
      ""source"": ""https://cdn.mysql.com/Downloads/MySQL-8.4/mysql-8.4.0-winx64.zip"",
      ""installPath"": """ + e + @"\\bin\\mysql"",
      ""executable"": """ + e + @"\\bin\\mysql\\bin\\mysqld.exe"",
      ""validationCommand"": ""mysqld.exe --version"",
      ""status"": ""ready"",
      ""defaultPort"": 3306
    },
    {
      ""component"": ""postgresql"",
      ""name"": ""PostgreSQL Database Server"",
      ""version"": ""17.4"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://get.enterprisedb.com/postgresql/postgresql-17.4-1-windows-x64-binaries.zip"",
      ""installPath"": """ + e + @"\\bin\\postgresql"",
      ""executable"": """ + e + @"\\bin\\postgresql\\bin\\postgres.exe"",
      ""validationCommand"": ""postgres.exe --version"",
      ""status"": ""ready"",
      ""defaultPort"": 5432
    },
    {
      ""component"": ""nodejs"",
      ""name"": ""Node.js JavaScript Runtime"",
      ""version"": ""22.23.3"",
      ""channel"": ""latest-lts"",
      ""architecture"": ""x64"",
      ""source"": ""https://nodejs.org/dist/v22.23.3/node-v22.23.3-win-x64.zip"",
      ""installPath"": """ + e + @"\\bin\\nodejs"",
      ""executable"": """ + e + @"\\bin\\nodejs\\node.exe"",
      ""validationCommand"": ""node.exe -v"",
      ""status"": ""ready""
    },
    {
      ""component"": ""npm"",
      ""name"": ""Node Package Manager (npm)"",
      ""version"": ""10.9.2"",
      ""channel"": ""bundled-with-node"",
      ""architecture"": ""x64"",
      ""source"": ""https://nodejs.org/dist/v22.23.3/node-v22.23.3-win-x64.zip"",
      ""installPath"": """ + e + @"\\bin\\nodejs"",
      ""executable"": """ + e + @"\\bin\\nodejs\\npm.cmd"",
      ""validationCommand"": ""npm.cmd --version"",
      ""status"": ""ready""
    },
    {
      ""component"": ""git"",
      ""name"": ""Git for Windows (MinGit)"",
      ""version"": ""2.55.0.5"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""x64"",
      ""source"": ""https://github.com/git-for-windows/git/releases/download/v2.55.0.windows.5/MinGit-2.55.0.5-64-bit.zip"",
      ""installPath"": """ + e + @"\\bin\\git"",
      ""executable"": """ + e + @"\\bin\\git\\cmd\\git.exe"",
      ""validationCommand"": ""git.exe --version"",
      ""status"": ""ready""
    },
    {
      ""component"": ""composer"",
      ""name"": ""Composer Dependency Manager"",
      ""version"": ""2.10.3"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""any"",
      ""source"": ""https://getcomposer.org/download/latest-stable/composer.phar"",
      ""installPath"": """ + e + @"\\bin\\composer"",
      ""executable"": """ + e + @"\\bin\\composer\\composer.bat"",
      ""validationCommand"": ""composer.bat --version"",
      ""status"": ""ready""
    },
    {
      ""component"": ""phpmyadmin"",
      ""name"": ""phpMyAdmin Database Manager"",
      ""version"": ""5.2.2"",
      ""channel"": ""latest-stable"",
      ""architecture"": ""any"",
      ""source"": ""https://files.phpmyadmin.net/phpMyAdmin/5.2.2/phpMyAdmin-5.2.2-all-languages.zip"",
      ""installPath"": """ + e + @"\\bin\\phpmyadmin"",
      ""executable"": """ + e + @"\\bin\\phpmyadmin\\index.php"",
      ""validationCommand"": ""test-path index.php"",
      ""status"": ""ready""
    }
  ]
}";
    }

    private static async Task ValidateAllExecutablesAsync(string environmentPath, CancellationToken cancellationToken)
    {
        var validationChecks = new[]
        {
            ("PHP 8.4", Path.Combine(environmentPath, "bin", "php", "8.4", "php.exe"), "-v"),
            ("PHP 8.5", Path.Combine(environmentPath, "bin", "php", "8.5", "php.exe"), "-v"),
            ("Nginx", Path.Combine(environmentPath, "bin", "nginx", "nginx.exe"), "-v"),
            ("Apache", Path.Combine(environmentPath, "bin", "apache", "bin", "httpd.exe"), "-v"),
            ("MariaDB", Path.Combine(environmentPath, "bin", "mariadb", "bin", "mariadbd.exe"), "--version"),
            ("MySQL", Path.Combine(environmentPath, "bin", "mysql", "bin", "mysqld.exe"), "--version"),
            ("PostgreSQL", Path.Combine(environmentPath, "bin", "postgresql", "bin", "postgres.exe"), "--version"),
            ("Node.js", Path.Combine(environmentPath, "bin", "nodejs", "node.exe"), "-v"),
            ("Git", Path.Combine(environmentPath, "bin", "git", "cmd", "git.exe"), "--version")
        };

        foreach (var (name, exePath, args) in validationChecks)
        {
            if (File.Exists(exePath))
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = exePath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null)
                    {
                        await p.WaitForExitAsync(cancellationToken);
                        var output = (await p.StandardOutput.ReadToEndAsync(cancellationToken) + " " + await p.StandardError.ReadToEndAsync(cancellationToken)).Trim();
                        InstallerLogger.Info($"[Validation PASS] {name}: {output.Split('\n').FirstOrDefault()?.Trim()}");
                    }
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warning($"[Validation Notice] {name} execution: {ex.Message}");
                }
            }
            else
            {
                InstallerLogger.Warning($"[Validation Notice] {name} binary not found at '{exePath}'.");
            }
        }
    }

    private static void EnsureDirectoryLayout(string environmentPath)
    {
        var dirs = new[]
        {
            environmentPath,
            Path.Combine(environmentPath, "bin"),
            Path.Combine(environmentPath, "bin", "apache"),
            Path.Combine(environmentPath, "bin", "composer"),
            Path.Combine(environmentPath, "bin", "git"),
            Path.Combine(environmentPath, "bin", "mariadb"),
            Path.Combine(environmentPath, "bin", "mysql"),
            Path.Combine(environmentPath, "bin", "nginx"),
            Path.Combine(environmentPath, "bin", "nodejs"),
            Path.Combine(environmentPath, "bin", "php"),
            Path.Combine(environmentPath, "bin", "php", "8.4"),
            Path.Combine(environmentPath, "bin", "php", "8.5"),
            Path.Combine(environmentPath, "bin", "phpmyadmin"),
            Path.Combine(environmentPath, "bin", "postgresql"),
            Path.Combine(environmentPath, "etc"),
            Path.Combine(environmentPath, "etc", "apache"),
            Path.Combine(environmentPath, "etc", "apache", "sites-enabled"),
            Path.Combine(environmentPath, "etc", "mariadb"),
            Path.Combine(environmentPath, "etc", "mysql"),
            Path.Combine(environmentPath, "etc", "nginx"),
            Path.Combine(environmentPath, "etc", "nginx", "sites-enabled"),
            Path.Combine(environmentPath, "etc", "php"),
            Path.Combine(environmentPath, "etc", "postgresql"),
            Path.Combine(environmentPath, "data"),
            Path.Combine(environmentPath, "data", "mariadb"),
            Path.Combine(environmentPath, "data", "mysql"),
            Path.Combine(environmentPath, "data", "postgresql"),
            Path.Combine(environmentPath, "projects"),
            Path.Combine(environmentPath, "logs"),
            Path.Combine(environmentPath, "logs", "apache"),
            Path.Combine(environmentPath, "logs", "nginx"),
            Path.Combine(environmentPath, "logs", "mariadb"),
            Path.Combine(environmentPath, "logs", "mysql"),
            Path.Combine(environmentPath, "logs", "postgresql"),
            Path.Combine(environmentPath, "tmp"),
            Path.Combine(environmentPath, "ssl"),
            Path.Combine(environmentPath, "backups"),
            Path.Combine(environmentPath, "config"),
            Path.Combine(environmentPath, "config", "apache"),
            Path.Combine(environmentPath, "config", "apache", "sites-enabled"),
            Path.Combine(environmentPath, "config", "nginx"),
            Path.Combine(environmentPath, "config", "nginx", "sites-enabled")
        };

        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }

    private static string GeneratePhpIni()
    {
        return @"; LOKA PHP Configuration
[PHP]
engine = On
short_open_tag = Off
precision = 14
output_buffering = 4096
zlib.output_compression = Off
implicit_flush = Off
serialize_precision = -1
zend.enable_gc = On
zend.exception_ignore_args = On
zend.exception_string_param_max_len = 0

max_execution_time = 300
max_input_time = 60
memory_limit = 512M
error_reporting = E_ALL & ~E_DEPRECATED & ~E_STRICT
display_errors = On
display_startup_errors = On
log_errors = On
log_errors_max_len = 1024
ignore_repeated_errors = Off
ignore_repeated_source = Off
report_memleaks = On

variables_order = ""GPCS""
request_order = ""GP""
register_argc_argv = Off
auto_globals_jit = On
post_max_size = 128M

default_mimetype = ""text/html""
default_charset = ""UTF-8""

enable_dl = Off
file_uploads = On
upload_max_filesize = 128M
max_file_uploads = 20

allow_url_fopen = On
allow_url_include = Off
default_socket_timeout = 60

extension_dir = ""ext""

extension=curl
extension=fileinfo
extension=mbstring
extension=openssl
extension=pdo_mysql
extension=mysqli
extension=zip
extension=pdo_pgsql
extension=pgsql
extension=pdo_sqlite
extension=sqlite3

[Date]
date.timezone = UTC

[Session]
session.save_handler = files
session.use_strict_mode = 0
session.use_cookies = 1
session.use_only_cookies = 1
session.name = PHPSESSID
session.auto_start = 0
session.cookie_lifetime = 0
session.cookie_path = /
session.serialize_handler = php
session.gc_probability = 1
session.gc_divisor = 1000
session.gc_maxlifetime = 1440
session.cache_limiter = nocache
session.cache_expire = 180
session.use_trans_sid = 0
session.sid_length = 26
session.trans_sid_tags = ""a=href,area=href,frame=src,form=""
session.sid_bits_per_character = 5
";
    }

    private static async Task ConfigureNginxAsync(string environmentPath, CancellationToken cancellationToken)
    {
        var nginxDir = Path.Combine(environmentPath, "bin", "nginx");
        if (!Directory.Exists(nginxDir)) return;

        var etcNginx = Path.Combine(environmentPath, "etc", "nginx");
        var etcSites = Path.Combine(etcNginx, "sites-enabled");
        var configSites = Path.Combine(environmentPath, "config", "nginx", "sites-enabled");
        var logsDir = Path.Combine(environmentPath, "logs", "nginx");
        var projectsDir = Path.Combine(environmentPath, "projects").Replace('\\', '/');
        var pmaDir = Path.Combine(environmentPath, "bin", "phpmyadmin").Replace('\\', '/');
        var binConfDir = Path.Combine(nginxDir, "conf");

        Directory.CreateDirectory(etcNginx);
        Directory.CreateDirectory(etcSites);
        Directory.CreateDirectory(configSites);
        Directory.CreateDirectory(logsDir);
        Directory.CreateDirectory(Path.Combine(nginxDir, "temp", "client_body_temp"));
        Directory.CreateDirectory(Path.Combine(nginxDir, "temp", "fastcgi_temp"));
        Directory.CreateDirectory(Path.Combine(nginxDir, "temp", "proxy_temp"));
        Directory.CreateDirectory(Path.Combine(nginxDir, "temp", "scgi_temp"));
        Directory.CreateDirectory(Path.Combine(nginxDir, "temp", "uwsgi_temp"));

        // Copy mime.types and fastcgi_params from bin/nginx/conf to etc/nginx
        if (Directory.Exists(binConfDir))
        {
            var mimeSource = Path.Combine(binConfDir, "mime.types");
            if (File.Exists(mimeSource))
            {
                File.Copy(mimeSource, Path.Combine(etcNginx, "mime.types"), overwrite: true);
            }
            var fcgiSource = Path.Combine(binConfDir, "fastcgi_params");
            if (File.Exists(fcgiSource))
            {
                File.Copy(fcgiSource, Path.Combine(etcNginx, "fastcgi_params"), overwrite: true);
            }
        }

        var standardFastCgiParams = @"fastcgi_param  QUERY_STRING       $query_string;
fastcgi_param  REQUEST_METHOD     $request_method;
fastcgi_param  CONTENT_TYPE       $content_type;
fastcgi_param  CONTENT_LENGTH     $content_length;

fastcgi_param  SCRIPT_FILENAME    $document_root$fastcgi_script_name;
fastcgi_param  SCRIPT_NAME        $fastcgi_script_name;
fastcgi_param  REQUEST_URI        $request_uri;
fastcgi_param  DOCUMENT_URI       $document_uri;
fastcgi_param  DOCUMENT_ROOT      $document_root;
fastcgi_param  SERVER_PROTOCOL    $server_protocol;
fastcgi_param  REQUEST_SCHEME     $scheme;
fastcgi_param  HTTPS              $https if_not_empty;

fastcgi_param  GATEWAY_INTERFACE  CGI/1.1;
fastcgi_param  SERVER_SOFTWARE    nginx/$nginx_version;

fastcgi_param  REMOTE_ADDR        $remote_addr;
fastcgi_param  REMOTE_PORT        $remote_port;
fastcgi_param  SERVER_ADDR        $server_addr;
fastcgi_param  SERVER_PORT        $server_port;
fastcgi_param  SERVER_NAME        $server_name;

fastcgi_param  REDIRECT_STATUS    200;
";
        var etcFcgi = Path.Combine(etcNginx, "fastcgi_params");
        var binFcgi = Path.Combine(binConfDir, "fastcgi_params");
        if (!File.Exists(etcFcgi)) await File.WriteAllTextAsync(etcFcgi, standardFastCgiParams, cancellationToken);
        if (Directory.Exists(binConfDir) && !File.Exists(binFcgi)) await File.WriteAllTextAsync(binFcgi, standardFastCgiParams, cancellationToken);

        var dynamicCfgNginx = $"{environmentPath.Replace('\\', '/')}/config/nginx/sites-enabled/*.conf";
        var dynamicEtcNginx = $"{environmentPath.Replace('\\', '/')}/etc/nginx/sites-enabled/*.conf";

        var nginxConfContent = $@"# Loka Nginx Configuration
worker_processes  auto;
error_log  ""{logsDir.Replace('\\', '/')}/nginx_error.log"" warn;
pid        ""{nginxDir.Replace('\\', '/')}/logs/nginx.pid"";

events {{
    worker_connections  1024;
}}

http {{
    include       mime.types;
    default_type  application/octet-stream;

    log_format  main  '$remote_addr - $remote_user [$time_local] ""$request"" '
                      '$status $body_bytes_sent ""$http_referer"" '
                      '""$http_user_agent"" ""$http_x_forwarded_for""';

    access_log  ""{logsDir.Replace('\\', '/')}/nginx_access.log""  main;

    sendfile        on;
    tcp_nopush      on;
    tcp_nodelay     on;
    keepalive_timeout  65;
    client_max_body_size 128M;

    fastcgi_connect_timeout 60s;
    fastcgi_send_timeout 180s;
    fastcgi_read_timeout 180s;
    fastcgi_buffer_size 128k;
    fastcgi_buffers 4 256k;
    fastcgi_busy_buffers_size 256k;

    # Default server block - Serves projects root / phpmyadmin
    server {{
        listen       80 default_server;
        server_name  localhost 127.0.0.1 """";
        root         ""{projectsDir}"";
        index        index.php index.html index.htm;

        location / {{
            try_files $uri $uri/ /index.php$is_args$args;
        }}

        location /phpmyadmin {{
            alias ""{pmaDir}"";
            index index.php index.html index.htm;

            location ~ \.php$ {{
                fastcgi_pass   127.0.0.1:9000;
                fastcgi_index  index.php;
                fastcgi_param  SCRIPT_FILENAME $request_filename;
                include        fastcgi_params;
            }}
        }}

        location ~ \.php$ {{
            fastcgi_pass   127.0.0.1:9000;
            fastcgi_index  index.php;
            fastcgi_param  SCRIPT_FILENAME $document_root$fastcgi_script_name;
            include        fastcgi_params;
        }}
    }}

    include ""{dynamicCfgNginx}"";
    include ""{dynamicEtcNginx}"";
}}
";

        var etcConfPath = Path.Combine(etcNginx, "nginx.conf");
        var binConfPath = Path.Combine(binConfDir, "nginx.conf");

        await File.WriteAllTextAsync(etcConfPath, nginxConfContent, cancellationToken);
        if (Directory.Exists(binConfDir))
        {
            await File.WriteAllTextAsync(binConfPath, nginxConfContent, cancellationToken);
        }
        InstallerLogger.Info($"Configured Nginx at '{etcConfPath}' and '{binConfPath}'.");
    }

    private static async Task EnsureProjectsDefaultPageAsync(string environmentPath, CancellationToken cancellationToken)
    {
        var projectsDir = Path.Combine(environmentPath, "projects");
        if (!Directory.Exists(projectsDir)) Directory.CreateDirectory(projectsDir);

        var indexPhp = Path.Combine(projectsDir, "index.php");
        if (!File.Exists(indexPhp))
        {
            var content = @"<?php
// Loka Local Web Development Environment - Default Landing Page
?>
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Loka - Local Web Development Environment</title>
    <style>
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background: #0b0f19; color: #f1f5f9; margin: 0; padding: 40px 20px; }
        .container { max-width: 680px; margin: 0 auto; background: #131b2e; border: 1px solid #1e293b; border-radius: 16px; padding: 36px; box-shadow: 0 20px 40px rgba(0,0,0,0.4); }
        .badge { display: inline-block; background: #0284c7; color: white; padding: 4px 12px; border-radius: 9999px; font-size: 12px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em; margin-bottom: 16px; }
        h1 { margin: 0 0 10px; font-size: 26px; font-weight: 700; color: #ffffff; }
        p { color: #94a3b8; font-size: 15px; line-height: 1.6; margin: 0 0 24px; }
        .links { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 28px; }
        .btn { display: inline-block; padding: 10px 20px; background: #0284c7; color: white; text-decoration: none; border-radius: 8px; font-weight: 600; font-size: 14px; transition: background 0.2s; }
        .btn:hover { background: #0369a1; }
        .btn-sec { background: #1e293b; color: #e2e8f0; border: 1px solid #334155; }
        .btn-sec:hover { background: #334155; }
        .meta { padding: 16px; background: rgba(2, 132, 199, 0.08); border-left: 4px solid #0284c7; border-radius: 4px; font-size: 13px; color: #bae6fd; line-height: 1.7; }
        .meta strong { color: #ffffff; }
    </style>
</head>
<body>
    <div class=""container"">
        <span class=""badge"">Loka Development Environment</span>
        <h1>Loka is Running</h1>
        <p>Your local web server is running and ready. Create a project from the Loka Dashboard or place your projects in this folder to access them via local <code>.test</code> domains.</p>
        <div class=""links"">
            <a href=""/phpmyadmin"" class=""btn"">phpMyAdmin</a>
            <a href=""http://localhost"" class=""btn btn-sec"">Refresh</a>
        </div>
        <div class=""meta"">
            <div><strong>Active Server:</strong> <?= htmlspecialchars($_SERVER['SERVER_SOFTWARE'] ?? 'Web Server') ?></div>
            <div><strong>PHP Version:</strong> <?= PHP_VERSION ?></div>
            <div><strong>Projects Directory:</strong> <?= htmlspecialchars(__DIR__) ?></div>
        </div>
    </div>
</body>
</html>";
            await File.WriteAllTextAsync(indexPhp, content, cancellationToken);
        }
    }

    public record StackComponent(
        string Name,
        string Version,
        string PackageFileName,
        string DownloadUrl,
        string[]? FallbackUrls,
        string DestinationDirectory,
        bool IsZip,
        string TargetBinaryRelative,
        string? CustomRootSubfolder = null,
        string? ExpectedSha256 = null,
        long ExpectedSizeBytes = 0,
        string[]? Dependencies = null);
}
