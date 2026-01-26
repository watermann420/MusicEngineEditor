// MusicEngineEditor - Recent Files Panel Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Extended recent project entry with additional display properties.
/// </summary>
public class RecentFileEntry : INotifyPropertyChanged
{
    private bool _isPinned;

    /// <summary>
    /// Gets or sets the full path to the project file.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name of the project.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date and time when the project was last opened.
    /// </summary>
    [JsonPropertyName("lastOpened")]
    public DateTime LastOpened { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets whether this project is pinned to the top.
    /// </summary>
    [JsonPropertyName("isPinned")]
    public bool IsPinned
    {
        get => _isPinned;
        set { _isPinned = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the path to a thumbnail image.
    /// </summary>
    [JsonPropertyName("thumbnailPath")]
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Gets the folder path (parent directory) for display.
    /// </summary>
    [JsonIgnore]
    public string FolderPath
    {
        get
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(Path);
                return dir ?? Path;
            }
            catch
            {
                return Path;
            }
        }
    }

    /// <summary>
    /// Gets the relative time string for display (e.g., "2 hours ago").
    /// </summary>
    [JsonIgnore]
    public string LastOpenedRelative
    {
        get
        {
            var elapsed = DateTime.UtcNow - LastOpened;

            if (elapsed.TotalMinutes < 1)
                return "just now";
            if (elapsed.TotalMinutes < 60)
                return $"{(int)elapsed.TotalMinutes} minute{((int)elapsed.TotalMinutes == 1 ? "" : "s")} ago";
            if (elapsed.TotalHours < 24)
                return $"{(int)elapsed.TotalHours} hour{((int)elapsed.TotalHours == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 7)
                return $"{(int)elapsed.TotalDays} day{((int)elapsed.TotalDays == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 30)
                return $"{(int)(elapsed.TotalDays / 7)} week{((int)(elapsed.TotalDays / 7) == 1 ? "" : "s")} ago";
            if (elapsed.TotalDays < 365)
                return $"{(int)(elapsed.TotalDays / 30)} month{((int)(elapsed.TotalDays / 30) == 1 ? "" : "s")} ago";

            return $"{(int)(elapsed.TotalDays / 365)} year{((int)(elapsed.TotalDays / 365) == 1 ? "" : "s")} ago";
        }
    }

    /// <summary>
    /// Gets whether the project file exists on disk.
    /// </summary>
    [JsonIgnore]
    public bool Exists => File.Exists(Path);

    /// <summary>
    /// Gets whether a thumbnail is available.
    /// </summary>
    [JsonIgnore]
    public bool HasThumbnail => !string.IsNullOrEmpty(ThumbnailPath) && File.Exists(ThumbnailPath);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for the RecentFilesPanel.
/// </summary>
public class RecentFilesPanelViewModel : INotifyPropertyChanged
{
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets the collection of all recent files.
    /// </summary>
    public ObservableCollection<RecentFileEntry> RecentFiles { get; } = [];

    /// <summary>
    /// Gets the filtered recent files based on search text.
    /// </summary>
    public ObservableCollection<RecentFileEntry> FilteredRecentFiles { get; } = [];

    /// <summary>
    /// Gets or sets the search text for filtering.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            RefreshFiltered();
        }
    }

    /// <summary>
    /// Gets whether there are any recent files.
    /// </summary>
    public bool HasRecentFiles => FilteredRecentFiles.Count > 0;

    /// <summary>
    /// Gets the count of recent files.
    /// </summary>
    public int RecentFilesCount => RecentFiles.Count;

    /// <summary>
    /// Gets the count of pinned files.
    /// </summary>
    public int PinnedCount => RecentFiles.Count(f => f.IsPinned);

    /// <summary>
    /// Gets whether there are any pinned files.
    /// </summary>
    public bool HasPinned => PinnedCount > 0;

    /// <summary>
    /// Refreshes the filtered collection based on search text.
    /// </summary>
    public void RefreshFiltered()
    {
        FilteredRecentFiles.Clear();

        var query = RecentFiles.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var search = _searchText.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.Name.ToLowerInvariant().Contains(search) ||
                f.Path.ToLowerInvariant().Contains(search));
        }

        // Sort: pinned first, then by last opened
        query = query
            .OrderByDescending(f => f.IsPinned)
            .ThenByDescending(f => f.LastOpened);

        foreach (var file in query)
        {
            FilteredRecentFiles.Add(file);
        }

        OnPropertyChanged(nameof(HasRecentFiles));
        OnPropertyChanged(nameof(RecentFilesCount));
        OnPropertyChanged(nameof(PinnedCount));
        OnPropertyChanged(nameof(HasPinned));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// A panel control for displaying and managing recent projects.
/// </summary>
public partial class RecentFilesPanel : UserControl
{
    private static readonly string RecentFilesPath = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MusicEngineEditor",
        "recent-files.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RecentFilesPanelViewModel _viewModel;

    /// <summary>
    /// Event raised when a project should be opened.
    /// </summary>
    public event EventHandler<string>? OpenProjectRequested;

    /// <summary>
    /// Event raised when the user wants to browse for a project.
    /// </summary>
    public event EventHandler? BrowseForProjectRequested;

    /// <summary>
    /// Event raised when the recent files list changes.
    /// </summary>
    public event EventHandler? RecentFilesChanged;

    /// <summary>
    /// Creates a new RecentFilesPanel.
    /// </summary>
    public RecentFilesPanel()
    {
        InitializeComponent();

        _viewModel = new RecentFilesPanelViewModel();
        DataContext = _viewModel;

        LoadRecentFiles();
    }

    /// <summary>
    /// Adds a project to the recent files list.
    /// </summary>
    public void AddRecentFile(string path, string? name = null)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        // Remove existing entry with same path
        var existing = _viewModel.RecentFiles.FirstOrDefault(
            f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            _viewModel.RecentFiles.Remove(existing);
        }

        // Add new entry at the beginning
        var entry = new RecentFileEntry
        {
            Path = path,
            Name = name ?? System.IO.Path.GetFileNameWithoutExtension(path),
            LastOpened = DateTime.UtcNow,
            IsPinned = existing?.IsPinned ?? false,
            ThumbnailPath = GetThumbnailPath(path)
        };

        _viewModel.RecentFiles.Insert(0, entry);

        // Limit to 20 non-pinned entries
        while (_viewModel.RecentFiles.Count(f => !f.IsPinned) > 20)
        {
            var lastNonPinned = _viewModel.RecentFiles.LastOrDefault(f => !f.IsPinned);
            if (lastNonPinned != null)
            {
                _viewModel.RecentFiles.Remove(lastNonPinned);
            }
            else
            {
                break;
            }
        }

        _viewModel.RefreshFiltered();
        SaveRecentFiles();
        RecentFilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a project from the recent files list.
    /// </summary>
    public void RemoveRecentFile(string path)
    {
        var entry = _viewModel.RecentFiles.FirstOrDefault(
            f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            _viewModel.RecentFiles.Remove(entry);
            _viewModel.RefreshFiltered();
            SaveRecentFiles();
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Clears all recent files.
    /// </summary>
    public void ClearRecentFiles()
    {
        _viewModel.RecentFiles.Clear();
        _viewModel.RefreshFiltered();
        SaveRecentFiles();
        RecentFilesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Pins or unpins a recent file.
    /// </summary>
    public void SetPinned(string path, bool isPinned)
    {
        var entry = _viewModel.RecentFiles.FirstOrDefault(
            f => f.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

        if (entry != null)
        {
            entry.IsPinned = isPinned;
            _viewModel.RefreshFiltered();
            SaveRecentFiles();
        }
    }

    /// <summary>
    /// Gets the list of recent file paths.
    /// </summary>
    public string[] GetRecentFilePaths()
    {
        return _viewModel.RecentFiles
            .Where(f => f.Exists)
            .Select(f => f.Path)
            .ToArray();
    }

    private void LoadRecentFiles()
    {
        _viewModel.RecentFiles.Clear();

        if (!File.Exists(RecentFilesPath))
        {
            _viewModel.RefreshFiltered();
            return;
        }

        try
        {
            var json = File.ReadAllText(RecentFilesPath);
            var entries = JsonSerializer.Deserialize<RecentFileEntry[]>(json, JsonOptions);

            if (entries != null)
            {
                foreach (var entry in entries.Where(e => e.Exists))
                {
                    // Update thumbnail path
                    entry.ThumbnailPath = GetThumbnailPath(entry.Path);
                    _viewModel.RecentFiles.Add(entry);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load recent files: {ex.Message}");
        }

        _viewModel.RefreshFiltered();
    }

    private void SaveRecentFiles()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(RecentFilesPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var json = JsonSerializer.Serialize(_viewModel.RecentFiles.ToArray(), JsonOptions);
            File.WriteAllText(RecentFilesPath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save recent files: {ex.Message}");
        }
    }

    private static string? GetThumbnailPath(string projectPath)
    {
        try
        {
            var projectDir = System.IO.Path.GetDirectoryName(projectPath);
            if (projectDir == null) return null;

            // Look for a thumbnail file in the project directory
            var thumbPath = System.IO.Path.Combine(projectDir, "thumbnail.png");
            if (File.Exists(thumbPath))
            {
                return thumbPath;
            }

            thumbPath = System.IO.Path.Combine(projectDir, ".thumbnail.png");
            if (File.Exists(thumbPath))
            {
                return thumbPath;
            }
        }
        catch { }

        return null;
    }

    private void RecentFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RecentFilesList.SelectedItem is RecentFileEntry entry)
        {
            OpenProjectRequested?.Invoke(this, entry.Path);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchText = SearchBox.Text;
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear the recent projects list?",
            "Clear Recent Projects",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ClearRecentFiles();
        }
    }

    private void PinButton_Changed(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is RecentFileEntry entry)
        {
            _viewModel.RefreshFiltered();
            SaveRecentFiles();
        }
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is RecentFileEntry entry)
        {
            RemoveRecentFile(entry.Path);
        }
    }

    private void OpenProjectButton_Click(object sender, RoutedEventArgs e)
    {
        BrowseForProjectRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Converter that converts a boolean to Visibility (inverted).
/// </summary>
public class RecentFilesInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts pin state to tooltip text.
/// </summary>
public class PinTooltipConverter : MarkupExtension, IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            return isPinned ? "Unpin from favorites" : "Pin to favorites";
        }
        return "Pin to favorites";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return this;
    }
}
