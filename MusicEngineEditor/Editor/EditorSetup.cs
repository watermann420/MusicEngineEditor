using System;
using System.Reflection;
using System.Xml;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Color = System.Windows.Media.Color;
using Pen = System.Windows.Media.Pen;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace MusicEngineEditor.Editor;

public static class EditorSetup
{
    public static void Configure(TextEditor editor)
    {
        // Editor behavior settings
        editor.Options.EnableHyperlinks = false;
        editor.Options.EnableEmailHyperlinks = false;
        editor.Options.ConvertTabsToSpaces = true;
        editor.Options.IndentationSize = 4;
        editor.Options.HighlightCurrentLine = true;
        editor.Options.ShowEndOfLine = false;
        editor.Options.ShowSpaces = false;
        editor.Options.ShowTabs = false;
        editor.Options.AllowScrollBelowDocument = true;
        editor.Options.EnableRectangularSelection = true;
        editor.Options.EnableTextDragDrop = true;

        // Visual settings
        editor.ShowLineNumbers = true;
        editor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

        // Current line highlight
        editor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(
            Color.FromArgb(30, 255, 255, 255));
        editor.TextArea.TextView.CurrentLineBorder = new Pen(
            new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)), 1);

        // Load custom syntax highlighting
        LoadSyntaxHighlighting(editor);
    }

    public static void LoadSyntaxHighlighting(TextEditor editor)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MusicEngineEditor.Editor.CSharpScript.xshd");

            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                editor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
                return;
            }
        }
        catch
        {
            // Fall through to default
        }

        // Fallback to built-in C# highlighting
        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
    }
}
