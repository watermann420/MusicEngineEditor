// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using Control = System.Windows.Controls.Control;
using CheckBox = System.Windows.Controls.CheckBox;
using ComboBox = System.Windows.Controls.ComboBox;
using HorizontalAlignment = System.Windows.HorizontalAlignment;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Processing mode for AudioSuite.
/// </summary>
public enum AudioSuiteProcessingMode
{
    ClipByClip,
    RegionByRegion,
    EntireSelection
}

/// <summary>
/// Effect parameter definition.
/// </summary>
public class AudioSuiteParameter
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public double Value { get; set; }
    public double MinValue { get; set; }
    public double MaxValue { get; set; } = 1.0;
    public double DefaultValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public ParameterType Type { get; set; } = ParameterType.Linear;

    public enum ParameterType
    {
        Linear,
        Logarithmic,
        Exponential,
        Integer,
        Boolean,
        Choice
    }

    public List<string>? Choices { get; set; }
}

/// <summary>
/// AudioSuite effect definition.
/// </summary>
public class AudioSuiteEffect
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<AudioSuiteParameter> Parameters { get; set; } = [];
    public int LatencySamples { get; set; }
    public bool SupportsPreview { get; set; } = true;
}

/// <summary>
/// Result of AudioSuite processing.
/// </summary>
public class AudioSuiteResult
{
    public bool Success { get; set; }
    public string EffectId { get; set; } = string.Empty;
    public Dictionary<string, double> ParameterValues { get; set; } = [];
    public AudioSuiteProcessingMode ProcessingMode { get; set; }
    public bool CreateNewFile { get; set; }
    public float[]? ProcessedSamples { get; set; }
}

/// <summary>
/// Dialog for offline audio processing with preview.
/// </summary>
public partial class AudioSuiteDialog : Window
{
    #region Private Fields

    private AudioSuiteEffect? _selectedEffect;
    private readonly Dictionary<string, Control> _parameterControls = [];
    private float[]? _originalSamples;
    private float[]? _processedSamples;
    private int _sampleRate = 44100;
    private int _channels = 2;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isPreviewing;
#pragma warning restore CS0414
    private CancellationTokenSource? _previewCts;
    private bool _isComparing;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the original audio samples.
    /// </summary>
    public float[]? OriginalSamples
    {
        get => _originalSamples;
        set
        {
            _originalSamples = value;
            UpdateWaveformDisplay();
        }
    }

    /// <summary>
    /// Gets or sets the sample rate.
    /// </summary>
    public int SampleRate
    {
        get => _sampleRate;
        set => _sampleRate = value;
    }

    /// <summary>
    /// Gets or sets the number of channels.
    /// </summary>
    public int Channels
    {
        get => _channels;
        set => _channels = value;
    }

    /// <summary>
    /// Gets or sets the clip name being processed.
    /// </summary>
    public string ClipName
    {
        get => ClipInfoText.Text;
        set => ClipInfoText.Text = $"Processing: {value}";
    }

    /// <summary>
    /// Gets the processing result.
    /// </summary>
    public AudioSuiteResult Result { get; private set; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when preview playback is requested.
    /// </summary>
    public event EventHandler<float[]>? PreviewRequested;

    /// <summary>
    /// Raised when preview should stop.
    /// </summary>
    public event EventHandler? StopPreviewRequested;

    /// <summary>
    /// Raised when processing is requested.
    /// </summary>
    public event EventHandler<AudioSuiteResult>? ProcessRequested;

    #endregion

    #region Constructor

    public AudioSuiteDialog()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWaveformDisplay();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopPreview();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string searchText = SearchBox.Text.ToLowerInvariant();
        FilterEffects(searchText);
    }

    private void EffectTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is ListBoxItem item && item.Tag is string effectId)
        {
            SelectEffect(effectId, item.Content?.ToString() ?? effectId);
        }
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetCombo.SelectedItem is ComboBoxItem item && _selectedEffect != null)
        {
            ApplyPreset(item.Content?.ToString() ?? "Default");
        }
    }

    private void SavePreset_Click(object sender, RoutedEventArgs e)
    {
        // Save current parameters as preset
        MessageBox.Show("Preset saving will be implemented in a future update.", "Save Preset",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        StartPreview();
    }

    private void StopPreview_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
    }

    private void CompareAB_Click(object sender, RoutedEventArgs e)
    {
        _isComparing = !_isComparing;
        UpdateWaveformDisplay();
    }

    private void Render_Click(object sender, RoutedEventArgs e)
    {
        RenderEffect();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
        DialogResult = false;
        Close();
    }

    private void Parameter_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AutoPreviewCheck.IsChecked == true && _selectedEffect != null)
        {
            // Debounce and start preview
            StartPreviewDebounced();
        }

        UpdateProcessedPreview();
    }

    #endregion

    #region Effect Selection

    private void SelectEffect(string effectId, string effectName)
    {
        _selectedEffect = CreateEffectDefinition(effectId, effectName);
        SelectedEffectName.Text = effectName;
        BuildParameterUI();
        UpdateProcessedPreview();
    }

    private static AudioSuiteEffect CreateEffectDefinition(string effectId, string effectName)
    {
        var effect = new AudioSuiteEffect
        {
            Id = effectId,
            Name = effectName
        };

        // Define parameters based on effect type
        switch (effectId)
        {
            case "Compressor":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Threshold", DisplayName = "Threshold", Value = -20, MinValue = -60, MaxValue = 0, Unit = "dB" },
                    new AudioSuiteParameter { Name = "Ratio", DisplayName = "Ratio", Value = 4, MinValue = 1, MaxValue = 20, Unit = ":1" },
                    new AudioSuiteParameter { Name = "Attack", DisplayName = "Attack", Value = 10, MinValue = 0.1, MaxValue = 100, Unit = "ms", Type = AudioSuiteParameter.ParameterType.Logarithmic },
                    new AudioSuiteParameter { Name = "Release", DisplayName = "Release", Value = 100, MinValue = 10, MaxValue = 1000, Unit = "ms", Type = AudioSuiteParameter.ParameterType.Logarithmic },
                    new AudioSuiteParameter { Name = "MakeupGain", DisplayName = "Makeup Gain", Value = 0, MinValue = 0, MaxValue = 24, Unit = "dB" },
                    new AudioSuiteParameter { Name = "Knee", DisplayName = "Knee", Value = 0, MinValue = 0, MaxValue = 12, Unit = "dB" }
                ];
                break;

            case "Limiter":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Ceiling", DisplayName = "Ceiling", Value = -0.3, MinValue = -12, MaxValue = 0, Unit = "dB" },
                    new AudioSuiteParameter { Name = "Release", DisplayName = "Release", Value = 50, MinValue = 1, MaxValue = 500, Unit = "ms" },
                    new AudioSuiteParameter { Name = "TruePeak", DisplayName = "True Peak", Value = 1, MinValue = 0, MaxValue = 1, Type = AudioSuiteParameter.ParameterType.Boolean }
                ];
                break;

            case "Gate":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Threshold", DisplayName = "Threshold", Value = -40, MinValue = -80, MaxValue = 0, Unit = "dB" },
                    new AudioSuiteParameter { Name = "Attack", DisplayName = "Attack", Value = 1, MinValue = 0.1, MaxValue = 50, Unit = "ms" },
                    new AudioSuiteParameter { Name = "Hold", DisplayName = "Hold", Value = 50, MinValue = 1, MaxValue = 500, Unit = "ms" },
                    new AudioSuiteParameter { Name = "Release", DisplayName = "Release", Value = 100, MinValue = 10, MaxValue = 1000, Unit = "ms" },
                    new AudioSuiteParameter { Name = "Range", DisplayName = "Range", Value = -80, MinValue = -80, MaxValue = 0, Unit = "dB" }
                ];
                break;

            case "ParametricEQ":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "LowGain", DisplayName = "Low Gain", Value = 0, MinValue = -12, MaxValue = 12, Unit = "dB" },
                    new AudioSuiteParameter { Name = "LowFreq", DisplayName = "Low Freq", Value = 100, MinValue = 20, MaxValue = 500, Unit = "Hz", Type = AudioSuiteParameter.ParameterType.Logarithmic },
                    new AudioSuiteParameter { Name = "MidGain", DisplayName = "Mid Gain", Value = 0, MinValue = -12, MaxValue = 12, Unit = "dB" },
                    new AudioSuiteParameter { Name = "MidFreq", DisplayName = "Mid Freq", Value = 1000, MinValue = 200, MaxValue = 8000, Unit = "Hz", Type = AudioSuiteParameter.ParameterType.Logarithmic },
                    new AudioSuiteParameter { Name = "MidQ", DisplayName = "Mid Q", Value = 1, MinValue = 0.1, MaxValue = 10 },
                    new AudioSuiteParameter { Name = "HighGain", DisplayName = "High Gain", Value = 0, MinValue = -12, MaxValue = 12, Unit = "dB" },
                    new AudioSuiteParameter { Name = "HighFreq", DisplayName = "High Freq", Value = 8000, MinValue = 2000, MaxValue = 20000, Unit = "Hz", Type = AudioSuiteParameter.ParameterType.Logarithmic }
                ];
                break;

            case "Reverb":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "RoomSize", DisplayName = "Room Size", Value = 0.5, MinValue = 0, MaxValue = 1 },
                    new AudioSuiteParameter { Name = "Damping", DisplayName = "Damping", Value = 0.5, MinValue = 0, MaxValue = 1 },
                    new AudioSuiteParameter { Name = "PreDelay", DisplayName = "Pre-Delay", Value = 20, MinValue = 0, MaxValue = 100, Unit = "ms" },
                    new AudioSuiteParameter { Name = "DecayTime", DisplayName = "Decay Time", Value = 2, MinValue = 0.1, MaxValue = 10, Unit = "s" },
                    new AudioSuiteParameter { Name = "Mix", DisplayName = "Mix", Value = 30, MinValue = 0, MaxValue = 100, Unit = "%" }
                ];
                break;

            case "Delay":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "DelayTime", DisplayName = "Delay Time", Value = 250, MinValue = 1, MaxValue = 2000, Unit = "ms" },
                    new AudioSuiteParameter { Name = "Feedback", DisplayName = "Feedback", Value = 30, MinValue = 0, MaxValue = 95, Unit = "%" },
                    new AudioSuiteParameter { Name = "Mix", DisplayName = "Mix", Value = 30, MinValue = 0, MaxValue = 100, Unit = "%" },
                    new AudioSuiteParameter { Name = "PingPong", DisplayName = "Ping Pong", Value = 0, MinValue = 0, MaxValue = 1, Type = AudioSuiteParameter.ParameterType.Boolean }
                ];
                break;

            case "PitchShift":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Semitones", DisplayName = "Semitones", Value = 0, MinValue = -24, MaxValue = 24, Unit = "st", Type = AudioSuiteParameter.ParameterType.Integer },
                    new AudioSuiteParameter { Name = "Cents", DisplayName = "Cents", Value = 0, MinValue = -100, MaxValue = 100, Unit = "ct", Type = AudioSuiteParameter.ParameterType.Integer },
                    new AudioSuiteParameter { Name = "Quality", DisplayName = "Quality", Value = 2, MinValue = 0, MaxValue = 3, Type = AudioSuiteParameter.ParameterType.Choice, Choices = ["Fast", "Standard", "High", "Ultra"] }
                ];
                break;

            case "TimeStretch":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "StretchRatio", DisplayName = "Stretch Ratio", Value = 100, MinValue = 25, MaxValue = 400, Unit = "%" },
                    new AudioSuiteParameter { Name = "PreservePitch", DisplayName = "Preserve Pitch", Value = 1, MinValue = 0, MaxValue = 1, Type = AudioSuiteParameter.ParameterType.Boolean },
                    new AudioSuiteParameter { Name = "Quality", DisplayName = "Quality", Value = 1, MinValue = 0, MaxValue = 2, Type = AudioSuiteParameter.ParameterType.Choice, Choices = ["Fast", "Standard", "High"] }
                ];
                break;

            case "Normalize":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "TargetLevel", DisplayName = "Target Level", Value = -0.3, MinValue = -24, MaxValue = 0, Unit = "dB" },
                    new AudioSuiteParameter { Name = "UseTruePeak", DisplayName = "Use True Peak", Value = 1, MinValue = 0, MaxValue = 1, Type = AudioSuiteParameter.ParameterType.Boolean },
                    new AudioSuiteParameter { Name = "IndependentChannels", DisplayName = "Independent Channels", Value = 0, MinValue = 0, MaxValue = 1, Type = AudioSuiteParameter.ParameterType.Boolean }
                ];
                break;

            case "Gain":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Gain", DisplayName = "Gain", Value = 0, MinValue = -24, MaxValue = 24, Unit = "dB" }
                ];
                break;

            case "NoiseReduction":
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Reduction", DisplayName = "Reduction", Value = 12, MinValue = 0, MaxValue = 40, Unit = "dB" },
                    new AudioSuiteParameter { Name = "Sensitivity", DisplayName = "Sensitivity", Value = 50, MinValue = 0, MaxValue = 100, Unit = "%" },
                    new AudioSuiteParameter { Name = "Smoothing", DisplayName = "Smoothing", Value = 0, MinValue = 0, MaxValue = 6, Type = AudioSuiteParameter.ParameterType.Integer }
                ];
                break;

            default:
                // Default parameter set
                effect.Parameters =
                [
                    new AudioSuiteParameter { Name = "Amount", DisplayName = "Amount", Value = 50, MinValue = 0, MaxValue = 100, Unit = "%" },
                    new AudioSuiteParameter { Name = "Mix", DisplayName = "Mix", Value = 100, MinValue = 0, MaxValue = 100, Unit = "%" }
                ];
                break;
        }

        return effect;
    }

    private void BuildParameterUI()
    {
        ParametersPanel.Children.Clear();
        _parameterControls.Clear();

        if (_selectedEffect == null) return;

        foreach (var param in _selectedEffect.Parameters)
        {
            var paramPanel = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            paramPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            paramPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            paramPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            // Label
            var label = new TextBlock
            {
                Text = param.DisplayName,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Brush)FindResource("TextPrimaryBrush")
            };
            Grid.SetColumn(label, 0);
            paramPanel.Children.Add(label);

            if (param.Type == AudioSuiteParameter.ParameterType.Boolean)
            {
                var checkBox = new CheckBox
                {
                    IsChecked = param.Value > 0.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                checkBox.Checked += (s, e) => param.Value = 1;
                checkBox.Unchecked += (s, e) => param.Value = 0;
                Grid.SetColumn(checkBox, 1);
                paramPanel.Children.Add(checkBox);
                _parameterControls[param.Name] = checkBox;
            }
            else if (param.Type == AudioSuiteParameter.ParameterType.Choice && param.Choices != null)
            {
                var comboBox = new ComboBox
                {
                    ItemsSource = param.Choices,
                    SelectedIndex = (int)param.Value,
                    VerticalAlignment = VerticalAlignment.Center
                };
                comboBox.SelectionChanged += (s, e) => param.Value = comboBox.SelectedIndex;
                Grid.SetColumn(comboBox, 1);
                Grid.SetColumnSpan(comboBox, 2);
                paramPanel.Children.Add(comboBox);
                _parameterControls[param.Name] = comboBox;
            }
            else
            {
                // Slider
                var slider = new Slider
                {
                    Minimum = param.MinValue,
                    Maximum = param.MaxValue,
                    Value = param.Value,
                    VerticalAlignment = VerticalAlignment.Center,
                    IsSnapToTickEnabled = param.Type == AudioSuiteParameter.ParameterType.Integer,
                    TickFrequency = param.Type == AudioSuiteParameter.ParameterType.Integer ? 1 : 0
                };
                slider.ValueChanged += (s, e) =>
                {
                    param.Value = e.NewValue;
                    Parameter_ValueChanged(s, e);
                };
                Grid.SetColumn(slider, 1);
                paramPanel.Children.Add(slider);
                _parameterControls[param.Name] = slider;

                // Value display
                var valueText = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    Margin = new Thickness(8, 0, 0, 0)
                };

                string format = param.Type == AudioSuiteParameter.ParameterType.Integer ? "F0" : "F1";
                valueText.Text = $"{param.Value.ToString(format)} {param.Unit}";

                slider.ValueChanged += (s, e) =>
                {
                    valueText.Text = $"{e.NewValue.ToString(format)} {param.Unit}";
                };

                Grid.SetColumn(valueText, 2);
                paramPanel.Children.Add(valueText);
            }

            ParametersPanel.Children.Add(paramPanel);
        }
    }

    private void ApplyPreset(string presetName)
    {
        if (_selectedEffect == null) return;

        // Apply preset values
        double multiplier = presetName switch
        {
            "Gentle" => 0.5,
            "Aggressive" => 1.5,
            _ => 1.0
        };

        foreach (var param in _selectedEffect.Parameters)
        {
            if (_parameterControls.TryGetValue(param.Name, out var control))
            {
                double newValue = param.DefaultValue * multiplier;
                newValue = Math.Clamp(newValue, param.MinValue, param.MaxValue);

                if (control is Slider slider)
                {
                    slider.Value = newValue;
                }
            }
        }
    }

    private void FilterEffects(string searchText)
    {
        foreach (var item in EffectTreeView.Items)
        {
            if (item is TreeViewItem category)
            {
                bool categoryHasMatch = false;

                foreach (var effectItem in category.Items)
                {
                    if (effectItem is ListBoxItem effect)
                    {
                        bool matches = string.IsNullOrEmpty(searchText) ||
                                       (effect.Content?.ToString()?.ToLowerInvariant().Contains(searchText) == true);
                        effect.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                        if (matches) categoryHasMatch = true;
                    }
                }

                category.Visibility = categoryHasMatch ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    #endregion

    #region Preview Methods

    private CancellationTokenSource? _debounceToken;

    private async void StartPreviewDebounced()
    {
        _debounceToken?.Cancel();
        _debounceToken = new CancellationTokenSource();

        try
        {
            await Task.Delay(300, _debounceToken.Token);
            StartPreview();
        }
        catch (TaskCanceledException)
        {
            // Debounce cancelled
        }
    }

    private void StartPreview()
    {
        if (_originalSamples == null || _selectedEffect == null) return;

        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();

        _isPreviewing = true;
        PreviewButton.IsEnabled = false;
        StopPreviewButton.IsEnabled = true;

        // Process and play preview
        var processedSamples = ProcessSamples(_originalSamples);
        _processedSamples = processedSamples;

        PreviewRequested?.Invoke(this, processedSamples);
        UpdateWaveformDisplay();
    }

    private void StopPreview()
    {
        _previewCts?.Cancel();
        _isPreviewing = false;
        PreviewButton.IsEnabled = true;
        StopPreviewButton.IsEnabled = false;

        StopPreviewRequested?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateProcessedPreview()
    {
        if (_originalSamples == null || _selectedEffect == null) return;

        _processedSamples = ProcessSamples(_originalSamples);
        UpdateWaveformDisplay();
    }

    private float[] ProcessSamples(float[] input)
    {
        // This is a placeholder - actual processing would use MusicEngine effects
        var output = new float[input.Length];
        Array.Copy(input, output, input.Length);

        // Simple example: apply gain if it's a gain effect
        if (_selectedEffect?.Id == "Gain")
        {
            var gainParam = _selectedEffect.Parameters.Find(p => p.Name == "Gain");
            if (gainParam != null)
            {
                double gainLinear = Math.Pow(10, gainParam.Value / 20.0);
                for (int i = 0; i < output.Length; i++)
                {
                    output[i] = (float)(output[i] * gainLinear);
                }
            }
        }

        return output;
    }

    #endregion

    #region Rendering

    private async void RenderEffect()
    {
        if (_originalSamples == null || _selectedEffect == null)
        {
            MessageBox.Show("Please select an effect and provide audio to process.", "No Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        StopPreview();

        ProgressPanel.Visibility = Visibility.Visible;
        ProcessingProgress.Value = 0;
        ProcessingProgress.IsIndeterminate = true;
        ProgressText.Text = "Processing...";

        try
        {
            // Collect parameters
            var parameterValues = new Dictionary<string, double>();
            foreach (var param in _selectedEffect.Parameters)
            {
                parameterValues[param.Name] = param.Value;
            }

            // Process
            var processedSamples = await Task.Run(() => ProcessSamples(_originalSamples));

            Result = new AudioSuiteResult
            {
                Success = true,
                EffectId = _selectedEffect.Id,
                ParameterValues = parameterValues,
                ProcessingMode = GetProcessingMode(),
                CreateNewFile = CreateNewFileCheck.IsChecked == true,
                ProcessedSamples = processedSamples
            };

            ProcessRequested?.Invoke(this, Result);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Processing failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ProgressPanel.Visibility = Visibility.Collapsed;
        }
    }

    private AudioSuiteProcessingMode GetProcessingMode()
    {
        if (ModeRegionByRegion.IsChecked == true) return AudioSuiteProcessingMode.RegionByRegion;
        if (ModeEntireSelection.IsChecked == true) return AudioSuiteProcessingMode.EntireSelection;
        return AudioSuiteProcessingMode.ClipByClip;
    }

    #endregion

    #region Waveform Display

    private void UpdateWaveformDisplay()
    {
        WaveformCanvas.Children.Clear();

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw original waveform
        if (_originalSamples != null && _originalSamples.Length > 0)
        {
            DrawWaveform(_originalSamples, Colors.Gray, 0.5);
        }

        // Draw processed waveform
        if (_processedSamples != null && _processedSamples.Length > 0 && !_isComparing)
        {
            DrawWaveform(_processedSamples, Color.FromRgb(0x55, 0xAA, 0xFF), 0.8);
        }
    }

    private void DrawWaveform(float[] samples, Color color, double opacity)
    {
        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        var points = new PointCollection();
        int samplesPerPixel = Math.Max(1, samples.Length / (int)width);

        for (int x = 0; x < (int)width; x++)
        {
            int startSample = x * samplesPerPixel;
            int endSample = Math.Min(startSample + samplesPerPixel, samples.Length);

            float maxSample = 0;
            for (int i = startSample; i < endSample; i++)
            {
                maxSample = Math.Max(maxSample, Math.Abs(samples[i]));
            }

            double y = height / 2 - (maxSample * height / 2 * 0.9);
            points.Add(new Point(x, y));
        }

        // Add return path
        for (int x = (int)width - 1; x >= 0; x--)
        {
            int startSample = x * samplesPerPixel;
            int endSample = Math.Min(startSample + samplesPerPixel, samples.Length);

            float minSample = 0;
            for (int i = startSample; i < endSample; i++)
            {
                minSample = Math.Min(minSample, -Math.Abs(samples[i]));
            }

            double y = height / 2 - (minSample * height / 2 * 0.9);
            points.Add(new Point(x, y));
        }

        var polygon = new Shapes.Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(color) { Opacity = opacity },
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 0.5
        };

        WaveformCanvas.Children.Add(polygon);
    }

    #endregion
}
