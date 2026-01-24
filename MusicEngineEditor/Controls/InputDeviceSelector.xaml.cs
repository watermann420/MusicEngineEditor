// MusicEngineEditor - Input Device Selector Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A device picker control for selecting audio input devices.
/// Includes a dropdown with available devices, refresh button, and device info display.
/// </summary>
public partial class InputDeviceSelector : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Dependency property for available devices collection.
    /// </summary>
    public static readonly DependencyProperty AvailableDevicesProperty =
        DependencyProperty.Register(
            nameof(AvailableDevices),
            typeof(ObservableCollection<InputDeviceInfo>),
            typeof(InputDeviceSelector),
            new PropertyMetadata(null));

    /// <summary>
    /// Dependency property for the selected device.
    /// </summary>
    public static readonly DependencyProperty SelectedDeviceProperty =
        DependencyProperty.Register(
            nameof(SelectedDevice),
            typeof(InputDeviceInfo),
            typeof(InputDeviceSelector),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDeviceChanged));

    /// <summary>
    /// Dependency property for the refresh command.
    /// </summary>
    public static readonly DependencyProperty RefreshCommandProperty =
        DependencyProperty.Register(
            nameof(RefreshCommand),
            typeof(ICommand),
            typeof(InputDeviceSelector),
            new PropertyMetadata(null));

    /// <summary>
    /// Dependency property for sample rate.
    /// </summary>
    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(
            nameof(SampleRate),
            typeof(int),
            typeof(InputDeviceSelector),
            new PropertyMetadata(44100));

    /// <summary>
    /// Dependency property for channel count.
    /// </summary>
    public static readonly DependencyProperty ChannelCountProperty =
        DependencyProperty.Register(
            nameof(ChannelCount),
            typeof(int),
            typeof(InputDeviceSelector),
            new PropertyMetadata(2));

    /// <summary>
    /// Gets or sets the collection of available input devices.
    /// </summary>
    public ObservableCollection<InputDeviceInfo>? AvailableDevices
    {
        get => (ObservableCollection<InputDeviceInfo>?)GetValue(AvailableDevicesProperty);
        set => SetValue(AvailableDevicesProperty, value);
    }

    /// <summary>
    /// Gets or sets the currently selected device.
    /// </summary>
    public InputDeviceInfo? SelectedDevice
    {
        get => (InputDeviceInfo?)GetValue(SelectedDeviceProperty);
        set => SetValue(SelectedDeviceProperty, value);
    }

    /// <summary>
    /// Gets or sets the command to refresh the device list.
    /// </summary>
    public ICommand? RefreshCommand
    {
        get => (ICommand?)GetValue(RefreshCommandProperty);
        set => SetValue(RefreshCommandProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate of the selected device.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the channel count of the selected device.
    /// </summary>
    public int ChannelCount
    {
        get => (int)GetValue(ChannelCountProperty);
        set => SetValue(ChannelCountProperty, value);
    }

    private static void OnSelectedDeviceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is InputDeviceSelector selector && e.NewValue is InputDeviceInfo device)
        {
            selector.ChannelCount = device.ChannelCount;
            selector.DeviceSelected?.Invoke(selector, new DeviceSelectedEventArgs(device));
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when a device is selected.
    /// </summary>
    public event EventHandler<DeviceSelectedEventArgs>? DeviceSelected;

    /// <summary>
    /// Raised when the refresh button is clicked.
    /// </summary>
    public event EventHandler? RefreshClicked;

    #endregion

    /// <summary>
    /// Creates a new InputDeviceSelector.
    /// </summary>
    public InputDeviceSelector()
    {
        InitializeComponent();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshClicked?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Event arguments for device selection events.
/// </summary>
public class DeviceSelectedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the selected device info.
    /// </summary>
    public InputDeviceInfo Device { get; }

    /// <summary>
    /// Creates new DeviceSelectedEventArgs.
    /// </summary>
    public DeviceSelectedEventArgs(InputDeviceInfo device)
    {
        Device = device;
    }
}
