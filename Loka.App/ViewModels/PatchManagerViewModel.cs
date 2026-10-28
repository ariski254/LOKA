using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.App.ViewModels;

public partial class PatchManagerViewModel : ObservableObject
{
    private readonly IPatchService _patchService;
    private CancellationTokenSource? _downloadCts;

    [ObservableProperty]
    private string _currentVersion = "1.0.0";

    [ObservableProperty]
    private string _latestVersion = "1.0.0";

    [ObservableProperty]
    private string _statusMessage = "Ready to check for updates.";

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isInstalling;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isDownloaded;

    [ObservableProperty]
    private bool _isVerified;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isLaunchAllowed = true;

    [ObservableProperty]
    private double _downloadPercentage;

    [ObservableProperty]
    private string _downloadProgressText = string.Empty;

    [ObservableProperty]
    private string _downloadSpeedText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private PatchInfo? _selectedPatch;

    public ObservableCollection<PatchInfo> AvailablePatches { get; } = new();
    public ObservableCollection<PatchInfo> InstalledPatches { get; } = new();

    public event Action? LaunchRequested;

    public PatchManagerViewModel(IPatchService patchService)
    {
        _patchService = patchService;
        CurrentVersion = _patchService.CurrentAppVersion;
        LatestVersion = _patchService.CurrentAppVersion;

        RefreshInstalledPatches();
    }

    public async Task InitializeAsync()
    {
        await CheckUpdatesAsync();
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        if (IsChecking || IsDownloading || IsInstalling) return;

        IsChecking = true;
        HasError = false;
        ErrorMessage = null;
        StatusMessage = "Checking for available patches...";

        try
        {
            var result = await _patchService.CheckForPatchesAsync();
            CurrentVersion = result.CurrentVersion;
            LatestVersion = result.LatestVersion;

            AvailablePatches.Clear();
            foreach (var patch in result.AvailablePatches)
            {
                AvailablePatches.Add(patch);
            }

            IsUpdateAvailable = result.IsUpdateAvailable;
            SelectedPatch = AvailablePatches.FirstOrDefault();

            if (IsUpdateAvailable && SelectedPatch != null)
            {
                StatusMessage = $"Patch {SelectedPatch.Version} available for download.";
                IsDownloaded = File.Exists(_patchService.GetLocalPatchFilePath(SelectedPatch));
                IsLaunchAllowed = !SelectedPatch.IsMandatory || IsInstalled;
            }
            else
            {
                StatusMessage = "Your LOKA installation is up to date.";
                IsLaunchAllowed = true;
            }

            RefreshInstalledPatches();
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Check failed: {ex.Message}";
            StatusMessage = "Could not check for updates.";
            IsLaunchAllowed = true; // allow launch if offline/network fails
        }
        finally
        {
            IsChecking = false;
        }
    }

    [RelayCommand]
    public async Task DownloadPatchAsync()
    {
        if (SelectedPatch == null || IsDownloading || IsInstalling) return;

        IsDownloading = true;
        HasError = false;
        ErrorMessage = null;
        DownloadPercentage = 0;
        DownloadProgressText = "Connecting...";
        DownloadSpeedText = string.Empty;
        StatusMessage = $"Downloading patch {SelectedPatch.Version}...";

        _downloadCts = new CancellationTokenSource();

        var progress = new Progress<PatchProgress>(p =>
        {
            DownloadPercentage = p.Percentage;
            DownloadProgressText = p.BytesProgressFormatted;
            DownloadSpeedText = p.SpeedFormatted;
            StatusMessage = p.Message;
        });

        try
        {
            bool success = await _patchService.DownloadPatchAsync(SelectedPatch, progress, _downloadCts.Token);
            if (success)
            {
                IsDownloaded = true;
                IsVerified = true;
                StatusMessage = "Patch downloaded and verified. Ready to install.";
            }
            else
            {
                HasError = true;
                ErrorMessage = "Download or integrity verification failed.";
                StatusMessage = "Download failed.";
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Download cancelled by user.";
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "Download encountered an error.";
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    [RelayCommand]
    public void CancelDownload()
    {
        if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
        {
            _downloadCts.Cancel();
            StatusMessage = "Cancelling download...";
        }
    }

    [RelayCommand]
    public async Task InstallPatchAsync()
    {
        if (SelectedPatch == null || IsDownloading || IsInstalling) return;

        IsInstalling = true;
        HasError = false;
        ErrorMessage = null;
        StatusMessage = $"Installing patch {SelectedPatch.Version}...";

        var progress = new Progress<string>(msg =>
        {
            StatusMessage = msg;
        });

        try
        {
            var result = await _patchService.InstallPatchAsync(SelectedPatch, progress);
            if (result.IsSuccess)
            {
                IsInstalled = true;
                IsLaunchAllowed = true;
                CurrentVersion = SelectedPatch.Version;
                StatusMessage = $"Patch {SelectedPatch.Version} installed successfully ({result.InstalledFilesCount} files updated).";

                RefreshInstalledPatches();
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage;
                StatusMessage = "Installation failed. Previous state restored.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = ex.Message;
            StatusMessage = "Installation error.";
        }
        finally
        {
            IsInstalling = false;
        }
    }

    [RelayCommand]
    public void LaunchApplication()
    {
        if (IsLaunchAllowed)
        {
            LaunchRequested?.Invoke();
        }
    }

    private void RefreshInstalledPatches()
    {
        InstalledPatches.Clear();
        foreach (var p in _patchService.InstalledPatches)
        {
            InstalledPatches.Add(p);
        }
    }
}
