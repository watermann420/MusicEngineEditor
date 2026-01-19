using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A punchcard visualization control for displaying musical sequences and notes in a timeline.
/// Similar to Strudel.cc's visualization style.
/// </summary>
public partial class PunchcardVisualization : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 80.0;
    private const double DefaultTrackHeight = 40.0;
    private const double NoteHeight = 32.0;
    private const double NotePadding = 4.0;
    private const double MinNoteWidth = 4.0;
    private const int DefaultBeatsToShow = 16;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BeatWidthProperty =
        DependencyProperty.Register(nameof(BeatWidth), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultBeatWidth, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty TrackHeightProperty =
        DependencyProperty.Register(nameof(TrackHeight), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultTrackHeight, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty TotalBeatsProperty =
        DependencyProperty.Register(nameof(TotalBeats), typeof(int), typeof(PunchcardVisualization),
            new PropertyMetadata(DefaultBeatsToShow, OnVisualizationPropertyChanged));

    public static readonly DependencyProperty CurrentBeatProperty =
        DependencyProperty.Register(nameof(CurrentBeat), typeof(double), typeof(PunchcardVisualization),
            new PropertyMetadata(0.0, OnCurrentBeatChanged));

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(PunchcardVisualization),
            new PropertyMetadata(false));

    public double BeatWidth
    {
        get => (double)GetValue(BeatWidthProperty);
        set => SetValue(BeatWidthProperty, value);
    }

    public double TrackHeight
    {
        get => (double)GetValue(TrackHeightProperty);
        set => SetValue(TrackHeightProperty, value);
    }

    public int TotalBeats
    {
        get => (int)GetValue(TotalBeatsProperty);
        set => SetValue(TotalBeatsProperty, value);
    }

    public double CurrentBeat
    {
        get => (double)GetValue(CurrentBeatProperty);
        set => SetValue(CurrentBeatProperty, value);
    }

    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    #endregion

    #region Private Fields

    private readonly List<Pattern> _patterns = new();
    private readonly Dictionary<System.Windows.Shapes.Rectangle, NoteInfo> _noteRectangles = new();
    private Storyboard? _playheadAnimation;

    #endregion

    #region Constructor

    public PunchcardVisualization()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderVisualization();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderVisualization();
    }

    private static void OnVisualizationPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PunchcardVisualization visualization)
        {
            visualization.RenderVisualization();
        }
    }

    private static void OnCurrentBeatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PunchcardVisualization visualization)
        {
            visualization.UpdatePlayheadPosition();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a new pattern (track) to the visualization.
    /// </summary>
    /// <param name="pattern">The pattern to add.</param>
    public void AddPattern(Pattern pattern)
    {
        _patterns.Add(pattern);
        RenderVisualization();
    }

    /// <summary>
    /// Removes a pattern from the visualization.
    /// </summary>
    /// <param name="pattern">The pattern to remove.</param>
    /// <returns>True if the pattern was removed, false otherwise.</returns>
    public bool RemovePattern(Pattern pattern)
    {
        var result = _patterns.Remove(pattern);
        if (result)
        {
            RenderVisualization();
        }
        return result;
    }

    /// <summary>
    /// Removes a pattern by index.
    /// </summary>
    /// <param name="index">The index of the pattern to remove.</param>
    public void RemovePatternAt(int index)
    {
        if (index >= 0 && index < _patterns.Count)
        {
            _patterns.RemoveAt(index);
            RenderVisualization();
        }
    }

    /// <summary>
    /// Clears all patterns from the visualization.
    /// </summary>
    public void ClearPatterns()
    {
        _patterns.Clear();
        RenderVisualization();
    }

    /// <summary>
    /// Gets all patterns in the visualization.
    /// </summary>
    /// <returns>A read-only list of patterns.</returns>
    public IReadOnlyList<Pattern> GetPatterns() => _patterns.AsReadOnly();

    /// <summary>
    /// Updates the playhead position to the specified beat.
    /// </summary>
    /// <param name="currentBeat">The current beat position.</param>
    public void UpdatePlayhead(double currentBeat)
    {
        CurrentBeat = currentBeat;
    }

    /// <summary>
    /// Starts the playhead animation.
    /// </summary>
    /// <param name="bpm">Beats per minute.</param>
    /// <param name="startBeat">Starting beat position.</param>
    public void StartPlayheadAnimation(double bpm, double startBeat = 0)
    {
        StopPlayheadAnimation();

        IsPlaying = true;
        CurrentBeat = startBeat;

        var duration = TimeSpan.FromMinutes(TotalBeats / bpm);
        var animation = new DoubleAnimation
        {
            From = startBeat,
            To = TotalBeats,
            Duration = new Duration(duration),
            RepeatBehavior = RepeatBehavior.Forever
        };

        _playheadAnimation = new Storyboard();
        _playheadAnimation.Children.Add(animation);
        Storyboard.SetTarget(animation, this);
        Storyboard.SetTargetProperty(animation, new PropertyPath(CurrentBeatProperty));
        _playheadAnimation.Begin();
    }

    /// <summary>
    /// Stops the playhead animation.
    /// </summary>
    public void StopPlayheadAnimation()
    {
        IsPlaying = false;
        _playheadAnimation?.Stop();
        _playheadAnimation = null;
    }

    /// <summary>
    /// Pauses the playhead animation.
    /// </summary>
    public void PausePlayheadAnimation()
    {
        _playheadAnimation?.Pause();
        IsPlaying = false;
    }

    /// <summary>
    /// Resumes the playhead animation.
    /// </summary>
    public void ResumePlayheadAnimation()
    {
        _playheadAnimation?.Resume();
        IsPlaying = true;
    }

    #endregion

    #region Private Methods - Rendering

    private void RenderVisualization()
    {
        if (!IsLoaded) return;

        var totalWidth = TotalBeats * BeatWidth;
        var totalHeight = Math.Max(_patterns.Count, 1) * TrackHeight;

        // Set canvas sizes
        GridCanvas.Width = totalWidth;
        GridCanvas.Height = totalHeight;
        NotesCanvas.Width = totalWidth;
        NotesCanvas.Height = totalHeight;
        PlayheadCanvas.Width = totalWidth;
        PlayheadCanvas.Height = totalHeight;

        // Clear existing drawings
        GridCanvas.Children.Clear();
        NotesCanvas.Children.Clear();
        BeatLabelsCanvas.Children.Clear();
        _noteRectangles.Clear();

        // Render components
        RenderGrid(totalWidth, totalHeight);
        RenderBeatLabels();
        RenderTrackSeparators(totalWidth, totalHeight);
        RenderNotes();
        UpdatePlayheadPosition();
    }

    private void RenderGrid(double totalWidth, double totalHeight)
    {
        var gridLineBrush = FindResource("GridLineBrush") as SolidColorBrush ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x39, 0x3B, 0x40));
        var beatLineBrush = FindResource("BeatLineBrush") as SolidColorBrush ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4A, 0x4D, 0x52));

        // Draw vertical grid lines (beat divisions)
        for (int beat = 0; beat <= TotalBeats; beat++)
        {
            var x = beat * BeatWidth;
            var isMajorBeat = beat % 4 == 0;

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = totalHeight,
                Stroke = isMajorBeat ? beatLineBrush : gridLineBrush,
                StrokeThickness = isMajorBeat ? 1.5 : 0.5,
                Opacity = isMajorBeat ? 0.8 : 0.4
            };
            GridCanvas.Children.Add(line);

            // Draw sub-beat divisions (quarters)
            if (beat < TotalBeats)
            {
                for (int sub = 1; sub < 4; sub++)
                {
                    var subX = x + (sub * BeatWidth / 4);
                    var subLine = new Line
                    {
                        X1 = subX,
                        Y1 = 0,
                        X2 = subX,
                        Y2 = totalHeight,
                        Stroke = gridLineBrush,
                        StrokeThickness = 0.5,
                        Opacity = 0.2
                    };
                    GridCanvas.Children.Add(subLine);
                }
            }
        }
    }

    private void RenderBeatLabels()
    {
        var labelBrush = FindResource("BeatLabelBrush") as SolidColorBrush ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A));

        for (int beat = 0; beat <= TotalBeats; beat++)
        {
            // Show labels for every beat, but emphasize every 4th beat
            var isMajorBeat = beat % 4 == 0;

            var label = new TextBlock
            {
                Text = beat.ToString(),
                Foreground = labelBrush,
                FontSize = isMajorBeat ? 11 : 9,
                FontWeight = isMajorBeat ? FontWeights.SemiBold : FontWeights.Normal,
                Opacity = isMajorBeat ? 1.0 : 0.6
            };

            Canvas.SetLeft(label, beat * BeatWidth - 4);
            Canvas.SetTop(label, 4);
            BeatLabelsCanvas.Children.Add(label);
        }
    }

    private void RenderTrackSeparators(double totalWidth, double totalHeight)
    {
        var separatorBrush = FindResource("TrackSeparatorBrush") as SolidColorBrush ?? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x2B, 0x2D, 0x30));

        for (int i = 1; i < _patterns.Count; i++)
        {
            var y = i * TrackHeight;
            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = separatorBrush,
                StrokeThickness = 2
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderNotes()
    {
        for (int trackIndex = 0; trackIndex < _patterns.Count; trackIndex++)
        {
            var pattern = _patterns[trackIndex];
            var trackY = trackIndex * TrackHeight + NotePadding;

            foreach (var note in pattern.Notes)
            {
                RenderNote(note, trackY, pattern.Name);
            }
        }
    }

    private void RenderNote(Note note, double trackY, string patternName)
    {
        var noteX = note.StartBeat * BeatWidth;
        var noteWidth = Math.Max(note.Duration * BeatWidth - 2, MinNoteWidth);
        var noteColor = GetNoteColor(note.Pitch);

        var rect = new System.Windows.Shapes.Rectangle
        {
            Width = noteWidth,
            Height = NoteHeight,
            Fill = new SolidColorBrush(noteColor),
            RadiusX = 4,
            RadiusY = 4,
            Opacity = 0.85,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        // Add subtle border
        rect.Stroke = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 255, 255, 255));
        rect.StrokeThickness = 1;

        Canvas.SetLeft(rect, noteX + 1);
        Canvas.SetTop(rect, trackY);

        // Store note info for tooltip
        var noteInfo = new NoteInfo
        {
            Note = note,
            PatternName = patternName
        };
        _noteRectangles[rect] = noteInfo;

        // Add event handlers for hover
        rect.MouseEnter += OnNoteMouseEnter;
        rect.MouseLeave += OnNoteMouseLeave;
        rect.MouseMove += OnNoteMouseMove;

        NotesCanvas.Children.Add(rect);

        // Add note name label if note is wide enough
        if (noteWidth > 30)
        {
            var label = new TextBlock
            {
                Text = note.Name,
                Foreground = new SolidColorBrush(Colors.White),
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                IsHitTestVisible = false,
                Opacity = 0.9
            };

            Canvas.SetLeft(label, noteX + 6);
            Canvas.SetTop(label, trackY + (NoteHeight - 12) / 2);
            NotesCanvas.Children.Add(label);
        }
    }

    private void UpdatePlayheadPosition()
    {
        var x = CurrentBeat * BeatWidth;
        Canvas.SetLeft(Playhead, x);
        Playhead.Y2 = Math.Max(_patterns.Count, 1) * TrackHeight;
    }

    #endregion

    #region Private Methods - Note Colors

    /// <summary>
    /// Gets a color for a note based on its pitch using the HSL color wheel.
    /// </summary>
    private System.Windows.Media.Color GetNoteColor(int pitch)
    {
        // Normalize pitch to 0-11 (chromatic scale)
        var normalizedPitch = pitch % 12;

        // Map pitch to hue (0-360 degrees)
        // C=0 (red), E=4 (yellow-green), G=7 (cyan), etc.
        var hue = (normalizedPitch / 12.0) * 360.0;

        // Use high saturation and medium-high lightness for vivid colors
        const double saturation = 0.75;
        const double lightness = 0.55;

        return HslToRgb(hue, saturation, lightness);
    }

    /// <summary>
    /// Converts HSL color values to RGB Color.
    /// </summary>
    private static System.Windows.Media.Color HslToRgb(double h, double s, double l)
    {
        double r, g, b;

        if (Math.Abs(s) < 0.001)
        {
            r = g = b = l;
        }
        else
        {
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h / 360.0 + 1.0 / 3.0);
            g = HueToRgb(p, q, h / 360.0);
            b = HueToRgb(p, q, h / 360.0 - 1.0 / 3.0);
        }

        return System.Windows.Media.Color.FromRgb(
            (byte)(r * 255),
            (byte)(g * 255),
            (byte)(b * 255));
    }

    private static double HueToRgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
        if (t < 1.0 / 2.0) return q;
        if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
        return p;
    }

    #endregion

    #region Private Methods - Tooltip Handling

    private void OnNoteMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Shapes.Rectangle rect && _noteRectangles.TryGetValue(rect, out var noteInfo))
        {
            // Highlight the note
            rect.Opacity = 1.0;
            rect.StrokeThickness = 2;

            // Update tooltip content
            var note = noteInfo.Note;
            NoteTooltipText.Text = $"{noteInfo.PatternName}: {note.Name}\n" +
                                   $"Beat: {note.StartBeat:F2} - {note.StartBeat + note.Duration:F2}\n" +
                                   $"Duration: {note.Duration:F2} beats\n" +
                                   $"Velocity: {note.Velocity}";
            NoteTooltip.IsOpen = true;
        }
    }

    private void OnNoteMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Shapes.Rectangle rect)
        {
            // Restore normal appearance
            rect.Opacity = 0.85;
            rect.StrokeThickness = 1;
        }
        NoteTooltip.IsOpen = false;
    }

    private void OnNoteMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        // Tooltip follows mouse automatically via Placement="Mouse"
    }

    #endregion

    #region Nested Types

    /// <summary>
    /// Internal class to store note information for tooltips.
    /// </summary>
    private class NoteInfo
    {
        public Note Note { get; init; } = null!;
        public string PatternName { get; init; } = string.Empty;
    }

    #endregion
}

#region Data Models

/// <summary>
/// Represents a pattern (track) containing notes.
/// </summary>
public class Pattern
{
    /// <summary>
    /// Gets or sets the name of the pattern.
    /// </summary>
    public string Name { get; set; } = "Pattern";

    /// <summary>
    /// Gets or sets the notes in this pattern.
    /// </summary>
    public List<Note> Notes { get; set; } = new();

    /// <summary>
    /// Gets or sets an optional color override for all notes in this pattern.
    /// If null, notes will use pitch-based coloring.
    /// </summary>
    public System.Windows.Media.Color? ColorOverride { get; set; }
}

/// <summary>
/// Represents a single note in a pattern.
/// </summary>
public class Note
{
    /// <summary>
    /// Gets or sets the MIDI pitch (0-127).
    /// </summary>
    public int Pitch { get; set; }

    /// <summary>
    /// Gets or sets the start beat position.
    /// </summary>
    public double StartBeat { get; set; }

    /// <summary>
    /// Gets or sets the duration in beats.
    /// </summary>
    public double Duration { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the velocity (0-127).
    /// </summary>
    public int Velocity { get; set; } = 100;

    /// <summary>
    /// Gets the note name (e.g., "C4", "F#3").
    /// </summary>
    public string Name => GetNoteName(Pitch);

    /// <summary>
    /// Gets an optional custom label for the note.
    /// </summary>
    public string? CustomLabel { get; set; }

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string GetNoteName(int pitch)
    {
        var octave = (pitch / 12) - 1;
        var noteName = NoteNames[pitch % 12];
        return $"{noteName}{octave}";
    }
}

#endregion
