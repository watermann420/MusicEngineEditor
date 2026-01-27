// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Live Looper Control for real-time loop recording and playback.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngine.Core.Performance;

namespace MusicEngineEditor.Controls.Performance;

#region Converters

/// <summary>
/// Converts boolean to Visibility.
/// </summary>
public class LiveLooperBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

/// <summary>
/// Converts boolean to inverse Visibility.
/// </summary>
public class LiveLooperInverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}

/// <summary>
/// Converts LoopLayerState to a color brush.
/// </summary>
public class LiveLooperLayerStateToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush EmptyBrush = new(Color.FromRgb(0x50, 0x50, 0x50));
    private static readonly SolidColorBrush RecordingBrush = new(Color.FromRgb(0xFF, 0x47, 0x57));
    private static readonly SolidColorBrush PlayingBrush = new(Color.FromRgb(0x00, 0xFF, 0x88));
    private static readonly SolidColorBrush StoppedBrush = new(Color.FromRgb(0x80, 0x80, 0x80));
    private static readonly SolidColorBrush OverdubbingBrush = new(Color.FromRgb(0xFF, 0x95, 0x00));

    static LiveLooperLayerStateToColorConverter()
    {
        EmptyBrush.Freeze();
        RecordingBrush.Freeze();
        PlayingBrush.Freeze();
        StoppedBrush.Freeze();
        OverdubbingBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LoopLayerState state)
        {
            return state switch
            {
                LoopLayerState.Empty => EmptyBrush,
                LoopLayerState.Recording => RecordingBrush,
                LoopLayerState.Playing => PlayingBrush,
                LoopLayerState.Stopped => StoppedBrush,
                LoopLayerState.Overdubbing => OverdubbingBrush,
                _ => EmptyBrush
            };
        }
        return EmptyBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts volume (0-1) to pixel height for visualization.
/// </summary>
public class LiveLooperVolumeToHeightConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float volume)
        {
            double maxHeight = parameter is double max ? max : 40.0;
            return volume * maxHeight;
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion

#region ViewModels

/// <summary>
/// ViewModel for a single loop layer.
/// </summary>
public class LoopLayerViewModel : INotifyPropertyChanged
{
    private readonly LoopLayer _layer;

    public int Index => _layer.Index;
    public string DisplayIndex => $"{_layer.Index + 1}";
    public string Name => _layer.Name;

    public LoopLayerState State
    {
        get => _layer.State;
    }

    public float Volume
    {
        get => _layer.Volume;
        set
        {
            if (Math.Abs(_layer.Volume - value) > 0.001f)
            {
                _layer.Volume = value;
                OnPropertyChanged();
            }
        }
    }

    public float Pan
    {
        get => _layer.Pan;
        set
        {
            if (Math.Abs(_layer.Pan - value) > 0.001f)
            {
                _layer.Pan = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsMuted
    {
        get => _layer.IsMuted;
        set
        {
            if (_layer.IsMuted != value)
            {
                _layer.IsMuted = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsSolo
    {
        get => _layer.IsSolo;
        set
        {
            if (_layer.IsSolo != value)
            {
                _layer.IsSolo = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasContent => _layer.HasContent;
    public bool IsEmpty => !_layer.HasContent;
    public bool IsRecording => _layer.State == LoopLayerState.Recording;
    public bool IsPlaying => _layer.State == LoopLayerState.Playing;
    public bool IsOverdubbing => _layer.State == LoopLayerState.Overdubbing;
    public int RecordedLength => _layer.RecordedLength;

    public LoopLayer Layer => _layer;

    public LoopLayerViewModel(LoopLayer layer)
    {
        _layer = layer;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(HasContent));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsRecording));
        OnPropertyChanged(nameof(IsPlaying));
        OnPropertyChanged(nameof(IsOverdubbing));
        OnPropertyChanged(nameof(RecordedLength));
        OnPropertyChanged(nameof(Volume));
        OnPropertyChanged(nameof(Pan));
        OnPropertyChanged(nameof(IsMuted));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion

/// <summary>
/// Live Looper Control for real-time loop recording with multiple layers.
/// Provides layer stack, transport controls, overdub mode, and undo functionality.
/// </summary>
public partial class LiveLooperControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty LiveLooperProperty =
        DependencyProperty.Register(nameof(LiveLooper), typeof(LiveLooper), typeof(LiveLooperControl),
            new PropertyMetadata(null, OnLiveLooperChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(LiveLooperControl),
            new PropertyMetadata(120.0, OnBpmChanged));

    public static readonly DependencyProperty LayerCountProperty =
        DependencyProperty.Register(nameof(LayerCount), typeof(int), typeof(LiveLooperControl),
            new PropertyMetadata(8));

    /// <summary>
    /// Gets or sets the LiveLooper instance.
    /// </summary>
    public LiveLooper? LiveLooper
    {
        get => (LiveLooper?)GetValue(LiveLooperProperty);
        set => SetValue(LiveLooperProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of layers (4-8).
    /// </summary>
    public int LayerCount
    {
        get => (int)GetValue(LayerCountProperty);
        set => SetValue(LayerCountProperty, Math.Clamp(value, 4, 8));
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when the looper state changes.
    /// </summary>
    public event EventHandler<LooperStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Fired when a loop cycle completes.
    /// </summary>
    public event EventHandler<LoopCycleEventArgs>? LoopCycleCompleted;

    /// <summary>
    /// Fired when the looper is cleared.
    /// </summary>
    public event EventHandler? Cleared;

    /// <summary>
    /// Fired when undo is performed.
    /// </summary>
    public event EventHandler? UndoPerformed;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<LoopLayerViewModel> _layerViewModels = new();
    private readonly DispatcherTimer _updateTimer;
    private bool _isInitialized;
    private float _inputLevel;
    private float _inputPeak;
    private int _peakHoldCounter;

    #endregion

    #region Constructor

    public LiveLooperControl()
    {
        InitializeComponent();

        LayerStack.ItemsSource = _layerViewModels;

        // Update timer for UI refresh
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30fps
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        KeyDown += OnKeyDown;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Create default looper if none is set
        if (LiveLooper == null)
        {
            LiveLooper = new LiveLooper(maxLayers: LayerCount);
        }

        BuildLayerViewModels();
        _updateTimer.Start();

        // Focus for keyboard input
        Focusable = true;
        Focus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _updateTimer.Stop();
        _isInitialized = false;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnLiveLooperChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveLooperControl control)
        {
            if (e.OldValue is LiveLooper oldLooper)
            {
                oldLooper.StateChanged -= control.OnLooperStateChanged;
                oldLooper.LayerStateChanged -= control.OnLayerStateChanged;
                oldLooper.LoopCycleCompleted -= control.OnLoopCycleCompleted;
                oldLooper.UndoPerformed -= control.OnUndoPerformed;
                oldLooper.Cleared -= control.OnCleared;
            }

            if (e.NewValue is LiveLooper newLooper)
            {
                newLooper.StateChanged += control.OnLooperStateChanged;
                newLooper.LayerStateChanged += control.OnLayerStateChanged;
                newLooper.LoopCycleCompleted += control.OnLoopCycleCompleted;
                newLooper.UndoPerformed += control.OnUndoPerformed;
                newLooper.Cleared += control.OnCleared;

                if (control._isInitialized)
                {
                    control.BuildLayerViewModels();
                    control.UpdateDisplay();
                }
            }
        }
    }

    private static void OnBpmChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LiveLooperControl control && control.LiveLooper != null)
        {
            control.LiveLooper.Bpm = (double)e.NewValue;
        }
    }

    #endregion

    #region Event Handlers

    private void OnLooperStateChanged(object? sender, LooperStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateStateDisplay();
            StateChanged?.Invoke(this, e);
        });
    }

    private void OnLayerStateChanged(object? sender, LayerStateChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (e.LayerIndex >= 0 && e.LayerIndex < _layerViewModels.Count)
            {
                _layerViewModels[e.LayerIndex].Refresh();
            }
        });
    }

    private void OnLoopCycleCompleted(object? sender, LoopCycleEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            CycleCountText.Text = e.CycleCount.ToString();
            LoopCycleCompleted?.Invoke(this, e);
        });
    }

    private void OnUndoPerformed(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshAllLayers();
            UndoPerformed?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnCleared(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RefreshAllLayers();
            UpdateDisplay();
            Cleared?.Invoke(this, EventArgs.Empty);
        });
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (LiveLooper == null) return;

        UpdateLoopProgress();
        UpdateInputLevel();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.R:
                Record_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Space:
                Play_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.S:
                Stop_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.T:
                Toggle_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
            case Key.Z when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                Undo_Click(this, new RoutedEventArgs());
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Button Click Handlers

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        LiveLooper?.Record();
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        LiveLooper?.Play();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        LiveLooper?.Stop();
    }

    private void Toggle_Click(object sender, RoutedEventArgs e)
    {
        LiveLooper?.Toggle();
    }

    private void Undo_Click(object sender, RoutedEventArgs e)
    {
        LiveLooper?.Undo();
    }

    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all layers?",
            "Clear All",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            LiveLooper?.Clear();
        }
    }

    private void LayerRecord_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && LiveLooper != null)
        {
            LiveLooper.ActiveLayerIndex = index;
            LiveLooper.Record();
        }
    }

    private void LayerPlay_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && LiveLooper != null)
        {
            var layer = LiveLooper.GetLayer(index);
            layer?.Play();
            if (index >= 0 && index < _layerViewModels.Count)
            {
                _layerViewModels[index].Refresh();
            }
        }
    }

    private void LayerStop_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int index && LiveLooper != null)
        {
            var layer = LiveLooper.GetLayer(index);
            layer?.Stop();
            if (index >= 0 && index < _layerViewModels.Count)
            {
                _layerViewModels[index].Refresh();
            }
        }
    }

    private void InputMonitor_Changed(object sender, RoutedEventArgs e)
    {
        if (LiveLooper != null)
        {
            LiveLooper.InputMonitorEnabled = InputMonitorToggle.IsChecked == true;
        }
    }

    private void TempoSync_Changed(object sender, RoutedEventArgs e)
    {
        if (LiveLooper != null)
        {
            LiveLooper.SyncToTempo = TempoSyncToggle.IsChecked == true;
        }
    }

    private void QuantizeMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (LiveLooper != null && QuantizeModeCombo.SelectedIndex >= 0)
        {
            LiveLooper.QuantizeMode = (LoopQuantizeMode)QuantizeModeCombo.SelectedIndex;
        }
    }

    #endregion

    #region Display Methods

    private void BuildLayerViewModels()
    {
        _layerViewModels.Clear();

        if (LiveLooper == null) return;

        for (int i = 0; i < LiveLooper.LayerCount; i++)
        {
            var layer = LiveLooper.GetLayer(i);
            if (layer != null)
            {
                _layerViewModels.Add(new LoopLayerViewModel(layer));
            }
        }
    }

    private void RefreshAllLayers()
    {
        foreach (var vm in _layerViewModels)
        {
            vm.Refresh();
        }
    }

    private void UpdateDisplay()
    {
        UpdateStateDisplay();
        UpdateLoopInfo();
        UpdateLoopProgress();
    }

    private void UpdateStateDisplay()
    {
        if (LiveLooper == null)
        {
            StateText.Text = " - No Looper";
            return;
        }

        var state = LiveLooper.State;
        StateText.Text = state switch
        {
            LooperState.Empty => " - Empty",
            LooperState.Recording => " - RECORDING",
            LooperState.Playing => " - Playing",
            LooperState.Stopped => " - Stopped",
            LooperState.Overdubbing => " - OVERDUB",
            LooperState.WaitingForSync => " - Waiting...",
            _ => " - Unknown"
        };

        // Update state text color
        StateText.Foreground = state switch
        {
            LooperState.Recording => new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)),
            LooperState.Overdubbing => new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00)),
            LooperState.Playing => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),
            _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
        };

        // Update undo button state
        UndoButton.IsEnabled = LiveLooper.UndoCount > 0;
    }

    private void UpdateLoopInfo()
    {
        if (LiveLooper == null)
        {
            LoopLengthText.Text = "0.0 beats (0.0s)";
            CycleCountText.Text = "0";
            return;
        }

        LoopLengthText.Text = $"{LiveLooper.LoopLengthBeats:F1} beats ({LiveLooper.LoopLengthSeconds:F1}s)";
        CycleCountText.Text = LiveLooper.CycleCount.ToString();
    }

    private void UpdateLoopProgress()
    {
        if (LiveLooper == null) return;

        var fraction = LiveLooper.PlaybackFraction;
        var containerWidth = LoopProgressBar.Parent is Grid grid ? grid.ActualWidth : 200;
        LoopProgressBar.Width = fraction * containerWidth;

        UpdateLoopInfo();
    }

    private void UpdateInputLevel()
    {
        // Simulated input level - in real implementation, this would come from audio input
        // For now, we'll decay the level
        _inputLevel *= 0.95f;
        if (_inputLevel < 0.01f) _inputLevel = 0;

        // Update peak hold
        if (_inputLevel > _inputPeak)
        {
            _inputPeak = _inputLevel;
            _peakHoldCounter = 30; // Hold for ~1 second at 30fps
        }
        else if (_peakHoldCounter > 0)
        {
            _peakHoldCounter--;
        }
        else
        {
            _inputPeak *= 0.98f;
        }

        var containerWidth = InputLevelLeft.Parent is Grid grid ? grid.ActualWidth : 200;
        InputLevelLeft.Width = _inputLevel * containerWidth;

        if (_inputPeak > 0.01f)
        {
            InputPeakIndicator.Visibility = Visibility.Visible;
            InputPeakIndicator.Margin = new Thickness(_inputPeak * containerWidth - 1, 0, 0, 0);
        }
        else
        {
            InputPeakIndicator.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Simulates input level for visualization.
    /// In a real implementation, this would be called with actual audio level data.
    /// </summary>
    /// <param name="level">Input level (0-1).</param>
    public void SetInputLevel(float level)
    {
        _inputLevel = Math.Clamp(level, 0f, 1f);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the layer view model at the specified index.
    /// </summary>
    /// <param name="index">Layer index.</param>
    /// <returns>The layer view model, or null if not found.</returns>
    public LoopLayerViewModel? GetLayerViewModel(int index)
    {
        if (index >= 0 && index < _layerViewModels.Count)
        {
            return _layerViewModels[index];
        }
        return null;
    }

    /// <summary>
    /// Sets the active layer for recording.
    /// </summary>
    /// <param name="index">Layer index.</param>
    public void SetActiveLayer(int index)
    {
        if (LiveLooper != null)
        {
            LiveLooper.ActiveLayerIndex = index;
        }
    }

    /// <summary>
    /// Syncs the looper to an external tempo source.
    /// </summary>
    /// <param name="bpm">Current tempo.</param>
    /// <param name="beat">Current beat position.</param>
    public void SyncToExternalTempo(double bpm, double beat)
    {
        LiveLooper?.SyncToExternalTempo(bpm, beat);
    }

    /// <summary>
    /// Refreshes the entire control display.
    /// </summary>
    public void RefreshDisplay()
    {
        BuildLayerViewModels();
        UpdateDisplay();
    }

    #endregion
}
