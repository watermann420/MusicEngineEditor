using System.Windows;

namespace MusicEngineEditor.Views.Dialogs;

public partial class InputDialog : Window
{
    public string Prompt
    {
        get => PromptText.Text;
        set => PromptText.Text = value;
    }

    public string Value
    {
        get => InputBox.Text;
        set => InputBox.Text = value;
    }

    public InputDialog()
    {
        InitializeComponent();
        Loaded += (s, e) =>
        {
            InputBox.Focus();
            InputBox.SelectAll();
        };
    }

    public static string? Show(string prompt, string title = "Input", string defaultValue = "", Window? owner = null)
    {
        var dialog = new InputDialog
        {
            Title = title,
            Prompt = prompt,
            Value = defaultValue,
            Owner = owner
        };

        return dialog.ShowDialog() == true ? dialog.Value : null;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
