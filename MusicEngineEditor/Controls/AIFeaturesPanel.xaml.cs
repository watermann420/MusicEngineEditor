// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: AI Features Panel control for AI-powered audio processing.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// AI Features Panel providing access to AI-powered audio processing tools
/// including denoising, declipping, chord suggestion, melody generation,
/// mix assistant, mastering assistant, and stem separation.
/// </summary>
public partial class AIFeaturesPanel : UserControl
{
    /// <summary>
    /// Gets the ViewModel for this panel.
    /// </summary>
    public AIFeaturesViewModel ViewModel => (AIFeaturesViewModel)DataContext;

    /// <summary>
    /// Creates a new AI Features Panel.
    /// </summary>
    public AIFeaturesPanel()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Sets the ViewModel for this panel.
    /// </summary>
    /// <param name="viewModel">The ViewModel to use.</param>
    public void SetViewModel(AIFeaturesViewModel viewModel)
    {
        DataContext = viewModel;
    }
}

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
public class AIPanelInverseBooleanConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelInverseBooleanConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return value;
    }
}

/// <summary>
/// Converts a boolean value to Visibility (true = Visible, false = Collapsed).
/// </summary>
public class AIPanelBoolToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility == Visibility.Visible;
        return false;
    }
}

/// <summary>
/// Converts a non-null object to true, null to false.
/// </summary>
public class AIPanelNotNullConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelNotNullConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a non-null object to Visibility.Visible, null to Visibility.Collapsed.
/// </summary>
public class AIPanelNotNullToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelNotNullToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an integer greater than zero to true, otherwise false.
/// </summary>
public class AIPanelGreaterThanZeroConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelGreaterThanZeroConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
            return intValue > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a float value (0-1) to percentage display string.
/// </summary>
public class AIPanelPercentageConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelPercentageConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float floatValue)
            return $"{floatValue * 100:F0}%";
        if (value is double doubleValue)
            return $"{doubleValue * 100:F0}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue && stringValue.EndsWith("%"))
        {
            if (float.TryParse(stringValue.TrimEnd('%'), out var result))
                return result / 100f;
        }
        return 0f;
    }
}

/// <summary>
/// Converts a float value to decibel display string.
/// </summary>
public class AIPanelDecibelConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelDecibelConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float floatValue)
            return $"{floatValue:F1} dB";
        if (value is double doubleValue)
            return $"{doubleValue:F1} dB";
        return "0.0 dB";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string stringValue)
        {
            var numericPart = stringValue.Replace(" dB", "").Replace("dB", "");
            if (float.TryParse(numericPart, out var result))
                return result;
        }
        return 0f;
    }
}

/// <summary>
/// Multi-value converter for progress bar width based on percentage and container width.
/// </summary>
public class AIPanelProgressWidthConverter : IMultiValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelProgressWidthConverter Instance = new();

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is double progress && values[1] is double containerWidth)
        {
            return progress * containerWidth;
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts enum values to user-friendly display strings.
/// </summary>
public class AIPanelEnumDisplayConverter : IValueConverter
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static readonly AIPanelEnumDisplayConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null)
            return string.Empty;

        var enumValue = value.ToString();
        if (enumValue == null)
            return string.Empty;

        // Insert spaces before capital letters
        var result = System.Text.RegularExpressions.Regex.Replace(enumValue, "([a-z])([A-Z])", "$1 $2");
        return result;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
