// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Rotary knob control for synthesizer parameters.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// A rotary knob control for synthesizer parameters.
/// </summary>
public partial class KnobControl : UserControl
{
    private bool _isDragging;
    private Point _lastMousePosition;
    private const double RotationRange = 270.0; // Total rotation range in degrees
    private const double MinAngle = -135.0;

    #region Dependency Properties

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(KnobControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(KnobControl),
            new PropertyMetadata(0.0, OnRangeChanged));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(KnobControl),
            new PropertyMetadata(1.0, OnRangeChanged));

    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(KnobControl),
            new PropertyMetadata(string.Empty, OnLabelChanged));

    public static readonly DependencyProperty ValueFormatProperty =
        DependencyProperty.Register(nameof(ValueFormat), typeof(string), typeof(KnobControl),
            new PropertyMetadata("F2", OnValueChanged));

    /// <summary>
    /// Gets or sets the current value.
    /// </summary>
    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, Math.Clamp(value, Minimum, Maximum));
    }

    /// <summary>
    /// Gets or sets the minimum value.
    /// </summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum value.
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    /// <summary>
    /// Gets or sets the label text.
    /// </summary>
    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>
    /// Gets or sets the format string for the value display.
    /// </summary>
    public string ValueFormat
    {
        get => (string)GetValue(ValueFormatProperty);
        set => SetValue(ValueFormatProperty, value);
    }

    #endregion

    public KnobControl()
    {
        InitializeComponent();
        UpdateDisplay();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KnobControl knob)
        {
            knob.UpdateDisplay();
        }
    }

    private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KnobControl knob)
        {
            knob.UpdateDisplay();
        }
    }

    private static void OnLabelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is KnobControl knob)
        {
            knob.LabelText.Text = e.NewValue?.ToString() ?? string.Empty;
        }
    }

    private void UpdateDisplay()
    {
        // Update rotation
        var normalizedValue = (Value - Minimum) / (Maximum - Minimum);
        var angle = MinAngle + normalizedValue * RotationRange;
        KnobRotation.Angle = angle;

        // Update value text
        ValueText.Text = Value.ToString(ValueFormat);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        _isDragging = true;
        _lastMousePosition = e.GetPosition(this);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        _isDragging = false;
        ReleaseMouseCapture();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!_isDragging) return;

        var currentPosition = e.GetPosition(this);
        var deltaY = _lastMousePosition.Y - currentPosition.Y;

        // Sensitivity factor
        var sensitivity = (Maximum - Minimum) / 200.0;
        var newValue = Value + deltaY * sensitivity;
        Value = Math.Clamp(newValue, Minimum, Maximum);

        _lastMousePosition = currentPosition;
        e.Handled = true;
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var sensitivity = (Maximum - Minimum) / 100.0;
        var delta = e.Delta > 0 ? sensitivity : -sensitivity;
        Value = Math.Clamp(Value + delta, Minimum, Maximum);
        e.Handled = true;
    }
}
