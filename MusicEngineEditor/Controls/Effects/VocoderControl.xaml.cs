// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Vocoder effect editor control with band visualization and parameter controls.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Effects.Special;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Vocoder effect editor control with band visualization, carrier selection,
/// formant shifting, and real-time band activity display.
/// </summary>
public partial class VocoderControl : UserControl
{
    #region Constants

    private const double BarSpacing = 2.0;
    private const double MinLevelDb = -60.0;

    #endregion

    #region Private Fields

    private EnhancedVocoder? _vocoder;
    private Shapes.Rectangle[]? _bandBars;
    private Shapes.Ellipse[]? _bandLeds;
    private float[]? _bandLevels;
    private float[]? _peakLevels;
    private int _currentBandCount = 16;
    private bool _isInitialized;
    private bool _isBypassed;
    private float _modulatorLevel;
    private float _modulatorPeak;
    private readonly DispatcherTimer _updateTimer;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty VocoderProperty =
        DependencyProperty.Register(nameof(Vocoder), typeof(EnhancedVocoder), typeof(VocoderControl),
            new PropertyMetadata(null, OnVocoderChanged));

    /// <summary>
    /// Gets or sets the EnhancedVocoder instance to control.
    /// </summary>
    public EnhancedVocoder? Vocoder
    {
        get => (EnhancedVocoder?)GetValue(VocoderProperty);
        set => SetValue(VocoderProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a vocoder parameter changes.
    /// </summary>
    public event EventHandler<VocoderParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Event raised when the carrier type changes.
    /// </summary>
    public event EventHandler<VocoderCarrierType>? CarrierTypeChanged;

    /// <summary>
    /// Event raised when the band count changes.
    /// </summary>
    public event EventHandler<VocoderBandCount>? BandCountChanged;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
            UpdateActiveIndicator();
        }
    }

    /// <summary>
    /// Gets or sets the carrier frequency in Hz.
    /// </summary>
    public float CarrierFrequency
    {
        get => (float)CarrierFrequencySlider.Value;
        set => CarrierFrequencySlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the formant shift in semitones (-12 to +12).
    /// </summary>
    public float FormantShift
    {
        get => (float)FormantShiftSlider.Value;
        set => FormantShiftSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the sibilance preservation amount (0-100%).
    /// </summary>
    public float Sibilance
    {
        get => (float)SibilanceSlider.Value / 100f;
        set => SibilanceSlider.Value = value * 100;
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public float AttackMs
    {
        get => (float)AttackSlider.Value;
        set => AttackSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public float ReleaseMs
    {
        get => (float)ReleaseSlider.Value;
        set => ReleaseSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the output gain (0-200%).
    /// </summary>
    public float OutputGain
    {
        get => (float)OutputGainSlider.Value / 100f;
        set => OutputGainSlider.Value = value * 100;
    }

    /// <summary>
    /// Gets or sets the wet/dry mix (0-100%).
    /// </summary>
    public float Mix
    {
        get => (float)MixSlider.Value / 100f;
        set => MixSlider.Value = value * 100;
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new vocoder control.
    /// </summary>
    public VocoderControl()
    {
        InitializeComponent();

        // Setup update timer for band visualization
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 fps
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildBandVisualization();
        _isInitialized = true;
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer.Stop();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateBandLayout();
        }
    }

    private static void OnVocoderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is VocoderControl control)
        {
            control._vocoder = e.NewValue as EnhancedVocoder;
            control.SyncFromVocoder();
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        UpdateBandVisualization();
        UpdateModulatorLevel();
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        UpdateActiveIndicator();
        BypassChanged?.Invoke(this, _isBypassed);

        // Bypass state is managed locally - vocoder processes based on this flag
    }

    private void CarrierType_Changed(object sender, RoutedEventArgs e)
    {
        var carrierType = GetSelectedCarrierType();

        // Show/hide carrier frequency panel based on carrier type
        CarrierFrequencyPanel.Visibility = carrierType == VocoderCarrierType.External
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (_vocoder != null)
        {
            _vocoder.CarrierType = carrierType;
        }

        CarrierTypeChanged?.Invoke(this, carrierType);
        RaiseParameterChanged("CarrierType", (int)carrierType);
    }

    private void CarrierFrequencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CarrierFrequencyValue == null) return;
        CarrierFrequencyValue.Text = $"{e.NewValue:0} Hz";

        if (_vocoder != null)
        {
            _vocoder.CarrierFrequency = (float)e.NewValue;
        }

        RaiseParameterChanged("CarrierFrequency", e.NewValue);
    }

    private void FormantShiftSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FormantShiftValue == null) return;
        var value = (int)e.NewValue;
        FormantShiftValue.Text = value >= 0 ? $"+{value} st" : $"{value} st";

        if (_vocoder != null)
        {
            _vocoder.FormantShift = (float)e.NewValue;
        }

        RaiseParameterChanged("FormantShift", e.NewValue);
    }

    private void BandCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BandCountComboBox.SelectedItem is not ComboBoxItem item) return;
        if (item.Tag is not string tagStr || !int.TryParse(tagStr, out int bandCount)) return;

        _currentBandCount = bandCount;
        BandCountValue.Text = $"{bandCount} bands";

        var vocoderBandCount = bandCount switch
        {
            8 => VocoderBandCount.Bands8,
            16 => VocoderBandCount.Bands16,
            32 => VocoderBandCount.Bands32,
            64 => VocoderBandCount.Bands64,
            _ => VocoderBandCount.Bands16
        };

        if (_vocoder != null)
        {
            _vocoder.BandCount = vocoderBandCount;
        }

        // Rebuild visualization for new band count
        BuildBandVisualization();

        BandCountChanged?.Invoke(this, vocoderBandCount);
        RaiseParameterChanged("BandCount", bandCount);
    }

    private void SibilanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SibilanceValue == null) return;
        SibilanceValue.Text = $"{e.NewValue:0}%";

        if (_vocoder != null)
        {
            _vocoder.Sibilance = (float)(e.NewValue / 100.0);
        }

        RaiseParameterChanged("Sibilance", e.NewValue / 100.0);
    }

    private void AttackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AttackValue == null) return;
        AttackValue.Text = $"{e.NewValue:0} ms";

        if (_vocoder != null)
        {
            _vocoder.Attack = (float)(e.NewValue / 1000.0); // Convert ms to seconds
        }

        RaiseParameterChanged("Attack", e.NewValue / 1000.0);
    }

    private void ReleaseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ReleaseValue == null) return;
        ReleaseValue.Text = $"{e.NewValue:0} ms";

        if (_vocoder != null)
        {
            _vocoder.Release = (float)(e.NewValue / 1000.0); // Convert ms to seconds
        }

        RaiseParameterChanged("Release", e.NewValue / 1000.0);
    }

    private void OutputGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OutputGainValue == null) return;
        OutputGainValue.Text = $"{e.NewValue:0}%";

        if (_vocoder != null)
        {
            _vocoder.OutputGain = (float)(e.NewValue / 100.0);
        }

        RaiseParameterChanged("OutputGain", e.NewValue / 100.0);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:0}%";

        if (_vocoder != null)
        {
            _vocoder.Mix = (float)(e.NewValue / 100.0);
        }

        RaiseParameterChanged("Mix", e.NewValue / 100.0);
    }

    #endregion

    #region Visualization

    private void BuildBandVisualization()
    {
        BandCanvas.Children.Clear();

        _bandBars = new Shapes.Rectangle[_currentBandCount];
        _bandLevels = new float[_currentBandCount];
        _peakLevels = new float[_currentBandCount];

        // Get the gradient brush for bars
        Brush barBrush = FindResource("VocoderBandGradientBrush") as Brush ?? Brushes.Cyan;

        // Create band bars
        for (int i = 0; i < _currentBandCount; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Fill = barBrush,
                RadiusX = 1,
                RadiusY = 1
            };
            _bandBars[i] = bar;
            BandCanvas.Children.Add(bar);
        }

        // Create LED indicators
        BuildBandLeds();

        UpdateBandLayout();
    }

    private void BuildBandLeds()
    {
        _bandLeds = new Shapes.Ellipse[_currentBandCount];

        var ledPanel = new StackPanel { Orientation = Orientation.Horizontal };

        Brush activeBrush = FindResource("VocoderLedActiveBrush") as Brush ?? Brushes.Cyan;
        Brush inactiveBrush = FindResource("VocoderLedInactiveBrush") as Brush ?? Brushes.DarkGray;

        for (int i = 0; i < _currentBandCount; i++)
        {
            var led = new Shapes.Ellipse
            {
                Width = Math.Max(4, 200.0 / _currentBandCount - 2),
                Height = 6,
                Fill = inactiveBrush,
                Margin = new Thickness(1, 0, 1, 0),
                ToolTip = GetBandFrequencyLabel(i)
            };
            _bandLeds[i] = led;
            ledPanel.Children.Add(led);
        }

        // Replace items in the LEDs display
        BandLedDisplay.Items.Clear();
        BandLedDisplay.ItemsSource = null;

        // Create a wrapper for custom LED display
        var wrapper = new ItemsControl();
        wrapper.Items.Add(ledPanel);
        BandLedDisplay.Items.Add(ledPanel);
    }

    private void UpdateBandLayout()
    {
        if (_bandBars == null) return;

        double width = BandCanvas.ActualWidth;
        double height = BandCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double barWidth = (width - (_currentBandCount - 1) * BarSpacing) / _currentBandCount;

        for (int i = 0; i < _currentBandCount; i++)
        {
            double x = i * (barWidth + BarSpacing);

            _bandBars[i].Width = Math.Max(1, barWidth);
            Canvas.SetLeft(_bandBars[i], x);
            Canvas.SetBottom(_bandBars[i], 0);
        }
    }

    private void UpdateBandVisualization()
    {
        if (_bandBars == null || _bandLevels == null || _bandLeds == null) return;

        double height = BandCanvas.ActualHeight;
        if (height <= 0) return;

        Brush activeBrush = FindResource("VocoderLedActiveBrush") as Brush ?? Brushes.Cyan;
        Brush inactiveBrush = FindResource("VocoderLedInactiveBrush") as Brush ?? Brushes.DarkGray;

        // Simulate band activity (in real usage, get from vocoder)
        var random = new Random();
        for (int i = 0; i < _currentBandCount; i++)
        {
            // Decay existing levels
            _bandLevels[i] *= 0.85f;

            // Add some random activity for visualization demo
            if (random.NextDouble() > 0.7)
            {
                _bandLevels[i] = Math.Max(_bandLevels[i], (float)(random.NextDouble() * 0.8 + 0.2));
            }

            // Update bar height
            double barHeight = height * Math.Clamp(_bandLevels[i], 0, 1);
            _bandBars[i].Height = Math.Max(0, barHeight);

            // Update LED
            bool isActive = _bandLevels[i] > 0.1f;
            _bandLeds[i].Fill = isActive ? activeBrush : inactiveBrush;
        }
    }

    private void UpdateModulatorLevel()
    {
        // Simulate modulator level (in real usage, get from vocoder)
        var random = new Random();
        float targetLevel = (float)(random.NextDouble() * 0.6 + 0.2);

        // Smooth the level
        _modulatorLevel = _modulatorLevel * 0.9f + targetLevel * 0.1f;

        // Track peak
        if (_modulatorLevel > _modulatorPeak)
        {
            _modulatorPeak = _modulatorLevel;
        }
        else
        {
            _modulatorPeak *= 0.995f; // Slow decay
        }

        // Update UI
        double meterWidth = ModulatorLevelBar.Parent is Grid grid ? grid.ActualWidth : 200;
        ModulatorLevelBar.Width = meterWidth * _modulatorLevel;

        // Update peak indicator
        if (_modulatorPeak > 0.01f)
        {
            ModulatorPeakIndicator.Visibility = Visibility.Visible;
            ModulatorPeakIndicator.Margin = new Thickness(meterWidth * _modulatorPeak - 1, 0, 0, 0);
        }
        else
        {
            ModulatorPeakIndicator.Visibility = Visibility.Collapsed;
        }

        // Update level text
        float levelDb = _modulatorLevel > 0.001f
            ? 20f * MathF.Log10(_modulatorLevel)
            : (float)MinLevelDb;
        ModulatorLevelText.Text = levelDb > MinLevelDb ? $"{levelDb:0.0} dB" : "-inf dB";
    }

    private void UpdateActiveIndicator()
    {
        ActiveIndicator.Fill = _isBypassed
            ? new SolidColorBrush(Color.FromRgb(128, 128, 128))
            : FindResource("VocoderSuccessBrush") as Brush ?? Brushes.Green;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the band levels from external data.
    /// </summary>
    /// <param name="levels">Array of band levels (0.0 to 1.0).</param>
    public void UpdateBandLevels(float[] levels)
    {
        if (_bandLevels == null) return;

        int count = Math.Min(levels.Length, _bandLevels.Length);
        for (int i = 0; i < count; i++)
        {
            _bandLevels[i] = levels[i];
        }
    }

    /// <summary>
    /// Updates the modulator input level.
    /// </summary>
    /// <param name="level">Level value (0.0 to 1.0).</param>
    public void SetModulatorLevel(float level)
    {
        _modulatorLevel = Math.Clamp(level, 0f, 1f);
    }

    /// <summary>
    /// Syncs the UI controls from the current vocoder settings.
    /// </summary>
    public void SyncFromVocoder()
    {
        if (_vocoder == null) return;

        // Carrier type
        switch (_vocoder.CarrierType)
        {
            case VocoderCarrierType.Sawtooth:
                CarrierSawtooth.IsChecked = true;
                break;
            case VocoderCarrierType.Square:
                CarrierSquare.IsChecked = true;
                break;
            case VocoderCarrierType.Noise:
                CarrierNoise.IsChecked = true;
                break;
            case VocoderCarrierType.Mixed:
                CarrierMixed.IsChecked = true;
                break;
            case VocoderCarrierType.External:
                CarrierExternal.IsChecked = true;
                break;
        }

        // Band count
        int bandCount = (int)_vocoder.BandCount;
        for (int i = 0; i < BandCountComboBox.Items.Count; i++)
        {
            if (BandCountComboBox.Items[i] is ComboBoxItem item &&
                item.Tag is string tagStr &&
                int.TryParse(tagStr, out int tag) &&
                tag == bandCount)
            {
                BandCountComboBox.SelectedIndex = i;
                break;
            }
        }

        // Parameters
        CarrierFrequencySlider.Value = _vocoder.CarrierFrequency;
        FormantShiftSlider.Value = _vocoder.FormantShift;
        SibilanceSlider.Value = _vocoder.Sibilance * 100;
        AttackSlider.Value = _vocoder.Attack * 1000; // Convert to ms
        ReleaseSlider.Value = _vocoder.Release * 1000; // Convert to ms
        OutputGainSlider.Value = _vocoder.OutputGain * 100;
        MixSlider.Value = _vocoder.Mix * 100;
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void Reset()
    {
        CarrierSawtooth.IsChecked = true;
        BandCountComboBox.SelectedIndex = 1; // 16 bands
        CarrierFrequencySlider.Value = 110;
        FormantShiftSlider.Value = 0;
        SibilanceSlider.Value = 50;
        AttackSlider.Value = 10;
        ReleaseSlider.Value = 100;
        OutputGainSlider.Value = 100;
        MixSlider.Value = 100;
        BypassToggle.IsChecked = false;
        _isBypassed = false;
        UpdateActiveIndicator();
    }

    #endregion

    #region Helper Methods

    private VocoderCarrierType GetSelectedCarrierType()
    {
        if (CarrierSawtooth.IsChecked == true) return VocoderCarrierType.Sawtooth;
        if (CarrierSquare.IsChecked == true) return VocoderCarrierType.Square;
        if (CarrierNoise.IsChecked == true) return VocoderCarrierType.Noise;
        if (CarrierMixed.IsChecked == true) return VocoderCarrierType.Mixed;
        if (CarrierExternal.IsChecked == true) return VocoderCarrierType.External;
        return VocoderCarrierType.Sawtooth;
    }

    private string GetBandFrequencyLabel(int bandIndex)
    {
        // Calculate approximate center frequency for the band
        float minFreq = 80f;
        float maxFreq = 12000f;
        float freqRatio = MathF.Pow(maxFreq / minFreq, 1f / (_currentBandCount - 1));
        float centerFreq = minFreq * MathF.Pow(freqRatio, bandIndex);

        return centerFreq >= 1000 ? $"{centerFreq / 1000:F1} kHz" : $"{centerFreq:F0} Hz";
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new VocoderParameterChangedEventArgs(parameterName, value));
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for vocoder parameter changes.
/// </summary>
public class VocoderParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public VocoderParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

#endregion

#region Converters

/// <summary>
/// Converter for band count to integer.
/// </summary>
public class VocoderBandCountToIntConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive
                ? new SolidColorBrush(Color.FromRgb(0, 217, 255)) // Accent color
                : new SolidColorBrush(Color.FromRgb(26, 42, 42)); // Inactive color
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for formant shift display.
/// </summary>
public class VocoderFormantShiftConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double shift)
        {
            int semitones = (int)shift;
            return semitones >= 0 ? $"+{semitones} st" : $"{semitones} st";
        }
        return "0 st";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for percentage display.
/// </summary>
public class VocoderPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double percent)
        {
            return $"{percent:0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for frequency display.
/// </summary>
public class VocoderFrequencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double freq)
        {
            return freq >= 1000 ? $"{freq / 1000:0.0} kHz" : $"{freq:0} Hz";
        }
        return "0 Hz";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for time display (milliseconds).
/// </summary>
public class VocoderTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double ms)
        {
            return ms >= 1000 ? $"{ms / 1000:0.0} s" : $"{ms:0} ms";
        }
        return "0 ms";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
