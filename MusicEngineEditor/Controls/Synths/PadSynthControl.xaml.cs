// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Pad Synthesizer Editor control.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.ViewModels.Synths;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Converter to calculate harmonic bar height from amplitude and container height.
/// </summary>
public class HarmonicBarHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
            return 0.0;

        // Get amplitude (0-1 range)
        double amplitude = 0;
        if (values[0] is float floatAmp)
            amplitude = floatAmp;
        else if (values[0] is double doubleAmp)
            amplitude = doubleAmp;

        // Get container height
        double containerHeight = 0;
        if (values[1] is double height)
            containerHeight = height;

        // Calculate bar height (amplitude * container height)
        return Math.Max(0, amplitude * containerHeight);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Interaction logic for PadSynthControl.xaml.
/// </summary>
public partial class PadSynthControl : UserControl
{
    private bool _isDragging;
    private HarmonicBarViewModel? _selectedHarmonic;

    /// <summary>
    /// Creates a new PadSynthControl.
    /// </summary>
    public PadSynthControl()
    {
        InitializeComponent();
    }

    private PadSynthViewModel? ViewModel => DataContext as PadSynthViewModel;

    #region Harmonic Bar Handlers

    private void HarmonicBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            // Get the harmonic from the DataContext
            if (element.DataContext is HarmonicBarViewModel harmonic)
            {
                _isDragging = true;
                _selectedHarmonic = harmonic;
                element.CaptureMouse();
                UpdateHarmonicLevel(e.GetPosition(element), element);
                e.Handled = true;
            }
        }
    }

    private void HarmonicBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging && sender is FrameworkElement element && _selectedHarmonic != null)
        {
            UpdateHarmonicLevel(e.GetPosition(element), element);
        }
    }

    private void HarmonicBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            _selectedHarmonic = null;
            if (sender is FrameworkElement element)
            {
                element.ReleaseMouseCapture();
            }
            e.Handled = true;
        }
    }

    private void HarmonicBar_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is HarmonicBarViewModel harmonic)
        {
            harmonic.IsHovered = true;
        }
    }

    private void HarmonicBar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is HarmonicBarViewModel harmonic)
        {
            if (!_isDragging)
            {
                harmonic.IsHovered = false;
            }
        }
    }

    private void UpdateHarmonicLevel(Point position, FrameworkElement element)
    {
        if (_selectedHarmonic == null) return;

        // Calculate level from Y position (inverted, top = max)
        double containerHeight = element.ActualHeight;
        if (containerHeight <= 0) return;

        var level = Math.Clamp(1.0 - position.Y / containerHeight, 0, 1);

        // Update harmonic amplitude
        _selectedHarmonic.Amplitude = (float)level;
    }

    #endregion
}
