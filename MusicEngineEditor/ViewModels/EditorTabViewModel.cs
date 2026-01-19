using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for an editor tab
/// </summary>
public partial class EditorTabViewModel : ViewModelBase
{
    [ObservableProperty]
    private MusicScript? _script;

    [ObservableProperty]
    private string _title = "Untitled";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private int _caretLine = 1;

    [ObservableProperty]
    private int _caretColumn = 1;

    [ObservableProperty]
    private bool _isSelected;

    public string ContentId => Script?.FilePath ?? Title;

    public EditorTabViewModel()
    {
    }

    public EditorTabViewModel(MusicScript script)
    {
        Script = script;
        Title = script.FileName;
        Content = script.Content;
        IsDirty = script.IsDirty;
    }

    partial void OnContentChanged(string value)
    {
        if (Script != null && Script.Content != value)
        {
            Script.Content = value;
            IsDirty = true;
        }
    }

    public void UpdateCaretPosition(int line, int column)
    {
        CaretLine = line;
        CaretColumn = column;
    }
}
