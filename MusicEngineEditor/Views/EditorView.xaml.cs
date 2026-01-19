using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit;
using MusicEngineEditor.Editor;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

public partial class EditorView : UserControl
{
    private EditorTabViewModel? _viewModel;

    public EditorView()
    {
        InitializeComponent();

        // Configure editor
        EditorSetup.Configure(CodeEditor);

        // Bind to ViewModel
        DataContextChanged += OnDataContextChanged;
        CodeEditor.TextChanged += OnTextChanged;
        CodeEditor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is EditorTabViewModel vm)
        {
            _viewModel = vm;

            // Set content from ViewModel
            if (!string.IsNullOrEmpty(vm.Content) && CodeEditor.Text != vm.Content)
            {
                CodeEditor.Text = vm.Content;
            }
        }
    }

    private void OnTextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null && _viewModel.Content != CodeEditor.Text)
        {
            _viewModel.Content = CodeEditor.Text;
        }
    }

    private void OnCaretPositionChanged(object? sender, System.EventArgs e)
    {
        _viewModel?.UpdateCaretPosition(
            CodeEditor.TextArea.Caret.Line,
            CodeEditor.TextArea.Caret.Column);
    }

    public TextEditor Editor => CodeEditor;
}
