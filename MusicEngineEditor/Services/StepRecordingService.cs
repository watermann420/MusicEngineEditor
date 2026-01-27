// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio/MIDI recording service.

using System;
using System.Collections.Generic;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for step-time MIDI input recording.
/// Allows stepping through time positions and recording notes one at a time.
/// </summary>
public sealed partial class StepRecordingService : ObservableObject, IDisposable
{
    #region Singleton

    private static readonly Lazy<StepRecordingService> _instance = new(
        () => new StepRecordingService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the StepRecordingService.
    /// </summary>
    public static StepRecordingService Instance => _instance.Value;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Whether step recording is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Current step position in beats.
    /// </summary>
    [ObservableProperty]
    private double _currentPosition;

    /// <summary>
    /// Step size in beats (note duration).
    /// </summary>
    [ObservableProperty]
    private double _stepSize = 0.25;

    /// <summary>
    /// Whether to use velocity from MIDI input.
    /// </summary>
    [ObservableProperty]
    private bool _useInputVelocity = true;

    /// <summary>
    /// Fixed velocity when not using input velocity (0-127).
    /// </summary>
    [ObservableProperty]
    private int _fixedVelocity = 100;

    /// <summary>
    /// Whether triplet mode is enabled (multiplies step size by 2/3).
    /// </summary>
    [ObservableProperty]
    private bool _tripletMode;

    /// <summary>
    /// Whether dotted mode is enabled (multiplies step size by 1.5).
    /// </summary>
    [ObservableProperty]
    private bool _dottedMode;

    /// <summary>
    /// Whether to tie notes (extend previous note instead of creating new).
    /// </summary>
    [ObservableProperty]
    private bool _tieMode;

    /// <summary>
    /// Current note duration multiplier (1.0 = step size).
    /// </summary>
    [ObservableProperty]
    private double _durationMultiplier = 1.0;

    #endregion

    #region Internal State

    /// <summary>
    /// The currently held notes for chord input (internal state, not observable).
    /// </summary>
    private readonly List<PendingNote> _heldNotes = [];

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a note should be added to the pattern.
    /// </summary>
    public event EventHandler<StepNoteEventArgs>? NoteAdded;

    /// <summary>
    /// Occurs when a rest is inserted.
    /// </summary>
    public event EventHandler<StepRestEventArgs>? RestInserted;

    /// <summary>
    /// Occurs when a note is tied (extended).
    /// </summary>
    public event EventHandler<StepTieEventArgs>? NoteTied;

    /// <summary>
    /// Occurs when the step position changes.
    /// </summary>
    public event EventHandler<double>? PositionChanged;

    /// <summary>
    /// Occurs when step recording is started or stopped.
    /// </summary>
    public event EventHandler<bool>? RecordingStateChanged;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private bool _disposed;
    private PianoRollNote? _lastNote;
    private readonly List<PianoRollNote> _recentNotes = [];

    #endregion

    #region Constructor

    private StepRecordingService()
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts step recording at the specified position.
    /// </summary>
    /// <param name="startPosition">Starting position in beats.</param>
    public void Start(double startPosition = 0)
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            CurrentPosition = Math.Max(0, startPosition);
            IsActive = true;
            _heldNotes.Clear();
            _lastNote = null;
            _recentNotes.Clear();

            RecordingStateChanged?.Invoke(this, true);
        }
    }

    /// <summary>
    /// Stops step recording.
    /// </summary>
    public void Stop()
    {
        ThrowIfDisposed();

        lock (_lock)
        {
            // Commit any held notes before stopping
            CommitHeldNotes();

            IsActive = false;
            _heldNotes.Clear();

            RecordingStateChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Processes a MIDI note on event.
    /// </summary>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="velocity">Note velocity (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void NoteOn(int noteNumber, int velocity, int channel = 0)
    {
        ThrowIfDisposed();

        if (!IsActive) return;

        lock (_lock)
        {
            var pendingNote = new PendingNote
            {
                NoteNumber = noteNumber,
                Velocity = UseInputVelocity ? velocity : FixedVelocity,
                Channel = channel,
                StartTime = DateTime.UtcNow
            };

            _heldNotes.Add(pendingNote);
        }
    }

    /// <summary>
    /// Processes a MIDI note off event.
    /// </summary>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void NoteOff(int noteNumber, int channel = 0)
    {
        ThrowIfDisposed();

        if (!IsActive) return;

        lock (_lock)
        {
            var heldNote = _heldNotes.Find(n => n.NoteNumber == noteNumber && n.Channel == channel);
            if (heldNote != null)
            {
                // If this is the last held note, commit all notes
                _heldNotes.Remove(heldNote);

                if (_heldNotes.Count == 0)
                {
                    CommitNote(heldNote);
                }
            }
        }
    }

    /// <summary>
    /// Inserts a rest at the current position and advances.
    /// </summary>
    public void InsertRest()
    {
        ThrowIfDisposed();

        if (!IsActive) return;

        lock (_lock)
        {
            var effectiveStep = GetEffectiveStepSize();
            var oldPosition = CurrentPosition;

            CurrentPosition += effectiveStep;

            RestInserted?.Invoke(this, new StepRestEventArgs(oldPosition, effectiveStep));
            PositionChanged?.Invoke(this, CurrentPosition);
        }
    }

    /// <summary>
    /// Steps forward without adding a note or rest.
    /// </summary>
    public void StepForward()
    {
        ThrowIfDisposed();

        if (!IsActive) return;

        lock (_lock)
        {
            CurrentPosition += GetEffectiveStepSize();
            PositionChanged?.Invoke(this, CurrentPosition);
        }
    }

    /// <summary>
    /// Steps backward.
    /// </summary>
    public void StepBackward()
    {
        ThrowIfDisposed();

        if (!IsActive) return;

        lock (_lock)
        {
            CurrentPosition = Math.Max(0, CurrentPosition - GetEffectiveStepSize());
            PositionChanged?.Invoke(this, CurrentPosition);
        }
    }

    /// <summary>
    /// Sets the step size based on a note value.
    /// </summary>
    /// <param name="noteValue">Note value (e.g., 4 = quarter, 8 = eighth).</param>
    public void SetStepFromNoteValue(int noteValue)
    {
        StepSize = noteValue switch
        {
            1 => 4.0,    // Whole note
            2 => 2.0,    // Half note
            4 => 1.0,    // Quarter note
            8 => 0.5,    // Eighth note
            16 => 0.25,  // Sixteenth note
            32 => 0.125, // 32nd note
            64 => 0.0625,// 64th note
            _ => 0.25    // Default to sixteenth
        };
    }

    /// <summary>
    /// Gets the effective step size considering triplet and dotted modes.
    /// </summary>
    /// <returns>The effective step size in beats.</returns>
    public double GetEffectiveStepSize()
    {
        var step = StepSize;

        if (TripletMode)
        {
            step *= 2.0 / 3.0; // Triplet
        }
        else if (DottedMode)
        {
            step *= 1.5; // Dotted
        }

        return step;
    }

    /// <summary>
    /// Ties the last note (extends its duration).
    /// </summary>
    public void TieLastNote()
    {
        ThrowIfDisposed();

        if (!IsActive || _lastNote == null) return;

        lock (_lock)
        {
            var extensionAmount = GetEffectiveStepSize();
            var oldDuration = _lastNote.Duration;
            _lastNote.Duration += extensionAmount;

            NoteTied?.Invoke(this, new StepTieEventArgs(_lastNote, oldDuration, extensionAmount));

            // Don't advance position when tying
        }
    }

    /// <summary>
    /// Clears the last recorded note (undo last step).
    /// </summary>
    public void UndoLastNote()
    {
        ThrowIfDisposed();

        if (!IsActive || _recentNotes.Count == 0) return;

        lock (_lock)
        {
            var lastNote = _recentNotes[^1];
            _recentNotes.RemoveAt(_recentNotes.Count - 1);

            // Move position back
            CurrentPosition = lastNote.StartBeat;
            _lastNote = _recentNotes.Count > 0 ? _recentNotes[^1] : null;

            PositionChanged?.Invoke(this, CurrentPosition);
        }
    }

    #endregion

    #region Private Methods

    private void CommitHeldNotes()
    {
        foreach (var note in _heldNotes.ToArray())
        {
            CommitNote(note);
        }
        _heldNotes.Clear();
    }

    private void CommitNote(PendingNote pendingNote)
    {
        var effectiveStep = GetEffectiveStepSize();
        var duration = effectiveStep * DurationMultiplier;

        if (TieMode && _lastNote != null && _lastNote.Note == pendingNote.NoteNumber)
        {
            // Tie mode: extend the last note
            var oldDuration = _lastNote.Duration;
            _lastNote.Duration += duration;

            NoteTied?.Invoke(this, new StepTieEventArgs(_lastNote, oldDuration, duration));
        }
        else
        {
            // Create new note
            var note = new PianoRollNote
            {
                Note = pendingNote.NoteNumber,
                StartBeat = CurrentPosition,
                Duration = duration,
                Velocity = pendingNote.Velocity,
                Channel = pendingNote.Channel
            };

            _lastNote = note;
            _recentNotes.Add(note);

            NoteAdded?.Invoke(this, new StepNoteEventArgs(note, CurrentPosition));
        }

        // Advance position
        CurrentPosition += effectiveStep;
        PositionChanged?.Invoke(this, CurrentPosition);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(StepRecordingService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _heldNotes.Clear();
            _recentNotes.Clear();
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Represents a note that is currently being held during step recording.
/// </summary>
public class PendingNote
{
    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    public int NoteNumber { get; set; }

    /// <summary>
    /// Note velocity (0-127).
    /// </summary>
    public int Velocity { get; set; }

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    public int Channel { get; set; }

    /// <summary>
    /// Time when the note was pressed.
    /// </summary>
    public DateTime StartTime { get; set; }
}

/// <summary>
/// Event arguments for when a note is added during step recording.
/// </summary>
public sealed class StepNoteEventArgs : EventArgs
{
    /// <summary>
    /// The note that was added.
    /// </summary>
    public PianoRollNote Note { get; }

    /// <summary>
    /// The position where the note was added.
    /// </summary>
    public double Position { get; }

    public StepNoteEventArgs(PianoRollNote note, double position)
    {
        Note = note;
        Position = position;
    }
}

/// <summary>
/// Event arguments for when a rest is inserted during step recording.
/// </summary>
public sealed class StepRestEventArgs : EventArgs
{
    /// <summary>
    /// The position where the rest was inserted.
    /// </summary>
    public double Position { get; }

    /// <summary>
    /// The duration of the rest in beats.
    /// </summary>
    public double Duration { get; }

    public StepRestEventArgs(double position, double duration)
    {
        Position = position;
        Duration = duration;
    }
}

/// <summary>
/// Event arguments for when a note is tied (extended).
/// </summary>
public sealed class StepTieEventArgs : EventArgs
{
    /// <summary>
    /// The note that was tied.
    /// </summary>
    public PianoRollNote Note { get; }

    /// <summary>
    /// The previous duration before tying.
    /// </summary>
    public double PreviousDuration { get; }

    /// <summary>
    /// The amount added to the duration.
    /// </summary>
    public double ExtensionAmount { get; }

    public StepTieEventArgs(PianoRollNote note, double previousDuration, double extensionAmount)
    {
        Note = note;
        PreviousDuration = previousDuration;
        ExtensionAmount = extensionAmount;
    }
}

#endregion
