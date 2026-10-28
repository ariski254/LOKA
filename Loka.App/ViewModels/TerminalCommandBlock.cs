using System;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Loka.App.Common;

namespace Loka.App.ViewModels;

public partial class TerminalCommandBlock : ObservableObject
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string WorkingDirectory { get; set; } = string.Empty;
    public string CommandText { get; set; } = string.Empty;
    public string OutputText { get; set; } = string.Empty;
    public string ErrorText { get; set; } = string.Empty;
    public int? ExitCode { get; set; }
    public bool HasExitCode => ExitCode.HasValue;
    public bool IsSuccess => ExitCode == 0;
    public bool IsFailure => ExitCode.HasValue && ExitCode.Value != 0;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    [ObservableProperty]
    private string _copyCommandLabel = "Copy Command";

    [ObservableProperty]
    private string _copyOutputLabel = "Copy Output";

    [ObservableProperty]
    private bool _isCommandCopied;

    [ObservableProperty]
    private bool _isOutputCopied;

    public string PromptText => $"PS {WorkingDirectory}> {CommandText}";

    public string FullOutput
    {
        get
        {
            if (string.IsNullOrEmpty(ErrorText)) return OutputText;
            if (string.IsNullOrEmpty(OutputText)) return ErrorText;
            return $"{OutputText}{Environment.NewLine}{ErrorText}";
        }
    }

    public bool HasOutput => !string.IsNullOrWhiteSpace(FullOutput);

    [RelayCommand]
    public async Task CopyCommandAsync()
    {
        if (string.IsNullOrEmpty(CommandText)) return;

        try
        {
            UiDispatcher.Run(() => Clipboard.SetDataObject(CommandText, true));

            CopyCommandLabel = "Copied!";
            IsCommandCopied = true;
            await Task.Delay(1500);
            CopyCommandLabel = "Copy Command";
            IsCommandCopied = false;
        }
        catch
        {
            CopyCommandLabel = "Copy Command";
            IsCommandCopied = false;
        }
    }

    [RelayCommand]
    public async Task CopyOutputAsync()
    {
        var textToCopy = FullOutput;
        if (string.IsNullOrEmpty(textToCopy) && HasExitCode)
        {
            textToCopy = $"[Process exited with code {ExitCode}]";
        }
        if (string.IsNullOrEmpty(textToCopy)) return;

        try
        {
            UiDispatcher.Run(() => Clipboard.SetDataObject(textToCopy, true));

            CopyOutputLabel = "Copied!";
            IsOutputCopied = true;
            await Task.Delay(1500);
            CopyOutputLabel = "Copy Output";
            IsOutputCopied = false;
        }
        catch
        {
            CopyOutputLabel = "Copy Output";
            IsOutputCopied = false;
        }
    }
}
