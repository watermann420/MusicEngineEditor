// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Convolution Reverb Editor control with IR waveform display and parameter editing.

using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using MusicEngine.Core.Effects.TimeBased;
using NAudio.Wave;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Convolution Reverb Editor control with IR waveform display, file browser, and parameter controls.
/// </summary>
public partial class ConvolutionReverbControl : UserControl
{
    private ConvolutionReverb? _convolutionReverb;
    private float[]? _irSamples;
    private int _irSampleRate;
    private int _irChannels;
    private string? _loadedIRPath;
    private bool _isBypassed;
    private bool _isUpdatingFromEngine;

    // Stereo width (not directly in engine, applied as post-process)
    private float _stereoWidth = 1.0f;

    /// <summary>
    /// Event raised when a parameter value changes.
    /// </summary>
    public event EventHandler<ConvolutionReverbParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when an IR file is loaded.
    /// </summary>
    public event EventHandler<string>? IRLoaded;

    /// <summary>
    /// Event raised when IR preview is requested.
    /// </summary>
    public event EventHandler? PreviewRequested;

    /// <summary>
    /// Event raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Gets or sets the associated ConvolutionReverb engine instance.
    /// </summary>
    public ConvolutionReverb? ConvolutionReverb
    {
        get => _convolutionReverb;
        set
        {
            _convolutionReverb = value;
            UpdateUIFromEngine();
        }
    }

    /// <summary>
    /// Gets or sets the pre-delay in milliseconds (0-200).
    /// </summary>
    public double PreDelayMs
    {
        get => PreDelaySlider.Value;
        set => PreDelaySlider.Value = Math.Clamp(value, 0, 200);
    }

    /// <summary>
    /// Gets or sets the decay time multiplier (0.5-2.0).
    /// </summary>
    public double DecayMultiplier
    {
        get => SliderToDecay(DecaySlider.Value);
        set => DecaySlider.Value = DecayToSlider(Math.Clamp(value, 0.5, 2.0));
    }

    /// <summary>
    /// Gets or sets the low cut frequency in Hz (20-2000).
    /// </summary>
    public double LoCutHz
    {
        get => SliderToLoCut(LoCutSlider.Value);
        set => LoCutSlider.Value = LoCutToSlider(Math.Clamp(value, 20, 2000));
    }

    /// <summary>
    /// Gets or sets the high cut frequency in Hz (1000-20000).
    /// </summary>
    public double HiCutHz
    {
        get => SliderToHiCut(HiCutSlider.Value);
        set => HiCutSlider.Value = HiCutToSlider(Math.Clamp(value, 1000, 20000));
    }

    /// <summary>
    /// Gets or sets the dry/wet mix (0-100%).
    /// </summary>
    public double DryWetPercent
    {
        get => DryWetSlider.Value;
        set => DryWetSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the stereo width (0-100%).
    /// </summary>
    public double StereoWidthPercent
    {
        get => StereoWidthSlider.Value;
        set => StereoWidthSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets the currently loaded IR file path.
    /// </summary>
    public string? LoadedIRPath => _loadedIRPath;

    /// <summary>
    /// Creates a new ConvolutionReverbControl.
    /// </summary>
    public ConvolutionReverbControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateUIFromEngine()
    {
        if (_convolutionReverb == null) return;

        _isUpdatingFromEngine = true;
        try
        {
            PreDelaySlider.Value = _convolutionReverb.PreDelay;
            DecaySlider.Value = DecayToSlider(_convolutionReverb.Decay);
            LoCutSlider.Value = LoCutToSlider(_convolutionReverb.LowCut);
            HiCutSlider.Value = HiCutToSlider(_convolutionReverb.HighCut);
            DryWetSlider.Value = _convolutionReverb.Mix * 100;

            UpdateValueDisplays();
        }
        finally
        {
            _isUpdatingFromEngine = false;
        }
    }

    private void UpdateValueDisplays()
    {
        PreDelayValue.Text = $"{PreDelaySlider.Value:0} ms";
        DecayValue.Text = $"{SliderToDecay(DecaySlider.Value):0.00}x";
        LoCutValue.Text = FormatFrequency(SliderToLoCut(LoCutSlider.Value));
        HiCutValue.Text = FormatFrequency(SliderToHiCut(HiCutSlider.Value));
        DryWetValue.Text = $"{DryWetSlider.Value:0}%";
        StereoWidthValue.Text = $"{StereoWidthSlider.Value:0}%";
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:0.0} kHz";
        return $"{hz:0} Hz";
    }

    // Decay slider conversion (0-100 -> 0.5-2.0)
    private static double SliderToDecay(double slider) => 0.5 + (slider / 100.0) * 1.5;
    private static double DecayToSlider(double decay) => ((decay - 0.5) / 1.5) * 100;

    // Lo-Cut slider conversion (0-100 -> 20-2000 Hz, logarithmic)
    private static double SliderToLoCut(double slider)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(2000);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double LoCutToSlider(double hz)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(2000);
        double logValue = Math.Log10(Math.Max(20, hz));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    // Hi-Cut slider conversion (0-100 -> 1000-20000 Hz, logarithmic)
    private static double SliderToHiCut(double slider)
    {
        double minLog = Math.Log10(1000);
        double maxLog = Math.Log10(20000);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double HiCutToSlider(double hz)
    {
        double minLog = Math.Log10(1000);
        double maxLog = Math.Log10(20000);
        double logValue = Math.Log10(Math.Max(1000, hz));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        BypassChanged?.Invoke(this, _isBypassed);
    }

    private void BrowseIRButton_Click(object sender, RoutedEventArgs e)
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Impulse Response File",
            Filter = "Audio Files (*.wav;*.aiff;*.flac)|*.wav;*.aiff;*.flac|WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (openFileDialog.ShowDialog() == true)
        {
            LoadIRFile(openFileDialog.FileName);
        }
    }

    /// <summary>
    /// Loads an impulse response from a file path.
    /// </summary>
    public void LoadIRFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                MessageBox.Show($"File not found: {filePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Load the IR file for waveform display
            using var reader = new AudioFileReader(filePath);
            _irSampleRate = reader.WaveFormat.SampleRate;
            _irChannels = reader.WaveFormat.Channels;

            int totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
            var buffer = new float[totalSamples];
            int samplesRead = reader.Read(buffer, 0, totalSamples);

            // Convert to mono for display
            int monoSamples = samplesRead / _irChannels;
            _irSamples = new float[monoSamples];
            for (int i = 0; i < monoSamples; i++)
            {
                float sum = 0;
                for (int c = 0; c < _irChannels; c++)
                {
                    int index = i * _irChannels + c;
                    if (index < samplesRead)
                        sum += buffer[index];
                }
                _irSamples[i] = sum / _irChannels;
            }

            _loadedIRPath = filePath;

            // Update UI
            IRFileNameText.Text = System.IO.Path.GetFileName(filePath);
            NoIRText.Visibility = Visibility.Collapsed;
            IRInfoPanel.Visibility = Visibility.Visible;
            PreviewIRButton.IsEnabled = true;

            // Update info text
            double durationSeconds = (double)monoSamples / _irSampleRate;
            IRInfoText.Text = $"{durationSeconds:0.00}s | {_irSampleRate / 1000.0:0.0}kHz | {(_irChannels == 1 ? "Mono" : "Stereo")}";

            // Draw waveform
            DrawIRWaveform();

            // Load into engine if available
            if (_convolutionReverb != null)
            {
                _convolutionReverb.LoadIR(filePath);
            }

            // Raise event
            IRLoaded?.Invoke(this, filePath);

            // Set preset to Custom
            SetPresetToCustom();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load IR file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PreviewIRButton_Click(object sender, RoutedEventArgs e)
    {
        PreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private void IRWaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawIRWaveform();
    }

    private void DrawIRWaveform()
    {
        if (_irSamples == null || _irSamples.Length == 0)
        {
            IRWaveformPath.Data = null;
            return;
        }

        double width = IRWaveformCanvas.ActualWidth;
        double height = IRWaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Update center line
        IRCenterLine.X1 = 0;
        IRCenterLine.X2 = width;
        IRCenterLine.Y1 = height / 2;
        IRCenterLine.Y2 = height / 2;

        // Calculate samples per pixel
        int samplesPerPixel = Math.Max(1, _irSamples.Length / (int)width);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            double centerY = height / 2;

            // Start at the first point
            ctx.BeginFigure(new Point(0, centerY), true, true);

            // Draw top half (positive values)
            for (int x = 0; x < (int)width; x++)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, _irSamples.Length);

                float maxValue = 0;
                for (int i = startSample; i < endSample; i++)
                {
                    float absValue = Math.Abs(_irSamples[i]);
                    if (absValue > maxValue)
                        maxValue = absValue;
                }

                double y = centerY - (maxValue * centerY * 0.9);
                ctx.LineTo(new Point(x, y), true, true);
            }

            // Draw bottom half (mirror)
            for (int x = (int)width - 1; x >= 0; x--)
            {
                int startSample = x * samplesPerPixel;
                int endSample = Math.Min(startSample + samplesPerPixel, _irSamples.Length);

                float maxValue = 0;
                for (int i = startSample; i < endSample; i++)
                {
                    float absValue = Math.Abs(_irSamples[i]);
                    if (absValue > maxValue)
                        maxValue = absValue;
                }

                double y = centerY + (maxValue * centerY * 0.9);
                ctx.LineTo(new Point(x, y), true, true);
            }
        }

        geometry.Freeze();
        IRWaveformPath.Data = geometry;
    }

    private void PreDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreDelayValue == null) return;

        PreDelayValue.Text = $"{e.NewValue:0} ms";

        if (!_isUpdatingFromEngine)
        {
            if (_convolutionReverb != null)
                _convolutionReverb.PreDelay = (float)e.NewValue;

            RaiseParameterChanged("PreDelay", e.NewValue);
        }
    }

    private void DecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DecayValue == null) return;

        double decay = SliderToDecay(e.NewValue);
        DecayValue.Text = $"{decay:0.00}x";

        if (!_isUpdatingFromEngine)
        {
            if (_convolutionReverb != null)
                _convolutionReverb.Decay = (float)decay;

            RaiseParameterChanged("Decay", decay);
        }
    }

    private void LoCutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LoCutValue == null) return;

        double freq = SliderToLoCut(e.NewValue);
        LoCutValue.Text = FormatFrequency(freq);

        if (!_isUpdatingFromEngine)
        {
            if (_convolutionReverb != null)
                _convolutionReverb.LowCut = (float)freq;

            RaiseParameterChanged("LoCut", freq);
        }
    }

    private void HiCutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HiCutValue == null) return;

        double freq = SliderToHiCut(e.NewValue);
        HiCutValue.Text = FormatFrequency(freq);

        if (!_isUpdatingFromEngine)
        {
            if (_convolutionReverb != null)
                _convolutionReverb.HighCut = (float)freq;

            RaiseParameterChanged("HiCut", freq);
        }
    }

    private void DryWetSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DryWetValue == null) return;

        DryWetValue.Text = $"{e.NewValue:0}%";

        if (!_isUpdatingFromEngine)
        {
            if (_convolutionReverb != null)
                _convolutionReverb.Mix = (float)(e.NewValue / 100.0);

            RaiseParameterChanged("DryWet", e.NewValue);
        }
    }

    private void StereoWidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (StereoWidthValue == null) return;

        StereoWidthValue.Text = $"{e.NewValue:0}%";
        _stereoWidth = (float)(e.NewValue / 100.0);

        if (!_isUpdatingFromEngine)
        {
            RaiseParameterChanged("StereoWidth", e.NewValue);
        }
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not ComboBoxItem item) return;

        var preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);
    }

    private void ApplyPreset(string preset)
    {
        _isUpdatingFromEngine = true;
        try
        {
            switch (preset)
            {
                case "Default":
                    PreDelaySlider.Value = 0;
                    DecaySlider.Value = 33.33; // 1.0x
                    LoCutSlider.Value = 0; // 20 Hz
                    HiCutSlider.Value = 100; // 20 kHz
                    DryWetSlider.Value = 30;
                    StereoWidthSlider.Value = 100;
                    break;

                case "Small Room":
                    PreDelaySlider.Value = 5;
                    DecaySlider.Value = 16.67; // 0.75x
                    LoCutSlider.Value = 20; // ~50 Hz
                    HiCutSlider.Value = 80; // ~8 kHz
                    DryWetSlider.Value = 25;
                    StereoWidthSlider.Value = 80;
                    break;

                case "Large Hall":
                    PreDelaySlider.Value = 25;
                    DecaySlider.Value = 50; // 1.25x
                    LoCutSlider.Value = 10; // ~30 Hz
                    HiCutSlider.Value = 90; // ~12 kHz
                    DryWetSlider.Value = 35;
                    StereoWidthSlider.Value = 100;
                    break;

                case "Plate":
                    PreDelaySlider.Value = 5;
                    DecaySlider.Value = 60; // 1.4x
                    LoCutSlider.Value = 30; // ~80 Hz
                    HiCutSlider.Value = 70; // ~5 kHz
                    DryWetSlider.Value = 40;
                    StereoWidthSlider.Value = 100;
                    break;

                case "Cathedral":
                    PreDelaySlider.Value = 50;
                    DecaySlider.Value = 100; // 2.0x
                    LoCutSlider.Value = 5; // ~25 Hz
                    HiCutSlider.Value = 85; // ~10 kHz
                    DryWetSlider.Value = 45;
                    StereoWidthSlider.Value = 100;
                    break;

                case "Bright Room":
                    PreDelaySlider.Value = 10;
                    DecaySlider.Value = 25; // 0.875x
                    LoCutSlider.Value = 40; // ~150 Hz
                    HiCutSlider.Value = 100; // 20 kHz
                    DryWetSlider.Value = 30;
                    StereoWidthSlider.Value = 90;
                    break;

                case "Dark Hall":
                    PreDelaySlider.Value = 30;
                    DecaySlider.Value = 66.67; // 1.5x
                    LoCutSlider.Value = 0; // 20 Hz
                    HiCutSlider.Value = 50; // ~2.5 kHz
                    DryWetSlider.Value = 40;
                    StereoWidthSlider.Value = 100;
                    break;
            }

            UpdateValueDisplays();
            UpdateEngineFromUI();
        }
        finally
        {
            _isUpdatingFromEngine = false;
        }
    }

    private void UpdateEngineFromUI()
    {
        if (_convolutionReverb == null) return;

        _convolutionReverb.PreDelay = (float)PreDelaySlider.Value;
        _convolutionReverb.Decay = (float)SliderToDecay(DecaySlider.Value);
        _convolutionReverb.LowCut = (float)SliderToLoCut(LoCutSlider.Value);
        _convolutionReverb.HighCut = (float)SliderToHiCut(HiCutSlider.Value);
        _convolutionReverb.Mix = (float)(DryWetSlider.Value / 100.0);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new ConvolutionReverbParameterChangedEventArgs(parameterName, value));
        SetPresetToCustom();
    }

    private void SetPresetToCustom()
    {
        if (PresetComboBox.SelectedItem is ComboBoxItem item &&
            item.Content?.ToString() != "Custom")
        {
            PresetComboBox.SelectionChanged -= PresetComboBox_SelectionChanged;
            PresetComboBox.SelectedIndex = PresetComboBox.Items.Count - 1; // Custom
            PresetComboBox.SelectionChanged += PresetComboBox_SelectionChanged;
        }
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        _isBypassed = false;
        BypassToggle.IsChecked = false;
        PresetComboBox.SelectedIndex = 0; // Default
        _irSamples = null;
        _loadedIRPath = null;
        IRFileNameText.Text = "Default IR";
        NoIRText.Visibility = Visibility.Visible;
        IRInfoPanel.Visibility = Visibility.Collapsed;
        PreviewIRButton.IsEnabled = false;
        IRWaveformPath.Data = null;
    }
}

/// <summary>
/// Event arguments for convolution reverb parameter changes.
/// </summary>
public class ConvolutionReverbParameterChangedEventArgs : EventArgs
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
    public ConvolutionReverbParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Converts slider value (0-100) to decay multiplier string (0.5x-2.0x).
/// </summary>
public class ConvolutionReverbDecayMultiplierConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double slider)
        {
            double decay = 0.5 + (slider / 100.0) * 1.5;
            return $"{decay:0.00}x";
        }
        return "1.00x";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts slider value to frequency string with Hz/kHz formatting.
/// </summary>
public class ConvolutionReverbFrequencyConverter : IValueConverter
{
    public double MinHz { get; set; } = 20;
    public double MaxHz { get; set; } = 20000;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double slider)
        {
            double minLog = Math.Log10(MinHz);
            double maxLog = Math.Log10(MaxHz);
            double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
            double hz = Math.Pow(10, logValue);

            if (hz >= 1000)
                return $"{hz / 1000:0.0} kHz";
            return $"{hz:0} Hz";
        }
        return "20 Hz";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts slider value to percentage string.
/// </summary>
public class ConvolutionReverbPercentConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double slider)
        {
            return $"{slider:0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts slider value to milliseconds string.
/// </summary>
public class ConvolutionReverbMsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double slider)
        {
            return $"{slider:0} ms";
        }
        return "0 ms";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
