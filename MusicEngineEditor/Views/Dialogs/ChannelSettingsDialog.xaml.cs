// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Represents an EQ band for the channel settings.
/// </summary>
public class EqBandSettings : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private double _frequency = 1000;
    private double _gain;
    private double _q = 1.0;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    public double Frequency
    {
        get => _frequency;
        set { _frequency = value; OnPropertyChanged(); OnPropertyChanged(nameof(FrequencyText)); }
    }

    public double Gain
    {
        get => _gain;
        set { _gain = value; OnPropertyChanged(); OnPropertyChanged(nameof(GainText)); }
    }

    public double Q
    {
        get => _q;
        set { _q = value; OnPropertyChanged(); }
    }

    public string FrequencyText => Frequency >= 1000 ? $"{Frequency / 1000:F1}k" : $"{Frequency:F0}";
    public string GainText => $"{Gain:+0.0;-0.0;0.0}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Represents a send configuration for the channel settings.
/// </summary>
public class SendSettings : INotifyPropertyChanged
{
    private string _targetId = string.Empty;
    private string _targetName = string.Empty;
    private float _level = 0.5f;

    public string TargetId
    {
        get => _targetId;
        set { _targetId = value; OnPropertyChanged(); }
    }

    public string TargetName
    {
        get => _targetName;
        set { _targetName = value; OnPropertyChanged(); }
    }

    public float Level
    {
        get => _level;
        set { _level = value; OnPropertyChanged(); OnPropertyChanged(nameof(LevelText)); }
    }

    public string LevelText => Level <= 0 ? "-inf" : $"{20 * Math.Log10(Level):F1}";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// A full channel strip popup dialog with input, EQ, dynamics, sends, and output sections.
/// </summary>
public partial class ChannelSettingsDialog : Window
{
    #region Private Fields

    private MixerChannel? _channel;
    private readonly ObservableCollection<EqBandSettings> _eqBands = new();
    private readonly ObservableCollection<SendSettings> _sends = new();
    private readonly ObservableCollection<string> _availableBuses = new();

    // Original values for reset/cancel
    private float _originalVolume;
    private float _originalPan;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private float _originalInputGain;
#pragma warning restore CS0414
    private bool _originalPhaseInvert;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _originalMono;
#pragma warning restore CS0414

    #endregion

    #region Events

    /// <summary>
    /// Event raised when settings are applied.
    /// </summary>
    public event EventHandler? SettingsApplied;

    #endregion

    #region Constructor

    public ChannelSettingsDialog()
    {
        InitializeComponent();

        EqBandsList.ItemsSource = _eqBands;
        SendsList.ItemsSource = _sends;

        InitializeDefaultEqBands();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the channel to edit.
    /// </summary>
    /// <param name="channel">The mixer channel to configure.</param>
    public void SetChannel(MixerChannel channel)
    {
        _channel = channel;

        // Store original values
        _originalVolume = channel.Volume;
        _originalPan = channel.Pan;
        _originalInputGain = 0; // Would need to be added to MixerChannel
        _originalPhaseInvert = channel.IsPhaseInverted;
        _originalMono = false; // Would need to be added

        // Update UI
        ChannelNameText.Text = channel.Name;
        ChannelColorIndicator.Fill = new SolidColorBrush(
            (Color)System.Windows.Media.ColorConverter.ConvertFromString(channel.Color));

        VolumeSlider.Value = channel.Volume;
        PanSlider.Value = channel.Pan;
        PhaseInvertButton.IsChecked = channel.IsPhaseInverted;

        // Load sends
        _sends.Clear();
        foreach (var send in channel.Sends)
        {
            _sends.Add(new SendSettings
            {
                TargetId = send.TargetBusId,
                TargetName = send.TargetBusName,
                Level = send.Level
            });
        }

        UpdateNoSendsVisibility();
        UpdateVolumeText();
        UpdatePanText();
        DrawEqCurve();
    }

    /// <summary>
    /// Sets the available output buses.
    /// </summary>
    /// <param name="buses">List of (id, name) tuples for available buses.</param>
    public void SetAvailableBuses(System.Collections.Generic.List<(string id, string name)> buses)
    {
        _availableBuses.Clear();
        OutputBusSelector.Items.Clear();

        OutputBusSelector.Items.Add(new ComboBoxItem { Content = "Master", Tag = "master" });

        foreach (var (id, name) in buses)
        {
            OutputBusSelector.Items.Add(new ComboBoxItem { Content = name, Tag = id });
        }

        // Select current output
        if (_channel != null)
        {
            for (int i = 0; i < OutputBusSelector.Items.Count; i++)
            {
                if (OutputBusSelector.Items[i] is ComboBoxItem item &&
                    item.Tag?.ToString() == _channel.OutputBusId)
                {
                    OutputBusSelector.SelectedIndex = i;
                    break;
                }
            }
        }

        if (OutputBusSelector.SelectedIndex < 0)
            OutputBusSelector.SelectedIndex = 0;
    }

    #endregion

    #region Event Handlers

    private void OnInputGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        InputGainText.Text = $"{e.NewValue:F1} dB";
    }

    private void OnPhaseInvertChanged(object sender, RoutedEventArgs e)
    {
        // Phase invert toggled
    }

    private void OnMonoChanged(object sender, RoutedEventArgs e)
    {
        // Mono toggled
    }

    private void OnEqBypassChanged(object sender, RoutedEventArgs e)
    {
        bool bypassed = EqBypassButton.IsChecked == true;
        EqBandsList.Opacity = bypassed ? 0.5 : 1.0;
        EqBandsList.IsEnabled = !bypassed;
    }

    private void OnDynamicsBypassChanged(object sender, RoutedEventArgs e)
    {
        bool bypassed = DynamicsBypassButton.IsChecked == true;
        ThresholdSlider.IsEnabled = !bypassed;
        RatioSlider.IsEnabled = !bypassed;
        AttackSlider.IsEnabled = !bypassed;
        ReleaseSlider.IsEnabled = !bypassed;
    }

    private void OnAddSendClick(object sender, RoutedEventArgs e)
    {
        // Show send selector or add default send
        _sends.Add(new SendSettings
        {
            TargetId = "fx1",
            TargetName = "FX 1",
            Level = 0.5f
        });
        UpdateNoSendsVisibility();
    }

    private void OnRemoveSendClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is SendSettings send)
        {
            _sends.Remove(send);
            UpdateNoSendsVisibility();
        }
    }

    private void OnOutputBusChanged(object sender, SelectionChangedEventArgs e)
    {
        // Output bus changed
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateVolumeText();
    }

    private void OnPanChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdatePanText();
    }

    private void OnResetClick(object sender, RoutedEventArgs e)
    {
        // Reset to defaults
        InputGainSlider.Value = 0;
        PhaseInvertButton.IsChecked = false;
        MonoButton.IsChecked = false;
        EqBypassButton.IsChecked = false;
        DynamicsBypassButton.IsChecked = false;

        VolumeSlider.Value = 0.8;
        PanSlider.Value = 0;

        ThresholdSlider.Value = -20;
        RatioSlider.Value = 4;
        AttackSlider.Value = 10;
        ReleaseSlider.Value = 100;

        foreach (var band in _eqBands)
        {
            band.Gain = 0;
        }

        DrawEqCurve();
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        ApplySettings();
        SettingsApplied?.Invoke(this, EventArgs.Empty);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Private Methods

    private void InitializeDefaultEqBands()
    {
        _eqBands.Add(new EqBandSettings { Name = "Low", Frequency = 80, Gain = 0 });
        _eqBands.Add(new EqBandSettings { Name = "Low Mid", Frequency = 400, Gain = 0 });
        _eqBands.Add(new EqBandSettings { Name = "High Mid", Frequency = 2500, Gain = 0 });
        _eqBands.Add(new EqBandSettings { Name = "High", Frequency = 8000, Gain = 0 });
    }

    private void ApplySettings()
    {
        if (_channel == null)
            return;

        _channel.Volume = (float)VolumeSlider.Value;
        _channel.Pan = (float)PanSlider.Value;
        _channel.IsPhaseInverted = PhaseInvertButton.IsChecked == true;

        // Update output bus
        if (OutputBusSelector.SelectedItem is ComboBoxItem item)
        {
            string? busId = item.Tag?.ToString();
            _channel.SetOutputBus(busId == "master" ? null : busId, item.Content?.ToString() ?? "Master");
        }

        // Update sends
        _channel.Sends.Clear();
        foreach (var send in _sends)
        {
            _channel.AddSend(send.TargetId, send.TargetName, send.Level);
        }
    }

    private void UpdateVolumeText()
    {
        double volume = VolumeSlider.Value;
        if (volume <= 0)
            VolumeText.Text = "-inf dB";
        else
            VolumeText.Text = $"{20 * Math.Log10(volume):F1} dB";
    }

    private void UpdatePanText()
    {
        double pan = PanSlider.Value;
        if (Math.Abs(pan) < 0.01)
            PanText.Text = "C";
        else if (pan < 0)
            PanText.Text = $"L{(int)(-pan * 100)}";
        else
            PanText.Text = $"R{(int)(pan * 100)}";
    }

    private void UpdateNoSendsVisibility()
    {
        NoSendsText.Visibility = _sends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DrawEqCurve()
    {
        EqCurveCanvas.Children.Clear();

        if (EqCurveCanvas.ActualWidth <= 0 || EqCurveCanvas.ActualHeight <= 0)
            return;

        double width = EqCurveCanvas.ActualWidth;
        double height = EqCurveCanvas.ActualHeight;
        double centerY = height / 2;

        // Draw center line
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = centerY,
            X2 = width,
            Y2 = centerY,
            Stroke = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 }
        };
        EqCurveCanvas.Children.Add(centerLine);

        // Draw EQ curve
        var points = new PointCollection();
        int numPoints = 100;

        for (int i = 0; i < numPoints; i++)
        {
            double x = i / (double)(numPoints - 1) * width;
            double freq = 20 * Math.Pow(1000, i / (double)(numPoints - 1)); // 20Hz to 20kHz

            // Calculate combined EQ response
            double gainDb = 0;
            foreach (var band in _eqBands)
            {
                double octaves = Math.Log(freq / band.Frequency) / Math.Log(2);
                double response = band.Gain * Math.Exp(-octaves * octaves / 2);
                gainDb += response;
            }

            // Convert to Y position (18dB = top, -18dB = bottom)
            double y = centerY - (gainDb / 18.0) * (height / 2 - 10);
            y = Math.Max(5, Math.Min(height - 5, y));

            points.Add(new Point(x, y));
        }

        var polyline = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x9E, 0xFF)),
            StrokeThickness = 2,
            Fill = null
        };
        EqCurveCanvas.Children.Add(polyline);
    }

    #endregion
}
