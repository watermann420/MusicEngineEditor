using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Services;

/// <summary>
/// Shuffle edit mode for ripple editing.
/// Supports ripple edit, insert edit, and delete-close-gap operations.
/// </summary>
public sealed class ShuffleEditService : INotifyPropertyChanged
{
    private static ShuffleEditService? _instance;
    private static readonly object _lock = new();

    private ShuffleEditMode _mode = ShuffleEditMode.Normal;
    private bool _affectAllTracks = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static ShuffleEditService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ShuffleEditService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets or sets the current shuffle edit mode.
    /// </summary>
    public ShuffleEditMode Mode
    {
        get => _mode;
        set
        {
            if (_mode != value)
            {
                _mode = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(ModeDisplayName));
                NotifyPropertyChanged(nameof(IsRippleMode));
                NotifyPropertyChanged(nameof(IsInsertMode));
                NotifyPropertyChanged(nameof(IsNormalMode));
                ModeChanged?.Invoke(this, _mode);
            }
        }
    }

    /// <summary>
    /// Gets or sets whether operations affect all tracks or just the current track.
    /// </summary>
    public bool AffectAllTracks
    {
        get => _affectAllTracks;
        set
        {
            if (_affectAllTracks != value)
            {
                _affectAllTracks = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the display name of the current mode.
    /// </summary>
    public string ModeDisplayName => Mode switch
    {
        ShuffleEditMode.Normal => "Normal",
        ShuffleEditMode.Ripple => "Ripple",
        ShuffleEditMode.Insert => "Insert",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets whether ripple mode is active.
    /// </summary>
    public bool IsRippleMode => _mode == ShuffleEditMode.Ripple;

    /// <summary>
    /// Gets whether insert mode is active.
    /// </summary>
    public bool IsInsertMode => _mode == ShuffleEditMode.Insert;

    /// <summary>
    /// Gets whether normal mode is active.
    /// </summary>
    public bool IsNormalMode => _mode == ShuffleEditMode.Normal;

    /// <summary>
    /// Raised when the mode changes.
    /// </summary>
    public event EventHandler<ShuffleEditMode>? ModeChanged;

    private ShuffleEditService() { }

    /// <summary>
    /// Cycles through the available edit modes.
    /// </summary>
    public void CycleMode()
    {
        Mode = Mode switch
        {
            ShuffleEditMode.Normal => ShuffleEditMode.Ripple,
            ShuffleEditMode.Ripple => ShuffleEditMode.Insert,
            ShuffleEditMode.Insert => ShuffleEditMode.Normal,
            _ => ShuffleEditMode.Normal
        };
    }

    /// <summary>
    /// Creates a command for inserting a clip with ripple.
    /// </summary>
    /// <param name="clips">All clips in the arrangement.</param>
    /// <param name="newClip">The clip being inserted.</param>
    /// <param name="trackIndex">The track to insert on (-1 for all tracks).</param>
    /// <returns>An undoable command for the insertion.</returns>
    public IUndoableCommand CreateInsertCommand(
        IList<ClipViewModel> clips,
        ClipViewModel newClip,
        int trackIndex = -1)
    {
        return new InsertClipCommand(clips, newClip, trackIndex, _affectAllTracks);
    }

    /// <summary>
    /// Creates a command for deleting a clip with gap closing.
    /// </summary>
    /// <param name="clips">All clips in the arrangement.</param>
    /// <param name="clipToDelete">The clip being deleted.</param>
    /// <param name="trackIndex">The track to affect (-1 for all tracks).</param>
    /// <returns>An undoable command for the deletion.</returns>
    public IUndoableCommand CreateDeleteAndCloseGapCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToDelete,
        int trackIndex = -1)
    {
        return new DeleteAndCloseGapCommand(clips, clipToDelete, trackIndex, _affectAllTracks);
    }

    /// <summary>
    /// Creates a command for moving a clip with ripple.
    /// </summary>
    /// <param name="clips">All clips in the arrangement.</param>
    /// <param name="clipToMove">The clip being moved.</param>
    /// <param name="newPosition">The new start position.</param>
    /// <param name="trackIndex">The track to affect (-1 for all tracks).</param>
    /// <returns>An undoable command for the move.</returns>
    public IUndoableCommand CreateRippleMoveCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToMove,
        double newPosition,
        int trackIndex = -1)
    {
        return new RippleMoveCommand(clips, clipToMove, newPosition, trackIndex, _affectAllTracks);
    }

    /// <summary>
    /// Creates a command for resizing a clip with ripple.
    /// </summary>
    /// <param name="clips">All clips in the arrangement.</param>
    /// <param name="clipToResize">The clip being resized.</param>
    /// <param name="newLength">The new length.</param>
    /// <param name="trackIndex">The track to affect (-1 for all tracks).</param>
    /// <returns>An undoable command for the resize.</returns>
    public IUndoableCommand CreateRippleResizeCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToResize,
        double newLength,
        int trackIndex = -1)
    {
        return new RippleResizeCommand(clips, clipToResize, newLength, trackIndex, _affectAllTracks);
    }

    /// <summary>
    /// Calculates the ripple delta for clips after a given position.
    /// </summary>
    /// <param name="clips">All clips.</param>
    /// <param name="position">The position to ripple from.</param>
    /// <param name="delta">The amount to shift.</param>
    /// <param name="trackIndex">The track to affect (-1 for all tracks).</param>
    /// <param name="affectAllTracks">Whether to affect all tracks.</param>
    /// <returns>Dictionary of clips and their new positions.</returns>
    public static Dictionary<ClipViewModel, double> CalculateRipple(
        IEnumerable<ClipViewModel> clips,
        double position,
        double delta,
        int trackIndex,
        bool affectAllTracks)
    {
        var result = new Dictionary<ClipViewModel, double>();

        var affectedClips = clips
            .Where(c => c.StartPosition >= position)
            .Where(c => affectAllTracks || trackIndex < 0 || c.TrackIndex == trackIndex);

        foreach (var clip in affectedClips)
        {
            var newPosition = clip.StartPosition + delta;
            if (newPosition >= 0)
            {
                result[clip] = newPosition;
            }
        }

        return result;
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Shuffle edit modes.
/// </summary>
public enum ShuffleEditMode
{
    /// <summary>Normal editing mode - no automatic rippling.</summary>
    Normal,

    /// <summary>Ripple mode - clips after edited clip move with changes.</summary>
    Ripple,

    /// <summary>Insert mode - inserts push clips right.</summary>
    Insert
}

/// <summary>
/// Command for inserting a clip with ripple effect.
/// </summary>
internal sealed class InsertClipCommand : IUndoableCommand
{
    private readonly IList<ClipViewModel> _clips;
    private readonly ClipViewModel _newClip;
    private readonly int _trackIndex;
    private readonly bool _affectAllTracks;
    private readonly Dictionary<ClipViewModel, double> _originalPositions = new();

    public string Description => $"Insert Clip \"{_newClip.Name}\"";

    public InsertClipCommand(
        IList<ClipViewModel> clips,
        ClipViewModel newClip,
        int trackIndex,
        bool affectAllTracks)
    {
        _clips = clips;
        _newClip = newClip;
        _trackIndex = trackIndex;
        _affectAllTracks = affectAllTracks;

        // Store original positions
        foreach (var clip in clips)
        {
            _originalPositions[clip] = clip.StartPosition;
        }
    }

    public void Execute()
    {
        // Calculate ripple
        var ripple = ShuffleEditService.CalculateRipple(
            _clips, _newClip.StartPosition, _newClip.Length, _trackIndex, _affectAllTracks);

        // Apply ripple
        foreach (var (clip, newPos) in ripple)
        {
            clip.StartPosition = newPos;
        }

        // Add the new clip
        if (!_clips.Contains(_newClip))
        {
            _clips.Add(_newClip);
        }
    }

    public void Undo()
    {
        // Remove the inserted clip
        _clips.Remove(_newClip);

        // Restore original positions
        foreach (var (clip, originalPos) in _originalPositions)
        {
            if (_clips.Contains(clip))
            {
                clip.StartPosition = originalPos;
            }
        }
    }
}

/// <summary>
/// Command for deleting a clip and closing the gap.
/// </summary>
internal sealed class DeleteAndCloseGapCommand : IUndoableCommand
{
    private readonly IList<ClipViewModel> _clips;
    private readonly ClipViewModel _deletedClip;
    private readonly int _trackIndex;
    private readonly bool _affectAllTracks;
    private readonly Dictionary<ClipViewModel, double> _originalPositions = new();
    private readonly double _deletedClipLength;
    private readonly double _deletedClipPosition;

    public string Description => $"Delete and Close Gap \"{_deletedClip.Name}\"";

    public DeleteAndCloseGapCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToDelete,
        int trackIndex,
        bool affectAllTracks)
    {
        _clips = clips;
        _deletedClip = clipToDelete;
        _trackIndex = trackIndex;
        _affectAllTracks = affectAllTracks;
        _deletedClipLength = clipToDelete.Length;
        _deletedClipPosition = clipToDelete.StartPosition;

        // Store original positions
        foreach (var clip in clips)
        {
            _originalPositions[clip] = clip.StartPosition;
        }
    }

    public void Execute()
    {
        // Remove the clip
        _clips.Remove(_deletedClip);

        // Calculate ripple (negative delta to close gap)
        var ripple = ShuffleEditService.CalculateRipple(
            _clips, _deletedClipPosition + _deletedClipLength, -_deletedClipLength, _trackIndex, _affectAllTracks);

        // Apply ripple
        foreach (var (clip, newPos) in ripple)
        {
            clip.StartPosition = newPos;
        }
    }

    public void Undo()
    {
        // Restore original positions
        foreach (var (clip, originalPos) in _originalPositions)
        {
            if (_clips.Contains(clip))
            {
                clip.StartPosition = originalPos;
            }
        }

        // Re-add the deleted clip
        if (!_clips.Contains(_deletedClip))
        {
            _clips.Add(_deletedClip);
        }
    }
}

/// <summary>
/// Command for moving a clip with ripple effect.
/// </summary>
internal sealed class RippleMoveCommand : IUndoableCommand
{
    private readonly IList<ClipViewModel> _clips;
    private readonly ClipViewModel _movedClip;
    private readonly double _newPosition;
    private readonly int _trackIndex;
    private readonly bool _affectAllTracks;
    private readonly Dictionary<ClipViewModel, double> _originalPositions = new();

    public string Description => $"Ripple Move \"{_movedClip.Name}\"";

    public RippleMoveCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToMove,
        double newPosition,
        int trackIndex,
        bool affectAllTracks)
    {
        _clips = clips;
        _movedClip = clipToMove;
        _newPosition = newPosition;
        _trackIndex = trackIndex;
        _affectAllTracks = affectAllTracks;

        // Store original positions
        foreach (var clip in clips)
        {
            _originalPositions[clip] = clip.StartPosition;
        }
    }

    public void Execute()
    {
        var delta = _newPosition - _movedClip.StartPosition;

        // Calculate ripple for clips after the original position
        var ripple = ShuffleEditService.CalculateRipple(
            _clips.Where(c => c != _movedClip),
            _movedClip.EndPosition,
            delta,
            _trackIndex,
            _affectAllTracks);

        // Apply ripple
        foreach (var (clip, newPos) in ripple)
        {
            clip.StartPosition = newPos;
        }

        // Move the clip
        _movedClip.StartPosition = _newPosition;
    }

    public void Undo()
    {
        // Restore all original positions
        foreach (var (clip, originalPos) in _originalPositions)
        {
            if (_clips.Contains(clip))
            {
                clip.StartPosition = originalPos;
            }
        }
    }
}

/// <summary>
/// Command for resizing a clip with ripple effect.
/// </summary>
internal sealed class RippleResizeCommand : IUndoableCommand
{
    private readonly IList<ClipViewModel> _clips;
    private readonly ClipViewModel _resizedClip;
    private readonly double _newLength;
    private readonly int _trackIndex;
    private readonly bool _affectAllTracks;
    private readonly Dictionary<ClipViewModel, double> _originalPositions = new();
    private readonly double _originalLength;

    public string Description => $"Ripple Resize \"{_resizedClip.Name}\"";

    public RippleResizeCommand(
        IList<ClipViewModel> clips,
        ClipViewModel clipToResize,
        double newLength,
        int trackIndex,
        bool affectAllTracks)
    {
        _clips = clips;
        _resizedClip = clipToResize;
        _newLength = newLength;
        _trackIndex = trackIndex;
        _affectAllTracks = affectAllTracks;
        _originalLength = clipToResize.Length;

        // Store original positions
        foreach (var clip in clips)
        {
            _originalPositions[clip] = clip.StartPosition;
        }
    }

    public void Execute()
    {
        var delta = _newLength - _originalLength;

        // Calculate ripple for clips after the resized clip
        var ripple = ShuffleEditService.CalculateRipple(
            _clips.Where(c => c != _resizedClip),
            _resizedClip.EndPosition,
            delta,
            _trackIndex,
            _affectAllTracks);

        // Apply ripple
        foreach (var (clip, newPos) in ripple)
        {
            clip.StartPosition = newPos;
        }

        // Resize the clip
        _resizedClip.Length = _newLength;
    }

    public void Undo()
    {
        // Restore the original length
        _resizedClip.Length = _originalLength;

        // Restore all original positions
        foreach (var (clip, originalPos) in _originalPositions)
        {
            if (_clips.Contains(clip) && clip != _resizedClip)
            {
                clip.StartPosition = originalPos;
            }
        }
    }
}
