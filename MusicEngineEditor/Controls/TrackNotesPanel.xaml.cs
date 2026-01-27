// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

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
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a timestamp marker within track notes.
/// </summary>
public class TrackTimestamp
{
    /// <summary>
    /// Gets or sets the time position in seconds.
    /// </summary>
    [JsonPropertyName("time")]
    public double TimeSeconds { get; set; }

    /// <summary>
    /// Gets or sets the optional label for this timestamp.
    /// </summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    /// <summary>
    /// Gets the display text for this timestamp (MM:SS.ms format).
    /// </summary>
    [JsonIgnore]
    public string DisplayText
    {
        get
        {
            var span = TimeSpan.FromSeconds(TimeSeconds);
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:D2}:{span.Seconds:D2}"
                : $"{span.Minutes:D2}:{span.Seconds:D2}";
        }
    }
}

/// <summary>
/// Represents notes for a single track.
/// </summary>
public class TrackNoteEntry : INotifyPropertyChanged
{
    private string _trackId = string.Empty;
    private string _trackName = string.Empty;
    private Color _trackColor = Colors.DodgerBlue;
    private string _notesXaml = string.Empty;
    private bool _isExpanded = true;

    /// <summary>
    /// Gets or sets the track identifier.
    /// </summary>
    [JsonPropertyName("trackId")]
    public string TrackId
    {
        get => _trackId;
        set { _trackId = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the track display name.
    /// </summary>
    [JsonPropertyName("trackName")]
    public string TrackName
    {
        get => _trackName;
        set { _trackName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the track color as a hex string.
    /// </summary>
    [JsonPropertyName("trackColorHex")]
    public string TrackColorHex
    {
        get => $"#{_trackColor.R:X2}{_trackColor.G:X2}{_trackColor.B:X2}";
        set
        {
            try
            {
                _trackColor = (Color)ColorConverter.ConvertFromString(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(TrackColor));
            }
            catch { }
        }
    }

    /// <summary>
    /// Gets the track color.
    /// </summary>
    [JsonIgnore]
    public Color TrackColor => _trackColor;

    /// <summary>
    /// Gets or sets the notes content as XAML.
    /// </summary>
    [JsonPropertyName("notesXaml")]
    public string NotesXaml
    {
        get => _notesXaml;
        set
        {
            _notesXaml = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasNotes));
            OnPropertyChanged(nameof(NoteCount));
        }
    }

    /// <summary>
    /// Gets or sets the timestamps for this track.
    /// </summary>
    [JsonPropertyName("timestamps")]
    public ObservableCollection<TrackTimestamp> Timestamps { get; set; } = [];

    /// <summary>
    /// Gets or sets whether this track's notes are expanded.
    /// </summary>
    [JsonIgnore]
    public bool IsExpanded
    {
        get => _isExpanded;
        set { _isExpanded = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets whether this track has any notes.
    /// </summary>
    [JsonIgnore]
    public bool HasNotes => !string.IsNullOrWhiteSpace(_notesXaml);

    /// <summary>
    /// Gets whether this track has any timestamps.
    /// </summary>
    [JsonIgnore]
    public bool HasTimestamps => Timestamps.Count > 0;

    /// <summary>
    /// Gets an approximate note count (word count).
    /// </summary>
    [JsonIgnore]
    public int NoteCount
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_notesXaml)) return 0;
            // Rough word count from plain text content
            var plainText = System.Text.RegularExpressions.Regex.Replace(_notesXaml, "<[^>]+>", " ");
            return plainText.Split(new[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }

    /// <summary>
    /// Gets or sets the last modified date.
    /// </summary>
    [JsonPropertyName("modified")]
    public DateTime Modified { get; set; } = DateTime.UtcNow;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// View model for the TrackNotesPanel.
/// </summary>
public class TrackNotesPanelViewModel : INotifyPropertyChanged
{
    private string? _projectPath;

    /// <summary>
    /// Gets the collection of track notes.
    /// </summary>
    public ObservableCollection<TrackNoteEntry> TrackNotes { get; } = [];

    /// <summary>
    /// Gets whether there are any notes across all tracks.
    /// </summary>
    public bool HasAnyNotes => TrackNotes.Any(t => t.HasNotes);

    /// <summary>
    /// Gets the total note count across all tracks.
    /// </summary>
    public int TotalNoteCount => TrackNotes.Sum(t => t.NoteCount);

    /// <summary>
    /// Command to jump to a timestamp.
    /// </summary>
    public ICommand? JumpToTimestampCommand { get; set; }

    /// <summary>
    /// Command to add a timestamp to a track.
    /// </summary>
    public ICommand? AddTimestampCommand { get; set; }

    /// <summary>
    /// Sets the project path for auto-save.
    /// </summary>
    public void SetProjectPath(string? path)
    {
        _projectPath = path;
    }

    /// <summary>
    /// Gets the notes file path for the current project.
    /// </summary>
    public string? GetNotesFilePath()
    {
        if (string.IsNullOrEmpty(_projectPath)) return null;
        var projectDir = Path.GetDirectoryName(_projectPath);
        return projectDir != null ? Path.Combine(projectDir, "track-notes.json") : null;
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(HasAnyNotes));
        OnPropertyChanged(nameof(TotalNoteCount));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// A panel control for managing per-track text notes with rich text support.
/// </summary>
public partial class TrackNotesPanel : UserControl, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TrackNotesPanelViewModel _viewModel;
#pragma warning disable CS0649 // Field is never assigned - will be set via UI interaction when implemented
    private System.Windows.Controls.RichTextBox? _activeRichTextBox;
#pragma warning restore CS0649
    private bool _isDirty;
    private DateTime _lastSaveTime = DateTime.MinValue;
    private System.Windows.Threading.DispatcherTimer? _autoSaveTimer;

    /// <summary>
    /// Event raised when requesting to jump to a timestamp position.
    /// </summary>
    public event EventHandler<double>? JumpToTimestampRequested;

    /// <summary>
    /// Event raised when notes content changes.
    /// </summary>
    public event EventHandler? NotesChanged;

    /// <summary>
    /// Creates a new TrackNotesPanel.
    /// </summary>
    public TrackNotesPanel()
    {
        InitializeComponent();

        _viewModel = new TrackNotesPanelViewModel
        {
            JumpToTimestampCommand = new RelayCommand<TrackTimestamp>(JumpToTimestamp),
            AddTimestampCommand = new RelayCommand<TrackNoteEntry>(AddTimestampToTrack)
        };

        DataContext = _viewModel;

        // Setup auto-save timer (every 30 seconds when dirty)
        _autoSaveTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _autoSaveTimer.Tick += AutoSaveTimer_Tick;
        _autoSaveTimer.Start();
    }

    /// <summary>
    /// Sets the project path for auto-save functionality.
    /// </summary>
    public void SetProjectPath(string? projectPath)
    {
        _viewModel.SetProjectPath(projectPath);
        LoadNotes();
    }

    /// <summary>
    /// Adds or updates a track in the notes panel.
    /// </summary>
    public void AddOrUpdateTrack(string trackId, string trackName, string colorHex)
    {
        var existing = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == trackId);
        if (existing != null)
        {
            existing.TrackName = trackName;
            existing.TrackColorHex = colorHex;
        }
        else
        {
            _viewModel.TrackNotes.Add(new TrackNoteEntry
            {
                TrackId = trackId,
                TrackName = trackName,
                TrackColorHex = colorHex
            });
        }

        _viewModel.RefreshProperties();
    }

    /// <summary>
    /// Removes a track from the notes panel.
    /// </summary>
    public void RemoveTrack(string trackId)
    {
        var existing = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == trackId);
        if (existing != null)
        {
            _viewModel.TrackNotes.Remove(existing);
            _viewModel.RefreshProperties();
            MarkDirty();
        }
    }

    /// <summary>
    /// Gets the notes for a specific track.
    /// </summary>
    public string? GetTrackNotes(string trackId)
    {
        var entry = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == trackId);
        return entry?.NotesXaml;
    }

    /// <summary>
    /// Sets the notes for a specific track.
    /// </summary>
    public void SetTrackNotes(string trackId, string notesXaml)
    {
        var entry = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == trackId);
        if (entry != null)
        {
            entry.NotesXaml = notesXaml;
            entry.Modified = DateTime.UtcNow;
            _viewModel.RefreshProperties();
            MarkDirty();
        }
    }

    /// <summary>
    /// Adds a timestamp to a track.
    /// </summary>
    public void AddTimestamp(string trackId, double timeSeconds, string? label = null)
    {
        var entry = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == trackId);
        if (entry != null)
        {
            entry.Timestamps.Add(new TrackTimestamp
            {
                TimeSeconds = timeSeconds,
                Label = label
            });
            entry.Modified = DateTime.UtcNow;
            MarkDirty();
        }
    }

    /// <summary>
    /// Loads notes from the project folder.
    /// </summary>
    public void LoadNotes()
    {
        var filePath = _viewModel.GetNotesFilePath();
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var entries = JsonSerializer.Deserialize<TrackNoteEntry[]>(json, JsonOptions);

            if (entries != null)
            {
                // Merge with existing tracks (preserve track list from project)
                foreach (var entry in entries)
                {
                    var existing = _viewModel.TrackNotes.FirstOrDefault(t => t.TrackId == entry.TrackId);
                    if (existing != null)
                    {
                        existing.NotesXaml = entry.NotesXaml;
                        existing.Timestamps = entry.Timestamps;
                        existing.Modified = entry.Modified;
                    }
                    else
                    {
                        _viewModel.TrackNotes.Add(entry);
                    }
                }
            }

            _isDirty = false;
            _lastSaveTime = DateTime.UtcNow;
            UpdateLastSavedText();
            _viewModel.RefreshProperties();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load track notes: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves notes to the project folder.
    /// </summary>
    public void SaveNotes()
    {
        var filePath = _viewModel.GetNotesFilePath();
        if (string.IsNullOrEmpty(filePath))
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(_viewModel.TrackNotes.ToArray(), JsonOptions);
            File.WriteAllText(filePath, json);

            _isDirty = false;
            _lastSaveTime = DateTime.UtcNow;
            UpdateLastSavedText();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save track notes: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears all notes.
    /// </summary>
    public void ClearAllNotes()
    {
        foreach (var entry in _viewModel.TrackNotes)
        {
            entry.NotesXaml = string.Empty;
            entry.Timestamps.Clear();
        }

        _viewModel.RefreshProperties();
        MarkDirty();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        NotesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateLastSavedText()
    {
        if (_lastSaveTime == DateTime.MinValue)
        {
            LastSavedText.Text = "Never saved";
        }
        else
        {
            var elapsed = DateTime.UtcNow - _lastSaveTime;
            LastSavedText.Text = elapsed.TotalMinutes < 1
                ? "Saved just now"
                : $"Saved {(int)elapsed.TotalMinutes} min ago";
        }
    }

    private void AutoSaveTimer_Tick(object? sender, EventArgs e)
    {
        if (_isDirty)
        {
            SaveNotes();
        }

        UpdateLastSavedText();
    }

    private void JumpToTimestamp(TrackTimestamp? timestamp)
    {
        if (timestamp != null)
        {
            JumpToTimestampRequested?.Invoke(this, timestamp.TimeSeconds);
        }
    }

    private void AddTimestampToTrack(TrackNoteEntry? entry)
    {
        if (entry == null) return;

        // This would typically get the current playhead position from the playback service
        // For now, we'll add a placeholder at 0 seconds
        entry.Timestamps.Add(new TrackTimestamp
        {
            TimeSeconds = 0,
            Label = "Marker"
        });
        entry.Modified = DateTime.UtcNow;
        MarkDirty();
    }

    private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _viewModel.TrackNotes)
        {
            entry.IsExpanded = true;
        }
    }

    private void CollapseAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var entry in _viewModel.TrackNotes)
        {
            entry.IsExpanded = false;
        }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRichTextBox?.Selection != null)
        {
            var currentWeight = _activeRichTextBox.Selection.GetPropertyValue(TextElement.FontWeightProperty);
            var newWeight = currentWeight != DependencyProperty.UnsetValue && currentWeight.Equals(FontWeights.Bold)
                ? FontWeights.Normal
                : FontWeights.Bold;
            _activeRichTextBox.Selection.ApplyPropertyValue(TextElement.FontWeightProperty, newWeight);
            MarkDirty();
        }
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        if (_activeRichTextBox?.Selection != null)
        {
            var currentStyle = _activeRichTextBox.Selection.GetPropertyValue(TextElement.FontStyleProperty);
            var newStyle = currentStyle != DependencyProperty.UnsetValue && currentStyle.Equals(FontStyles.Italic)
                ? FontStyles.Normal
                : FontStyles.Italic;
            _activeRichTextBox.Selection.ApplyPropertyValue(TextElement.FontStyleProperty, newStyle);
            MarkDirty();
        }
    }

    private void AddCurrentTimestampButton_Click(object sender, RoutedEventArgs e)
    {
        // This would be connected to the playback service to get current position
        // For now, just show a message
        StatusText.Text = "Select a track to add timestamp";
    }

    /// <summary>
    /// Disposes the panel and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDirty)
        {
            SaveNotes();
        }

        _autoSaveTimer?.Stop();
        _autoSaveTimer = null;
    }
}

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke((T?)parameter) ?? true;
    public void Execute(object? parameter) => _execute((T?)parameter);

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }
}

/// <summary>
/// Converter that inverts a boolean value to Visibility.
/// </summary>
public class TrackNotesInverseBoolToVisibilityConverter : IValueConverter
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
