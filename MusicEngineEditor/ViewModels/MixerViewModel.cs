//MusicEngineEditor - Mixer ViewModel
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the mixer view, managing multiple channel strips and the master channel.
/// </summary>
public partial class MixerViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the collection of mixer channels.
    /// </summary>
    public ObservableCollection<MixerChannel> Channels { get; } = new();

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
    /// Gets or sets whether any channel is currently soloed.
    /// </summary>
    [ObservableProperty]
    private bool _hasSoloedChannel;

    /// <summary>
    /// Gets or sets whether the mixer is in narrow channel mode.
    /// </summary>
    [ObservableProperty]
    private bool _narrowMode;

    /// <summary>
    /// Gets or sets whether meters should show peak hold.
    /// </summary>
    [ObservableProperty]
    private bool _showPeakHold = true;

    /// <summary>
    /// Creates a new MixerViewModel with default channels.
    /// </summary>
    public MixerViewModel()
    {
        InitializeDefaultChannels();
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
}
