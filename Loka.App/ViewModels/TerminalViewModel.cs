using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Microsoft.Win32;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class TerminalViewModel : ObservableObject
{
    private readonly IProcessManager _processManager;
    private readonly IConfigurationService _configService;
    private readonly IPhpService _phpService;
    private readonly IProjectService? _projectService;
    private CancellationTokenSource? _executionCts;

    [ObservableProperty]
    private string _currentCommand = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = @"C:\Loka\projects";

    [ObservableProperty]
    private string _currentPrompt = @"PS C:\Loka\projects> ";

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private Loka.Core.Models.ProjectInfo? _selectedProject;

    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private string _draftCommand = string.Empty;

    partial void OnWorkingDirectoryChanged(string value)
    {
        CurrentPrompt = $"PS {value}> ";
    }

    public void NavigateHistory(bool up)
    {
        if (_commandHistory.Count == 0) return;

        if (up)
        {
            if (_historyIndex == -1)
            {
                _draftCommand = CurrentCommand;
                _historyIndex = _commandHistory.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }
            CurrentCommand = _commandHistory[_historyIndex];
        }
        else
        {
            if (_historyIndex >= 0)
            {
                _historyIndex++;
                if (_historyIndex >= _commandHistory.Count)
                {
                    _historyIndex = -1;
                    CurrentCommand = _draftCommand;
                }
                else
                {
                    CurrentCommand = _commandHistory[_historyIndex];
                }
            }
        }
    }

    public ObservableCollection<TerminalCommandBlock> CommandBlocks { get; } = new();
    public ObservableCollection<string> OutputLines { get; } = new();
    public ObservableCollection<Loka.Core.Models.ProjectInfo> Projects { get; } = new();

    private readonly System.Text.StringBuilder _terminalBuffer = new();

    [ObservableProperty]
    private string _terminalText = string.Empty;

    public void AppendTerminalText(string text)
    {
        void DoAppend()
        {
            if (_terminalBuffer.Length > 0 && !_terminalBuffer.ToString().EndsWith(Environment.NewLine))
            {
                _terminalBuffer.AppendLine();
            }
            _terminalBuffer.AppendLine(text);
            TerminalText = _terminalBuffer.ToString();
        }

        UiDispatcher.Run(DoAppend);
    }

    private void SafeUiAction(Action action)
    {
        UiDispatcher.Run(action);
    }

    public TerminalViewModel(
        IProcessManager processManager,
        IConfigurationService configService,
        IPhpService phpService,
        IProjectService? projectService = null)
    {
        _processManager = processManager;
        _configService = configService;
        _phpService = phpService;
        _projectService = projectService;

        if (Directory.Exists(_configService.Config.Paths.ProjectsPath))
        {
            WorkingDirectory = _configService.Config.Paths.ProjectsPath;
        }

        OutputLines.Add("Loka Developer Shell [PowerShell Engine]");
        OutputLines.Add("Environment configured with active PHP, Composer, and Node.js.");
        OutputLines.Add($"Working Directory: {WorkingDirectory}");
        OutputLines.Add("");

        AppendTerminalText("Loka Developer Shell [PowerShell Engine]");
        AppendTerminalText("Environment configured with active PHP, Composer, and Node.js.");
        AppendTerminalText($"Working Directory: {WorkingDirectory}");
        AppendTerminalText("");
    }

    partial void OnSelectedProjectChanged(Loka.Core.Models.ProjectInfo? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.Path) && Directory.Exists(value.Path))
        {
            SetWorkingDirectory(value.Path);
        }
    }

    public async Task InitializeAsync()
    {
        if (_projectService != null)
        {
            try
            {
                var list = await _projectService.ScanProjectsAsync();
                SafeUiAction(() =>
                {
                    Projects.Clear();
                    foreach (var p in list)
                    {
                        Projects.Add(p);
                    }
                });
            }
            catch { }
        }
    }

    public void SetWorkingDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            WorkingDirectory = path;
            SafeUiAction(() =>
            {
                OutputLines.Add($"Directory changed: {WorkingDirectory}");
                OutputLines.Add("");
            });
            AppendTerminalText($"Directory changed: {WorkingDirectory}");
            AppendTerminalText("");
        }
    }

    [RelayCommand]
    public void BrowseWorkingDirectory()
    {
        var fbd = new OpenFolderDialog
        {
            Title = "Select Terminal Working Directory",
            InitialDirectory = Directory.Exists(WorkingDirectory) ? WorkingDirectory : @"C:\"
        };

        if (fbd.ShowDialog() == true)
        {
            SetWorkingDirectory(fbd.FolderName);
        }
    }

    [RelayCommand]
    public async Task ExecuteCommandAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentCommand) || IsExecuting) return;

        var cmd = CurrentCommand.Trim();
        if (!string.IsNullOrWhiteSpace(cmd))
        {
            if (_commandHistory.Count == 0 || _commandHistory[^1] != cmd)
            {
                _commandHistory.Add(cmd);
            }
        }
        _historyIndex = -1;
        _draftCommand = string.Empty;
        CurrentCommand = string.Empty;

        var currentWd = Directory.Exists(WorkingDirectory) ? WorkingDirectory : @"C:\";
        var block = new TerminalCommandBlock
        {
            WorkingDirectory = currentWd,
            CommandText = cmd
        };
        SafeUiAction(() =>
        {
            CommandBlocks.Add(block);
            OutputLines.Add($"PS {currentWd}> {cmd}");
        });
        AppendTerminalText($"PS {currentWd}> {cmd}");
        IsExecuting = true;
        _executionCts = new CancellationTokenSource();

        try
        {
            var env = new Dictionary<string, string>();
            var lokaEnvPath = _configService.GetInternalEnvironmentPath();
            var existingPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            env["LOKA_ENV_PATH"] = lokaEnvPath;
            env["PATH"] = string.IsNullOrEmpty(lokaEnvPath) ? existingPath : $"{lokaEnvPath};{existingPath}";

            var escapedCmd = cmd.Replace("\"", "\\\"\"");
            var wrappedCmd = $"& {{ {escapedCmd} }}; [Console]::Out.WriteLine('__LOKA_PWD__:' + (Get-Location).ProviderPath)";

            var result = await _processManager.ExecuteAsync(
                "powershell.exe",
                $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{wrappedCmd}\"",
                workingDirectory: currentWd,
                environmentVariables: env,
                cancellationToken: _executionCts.Token);

            var rawStdOut = result.StandardOutput ?? string.Empty;
            var rawStdErr = result.StandardError ?? string.Empty;

            // Extract working directory changes from trailer
            var pwdIdx = rawStdOut.IndexOf("__LOKA_PWD__:", StringComparison.Ordinal);
            if (pwdIdx >= 0)
            {
                var extractedPwd = rawStdOut.Substring(pwdIdx + "__LOKA_PWD__:".Length).Trim();
                rawStdOut = rawStdOut.Substring(0, pwdIdx).TrimEnd();
                if (!string.IsNullOrEmpty(extractedPwd) && Directory.Exists(extractedPwd))
                {
                    WorkingDirectory = extractedPwd;
                }
            }

            block.OutputText = rawStdOut;
            block.ErrorText = rawStdErr;
            block.ExitCode = result.ExitCode;

            if (!string.IsNullOrEmpty(rawStdOut))
            {
                AppendTerminalText(rawStdOut);
                var outLines = rawStdOut.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                SafeUiAction(() =>
                {
                    foreach (var line in outLines)
                    {
                        OutputLines.Add(line);
                    }
                });
            }

            if (!string.IsNullOrEmpty(rawStdErr))
            {
                AppendTerminalText(rawStdErr);
                var errLines = rawStdErr.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                SafeUiAction(() =>
                {
                    foreach (var line in errLines)
                    {
                        // Preserve original raw line without artificial "ERR:" prefix
                        OutputLines.Add(line);
                    }
                });
            }

            if (result.ExitCode != 0)
            {
                AppendTerminalText($"[Process exited with code {result.ExitCode}]");
                SafeUiAction(() => OutputLines.Add($"[Process exited with code {result.ExitCode}]"));
            }
        }
        catch (OperationCanceledException)
        {
            block.ErrorText = "[Process terminated by user]";
            block.ExitCode = -1;
            AppendTerminalText("[Process terminated by user]");
            SafeUiAction(() => OutputLines.Add("[Process terminated by user]"));
        }
        catch (Exception ex)
        {
            block.ErrorText = $"Execution failed: {ex.Message}";
            block.ExitCode = -1;
            AppendTerminalText($"Execution failed: {ex.Message}");
            SafeUiAction(() => OutputLines.Add($"Execution failed: {ex.Message}"));
        }
        finally
        {
            _executionCts?.Dispose();
            _executionCts = null;
            IsExecuting = false;
            AppendTerminalText("");
            SafeUiAction(() => OutputLines.Add(""));
        }
    }

    [RelayCommand]
    public void StopExecution()
    {
        if (IsExecuting && _executionCts != null)
        {
            try
            {
                _executionCts.Cancel();
            }
            catch { }
        }
    }

    [RelayCommand]
    public void ClearOutput()
    {
        SafeUiAction(() =>
        {
            _terminalBuffer.Clear();
            TerminalText = string.Empty;
            CommandBlocks.Clear();
            OutputLines.Clear();
        });
    }

    [RelayCommand]
    public void CopyOutput()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var b in CommandBlocks)
        {
            sb.AppendLine(b.PromptText);
            if (!string.IsNullOrEmpty(b.FullOutput))
            {
                sb.AppendLine(b.FullOutput);
            }
            if (b.HasExitCode && b.ExitCode != 0)
            {
                sb.AppendLine($"[Process exited with code {b.ExitCode}]");
            }
            sb.AppendLine();
        }

        var all = !string.IsNullOrWhiteSpace(TerminalText) 
            ? TerminalText.TrimEnd() 
            : (sb.Length > 0 ? sb.ToString().TrimEnd() : string.Join(Environment.NewLine, OutputLines));
        if (!string.IsNullOrEmpty(all))
        {
            try
            {
                SafeUiAction(() => Clipboard.SetDataObject(all, true));
            }
            catch { }
        }
    }

    [RelayCommand]
    public async Task RunPresetAsync(string commandText)
    {
        CurrentCommand = commandText;
        await ExecuteCommandAsync();
    }
}
