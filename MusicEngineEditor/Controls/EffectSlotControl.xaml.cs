using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A compact control for displaying and interacting with an effect slot in a mixer channel.
/// </summary>
public partial class EffectSlotControl : UserControl
{
    #region Events

    /// <summary>
    /// Raised when the user requests to add an effect to this slot.
    /// </summary>
    public event EventHandler? AddEffectRequested;

    /// <summary>
    /// Raised when the user requests to edit the effect in this slot.
    /// </summary>
    public event EventHandler? EditEffectRequested;

    /// <summary>
    /// Raised when the user requests to remove the effect from this slot.
    /// </summary>
    public event EventHandler? RemoveEffectRequested;

    /// <summary>
    /// Raised when the user requests to move the effect up in the chain.
    /// </summary>
    public event EventHandler? MoveUpRequested;

    /// <summary>
    /// Raised when the user requests to move the effect down in the chain.
    /// </summary>
    public event EventHandler? MoveDownRequested;

    /// <summary>
    /// Raised when the slot is double-clicked.
    /// </summary>
    public event EventHandler? SlotDoubleClicked;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty EffectSlotProperty =
        DependencyProperty.Register(nameof(EffectSlot), typeof(EffectSlot), typeof(EffectSlotControl),
            new PropertyMetadata(null, OnEffectSlotChanged));

    /// <summary>
    /// Gets or sets the effect slot data.
    /// </summary>
    public EffectSlot? EffectSlot
    {
        get => (EffectSlot?)GetValue(EffectSlotProperty);
        set => SetValue(EffectSlotProperty, value);
    }

    private static void OnEffectSlotChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EffectSlotControl control)
        {
            control.DataContext = e.NewValue;
        }
    }

    #endregion

    #region Constructor

    public EffectSlotControl()
    {
        InitializeComponent();

        // Register converters in resources
        Resources["InverseBoolConverter"] = new EffectSlotInverseBoolConverter();
        Resources["InverseBoolToVisConverter"] = new EffectSlotInverseBoolToVisConverter();
        Resources["EmptyToAddTooltipConverter"] = new EffectSlotEmptyToAddTooltipConverter();
    }

    #endregion

    #region Event Handlers

    private void SlotBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            e.Handled = true;
            OnSlotDoubleClicked();
        }
    }

    private void SlotBorder_MouseEnter(object sender, MouseEventArgs e)
    {
        SlotBorder.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x3B, 0x40));
    }

    private void SlotBorder_MouseLeave(object sender, MouseEventArgs e)
    {
        SlotBorder.Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2E, 0x32));
    }

    private void EditOrAdd_Click(object sender, RoutedEventArgs e)
    {
        var slot = DataContext as EffectSlot;
        if (slot?.IsEmpty == true)
        {
            AddEffectRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            EditEffectRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RemoveEffect_Click(object sender, RoutedEventArgs e)
    {
        RemoveEffectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void Bypass_Click(object sender, RoutedEventArgs e)
    {
        // Handled via binding
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        MoveUpRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        MoveDownRequested?.Invoke(this, EventArgs.Empty);
    }

    private void EditEffect_Click(object sender, RoutedEventArgs e)
    {
        EditEffectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSlotDoubleClicked()
    {
        var slot = DataContext as EffectSlot;
        if (slot?.IsEmpty == true)
        {
            AddEffectRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            SlotDoubleClicked?.Invoke(this, EventArgs.Empty);
            EditEffectRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    #endregion
}

#region Converters

/// <summary>
/// Converts a boolean value to its inverse.
/// </summary>
internal class EffectSlotInverseBoolConverter : IValueConverter
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
/// Converts a boolean value to its inverse visibility (true = Collapsed, false = Visible).
/// </summary>
internal class EffectSlotInverseBoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts empty state to appropriate tooltip text.
/// </summary>
internal class EffectSlotEmptyToAddTooltipConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEmpty && isEmpty)
        {
            return "Add Effect";
        }
        return "Edit Effect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
