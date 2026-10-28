using System.Windows;
using Loka.App.ViewModels;

namespace Loka.App.Views;

public partial class PatchManagerWindow : Window
{
    private readonly PatchManagerViewModel? _viewModel;

    public bool ShouldLaunchApp { get; private set; }

    public PatchManagerWindow(PatchManagerViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = _viewModel;

        if (_viewModel != null)
        {
            _viewModel.LaunchRequested += OnLaunchRequested;
        }

        Loaded += async (s, e) =>
        {
            if (_viewModel != null)
            {
                await _viewModel.InitializeAsync();
            }
        };
    }

    private void OnLaunchRequested()
    {
        ShouldLaunchApp = true;
        DialogResult = true;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.LaunchRequested -= OnLaunchRequested;
        }
        base.OnClosed(e);
    }
}
