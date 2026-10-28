using Loka.Core.Enums;
using Loka.Core.Interfaces;
using Loka.Database;
using Loka.Infrastructure.Configuration;
using Loka.Infrastructure.Diagnostics;
using Loka.Infrastructure.Logging;
using Loka.Infrastructure.Networking;
using Loka.Infrastructure.Process;
using Loka.Infrastructure.Windows;
using Loka.Project;
using Loka.Runtime.Apache;
using Loka.Runtime.Detector;
using Loka.Runtime.MySql;
using Loka.Runtime.Nginx;
using Loka.Runtime.Php;
using Loka.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Loka.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch { }

        var services = new ServiceCollection();
        ConfigureServices(services);
        var provider = services.BuildServiceProvider();

        var configService = provider.GetRequiredService<IConfigurationService>();
        await configService.LoadAsync();

        var serviceManager = provider.GetRequiredService<IServiceManager>();
        var detector = provider.GetRequiredService<IRuntimeDetector>();
        var diagnostics = provider.GetRequiredService<IDiagnosticsService>();
        var phpService = provider.GetRequiredService<IPhpService>();
        var projectService = provider.GetRequiredService<IProjectService>();
        var dbService = provider.GetRequiredService<IDatabaseService>();

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        try
        {
            switch (command)
            {
                case "status":
                    return await HandleStatusAsync(serviceManager, detector);

                case "doctor":
                    return await HandleDoctorAsync(diagnostics);

                case "start":
                    return await HandleStartAsync(serviceManager, args.Skip(1).ToArray());

                case "stop":
                    return await HandleStopAsync(serviceManager, args.Skip(1).ToArray());

                case "restart":
                    return await HandleRestartAsync(serviceManager, args.Skip(1).ToArray());

                case "php":
                    return await HandlePhpAsync(phpService, args.Skip(1).ToArray());

                case "projects":
                    return await HandleProjectsAsync(projectService);

                case "sync":
                case "sync-hosts":
                    return await HandleSyncAsync(projectService);

                case "new":
                case "create":
                    return await HandleNewProjectAsync(projectService, configService, args.Skip(1).ToArray());

                case "db":
                    return await HandleDbAsync(dbService);

                case "trust":
                case "ssl":
                    return await HandleSslAsync(provider.GetRequiredService<ISslCertificateService>(), configService, args.Skip(1).ToArray());

                default:
                    SetColor(ConsoleColor.Red);
                    Console.WriteLine($"Unknown command: {command}");
                    ResetColor();
                    PrintUsage();
                    return 1;
            }
        }
        catch (Exception ex)
        {
            SetColor(ConsoleColor.Red);
            Console.WriteLine($"Error: {ex.Message}");
            ResetColor();
            return 1;
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IPortService, PortService>();
        services.AddSingleton<IHostsService, HostsService>();
        services.AddSingleton<ISslCertificateService, SslCertificateService>();
        services.AddSingleton<IRuntimeDetector, RuntimeDetector>();
        services.AddSingleton<IApacheService, ApacheService>();
        services.AddSingleton<INginxService, NginxService>();
        services.AddSingleton<IMySqlService, MySqlService>();
        services.AddSingleton<IPhpService, PhpService>();
        services.AddSingleton<IServiceManager, ServiceManager>();
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"Loka CLI - Windows Local Development Environment Manager
Usage:
  loka status              Display status of web servers, database, and runtimes
  loka doctor              Run comprehensive system diagnostic checks
  loka start [service]     Start a service (apache, mysql, nginx, or all)
  loka stop [service]      Stop a service (apache, mysql, nginx, or all)
  loka restart [service]   Restart service(s)
  loka php [version]       View or switch active PHP version
  loka projects            List all detected development projects
  loka new [name] [fw]     Create and install a new project (e.g. loka new blog laravel)
  loka sync                Synchronize all projects, virtual hosts, and .test domains in hosts
  loka trust               Install and trust Loka Root CA in Windows, eliminate browser SSL warnings
  loka ssl [trust|status]  Manage Root CA and SSL certificates
  loka db                  List all databases
");
    }

    private static async Task<int> HandleSyncAsync(IProjectService projectService)
    {
        Console.WriteLine("\n[Loka] Synchronizing all projects, virtual hosts, and .test domains...");
        var ok = await projectService.SyncAllProjectsAsync();
        if (ok)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine("[Loka] All projects and .test domains synced successfully!\n");
            ResetColor();
            return 0;
        }
        else
        {
            SetColor(ConsoleColor.Yellow);
            Console.WriteLine("[Loka] Note: If Windows UAC prompt appeared, please grant permission, or run 'sync-hosts.bat' as Administrator.\n");
            ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleStatusAsync(IServiceManager serviceManager, IRuntimeDetector detector)
    {
        await serviceManager.RefreshAllAsync();
        var runtimes = await detector.DetectAllRuntimesAsync();

        Console.WriteLine("\n=== Web & Database Services ===");
        foreach (var svc in serviceManager.GetAllServices())
        {
            var color = svc.IsRunning ? ConsoleColor.Green : ConsoleColor.DarkGray;
            SetColor(color);
            var symbol = svc.IsRunning ? "●" : "○";
            Console.WriteLine($"  {symbol} {svc.Name,-12} {svc.State,-10} (Port: {svc.Port}{(svc.ProcessId.HasValue ? $", PID: {svc.ProcessId}" : "")})");
        }

        ResetColor();
        Console.WriteLine("\n=== Active Runtimes ===");
        foreach (var rt in runtimes)
        {
            if (rt.IsInstalled)
            {
                Console.WriteLine($"  ✓ {rt.Name,-12} {rt.Version,-12} [{rt.Source}]");
            }
            else
            {
                SetColor(ConsoleColor.DarkGray);
                Console.WriteLine($"  ○ {rt.Name,-12} Not Detected");
                ResetColor();
            }
        }
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleDoctorAsync(IDiagnosticsService diagnostics)
    {
        Console.WriteLine("\nRunning Loka Doctor...\n");
        var items = await diagnostics.RunDiagnosticsAsync();
        int failed = 0;

        foreach (var item in items)
        {
            if (item.IsPassed)
            {
                SetColor(ConsoleColor.Green);
                Console.Write("  ✓ ");
                ResetColor();
                Console.WriteLine($"{item.Title}: {item.Details}");
            }
            else
            {
                failed++;
                SetColor(ConsoleColor.Red);
                Console.Write("  ✗ ");
                ResetColor();
                Console.WriteLine($"{item.Title}: {item.Details}");
                if (!string.IsNullOrEmpty(item.RemediationSuggestion))
                {
                    SetColor(ConsoleColor.Yellow);
                    Console.WriteLine($"    Suggestion: {item.RemediationSuggestion}");
                    ResetColor();
                }
            }
        }

        Console.WriteLine();
        if (failed == 0)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine("All diagnostic checks passed! Your Loka environment is healthy.\n");
        }
        else
        {
            SetColor(ConsoleColor.Yellow);
            Console.WriteLine($"Found {failed} issue(s) that need attention.\n");
        }
        ResetColor();
        return failed == 0 ? 0 : 1;
    }

    private static async Task<int> HandleStartAsync(IServiceManager serviceManager, string[] args)
    {
        var target = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
        if (target == "all")
        {
            Console.WriteLine("Starting Apache and MySQL...");
            await serviceManager.StartServiceAsync(ServiceType.Apache);
            await serviceManager.StartServiceAsync(ServiceType.MySQL);
            return 0;
        }

        var type = ParseServiceType(target);
        if (!type.HasValue)
        {
            Console.WriteLine($"Unknown service '{target}'. Choose apache, mysql, or nginx.");
            return 1;
        }

        Console.WriteLine($"Starting {target}...");
        bool ok = await serviceManager.StartServiceAsync(type.Value);
        return ok ? 0 : 1;
    }

    private static async Task<int> HandleStopAsync(IServiceManager serviceManager, string[] args)
    {
        var target = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
        if (target == "all")
        {
            Console.WriteLine("Stopping all services...");
            await serviceManager.StopServiceAsync(ServiceType.Apache);
            await serviceManager.StopServiceAsync(ServiceType.MySQL);
            await serviceManager.StopServiceAsync(ServiceType.Nginx);
            return 0;
        }

        var type = ParseServiceType(target);
        if (!type.HasValue)
        {
            Console.WriteLine($"Unknown service '{target}'.");
            return 1;
        }

        Console.WriteLine($"Stopping {target}...");
        bool ok = await serviceManager.StopServiceAsync(type.Value);
        return ok ? 0 : 1;
    }

    private static async Task<int> HandleRestartAsync(IServiceManager serviceManager, string[] args)
    {
        var target = args.FirstOrDefault()?.ToLowerInvariant() ?? "all";
        if (target == "all")
        {
            await HandleStopAsync(serviceManager, args);
            await Task.Delay(500);
            return await HandleStartAsync(serviceManager, args);
        }

        var type = ParseServiceType(target);
        if (!type.HasValue) return 1;

        await serviceManager.RestartServiceAsync(type.Value);
        return 0;
    }

    private static async Task<int> HandlePhpAsync(IPhpService phpService, string[] args)
    {
        if (args.Length == 0)
        {
            var versions = await phpService.GetAvailablePhpVersionsAsync();
            Console.WriteLine($"Active PHP: {phpService.GetActivePhpBinary()}");
            Console.WriteLine("Available PHP Versions:");
            foreach (var v in versions)
            {
                Console.WriteLine($"  - {v}");
            }
            return 0;
        }

        var targetVer = args[0];
        Console.WriteLine($"Setting active PHP to {targetVer}...");
        bool ok = await phpService.SetActivePhpVersionAsync(targetVer);
        return ok ? 0 : 1;
    }

    private static async Task<int> HandleProjectsAsync(IProjectService projectService)
    {
        var projects = await projectService.ScanProjectsAsync();
        Console.WriteLine($"\nFound {projects.Count} project(s):\n");
        foreach (var p in projects)
        {
            Console.WriteLine($"  [{p.FrameworkName,-10}] {p.Name,-20} -> {p.LocalUrl} (Path: {p.Path})");
        }
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleDbAsync(IDatabaseService dbService)
    {
        var res = await dbService.ResolveCurrentDatabaseServerAsync();
        Console.WriteLine("\n[Loka] Database Engine Resolution & Verification");
        Console.WriteLine("==================================================");
        Console.WriteLine($"Current Server:  {res.ActiveServerName ?? "Not available / Stopped"} (Port: {res.ActivePort})");
        Console.WriteLine($"phpMyAdmin:      ServerDefault = {res.PhpMyAdminServerIndex}");
        Console.WriteLine($"Resolution:      {res.Message}");
        Console.WriteLine($"Diagnostic:      {res.DiagnosticReason}");
        Console.WriteLine($"\nMySQL Probe:     {res.MySqlStatus.StatusText} (Port: {res.MySqlStatus.ActualPort}, PID: {res.MySqlStatus.ProcessId?.ToString() ?? "None"}, Vendor: {res.MySqlStatus.DetectedVendor ?? "-"}, Ver: {res.MySqlStatus.Version})");
        Console.WriteLine($"MariaDB Probe:   {res.MariaDbStatus.StatusText} (Port: {res.MariaDbStatus.ActualPort}, PID: {res.MariaDbStatus.ProcessId?.ToString() ?? "None"}, Vendor: {res.MariaDbStatus.DetectedVendor ?? "-"}, Ver: {res.MariaDbStatus.Version})");

        var dbs = await dbService.GetDatabasesAsync();
        Console.WriteLine($"\nDatabases ({dbs.Count}):\n");
        foreach (var db in dbs)
        {
            Console.WriteLine($"  - {db.Name,-25} {db.TablesCount,4} tables  {db.FormattedSize,10}  ({db.Collation})");
        }
        Console.WriteLine();
        return 0;
    }

    private static async Task<int> HandleNewProjectAsync(IProjectService projectService, IConfigurationService configService, string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: loka new <project-name> [framework]");
            Console.WriteLine("Frameworks: Laravel, WordPress, Next.js, React, Vue, Vite, CodeIgniter, Symfony, Kyro, Plain PHP, Static HTML");
            return 1;
        }

        var name = args[0].Trim();
        var framework = args.Length > 1 ? args[1].Trim() : "Laravel";
        var clean = name.ToLowerInvariant().Replace(" ", "-");
        var domain = $"{clean}.test";
        var targetPath = Path.Combine(configService.Config.Paths.ProjectsPath, clean);

        Console.WriteLine($"\n[Loka] Creating and installing '{framework}' project: {name} ({domain})...");
        var ok = await projectService.CreateAndInstallProjectAsync(
            name,
            targetPath,
            domain,
            framework,
            configService.Config.Php.ActiveVersion,
            configService.Config.Services.PreferredWebServer,
            msg => Console.WriteLine($"  -> {msg}"));

        if (ok)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine($"\n[Loka] Project '{name}' created and installed successfully!");
            Console.WriteLine($"  URL:    http://{domain}");
            Console.WriteLine($"  Path:   {targetPath}\n");
            ResetColor();
            return 0;
        }
        else
        {
            SetColor(ConsoleColor.Red);
            Console.WriteLine($"\n[Loka] Failed to create project '{name}'.\n");
            ResetColor();
            return 1;
        }
    }

    private static async Task<int> HandleSslAsync(ISslCertificateService sslService, IConfigurationService configService, string[] args)
    {
        var sslDir = Path.Combine(configService.Config.Paths.ConfigPath, "ssl");
        var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "trust";

        Console.WriteLine("\n[Loka] SSL Certificate Authority & HTTPS Management");
        Console.WriteLine("==================================================");

        if (sub == "status")
        {
            bool trusted = sslService.IsRootCertificateTrusted(sslDir);
            if (trusted)
            {
                SetColor(ConsoleColor.Green);
                Console.WriteLine("[Loka] Root CA Status: TRUSTED in Windows Certificate Store");
                ResetColor();
            }
            else
            {
                SetColor(ConsoleColor.Red);
                Console.WriteLine("[Loka] Root CA Status: NOT TRUSTED (run 'loka trust' to install)");
                ResetColor();
            }
            return 0;
        }

        Console.WriteLine("[Loka] Ensuring Root Certificate Authority (Loka Development Root CA)...");
        await sslService.EnsureRootCertificateAuthorityAsync(sslDir);

        Console.WriteLine("[Loka] Installing Root CA to Windows Certificate Store (CurrentUser\\Root)...");
        bool ok = await sslService.TrustRootCertificateAsync(sslDir);
        bool isTrusted = sslService.IsRootCertificateTrusted(sslDir);

        if (isTrusted)
        {
            SetColor(ConsoleColor.Green);
            Console.WriteLine("[Loka] SUCCESS: Loka Root CA is installed and trusted in Windows!");
            ResetColor();
        }
        else
        {
            SetColor(ConsoleColor.Yellow);
            Console.WriteLine("[Loka] Prompt dispatched. If Windows displayed a 'Security Warning' dialog, click 'Yes' to accept.");
            ResetColor();
        }

        Console.WriteLine("[Loka] Regenerating all project SSL certificates signed by Loka Root CA...");
        int count = await sslService.RegenerateAllCertificatesAsync(sslDir);
        Console.WriteLine($"[Loka] Done! {count} project certificate(s) regenerated.\n");
        return 0;
    }

    private static ServiceType? ParseServiceType(string name) => name switch
    {
        "apache" => ServiceType.Apache,
        "mysql" => ServiceType.MySQL,
        "nginx" => ServiceType.Nginx,
        "mariadb" => ServiceType.MariaDB,
        _ => null
    };

    private static void SetColor(ConsoleColor color)
    {
        try { Console.ForegroundColor = color; } catch { }
    }

    private static void ResetColor()
    {
        try { Console.ResetColor(); } catch { }
    }
}
