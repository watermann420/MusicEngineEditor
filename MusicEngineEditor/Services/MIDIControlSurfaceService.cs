// MusicEngineEditor - MIDI Control Surface Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using NAudio.Midi;
using MusicEngine.Core;
using NoteEvent = NAudio.Midi.NoteEvent;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents a MIDI CC to parameter mapping.
/// </summary>
public class MidiCCMapping
{
    /// <summary>Unique identifier for this mapping.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>MIDI channel (0-15, or -1 for omni).</summary>
    public int Channel { get; set; } = -1;

    /// <summary>MIDI CC number (0-127).</summary>
    public int CCNumber { get; set; }

    /// <summary>Target parameter identifier.</summary>
    public string ParameterId { get; set; } = string.Empty;

    /// <summary>Descriptive name for UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Minimum output value.</summary>
    public float MinValue { get; set; }

    /// <summary>Maximum output value.</summary>
    public float MaxValue { get; set; } = 1f;

    /// <summary>Curve type for value scaling.</summary>
    public MidiMappingCurve Curve { get; set; } = MidiMappingCurve.Linear;

    /// <summary>Whether to send feedback to controller.</summary>
    public bool SendFeedback { get; set; } = true;

    /// <summary>Whether this mapping is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>14-bit mode (uses CC and CC+32).</summary>
    public bool Is14Bit { get; set; }

    /// <summary>Soft takeover mode (prevents jumps).</summary>
    public bool SoftTakeover { get; set; }

    /// <summary>Last known value for soft takeover.</summary>
    internal float LastValue { get; set; }

    /// <summary>Whether soft takeover has been activated.</summary>
    internal bool TakeoverActive { get; set; }
}

/// <summary>
/// Represents a MIDI note trigger mapping.
/// </summary>
public class MidiNoteMapping
{
    /// <summary>Unique identifier for this mapping.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>MIDI channel (0-15, or -1 for omni).</summary>
    public int Channel { get; set; } = -1;

    /// <summary>MIDI note number (0-127).</summary>
    public int NoteNumber { get; set; }

    /// <summary>Action to trigger.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Descriptive name for UI.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Trigger on note on only (true) or toggle (false).</summary>
    public bool MomentaryMode { get; set; } = true;

    /// <summary>Whether this mapping is enabled.</summary>
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Represents a MIDI control surface preset.
/// </summary>
public class MidiControlPreset
{
    /// <summary>Preset name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Preset description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Author name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Target device name.</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>CC mappings.</summary>
    public List<MidiCCMapping> CCMappings { get; set; } = new();

    /// <summary>Note trigger mappings.</summary>
    public List<MidiNoteMapping> NoteMappings { get; set; } = new();

    /// <summary>Date created.</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>Date modified.</summary>
    public DateTime Modified { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Event args for CC value changes.
/// </summary>
public class MidiCCValueEventArgs : EventArgs
{
    public MidiCCMapping Mapping { get; }
    public int RawValue { get; }
    public float ScaledValue { get; }
    public int DeviceIndex { get; }

    public MidiCCValueEventArgs(MidiCCMapping mapping, int rawValue, float scaledValue, int deviceIndex)
    {
        Mapping = mapping;
        RawValue = rawValue;
        ScaledValue = scaledValue;
        DeviceIndex = deviceIndex;
    }
}

/// <summary>
/// Event args for note trigger.
/// </summary>
public class MidiNoteTriggerEventArgs : EventArgs
{
    public MidiNoteMapping Mapping { get; }
    public bool IsPressed { get; }
    public int Velocity { get; }
    public int DeviceIndex { get; }

    public MidiNoteTriggerEventArgs(MidiNoteMapping mapping, bool isPressed, int velocity, int deviceIndex)
    {
        Mapping = mapping;
        IsPressed = isPressed;
        Velocity = velocity;
        DeviceIndex = deviceIndex;
    }
}

/// <summary>
/// MIDI device info for UI display.
/// </summary>
public record MidiDeviceInfo(int Index, string Name, bool IsInput);

/// <summary>
/// Service for generic MIDI control surface mapping with Learn mode.
/// Supports multiple devices, CC/Note mapping, and preset save/load.
/// </summary>
public sealed class MIDIControlSurfaceService : IDisposable
{
    #region Singleton

    private static readonly Lazy<MIDIControlSurfaceService> _instance = new(
        () => new MIDIControlSurfaceService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static MIDIControlSurfaceService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly Dictionary<int, MidiIn> _midiInputs = new();
    private readonly Dictionary<int, MidiOut> _midiOutputs = new();
    private readonly List<MidiCCMapping> _ccMappings = new();
    private readonly List<MidiNoteMapping> _noteMappings = new();
    private readonly Dictionary<string, float> _parameterValues = new();

    // 14-bit CC tracking (LSB values)
    private readonly Dictionary<(int channel, int cc), int> _lsbValues = new();

    // Learn mode state
    private bool _isLearning;
    private string? _learningParameterId;
    private float _learningMinValue;
    private float _learningMaxValue;
    private MidiMappingCurve _learningCurve = MidiMappingCurve.Linear;

    private bool _disposed;
    private readonly object _lock = new();

    private const float SoftTakeoverThreshold = 0.05f;

    #endregion

    #region Properties

    /// <summary>Gets whether any device is connected.</summary>
    public bool IsConnected => _midiInputs.Count > 0;

    /// <summary>Gets whether learn mode is active.</summary>
    public bool IsLearning => _isLearning;

    /// <summary>Gets the parameter being learned.</summary>
    public string? LearningParameter => _learningParameterId;

    /// <summary>Gets all CC mappings.</summary>
    public IReadOnlyList<MidiCCMapping> CCMappings
    {
        get
        {
            lock (_lock)
            {
                return _ccMappings.ToList();
            }
        }
    }

    /// <summary>Gets all note mappings.</summary>
    public IReadOnlyList<MidiNoteMapping> NoteMappings
    {
        get
        {
            lock (_lock)
            {
                return _noteMappings.ToList();
            }
        }
    }

    #endregion

    #region Events

    /// <summary>Raised when a CC value changes through a mapping.</summary>
    public event EventHandler<MidiCCValueEventArgs>? CCValueChanged;

    /// <summary>Raised when a note trigger fires.</summary>
    public event EventHandler<MidiNoteTriggerEventArgs>? NoteTriggerFired;

    /// <summary>Raised when a mapping is learned.</summary>
    public event EventHandler<MidiCCMapping>? MappingLearned;

    /// <summary>Raised when connection state changes.</summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>Raised when raw MIDI is received (for monitoring).</summary>
    public event EventHandler<(int channel, int cc, int value, int deviceIndex)>? RawCCReceived;

    /// <summary>Raised when raw MIDI note is received (for monitoring).</summary>
    public event EventHandler<(int channel, int note, int velocity, bool isNoteOn, int deviceIndex)>? RawNoteReceived;

    #endregion

    #region Constructor

    private MIDIControlSurfaceService()
    {
    }

    #endregion

    #region Device Discovery

    /// <summary>
    /// Gets all available MIDI input devices.
    /// </summary>
    public static IEnumerable<MidiDeviceInfo> GetInputDevices()
    {
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var info = MidiIn.DeviceInfo(i);
            yield return new MidiDeviceInfo(i, info.ProductName, true);
        }
    }

    /// <summary>
    /// Gets all available MIDI output devices.
    /// </summary>
    public static IEnumerable<MidiDeviceInfo> GetOutputDevices()
    {
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var info = MidiOut.DeviceInfo(i);
            yield return new MidiDeviceInfo(i, info.ProductName, false);
        }
    }

    #endregion

    #region Device Connection

    /// <summary>
    /// Opens a MIDI input device.
    /// </summary>
    public bool OpenInputDevice(int deviceIndex)
    {
        lock (_lock)
        {
            if (_midiInputs.ContainsKey(deviceIndex)) return true;

            try
            {
                var midiIn = new MidiIn(deviceIndex);
                midiIn.MessageReceived += (s, e) => OnMidiMessageReceived(e, deviceIndex);
                midiIn.Start();

                _midiInputs[deviceIndex] = midiIn;
                ConnectionStateChanged?.Invoke(this, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Opens a MIDI output device for feedback.
    /// </summary>
    public bool OpenOutputDevice(int deviceIndex)
    {
        lock (_lock)
        {
            if (_midiOutputs.ContainsKey(deviceIndex)) return true;

            try
            {
                var midiOut = new MidiOut(deviceIndex);
                _midiOutputs[deviceIndex] = midiOut;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Closes a MIDI input device.
    /// </summary>
    public void CloseInputDevice(int deviceIndex)
    {
        lock (_lock)
        {
            if (_midiInputs.TryGetValue(deviceIndex, out var midiIn))
            {
                midiIn.Stop();
                midiIn.Dispose();
                _midiInputs.Remove(deviceIndex);

                if (_midiInputs.Count == 0)
                {
                    ConnectionStateChanged?.Invoke(this, false);
                }
            }
        }
    }

    /// <summary>
    /// Closes a MIDI output device.
    /// </summary>
    public void CloseOutputDevice(int deviceIndex)
    {
        lock (_lock)
        {
            if (_midiOutputs.TryGetValue(deviceIndex, out var midiOut))
            {
                midiOut.Dispose();
                _midiOutputs.Remove(deviceIndex);
            }
        }
    }

    /// <summary>
    /// Closes all MIDI devices.
    /// </summary>
    public void CloseAllDevices()
    {
        lock (_lock)
        {
            foreach (var midiIn in _midiInputs.Values)
            {
                midiIn.Stop();
                midiIn.Dispose();
            }
            _midiInputs.Clear();

            foreach (var midiOut in _midiOutputs.Values)
            {
                midiOut.Dispose();
            }
            _midiOutputs.Clear();

            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    #endregion

    #region MIDI Processing

    private void OnMidiMessageReceived(MidiInMessageEventArgs e, int deviceIndex)
    {
        try
        {
            var message = e.MidiEvent;

            switch (message.CommandCode)
            {
                case MidiCommandCode.ControlChange:
                    var cc = (ControlChangeEvent)message;
                    ProcessCC(cc.Channel - 1, (int)cc.Controller, cc.ControllerValue, deviceIndex);
                    break;

                case MidiCommandCode.NoteOn:
                    var noteOn = (NoteEvent)message;
                    ProcessNote(noteOn.Channel - 1, noteOn.NoteNumber, noteOn.Velocity, true, deviceIndex);
                    break;

                case MidiCommandCode.NoteOff:
                    var noteOff = (NoteEvent)message;
                    ProcessNote(noteOff.Channel - 1, noteOff.NoteNumber, 0, false, deviceIndex);
                    break;
            }
        }
        catch { }
    }

    private void ProcessCC(int channel, int ccNumber, int value, int deviceIndex)
    {
        // Notify raw CC received
        RawCCReceived?.Invoke(this, (channel, ccNumber, value, deviceIndex));

        // Learn mode
        if (_isLearning && _learningParameterId != null)
        {
            LearnCC(channel, ccNumber);
            return;
        }

        // Process through mappings
        lock (_lock)
        {
            // Check for 14-bit LSB (CC 32-63 are LSB for CC 0-31)
            if (ccNumber >= 32 && ccNumber <= 63)
            {
                _lsbValues[(channel, ccNumber - 32)] = value;
                return;
            }

            foreach (var mapping in _ccMappings.Where(m => m.IsEnabled))
            {
                bool channelMatch = mapping.Channel == -1 || mapping.Channel == channel;
                bool ccMatch = mapping.CCNumber == ccNumber;

                if (channelMatch && ccMatch)
                {
                    int finalValue = value;

                    // 14-bit mode
                    if (mapping.Is14Bit && _lsbValues.TryGetValue((channel, ccNumber), out int lsb))
                    {
                        finalValue = (value << 7) | lsb;
                    }

                    float scaledValue = ScaleValue(finalValue, mapping);

                    // Soft takeover
                    if (mapping.SoftTakeover && !mapping.TakeoverActive)
                    {
                        float currentValue = _parameterValues.GetValueOrDefault(mapping.ParameterId, 0.5f);
                        float incomingNormalized = mapping.Is14Bit ? finalValue / 16383f : finalValue / 127f;

                        if (Math.Abs(incomingNormalized - currentValue) > SoftTakeoverThreshold)
                        {
                            return; // Ignore until value crosses current
                        }
                        mapping.TakeoverActive = true;
                    }

                    mapping.LastValue = scaledValue;
                    _parameterValues[mapping.ParameterId] = scaledValue;

                    CCValueChanged?.Invoke(this, new MidiCCValueEventArgs(mapping, finalValue, scaledValue, deviceIndex));
                }
            }
        }
    }

    private void ProcessNote(int channel, int noteNumber, int velocity, bool isNoteOn, int deviceIndex)
    {
        // Notify raw note received
        RawNoteReceived?.Invoke(this, (channel, noteNumber, velocity, isNoteOn, deviceIndex));

        // Learn mode for notes
        if (_isLearning && _learningParameterId != null && isNoteOn)
        {
            // Notes are not typically used for continuous parameters
            // but could trigger actions
            return;
        }

        // Process through note mappings
        lock (_lock)
        {
            foreach (var mapping in _noteMappings.Where(m => m.IsEnabled))
            {
                bool channelMatch = mapping.Channel == -1 || mapping.Channel == channel;
                bool noteMatch = mapping.NoteNumber == noteNumber;

                if (channelMatch && noteMatch)
                {
                    if (mapping.MomentaryMode)
                    {
                        // Fire on note on only
                        if (isNoteOn)
                        {
                            NoteTriggerFired?.Invoke(this, new MidiNoteTriggerEventArgs(mapping, true, velocity, deviceIndex));
                        }
                    }
                    else
                    {
                        // Toggle mode
                        NoteTriggerFired?.Invoke(this, new MidiNoteTriggerEventArgs(mapping, isNoteOn, velocity, deviceIndex));
                    }
                }
            }
        }
    }

    private float ScaleValue(int rawValue, MidiCCMapping mapping)
    {
        float maxRaw = mapping.Is14Bit ? 16383f : 127f;
        float normalized = rawValue / maxRaw;

        // Apply curve
        float curved = mapping.Curve switch
        {
            MidiMappingCurve.Linear => normalized,
            MidiMappingCurve.Exponential => MathF.Pow(normalized, 3f),
            MidiMappingCurve.Logarithmic => MathF.Pow(normalized, 1f / 3f),
            _ => normalized
        };

        return mapping.MinValue + (mapping.MaxValue - mapping.MinValue) * curved;
    }

    #endregion

    #region Learn Mode

    /// <summary>
    /// Starts learn mode for a parameter.
    /// </summary>
    public void StartLearning(string parameterId, float minValue = 0f, float maxValue = 1f, MidiMappingCurve curve = MidiMappingCurve.Linear)
    {
        lock (_lock)
        {
            _isLearning = true;
            _learningParameterId = parameterId;
            _learningMinValue = minValue;
            _learningMaxValue = maxValue;
            _learningCurve = curve;
        }
    }

    /// <summary>
    /// Cancels learn mode.
    /// </summary>
    public void CancelLearning()
    {
        lock (_lock)
        {
            _isLearning = false;
            _learningParameterId = null;
        }
    }

    private void LearnCC(int channel, int ccNumber)
    {
        lock (_lock)
        {
            if (!_isLearning || _learningParameterId == null) return;

            // Check if mapping already exists for this CC
            var existing = _ccMappings.FirstOrDefault(m => m.Channel == channel && m.CCNumber == ccNumber);
            if (existing != null)
            {
                // Update existing mapping
                existing.ParameterId = _learningParameterId;
                existing.MinValue = _learningMinValue;
                existing.MaxValue = _learningMaxValue;
                existing.Curve = _learningCurve;
            }
            else
            {
                // Create new mapping
                var mapping = new MidiCCMapping
                {
                    Channel = channel,
                    CCNumber = ccNumber,
                    ParameterId = _learningParameterId,
                    Name = $"CC{ccNumber} Ch{channel + 1}",
                    MinValue = _learningMinValue,
                    MaxValue = _learningMaxValue,
                    Curve = _learningCurve
                };
                _ccMappings.Add(mapping);
                MappingLearned?.Invoke(this, mapping);
            }

            // Exit learn mode
            _isLearning = false;
            _learningParameterId = null;
        }
    }

    #endregion

    #region Mapping Management

    /// <summary>
    /// Adds a CC mapping.
    /// </summary>
    public void AddCCMapping(MidiCCMapping mapping)
    {
        lock (_lock)
        {
            _ccMappings.Add(mapping);
        }
    }

    /// <summary>
    /// Adds a note mapping.
    /// </summary>
    public void AddNoteMapping(MidiNoteMapping mapping)
    {
        lock (_lock)
        {
            _noteMappings.Add(mapping);
        }
    }

    /// <summary>
    /// Removes a CC mapping by ID.
    /// </summary>
    public bool RemoveCCMapping(string id)
    {
        lock (_lock)
        {
            var mapping = _ccMappings.FirstOrDefault(m => m.Id == id);
            if (mapping != null)
            {
                return _ccMappings.Remove(mapping);
            }
            return false;
        }
    }

    /// <summary>
    /// Removes a note mapping by ID.
    /// </summary>
    public bool RemoveNoteMapping(string id)
    {
        lock (_lock)
        {
            var mapping = _noteMappings.FirstOrDefault(m => m.Id == id);
            if (mapping != null)
            {
                return _noteMappings.Remove(mapping);
            }
            return false;
        }
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearAllMappings()
    {
        lock (_lock)
        {
            _ccMappings.Clear();
            _noteMappings.Clear();
        }
    }

    /// <summary>
    /// Gets mappings for a parameter.
    /// </summary>
    public IEnumerable<MidiCCMapping> GetMappingsForParameter(string parameterId)
    {
        lock (_lock)
        {
            return _ccMappings.Where(m => m.ParameterId == parameterId).ToList();
        }
    }

    #endregion

    #region Feedback

    /// <summary>
    /// Sends CC feedback to all output devices.
    /// </summary>
    public void SendFeedback(int channel, int ccNumber, int value)
    {
        lock (_lock)
        {
            foreach (var midiOut in _midiOutputs.Values)
            {
                try
                {
                    var message = new ControlChangeEvent(0, channel + 1, (MidiController)ccNumber, value);
                    midiOut.Send(message.GetAsShortMessage());
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Sends feedback to a specific device.
    /// </summary>
    public void SendFeedback(int deviceIndex, int channel, int ccNumber, int value)
    {
        lock (_lock)
        {
            if (_midiOutputs.TryGetValue(deviceIndex, out var midiOut))
            {
                try
                {
                    var message = new ControlChangeEvent(0, channel + 1, (MidiController)ccNumber, value);
                    midiOut.Send(message.GetAsShortMessage());
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Updates a parameter and sends feedback to all mapped CCs.
    /// </summary>
    public void UpdateParameter(string parameterId, float value)
    {
        lock (_lock)
        {
            _parameterValues[parameterId] = value;

            foreach (var mapping in _ccMappings.Where(m => m.ParameterId == parameterId && m.SendFeedback))
            {
                // Convert value back to CC range
                float normalized = (value - mapping.MinValue) / (mapping.MaxValue - mapping.MinValue);
                normalized = Math.Clamp(normalized, 0f, 1f);

                int ccValue = (int)(normalized * (mapping.Is14Bit ? 16383 : 127));

                if (mapping.Is14Bit)
                {
                    int msb = ccValue >> 7;
                    int lsb = ccValue & 0x7F;
                    SendFeedback(mapping.Channel >= 0 ? mapping.Channel : 0, mapping.CCNumber, msb);
                    SendFeedback(mapping.Channel >= 0 ? mapping.Channel : 0, mapping.CCNumber + 32, lsb);
                }
                else
                {
                    SendFeedback(mapping.Channel >= 0 ? mapping.Channel : 0, mapping.CCNumber, ccValue);
                }

                mapping.TakeoverActive = false; // Reset soft takeover on feedback
            }
        }
    }

    /// <summary>
    /// Sends note on/off for button feedback (LEDs).
    /// </summary>
    public void SendNoteFeedback(int channel, int noteNumber, bool on)
    {
        lock (_lock)
        {
            foreach (var midiOut in _midiOutputs.Values)
            {
                try
                {
                    var message = new NoteOnEvent(0, channel + 1, noteNumber, on ? 127 : 0, 0);
                    midiOut.Send(message.GetAsShortMessage());
                }
                catch { }
            }
        }
    }

    #endregion

    #region Preset Save/Load

    /// <summary>
    /// Saves current mappings to a preset file.
    /// </summary>
    public void SavePreset(string filePath, string name, string description = "", string deviceName = "")
    {
        var preset = new MidiControlPreset
        {
            Name = name,
            Description = description,
            DeviceName = deviceName,
            Modified = DateTime.UtcNow
        };

        lock (_lock)
        {
            preset.CCMappings = _ccMappings.ToList();
            preset.NoteMappings = _noteMappings.ToList();
        }

        var json = JsonSerializer.Serialize(preset, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads mappings from a preset file.
    /// </summary>
    public MidiControlPreset? LoadPreset(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var preset = JsonSerializer.Deserialize<MidiControlPreset>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (preset != null)
            {
                ApplyPreset(preset);
            }

            return preset;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Applies a preset to current mappings.
    /// </summary>
    public void ApplyPreset(MidiControlPreset preset)
    {
        lock (_lock)
        {
            _ccMappings.Clear();
            _noteMappings.Clear();

            _ccMappings.AddRange(preset.CCMappings);
            _noteMappings.AddRange(preset.NoteMappings);
        }
    }

    /// <summary>
    /// Creates a preset from current mappings.
    /// </summary>
    public MidiControlPreset CreatePreset(string name, string description = "")
    {
        lock (_lock)
        {
            return new MidiControlPreset
            {
                Name = name,
                Description = description,
                CCMappings = _ccMappings.ToList(),
                NoteMappings = _noteMappings.ToList()
            };
        }
    }

    /// <summary>
    /// Gets built-in presets for common controllers.
    /// </summary>
    public static IEnumerable<MidiControlPreset> GetBuiltInPresets()
    {
        yield return CreateAkaiMPKPreset();
        yield return CreateNovationLaunchControlPreset();
        yield return CreateKorgNanoKontrolPreset();
        yield return CreateGenericFaderPreset();
    }

    private static MidiControlPreset CreateAkaiMPKPreset()
    {
        var preset = new MidiControlPreset
        {
            Name = "Akai MPK Mini",
            Description = "Default mapping for Akai MPK Mini controller",
            DeviceName = "MPK mini"
        };

        // 8 knobs on CC 1-8
        for (int i = 1; i <= 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = i,
                ParameterId = $"knob.{i}",
                Name = $"Knob {i}"
            });
        }

        return preset;
    }

    private static MidiControlPreset CreateNovationLaunchControlPreset()
    {
        var preset = new MidiControlPreset
        {
            Name = "Novation Launch Control",
            Description = "Default mapping for Novation Launch Control",
            DeviceName = "Launch Control"
        };

        // Top row knobs
        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = 21 + i,
                ParameterId = $"track.{i + 1}.send1",
                Name = $"Send 1 Ch {i + 1}"
            });
        }

        // Bottom row knobs
        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = 41 + i,
                ParameterId = $"track.{i + 1}.send2",
                Name = $"Send 2 Ch {i + 1}"
            });
        }

        // Faders
        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = 77 + i,
                ParameterId = $"track.{i + 1}.volume",
                Name = $"Volume Ch {i + 1}"
            });
        }

        return preset;
    }

    private static MidiControlPreset CreateKorgNanoKontrolPreset()
    {
        var preset = new MidiControlPreset
        {
            Name = "Korg nanoKONTROL2",
            Description = "Default mapping for Korg nanoKONTROL2",
            DeviceName = "nanoKONTROL2"
        };

        // Faders (default CC mapping)
        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = i,
                ParameterId = $"track.{i + 1}.volume",
                Name = $"Fader {i + 1}",
                SoftTakeover = true
            });
        }

        // Knobs
        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = 16 + i,
                ParameterId = $"track.{i + 1}.pan",
                Name = $"Knob {i + 1}",
                MinValue = -1f,
                MaxValue = 1f
            });
        }

        // Transport buttons (notes)
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 41, Action = "rewind", Name = "Rewind" });
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 42, Action = "forward", Name = "Forward" });
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 43, Action = "stop", Name = "Stop" });
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 44, Action = "play", Name = "Play" });
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 45, Action = "record", Name = "Record" });
        preset.NoteMappings.Add(new MidiNoteMapping { NoteNumber = 46, Action = "cycle", Name = "Cycle" });

        return preset;
    }

    private static MidiControlPreset CreateGenericFaderPreset()
    {
        var preset = new MidiControlPreset
        {
            Name = "Generic 8-Channel Mixer",
            Description = "Generic mapping for 8 faders on CC 0-7",
            DeviceName = "Generic"
        };

        for (int i = 0; i < 8; i++)
        {
            preset.CCMappings.Add(new MidiCCMapping
            {
                CCNumber = i,
                ParameterId = $"track.{i + 1}.volume",
                Name = $"Channel {i + 1} Volume"
            });
        }

        return preset;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        CloseAllDevices();
        ClearAllMappings();
    }

    #endregion
}
