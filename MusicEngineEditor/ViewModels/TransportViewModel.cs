// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for transport controls.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the transport controls, providing formatted time/beat display
/// and playback state management.
/// </summary>
public partial class TransportViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private readonly PlaybackService _playbackService;
    private readonly RecordingService _recordingService;
    private readonly DispatcherTimer _displayTimer;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private EventBus.SubscriptionToken? _bpmChangedSubscription;
    private bool _disposed;

    // Tap tempo tracking
    private readonly List<DateTime> _tapTimestamps = new();
    private const int TapTempoMinTaps = 2;
    private const int TapTempoMaxTaps = 8;
    private const double TapTempoTimeoutSeconds = 2.0;
    private const double TapTempoMinBpm = 30.0;
    private const double TapTempoMaxBpm = 300.0;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the current beat position.
    /// </summary>
    [ObservableProperty]
    private double _currentBeat;

    /// <summary>
    /// Gets or sets the current bar number (1-based).
    /// </summary>
    [ObservableProperty]
    private int _currentBar = 1;

    /// <summary>
    /// Gets or sets the beat within the current bar (1-based).
    /// </summary>
    [ObservableProperty]
    private int _beatInBar = 1;

    /// <summary>
    /// Gets or sets the current time in seconds.
    /// </summary>
    [ObservableProperty]
    private double _currentTime;

    /// <summary>
    /// Gets or sets the BPM (beats per minute).
    /// </summary>
    [ObservableProperty]
    private double _bpm = 120.0;

    /// <summary>
    /// Gets or sets whether playback is active.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Gets or sets whether playback is paused.
    /// </summary>
    [ObservableProperty]
    private bool _isPaused;

    /// <summary>
    /// Gets or sets whether recording is active.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Gets or sets whether count-in is active.
    /// </summary>
    [ObservableProperty]
    private bool _isCountingIn;

    /// <summary>
    /// Gets or sets whether any tracks are armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _hasArmedTracks;

    /// <summary>
    /// Gets or sets the current recording duration formatted.
    /// </summary>
    [ObservableProperty]
    private string _recordingDuration = "00:00";

    /// <summary>
    /// Gets or sets whether loop playback is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _loopEnabled;

    /// <summary>
    /// Gets or sets the loop start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _loopStart;

    /// <summary>
    /// Gets or sets the loop end position in beats.
    /// </summary>
    [ObservableProperty]
    private double _loopEnd = 16.0;

    /// <summary>
    /// Gets or sets the time signature numerator.
    /// </summary>
    [ObservableProperty]
    private int _timeSignatureNumerator = 4;

    /// <summary>
    /// Gets or sets the time signature denominator.
    /// </summary>
    [ObservableProperty]
    private int _timeSignatureDenominator = 4;

    /// <summary>
    /// Gets or sets whether metronome click is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _metronomeEnabled;

    /// <summary>
    /// Gets or sets the metronome volume (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _metronomeVolume = 0.7f;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the current beat formatted as "Bar:Beat" (e.g., "5:3").
    /// </summary>
    public string CurrentBeatFormatted => $"{CurrentBar}:{BeatInBar}";

    /// <summary>
    /// Gets the current beat formatted with sub-beat precision (e.g., "5:3.25").
    /// </summary>
    public string CurrentBeatDetailedFormatted
    {
        get
        {
            var subBeat = (CurrentBeat % 1.0) * TimeSignatureNumerator;
            return $"{CurrentBar}:{BeatInBar}.{subBeat:F0}";
        }
    }

    /// <summary>
    /// Gets the current time formatted as "MM:SS.mmm".
    /// </summary>
    public string CurrentTimeFormatted
    {
        get
        {
            var totalSeconds = CurrentTime;
            var minutes = (int)(totalSeconds / 60);
            var seconds = (int)(totalSeconds % 60);
            var milliseconds = (int)((totalSeconds % 1) * 1000);
            return $"{minutes:D2}:{seconds:D2}.{milliseconds:D3}";
        }
    }

    /// <summary>
    /// Gets the current time formatted as "MM:SS" (short format).
    /// </summary>
    public string CurrentTimeShortFormatted
    {
        get
        {
            var totalSeconds = CurrentTime;
            var minutes = (int)(totalSeconds / 60);
            var seconds = (int)(totalSeconds % 60);
            return $"{minutes:D2}:{seconds:D2}";
        }
    }

    /// <summary>
    /// Gets the BPM formatted as a string.
    /// </summary>
    public string BpmFormatted => $"{Bpm:F1}";

    /// <summary>
    /// Gets the time signature formatted as a string (e.g., "4/4").
    /// </summary>
    public string TimeSignatureFormatted => $"{TimeSignatureNumerator}/{TimeSignatureDenominator}";

    /// <summary>
    /// Gets the loop region formatted as a string.
    /// </summary>
    public string LoopRegionFormatted => $"{LoopStart:F1} - {LoopEnd:F1}";

    /// <summary>
    /// Gets the playback status text.
    /// </summary>
    public string PlaybackStatus
    {
        get
        {
            if (IsCountingIn) return "Count-In";
            if (IsRecording) return "Recording";
            if (IsPlaying) return "Playing";
            if (IsPaused) return "Paused";
            return "Stopped";
        }
    }

    /// <summary>
    /// Gets whether the record button should show as active (armed or recording).
    /// </summary>
    public bool RecordButtonActive => IsRecording || IsCountingIn;

    /// <summary>
    /// Gets whether the stop command can execute.
    /// </summary>
    public bool CanStop => IsPlaying || IsPaused;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new TransportViewModel instance.
    /// </summary>
    public TransportViewModel()
    {
        _playbackService = PlaybackService.Instance;
        _recordingService = RecordingService.Instance;

        // Initialize from playback service
        SyncFromPlaybackService();

        // Subscribe to playback service events
        _playbackService.PlaybackStarted += OnPlaybackStarted;
        _playbackService.PlaybackStopped += OnPlaybackStopped;
        _playbackService.PlaybackPaused += OnPlaybackPaused;
        _playbackService.PlaybackResumed += OnPlaybackResumed;
        _playbackService.PositionChanged += OnPositionChanged;
        _playbackService.BpmChanged += OnBpmChanged;
        _playbackService.LoopStateChanged += OnLoopStateChanged;
        _playbackService.LoopRegionChanged += OnLoopRegionChanged;

        // Subscribe to recording service events
        _recordingService.RecordingStarted += OnRecordingServiceStarted;
        _recordingService.RecordingStopped += OnRecordingServiceStopped;
        _recordingService.RecordingStateChanged += OnRecordingServiceStateChanged;
        _recordingService.CountInStarted += OnCountInStarted;
        _recordingService.CountInBeat += OnCountInBeat;
        _recordingService.TrackArmed += OnTrackArmedChanged;
        _recordingService.TrackDisarmed += OnTrackArmedChanged;

        // Initialize armed tracks state
        HasArmedTracks = _recordingService.HasArmedTracks;

        // Subscribe to EventBus for additional updates
        SubscribeToEventBus();

        // Setup display update timer (60fps for smooth display)
        _displayTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
        };
        _displayTimer.Tick += OnDisplayTimerTick;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Starts or resumes playback.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        _playbackService.Play();
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    [RelayCommand]
    private void Pause()
    {
        _playbackService.Pause();
    }

    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _playbackService.Stop();
    }

    /// <summary>
    /// Toggles between play and pause.
    /// </summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    /// <summary>
    /// Toggles loop playback.
    /// </summary>
    [RelayCommand]
    private void ToggleLoop()
    {
        _playbackService.ToggleLoop();
    }

    /// <summary>
    /// Jumps to the beginning.
    /// </summary>
    [RelayCommand]
    private void JumpToStart()
    {
        _playbackService.JumpToStart();
    }

    /// <summary>
    /// Jumps to the end of the loop region.
    /// </summary>
    [RelayCommand]
    private void JumpToEnd()
    {
        _playbackService.JumpToEnd();
    }

    /// <summary>
    /// Moves back by one bar.
    /// </summary>
    [RelayCommand]
    private void StepBackward()
    {
        var newBeat = Math.Max(0, CurrentBeat - TimeSignatureNumerator);
        _playbackService.SetPosition(newBeat);
    }

    /// <summary>
    /// Moves forward by one bar.
    /// </summary>
    [RelayCommand]
    private void StepForward()
    {
        var newBeat = CurrentBeat + TimeSignatureNumerator;
        _playbackService.SetPosition(newBeat);
    }

    /// <summary>
    /// Toggles recording state.
    /// </summary>
    [RelayCommand]
    private async Task ToggleRecordAsync()
    {
        if (IsRecording || IsCountingIn)
        {
            // Stop recording
            _recordingService.StopRecording();
        }
        else if (HasArmedTracks)
        {
            // Start recording with count-in
            try
            {
                await _recordingService.StartRecordingAsync(useCountIn: true);
            }
            catch (Exception ex)
            {
                StatusMessage = $"Recording failed: {ex.Message}";
            }
        }
        else
        {
            // No tracks armed - just toggle the recording armed state
            StatusMessage = "No tracks armed for recording";
        }
    }

    /// <summary>
    /// Starts recording immediately without count-in.
    /// </summary>
    [RelayCommand]
    private void StartRecordingNow()
    {
        if (!HasArmedTracks)
        {
            StatusMessage = "No tracks armed for recording";
            return;
        }

        try
        {
            _recordingService.StartRecording();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Recording failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Stops recording.
    /// </summary>
    [RelayCommand]
    private void StopRecording()
    {
        if (IsRecording || IsCountingIn)
        {
            _recordingService.StopRecording();
        }
    }

    /// <summary>
    /// Cancels recording and discards data.
    /// </summary>
    [RelayCommand]
    private void CancelRecording()
    {
        if (IsRecording || IsCountingIn)
        {
            _recordingService.CancelRecording();
        }
    }

    /// <summary>
    /// Toggles metronome.
    /// </summary>
    [RelayCommand]
    private void ToggleMetronome()
    {
        MetronomeEnabled = !MetronomeEnabled;
    }

    /// <summary>
    /// Sets the BPM to a specific value.
    /// </summary>
    /// <param name="bpm">The BPM value.</param>
    [RelayCommand]
    private void SetBpm(double bpm)
    {
        if (bpm > 0 && bpm <= 999)
        {
            Bpm = bpm;
            _playbackService.BPM = bpm;
        }
    }

    /// <summary>
    /// Increases BPM by 1.
    /// </summary>
    [RelayCommand]
    private void IncreaseBpm()
    {
        SetBpm(Bpm + 1);
    }

    /// <summary>
    /// Decreases BPM by 1.
    /// </summary>
    [RelayCommand]
    private void DecreaseBpm()
    {
        SetBpm(Bpm - 1);
    }

    /// <summary>
    /// Taps tempo to calculate BPM from consecutive taps.
    /// Requires at least 2 taps within 2 seconds of each other.
    /// Uses up to 8 recent taps for averaging.
    /// </summary>
    [RelayCommand]
    private void TapTempo()
    {
        var now = DateTime.UtcNow;

        // Reset if too much time has passed since the last tap
        if (_tapTimestamps.Count > 0)
        {
            var lastTap = _tapTimestamps[^1];
            var timeSinceLastTap = (now - lastTap).TotalSeconds;

            if (timeSinceLastTap > TapTempoTimeoutSeconds)
            {
                _tapTimestamps.Clear();
                StatusMessage = "Tap tempo reset - tap again";
            }
        }

        // Add current tap
        _tapTimestamps.Add(now);

        // Keep only the most recent taps for averaging
        while (_tapTimestamps.Count > TapTempoMaxTaps)
        {
            _tapTimestamps.RemoveAt(0);
        }

        // Need at least 2 taps to calculate BPM
        if (_tapTimestamps.Count < TapTempoMinTaps)
        {
            StatusMessage = $"Tap tempo: {_tapTimestamps.Count} tap(s) - keep tapping...";
            return;
        }

        // Calculate average interval between taps
        var intervals = new List<double>();
        for (var i = 1; i < _tapTimestamps.Count; i++)
        {
            var interval = (_tapTimestamps[i] - _tapTimestamps[i - 1]).TotalSeconds;
            intervals.Add(interval);
        }

        var averageInterval = intervals.Average();

        // Convert interval to BPM (beats per minute)
        // interval in seconds -> 60 / interval = BPM
        var calculatedBpm = 60.0 / averageInterval;

        // Clamp to reasonable BPM range
        calculatedBpm = Math.Clamp(calculatedBpm, TapTempoMinBpm, TapTempoMaxBpm);

        // Round to one decimal place for cleaner display
        calculatedBpm = Math.Round(calculatedBpm, 1);

        // Set the BPM
        SetBpm(calculatedBpm);

        StatusMessage = $"Tap tempo: {calculatedBpm:F1} BPM ({_tapTimestamps.Count} taps)";
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    [RelayCommand]
    private void SetLoopRegion((double start, double end) region)
    {
        _playbackService.SetLoopRegion(region.start, region.end);
    }

    /// <summary>
    /// Sets the loop start to current position.
    /// </summary>
    [RelayCommand]
    private void SetLoopStartHere()
    {
        LoopStart = CurrentBeat;
        _playbackService.LoopStart = CurrentBeat;
    }

    /// <summary>
    /// Sets the loop end to current position.
    /// </summary>
    [RelayCommand]
    private void SetLoopEndHere()
    {
        LoopEnd = CurrentBeat;
        _playbackService.LoopEnd = CurrentBeat;
    }

    #endregion

    #region Event Handlers

    private void OnPlaybackStarted(object? sender, PlaybackStartedEventArgs e)
    {
        IsPlaying = true;
        IsPaused = false;
        _displayTimer.Start();
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(CanStop));
    }

    private void OnPlaybackStopped(object? sender, PlaybackStoppedEventArgs e)
    {
        IsPlaying = false;
        IsPaused = false;
        IsRecording = false;
        _displayTimer.Stop();

        // Reset position display
        CurrentBeat = 0;
        CurrentTime = 0;
        UpdateBarAndBeat();

        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(CanStop));
    }

    private void OnPlaybackPaused(object? sender, EventArgs e)
    {
        IsPlaying = false;
        IsPaused = true;
        _displayTimer.Stop();
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(CanStop));
    }

    private void OnPlaybackResumed(object? sender, EventArgs e)
    {
        IsPlaying = true;
        IsPaused = false;
        _displayTimer.Start();
        OnPropertyChanged(nameof(PlaybackStatus));
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e)
    {
        CurrentBeat = e.Beat;
        CurrentTime = e.Time;
        UpdateBarAndBeat();
    }

    private void OnBpmChanged(object? sender, BpmChangedEventArgs e)
    {
        Bpm = e.NewBpm;
        OnPropertyChanged(nameof(BpmFormatted));
    }

    private void OnLoopStateChanged(object? sender, EventArgs e)
    {
        LoopEnabled = _playbackService.LoopEnabled;
    }

    private void OnLoopRegionChanged(object? sender, EventArgs e)
    {
        LoopStart = _playbackService.LoopStart;
        LoopEnd = _playbackService.LoopEnd;
        OnPropertyChanged(nameof(LoopRegionFormatted));
    }

    private void OnDisplayTimerTick(object? sender, EventArgs e)
    {
        // Update display from playback service
        CurrentBeat = _playbackService.CurrentBeat;
        CurrentTime = _playbackService.CurrentTime;
        UpdateBarAndBeat();

        // Notify all formatted properties
        OnPropertyChanged(nameof(CurrentBeatFormatted));
        OnPropertyChanged(nameof(CurrentBeatDetailedFormatted));
        OnPropertyChanged(nameof(CurrentTimeFormatted));
        OnPropertyChanged(nameof(CurrentTimeShortFormatted));

        // Update recording duration if recording
        if (IsRecording)
        {
            RecordingDuration = _recordingService.RecordingDurationFormatted;
        }
    }

    #endregion

    #region Recording Service Event Handlers

    private void OnRecordingServiceStarted(object? sender, RecordingEventArgs e)
    {
        IsRecording = true;
        IsCountingIn = false;
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));
        StatusMessage = $"Recording {e.ArmedTracks.Count} track(s)";
    }

    private void OnRecordingServiceStopped(object? sender, RecordingStoppedEventArgs e)
    {
        IsRecording = false;
        IsCountingIn = false;
        RecordingDuration = "00:00";
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));

        if (e.WasCancelled)
        {
            StatusMessage = "Recording cancelled";
        }
        else
        {
            StatusMessage = $"Recorded {e.RecordedClips.Count} clip(s)";
        }
    }

    private void OnRecordingServiceStateChanged(object? sender, bool isRecording)
    {
        IsRecording = isRecording;
        if (!isRecording)
        {
            IsCountingIn = false;
        }
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));
    }

    private void OnCountInStarted(object? sender, int totalBars)
    {
        IsCountingIn = true;
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));
        StatusMessage = $"Count-in: {totalBars} bar(s)";
    }

    private void OnCountInBeat(object? sender, int beatNumber)
    {
        StatusMessage = $"Count-in: Beat {beatNumber}";
    }

    private void OnTrackArmedChanged(object? sender, TrackArmEventArgs e)
    {
        HasArmedTracks = _recordingService.HasArmedTracks;
    }

    #endregion

    #region Helper Methods

    private void SyncFromPlaybackService()
    {
        IsPlaying = _playbackService.IsPlaying;
        IsPaused = _playbackService.IsPaused;
        CurrentBeat = _playbackService.CurrentBeat;
        CurrentTime = _playbackService.CurrentTime;
        Bpm = _playbackService.BPM;
        LoopEnabled = _playbackService.LoopEnabled;
        LoopStart = _playbackService.LoopStart;
        LoopEnd = _playbackService.LoopEnd;
        UpdateBarAndBeat();
    }

    private void UpdateBarAndBeat()
    {
        // Calculate bar and beat from current beat position
        // Assuming 4/4 time signature by default
        var beatsPerBar = TimeSignatureNumerator;
        CurrentBar = (int)(CurrentBeat / beatsPerBar) + 1;
        BeatInBar = (int)(CurrentBeat % beatsPerBar) + 1;
    }

    private void SubscribeToEventBus()
    {
        var eventBus = EventBus.Instance;

        _beatSubscription = eventBus.SubscribeBeatChanged(args =>
        {
            CurrentBeat = args.CurrentBeat;
            CurrentTime = _playbackService.BeatToTime(args.CurrentBeat);
            UpdateBarAndBeat();
        });

        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(args =>
        {
            IsPlaying = true;
            IsPaused = false;
            Bpm = args.Bpm;
            _displayTimer.Start();
            OnPropertyChanged(nameof(PlaybackStatus));
        });

        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(args =>
        {
            IsPlaying = false;
            IsPaused = false;
            IsRecording = false;
            _displayTimer.Stop();
            OnPropertyChanged(nameof(PlaybackStatus));
        });

        _bpmChangedSubscription = eventBus.SubscribeBpmChanged(args =>
        {
            Bpm = args.NewBpm;
            OnPropertyChanged(nameof(BpmFormatted));
        });
    }

    #endregion

    #region Property Change Handlers

    partial void OnBpmChanged(double value)
    {
        OnPropertyChanged(nameof(BpmFormatted));
    }

    partial void OnCurrentBeatChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentBeatFormatted));
        OnPropertyChanged(nameof(CurrentBeatDetailedFormatted));
    }

    partial void OnCurrentTimeChanged(double value)
    {
        OnPropertyChanged(nameof(CurrentTimeFormatted));
        OnPropertyChanged(nameof(CurrentTimeShortFormatted));
    }

    partial void OnCurrentBarChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentBeatFormatted));
        OnPropertyChanged(nameof(CurrentBeatDetailedFormatted));
    }

    partial void OnBeatInBarChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentBeatFormatted));
        OnPropertyChanged(nameof(CurrentBeatDetailedFormatted));
    }

    partial void OnLoopStartChanged(double value)
    {
        OnPropertyChanged(nameof(LoopRegionFormatted));
    }

    partial void OnLoopEndChanged(double value)
    {
        OnPropertyChanged(nameof(LoopRegionFormatted));
    }

    partial void OnTimeSignatureNumeratorChanged(int value)
    {
        OnPropertyChanged(nameof(TimeSignatureFormatted));
        UpdateBarAndBeat();
    }

    partial void OnTimeSignatureDenominatorChanged(int value)
    {
        OnPropertyChanged(nameof(TimeSignatureFormatted));
    }

    partial void OnIsPlayingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnIsPausedChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(CanStop));
    }

    partial void OnIsRecordingChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));
    }

    partial void OnIsCountingInChanged(bool value)
    {
        OnPropertyChanged(nameof(PlaybackStatus));
        OnPropertyChanged(nameof(RecordButtonActive));
    }

    partial void OnHasArmedTracksChanged(bool value)
    {
        // Could notify any dependent UI elements
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the TransportViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop timer
        _displayTimer.Stop();

        // Unsubscribe from playback service events
        _playbackService.PlaybackStarted -= OnPlaybackStarted;
        _playbackService.PlaybackStopped -= OnPlaybackStopped;
        _playbackService.PlaybackPaused -= OnPlaybackPaused;
        _playbackService.PlaybackResumed -= OnPlaybackResumed;
        _playbackService.PositionChanged -= OnPositionChanged;
        _playbackService.BpmChanged -= OnBpmChanged;
        _playbackService.LoopStateChanged -= OnLoopStateChanged;
        _playbackService.LoopRegionChanged -= OnLoopRegionChanged;

        // Unsubscribe from recording service events
        _recordingService.RecordingStarted -= OnRecordingServiceStarted;
        _recordingService.RecordingStopped -= OnRecordingServiceStopped;
        _recordingService.RecordingStateChanged -= OnRecordingServiceStateChanged;
        _recordingService.CountInStarted -= OnCountInStarted;
        _recordingService.CountInBeat -= OnCountInBeat;
        _recordingService.TrackArmed -= OnTrackArmedChanged;
        _recordingService.TrackDisarmed -= OnTrackArmedChanged;

        // Dispose EventBus subscriptions
        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();
        _bpmChangedSubscription?.Dispose();
    }

    #endregion
}
