// PlaybackHighlightRenderer.cs
// Renders real-time visual feedback in the code editor synchronized to audio playback.
// Shows white rectangular outlines around code that is currently playing.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using MusicEngine.Core;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Represents a highlighted region in the editor with animation state.
/// </summary>
public class HighlightedRegion
{
    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string InstrumentName { get; set; } = "";
    public MusicalEvent? Event { get; set; }
    public DateTime HighlightStart { get; set; }
    public DateTime HighlightEnd { get; set; }
    public double Opacity { get; set; } = 1.0;
    public bool IsFadingOut { get; set; }

    /// <summary>Current animation progress (0.0 to 1.0).</summary>
    public double Progress => Event?.PlayProgress ?? 1.0;

    /// <summary>Whether this highlight is still active.</summary>
    public bool IsActive => DateTime.Now < HighlightEnd && !IsFadingOut;
}

/// <summary>
/// Manages instrument states for glow/dim visualization.
/// </summary>
public class InstrumentState
{
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public double Brightness { get; set; } = 0.3; // 0.3 = dimmed, 1.0 = fully bright
    public double TargetBrightness { get; set; } = 0.3;
    public DateTime LastActiveTime { get; set; }
    public List<(int Start, int End)> CodeRegions { get; set; } = new();

    /// <summary>Time in ms for fade transitions.</summary>
    public const double FadeInDuration = 150;
    public const double FadeOutDuration = 400;
}

/// <summary>
/// Background renderer that draws playback highlights on the code editor.
/// This is visual-only and does not modify the text.
/// </summary>
public class PlaybackHighlightRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private readonly List<HighlightedRegion> _activeHighlights = new();
    private readonly Dictionary<string, InstrumentState> _instrumentStates = new();
    private readonly object _lock = new();

    // Visual settings
    private static readonly Pen ActiveNotePen = new(new SolidColorBrush(Color.FromArgb(255, 255, 255, 255)), 2.0);
    private static readonly Pen FadingNotePen = new(new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)), 1.5);
    private static readonly Brush ActiveNoteFill = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
    private static readonly Brush GlowBrush = new SolidColorBrush(Color.FromArgb(60, 100, 200, 255));

    // Animation
    private readonly DispatcherTimer _animationTimer;
    private DateTime _lastFrame;

    static PlaybackHighlightRenderer()
    {
        ActiveNotePen.Freeze();
        FadingNotePen.Freeze();
        ActiveNoteFill.Freeze();
        GlowBrush.Freeze();
    }

    public PlaybackHighlightRenderer(TextEditor editor)
    {
        _editor = editor;
        _lastFrame = DateTime.Now;

        // Animation timer for smooth updates (~60fps)
        _animationTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _animationTimer.Tick += AnimationTimer_Tick;
    }

    /// <summary>Gets the layer on which to draw (behind the text).</summary>
    public KnownLayer Layer => KnownLayer.Background;

    /// <summary>Starts the animation timer.</summary>
    public void Start()
    {
        _animationTimer.Start();
    }

    /// <summary>Stops the animation timer.</summary>
    public void Stop()
    {
        _animationTimer.Stop();
        lock (_lock)
        {
            _activeHighlights.Clear();
            foreach (var state in _instrumentStates.Values)
            {
                state.IsActive = false;
                state.TargetBrightness = 0.3;
            }
        }
        _editor.TextArea.TextView.InvalidateLayer(Layer);
    }

    /// <summary>Registers an instrument and its code regions for glow/dim effects.</summary>
    public void RegisterInstrument(string name, List<(int Start, int End)> codeRegions)
    {
        lock (_lock)
        {
            if (!_instrumentStates.TryGetValue(name, out var state))
            {
                state = new InstrumentState { Name = name };
                _instrumentStates[name] = state;
            }
            state.CodeRegions = codeRegions;
        }
    }

    /// <summary>Clears all instrument registrations.</summary>
    public void ClearInstruments()
    {
        lock (_lock)
        {
            _instrumentStates.Clear();
        }
    }

    /// <summary>Called when a musical event starts playing.</summary>
    public void OnNoteTriggered(MusicalEvent musicalEvent)
    {
        if (musicalEvent.SourceInfo == null) return;

        lock (_lock)
        {
            var highlight = new HighlightedRegion
            {
                StartOffset = musicalEvent.SourceInfo.StartIndex,
                EndOffset = musicalEvent.SourceInfo.EndIndex,
                InstrumentName = musicalEvent.InstrumentName,
                Event = musicalEvent,
                HighlightStart = DateTime.Now,
                HighlightEnd = musicalEvent.EndsAt,
                Opacity = 1.0
            };
            _activeHighlights.Add(highlight);

            // Activate instrument glow
            if (_instrumentStates.TryGetValue(musicalEvent.InstrumentName, out var state))
            {
                state.IsActive = true;
                state.TargetBrightness = 1.0;
                state.LastActiveTime = DateTime.Now;
            }
        }
    }

    /// <summary>Called when a musical event ends.</summary>
    public void OnNoteEnded(MusicalEvent musicalEvent)
    {
        lock (_lock)
        {
            var highlight = _activeHighlights.FirstOrDefault(h => h.Event?.Id == musicalEvent.Id);
            if (highlight != null)
            {
                highlight.IsFadingOut = true;
            }
        }
    }

    /// <summary>Called on each beat change for playhead synchronization.</summary>
    public void OnBeatChanged(BeatChangedEventArgs args)
    {
        // This can be used for additional beat-synced effects
        // Currently the highlights are time-based, not beat-based
    }

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;
        var deltaTime = (now - _lastFrame).TotalMilliseconds;
        _lastFrame = now;

        bool needsRedraw = false;

        lock (_lock)
        {
            // Update active highlights
            for (int i = _activeHighlights.Count - 1; i >= 0; i--)
            {
                var highlight = _activeHighlights[i];

                if (highlight.IsFadingOut)
                {
                    // Fade out animation
                    highlight.Opacity -= deltaTime / 200.0; // 200ms fade out
                    if (highlight.Opacity <= 0)
                    {
                        _activeHighlights.RemoveAt(i);
                    }
                    needsRedraw = true;
                }
                else if (now >= highlight.HighlightEnd)
                {
                    // Start fading out when note ends
                    highlight.IsFadingOut = true;
                    needsRedraw = true;
                }
                else
                {
                    // Still active - check if we need to redraw for progress
                    needsRedraw = true;
                }
            }

            // Update instrument brightness with smooth transitions
            foreach (var state in _instrumentStates.Values)
            {
                // Check if instrument should dim (no active notes for a while)
                if (state.IsActive)
                {
                    bool hasActiveNotes = _activeHighlights.Any(h =>
                        h.InstrumentName == state.Name && !h.IsFadingOut);

                    if (!hasActiveNotes && (now - state.LastActiveTime).TotalMilliseconds > 100)
                    {
                        state.IsActive = false;
                        state.TargetBrightness = 0.3;
                    }
                }

                // Smooth brightness transition
                if (Math.Abs(state.Brightness - state.TargetBrightness) > 0.01)
                {
                    double speed = state.TargetBrightness > state.Brightness
                        ? deltaTime / InstrumentState.FadeInDuration
                        : deltaTime / InstrumentState.FadeOutDuration;

                    if (state.Brightness < state.TargetBrightness)
                    {
                        state.Brightness = Math.Min(state.Brightness + speed, state.TargetBrightness);
                    }
                    else
                    {
                        state.Brightness = Math.Max(state.Brightness - speed, state.TargetBrightness);
                    }
                    needsRedraw = true;
                }
            }
        }

        if (needsRedraw)
        {
            _editor.TextArea.TextView.InvalidateLayer(Layer);
        }
    }

    /// <summary>Main drawing method called by AvalonEdit.</summary>
    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        var document = textView.Document;
        if (document == null) return;

        lock (_lock)
        {
            // Draw instrument glow/dim effects first (background)
            DrawInstrumentGlow(textView, drawingContext, document);

            // Draw active note highlights on top
            DrawActiveHighlights(textView, drawingContext, document);
        }
    }

    private void DrawInstrumentGlow(TextView textView, DrawingContext drawingContext, TextDocument document)
    {
        foreach (var state in _instrumentStates.Values)
        {
            if (state.Brightness < 0.35) continue; // Don't draw if fully dimmed

            var brush = new SolidColorBrush(Color.FromArgb(
                (byte)(40 * state.Brightness),
                100, 180, 255));

            foreach (var region in state.CodeRegions)
            {
                if (region.Start >= document.TextLength || region.End > document.TextLength)
                    continue;

                var segment = new TextSegment { StartOffset = region.Start, EndOffset = region.End };

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    // Draw subtle glow behind the code
                    var glowRect = new Rect(
                        rect.X - 2,
                        rect.Y - 1,
                        rect.Width + 4,
                        rect.Height + 2);

                    drawingContext.DrawRoundedRectangle(brush, null, glowRect, 3, 3);
                }
            }
        }
    }

    private void DrawActiveHighlights(TextView textView, DrawingContext drawingContext, TextDocument document)
    {
        foreach (var highlight in _activeHighlights)
        {
            if (highlight.StartOffset >= document.TextLength ||
                highlight.EndOffset > document.TextLength)
                continue;

            var segment = new TextSegment
            {
                StartOffset = highlight.StartOffset,
                EndOffset = highlight.EndOffset
            };

            // Get visual rectangles for this text segment
            var rects = BackgroundGeometryBuilder.GetRectsForSegment(textView, segment).ToList();

            foreach (var rect in rects)
            {
                // Calculate opacity based on state
                double opacity = highlight.Opacity;
                if (!highlight.IsFadingOut && highlight.Event != null)
                {
                    // Pulse effect based on note progress
                    double progress = highlight.Event.PlayProgress;
                    opacity = 1.0 - (progress * 0.3); // Slight fade as note progresses
                }

                // Create brushes with current opacity
                var fillBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)(40 * opacity), 255, 255, 255));

                var strokeBrush = new SolidColorBrush(Color.FromArgb(
                    (byte)(255 * opacity), 255, 255, 255));

                var pen = new Pen(strokeBrush, highlight.IsFadingOut ? 1.5 : 2.0);

                // Draw the highlight rectangle
                var highlightRect = new Rect(
                    rect.X - 1,
                    rect.Y,
                    rect.Width + 2,
                    rect.Height);

                // Draw fill
                drawingContext.DrawRoundedRectangle(fillBrush, null, highlightRect, 2, 2);

                // Draw white outline
                drawingContext.DrawRoundedRectangle(null, pen, highlightRect, 2, 2);

                // Draw progress indicator (small line at bottom showing note duration)
                if (!highlight.IsFadingOut && highlight.Event != null)
                {
                    double progress = highlight.Event.PlayProgress;
                    var progressWidth = highlightRect.Width * progress;

                    var progressPen = new Pen(new SolidColorBrush(Color.FromArgb(
                        (byte)(200 * opacity), 100, 200, 255)), 2);

                    drawingContext.DrawLine(progressPen,
                        new Point(highlightRect.Left, highlightRect.Bottom),
                        new Point(highlightRect.Left + progressWidth, highlightRect.Bottom));
                }
            }
        }
    }

    /// <summary>Gets all currently highlighted regions for debugging/testing.</summary>
    public IReadOnlyList<HighlightedRegion> GetActiveHighlights()
    {
        lock (_lock)
        {
            return _activeHighlights.ToArray();
        }
    }
}

/// <summary>
/// Service to manage playback highlighting for a TextEditor.
/// Connects the MusicEngine events to the visual renderer.
/// </summary>
public class PlaybackHighlightService : IDisposable
{
    private readonly TextEditor _editor;
    private readonly PlaybackHighlightRenderer _renderer;
    private Sequencer? _sequencer;
    private bool _isDisposed;

    public PlaybackHighlightService(TextEditor editor)
    {
        _editor = editor;
        _renderer = new PlaybackHighlightRenderer(editor);
        _editor.TextArea.TextView.BackgroundRenderers.Add(_renderer);
    }

    /// <summary>Binds to a sequencer to receive events.</summary>
    public void BindToSequencer(Sequencer sequencer)
    {
        UnbindSequencer();

        _sequencer = sequencer;
        _sequencer.NoteTriggered += Sequencer_NoteTriggered;
        _sequencer.NoteEnded += Sequencer_NoteEnded;
        _sequencer.BeatChanged += Sequencer_BeatChanged;
        _sequencer.PlaybackStarted += Sequencer_PlaybackStarted;
        _sequencer.PlaybackStopped += Sequencer_PlaybackStopped;
    }

    /// <summary>Unbinds from the current sequencer.</summary>
    public void UnbindSequencer()
    {
        if (_sequencer != null)
        {
            _sequencer.NoteTriggered -= Sequencer_NoteTriggered;
            _sequencer.NoteEnded -= Sequencer_NoteEnded;
            _sequencer.BeatChanged -= Sequencer_BeatChanged;
            _sequencer.PlaybackStarted -= Sequencer_PlaybackStarted;
            _sequencer.PlaybackStopped -= Sequencer_PlaybackStopped;
            _sequencer = null;
        }
    }

    /// <summary>Registers instrument code regions for glow/dim effects.</summary>
    public void RegisterInstrument(string name, List<(int Start, int End)> codeRegions)
    {
        _renderer.RegisterInstrument(name, codeRegions);
    }

    /// <summary>Clears all registered instruments.</summary>
    public void ClearInstruments()
    {
        _renderer.ClearInstruments();
    }

    /// <summary>Starts the highlight animation.</summary>
    public void Start()
    {
        _renderer.Start();
    }

    /// <summary>Stops the highlight animation.</summary>
    public void Stop()
    {
        _renderer.Stop();
    }

    private void Sequencer_NoteTriggered(object? sender, MusicalEventArgs e)
    {
        _editor.Dispatcher.BeginInvoke(() => _renderer.OnNoteTriggered(e.Event));
    }

    private void Sequencer_NoteEnded(object? sender, MusicalEventArgs e)
    {
        _editor.Dispatcher.BeginInvoke(() => _renderer.OnNoteEnded(e.Event));
    }

    private void Sequencer_BeatChanged(object? sender, BeatChangedEventArgs e)
    {
        _editor.Dispatcher.BeginInvoke(() => _renderer.OnBeatChanged(e));
    }

    private void Sequencer_PlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        _editor.Dispatcher.BeginInvoke(() => _renderer.Start());
    }

    private void Sequencer_PlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        _editor.Dispatcher.BeginInvoke(() => _renderer.Stop());
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        UnbindSequencer();
        _renderer.Stop();
        _editor.TextArea.TextView.BackgroundRenderers.Remove(_renderer);
    }
}
