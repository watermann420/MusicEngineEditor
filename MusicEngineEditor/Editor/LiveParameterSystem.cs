// LiveParameterSystem.cs
// System for live parameter updates that affect running patterns without restart.
// Enables real-time slider control of velocity, duration, BPM, and other parameters.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using MusicEngine.Core;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Represents a parameter that can be controlled live during playback.
/// </summary>
public class LiveParameterBinding
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "";
    public string ParameterType { get; set; } = ""; // "velocity", "duration", "bpm", "note", "beat", etc.
    public double Value { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; }
    public double Step { get; set; } = 1.0;
    public int SourceStart { get; set; }
    public int SourceEnd { get; set; }
    public string OriginalText { get; set; } = "";

    /// <summary>Target object this parameter controls (Pattern, NoteEvent, Sequencer, etc.)</summary>
    public object? Target { get; set; }

    /// <summary>Property path on the target (e.g., "Velocity", "Duration", "Bpm")</summary>
    public string TargetProperty { get; set; } = "";

    /// <summary>Callback to apply value changes.</summary>
    public Action<double>? OnValueChanged { get; set; }
}

/// <summary>
/// Manages live parameter bindings between code and running musical objects.
/// </summary>
public class LiveParameterSystem : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Dictionary<string, LiveParameterBinding> _bindings = new();
    private readonly object _lock = new();
    private Sequencer? _sequencer;
    private bool _isActive;
    private string _lastCode = "";

    /// <summary>Fired when a parameter value changes.</summary>
    public event EventHandler<ParameterChangedEventArgs>? ParameterChanged;

    /// <summary>Fired when bindings are updated.</summary>
    public event EventHandler? BindingsUpdated;

    public LiveParameterSystem(TextEditor editor)
    {
        _editor = editor;
    }

    /// <summary>Gets all current parameter bindings.</summary>
    public IReadOnlyList<LiveParameterBinding> Bindings
    {
        get
        {
            lock (_lock)
            {
                return _bindings.Values.ToList();
            }
        }
    }

    /// <summary>Whether the system is actively tracking parameters.</summary>
    public bool IsActive => _isActive;

    #region Setup

    /// <summary>
    /// Binds to a sequencer for live updates.
    /// </summary>
    public void BindToSequencer(Sequencer sequencer)
    {
        _sequencer = sequencer;
    }

    /// <summary>
    /// Unbinds from the current sequencer.
    /// </summary>
    public void UnbindSequencer()
    {
        _sequencer = null;
    }

    /// <summary>
    /// Starts the live parameter system.
    /// </summary>
    public void Start()
    {
        _isActive = true;
        AnalyzeAndBindParameters(_editor.Text);
    }

    /// <summary>
    /// Stops the live parameter system.
    /// </summary>
    public void Stop()
    {
        _isActive = false;
        ClearBindings();
    }

    #endregion

    #region Parameter Analysis

    /// <summary>
    /// Analyzes code and creates parameter bindings for controllable values.
    /// </summary>
    public void AnalyzeAndBindParameters(string code)
    {
        if (code == _lastCode) return;
        _lastCode = code;

        lock (_lock)
        {
            _bindings.Clear();

            // Find all numeric literals that represent controllable parameters
            FindBpmParameters(code);
            FindVelocityParameters(code);
            FindDurationParameters(code);
            FindBeatParameters(code);
            FindNoteParameters(code);
            FindGenericNumericParameters(code);
        }

        BindingsUpdated?.Invoke(this, EventArgs.Empty);
    }

    private void FindBpmParameters(string code)
    {
        // Match patterns like: Bpm = 120, SetBpm(120), sequencer.Bpm = 120
        var patterns = new[]
        {
            @"\.?Bpm\s*=\s*(?<value>\d+(?:\.\d+)?)",
            @"SetBpm\s*\(\s*(?<value>\d+(?:\.\d+)?)\s*\)"
        };

        foreach (var pattern in patterns)
        {
            foreach (Match match in Regex.Matches(code, pattern))
            {
                var valueGroup = match.Groups["value"];
                if (double.TryParse(valueGroup.Value, out double value))
                {
                    CreateBinding("bpm", "BPM", value, 20, 300, 1,
                        valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                        newValue => _sequencer?.SetPropertyValue("Bpm", newValue));
                }
            }
        }
    }

    private void FindVelocityParameters(string code)
    {
        // Match: Velocity = 100, velocity: 100
        var pattern = @"Velocity\s*=\s*(?<value>\d+)";

        foreach (Match match in Regex.Matches(code, pattern))
        {
            var valueGroup = match.Groups["value"];
            if (int.TryParse(valueGroup.Value, out int value))
            {
                CreateBinding("velocity", "Velocity", value, 0, 127, 1,
                    valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                    newValue => UpdateNoteEventProperty(match.Index, "Velocity", (int)newValue));
            }
        }
    }

    private void FindDurationParameters(string code)
    {
        // Match: Duration = 0.5
        var pattern = @"Duration\s*=\s*(?<value>\d+(?:\.\d+)?)";

        foreach (Match match in Regex.Matches(code, pattern))
        {
            var valueGroup = match.Groups["value"];
            if (double.TryParse(valueGroup.Value, out double value))
            {
                CreateBinding("duration", "Duration", value, 0.0625, 4.0, 0.0625,
                    valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                    newValue => UpdateNoteEventProperty(match.Index, "Duration", newValue));
            }
        }
    }

    private void FindBeatParameters(string code)
    {
        // Match: Beat = 0.0
        var pattern = @"Beat\s*=\s*(?<value>\d+(?:\.\d+)?)";

        foreach (Match match in Regex.Matches(code, pattern))
        {
            var valueGroup = match.Groups["value"];
            if (double.TryParse(valueGroup.Value, out double value))
            {
                CreateBinding("beat", "Beat Position", value, 0, 16, 0.25,
                    valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                    newValue => UpdateNoteEventProperty(match.Index, "Beat", newValue));
            }
        }
    }

    private void FindNoteParameters(string code)
    {
        // Match: Note = 60
        var pattern = @"Note\s*=\s*(?<value>\d+)";

        foreach (Match match in Regex.Matches(code, pattern))
        {
            var valueGroup = match.Groups["value"];
            if (int.TryParse(valueGroup.Value, out int value))
            {
                CreateBinding("note", "MIDI Note", value, 0, 127, 1,
                    valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                    newValue => UpdateNoteEventProperty(match.Index, "Note", (int)newValue));
            }
        }
    }

    private void FindGenericNumericParameters(string code)
    {
        // Find slider annotations: // @slider(min, max) or // @slider(min, max, step, "label")
        var pattern = @"(?<assignment>\w+\s*=\s*)(?<value>\d+(?:\.\d+)?)\s*;?\s*//\s*@slider\s*\(\s*(?<min>\d+(?:\.\d+)?)\s*,\s*(?<max>\d+(?:\.\d+)?)(?:\s*,\s*(?<step>\d+(?:\.\d+)?))?(?:\s*,\s*[""'](?<label>[^""']+)[""'])?\s*\)";

        foreach (Match match in Regex.Matches(code, pattern))
        {
            var valueGroup = match.Groups["value"];
            var labelGroup = match.Groups["label"];

            if (double.TryParse(valueGroup.Value, out double value) &&
                double.TryParse(match.Groups["min"].Value, out double min) &&
                double.TryParse(match.Groups["max"].Value, out double max))
            {
                double step = 1.0;
                if (match.Groups["step"].Success)
                {
                    double.TryParse(match.Groups["step"].Value, out step);
                }

                string label = labelGroup.Success ? labelGroup.Value : "Parameter";

                CreateBinding("custom", label, value, min, max, step,
                    valueGroup.Index, valueGroup.Index + valueGroup.Length, valueGroup.Value,
                    newValue => { /* Custom parameters need specific handling */ });
            }
        }
    }

    private void CreateBinding(string type, string name, double value, double min, double max, double step,
        int start, int end, string originalText, Action<double>? onChanged)
    {
        var binding = new LiveParameterBinding
        {
            ParameterType = type,
            Name = name,
            Value = value,
            MinValue = min,
            MaxValue = max,
            Step = step,
            SourceStart = start,
            SourceEnd = end,
            OriginalText = originalText,
            OnValueChanged = onChanged
        };

        _bindings[binding.Id] = binding;
    }

    #endregion

    #region Value Updates

    /// <summary>
    /// Updates a parameter value and applies it to the running pattern.
    /// </summary>
    public void UpdateParameter(string bindingId, double newValue)
    {
        LiveParameterBinding? binding;
        lock (_lock)
        {
            if (!_bindings.TryGetValue(bindingId, out binding))
                return;

            var oldValue = binding.Value;
            binding.Value = Math.Clamp(newValue, binding.MinValue, binding.MaxValue);

            // Update the code text
            UpdateCodeText(binding);

            // Apply the value change
            binding.OnValueChanged?.Invoke(binding.Value);

            // Fire event
            ParameterChanged?.Invoke(this, new ParameterChangedEventArgs(
                binding.Name, oldValue, binding.Value,
                new CodeSourceInfo { StartIndex = binding.SourceStart, EndIndex = binding.SourceEnd }));
        }
    }

    /// <summary>
    /// Updates a parameter by its source location.
    /// </summary>
    public void UpdateParameterAtOffset(int offset, double newValue)
    {
        lock (_lock)
        {
            var binding = _bindings.Values.FirstOrDefault(b =>
                offset >= b.SourceStart && offset <= b.SourceEnd);

            if (binding != null)
            {
                UpdateParameter(binding.Id, newValue);
            }
        }
    }

    private void UpdateCodeText(LiveParameterBinding binding)
    {
        _editor.Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var document = _editor.Document;
                var newText = FormatValue(binding.Value, binding.OriginalText);

                // Calculate offset adjustment for subsequent bindings
                int lengthDiff = newText.Length - (binding.SourceEnd - binding.SourceStart);

                // Replace the text
                document.Replace(binding.SourceStart, binding.SourceEnd - binding.SourceStart, newText);

                // Update this binding's end position
                binding.SourceEnd = binding.SourceStart + newText.Length;
                binding.OriginalText = newText;

                // Adjust positions of subsequent bindings
                foreach (var other in _bindings.Values.Where(b => b.SourceStart > binding.SourceStart))
                {
                    other.SourceStart += lengthDiff;
                    other.SourceEnd += lengthDiff;
                }
            }
            catch
            {
                // Ignore text update errors - audio continues
            }
        });
    }

    private string FormatValue(double value, string originalText)
    {
        // Try to match the original format
        if (originalText.Contains('.'))
        {
            // Preserve decimal places from original
            int decimals = originalText.Length - originalText.IndexOf('.') - 1;
            return value.ToString($"F{decimals}");
        }
        else
        {
            return ((int)value).ToString();
        }
    }

    private void UpdateNoteEventProperty(int codeOffset, string property, object value)
    {
        if (_sequencer == null) return;

        // Find the NoteEvent that corresponds to this code location
        foreach (var pattern in _sequencer.Patterns)
        {
            foreach (var noteEvent in pattern.Events)
            {
                if (noteEvent.SourceInfo != null &&
                    codeOffset >= noteEvent.SourceInfo.StartIndex &&
                    codeOffset <= noteEvent.SourceInfo.EndIndex)
                {
                    // Update the property
                    switch (property)
                    {
                        case "Velocity":
                            noteEvent.Velocity = (int)value;
                            break;
                        case "Duration":
                            noteEvent.Duration = (double)value;
                            break;
                        case "Beat":
                            noteEvent.Beat = (double)value;
                            break;
                        case "Note":
                            noteEvent.Note = (int)value;
                            break;
                    }
                    return;
                }
            }
        }
    }

    private void ClearBindings()
    {
        lock (_lock)
        {
            _bindings.Clear();
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Stop();
        UnbindSequencer();
    }

    #endregion
}

/// <summary>
/// Extension methods for live parameter updates.
/// </summary>
public static class SequencerExtensions
{
    /// <summary>
    /// Sets a property value on the sequencer using reflection.
    /// </summary>
    public static void SetPropertyValue(this Sequencer sequencer, string propertyName, object value)
    {
        var property = typeof(Sequencer).GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(sequencer, Convert.ChangeType(value, property.PropertyType));
        }
    }
}
