using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;
using Microsoft.Win32;

namespace Loka.App.ViewModels;

public partial class DiagnosticsViewModel : ObservableObject
{
    private readonly IDiagnosticsService _diagnosticsService;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _summary = "Click 'Run Diagnostics' to check your environment.";

    [ObservableProperty]
    private bool _isHealthy = true;

    [ObservableProperty]
    private string _statusFeedback = string.Empty;

    public ObservableCollection<DiagnosticItem> Items { get; } = new();

    public DiagnosticsViewModel(IDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    public async Task InitializeAsync()
    {
        await RunDiagnosticsAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        await RunDiagnosticsAsync();
    }

    [RelayCommand]
    public async Task RunDiagnosticsAsync()
    {
        IsRunning = true;
        StatusFeedback = string.Empty;
        Summary = "Testing runtimes, configurations, ports, and permissions...";

        try
        {
            var results = await _diagnosticsService.RunDiagnosticsAsync();
            Items.Clear();

            int failed = 0;
            foreach (var r in results)
            {
                Items.Add(r);
                if (!r.IsPassed) failed++;
            }

            IsHealthy = (failed == 0);
            Summary = IsHealthy 
                ? "All diagnostic checks passed! Your Loka environment is fully operational."
                : $"Diagnostic complete with {failed} issue(s) detected.";
        }
        catch (Exception ex)
        {
            Summary = $"Diagnostics error: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    [RelayCommand]
    public void CopyReport()
    {
        var report = GenerateReportText();
        try
        {
            Clipboard.SetDataObject(report, true);
            StatusFeedback = "Diagnostic report copied to clipboard.";
        }
        catch (Exception ex)
        {
            StatusFeedback = $"Could not copy report: {ex.Message}";
        }
    }

    [RelayCommand]
    public void ExportReport()
    {
        var sfd = new SaveFileDialog
        {
            Title = "Export Diagnostic Report",
            FileName = $"loka-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            Filter = "Text Files (*.txt)|*.txt|Log Files (*.log)|*.log|All Files (*.*)|*.*"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(sfd.FileName, GenerateReportText(), Encoding.UTF8);
                StatusFeedback = $"Report exported to {Path.GetFileName(sfd.FileName)}.";
            }
            catch (Exception ex)
            {
                StatusFeedback = $"Export failed: {ex.Message}";
            }
        }
    }

    private string GenerateReportText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=================================================");
        sb.AppendLine("           LOKA ENVIRONMENT DIAGNOSTIC REPORT    ");
        sb.AppendLine("=================================================");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Result:    {(IsHealthy ? "PASSED (Healthy)" : "ISSUES DETECTED")}");
        sb.AppendLine($"Summary:   {Summary}");
        sb.AppendLine();
        sb.AppendLine("-------------------------------------------------");
        sb.AppendLine("COMPONENT CHECKS:");
        sb.AppendLine("-------------------------------------------------");

        foreach (var item in Items)
        {
            var statusStr = item.IsPassed ? "[OK]  " : "[FAIL]";
            sb.AppendLine($"{statusStr} {item.Title}");
            if (!string.IsNullOrEmpty(item.Details))
            {
                sb.AppendLine($"       Details: {item.Details}");
            }
            if (!item.IsPassed && !string.IsNullOrEmpty(item.RemediationSuggestion))
            {
                sb.AppendLine($"       Suggestion: {item.RemediationSuggestion}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("=================================================");
        return sb.ToString();
    }
}
