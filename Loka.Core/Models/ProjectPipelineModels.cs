using Loka.Core.Enums;

namespace Loka.Core.Models;

public enum PipelineStageStatus
{
    Pending,
    Running,
    Succeeded,
    Failed,
    Skipped
}

public class ProjectCreationStage : ObservableModel
{
    private int _stageNumber;
    private string _name = string.Empty;
    private string _description = string.Empty;
    private PipelineStageStatus _status = PipelineStageStatus.Pending;
    private string _detail = string.Empty;
    private string _errorMessage = string.Empty;
    private TimeSpan _duration = TimeSpan.Zero;

    public int StageNumber
    {
        get => _stageNumber;
        set => SetProperty(ref _stageNumber, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public PipelineStageStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusDisplay));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsSucceeded));
                OnPropertyChanged(nameof(IsFailed));
            }
        }
    }

    public string Detail
    {
        get => _detail;
        set => SetProperty(ref _detail, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    public TimeSpan Duration
    {
        get => _duration;
        set => SetProperty(ref _duration, value);
    }

    public string StatusDisplay => Status switch
    {
        PipelineStageStatus.Pending => "Pending",
        PipelineStageStatus.Running => "Running...",
        PipelineStageStatus.Succeeded => "Completed",
        PipelineStageStatus.Failed => "Failed",
        PipelineStageStatus.Skipped => "Skipped",
        _ => "Unknown"
    };

    public bool IsRunning => Status == PipelineStageStatus.Running;
    public bool IsSucceeded => Status == PipelineStageStatus.Succeeded;
    public bool IsFailed => Status == PipelineStageStatus.Failed;
}

public class ProjectCreationPipelineResult
{
    public bool IsSuccess { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectPath { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public int FailedStageIndex { get; set; } = -1;
    public string ErrorMessage { get; set; } = string.Empty;
    public string LogFilePath { get; set; } = string.Empty;
    public List<ProjectCreationStage> Stages { get; set; } = new();

    public static List<ProjectCreationStage> CreateInitialStages()
    {
        return new List<ProjectCreationStage>
        {
            new() { StageNumber = 1, Name = "Validate Project Parameters", Description = "Validate project name, target path, domain, PHP version, and web server." },
            new() { StageNumber = 2, Name = "Check Prerequisites & Executables", Description = "Verify PHP CLI, Composer, web server, and runtime availability." },
            new() { StageNumber = 3, Name = "Create Framework Project", Description = "Execute official framework package installer (Composer / Package Manager)." },
            new() { StageNumber = 4, Name = "Install Required Dependencies", Description = "Resolve and install vendor dependencies without skipping." },
            new() { StageNumber = 5, Name = "Verify Dependency Installation", Description = "Verify vendor/autoload.php and core framework executable existence." },
            new() { StageNumber = 6, Name = "Generate Environment Configuration", Description = "Configure project .env with app settings, URL, and database credentials." },
            new() { StageNumber = 7, Name = "Framework Key & Initialization", Description = "Generate framework application encryption key (APP_KEY / spark init)." },
            new() { StageNumber = 8, Name = "Provision Database", Description = "Create isolated database for the project if requested." },
            new() { StageNumber = 9, Name = "Generate Virtual Host & SSL", Description = "Create Apache / Nginx virtual host configurations and local SSL certificate." },
            new() { StageNumber = 10, Name = "Validate Server Configuration", Description = "Test Apache configuration syntax before applying changes." },
            new() { StageNumber = 11, Name = "Reload Web Services", Description = "Reload web server to register virtual host domain without interruption." },
            new() { StageNumber = 12, Name = "Apply Indonesian Localization", Description = "Configure Indonesian locale resources (lang/id or app/Language/id)." },
            new() { StageNumber = 13, Name = "Run Framework Validation Checks", Description = "Execute framework CLI health checks (artisan / spark)." },
            new() { StageNumber = 14, Name = "Verify Document Root & Domain", Description = "Ensure document root resolves correctly to public directory." },
            new() { StageNumber = 15, Name = "Finalize Project Readiness", Description = "Mark project as active and accessible in Loka development environment." }
        };
    }
}
