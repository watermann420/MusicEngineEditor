using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing multiple versions (playlists) per track.
/// Supports version history, comparison, and switching between versions.
/// </summary>
public class TrackVersioningService : ITrackVersioningService
{
    #region Fields

    private readonly Dictionary<string, TrackVersionCollection> _trackVersions = new();
    private readonly Dictionary<string, string> _activeVersions = new();
    private readonly List<VersionHistoryEntry> _globalHistory = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    #endregion

    #region Events

    public event EventHandler<VersionChangedEventArgs>? VersionCreated;
    public event EventHandler<VersionChangedEventArgs>? VersionDeleted;
    public event EventHandler<VersionChangedEventArgs>? VersionRenamed;
    public event EventHandler<VersionSwitchedEventArgs>? VersionSwitched;
    public event EventHandler<VersionCompareEventArgs>? VersionCompared;

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a new version for a track.
    /// </summary>
    public TrackVersion CreateVersion(string trackId, string name, TrackVersionData data)
    {
        if (string.IsNullOrWhiteSpace(trackId))
            throw new ArgumentException("Track ID cannot be empty", nameof(trackId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Version name cannot be empty", nameof(name));

        var collection = GetOrCreateCollection(trackId);

        // Ensure unique name
        var finalName = GetUniqueVersionName(collection, name);

        var version = new TrackVersion
        {
            Id = Guid.NewGuid().ToString(),
            TrackId = trackId,
            Name = finalName,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Data = data
        };

        collection.Versions.Add(version);

        // If this is the first version, make it active
        if (collection.Versions.Count == 1 || !_activeVersions.ContainsKey(trackId))
        {
            _activeVersions[trackId] = version.Id;
        }

        AddHistoryEntry(trackId, version.Id, VersionAction.Created, $"Created version '{finalName}'");

        VersionCreated?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return version;
    }

    /// <summary>
    /// Creates a duplicate of an existing version.
    /// </summary>
    public TrackVersion DuplicateVersion(string trackId, string sourceVersionId, string newName)
    {
        var source = GetVersion(trackId, sourceVersionId);
        if (source == null)
            throw new InvalidOperationException($"Version '{sourceVersionId}' not found for track '{trackId}'");

        var dataCopy = source.Data?.Clone() ?? new TrackVersionData();
        return CreateVersion(trackId, newName, dataCopy);
    }

    /// <summary>
    /// Deletes a version.
    /// </summary>
    public bool DeleteVersion(string trackId, string versionId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return false;

        var version = collection.Versions.FirstOrDefault(v => v.Id == versionId);
        if (version == null)
            return false;

        // Don't delete if it's the only version
        if (collection.Versions.Count <= 1)
            return false;

        collection.Versions.Remove(version);

        // If deleting active version, switch to another
        if (_activeVersions.TryGetValue(trackId, out var activeId) && activeId == versionId)
        {
            var newActive = collection.Versions.First();
            _activeVersions[trackId] = newActive.Id;
            VersionSwitched?.Invoke(this, new VersionSwitchedEventArgs(trackId, version, newActive));
        }

        AddHistoryEntry(trackId, versionId, VersionAction.Deleted, $"Deleted version '{version.Name}'");

        VersionDeleted?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return true;
    }

    /// <summary>
    /// Renames a version.
    /// </summary>
    public bool RenameVersion(string trackId, string versionId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            return false;

        var version = GetVersion(trackId, versionId);
        if (version == null)
            return false;

        var collection = _trackVersions[trackId];
        var finalName = GetUniqueVersionName(collection, newName, versionId);

        var oldName = version.Name;
        version.Name = finalName;
        version.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(trackId, versionId, VersionAction.Renamed, $"Renamed version from '{oldName}' to '{finalName}'");

        VersionRenamed?.Invoke(this, new VersionChangedEventArgs(trackId, version));

        return true;
    }

    /// <summary>
    /// Switches to a different version.
    /// </summary>
    public TrackVersion? SwitchVersion(string trackId, string versionId)
    {
        var version = GetVersion(trackId, versionId);
        if (version == null)
            return null;

        TrackVersion? previousVersion = null;
        if (_activeVersions.TryGetValue(trackId, out var previousId))
        {
            previousVersion = GetVersion(trackId, previousId);
        }

        _activeVersions[trackId] = versionId;

        AddHistoryEntry(trackId, versionId, VersionAction.Switched, $"Switched to version '{version.Name}'");

        VersionSwitched?.Invoke(this, new VersionSwitchedEventArgs(trackId, previousVersion, version));

        return version;
    }

    /// <summary>
    /// Gets a specific version.
    /// </summary>
    public TrackVersion? GetVersion(string trackId, string versionId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return null;

        return collection.Versions.FirstOrDefault(v => v.Id == versionId);
    }

    /// <summary>
    /// Gets the currently active version for a track.
    /// </summary>
    public TrackVersion? GetActiveVersion(string trackId)
    {
        if (!_activeVersions.TryGetValue(trackId, out var versionId))
            return null;

        return GetVersion(trackId, versionId);
    }

    /// <summary>
    /// Gets all versions for a track.
    /// </summary>
    public IReadOnlyList<TrackVersion> GetVersions(string trackId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
            return Array.Empty<TrackVersion>();

        return collection.Versions.AsReadOnly();
    }

    /// <summary>
    /// Gets version history for a track.
    /// </summary>
    public IReadOnlyList<VersionHistoryEntry> GetVersionHistory(string trackId)
    {
        return _globalHistory
            .Where(h => h.TrackId == trackId)
            .OrderByDescending(h => h.Timestamp)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Compares two versions and returns the differences.
    /// </summary>
    public VersionComparison CompareVersions(string trackId, string versionId1, string versionId2)
    {
        var version1 = GetVersion(trackId, versionId1);
        var version2 = GetVersion(trackId, versionId2);

        if (version1 == null || version2 == null)
            throw new InvalidOperationException("One or both versions not found");

        var comparison = new VersionComparison
        {
            TrackId = trackId,
            Version1 = version1,
            Version2 = version2,
            ComparedAt = DateTime.UtcNow
        };

        // Compare data properties
        var data1 = version1.Data;
        var data2 = version2.Data;

        if (data1 != null && data2 != null)
        {
            // Compare notes
            if (!SequenceEqual(data1.NoteEvents, data2.NoteEvents))
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "NoteEvents",
                    Value1 = $"{data1.NoteEvents?.Count ?? 0} notes",
                    Value2 = $"{data2.NoteEvents?.Count ?? 0} notes"
                });
            }

            // Compare clips
            if (!SequenceEqual(data1.Clips, data2.Clips))
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "Clips",
                    Value1 = $"{data1.Clips?.Count ?? 0} clips",
                    Value2 = $"{data2.Clips?.Count ?? 0} clips"
                });
            }

            // Compare automation
            if (!SequenceEqual(data1.AutomationLanes, data2.AutomationLanes))
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "AutomationLanes",
                    Value1 = $"{data1.AutomationLanes?.Count ?? 0} lanes",
                    Value2 = $"{data2.AutomationLanes?.Count ?? 0} lanes"
                });
            }

            // Compare effects
            if (!SequenceEqual(data1.Effects, data2.Effects))
            {
                comparison.Differences.Add(new VersionDifference
                {
                    Property = "Effects",
                    Value1 = $"{data1.Effects?.Count ?? 0} effects",
                    Value2 = $"{data2.Effects?.Count ?? 0} effects"
                });
            }
        }

        VersionCompared?.Invoke(this, new VersionCompareEventArgs(comparison));

        return comparison;
    }

    /// <summary>
    /// Updates the data for a version.
    /// </summary>
    public bool UpdateVersionData(string trackId, string versionId, TrackVersionData data)
    {
        var version = GetVersion(trackId, versionId);
        if (version == null)
            return false;

        version.Data = data;
        version.ModifiedAt = DateTime.UtcNow;

        AddHistoryEntry(trackId, versionId, VersionAction.Modified, $"Updated version '{version.Name}'");

        return true;
    }

    /// <summary>
    /// Saves versions to a file.
    /// </summary>
    public async Task SaveToFileAsync(string filePath)
    {
        var saveData = new VersioningPersistenceData
        {
            TrackVersions = _trackVersions,
            ActiveVersions = _activeVersions,
            History = _globalHistory
        };

        var json = JsonSerializer.Serialize(saveData, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads versions from a file.
    /// </summary>
    public async Task LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        var json = await File.ReadAllTextAsync(filePath);
        var loadData = JsonSerializer.Deserialize<VersioningPersistenceData>(json, JsonOptions);

        if (loadData != null)
        {
            _trackVersions.Clear();
            _activeVersions.Clear();
            _globalHistory.Clear();

            foreach (var kvp in loadData.TrackVersions)
            {
                _trackVersions[kvp.Key] = kvp.Value;
            }

            foreach (var kvp in loadData.ActiveVersions)
            {
                _activeVersions[kvp.Key] = kvp.Value;
            }

            _globalHistory.AddRange(loadData.History);
        }
    }

    /// <summary>
    /// Clears all version data.
    /// </summary>
    public void Clear()
    {
        _trackVersions.Clear();
        _activeVersions.Clear();
        _globalHistory.Clear();
    }

    #endregion

    #region Private Methods

    private TrackVersionCollection GetOrCreateCollection(string trackId)
    {
        if (!_trackVersions.TryGetValue(trackId, out var collection))
        {
            collection = new TrackVersionCollection { TrackId = trackId };
            _trackVersions[trackId] = collection;
        }
        return collection;
    }

    private static string GetUniqueVersionName(TrackVersionCollection collection, string baseName, string? excludeId = null)
    {
        var name = baseName;
        var counter = 1;

        while (collection.Versions.Any(v => v.Name == name && v.Id != excludeId))
        {
            name = $"{baseName} ({counter++})";
        }

        return name;
    }

    private void AddHistoryEntry(string trackId, string versionId, VersionAction action, string description)
    {
        _globalHistory.Add(new VersionHistoryEntry
        {
            Id = Guid.NewGuid().ToString(),
            TrackId = trackId,
            VersionId = versionId,
            Action = action,
            Description = description,
            Timestamp = DateTime.UtcNow
        });

        // Keep history manageable
        if (_globalHistory.Count > 1000)
        {
            _globalHistory.RemoveRange(0, 500);
        }
    }

    private static bool SequenceEqual<T>(IList<T>? list1, IList<T>? list2)
    {
        if (list1 == null && list2 == null) return true;
        if (list1 == null || list2 == null) return false;
        if (list1.Count != list2.Count) return false;

        var json1 = JsonSerializer.Serialize(list1, JsonOptions);
        var json2 = JsonSerializer.Serialize(list2, JsonOptions);
        return json1 == json2;
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for track versioning service.
/// </summary>
public interface ITrackVersioningService
{
    event EventHandler<VersionChangedEventArgs>? VersionCreated;
    event EventHandler<VersionChangedEventArgs>? VersionDeleted;
    event EventHandler<VersionChangedEventArgs>? VersionRenamed;
    event EventHandler<VersionSwitchedEventArgs>? VersionSwitched;

    TrackVersion CreateVersion(string trackId, string name, TrackVersionData data);
    TrackVersion DuplicateVersion(string trackId, string sourceVersionId, string newName);
    bool DeleteVersion(string trackId, string versionId);
    bool RenameVersion(string trackId, string versionId, string newName);
    TrackVersion? SwitchVersion(string trackId, string versionId);
    TrackVersion? GetVersion(string trackId, string versionId);
    TrackVersion? GetActiveVersion(string trackId);
    IReadOnlyList<TrackVersion> GetVersions(string trackId);
    IReadOnlyList<VersionHistoryEntry> GetVersionHistory(string trackId);
    VersionComparison CompareVersions(string trackId, string versionId1, string versionId2);
    bool UpdateVersionData(string trackId, string versionId, TrackVersionData data);
    Task SaveToFileAsync(string filePath);
    Task LoadFromFileAsync(string filePath);
    void Clear();
}

#endregion

#region Models

/// <summary>
/// Represents a single version of a track.
/// </summary>
public class TrackVersion : INotifyPropertyChanged
{
    private string _id = string.Empty;
    private string _trackId = string.Empty;
    private string _name = string.Empty;
    private DateTime _createdAt;
    private DateTime _modifiedAt;
    private string _description = string.Empty;
    private TrackVersionData? _data;

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string TrackId { get => _trackId; set { _trackId = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public DateTime CreatedAt { get => _createdAt; set { _createdAt = value; OnPropertyChanged(); } }
    public DateTime ModifiedAt { get => _modifiedAt; set { _modifiedAt = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public TrackVersionData? Data { get => _data; set { _data = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Data stored in a track version.
/// </summary>
public class TrackVersionData
{
    public List<NoteEventData>? NoteEvents { get; set; }
    public List<ClipData>? Clips { get; set; }
    public List<AutomationLaneData>? AutomationLanes { get; set; }
    public List<EffectData>? Effects { get; set; }
    public Dictionary<string, object>? CustomData { get; set; }

    public TrackVersionData Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<TrackVersionData>(json) ?? new TrackVersionData();
    }
}

public class NoteEventData
{
    public int NoteNumber { get; set; }
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
    public int Velocity { get; set; }
}

public class ClipData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double StartBeat { get; set; }
    public double DurationBeats { get; set; }
    public string AudioFilePath { get; set; } = string.Empty;
}

public class AutomationLaneData
{
    public string ParameterId { get; set; } = string.Empty;
    public string ParameterName { get; set; } = string.Empty;
    public List<AutomationPointData>? Points { get; set; }
}

public class AutomationPointData
{
    public double Beat { get; set; }
    public double Value { get; set; }
    public int CurveType { get; set; }
}

public class EffectData
{
    public string Id { get; set; } = string.Empty;
    public string PluginId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsBypassed { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Collection of versions for a single track.
/// </summary>
public class TrackVersionCollection
{
    public string TrackId { get; set; } = string.Empty;
    public List<TrackVersion> Versions { get; set; } = new();
}

/// <summary>
/// History entry for version changes.
/// </summary>
public class VersionHistoryEntry
{
    public string Id { get; set; } = string.Empty;
    public string TrackId { get; set; } = string.Empty;
    public string VersionId { get; set; } = string.Empty;
    public VersionAction Action { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public enum VersionAction
{
    Created,
    Deleted,
    Renamed,
    Modified,
    Switched
}

/// <summary>
/// Result of comparing two versions.
/// </summary>
public class VersionComparison
{
    public string TrackId { get; set; } = string.Empty;
    public TrackVersion? Version1 { get; set; }
    public TrackVersion? Version2 { get; set; }
    public DateTime ComparedAt { get; set; }
    public List<VersionDifference> Differences { get; set; } = new();

    public bool HasDifferences => Differences.Count > 0;
}

public class VersionDifference
{
    public string Property { get; set; } = string.Empty;
    public string Value1 { get; set; } = string.Empty;
    public string Value2 { get; set; } = string.Empty;
}

/// <summary>
/// Data for persistence.
/// </summary>
internal class VersioningPersistenceData
{
    public Dictionary<string, TrackVersionCollection> TrackVersions { get; set; } = new();
    public Dictionary<string, string> ActiveVersions { get; set; } = new();
    public List<VersionHistoryEntry> History { get; set; } = new();
}

#endregion

#region Event Args

public class VersionChangedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackVersion Version { get; }

    public VersionChangedEventArgs(string trackId, TrackVersion version)
    {
        TrackId = trackId;
        Version = version;
    }
}

public class VersionSwitchedEventArgs : EventArgs
{
    public string TrackId { get; }
    public TrackVersion? PreviousVersion { get; }
    public TrackVersion NewVersion { get; }

    public VersionSwitchedEventArgs(string trackId, TrackVersion? previousVersion, TrackVersion newVersion)
    {
        TrackId = trackId;
        PreviousVersion = previousVersion;
        NewVersion = newVersion;
    }
}

public class VersionCompareEventArgs : EventArgs
{
    public VersionComparison Comparison { get; }

    public VersionCompareEventArgs(VersionComparison comparison)
    {
        Comparison = comparison;
    }
}

#endregion
