using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Project;

public class ProjectInstaller
{
    private readonly IProcessManager _processManager;
    private readonly IConfigurationService _configService;
    private readonly IDatabaseService? _databaseService;
    private readonly ILogService _logger;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(60) };

    public ProjectInstaller(
        IProcessManager processManager,
        IConfigurationService configService,
        IDatabaseService? databaseService,
        ILogService logger)
    {
        _processManager = processManager;
        _configService = configService;
        _databaseService = databaseService;
        _logger = logger;
    }

    public async Task<bool> InstallFrameworkAsync(
        string framework,
        string targetPath,
        string projectName,
        string domain,
        string? phpVersion = null,
        Action<string>? progress = null)
    {
        _logger.LogInfo("Installer", $"Starting framework installation: '{framework}' in '{targetPath}' (PHP: {phpVersion ?? "default"})...");
        progress?.Invoke($"Preparing project directory '{projectName}'...");

        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);
        }

        var cleanDbName = Regex.Replace(projectName.ToLowerInvariant().Replace(' ', '_').Replace('-', '_'), @"[^a-z0-9_]", "");
        if (string.IsNullOrEmpty(cleanDbName)) cleanDbName = "loka_db";

        var normalizedFramework = framework?.Trim() ?? "Laravel";

        try
        {
            switch (normalizedFramework.ToLowerInvariant())
            {
                case "laravel":
                    return await InstallLaravelAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "codeigniter":
                case "codeigniter4":
                    return await InstallCodeIgniterAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "wordpress":
                    return await InstallWordPressAsync(targetPath, projectName, domain, cleanDbName, progress);

                case "symfony":
                    return await InstallSymfonyAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "kyro":
                    return await InstallKyroAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "yii2":
                    return await InstallYii2Async(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "cakephp":
                    return await InstallCakePhpAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "slim":
                    return await InstallSlimAsync(targetPath, projectName, domain, cleanDbName, phpVersion, progress);

                case "next.js":
                case "next":
                case "nextjs":
                    return await InstallNextJsAsync(targetPath, projectName, domain, progress);

                case "react":
                    return await InstallReactAsync(targetPath, projectName, domain, progress);

                case "vue":
                    return await InstallVueAsync(targetPath, projectName, domain, progress);

                case "vite":
                    return await InstallViteAsync(targetPath, projectName, domain, progress);

                case "nuxt.js":
                case "nuxt":
                case "nuxtjs":
                    return await InstallNuxtAsync(targetPath, projectName, domain, progress);

                case "node.js":
                case "nodejs":
                    return await InstallNodeJsAsync(targetPath, projectName, domain, progress);

                case "static html":
                case "html":
                case "static":
                    return await InstallStaticHtmlAsync(targetPath, projectName, domain, progress);

                case "plain php":
                case "php":
                default:
                    return await InstallPlainPhpAsync(targetPath, projectName, domain, cleanDbName, progress);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Installer", $"Failed during framework installation '{framework}'", ex);
            progress?.Invoke($"Framework installation error: {ex.Message}");
            return false;
        }
    }

    public string GetPhpExecutable(string? requestedVersion = null)
    {
        var binRoot = _configService.Config.Paths.BinPath;

        if (!string.IsNullOrWhiteSpace(requestedVersion) && !requestedVersion.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            var requestedExe = Path.Combine(binRoot, "php", requestedVersion, "php.exe");
            if (File.Exists(requestedExe)) return requestedExe;

            var phpBase = Path.Combine(binRoot, "php");
            if (Directory.Exists(phpBase))
            {
                var match = Directory.GetDirectories(phpBase)
                    .FirstOrDefault(d => Path.GetFileName(d).StartsWith(requestedVersion, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    var exe = Path.Combine(match, "php.exe");
                    if (File.Exists(exe)) return exe;
                }
            }
        }

        var activePhp = _configService.GetActivePhpBinary();
        if (File.Exists(activePhp)) return activePhp;

        var defaultExe = Path.Combine(binRoot, "php", "8.4", "php.exe");
        if (File.Exists(defaultExe)) return defaultExe;

        var anyPhp = Directory.Exists(Path.Combine(binRoot, "php"))
            ? Directory.GetFiles(Path.Combine(binRoot, "php"), "php.exe", SearchOption.AllDirectories).FirstOrDefault()
            : null;

        return anyPhp ?? "php.exe";
    }

    public string GetComposerPharPath()
    {
        var pharPath = Path.Combine(_configService.Config.Paths.BinPath, "composer", "composer.phar");
        if (File.Exists(pharPath)) return pharPath;

        var batPath = Path.Combine(_configService.Config.Paths.BinPath, "composer", "composer.bat");
        if (File.Exists(batPath)) return batPath;

        return "composer";
    }

    public Dictionary<string, string> GetEnvironmentVariables(string? phpExe = null)
    {
        var binRoot = _configService.Config.Paths.BinPath;
        var paths = new List<string>();

        // 1. PHP binary directory
        if (!string.IsNullOrEmpty(phpExe) && File.Exists(phpExe))
        {
            var dir = Path.GetDirectoryName(phpExe);
            if (!string.IsNullOrEmpty(dir)) paths.Add(dir);
        }
        else
        {
            var active = _configService.GetActivePhpBinary();
            if (File.Exists(active))
            {
                var dir = Path.GetDirectoryName(active);
                if (!string.IsNullOrEmpty(dir)) paths.Add(dir);
            }
        }

        // 2. Composer bin
        var composerDir = Path.Combine(binRoot, "composer");
        if (Directory.Exists(composerDir)) paths.Add(composerDir);

        // 3. Node.js bin
        var nodeDir = Path.Combine(binRoot, "nodejs");
        if (Directory.Exists(nodeDir))
        {
            var sub = Directory.GetDirectories(nodeDir).FirstOrDefault() ?? nodeDir;
            paths.Add(sub);
        }

        // 4. Git bin
        var gitDir = Path.Combine(binRoot, "git");
        if (Directory.Exists(gitDir))
        {
            var cmdDir = Path.Combine(gitDir, "cmd");
            paths.Add(Directory.Exists(cmdDir) ? cmdDir : gitDir);
        }

        // 5. Database binaries
        var mysqlDir = Path.Combine(binRoot, "mysql", "bin");
        if (Directory.Exists(mysqlDir)) paths.Add(mysqlDir);

        var mariaDir = Path.Combine(binRoot, "mariadb", "bin");
        if (Directory.Exists(mariaDir)) paths.Add(mariaDir);

        var composerHome = Path.Combine(_configService.Config.Paths.RootPath, "tmp", "composer");
        try { Directory.CreateDirectory(composerHome); } catch { }

        var systemPath = Environment.GetEnvironmentVariable("PATH") ?? "";
        var mergedPath = string.Join(";", paths) + ";" + systemPath;

        return new Dictionary<string, string>
        {
            { "PATH", mergedPath },
            { "COMPOSER_HOME", composerHome },
            { "COMPOSER_NO_INTERACTION", "1" },
            { "COMPOSER_ALLOW_SUPERUSER", "1" }
        };
    }

    public async Task<ProcessExecutionResult> RunComposerAsync(
        string arguments,
        string workingDirectory,
        string? phpVersion = null,
        Action<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var phpExe = GetPhpExecutable(phpVersion);
        var composerPhar = GetComposerPharPath();
        var env = GetEnvironmentVariables(phpExe);

        string exe;
        string fullArgs;

        if (composerPhar.EndsWith(".phar", StringComparison.OrdinalIgnoreCase))
        {
            exe = phpExe;
            fullArgs = $"\"{composerPhar}\" {arguments}";
        }
        else
        {
            exe = composerPhar;
            fullArgs = arguments;
        }

        _logger.LogInfo("Composer", $"Executing: {exe} {fullArgs} in {workingDirectory}");

        return await _processManager.ExecuteAsync(
            exe,
            fullArgs,
            workingDirectory,
            env,
            cancellationToken,
            onOutputLine: line =>
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    progress?.Invoke(line.Trim());
                }
            });
    }

    public async Task TryCreateDatabaseAsync(string dbName, Action<string>? progress)
    {
        if (_databaseService != null && !string.IsNullOrWhiteSpace(dbName))
        {
            try
            {
                progress?.Invoke($"Creating database '{dbName}'...");
                await _databaseService.CreateDatabaseAsync(dbName);
                _logger.LogInfo("Installer", $"Database '{dbName}' ready for project.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Installer", $"Database setup notice for '{dbName}': {ex.Message}");
            }
        }
    }

    #region Laravel
    public async Task<bool> InstallLaravelAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing official Laravel application via Composer (this may take 1-3 minutes)...");

        // Check if project has already been installed or is empty
        var hasComposerJson = File.Exists(Path.Combine(targetPath, "composer.json"));
        var hasArtisan = File.Exists(Path.Combine(targetPath, "artisan"));

        if (!hasComposerJson || !hasArtisan)
        {
            // Prepare clean directory for create-project
            var existingFiles = Directory.GetFileSystemEntries(targetPath);
            if (existingFiles.Length > 0 && !hasComposerJson)
            {
                // Directory has non-composer leftover files; clean them to prevent Composer "directory not empty" error
                foreach (var file in Directory.GetFiles(targetPath))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(targetPath))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            var res = await RunComposerAsync("create-project laravel/laravel . --prefer-dist --no-interaction", targetPath, phpVersion, progress, cts.Token);

            if (res.ExitCode != 0)
            {
                var err = !string.IsNullOrEmpty(res.StandardError) ? res.StandardError : res.StandardOutput;
                _logger.LogError("Installer", $"Composer create-project failed with exit code {res.ExitCode}: {err}");
                progress?.Invoke($"Composer installation failed: {err}");
                return false;
            }
        }
        else
        {
            // composer.json already present; verify dependencies
            if (!File.Exists(Path.Combine(targetPath, "vendor", "autoload.php")))
            {
                progress?.Invoke("Installing vendor dependencies via Composer...");
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(6));
                var res = await RunComposerAsync("install --prefer-dist --no-interaction", targetPath, phpVersion, progress, cts.Token);
                if (res.ExitCode != 0)
                {
                    _logger.LogError("Installer", $"Composer install failed: {res.StandardError}");
                    progress?.Invoke($"Dependency installation failed: {res.StandardError}");
                    return false;
                }
            }
        }

        // Verify required Laravel core files
        var artisanPath = Path.Combine(targetPath, "artisan");
        var vendorAutoload = Path.Combine(targetPath, "vendor", "autoload.php");
        var publicIndex = Path.Combine(targetPath, "public", "index.php");

        if (!File.Exists(artisanPath) || !File.Exists(vendorAutoload) || !File.Exists(publicIndex))
        {
            _logger.LogError("Installer", "Laravel core files verification failed: artisan, vendor/autoload.php, or public/index.php is missing.");
            progress?.Invoke("Validation error: Laravel core framework files are missing after installation.");
            return false;
        }

        // Configure environment
        progress?.Invoke("Configuring Laravel environment (.env)...");
        var envFile = Path.Combine(targetPath, ".env");
        var envExample = Path.Combine(targetPath, ".env.example");

        if (!File.Exists(envFile) && File.Exists(envExample))
        {
            File.Copy(envExample, envFile, overwrite: true);
        }

        if (File.Exists(envFile))
        {
            var content = await File.ReadAllTextAsync(envFile);
            content = Regex.Replace(content, @"APP_NAME=.*", $"APP_NAME=\"{name}\"");
            content = Regex.Replace(content, @"APP_URL=.*", $"APP_URL=http://{domain}");
            
            // Set locale to Indonesian with English fallback (Agent.md Section 5)
            if (content.Contains("APP_LOCALE="))
            {
                content = Regex.Replace(content, @"APP_LOCALE=.*", "APP_LOCALE=id");
            }
            else
            {
                content += "\nAPP_LOCALE=id\nAPP_FALLBACK_LOCALE=en\n";
            }

            content = Regex.Replace(content, @"#?\s*DB_CONNECTION=.*", "DB_CONNECTION=mysql");
            content = Regex.Replace(content, @"#?\s*DB_HOST=.*", "DB_HOST=127.0.0.1");
            content = Regex.Replace(content, @"#?\s*DB_PORT=.*", "DB_PORT=3306");
            content = Regex.Replace(content, @"#?\s*DB_DATABASE=.*", $"DB_DATABASE={dbName}");
            content = Regex.Replace(content, @"#?\s*DB_USERNAME=.*", "DB_USERNAME=root");
            content = Regex.Replace(content, @"#?\s*DB_PASSWORD=.*", "DB_PASSWORD=");
            await File.WriteAllTextAsync(envFile, content);
        }

        // Generate application key
        progress?.Invoke("Generating Laravel application key (APP_KEY)...");
        try
        {
            var phpExe = GetPhpExecutable(phpVersion);
            var env = GetEnvironmentVariables(phpExe);
            await _processManager.ExecuteAsync(phpExe, "artisan key:generate --force --ansi", targetPath, env);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Installer", $"Key generation notice: {ex.Message}");
        }

        // Indonesian Localization Scaffolding (Agent.md Section 5)
        progress?.Invoke("Scaffolding Indonesian localization resources (lang/id)...");
        await LocalizationScaffolder.ScaffoldLaravelIndonesianLocaleAsync(targetPath);

        _logger.LogInfo("Installer", "Laravel successfully installed and localized.");
        progress?.Invoke($"Laravel project '{name}' successfully installed and ready!");
        return true;
    }
    #endregion

    #region CodeIgniter 4
    public async Task<bool> InstallCodeIgniterAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing official CodeIgniter 4 application via Composer...");

        var hasSpark = File.Exists(Path.Combine(targetPath, "spark"));
        var hasAutoload = File.Exists(Path.Combine(targetPath, "vendor", "autoload.php"));

        if (!hasSpark || !hasAutoload)
        {
            var existingFiles = Directory.GetFileSystemEntries(targetPath);
            if (existingFiles.Length > 0 && !hasSpark)
            {
                foreach (var file in Directory.GetFiles(targetPath))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var dir in Directory.GetDirectories(targetPath))
                {
                    try { Directory.Delete(dir, recursive: true); } catch { }
                }
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            var res = await RunComposerAsync("create-project codeigniter4/appstarter . --prefer-dist --no-interaction", targetPath, phpVersion, progress, cts.Token);

            if (res.ExitCode != 0)
            {
                var err = !string.IsNullOrEmpty(res.StandardError) ? res.StandardError : res.StandardOutput;
                _logger.LogError("Installer", $"Composer create-project for CodeIgniter 4 failed with exit code {res.ExitCode}: {err}");
                progress?.Invoke($"CodeIgniter 4 installation failed: {err}");
                return false;
            }
        }

        var sparkFile = Path.Combine(targetPath, "spark");
        var vendorFile = Path.Combine(targetPath, "vendor", "autoload.php");
        var indexFile = Path.Combine(targetPath, "public", "index.php");

        if (!File.Exists(sparkFile) || !File.Exists(vendorFile) || !File.Exists(indexFile))
        {
            _logger.LogError("Installer", "CodeIgniter 4 core verification failed: spark, vendor/autoload.php, or public/index.php is missing.");
            progress?.Invoke("Validation error: CodeIgniter 4 core framework files are missing after installation.");
            return false;
        }

        // Environment Configuration
        progress?.Invoke("Configuring CodeIgniter 4 environment (.env)...");
        var envFile = Path.Combine(targetPath, ".env");
        var sampleEnv = Path.Combine(targetPath, "env");

        if (!File.Exists(envFile) && File.Exists(sampleEnv))
        {
            File.Copy(sampleEnv, envFile, overwrite: true);
        }

        if (File.Exists(envFile))
        {
            var content = await File.ReadAllTextAsync(envFile);
            content = Regex.Replace(content, @"#?\s*CI_ENVIRONMENT\s*=.*", "CI_ENVIRONMENT = development");
            content = Regex.Replace(content, @"#?\s*app\.baseURL\s*=.*", $"app.baseURL = 'http://{domain}/'");
            content = Regex.Replace(content, @"#?\s*app\.defaultLocale\s*=.*", "app.defaultLocale = 'id'");
            content = Regex.Replace(content, @"#?\s*database\.default\.hostname\s*=.*", "database.default.hostname = 127.0.0.1");
            content = Regex.Replace(content, @"#?\s*database\.default\.database\s*=.*", $"database.default.database = {dbName}");
            content = Regex.Replace(content, @"#?\s*database\.default\.username\s*=.*", "database.default.username = root");
            content = Regex.Replace(content, @"#?\s*database\.default\.password\s*=.*", "database.default.password = ''");
            content = Regex.Replace(content, @"#?\s*database\.default\.DBDriver\s*=.*", "database.default.DBDriver = MySQLi");
            content = Regex.Replace(content, @"#?\s*database\.default\.port\s*=.*", "database.default.port = 3306");
            await File.WriteAllTextAsync(envFile, content);
        }

        // Configure defaultLocale in app/Config/App.php
        var appConfigFile = Path.Combine(targetPath, "app", "Config", "App.php");
        if (File.Exists(appConfigFile))
        {
            var appConfigText = await File.ReadAllTextAsync(appConfigFile);
            appConfigText = Regex.Replace(appConfigText, @"public\s+string\s+\$defaultLocale\s*=.*?;", "public string $defaultLocale = 'id';");
            await File.WriteAllTextAsync(appConfigFile, appConfigText);
        }

        // Indonesian Localization Scaffolding (Agent.md Section 5)
        progress?.Invoke("Scaffolding Indonesian localization resources (app/Language/id)...");
        await LocalizationScaffolder.ScaffoldCodeIgniterIndonesianLocaleAsync(targetPath);

        _logger.LogInfo("Installer", "CodeIgniter 4 successfully installed and localized.");
        progress?.Invoke($"CodeIgniter 4 project '{name}' successfully installed and ready!");
        return true;
    }
    #endregion

    #region Symfony
    private async Task<bool> InstallSymfonyAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing Symfony skeleton via Composer...");

        var hasConsole = File.Exists(Path.Combine(targetPath, "bin", "console"));
        if (!hasConsole)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
            var res = await RunComposerAsync("create-project symfony/skeleton . --no-interaction", targetPath, phpVersion, progress, cts.Token);
            if (res.ExitCode != 0)
            {
                _logger.LogError("Installer", $"Composer create-project for Symfony failed: {res.StandardError}");
                progress?.Invoke($"Symfony installation failed: {res.StandardError}");
                return false;
            }
        }

        return true;
    }
    #endregion

    #region Kyro Framework
    private async Task<bool> InstallKyroAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Downloading Kyro Framework from GitHub repository...");

        bool installed = false;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
            var env = GetEnvironmentVariables(GetPhpExecutable(phpVersion));
            var res = await _processManager.ExecuteAsync("git.exe", "clone --depth 1 https://github.com/ariski254/Kyro-Framework.git .", targetPath, env, cts.Token);
            if (res.ExitCode == 0 && File.Exists(Path.Combine(targetPath, "kyro")))
            {
                installed = true;
                _logger.LogInfo("Installer", "Kyro Framework cloned successfully via git.");

                var gitDir = Path.Combine(targetPath, ".git");
                if (Directory.Exists(gitDir))
                {
                    try
                    {
                        SetAttributesNormal(new DirectoryInfo(gitDir));
                        Directory.Delete(gitDir, recursive: true);
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Installer", $"Git clone notice for Kyro: {ex.Message}");
        }

        if (!installed)
        {
            progress?.Invoke("Downloading Kyro Framework zip archive from GitHub...");
            var zipPath = Path.Combine(targetPath, "kyro_temp.zip");
            try
            {
                using var response = await _httpClient.GetAsync("https://github.com/ariski254/Kyro-Framework/archive/refs/heads/main.zip", HttpCompletionOption.ResponseHeadersRead);
                if (response.IsSuccessStatusCode)
                {
                    progress?.Invoke("Extracting Kyro Framework files...");
                    await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await response.Content.CopyToAsync(fs);
                    }

                    using (var archive = ZipFile.OpenRead(zipPath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            var subPath = entry.FullName;
                            var slashIdx = subPath.IndexOf('/');
                            if (slashIdx >= 0) subPath = subPath.Substring(slashIdx + 1);
                            if (string.IsNullOrEmpty(subPath)) continue;

                            var destinationPath = Path.Combine(targetPath, subPath);
                            if (entry.FullName.EndsWith("/"))
                            {
                                Directory.CreateDirectory(destinationPath);
                            }
                            else
                            {
                                var parent = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }
                        }
                    }

                    try { File.Delete(zipPath); } catch { }
                    installed = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Installer", "Failed downloading Kyro zip from GitHub", ex);
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
                progress?.Invoke($"Kyro download failed: {ex.Message}");
                return false;
            }
        }

        if (installed)
        {
            progress?.Invoke("Configuring Kyro environment (.env)...");
            var envExample = Path.Combine(targetPath, ".env.example");
            var envFile = Path.Combine(targetPath, ".env");

            if (File.Exists(envExample) && !File.Exists(envFile))
            {
                File.Copy(envExample, envFile, overwrite: true);
            }

            if (File.Exists(envFile))
            {
                var content = await File.ReadAllTextAsync(envFile);
                content = Regex.Replace(content, @"APP_NAME=.*", $"APP_NAME=\"{name}\"");
                content = Regex.Replace(content, @"APP_URL=.*", $"APP_URL=http://{domain}");
                content = Regex.Replace(content, @"DB_NAME=.*", $"DB_NAME={dbName}");
                content = Regex.Replace(content, @"DB_USER=.*", "DB_USER=root");
                content = Regex.Replace(content, @"DB_PASS=.*", "DB_PASS=");
                content = Regex.Replace(content, @"DB_HOST=.*", "DB_HOST=127.0.0.1");
                content = Regex.Replace(content, @"DB_PORT=.*", "DB_PORT=3306");
                await File.WriteAllTextAsync(envFile, content);
            }

            progress?.Invoke("Configuring autoload and key for Kyro...");
            try
            {
                var phpExe = GetPhpExecutable(phpVersion);
                var env = GetEnvironmentVariables(phpExe);
                await RunComposerAsync("dump-autoload", targetPath, phpVersion);
                await _processManager.ExecuteAsync(phpExe, "kyro key:generate", targetPath, env);
            }
            catch { }

            return true;
        }

        return false;
    }

    private static void SetAttributesNormal(DirectoryInfo dir)
    {
        foreach (var subDir in dir.GetDirectories())
        {
            SetAttributesNormal(subDir);
            subDir.Attributes = FileAttributes.Normal;
        }
        foreach (var file in dir.GetFiles())
        {
            file.Attributes = FileAttributes.Normal;
        }
        dir.Attributes = FileAttributes.Normal;
    }
    #endregion

    #region Yii2
    private async Task<bool> InstallYii2Async(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing Yii2 Basic via Composer...");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var res = await RunComposerAsync("create-project yiisoft/yii2-app-basic . --no-interaction", targetPath, phpVersion, progress, cts.Token);
        if (res.ExitCode != 0)
        {
            _logger.LogError("Installer", $"Composer create-project for Yii2 failed: {res.StandardError}");
            progress?.Invoke($"Yii2 installation failed: {res.StandardError}");
            return false;
        }

        return true;
    }
    #endregion

    #region CakePHP
    private async Task<bool> InstallCakePhpAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing CakePHP via Composer...");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var res = await RunComposerAsync("create-project cakephp/app . --no-interaction", targetPath, phpVersion, progress, cts.Token);
        if (res.ExitCode != 0)
        {
            _logger.LogError("Installer", $"Composer create-project for CakePHP failed: {res.StandardError}");
            progress?.Invoke($"CakePHP installation failed: {res.StandardError}");
            return false;
        }

        return true;
    }
    #endregion

    #region Slim
    private async Task<bool> InstallSlimAsync(
        string targetPath,
        string name,
        string domain,
        string dbName,
        string? phpVersion,
        Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Installing Slim Framework skeleton via Composer...");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        var res = await RunComposerAsync("create-project slim/slim-skeleton . --no-interaction", targetPath, phpVersion, progress, cts.Token);
        if (res.ExitCode != 0)
        {
            _logger.LogError("Installer", $"Composer create-project for Slim failed: {res.StandardError}");
            progress?.Invoke($"Slim installation failed: {res.StandardError}");
            return false;
        }

        return true;
    }
    #endregion

    #region WordPress
    private async Task<bool> InstallWordPressAsync(string targetPath, string name, string domain, string dbName, Action<string>? progress)
    {
        await TryCreateDatabaseAsync(dbName, progress);
        progress?.Invoke("Downloading latest official WordPress package from wordpress.org...");

        var zipPath = Path.Combine(targetPath, "wordpress_temp.zip");
        try
        {
            using var response = await _httpClient.GetAsync("https://wordpress.org/latest.zip", HttpCompletionOption.ResponseHeadersRead);
            if (response.IsSuccessStatusCode)
            {
                progress?.Invoke("Extracting WordPress files...");
                await using (var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await response.Content.CopyToAsync(fs);
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries)
                    {
                        var subPath = entry.FullName;
                        if (subPath.StartsWith("wordpress/", StringComparison.OrdinalIgnoreCase))
                        {
                            subPath = subPath.Substring("wordpress/".Length);
                        }

                        if (string.IsNullOrEmpty(subPath)) continue;

                        var destinationPath = Path.Combine(targetPath, subPath);
                        if (entry.FullName.EndsWith("/"))
                        {
                            Directory.CreateDirectory(destinationPath);
                        }
                        else
                        {
                            var parent = Path.GetDirectoryName(destinationPath);
                            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                            entry.ExtractToFile(destinationPath, overwrite: true);
                        }
                    }
                }

                try { File.Delete(zipPath); } catch { }
            }
            else
            {
                _logger.LogError("Installer", $"Failed downloading WordPress: HTTP {response.StatusCode}");
                progress?.Invoke($"WordPress download failed: HTTP {response.StatusCode}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Installer", "Failed downloading WordPress", ex);
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            progress?.Invoke($"WordPress download error: {ex.Message}");
            return false;
        }

        progress?.Invoke("Configuring wp-config.php...");
        var wpConfigPath = Path.Combine(targetPath, "wp-config.php");
        var sampleConfig = Path.Combine(targetPath, "wp-config-sample.php");

        if (File.Exists(sampleConfig) && !File.Exists(wpConfigPath))
        {
            var content = await File.ReadAllTextAsync(sampleConfig);
            content = content.Replace("database_name_here", dbName)
                             .Replace("username_here", "root")
                             .Replace("password_here", "")
                             .Replace("localhost", "127.0.0.1");
            await File.WriteAllTextAsync(wpConfigPath, content);
        }

        _logger.LogInfo("Installer", "WordPress installed successfully.");
        return true;
    }
    #endregion

    #region Next.js
    private async Task<bool> InstallNextJsAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Installing Next.js via create-next-app (this may take a few moments)...");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var env = GetEnvironmentVariables();
        var cmd = "npx.cmd -y create-next-app@latest . --typescript --eslint --tailwind --app --no-src-dir --import-alias \"@/*\" --use-npm --yes";
        var res = await _processManager.ExecuteAsync("cmd.exe", $"/c {cmd}", targetPath, env, cts.Token, onOutputLine: line => progress?.Invoke(line));

        if (res.ExitCode == 0 && File.Exists(Path.Combine(targetPath, "package.json")))
        {
            return true;
        }

        _logger.LogWarning("Installer", $"Next.js creation exited with code {res.ExitCode}: {res.StandardError}");
        progress?.Invoke($"Next.js installation encountered an issue: {res.StandardError}");
        return false;
    }
    #endregion

    #region React (Vite)
    private async Task<bool> InstallReactAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Installing React with Vite template...");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var env = GetEnvironmentVariables();
        var res = await _processManager.ExecuteAsync("cmd.exe", "/c npm.cmd create vite@latest . -- --template react", targetPath, env, cts.Token, onOutputLine: line => progress?.Invoke(line));
        return res.ExitCode == 0;
    }
    #endregion

    #region Vue (Vite)
    private async Task<bool> InstallVueAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Installing Vue with Vite template...");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var env = GetEnvironmentVariables();
        var res = await _processManager.ExecuteAsync("cmd.exe", "/c npm.cmd create vite@latest . -- --template vue", targetPath, env, cts.Token, onOutputLine: line => progress?.Invoke(line));
        return res.ExitCode == 0;
    }
    #endregion

    #region Vite
    private async Task<bool> InstallViteAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Installing Vite starter...");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var env = GetEnvironmentVariables();
        var res = await _processManager.ExecuteAsync("cmd.exe", "/c npm.cmd create vite@latest . -- --template vanilla", targetPath, env, cts.Token, onOutputLine: line => progress?.Invoke(line));
        return res.ExitCode == 0;
    }
    #endregion

    #region Nuxt.js
    private async Task<bool> InstallNuxtAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Installing Nuxt.js...");
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var env = GetEnvironmentVariables();
        var res = await _processManager.ExecuteAsync("cmd.exe", "/c npx.cmd -y nuxi@latest init . --package-manager npm --gitInit false", targetPath, env, cts.Token, onOutputLine: line => progress?.Invoke(line));
        return res.ExitCode == 0;
    }
    #endregion

    #region Node.js
    private static async Task<bool> InstallNodeJsAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Preparing Node.js starter application...");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "package.json"),
            $"{{\n  \"name\": \"{name.ToLowerInvariant()}\",\n  \"version\": \"1.0.0\",\n  \"description\": \"{name} Node.js app\",\n  \"main\": \"server.js\",\n  \"scripts\": {{\n    \"start\": \"node server.js\"\n  }}\n}}");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "server.js"),
            $"const http = require('http');\nconst port = process.env.PORT || 3000;\n\nconst server = http.createServer((req, res) => {{\n  res.statusCode = 200;\n  res.setHeader('Content-Type', 'text/html');\n  res.end('<h1>{name} - Node.js</h1><p>Domain: http://{domain}</p>');\n}});\n\nserver.listen(port, () => {{\n  console.log(`Server running on port ${{port}}`);\n}});\n");

        return true;
    }
    #endregion

    #region Plain PHP
    private static async Task<bool> InstallPlainPhpAsync(string targetPath, string name, string domain, string dbName, Action<string>? progress)
    {
        progress?.Invoke("Preparing PHP project...");

        var starter = $@"<?php
$name = htmlspecialchars('{name}');
$domain = htmlspecialchars('{domain}');
$dbName = htmlspecialchars('{dbName}');
?>
<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <title><?= $name ?> - PHP Application</title>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0f172a; color: #f8fafc; padding: 50px 20px; margin: 0; }}
        .card {{ max-width: 650px; margin: 0 auto; background: #1e293b; border-radius: 12px; padding: 32px; border: 1px solid #334155; }}
        h1 {{ font-size: 26px; margin: 0 0 10px; color: #38bdf8; }}
        p {{ color: #94a3b8; font-size: 14px; line-height: 1.6; }}
        .specs {{ display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin-top: 24px; }}
        .box {{ background: #0f172a; padding: 12px; border-radius: 8px; border: 1px solid #334155; }}
        .box span {{ color: #64748b; font-size: 11px; text-transform: uppercase; display: block; }}
        .box strong {{ color: #f8fafc; font-size: 13px; }}
    </style>
</head>
<body>
    <div class=""card"">
        <h1>Welcome to <?= $name ?></h1>
        <p>Your local PHP project is ready for development with Loka.</p>
        <div class=""specs"">
            <div class=""box""><span>Domain</span><strong>http://<?= $domain ?></strong></div>
            <div class=""box""><span>PHP Version</span><strong><?= phpversion() ?></strong></div>
            <div class=""box""><span>Document Root</span><strong><?= __DIR__ ?></strong></div>
            <div class=""box""><span>Server Time</span><strong><?= date('Y-m-d H:i:s') ?></strong></div>
        </div>
    </div>
</body>
</html>";

        await File.WriteAllTextAsync(Path.Combine(targetPath, "index.php"), starter);
        return true;
    }
    #endregion

    #region Static HTML
    private static async Task<bool> InstallStaticHtmlAsync(string targetPath, string name, string domain, Action<string>? progress)
    {
        progress?.Invoke("Preparing Static HTML5/CSS3/JS template...");

        Directory.CreateDirectory(Path.Combine(targetPath, "css"));
        Directory.CreateDirectory(Path.Combine(targetPath, "js"));

        await File.WriteAllTextAsync(Path.Combine(targetPath, "index.html"),
            $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{name}</title>
    <link rel=""stylesheet"" href=""css/style.css"">
</head>
<body>
    <div class=""container"">
        <h1>{name}</h1>
        <p class=""sub"">Static HTML Project running on <strong>http://{domain}</strong></p>
        <div class=""badge"">Powered by Loka</div>
    </div>
    <script src=""js/app.js""></script>
</body>
</html>");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "css", "style.css"),
            @"body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #0a0a0c; color: #ededed; margin: 0; display: flex; align-items: center; justify-content: center; min-height: 100vh; }
.container { text-align: center; padding: 40px; background: #16161a; border: 1px solid #2e2e38; border-radius: 12px; box-shadow: 0 10px 30px rgba(0,0,0,0.5); max-width: 500px; width: 100%; }
h1 { font-size: 32px; margin: 0 0 8px; color: #fff; }
p.sub { color: #9494a0; font-size: 14px; margin-bottom: 24px; }
.badge { display: inline-block; background: #2563eb; color: #fff; font-size: 12px; font-weight: 600; padding: 4px 12px; border-radius: 20px; }");

        await File.WriteAllTextAsync(Path.Combine(targetPath, "js", "app.js"),
            "console.log('Project loaded successfully with Loka.');\n");

        return true;
    }
    #endregion
}
