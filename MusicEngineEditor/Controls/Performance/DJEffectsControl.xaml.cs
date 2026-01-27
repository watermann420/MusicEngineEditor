// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: DJ Effects Panel control for live performance with filter sweep XY pad,
// beat repeat, brake, spinback, echo out, and flanger effects.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Performance;

namespace MusicEngineEditor.Controls.Performance;

#region Converters

/// <summary>
/// Converts boolean values to visibility.
/// </summary>
public class DJEffectsBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts a percentage value (0-100) to width for slider fill.
/// </summary>
public class DJEffectsPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value;
    }
}

#endregion

#region Event Args

/// <summary>
/// Event arguments for DJ effect events.
/// </summary>
public class DJEffectEventArgs : EventArgs
{
    /// <summary>Gets the effect type.</summary>
    public DJEffectType EffectType { get; }

    /// <summary>Gets whether the effect is active.</summary>
    public bool IsActive { get; }

    /// <summary>
    /// Creates new DJ effect event arguments.
    /// </summary>
    /// <param name="effectType">The effect type.</param>
    /// <param name="isActive">Whether the effect is active.</param>
    public DJEffectEventArgs(DJEffectType effectType, bool isActive)
    {
        EffectType = effectType;
        IsActive = isActive;
    }
}

/// <summary>
/// Event arguments for XY pad position changes.
/// </summary>
public class XYPadChangedEventArgs : EventArgs
{
    /// <summary>Gets the X position (0.0 to 1.0).</summary>
    public float X { get; }

    /// <summary>Gets the Y position (0.0 to 1.0).</summary>
    public float Y { get; }

    /// <summary>
    /// Creates new XY pad changed event arguments.
    /// </summary>
    /// <param name="x">The X position.</param>
    /// <param name="y">The Y position.</param>
    public XYPadChangedEventArgs(float x, float y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// Event arguments for beat repeat.
/// </summary>
public class BeatRepeatEventArgs : EventArgs
{
    /// <summary>Gets the beat division (4, 8, 16, or 32).</summary>
    public int Division { get; }

    /// <summary>
    /// Creates new beat repeat event arguments.
    /// </summary>
    /// <param name="division">The beat division.</param>
    public BeatRepeatEventArgs(int division)
    {
        Division = division;
    }
}

#endregion

/// <summary>
/// DJ Effects Panel control providing filter sweep XY pad, beat repeat buttons,
/// brake, spinback, echo out, and flanger effects for live performance.
/// </summary>
public partial class DJEffectsControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty DJEffectsProperty =
        DependencyProperty.Register(nameof(DJEffects), typeof(DJEffects), typeof(DJEffectsControl),
            new PropertyMetadata(null, OnDJEffectsChanged));

    public static readonly DependencyProperty TempoProperty =
        DependencyProperty.Register(nameof(Tempo), typeof(float), typeof(DJEffectsControl),
            new PropertyMetadata(120f, OnTempoChanged));

    public static readonly DependencyProperty FilterCutoffProperty =
        DependencyProperty.Register(nameof(FilterCutoff), typeof(float), typeof(DJEffectsControl),
            new PropertyMetadata(0.5f, OnFilterCutoffChanged));

    public static readonly DependencyProperty FilterResonanceProperty =
        DependencyProperty.Register(nameof(FilterResonance), typeof(float), typeof(DJEffectsControl),
            new PropertyMetadata(0.5f, OnFilterResonanceChanged));

    public static readonly DependencyProperty WetDryMixProperty =
        DependencyProperty.Register(nameof(WetDryMix), typeof(float), typeof(DJEffectsControl),
            new PropertyMetadata(0.5f, OnWetDryMixChanged));

    public static readonly DependencyProperty IsFilterActiveProperty =
        DependencyProperty.Register(nameof(IsFilterActive), typeof(bool), typeof(DJEffectsControl),
            new PropertyMetadata(false, OnIsFilterActiveChanged));

    public static readonly DependencyProperty IsFlangerActiveProperty =
        DependencyProperty.Register(nameof(IsFlangerActive), typeof(bool), typeof(DJEffectsControl),
            new PropertyMetadata(false, OnIsFlangerActiveChanged));

    /// <summary>
    /// Gets or sets the DJ effects processor.
    /// </summary>
    public DJEffects? DJEffects
    {
        get => (DJEffects?)GetValue(DJEffectsProperty);
        set => SetValue(DJEffectsProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public float Tempo
    {
        get => (float)GetValue(TempoProperty);
        set => SetValue(TempoProperty, value);
    }

    /// <summary>
    /// Gets or sets the filter cutoff position (0.0 to 1.0).
    /// </summary>
    public float FilterCutoff
    {
        get => (float)GetValue(FilterCutoffProperty);
        set => SetValue(FilterCutoffProperty, value);
    }

    /// <summary>
    /// Gets or sets the filter resonance (0.0 to 1.0).
    /// </summary>
    public float FilterResonance
    {
        get => (float)GetValue(FilterResonanceProperty);
        set => SetValue(FilterResonanceProperty, value);
    }

    /// <summary>
    /// Gets or sets the wet/dry mix (0.0 to 1.0).
    /// </summary>
    public float WetDryMix
    {
        get => (float)GetValue(WetDryMixProperty);
        set => SetValue(WetDryMixProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the filter sweep is active.
    /// </summary>
    public bool IsFilterActive
    {
        get => (bool)GetValue(IsFilterActiveProperty);
        set => SetValue(IsFilterActiveProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the flanger is active.
    /// </summary>
    public bool IsFlangerActive
    {
        get => (bool)GetValue(IsFlangerActiveProperty);
        set => SetValue(IsFlangerActiveProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when an effect is triggered or stopped.
    /// </summary>
    public event EventHandler<DJEffectEventArgs>? EffectChanged;

    /// <summary>
    /// Fired when the XY pad position changes.
    /// </summary>
    public event EventHandler<XYPadChangedEventArgs>? XYPadChanged;

    /// <summary>
    /// Fired when a beat repeat button is pressed.
    /// </summary>
    public event EventHandler<BeatRepeatEventArgs>? BeatRepeatTriggered;

    /// <summary>
    /// Fired when the wet/dry mix changes.
    /// </summary>
    public event EventHandler<float>? WetDryMixChanged;

    /// <summary>
    /// Fired when all effects are killed.
    /// </summary>
    public event EventHandler? AllEffectsKilled;

    #endregion

    #region Private Fields

    private bool _isXYPadDragging;
    private readonly DispatcherTimer _updateTimer;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public DJEffectsControl()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Initialize XY pad
        UpdateXYPadIndicator();
        UpdateXYPadGridLines();

        // Update displays
        UpdateTempoDisplay();
        UpdateWetDryDisplay();
        UpdateActiveEffectDisplay();

        // Start update timer
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
            UpdateXYPadIndicator();
            UpdateXYPadGridLines();
        }
    }

    #endregion

    #region Property Changed Handlers

    private static void OnDJEffectsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control)
        {
            if (e.OldValue is DJEffects oldEffects)
            {
                oldEffects.EffectTriggered -= control.DJEffects_EffectTriggered;
                oldEffects.EffectCompleted -= control.DJEffects_EffectCompleted;
            }

            if (e.NewValue is DJEffects newEffects)
            {
                newEffects.EffectTriggered += control.DJEffects_EffectTriggered;
                newEffects.EffectCompleted += control.DJEffects_EffectCompleted;
                control.SyncFromDJEffects(newEffects);
            }
        }
    }

    private static void OnTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control)
        {
            if (control.DJEffects != null)
            {
                control.DJEffects.Tempo = (float)e.NewValue;
            }
            control.UpdateTempoDisplay();
        }
    }

    private static void OnFilterCutoffChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control && control._isInitialized)
        {
            control.UpdateXYPadIndicator();
            control.UpdateCutoffDisplay();
            control.ApplyFilterToEngine();
        }
    }

    private static void OnFilterResonanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control && control._isInitialized)
        {
            control.UpdateXYPadIndicator();
            control.UpdateResonanceDisplay();
            control.ApplyFilterToEngine();
        }
    }

    private static void OnWetDryMixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control && control._isInitialized)
        {
            float newValue = (float)e.NewValue;
            control.WetDrySlider.Value = newValue * 100;
            control.UpdateWetDryDisplay();
            control.ApplyWetDryToEngine();
        }
    }

    private static void OnIsFilterActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control)
        {
            bool isActive = (bool)e.NewValue;
            control.FilterActiveToggle.IsChecked = isActive;

            if (control.DJEffects != null)
            {
                if (isActive)
                {
                    control.DJEffects.TriggerEffect(DJEffectType.FilterSweep);
                }
                else
                {
                    control.DJEffects.StopEffect(DJEffectType.FilterSweep);
                }
            }

            control.EffectChanged?.Invoke(control, new DJEffectEventArgs(DJEffectType.FilterSweep, isActive));
            control.UpdateActiveEffectDisplay();
        }
    }

    private static void OnIsFlangerActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DJEffectsControl control)
        {
            bool isActive = (bool)e.NewValue;
            control.FlangerToggle.IsChecked = isActive;

            if (control.DJEffects != null)
            {
                if (isActive)
                {
                    control.DJEffects.TriggerEffect(DJEffectType.Flanger);
                }
                else
                {
                    control.DJEffects.StopEffect(DJEffectType.Flanger);
                }
            }

            control.EffectChanged?.Invoke(control, new DJEffectEventArgs(DJEffectType.Flanger, isActive));
            control.UpdateActiveEffectDisplay();
        }
    }

    #endregion

    #region DJEffects Event Handlers

    private void DJEffects_EffectTriggered(object? sender, DJEffectType e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateActiveEffectDisplay();
            ShowProgressPanel(e);
        });
    }

    private void DJEffects_EffectCompleted(object? sender, DJEffectType e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateActiveEffectDisplay();
            HideProgressPanel();
        });
    }

    #endregion

    #region XY Pad Handling

    private void XYPad_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isXYPadDragging = true;
            XYPadCanvas.CaptureMouse();
            UpdateXYPadFromMouse(e.GetPosition(XYPadCanvas));
        }
    }

    private void XYPad_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isXYPadDragging)
        {
            UpdateXYPadFromMouse(e.GetPosition(XYPadCanvas));
        }
    }

    private void XYPad_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isXYPadDragging)
        {
            _isXYPadDragging = false;
            XYPadCanvas.ReleaseMouseCapture();
        }
    }

    private void XYPad_MouseLeave(object sender, MouseEventArgs e)
    {
        // Keep dragging if mouse leaves but button is still held
    }

    private void UpdateXYPadFromMouse(Point position)
    {
        double width = XYPadCanvas.ActualWidth;
        double height = XYPadCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate normalized position (0 to 1)
        float x = (float)Math.Clamp(position.X / width, 0, 1);
        float y = (float)Math.Clamp(1 - (position.Y / height), 0, 1); // Invert Y so up is higher

        // Update properties
        FilterCutoff = x;
        FilterResonance = y;

        // Fire event
        XYPadChanged?.Invoke(this, new XYPadChangedEventArgs(x, y));
    }

    private void UpdateXYPadIndicator()
    {
        double width = XYPadCanvas.ActualWidth;
        double height = XYPadCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate pixel position from normalized values
        double x = FilterCutoff * width - 10; // -10 for indicator radius
        double y = (1 - FilterResonance) * height - 10; // Invert Y

        // Clamp to canvas bounds
        x = Math.Clamp(x, -10, width - 10);
        y = Math.Clamp(y, -10, height - 10);

        Canvas.SetLeft(XYPadIndicator, x);
        Canvas.SetTop(XYPadIndicator, y);

        // Update displays
        UpdateCutoffDisplay();
        UpdateResonanceDisplay();
    }

    private void UpdateXYPadGridLines()
    {
        double width = XYPadCanvas.ActualWidth;
        double height = XYPadCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Center vertical line
        XYPadVerticalLine.X1 = width / 2;
        XYPadVerticalLine.X2 = width / 2;
        XYPadVerticalLine.Y1 = 0;
        XYPadVerticalLine.Y2 = height;

        // Center horizontal line
        XYPadHorizontalLine.X1 = 0;
        XYPadHorizontalLine.X2 = width;
        XYPadHorizontalLine.Y1 = height / 2;
        XYPadHorizontalLine.Y2 = height / 2;
    }

    #endregion

    #region Button Click Handlers

    private void FilterActiveToggle_Checked(object sender, RoutedEventArgs e)
    {
        IsFilterActive = true;
    }

    private void FilterActiveToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        IsFilterActive = false;
    }

    private void BeatRepeat_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagStr && int.TryParse(tagStr, out int division))
        {
            // Set echo out division
            if (DJEffects != null)
            {
                DJEffects.SetParameter("EchoOut.Division", division / 4f);
                DJEffects.TriggerEffect(DJEffectType.EchoOut);
            }

            BeatRepeatTriggered?.Invoke(this, new BeatRepeatEventArgs(division));
            UpdateActiveEffectDisplay();
        }
    }

    private void Brake_Click(object sender, RoutedEventArgs e)
    {
        DJEffects?.TriggerEffect(DJEffectType.Brake);
        EffectChanged?.Invoke(this, new DJEffectEventArgs(DJEffectType.Brake, true));
        UpdateActiveEffectDisplay();
        ShowProgressPanel(DJEffectType.Brake);
    }

    private void Spinback_Click(object sender, RoutedEventArgs e)
    {
        DJEffects?.TriggerEffect(DJEffectType.Backspin);
        EffectChanged?.Invoke(this, new DJEffectEventArgs(DJEffectType.Backspin, true));
        UpdateActiveEffectDisplay();
        ShowProgressPanel(DJEffectType.Backspin);
    }

    private void EchoOut_Click(object sender, RoutedEventArgs e)
    {
        if (DJEffects?.EchoOut.IsActive == true)
        {
            DJEffects.StopEffect(DJEffectType.EchoOut);
            EffectChanged?.Invoke(this, new DJEffectEventArgs(DJEffectType.EchoOut, false));
        }
        else
        {
            DJEffects?.TriggerEffect(DJEffectType.EchoOut);
            EffectChanged?.Invoke(this, new DJEffectEventArgs(DJEffectType.EchoOut, true));
        }
        UpdateActiveEffectDisplay();
    }

    private void Flanger_Checked(object sender, RoutedEventArgs e)
    {
        IsFlangerActive = true;
    }

    private void Flanger_Unchecked(object sender, RoutedEventArgs e)
    {
        IsFlangerActive = false;
    }

    private void WetDrySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float newValue = (float)(e.NewValue / 100.0);
        WetDryMix = newValue;
        UpdateWetDryDisplay();
        WetDryMixChanged?.Invoke(this, newValue);
    }

    private void KillAll_Click(object sender, RoutedEventArgs e)
    {
        DJEffects?.StopAllEffects();
        IsFilterActive = false;
        IsFlangerActive = false;
        UpdateActiveEffectDisplay();
        HideProgressPanel();
        AllEffectsKilled?.Invoke(this, EventArgs.Empty);
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized || DJEffects == null) return;

        string? presetName = (PresetComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant();
        if (string.IsNullOrEmpty(presetName)) return;

        // Apply preset settings to the current DJEffects instance
        // Note: This would ideally be done through the DJEffects.CreatePreset factory,
        // but we apply settings to the existing instance instead
        ApplyPresetSettings(presetName);
    }

    #endregion

    #region Update Methods

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isInitialized || DJEffects == null) return;

        // Update effect progress if applicable
        UpdateEffectProgress();

        // Update active effect indicator
        UpdateActiveEffectDisplay();
    }

    private void UpdateTempoDisplay()
    {
        TempoText.Text = $"{Tempo:F0} BPM";
    }

    private void UpdateWetDryDisplay()
    {
        WetDryValueText.Text = $"{WetDryMix * 100:F0}%";
    }

    private void UpdateCutoffDisplay()
    {
        CutoffValueText.Text = $"{FilterCutoff * 100:F0}%";
    }

    private void UpdateResonanceDisplay()
    {
        ResonanceValueText.Text = $"{FilterResonance * 100:F0}%";
    }

    private void UpdateActiveEffectDisplay()
    {
        if (DJEffects == null)
        {
            ActiveEffectText.Text = "NO ENGINE";
            ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
            return;
        }

        DJEffectType activeEffect = DJEffects.ActiveEffect;

        if (activeEffect == DJEffectType.None)
        {
            // Check if any effect is active
            if (DJEffects.FilterSweep.IsActive)
            {
                ActiveEffectText.Text = "FILTER";
                ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
            }
            else if (DJEffects.Flanger.IsActive)
            {
                ActiveEffectText.Text = "FLANGER";
                ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
            }
            else if (DJEffects.EchoOut.IsActive)
            {
                ActiveEffectText.Text = "ECHO";
                ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00));
            }
            else
            {
                ActiveEffectText.Text = "READY";
                ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
            }
        }
        else
        {
            ActiveEffectText.Text = activeEffect.ToString().ToUpperInvariant();
            ActiveEffectText.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00));
        }
    }

    private void UpdateEffectProgress()
    {
        if (DJEffects == null) return;

        float progress = 0;

        if (DJEffects.Brake.IsActive)
        {
            progress = DJEffects.Brake.Progress;
        }
        else if (DJEffects.Backspin.IsActive)
        {
            progress = DJEffects.Backspin.Progress;
        }
        else if (DJEffects.NoiseBuild.IsActive)
        {
            progress = DJEffects.NoiseBuild.Progress;
        }
        else if (DJEffects.EchoOut.IsActive)
        {
            progress = DJEffects.EchoOut.Progress;
        }

        EffectProgress.Value = progress * 100;
    }

    private void ShowProgressPanel(DJEffectType effectType)
    {
        ProgressPanel.Visibility = Visibility.Visible;
        ProgressLabel.Text = $"{effectType.ToString().ToUpperInvariant()} PROGRESS";
        EffectProgress.Value = 0;
    }

    private void HideProgressPanel()
    {
        ProgressPanel.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Engine Synchronization

    private void SyncFromDJEffects(DJEffects effects)
    {
        if (!_isInitialized) return;

        // Sync filter state
        FilterCutoff = effects.FilterSweep.SweepPosition;
        FilterResonance = (effects.FilterSweep.Resonance - 0.5f) / 14.5f; // Normalize from 0.5-15 range
        IsFilterActive = effects.FilterSweep.IsActive;
        IsFlangerActive = effects.Flanger.IsActive;
        Tempo = effects.Tempo;

        // Update displays
        UpdateTempoDisplay();
        UpdateActiveEffectDisplay();
    }

    private void ApplyFilterToEngine()
    {
        if (DJEffects == null) return;

        // Map cutoff (0-1) to sweep position
        DJEffects.FilterSweep.SweepPosition = FilterCutoff;

        // Map resonance (0-1) to 0.5-15 range
        DJEffects.FilterSweep.Resonance = 0.5f + FilterResonance * 14.5f;

        // Also update XY pad in DJEffects
        DJEffects.XYPadX = FilterCutoff;
        DJEffects.XYPadY = FilterResonance;
    }

    private void ApplyWetDryToEngine()
    {
        if (DJEffects == null) return;

        // Apply to flanger mix
        DJEffects.Flanger.Mix = WetDryMix;
    }

    private void ApplyPresetSettings(string presetName)
    {
        if (DJEffects == null) return;

        switch (presetName)
        {
            case "edm":
                DJEffects.SetParameter("FilterSweep.Resonance", 8f);
                DJEffects.SetParameter("FilterSweep.MinFrequency", 60f);
                DJEffects.SetParameter("FilterSweep.MaxFrequency", 18000f);
                DJEffects.SetParameter("EchoOut.Feedback", 0.7f);
                DJEffects.SetParameter("EchoOut.Damping", 0.2f);
                break;

            case "hip-hop":
                DJEffects.SetParameter("FilterSweep.Resonance", 4f);
                DJEffects.SetParameter("Brake.BrakeTime", 2f);
                DJEffects.SetParameter("Brake.CurveType", 2f);
                DJEffects.SetParameter("Backspin.SpinTime", 1.2f);
                break;

            case "house":
                DJEffects.SetParameter("FilterSweep.Resonance", 5f);
                DJEffects.SetParameter("FilterSweep.SweepRate", 0.1f);
                DJEffects.SetParameter("Flanger.Rate", 0.25f);
                DJEffects.SetParameter("Flanger.Depth", 0.004f);
                DJEffects.SetParameter("Flanger.Feedback", 0.6f);
                break;

            case "dnb":
                DJEffects.SetParameter("FilterSweep.Resonance", 10f);
                DJEffects.SetParameter("Brake.BrakeTime", 0.5f);
                DJEffects.SetParameter("Brake.CurveType", 1f);
                DJEffects.SetParameter("Backspin.SpinTime", 0.4f);
                break;

            case "techno":
                DJEffects.SetParameter("FilterSweep.Resonance", 12f);
                DJEffects.SetParameter("FilterSweep.MinFrequency", 40f);
                DJEffects.SetParameter("Flanger.Rate", 0.1f);
                DJEffects.SetParameter("Flanger.Feedback", 0.85f);
                break;

            default:
                // Default preset - reset to standard values
                DJEffects.SetParameter("FilterSweep.Resonance", 4f);
                DJEffects.SetParameter("FilterSweep.MinFrequency", 80f);
                DJEffects.SetParameter("FilterSweep.MaxFrequency", 16000f);
                DJEffects.SetParameter("EchoOut.Feedback", 0.6f);
                DJEffects.SetParameter("Brake.BrakeTime", 1.5f);
                DJEffects.SetParameter("Backspin.SpinTime", 0.8f);
                break;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Triggers a specific DJ effect.
    /// </summary>
    /// <param name="effectType">The effect type to trigger.</param>
    public void TriggerEffect(DJEffectType effectType)
    {
        DJEffects?.TriggerEffect(effectType);
        EffectChanged?.Invoke(this, new DJEffectEventArgs(effectType, true));
        UpdateActiveEffectDisplay();

        if (effectType == DJEffectType.Brake || effectType == DJEffectType.Backspin ||
            effectType == DJEffectType.NoiseBuild)
        {
            ShowProgressPanel(effectType);
        }
    }

    /// <summary>
    /// Stops a specific DJ effect.
    /// </summary>
    /// <param name="effectType">The effect type to stop.</param>
    public void StopEffect(DJEffectType effectType)
    {
        DJEffects?.StopEffect(effectType);
        EffectChanged?.Invoke(this, new DJEffectEventArgs(effectType, false));
        UpdateActiveEffectDisplay();

        if (effectType == DJEffectType.Brake || effectType == DJEffectType.Backspin ||
            effectType == DJEffectType.NoiseBuild)
        {
            HideProgressPanel();
        }
    }

    /// <summary>
    /// Stops all active effects.
    /// </summary>
    public void StopAllEffects()
    {
        KillAll_Click(this, new RoutedEventArgs());
    }

    /// <summary>
    /// Sets the XY pad position programmatically.
    /// </summary>
    /// <param name="x">X position (0.0 to 1.0).</param>
    /// <param name="y">Y position (0.0 to 1.0).</param>
    public void SetXYPadPosition(float x, float y)
    {
        FilterCutoff = Math.Clamp(x, 0f, 1f);
        FilterResonance = Math.Clamp(y, 0f, 1f);
        XYPadChanged?.Invoke(this, new XYPadChangedEventArgs(FilterCutoff, FilterResonance));
    }

    /// <summary>
    /// Refreshes the control display from the engine state.
    /// </summary>
    public void RefreshFromEngine()
    {
        if (DJEffects != null)
        {
            SyncFromDJEffects(DJEffects);
        }
    }

    #endregion
}
