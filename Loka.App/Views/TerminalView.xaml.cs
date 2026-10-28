using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Loka.App.ViewModels;

namespace Loka.App.Views;

public partial class TerminalView : UserControl
{
    public TerminalView()
    {
        InitializeComponent();
        Loaded += TerminalView_Loaded;
    }

    private void TerminalView_Loaded(object sender, RoutedEventArgs e)
    {
        FocusTerminalInput();
    }

    public void FocusTerminalInput()
    {
        CommandInputBox?.Focus();
        if (CommandInputBox != null)
        {
            CommandInputBox.CaretIndex = CommandInputBox.Text.Length;
        }
    }

    private void TerminalOutputBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (TerminalOutputBox.SelectionLength == 0)
        {
            TerminalScrollViewer?.ScrollToEnd();
        }
    }

    private void TerminalOutputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (TerminalOutputBox.SelectionLength > 0)
            {
                // Has selection -> native copy
                return;
            }
            else if (DataContext is TerminalViewModel vm && vm.IsExecuting)
            {
                // No selection and running command -> trigger Stop execution like a real terminal
                vm.StopExecutionCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            TerminalOutputBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key != Key.Left && e.Key != Key.Right && e.Key != Key.Up && e.Key != Key.Down &&
                 e.Key != Key.PageUp && e.Key != Key.PageDown && e.Key != Key.Home && e.Key != Key.End)
        {
            // If typing printable characters or backspace while output box has focus without selection,
            // seamlessly transfer focus to active prompt line
            if (TerminalOutputBox.SelectionLength == 0)
            {
                FocusTerminalInput();
            }
        }
    }

    private void CommandInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not TerminalViewModel vm) return;

        if (e.Key == Key.Enter)
        {
            if (!vm.IsExecuting && !string.IsNullOrWhiteSpace(vm.CurrentCommand))
            {
                vm.ExecuteCommandCommand.Execute(null);
                TerminalScrollViewer?.ScrollToEnd();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Up)
        {
            vm.NavigateHistory(up: true);
            CommandInputBox.CaretIndex = CommandInputBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Down)
        {
            vm.NavigateHistory(up: false);
            CommandInputBox.CaretIndex = CommandInputBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (CommandInputBox.SelectionLength == 0 && vm.IsExecuting)
            {
                vm.StopExecutionCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void OnTerminalAreaMouseDown(object sender, MouseButtonEventArgs e)
    {
        // If clicking empty background inside terminal viewport, focus the active input line
        if (e.OriginalSource is not TextBox)
        {
            FocusTerminalInput();
        }
    }
}
