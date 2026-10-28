using CommunityToolkit.Mvvm.ComponentModel;

namespace Loka.Installer.Models;

public enum StepStatus
{
    Waiting,
    InProgress,
    Completed,
    Warning,
    Failed
}

public partial class InstallationStep : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private StepStatus _status = StepStatus.Waiting;

    public string StatusIcon => Status switch
    {
        StepStatus.Completed => "✓",
        StepStatus.InProgress => "●",
        StepStatus.Warning => "⚠",
        StepStatus.Failed => "✕",
        _ => "○"
    };

    public string StatusColor => Status switch
    {
        StepStatus.Completed => "#10B981", // Green
        StepStatus.InProgress => "#2563EB", // Blue
        StepStatus.Warning => "#F59E0B",   // Orange
        StepStatus.Failed => "#EF4444",    // Red
        _ => "#9CA3AF"                     // Gray
    };

    partial void OnStatusChanged(StepStatus value)
    {
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
    }
}
