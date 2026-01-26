//MusicEngineEditor - Mixer ViewModel
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Commands;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the mixer view, managing multiple channel strips, bus channels,
/// send/return routing, and the master channel.
/// </summary>
public partial class MixerViewModel : ViewModelBase, IDisposable
{
    private readonly RecordingService _recordingService;
    private readonly MixerLayoutSettings _layoutSettings;
    private bool _disposed;

    /// <summary>
    /// Gets the collection of mixer channels.
    /// </summary>
    public ObservableCollection<MixerChannel> Channels { get; } = [];

    /// <summary>
    /// Gets the collection of bus channels (groups and aux).
    /// </summary>
    public ObservableCollection<BusChannel> Buses { get; } = [];

    /// <summary>
    /// Gets the collection of return channels.
    /// </summary>
    public ObservableCollection<ReturnChannel> Returns { get; } = [];

    /// <summary>
    /// Gets the collection of send/return configurations.
    /// </summary>
    public ObservableCollection<SendReturnConfiguration> SendReturnConfigs { get; } = [];

    /// <summary>
    /// Gets the master channel.
    /// </summary>
    [ObservableProperty]
    private MasterChannel _masterChannel = new();

    /// <summary>
    /// Gets or sets the currently selected channel.
    /// </summary>
    [ObservableProperty]
    private MixerChannel? _selectedChannel;

    /// <summary>
    /// Gets or sets the currently selected bus.
    /// </summary>
    [ObservableProperty]
    private BusChannel? _selectedBus;

    /// <summary>
    /// Gets or sets whether any channel is currently soloed.
    /// </summary>
    [ObservableProperty]
    private bool _hasSoloedChannel;

    /// <summary>
    /// Gets or sets whether any channel is currently muted.
    /// </summary>
    [ObservableProperty]
    private bool _hasMutedChannel;

    /// <summary>
    /// Gets or sets whether any bus is currently soloed.
    /// </summary>
    [ObservableProperty]
    private bool _hasSoloedBus;

    /// <summary>
    /// Gets or sets whether any channel is currently armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _hasArmedChannel;

    /// <summary>
    /// Gets or sets whether recording is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isRecording;

    /// <summary>
    /// Gets or sets the mixer is in narrow channel mode.
    /// </summary>
    [ObservableProperty]
    private bool _narrowMode;

    /// <summary>
    /// Gets or sets whether meters should show peak hold.
    /// </summary>
    [ObservableProperty]
    private bool _showPeakHold = true;

    /// <summary>
    /// Gets or sets the current mixer layout type.
    /// </summary>
    [ObservableProperty]
    private MixerLayoutType _currentLayout = MixerLayoutType.LargeFaders;

    /// <summary>
    /// Gets the available mixer layouts.
    /// </summary>
    public MixerLayoutType[] AvailableLayouts => MixerLayoutSettings.AvailableLayouts;

    /// <summary>
    /// Gets the current channel width based on layout.
    /// </summary>
    public double ChannelWidth => _layoutSettings.GetChannelWidth();

    /// <summary>
    /// Gets whether the current layout shows faders.
    /// </summary>
    public bool ShowFaders => CurrentLayout != MixerLayoutType.MetersOnly;

    /// <summary>
    /// Gets whether the current layout is large faders.
    /// </summary>
    public bool IsLargeFadersLayout => CurrentLayout == MixerLayoutType.LargeFaders;

    /// <summary>
    /// Gets whether the current layout is small faders.
    /// </summary>
    public bool IsSmallFadersLayout => CurrentLayout == MixerLayoutType.SmallFaders;

    /// <summary>
    /// Gets whether the current layout is meters only.
    /// </summary>
    public bool IsMetersOnlyLayout => CurrentLayout == MixerLayoutType.MetersOnly;

    /// <summary>
    /// Gets or sets whether to show the bus section.
    /// </summary>
    [ObservableProperty]
    private bool _showBuses = true;

    /// <summary>
    /// Gets or sets whether to show the sends section.
    /// </summary>
    [ObservableProperty]
    private bool _showSends = true;

    /// <summary>
    /// Gets the channel count.
    /// </summary>
    public int ChannelCount => Channels.Count;

    /// <summary>
    /// Gets the bus count.
    /// </summary>
    public int BusCount => Buses.Count;

    /// <summary>
    /// Gets the count of armed channels.
    /// </summary>
    public int ArmedChannelCount => Channels.Count(c => c.IsArmed);

    /// <summary>
    /// Gets the armed channels.
    /// </summary>
    public IEnumerable<MixerChannel> ArmedChannels => Channels.Where(c => c.IsArmed);

    /// <summary>
    /// Creates a new MixerViewModel with default channels.
    /// </summary>
    public MixerViewModel()
    {
        _recordingService = RecordingService.Instance;
        _layoutSettings = MixerLayoutSettings.Load();
        _currentLayout = _layoutSettings.CurrentLayout;

        // Subscribe to recording service events
        _recordingService.TrackArmed += OnTrackArmed;
        _recordingService.TrackDisarmed += OnTrackDisarmed;
        _recordingService.RecordingStarted += OnRecordingStarted;
        _recordingService.RecordingStopped += OnRecordingStopped;
        _recordingService.RecordingStateChanged += OnRecordingStateChanged;
        _recordingService.InputLevelsUpdated += OnInputLevelsUpdated;

        InitializeDefaultChannels();
        InitializeDefaultBuses();
    }

    partial void OnCurrentLayoutChanged(MixerLayoutType value)
    {
        _layoutSettings.CurrentLayout = value;
        _layoutSettings.Save();

        // Notify dependent properties
        OnPropertyChanged(nameof(ChannelWidth));
        OnPropertyChanged(nameof(ShowFaders));
        OnPropertyChanged(nameof(IsLargeFadersLayout));
        OnPropertyChanged(nameof(IsSmallFadersLayout));
        OnPropertyChanged(nameof(IsMetersOnlyLayout));
    }

    /// <summary>
    /// Sets the mixer layout.
    /// </summary>
    /// <param name="layout">The layout to set.</param>
    [RelayCommand]
    private void SetLayout(MixerLayoutType layout)
    {
        CurrentLayout = layout;
    }

    /// <summary>
    /// Initializes default mixer channels for demonstration.
    /// </summary>
    private void InitializeDefaultChannels()
    {
        var defaultChannels = new[]
        {
            new MixerChannel(0, "Kick") { Color = "#FF5555", Volume = 0.85f },
            new MixerChannel(1, "Snare") { Color = "#55FF55", Volume = 0.75f },
            new MixerChannel(2, "Hi-Hat") { Color = "#5555FF", Volume = 0.6f },
            new MixerChannel(3, "Bass") { Color = "#FF9500", Volume = 0.9f },
            new MixerChannel(4, "Lead") { Color = "#FF55FF", Volume = 0.7f },
            new MixerChannel(5, "Pad") { Color = "#55FFFF", Volume = 0.65f },
            new MixerChannel(6, "FX") { Color = "#FFFF55", Volume = 0.5f },
            new MixerChannel(7, "Vox") { Color = "#AA55FF", Volume = 0.8f },
        };

        foreach (var channel in defaultChannels)
        {
            Channels.Add(channel);
        }
    }

    /// <summary>
    /// Initializes default bus channels.
    /// </summary>
    private void InitializeDefaultBuses()
    {
        // Create default group buses
        var drumBus = new BusChannel("Drums", BusChannelType.Group)
        {
            Color = "#FF5555"
        };

        var synthBus = new BusChannel("Synths", BusChannelType.Group)
        {
            Color = "#55FFFF"
        };

        Buses.Add(drumBus);
        Buses.Add(synthBus);

        // Create default aux buses for send effects
        var reverbBus = new BusChannel("Reverb", BusChannelType.Aux)
        {
            Color = "#3B82F6"
        };
        reverbBus.AddEffect("EnhancedReverbEffect", "Hall Reverb");

        var delayBus = new BusChannel("Delay", BusChannelType.Aux)
        {
            Color = "#8B5CF6"
        };
        delayBus.AddEffect("EnhancedDelayEffect", "Stereo Delay");

        Buses.Add(reverbBus);
        Buses.Add(delayBus);

        // Create return channels for aux buses
        Returns.Add(new ReturnChannel("Rev Return", reverbBus));
        Returns.Add(new ReturnChannel("Dly Return", delayBus));

        // Route some channels to the drum bus
        drumBus.AddRoutedChannel(0); // Kick
        drumBus.AddRoutedChannel(1); // Snare
        drumBus.AddRoutedChannel(2); // Hi-Hat

        // Route synth channels to synth bus
        synthBus.AddRoutedChannel(4); // Lead
        synthBus.AddRoutedChannel(5); // Pad

        // Add some default sends
        if (Channels.Count >= 5)
        {
            Channels[4].AddSend(reverbBus.Id, reverbBus.Name, 0.3f); // Lead -> Reverb
            Channels[5].AddSend(reverbBus.Id, reverbBus.Name, 0.5f); // Pad -> Reverb
            Channels[5].AddSend(delayBus.Id, delayBus.Name, 0.2f);   // Pad -> Delay
        }
    }

    /// <summary>
    /// Adds a new channel to the mixer.
    /// </summary>
    [RelayCommand]
    private void AddChannel()
    {
        var newChannel = new MixerChannel(Channels.Count, $"Ch {Channels.Count + 1}");
        Channels.Add(newChannel);
    }

    /// <summary>
    /// Removes the selected channel from the mixer.
    /// </summary>
    [RelayCommand]
    private void RemoveChannel()
    {
        if (SelectedChannel != null && Channels.Contains(SelectedChannel))
        {
            Channels.Remove(SelectedChannel);
            SelectedChannel = null;
        }
    }

    /// <summary>
    /// Resets all channels to default values.
    /// </summary>
    [RelayCommand]
    private void ResetAllChannels()
    {
        foreach (var channel in Channels)
        {
            channel.Reset();
        }
        MasterChannel.Reset();
        MasterChannel.Volume = 1.0f; // Master defaults to unity
        HasSoloedChannel = false;
    }

    /// <summary>
    /// Clears all solo states.
    /// </summary>
    [RelayCommand]
    private void ClearSolos()
    {
        foreach (var channel in Channels)
        {
            channel.IsSoloed = false;
        }
        HasSoloedChannel = false;
        UpdateEffectiveMuteStates();
    }

    /// <summary>
    /// Clears all mute states.
    /// </summary>
    [RelayCommand]
    private void ClearMutes()
    {
        foreach (var channel in Channels)
        {
            channel.IsMuted = false;
        }
        UpdateEffectiveMuteStates();
    }

    /// <summary>
    /// Updates the effective mute states based on solo/mute configuration.
    /// </summary>
    public void UpdateEffectiveMuteStates()
    {
        HasSoloedChannel = Channels.Any(c => c.IsSoloed);
        HasMutedChannel = Channels.Any(c => c.IsMuted);

        foreach (var channel in Channels)
        {
            channel.IsEffectivelyMuted = channel.IsMuted || (HasSoloedChannel && !channel.IsSoloed);
        }
    }

    /// <summary>
    /// Updates meter levels for a specific channel.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateChannelMeters(int channelIndex, float left, float right)
    {
        if (channelIndex >= 0 && channelIndex < Channels.Count)
        {
            Channels[channelIndex].UpdateMeters(left, right);
        }
    }

    /// <summary>
    /// Updates the master channel meter levels.
    /// </summary>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateMasterMeters(float left, float right)
    {
        MasterChannel.UpdateMeters(left, right);
    }

    /// <summary>
    /// Resets all peak hold indicators.
    /// </summary>
    [RelayCommand]
    private void ResetPeakHold()
    {
        // This would be called to reset peak indicators on all channels
        // Implementation depends on the meter control's peak reset logic
    }

    /// <summary>
    /// Adds a new group bus.
    /// </summary>
    [RelayCommand]
    private void AddGroupBus()
    {
        var newBus = new BusChannel($"Group {Buses.Count + 1}", BusChannelType.Group);
        Buses.Add(newBus);
        OnPropertyChanged(nameof(BusCount));
    }

    /// <summary>
    /// Adds a new aux bus for send effects.
    /// </summary>
    [RelayCommand]
    private void AddAuxBus()
    {
        var newBus = new BusChannel($"Aux {Buses.Count + 1}", BusChannelType.Aux);
        Buses.Add(newBus);

        // Also create a return channel
        var returnChannel = new ReturnChannel($"Aux {Buses.Count} Return", newBus);
        Returns.Add(returnChannel);

        OnPropertyChanged(nameof(BusCount));
    }

    /// <summary>
    /// Removes the selected bus.
    /// </summary>
    [RelayCommand]
    private void RemoveBus()
    {
        if (SelectedBus != null && Buses.Contains(SelectedBus))
        {
            // Remove associated return channel
            ReturnChannel? returnToRemove = null;
            foreach (var ret in Returns)
            {
                if (ret.SourceBusId == SelectedBus.Id)
                {
                    returnToRemove = ret;
                    break;
                }
            }

            if (returnToRemove != null)
            {
                Returns.Remove(returnToRemove);
            }

            // Remove sends to this bus from all channels
            foreach (var channel in Channels)
            {
                channel.RemoveSend(SelectedBus.Id);
            }

            Buses.Remove(SelectedBus);
            SelectedBus = null;
            OnPropertyChanged(nameof(BusCount));
        }
    }

    /// <summary>
    /// Routes a channel to a bus.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="busId">The target bus ID.</param>
    public void RouteChannelToBus(int channelIndex, string busId)
    {
        if (channelIndex < 0 || channelIndex >= Channels.Count)
            return;

        var channel = Channels[channelIndex];
        BusChannel? targetBus = null;

        foreach (var bus in Buses)
        {
            if (bus.Id == busId)
            {
                targetBus = bus;
                break;
            }
        }

        if (targetBus != null)
        {
            // Remove from previous bus
            foreach (var bus in Buses)
            {
                bus.RemoveRoutedChannel(channelIndex);
            }

            // Add to new bus
            targetBus.AddRoutedChannel(channelIndex);
            channel.SetOutputBus(busId, targetBus.Name);
        }
    }

    /// <summary>
    /// Routes a channel directly to master.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    public void RouteChannelToMaster(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= Channels.Count)
            return;

        var channel = Channels[channelIndex];

        // Remove from all buses
        foreach (var bus in Buses)
        {
            bus.RemoveRoutedChannel(channelIndex);
        }

        channel.SetOutputBus(null, "Master");
    }

    /// <summary>
    /// Adds a send from a channel to a bus.
    /// </summary>
    /// <param name="channelIndex">The channel index.</param>
    /// <param name="busId">The target bus ID.</param>
    /// <param name="level">The send level.</param>
    public void AddChannelSend(int channelIndex, string busId, float level = 0.5f)
    {
        if (channelIndex < 0 || channelIndex >= Channels.Count)
            return;

        BusChannel? targetBus = null;
        foreach (var bus in Buses)
        {
            if (bus.Id == busId)
            {
                targetBus = bus;
                break;
            }
        }

        if (targetBus != null)
        {
            Channels[channelIndex].AddSend(busId, targetBus.Name, level);
        }
    }

    /// <summary>
    /// Updates bus meter levels.
    /// </summary>
    /// <param name="busId">The bus ID.</param>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateBusMeters(string busId, float left, float right)
    {
        foreach (var bus in Buses)
        {
            if (bus.Id == busId)
            {
                bus.UpdateMeters(left, right);
                break;
            }
        }
    }

    /// <summary>
    /// Updates return channel meter levels.
    /// </summary>
    /// <param name="returnId">The return ID.</param>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateReturnMeters(string returnId, float left, float right)
    {
        foreach (var ret in Returns)
        {
            if (ret.Id == returnId)
            {
                ret.UpdateMeters(left, right);
                break;
            }
        }
    }

    /// <summary>
    /// Updates the effective mute states for buses based on solo configuration.
    /// </summary>
    public void UpdateBusEffectiveMuteStates()
    {
        HasSoloedBus = false;
        foreach (var bus in Buses)
        {
            if (bus.IsSoloed)
            {
                HasSoloedBus = true;
                break;
            }
        }

        foreach (var bus in Buses)
        {
            bus.IsEffectivelyMuted = bus.IsMuted || (HasSoloedBus && !bus.IsSoloed);
        }
    }

    /// <summary>
    /// Clears all bus solos.
    /// </summary>
    [RelayCommand]
    private void ClearBusSolos()
    {
        foreach (var bus in Buses)
        {
            bus.IsSoloed = false;
        }
        HasSoloedBus = false;
        UpdateBusEffectiveMuteStates();
    }

    /// <summary>
    /// Resets all buses to default values.
    /// </summary>
    [RelayCommand]
    private void ResetAllBuses()
    {
        foreach (var bus in Buses)
        {
            bus.Reset();
        }
        HasSoloedBus = false;
    }

    /// <summary>
    /// Gets available buses for routing (excluding the specified bus to prevent feedback).
    /// </summary>
    /// <param name="excludeBusId">The bus ID to exclude.</param>
    /// <returns>List of available buses.</returns>
    public IEnumerable<BusChannel> GetAvailableBusesForRouting(string? excludeBusId = null)
    {
        foreach (var bus in Buses)
        {
            if (bus.Id != excludeBusId)
            {
                yield return bus;
            }
        }
    }

    /// <summary>
    /// Gets available aux buses for sends.
    /// </summary>
    /// <returns>List of aux buses.</returns>
    public IEnumerable<BusChannel> GetAuxBuses()
    {
        foreach (var bus in Buses)
        {
            if (bus.BusType == BusChannelType.Aux)
            {
                yield return bus;
            }
        }
    }

    #region Undo/Redo Integration

    /// <summary>
    /// Gets the editor undo service.
    /// </summary>
    private EditorUndoService UndoService => EditorUndoService.Instance;

    /// <summary>
    /// Sets channel volume with undo support (for fader movement).
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    /// <param name="newVolume">The new volume value.</param>
    public void SetChannelVolumeWithUndo(MixerChannel channel, float newVolume)
    {
        var command = new MixerVolumeCommand(channel, newVolume);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets channel pan with undo support (for pan knob movement).
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    /// <param name="newPan">The new pan value.</param>
    public void SetChannelPanWithUndo(MixerChannel channel, float newPan)
    {
        var command = new MixerPanCommand(channel, newPan);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Toggles channel mute with undo support.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    [RelayCommand]
    private void ToggleMuteWithUndo(MixerChannel? channel)
    {
        if (channel == null) return;

        var command = new MixerMuteCommand(channel, !channel.IsMuted);
        UndoService.Execute(command);
        UpdateEffectiveMuteStates();
    }

    /// <summary>
    /// Toggles channel solo with undo support.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    [RelayCommand]
    private void ToggleSoloWithUndo(MixerChannel? channel)
    {
        if (channel == null) return;

        var command = new MixerSoloCommand(channel, !channel.IsSoloed);
        UndoService.Execute(command);
        UpdateEffectiveMuteStates();
    }

    /// <summary>
    /// Renames a channel with undo support.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    /// <param name="newName">The new name.</param>
    public void RenameChannelWithUndo(MixerChannel channel, string newName)
    {
        var command = new MixerChannelRenameCommand(channel, newName);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Changes channel color with undo support.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    /// <param name="newColor">The new color hex string.</param>
    public void SetChannelColorWithUndo(MixerChannel channel, string newColor)
    {
        var command = new MixerChannelColorCommand(channel, newColor);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets bus volume with undo support (for fader movement).
    /// </summary>
    /// <param name="bus">The bus channel.</param>
    /// <param name="newVolume">The new volume value.</param>
    public void SetBusVolumeWithUndo(BusChannel bus, float newVolume)
    {
        var command = new BusVolumeCommand(bus, newVolume);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets send level with undo support (for send knob movement).
    /// </summary>
    /// <param name="send">The send.</param>
    /// <param name="newLevel">The new level value.</param>
    public void SetSendLevelWithUndo(Send send, float newLevel)
    {
        var command = new SendLevelCommand(send, newLevel);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets master channel property with undo support.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="oldValue">The old value.</param>
    /// <param name="newValue">The new value.</param>
    public void SetMasterPropertyWithUndo(string propertyName, object? oldValue, object? newValue)
    {
        var command = new MasterChannelPropertyCommand(MasterChannel, propertyName, oldValue, newValue);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets master volume with undo support.
    /// </summary>
    /// <param name="newVolume">The new volume value.</param>
    public void SetMasterVolumeWithUndo(float newVolume)
    {
        var oldVolume = MasterChannel.Volume;
        SetMasterPropertyWithUndo(nameof(MasterChannel.Volume), oldVolume, newVolume);
    }

    #endregion

    #region Recording/Arm Functionality

    /// <summary>
    /// Event raised when a channel's armed state changes.
    /// </summary>
    public event EventHandler<MixerChannel>? ChannelArmedChanged;

    /// <summary>
    /// Toggles the armed state for a channel.
    /// </summary>
    /// <param name="channel">The mixer channel.</param>
    [RelayCommand]
    private void ToggleArm(MixerChannel? channel)
    {
        if (channel == null) return;

        if (IsRecording)
        {
            StatusMessage = "Cannot change arm state while recording";
            return;
        }

        channel.IsArmed = !channel.IsArmed;

        if (channel.IsArmed)
        {
            // Arm the track in the recording service
            _recordingService.ArmTrack(
                channel.Index,
                channel.Name,
                channel.Color,
                null, // Input source would come from track settings
                "Default Input");
        }
        else
        {
            // Disarm the track
            _recordingService.DisarmTrack(channel.Index);
        }

        UpdateArmedState();
        ChannelArmedChanged?.Invoke(this, channel);
    }

    /// <summary>
    /// Arms a channel for recording.
    /// </summary>
    /// <param name="channel">The channel to arm.</param>
    public void ArmChannel(MixerChannel channel)
    {
        if (channel == null || IsRecording) return;

        if (!channel.IsArmed)
        {
            channel.IsArmed = true;
            _recordingService.ArmTrack(
                channel.Index,
                channel.Name,
                channel.Color,
                null,
                "Default Input");
            UpdateArmedState();
            ChannelArmedChanged?.Invoke(this, channel);
        }
    }

    /// <summary>
    /// Disarms a channel from recording.
    /// </summary>
    /// <param name="channel">The channel to disarm.</param>
    public void DisarmChannel(MixerChannel channel)
    {
        if (channel == null || IsRecording) return;

        if (channel.IsArmed)
        {
            channel.IsArmed = false;
            _recordingService.DisarmTrack(channel.Index);
            UpdateArmedState();
            ChannelArmedChanged?.Invoke(this, channel);
        }
    }

    /// <summary>
    /// Arms all channels for recording.
    /// </summary>
    [RelayCommand]
    private void ArmAllChannels()
    {
        if (IsRecording) return;

        foreach (var channel in Channels)
        {
            if (!channel.IsArmed)
            {
                channel.IsArmed = true;
                _recordingService.ArmTrack(
                    channel.Index,
                    channel.Name,
                    channel.Color,
                    null,
                    "Default Input");
            }
        }

        UpdateArmedState();
        StatusMessage = $"Armed {Channels.Count} channels";
    }

    /// <summary>
    /// Disarms all channels from recording.
    /// </summary>
    [RelayCommand]
    private void DisarmAllChannels()
    {
        if (IsRecording) return;

        foreach (var channel in Channels)
        {
            if (channel.IsArmed)
            {
                channel.IsArmed = false;
                _recordingService.DisarmTrack(channel.Index);
            }
        }

        UpdateArmedState();
        StatusMessage = "All channels disarmed";
    }

    /// <summary>
    /// Updates the HasArmedChannel state.
    /// </summary>
    private void UpdateArmedState()
    {
        HasArmedChannel = Channels.Any(c => c.IsArmed);
        OnPropertyChanged(nameof(ArmedChannelCount));
        OnPropertyChanged(nameof(ArmedChannels));
    }

    /// <summary>
    /// Syncs channel armed states with the recording service.
    /// </summary>
    public void SyncArmedStatesFromService()
    {
        foreach (var channel in Channels)
        {
            channel.IsArmed = _recordingService.IsTrackArmed(channel.Index);
        }
        UpdateArmedState();
    }

    #endregion

    #region Recording Service Event Handlers

    private void OnTrackArmed(object? sender, TrackArmEventArgs e)
    {
        // Update channel armed state
        var channel = Channels.FirstOrDefault(c => c.Index == e.TrackId);
        if (channel != null && !channel.IsArmed)
        {
            channel.IsArmed = true;
            UpdateArmedState();
        }
    }

    private void OnTrackDisarmed(object? sender, TrackArmEventArgs e)
    {
        // Update channel armed state
        var channel = Channels.FirstOrDefault(c => c.Index == e.TrackId);
        if (channel != null && channel.IsArmed)
        {
            channel.IsArmed = false;
            UpdateArmedState();
        }
    }

    private void OnRecordingStarted(object? sender, RecordingEventArgs e)
    {
        IsRecording = true;
        StatusMessage = $"Recording {e.ArmedTracks.Count} track(s)";
    }

    private void OnRecordingStopped(object? sender, RecordingStoppedEventArgs e)
    {
        IsRecording = false;
        if (e.WasCancelled)
        {
            StatusMessage = "Recording cancelled";
        }
        else
        {
            StatusMessage = $"Recorded {e.RecordedClips.Count} clip(s)";
        }
    }

    private void OnRecordingStateChanged(object? sender, bool isRecording)
    {
        IsRecording = isRecording;
    }

    private void OnInputLevelsUpdated(object? sender, EventArgs e)
    {
        // Update meter levels for armed channels from recording service
        foreach (var channel in Channels.Where(c => c.IsArmed))
        {
            var armedTrack = _recordingService.GetArmedTrack(channel.Index);
            if (armedTrack != null)
            {
                // Use input level for armed channels
                channel.UpdateMeters(armedTrack.InputLevel, armedTrack.InputLevel);
            }
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the MixerViewModel.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        // Unsubscribe from recording service events
        _recordingService.TrackArmed -= OnTrackArmed;
        _recordingService.TrackDisarmed -= OnTrackDisarmed;
        _recordingService.RecordingStarted -= OnRecordingStarted;
        _recordingService.RecordingStopped -= OnRecordingStopped;
        _recordingService.RecordingStateChanged -= OnRecordingStateChanged;
        _recordingService.InputLevelsUpdated -= OnInputLevelsUpdated;
    }

    #endregion
}
