// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

// Event is declared for future use / public API
#pragma warning disable CS0067

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and managing an effect chain with multiple effect slots.
/// Supports drag-and-drop reordering, bypass, and effect selection.
/// </summary>
public partial class EffectChainControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty EffectSlotsProperty =
        DependencyProperty.Register(nameof(EffectSlots), typeof(ObservableCollection<EffectSlot>),
            typeof(EffectChainControl), new PropertyMetadata(null));

    public static readonly DependencyProperty HasEffectsProperty =
        DependencyProperty.Register(nameof(HasEffects), typeof(bool),
            typeof(EffectChainControl), new PropertyMetadata(false));

    public static readonly DependencyProperty EffectCountProperty =
        DependencyProperty.Register(nameof(EffectCount), typeof(int),
            typeof(EffectChainControl), new PropertyMetadata(0));

    public static readonly DependencyProperty IsChainBypassedProperty =
        DependencyProperty.Register(nameof(IsChainBypassed), typeof(bool),
            typeof(EffectChainControl), new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    /// <summary>
    /// Gets or sets the collection of effect slots.
    /// </summary>
    public ObservableCollection<EffectSlot> EffectSlots
    {
        get => (ObservableCollection<EffectSlot>)GetValue(EffectSlotsProperty);
        set => SetValue(EffectSlotsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the chain has any effects.
    /// </summary>
    public bool HasEffects
    {
        get => (bool)GetValue(HasEffectsProperty);
        set => SetValue(HasEffectsProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of effects in the chain.
    /// </summary>
    public int EffectCount
    {
        get => (int)GetValue(EffectCountProperty);
        set => SetValue(EffectCountProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the entire chain is bypassed.
    /// </summary>
    public bool IsChainBypassed
    {
        get => (bool)GetValue(IsChainBypassedProperty);
        set => SetValue(IsChainBypassedProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when an effect slot is selected.
    /// </summary>
    public event EventHandler<EffectSlotEventArgs>? EffectSlotSelected;

    /// <summary>
    /// Raised when the user requests to add an effect.
    /// </summary>
    public event EventHandler<EffectSlotEventArgs>? AddEffectRequested;

    /// <summary>
    /// Raised when an effect is removed.
    /// </summary>
    public event EventHandler<EffectSlotEventArgs>? EffectRemoved;

    /// <summary>
    /// Raised when an effect's bypass state changes.
    /// </summary>
    public event EventHandler<EffectBypassChangedEventArgs>? EffectBypassChanged;

    #endregion

    #region Constructor

    public EffectChainControl()
    {
        InitializeComponent();

        // Initialize with empty collection if not bound
        if (EffectSlots == null)
        {
            EffectSlots = [];
            InitializeDefaultSlots();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds an effect to the first available slot.
    /// </summary>
    /// <param name="effectType">The effect type identifier.</param>
    /// <param name="displayName">The display name.</param>
    /// <returns>True if the effect was added.</returns>
    public bool AddEffect(string effectType, string displayName)
    {
        foreach (var slot in EffectSlots)
        {
            if (slot.IsEmpty)
            {
                slot.LoadEffect(effectType, displayName);
                UpdateEffectCount();
                return true;
            }
        }

        // Add a new slot if all are full
        var newSlot = new EffectSlot(EffectSlots.Count, effectType, displayName);
        EffectSlots.Add(newSlot);
        UpdateEffectCount();
        return true;
    }

    /// <summary>
    /// Removes an effect at the specified index.
    /// </summary>
    /// <param name="index">The slot index.</param>
    public void RemoveEffect(int index)
    {
        if (index >= 0 && index < EffectSlots.Count)
        {
            EffectSlots[index].ClearEffect();
            UpdateEffectCount();
        }
    }

    /// <summary>
    /// Clears all effects from the chain.
    /// </summary>
    public void ClearAllEffects()
    {
        foreach (var slot in EffectSlots)
        {
            slot.ClearEffect();
        }
        UpdateEffectCount();
    }

    #endregion

    #region Event Handlers

    private void EffectSlot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is EffectSlot slot)
        {
            // Deselect all other slots
            foreach (var s in EffectSlots)
            {
                s.IsSelected = false;
            }

            slot.IsSelected = true;
            EffectSlotSelected?.Invoke(this, new EffectSlotEventArgs(slot));

            // If empty slot, trigger add effect
            if (slot.IsEmpty)
            {
                AddEffectRequested?.Invoke(this, new EffectSlotEventArgs(slot));
            }
        }
    }

    private void AddEffect_Click(object sender, RoutedEventArgs e)
    {
        // Find first empty slot or create new one
        EffectSlot? targetSlot = null;
        foreach (var slot in EffectSlots)
        {
            if (slot.IsEmpty)
            {
                targetSlot = slot;
                break;
            }
        }

        if (targetSlot == null)
        {
            targetSlot = new EffectSlot(EffectSlots.Count);
            EffectSlots.Add(targetSlot);
        }

        AddEffectRequested?.Invoke(this, new EffectSlotEventArgs(targetSlot));
    }

    private void RemoveEffect_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is EffectSlot slot)
        {
            slot.ClearEffect();
            UpdateEffectCount();
            EffectRemoved?.Invoke(this, new EffectSlotEventArgs(slot));
        }
    }

    private void EffectSlot_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(EffectSlot)))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void EffectSlot_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (sender is FrameworkElement element &&
            element.DataContext is EffectSlot targetSlot &&
            e.Data.GetData(typeof(EffectSlot)) is EffectSlot sourceSlot)
        {
            // Swap effects between slots
            if (sourceSlot.Index != targetSlot.Index)
            {
                var tempType = targetSlot.EffectType;
                var tempName = targetSlot.DisplayName;
                var tempEmpty = targetSlot.IsEmpty;
                var tempBypassed = targetSlot.IsBypassed;
                var tempMix = targetSlot.Mix;

                if (sourceSlot.IsEmpty)
                {
                    targetSlot.ClearEffect();
                }
                else
                {
                    targetSlot.LoadEffect(sourceSlot.EffectType, sourceSlot.DisplayName);
                    targetSlot.IsBypassed = sourceSlot.IsBypassed;
                    targetSlot.Mix = sourceSlot.Mix;
                }

                if (tempEmpty)
                {
                    sourceSlot.ClearEffect();
                }
                else
                {
                    sourceSlot.LoadEffect(tempType, tempName);
                    sourceSlot.IsBypassed = tempBypassed;
                    sourceSlot.Mix = tempMix;
                }
            }
        }
    }

    #endregion

    #region Private Methods

    private void InitializeDefaultSlots()
    {
        // Create 4 empty slots by default
        for (int i = 0; i < 4; i++)
        {
            EffectSlots.Add(new EffectSlot(i));
        }
    }

    private void UpdateEffectCount()
    {
        int count = 0;
        foreach (var slot in EffectSlots)
        {
            if (!slot.IsEmpty)
                count++;
        }

        EffectCount = count;
        HasEffects = count > 0;
    }

    #endregion
}

#region Event Args

/// <summary>
/// Event arguments for effect slot events.
/// </summary>
public class EffectSlotEventArgs : EventArgs
{
    /// <summary>
    /// Gets the effect slot.
    /// </summary>
    public EffectSlot Slot { get; }

    /// <summary>
    /// Creates new effect slot event arguments.
    /// </summary>
    /// <param name="slot">The effect slot.</param>
    public EffectSlotEventArgs(EffectSlot slot)
    {
        Slot = slot;
    }
}

/// <summary>
/// Event arguments for effect bypass changes.
/// </summary>
public class EffectBypassChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the effect slot.
    /// </summary>
    public EffectSlot Slot { get; }

    /// <summary>
    /// Gets the new bypass state.
    /// </summary>
    public bool IsBypassed { get; }

    /// <summary>
    /// Creates new bypass changed event arguments.
    /// </summary>
    /// <param name="slot">The effect slot.</param>
    /// <param name="isBypassed">The bypass state.</param>
    public EffectBypassChangedEventArgs(EffectSlot slot, bool isBypassed)
    {
        Slot = slot;
        IsBypassed = isBypassed;
    }
}

#endregion

#region Converters

/// <summary>
/// Converts a boolean to inverted Visibility.
/// </summary>
public class InverseBooleanToVisibilityConverter : IValueConverter
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
/// Converts a percentage value to a width.
/// </summary>
public class EffectChainPercentageConverter : IValueConverter
{
    public static EffectChainPercentageConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double doubleValue)
        {
            return doubleValue * 100; // Simplified - actual implementation would need parent width
        }
        return 0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
