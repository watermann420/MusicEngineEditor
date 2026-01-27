// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Step Sequencer / Drum Editor control.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Direction modes for step sequencer playback.
/// </summary>
public enum PlaybackDirection
{
    /// <summary>Play steps forward (1-2-3-4...).</summary>
    Forward,
    /// <summary>Play steps in reverse (4-3-2-1...).</summary>
    Reverse,
    /// <summary>Alternate between forward and reverse.</summary>
    PingPong,
    /// <summary>Random step selection.</summary>
    Random
}

/// <summary>
/// Represents a single step in the step sequencer with probability support.
/// </summary>
public partial class SequencerStepViewModel : ObservableObject
{
    /// <summary>
    /// Step index (0-based).
    /// </summary>
    [ObservableProperty]
    private int _stepIndex;

    /// <summary>
    /// Whether the step is active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Velocity (0-127).
    /// </summary>
    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Probability of triggering (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _probability = 1.0;

    /// <summary>
    /// Whether this step is currently playing.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Whether this step has an accent.
    /// </summary>
    [ObservableProperty]
    private bool _hasAccent;

    /// <summary>
    /// Retrigger count (1 = normal, 2+ = ratchet).
    /// </summary>
    [ObservableProperty]
    private int _retrigger = 1;

    /// <summary>
    /// Gets the normalized velocity (0.0 - 1.0).
    /// </summary>
    public double NormalizedVelocity => Velocity / 127.0;

    /// <summary>
    /// Gets the display opacity based on probability.
    /// </summary>
    public double ProbabilityOpacity => IsActive ? Math.Max(0.3, Probability) : 0.15;

    /// <summary>
    /// Creates a new step.
    /// </summary>
    public SequencerStepViewModel(int index)
    {
        StepIndex = index;
    }

    partial void OnIsActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ProbabilityOpacity));
    }

    partial void OnProbabilityChanged(double value)
    {
        OnPropertyChanged(nameof(ProbabilityOpacity));
    }

    partial void OnVelocityChanged(int value)
    {
        OnPropertyChanged(nameof(NormalizedVelocity));
    }
}

/// <summary>
/// Represents a row (drum sound) in the step sequencer.
/// </summary>
public partial class SequencerRowViewModel : ObservableObject
{
    /// <summary>
    /// Display name of the sound.
    /// </summary>
    [ObservableProperty]
    private string _name = "Kick";

    /// <summary>
    /// MIDI note number.
    /// </summary>
    [ObservableProperty]
    private int _midiNote = 36;

    /// <summary>
    /// Whether the row is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Whether the row is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSolo;

    /// <summary>
    /// Row volume (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _volume = 1.0;

    /// <summary>
    /// Display color for the row.
    /// </summary>
    [ObservableProperty]
    private string _color = "#00D9FF";

    /// <summary>
    /// Steps in this row.
    /// </summary>
    public ObservableCollection<SequencerStepViewModel> Steps { get; } = new();

    /// <summary>
    /// Creates a new row with the specified step count.
    /// </summary>
    public SequencerRowViewModel(string name, int midiNote, string color, int stepCount)
    {
        Name = name;
        MidiNote = midiNote;
        Color = color;
        InitializeSteps(stepCount);
    }

    /// <summary>
    /// Initializes steps for this row.
    /// </summary>
    public void InitializeSteps(int count)
    {
        Steps.Clear();
        for (int i = 0; i < count; i++)
        {
            Steps.Add(new SequencerStepViewModel(i));
        }
    }

    /// <summary>
    /// Updates step count, preserving existing step states where possible.
    /// </summary>
    public void SetStepCount(int count)
    {
        while (Steps.Count > count)
        {
            Steps.RemoveAt(Steps.Count - 1);
        }
        while (Steps.Count < count)
        {
            Steps.Add(new SequencerStepViewModel(Steps.Count));
        }
    }

    /// <summary>
    /// Clears all steps in this row.
    /// </summary>
    public void ClearAllSteps()
    {
        foreach (var step in Steps)
        {
            step.IsActive = false;
            step.Velocity = 100;
            step.Probability = 1.0;
            step.HasAccent = false;
            step.Retrigger = 1;
        }
    }

    /// <summary>
    /// Loads a pattern from a boolean array.
    /// </summary>
    public void LoadPattern(bool[] pattern)
    {
        for (int i = 0; i < Math.Min(pattern.Length, Steps.Count); i++)
        {
            Steps[i].IsActive = pattern[i];
        }
    }

    /// <summary>
    /// Gets the pattern as a boolean array.
    /// </summary>
    public bool[] GetPattern()
    {
        return Steps.Select(s => s.IsActive).ToArray();
    }
}

/// <summary>
/// ViewModel for the Step Sequencer / Drum Editor control.
/// Provides multi-row drum machine functionality with probability, swing, and direction modes.
/// </summary>
public partial class StepSequencerViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private readonly PlaybackService _playbackService;
    private readonly AudioEngineService _audioEngineService;
    private StepSequencer? _engineSequencer;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private bool _disposed;
    private int _lastPlayedStep = -1;
    private readonly Random _random = new();

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of rows (drum sounds).
    /// </summary>
    public ObservableCollection<SequencerRowViewModel> Rows { get; } = new();

    /// <summary>
    /// Available step count options.
    /// </summary>
    public int[] StepCountOptions { get; } = { 16, 32, 64 };

    /// <summary>
    /// Available direction modes.
    /// </summary>
    public PlaybackDirection[] DirectionOptions { get; } = Enum.GetValues<PlaybackDirection>();

    #endregion

    #region Step Properties

    /// <summary>
    /// Current number of steps.
    /// </summary>
    [ObservableProperty]
    private int _stepCount = 16;

    /// <summary>
    /// Currently playing step index.
    /// </summary>
    [ObservableProperty]
    private int _currentStep = -1;

    /// <summary>
    /// Loop start step (inclusive).
    /// </summary>
    [ObservableProperty]
    private int _loopStart;

    /// <summary>
    /// Loop end step (exclusive).
    /// </summary>
    [ObservableProperty]
    private int _loopEnd = 16;

    #endregion

    #region Playback Properties

    /// <summary>
    /// Whether the sequencer is playing.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Playback direction mode.
    /// </summary>
    [ObservableProperty]
    private PlaybackDirection _direction = PlaybackDirection.Forward;

    /// <summary>
    /// Swing amount (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _swing;

    /// <summary>
    /// Step length in beats (0.25 = 16th, 0.5 = 8th).
    /// </summary>
    [ObservableProperty]
    private double _stepLength = 0.25;

    /// <summary>
    /// Whether looping is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _loopEnabled = true;

    #endregion

    #region Display Properties

    /// <summary>
    /// Current beat position for display.
    /// </summary>
    [ObservableProperty]
    private double _beatPosition;

    /// <summary>
    /// Tempo in BPM.
    /// </summary>
    [ObservableProperty]
    private double _tempo = 120.0;

    /// <summary>
    /// Whether to show velocity in step cells.
    /// </summary>
    [ObservableProperty]
    private bool _showVelocity = true;

    /// <summary>
    /// Whether to show probability (opacity) in step cells.
    /// </summary>
    [ObservableProperty]
    private bool _showProbability = true;

    /// <summary>
    /// Currently selected row index.
    /// </summary>
    [ObservableProperty]
    private int _selectedRowIndex = -1;

    /// <summary>
    /// Currently selected step index.
    /// </summary>
    [ObservableProperty]
    private int _selectedStepIndex = -1;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a note should be triggered.
    /// </summary>
    public event EventHandler<StepTriggeredEventArgs>? StepTriggered;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new StepSequencerViewModel.
    /// </summary>
    public StepSequencerViewModel()
    {
        _playbackService = PlaybackService.Instance;
        _audioEngineService = AudioEngineService.Instance;

        // Initialize with default 808-style kit
        Initialize808Kit();

        // Subscribe to playback events
        SubscribeToPlaybackEvents();

        // Create engine sequencer
        CreateEngineSequencer();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes with a standard 808-style drum kit.
    /// </summary>
    private void Initialize808Kit()
    {
        Rows.Clear();
        Rows.Add(new SequencerRowViewModel("Kick", 36, "#DC143C", StepCount));
        Rows.Add(new SequencerRowViewModel("Snare", 38, "#FFD700", StepCount));
        Rows.Add(new SequencerRowViewModel("Closed HH", 42, "#00D9FF", StepCount));
        Rows.Add(new SequencerRowViewModel("Open HH", 46, "#00BFFF", StepCount));
        Rows.Add(new SequencerRowViewModel("Clap", 39, "#FF69B4", StepCount));
        Rows.Add(new SequencerRowViewModel("Low Tom", 45, "#FF8C00", StepCount));
        Rows.Add(new SequencerRowViewModel("Mid Tom", 47, "#FFA500", StepCount));
        Rows.Add(new SequencerRowViewModel("Hi Tom", 50, "#00FF88", StepCount));
    }

    /// <summary>
    /// Creates the underlying engine sequencer.
    /// </summary>
    private void CreateEngineSequencer()
    {
        _engineSequencer = new StepSequencer(StepCount);
        _engineSequencer.Direction = MapDirection(Direction);
        _engineSequencer.Swing = (float)Swing;
        _engineSequencer.StepLength = StepLength;
        _engineSequencer.Loop = LoopEnabled;
        _engineSequencer.LoopStart = LoopStart;
        _engineSequencer.LoopEnd = LoopEnd;

        // Add rows to engine sequencer
        foreach (var row in Rows)
        {
            _engineSequencer.AddRow(row.Name, row.MidiNote);
        }

        // Subscribe to engine events
        _engineSequencer.NoteTriggered += OnEngineNoteTriggered;
        _engineSequencer.StepChanged += OnEngineStepChanged;
    }

    /// <summary>
    /// Subscribes to playback service events.
    /// </summary>
    private void SubscribeToPlaybackEvents()
    {
        var eventBus = EventBus.Instance;
        _beatSubscription = eventBus.SubscribeBeatChanged(OnBeatChanged);
        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(OnPlaybackStarted);
        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(OnPlaybackStopped);
    }

    #endregion

    #region Playback Event Handlers

    private void OnBeatChanged(EventBus.BeatChangedEventArgs args)
    {
        BeatPosition = args.CurrentBeat;

        if (IsPlaying && _engineSequencer != null)
        {
            _engineSequencer.Process(args.CurrentBeat);
        }
    }

    private void OnPlaybackStarted(EventBus.PlaybackStartedEventArgs args)
    {
        IsPlaying = true;
        _engineSequencer?.Start();
    }

    private void OnPlaybackStopped(EventBus.PlaybackStoppedEventArgs args)
    {
        IsPlaying = false;
        _engineSequencer?.Stop();
        ClearPlayingState();
    }

    private void OnEngineNoteTriggered(int note, int velocity, float gate)
    {
        // Find the row with this note
        var row = Rows.FirstOrDefault(r => r.MidiNote == note);
        if (row == null || row.IsMuted) return;

        // Check for solo - if any row is soloed, only play soloed rows
        var anySolo = Rows.Any(r => r.IsSolo);
        if (anySolo && !row.IsSolo) return;

        // Apply row volume
        velocity = (int)(velocity * row.Volume);

        // Trigger the note through the audio engine
        try
        {
            _audioEngineService.PlayNotePreview(note, velocity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error triggering note: {ex.Message}");
        }

        // Raise event for UI
        StepTriggered?.Invoke(this, new StepTriggeredEventArgs(note, velocity, gate));
    }

    private void OnEngineStepChanged(int stepIndex)
    {
        // Update playing state in UI
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            // Clear previous step's playing state
            if (_lastPlayedStep >= 0)
            {
                foreach (var row in Rows)
                {
                    if (_lastPlayedStep < row.Steps.Count)
                    {
                        row.Steps[_lastPlayedStep].IsPlaying = false;
                    }
                }
            }

            // Set current step's playing state
            CurrentStep = stepIndex;
            foreach (var row in Rows)
            {
                if (stepIndex >= 0 && stepIndex < row.Steps.Count)
                {
                    row.Steps[stepIndex].IsPlaying = true;
                }
            }

            _lastPlayedStep = stepIndex;
        });
    }

    /// <summary>
    /// Clears playing state from all steps.
    /// </summary>
    private void ClearPlayingState()
    {
        foreach (var row in Rows)
        {
            foreach (var step in row.Steps)
            {
                step.IsPlaying = false;
            }
        }
        CurrentStep = -1;
        _lastPlayedStep = -1;
    }

    #endregion

    #region Step Commands

    /// <summary>
    /// Toggles a step's active state.
    /// </summary>
    [RelayCommand]
    private void ToggleStep(SequencerStepViewModel? step)
    {
        if (step == null) return;

        step.IsActive = !step.IsActive;
        SyncStepToEngine(step);

        // Preview the sound if enabled
        if (step.IsActive && SelectedRowIndex >= 0 && SelectedRowIndex < Rows.Count)
        {
            var row = Rows[SelectedRowIndex];
            PreviewNote(row.MidiNote, step.Velocity);
        }
    }

    /// <summary>
    /// Sets a step's velocity.
    /// </summary>
    [RelayCommand]
    private void SetStepVelocity((SequencerStepViewModel step, int velocity) args)
    {
        args.step.Velocity = Math.Clamp(args.velocity, 0, 127);
        SyncStepToEngine(args.step);
    }

    /// <summary>
    /// Sets a step's probability.
    /// </summary>
    [RelayCommand]
    private void SetStepProbability((SequencerStepViewModel step, double probability) args)
    {
        args.step.Probability = Math.Clamp(args.probability, 0.0, 1.0);
        // Probability is not directly in MusicEngine StepSequencer but affects playback logic
    }

    /// <summary>
    /// Toggles a step's accent.
    /// </summary>
    [RelayCommand]
    private void ToggleStepAccent(SequencerStepViewModel? step)
    {
        if (step == null) return;
        step.HasAccent = !step.HasAccent;
        SyncStepToEngine(step);
    }

    /// <summary>
    /// Syncs a step's state to the engine sequencer.
    /// </summary>
    private void SyncStepToEngine(SequencerStepViewModel step)
    {
        if (_engineSequencer == null) return;

        // Find the row containing this step
        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var row = Rows[rowIndex];
            int stepIndex = row.Steps.IndexOf(step);
            if (stepIndex >= 0)
            {
                _engineSequencer.SetStepProperties(
                    rowIndex,
                    stepIndex,
                    active: step.IsActive,
                    velocity: step.Velocity,
                    accent: step.HasAccent,
                    retrigger: step.Retrigger
                );
                break;
            }
        }
    }

    #endregion

    #region Row Commands

    /// <summary>
    /// Adds a new row.
    /// </summary>
    [RelayCommand]
    private void AddRow()
    {
        var newRow = new SequencerRowViewModel(
            $"Drum {Rows.Count + 1}",
            60 + Rows.Count,
            GetRandomColor(),
            StepCount
        );
        Rows.Add(newRow);
        _engineSequencer?.AddRow(newRow.Name, newRow.MidiNote);
    }

    /// <summary>
    /// Removes a row.
    /// </summary>
    [RelayCommand]
    private void RemoveRow(SequencerRowViewModel? row)
    {
        if (row == null || Rows.Count <= 1) return;

        int index = Rows.IndexOf(row);
        if (index >= 0)
        {
            Rows.Remove(row);
            var engineRow = _engineSequencer?.GetRow(index);
            if (engineRow != null)
            {
                _engineSequencer?.RemoveRow(engineRow);
            }
        }
    }

    /// <summary>
    /// Toggles mute state for a row.
    /// </summary>
    [RelayCommand]
    private void ToggleMute(SequencerRowViewModel? row)
    {
        if (row == null) return;
        row.IsMuted = !row.IsMuted;

        // Update engine
        int index = Rows.IndexOf(row);
        var engineRow = _engineSequencer?.GetRow(index);
        if (engineRow != null)
        {
            engineRow.Muted = row.IsMuted;
        }
    }

    /// <summary>
    /// Toggles solo state for a row.
    /// </summary>
    [RelayCommand]
    private void ToggleSolo(SequencerRowViewModel? row)
    {
        if (row == null) return;
        row.IsSolo = !row.IsSolo;

        // Update engine
        int index = Rows.IndexOf(row);
        var engineRow = _engineSequencer?.GetRow(index);
        if (engineRow != null)
        {
            engineRow.Soloed = row.IsSolo;
        }
    }

    /// <summary>
    /// Clears all steps in a row.
    /// </summary>
    [RelayCommand]
    private void ClearRow(SequencerRowViewModel? row)
    {
        if (row == null) return;
        row.ClearAllSteps();

        // Clear engine row
        int index = Rows.IndexOf(row);
        if (index >= 0 && _engineSequencer != null)
        {
            for (int i = 0; i < StepCount; i++)
            {
                _engineSequencer.SetStep(index, i, false);
            }
        }
    }

    #endregion

    #region Pattern Commands

    /// <summary>
    /// Clears all steps in all rows.
    /// </summary>
    [RelayCommand]
    private void ClearAll()
    {
        foreach (var row in Rows)
        {
            row.ClearAllSteps();
        }
        _engineSequencer?.Clear();
        StatusMessage = "Pattern cleared";
    }

    /// <summary>
    /// Randomizes the pattern.
    /// </summary>
    [RelayCommand]
    private void Randomize()
    {
        foreach (var row in Rows)
        {
            foreach (var step in row.Steps)
            {
                step.IsActive = _random.NextDouble() < 0.3; // 30% chance
                if (step.IsActive)
                {
                    step.Velocity = 60 + _random.Next(68); // 60-127
                    step.Probability = 0.5 + _random.NextDouble() * 0.5; // 0.5-1.0
                }
            }
        }
        SyncAllToEngine();
        StatusMessage = "Pattern randomized";
    }

    /// <summary>
    /// Loads a basic 4/4 beat preset.
    /// </summary>
    [RelayCommand]
    private void LoadBasicBeat()
    {
        ClearAll();

        if (Rows.Count >= 3)
        {
            // Kick: 1 and 3
            Rows[0].Steps[0].IsActive = true;
            Rows[0].Steps[4].IsActive = true;
            Rows[0].Steps[8].IsActive = true;
            Rows[0].Steps[12].IsActive = true;

            // Snare: 2 and 4
            Rows[1].Steps[4].IsActive = true;
            Rows[1].Steps[12].IsActive = true;

            // Hi-hat: all 8ths
            for (int i = 0; i < Math.Min(16, Rows[2].Steps.Count); i += 2)
            {
                Rows[2].Steps[i].IsActive = true;
            }
        }

        SyncAllToEngine();
        StatusMessage = "Loaded basic beat";
    }

    /// <summary>
    /// Copies the current row's pattern.
    /// </summary>
    [RelayCommand]
    private void CopyRow(SequencerRowViewModel? row)
    {
        if (row == null) return;
        _copiedPattern = row.GetPattern();
        _copiedVelocities = row.Steps.Select(s => s.Velocity).ToArray();
        _copiedProbabilities = row.Steps.Select(s => s.Probability).ToArray();
        StatusMessage = "Row copied";
    }

    /// <summary>
    /// Pastes the copied pattern to a row.
    /// </summary>
    [RelayCommand]
    private void PasteRow(SequencerRowViewModel? row)
    {
        if (row == null || _copiedPattern == null) return;

        row.LoadPattern(_copiedPattern);

        if (_copiedVelocities != null)
        {
            for (int i = 0; i < Math.Min(_copiedVelocities.Length, row.Steps.Count); i++)
            {
                row.Steps[i].Velocity = _copiedVelocities[i];
            }
        }

        if (_copiedProbabilities != null)
        {
            for (int i = 0; i < Math.Min(_copiedProbabilities.Length, row.Steps.Count); i++)
            {
                row.Steps[i].Probability = _copiedProbabilities[i];
            }
        }

        SyncRowToEngine(row);
        StatusMessage = "Row pasted";
    }

    private bool[]? _copiedPattern;
    private int[]? _copiedVelocities;
    private double[]? _copiedProbabilities;

    #endregion

    #region Playback Commands

    /// <summary>
    /// Starts playback.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        _playbackService.Play();
    }

    /// <summary>
    /// Stops playback.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _playbackService.Stop();
    }

    /// <summary>
    /// Toggles play/pause.
    /// </summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    /// <summary>
    /// Resets to the beginning.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        _engineSequencer?.Reset();
        ClearPlayingState();
        _playbackService.SetPosition(0);
    }

    #endregion

    #region Property Change Handlers

    partial void OnStepCountChanged(int value)
    {
        foreach (var row in Rows)
        {
            row.SetStepCount(value);
        }

        // Recreate engine sequencer
        CreateEngineSequencer();
        SyncAllToEngine();

        // Update loop end if needed
        if (LoopEnd > value)
        {
            LoopEnd = value;
        }
    }

    partial void OnDirectionChanged(PlaybackDirection value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.Direction = MapDirection(value);
        }
    }

    partial void OnSwingChanged(double value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.Swing = (float)Math.Clamp(value, 0.0, 1.0);
        }
    }

    partial void OnStepLengthChanged(double value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.StepLength = value;
        }
    }

    partial void OnLoopEnabledChanged(bool value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.Loop = value;
        }
    }

    partial void OnLoopStartChanged(int value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.LoopStart = Math.Clamp(value, 0, StepCount - 1);
        }
    }

    partial void OnLoopEndChanged(int value)
    {
        if (_engineSequencer != null)
        {
            _engineSequencer.LoopEnd = Math.Clamp(value, 1, StepCount);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Maps UI direction enum to engine direction enum.
    /// </summary>
    private static SequencerDirection MapDirection(PlaybackDirection direction)
    {
        return direction switch
        {
            PlaybackDirection.Forward => SequencerDirection.Forward,
            PlaybackDirection.Reverse => SequencerDirection.Backward,
            PlaybackDirection.PingPong => SequencerDirection.PingPong,
            PlaybackDirection.Random => SequencerDirection.Random,
            _ => SequencerDirection.Forward
        };
    }

    /// <summary>
    /// Gets a random color for new rows.
    /// </summary>
    private string GetRandomColor()
    {
        string[] colors = { "#00D9FF", "#FF69B4", "#00FF88", "#FFD700", "#FF8C00", "#9370DB", "#32CD32", "#DC143C" };
        return colors[_random.Next(colors.Length)];
    }

    /// <summary>
    /// Syncs all steps to the engine sequencer.
    /// </summary>
    private void SyncAllToEngine()
    {
        if (_engineSequencer == null) return;

        for (int rowIndex = 0; rowIndex < Rows.Count; rowIndex++)
        {
            var row = Rows[rowIndex];
            for (int stepIndex = 0; stepIndex < row.Steps.Count; stepIndex++)
            {
                var step = row.Steps[stepIndex];
                _engineSequencer.SetStepProperties(
                    rowIndex,
                    stepIndex,
                    active: step.IsActive,
                    velocity: step.Velocity,
                    accent: step.HasAccent,
                    retrigger: step.Retrigger
                );
            }
        }
    }

    /// <summary>
    /// Syncs a specific row to the engine.
    /// </summary>
    private void SyncRowToEngine(SequencerRowViewModel row)
    {
        if (_engineSequencer == null) return;

        int rowIndex = Rows.IndexOf(row);
        if (rowIndex < 0) return;

        for (int stepIndex = 0; stepIndex < row.Steps.Count; stepIndex++)
        {
            var step = row.Steps[stepIndex];
            _engineSequencer.SetStepProperties(
                rowIndex,
                stepIndex,
                active: step.IsActive,
                velocity: step.Velocity,
                accent: step.HasAccent,
                retrigger: step.Retrigger
            );
        }
    }

    /// <summary>
    /// Previews a note sound.
    /// </summary>
    private void PreviewNote(int note, int velocity)
    {
        try
        {
            _audioEngineService.PlayNotePreview(note, velocity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Note preview error: {ex.Message}");
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();

        if (_engineSequencer != null)
        {
            _engineSequencer.NoteTriggered -= OnEngineNoteTriggered;
            _engineSequencer.StepChanged -= OnEngineStepChanged;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Event arguments for step triggered events.
/// </summary>
public class StepTriggeredEventArgs : EventArgs
{
    /// <summary>
    /// MIDI note number.
    /// </summary>
    public int Note { get; }

    /// <summary>
    /// Velocity (0-127).
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gate length (0.0 - 1.0).
    /// </summary>
    public float Gate { get; }

    /// <summary>
    /// Creates new event args.
    /// </summary>
    public StepTriggeredEventArgs(int note, int velocity, float gate)
    {
        Note = note;
        Velocity = velocity;
        Gate = gate;
    }
}
