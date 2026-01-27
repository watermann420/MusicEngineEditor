// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Recording dialog for audio input recording with device selection,
/// monitoring, punch-in/out, and level metering.
/// </summary>
public partial class RecordingDialog : Window
{
    private readonly RecordingViewModel _viewModel;

    /// <summary>
    /// Gets the path to the recorded file after recording completes.
    /// </summary>
    public string? RecordedFilePath { get; private set; }

    /// <summary>
    /// Creates a new RecordingDialog.
    /// </summary>
    public RecordingDialog()
    {
        InitializeComponent();

        _viewModel = new RecordingViewModel();
        _viewModel.CloseRequested += OnCloseRequested;
        _viewModel.RecordingCompleted += OnRecordingCompleted;

        DataContext = _viewModel;
        Closed += OnWindowClosed;
    }

    /// <summary>
    /// Shows the recording dialog and returns the recorded file path.
    /// </summary>
    /// <param name="owner">Optional owner window.</param>
    /// <returns>The path to the recorded file, or null if cancelled.</returns>
    public static string? Show(Window? owner = null)
    {
        var dialog = new RecordingDialog
        {
            Owner = owner
        };

        dialog.ShowDialog();
        return dialog.RecordedFilePath;
    }

    /// <summary>
    /// Shows the recording dialog with custom output settings.
    /// </summary>
    /// <param name="outputPath">Default output directory.</param>
    /// <param name="fileName">Default filename.</param>
    /// <param name="owner">Optional owner window.</param>
    /// <returns>The path to the recorded file, or null if cancelled.</returns>
    public static string? Show(string outputPath, string fileName, Window? owner = null)
    {
        var dialog = new RecordingDialog
        {
            Owner = owner
        };

        if (dialog._viewModel != null)
        {
            dialog._viewModel.OutputPath = outputPath;
            dialog._viewModel.OutputFileName = fileName;
        }

        dialog.ShowDialog();
        return dialog.RecordedFilePath;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = RecordedFilePath != null;
        Close();
    }

    private void OnRecordingCompleted(object? sender, string filePath)
    {
        RecordedFilePath = filePath;
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _viewModel.CloseRequested -= OnCloseRequested;
        _viewModel.RecordingCompleted -= OnRecordingCompleted;
        _viewModel.Dispose();
    }
}

/// <summary>
/// Converts a boolean to a color based on the converter parameter.
/// Parameter format: "TrueColor|FalseColor" (e.g., "#F44336|#424242").
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string colorParam)
        {
            return Colors.Gray;
        }

        var colors = colorParam.Split('|');
        if (colors.Length != 2)
        {
            return Colors.Gray;
        }

        try
        {
            var trueColor = (Color)ColorConverter.ConvertFromString(colors[0]);
            var falseColor = (Color)ColorConverter.ConvertFromString(colors[1]);
            return boolValue ? trueColor : falseColor;
        }
        catch
        {
            return Colors.Gray;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class RecordingInverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return false;
    }
}
