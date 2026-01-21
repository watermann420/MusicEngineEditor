using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A mixer channel strip control for audio mixing.
/// Features volume fader, pan control, mute/solo/arm buttons, and level metering.
/// Styled to match professional DAWs like Ableton Live and FL Studio.
/// </summary>
public partial class MixerChannelControl : UserControl
{
    #region Constants

    private const float UnityGainVolume = 0.8f;
    private const float CenterPan = 0f;
    private const float MinVolume = 0f;
    private const float MaxVolume = 1.25f;
    private const double MinDb = -60.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ChannelNameProperty =
        DependencyProperty.Register(nameof(ChannelName), typeof(string), typeof(MixerChannelControl),
            new PropertyMetadata("Channel"));

    public static readonly DependencyProperty ChannelColorProperty =
        DependencyProperty.Register(nameof(ChannelColor), typeof(Brush), typeof(MixerChannelControl),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF))));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(nameof(Volume), typeof(float), typeof(MixerChannelControl),
            new FrameworkPropertyMetadata(UnityGainVolume,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnVolumeChanged,
                CoerceVolume));

    public static readonly DependencyProperty VolumeDbProperty =
        DependencyProperty.Register(nameof(VolumeDb), typeof(float), typeof(MixerChannelControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty PanProperty =
        DependencyProperty.Register(nameof(Pan), typeof(float), typeof(MixerChannelControl),
            new FrameworkPropertyMetadata(CenterPan,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                null,
                CoercePan));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(MixerChannelControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsSoloedProperty =
        DependencyProperty.Register(nameof(IsSoloed), typeof(bool), typeof(MixerChannelControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty IsArmedProperty =
        DependencyProperty.Register(nameof(IsArmed), typeof(bool), typeof(MixerChannelControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty MeterLeftProperty =
        DependencyProperty.Register(nameof(MeterLeft), typeof(float), typeof(MixerChannelControl),
            new PropertyMetadata(0f));

    public static readonly DependencyProperty MeterRightProperty =
        DependencyProperty.Register(nameof(MeterRight), typeof(float), typeof(MixerChannelControl),
            new PropertyMetadata(0f));

    /// <summary>
    /// Gets or sets the channel name displayed at the top of the strip.
    /// </summary>
    public string ChannelName
    {
        get => (string)GetValue(ChannelNameProperty);
        set => SetValue(ChannelNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the channel color used for the name header.
    /// </summary>
    public Brush ChannelColor
    {
        get => (Brush)GetValue(ChannelColorProperty);
        set => SetValue(ChannelColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.25, where 0.8 is unity gain/0dB).
    /// </summary>
    public float Volume
    {
        get => (float)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    /// <summary>
    /// Gets the volume in decibels (read-only, calculated from Volume).
    /// </summary>
    public float VolumeDb
    {
        get => (float)GetValue(VolumeDbProperty);
        private set => SetValue(VolumeDbProperty, value);
    }

    /// <summary>
    /// Gets or sets the pan position (-1.0 = full left, 0.0 = center, 1.0 = full right).
    /// </summary>
    public float Pan
    {
        get => (float)GetValue(PanProperty);
        set => SetValue(PanProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the channel is muted.
    /// </summary>
    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the channel is soloed.
    /// </summary>
    public bool IsSoloed
    {
        get => (bool)GetValue(IsSoloedProperty);
        set => SetValue(IsSoloedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the channel is armed for recording.
    /// </summary>
    public bool IsArmed
    {
        get => (bool)GetValue(IsArmedProperty);
        set => SetValue(IsArmedProperty, value);
    }

    /// <summary>
    /// Gets or sets the left channel meter level (0.0 to 1.0+).
    /// </summary>
    public float MeterLeft
    {
        get => (float)GetValue(MeterLeftProperty);
        set => SetValue(MeterLeftProperty, value);
    }

    /// <summary>
    /// Gets or sets the right channel meter level (0.0 to 1.0+).
    /// </summary>
    public float MeterRight
    {
        get => (float)GetValue(MeterRightProperty);
        set => SetValue(MeterRightProperty, value);
    }

    #endregion

    #region Constructor

    public MixerChannelControl()
    {
        InitializeComponent();
        DataContext = this;

        // Initialize VolumeDb from default Volume
        UpdateVolumeDb(UnityGainVolume);
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixerChannelControl control)
        {
            control.UpdateVolumeDb((float)e.NewValue);
        }
    }

    private static object CoerceVolume(DependencyObject d, object baseValue)
    {
        float value = (float)baseValue;
        return Math.Clamp(value, MinVolume, MaxVolume);
    }

    private static object CoercePan(DependencyObject d, object baseValue)
    {
        float value = (float)baseValue;
        return Math.Clamp(value, -1f, 1f);
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Resets the volume fader to unity gain (0.8 / 0dB) on double-click.
    /// </summary>
    private void VolumeFader_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Volume = UnityGainVolume;
        e.Handled = true;
    }

    /// <summary>
    /// Resets the pan slider to center (0) on double-click.
    /// </summary>
    private void PanSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Pan = CenterPan;
        e.Handled = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets both meter levels at once for efficiency.
    /// </summary>
    /// <param name="left">Left channel level (0.0 to 1.0+)</param>
    /// <param name="right">Right channel level (0.0 to 1.0+)</param>
    public void SetMeterLevels(float left, float right)
    {
        MeterLeft = left;
        MeterRight = right;
    }

    /// <summary>
    /// Resets the channel to default values.
    /// </summary>
    public void Reset()
    {
        Volume = UnityGainVolume;
        Pan = CenterPan;
        IsMuted = false;
        IsSoloed = false;
        IsArmed = false;
        MeterLeft = 0f;
        MeterRight = 0f;
    }

    /// <summary>
    /// Resets the level meter clip indicators.
    /// </summary>
    public void ResetClipIndicators()
    {
        ChannelMeter?.ResetClipIndicators();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the VolumeDb property based on the current Volume.
    /// Unity gain (0.8) = 0dB, with range approximately -infinity to +2dB.
    /// </summary>
    private void UpdateVolumeDb(float volume)
    {
        if (volume <= 0)
        {
            VolumeDb = (float)MinDb;
            return;
        }

        // Unity gain is at 0.8, which should equal 0dB
        // Convert to dB relative to unity gain
        double db = 20.0 * Math.Log10(volume / UnityGainVolume);
        VolumeDb = (float)Math.Max(MinDb, db);
    }

    #endregion
}
