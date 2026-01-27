// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Converter that converts a normalized value (0-1) to a width based on parent width.
/// </summary>
public class PercentageToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double normalizedValue)
        {
            // Return a width that will be used with ActualWidth binding
            // For now, return as percentage for star sizing
            return Math.Max(0, Math.Min(1, normalizedValue)) * 50; // Max width ~50px
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that shows/hides based on string content.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && !string.IsNullOrWhiteSpace(str))
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts boolean to visibility.
/// </summary>
public class LatencyBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue)
        {
            return Visibility.Visible;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// A compact indicator showing per-track latency with a horizontal bar.
/// Color-coded based on latency severity (green/yellow/red).
/// </summary>
public partial class TrackLatencyIndicator : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Dependency property for the track latency info.
    /// </summary>
    public static readonly DependencyProperty LatencyInfoProperty =
        DependencyProperty.Register(
            nameof(LatencyInfo),
            typeof(TrackLatencyInfo),
            typeof(TrackLatencyIndicator),
            new PropertyMetadata(null, OnLatencyInfoChanged));

    /// <summary>
    /// Dependency property for the track name.
    /// </summary>
    public static readonly DependencyProperty TrackNameProperty =
        DependencyProperty.Register(
            nameof(TrackName),
            typeof(string),
            typeof(TrackLatencyIndicator),
            new PropertyMetadata(string.Empty));

    /// <summary>
    /// Dependency property for latency in samples.
    /// </summary>
    public static readonly DependencyProperty LatencySamplesProperty =
        DependencyProperty.Register(
            nameof(LatencySamples),
            typeof(int),
            typeof(TrackLatencyIndicator),
            new PropertyMetadata(0));

    /// <summary>
    /// Dependency property for latency in milliseconds.
    /// </summary>
    public static readonly DependencyProperty LatencyMsProperty =
        DependencyProperty.Register(
            nameof(LatencyMs),
            typeof(double),
            typeof(TrackLatencyIndicator),
            new PropertyMetadata(0.0));

    /// <summary>
    /// Gets or sets the track latency information.
    /// </summary>
    public TrackLatencyInfo? LatencyInfo
    {
        get => (TrackLatencyInfo?)GetValue(LatencyInfoProperty);
        set => SetValue(LatencyInfoProperty, value);
    }

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    public string TrackName
    {
        get => (string)GetValue(TrackNameProperty);
        set => SetValue(TrackNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the latency in samples.
    /// </summary>
    public int LatencySamples
    {
        get => (int)GetValue(LatencySamplesProperty);
        set => SetValue(LatencySamplesProperty, value);
    }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public double LatencyMs
    {
        get => (double)GetValue(LatencyMsProperty);
        set => SetValue(LatencyMsProperty, value);
    }

    private static void OnLatencyInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackLatencyIndicator indicator && e.NewValue is TrackLatencyInfo info)
        {
            indicator.DataContext = info;
        }
    }

    #endregion

    /// <summary>
    /// Creates a new TrackLatencyIndicator.
    /// </summary>
    public TrackLatencyIndicator()
    {
        InitializeComponent();

        // Add converters to resources
        if (!Resources.Contains("PercentageToWidthConverter"))
        {
            Resources.Add("PercentageToWidthConverter", new PercentageToWidthConverter());
        }
        if (!Resources.Contains("StringToVisibilityConverter"))
        {
            Resources.Add("StringToVisibilityConverter", new StringToVisibilityConverter());
        }
    }
}
