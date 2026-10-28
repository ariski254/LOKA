using System.Windows;
using Loka.App.ViewModels;

namespace Loka.App.Views;

public partial class SetupWizardWindow : Window
{
    private readonly SetupWizardViewModel _viewModel;

    public SetupWizardWindow(SetupWizardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.RequestCloseWithSuccess += () =>
        {
            DialogResult = true;
            Close();
        };

        _viewModel.RequestCloseCancel += () =>
        {
            DialogResult = false;
            Close();
        };
    }
}
