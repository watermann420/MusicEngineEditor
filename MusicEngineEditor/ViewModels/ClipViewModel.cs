using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents the type of clip.
/// </summary>
public enum ClipType
{
    Audio,
    Midi
}

/// <summary>
/// ViewModel for clips in the arrangement view.
/// Provides a unified interface for both Audio and MIDI clips.
/// </summary>
public partial class ClipViewModel : ViewModelBase
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique identifier for this clip.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets the globally unique identifier for this clip.
    /// </summary>
    public Guid Guid { get; } = Guid.NewGuid();

    /// <summary>
    /// Gets the type of clip (Audio or MIDI).
    /// </summary>
    public ClipType ClipType { get; }

    /// <summary>
    /// Gets or sets the name of the clip.
    /// </summary>
    [ObservableProperty]
    private string _name = "Clip";

    /// <summary>
    /// Gets or sets the start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _startPosition;

    /// <summary>
    /// Gets or sets the length in beats.
    /// </summary>
    [ObservableProperty]
    private double _length = 4.0;

    /// <summary>
    /// Gets the end position in beats.
    /// </summary>
    public double EndPosition => StartPosition + Length;

    /// <summary>
    /// Gets or sets the track index this clip belongs to.
    /// </summary>
    [ObservableProperty]
    private int _trackIndex;

    /// <summary>
    /// Gets or sets whether the clip is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets or sets whether the clip is locked (cannot be edited).
    /// </summary>
    [ObservableProperty]
    private bool _isLocked;

    /// <summary>
    /// Gets or sets whether the clip is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets the color of the clip (hex string).
    /// </summary>
    [ObservableProperty]
    private string _color = "#4CAF50";

    /// <summary>
    /// Gets or sets the fade-in duration in beats (for audio clips).
    /// </summary>
    [ObservableProperty]
    private double _fadeInDuration;

    /// <summary>
    /// Gets or sets the fade-out duration in beats (for audio clips).
    /// </summary>
    [ObservableProperty]
    private double _fadeOutDuration;

    /// <summary>
    /// Gets or sets the gain in dB (for audio clips).
    /// </summary>
    [ObservableProperty]
    private double _gainDb;

    /// <summary>
    /// Gets or sets the file path (for audio clips).
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// Gets or sets the waveform data for display (for audio clips).
    /// </summary>
    [ObservableProperty]
    private float[]? _waveformData;

    /// <summary>
    /// Gets or sets whether the clip is looping (for MIDI clips).
    /// </summary>
    [ObservableProperty]
    private bool _isLooping;

    /// <summary>
    /// Gets or sets the loop length in beats (for MIDI clips).
    /// </summary>
    [ObservableProperty]
    private double _loopLength;

    /// <summary>
    /// Gets or sets the note data for display (for MIDI clips).
    /// </summary>
    [ObservableProperty]
    private MiniNoteData[]? _noteData;

    /// <summary>
    /// Gets or sets the source offset in beats (for trimmed clips).
    /// </summary>
    [ObservableProperty]
    private double _sourceOffset;

    /// <summary>
    /// Gets or sets the original length before trimming.
    /// </summary>
    [ObservableProperty]
    private double _originalLength;

    /// <summary>
    /// Gets or sets whether the clip has been modified.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    /// Event raised when the clip is split.
    /// </summary>
    public event EventHandler<ClipSplitEventArgs>? SplitRequested;

    /// <summary>
    /// Event raised when the clip is duplicated.
    /// </summary>
    public event EventHandler<ClipViewModel>? DuplicateRequested;

    /// <summary>
    /// Event raised when the clip is deleted.
    /// </summary>
    public event EventHandler<ClipViewModel>? DeleteRequested;

    /// <summary>
    /// Event raised when the clip edit is requested.
    /// </summary>
    public event EventHandler<ClipViewModel>? EditRequested;

    /// <summary>
    /// Event raised when bounce is requested.
    /// </summary>
    public event EventHandler<ClipViewModel>? BounceRequested;

    /// <summary>
    /// Creates a new clip view model.
    /// </summary>
    /// <param name="clipType">The type of clip.</param>
    public ClipViewModel(ClipType clipType)
    {
        Id = _nextId++;
        ClipType = clipType;
        OriginalLength = Length;
    }

    /// <summary>
    /// Creates a new audio clip view model.
    /// </summary>
    public static ClipViewModel CreateAudioClip(string name, double startPosition, double length, string? filePath = null)
    {
        return new ClipViewModel(ClipType.Audio)
        {
            Name = name,
            StartPosition = startPosition,
            Length = length,
            OriginalLength = length,
            FilePath = filePath,
            Color = "#4CAF50" // Green for audio
        };
    }

    /// <summary>
    /// Creates a new MIDI clip view model.
    /// </summary>
    public static ClipViewModel CreateMidiClip(string name, double startPosition, double length)
    {
        return new ClipViewModel(ClipType.Midi)
        {
            Name = name,
            StartPosition = startPosition,
            Length = length,
            OriginalLength = length,
            Color = "#2196F3" // Blue for MIDI
        };
    }

    /// <summary>
    /// Moves the clip to a new position.
    /// </summary>
    [RelayCommand]
    private void MoveTo(double newPosition)
    {
        if (IsLocked) return;
        StartPosition = Math.Max(0, newPosition);
        IsDirty = true;
    }

    /// <summary>
    /// Splits the clip at the specified position.
    /// </summary>
    [RelayCommand]
    private void Split(double position)
    {
        if (IsLocked) return;
        if (position <= StartPosition || position >= EndPosition) return;

        SplitRequested?.Invoke(this, new ClipSplitEventArgs(this, position));
    }

    /// <summary>
    /// Duplicates the clip.
    /// </summary>
    [RelayCommand]
    private void Duplicate()
    {
        var copy = new ClipViewModel(ClipType)
        {
            Name = Name + " (Copy)",
            StartPosition = EndPosition, // Place after original
            Length = Length,
            OriginalLength = OriginalLength,
            TrackIndex = TrackIndex,
            Color = Color,
            FadeInDuration = FadeInDuration,
            FadeOutDuration = FadeOutDuration,
            GainDb = GainDb,
            FilePath = FilePath,
            WaveformData = WaveformData,
            IsLooping = IsLooping,
            LoopLength = LoopLength,
            NoteData = NoteData,
            SourceOffset = SourceOffset
        };

        DuplicateRequested?.Invoke(this, copy);
    }

    /// <summary>
    /// Deletes the clip.
    /// </summary>
    [RelayCommand]
    private void Delete()
    {
        if (IsLocked) return;
        DeleteRequested?.Invoke(this, this);
    }

    /// <summary>
    /// Opens the clip for editing.
    /// </summary>
    [RelayCommand]
    private void Edit()
    {
        EditRequested?.Invoke(this, this);
    }

    /// <summary>
    /// Bounces the clip to audio.
    /// </summary>
    [RelayCommand]
    private void Bounce()
    {
        if (ClipType == ClipType.Audio) return; // Already audio
        BounceRequested?.Invoke(this, this);
    }

    /// <summary>
    /// Trims the start of the clip.
    /// </summary>
    [RelayCommand]
    private void TrimStart(double amount)
    {
        if (IsLocked) return;
        if (amount >= Length) return;

        StartPosition += amount;
        SourceOffset += amount;
        Length -= amount;
        IsDirty = true;
    }

    /// <summary>
    /// Trims the end of the clip.
    /// </summary>
    [RelayCommand]
    private void TrimEnd(double amount)
    {
        if (IsLocked) return;
        if (amount >= Length) return;

        Length -= amount;
        IsDirty = true;
    }

    /// <summary>
    /// Resizes the clip.
    /// </summary>
    public void Resize(double newLength)
    {
        if (IsLocked) return;
        Length = Math.Max(0.25, newLength); // Minimum 1/4 beat
        IsDirty = true;
    }

    /// <summary>
    /// Resizes from the left edge (changes start position and length).
    /// </summary>
    public void ResizeLeft(double newStart)
    {
        if (IsLocked) return;
        var end = EndPosition;
        if (newStart >= end) return;

        var delta = newStart - StartPosition;
        StartPosition = newStart;
        SourceOffset += delta;
        Length = end - newStart;
        IsDirty = true;
    }

    /// <summary>
    /// Sets the fade-in.
    /// </summary>
    [RelayCommand]
    private void SetFadeIn(double duration)
    {
        if (IsLocked || ClipType != ClipType.Audio) return;
        FadeInDuration = Math.Min(duration, Length / 2);
        IsDirty = true;
    }

    /// <summary>
    /// Sets the fade-out.
    /// </summary>
    [RelayCommand]
    private void SetFadeOut(double duration)
    {
        if (IsLocked || ClipType != ClipType.Audio) return;
        FadeOutDuration = Math.Min(duration, Length / 2);
        IsDirty = true;
    }

    partial void OnStartPositionChanged(double value)
    {
        OnPropertyChanged(nameof(EndPosition));
    }

    partial void OnLengthChanged(double value)
    {
        OnPropertyChanged(nameof(EndPosition));
    }
}

/// <summary>
/// Event arguments for clip split events.
/// </summary>
public class ClipSplitEventArgs : EventArgs
{
    /// <summary>
    /// Gets the original clip being split.
    /// </summary>
    public ClipViewModel OriginalClip { get; }

    /// <summary>
    /// Gets the split position in beats.
    /// </summary>
    public double SplitPosition { get; }

    public ClipSplitEventArgs(ClipViewModel originalClip, double splitPosition)
    {
        OriginalClip = originalClip;
        SplitPosition = splitPosition;
    }
}

/// <summary>
/// Represents a note for mini piano roll display.
/// </summary>
public struct MiniNoteData
{
    /// <summary>
    /// The note number (0-127).
    /// </summary>
    public int Note { get; set; }

    /// <summary>
    /// The start position relative to clip start.
    /// </summary>
    public double Start { get; set; }

    /// <summary>
    /// The duration in beats.
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// The velocity (0-127).
    /// </summary>
    public int Velocity { get; set; }

    public MiniNoteData(int note, double start, double duration, int velocity = 100)
    {
        Note = note;
        Start = start;
        Duration = duration;
        Velocity = velocity;
    }
}
