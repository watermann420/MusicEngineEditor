// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Modular effect system inspired by VCV Rack - allows thousands of effect combinations.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Effects;

#region Core Interfaces

/// <summary>
/// Base interface for all modular effect nodes.
/// Each node can have multiple inputs, outputs, and parameters.
/// </summary>
public interface IEffectNode : IDisposable
{
    /// <summary>Unique identifier for this node instance.</summary>
    string Id { get; }

    /// <summary>Node type identifier (e.g., "Oscillator", "Filter", "Gain").</summary>
    string NodeType { get; }

    /// <summary>Display name for the node.</summary>
    string DisplayName { get; set; }

    /// <summary>Category for organization (e.g., "Generators", "Filters", "Modulators").</summary>
    string Category { get; }

    /// <summary>Whether this node is currently enabled.</summary>
    bool IsEnabled { get; set; }

    /// <summary>Input ports for this node.</summary>
    IReadOnlyList<NodePort> Inputs { get; }

    /// <summary>Output ports for this node.</summary>
    IReadOnlyList<NodePort> Outputs { get; }

    /// <summary>Parameters that can be adjusted.</summary>
    IReadOnlyList<NodeParameter> Parameters { get; }

    /// <summary>Process audio samples through this node.</summary>
    void Process(float[] buffer, int sampleCount, int sampleRate);

    /// <summary>Reset the node state.</summary>
    void Reset();

    /// <summary>Get the current output value for a specific port.</summary>
    float GetOutputValue(int portIndex);

    /// <summary>Set an input value from a connected node.</summary>
    void SetInputValue(int portIndex, float value);
}

/// <summary>
/// Represents a connection port on a node.
/// </summary>
public class NodePort
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PortType Type { get; set; }
    public PortDataType DataType { get; set; }
    public int Index { get; set; }
    public float DefaultValue { get; set; }
    public float CurrentValue { get; set; }
    public bool IsConnected { get; set; }
}

/// <summary>
/// Represents a parameter on a node that can be adjusted.
/// </summary>
public partial class NodeParameter : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private float _value;
    [ObservableProperty] private float _minimum;
    [ObservableProperty] private float _maximum;
    [ObservableProperty] private float _defaultValue;
    [ObservableProperty] private string _unit = string.Empty;
    [ObservableProperty] private ParameterScale _scale = ParameterScale.Linear;
    [ObservableProperty] private bool _isModulatable = true;
    [ObservableProperty] private float _modulationAmount;

    /// <summary>Normalized value (0.0 - 1.0)</summary>
    public float NormalizedValue
    {
        get => (Maximum - Minimum) > 0 ? (Value - Minimum) / (Maximum - Minimum) : 0;
        set => Value = Minimum + value * (Maximum - Minimum);
    }

    public string DisplayValue => $"{Value:F2} {Unit}".Trim();
}

public enum PortType { Input, Output }
public enum PortDataType { Audio, Control, Trigger, Gate }
public enum ParameterScale { Linear, Logarithmic, Exponential, Toggle }

#endregion

#region Node Connection

/// <summary>
/// Represents a connection between two nodes.
/// </summary>
public class NodeConnection
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string SourceNodeId { get; set; } = string.Empty;
    public int SourcePortIndex { get; set; }
    public string TargetNodeId { get; set; } = string.Empty;
    public int TargetPortIndex { get; set; }
    public float Amount { get; set; } = 1.0f; // Connection strength/attenuation
}

#endregion

#region Effect Graph

/// <summary>
/// A graph of connected effect nodes that can be processed together.
/// This is like a "patch" in modular synthesis.
/// </summary>
public class EffectGraph : IDisposable
{
    private readonly Dictionary<string, IEffectNode> _nodes = new();
    private readonly List<NodeConnection> _connections = new();
    private readonly List<string> _processingOrder = new();
    private readonly object _lock = new();
    private bool _needsReorder = true;

    public string Id { get; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "New Patch";
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public IReadOnlyDictionary<string, IEffectNode> Nodes => _nodes;
    public IReadOnlyList<NodeConnection> Connections => _connections;

    public event EventHandler<IEffectNode>? NodeAdded;
    public event EventHandler<string>? NodeRemoved;
    public event EventHandler<NodeConnection>? ConnectionAdded;
    public event EventHandler<string>? ConnectionRemoved;
    public event EventHandler? GraphChanged;

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    public void AddNode(IEffectNode node)
    {
        lock (_lock)
        {
            _nodes[node.Id] = node;
            _needsReorder = true;
        }
        NodeAdded?.Invoke(this, node);
        GraphChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes a node and all its connections.
    /// </summary>
    public bool RemoveNode(string nodeId)
    {
        lock (_lock)
        {
            if (!_nodes.TryGetValue(nodeId, out var node))
                return false;

            // Remove all connections to/from this node
            _connections.RemoveAll(c => c.SourceNodeId == nodeId || c.TargetNodeId == nodeId);

            _nodes.Remove(nodeId);
            node.Dispose();
            _needsReorder = true;
        }
        NodeRemoved?.Invoke(this, nodeId);
        GraphChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Connects two nodes.
    /// </summary>
    public NodeConnection? Connect(string sourceNodeId, int sourcePort, string targetNodeId, int targetPort, float amount = 1.0f)
    {
        lock (_lock)
        {
            if (!_nodes.ContainsKey(sourceNodeId) || !_nodes.ContainsKey(targetNodeId))
                return null;

            // Check for existing connection
            var existing = _connections.FirstOrDefault(c =>
                c.TargetNodeId == targetNodeId && c.TargetPortIndex == targetPort);

            if (existing != null)
            {
                // Replace existing connection
                _connections.Remove(existing);
            }

            var connection = new NodeConnection
            {
                SourceNodeId = sourceNodeId,
                SourcePortIndex = sourcePort,
                TargetNodeId = targetNodeId,
                TargetPortIndex = targetPort,
                Amount = amount
            };

            _connections.Add(connection);
            _needsReorder = true;

            // Mark ports as connected
            var sourceNode = _nodes[sourceNodeId];
            var targetNode = _nodes[targetNodeId];
            if (sourcePort < sourceNode.Outputs.Count)
                sourceNode.Outputs[sourcePort].IsConnected = true;
            if (targetPort < targetNode.Inputs.Count)
                targetNode.Inputs[targetPort].IsConnected = true;

            ConnectionAdded?.Invoke(this, connection);
            GraphChanged?.Invoke(this, EventArgs.Empty);
            return connection;
        }
    }

    /// <summary>
    /// Disconnects a connection.
    /// </summary>
    public bool Disconnect(string connectionId)
    {
        lock (_lock)
        {
            var connection = _connections.FirstOrDefault(c => c.Id == connectionId);
            if (connection == null) return false;

            _connections.Remove(connection);
            _needsReorder = true;

            // Update port connection status
            if (_nodes.TryGetValue(connection.TargetNodeId, out var targetNode))
            {
                var port = targetNode.Inputs.ElementAtOrDefault(connection.TargetPortIndex);
                if (port != null)
                    port.IsConnected = _connections.Any(c =>
                        c.TargetNodeId == connection.TargetNodeId &&
                        c.TargetPortIndex == connection.TargetPortIndex);
            }

            ConnectionRemoved?.Invoke(this, connectionId);
            GraphChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
    }

    /// <summary>
    /// Processes audio through all nodes in the correct order.
    /// </summary>
    public void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        lock (_lock)
        {
            if (_needsReorder)
            {
                ReorderNodes();
                _needsReorder = false;
            }

            // Process nodes in topological order
            foreach (var nodeId in _processingOrder)
            {
                if (!_nodes.TryGetValue(nodeId, out var node)) continue;
                if (!node.IsEnabled) continue;

                // Apply input connections
                foreach (var connection in _connections.Where(c => c.TargetNodeId == nodeId))
                {
                    if (_nodes.TryGetValue(connection.SourceNodeId, out var sourceNode))
                    {
                        var value = sourceNode.GetOutputValue(connection.SourcePortIndex);
                        node.SetInputValue(connection.TargetPortIndex, value * connection.Amount);
                    }
                }

                // Process the node
                node.Process(buffer, sampleCount, sampleRate);
            }
        }
    }

    /// <summary>
    /// Topologically sorts nodes for correct processing order.
    /// </summary>
    private void ReorderNodes()
    {
        _processingOrder.Clear();
        var visited = new HashSet<string>();
        var visiting = new HashSet<string>();

        foreach (var nodeId in _nodes.Keys)
        {
            if (!visited.Contains(nodeId))
                TopologicalSort(nodeId, visited, visiting);
        }

        _processingOrder.Reverse();
    }

    private void TopologicalSort(string nodeId, HashSet<string> visited, HashSet<string> visiting)
    {
        if (visiting.Contains(nodeId))
            return; // Cycle detected, skip

        if (visited.Contains(nodeId))
            return;

        visiting.Add(nodeId);

        // Visit all nodes that this node depends on
        foreach (var connection in _connections.Where(c => c.TargetNodeId == nodeId))
        {
            TopologicalSort(connection.SourceNodeId, visited, visiting);
        }

        visiting.Remove(nodeId);
        visited.Add(nodeId);
        _processingOrder.Add(nodeId);
    }

    /// <summary>
    /// Resets all nodes.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            foreach (var node in _nodes.Values)
                node.Reset();
        }
    }

    /// <summary>
    /// Serializes the graph to JSON.
    /// </summary>
    public string Serialize()
    {
        var data = new EffectGraphData
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Author = Author,
            CreatedAt = CreatedAt,
            Nodes = _nodes.Values.Select(n => new NodeData
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
            Connections = _connections.Select(c => new ConnectionData
            {
                Id = c.Id,
                SourceNodeId = c.SourceNodeId,
                SourcePortIndex = c.SourcePortIndex,
                TargetNodeId = c.TargetNodeId,
                TargetPortIndex = c.TargetPortIndex,
                Amount = c.Amount
            }).ToList()
        };

        return JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var node in _nodes.Values)
                node.Dispose();
            _nodes.Clear();
            _connections.Clear();
        }
    }
}

#region Serialization DTOs

public class EffectGraphData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<NodeData> Nodes { get; set; } = new();
    public List<ConnectionData> Connections { get; set; } = new();
}

public class NodeData
{
    public string Id { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public List<ParameterData> Parameters { get; set; } = new();
}

public class ParameterData
{
    public string Name { get; set; } = string.Empty;
    public float Value { get; set; }
}

public class ConnectionData
{
    public string Id { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public int SourcePortIndex { get; set; }
    public string TargetNodeId { get; set; } = string.Empty;
    public int TargetPortIndex { get; set; }
    public float Amount { get; set; }
}

#endregion

#endregion

#region Node Factory

/// <summary>
/// Factory for creating effect nodes. Supports registration of custom nodes.
/// </summary>
public static class EffectNodeFactory
{
    private static readonly ConcurrentDictionary<string, Func<IEffectNode>> _nodeTypes = new();
    private static readonly ConcurrentDictionary<string, NodeTypeInfo> _nodeTypeInfos = new();

    static EffectNodeFactory()
    {
        // Register built-in nodes
        RegisterBuiltInNodes();
    }

    /// <summary>
    /// Registers a node type.
    /// </summary>
    public static void RegisterNodeType(string nodeType, string displayName, string category,
        string description, Func<IEffectNode> factory)
    {
        _nodeTypes[nodeType] = factory;
        _nodeTypeInfos[nodeType] = new NodeTypeInfo
        {
            NodeType = nodeType,
            DisplayName = displayName,
            Category = category,
            Description = description
        };
    }

    /// <summary>
    /// Creates a node instance by type.
    /// </summary>
    public static IEffectNode? CreateNode(string nodeType)
    {
        return _nodeTypes.TryGetValue(nodeType, out var factory) ? factory() : null;
    }

    /// <summary>
    /// Gets all registered node types.
    /// </summary>
    public static IEnumerable<NodeTypeInfo> GetAllNodeTypes() => _nodeTypeInfos.Values;

    /// <summary>
    /// Gets node types by category.
    /// </summary>
    public static IEnumerable<NodeTypeInfo> GetNodeTypesByCategory(string category) =>
        _nodeTypeInfos.Values.Where(n => n.Category == category);

    /// <summary>
    /// Gets all categories.
    /// </summary>
    public static IEnumerable<string> GetCategories() =>
        _nodeTypeInfos.Values.Select(n => n.Category).Distinct().OrderBy(c => c);

    private static void RegisterBuiltInNodes()
    {
        // === GENERATORS ===
        RegisterNodeType("Oscillator", "Oscillator", "Generators",
            "Generates basic waveforms (Sine, Saw, Square, Triangle)",
            () => new OscillatorNode());

        RegisterNodeType("NoiseGenerator", "Noise Generator", "Generators",
            "Generates white, pink, or brown noise",
            () => new NoiseGeneratorNode());

        RegisterNodeType("LFO", "LFO", "Generators",
            "Low frequency oscillator for modulation",
            () => new LfoNode());

        // === FILTERS ===
        RegisterNodeType("Filter", "Filter", "Filters",
            "Multi-mode filter (LP, HP, BP, Notch)",
            () => new FilterNode());

        RegisterNodeType("EQ3Band", "3-Band EQ", "Filters",
            "Simple 3-band equalizer",
            () => new Eq3BandNode());

        RegisterNodeType("FormantFilter", "Formant Filter", "Filters",
            "Vowel formant filter",
            () => new FormantFilterNode());

        // === DYNAMICS ===
        RegisterNodeType("Gain", "Gain", "Dynamics",
            "Simple volume control",
            () => new GainNode());

        RegisterNodeType("Compressor", "Compressor", "Dynamics",
            "Dynamic range compressor",
            () => new CompressorNode());

        RegisterNodeType("Limiter", "Limiter", "Dynamics",
            "Brickwall limiter",
            () => new LimiterNode());

        RegisterNodeType("Gate", "Gate", "Dynamics",
            "Noise gate",
            () => new GateNode());

        // === EFFECTS ===
        RegisterNodeType("Delay", "Delay", "Effects",
            "Simple delay effect",
            () => new DelayNode());

        RegisterNodeType("Reverb", "Reverb", "Effects",
            "Reverb effect",
            () => new ReverbNode());

        RegisterNodeType("Chorus", "Chorus", "Effects",
            "Chorus effect",
            () => new ChorusNode());

        RegisterNodeType("Flanger", "Flanger", "Effects",
            "Flanger effect",
            () => new FlangerNode());

        RegisterNodeType("Phaser", "Phaser", "Effects",
            "Phaser effect",
            () => new PhaserNode());

        RegisterNodeType("Bitcrusher", "Bitcrusher", "Effects",
            "Bit reduction and sample rate reduction",
            () => new BitcrusherNode());

        RegisterNodeType("Distortion", "Distortion", "Effects",
            "Waveshaping distortion",
            () => new DistortionNode());

        RegisterNodeType("RingMod", "Ring Modulator", "Effects",
            "Ring modulation effect",
            () => new RingModNode());

        // === MODULATORS ===
        RegisterNodeType("Envelope", "Envelope", "Modulators",
            "ADSR envelope generator",
            () => new EnvelopeNode());

        RegisterNodeType("SampleAndHold", "Sample & Hold", "Modulators",
            "Sample and hold circuit",
            () => new SampleAndHoldNode());

        RegisterNodeType("Slew", "Slew Limiter", "Modulators",
            "Limits rate of change (portamento)",
            () => new SlewNode());

        RegisterNodeType("Quantizer", "Quantizer", "Modulators",
            "Quantizes values to musical scales",
            () => new QuantizerNode());

        // === UTILITIES ===
        RegisterNodeType("Mixer", "Mixer", "Utilities",
            "Mix multiple inputs",
            () => new MixerNode());

        RegisterNodeType("VCA", "VCA", "Utilities",
            "Voltage controlled amplifier",
            () => new VcaNode());

        RegisterNodeType("Split", "Splitter", "Utilities",
            "Split one signal to multiple outputs",
            () => new SplitNode());

        RegisterNodeType("Merge", "Merger", "Utilities",
            "Merge multiple signals",
            () => new MergeNode());

        RegisterNodeType("Inverter", "Inverter", "Utilities",
            "Inverts signal polarity",
            () => new InverterNode());

        RegisterNodeType("Offset", "Offset", "Utilities",
            "Adds DC offset to signal",
            () => new OffsetNode());

        RegisterNodeType("Rectifier", "Rectifier", "Utilities",
            "Full or half wave rectification",
            () => new RectifierNode());

        RegisterNodeType("Crossfade", "Crossfade", "Utilities",
            "Crossfade between two inputs",
            () => new CrossfadeNode());

        // === ANALYZERS ===
        RegisterNodeType("Follower", "Envelope Follower", "Analyzers",
            "Tracks amplitude of input signal",
            () => new EnvelopeFollowerNode());

        RegisterNodeType("PitchDetect", "Pitch Detector", "Analyzers",
            "Detects pitch of input signal",
            () => new PitchDetectorNode());

        // === SEQUENCING ===
        RegisterNodeType("StepSequencer", "Step Sequencer", "Sequencing",
            "8-step CV sequencer",
            () => new StepSequencerNode());

        RegisterNodeType("Clock", "Clock", "Sequencing",
            "Master clock generator",
            () => new ClockNode());

        RegisterNodeType("ClockDiv", "Clock Divider", "Sequencing",
            "Divides clock signal",
            () => new ClockDividerNode());

        // === I/O ===
        RegisterNodeType("AudioInput", "Audio Input", "I/O",
            "Receives audio from track",
            () => new AudioInputNode());

        RegisterNodeType("AudioOutput", "Audio Output", "I/O",
            "Sends audio to output",
            () => new AudioOutputNode());

        RegisterNodeType("MidiInput", "MIDI Input", "I/O",
            "Receives MIDI data",
            () => new MidiInputNode());

        RegisterNodeType("MidiOutput", "MIDI Output", "I/O",
            "Sends MIDI data",
            () => new MidiOutputNode());
    }
}

/// <summary>
/// Information about a registered node type.
/// </summary>
public class NodeTypeInfo
{
    public string NodeType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

#endregion

#region Base Node Implementation

/// <summary>
/// Base class for effect nodes with common functionality.
/// </summary>
public abstract class EffectNodeBase : IEffectNode
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public abstract string NodeType { get; }
    public string DisplayName { get; set; }
    public abstract string Category { get; }
    public virtual string Description => string.Empty;
    public bool IsEnabled { get; set; } = true;

    protected readonly List<NodePort> _inputs = new();
    protected readonly List<NodePort> _outputs = new();
    protected readonly List<NodeParameter> _parameters = new();

    public IReadOnlyList<NodePort> Inputs => _inputs;
    public IReadOnlyList<NodePort> Outputs => _outputs;
    public IReadOnlyList<NodeParameter> Parameters => _parameters;

    protected EffectNodeBase()
    {
        DisplayName = NodeType;
        InitializePorts();
        InitializeParameters();
    }

    protected abstract void InitializePorts();
    protected abstract void InitializeParameters();

    public abstract void Process(float[] buffer, int sampleCount, int sampleRate);

    public virtual float GetOutputValue(int portIndex) =>
        portIndex < _outputs.Count ? _outputs[portIndex].CurrentValue : 0f;

    public virtual void SetInputValue(int portIndex, float value)
    {
        if (portIndex < _inputs.Count)
            _inputs[portIndex].CurrentValue = value;
    }

    public virtual void Reset()
    {
        foreach (var port in _inputs)
            port.CurrentValue = port.DefaultValue;
        foreach (var port in _outputs)
            port.CurrentValue = 0f;
    }

    protected NodePort AddInput(string name, PortDataType dataType = PortDataType.Audio, float defaultValue = 0f)
    {
        var port = new NodePort
        {
            Name = name,
            Type = PortType.Input,
            DataType = dataType,
            Index = _inputs.Count,
            DefaultValue = defaultValue,
            CurrentValue = defaultValue
        };
        _inputs.Add(port);
        return port;
    }

    protected NodePort AddOutput(string name, PortDataType dataType = PortDataType.Audio)
    {
        var port = new NodePort
        {
            Name = name,
            Type = PortType.Output,
            DataType = dataType,
            Index = _outputs.Count
        };
        _outputs.Add(port);
        return port;
    }

    protected NodeParameter AddParameter(string name, float defaultValue, float min, float max,
        string unit = "", ParameterScale scale = ParameterScale.Linear)
    {
        var param = new NodeParameter
        {
            Name = name,
            Value = defaultValue,
            DefaultValue = defaultValue,
            Minimum = min,
            Maximum = max,
            Unit = unit,
            Scale = scale
        };
        _parameters.Add(param);
        return param;
    }

    protected float GetParam(string name) =>
        _parameters.FirstOrDefault(p => p.Name == name)?.Value ?? 0f;

    protected float GetInput(int index) =>
        index < _inputs.Count ? _inputs[index].CurrentValue : 0f;

    protected void SetOutput(int index, float value)
    {
        if (index < _outputs.Count)
            _outputs[index].CurrentValue = value;
    }

    public virtual void Dispose() { }
}

#endregion
