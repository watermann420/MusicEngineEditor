// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Noise reduction panel with parameter controls, noise profile learning, and A/B comparison.
/// </summary>
public partial class NoiseReductionPanel : UserControl
{
    private CancellationTokenSource? _learningCts;
    private bool _hasNoiseProfile;
    private bool _isBypassed;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isShowingOriginal;
#pragma warning restore CS0414

    /// <summary>
    /// Event raised when a parameter value changes.
    /// </summary>
    public event EventHandler<NoiseReductionParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when noise learning is requested.
    /// </summary>
    public event EventHandler? LearnNoiseRequested;

    /// <summary>
    /// Event raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Event raised when before/after preview mode changes.
    /// </summary>
    public event EventHandler<bool>? PreviewModeChanged;

    /// <summary>
    /// Gets or sets the reduction amount (0-100).
    /// </summary>
    public double ReductionAmount
    {
        get => ReductionSlider.Value;
        set => ReductionSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the sensitivity (0-100).
    /// </summary>
    public double Sensitivity
    {
        get => SensitivitySlider.Value;
        set => SensitivitySlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the frequency smoothing (0-10 bands).
    /// </summary>
    public int FrequencySmoothing
    {
        get => (int)FrequencySmoothingSlider.Value;
        set => FrequencySmoothingSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public double AttackMs
    {
        get => AttackSlider.Value;
        set => AttackSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public double ReleaseMs
    {
        get => ReleaseSlider.Value;
        set => ReleaseSlider.Value = value;
    }

    /// <summary>
    /// Gets whether a noise profile has been captured.
    /// </summary>
    public bool HasNoiseProfile => _hasNoiseProfile;

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
        }
    }

    /// <summary>
    /// Creates a new noise reduction panel.
    /// </summary>
    public NoiseReductionPanel()
    {
        InitializeComponent();
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        StatusText.Text = _isBypassed ? "Effect bypassed" : "Effect active";
        BypassChanged?.Invoke(this, _isBypassed);
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not ComboBoxItem item) return;

        var preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);
    }

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Light Hiss Removal":
                ReductionSlider.Value = 30;
                SensitivitySlider.Value = 20;
                FrequencySmoothingSlider.Value = 2;
                AttackSlider.Value = 5;
                ReleaseSlider.Value = 30;
                break;

            case "Medium Noise Reduction":
                ReductionSlider.Value = 50;
                SensitivitySlider.Value = 35;
                FrequencySmoothingSlider.Value = 3;
                AttackSlider.Value = 5;
                ReleaseSlider.Value = 50;
                break;

            case "Heavy Noise Reduction":
                ReductionSlider.Value = 80;
                SensitivitySlider.Value = 50;
                FrequencySmoothingSlider.Value = 5;
                AttackSlider.Value = 10;
                ReleaseSlider.Value = 100;
                break;

            case "Broadband Noise":
                ReductionSlider.Value = 60;
                SensitivitySlider.Value = 40;
                FrequencySmoothingSlider.Value = 6;
                AttackSlider.Value = 5;
                ReleaseSlider.Value = 75;
                break;

            case "Air Conditioning/HVAC":
                ReductionSlider.Value = 70;
                SensitivitySlider.Value = 30;
                FrequencySmoothingSlider.Value = 4;
                AttackSlider.Value = 10;
                ReleaseSlider.Value = 150;
                break;

            case "Hum Removal (50/60 Hz)":
                ReductionSlider.Value = 90;
                SensitivitySlider.Value = 60;
                FrequencySmoothingSlider.Value = 1;
                AttackSlider.Value = 1;
                ReleaseSlider.Value = 20;
                break;
        }

        StatusText.Text = $"Preset applied: {preset}";
    }

    private async void LearnNoiseButton_Click(object sender, RoutedEventArgs e)
    {
        LearnNoiseRequested?.Invoke(this, EventArgs.Empty);
        await LearnNoiseProfileAsync();
    }

    /// <summary>
    /// Learns a noise profile from the current selection or input.
    /// </summary>
    public async Task LearnNoiseProfileAsync()
    {
        // Cancel any existing learning operation
        _learningCts?.Cancel();
        _learningCts = new CancellationTokenSource();

        try
        {
            // Show learning progress
            LearnNoiseButton.Visibility = Visibility.Collapsed;
            ClearProfileButton.Visibility = Visibility.Collapsed;
            LearningProgress.Visibility = Visibility.Visible;
            LearnProgressBar.Value = 0;
            StatusText.Text = "Learning noise profile...";

            // Simulate learning process
            for (int i = 0; i <= 100; i += 5)
            {
                _learningCts.Token.ThrowIfCancellationRequested();
                await Task.Delay(50, _learningCts.Token);

                LearnProgressBar.Value = i;
                LearnProgressText.Text = $"Learning noise profile... {i}%";
            }

            // Learning complete
            _hasNoiseProfile = true;
            NoiseProfileStatus.Text = "Noise profile captured";
            StatusText.Text = "Noise profile learned successfully";
            ClearProfileButton.IsEnabled = true;
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Learning cancelled";
        }
        finally
        {
            LearningProgress.Visibility = Visibility.Collapsed;
            LearnNoiseButton.Visibility = Visibility.Visible;
            ClearProfileButton.Visibility = Visibility.Visible;
        }
    }

    /// <summary>
    /// Sets the noise profile from external data.
    /// </summary>
    public void SetNoiseProfile(float[] noiseSpectrum)
    {
        _hasNoiseProfile = true;
        NoiseProfileStatus.Text = "Noise profile set";
        ClearProfileButton.IsEnabled = true;
        StatusText.Text = "Noise profile loaded";
    }

    private void ClearProfileButton_Click(object sender, RoutedEventArgs e)
    {
        _hasNoiseProfile = false;
        NoiseProfileStatus.Text = "No noise profile captured";
        ClearProfileButton.IsEnabled = false;
        StatusText.Text = "Noise profile cleared";
    }

    private void ReductionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ReductionValue == null) return;
        ReductionValue.Text = $"{e.NewValue:0}%";
        RaiseParameterChanged("ReductionAmount", e.NewValue);
    }

    private void SensitivitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SensitivityValue == null) return;
        SensitivityValue.Text = $"{e.NewValue:0}%";
        RaiseParameterChanged("Sensitivity", e.NewValue);
    }

    private void FrequencySmoothingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FrequencySmoothingValue == null) return;
        FrequencySmoothingValue.Text = $"{e.NewValue:0} bands";
        RaiseParameterChanged("FrequencySmoothing", e.NewValue);
    }

    private void AttackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AttackValue == null) return;
        AttackValue.Text = $"{e.NewValue:0} ms";
        RaiseParameterChanged("AttackMs", e.NewValue);
    }

    private void ReleaseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ReleaseValue == null) return;
        ReleaseValue.Text = $"{e.NewValue:0} ms";
        RaiseParameterChanged("ReleaseMs", e.NewValue);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new NoiseReductionParameterChangedEventArgs(parameterName, value));

        // Set preset to Custom when manually adjusting
        if (PresetComboBox.SelectedIndex != PresetComboBox.Items.Count - 1)
        {
            // Only update if not already Custom and user is manually changing
            if (IsLoaded && PresetComboBox.SelectedItem is ComboBoxItem item &&
                item.Content?.ToString() != "Custom")
            {
                PresetComboBox.SelectionChanged -= PresetComboBox_SelectionChanged;
                PresetComboBox.SelectedIndex = PresetComboBox.Items.Count - 1;
                PresetComboBox.SelectionChanged += PresetComboBox_SelectionChanged;
            }
        }
    }

    private void AfterRadio_Checked(object sender, RoutedEventArgs e)
    {
        _isShowingOriginal = false;
        StatusText.Text = "Previewing processed audio";
        PreviewModeChanged?.Invoke(this, false);
    }

    private void BeforeRadio_Checked(object sender, RoutedEventArgs e)
    {
        _isShowingOriginal = true;
        StatusText.Text = "Previewing original audio";
        PreviewModeChanged?.Invoke(this, true);
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        _learningCts?.Cancel();
        _hasNoiseProfile = false;
        _isBypassed = false;
        _isShowingOriginal = false;

        PresetComboBox.SelectedIndex = 0;
        BypassToggle.IsChecked = false;
        AfterRadio.IsChecked = true;
        NoiseProfileStatus.Text = "No noise profile captured";
        ClearProfileButton.IsEnabled = false;
        StatusText.Text = "Reset to defaults";
    }
}

/// <summary>
/// Event arguments for noise reduction parameter changes.
/// </summary>
public class NoiseReductionParameterChangedEventArgs : EventArgs
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
    public NoiseReductionParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}
