// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Panel that displays the undo/redo history with the ability to jump to any state.
/// </summary>
public partial class UndoHistoryPanel : UserControl, IDisposable
{
    private readonly UndoHistoryViewModel _viewModel;
    private bool _disposed;

    /// <summary>
    /// Event raised when the panel requests to be closed.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for future external consumers
    public event EventHandler? CloseRequested;
#pragma warning restore CS0067

    /// <summary>
    /// Creates a new UndoHistoryPanel.
    /// </summary>
    public UndoHistoryPanel()
    {
        InitializeComponent();

        _viewModel = new UndoHistoryViewModel();
        DataContext = _viewModel;
    }

    /// <summary>
    /// Gets the ViewModel for external access.
    /// </summary>
    public UndoHistoryViewModel ViewModel => _viewModel;

    /// <summary>
    /// Handles double-click on a history item to jump to that state.
    /// </summary>
    private void HistoryList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedItem != null)
        {
            _viewModel.JumpToSelectedState();
        }
    }

    /// <summary>
    /// Refreshes the history display.
    /// </summary>
    public void Refresh()
    {
        _viewModel.RefreshHistory();
    }

    /// <summary>
    /// Disposes the panel and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _viewModel.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Converter that converts a non-null/non-empty string to Visible.
/// </summary>
public class UndoHistoryStringToVisibilityConverter : IValueConverter
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
/// Converter that converts an int greater than 0 to true.
/// </summary>
public class IntToBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int intVal)
        {
            return intVal > 0;
        }
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter that converts a boolean to a color (for undo/redo item styling).
/// </summary>
public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isUndoItem)
        {
            // Undo items = green/success color, Redo items = gray
            return isUndoItem
                ? Color.FromRgb(0x49, 0x9C, 0x54) // Green
                : Color.FromRgb(0x6F, 0x73, 0x7A); // Gray
        }
        return Color.FromRgb(0x6F, 0x73, 0x7A);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
