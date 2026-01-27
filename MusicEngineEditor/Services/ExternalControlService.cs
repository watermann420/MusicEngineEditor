// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: External control service for game engine and external application integration.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for external application integration (Game Engines, custom applications).
/// Provides a simple API for controlling MusicEngine from external sources via:
/// - Variable binding (get/set parameters like BPM, volume, effects)
/// - Event triggers (play, stop, trigger patterns, fire custom events)
/// - State queries (current playback position, active patterns, etc.)
/// </summary>
public sealed class ExternalControlService : IDisposable
{
    #region Singleton

    private static readonly Lazy<ExternalControlService> _instance = new(
        () => new ExternalControlService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the ExternalControlService.
    /// </summary>
    public static ExternalControlService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly ConcurrentDictionary<string, ExternalVariable> _variables = new();
    private readonly ConcurrentDictionary<string, Action<object?>> _eventHandlers = new();
    private readonly ConcurrentDictionary<string, List<Action<ExternalVariable>>> _variableCallbacks = new();
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isEnabled;

    #endregion

    #region Events

    /// <summary>
    /// Raised when an external event is triggered.
    /// </summary>
    public event EventHandler<ExternalEventArgs>? ExternalEventTriggered;

    /// <summary>
    /// Raised when a variable value changes.
    /// </summary>
    public event EventHandler<VariableChangedEventArgs>? VariableChanged;

    /// <summary>
    /// Raised when the service is enabled or disabled.
    /// </summary>
    public event EventHandler<bool>? EnabledChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether external control is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                EnabledChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets all registered variable names.
    /// </summary>
    public IEnumerable<string> VariableNames => _variables.Keys;

    /// <summary>
    /// Gets all registered event names.
    /// </summary>
    public IEnumerable<string> EventNames => _eventHandlers.Keys;

    #endregion

    #region Constructor

    private ExternalControlService()
    {
        RegisterDefaultVariables();
        RegisterDefaultEvents();
    }

    #endregion

    #region Variable Management

    /// <summary>
    /// Registers a new external variable that can be controlled from outside.
    /// </summary>
    /// <param name="name">Unique variable name (e.g., "MasterVolume", "BPM")</param>
    /// <param name="defaultValue">Default value</param>
    /// <param name="minValue">Minimum allowed value (optional)</param>
    /// <param name="maxValue">Maximum allowed value (optional)</param>
    /// <param name="description">Human-readable description</param>
    public void RegisterVariable(string name, object defaultValue, object? minValue = null,
        object? maxValue = null, string? description = null)
    {
        ThrowIfDisposed();

        var variable = new ExternalVariable
        {
            Name = name,
            Value = defaultValue,
            DefaultValue = defaultValue,
            MinValue = minValue,
            MaxValue = maxValue,
            Description = description ?? name,
            ValueType = defaultValue?.GetType() ?? typeof(object)
        };

        _variables[name] = variable;
    }

    /// <summary>
    /// Sets a variable value from an external source.
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <param name="value">New value</param>
    /// <returns>True if the variable was set successfully</returns>
    public bool SetVariable(string name, object? value)
    {
        ThrowIfDisposed();

        if (!_isEnabled) return false;
        if (!_variables.TryGetValue(name, out var variable)) return false;

        var oldValue = variable.Value;

        // Apply min/max constraints for numeric types
        if (value != null && variable.MinValue != null && variable.MaxValue != null)
        {
            value = ClampValue(value, variable.MinValue, variable.MaxValue);
        }

        variable.Value = value;
        variable.LastModified = DateTime.UtcNow;

        // Notify listeners
        VariableChanged?.Invoke(this, new VariableChangedEventArgs(name, oldValue, value));

        // Invoke registered callbacks
        if (_variableCallbacks.TryGetValue(name, out var callbacks))
        {
            foreach (var callback in callbacks)
            {
                try
                {
                    callback(variable);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ExternalControl] Callback error for {name}: {ex.Message}");
                }
            }
        }

        // Apply to engine
        ApplyVariableToEngine(name, value);

        return true;
    }

    /// <summary>
    /// Gets a variable value.
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <returns>The variable value, or null if not found</returns>
    public object? GetVariable(string name)
    {
        ThrowIfDisposed();

        return _variables.TryGetValue(name, out var variable) ? variable.Value : null;
    }

    /// <summary>
    /// Gets a variable value with type conversion.
    /// </summary>
    /// <typeparam name="T">Expected type</typeparam>
    /// <param name="name">Variable name</param>
    /// <param name="defaultValue">Default value if not found or conversion fails</param>
    /// <returns>The variable value</returns>
    public T GetVariable<T>(string name, T defaultValue = default!)
    {
        var value = GetVariable(name);
        if (value == null) return defaultValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Gets full variable information.
    /// </summary>
    /// <param name="name">Variable name</param>
    /// <returns>Variable info, or null if not found</returns>
    public ExternalVariable? GetVariableInfo(string name)
    {
        return _variables.TryGetValue(name, out var variable) ? variable : null;
    }

    /// <summary>
    /// Registers a callback to be invoked when a variable changes.
    /// </summary>
    /// <param name="variableName">Variable name to watch</param>
    /// <param name="callback">Callback action</param>
    public void OnVariableChanged(string variableName, Action<ExternalVariable> callback)
    {
        ThrowIfDisposed();

        if (!_variableCallbacks.ContainsKey(variableName))
        {
            _variableCallbacks[variableName] = new List<Action<ExternalVariable>>();
        }

        _variableCallbacks[variableName].Add(callback);
    }

    #endregion

    #region Event Triggers

    /// <summary>
    /// Registers an event that can be triggered from outside.
    /// </summary>
    /// <param name="eventName">Unique event name (e.g., "PlayPattern", "StopAll")</param>
    /// <param name="handler">Handler to execute when the event is triggered</param>
    public void RegisterEvent(string eventName, Action<object?> handler)
    {
        ThrowIfDisposed();

        _eventHandlers[eventName] = handler;
    }

    /// <summary>
    /// Triggers an event from an external source.
    /// </summary>
    /// <param name="eventName">Event name</param>
    /// <param name="parameter">Optional event parameter</param>
    /// <returns>True if the event was triggered successfully</returns>
    public bool TriggerEvent(string eventName, object? parameter = null)
    {
        ThrowIfDisposed();

        if (!_isEnabled) return false;
        if (!_eventHandlers.TryGetValue(eventName, out var handler)) return false;

        try
        {
            handler(parameter);
            ExternalEventTriggered?.Invoke(this, new ExternalEventArgs(eventName, parameter));
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExternalControl] Event error {eventName}: {ex.Message}");
            return false;
        }
    }

    #endregion

    #region State Queries

    /// <summary>
    /// Gets the current engine state for external consumers.
    /// </summary>
    /// <returns>Dictionary of state values</returns>
    public Dictionary<string, object?> GetEngineState()
    {
        ThrowIfDisposed();

        var playbackService = PlaybackService.Instance;
        var audioEngine = AudioEngineService.Instance;

        return new Dictionary<string, object?>
        {
            ["IsPlaying"] = playbackService.IsPlaying,
            ["CurrentBeat"] = playbackService.CurrentBeat,
            ["CurrentTime"] = playbackService.CurrentTime,
            ["BPM"] = playbackService.BPM,
            ["MasterVolume"] = audioEngine.MasterVolume,
            ["SampleRate"] = audioEngine.SampleRate,
            ["BufferSize"] = audioEngine.BufferSize,
            ["IsInitialized"] = audioEngine.IsInitialized
        };
    }

    /// <summary>
    /// Gets all variables as a dictionary (for serialization/network transfer).
    /// </summary>
    /// <returns>Dictionary of variable name to value</returns>
    public Dictionary<string, object?> GetAllVariables()
    {
        var result = new Dictionary<string, object?>();
        foreach (var kvp in _variables)
        {
            result[kvp.Key] = kvp.Value.Value;
        }
        return result;
    }

    /// <summary>
    /// Sets multiple variables at once (batch update).
    /// </summary>
    /// <param name="variables">Dictionary of variable name to value</param>
    public void SetVariables(Dictionary<string, object?> variables)
    {
        foreach (var kvp in variables)
        {
            SetVariable(kvp.Key, kvp.Value);
        }
    }

    #endregion

    #region Default Registrations

    private void RegisterDefaultVariables()
    {
        // Playback variables
        RegisterVariable("BPM", 120.0, 20.0, 999.0, "Beats per minute");
        RegisterVariable("MasterVolume", 1.0f, 0.0f, 1.0f, "Master output volume (0-1)");
        RegisterVariable("PlaybackPosition", 0.0, 0.0, null, "Current playback position in beats");
        RegisterVariable("LoopStart", 0.0, 0.0, null, "Loop start position in beats");
        RegisterVariable("LoopEnd", 16.0, 0.0, null, "Loop end position in beats");
        RegisterVariable("LoopEnabled", false, null, null, "Whether looping is enabled");

        // Mixer variables
        RegisterVariable("Track1Volume", 1.0f, 0.0f, 2.0f, "Track 1 volume");
        RegisterVariable("Track1Pan", 0.0f, -1.0f, 1.0f, "Track 1 pan (-1 to 1)");
        RegisterVariable("Track1Mute", false, null, null, "Track 1 mute state");
        RegisterVariable("Track1Solo", false, null, null, "Track 1 solo state");

        // Effect parameters (examples)
        RegisterVariable("ReverbMix", 0.3f, 0.0f, 1.0f, "Reverb wet/dry mix");
        RegisterVariable("DelayTime", 0.25f, 0.01f, 2.0f, "Delay time in seconds");
        RegisterVariable("FilterCutoff", 1000.0f, 20.0f, 20000.0f, "Filter cutoff frequency");
        RegisterVariable("FilterResonance", 0.5f, 0.0f, 1.0f, "Filter resonance");

        // Game integration specific
        RegisterVariable("IntensityLevel", 0.5f, 0.0f, 1.0f, "Game intensity level for adaptive music");
        RegisterVariable("EnvironmentType", "default", null, null, "Current game environment");
        RegisterVariable("CombatActive", false, null, null, "Whether combat is active");
        RegisterVariable("PlayerHealth", 1.0f, 0.0f, 1.0f, "Player health percentage");
    }

    private void RegisterDefaultEvents()
    {
        // Playback events
        RegisterEvent("Play", _ => PlaybackService.Instance.Play());
        RegisterEvent("Stop", _ => PlaybackService.Instance.Stop());
        RegisterEvent("Pause", _ => PlaybackService.Instance.Pause());
        RegisterEvent("TogglePlayback", _ =>
        {
            if (PlaybackService.Instance.IsPlaying)
                PlaybackService.Instance.Pause();
            else
                PlaybackService.Instance.Play();
        });

        // Position events
        RegisterEvent("SeekTo", param =>
        {
            if (param is double beat)
                PlaybackService.Instance.SetPosition(beat);
        });

        RegisterEvent("SeekToStart", _ => PlaybackService.Instance.JumpToStart());

        // BPM events
        RegisterEvent("SetBPM", param =>
        {
            if (param is double bpm)
                PlaybackService.Instance.BPM = bpm;
        });

        // Pattern events
        RegisterEvent("TriggerPattern", param =>
        {
            if (param is string patternName)
            {
                // TODO: Implement pattern triggering by name
                System.Diagnostics.Debug.WriteLine($"[ExternalControl] TriggerPattern: {patternName}");
            }
        });

        // Note events (for game sound effects)
        RegisterEvent("PlayNote", param =>
        {
            if (param is int midiNote)
            {
                NotePreviewService.Instance.PlayNote(midiNote, 100);
            }
        });

        RegisterEvent("StopNote", param =>
        {
            if (param is int midiNote)
            {
                NotePreviewService.Instance.StopNote(midiNote);
            }
        });

        // Panic/All notes off
        RegisterEvent("AllNotesOff", _ => AudioEngineService.Instance.AllNotesOff());

        // Adaptive music events
        RegisterEvent("SetIntensity", param =>
        {
            if (param is float intensity)
            {
                SetVariable("IntensityLevel", intensity);
                // TODO: Implement adaptive music layer switching
            }
        });

        RegisterEvent("TransitionTo", param =>
        {
            if (param is string sectionName)
            {
                // TODO: Implement section transitions
                System.Diagnostics.Debug.WriteLine($"[ExternalControl] TransitionTo: {sectionName}");
            }
        });
    }

    #endregion

    #region Engine Integration

    private void ApplyVariableToEngine(string name, object? value)
    {
        try
        {
            switch (name)
            {
                case "BPM" when value is double bpm:
                    PlaybackService.Instance.BPM = bpm;
                    break;

                case "MasterVolume" when value is float volume:
                    AudioEngineService.Instance.SetMasterVolume(volume);
                    break;

                case "PlaybackPosition" when value is double position:
                    PlaybackService.Instance.SetPosition(position);
                    break;

                case "LoopEnabled" when value is bool enabled:
                    PlaybackService.Instance.LoopEnabled = enabled;
                    break;

                // Add more variable applications as needed
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ExternalControl] Failed to apply {name}: {ex.Message}");
        }
    }

    private static object ClampValue(object value, object min, object max)
    {
        if (value is double d && min is double dMin && max is double dMax)
            return Math.Clamp(d, dMin, dMax);

        if (value is float f && min is float fMin && max is float fMax)
            return Math.Clamp(f, fMin, fMax);

        if (value is int i && min is int iMin && max is int iMax)
            return Math.Clamp(i, iMin, iMax);

        return value;
    }

    #endregion

    #region Helper Methods

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ExternalControlService));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _variables.Clear();
            _eventHandlers.Clear();
            _variableCallbacks.Clear();
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Represents an external variable that can be controlled from outside.
/// </summary>
public class ExternalVariable
{
    /// <summary>
    /// Unique variable name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Current value.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>
    /// Default value.
    /// </summary>
    public object? DefaultValue { get; set; }

    /// <summary>
    /// Minimum allowed value (for numeric types).
    /// </summary>
    public object? MinValue { get; set; }

    /// <summary>
    /// Maximum allowed value (for numeric types).
    /// </summary>
    public object? MaxValue { get; set; }

    /// <summary>
    /// Human-readable description.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The type of the value.
    /// </summary>
    public Type ValueType { get; set; } = typeof(object);

    /// <summary>
    /// When the variable was last modified.
    /// </summary>
    public DateTime LastModified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event arguments for external events.
/// </summary>
public class ExternalEventArgs : EventArgs
{
    public string EventName { get; }
    public object? Parameter { get; }
    public DateTime Timestamp { get; }

    public ExternalEventArgs(string eventName, object? parameter)
    {
        EventName = eventName;
        Parameter = parameter;
        Timestamp = DateTime.UtcNow;
    }
}

/// <summary>
/// Event arguments for variable changes.
/// </summary>
public class VariableChangedEventArgs : EventArgs
{
    public string VariableName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }
    public DateTime Timestamp { get; }

    public VariableChangedEventArgs(string variableName, object? oldValue, object? newValue)
    {
        VariableName = variableName;
        OldValue = oldValue;
        NewValue = newValue;
        Timestamp = DateTime.UtcNow;
    }
}

#endregion
