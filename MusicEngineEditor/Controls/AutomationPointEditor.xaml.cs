// MusicEngineEditor - Automation Point Editor Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngine.Core.Automation;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A popup editor control for editing automation point properties.
/// Allows editing time, value, and curve type with various input formats.
/// </summary>
public partial class AutomationPointEditor : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty PointProperty =
        DependencyProperty.Register(nameof(Point), typeof(AutomationPoint), typeof(AutomationPointEditor),
            new PropertyMetadata(null, OnPointChanged));

    public static readonly DependencyProperty MinValueProperty =
        DependencyProperty.Register(nameof(MinValue), typeof(float), typeof(AutomationPointEditor),
            new PropertyMetadata(0f, OnRangeChanged));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(float), typeof(AutomationPointEditor),
            new PropertyMetadata(1f, OnRangeChanged));

    public static readonly DependencyProperty UnitProperty =
        DependencyProperty.Register(nameof(Unit), typeof(string), typeof(AutomationPointEditor),
            new PropertyMetadata(string.Empty, OnUnitChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(AutomationPointEditor),
            new PropertyMetadata(120.0));

    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(nameof(BeatsPerBar), typeof(int), typeof(AutomationPointEditor),
            new PropertyMetadata(4));

    /// <summary>
    /// Gets or sets the automation point being edited.
    /// </summary>
    public AutomationPoint? Point
    {
        get => (AutomationPoint?)GetValue(PointProperty);
        set => SetValue(PointProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum value for the parameter.
    /// </summary>
    public float MinValue
    {
        get => (float)GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum value for the parameter.
    /// </summary>
    public float MaxValue
    {
        get => (float)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    /// <summary>
    /// Gets or sets the unit string to display.
    /// </summary>
    public string Unit
    {
        get => (string)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    /// <summary>
    /// Gets or sets the BPM for time conversion.
    /// </summary>
    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    /// <summary>
    /// Gets or sets the beats per bar for bar:beat format.
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when the point is modified and applied.
    /// </summary>
    public event EventHandler<PointEditedEventArgs>? PointEdited;

    /// <summary>
    /// Fired when deletion is requested.
    /// </summary>
    public event EventHandler<AutomationPoint>? DeleteRequested;

    /// <summary>
    /// Fired when the editor is cancelled.
    /// </summary>
    public event EventHandler? Cancelled;

    #endregion

    #region Private Fields

    private bool _isUpdating;
    private double _editedTime;
    private float _editedValue;
    private AutomationCurveType _editedCurveType;
    private float _editedTension;
    private TimeFormat _currentTimeFormat = TimeFormat.Beats;

    #endregion

    #region Constructor

    public AutomationPointEditor()
    {
        InitializeComponent();
    }

    #endregion

    #region Property Changed Handlers

    private static void OnPointChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPointEditor editor)
        {
            editor.LoadPointData();
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPointEditor editor)
        {
            editor.UpdateValueDisplay();
        }
    }

    private static void OnUnitChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationPointEditor editor)
        {
            editor.UnitText.Text = e.NewValue as string ?? string.Empty;
        }
    }

    #endregion

    #region Data Loading

    private void LoadPointData()
    {
        if (Point == null) return;

        _isUpdating = true;
        try
        {
            _editedTime = Point.Time;
            _editedValue = Point.Value;
            _editedCurveType = Point.CurveType;
            _editedTension = Point.Tension;

            UpdateTimeDisplay();
            UpdateValueDisplay();
            UpdateCurveTypeDisplay();
            UpdateTensionDisplay();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateTimeDisplay()
    {
        _isUpdating = true;
        try
        {
            TimeTextBox.Text = FormatTime(_editedTime, _currentTimeFormat);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateValueDisplay()
    {
        _isUpdating = true;
        try
        {
            ValueTextBox.Text = _editedValue.ToString("F3", CultureInfo.InvariantCulture);

            // Update slider (normalized 0-1)
            float range = MaxValue - MinValue;
            if (Math.Abs(range) > float.Epsilon)
            {
                float normalized = (_editedValue - MinValue) / range;
                ValueSlider.Value = Math.Clamp(normalized, 0, 1);
                PercentText.Text = $"{normalized * 100:F0}%";
            }
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateCurveTypeDisplay()
    {
        _isUpdating = true;
        try
        {
            CurveTypeCombo.SelectedIndex = _editedCurveType switch
            {
                AutomationCurveType.Linear => 0,
                AutomationCurveType.Bezier => 1,
                AutomationCurveType.Step => 2,
                AutomationCurveType.Exponential => 3,
                AutomationCurveType.Logarithmic => 4,
                AutomationCurveType.SCurve => 5,
                AutomationCurveType.FastAttack => 6,
                AutomationCurveType.SlowAttack => 7,
                _ => 0
            };

            BezierControls.Visibility = _editedCurveType == AutomationCurveType.Bezier
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void UpdateTensionDisplay()
    {
        _isUpdating = true;
        try
        {
            TensionSlider.Value = _editedTension;
            TensionText.Text = _editedTension.ToString("F2", CultureInfo.InvariantCulture);
        }
        finally
        {
            _isUpdating = false;
        }
    }

    #endregion

    #region Time Format Helpers

    private string FormatTime(double time, TimeFormat format)
    {
        return format switch
        {
            TimeFormat.Beats => time.ToString("F3", CultureInfo.InvariantCulture),
            TimeFormat.BarBeat => FormatBarBeat(time),
            TimeFormat.Seconds => FormatSeconds(time),
            _ => time.ToString("F3", CultureInfo.InvariantCulture)
        };
    }

    private string FormatBarBeat(double beats)
    {
        int bar = (int)(beats / BeatsPerBar) + 1;
        double beat = (beats % BeatsPerBar) + 1;
        return $"{bar}:{beat:F2}";
    }

    private string FormatSeconds(double beats)
    {
        double seconds = beats * (60.0 / Bpm);
        return $"{seconds:F3}s";
    }

    private double ParseTime(string text, TimeFormat format)
    {
        text = text.Trim().TrimEnd('s');

        return format switch
        {
            TimeFormat.Beats => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var beats) ? beats : _editedTime,
            TimeFormat.BarBeat => ParseBarBeat(text),
            TimeFormat.Seconds => ParseSeconds(text),
            _ => _editedTime
        };
    }

    private double ParseBarBeat(string text)
    {
        var parts = text.Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int bar) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beat))
        {
            return (bar - 1) * BeatsPerBar + (beat - 1);
        }
        return _editedTime;
    }

    private double ParseSeconds(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
        {
            return seconds * (Bpm / 60.0);
        }
        return _editedTime;
    }

    #endregion

    #region UI Event Handlers

    private void TimeTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        _editedTime = ParseTime(TimeTextBox.Text, _currentTimeFormat);
    }

    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdating) return;

        if (float.TryParse(ValueTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            _editedValue = Math.Clamp(value, MinValue, MaxValue);
            UpdateValueDisplay();
        }
    }

    private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        float normalized = (float)e.NewValue;
        _editedValue = MinValue + normalized * (MaxValue - MinValue);

        _isUpdating = true;
        ValueTextBox.Text = _editedValue.ToString("F3", CultureInfo.InvariantCulture);
        PercentText.Text = $"{normalized * 100:F0}%";
        _isUpdating = false;
    }

    private void TimeFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _currentTimeFormat = (TimeFormat)TimeFormatCombo.SelectedIndex;
        UpdateTimeDisplay();
    }

    private void CurveTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        _editedCurveType = CurveTypeCombo.SelectedIndex switch
        {
            0 => AutomationCurveType.Linear,
            1 => AutomationCurveType.Bezier,
            2 => AutomationCurveType.Step,
            3 => AutomationCurveType.Exponential,
            4 => AutomationCurveType.Logarithmic,
            5 => AutomationCurveType.SCurve,
            6 => AutomationCurveType.FastAttack,
            7 => AutomationCurveType.SlowAttack,
            _ => AutomationCurveType.Linear
        };

        BezierControls.Visibility = _editedCurveType == AutomationCurveType.Bezier
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void TensionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdating) return;

        _editedTension = (float)e.NewValue;
        TensionText.Text = _editedTension.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyChanges();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEdit();
            e.Handled = true;
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        ApplyChanges();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEdit();
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Point != null)
        {
            DeleteRequested?.Invoke(this, Point);
        }
    }

    #endregion

    #region Actions

    private void ApplyChanges()
    {
        if (Point == null) return;

        var args = new PointEditedEventArgs(
            Point,
            _editedTime,
            _editedValue,
            _editedCurveType,
            _editedTension);

        PointEdited?.Invoke(this, args);
    }

    private void CancelEdit()
    {
        LoadPointData(); // Reset to original values
        Cancelled?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Enums

    private enum TimeFormat
    {
        Beats = 0,
        BarBeat = 1,
        Seconds = 2
    }

    #endregion
}

/// <summary>
/// Event arguments for point edited events.
/// </summary>
public class PointEditedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the original point being edited.
    /// </summary>
    public AutomationPoint OriginalPoint { get; }

    /// <summary>
    /// Gets the new time value.
    /// </summary>
    public double NewTime { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public float NewValue { get; }

    /// <summary>
    /// Gets the new curve type.
    /// </summary>
    public AutomationCurveType NewCurveType { get; }

    /// <summary>
    /// Gets the new tension value.
    /// </summary>
    public float NewTension { get; }

    /// <summary>
    /// Creates a new PointEditedEventArgs.
    /// </summary>
    public PointEditedEventArgs(
        AutomationPoint originalPoint,
        double newTime,
        float newValue,
        AutomationCurveType newCurveType,
        float newTension)
    {
        OriginalPoint = originalPoint;
        NewTime = newTime;
        NewValue = newValue;
        NewCurveType = newCurveType;
        NewTension = newTension;
    }
}
