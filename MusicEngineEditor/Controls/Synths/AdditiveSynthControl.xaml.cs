// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Additive Synthesizer Editor control.

using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for AdditiveSynthControl.xaml.
/// Provides a visual editor for additive synthesis with harmonic bars,
/// Hammond-style drawbars, and waveform preview.
/// </summary>
public partial class AdditiveSynthControl : UserControl
{
    #region Static Converters

    /// <summary>
    /// Converter for Color string to Brush.
    /// </summary>
    public static AdditiveColorToBrushConverter ColorToBrushConverter { get; } = new();

    /// <summary>
    /// Converter for bool to Visibility (true = Visible).
    /// </summary>
    public static AdditiveBoolToVisibilityConverter BoolToVisibilityConverter { get; } = new();

    /// <summary>
    /// Converter for bool to Visibility (true = Collapsed).
    /// </summary>
    public static AdditiveInverseBoolToVisibilityConverter InverseBoolToVisibilityConverter { get; } = new();

    /// <summary>
    /// Converter for null to Visibility (null = Collapsed, non-null = Visible).
    /// </summary>
    public static AdditiveNullToVisibilityConverter NullToVisibilityConverter { get; } = new();

    /// <summary>
    /// Converter for null to Visibility (null = Visible, non-null = Collapsed).
    /// </summary>
    public static AdditiveNullToCollapsedConverter NullToCollapsedConverter { get; } = new();

    /// <summary>
    /// Converter for mode text display.
    /// </summary>
    public static AdditiveModeTextConverter ModeTextConverter { get; } = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new AdditiveSynthControl.
    /// </summary>
    public AdditiveSynthControl()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    #endregion

    #region Properties

    private AdditiveSynthViewModel? ViewModel => DataContext as AdditiveSynthViewModel;

    #endregion

    #region Event Handlers

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is AdditiveSynthViewModel oldVm)
        {
            oldVm.WaveformChanged -= OnWaveformChanged;
        }

        if (e.NewValue is AdditiveSynthViewModel newVm)
        {
            newVm.WaveformChanged += OnWaveformChanged;
            DrawWaveform();
        }
    }

    private void OnWaveformChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(DrawWaveform);
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawWaveform();
    }

    private void EvenOddBalanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel == null) return;

        // Apply even/odd harmonic balance
        // Negative values emphasize odd harmonics, positive values emphasize even harmonics
        float balance = (float)e.NewValue;

        foreach (var harmonic in ViewModel.Harmonics)
        {
            if (harmonic.Amplitude > 0.001f)
            {
                bool isOdd = harmonic.HarmonicNumber % 2 == 1;

                if (balance < 0)
                {
                    // Emphasize odd harmonics
                    float factor = isOdd ? 1.0f : 1.0f + balance;
                    // Don't modify amplitude directly here - this is just a visual indicator
                }
                else if (balance > 0)
                {
                    // Emphasize even harmonics
                    float factor = isOdd ? 1.0f - balance : 1.0f;
                    // Don't modify amplitude directly here - this is just a visual indicator
                }
            }
        }

        // The actual balance application would be done via a command if needed
        // For now, this slider provides visual feedback only
    }

    #endregion

    #region Waveform Drawing

    private void DrawWaveform()
    {
        if (ViewModel == null || WaveformCanvas == null) return;

        WaveformCanvas.Children.Clear();

        var waveformData = ViewModel.WaveformData;
        if (waveformData == null || waveformData.Length == 0) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw center line
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = height / 2,
            X2 = width,
            Y2 = height / 2,
            Stroke = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            StrokeThickness = 1
        };
        WaveformCanvas.Children.Add(centerLine);

        // Draw waveform
        var polyline = new Polyline
        {
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        for (int i = 0; i < waveformData.Length; i++)
        {
            double x = (double)i / (waveformData.Length - 1) * width;
            double y = (1 - waveformData[i]) / 2 * height;
            polyline.Points.Add(new Point(x, y));
        }

        WaveformCanvas.Children.Add(polyline);

        // Draw grid lines
        for (int i = 1; i < 4; i++)
        {
            double y = height * i / 4;
            var gridLine = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            WaveformCanvas.Children.Add(gridLine);
        }
    }

    #endregion
}

#region Converters

/// <summary>
/// Converts Color string to SolidColorBrush for Additive Synth.
/// </summary>
public class AdditiveColorToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is System.Windows.Media.Color color)
        {
            return new SolidColorBrush(color);
        }
        if (value is string colorString)
        {
            try
            {
                var convertedColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
                return new SolidColorBrush(convertedColor);
            }
            catch
            {
                return Brushes.Gray;
            }
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to Visibility (true = Visible, false = Collapsed) for Additive Synth.
/// </summary>
public class AdditiveBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts bool to Visibility (true = Collapsed, false = Visible) for Additive Synth.
/// </summary>
public class AdditiveInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Collapsed;
        }
        return false;
    }
}

/// <summary>
/// Converts null to Collapsed, non-null to Visible for Additive Synth.
/// </summary>
public class AdditiveNullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts null to Visible, non-null to Collapsed for Additive Synth.
/// </summary>
public class AdditiveNullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts bool to mode text (true = "Drawbar", false = "Harmonic") for Additive Synth.
/// </summary>
public class AdditiveModeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isDrawbarMode)
        {
            return isDrawbarMode ? "Drawbar" : "Harmonic";
        }
        return "Harmonic";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
