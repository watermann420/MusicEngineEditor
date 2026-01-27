// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Converts string to Visibility.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MusicEngineEditor.Converters;

/// <summary>
/// Converts a boolean to Visibility.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public static BoolToVisibilityConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check for inverse parameter
            if (parameter is string param && param.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }

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
/// Converts a string to Visibility.
/// Returns Visible if the string is not null or empty, Collapsed otherwise.
/// If ConverterParameter is "inverse", the behavior is inverted.
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool hasValue = !string.IsNullOrEmpty(value as string);

        // Check for inverse parameter
        if (parameter is string param && param.Equals("inverse", StringComparison.OrdinalIgnoreCase))
        {
            hasValue = !hasValue;
        }

        return hasValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}

/// <summary>
/// Converts an integer to Visibility.
/// Returns Visible if the value is greater than 0, Collapsed otherwise.
/// </summary>
public class IntToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intValue)
        {
            return intValue > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts an enum value to boolean.
/// Returns true if the value equals the converter parameter.
/// Used for binding ToggleButton.IsChecked to enum properties.
/// </summary>
public class EnumBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return false;

        string paramString = parameter.ToString() ?? string.Empty;
        string valueString = value.ToString() ?? string.Empty;

        return valueString.Equals(paramString, StringComparison.OrdinalIgnoreCase);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && boolValue && parameter != null)
        {
            string paramString = parameter.ToString() ?? string.Empty;
            if (targetType.IsEnum)
            {
                return Enum.Parse(targetType, paramString);
            }
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}

/// <summary>
/// Converts an enum value to Visibility.
/// Returns Visible if the value equals the converter parameter, Collapsed otherwise.
/// Used for showing/hiding UI elements based on enum selection.
/// </summary>
public class EnumVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        string paramString = parameter.ToString() ?? string.Empty;
        string valueString = value.ToString() ?? string.Empty;

        return valueString.Equals(paramString, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
