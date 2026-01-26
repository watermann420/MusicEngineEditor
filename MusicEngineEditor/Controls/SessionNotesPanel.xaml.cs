using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Panel for managing timestamped session notes.
/// </summary>
public partial class SessionNotesPanel : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(ObservableCollection<SessionNote>), typeof(SessionNotesPanel),
            new PropertyMetadata(null, OnNotesChanged));

    public static readonly DependencyProperty CurrentPositionProperty =
        DependencyProperty.Register(nameof(CurrentPosition), typeof(TimeSpan), typeof(SessionNotesPanel),
            new PropertyMetadata(TimeSpan.Zero));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(SessionNotesPanel),
            new PropertyMetadata(120.0));

    #endregion

    #region Properties

    public ObservableCollection<SessionNote>? Notes
    {
        get => (ObservableCollection<SessionNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    public TimeSpan CurrentPosition
    {
        get => (TimeSpan)GetValue(CurrentPositionProperty);
        set => SetValue(CurrentPositionProperty, value);
    }

    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<SessionNote>? NoteAdded;
    public event EventHandler<SessionNote>? NoteDeleted;
    public event EventHandler<SessionNote>? NoteEdited;
    public event EventHandler<TimeSpan>? JumpToPositionRequested;

    #endregion

    #region Fields

    private readonly ObservableCollection<SessionNote> _allNotes = new();
    private string _searchFilter = string.Empty;
    private string _categoryFilter = string.Empty;

    #endregion

    public SessionNotesPanel()
    {
        InitializeComponent();

        Notes = _allNotes;
        NotesItemsControl.ItemsSource = _allNotes;

        UpdateEmptyState();
    }

    #region Property Changed Callbacks

    private static void OnNotesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SessionNotesPanel panel)
        {
            if (e.OldValue is ObservableCollection<SessionNote> oldCollection)
            {
                oldCollection.CollectionChanged -= panel.OnNotesCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<SessionNote> newCollection)
            {
                newCollection.CollectionChanged += panel.OnNotesCollectionChanged;
            }

            panel.ApplyFilters();
        }
    }

    private void OnNotesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        ApplyFilters();
        UpdateEmptyState();
    }

    #endregion

    #region Event Handlers

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchFilter = SearchTextBox.Text ?? string.Empty;
        ApplyFilters();
    }

    private void CategoryFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoryFilterCombo.SelectedItem is ComboBoxItem item)
        {
            _categoryFilter = item.Tag?.ToString() ?? string.Empty;
            ApplyFilters();
        }
    }

    private void NewNoteTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            AddNote();
            e.Handled = true;
        }
    }

    private void AddNoteButton_Click(object sender, RoutedEventArgs e)
    {
        AddNote();
    }

    private void NoteItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Detect double-click for jump
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is SessionNote note)
        {
            JumpToPositionRequested?.Invoke(this, note.Timestamp);
            e.Handled = true;
        }
    }

    private void NoteItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Single click on timestamp area
        if (e.OriginalSource is FrameworkElement source && source.DataContext is SessionNote note)
        {
            // Check if clicking on the timestamp border
            var parent = FindParent<Border>(source);
            if (parent?.ToolTip?.ToString()?.Contains("jump") == true)
            {
                JumpToPositionRequested?.Invoke(this, note.Timestamp);
                e.Handled = true;
            }
        }
    }

    private void JumpToPositionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            JumpToPositionRequested?.Invoke(this, note.Timestamp);
        }
    }

    private void EditNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            EditNote(note);
        }
    }

    private void DeleteNoteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            DeleteNote(note);
        }
    }

    private void CopyContentMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetNoteFromMenuItem(sender) is { } note)
        {
            Clipboard.SetText(note.Content);
        }
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        ExportNotes();
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(
            "Are you sure you want to clear all notes?",
            "Clear Notes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            ClearAllNotes();
        }
    }

    #endregion

    #region Public Methods

    public void AddNote()
    {
        var content = NewNoteTextBox.Text?.Trim();
        if (string.IsNullOrEmpty(content)) return;

        var category = (NewNoteCategoryCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "General";
        var tags = ParseTags(TagsTextBox.Text);

        var note = new SessionNote
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = CurrentPosition,
            Content = content,
            Category = category,
            Tags = tags,
            CreatedAt = DateTime.Now
        };

        _allNotes.Insert(0, note);
        NoteAdded?.Invoke(this, note);

        NewNoteTextBox.Text = string.Empty;
        TagsTextBox.Text = string.Empty;
        UpdateEmptyState();
    }

    public void AddNoteAtPosition(TimeSpan position, string content, string category = "General", List<string>? tags = null)
    {
        var note = new SessionNote
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = position,
            Content = content,
            Category = category,
            Tags = tags ?? new List<string>(),
            CreatedAt = DateTime.Now
        };

        _allNotes.Insert(0, note);
        NoteAdded?.Invoke(this, note);
        UpdateEmptyState();
    }

    public void EditNote(SessionNote note)
    {
        // In a real implementation, would show an edit dialog
        var newContent = Microsoft.VisualBasic.Interaction.InputBox(
            "Edit note content:",
            "Edit Note",
            note.Content);

        if (!string.IsNullOrEmpty(newContent))
        {
            note.Content = newContent;
            note.ModifiedAt = DateTime.Now;
            NoteEdited?.Invoke(this, note);
        }
    }

    public void DeleteNote(SessionNote note)
    {
        if (MessageBox.Show(
            "Delete this note?",
            "Delete Note",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _allNotes.Remove(note);
            NoteDeleted?.Invoke(this, note);
            UpdateEmptyState();
        }
    }

    public void ClearAllNotes()
    {
        _allNotes.Clear();
        UpdateEmptyState();
    }

    public void ExportNotes()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Text File (*.txt)|*.txt|Markdown (*.md)|*.md|All Files (*.*)|*.*",
            FileName = $"SessionNotes_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dialog.ShowDialog() == true)
        {
            ExportNotesToFile(dialog.FileName);
        }
    }

    public IReadOnlyList<SessionNote> GetAllNotes() => _allNotes.ToList().AsReadOnly();

    public IReadOnlyList<SessionNote> GetNotesByCategory(string category) =>
        _allNotes.Where(n => n.Category == category).ToList().AsReadOnly();

    public IReadOnlyList<SessionNote> GetNotesInRange(TimeSpan start, TimeSpan end) =>
        _allNotes.Where(n => n.Timestamp >= start && n.Timestamp <= end).ToList().AsReadOnly();

    #endregion

    #region Private Methods

    private void ApplyFilters()
    {
        var filtered = Notes?.AsEnumerable() ?? _allNotes.AsEnumerable();

        // Apply category filter
        if (!string.IsNullOrEmpty(_categoryFilter))
        {
            filtered = filtered.Where(n => n.Category == _categoryFilter);
        }

        // Apply search filter
        if (!string.IsNullOrEmpty(_searchFilter))
        {
            var search = _searchFilter.ToLowerInvariant();
            filtered = filtered.Where(n =>
                n.Content.ToLowerInvariant().Contains(search) ||
                n.Tags.Any(t => t.ToLowerInvariant().Contains(search)) ||
                n.Category.ToLowerInvariant().Contains(search));
        }

        // Sort by timestamp (most recent first)
        var sortedList = filtered.OrderByDescending(n => n.CreatedAt).ToList();

        NotesItemsControl.ItemsSource = sortedList;
        UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var hasNotes = _allNotes.Count > 0;
        EmptyStateText.Visibility = hasNotes ? Visibility.Collapsed : Visibility.Visible;
    }

    private static List<string> ParseTags(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();

        return input
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .Distinct()
            .ToList();
    }

    private void ExportNotesToFile(string filePath)
    {
        var sb = new StringBuilder();
        var isMarkdown = filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);

        if (isMarkdown)
        {
            sb.AppendLine("# Session Notes");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Session Notes");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine(new string('-', 50));
            sb.AppendLine();
        }

        foreach (var categoryGroup in _allNotes.OrderBy(n => n.Timestamp).GroupBy(n => n.Category))
        {
            if (isMarkdown)
            {
                sb.AppendLine($"## {categoryGroup.Key}");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine($"=== {categoryGroup.Key} ===");
                sb.AppendLine();
            }

            foreach (var note in categoryGroup)
            {
                var timeStr = FormatTimeSpan(note.Timestamp);

                if (isMarkdown)
                {
                    sb.AppendLine($"### [{timeStr}]");
                    sb.AppendLine(note.Content);
                    if (note.Tags.Count > 0)
                    {
                        sb.AppendLine($"*Tags: {string.Join(", ", note.Tags)}*");
                    }
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendLine($"[{timeStr}]");
                    sb.AppendLine(note.Content);
                    if (note.Tags.Count > 0)
                    {
                        sb.AppendLine($"Tags: {string.Join(", ", note.Tags)}");
                    }
                    sb.AppendLine();
                }
            }

            sb.AppendLine();
        }

        File.WriteAllText(filePath, sb.ToString());
        MessageBox.Show($"Notes exported to:\n{filePath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
        }
        return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
    }

    private static SessionNote? GetNoteFromMenuItem(object sender)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is System.Windows.Controls.ContextMenu contextMenu &&
            contextMenu.PlacementTarget is FrameworkElement element)
        {
            return element.DataContext as SessionNote;
        }
        return null;
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as T;
    }

    #endregion
}

/// <summary>
/// Represents a timestamped session note.
/// </summary>
public class SessionNote : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private TimeSpan _timestamp;
    private string _content = string.Empty;
    private string _category = "General";
    private List<string> _tags = new();
    private DateTime _createdAt;
    private DateTime? _modifiedAt;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public TimeSpan Timestamp { get => _timestamp; set { _timestamp = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimestampDisplay)); } }
    public string Content { get => _content; set { _content = value; OnPropertyChanged(); } }
    public string Category { get => _category; set { _category = value; OnPropertyChanged(); OnPropertyChanged(nameof(CategoryBrush)); } }
    public List<string> Tags { get => _tags; set { _tags = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasTags)); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public DateTime? ModifiedAt { get => _modifiedAt; set { _modifiedAt = value; OnPropertyChanged(); } }

    public string TimestampDisplay
    {
        get
        {
            if (Timestamp.TotalHours >= 1)
            {
                return $"{(int)Timestamp.TotalHours}:{Timestamp.Minutes:D2}:{Timestamp.Seconds:D2}";
            }
            return $"{(int)Timestamp.TotalMinutes}:{Timestamp.Seconds:D2}.{Timestamp.Milliseconds / 100:D1}";
        }
    }

    public bool HasTags => Tags.Count > 0;

    public SolidColorBrush CategoryBrush => Category switch
    {
        "Idea" => new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)),
        "Todo" => new SolidColorBrush(Color.FromRgb(0xFF, 0x98, 0x00)),
        "Issue" => new SolidColorBrush(Color.FromRgb(0xF4, 0x43, 0x36)),
        "Mix" => new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3)),
        _ => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E))
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
