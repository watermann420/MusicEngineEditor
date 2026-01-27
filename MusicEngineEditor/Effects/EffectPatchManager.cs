// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Manages effect patches (presets) for the modular effect system.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MusicEngineEditor.Effects;

/// <summary>
/// Manages loading, saving, and organizing effect patches.
/// </summary>
public class EffectPatchManager
{
    private static readonly Lazy<EffectPatchManager> _instance = new(() => new EffectPatchManager());
    public static EffectPatchManager Instance => _instance.Value;

    private readonly Dictionary<string, EffectPatch> _patches = new();
    private readonly Dictionary<string, List<string>> _patchesByCategory = new();
    private string _patchDirectory = string.Empty;

    public IReadOnlyDictionary<string, EffectPatch> Patches => _patches;
    public IEnumerable<string> Categories => _patchesByCategory.Keys.OrderBy(c => c);

    public event EventHandler<EffectPatch>? PatchAdded;
    public event EventHandler<string>? PatchRemoved;
    public event EventHandler? PatchesLoaded;

    private EffectPatchManager()
    {
        RegisterFactoryPatches();
    }

    /// <summary>
    /// Sets the directory for user patches.
    /// </summary>
    public void SetPatchDirectory(string directory)
    {
        _patchDirectory = directory;
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    /// <summary>
    /// Loads all patches from the patch directory.
    /// </summary>
    public async Task LoadPatchesAsync()
    {
        if (string.IsNullOrEmpty(_patchDirectory) || !Directory.Exists(_patchDirectory))
            return;

        foreach (var file in Directory.GetFiles(_patchDirectory, "*.mepatch", SearchOption.AllDirectories))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var patch = JsonSerializer.Deserialize<EffectPatch>(json);
                if (patch != null)
                {
                    patch.FilePath = file;
                    AddPatch(patch);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PatchManager] Failed to load {file}: {ex.Message}");
            }
        }

        PatchesLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves a patch to file.
    /// </summary>
    public async Task SavePatchAsync(EffectPatch patch)
    {
        if (string.IsNullOrEmpty(_patchDirectory))
            throw new InvalidOperationException("Patch directory not set");

        var categoryDir = Path.Combine(_patchDirectory, patch.Category);
        if (!Directory.Exists(categoryDir))
            Directory.CreateDirectory(categoryDir);

        var fileName = SanitizeFileName(patch.Name) + ".mepatch";
        var filePath = Path.Combine(categoryDir, fileName);

        patch.FilePath = filePath;
        patch.ModifiedAt = DateTime.UtcNow;

        var json = JsonSerializer.Serialize(patch, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);

        AddPatch(patch);
    }

    /// <summary>
    /// Creates an EffectGraph from a patch.
    /// </summary>
    public EffectGraph? CreateGraphFromPatch(string patchId)
    {
        if (!_patches.TryGetValue(patchId, out var patch))
            return null;

        return CreateGraphFromPatch(patch);
    }

    /// <summary>
    /// Creates an EffectGraph from a patch.
    /// </summary>
    public EffectGraph? CreateGraphFromPatch(EffectPatch patch)
    {
        var graph = new EffectGraph
        {
            Name = patch.Name,
            Description = patch.Description,
            Author = patch.Author
        };

        // Create nodes
        var nodeIdMap = new Dictionary<string, string>();
        foreach (var nodeData in patch.Nodes)
        {
            var node = EffectNodeFactory.CreateNode(nodeData.NodeType);
            if (node == null) continue;

            node.DisplayName = nodeData.DisplayName;
            node.IsEnabled = nodeData.IsEnabled;

            // Restore parameters
            foreach (var paramData in nodeData.Parameters)
            {
                var param = node.Parameters.FirstOrDefault(p => p.Name == paramData.Name);
                if (param != null)
                    param.Value = paramData.Value;
            }

            nodeIdMap[nodeData.Id] = node.Id;
            graph.AddNode(node);
        }

        // Create connections
        foreach (var connData in patch.Connections)
        {
            if (nodeIdMap.TryGetValue(connData.SourceNodeId, out var sourceId) &&
                nodeIdMap.TryGetValue(connData.TargetNodeId, out var targetId))
            {
                graph.Connect(sourceId, connData.SourcePortIndex, targetId, connData.TargetPortIndex, connData.Amount);
            }
        }

        return graph;
    }

    /// <summary>
    /// Creates a patch from an EffectGraph.
    /// </summary>
    public EffectPatch CreatePatchFromGraph(EffectGraph graph, string category)
    {
        var patch = new EffectPatch
        {
            Id = Guid.NewGuid().ToString(),
            Name = graph.Name,
            Description = graph.Description,
            Author = graph.Author,
            Category = category,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Nodes = graph.Nodes.Values.Select(n => new NodeData
            {
                Id = n.Id,
                NodeType = n.NodeType,
                DisplayName = n.DisplayName,
                IsEnabled = n.IsEnabled,
                Parameters = n.Parameters.Select(p => new ParameterData
                {
                    Name = p.Name,
                    Value = p.Value
                }).ToList()
            }).ToList(),
            Connections = graph.Connections.Select(c => new ConnectionData
            {
                Id = c.Id,
                SourceNodeId = c.SourceNodeId,
                SourcePortIndex = c.SourcePortIndex,
                TargetNodeId = c.TargetNodeId,
                TargetPortIndex = c.TargetPortIndex,
                Amount = c.Amount
            }).ToList()
        };

        return patch;
    }

    /// <summary>
    /// Gets patches by category.
    /// </summary>
    public IEnumerable<EffectPatch> GetPatchesByCategory(string category)
    {
        if (_patchesByCategory.TryGetValue(category, out var patchIds))
            return patchIds.Select(id => _patches[id]).Where(p => p != null);
        return Enumerable.Empty<EffectPatch>();
    }

    /// <summary>
    /// Searches patches by name or tags.
    /// </summary>
    public IEnumerable<EffectPatch> SearchPatches(string query)
    {
        query = query.ToLowerInvariant();
        return _patches.Values.Where(p =>
            p.Name.ToLowerInvariant().Contains(query) ||
            p.Description.ToLowerInvariant().Contains(query) ||
            p.Tags.Any(t => t.ToLowerInvariant().Contains(query)));
    }

    /// <summary>
    /// Deletes a patch.
    /// </summary>
    public void DeletePatch(string patchId)
    {
        if (!_patches.TryGetValue(patchId, out var patch))
            return;

        if (!string.IsNullOrEmpty(patch.FilePath) && File.Exists(patch.FilePath))
            File.Delete(patch.FilePath);

        _patches.Remove(patchId);
        if (_patchesByCategory.TryGetValue(patch.Category, out var list))
            list.Remove(patchId);

        PatchRemoved?.Invoke(this, patchId);
    }

    private void AddPatch(EffectPatch patch)
    {
        _patches[patch.Id] = patch;

        if (!_patchesByCategory.ContainsKey(patch.Category))
            _patchesByCategory[patch.Category] = new List<string>();

        if (!_patchesByCategory[patch.Category].Contains(patch.Id))
            _patchesByCategory[patch.Category].Add(patch.Id);

        PatchAdded?.Invoke(this, patch);
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
    }

    private void RegisterFactoryPatches()
    {
        // Basic Patches
        AddFactoryPatch("Clean Amp", "Basics", "Simple clean amplifier with gain control",
            new[] { "AudioInput", "Gain", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0) });

        AddFactoryPatch("Simple Delay", "Basics", "Basic delay effect",
            new[] { "AudioInput", "Delay", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0) });

        AddFactoryPatch("Basic Reverb", "Basics", "Simple reverb effect",
            new[] { "AudioInput", "Reverb", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0) });

        // Synth Patches
        AddFactoryPatch("Basic Synth", "Synths", "Simple subtractive synth voice",
            new[] { "Oscillator", "Filter", "Envelope", "VCA", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 3, 0), (2, 0, 3, 1), (3, 0, 4, 0) });

        AddFactoryPatch("Dual Osc Synth", "Synths", "Two oscillator synth with filter",
            new[] { "Oscillator", "Oscillator", "Mixer", "Filter", "Envelope", "VCA", "AudioOutput" },
            new[] { (0, 0, 2, 0), (1, 0, 2, 1), (2, 0, 3, 0), (3, 0, 5, 0), (4, 0, 5, 1), (5, 0, 6, 0) });

        AddFactoryPatch("FM Synth", "Synths", "Simple FM synthesis patch",
            new[] { "Oscillator", "Oscillator", "VCA", "Filter", "AudioOutput" },
            new[] { (0, 0, 2, 1), (1, 0, 2, 0), (2, 0, 3, 0), (3, 0, 4, 0) });

        // Effects Chains
        AddFactoryPatch("Guitar Amp", "Guitar", "Distortion with cab sim",
            new[] { "AudioInput", "EQ3Band", "Distortion", "EQ3Band", "Reverb", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (2, 0, 3, 0), (3, 0, 4, 0), (4, 0, 5, 0) });

        AddFactoryPatch("Vocal Chain", "Vocals", "Compression and reverb for vocals",
            new[] { "AudioInput", "EQ3Band", "Compressor", "Delay", "Reverb", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (2, 0, 3, 0), (3, 0, 4, 0), (4, 0, 5, 0) });

        AddFactoryPatch("Drum Bus", "Drums", "Parallel compression for drums",
            new[] { "AudioInput", "Split", "Compressor", "Mixer", "Limiter", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (1, 1, 3, 1), (2, 0, 3, 0), (3, 0, 4, 0), (4, 0, 5, 0) });

        // Modular/Experimental
        AddFactoryPatch("Auto-Wah", "Modulation", "Envelope-controlled filter",
            new[] { "AudioInput", "Follower", "Filter", "AudioOutput" },
            new[] { (0, 0, 1, 0), (0, 0, 2, 0), (1, 0, 2, 1), (2, 0, 3, 0) });

        AddFactoryPatch("Tremolo", "Modulation", "LFO-controlled amplitude",
            new[] { "AudioInput", "LFO", "VCA", "AudioOutput" },
            new[] { (0, 0, 2, 0), (1, 0, 2, 1), (2, 0, 3, 0) });

        AddFactoryPatch("Ring Mod Madness", "Experimental", "Ring modulation effect",
            new[] { "AudioInput", "Oscillator", "RingMod", "Filter", "AudioOutput" },
            new[] { (0, 0, 2, 0), (1, 0, 2, 1), (2, 0, 3, 0), (3, 0, 4, 0) });

        AddFactoryPatch("Bitcrusher Lo-Fi", "Experimental", "Bitcrushing with filtering",
            new[] { "AudioInput", "Filter", "Bitcrusher", "EQ3Band", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (2, 0, 3, 0), (3, 0, 4, 0) });

        AddFactoryPatch("Generative", "Experimental", "Self-generating patch",
            new[] { "Clock", "StepSequencer", "Oscillator", "Filter", "Envelope", "VCA", "Reverb", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (2, 0, 3, 0), (0, 0, 4, 1), (4, 0, 5, 1), (3, 0, 5, 0), (5, 0, 6, 0), (6, 0, 7, 0) });

        // Mastering
        AddFactoryPatch("Master Bus", "Mastering", "Basic mastering chain",
            new[] { "AudioInput", "EQ3Band", "Compressor", "Limiter", "AudioOutput" },
            new[] { (0, 0, 1, 0), (1, 0, 2, 0), (2, 0, 3, 0), (3, 0, 4, 0) });
    }

    private void AddFactoryPatch(string name, string category, string description,
        string[] nodeTypes, (int src, int srcPort, int dst, int dstPort)[] connections)
    {
        var patch = new EffectPatch
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Category = category,
            Description = description,
            Author = "MusicEngine",
            IsFactory = true,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        // Create node data
        var nodeIds = new string[nodeTypes.Length];
        for (int i = 0; i < nodeTypes.Length; i++)
        {
            nodeIds[i] = Guid.NewGuid().ToString();
            patch.Nodes.Add(new NodeData
            {
                Id = nodeIds[i],
                NodeType = nodeTypes[i],
                DisplayName = nodeTypes[i],
                IsEnabled = true
            });
        }

        // Create connection data
        foreach (var (src, srcPort, dst, dstPort) in connections)
        {
            patch.Connections.Add(new ConnectionData
            {
                Id = Guid.NewGuid().ToString(),
                SourceNodeId = nodeIds[src],
                SourcePortIndex = srcPort,
                TargetNodeId = nodeIds[dst],
                TargetPortIndex = dstPort,
                Amount = 1f
            });
        }

        AddPatch(patch);
    }
}

/// <summary>
/// Represents a saved effect patch.
/// </summary>
public class EffectPatch
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Patch";
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = "User";
    public List<string> Tags { get; set; } = new();
    public bool IsFactory { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    public string FilePath { get; set; } = string.Empty;

    public List<NodeData> Nodes { get; set; } = new();
    public List<ConnectionData> Connections { get; set; } = new();
}
