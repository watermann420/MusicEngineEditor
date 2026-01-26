// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Note preview service for piano roll and keyboard preview.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace MusicEngineEditor.Services;

/// <summary>
/// Waveform type for the preview synthesizer.
/// </summary>
public enum PreviewWaveform
{
    /// <summary>Sine wave - smooth and pure tone.</summary>
    Sine,
    /// <summary>Triangle wave - warmer than sine.</summary>
    Triangle,
    /// <summary>Square wave - hollow, digital sound.</summary>
    Square,
    /// <summary>Sawtooth wave - bright, buzzy sound.</summary>
    Sawtooth,
    /// <summary>Piano-like sound with harmonics.</summary>
    Piano
}

/// <summary>
/// Singleton service for previewing notes in the piano roll and keyboard.
/// Provides instant audio feedback when notes are clicked or keys are pressed.
/// </summary>
public sealed class NotePreviewService : IDisposable
{
    #region Singleton

    private static readonly Lazy<NotePreviewService> _instance = new(
        () => new NotePreviewService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the NotePreviewService.
    /// </summary>
    public static NotePreviewService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private SimpleSynth? _previewSynth;
    private WaveOutEvent? _waveOut;
    private VolumeSampleProvider? _volumeProvider;
    private bool _isInitialized;
    private bool _disposed;
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<int, CancellationTokenSource> _autoStopTokens = new();

    private float _volume = 0.7f;
    private PreviewWaveform _waveform = PreviewWaveform.Triangle;
    private int _defaultNoteOffDelayMs = 150;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the service has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets or sets the preview volume (0.0 to 1.0).
    /// </summary>
    public float Volume
    {
        get => _volume;
        set
        {
            _volume = Math.Clamp(value, 0f, 1f);
            if (_volumeProvider != null)
            {
                _volumeProvider.Volume = _volume;
            }
        }
    }

    /// <summary>
    /// Gets or sets the waveform type for the preview synth.
    /// </summary>
    public PreviewWaveform Waveform
    {
        get => _waveform;
        set
        {
            if (_waveform != value)
            {
                _waveform = value;
                ApplyWaveform();
            }
        }
    }

    /// <summary>
    /// Gets or sets the default note-off delay in milliseconds.
    /// This is used when PlayNote is called without explicit duration.
    /// </summary>
    public int DefaultNoteOffDelayMs
    {
        get => _defaultNoteOffDelayMs;
        set => _defaultNoteOffDelayMs = Math.Clamp(value, 50, 2000);
    }

    /// <summary>
    /// Gets or sets whether note preview is enabled globally.
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    #endregion

    #region Events

    /// <summary>
    /// Raised when a note preview starts.
    /// </summary>
    public event EventHandler<NotePreviewStartedEventArgs>? NotePreviewStarted;

    /// <summary>
    /// Raised when a note preview stops.
    /// </summary>
    public event EventHandler<NotePreviewStoppedEventArgs>? NotePreviewStopped;

    #endregion

    #region Constructor

    private NotePreviewService()
    {
        // Private constructor for singleton
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the note preview service with a dedicated synth and audio output.
    /// </summary>
    /// <param name="sampleRate">Optional sample rate (uses default if not specified).</param>
    public void Initialize(int? sampleRate = null)
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                // Create the preview synth
                _previewSynth = new SimpleSynth(sampleRate);
                _previewSynth.Name = "NotePreviewSynth";
                ApplyWaveform();

                // Create volume provider
                _volumeProvider = new VolumeSampleProvider(_previewSynth)
                {
                    Volume = _volume
                };

                // Create dedicated output for preview
                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50 // Low latency for responsive preview
                };
                _waveOut.Init(_volumeProvider);
                _waveOut.Play();

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("[NotePreviewService] Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NotePreviewService] Initialization failed: {ex.Message}");
                Cleanup();
                throw;
            }
        }
    }

    /// <summary>
    /// Ensures the service is initialized, initializing it if necessary.
    /// </summary>
    public void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            Initialize();
        }
    }

    #endregion

    #region Note Playback

    /// <summary>
    /// Plays a note preview with automatic note-off after a delay.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <param name="velocity">The velocity (0-127). Defaults to 100.</param>
    /// <param name="durationMs">Duration in milliseconds before auto note-off. Uses DefaultNoteOffDelayMs if not specified.</param>
    public void PlayNote(int midiNote, int velocity = 100, int? durationMs = null)
    {
        if (!IsEnabled) return;
        ThrowIfDisposed();
        EnsureInitialized();

        if (_previewSynth == null) return;

        // Validate inputs
        midiNote = Math.Clamp(midiNote, 0, 127);
        velocity = Math.Clamp(velocity, 1, 127);

        // Cancel any existing auto-stop for this note
        CancelAutoStop(midiNote);

        // Send note on
        _previewSynth.NoteOn(midiNote, velocity);
        NotePreviewStarted?.Invoke(this, new NotePreviewStartedEventArgs(midiNote, velocity));

        // Schedule auto note-off
        int duration = durationMs ?? _defaultNoteOffDelayMs;
        ScheduleAutoStop(midiNote, duration);
    }

    /// <summary>
    /// Starts playing a note without automatic note-off.
    /// Call StopNote to release the note.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <param name="velocity">The velocity (0-127). Defaults to 100.</param>
    public void StartNote(int midiNote, int velocity = 100)
    {
        if (!IsEnabled) return;
        ThrowIfDisposed();
        EnsureInitialized();

        if (_previewSynth == null) return;

        // Validate inputs
        midiNote = Math.Clamp(midiNote, 0, 127);
        velocity = Math.Clamp(velocity, 1, 127);

        // Cancel any existing auto-stop for this note
        CancelAutoStop(midiNote);

        // Send note on
        _previewSynth.NoteOn(midiNote, velocity);
        NotePreviewStarted?.Invoke(this, new NotePreviewStartedEventArgs(midiNote, velocity));
    }

    /// <summary>
    /// Stops a note that was started with StartNote.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    public void StopNote(int midiNote)
    {
        ThrowIfDisposed();

        if (_previewSynth == null) return;

        // Cancel any pending auto-stop
        CancelAutoStop(midiNote);

        // Send note off
        midiNote = Math.Clamp(midiNote, 0, 127);
        _previewSynth.NoteOff(midiNote);
        NotePreviewStopped?.Invoke(this, new NotePreviewStoppedEventArgs(midiNote));
    }

    /// <summary>
    /// Stops all currently playing preview notes.
    /// </summary>
    public void StopAllNotes()
    {
        ThrowIfDisposed();

        // Cancel all auto-stop timers
        foreach (var kvp in _autoStopTokens)
        {
            kvp.Value.Cancel();
        }
        _autoStopTokens.Clear();

        // Send all notes off
        _previewSynth?.AllNotesOff();
    }

    /// <summary>
    /// Plays a chord (multiple notes simultaneously).
    /// </summary>
    /// <param name="midiNotes">Array of MIDI note numbers.</param>
    /// <param name="velocity">The velocity for all notes.</param>
    /// <param name="durationMs">Duration before auto note-off.</param>
    public void PlayChord(int[] midiNotes, int velocity = 100, int? durationMs = null)
    {
        if (!IsEnabled || midiNotes == null || midiNotes.Length == 0) return;

        foreach (var note in midiNotes)
        {
            PlayNote(note, velocity, durationMs);
        }
    }

    #endregion

    #region Auto-Stop Management

    private void ScheduleAutoStop(int midiNote, int delayMs)
    {
        var cts = new CancellationTokenSource();
        _autoStopTokens[midiNote] = cts;

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delayMs, cts.Token);
                if (!cts.Token.IsCancellationRequested && _previewSynth != null)
                {
                    _previewSynth.NoteOff(midiNote);
                    NotePreviewStopped?.Invoke(this, new NotePreviewStoppedEventArgs(midiNote));
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when cancelled
            }
            finally
            {
                _autoStopTokens.TryRemove(midiNote, out _);
            }
        }, cts.Token);
    }

    private void CancelAutoStop(int midiNote)
    {
        if (_autoStopTokens.TryRemove(midiNote, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    #endregion

    #region Waveform Configuration

    private void ApplyWaveform()
    {
        if (_previewSynth == null) return;

        var waveType = _waveform switch
        {
            PreviewWaveform.Sine => WaveType.Sine,
            PreviewWaveform.Triangle => WaveType.Triangle,
            PreviewWaveform.Square => WaveType.Square,
            PreviewWaveform.Sawtooth => WaveType.Sawtooth,
            PreviewWaveform.Piano => WaveType.Triangle, // Triangle with filter sounds piano-like
            _ => WaveType.Triangle
        };

        _previewSynth.Waveform = waveType;

        // Apply filter settings for piano-like sound
        if (_waveform == PreviewWaveform.Piano)
        {
            _previewSynth.Cutoff = 0.6f;
            _previewSynth.Resonance = 0.2f;
        }
        else
        {
            _previewSynth.Cutoff = 1.0f;
            _previewSynth.Resonance = 0.0f;
        }
    }

    #endregion

    #region Cleanup

    private void Cleanup()
    {
        StopAllNotes();

        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _volumeProvider = null;
        _previewSynth = null;
        _isInitialized = false;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NotePreviewService));
        }
    }

    /// <summary>
    /// Disposes the note preview service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Cleanup();

            // Dispose all cancellation tokens
            foreach (var kvp in _autoStopTokens)
            {
                kvp.Value.Dispose();
            }
            _autoStopTokens.Clear();
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for note preview started event.
/// </summary>
public sealed class NotePreviewStartedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the MIDI note number.
    /// </summary>
    public int MidiNote { get; }

    /// <summary>
    /// Gets the velocity.
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gets the note name (e.g., "C4", "F#5").
    /// </summary>
    public string NoteName => GetNoteName(MidiNote);

    public NotePreviewStartedEventArgs(int midiNote, int velocity)
    {
        MidiNote = midiNote;
        Velocity = velocity;
    }

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }
}

/// <summary>
/// Event arguments for note preview stopped event.
/// </summary>
public sealed class NotePreviewStoppedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the MIDI note number.
    /// </summary>
    public int MidiNote { get; }

    /// <summary>
    /// Gets the note name (e.g., "C4", "F#5").
    /// </summary>
    public string NoteName => GetNoteName(MidiNote);

    public NotePreviewStoppedEventArgs(int midiNote)
    {
        MidiNote = midiNote;
    }

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }
}

#endregion
