using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for comparing two project versions and showing differences.
/// </summary>
public partial class ProjectCompareDialog : Window
{
    #region Fields

    private string? _projectAPath;
    private string? _projectBPath;
    private List<DiffItem> _allDiffItems = new();
    private List<DiffItem> _filteredDiffItems = new();

    #endregion

    #region Constructor

    public ProjectCompareDialog()
    {
        InitializeComponent();
    }

    #endregion

    #region Event Handlers

    private void BrowseProjectA_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseForProject("Select Base Version (A)");
        if (path != null)
        {
            _projectAPath = path;
            ProjectAPathText.Text = System.IO.Path.GetFileName(path);
            ProjectAPathText.Foreground = FindResource("BrightForegroundBrush") as Brush;
            UpdateCompareButtonState();
        }
    }

    private void BrowseProjectB_Click(object sender, RoutedEventArgs e)
    {
        var path = BrowseForProject("Select Compare Version (B)");
        if (path != null)
        {
            _projectBPath = path;
            ProjectBPathText.Text = System.IO.Path.GetFileName(path);
            ProjectBPathText.Foreground = FindResource("BrightForegroundBrush") as Brush;
            UpdateCompareButtonState();
        }
    }

    private void SwapProjects_Click(object sender, RoutedEventArgs e)
    {
        (_projectAPath, _projectBPath) = (_projectBPath, _projectAPath);

        var tempText = ProjectAPathText.Text;
        ProjectAPathText.Text = ProjectBPathText.Text;
        ProjectBPathText.Text = tempText;

        var tempBrush = ProjectAPathText.Foreground;
        ProjectAPathText.Foreground = ProjectBPathText.Foreground;
        ProjectBPathText.Foreground = tempBrush;

        // Re-run comparison if already compared
        if (_allDiffItems.Count > 0)
        {
            PerformComparison();
        }
    }

    private void CompareButton_Click(object sender, RoutedEventArgs e)
    {
        PerformComparison();
    }

    private void MergeButton_Click(object sender, RoutedEventArgs e)
    {
        // Show merge confirmation dialog
        var result = MessageBox.Show(
            "Merge selected changes into the base project?\n\nThis will modify the base project file.",
            "Confirm Merge",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            PerformMerge();
        }
    }

    private void FilterChanged(object sender, RoutedEventArgs e)
    {
        ApplyFilter();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Comparison Logic

    private string? BrowseForProject(string title)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            Filter = "Music Engine Project|*.mep;*.meproj|All Files|*.*",
            FilterIndex = 1
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void UpdateCompareButtonState()
    {
        CompareButton.IsEnabled = !string.IsNullOrEmpty(_projectAPath) && !string.IsNullOrEmpty(_projectBPath);
    }

    private void PerformComparison()
    {
        if (string.IsNullOrEmpty(_projectAPath) || string.IsNullOrEmpty(_projectBPath))
            return;

        try
        {
            // In a real implementation, this would load and compare the actual project files
            // For now, we generate sample diff data to demonstrate the UI
            _allDiffItems = GenerateSampleDiff();

            // Update summary counts
            int added = 0, removed = 0, changed = 0, unchanged = 0;
            foreach (var item in _allDiffItems)
            {
                switch (item.ChangeType)
                {
                    case DiffChangeType.Added: added++; break;
                    case DiffChangeType.Removed: removed++; break;
                    case DiffChangeType.Modified: changed++; break;
                    case DiffChangeType.Unchanged: unchanged++; break;
                }
            }

            AddedCountText.Text = added.ToString();
            RemovedCountText.Text = removed.ToString();
            ChangedCountText.Text = changed.ToString();
            UnchangedCountText.Text = unchanged.ToString();

            // Apply filter and display
            ApplyFilter();

            // Update UI state
            EmptyStateText.Visibility = Visibility.Collapsed;
            MergeButton.IsEnabled = changed > 0 || added > 0 || removed > 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to compare projects:\n{ex.Message}", "Comparison Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyFilter()
    {
        DiffCategory? filterCategory = null;

        if (FilterTracksRadio.IsChecked == true)
            filterCategory = DiffCategory.Track;
        else if (FilterPluginsRadio.IsChecked == true)
            filterCategory = DiffCategory.Plugin;
        else if (FilterParametersRadio.IsChecked == true)
            filterCategory = DiffCategory.Parameter;
        else if (FilterSettingsRadio.IsChecked == true)
            filterCategory = DiffCategory.Setting;

        if (filterCategory.HasValue)
        {
            _filteredDiffItems = _allDiffItems.FindAll(item => item.Category == filterCategory.Value);
        }
        else
        {
            _filteredDiffItems = new List<DiffItem>(_allDiffItems);
        }

        DiffItemsList.ItemsSource = _filteredDiffItems;
    }

    private void PerformMerge()
    {
        // In a real implementation, this would actually merge the selected changes
        MessageBox.Show("Merge functionality would be implemented here.\n\nSelected changes would be merged into the base project.",
            "Merge", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private List<DiffItem> GenerateSampleDiff()
    {
        // Generate sample diff items to demonstrate the UI
        var items = new List<DiffItem>
        {
            new()
            {
                ChangeType = DiffChangeType.Added,
                Category = DiffCategory.Track,
                ItemName = "Synth Lead",
                ItemPath = "Tracks/Synth Lead",
                ChangeDescription = "New MIDI track added"
            },
            new()
            {
                ChangeType = DiffChangeType.Removed,
                Category = DiffCategory.Track,
                ItemName = "Old Pad",
                ItemPath = "Tracks/Old Pad",
                ChangeDescription = "Audio track removed"
            },
            new()
            {
                ChangeType = DiffChangeType.Modified,
                Category = DiffCategory.Track,
                ItemName = "Drums",
                ItemPath = "Tracks/Drums",
                ChangeDescription = "Volume: -6.0 dB -> -3.0 dB, Pan: 0 -> -15"
            },
            new()
            {
                ChangeType = DiffChangeType.Added,
                Category = DiffCategory.Plugin,
                ItemName = "Reverb (Convolution)",
                ItemPath = "Tracks/Vocals/Effects/Slot 3",
                ChangeDescription = "New effect instance"
            },
            new()
            {
                ChangeType = DiffChangeType.Modified,
                Category = DiffCategory.Plugin,
                ItemName = "Compressor",
                ItemPath = "Tracks/Bass/Effects/Slot 1",
                ChangeDescription = "Threshold: -18 dB -> -12 dB, Ratio: 4:1 -> 6:1"
            },
            new()
            {
                ChangeType = DiffChangeType.Modified,
                Category = DiffCategory.Parameter,
                ItemName = "Master Volume",
                ItemPath = "Master/Volume",
                ChangeDescription = "0.0 dB -> -1.5 dB"
            },
            new()
            {
                ChangeType = DiffChangeType.Modified,
                Category = DiffCategory.Setting,
                ItemName = "Project Tempo",
                ItemPath = "Project/Tempo",
                ChangeDescription = "120 BPM -> 125 BPM"
            },
            new()
            {
                ChangeType = DiffChangeType.Unchanged,
                Category = DiffCategory.Track,
                ItemName = "Vocals",
                ItemPath = "Tracks/Vocals"
            },
            new()
            {
                ChangeType = DiffChangeType.Unchanged,
                Category = DiffCategory.Track,
                ItemName = "Bass",
                ItemPath = "Tracks/Bass"
            }
        };

        return items;
    }

    #endregion
}

/// <summary>
/// Represents a single difference item between two projects.
/// </summary>
public class DiffItem
{
    public DiffChangeType ChangeType { get; set; }
    public DiffCategory Category { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? ItemPath { get; set; }
    public string? ChangeDescription { get; set; }
    public bool CanMerge => ChangeType != DiffChangeType.Unchanged;
    public bool HasPath => !string.IsNullOrEmpty(ItemPath);
    public bool HasDescription => !string.IsNullOrEmpty(ChangeDescription);

    public string ChangeIndicator => ChangeType switch
    {
        DiffChangeType.Added => "+",
        DiffChangeType.Removed => "-",
        DiffChangeType.Modified => "~",
        _ => " "
    };

    public Brush IndicatorColor => ChangeType switch
    {
        DiffChangeType.Added => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        DiffChangeType.Removed => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        DiffChangeType.Modified => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A))
    };

    public Brush DescriptionColor => ChangeType switch
    {
        DiffChangeType.Added => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        DiffChangeType.Removed => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        DiffChangeType.Modified => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        _ => new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A))
    };

    public Style DiffStyle
    {
        get
        {
            // Return appropriate style based on change type
            // In practice, this would reference styles from resources
            return null!;
        }
    }

    public ICommand? KeepACommand { get; set; }
    public ICommand? UseBCommand { get; set; }
}

/// <summary>
/// Type of change in a diff item.
/// </summary>
public enum DiffChangeType
{
    Unchanged,
    Added,
    Removed,
    Modified
}

/// <summary>
/// Category of a diff item.
/// </summary>
public enum DiffCategory
{
    Track,
    Plugin,
    Parameter,
    Setting
}
