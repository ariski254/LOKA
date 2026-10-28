using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Windows;
using Loka.Installer.Services;

namespace Loka.Installer;

public partial class App : Application
{
    private static System.Threading.Mutex? _installerMutex;

    public static bool IsAdministrator()
    {
        if (!OperatingSystem.IsWindows()) return true;
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void Application_Startup(object sender, StartupEventArgs e)
    {
        // Enforce explicit shutdown until interactive UI or worker finishes
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        bool isSilent = e.Args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || 
                                       a.Equals("--provision", StringComparison.OrdinalIgnoreCase) || 
                                       a.Equals("/S", StringComparison.OrdinalIgnoreCase));

        bool isElevated = IsAdministrator();
        var processName = Environment.ProcessPath ?? "";
        InstallerLogger.Info($"[Startup] Process: '{processName}', Elevated: {isElevated}, Silent: {isSilent}, Args: '{string.Join(" ", e.Args)}'");

        // 1. Guard and enforce Administrator elevation before any administrative actions
        if (!isElevated)
        {
            if (e.Args.Any(a => a.Equals("--elevated-attempt", StringComparison.OrdinalIgnoreCase)))
            {
                InstallerLogger.Error("Administrator elevation was denied or failed on previous attempt. Aborting to prevent infinite loop.");
                Console.Error.WriteLine("[LOKA Installer Error] Administrator elevation was not granted. Aborting.");
                if (!isSilent)
                {
                    MessageBox.Show("Administrator privileges are required to install or update LOKA.\nThe operation cannot continue without Administrator permissions.",
                        "LOKA Installer - Administrator Required", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Shutdown(740); // ERROR_ELEVATION_REQUIRED
                return;
            }

            var exePath = processName;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                InstallerLogger.Error($"Cannot find installer executable to request elevation. Path: '{exePath}'");
                Console.Error.WriteLine("[LOKA Installer Error] Executable path could not be resolved.");
                Shutdown(2); // ERROR_FILE_NOT_FOUND
                return;
            }

            var elevatedArgs = new List<string>(e.Args) { "--elevated-attempt" };
            var argString = string.Join(" ", elevatedArgs.Select(a => $"\"{a}\""));

            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = argString,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? Environment.CurrentDirectory
            };

            try
            {
                using var elevatedProc = Process.Start(psi);
                if (elevatedProc != null && isSilent)
                {
                    elevatedProc.WaitForExit();
                    Shutdown(elevatedProc.ExitCode);
                    return;
                }
                Shutdown(0);
                return;
            }
            catch (Win32Exception win32Ex) when (win32Ex.NativeErrorCode == 1223) // ERROR_CANCELLED (User closed or clicked 'No' on UAC)
            {
                InstallerLogger.Warning("User cancelled UAC elevation consent dialog (Win32 Error 1223).");
                Console.Error.WriteLine("[LOKA Installer Warning] Administrator elevation cancelled by user.");
                if (!isSilent)
                {
                    MessageBox.Show("Administrator privileges are required to install or update LOKA.\nThe process was cancelled because elevation was declined.",
                        "LOKA Installer", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                Shutdown(1223);
                return;
            }
            catch (Win32Exception win32Ex)
            {
                InstallerLogger.Error($"Win32 error requesting elevation (Error {win32Ex.NativeErrorCode}): {win32Ex.Message}", win32Ex);
                Console.Error.WriteLine($"[LOKA Installer Error] Win32 elevation error ({win32Ex.NativeErrorCode}): {win32Ex.Message}");
                if (!isSilent)
                {
                    MessageBox.Show($"Administrator elevation failed:\n{win32Ex.Message}",
                        "LOKA Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                Shutdown(win32Ex.NativeErrorCode);
                return;
            }
            catch (Exception ex)
            {
                InstallerLogger.Error("Unexpected exception requesting elevation", ex);
                Console.Error.WriteLine($"[LOKA Installer Error] Failed requesting elevation: {ex.Message}");
                Shutdown(1);
                return;
            }
        }

        // Mutual exclusion: ensure only one installer/uninstaller runs at a time
        try
        {
            const string mutexName = "LokaInstallerExecutionMutex";
            _installerMutex = new System.Threading.Mutex(true, mutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                InstallerLogger.Warning("Another instance of LOKA Installer or Uninstaller is already running.");
                if (!isSilent)
                {
                    MessageBox.Show("Another instance of LOKA Installer is already running.\nPlease wait for it to complete or close it first.",
                        "LOKA Installer", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                Shutdown(0);
                return;
            }
        }
        catch (System.Threading.AbandonedMutexException)
        {
            // Abandoned mutex means previous instance terminated abruptly; safe to take over
        }
        catch { }

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                InstallerLogger.Error("Unhandled AppDomain Exception", ex);
                MessageBox.Show($"A fatal error occurred:\n{ex.Message}\n\nLog: {InstallerLogger.LogFilePath}",
                    "LOKA Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        DispatcherUnhandledException += (s, args) =>
        {
            var msg = args.Exception.InnerException != null 
                ? $"{args.Exception.Message}\nDetails: {args.Exception.InnerException.Message}" 
                : args.Exception.Message;
            InstallerLogger.Error("Unhandled Dispatcher Exception", args.Exception);
            MessageBox.Show($"An installer error occurred:\n{msg}\n\nLog: {InstallerLogger.LogFilePath}",
                "LOKA Installer Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        bool isUninstall = e.Args.Any(a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)) ||
                           Path.GetFileNameWithoutExtension(processName).Contains("uninstall", StringComparison.OrdinalIgnoreCase);
        bool isRepair = e.Args.Any(a => a.Equals("--repair", StringComparison.OrdinalIgnoreCase));
        bool isUpdate = e.Args.Any(a => a.Equals("--update", StringComparison.OrdinalIgnoreCase));
        string? origDir = null;

        for (int i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i].Equals("--orig-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
            {
                origDir = e.Args[i + 1];
            }
        }

        if (isSilent)
        {
            var engine = new InstallerEngine
            {
                InstallPath = @"C:\Program Files\Loka",
                EnvironmentPath = @"C:\Loka",
                CreateDesktopShortcut = true,
                CreateStartMenuShortcut = true,
                LaunchAfterInstall = false
            };
            var steps = new List<Models.InstallationStep>
            {
                new() { Title = "Checking system compatibility" },
                new() { Title = "Preparing application directory" },
                new() { Title = "Installing LOKA core files" },
                new() { Title = "Installing development stack components" },
                new() { Title = "Registering Windows application" },
                new() { Title = "Creating Windows shortcuts" },
                new() { Title = "Finalizing installation" }
            };

            Task.Run(async () =>
            {
                try
                {
                    Console.WriteLine("Starting silent installation of LOKA...");
                    await engine.InstallAsync(steps, (pct, status) =>
                    {
                        Console.WriteLine($"[{pct}%] {status}");
                        InstallerLogger.Info($"[Silent {pct}%] {status}");
                    });
                    Console.WriteLine("Silent installation completed successfully.");
                    Dispatcher.Invoke(() => Shutdown(0));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Installation failed: {ex.Message}");
                    InstallerLogger.Error("Silent installation failed", ex);
                    Dispatcher.Invoke(() => Shutdown(1));
                }
            });
            return;
        }

        if (isUninstall)
        {
            bool isTempMode = e.Args.Any(a => a.Equals("--temp-mode", StringComparison.OrdinalIgnoreCase));
            if (!isTempMode && !string.IsNullOrEmpty(processName))
            {
                try
                {
                    string tempDir = Path.Combine(Path.GetTempPath(), "Loka_Uninstaller");
                    Directory.CreateDirectory(tempDir);
                    string tempExe = Path.Combine(tempDir, "Uninstall-Loka.exe");

                    File.Copy(processName, tempExe, overwrite: true);

                    var currentDir = Path.GetDirectoryName(processName) ?? "";
                    var argsList = new List<string> { "--uninstall", "--temp-mode", $"--orig-dir \"{currentDir}\"" };
                    foreach (var arg in e.Args)
                    {
                        if (!arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) &&
                            !arg.Equals("--temp-mode", StringComparison.OrdinalIgnoreCase) &&
                            !arg.Equals("--orig-dir", StringComparison.OrdinalIgnoreCase) &&
                            !arg.Equals(origDir, StringComparison.OrdinalIgnoreCase))
                        {
                            argsList.Add(arg);
                        }
                    }

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = tempExe,
                        Arguments = string.Join(" ", argsList),
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(psi);

                    Shutdown(0);
                    return;
                }
                catch (Exception ex)
                {
                    InstallerLogger.Warning($"Could not self-relaunch uninstaller from temp: {ex.Message}");
                }
            }
        }

        var mainWindow = new MainWindow(isUninstall, origDir, isRepair, isUpdate);
        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            _installerMutex?.ReleaseMutex();
            _installerMutex?.Dispose();
        }
        catch { }
        base.OnExit(e);
    }
}
