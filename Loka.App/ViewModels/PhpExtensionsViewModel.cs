using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.Core.Interfaces;
using Loka.Core.Models;

namespace Loka.App.ViewModels;

public partial class PhpExtensionsViewModel : ObservableObject
{
    private readonly IPhpService _phpService;
    private readonly ILogService _logger;
    private List<PhpExtensionInfo> _allExtensions = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _activePhpBinary = string.Empty;

    public ObservableCollection<PhpExtensionInfo> Extensions { get; } = new();

    public PhpExtensionsViewModel(IPhpService phpService, ILogService logger)
    {
        _phpService = phpService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        await LoadExtensionsAsync();
    }

    [RelayCommand]
    public async Task LoadExtensionsAsync()
    {
        IsLoading = true;
        StatusMessage = "Reading php.ini configuration...";
        ActivePhpBinary = _phpService.GetActivePhpBinary();

        try
        {
            var list = await _phpService.GetExtensionsAsync();
            _allExtensions = list.ToList();
            FilterExtensions();
            StatusMessage = $"Loaded {_allExtensions.Count} PHP extensions.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            _logger.LogError("PHP", "Error reading extensions", ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        FilterExtensions();
    }

    private void FilterExtensions()
    {
        Extensions.Clear();
        var q = SearchQuery.Trim().ToLowerInvariant();
        var filtered = string.IsNullOrEmpty(q)
            ? _allExtensions
            : _allExtensions.Where(e => e.Name.ToLowerInvariant().Contains(q) || e.Description.ToLowerInvariant().Contains(q));

        foreach (var ext in filtered)
        {
            Extensions.Add(ext);
        }
    }

    [RelayCommand]
    public async Task ToggleExtensionAsync(PhpExtensionInfo? ext)
    {
        if (ext == null) return;

        StatusMessage = $"{(ext.IsEnabled ? "Enabling" : "Disabling")} {ext.Name}...";
        try
        {
            bool ok = await _phpService.SetExtensionEnabledAsync(ext.Name, ext.IsEnabled);
            if (ok)
            {
                StatusMessage = $"Extension {ext.Name} updated in php.ini.";
            }
            else
            {
                StatusMessage = $"Failed to update {ext.Name}. Reverted.";
                ext.IsEnabled = !ext.IsEnabled; // revert
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            ext.IsEnabled = !ext.IsEnabled;
        }
    }
}
