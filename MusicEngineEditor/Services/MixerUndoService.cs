// MusicEngineEditor - Mixer Undo Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents an undoable mixer change.
/// </summary>
public interface IMixerUndoCommand
{
    /// <summary>
    /// Gets the description of this command.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the category of this command.
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Gets the channel name affected by this command.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Gets the timestamp when this command was executed.
    /// </summary>
    DateTime Timestamp { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undoes the command.
    /// </summary>
    void Undo();
}

/// <summary>
/// Base class for mixer undo commands.
/// </summary>
public abstract class MixerUndoCommandBase : IMixerUndoCommand
{
    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public abstract string Category { get; }

    /// <inheritdoc/>
    public string ChannelName { get; }

    /// <inheritdoc/>
    public DateTime Timestamp { get; } = DateTime.UtcNow;

    protected MixerUndoCommandBase(string channelName)
    {
        ChannelName = channelName;
    }

    /// <inheritdoc/>
    public abstract void Execute();

    /// <inheritdoc/>
    public abstract void Undo();
}

/// <summary>
/// Command for changing a mixer channel's fader (volume).
/// </summary>
public class FaderChangeCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly float _oldValue;
    private readonly float _newValue;

    public override string Description => $"Fader: {_oldValue:F2} -> {_newValue:F2}";
    public override string Category => "Fader";

    public FaderChangeCommand(MixerChannel channel, float oldValue, float newValue)
        : base(channel.Name)
    {
        _channel = channel;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _channel.Volume = _newValue;
    public override void Undo() => _channel.Volume = _oldValue;
}

/// <summary>
/// Command for changing a mixer channel's pan.
/// </summary>
public class PanChangeCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly float _oldValue;
    private readonly float _newValue;

    public override string Description => $"Pan: {FormatPan(_oldValue)} -> {FormatPan(_newValue)}";
    public override string Category => "Pan";

    public PanChangeCommand(MixerChannel channel, float oldValue, float newValue)
        : base(channel.Name)
    {
        _channel = channel;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _channel.Pan = _newValue;
    public override void Undo() => _channel.Pan = _oldValue;

    private static string FormatPan(float value)
    {
        if (Math.Abs(value) < 0.01f) return "C";
        return value < 0 ? $"L{(int)(-value * 100)}" : $"R{(int)(value * 100)}";
    }
}

/// <summary>
/// Command for toggling a mixer channel's mute state.
/// </summary>
public class MuteToggleCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly bool _oldValue;
    private readonly bool _newValue;

    public override string Description => _newValue ? "Mute On" : "Mute Off";
    public override string Category => "Mute";

    public MuteToggleCommand(MixerChannel channel, bool oldValue, bool newValue)
        : base(channel.Name)
    {
        _channel = channel;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _channel.IsMuted = _newValue;
    public override void Undo() => _channel.IsMuted = _oldValue;
}

/// <summary>
/// Command for toggling a mixer channel's solo state.
/// </summary>
public class SoloToggleCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly bool _oldValue;
    private readonly bool _newValue;

    public override string Description => _newValue ? "Solo On" : "Solo Off";
    public override string Category => "Solo";

    public SoloToggleCommand(MixerChannel channel, bool oldValue, bool newValue)
        : base(channel.Name)
    {
        _channel = channel;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _channel.IsSoloed = _newValue;
    public override void Undo() => _channel.IsSoloed = _oldValue;
}

/// <summary>
/// Command for changing an effect parameter.
/// </summary>
public class EffectParameterChangeCommand : MixerUndoCommandBase
{
    private readonly EffectSlot _slot;
    private readonly string _parameterName;
    private readonly object _oldValue;
    private readonly object _newValue;
    private readonly Action<object> _setter;

    public override string Description => $"{_slot.DisplayName}: {_parameterName}";
    public override string Category => "Effect";

    public EffectParameterChangeCommand(
        string channelName,
        EffectSlot slot,
        string parameterName,
        object oldValue,
        object newValue,
        Action<object> setter)
        : base(channelName)
    {
        _slot = slot;
        _parameterName = parameterName;
        _oldValue = oldValue;
        _newValue = newValue;
        _setter = setter;
    }

    public override void Execute() => _setter(_newValue);
    public override void Undo() => _setter(_oldValue);
}

/// <summary>
/// Command for changing a send level.
/// </summary>
public class SendLevelChangeCommand : MixerUndoCommandBase
{
    private readonly Send _send;
    private readonly float _oldValue;
    private readonly float _newValue;

    public override string Description => $"Send to {_send.TargetBusName}: {_oldValue:F2} -> {_newValue:F2}";
    public override string Category => "Send";

    public SendLevelChangeCommand(string channelName, Send send, float oldValue, float newValue)
        : base(channelName)
    {
        _send = send;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _send.Level = _newValue;
    public override void Undo() => _send.Level = _oldValue;
}

/// <summary>
/// Command for toggling effect bypass.
/// </summary>
public class EffectBypassToggleCommand : MixerUndoCommandBase
{
    private readonly EffectSlot _slot;
    private readonly bool _oldValue;
    private readonly bool _newValue;

    public override string Description => $"{_slot.DisplayName}: {(_newValue ? "Bypassed" : "Active")}";
    public override string Category => "Effect Bypass";

    public EffectBypassToggleCommand(string channelName, EffectSlot slot, bool oldValue, bool newValue)
        : base(channelName)
    {
        _slot = slot;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public override void Execute() => _slot.IsBypassed = _newValue;
    public override void Undo() => _slot.IsBypassed = _oldValue;
}

/// <summary>
/// Command for changing channel color.
/// </summary>
public class ChannelColorChangeCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly string _oldColor;
    private readonly string _newColor;

    public override string Description => $"Color: {_oldColor} -> {_newColor}";
    public override string Category => "Color";

    public ChannelColorChangeCommand(MixerChannel channel, string oldColor, string newColor)
        : base(channel.Name)
    {
        _channel = channel;
        _oldColor = oldColor;
        _newColor = newColor;
    }

    public override void Execute() => _channel.Color = _newColor;
    public override void Undo() => _channel.Color = _oldColor;
}

/// <summary>
/// Command for renaming a channel.
/// </summary>
public class ChannelRenameCommand : MixerUndoCommandBase
{
    private readonly MixerChannel _channel;
    private readonly string _oldName;
    private readonly string _newName;

    public override string Description => $"Rename: {_oldName} -> {_newName}";
    public override string Category => "Rename";

    public ChannelRenameCommand(MixerChannel channel, string oldName, string newName)
        : base(oldName)
    {
        _channel = channel;
        _oldName = oldName;
        _newName = newName;
    }

    public override void Execute() => _channel.Name = _newName;
    public override void Undo() => _channel.Name = _oldName;
}

/// <summary>
/// Service providing a separate undo stack for mixer changes.
/// </summary>
public sealed class MixerUndoService : INotifyPropertyChanged, IDisposable
{
    private static MixerUndoService? _instance;
    private static readonly object _lock = new();

    private readonly Stack<IMixerUndoCommand> _undoStack = new();
    private readonly Stack<IMixerUndoCommand> _redoStack = new();
    private readonly int _maxHistorySize;
    private bool _disposed;

    /// <summary>
    /// Gets the singleton instance of the MixerUndoService.
    /// </summary>
    public static MixerUndoService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new MixerUndoService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Gets whether there are commands that can be undone.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Gets whether there are commands that can be redone.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Gets the number of commands in the undo stack.
    /// </summary>
    public int UndoCount => _undoStack.Count;

    /// <summary>
    /// Gets the number of commands in the redo stack.
    /// </summary>
    public int RedoCount => _redoStack.Count;

    /// <summary>
    /// Gets the description of the next undo operation.
    /// </summary>
    public string? NextUndoDescription => _undoStack.Count > 0 ? _undoStack.Peek().Description : null;

    /// <summary>
    /// Gets the description of the next redo operation.
    /// </summary>
    public string? NextRedoDescription => _redoStack.Count > 0 ? _redoStack.Peek().Description : null;

    /// <summary>
    /// Gets the undo history for display.
    /// </summary>
    public IReadOnlyList<IMixerUndoCommand> UndoHistory => _undoStack.ToArray();

    /// <summary>
    /// Gets the redo history for display.
    /// </summary>
    public IReadOnlyList<IMixerUndoCommand> RedoHistory => _redoStack.ToArray();

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Creates a new MixerUndoService with the specified history size.
    /// </summary>
    private MixerUndoService(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Creates a new instance for testing or DI scenarios.
    /// </summary>
    public static MixerUndoService CreateInstance(int maxHistorySize = 100)
    {
        return new MixerUndoService(maxHistorySize);
    }

    /// <summary>
    /// Executes a command and adds it to the undo stack.
    /// </summary>
    public void Execute(IMixerUndoCommand command)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(command);

        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();

        // Trim history if needed
        while (_undoStack.Count > _maxHistorySize)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = 0; i < items.Length - 1; i++)
            {
                _undoStack.Push(items[i]);
            }
        }

        RaiseStateChanged();
    }

    /// <summary>
    /// Records a fader change.
    /// </summary>
    public void RecordFaderChange(MixerChannel channel, float oldValue, float newValue)
    {
        if (Math.Abs(oldValue - newValue) < 0.001f) return;
        Execute(new FaderChangeCommand(channel, oldValue, newValue));
    }

    /// <summary>
    /// Records a pan change.
    /// </summary>
    public void RecordPanChange(MixerChannel channel, float oldValue, float newValue)
    {
        if (Math.Abs(oldValue - newValue) < 0.001f) return;
        Execute(new PanChangeCommand(channel, oldValue, newValue));
    }

    /// <summary>
    /// Records a mute toggle.
    /// </summary>
    public void RecordMuteToggle(MixerChannel channel, bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;
        Execute(new MuteToggleCommand(channel, oldValue, newValue));
    }

    /// <summary>
    /// Records a solo toggle.
    /// </summary>
    public void RecordSoloToggle(MixerChannel channel, bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;
        Execute(new SoloToggleCommand(channel, oldValue, newValue));
    }

    /// <summary>
    /// Records an effect bypass toggle.
    /// </summary>
    public void RecordEffectBypassToggle(string channelName, EffectSlot slot, bool oldValue, bool newValue)
    {
        if (oldValue == newValue) return;
        Execute(new EffectBypassToggleCommand(channelName, slot, oldValue, newValue));
    }

    /// <summary>
    /// Records an effect parameter change.
    /// </summary>
    public void RecordEffectParameterChange(
        string channelName,
        EffectSlot slot,
        string parameterName,
        object oldValue,
        object newValue,
        Action<object> setter)
    {
        Execute(new EffectParameterChangeCommand(channelName, slot, parameterName, oldValue, newValue, setter));
    }

    /// <summary>
    /// Records a send level change.
    /// </summary>
    public void RecordSendLevelChange(string channelName, Send send, float oldValue, float newValue)
    {
        if (Math.Abs(oldValue - newValue) < 0.001f) return;
        Execute(new SendLevelChangeCommand(channelName, send, oldValue, newValue));
    }

    /// <summary>
    /// Records a channel color change.
    /// </summary>
    public void RecordChannelColorChange(MixerChannel channel, string oldColor, string newColor)
    {
        if (oldColor == newColor) return;
        Execute(new ChannelColorChangeCommand(channel, oldColor, newColor));
    }

    /// <summary>
    /// Records a channel rename.
    /// </summary>
    public void RecordChannelRename(MixerChannel channel, string oldName, string newName)
    {
        if (oldName == newName) return;
        Execute(new ChannelRenameCommand(channel, oldName, newName));
    }

    /// <summary>
    /// Undoes the most recent command.
    /// </summary>
    public bool Undo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_undoStack.Count == 0)
            return false;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        RaiseStateChanged();
        return true;
    }

    /// <summary>
    /// Redoes the most recently undone command.
    /// </summary>
    public bool Redo()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_redoStack.Count == 0)
            return false;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        RaiseStateChanged();
        return true;
    }

    /// <summary>
    /// Clears all undo and redo history.
    /// </summary>
    public void Clear()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _undoStack.Clear();
        _redoStack.Clear();

        RaiseStateChanged();
    }

    /// <summary>
    /// Performs multiple undos.
    /// </summary>
    public int UndoMultiple(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int undone = 0;
        for (int i = 0; i < count && Undo(); i++)
        {
            undone++;
        }
        return undone;
    }

    /// <summary>
    /// Performs multiple redos.
    /// </summary>
    public int RedoMultiple(int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int redone = 0;
        for (int i = 0; i < count && Redo(); i++)
        {
            redone++;
        }
        return redone;
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoCount));
        OnPropertyChanged(nameof(RedoCount));
        OnPropertyChanged(nameof(NextUndoDescription));
        OnPropertyChanged(nameof(NextRedoDescription));
        OnPropertyChanged(nameof(UndoHistory));
        OnPropertyChanged(nameof(RedoHistory));
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Disposes the service and clears history.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _undoStack.Clear();
        _redoStack.Clear();
        _disposed = true;
    }
}
