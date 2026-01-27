// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A control for displaying and editing track group/folder headers.
/// Features expand/collapse arrow, editable group name, color indicator,
/// solo/mute buttons, volume slider, and track count badge.
/// </summary>
public partial class TrackGroupHeader : UserControl
{
    #region Constants

    private const float UnityGainVolume = 0.8f;
    private const float MinVolume = 0f;
    private const float MaxVolume = 1.25f;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty GroupNameProperty =
        DependencyProperty.Register(nameof(GroupName), typeof(string), typeof(TrackGroupHeader),
            new FrameworkPropertyMetadata("Track Group",
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty GroupColorProperty =
        DependencyProperty.Register(nameof(GroupColor), typeof(Brush), typeof(TrackGroupHeader),
            new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(TrackGroupHeader),
            new FrameworkPropertyMetadata(true,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsExpandedChanged));

    public static readonly DependencyProperty IsMutedProperty =
        DependencyProperty.Register(nameof(IsMuted), typeof(bool), typeof(TrackGroupHeader),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsMutedChanged));

    public static readonly DependencyProperty IsSoloedProperty =
        DependencyProperty.Register(nameof(IsSoloed), typeof(bool), typeof(TrackGroupHeader),
            new FrameworkPropertyMetadata(false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsSoloedChanged));

    public static readonly DependencyProperty VolumeProperty =
        DependencyProperty.Register(nameof(Volume), typeof(float), typeof(TrackGroupHeader),
            new FrameworkPropertyMetadata(UnityGainVolume,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                null,
                CoerceVolume));

    public static readonly DependencyProperty TrackCountProperty =
        DependencyProperty.Register(nameof(TrackCount), typeof(int), typeof(TrackGroupHeader),
            new PropertyMetadata(0));

    public static readonly DependencyProperty GroupIdProperty =
        DependencyProperty.Register(nameof(GroupId), typeof(string), typeof(TrackGroupHeader),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty NestingLevelProperty =
        DependencyProperty.Register(nameof(NestingLevel), typeof(int), typeof(TrackGroupHeader),
            new PropertyMetadata(0, OnNestingLevelChanged));

    /// <summary>
    /// Gets or sets the group name.
    /// </summary>
    public string GroupName
    {
        get => (string)GetValue(GroupNameProperty);
        set => SetValue(GroupNameProperty, value);
    }

    /// <summary>
    /// Gets or sets the group color brush.
    /// </summary>
    public Brush GroupColor
    {
        get => (Brush)GetValue(GroupColorProperty);
        set => SetValue(GroupColorProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the group is expanded (showing child tracks).
    /// </summary>
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the group is muted.
    /// </summary>
    public bool IsMuted
    {
        get => (bool)GetValue(IsMutedProperty);
        set => SetValue(IsMutedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the group is soloed.
    /// </summary>
    public bool IsSoloed
    {
        get => (bool)GetValue(IsSoloedProperty);
        set => SetValue(IsSoloedProperty, value);
    }

    /// <summary>
    /// Gets or sets the volume level (0.0 to 1.25, where 0.8 is unity gain).
    /// </summary>
    public float Volume
    {
        get => (float)GetValue(VolumeProperty);
        set => SetValue(VolumeProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of tracks in this group.
    /// </summary>
    public int TrackCount
    {
        get => (int)GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

    /// <summary>
    /// Gets or sets the unique identifier for this group.
    /// </summary>
    public string GroupId
    {
        get => (string)GetValue(GroupIdProperty);
        set => SetValue(GroupIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the nesting level (indentation depth) for nested groups.
    /// </summary>
    public int NestingLevel
    {
        get => (int)GetValue(NestingLevelProperty);
        set => SetValue(NestingLevelProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the group name is changed.
    /// </summary>
    public event EventHandler<string>? GroupNameChanged;

    /// <summary>
    /// Event raised when the expand/collapse state changes.
    /// </summary>
    public event EventHandler<bool>? ExpandedChanged;

    /// <summary>
    /// Event raised when the mute state changes.
    /// </summary>
    public event EventHandler<bool>? MutedChanged;

    /// <summary>
    /// Event raised when the solo state changes.
    /// </summary>
    public event EventHandler<bool>? SoloedChanged;

    /// <summary>
    /// Event raised when the volume changes.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for binding
    public event EventHandler<float>? VolumeChanged;
#pragma warning restore CS0067

    #endregion

    #region Constructor

    public TrackGroupHeader()
    {
        InitializeComponent();
    }

    #endregion

    #region Property Changed Callbacks

    private static void OnIsExpandedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackGroupHeader header)
        {
            header.ExpandedChanged?.Invoke(header, (bool)e.NewValue);
        }
    }

    private static void OnIsMutedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackGroupHeader header)
        {
            header.MutedChanged?.Invoke(header, (bool)e.NewValue);
        }
    }

    private static void OnIsSoloedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackGroupHeader header)
        {
            header.SoloedChanged?.Invoke(header, (bool)e.NewValue);
        }
    }

    private static object CoerceVolume(DependencyObject d, object baseValue)
    {
        float value = (float)baseValue;
        return Math.Clamp(value, MinVolume, MaxVolume);
    }

    private static void OnNestingLevelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TrackGroupHeader header)
        {
            header.UpdateIndentation((int)e.NewValue);
        }
    }

    #endregion

    #region Event Handlers

    /// <summary>
    /// Handles clicking on the group name to enter edit mode.
    /// </summary>
    private void GroupNameDisplay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            EnterEditMode();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Handles leaving the name editor to exit edit mode.
    /// </summary>
    private void GroupNameEditor_LostFocus(object sender, RoutedEventArgs e)
    {
        ExitEditMode();
    }

    /// <summary>
    /// Handles key presses in the name editor.
    /// </summary>
    private void GroupNameEditor_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExitEditMode();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            // Cancel edit and restore original name
            GroupNameEditor.Text = GroupName;
            ExitEditMode();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Resets volume to unity gain on double-click.
    /// </summary>
    private void VolumeSlider_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Volume = UnityGainVolume;
        e.Handled = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Toggles the expanded state of the group.
    /// </summary>
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    /// <summary>
    /// Toggles the mute state of the group.
    /// </summary>
    public void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    /// <summary>
    /// Toggles the solo state of the group.
    /// </summary>
    public void ToggleSolo()
    {
        IsSoloed = !IsSoloed;
    }

    /// <summary>
    /// Resets the group header to default values.
    /// </summary>
    public void Reset()
    {
        Volume = UnityGainVolume;
        IsMuted = false;
        IsSoloed = false;
        IsExpanded = true;
    }

    /// <summary>
    /// Sets the group color from RGB values.
    /// </summary>
    /// <param name="r">Red component (0-255)</param>
    /// <param name="g">Green component (0-255)</param>
    /// <param name="b">Blue component (0-255)</param>
    public void SetColor(byte r, byte g, byte b)
    {
        GroupColor = new SolidColorBrush(Color.FromRgb(r, g, b));
    }

    /// <summary>
    /// Sets the group color from a System.Drawing.Color.
    /// </summary>
    /// <param name="color">The color to set.</param>
    public void SetColor(System.Drawing.Color color)
    {
        GroupColor = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Enters edit mode for the group name.
    /// </summary>
    private void EnterEditMode()
    {
        GroupNameDisplay.Visibility = Visibility.Collapsed;
        GroupNameEditor.Visibility = Visibility.Visible;
        GroupNameEditor.SelectAll();
        GroupNameEditor.Focus();
    }

    /// <summary>
    /// Exits edit mode for the group name.
    /// </summary>
    private void ExitEditMode()
    {
        string newName = GroupNameEditor.Text.Trim();

        if (!string.IsNullOrEmpty(newName) && newName != GroupName)
        {
            GroupName = newName;
            GroupNameChanged?.Invoke(this, newName);
        }

        GroupNameDisplay.Visibility = Visibility.Visible;
        GroupNameEditor.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Updates the indentation based on nesting level.
    /// </summary>
    private void UpdateIndentation(int level)
    {
        // Add left margin for nested groups (16 pixels per level)
        Margin = new Thickness(level * 16, Margin.Top, Margin.Right, Margin.Bottom);
    }

    #endregion
}
