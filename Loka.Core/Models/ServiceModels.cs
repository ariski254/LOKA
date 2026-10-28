using Loka.Core.Enums;

namespace Loka.Core.Models;

public class ServiceInfo : ObservableModel
{
    private ServiceType _type;
    private string _name = string.Empty;
    private ServiceState _state = ServiceState.Stopped;
    private int _port;
    private int? _processId;
    private string _version = string.Empty;
    private string _executablePath = string.Empty;
    private string _configPath = string.Empty;
    private string _logPath = string.Empty;
    private string _dataPath = string.Empty;
    private string? _lastError;
    private DateTime? _startedAt;
    private bool _autoStart;
    private bool _isBusy;
    private string? _connectionStatus;

    public ServiceType Type
    {
        get => _type;
        set => SetProperty(ref _type, value);
    }

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public ServiceState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsBusy));
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(CanRestart));
                OnPropertyChanged(nameof(ActionText));
                OnPropertyChanged(nameof(StateText));
            }
        }
    }

    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    public int? ProcessId
    {
        get => _processId;
        set => SetProperty(ref _processId, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string ConfigPath
    {
        get => _configPath;
        set => SetProperty(ref _configPath, value);
    }

    public string LogPath
    {
        get => _logPath;
        set => SetProperty(ref _logPath, value);
    }

    public string DataPath
    {
        get => _dataPath;
        set => SetProperty(ref _dataPath, value);
    }

    public string? LastError
    {
        get => _lastError;
        set => SetProperty(ref _lastError, value);
    }

    public string? ConnectionStatus
    {
        get => _connectionStatus;
        set => SetProperty(ref _connectionStatus, value);
    }

    public DateTime? StartedAt
    {
        get => _startedAt;
        set => SetProperty(ref _startedAt, value);
    }

    public bool AutoStart
    {
        get => _autoStart;
        set => SetProperty(ref _autoStart, value);
    }

    public bool IsBusy
    {
        get => _isBusy || State == ServiceState.Starting || State == ServiceState.Stopping;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(CanRestart));
                OnPropertyChanged(nameof(ActionText));
            }
        }
    }

    public bool IsRunning => State == ServiceState.Running;
    public bool CanToggle => !IsBusy;
    public bool CanRestart => State == ServiceState.Running && !IsBusy;

    public string ActionText => State switch
    {
        ServiceState.Starting => "Starting...",
        ServiceState.Stopping => "Stopping...",
        ServiceState.Running => "Stop",
        _ => "Start"
    };

    public string StateText => State.ToString();
}

public class RuntimeInfo : ObservableModel
{
    private string _name = string.Empty;
    private string _version = string.Empty;
    private string _executablePath = string.Empty;
    private string _directoryPath = string.Empty;
    private bool _isActive;
    private bool _isInstalled;
    private string _status = "Available to install";
    private bool _canInstall;
    private string _source = string.Empty;
    private IReadOnlyList<string> _availableVersions = Array.Empty<string>();

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string ExecutablePath
    {
        get => _executablePath;
        set => SetProperty(ref _executablePath, value);
    }

    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public bool CanInstall
    {
        get => _canInstall;
        set => SetProperty(ref _canInstall, value);
    }

    public string Source
    {
        get => _source;
        set => SetProperty(ref _source, value); // e.g. "Loka", "System PATH", "Laragon", "XAMPP"
    }

    public IReadOnlyList<string> AvailableVersions
    {
        get => _availableVersions;
        set => SetProperty(ref _availableVersions, value);
    }
}

public class PhpExtensionInfo : ObservableModel
{
    private string _name = string.Empty;
    private bool _isEnabled;
    private string _description = string.Empty;
    private bool _isBusy;

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (SetProperty(ref _isEnabled, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(ActionText));
            }
        }
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanToggle));
                OnPropertyChanged(nameof(ActionText));
            }
        }
    }

    public bool CanToggle => !IsBusy;
    public string StatusText => IsEnabled ? "Enabled" : "Disabled";
    public string ActionText => IsBusy ? "Updating..." : (IsEnabled ? "Disable" : "Enable");
}

public class PhpVersionItem : ObservableModel
{
    private string _version = string.Empty;
    private string _status = string.Empty;
    private string _binaryPath = string.Empty;
    private string _releaseInfo = string.Empty;
    private bool _isActive;
    private bool _isInstalled;
    private bool _canActivate;
    private bool _isBusy;

    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    public string DisplayName => $"PHP {Version}";

    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public string BinaryPath
    {
        get => _binaryPath;
        set => SetProperty(ref _binaryPath, value);
    }

    public string ReleaseInfo
    {
        get => _releaseInfo;
        set => SetProperty(ref _releaseInfo, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    public bool IsInstalled
    {
        get => _isInstalled;
        set => SetProperty(ref _isInstalled, value);
    }

    public bool CanActivate
    {
        get => _canActivate && !_isBusy;
        set => SetProperty(ref _canActivate, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanActivate));
            }
        }
    }
}
