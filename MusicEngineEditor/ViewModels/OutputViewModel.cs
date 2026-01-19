using System;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Output panel
/// </summary>
public partial class OutputViewModel : ViewModelBase
{
    private readonly StringBuilder _outputBuilder = new();

    [ObservableProperty]
    private string _outputText = string.Empty;

    [ObservableProperty]
    private bool _autoScroll = true;

    public event EventHandler? OutputChanged;

    public void AppendLine(string text)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        _outputBuilder.AppendLine($"[{timestamp}] {text}");
        OutputText = _outputBuilder.ToString();
        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Append(string text)
    {
        _outputBuilder.Append(text);
        OutputText = _outputBuilder.ToString();
        OutputChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Clear()
    {
        _outputBuilder.Clear();
        OutputText = string.Empty;
    }

    [RelayCommand]
    private void CopyAll()
    {
        if (!string.IsNullOrEmpty(OutputText))
        {
            System.Windows.Clipboard.SetText(OutputText);
        }
    }
}
