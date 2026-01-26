using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a single lyric line with timing information.
/// </summary>
public partial class LyricLine : ObservableObject
{
    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private List<LyricWord> _words = new();

    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Gets the duration of this line.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;

    /// <summary>
    /// Gets a formatted time display string.
    /// </summary>
    public string TimeDisplay => $"{StartTime:mm\\:ss\\.ff}";

    partial void OnStartTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(TimeDisplay));
        OnPropertyChanged(nameof(Duration));
    }

    partial void OnEndTimeChanged(TimeSpan value)
    {
        OnPropertyChanged(nameof(Duration));
    }

    /// <summary>
    /// Creates a clone of this line.
    /// </summary>
    public LyricLine Clone()
    {
        return new LyricLine
        {
            StartTime = StartTime,
            EndTime = EndTime,
            Text = Text,
            Words = Words.Select(w => w.Clone()).ToList()
        };
    }
}

/// <summary>
/// Represents a single word within a lyric line for word-level sync.
/// </summary>
public partial class LyricWord : ObservableObject
{
    [ObservableProperty]
    private TimeSpan _startTime;

    [ObservableProperty]
    private TimeSpan _endTime;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Creates a clone of this word.
    /// </summary>
    public LyricWord Clone()
    {
        return new LyricWord
        {
            StartTime = StartTime,
            EndTime = EndTime,
            Text = Text
        };
    }
}

/// <summary>
/// Sync mode for lyrics.
/// </summary>
public enum LyricSyncMode
{
    /// <summary>Line-by-line sync.</summary>
    Line,
    /// <summary>Word-by-word sync.</summary>
    Word
}

/// <summary>
/// Editor control for synchronizing lyrics with audio.
/// Supports import/export of LRC and SRT formats.
/// </summary>
public partial class LyricSyncEditor : UserControl
{
    private readonly ObservableCollection<LyricLine> _lyrics = new();
    private readonly DispatcherTimer _previewTimer;
    private LyricLine? _selectedLine;
    private LyricSyncMode _syncMode = LyricSyncMode.Line;
    private bool _isTapSyncActive;
    private int _tapSyncIndex;
    private TimeSpan _currentPlaybackTime;

    /// <summary>
    /// Gets or sets the current playback time for sync reference.
    /// </summary>
    public TimeSpan CurrentPlaybackTime
    {
        get => _currentPlaybackTime;
        set
        {
            _currentPlaybackTime = value;
            UpdatePreviewDisplay();
        }
    }

    /// <summary>
    /// Event raised when playback time should be retrieved.
    /// </summary>
    public event EventHandler<TimeSpan>? PlaybackTimeRequested;

    /// <summary>
    /// Event raised when lyrics change.
    /// </summary>
    public event EventHandler? LyricsChanged;

    /// <summary>
    /// Gets the lyrics collection.
    /// </summary>
    public IReadOnlyList<LyricLine> Lyrics => _lyrics;

    public LyricSyncEditor()
    {
        InitializeComponent();
        LyricsItemsControl.ItemsSource = _lyrics;

        _previewTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _previewTimer.Tick += OnPreviewTimerTick;
    }

    #region Import/Export

    private void OnImportClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Lyric files (*.lrc;*.srt;*.txt)|*.lrc;*.srt;*.txt|LRC files (*.lrc)|*.lrc|SRT files (*.srt)|*.srt|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Import Lyrics"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                var content = File.ReadAllText(dialog.FileName);

                _lyrics.Clear();

                switch (extension)
                {
                    case ".lrc":
                        ImportLrc(content);
                        break;
                    case ".srt":
                        ImportSrt(content);
                        break;
                    default:
                        ImportPlainText(content);
                        break;
                }

                LyricsChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to import lyrics: {ex.Message}", "Import Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ImportLrc(string content)
    {
        // LRC format: [mm:ss.ff]text or [mm:ss:ff]text
        var regex = new Regex(@"\[(\d{2}):(\d{2})[\.:]([\d]{2,3})\](.*)");
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var rawLine in lines)
        {
            var match = regex.Match(rawLine.Trim());
            if (match.Success)
            {
                var minutes = int.Parse(match.Groups[1].Value);
                var seconds = int.Parse(match.Groups[2].Value);
                var ms = match.Groups[3].Value;
                // Handle both .ff (hundredths) and .fff (milliseconds)
                var milliseconds = ms.Length == 2 ? int.Parse(ms) * 10 : int.Parse(ms);

                var text = match.Groups[4].Value.Trim();
                if (!string.IsNullOrEmpty(text))
                {
                    _lyrics.Add(new LyricLine
                    {
                        StartTime = new TimeSpan(0, minutes, seconds).Add(TimeSpan.FromMilliseconds(milliseconds)),
                        Text = text,
                        Words = ParseWords(text)
                    });
                }
            }
        }

        // Set end times (next line's start time or +3 seconds)
        for (int i = 0; i < _lyrics.Count; i++)
        {
            _lyrics[i].EndTime = i < _lyrics.Count - 1
                ? _lyrics[i + 1].StartTime
                : _lyrics[i].StartTime.Add(TimeSpan.FromSeconds(3));
        }
    }

    private void ImportSrt(string content)
    {
        // SRT format:
        // 1
        // 00:00:00,000 --> 00:00:03,000
        // Subtitle text
        var blocks = content.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var timeRegex = new Regex(@"(\d{2}):(\d{2}):(\d{2})[,\.](\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2})[,\.](\d{3})");

        foreach (var block in blocks)
        {
            var lines = block.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length >= 2)
            {
                var timeMatch = timeRegex.Match(lines[1]);
                if (timeMatch.Success)
                {
                    var startTime = new TimeSpan(0,
                        int.Parse(timeMatch.Groups[1].Value),
                        int.Parse(timeMatch.Groups[2].Value),
                        int.Parse(timeMatch.Groups[3].Value))
                        .Add(TimeSpan.FromMilliseconds(int.Parse(timeMatch.Groups[4].Value)));

                    var endTime = new TimeSpan(0,
                        int.Parse(timeMatch.Groups[5].Value),
                        int.Parse(timeMatch.Groups[6].Value),
                        int.Parse(timeMatch.Groups[7].Value))
                        .Add(TimeSpan.FromMilliseconds(int.Parse(timeMatch.Groups[8].Value)));

                    var text = string.Join(" ", lines.Skip(2)).Trim();
                    if (!string.IsNullOrEmpty(text))
                    {
                        _lyrics.Add(new LyricLine
                        {
                            StartTime = startTime,
                            EndTime = endTime,
                            Text = text,
                            Words = ParseWords(text)
                        });
                    }
                }
            }
        }
    }

    private void ImportPlainText(string content)
    {
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var currentTime = TimeSpan.Zero;

        foreach (var line in lines)
        {
            var text = line.Trim();
            if (!string.IsNullOrEmpty(text))
            {
                _lyrics.Add(new LyricLine
                {
                    StartTime = currentTime,
                    EndTime = currentTime.Add(TimeSpan.FromSeconds(3)),
                    Text = text,
                    Words = ParseWords(text)
                });
                currentTime = currentTime.Add(TimeSpan.FromSeconds(3));
            }
        }
    }

    private List<LyricWord> ParseWords(string text)
    {
        var words = new List<LyricWord>();
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            words.Add(new LyricWord { Text = part });
        }

        return words;
    }

    private void OnExportClick(object sender, RoutedEventArgs e)
    {
        if (_lyrics.Count == 0)
        {
            MessageBox.Show("No lyrics to export.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "LRC files (*.lrc)|*.lrc|SRT files (*.srt)|*.srt",
            Title = "Export Lyrics"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
                string content = extension == ".srt" ? ExportSrt() : ExportLrc();
                File.WriteAllText(dialog.FileName, content);

                MessageBox.Show("Lyrics exported successfully.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export lyrics: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private string ExportLrc()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[ti:Lyrics]");
        sb.AppendLine("[ar:Artist]");
        sb.AppendLine();

        foreach (var line in _lyrics)
        {
            var timeStr = $"[{line.StartTime.Minutes:D2}:{line.StartTime.Seconds:D2}.{line.StartTime.Milliseconds / 10:D2}]";
            sb.AppendLine($"{timeStr}{line.Text}");
        }

        return sb.ToString();
    }

    private string ExportSrt()
    {
        var sb = new StringBuilder();
        int index = 1;

        foreach (var line in _lyrics)
        {
            sb.AppendLine(index.ToString());
            sb.AppendLine($"{FormatSrtTime(line.StartTime)} --> {FormatSrtTime(line.EndTime)}");
            sb.AppendLine(line.Text);
            sb.AppendLine();
            index++;
        }

        return sb.ToString();
    }

    private static string FormatSrtTime(TimeSpan time)
    {
        return $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2},{time.Milliseconds:D3}";
    }

    #endregion

    #region Tap Sync

    private void OnTapSyncToggle(object sender, RoutedEventArgs e)
    {
        if (_lyrics.Count == 0)
        {
            TapSyncButton.IsChecked = false;
            MessageBox.Show("Please import or add lyrics first.", "Tap Sync",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _isTapSyncActive = TapSyncButton.IsChecked == true;

        if (_isTapSyncActive)
        {
            _tapSyncIndex = 0;
            UpdateTapSyncDisplay();
            TapSyncOverlay.Visibility = Visibility.Visible;
            TapSyncOverlay.Focus();
        }
        else
        {
            TapSyncOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateTapSyncDisplay()
    {
        if (_tapSyncIndex < _lyrics.Count)
        {
            TapSyncCurrentLine.Text = _lyrics[_tapSyncIndex].Text;
            TapSyncProgress.Text = $"{_tapSyncIndex + 1} / {_lyrics.Count}";
        }
    }

    private void OnTapSyncTap(object sender, MouseButtonEventArgs e)
    {
        ProcessTapSync();
    }

    private void OnTapSyncKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            TapSyncButton.IsChecked = false;
            TapSyncOverlay.Visibility = Visibility.Collapsed;
            _isTapSyncActive = false;
        }
        else if (e.Key == Key.Space)
        {
            ProcessTapSync();
            e.Handled = true;
        }
    }

    private void ProcessTapSync()
    {
        if (!_isTapSyncActive || _tapSyncIndex >= _lyrics.Count) return;

        // Get current playback time
        PlaybackTimeRequested?.Invoke(this, _currentPlaybackTime);

        // Set the start time of current line
        _lyrics[_tapSyncIndex].StartTime = _currentPlaybackTime;

        // Set end time of previous line
        if (_tapSyncIndex > 0)
        {
            _lyrics[_tapSyncIndex - 1].EndTime = _currentPlaybackTime;
        }

        _tapSyncIndex++;

        if (_tapSyncIndex >= _lyrics.Count)
        {
            // Finished
            _lyrics[^1].EndTime = _currentPlaybackTime.Add(TimeSpan.FromSeconds(3));
            TapSyncButton.IsChecked = false;
            TapSyncOverlay.Visibility = Visibility.Collapsed;
            _isTapSyncActive = false;
            LyricsChanged?.Invoke(this, EventArgs.Empty);
            MessageBox.Show("Tap sync completed!", "Tap Sync", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            UpdateTapSyncDisplay();
        }
    }

    #endregion

    #region Line Editing

    private void OnLyricLineClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is LyricLine line)
        {
            SelectLine(line);
        }
    }

    private void OnEditLineClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is LyricLine line)
        {
            SelectLine(line);
        }
    }

    private void SelectLine(LyricLine line)
    {
        _selectedLine = line;
        TimeEditTextBox.Text = line.StartTime.ToString(@"mm\:ss\.fff");
        DurationEditTextBox.Text = line.Duration.TotalSeconds.ToString("F2", CultureInfo.InvariantCulture);
        TextEditTextBox.Text = line.Text;
    }

    private void OnApplyEditClick(object sender, RoutedEventArgs e)
    {
        if (_selectedLine == null) return;

        try
        {
            if (TimeSpan.TryParseExact(TimeEditTextBox.Text, @"mm\:ss\.fff", CultureInfo.InvariantCulture, out var startTime))
            {
                _selectedLine.StartTime = startTime;
            }

            if (double.TryParse(DurationEditTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var duration))
            {
                _selectedLine.EndTime = _selectedLine.StartTime.Add(TimeSpan.FromSeconds(duration));
            }

            _selectedLine.Text = TextEditTextBox.Text;
            _selectedLine.Words = ParseWords(TextEditTextBox.Text);

            LyricsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Invalid input: {ex.Message}", "Edit Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OnAddLineClick(object sender, RoutedEventArgs e)
    {
        var lastTime = _lyrics.Count > 0 ? _lyrics[^1].EndTime : TimeSpan.Zero;

        var newLine = new LyricLine
        {
            StartTime = lastTime,
            EndTime = lastTime.Add(TimeSpan.FromSeconds(3)),
            Text = "New line"
        };
        newLine.Words = ParseWords(newLine.Text);

        _lyrics.Add(newLine);
        SelectLine(newLine);
        LyricsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnDeleteLineClick(object sender, RoutedEventArgs e)
    {
        if (_selectedLine == null) return;

        _lyrics.Remove(_selectedLine);
        _selectedLine = null;
        TimeEditTextBox.Text = string.Empty;
        DurationEditTextBox.Text = string.Empty;
        TextEditTextBox.Text = string.Empty;
        LyricsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Preview

    private void OnPreviewPlayClick(object sender, RoutedEventArgs e)
    {
        if (PreviewPlayButton.IsChecked == true)
        {
            _previewTimer.Start();
        }
        else
        {
            _previewTimer.Stop();
        }
    }

    private void OnPreviewTimerTick(object? sender, EventArgs e)
    {
        // Request current playback time from parent
        PlaybackTimeRequested?.Invoke(this, _currentPlaybackTime);
        UpdatePreviewDisplay();
    }

    private void UpdatePreviewDisplay()
    {
        // Find current line based on playback time
        var currentLine = _lyrics.FirstOrDefault(l =>
            _currentPlaybackTime >= l.StartTime && _currentPlaybackTime < l.EndTime);

        var currentIndex = currentLine != null ? _lyrics.IndexOf(currentLine) : -1;

        // Update previous line
        if (currentIndex > 0)
        {
            PreviousLineText.Text = _lyrics[currentIndex - 1].Text;
            Canvas.SetTop(PreviousLineText, 20);
        }
        else
        {
            PreviousLineText.Text = string.Empty;
        }

        // Update current line
        if (currentLine != null)
        {
            CurrentLineText.Text = currentLine.Text;
            Canvas.SetTop(CurrentLineText, 50);

            // Word highlighting for word-level sync
            if (_syncMode == LyricSyncMode.Word && currentLine.Words.Count > 0)
            {
                UpdateWordHighlight(currentLine);
            }
            else
            {
                WordHighlight.Visibility = Visibility.Collapsed;
            }
        }
        else
        {
            CurrentLineText.Text = string.Empty;
            WordHighlight.Visibility = Visibility.Collapsed;
        }

        // Update next line
        if (currentIndex >= 0 && currentIndex < _lyrics.Count - 1)
        {
            NextLineText.Text = _lyrics[currentIndex + 1].Text;
            Canvas.SetTop(NextLineText, 90);
        }
        else
        {
            NextLineText.Text = string.Empty;
        }

        // Update active states
        foreach (var line in _lyrics)
        {
            line.IsActive = line == currentLine;
        }
    }

    private void UpdateWordHighlight(LyricLine line)
    {
        // Find active word based on timing
        var lineProgress = (_currentPlaybackTime - line.StartTime).TotalSeconds / line.Duration.TotalSeconds;
        var wordIndex = (int)(lineProgress * line.Words.Count);
        wordIndex = Math.Clamp(wordIndex, 0, line.Words.Count - 1);

        // Simple visual indication (full word highlighting would require measuring text)
        WordHighlight.Visibility = Visibility.Visible;
        WordHighlight.Width = 100;
        WordHighlight.Height = 30;
        Canvas.SetLeft(WordHighlight, 20 + wordIndex * 80);
        Canvas.SetTop(WordHighlight, 48);
    }

    #endregion

    private void OnSyncModeChanged(object sender, SelectionChangedEventArgs e)
    {
        _syncMode = SyncModeComboBox.SelectedIndex == 0 ? LyricSyncMode.Line : LyricSyncMode.Word;
    }

    private void OnAutoSyncClick(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Auto sync will distribute lyrics evenly across markers.\nThis feature requires markers to be set in the arrangement.",
            "Auto Sync", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>
    /// Sets lyrics from a list of LyricLine objects.
    /// </summary>
    public void SetLyrics(IEnumerable<LyricLine> lyrics)
    {
        _lyrics.Clear();
        foreach (var line in lyrics)
        {
            _lyrics.Add(line);
        }
    }

    /// <summary>
    /// Clears all lyrics.
    /// </summary>
    public void Clear()
    {
        _lyrics.Clear();
        _selectedLine = null;
        LyricsChanged?.Invoke(this, EventArgs.Empty);
    }
}
