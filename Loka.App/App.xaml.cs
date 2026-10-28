using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Loka.App.ViewModels;
using Loka.App.Views;
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

namespace Loka.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private IServiceProvider? _serviceProvider;

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CreateFile(
        string fileName,
        uint desiredAccess,
        int shareMode,
        IntPtr securityAttributes,
        int creationDisposition,
        int flagsAndAttributes,
        IntPtr templateFile);

    private const uint ATTACH_PARENT_PROCESS = 0x0ffffffff;
    private const int STD_OUTPUT_HANDLE = -11;
    private const int STD_ERROR_HANDLE = -12;
    private const uint GENERIC_WRITE = 0x40000000;
    private const int FILE_SHARE_WRITE = 2;
    private const int OPEN_EXISTING = 3;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Check if invoked as CLI command (1 unified application for GUI & CLI)
        if (e.Args.Length > 0 && !e.Args.Any(a => a.Equals("--gui", StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var stdOutHandle = GetStdHandle(STD_OUTPUT_HANDLE);
                if (stdOutHandle == IntPtr.Zero || stdOutHandle == new IntPtr(-1))
                {
                    AttachConsole(ATTACH_PARENT_PROCESS);
                    stdOutHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                }

                if (stdOutHandle != IntPtr.Zero && stdOutHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdOutHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, System.Text.Encoding.UTF8) { AutoFlush = true };
                    Console.SetOut(writer);
                }

                var stdErrHandle = GetStdHandle(STD_ERROR_HANDLE);
                if (stdErrHandle == IntPtr.Zero || stdErrHandle == new IntPtr(-1))
                {
                    stdErrHandle = CreateFile("CONOUT$", GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
                }
                if (stdErrHandle != IntPtr.Zero && stdErrHandle != new IntPtr(-1))
                {
                    var safeHandle = new Microsoft.Win32.SafeHandles.SafeFileHandle(stdErrHandle, ownsHandle: false);
                    var fs = new FileStream(safeHandle, FileAccess.Write);
                    var writer = new StreamWriter(fs, System.Text.Encoding.UTF8) { AutoFlush = true };
                    Console.SetError(writer);
                }
            }
            catch { }

            int exitCode = 0;
            try
            {
                exitCode = await Loka.CLI.Program.Main(e.Args);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[Loka Error] {ex.Message}");
                Console.ResetColor();
                exitCode = 1;
            }
            finally
            {
                try { FreeConsole(); } catch { }
            }
            Environment.Exit(exitCode);
            return;
        }

        // Global Exception Handling (Section 38)
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            string detail = ex?.InnerException != null ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}" : (ex?.Message ?? "Unknown error");
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), ex?.ToString() ?? "");
            }
            catch { }
            MessageBox.Show($"Unexpected error occurred:\n{detail}", "Loka Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DateTime lastErrorDialogTime = DateTime.MinValue;
        string lastErrorMessage = string.Empty;

        DispatcherUnhandledException += (s, args) =>
        {
            var ex = args.Exception;
            string detail = ex.InnerException != null ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}" : ex.Message;
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), ex.ToString());
            }
            catch { }

            // Guard against cascading modal dialog loops (Section 1: Hentikan error dialog beruntun)
            var now = DateTime.UtcNow;
            if ((now - lastErrorDialogTime).TotalSeconds > 3 || detail != lastErrorMessage)
            {
                lastErrorDialogTime = now;
                lastErrorMessage = detail;
                MessageBox.Show($"An unexpected error occurred:\n{detail}", "Loka Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            args.Handled = true;
        };

        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Loka.Core.Models.ThreadDispatcher.DispatcherAction = action =>
        {
            var dispatcher = Current?.Dispatcher;
            if (dispatcher != null && dispatcher.Thread.IsAlive && !dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
            {
                if (dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    dispatcher.BeginInvoke(action);
                }
            }
            else
            {
                action();
            }
        };

        try
        {
            // 1. Single Instance Check (Section 44)
            try
            {
                const string mutexName = "LokaLocalDevEnvironmentMutex";
                _singleInstanceMutex = new Mutex(true, mutexName, out bool isNewInstance);
                if (!isNewInstance)
                {
                    // Verify if another Loka or Loka.App process is truly running
                    var currentPid = Environment.ProcessId;
                    var otherLokaProcs = Process.GetProcessesByName("Loka")
                        .Concat(Process.GetProcessesByName("Loka.App"))
                        .Where(p => p.Id != currentPid)
                        .ToList();

                    if (otherLokaProcs.Any())
                    {
                        MessageBox.Show("Loka is already running.", "Loka", MessageBoxButton.OK, MessageBoxImage.Information);
                        Shutdown();
                        return;
                    }
                }
            }
            catch (AbandonedMutexException)
            {
                // Abandoned mutex means previous instance terminated; safe to take over and proceed
            }
            catch
            {
                // Mutex access error fallback
            }

            // 2. Build Dependency Injection Container
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();

            // 3. Load Configuration
            var configService = _serviceProvider.GetRequiredService<IConfigurationService>();
            await configService.LoadAsync();

            // Ensure Root CA and project SSL certificates are in sync in background
            var sslService = _serviceProvider.GetRequiredService<ISslCertificateService>();
            var sslDir = Path.Combine(configService.Config.Paths.ConfigPath, "ssl");
            _ = Task.Run(async () =>
            {
                try
                {
                    await sslService.EnsureRootCertificateAuthorityAsync(sslDir);
                    await sslService.RegenerateAllCertificatesAsync(sslDir);
                }
                catch { }
            });

            // Ensure environment directories silently (Fitur.md Section 1)
            configService.EnsureDirectories();

            // First-run Environment Setup or manual reconfiguration (Ui.md Section 3.C)
            if (!configService.Config.General.IsFirstRunCompleted || e.Args.Any(a => a.Equals("--setup-wizard", StringComparison.OrdinalIgnoreCase)))
            {
                var setupWizard = _serviceProvider.GetRequiredService<SetupWizardWindow>();
                setupWizard.ShowDialog();
                if (!configService.Config.General.IsFirstRunCompleted)
                {
                    // User closed setup wizard without completing or applying
                    Shutdown();
                    return;
                }
            }

            // Optional explicit flags for manual maintenance if requested via CLI
            if (e.Args.Any(a => a.Equals("--show-patch-manager", StringComparison.OrdinalIgnoreCase)))
            {
                var patchWindow = _serviceProvider.GetRequiredService<PatchManagerWindow>();
                patchWindow.ShowDialog();
            }

            // 5. Launch MainWindow directly (Fitur.md Section 1: instant direct desktop startup)
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            string detail = ex.InnerException != null ? $"{ex.Message}\n\nInner: {ex.InnerException.Message}" : ex.Message;
            try
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log"), ex.ToString());
            }
            catch { }
            MessageBox.Show($"Unexpected error occurred:\n{detail}", "Loka Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core & Infrastructure
        services.AddSingleton<ILogService, LogService>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IProcessManager, ProcessManager>();
        services.AddSingleton<IPortService, PortService>();
        services.AddSingleton<IHostsService, HostsService>();
        services.AddSingleton<ISslCertificateService, SslCertificateService>();
        services.AddSingleton<IPatchService, Loka.Infrastructure.Services.PatchService>();

        // Runtimes & Services
        services.AddSingleton<IRuntimeDetector, RuntimeDetector>();
        services.AddSingleton<IApacheService, ApacheService>();
        services.AddSingleton<INginxService, NginxService>();
        services.AddSingleton<IMySqlService, MySqlService>();
        services.AddSingleton<IPhpService, PhpService>();
        services.AddSingleton<IServiceManager, ServiceManager>();

        // Projects & Databases & Diagnostics
        services.AddSingleton<IProjectService, ProjectService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<IDiagnosticsService, DiagnosticsService>();

        // ViewModels
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ProjectsViewModel>();
        services.AddSingleton<DatabaseViewModel>();
        services.AddSingleton<RuntimesViewModel>();
        services.AddSingleton<PhpMyAdminViewModel>();
        services.AddSingleton<DiagnosticsViewModel>();
        services.AddSingleton<TerminalViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddSingleton<PatchManagerViewModel>();
        services.AddSingleton<SetupWizardViewModel>();
        services.AddSingleton<MainViewModel>();

        // Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<SetupWizardWindow>();
        services.AddTransient<PatchManagerWindow>();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();
        }
        catch { }
        base.OnExit(e);
    }
}
