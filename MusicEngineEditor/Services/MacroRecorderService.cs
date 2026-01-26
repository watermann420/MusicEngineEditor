using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Services;

/// <summary>
/// Types of actions that can be recorded in a macro.
/// </summary>
public enum MacroActionType
{
    /// <summary>Menu command execution.</summary>
    MenuCommand,
    /// <summary>Parameter value change.</summary>
    ParameterChange,
    /// <summary>Tool selection.</summary>
    ToolUsage,
    /// <summary>File operation.</summary>
    FileOperation,
    /// <summary>Navigation action.</summary>
    Navigation,
    /// <summary>Selection change.</summary>
    Selection,
    /// <summary>Custom action.</summary>
    Custom
}

/// <summary>
/// Represents a single action step in a macro.
/// </summary>
public class MacroStep : ObservableObject
{
    private int _order;
    private MacroActionType _actionType;
    private string _actionName = string.Empty;
    private string _targetId = string.Empty;
    private Dictionary<string, object> _parameters = new();
    private TimeSpan _delay = TimeSpan.Zero;
    private bool _isEnabled = true;

    /// <summary>
    /// Order of execution within the macro.
    /// </summary>
    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }

    /// <summary>
    /// Type of action.
    /// </summary>
    public MacroActionType ActionType
    {
        get => _actionType;
        set => SetProperty(ref _actionType, value);
    }

    /// <summary>
    /// Name of the action (e.g., command name, parameter name).
    /// </summary>
    public string ActionName
    {
        get => _actionName;
        set => SetProperty(ref _actionName, value);
    }

    /// <summary>
    /// Target identifier (e.g., control ID, track ID).
    /// </summary>
    public string TargetId
    {
        get => _targetId;
        set => SetProperty(ref _targetId, value);
    }

    /// <summary>
    /// Parameters for the action.
    /// </summary>
    public Dictionary<string, object> Parameters
    {
        get => _parameters;
        set => SetProperty(ref _parameters, value);
    }

    /// <summary>
    /// Delay before executing this step.
    /// </summary>
    public TimeSpan Delay
    {
        get => _delay;
        set => SetProperty(ref _delay, value);
    }

    /// <summary>
    /// Whether this step is enabled for playback.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    /// <summary>
    /// Creates a clone of this step.
    /// </summary>
    public MacroStep Clone()
    {
        return new MacroStep
        {
            Order = Order,
            ActionType = ActionType,
            ActionName = ActionName,
            TargetId = TargetId,
            Parameters = new Dictionary<string, object>(Parameters),
            Delay = Delay,
            IsEnabled = IsEnabled
        };
    }

    /// <summary>
    /// Gets a human-readable description of this step.
    /// </summary>
    public string GetDescription()
    {
        return ActionType switch
        {
            MacroActionType.MenuCommand => $"Execute: {ActionName}",
            MacroActionType.ParameterChange => $"Set {ActionName} = {(Parameters.TryGetValue("Value", out var v) ? v : "?")}",
            MacroActionType.ToolUsage => $"Use tool: {ActionName}",
            MacroActionType.FileOperation => $"File: {ActionName}",
            MacroActionType.Navigation => $"Navigate: {ActionName}",
            MacroActionType.Selection => $"Select: {ActionName}",
            MacroActionType.Custom => $"Custom: {ActionName}",
            _ => ActionName
        };
    }
}

/// <summary>
/// Represents a recorded macro containing multiple action steps.
/// </summary>
public class Macro : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = "New Macro";
    private string _description = string.Empty;
    private List<MacroStep> _steps = new();
    private Key _shortcutKey = Key.None;
    private ModifierKeys _shortcutModifiers = ModifierKeys.None;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _modifiedAt = DateTime.UtcNow;
    private string _category = "General";

    /// <summary>
    /// Unique identifier.
    /// </summary>
    public Guid Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// Display name.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
                ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Description of what the macro does.
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            if (SetProperty(ref _description, value))
                ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Steps in this macro.
    /// </summary>
    public List<MacroStep> Steps
    {
        get => _steps;
        set
        {
            if (SetProperty(ref _steps, value))
                ModifiedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Assigned keyboard shortcut key.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public Key ShortcutKey
    {
        get => _shortcutKey;
        set => SetProperty(ref _shortcutKey, value);
    }

    /// <summary>
    /// Assigned keyboard shortcut modifiers.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ModifierKeys ShortcutModifiers
    {
        get => _shortcutModifiers;
        set => SetProperty(ref _shortcutModifiers, value);
    }

    /// <summary>
    /// Creation timestamp.
    /// </summary>
    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetProperty(ref _createdAt, value);
    }

    /// <summary>
    /// Last modification timestamp.
    /// </summary>
    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set => SetProperty(ref _modifiedAt, value);
    }

    /// <summary>
    /// Category for organization.
    /// </summary>
    public string Category
    {
        get => _category;
        set => SetProperty(ref _category, value);
    }

    /// <summary>
    /// Gets the shortcut display string.
    /// </summary>
    [JsonIgnore]
    public string ShortcutDisplay
    {
        get
        {
            if (ShortcutKey == Key.None)
                return string.Empty;

            var parts = new List<string>();
            if (ShortcutModifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (ShortcutModifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (ShortcutModifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            parts.Add(ShortcutKey.ToString());
            return string.Join("+", parts);
        }
    }

    /// <summary>
    /// Creates a clone of this macro.
    /// </summary>
    public Macro Clone()
    {
        return new Macro
        {
            Id = Guid.NewGuid(),
            Name = Name + " (Copy)",
            Description = Description,
            Steps = Steps.Select(s => s.Clone()).ToList(),
            ShortcutKey = Key.None,
            ShortcutModifiers = ModifierKeys.None,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Category = Category
        };
    }
}

/// <summary>
/// Event arguments for macro playback events.
/// </summary>
public class MacroPlaybackEventArgs : EventArgs
{
    /// <summary>The macro being played.</summary>
    public Macro Macro { get; }
    /// <summary>The current step being executed.</summary>
    public MacroStep? CurrentStep { get; }
    /// <summary>Index of the current step.</summary>
    public int StepIndex { get; }
    /// <summary>Total number of steps.</summary>
    public int TotalSteps { get; }
    /// <summary>Error message if any.</summary>
    public string? Error { get; }

    public MacroPlaybackEventArgs(Macro macro, MacroStep? step, int index, int total, string? error = null)
    {
        Macro = macro;
        CurrentStep = step;
        StepIndex = index;
        TotalSteps = total;
        Error = error;
    }
}

/// <summary>
/// Event arguments for macro recording events.
/// </summary>
public class MacroRecordingEventArgs : EventArgs
{
    /// <summary>The step that was recorded.</summary>
    public MacroStep Step { get; }

    public MacroRecordingEventArgs(MacroStep step)
    {
        Step = step;
    }
}

/// <summary>
/// Service for recording and playing back user action macros.
/// </summary>
public class MacroRecorderService : ObservableObject
{
    private static readonly string MacrosFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor", "Macros");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly List<Macro> _macros = new();
    private readonly Dictionary<(Key, ModifierKeys), Macro> _shortcutMap = new();
    private Macro? _recordingMacro;
    private bool _isRecording;
    private bool _isPlaying;
    private int _currentPlaybackStep;

    /// <summary>
    /// Gets all loaded macros.
    /// </summary>
    public IReadOnlyList<Macro> Macros => _macros.AsReadOnly();

    /// <summary>
    /// Gets whether recording is active.
    /// </summary>
    public bool IsRecording
    {
        get => _isRecording;
        private set => SetProperty(ref _isRecording, value);
    }

    /// <summary>
    /// Gets whether playback is active.
    /// </summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        private set => SetProperty(ref _isPlaying, value);
    }

    /// <summary>
    /// Gets the current macro being recorded.
    /// </summary>
    public Macro? RecordingMacro => _recordingMacro;

    /// <summary>
    /// Event raised when recording starts.
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Event raised when a step is recorded.
    /// </summary>
    public event EventHandler<MacroRecordingEventArgs>? StepRecorded;

    /// <summary>
    /// Event raised when recording stops.
    /// </summary>
    public event EventHandler<Macro>? RecordingStopped;

    /// <summary>
    /// Event raised when playback starts.
    /// </summary>
    public event EventHandler<MacroPlaybackEventArgs>? PlaybackStarted;

    /// <summary>
    /// Event raised for each step during playback.
    /// </summary>
    public event EventHandler<MacroPlaybackEventArgs>? PlaybackStep;

    /// <summary>
    /// Event raised when playback completes.
    /// </summary>
    public event EventHandler<MacroPlaybackEventArgs>? PlaybackCompleted;

    /// <summary>
    /// Event raised when an action should be executed during playback.
    /// </summary>
    public event EventHandler<MacroStep>? ExecuteAction;

    /// <summary>
    /// Event raised when macros are changed.
    /// </summary>
    public event EventHandler? MacrosChanged;

    /// <summary>
    /// Starts recording a new macro.
    /// </summary>
    /// <param name="name">Name for the macro.</param>
    public void StartRecording(string name = "New Macro")
    {
        if (IsRecording || IsPlaying) return;

        _recordingMacro = new Macro
        {
            Name = name,
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };

        IsRecording = true;
        RecordingStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Records an action step.
    /// </summary>
    public void RecordStep(MacroActionType actionType, string actionName, string targetId = "",
        Dictionary<string, object>? parameters = null, TimeSpan? delay = null)
    {
        if (!IsRecording || _recordingMacro == null) return;

        var step = new MacroStep
        {
            Order = _recordingMacro.Steps.Count,
            ActionType = actionType,
            ActionName = actionName,
            TargetId = targetId,
            Parameters = parameters ?? new Dictionary<string, object>(),
            Delay = delay ?? TimeSpan.Zero
        };

        _recordingMacro.Steps.Add(step);
        StepRecorded?.Invoke(this, new MacroRecordingEventArgs(step));
    }

    /// <summary>
    /// Records a menu command execution.
    /// </summary>
    public void RecordMenuCommand(string commandName)
    {
        RecordStep(MacroActionType.MenuCommand, commandName);
    }

    /// <summary>
    /// Records a parameter change.
    /// </summary>
    public void RecordParameterChange(string parameterName, object value, string targetId = "")
    {
        RecordStep(MacroActionType.ParameterChange, parameterName, targetId,
            new Dictionary<string, object> { { "Value", value } });
    }

    /// <summary>
    /// Records a tool usage.
    /// </summary>
    public void RecordToolUsage(string toolName)
    {
        RecordStep(MacroActionType.ToolUsage, toolName);
    }

    /// <summary>
    /// Stops recording and returns the recorded macro.
    /// </summary>
    public Macro? StopRecording()
    {
        if (!IsRecording || _recordingMacro == null) return null;

        var macro = _recordingMacro;
        _recordingMacro = null;
        IsRecording = false;

        if (macro.Steps.Count > 0)
        {
            _macros.Add(macro);
            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }

        RecordingStopped?.Invoke(this, macro);
        return macro;
    }

    /// <summary>
    /// Cancels recording without saving.
    /// </summary>
    public void CancelRecording()
    {
        _recordingMacro = null;
        IsRecording = false;
    }

    /// <summary>
    /// Plays back a macro asynchronously.
    /// </summary>
    public async Task PlayMacroAsync(Macro macro)
    {
        if (IsRecording || IsPlaying) return;

        IsPlaying = true;
        _currentPlaybackStep = 0;
        var enabledSteps = macro.Steps.Where(s => s.IsEnabled).OrderBy(s => s.Order).ToList();

        PlaybackStarted?.Invoke(this, new MacroPlaybackEventArgs(macro, null, 0, enabledSteps.Count));

        try
        {
            for (int i = 0; i < enabledSteps.Count; i++)
            {
                if (!IsPlaying) break; // Allow stopping

                var step = enabledSteps[i];
                _currentPlaybackStep = i;

                // Apply delay
                if (step.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(step.Delay);
                }

                // Raise event for UI to execute the action
                PlaybackStep?.Invoke(this, new MacroPlaybackEventArgs(macro, step, i, enabledSteps.Count));

                // Execute the action
                ExecuteAction?.Invoke(this, step);

                // Small delay between steps for UI responsiveness
                await Task.Delay(50);
            }

            PlaybackCompleted?.Invoke(this, new MacroPlaybackEventArgs(macro, null, enabledSteps.Count, enabledSteps.Count));
        }
        catch (Exception ex)
        {
            PlaybackCompleted?.Invoke(this, new MacroPlaybackEventArgs(macro, null, _currentPlaybackStep, enabledSteps.Count, ex.Message));
        }
        finally
        {
            IsPlaying = false;
        }
    }

    /// <summary>
    /// Stops the current playback.
    /// </summary>
    public void StopPlayback()
    {
        IsPlaying = false;
    }

    /// <summary>
    /// Adds or updates a macro.
    /// </summary>
    public void SaveMacro(Macro macro)
    {
        var existing = _macros.FirstOrDefault(m => m.Id == macro.Id);
        if (existing != null)
        {
            var index = _macros.IndexOf(existing);
            _macros[index] = macro;
        }
        else
        {
            _macros.Add(macro);
        }

        UpdateShortcutMap();
        MacrosChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Deletes a macro.
    /// </summary>
    public bool DeleteMacro(Guid macroId)
    {
        var macro = _macros.FirstOrDefault(m => m.Id == macroId);
        if (macro != null)
        {
            _macros.Remove(macro);
            UpdateShortcutMap();
            MacrosChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets a macro by ID.
    /// </summary>
    public Macro? GetMacro(Guid id)
    {
        return _macros.FirstOrDefault(m => m.Id == id);
    }

    /// <summary>
    /// Gets macros by category.
    /// </summary>
    public IReadOnlyList<Macro> GetMacrosByCategory(string category)
    {
        return _macros.Where(m => m.Category == category).ToList();
    }

    /// <summary>
    /// Gets all unique categories.
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        return _macros.Select(m => m.Category).Distinct().OrderBy(c => c).ToList();
    }

    /// <summary>
    /// Assigns a keyboard shortcut to a macro.
    /// </summary>
    public bool AssignShortcut(Guid macroId, Key key, ModifierKeys modifiers)
    {
        var macro = _macros.FirstOrDefault(m => m.Id == macroId);
        if (macro == null) return false;

        // Check for conflicts
        if (key != Key.None && _shortcutMap.ContainsKey((key, modifiers)))
        {
            var conflict = _shortcutMap[(key, modifiers)];
            if (conflict.Id != macroId)
            {
                return false; // Conflict with another macro
            }
        }

        // Remove old shortcut if exists
        var oldKey = (macro.ShortcutKey, macro.ShortcutModifiers);
        if (oldKey.ShortcutKey != Key.None)
        {
            _shortcutMap.Remove(oldKey);
        }

        // Assign new shortcut
        macro.ShortcutKey = key;
        macro.ShortcutModifiers = modifiers;

        if (key != Key.None)
        {
            _shortcutMap[(key, modifiers)] = macro;
        }

        MacrosChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Processes a key press and plays the associated macro if any.
    /// </summary>
    public async Task<bool> ProcessKeyDownAsync(Key key, ModifierKeys modifiers)
    {
        if (IsRecording || IsPlaying) return false;

        if (_shortcutMap.TryGetValue((key, modifiers), out var macro))
        {
            await PlayMacroAsync(macro);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Loads macros from disk.
    /// </summary>
    public async Task LoadMacrosAsync()
    {
        try
        {
            if (!Directory.Exists(MacrosFolder))
            {
                Directory.CreateDirectory(MacrosFolder);
                return;
            }

            var files = Directory.GetFiles(MacrosFolder, "*.json");
            _macros.Clear();

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var macro = JsonSerializer.Deserialize<Macro>(json, JsonOptions);
                    if (macro != null)
                    {
                        _macros.Add(macro);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load macro {file}: {ex.Message}");
                }
            }

            UpdateShortcutMap();
            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load macros: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves all macros to disk.
    /// </summary>
    public async Task SaveMacrosAsync()
    {
        try
        {
            Directory.CreateDirectory(MacrosFolder);

            // Clear old files
            foreach (var file in Directory.GetFiles(MacrosFolder, "*.json"))
            {
                File.Delete(file);
            }

            // Save each macro
            foreach (var macro in _macros)
            {
                var filePath = Path.Combine(MacrosFolder, $"{macro.Id}.json");
                var json = JsonSerializer.Serialize(macro, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save macros: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Exports a macro to a file.
    /// </summary>
    public async Task ExportMacroAsync(Macro macro, string filePath)
    {
        var json = JsonSerializer.Serialize(macro, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Imports a macro from a file.
    /// </summary>
    public async Task<Macro?> ImportMacroAsync(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var json = await File.ReadAllTextAsync(filePath);
        var macro = JsonSerializer.Deserialize<Macro>(json, JsonOptions);

        if (macro != null)
        {
            // Generate new ID to avoid conflicts
            macro.Id = Guid.NewGuid();
            macro.ShortcutKey = Key.None;
            macro.ShortcutModifiers = ModifierKeys.None;
            _macros.Add(macro);
            MacrosChanged?.Invoke(this, EventArgs.Empty);
        }

        return macro;
    }

    private void UpdateShortcutMap()
    {
        _shortcutMap.Clear();
        foreach (var macro in _macros.Where(m => m.ShortcutKey != Key.None))
        {
            _shortcutMap[(macro.ShortcutKey, macro.ShortcutModifiers)] = macro;
        }
    }
}
