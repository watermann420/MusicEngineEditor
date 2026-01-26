using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing track groups and group-based editing operations.
/// Supports grouped editing where operations apply to all group members.
/// </summary>
public sealed class GroupEditingService : INotifyPropertyChanged
{
    private static GroupEditingService? _instance;
    private static readonly object _lock = new();

    private readonly ObservableCollection<TrackEditGroup> _groups = new();
    private TrackEditGroup? _selectedGroup;
    private bool _isPhaseLockEnabled;
    private int _nextGroupId = 1;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static GroupEditingService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new GroupEditingService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets the collection of track groups.
    /// </summary>
    public ObservableCollection<TrackEditGroup> Groups => _groups;

    /// <summary>
    /// Gets or sets the selected group.
    /// </summary>
    public TrackEditGroup? SelectedGroup
    {
        get => _selectedGroup;
        set
        {
            if (_selectedGroup != value)
            {
                _selectedGroup = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged(nameof(HasSelectedGroup));
            }
        }
    }

    /// <summary>
    /// Gets whether a group is selected.
    /// </summary>
    public bool HasSelectedGroup => _selectedGroup != null;

    /// <summary>
    /// Gets or sets whether phase-lock editing is enabled.
    /// When enabled, edits to grouped clips maintain phase relationships.
    /// </summary>
    public bool IsPhaseLockEnabled
    {
        get => _isPhaseLockEnabled;
        set
        {
            if (_isPhaseLockEnabled != value)
            {
                _isPhaseLockEnabled = value;
                NotifyPropertyChanged();
                PhaseLockChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Raised when a group is created.
    /// </summary>
    public event EventHandler<TrackEditGroup>? GroupCreated;

    /// <summary>
    /// Raised when a group is deleted.
    /// </summary>
    public event EventHandler<TrackEditGroup>? GroupDeleted;

    /// <summary>
    /// Raised when a group is modified.
    /// </summary>
    public event EventHandler<TrackEditGroup>? GroupModified;

    /// <summary>
    /// Raised when phase-lock setting changes.
    /// </summary>
    public event EventHandler<bool>? PhaseLockChanged;

    private GroupEditingService() { }

    /// <summary>
    /// Creates a new track group.
    /// </summary>
    /// <param name="name">The group name.</param>
    /// <param name="color">The group color (hex string).</param>
    /// <returns>The created group.</returns>
    public TrackEditGroup CreateGroup(string name, string color = "#FF9800")
    {
        var group = new TrackEditGroup
        {
            Id = _nextGroupId++,
            Name = string.IsNullOrWhiteSpace(name) ? $"Group {_nextGroupId}" : name,
            Color = color
        };

        _groups.Add(group);
        GroupCreated?.Invoke(this, group);

        return group;
    }

    /// <summary>
    /// Creates a group from selected tracks.
    /// </summary>
    /// <param name="trackIndices">The track indices to group.</param>
    /// <param name="name">The group name.</param>
    /// <param name="color">The group color.</param>
    /// <returns>The created group.</returns>
    public TrackEditGroup CreateGroupFromTracks(IEnumerable<int> trackIndices, string name, string color = "#FF9800")
    {
        var group = CreateGroup(name, color);

        foreach (var index in trackIndices)
        {
            group.AddTrack(index);
        }

        return group;
    }

    /// <summary>
    /// Deletes a group.
    /// </summary>
    /// <param name="group">The group to delete.</param>
    public void DeleteGroup(TrackEditGroup group)
    {
        if (_groups.Remove(group))
        {
            if (_selectedGroup == group)
            {
                SelectedGroup = null;
            }

            GroupDeleted?.Invoke(this, group);
        }
    }

    /// <summary>
    /// Gets the group that contains a specific track.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>The group, or null if the track is not in a group.</returns>
    public TrackEditGroup? GetGroupForTrack(int trackIndex)
    {
        return _groups.FirstOrDefault(g => g.TrackIndices.Contains(trackIndex));
    }

    /// <summary>
    /// Gets all tracks that should be affected by an operation on a track.
    /// Returns the track itself plus all tracks in its group.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>All affected track indices.</returns>
    public IEnumerable<int> GetAffectedTracks(int trackIndex)
    {
        var group = GetGroupForTrack(trackIndex);
        if (group != null && group.IsEditLinked)
        {
            return group.TrackIndices;
        }

        return new[] { trackIndex };
    }

    /// <summary>
    /// Adds a track to a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="trackIndex">The track index to add.</param>
    public void AddTrackToGroup(TrackEditGroup group, int trackIndex)
    {
        // Remove from any existing group first
        var existingGroup = GetGroupForTrack(trackIndex);
        existingGroup?.RemoveTrack(trackIndex);

        group.AddTrack(trackIndex);
        GroupModified?.Invoke(this, group);
    }

    /// <summary>
    /// Removes a track from its group.
    /// </summary>
    /// <param name="trackIndex">The track index to remove.</param>
    public void RemoveTrackFromGroup(int trackIndex)
    {
        var group = GetGroupForTrack(trackIndex);
        if (group != null)
        {
            group.RemoveTrack(trackIndex);
            GroupModified?.Invoke(this, group);

            // Delete empty groups
            if (group.TrackIndices.Count == 0)
            {
                DeleteGroup(group);
            }
        }
    }

    /// <summary>
    /// Creates a command for grouped gain change.
    /// </summary>
    /// <param name="clips">All clips.</param>
    /// <param name="sourceClip">The clip that triggered the change.</param>
    /// <param name="newGainDb">The new gain value.</param>
    /// <returns>An undoable command.</returns>
    public IUndoableCommand CreateGroupedGainCommand(
        IEnumerable<ClipViewModel> clips,
        ClipViewModel sourceClip,
        double newGainDb)
    {
        var affectedTracks = GetAffectedTracks(sourceClip.TrackIndex);
        var affectedClips = clips.Where(c => affectedTracks.Contains(c.TrackIndex)).ToList();

        return new GroupedGainCommand(affectedClips, newGainDb);
    }

    /// <summary>
    /// Creates a command for grouped mute.
    /// </summary>
    /// <param name="clips">All clips.</param>
    /// <param name="sourceClip">The clip that triggered the change.</param>
    /// <param name="isMuted">The mute state.</param>
    /// <returns>An undoable command.</returns>
    public IUndoableCommand CreateGroupedMuteCommand(
        IEnumerable<ClipViewModel> clips,
        ClipViewModel sourceClip,
        bool isMuted)
    {
        var affectedTracks = GetAffectedTracks(sourceClip.TrackIndex);
        var affectedClips = clips.Where(c => affectedTracks.Contains(c.TrackIndex)).ToList();

        return new GroupedMuteCommand(affectedClips, isMuted);
    }

    /// <summary>
    /// Creates a command for phase-locked editing.
    /// Maintains relative positions when moving grouped clips.
    /// </summary>
    /// <param name="clips">All clips.</param>
    /// <param name="sourceClip">The clip being moved.</param>
    /// <param name="newPosition">The new position.</param>
    /// <returns>An undoable command.</returns>
    public IUndoableCommand CreatePhaseLockMoveCommand(
        IEnumerable<ClipViewModel> clips,
        ClipViewModel sourceClip,
        double newPosition)
    {
        if (!IsPhaseLockEnabled)
        {
            return new SingleClipMoveCommand(sourceClip, newPosition);
        }

        var affectedTracks = GetAffectedTracks(sourceClip.TrackIndex);
        var affectedClips = clips.Where(c => affectedTracks.Contains(c.TrackIndex)).ToList();
        var delta = newPosition - sourceClip.StartPosition;

        return new PhaseLockMoveCommand(affectedClips, delta);
    }

    /// <summary>
    /// Solos a group (mutes all tracks not in the group).
    /// </summary>
    /// <param name="group">The group to solo.</param>
    /// <param name="allTrackIndices">All track indices in the project.</param>
    public void SoloGroup(TrackEditGroup group, IEnumerable<int> allTrackIndices)
    {
        group.IsSolo = true;

        // Notify about solo state change
        GroupModified?.Invoke(this, group);
    }

    /// <summary>
    /// Mutes all tracks in a group.
    /// </summary>
    /// <param name="group">The group to mute.</param>
    public void MuteGroup(TrackEditGroup group)
    {
        group.IsMuted = true;
        GroupModified?.Invoke(this, group);
    }

    /// <summary>
    /// Unmutes all tracks in a group.
    /// </summary>
    /// <param name="group">The group to unmute.</param>
    public void UnmuteGroup(TrackEditGroup group)
    {
        group.IsMuted = false;
        GroupModified?.Invoke(this, group);
    }

    /// <summary>
    /// Sets the gain for all tracks in a group.
    /// </summary>
    /// <param name="group">The group.</param>
    /// <param name="gainDb">The gain in dB.</param>
    public void SetGroupGain(TrackEditGroup group, double gainDb)
    {
        group.GainDb = gainDb;
        GroupModified?.Invoke(this, group);
    }

    /// <summary>
    /// Clears all groups.
    /// </summary>
    public void ClearAllGroups()
    {
        var groupsCopy = _groups.ToList();
        _groups.Clear();
        SelectedGroup = null;

        foreach (var group in groupsCopy)
        {
            GroupDeleted?.Invoke(this, group);
        }
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Represents a group of tracks for editing purposes.
/// </summary>
public class TrackEditGroup : INotifyPropertyChanged
{
    private string _name = "";
    private string _color = "#FF9800";
    private bool _isMuted;
    private bool _isSolo;
    private double _gainDb;
    private bool _isEditLinked = true;
    private readonly List<int> _trackIndices = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets the group ID.
    /// </summary>
    public int Id { get; init; }

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the group color.
    /// </summary>
    public string Color
    {
        get => _color;
        set
        {
            if (_color != value)
            {
                _color = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the track indices in this group.
    /// </summary>
    public IReadOnlyList<int> TrackIndices => _trackIndices;

    /// <summary>
    /// Gets or sets whether the group is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (_isMuted != value)
            {
                _isMuted = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the group is soloed.
    /// </summary>
    public bool IsSolo
    {
        get => _isSolo;
        set
        {
            if (_isSolo != value)
            {
                _isSolo = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the group gain in dB.
    /// </summary>
    public double GainDb
    {
        get => _gainDb;
        set
        {
            if (Math.Abs(_gainDb - value) > 0.001)
            {
                _gainDb = Math.Clamp(value, -96.0, 12.0);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether edits are linked across the group.
    /// </summary>
    public bool IsEditLinked
    {
        get => _isEditLinked;
        set
        {
            if (_isEditLinked != value)
            {
                _isEditLinked = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the display text for the group.
    /// </summary>
    public string DisplayText => $"{Name} ({_trackIndices.Count} tracks)";

    /// <summary>
    /// Adds a track to the group.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    public void AddTrack(int trackIndex)
    {
        if (!_trackIndices.Contains(trackIndex))
        {
            _trackIndices.Add(trackIndex);
            _trackIndices.Sort();
            OnPropertyChanged(nameof(TrackIndices));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    /// Removes a track from the group.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    public void RemoveTrack(int trackIndex)
    {
        if (_trackIndices.Remove(trackIndex))
        {
            OnPropertyChanged(nameof(TrackIndices));
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    /// Checks if a track is in this group.
    /// </summary>
    /// <param name="trackIndex">The track index.</param>
    /// <returns>True if the track is in the group.</returns>
    public bool ContainsTrack(int trackIndex) => _trackIndices.Contains(trackIndex);

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Command for grouped gain changes.
/// </summary>
internal sealed class GroupedGainCommand : IUndoableCommand
{
    private readonly List<ClipViewModel> _clips;
    private readonly Dictionary<ClipViewModel, double> _originalGains = new();
    private readonly double _newGainDb;

    public string Description => $"Group Gain Change to {_newGainDb:F1} dB";

    public GroupedGainCommand(List<ClipViewModel> clips, double newGainDb)
    {
        _clips = clips;
        _newGainDb = newGainDb;

        foreach (var clip in clips)
        {
            _originalGains[clip] = clip.GainDb;
        }
    }

    public void Execute()
    {
        foreach (var clip in _clips)
        {
            clip.GainDb = _newGainDb;
        }
    }

    public void Undo()
    {
        foreach (var (clip, originalGain) in _originalGains)
        {
            clip.GainDb = originalGain;
        }
    }
}

/// <summary>
/// Command for grouped mute changes.
/// </summary>
internal sealed class GroupedMuteCommand : IUndoableCommand
{
    private readonly List<ClipViewModel> _clips;
    private readonly Dictionary<ClipViewModel, bool> _originalMuteStates = new();
    private readonly bool _isMuted;

    public string Description => _isMuted ? "Group Mute" : "Group Unmute";

    public GroupedMuteCommand(List<ClipViewModel> clips, bool isMuted)
    {
        _clips = clips;
        _isMuted = isMuted;

        foreach (var clip in clips)
        {
            _originalMuteStates[clip] = clip.IsMuted;
        }
    }

    public void Execute()
    {
        foreach (var clip in _clips)
        {
            clip.IsMuted = _isMuted;
        }
    }

    public void Undo()
    {
        foreach (var (clip, originalState) in _originalMuteStates)
        {
            clip.IsMuted = originalState;
        }
    }
}

/// <summary>
/// Command for moving a single clip.
/// </summary>
internal sealed class SingleClipMoveCommand : IUndoableCommand
{
    private readonly ClipViewModel _clip;
    private readonly double _originalPosition;
    private readonly double _newPosition;

    public string Description => $"Move Clip \"{_clip.Name}\"";

    public SingleClipMoveCommand(ClipViewModel clip, double newPosition)
    {
        _clip = clip;
        _originalPosition = clip.StartPosition;
        _newPosition = newPosition;
    }

    public void Execute()
    {
        _clip.StartPosition = _newPosition;
    }

    public void Undo()
    {
        _clip.StartPosition = _originalPosition;
    }
}

/// <summary>
/// Command for phase-locked movement of grouped clips.
/// </summary>
internal sealed class PhaseLockMoveCommand : IUndoableCommand
{
    private readonly List<ClipViewModel> _clips;
    private readonly Dictionary<ClipViewModel, double> _originalPositions = new();
    private readonly double _delta;

    public string Description => "Phase-Lock Move";

    public PhaseLockMoveCommand(List<ClipViewModel> clips, double delta)
    {
        _clips = clips;
        _delta = delta;

        foreach (var clip in clips)
        {
            _originalPositions[clip] = clip.StartPosition;
        }
    }

    public void Execute()
    {
        foreach (var clip in _clips)
        {
            var newPosition = clip.StartPosition + _delta;
            if (newPosition >= 0)
            {
                clip.StartPosition = newPosition;
            }
        }
    }

    public void Undo()
    {
        foreach (var (clip, originalPosition) in _originalPositions)
        {
            clip.StartPosition = originalPosition;
        }
    }
}
