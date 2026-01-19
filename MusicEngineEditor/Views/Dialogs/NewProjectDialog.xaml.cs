using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

public partial class NewProjectDialog : Window
{
    public string ProjectName { get; set; } = "MyMusicProject";
    public string ProjectLocation { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string Namespace { get; set; } = "MyMusicProject";

    public NewProjectDialog()
    {
        InitializeComponent();
        DataContext = this;

        ProjectNameBox.Focus();
        ProjectNameBox.SelectAll();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select project location",
            SelectedPath = ProjectLocation
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            ProjectLocation = dialog.SelectedPath;
        }
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
        {
            MessageBox.Show("Please enter a project name.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ProjectLocation) || !Directory.Exists(ProjectLocation))
        {
            MessageBox.Show("Please select a valid location.", "Validation Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Update namespace if still default
        if (string.IsNullOrWhiteSpace(Namespace) || Namespace == "MyMusicProject")
        {
            Namespace = SanitizeNamespace(ProjectName);
        }

        DialogResult = true;
        Close();
    }

    private static string SanitizeNamespace(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }
}
