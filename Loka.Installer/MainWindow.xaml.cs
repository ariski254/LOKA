using System.Windows;
using Loka.Installer.ViewModels;

namespace Loka.Installer;

public partial class MainWindow : Window
{
    public MainWindow(bool isUninstallMode = false, string? originalDir = null, bool isRepairMode = false, bool isUpdateMode = false)
    {
        InitializeComponent();
        DataContext = new MainInstallerViewModel(isUninstallMode, originalDir, isRepairMode, isUpdateMode);

        if (isUninstallMode)
        {
            Title = "LOKA Uninstaller";
        }
        else if (isRepairMode)
        {
            Title = "LOKA Setup - Repair Installation";
        }
        else if (isUpdateMode)
        {
            Title = "LOKA Setup - Update";
        }
    }
}
