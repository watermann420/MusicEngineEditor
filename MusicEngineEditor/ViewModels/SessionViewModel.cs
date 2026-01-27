// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Session View / Clip Launcher.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Session;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Quantize mode options for the UI.
/// </summary>
public enum QuantizeLaunchOption
{
    None,
    OneBar,
    HalfBar,
    QuarterNote,
    EighthNote,
    SixteenthNote
}

/// <summary>
/// ViewModel representing a single clip slot in the session grid.
/// </summary>
public partial class ClipSlotViewModel : ObservableObject
{
    private readonly ClipSlot? _clipSlot;

    /// <summary>
    /// Track index (column).
    /// </summary>
    [ObservableProperty]
    private int _trackIndex;

    /// <summary>
    /// Scene index (row).
    /// </summary>
    [ObservableProperty]
    private int _sceneIndex;

    /// <summary>
    /// Name of the clip in this slot.
    /// </summary>
    [ObservableProperty]
    private string _clipName = string.Empty;

    /// <summary>
    /// Whether this slot has a clip.
    /// </summary>
    [ObservableProperty]
    private bool _hasClip;

    /// <summary>
    /// Whether the clip is currently playing.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Whether the clip is queued to play.
    /// </summary>
    [ObservableProperty]
    private bool _isQueued;

    /// <summary>
    /// Whether the clip is recording.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Whether the clip is stopped (has content but not playing).
    /// </summary>
    [ObservableProperty]
    private bool _isStopped;

    /// <summary>
    /// Color of the clip.
    /// </summary>
    [ObservableProperty]
    private Color _clipColor = Color.FromRgb(100, 149, 237);

    /// <summary>
    /// Playback progress (0-1).
    /// </summary>
    [ObservableProperty]
    private double _playbackProgress;

    /// <summary>
    /// Whether this slot is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether this is an audio clip.
    /// </summary>
    [ObservableProperty]
    private bool _isAudioClip;

    /// <summary>
    /// Whether this is a MIDI clip.
    /// </summary>
    [ObservableProperty]
    private bool _isMidiClip;

    /// <summary>
    /// Creates a new ClipSlotViewModel.
    /// </summary>
    public ClipSlotViewModel()
    {
    }

    /// <summary>
    /// Creates a ClipSlotViewModel from a ClipSlot.
    /// </summary>
    /// <param name="clipSlot">The clip slot from the engine.</param>
    public ClipSlotViewModel(ClipSlot clipSlot)
    {
        _clipSlot = clipSlot;
        TrackIndex = clipSlot.TrackIndex;
        SceneIndex = clipSlot.SceneIndex;
        UpdateFromSlot();
    }

    /// <summary>
    /// Updates the ViewModel from the underlying ClipSlot.
    /// </summary>
    public void UpdateFromSlot()
    {
        if (_clipSlot == null) return;

        HasClip = _clipSlot.HasClip;
        IsPlaying = _clipSlot.IsPlaying;
        IsQueued = _clipSlot.IsQueued;
        IsRecording = _clipSlot.IsRecording;
        IsStopped = HasClip && !IsPlaying && !IsQueued && !IsRecording;

        if (_clipSlot.Clip != null)
        {
            ClipName = _clipSlot.Clip.Name;
            var c = _clipSlot.Clip.Color;
            ClipColor = Color.FromArgb(c.A, c.R, c.G, c.B);
            IsAudioClip = _clipSlot.Clip.IsAudio;
            IsMidiClip = _clipSlot.Clip.IsMidi;

            if (IsPlaying && _clipSlot.Clip.EffectiveLength > 0)
            {
                PlaybackProgress = _clipSlot.Clip.PlayPosition / _clipSlot.Clip.EffectiveLength;
            }
            else
            {
                PlaybackProgress = 0;
            }
        }
        else
        {
            ClipName = string.Empty;
            ClipColor = Color.FromRgb(100, 149, 237);
            IsAudioClip = false;
            IsMidiClip = false;
            PlaybackProgress = 0;
        }
    }

    /// <summary>
    /// Gets the underlying ClipSlot.
    /// </summary>
    public ClipSlot? GetClipSlot() => _clipSlot;
}

/// <summary>
/// ViewModel representing a scene (row) in the session grid.
/// </summary>
public partial class SceneViewModel : ObservableObject
{
    private readonly Scene? _scene;

    /// <summary>
    /// Scene index.
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// Scene name.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Whether this scene is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Color of the scene.
    /// </summary>
    [ObservableProperty]
    private Color _color = Color.FromRgb(80, 80, 80);

    /// <summary>
    /// Tempo override for this scene (null if no override).
    /// </summary>
    [ObservableProperty]
    private double? _tempoOverride;

    /// <summary>
    /// Creates a new SceneViewModel.
    /// </summary>
    public SceneViewModel()
    {
    }

    /// <summary>
    /// Creates a SceneViewModel from a Scene.
    /// </summary>
    /// <param name="scene">The scene from the engine.</param>
    public SceneViewModel(Scene scene)
    {
        _scene = scene;
        UpdateFromScene();
    }

    /// <summary>
    /// Updates the ViewModel from the underlying Scene.
    /// </summary>
    public void UpdateFromScene()
    {
        if (_scene == null) return;

        Index = _scene.Index;
        Name = _scene.Name;
        IsActive = _scene.IsActive;
        TempoOverride = _scene.TempoOverride;
        var c = _scene.Color;
        Color = Color.FromArgb(c.A, c.R, c.G, c.B);
    }

    /// <summary>
    /// Gets the underlying Scene.
    /// </summary>
    public Scene? GetScene() => _scene;
}

/// <summary>
/// ViewModel representing a track (column) in the session grid.
/// </summary>
public partial class SessionTrackViewModel : ObservableObject
{
    /// <summary>
    /// Track index.
    /// </summary>
    [ObservableProperty]
    private int _index;

    /// <summary>
    /// Track name.
    /// </summary>
    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>
    /// Track color.
    /// </summary>
    [ObservableProperty]
    private Color _color = Color.FromRgb(100, 149, 237);

    /// <summary>
    /// Whether this track is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

    /// <summary>
    /// Whether this track is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Whether this track is armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Whether a clip is currently playing on this track.
    /// </summary>
    [ObservableProperty]
    private bool _hasPlayingClip;
}

/// <summary>
/// ViewModel for the Session View / Clip Launcher.
/// Manages an Ableton-style grid of clip slots organized by tracks and scenes.
/// </summary>
public partial class SessionViewModel : ViewModelBase, IDisposable
{
    private ClipLauncher? _clipLauncher;
    private bool _disposed;
    private System.Timers.Timer? _updateTimer;

    /// <summary>
    /// Number of tracks (columns).
    /// </summary>
    [ObservableProperty]
    private int _trackCount = 8;

    /// <summary>
    /// Number of scenes (rows).
    /// </summary>
    [ObservableProperty]
    private int _sceneCount = 8;

    /// <summary>
    /// Current tempo in BPM.
    /// </summary>
    [ObservableProperty]
    private double _bpm = 120.0;

    /// <summary>
    /// Whether the launcher is running.
    /// </summary>
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// Current beat position.
    /// </summary>
    [ObservableProperty]
    private double _currentBeat;

    /// <summary>
    /// Selected quantize launch option.
    /// </summary>
    [ObservableProperty]
    private QuantizeLaunchOption _selectedQuantize = QuantizeLaunchOption.OneBar;

    /// <summary>
    /// Currently selected clip slot.
    /// </summary>
    [ObservableProperty]
    private ClipSlotViewModel? _selectedSlot;

    /// <summary>
    /// Currently selected scene.
    /// </summary>
    [ObservableProperty]
    private SceneViewModel? _selectedScene;

    /// <summary>
    /// All clip slots in the grid (flattened).
    /// </summary>
    public ObservableCollection<ClipSlotViewModel> ClipSlots { get; } = new();

    /// <summary>
    /// All scenes.
    /// </summary>
    public ObservableCollection<SceneViewModel> Scenes { get; } = new();

    /// <summary>
    /// All tracks.
    /// </summary>
    public ObservableCollection<SessionTrackViewModel> Tracks { get; } = new();

    /// <summary>
    /// Available quantize options.
    /// </summary>
    public QuantizeLaunchOption[] QuantizeOptions { get; } =
    [
        QuantizeLaunchOption.None,
        QuantizeLaunchOption.OneBar,
        QuantizeLaunchOption.HalfBar,
        QuantizeLaunchOption.QuarterNote,
        QuantizeLaunchOption.EighthNote,
        QuantizeLaunchOption.SixteenthNote
    ];

    /// <summary>
    /// Event raised when a clip is launched.
    /// </summary>
    public event EventHandler<(int Track, int Scene)>? ClipLaunched;

    /// <summary>
    /// Event raised when a scene is launched.
    /// </summary>
    public event EventHandler<int>? SceneLaunched;

    /// <summary>
    /// Event raised when a clip is double-clicked for editing.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? ClipEditRequested;

    /// <summary>
    /// Creates a new SessionViewModel.
    /// </summary>
    public SessionViewModel()
    {
        InitializeClipLauncher();
        StartUpdateTimer();
    }

    /// <summary>
    /// Initializes the clip launcher with default configuration.
    /// </summary>
    private void InitializeClipLauncher()
    {
        _clipLauncher = new ClipLauncher(TrackCount);
        _clipLauncher.Bpm = Bpm;
        _clipLauncher.GlobalQuantize = ConvertQuantizeOption(SelectedQuantize);

        // Subscribe to events
        _clipLauncher.ClipStateChanged += OnClipStateChanged;
        _clipLauncher.SceneLaunched += OnSceneLaunched;
        _clipLauncher.TrackStopped += OnTrackStopped;
        _clipLauncher.AllStopped += OnAllStopped;

        // Create default scenes
        for (int i = 0; i < SceneCount; i++)
        {
            _clipLauncher.AddScene($"Scene {i + 1}");
        }

        // Initialize tracks
        InitializeTracks();

        // Populate ViewModels
        RefreshFromLauncher();

        // Add some demo clips
        AddDemoClips();
    }

    /// <summary>
    /// Initializes track ViewModels.
    /// </summary>
    private void InitializeTracks()
    {
        Tracks.Clear();
        var trackColors = new[]
        {
            Color.FromRgb(255, 85, 85),   // Red
            Color.FromRgb(85, 255, 85),   // Green
            Color.FromRgb(85, 85, 255),   // Blue
            Color.FromRgb(255, 149, 0),   // Orange
            Color.FromRgb(255, 85, 255),  // Magenta
            Color.FromRgb(85, 255, 255),  // Cyan
            Color.FromRgb(255, 255, 85),  // Yellow
            Color.FromRgb(170, 85, 255)   // Purple
        };

        var trackNames = new[] { "Drums", "Bass", "Lead", "Pad", "FX", "Vox", "Keys", "Aux" };

        for (int i = 0; i < TrackCount; i++)
        {
            Tracks.Add(new SessionTrackViewModel
            {
                Index = i,
                Name = i < trackNames.Length ? trackNames[i] : $"Track {i + 1}",
                Color = i < trackColors.Length ? trackColors[i] : Color.FromRgb(100, 149, 237)
            });
        }
    }

    /// <summary>
    /// Adds some demo clips for testing.
    /// </summary>
    private void AddDemoClips()
    {
        if (_clipLauncher == null) return;

        // Add some sample clips
        var demoClips = new[]
        {
            (0, 0, "Kick Pattern", Color.FromRgb(255, 85, 85)),
            (0, 1, "Kick Fill", Color.FromRgb(255, 120, 120)),
            (1, 0, "Bass Line A", Color.FromRgb(85, 255, 85)),
            (1, 1, "Bass Line B", Color.FromRgb(120, 255, 120)),
            (2, 0, "Lead Melody", Color.FromRgb(85, 85, 255)),
            (2, 2, "Lead Hook", Color.FromRgb(120, 120, 255)),
            (3, 0, "Pad Drone", Color.FromRgb(255, 149, 0)),
            (3, 1, "Pad Swell", Color.FromRgb(255, 180, 60)),
            (4, 0, "Riser", Color.FromRgb(255, 85, 255)),
            (4, 3, "Impact", Color.FromRgb(255, 120, 255)),
            (5, 0, "Vocal Chop", Color.FromRgb(85, 255, 255)),
            (6, 0, "Keys Stab", Color.FromRgb(255, 255, 85)),
            (6, 1, "Keys Arp", Color.FromRgb(255, 255, 120)),
        };

        foreach (var (track, scene, name, color) in demoClips)
        {
            if (track < _clipLauncher.TrackCount && scene < _clipLauncher.SceneCount)
            {
                var slot = _clipLauncher.GetSlot(track, scene);
                var clip = new LaunchClip
                {
                    Name = name,
                    Color = System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B),
                    LengthBeats = 4.0,
                    Loop = true
                };
                slot.SetClip(clip);
            }
        }

        RefreshFromLauncher();
    }

    /// <summary>
    /// Refreshes all ViewModels from the ClipLauncher.
    /// </summary>
    public void RefreshFromLauncher()
    {
        if (_clipLauncher == null) return;

        // Update scenes
        Scenes.Clear();
        foreach (var scene in _clipLauncher.Scenes)
        {
            Scenes.Add(new SceneViewModel(scene));
        }

        // Update clip slots
        ClipSlots.Clear();
        for (int scene = 0; scene < _clipLauncher.SceneCount; scene++)
        {
            for (int track = 0; track < _clipLauncher.TrackCount; track++)
            {
                var slot = _clipLauncher.GetSlot(track, scene);
                ClipSlots.Add(new ClipSlotViewModel(slot));
            }
        }
    }

    /// <summary>
    /// Starts the update timer for refreshing playback state.
    /// </summary>
    private void StartUpdateTimer()
    {
        _updateTimer = new System.Timers.Timer(50); // 20Hz update rate
        _updateTimer.Elapsed += (s, e) => UpdatePlaybackState();
        _updateTimer.Start();
    }

    /// <summary>
    /// Updates playback state from the clip launcher.
    /// </summary>
    private void UpdatePlaybackState()
    {
        if (_clipLauncher == null || _disposed) return;

        try
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                if (_clipLauncher == null) return;

                CurrentBeat = _clipLauncher.CurrentBeat;
                IsRunning = _clipLauncher.IsRunning;

                // Update clip slots
                foreach (var slotVm in ClipSlots)
                {
                    slotVm.UpdateFromSlot();
                }

                // Update scenes
                foreach (var sceneVm in Scenes)
                {
                    sceneVm.UpdateFromScene();
                }

                // Update track playing states
                for (int i = 0; i < TrackCount && i < Tracks.Count; i++)
                {
                    var playingClip = _clipLauncher.GetPlayingClip(i);
                    Tracks[i].HasPlayingClip = playingClip != null;
                }
            });
        }
        catch
        {
            // Ignore dispatcher exceptions during shutdown
        }
    }

    /// <summary>
    /// Converts a QuantizeLaunchOption to QuantizeMode.
    /// </summary>
    private static QuantizeMode ConvertQuantizeOption(QuantizeLaunchOption option)
    {
        return option switch
        {
            QuantizeLaunchOption.None => QuantizeMode.None,
            QuantizeLaunchOption.OneBar => QuantizeMode.Bar,
            QuantizeLaunchOption.HalfBar => QuantizeMode.Beat, // 2 beats
            QuantizeLaunchOption.QuarterNote => QuantizeMode.Beat,
            QuantizeLaunchOption.EighthNote => QuantizeMode.Eighth,
            QuantizeLaunchOption.SixteenthNote => QuantizeMode.Sixteenth,
            _ => QuantizeMode.Bar
        };
    }

    partial void OnSelectedQuantizeChanged(QuantizeLaunchOption value)
    {
        if (_clipLauncher != null)
        {
            _clipLauncher.GlobalQuantize = ConvertQuantizeOption(value);
        }
    }

    partial void OnBpmChanged(double value)
    {
        if (_clipLauncher != null)
        {
            _clipLauncher.Bpm = value;
        }
    }

    /// <summary>
    /// Launches a clip at the specified position.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="sceneIndex">Scene index.</param>
    [RelayCommand]
    private void LaunchClip(ClipSlotViewModel? slot)
    {
        if (slot == null || _clipLauncher == null) return;

        _clipLauncher.LaunchClip(slot.TrackIndex, slot.SceneIndex);
        ClipLaunched?.Invoke(this, (slot.TrackIndex, slot.SceneIndex));
        StatusMessage = $"Launched clip at Track {slot.TrackIndex + 1}, Scene {slot.SceneIndex + 1}";
    }

    /// <summary>
    /// Launches a scene.
    /// </summary>
    /// <param name="sceneIndex">Scene index.</param>
    [RelayCommand]
    private void LaunchScene(SceneViewModel? scene)
    {
        if (scene == null || _clipLauncher == null) return;

        _clipLauncher.LaunchScene(scene.Index);
        SceneLaunched?.Invoke(this, scene.Index);
        StatusMessage = $"Launched {scene.Name}";
    }

    /// <summary>
    /// Stops all clips on a track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    [RelayCommand]
    private void StopTrack(SessionTrackViewModel? track)
    {
        if (track == null || _clipLauncher == null) return;

        _clipLauncher.StopTrack(track.Index);
        StatusMessage = $"Stopped {track.Name}";
    }

    /// <summary>
    /// Stops all clips.
    /// </summary>
    [RelayCommand]
    private void StopAll()
    {
        _clipLauncher?.StopAll();
        StatusMessage = "Stopped all clips";
    }

    /// <summary>
    /// Starts the clip launcher.
    /// </summary>
    [RelayCommand]
    private void Start()
    {
        _clipLauncher?.Start();
        IsRunning = true;
        StatusMessage = "Session started";
    }

    /// <summary>
    /// Stops the clip launcher.
    /// </summary>
    [RelayCommand]
    private void Stop()
    {
        _clipLauncher?.Stop();
        IsRunning = false;
        StatusMessage = "Session stopped";
    }

    /// <summary>
    /// Resets the clip launcher to beat 0.
    /// </summary>
    [RelayCommand]
    private void Reset()
    {
        _clipLauncher?.Reset();
        CurrentBeat = 0;
        StatusMessage = "Session reset";
    }

    /// <summary>
    /// Adds a new scene.
    /// </summary>
    [RelayCommand]
    private void AddScene()
    {
        if (_clipLauncher == null) return;

        var scene = _clipLauncher.AddScene();
        Scenes.Add(new SceneViewModel(scene));

        // Add slots for the new scene
        for (int track = 0; track < TrackCount; track++)
        {
            var slot = _clipLauncher.GetSlot(track, scene.Index);
            ClipSlots.Add(new ClipSlotViewModel(slot));
        }

        SceneCount = _clipLauncher.SceneCount;
        StatusMessage = $"Added {scene.Name}";
    }

    /// <summary>
    /// Requests editing of a clip.
    /// </summary>
    /// <param name="slot">The slot containing the clip to edit.</param>
    [RelayCommand]
    private void EditClip(ClipSlotViewModel? slot)
    {
        if (slot == null || !slot.HasClip) return;

        ClipEditRequested?.Invoke(this, slot);
        StatusMessage = $"Editing {slot.ClipName}";
    }

    /// <summary>
    /// Selects a clip slot.
    /// </summary>
    /// <param name="slot">The slot to select.</param>
    [RelayCommand]
    private void SelectSlot(ClipSlotViewModel? slot)
    {
        if (SelectedSlot != null)
        {
            SelectedSlot.IsSelected = false;
        }

        SelectedSlot = slot;

        if (slot != null)
        {
            slot.IsSelected = true;
        }
    }

    /// <summary>
    /// Creates a new clip in an empty slot.
    /// </summary>
    /// <param name="slot">The slot to create a clip in.</param>
    [RelayCommand]
    private void CreateClip(ClipSlotViewModel? slot)
    {
        if (slot == null || slot.HasClip || _clipLauncher == null) return;

        var engineSlot = slot.GetClipSlot();
        if (engineSlot == null) return;

        var clip = new LaunchClip
        {
            Name = $"New Clip {slot.TrackIndex + 1}-{slot.SceneIndex + 1}",
            LengthBeats = 4.0,
            Loop = true
        };

        // Use track color if available
        if (slot.TrackIndex < Tracks.Count)
        {
            var trackColor = Tracks[slot.TrackIndex].Color;
            clip.Color = System.Drawing.Color.FromArgb(trackColor.A, trackColor.R, trackColor.G, trackColor.B);
        }

        engineSlot.SetClip(clip);
        slot.UpdateFromSlot();
        StatusMessage = $"Created clip at Track {slot.TrackIndex + 1}, Scene {slot.SceneIndex + 1}";
    }

    /// <summary>
    /// Deletes a clip from a slot.
    /// </summary>
    /// <param name="slot">The slot containing the clip to delete.</param>
    [RelayCommand]
    private void DeleteClip(ClipSlotViewModel? slot)
    {
        if (slot == null || !slot.HasClip) return;

        var engineSlot = slot.GetClipSlot();
        engineSlot?.Clear();
        slot.UpdateFromSlot();
        StatusMessage = $"Deleted clip from Track {slot.TrackIndex + 1}, Scene {slot.SceneIndex + 1}";
    }

    /// <summary>
    /// Duplicates a clip to another slot.
    /// </summary>
    /// <param name="slot">The slot containing the clip to duplicate.</param>
    [RelayCommand]
    private void DuplicateClip(ClipSlotViewModel? slot)
    {
        if (slot == null || !slot.HasClip || _clipLauncher == null) return;

        var engineSlot = slot.GetClipSlot();
        if (engineSlot?.Clip == null) return;

        // Find next empty slot in the same track
        for (int scene = slot.SceneIndex + 1; scene < SceneCount; scene++)
        {
            var targetSlot = _clipLauncher.GetSlot(slot.TrackIndex, scene);
            if (!targetSlot.HasClip)
            {
                engineSlot.DuplicateTo(targetSlot);
                RefreshFromLauncher();
                StatusMessage = $"Duplicated clip to Scene {scene + 1}";
                return;
            }
        }

        StatusMessage = "No empty slot found for duplication";
    }

    /// <summary>
    /// Toggles solo state for a track.
    /// </summary>
    [RelayCommand]
    private void ToggleSolo(SessionTrackViewModel? track)
    {
        if (track == null) return;
        track.IsSoloed = !track.IsSoloed;
    }

    /// <summary>
    /// Toggles mute state for a track.
    /// </summary>
    [RelayCommand]
    private void ToggleMute(SessionTrackViewModel? track)
    {
        if (track == null) return;
        track.IsMuted = !track.IsMuted;
    }

    /// <summary>
    /// Toggles arm state for a track.
    /// </summary>
    [RelayCommand]
    private void ToggleArm(SessionTrackViewModel? track)
    {
        if (track == null) return;
        track.IsArmed = !track.IsArmed;
    }

    /// <summary>
    /// Processes clip launcher for the given time delta.
    /// Should be called from the audio engine.
    /// </summary>
    /// <param name="deltaBeats">Time elapsed in beats.</param>
    public void Process(double deltaBeats)
    {
        _clipLauncher?.Process(deltaBeats);
    }

    #region Event Handlers

    private void OnClipStateChanged(object? sender, ClipStateChangedEventArgs e)
    {
        // Update relevant slot ViewModel
        var slot = ClipSlots.FirstOrDefault(s => s.TrackIndex == e.TrackIndex && s.SceneIndex == e.SceneIndex);
        slot?.UpdateFromSlot();
    }

    private void OnSceneLaunched(object? sender, Scene scene)
    {
        SelectedScene = Scenes.FirstOrDefault(s => s.Index == scene.Index);
    }

    private void OnTrackStopped(object? sender, TrackStopEventArgs e)
    {
        if (e.TrackIndex < Tracks.Count)
        {
            Tracks[e.TrackIndex].HasPlayingClip = false;
        }
    }

    private void OnAllStopped(object? sender, EventArgs e)
    {
        foreach (var track in Tracks)
        {
            track.HasPlayingClip = false;
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the SessionViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _updateTimer?.Stop();
        _updateTimer?.Dispose();

        if (_clipLauncher != null)
        {
            _clipLauncher.ClipStateChanged -= OnClipStateChanged;
            _clipLauncher.SceneLaunched -= OnSceneLaunched;
            _clipLauncher.TrackStopped -= OnTrackStopped;
            _clipLauncher.AllStopped -= OnAllStopped;
            _clipLauncher.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
