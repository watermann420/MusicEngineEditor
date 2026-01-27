// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Clip Launcher Grid control.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MusicEngineEditor.ViewModels;

using ContextMenu = System.Windows.Controls.ContextMenu;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Interaction logic for ClipLauncherGrid.xaml.
/// Provides an Ableton-style grid of clip slots organized by tracks and scenes.
/// </summary>
public partial class ClipLauncherGrid : UserControl
{
    /// <summary>
    /// Dependency property for the SessionViewModel.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SessionViewModel),
            typeof(ClipLauncherGrid),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>
    /// Gets or sets the SessionViewModel.
    /// </summary>
    public SessionViewModel? ViewModel
    {
        get => (SessionViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Creates a new ClipLauncherGrid.
    /// </summary>
    public ClipLauncherGrid()
    {
        // Add value converters to resources before InitializeComponent
        Resources.Add("BoolToVisConverter", new BooleanToVisibilityConverter());
        Resources.Add("InverseBoolToVisConverter", new SessionInverseBoolToVisConverter());
        Resources.Add("InverseBoolConverter", new SessionInverseBoolConverter());
        Resources.Add("SessionNullToCollapsedConverter", new SessionNullToCollapsedConverter());

        InitializeComponent();
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipLauncherGrid grid && e.NewValue is SessionViewModel vm)
        {
            grid.DataContext = vm;
        }
    }

    /// <summary>
    /// Handles click on a clip slot.
    /// </summary>
    private void SlotButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is ClipSlotViewModel slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.SelectSlotCommand.Execute(slot);

            if (slot.HasClip)
            {
                vm?.LaunchClipCommand.Execute(slot);
            }
        }
    }

    /// <summary>
    /// Handles double-click on a clip slot for editing.
    /// </summary>
    private void SlotButton_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is ClipSlotViewModel slot)
        {
            var vm = DataContext as SessionViewModel;

            if (slot.HasClip)
            {
                vm?.EditClipCommand.Execute(slot);
            }
            else
            {
                vm?.CreateClipCommand.Execute(slot);
            }

            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles click on a scene launch button.
    /// </summary>
    private void SceneLaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SceneViewModel scene)
        {
            var vm = DataContext as SessionViewModel;
            vm?.LaunchSceneCommand.Execute(scene);
        }
    }

    /// <summary>
    /// Handles click on a track stop button.
    /// </summary>
    private void TrackStopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SessionTrackViewModel track)
        {
            var vm = DataContext as SessionViewModel;
            vm?.StopTrackCommand.Execute(track);
        }
    }

    /// <summary>
    /// Synchronizes scroll positions between the clip grid and headers.
    /// </summary>
    private void ClipGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync horizontal scroll with track headers
        TrackHeaderScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        TrackStopScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);

        // Sync vertical scroll with scene buttons
        SceneLaunchScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
    }

    #region Context Menu Handlers

    private void LaunchMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSlotFromMenuItem(sender) is { } slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.LaunchClipCommand.Execute(slot);
        }
    }

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSlotFromMenuItem(sender) is { } slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.EditClipCommand.Execute(slot);
        }
    }

    private void CreateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSlotFromMenuItem(sender) is { } slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.CreateClipCommand.Execute(slot);
        }
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSlotFromMenuItem(sender) is { } slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.DuplicateClipCommand.Execute(slot);
        }
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (GetSlotFromMenuItem(sender) is { } slot)
        {
            var vm = DataContext as SessionViewModel;
            vm?.DeleteClipCommand.Execute(slot);
        }
    }

    private static ClipSlotViewModel? GetSlotFromMenuItem(object sender)
    {
        if (sender is MenuItem menuItem &&
            menuItem.Parent is ContextMenu contextMenu &&
            contextMenu.PlacementTarget is Button button &&
            button.Tag is ClipSlotViewModel slot)
        {
            return slot;
        }
        return null;
    }

    #endregion
}

/// <summary>
/// Converts a boolean to the inverse boolean.
/// </summary>
public class SessionInverseBoolConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return value;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return !b;
        }
        return value;
    }
}

/// <summary>
/// Converts a boolean to Visibility (inverse - true = Collapsed, false = Visible).
/// </summary>
public class SessionInverseBoolToVisConverter : IValueConverter
{
    public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool b)
        {
            return b ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Visibility v)
        {
            return v != Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Converts null to Collapsed, non-null to Visible.
/// </summary>
public class SessionNullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value == null ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new System.NotImplementedException();
    }
}
