using System.Diagnostics;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.Infrastructure.Process;

public class MonitoredProcess : IMonitoredProcess
{
    private readonly System.Diagnostics.Process _process;
    private readonly ILogService _logger;
    private readonly string _name;
    private readonly string _executablePath;
    private readonly DateTime _startTime;
    private bool _isManualStopRequested;
    private string _lastErrorOutput = string.Empty;

    public string Name => _name;
    public int ProcessId { get; private set; }
    public bool IsRunning => !_process.HasExited;
    public DateTime StartTime => _startTime;
    public string ExecutablePath => _executablePath;

    public event Action<string>? OutputReceived;
    public event Action<string>? ErrorReceived;
    public event Action<int>? Exited;
    public event EventHandler<ProcessUnexpectedExitEventArgs>? UnexpectedlyExited;

    public MonitoredProcess(string name, System.Diagnostics.Process process, string executablePath, ILogService logger)
    {
        _name = name;
        _process = process;
        _executablePath = executablePath;
        _logger = logger;
        _startTime = DateTime.Now;
        ProcessId = process.Id;

        _process.EnableRaisingEvents = true;
        _process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                OutputReceived?.Invoke(e.Data);
                _logger.LogInfo(_name, e.Data);
            }
        };

        _process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null)
            {
                _lastErrorOutput = e.Data;
                ErrorReceived?.Invoke(e.Data);

                var lower = e.Data.ToLowerInvariant();
                if (lower.Contains("error") || lower.Contains("fatal") || lower.Contains("panic") || lower.Contains("emerg"))
                {
                    _logger.LogError(_name, e.Data);
                }
                else if (lower.Contains("warn"))
                {
                    _logger.LogWarning(_name, e.Data);
                }
                else
                {
                    _logger.LogInfo(_name, e.Data);
                }
            }
        };

        _process.Exited += OnProcessExited;

        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();
    }

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var exitCode = 0;
        try
        {
            exitCode = _process.ExitCode;
        }
        catch { }

        _logger.LogWarning(_name, $"Process '{_name}' (PID: {ProcessId}) exited with code {exitCode}.");
        Exited?.Invoke(exitCode);

        if (!_isManualStopRequested)
        {
            UnexpectedlyExited?.Invoke(this, new ProcessUnexpectedExitEventArgs(_name, ProcessId, exitCode, _lastErrorOutput));
        }
    }

    public async Task KillAsync()
    {
        _isManualStopRequested = true;
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(_name, $"Error killing process '{_name}': {ex.Message}");
        }
    }

    public void MarkManualStop()
    {
        _isManualStopRequested = true;
    }
}
