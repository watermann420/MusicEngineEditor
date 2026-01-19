// VisualizationBridge.cs
// Central service that connects the MusicEngine to all visualization components.
// Ensures real-time synchronization between audio playback and visual feedback.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using MusicEngine.Core;
using MusicEngineEditor.Controls;
using MusicEngineEditor.Editor;

namespace MusicEngineEditor.Services;

/// <summary>
/// Central bridge that connects the MusicEngine to all visualization components.
/// Handles synchronization between code editor, punchcard, and audio playback.
/// </summary>
public class VisualizationBridge : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private Sequencer? _sequencer;
    private TextEditor? _editor;
    private PunchcardVisualization? _punchcard;
    private PlaybackHighlightService? _highlightService;

    private string _lastAnalyzedCode = "";
    private CodeAnalysisResult? _lastAnalysis;
    private bool _isDisposed;

    /// <summary>Event fired when visualization state changes.</summary>
    public event EventHandler<VisualizationStateEventArgs>? StateChanged;

    /// <summary>Event fired when an error occurs (but audio continues).</summary>
    public event EventHandler<VisualizationErrorEventArgs>? VisualizationError;

    public VisualizationBridge(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    #region Setup Methods

    /// <summary>
    /// Connects the bridge to a Sequencer for event subscription.
    /// </summary>
    public void ConnectSequencer(Sequencer sequencer)
    {
        DisconnectSequencer();

        _sequencer = sequencer;

        // Subscribe to sequencer events
        _sequencer.NoteTriggered += OnNoteTriggered;
        _sequencer.NoteEnded += OnNoteEnded;
        _sequencer.BeatChanged += OnBeatChanged;
        _sequencer.PlaybackStarted += OnPlaybackStarted;
        _sequencer.PlaybackStopped += OnPlaybackStopped;
        _sequencer.PatternAdded += OnPatternAdded;
        _sequencer.PatternRemoved += OnPatternRemoved;
        _sequencer.PatternsCleared += OnPatternsCleared;
        _sequencer.BpmChanged += OnBpmChanged;

        // Connect to punchcard
        _punchcard?.BindToSequencer(_sequencer);

        // Connect to highlight service
        _highlightService?.BindToSequencer(_sequencer);
    }

    /// <summary>
    /// Disconnects from the current Sequencer.
    /// </summary>
    public void DisconnectSequencer()
    {
        if (_sequencer == null) return;

        _sequencer.NoteTriggered -= OnNoteTriggered;
        _sequencer.NoteEnded -= OnNoteEnded;
        _sequencer.BeatChanged -= OnBeatChanged;
        _sequencer.PlaybackStarted -= OnPlaybackStarted;
        _sequencer.PlaybackStopped -= OnPlaybackStopped;
        _sequencer.PatternAdded -= OnPatternAdded;
        _sequencer.PatternRemoved -= OnPatternRemoved;
        _sequencer.PatternsCleared -= OnPatternsCleared;
        _sequencer.BpmChanged -= OnBpmChanged;

        _punchcard?.UnbindSequencer();
        _highlightService?.UnbindSequencer();

        _sequencer = null;
    }

    /// <summary>
    /// Connects the bridge to a TextEditor for code highlighting.
    /// </summary>
    public void ConnectEditor(TextEditor editor)
    {
        DisconnectEditor();

        _editor = editor;
        _highlightService = new PlaybackHighlightService(editor);

        if (_sequencer != null)
        {
            _highlightService.BindToSequencer(_sequencer);
        }
    }

    /// <summary>
    /// Disconnects from the current TextEditor.
    /// </summary>
    public void DisconnectEditor()
    {
        _highlightService?.Dispose();
        _highlightService = null;
        _editor = null;
    }

    /// <summary>
    /// Connects the bridge to a PunchcardVisualization.
    /// </summary>
    public void ConnectPunchcard(PunchcardVisualization punchcard)
    {
        _punchcard = punchcard;

        if (_sequencer != null)
        {
            _punchcard.BindToSequencer(_sequencer);
        }
    }

    /// <summary>
    /// Disconnects from the current PunchcardVisualization.
    /// </summary>
    public void DisconnectPunchcard()
    {
        _punchcard?.UnbindSequencer();
        _punchcard = null;
    }

    #endregion

    #region Code Analysis

    /// <summary>
    /// Analyzes code and sets up source tracking for visualization.
    /// Call this after code changes and before execution.
    /// </summary>
    public void AnalyzeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        if (code == _lastAnalyzedCode) return;

        try
        {
            _lastAnalyzedCode = code;
            _lastAnalysis = CodeSourceAnalyzer.Analyze(code);

            // Register instruments for glow/dim effects
            if (_highlightService != null)
            {
                _highlightService.ClearInstruments();
                foreach (var kvp in _lastAnalysis.InstrumentCodeRegions)
                {
                    _highlightService.RegisterInstrument(kvp.Key, kvp.Value);
                }
            }
        }
        catch (Exception ex)
        {
            OnVisualizationError("Code analysis failed", ex);
        }
    }

    /// <summary>
    /// Attaches source info to patterns after script execution.
    /// Call this after ExecuteScriptAsync completes successfully.
    /// </summary>
    public void AttachSourceInfoToPatterns()
    {
        if (_sequencer == null || _lastAnalysis == null) return;

        try
        {
            CodeSourceAnalyzer.AttachSourceInfoToPatterns(_lastAnalyzedCode, _sequencer.Patterns);
        }
        catch (Exception ex)
        {
            OnVisualizationError("Failed to attach source info", ex);
        }
    }

    /// <summary>
    /// Updates the punchcard with current patterns from the sequencer.
    /// </summary>
    public void SyncPunchcardPatterns()
    {
        if (_punchcard == null || _sequencer == null) return;

        try
        {
            _punchcard.UpdatePatternsFromSequencer(_sequencer.Patterns);
        }
        catch (Exception ex)
        {
            OnVisualizationError("Failed to sync punchcard", ex);
        }
    }

    #endregion

    #region Sequencer Event Handlers

    private void OnNoteTriggered(object? sender, MusicalEventArgs e)
    {
        // Event is already forwarded to highlight service via binding
        // This is for additional processing if needed

        SafeDispatch(() =>
        {
            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.NoteOn,
                Event = e.Event
            });
        });
    }

    private void OnNoteEnded(object? sender, MusicalEventArgs e)
    {
        SafeDispatch(() =>
        {
            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.NoteOff,
                Event = e.Event
            });
        });
    }

    private void OnBeatChanged(object? sender, BeatChangedEventArgs e)
    {
        // Punchcard sync is handled by direct binding
        // This is for additional UI updates if needed
    }

    private void OnPlaybackStarted(object? sender, PlaybackStateEventArgs e)
    {
        SafeDispatch(() =>
        {
            _highlightService?.Start();
            _punchcard?.StartSync();

            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.PlaybackStarted
            });
        });
    }

    private void OnPlaybackStopped(object? sender, PlaybackStateEventArgs e)
    {
        SafeDispatch(() =>
        {
            _highlightService?.Stop();
            _punchcard?.StopSync();

            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.PlaybackStopped
            });
        });
    }

    private void OnPatternAdded(object? sender, MusicEngine.Core.Pattern e)
    {
        SafeDispatch(() =>
        {
            SyncPunchcardPatterns();

            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.PatternAdded
            });
        });
    }

    private void OnPatternRemoved(object? sender, MusicEngine.Core.Pattern e)
    {
        SafeDispatch(() =>
        {
            SyncPunchcardPatterns();

            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.PatternRemoved
            });
        });
    }

    private void OnPatternsCleared(object? sender, EventArgs e)
    {
        SafeDispatch(() =>
        {
            _punchcard?.ClearPatterns();

            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.PatternsCleared
            });
        });
    }

    private void OnBpmChanged(object? sender, ParameterChangedEventArgs e)
    {
        SafeDispatch(() =>
        {
            StateChanged?.Invoke(this, new VisualizationStateEventArgs
            {
                EventType = VisualizationEventType.BpmChanged,
                NewBpm = e.NewValue
            });
        });
    }

    #endregion

    #region Helper Methods

    private void SafeDispatch(Action action)
    {
        try
        {
            if (_dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                _dispatcher.BeginInvoke(action);
            }
        }
        catch (Exception ex)
        {
            OnVisualizationError("Dispatch error", ex);
        }
    }

    private void OnVisualizationError(string message, Exception ex)
    {
        // Log error but never stop audio
        SafeDispatch(() =>
        {
            VisualizationError?.Invoke(this, new VisualizationErrorEventArgs
            {
                Message = message,
                Exception = ex
            });
        });
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        DisconnectSequencer();
        DisconnectEditor();
        DisconnectPunchcard();
    }

    #endregion
}

#region Event Args

public enum VisualizationEventType
{
    NoteOn,
    NoteOff,
    PlaybackStarted,
    PlaybackStopped,
    PatternAdded,
    PatternRemoved,
    PatternsCleared,
    BpmChanged
}

public class VisualizationStateEventArgs : EventArgs
{
    public VisualizationEventType EventType { get; set; }
    public MusicalEvent? Event { get; set; }
    public double NewBpm { get; set; }
}

public class VisualizationErrorEventArgs : EventArgs
{
    public string Message { get; set; } = "";
    public Exception? Exception { get; set; }
}

#endregion
