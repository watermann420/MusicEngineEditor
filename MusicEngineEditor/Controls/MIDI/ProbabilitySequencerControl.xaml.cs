// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Probability Sequencer control for step-based generative patterns.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls.MIDI;

#region Value Converters

/// <summary>
/// Converts probability value (0-1) to visual height for the bar display.
/// </summary>
public class ProbabilitySequencerProbabilityToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float probability && parameter is double maxHeight)
        {
            return Math.Max(4, probability * maxHeight);
        }
        return 4.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts probability value (0-1) to color (low = red-ish, high = green/cyan).
/// </summary>
public class ProbabilitySequencerProbabilityToColorConverter : IValueConverter
{
    private static readonly Color LowProbColor = Color.FromRgb(0xFF, 0x47, 0x57);
    private static readonly Color MidProbColor = Color.FromRgb(0xFF, 0xD9, 0x3D);
    private static readonly Color HighProbColor = Color.FromRgb(0x00, 0xD9, 0xFF);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float probability)
        {
            Color color;
            if (probability < 0.5f)
            {
                // Interpolate between low and mid
                float t = probability * 2f;
                color = InterpolateColor(LowProbColor, MidProbColor, t);
            }
            else
            {
                // Interpolate between mid and high
                float t = (probability - 0.5f) * 2f;
                color = InterpolateColor(MidProbColor, HighProbColor, t);
            }
            return new SolidColorBrush(color);
        }
        return new SolidColorBrush(HighProbColor);
    }

    private static Color InterpolateColor(Color a, Color b, float t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts StepCondition enum to display string.
/// </summary>
public class ProbabilitySequencerConditionToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is StepCondition condition)
        {
            return condition switch
            {
                StepCondition.Always => "Always",
                StepCondition.EveryN => "Every N",
                StepCondition.NofM => "N of M",
                StepCondition.FirstOnly => "First",
                StepCondition.NotFirst => "Not First",
                StepCondition.Random50 => "Rnd 50%",
                StepCondition.Fill => "Fill",
                _ => "?"
            };
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts ratchet speed value to display string.
/// </summary>
public class ProbabilitySequencerRatchetSpeedToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double speed)
        {
            return speed switch
            {
                >= 1.0 => "1/1",
                >= 0.5 => "1/2",
                >= 0.25 => "1/4",
                >= 0.125 => "1/8",
                >= 0.0625 => "1/16",
                _ => "1/32"
            };
        }
        return "1/1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class ProbabilitySequencerBoolToVisibilityConverter : IValueConverter
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
/// Converts value to percentage string.
/// </summary>
public class ProbabilitySequencerPercentageConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float f)
        {
            return $"{(int)(f * 100)}%";
        }
        if (value is double d)
        {
            return $"{(int)(d * 100)}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion

/// <summary>
/// Visual data for a step in the sequencer.
/// </summary>
public class ProbabilityStepVisual
{
    public int Index { get; set; }
    public Rectangle BackgroundRect { get; set; } = null!;
    public Rectangle ProbabilityBar { get; set; } = null!;
    public Rectangle PlayIndicator { get; set; } = null!;
    public Ellipse RatchetIndicator { get; set; } = null!;
    public TextBlock ConditionIndicator { get; set; } = null!;
}

/// <summary>
/// Event args for step selection events.
/// </summary>
public class ProbabilityStepEventArgs : EventArgs
{
    public int StepIndex { get; }
    public ProbabilityStep Step { get; }

    public ProbabilityStepEventArgs(int index, ProbabilityStep step)
    {
        StepIndex = index;
        Step = step;
    }
}

/// <summary>
/// A visual control for the ProbabilitySequencer engine class.
/// Displays a step grid with probability bars, ratchet indicators, and condition icons.
/// </summary>
public partial class ProbabilitySequencerControl : UserControl
{
    #region Constants

    private static readonly Color BackgroundColor = Color.FromRgb(0x0D, 0x0D, 0x0D);
    private static readonly Color PanelColor = Color.FromRgb(0x18, 0x18, 0x18);
    private static readonly Color BorderColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color TextPrimaryColor = Color.FromRgb(0xE0, 0xE0, 0xE0);
    private static readonly Color TextSecondaryColor = Color.FromRgb(0x80, 0x80, 0x80);
    private static readonly Color DownbeatColor = Color.FromRgb(0x25, 0x25, 0x25);
    private static readonly Color OffbeatColor = Color.FromRgb(0x1E, 0x1E, 0x1E);
    private static readonly Color StepPlayingColor = Color.FromRgb(0x00, 0xFF, 0x88);

    private const double StepMinWidth = 30;
    private const double StepPadding = 2;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SequencerProperty =
        DependencyProperty.Register(nameof(Sequencer), typeof(ProbabilitySequencer), typeof(ProbabilitySequencerControl),
            new PropertyMetadata(null, OnSequencerChanged));

    /// <summary>
    /// Gets or sets the ProbabilitySequencer instance.
    /// </summary>
    public ProbabilitySequencer? Sequencer
    {
        get => (ProbabilitySequencer?)GetValue(SequencerProperty);
        set => SetValue(SequencerProperty, value);
    }

    public static readonly DependencyProperty CurrentStepProperty =
        DependencyProperty.Register(nameof(CurrentStep), typeof(int), typeof(ProbabilitySequencerControl),
            new PropertyMetadata(-1, OnCurrentStepChanged));

    /// <summary>
    /// Gets or sets the current playback step (for highlighting).
    /// </summary>
    public int CurrentStep
    {
        get => (int)GetValue(CurrentStepProperty);
        set => SetValue(CurrentStepProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a step is selected.
    /// </summary>
    public event EventHandler<ProbabilityStepEventArgs>? StepSelected;

    /// <summary>
    /// Event raised when step probability is changed.
    /// </summary>
    public event EventHandler<ProbabilityStepEventArgs>? ProbabilityChanged;

    /// <summary>
    /// Event raised when step is toggled on/off.
    /// </summary>
    public event EventHandler<ProbabilityStepEventArgs>? StepToggled;

    /// <summary>
    /// Event raised when step count changes.
    /// </summary>
    public event EventHandler<int>? StepCountChanged;

    #endregion

    #region Private Fields

    private readonly List<ProbabilityStepVisual> _stepVisuals = new();
    private readonly List<TextBlock> _stepNumberLabels = new();
    private readonly List<TextBlock> _probabilityValueLabels = new();

    private int _selectedStepIndex = -1;
    private bool _isDragging;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public ProbabilitySequencerControl()
    {
        InitializeComponent();
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Create default sequencer if none provided
        if (Sequencer == null)
        {
            Sequencer = new ProbabilitySequencer(16);
        }

        BuildStepGrid();

        SizeChanged += (_, _) => BuildStepGrid();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;

        if (Sequencer != null)
        {
            Sequencer.StepChanged -= Sequencer_StepChanged;
        }
    }

    #endregion

    #region Property Changed Handlers

    private static void OnSequencerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProbabilitySequencerControl control)
        {
            // Unsubscribe from old sequencer
            if (e.OldValue is ProbabilitySequencer oldSeq)
            {
                oldSeq.StepChanged -= control.Sequencer_StepChanged;
            }

            // Subscribe to new sequencer
            if (e.NewValue is ProbabilitySequencer newSeq)
            {
                newSeq.StepChanged += control.Sequencer_StepChanged;
            }

            if (control._isInitialized)
            {
                control.BuildStepGrid();
            }
        }
    }

    private static void OnCurrentStepChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProbabilitySequencerControl control)
        {
            control.HighlightCurrentStep((int)e.OldValue, (int)e.NewValue);
        }
    }

    private void Sequencer_StepChanged(int stepIndex)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentStep = stepIndex;
        });
    }

    #endregion

    #region Grid Building

    private void BuildStepGrid()
    {
        if (Sequencer == null || !_isInitialized) return;

        ClearVisuals();

        int stepCount = Sequencer.StepCount;
        double canvasWidth = StepGridCanvas.ActualWidth;
        double canvasHeight = StepGridCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0 || stepCount == 0)
            return;

        double stepWidth = Math.Max(StepMinWidth, canvasWidth / stepCount);

        for (int i = 0; i < stepCount; i++)
        {
            var step = Sequencer.GetStep(i);
            CreateStepVisual(i, step, stepWidth, canvasHeight);
            CreateStepNumberLabel(i, stepWidth);
            CreateProbabilityValueLabel(i, step, stepWidth);
        }

        UpdateStepCountText();
    }

    private void ClearVisuals()
    {
        StepGridCanvas.Children.Clear();
        StepNumbersCanvas.Children.Clear();
        ProbabilityValuesCanvas.Children.Clear();

        _stepVisuals.Clear();
        _stepNumberLabels.Clear();
        _probabilityValueLabels.Clear();
    }

    private void CreateStepVisual(int index, ProbabilityStep step, double stepWidth, double canvasHeight)
    {
        double x = index * stepWidth;
        bool isDownbeat = index % 4 == 0;

        var visual = new ProbabilityStepVisual { Index = index };

        // Background rectangle
        visual.BackgroundRect = new Rectangle
        {
            Width = stepWidth - StepPadding * 2,
            Height = canvasHeight,
            Fill = new SolidColorBrush(isDownbeat ? DownbeatColor : OffbeatColor),
            Stroke = new SolidColorBrush(BorderColor),
            StrokeThickness = 1,
            RadiusX = 3,
            RadiusY = 3,
            Tag = index
        };
        Canvas.SetLeft(visual.BackgroundRect, x + StepPadding);
        Canvas.SetTop(visual.BackgroundRect, 0);
        StepGridCanvas.Children.Add(visual.BackgroundRect);

        // Probability bar (from bottom)
        double barHeight = step.Probability * (canvasHeight - 8);
        var probColor = GetProbabilityColor(step.Probability);
        visual.ProbabilityBar = new Rectangle
        {
            Width = stepWidth - StepPadding * 2 - 8,
            Height = Math.Max(4, barHeight),
            Fill = new SolidColorBrush(probColor),
            RadiusX = 2,
            RadiusY = 2,
            Opacity = step.Enabled ? 1.0 : 0.3,
            Tag = index
        };
        Canvas.SetLeft(visual.ProbabilityBar, x + StepPadding + 4);
        Canvas.SetTop(visual.ProbabilityBar, canvasHeight - barHeight - 4);
        StepGridCanvas.Children.Add(visual.ProbabilityBar);

        // Play indicator (top line that lights up when step plays)
        visual.PlayIndicator = new Rectangle
        {
            Width = stepWidth - StepPadding * 2 - 4,
            Height = 3,
            Fill = new SolidColorBrush(StepPlayingColor),
            RadiusX = 1,
            RadiusY = 1,
            Opacity = 0,
            Tag = index
        };
        Canvas.SetLeft(visual.PlayIndicator, x + StepPadding + 2);
        Canvas.SetTop(visual.PlayIndicator, 2);
        StepGridCanvas.Children.Add(visual.PlayIndicator);

        // Ratchet indicator (small dots at bottom if ratchet > 1)
        visual.RatchetIndicator = new Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = new SolidColorBrush(AccentColor),
            Visibility = step.Ratchet > 1 ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(visual.RatchetIndicator, x + stepWidth / 2 - 3);
        Canvas.SetTop(visual.RatchetIndicator, canvasHeight - 12);
        StepGridCanvas.Children.Add(visual.RatchetIndicator);

        // Condition indicator (text at top if not "Always")
        visual.ConditionIndicator = new TextBlock
        {
            Text = GetConditionSymbol(step.Condition),
            Foreground = new SolidColorBrush(TextSecondaryColor),
            FontSize = 8,
            FontWeight = FontWeights.Bold,
            Visibility = step.Condition != StepCondition.Always ? Visibility.Visible : Visibility.Collapsed
        };
        Canvas.SetLeft(visual.ConditionIndicator, x + stepWidth / 2 - 6);
        Canvas.SetTop(visual.ConditionIndicator, 6);
        StepGridCanvas.Children.Add(visual.ConditionIndicator);

        _stepVisuals.Add(visual);
    }

    private void CreateStepNumberLabel(int index, double stepWidth)
    {
        double x = index * stepWidth;

        var label = new TextBlock
        {
            Text = (index + 1).ToString(),
            Foreground = new SolidColorBrush(index % 4 == 0 ? TextPrimaryColor : TextSecondaryColor),
            FontSize = 9,
            FontWeight = index % 4 == 0 ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            Width = stepWidth
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, 2);
        StepNumbersCanvas.Children.Add(label);

        _stepNumberLabels.Add(label);
    }

    private void CreateProbabilityValueLabel(int index, ProbabilityStep step, double stepWidth)
    {
        double x = index * stepWidth;

        var label = new TextBlock
        {
            Text = $"{(int)(step.Probability * 100)}%",
            Foreground = new SolidColorBrush(GetProbabilityColor(step.Probability)),
            FontSize = 8,
            TextAlignment = TextAlignment.Center,
            Width = stepWidth
        };
        Canvas.SetLeft(label, x);
        Canvas.SetTop(label, 2);
        ProbabilityValuesCanvas.Children.Add(label);

        _probabilityValueLabels.Add(label);
    }

    private void UpdateStepCountText()
    {
        if (Sequencer != null)
        {
            StepCountText.Text = $" ({Sequencer.StepCount} steps)";
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateStepVisual(int index)
    {
        if (Sequencer == null || index < 0 || index >= _stepVisuals.Count) return;

        var step = Sequencer.GetStep(index);
        var visual = _stepVisuals[index];
        double canvasHeight = StepGridCanvas.ActualHeight;

        // Update probability bar
        double barHeight = step.Probability * (canvasHeight - 8);
        visual.ProbabilityBar.Height = Math.Max(4, barHeight);
        visual.ProbabilityBar.Fill = new SolidColorBrush(GetProbabilityColor(step.Probability));
        visual.ProbabilityBar.Opacity = step.Enabled ? 1.0 : 0.3;
        Canvas.SetTop(visual.ProbabilityBar, canvasHeight - barHeight - 4);

        // Update ratchet indicator
        visual.RatchetIndicator.Visibility = step.Ratchet > 1 ? Visibility.Visible : Visibility.Collapsed;

        // Update condition indicator
        visual.ConditionIndicator.Text = GetConditionSymbol(step.Condition);
        visual.ConditionIndicator.Visibility = step.Condition != StepCondition.Always ? Visibility.Visible : Visibility.Collapsed;

        // Update probability value label
        if (index < _probabilityValueLabels.Count)
        {
            _probabilityValueLabels[index].Text = $"{(int)(step.Probability * 100)}%";
            _probabilityValueLabels[index].Foreground = new SolidColorBrush(GetProbabilityColor(step.Probability));
        }
    }

    private void HighlightCurrentStep(int oldStep, int newStep)
    {
        // Remove highlight from old step
        if (oldStep >= 0 && oldStep < _stepVisuals.Count)
        {
            _stepVisuals[oldStep].PlayIndicator.Opacity = 0;
            _stepVisuals[oldStep].BackgroundRect.Stroke = new SolidColorBrush(BorderColor);
            _stepVisuals[oldStep].BackgroundRect.StrokeThickness = 1;
        }

        // Add highlight to new step
        if (newStep >= 0 && newStep < _stepVisuals.Count)
        {
            _stepVisuals[newStep].PlayIndicator.Opacity = 1;
            _stepVisuals[newStep].BackgroundRect.Stroke = new SolidColorBrush(StepPlayingColor);
            _stepVisuals[newStep].BackgroundRect.StrokeThickness = 2;
        }
    }

    private void SelectStep(int index)
    {
        if (Sequencer == null || index < 0 || index >= Sequencer.StepCount) return;

        // Deselect previous
        if (_selectedStepIndex >= 0 && _selectedStepIndex < _stepVisuals.Count)
        {
            _stepVisuals[_selectedStepIndex].BackgroundRect.Stroke = new SolidColorBrush(BorderColor);
        }

        _selectedStepIndex = index;

        // Select new
        _stepVisuals[index].BackgroundRect.Stroke = new SolidColorBrush(AccentColor);
        _stepVisuals[index].BackgroundRect.StrokeThickness = 2;

        // Update detail panel
        var step = Sequencer.GetStep(index);
        SelectedStepText.Text = (index + 1).ToString();
        ProbabilitySlider.Value = step.Probability * 100;
        ProbabilityValueText.Text = $"{(int)(step.Probability * 100)}%";

        // Update ratchet combo
        for (int i = 0; i < RatchetCountCombo.Items.Count; i++)
        {
            if (RatchetCountCombo.Items[i] is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int val))
            {
                if (val == step.Ratchet)
                {
                    RatchetCountCombo.SelectedIndex = i;
                    break;
                }
            }
        }

        // Update condition combo
        UpdateConditionComboSelection(step.Condition);

        StepDetailPanel.Visibility = Visibility.Visible;

        StepSelected?.Invoke(this, new ProbabilityStepEventArgs(index, step));
    }

    private void UpdateConditionComboSelection(StepCondition condition)
    {
        string tagToFind = condition switch
        {
            StepCondition.Always => "Always",
            StepCondition.EveryN => "EveryN",
            StepCondition.NofM => "EveryN4",
            StepCondition.FirstOnly => "FirstOnly",
            StepCondition.NotFirst => "NotFirst",
            StepCondition.Random50 => "Random50",
            StepCondition.Fill => "Fill",
            _ => "Always"
        };

        for (int i = 0; i < ConditionCombo.Items.Count; i++)
        {
            if (ConditionCombo.Items[i] is ComboBoxItem item && item.Tag?.ToString() == tagToFind)
            {
                ConditionCombo.SelectedIndex = i;
                break;
            }
        }
    }

    #endregion

    #region Helper Methods

    private static Color GetProbabilityColor(float probability)
    {
        var lowColor = Color.FromRgb(0xFF, 0x47, 0x57);
        var midColor = Color.FromRgb(0xFF, 0xD9, 0x3D);
        var highColor = Color.FromRgb(0x00, 0xD9, 0xFF);

        if (probability < 0.5f)
        {
            float t = probability * 2f;
            return Color.FromRgb(
                (byte)(lowColor.R + (midColor.R - lowColor.R) * t),
                (byte)(lowColor.G + (midColor.G - lowColor.G) * t),
                (byte)(lowColor.B + (midColor.B - lowColor.B) * t));
        }
        else
        {
            float t = (probability - 0.5f) * 2f;
            return Color.FromRgb(
                (byte)(midColor.R + (highColor.R - midColor.R) * t),
                (byte)(midColor.G + (highColor.G - midColor.G) * t),
                (byte)(midColor.B + (highColor.B - midColor.B) * t));
        }
    }

    private static string GetConditionSymbol(StepCondition condition)
    {
        return condition switch
        {
            StepCondition.EveryN => "2",
            StepCondition.NofM => "4",
            StepCondition.FirstOnly => "1st",
            StepCondition.NotFirst => "!1",
            StepCondition.Random50 => "?",
            StepCondition.Fill => "F",
            _ => ""
        };
    }

    private int GetStepAtPosition(double x)
    {
        if (Sequencer == null || StepGridCanvas.ActualWidth <= 0)
            return -1;

        double stepWidth = StepGridCanvas.ActualWidth / Sequencer.StepCount;
        int step = (int)(x / stepWidth);
        return Math.Clamp(step, 0, Sequencer.StepCount - 1);
    }

    private float GetProbabilityAtPosition(double y)
    {
        double canvasHeight = StepGridCanvas.ActualHeight;
        if (canvasHeight <= 0) return 1f;

        // Invert Y (top = 100%, bottom = 0%)
        float probability = (float)(1.0 - y / canvasHeight);
        return Math.Clamp(probability, 0f, 1f);
    }

    #endregion

    #region Event Handlers - Mouse

    private void StepGridCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(StepGridCanvas);
        int stepIndex = GetStepAtPosition(pos.X);

        if (stepIndex >= 0)
        {
            SelectStep(stepIndex);
            _isDragging = true;
            StepGridCanvas.CaptureMouse();

            // Set probability based on Y position
            float probability = GetProbabilityAtPosition(pos.Y);
            SetStepProbability(stepIndex, probability);
        }
    }

    private void StepGridCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || Sequencer == null) return;

        var pos = e.GetPosition(StepGridCanvas);
        int stepIndex = GetStepAtPosition(pos.X);

        if (stepIndex >= 0)
        {
            float probability = GetProbabilityAtPosition(pos.Y);
            SetStepProbability(stepIndex, probability);
        }
    }

    private void StepGridCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        StepGridCanvas.ReleaseMouseCapture();
    }

    private void StepGridCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(StepGridCanvas);
        int stepIndex = GetStepAtPosition(pos.X);

        if (stepIndex >= 0 && Sequencer != null)
        {
            var step = Sequencer.GetStep(stepIndex);
            step.Enabled = !step.Enabled;
            UpdateStepVisual(stepIndex);

            StepToggled?.Invoke(this, new ProbabilityStepEventArgs(stepIndex, step));
        }
    }

    private void StepGridCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var pos = e.GetPosition(StepGridCanvas);
        int stepIndex = GetStepAtPosition(pos.X);

        if (stepIndex >= 0 && Sequencer != null)
        {
            var step = Sequencer.GetStep(stepIndex);
            float delta = e.Delta > 0 ? 0.05f : -0.05f;
            float newProbability = Math.Clamp(step.Probability + delta, 0f, 1f);

            Sequencer.SetProbability(stepIndex, newProbability);
            UpdateStepVisual(stepIndex);

            if (stepIndex == _selectedStepIndex)
            {
                ProbabilitySlider.Value = newProbability * 100;
                ProbabilityValueText.Text = $"{(int)(newProbability * 100)}%";
            }

            ProbabilityChanged?.Invoke(this, new ProbabilityStepEventArgs(stepIndex, step));
            e.Handled = true;
        }
    }

    private void SetStepProbability(int stepIndex, float probability)
    {
        if (Sequencer == null) return;

        Sequencer.SetProbability(stepIndex, probability);
        UpdateStepVisual(stepIndex);

        if (stepIndex == _selectedStepIndex)
        {
            ProbabilitySlider.Value = probability * 100;
            ProbabilityValueText.Text = $"{(int)(probability * 100)}%";
        }

        var step = Sequencer.GetStep(stepIndex);
        ProbabilityChanged?.Invoke(this, new ProbabilityStepEventArgs(stepIndex, step));
    }

    #endregion

    #region Event Handlers - Controls

    private void StepCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sequencer == null || !_isInitialized) return;

        if (StepCountCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int count))
        {
            Sequencer.StepCount = count;
            BuildStepGrid();
            _selectedStepIndex = -1;
            StepDetailPanel.Visibility = Visibility.Collapsed;

            StepCountChanged?.Invoke(this, count);
        }
    }

    private void StepLengthCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sequencer == null || !_isInitialized) return;

        if (StepLengthCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && double.TryParse(tagStr, out double length))
        {
            Sequencer.StepLength = length;
        }
    }

    private void FillModeToggle_Click(object sender, RoutedEventArgs e)
    {
        if (Sequencer != null)
        {
            Sequencer.FillMode = FillModeToggle.IsChecked == true;
        }
    }

    private void RandomizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (Sequencer != null)
        {
            Sequencer.RandomizeProbabilities(0.2f, 1.0f);
            BuildStepGrid();
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        if (Sequencer != null)
        {
            Sequencer.SetAllProbabilities(1.0f);
            for (int i = 0; i < Sequencer.StepCount; i++)
            {
                var step = Sequencer.GetStep(i);
                step.Enabled = true;
                step.Ratchet = 1;
                step.Condition = StepCondition.Always;
            }
            BuildStepGrid();
        }
    }

    private void ProbabilitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Sequencer == null || _selectedStepIndex < 0 || !_isInitialized) return;

        float probability = (float)(e.NewValue / 100.0);
        Sequencer.SetProbability(_selectedStepIndex, probability);
        ProbabilityValueText.Text = $"{(int)(probability * 100)}%";
        UpdateStepVisual(_selectedStepIndex);

        var step = Sequencer.GetStep(_selectedStepIndex);
        ProbabilityChanged?.Invoke(this, new ProbabilityStepEventArgs(_selectedStepIndex, step));
    }

    private void RatchetCountCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sequencer == null || _selectedStepIndex < 0 || !_isInitialized) return;

        if (RatchetCountCombo.SelectedItem is ComboBoxItem item && int.TryParse(item.Content?.ToString(), out int ratchet))
        {
            var step = Sequencer.GetStep(_selectedStepIndex);
            step.Ratchet = ratchet;
            UpdateStepVisual(_selectedStepIndex);
        }
    }

    private void RatchetSpeedCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sequencer == null || _selectedStepIndex < 0 || !_isInitialized) return;

        if (RatchetSpeedCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && double.TryParse(tagStr, out double speed))
        {
            var step = Sequencer.GetStep(_selectedStepIndex);
            step.Duration = speed;
        }
    }

    private void ConditionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Sequencer == null || _selectedStepIndex < 0 || !_isInitialized) return;

        if (ConditionCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            var step = Sequencer.GetStep(_selectedStepIndex);
            step.Condition = tag switch
            {
                "Always" => StepCondition.Always,
                "EveryN" => StepCondition.EveryN,
                "EveryN4" => StepCondition.NofM,
                "FirstOnly" => StepCondition.FirstOnly,
                "NotFirst" => StepCondition.NotFirst,
                "Random50" => StepCondition.Random50,
                "Fill" => StepCondition.Fill,
                _ => StepCondition.Always
            };

            // Set condition param based on selection
            if (tag == "EveryN")
            {
                step.ConditionParam = 2;
            }
            else if (tag == "EveryN4")
            {
                step.Condition = StepCondition.EveryN;
                step.ConditionParam = 4;
            }

            UpdateStepVisual(_selectedStepIndex);
        }
    }

    private void CloseDetailPanel_Click(object sender, RoutedEventArgs e)
    {
        StepDetailPanel.Visibility = Visibility.Collapsed;

        // Deselect step
        if (_selectedStepIndex >= 0 && _selectedStepIndex < _stepVisuals.Count)
        {
            _stepVisuals[_selectedStepIndex].BackgroundRect.Stroke = new SolidColorBrush(BorderColor);
            _stepVisuals[_selectedStepIndex].BackgroundRect.StrokeThickness = 1;
        }
        _selectedStepIndex = -1;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the visual display from the sequencer data.
    /// </summary>
    public void Refresh()
    {
        BuildStepGrid();
    }

    /// <summary>
    /// Gets the pattern generated by the current sequencer state.
    /// </summary>
    /// <param name="synth">The synth to use for the pattern.</param>
    /// <param name="iterations">Number of iterations to generate.</param>
    /// <returns>Generated pattern.</returns>
    public MusicEngine.Core.Pattern? GeneratePattern(ISynth synth, int iterations = 1)
    {
        return Sequencer?.GeneratePattern(synth, iterations);
    }

    /// <summary>
    /// Loads a preset pattern.
    /// </summary>
    /// <param name="presetName">Name of the preset (kick, hihat, melodic).</param>
    public void LoadPreset(string presetName)
    {
        ProbabilitySequencer? newSequencer = presetName.ToLowerInvariant() switch
        {
            "kick" => ProbabilitySequencer.CreateKickPattern(),
            "hihat" => ProbabilitySequencer.CreateHiHatPattern(),
            "melodic" => ProbabilitySequencer.CreateMelodicPattern(new[] { 0, 2, 4, 5, 7, 9, 11 }), // C major
            _ => null
        };

        if (newSequencer != null)
        {
            Sequencer = newSequencer;
        }
    }

    /// <summary>
    /// Starts playback.
    /// </summary>
    public void Start()
    {
        Sequencer?.Start();
    }

    /// <summary>
    /// Stops playback.
    /// </summary>
    public void Stop()
    {
        Sequencer?.Stop();
    }

    /// <summary>
    /// Resets the sequencer to the beginning.
    /// </summary>
    public void Reset()
    {
        Sequencer?.Reset();
        CurrentStep = -1;
    }

    #endregion
}
