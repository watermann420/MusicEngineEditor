//MusicEngineEditor - Mixer Channel Model
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a single channel in the mixer with volume, pan, mute/solo, and metering.
/// </summary>
public partial class MixerChannel : ObservableObject
{
    private readonly int _index;

    /// <summary>
    /// Gets the channel index (0-based).
    /// </summary>
    public int Index => _index;

    /// <summary>
    /// Gets or sets the channel name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Channel";

    /// <summary>
    /// Gets or sets the channel color for visual identification.
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.0, can exceed for gain).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.8f;

    /// <summary>
    /// Gets or sets the volume in decibels (-60 to +12 dB).
    /// </summary>
    public float VolumeDb
    {
        get => Volume <= 0 ? -60f : (float)(20.0 * Math.Log10(Volume));
        set => Volume = value <= -60f ? 0f : (float)Math.Pow(10.0, value / 20.0);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    [ObservableProperty]
    private float _pan = 0f;

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSoloed;

    /// <summary>
    /// Gets or sets whether the channel is armed for recording.
    /// </summary>
    [ObservableProperty]
    private bool _isArmed;

    /// <summary>
    /// Gets or sets the current left channel meter level (0.0 to 1.0+).
    /// </summary>
    [ObservableProperty]
    private float _meterLeft;

    /// <summary>
    /// Gets or sets the current right channel meter level (0.0 to 1.0+).
    /// </summary>
    [ObservableProperty]
    private float _meterRight;

    /// <summary>
    /// Gets or sets the instrument/synth name associated with this channel.
    /// </summary>
    [ObservableProperty]
    private string? _instrumentName;

    /// <summary>
    /// Gets or sets whether this channel has an effect chain.
    /// </summary>
    [ObservableProperty]
    private bool _hasEffects;

    /// <summary>
    /// Gets or sets the number of effects in the chain.
    /// </summary>
    [ObservableProperty]
    private int _effectCount;

    /// <summary>
    /// Gets whether the channel is effectively muted (muted or other channels are soloed).
    /// </summary>
    [ObservableProperty]
    private bool _isEffectivelyMuted;

    /// <summary>
    /// Creates a new mixer channel.
    /// </summary>
    /// <param name="index">The channel index.</param>
    /// <param name="name">The channel name.</param>
    public MixerChannel(int index, string name)
    {
        _index = index;
        _name = name;
    }

    /// <summary>
    /// Creates a new mixer channel with default name.
    /// </summary>
    /// <param name="index">The channel index.</param>
    public MixerChannel(int index) : this(index, $"Ch {index + 1}")
    {
    }

    /// <summary>
    /// Resets the channel to default values.
    /// </summary>
    public void Reset()
    {
        Volume = 0.8f;
        Pan = 0f;
        IsMuted = false;
        IsSoloed = false;
        IsArmed = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }

    /// <summary>
    /// Updates the meter levels.
    /// </summary>
    /// <param name="left">Left channel level.</param>
    /// <param name="right">Right channel level.</param>
    public void UpdateMeters(float left, float right)
    {
        MeterLeft = left;
        MeterRight = right;
    }
}

/// <summary>
/// Represents the master channel with additional properties.
/// </summary>
public partial class MasterChannel : MixerChannel
{
    /// <summary>
    /// Gets or sets whether the limiter is enabled on the master.
    /// </summary>
    [ObservableProperty]
    private bool _limiterEnabled = true;

    /// <summary>
    /// Gets or sets the limiter ceiling in dB.
    /// </summary>
    [ObservableProperty]
    private float _limiterCeiling = -0.3f;

    /// <summary>
    /// Creates the master channel.
    /// </summary>
    public MasterChannel() : base(-1, "Master")
    {
        Color = "#FF9500";
        Volume = 1.0f;
    }
}
