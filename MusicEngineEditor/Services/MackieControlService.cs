// MusicEngineEditor - Mackie Control Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;
using MusicEngine.Core;
using NoteEvent = NAudio.Midi.NoteEvent;

namespace MusicEngineEditor.Services;

/// <summary>
/// Protocol type for control surface communication.
/// </summary>
public enum ControlSurfaceProtocol
{
    /// <summary>Mackie Control Universal protocol.</summary>
    MCU,
    /// <summary>Human User Interface protocol (Mackie HUI).</summary>
    HUI
}

/// <summary>
/// Represents a channel strip on the control surface.
/// </summary>
public class ChannelStrip
{
    /// <summary>Index of the channel (0-7 within current bank).</summary>
    public int Index { get; set; }

    /// <summary>Absolute track number in the mixer.</summary>
    public int TrackNumber { get; set; }

    /// <summary>Current fader value (0-16383 for 14-bit).</summary>
    public int FaderValue { get; set; }

    /// <summary>Current V-Pot value (0-127).</summary>
    public int VPotValue { get; set; }

    /// <summary>Whether the fader is currently touched.</summary>
    public bool FaderTouched { get; set; }

    /// <summary>Mute state.</summary>
    public bool IsMuted { get; set; }

    /// <summary>Solo state.</summary>
    public bool IsSolo { get; set; }

    /// <summary>Record arm state.</summary>
    public bool IsRecordArmed { get; set; }

    /// <summary>Select state.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Display name for the channel.</summary>
    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Event args for fader movement.
/// </summary>
public class FaderEventArgs : EventArgs
{
    public int Channel { get; }
    public int Value { get; }
    public bool IsTouched { get; }

    public FaderEventArgs(int channel, int value, bool isTouched)
    {
        Channel = channel;
        Value = value;
        IsTouched = isTouched;
    }
}

/// <summary>
/// Event args for V-Pot rotation.
/// </summary>
public class VPotEventArgs : EventArgs
{
    public int Channel { get; }
    public int Delta { get; }
    public bool Pressed { get; }

    public VPotEventArgs(int channel, int delta, bool pressed)
    {
        Channel = channel;
        Delta = delta;
        Pressed = pressed;
    }
}

/// <summary>
/// Event args for transport button presses.
/// </summary>
public class TransportButtonEventArgs : EventArgs
{
    public TransportButton Button { get; }
    public bool IsPressed { get; }

    public TransportButtonEventArgs(TransportButton button, bool isPressed)
    {
        Button = button;
        IsPressed = isPressed;
    }
}

/// <summary>
/// Transport button types.
/// </summary>
public enum TransportButton
{
    Play,
    Stop,
    Record,
    FastForward,
    Rewind,
    CycleLoop,
    Click,
    Solo,
    DropIn,
    DropOut,
    Scrub,
    Nudge
}

/// <summary>
/// Service for Mackie Control Universal (MCU) and HUI protocol support.
/// Provides bi-directional communication with hardware control surfaces.
/// </summary>
public sealed class MackieControlService : IDisposable
{
    #region Singleton

    private static readonly Lazy<MackieControlService> _instance = new(
        () => new MackieControlService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static MackieControlService Instance => _instance.Value;

    #endregion

    #region Constants - MCU

    // MCU MIDI Note numbers
    private const int MCU_REC_ARM_BASE = 0x00;      // 0-7 for channels 1-8
    private const int MCU_SOLO_BASE = 0x08;         // 8-15 for channels 1-8
    private const int MCU_MUTE_BASE = 0x10;         // 16-23 for channels 1-8
    private const int MCU_SELECT_BASE = 0x18;       // 24-31 for channels 1-8
    private const int MCU_VPOT_PUSH_BASE = 0x20;    // 32-39 for channels 1-8
    private const int MCU_FADER_TOUCH_BASE = 0x68;  // 104-111 for channels 1-8

    // Transport
    private const int MCU_REWIND = 0x5B;
    private const int MCU_FAST_FWD = 0x5C;
    private const int MCU_STOP = 0x5D;
    private const int MCU_PLAY = 0x5E;
    private const int MCU_RECORD = 0x5F;
    private const int MCU_CYCLE = 0x56;
    private const int MCU_CLICK = 0x59;
    private const int MCU_SOLO_DEFEAT = 0x5A;

    // Navigation
    private const int MCU_BANK_LEFT = 0x2E;
    private const int MCU_BANK_RIGHT = 0x2F;
    private const int MCU_CHANNEL_LEFT = 0x30;
    private const int MCU_CHANNEL_RIGHT = 0x31;

    // V-Pot CC base
    private const int MCU_VPOT_CC_BASE = 0x10; // CC 16-23 for V-Pots 1-8

    #endregion

    #region Private Fields

    private MidiIn? _midiIn;
    private MidiOut? _midiOut;
    private int _midiInDeviceIndex = -1;
    private int _midiOutDeviceIndex = -1;
    private ControlSurfaceProtocol _protocol = ControlSurfaceProtocol.MCU;
    private readonly ChannelStrip[] _channelStrips = new ChannelStrip[8];
    private int _currentBank;
    private int _totalTracks = 8;
    private bool _isConnected;
    private bool _disposed;
    private readonly object _lock = new();

    #endregion

    #region Properties

    /// <summary>Gets whether the service is connected to a device.</summary>
    public bool IsConnected => _isConnected;

    /// <summary>Gets or sets the protocol to use.</summary>
    public ControlSurfaceProtocol Protocol
    {
        get => _protocol;
        set => _protocol = value;
    }

    /// <summary>Gets the current bank offset (0 = tracks 1-8, 1 = tracks 9-16, etc).</summary>
    public int CurrentBank => _currentBank;

    /// <summary>Gets or sets the total number of tracks in the mixer.</summary>
    public int TotalTracks
    {
        get => _totalTracks;
        set => _totalTracks = Math.Max(1, value);
    }

    /// <summary>Gets the channel strips.</summary>
    public IReadOnlyList<ChannelStrip> ChannelStrips => _channelStrips;

    #endregion

    #region Events

    /// <summary>Raised when a fader is moved.</summary>
    public event EventHandler<FaderEventArgs>? FaderMoved;

    /// <summary>Raised when a V-Pot is rotated.</summary>
    public event EventHandler<VPotEventArgs>? VPotRotated;

    /// <summary>Raised when a transport button is pressed.</summary>
    public event EventHandler<TransportButtonEventArgs>? TransportButtonPressed;

    /// <summary>Raised when a channel mute button is pressed.</summary>
    public event EventHandler<(int Channel, bool IsMuted)>? MuteChanged;

    /// <summary>Raised when a channel solo button is pressed.</summary>
    public event EventHandler<(int Channel, bool IsSolo)>? SoloChanged;

    /// <summary>Raised when a channel record arm button is pressed.</summary>
    public event EventHandler<(int Channel, bool IsArmed)>? RecordArmChanged;

    /// <summary>Raised when a channel select button is pressed.</summary>
    public event EventHandler<(int Channel, bool IsSelected)>? SelectChanged;

    /// <summary>Raised when bank changes.</summary>
    public event EventHandler<int>? BankChanged;

    /// <summary>Raised on connection state change.</summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    #endregion

    #region Constructor

    private MackieControlService()
    {
        // Initialize channel strips
        for (int i = 0; i < 8; i++)
        {
            _channelStrips[i] = new ChannelStrip { Index = i, TrackNumber = i };
        }
    }

    #endregion

    #region Device Discovery

    /// <summary>
    /// Gets available MIDI input devices.
    /// </summary>
    public static IEnumerable<(int Index, string Name)> GetMidiInputDevices()
    {
        var devices = new List<(int, string)>();
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var info = MidiIn.DeviceInfo(i);
            devices.Add((i, info.ProductName));
        }
        return devices;
    }

    /// <summary>
    /// Gets available MIDI output devices.
    /// </summary>
    public static IEnumerable<(int Index, string Name)> GetMidiOutputDevices()
    {
        var devices = new List<(int, string)>();
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var info = MidiOut.DeviceInfo(i);
            devices.Add((i, info.ProductName));
        }
        return devices;
    }

    #endregion

    #region Connection

    /// <summary>
    /// Connects to MIDI devices.
    /// </summary>
    /// <param name="inputDeviceIndex">MIDI input device index.</param>
    /// <param name="outputDeviceIndex">MIDI output device index.</param>
    /// <returns>True if connected successfully.</returns>
    public bool Connect(int inputDeviceIndex, int outputDeviceIndex)
    {
        lock (_lock)
        {
            Disconnect();

            try
            {
                _midiInDeviceIndex = inputDeviceIndex;
                _midiOutDeviceIndex = outputDeviceIndex;

                // Open MIDI In
                if (inputDeviceIndex >= 0 && inputDeviceIndex < MidiIn.NumberOfDevices)
                {
                    _midiIn = new MidiIn(inputDeviceIndex);
                    _midiIn.MessageReceived += OnMidiMessageReceived;
                    _midiIn.Start();
                }

                // Open MIDI Out
                if (outputDeviceIndex >= 0 && outputDeviceIndex < MidiOut.NumberOfDevices)
                {
                    _midiOut = new MidiOut(outputDeviceIndex);
                }

                _isConnected = _midiIn != null && _midiOut != null;

                if (_isConnected)
                {
                    // Send reset/handshake
                    SendDeviceQuery();
                    RefreshAllLEDs();
                }

                ConnectionStateChanged?.Invoke(this, _isConnected);
                return _isConnected;
            }
            catch (Exception)
            {
                Disconnect();
                return false;
            }
        }
    }

    /// <summary>
    /// Disconnects from MIDI devices.
    /// </summary>
    public void Disconnect()
    {
        lock (_lock)
        {
            if (_midiIn != null)
            {
                _midiIn.Stop();
                _midiIn.MessageReceived -= OnMidiMessageReceived;
                _midiIn.Dispose();
                _midiIn = null;
            }

            if (_midiOut != null)
            {
                _midiOut.Dispose();
                _midiOut = null;
            }

            _isConnected = false;
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    #endregion

    #region MIDI Message Handling

    private void OnMidiMessageReceived(object? sender, MidiInMessageEventArgs e)
    {
        try
        {
            var message = e.MidiEvent;

            switch (_protocol)
            {
                case ControlSurfaceProtocol.MCU:
                    ProcessMcuMessage(message);
                    break;
                case ControlSurfaceProtocol.HUI:
                    ProcessHuiMessage(message);
                    break;
            }
        }
        catch (Exception)
        {
            // Ignore processing errors
        }
    }

    private void ProcessMcuMessage(MidiEvent message)
    {
        switch (message.CommandCode)
        {
            case MidiCommandCode.NoteOn:
                var noteOn = (NoteEvent)message;
                ProcessMcuButton(noteOn.NoteNumber, noteOn.Velocity > 0);
                break;

            case MidiCommandCode.ControlChange:
                var cc = (ControlChangeEvent)message;
                ProcessMcuControlChange((int)cc.Controller, cc.ControllerValue);
                break;

            case MidiCommandCode.PitchWheelChange:
                var pitch = (PitchWheelChangeEvent)message;
                ProcessMcuFader(pitch.Channel - 1, pitch.Pitch);
                break;

            case MidiCommandCode.ChannelAfterTouch:
                // Some controllers use aftertouch for fader touch
                break;
        }
    }

    private void ProcessMcuButton(int note, bool pressed)
    {
        // Channel strip buttons
        if (note >= MCU_REC_ARM_BASE && note < MCU_REC_ARM_BASE + 8)
        {
            int channel = note - MCU_REC_ARM_BASE;
            if (pressed)
            {
                var strip = _channelStrips[channel];
                strip.IsRecordArmed = !strip.IsRecordArmed;
                SendLED(note, strip.IsRecordArmed);
                RecordArmChanged?.Invoke(this, (_currentBank * 8 + channel, strip.IsRecordArmed));
            }
        }
        else if (note >= MCU_SOLO_BASE && note < MCU_SOLO_BASE + 8)
        {
            int channel = note - MCU_SOLO_BASE;
            if (pressed)
            {
                var strip = _channelStrips[channel];
                strip.IsSolo = !strip.IsSolo;
                SendLED(note, strip.IsSolo);
                SoloChanged?.Invoke(this, (_currentBank * 8 + channel, strip.IsSolo));
            }
        }
        else if (note >= MCU_MUTE_BASE && note < MCU_MUTE_BASE + 8)
        {
            int channel = note - MCU_MUTE_BASE;
            if (pressed)
            {
                var strip = _channelStrips[channel];
                strip.IsMuted = !strip.IsMuted;
                SendLED(note, strip.IsMuted);
                MuteChanged?.Invoke(this, (_currentBank * 8 + channel, strip.IsMuted));
            }
        }
        else if (note >= MCU_SELECT_BASE && note < MCU_SELECT_BASE + 8)
        {
            int channel = note - MCU_SELECT_BASE;
            if (pressed)
            {
                // Exclusive select
                for (int i = 0; i < 8; i++)
                {
                    _channelStrips[i].IsSelected = (i == channel);
                    SendLED(MCU_SELECT_BASE + i, i == channel);
                }
                SelectChanged?.Invoke(this, (_currentBank * 8 + channel, true));
            }
        }
        else if (note >= MCU_VPOT_PUSH_BASE && note < MCU_VPOT_PUSH_BASE + 8)
        {
            int channel = note - MCU_VPOT_PUSH_BASE;
            VPotRotated?.Invoke(this, new VPotEventArgs(channel, 0, pressed));
        }
        else if (note >= MCU_FADER_TOUCH_BASE && note < MCU_FADER_TOUCH_BASE + 8)
        {
            int channel = note - MCU_FADER_TOUCH_BASE;
            _channelStrips[channel].FaderTouched = pressed;
            FaderMoved?.Invoke(this, new FaderEventArgs(channel, _channelStrips[channel].FaderValue, pressed));
        }
        // Transport buttons
        else
        {
            TransportButton? button = note switch
            {
                MCU_REWIND => TransportButton.Rewind,
                MCU_FAST_FWD => TransportButton.FastForward,
                MCU_STOP => TransportButton.Stop,
                MCU_PLAY => TransportButton.Play,
                MCU_RECORD => TransportButton.Record,
                MCU_CYCLE => TransportButton.CycleLoop,
                MCU_CLICK => TransportButton.Click,
                MCU_SOLO_DEFEAT => TransportButton.Solo,
                _ => null
            };

            if (button.HasValue)
            {
                TransportButtonPressed?.Invoke(this, new TransportButtonEventArgs(button.Value, pressed));
            }

            // Bank switching
            if (pressed)
            {
                switch (note)
                {
                    case MCU_BANK_LEFT:
                        SwitchBank(_currentBank - 1);
                        break;
                    case MCU_BANK_RIGHT:
                        SwitchBank(_currentBank + 1);
                        break;
                    case MCU_CHANNEL_LEFT:
                        // Single channel nudge (implemented as bank -1)
                        SwitchBank(_currentBank - 1);
                        break;
                    case MCU_CHANNEL_RIGHT:
                        SwitchBank(_currentBank + 1);
                        break;
                }
            }
        }
    }

    private void ProcessMcuControlChange(int controller, int value)
    {
        // V-Pots (CC 16-23)
        if (controller >= MCU_VPOT_CC_BASE && controller < MCU_VPOT_CC_BASE + 8)
        {
            int channel = controller - MCU_VPOT_CC_BASE;

            // MCU encodes relative movement:
            // 0x01-0x0F = clockwise (delta = value)
            // 0x41-0x4F = counter-clockwise (delta = -(value - 0x40))
            int delta = value < 0x40 ? value : -(value - 0x40);

            var strip = _channelStrips[channel];
            strip.VPotValue = Math.Clamp(strip.VPotValue + delta, 0, 127);

            VPotRotated?.Invoke(this, new VPotEventArgs(channel, delta, false));
        }
    }

    private void ProcessMcuFader(int channel, int value)
    {
        if (channel >= 0 && channel < 8)
        {
            // MCU faders use 14-bit resolution (pitchbend)
            // value is already 0-16383
            _channelStrips[channel].FaderValue = value;
            FaderMoved?.Invoke(this, new FaderEventArgs(channel, value, _channelStrips[channel].FaderTouched));
        }
        else if (channel == 8)
        {
            // Master fader
            FaderMoved?.Invoke(this, new FaderEventArgs(8, value, false));
        }
    }

    private void ProcessHuiMessage(MidiEvent message)
    {
        // HUI uses a zone/port system
        // This is a simplified implementation
        switch (message.CommandCode)
        {
            case MidiCommandCode.ControlChange:
                var cc = (ControlChangeEvent)message;
                // HUI zone select (CC 0x0F) followed by port (CC 0x2F)
                // Full HUI implementation would track zone state
                break;
        }
    }

    #endregion

    #region Output Methods

    /// <summary>
    /// Sends an LED state to the control surface.
    /// </summary>
    public void SendLED(int note, bool on)
    {
        if (_midiOut == null) return;

        try
        {
            var message = new NoteOnEvent(0, 1, note, on ? 127 : 0, 0);
            _midiOut.Send(message.GetAsShortMessage());
        }
        catch { }
    }

    /// <summary>
    /// Sets a fader position (motor control).
    /// </summary>
    /// <param name="channel">Channel 0-7 (or 8 for master).</param>
    /// <param name="value">Fader value 0-16383 (14-bit).</param>
    public void SetFaderPosition(int channel, int value)
    {
        if (_midiOut == null) return;

        try
        {
            value = Math.Clamp(value, 0, 16383);
            var message = new PitchWheelChangeEvent(0, channel + 1, value);
            _midiOut.Send(message.GetAsShortMessage());

            if (channel >= 0 && channel < 8)
            {
                _channelStrips[channel].FaderValue = value;
            }
        }
        catch { }
    }

    /// <summary>
    /// Sets a V-Pot LED ring mode and value.
    /// </summary>
    /// <param name="channel">Channel 0-7.</param>
    /// <param name="mode">LED mode (0-3).</param>
    /// <param name="value">Position value (0-11).</param>
    public void SetVPotLED(int channel, int mode, int value)
    {
        if (_midiOut == null || channel < 0 || channel > 7) return;

        try
        {
            // MCU V-Pot LED ring: CC 48-55, value = (mode << 4) | position
            int cc = 0x30 + channel;
            int data = ((mode & 0x07) << 4) | (value & 0x0F);
            var message = new ControlChangeEvent(0, 1, (MidiController)cc, data);
            _midiOut.Send(message.GetAsShortMessage());
        }
        catch { }
    }

    /// <summary>
    /// Updates the scribble strip display text for a channel.
    /// </summary>
    /// <param name="channel">Channel 0-7.</param>
    /// <param name="topLine">Top line text (max 7 chars).</param>
    /// <param name="bottomLine">Bottom line text (max 7 chars).</param>
    public void SetDisplayText(int channel, string topLine, string bottomLine)
    {
        if (_midiOut == null || channel < 0 || channel > 7) return;

        try
        {
            // MCU LCD SysEx: F0 00 00 66 14 12 <offset> <text> F7
            // Each channel has 7 characters on each line

            int topOffset = channel * 7;
            int bottomOffset = 56 + channel * 7;

            topLine = (topLine ?? "").PadRight(7).Substring(0, 7);
            bottomLine = (bottomLine ?? "").PadRight(7).Substring(0, 7);

            // Top line
            var topSysex = new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x12, (byte)topOffset }
                .Concat(System.Text.Encoding.ASCII.GetBytes(topLine))
                .Concat(new byte[] { 0xF7 })
                .ToArray();

            // Bottom line
            var bottomSysex = new byte[] { 0xF0, 0x00, 0x00, 0x66, 0x14, 0x12, (byte)bottomOffset }
                .Concat(System.Text.Encoding.ASCII.GetBytes(bottomLine))
                .Concat(new byte[] { 0xF7 })
                .ToArray();

            // NAudio doesn't directly support SysEx via MidiOut, would need raw byte sending
            // For now, store the display name
            _channelStrips[channel].DisplayName = $"{topLine.Trim()}\n{bottomLine.Trim()}";
        }
        catch { }
    }

    /// <summary>
    /// Updates the timecode/BBT display.
    /// </summary>
    /// <param name="text">Display text (max 10 chars).</param>
    public void SetTimecodeDisplay(string text)
    {
        if (_midiOut == null) return;

        // MCU timecode display uses CC 0x40-0x49 for digit segments
        text = (text ?? "").PadRight(10).Substring(0, 10);

        try
        {
            for (int i = 0; i < 10; i++)
            {
                char c = text[9 - i]; // Rightmost digit first
                int segment = CharToSegment(c);
                var message = new ControlChangeEvent(0, 1, (MidiController)(0x40 + i), segment);
                _midiOut.Send(message.GetAsShortMessage());
            }
        }
        catch { }
    }

    private static int CharToSegment(char c)
    {
        // Simple 7-segment encoding for digits
        return c switch
        {
            '0' => 0x30,
            '1' => 0x31,
            '2' => 0x32,
            '3' => 0x33,
            '4' => 0x34,
            '5' => 0x35,
            '6' => 0x36,
            '7' => 0x37,
            '8' => 0x38,
            '9' => 0x39,
            ':' => 0x3A,
            '.' => 0x2E,
            ' ' => 0x20,
            '-' => 0x2D,
            _ => 0x20
        };
    }

    private void SendDeviceQuery()
    {
        if (_midiOut == null) return;

        // MCU Device Query SysEx
        try
        {
            // Basic handshake - would send device query in real implementation
        }
        catch { }
    }

    private void RefreshAllLEDs()
    {
        for (int i = 0; i < 8; i++)
        {
            var strip = _channelStrips[i];
            SendLED(MCU_REC_ARM_BASE + i, strip.IsRecordArmed);
            SendLED(MCU_SOLO_BASE + i, strip.IsSolo);
            SendLED(MCU_MUTE_BASE + i, strip.IsMuted);
            SendLED(MCU_SELECT_BASE + i, strip.IsSelected);
        }
    }

    #endregion

    #region Bank Switching

    /// <summary>
    /// Switches to a different bank of channels.
    /// </summary>
    /// <param name="bank">Bank number (0 = tracks 1-8, 1 = tracks 9-16, etc).</param>
    public void SwitchBank(int bank)
    {
        int maxBank = (_totalTracks - 1) / 8;
        bank = Math.Clamp(bank, 0, maxBank);

        if (bank == _currentBank) return;

        _currentBank = bank;

        // Update track numbers for each strip
        for (int i = 0; i < 8; i++)
        {
            _channelStrips[i].TrackNumber = _currentBank * 8 + i;
        }

        RefreshAllLEDs();
        BankChanged?.Invoke(this, _currentBank);
    }

    #endregion

    #region Mixer Integration

    /// <summary>
    /// Updates channel strip state from mixer.
    /// </summary>
    public void UpdateChannelState(int channel, float volume, float pan, bool mute, bool solo, bool recordArm, string name)
    {
        int localChannel = channel - (_currentBank * 8);
        if (localChannel < 0 || localChannel >= 8) return;

        var strip = _channelStrips[localChannel];

        // Convert volume (0-1) to fader (0-16383)
        int faderValue = (int)(volume * 16383);
        if (Math.Abs(strip.FaderValue - faderValue) > 100 && !strip.FaderTouched)
        {
            SetFaderPosition(localChannel, faderValue);
        }

        // Convert pan (-1 to 1) to V-Pot (0-127)
        int panValue = (int)((pan + 1) * 63.5f);
        strip.VPotValue = panValue;
        SetVPotLED(localChannel, 1, panValue / 11); // Mode 1 = single dot

        // Update button states
        if (strip.IsMuted != mute)
        {
            strip.IsMuted = mute;
            SendLED(MCU_MUTE_BASE + localChannel, mute);
        }

        if (strip.IsSolo != solo)
        {
            strip.IsSolo = solo;
            SendLED(MCU_SOLO_BASE + localChannel, solo);
        }

        if (strip.IsRecordArmed != recordArm)
        {
            strip.IsRecordArmed = recordArm;
            SendLED(MCU_REC_ARM_BASE + localChannel, recordArm);
        }

        // Update display
        string volDb = $"{(20 * Math.Log10(volume + 0.001)):F1}dB";
        SetDisplayText(localChannel, name, volDb);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Disconnect();
    }

    #endregion
}
