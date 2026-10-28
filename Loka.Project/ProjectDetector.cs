using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Loka.Core.Enums;
using Loka.Core.Models;

namespace Loka.Project;

public static class ProjectDetector
{
    public static ProjectInfo Detect(string directoryPath)
    {
        var name = Path.GetFileName(directoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var domain = $"{name.ToLowerInvariant().Replace(' ', '-')}.test";

        var project = new ProjectInfo
        {
            Name = name,
            Path = directoryPath,
            Domain = domain,
            DocumentRoot = directoryPath,
            Framework = ProjectFramework.PlainPhp,
            LastModified = Directory.GetLastWriteTime(directoryPath)
        };

        // Universal DocumentRoot resolution
        var pubDir = Path.Combine(directoryPath, "public");
        var webDir = Path.Combine(directoryPath, "web");
        var webrootDir = Path.Combine(directoryPath, "webroot");
        var distDir = Path.Combine(directoryPath, "dist");
        var buildDir = Path.Combine(directoryPath, "build");

        if (Directory.Exists(pubDir))
        {
            project.DocumentRoot = pubDir;
        }
        else if (Directory.Exists(webDir))
        {
            project.DocumentRoot = webDir;
        }
        else if (Directory.Exists(webrootDir))
        {
            project.DocumentRoot = webrootDir;
        }
        else if (Directory.Exists(distDir))
        {
            project.DocumentRoot = distDir;
        }
        else if (Directory.Exists(buildDir))
        {
            project.DocumentRoot = buildDir;
        }

        // Read composer.json if present
        var composerFile = Path.Combine(directoryPath, "composer.json");
        string composerContent = string.Empty;
        if (File.Exists(composerFile))
        {
            try
            {
                composerContent = File.ReadAllText(composerFile);
                DetectComposerPhpRequirement(directoryPath, project);
            }
            catch { }
        }

        // Read package.json if present
        var packageJson = Path.Combine(directoryPath, "package.json");
        string packageContent = string.Empty;
        if (File.Exists(packageJson))
        {
            try
            {
                packageContent = File.ReadAllText(packageJson);
                using var doc = JsonDocument.Parse(packageContent);
                if (doc.RootElement.TryGetProperty("engines", out var engines) &&
                    engines.TryGetProperty("node", out var nodeVer))
                {
                    project.NodeVersion = nodeVer.GetString() ?? "";
                }
            }
            catch { }
        }

        // 1. Detect Laravel
        if (File.Exists(Path.Combine(directoryPath, "artisan")) ||
            composerContent.Contains("\"laravel/framework\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.Laravel;
        }
        // 2. Detect Kyro Framework
        else if (File.Exists(Path.Combine(directoryPath, "kyro")) ||
                 composerContent.Contains("\"kyro\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.Kyro;
        }
        // 3. Detect WordPress
        else if (File.Exists(Path.Combine(directoryPath, "wp-config.php")) ||
                 (Directory.Exists(Path.Combine(directoryPath, "wp-admin")) && Directory.Exists(Path.Combine(directoryPath, "wp-content"))) ||
                 composerContent.Contains("\"roots/bedrock\"", StringComparison.OrdinalIgnoreCase) ||
                 composerContent.Contains("\"johnpbloch/wordpress\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.WordPress;
            // If standard WordPress (no public folder), use root
            if (!Directory.Exists(pubDir) && !Directory.Exists(webDir))
            {
                project.DocumentRoot = directoryPath;
            }
        }
        // 4. Detect Symfony
        else if (File.Exists(Path.Combine(directoryPath, "bin", "console")) ||
                 composerContent.Contains("\"symfony/framework-bundle\"", StringComparison.OrdinalIgnoreCase) ||
                 composerContent.Contains("\"symfony/symfony\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.Symfony;
        }
        // 5. Detect CodeIgniter (CI4 spark or CI3 application/config)
        else if (File.Exists(Path.Combine(directoryPath, "spark")) ||
                 composerContent.Contains("\"codeigniter4/framework\"", StringComparison.OrdinalIgnoreCase) ||
                 File.Exists(Path.Combine(directoryPath, "application", "config", "config.php")) ||
                 File.Exists(Path.Combine(directoryPath, "system", "core", "CodeIgniter.php")))
        {
            project.Framework = ProjectFramework.CodeIgniter;
            if (!Directory.Exists(pubDir))
            {
                project.DocumentRoot = directoryPath;
            }
        }
        // 6. Detect Yii2
        else if (File.Exists(Path.Combine(directoryPath, "yii")) ||
                 composerContent.Contains("\"yiisoft/yii2\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.Yii2;
        }
        // 7. Detect CakePHP
        else if (File.Exists(Path.Combine(directoryPath, "bin", "cake")) ||
                 composerContent.Contains("\"cakephp/cakephp\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.CakePhp;
        }
        // 8. Detect Slim
        else if (composerContent.Contains("\"slim/slim\"", StringComparison.OrdinalIgnoreCase))
        {
            project.Framework = ProjectFramework.Slim;
        }
        // 9. Detect Node / Frontend Frameworks
        else if (!string.IsNullOrEmpty(packageContent))
        {
            if (packageContent.Contains("\"next\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.NextJs;
            }
            else if (packageContent.Contains("\"nuxt\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.NuxtJs;
            }
            else if (packageContent.Contains("\"vite\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.Vite;
            }
            else if (packageContent.Contains("\"react\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.React;
            }
            else if (packageContent.Contains("\"vue\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.Vue;
            }
            else if (packageContent.Contains("\"express\"", StringComparison.OrdinalIgnoreCase))
            {
                project.Framework = ProjectFramework.Express;
            }
            else
            {
                project.Framework = ProjectFramework.NodeJs;
            }
        }
        // 10. Fallback PHP or Static HTML
        else if (File.Exists(Path.Combine(project.DocumentRoot, "index.php")) ||
                 File.Exists(Path.Combine(directoryPath, "index.php")) ||
                 !string.IsNullOrEmpty(composerContent))
        {
            project.Framework = ProjectFramework.PlainPhp;
        }
        else if (File.Exists(Path.Combine(project.DocumentRoot, "index.html")) ||
                 File.Exists(Path.Combine(directoryPath, "index.html")))
        {
            project.Framework = ProjectFramework.StaticHtml;
        }

        CheckGitInfo(directoryPath, project);
        return project;
    }

    private static void DetectComposerPhpRequirement(string directoryPath, ProjectInfo project)
    {
        var composerFile = Path.Combine(directoryPath, "composer.json");
        if (File.Exists(composerFile))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(composerFile));
                if (doc.RootElement.TryGetProperty("require", out var req) &&
                    req.TryGetProperty("php", out var phpVer))
                {
                    project.PhpVersion = phpVer.GetString() ?? "";
                }
            }
            catch { }
        }
    }

    private static void CheckGitInfo(string directoryPath, ProjectInfo project)
    {
        var gitDir = Path.Combine(directoryPath, ".git");
        if (!Directory.Exists(gitDir)) return;

        try
        {
            // Read HEAD
            var headFile = Path.Combine(gitDir, "HEAD");
            if (File.Exists(headFile))
            {
                var head = File.ReadAllText(headFile).Trim();
                if (head.StartsWith("ref: refs/heads/"))
                {
                    project.GitBranch = head.Substring("ref: refs/heads/".Length);
                }
                else
                {
                    project.GitBranch = head.Length > 7 ? head.Substring(0, 7) : head;
                }
                project.GitStatus = "Clean";
            }
        }
        catch { }
    }
}
