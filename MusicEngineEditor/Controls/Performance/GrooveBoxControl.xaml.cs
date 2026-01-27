// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: GrooveBox control providing drum pads, pattern selection, kit browser,
// swing control, bass synth controls, and pattern chain mode for live performance.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using MusicEngine.Core.Performance;

namespace MusicEngineEditor.Controls.Performance;

#region Converters

/// <summary>
/// Converts boolean values to visibility.
/// </summary>
public class GrooveBoxBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
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
/// Inverts boolean values.
/// </summary>
public class GrooveBoxInverseBoolConverter : IValueConverter
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

/// <summary>
/// Converts pattern index to display string (A1-H8).
/// </summary>
public class GrooveBoxPatternIndexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int index)
        {
            int bank = index / 8;
            int number = (index % 8) + 1;
            char bankChar = (char)('A' + bank);
            return $"{bankChar}{number}";
        }
        return "A1";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string str && str.Length >= 2)
        {
            char bankChar = char.ToUpper(str[0]);
            if (bankChar >= 'A' && bankChar <= 'H' && int.TryParse(str.Substring(1), out int number))
            {
                int bank = bankChar - 'A';
                return bank * 8 + (number - 1);
            }
        }
        return 0;
    }
}

/// <summary>
/// Converts pad state to color for visual feedback.
/// </summary>
public class GrooveBoxPadColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive
                ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
                : new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
        }
        return new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return false;
    }
}

#endregion

#region Event Args

/// <summary>
/// Event arguments for pad trigger events.
/// </summary>
public class GrooveBoxPadEventArgs : EventArgs
{
    /// <summary>Gets the pad index (0-15).</summary>
    public int PadIndex { get; }

    /// <summary>Gets the velocity (0-127).</summary>
    public int Velocity { get; }

    /// <summary>Gets whether the pad was pressed (true) or released (false).</summary>
    public bool IsPressed { get; }

    /// <summary>
    /// Creates new pad event arguments.
    /// </summary>
    public GrooveBoxPadEventArgs(int padIndex, int velocity, bool isPressed)
    {
        PadIndex = padIndex;
        Velocity = velocity;
        IsPressed = isPressed;
    }
}

/// <summary>
/// Event arguments for pattern change events.
/// </summary>
public class GrooveBoxPatternEventArgs : EventArgs
{
    /// <summary>Gets the previous pattern index.</summary>
    public int PreviousPattern { get; }

    /// <summary>Gets the new pattern index.</summary>
    public int NewPattern { get; }

    /// <summary>Gets whether the change was queued.</summary>
    public bool IsQueued { get; }

    /// <summary>
    /// Creates new pattern event arguments.
    /// </summary>
    public GrooveBoxPatternEventArgs(int previousPattern, int newPattern, bool isQueued = false)
    {
        PreviousPattern = previousPattern;
        NewPattern = newPattern;
        IsQueued = isQueued;
    }
}

/// <summary>
/// Event arguments for swing change events.
/// </summary>
public class GrooveBoxSwingEventArgs : EventArgs
{
    /// <summary>Gets the swing amount (0.0 to 1.0).</summary>
    public float Swing { get; }

    /// <summary>
    /// Creates new swing event arguments.
    /// </summary>
    public GrooveBoxSwingEventArgs(float swing)
    {
        Swing = swing;
    }
}

#endregion

/// <summary>
/// GrooveBox control providing drum pads, pattern selection, kit browser,
/// swing control, bass synth controls, and pattern chain mode for live performance.
/// </summary>
public partial class GrooveBoxControl : UserControl
{
    #region Constants

    private const int PadCount = 16;
    private const int PatternBankCount = 8;
    private const int PatternsPerBank = 8;
    private const int StepCount = 16;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty GrooveBoxProperty =
        DependencyProperty.Register(nameof(GrooveBox), typeof(GrooveBox), typeof(GrooveBoxControl),
            new PropertyMetadata(null, OnGrooveBoxChanged));

    public static readonly DependencyProperty TempoProperty =
        DependencyProperty.Register(nameof(Tempo), typeof(double), typeof(GrooveBoxControl),
            new PropertyMetadata(120.0, OnTempoChanged));

    public static readonly DependencyProperty SwingProperty =
        DependencyProperty.Register(nameof(Swing), typeof(float), typeof(GrooveBoxControl),
            new PropertyMetadata(0f, OnSwingChanged));

    public static readonly DependencyProperty IsPlayingProperty =
        DependencyProperty.Register(nameof(IsPlaying), typeof(bool), typeof(GrooveBoxControl),
            new PropertyMetadata(false, OnIsPlayingChanged));

    public static readonly DependencyProperty CurrentPatternProperty =
        DependencyProperty.Register(nameof(CurrentPattern), typeof(int), typeof(GrooveBoxControl),
            new PropertyMetadata(0, OnCurrentPatternChanged));

    public static readonly DependencyProperty IsTempoLockedProperty =
        DependencyProperty.Register(nameof(IsTempoLocked), typeof(bool), typeof(GrooveBoxControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty IsPatternChainEnabledProperty =
        DependencyProperty.Register(nameof(IsPatternChainEnabled), typeof(bool), typeof(GrooveBoxControl),
            new PropertyMetadata(false, OnIsPatternChainEnabledChanged));

    public static readonly DependencyProperty IsBassEnabledProperty =
        DependencyProperty.Register(nameof(IsBassEnabled), typeof(bool), typeof(GrooveBoxControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty MasterVolumeProperty =
        DependencyProperty.Register(nameof(MasterVolume), typeof(float), typeof(GrooveBoxControl),
            new PropertyMetadata(0.8f, OnMasterVolumeChanged));

    /// <summary>
    /// Gets or sets the GrooveBox engine instance.
    /// </summary>
    public GrooveBox? GrooveBox
    {
        get => (GrooveBox?)GetValue(GrooveBoxProperty);
        set => SetValue(GrooveBoxProperty, value);
    }

    /// <summary>
    /// Gets or sets the tempo in BPM.
    /// </summary>
    public double Tempo
    {
        get => (double)GetValue(TempoProperty);
        set => SetValue(TempoProperty, value);
    }

    /// <summary>
    /// Gets or sets the swing amount (0.0 to 1.0).
    /// </summary>
    public float Swing
    {
        get => (float)GetValue(SwingProperty);
        set => SetValue(SwingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the sequencer is playing.
    /// </summary>
    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>
    /// Gets or sets the current pattern index.
    /// </summary>
    public int CurrentPattern
    {
        get => (int)GetValue(CurrentPatternProperty);
        set => SetValue(CurrentPatternProperty, value);
    }

    /// <summary>
    /// Gets or sets whether tempo is locked.
    /// </summary>
    public bool IsTempoLocked
    {
        get => (bool)GetValue(IsTempoLockedProperty);
        set => SetValue(IsTempoLockedProperty, value);
    }

    /// <summary>
    /// Gets or sets whether pattern chain mode is enabled.
    /// </summary>
    public bool IsPatternChainEnabled
    {
        get => (bool)GetValue(IsPatternChainEnabledProperty);
        set => SetValue(IsPatternChainEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets whether bass synth is enabled.
    /// </summary>
    public bool IsBassEnabled
    {
        get => (bool)GetValue(IsBassEnabledProperty);
        set => SetValue(IsBassEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets the master volume (0.0 to 1.0).
    /// </summary>
    public float MasterVolume
    {
        get => (float)GetValue(MasterVolumeProperty);
        set => SetValue(MasterVolumeProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when a pad is triggered.
    /// </summary>
    public event EventHandler<GrooveBoxPadEventArgs>? PadTriggered;

    /// <summary>
    /// Fired when a pattern is changed.
    /// </summary>
    public event EventHandler<GrooveBoxPatternEventArgs>? PatternChanged;

    /// <summary>
    /// Fired when swing is changed.
    /// </summary>
    public event EventHandler<GrooveBoxSwingEventArgs>? SwingChanged;

    /// <summary>
    /// Fired when playback starts.
    /// </summary>
    public event EventHandler? PlaybackStarted;

    /// <summary>
    /// Fired when playback stops.
    /// </summary>
    public event EventHandler? PlaybackStopped;

    /// <summary>
    /// Fired when a kit is selected.
    /// </summary>
    public event EventHandler<string>? KitSelected;

    /// <summary>
    /// Fired when master volume changes.
    /// </summary>
    public event EventHandler<float>? MasterVolumeChanged;

    #endregion

    #region Private Fields

    private readonly DispatcherTimer _updateTimer;
    private readonly List<Ellipse> _stepIndicators = new();
    private readonly Button[] _padButtons = new Button[PadCount];
    private bool _isInitialized;
    private int _currentBank;
    private int _currentPatternInBank;
    private int _queuedPattern = -1;
    private bool _isRecording;

    #endregion

    #region Constructor

    public GrooveBoxControl()
    {
        InitializeComponent();

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += UpdateTimer_Tick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;

        // Initialize step indicators
        InitializeStepIndicators();

        // Cache pad buttons
        CachePadButtons();

        // Initialize pattern selector
        UpdatePatternSelector();

        // Update displays
        UpdateTempoDisplay();
        UpdateStepDisplay();
        UpdateSwingDisplay();
        UpdateCurrentPatternDisplay();

        // Start update timer
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer.Stop();
    }

    #endregion

    #region Initialization

    private void InitializeStepIndicators()
    {
        StepIndicatorGrid.Children.Clear();
        _stepIndicators.Clear();

        for (int i = 0; i < StepCount; i++)
        {
            var indicator = new Ellipse
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                Margin = new Thickness(2, 0, 2, 0)
            };
            _stepIndicators.Add(indicator);
            StepIndicatorGrid.Children.Add(indicator);
        }
    }

    private void CachePadButtons()
    {
        _padButtons[0] = Pad1;
        _padButtons[1] = Pad2;
        _padButtons[2] = Pad3;
        _padButtons[3] = Pad4;
        _padButtons[4] = Pad5;
        _padButtons[5] = Pad6;
        _padButtons[6] = Pad7;
        _padButtons[7] = Pad8;
        _padButtons[8] = Pad9;
        _padButtons[9] = Pad10;
        _padButtons[10] = Pad11;
        _padButtons[11] = Pad12;
        _padButtons[12] = Pad13;
        _padButtons[13] = Pad14;
        _padButtons[14] = Pad15;
        _padButtons[15] = Pad16;
    }

    #endregion

    #region Property Changed Handlers

    private static void OnGrooveBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control)
        {
            if (e.OldValue is GrooveBox oldGrooveBox)
            {
                oldGrooveBox.PadTriggered -= control.GrooveBox_PadTriggered;
                oldGrooveBox.StepChanged -= control.GrooveBox_StepChanged;
                oldGrooveBox.PatternChanged -= control.GrooveBox_PatternChanged;
            }

            if (e.NewValue is GrooveBox newGrooveBox)
            {
                newGrooveBox.PadTriggered += control.GrooveBox_PadTriggered;
                newGrooveBox.StepChanged += control.GrooveBox_StepChanged;
                newGrooveBox.PatternChanged += control.GrooveBox_PatternChanged;
                control.SyncFromGrooveBox(newGrooveBox);
            }
        }
    }

    private static void OnTempoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control)
        {
            if (!control.IsTempoLocked && control.GrooveBox != null)
            {
                control.GrooveBox.Bpm = (double)e.NewValue;
            }
            control.UpdateTempoDisplay();
        }
    }

    private static void OnSwingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control)
        {
            if (control.GrooveBox != null)
            {
                control.GrooveBox.Swing = (float)e.NewValue;
            }
            control.UpdateSwingDisplay();
        }
    }

    private static void OnIsPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control)
        {
            control.UpdatePlaybackState();
        }
    }

    private static void OnCurrentPatternChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control)
        {
            int newPattern = (int)e.NewValue;
            control._currentBank = newPattern / PatternsPerBank;
            control._currentPatternInBank = newPattern % PatternsPerBank;
            control.UpdatePatternSelector();
            control.UpdateCurrentPatternDisplay();

            if (control.GrooveBox != null)
            {
                control.GrooveBox.SelectPattern(newPattern);
            }
        }
    }

    private static void OnIsPatternChainEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control && control.GrooveBox != null)
        {
            control.GrooveBox.SongMode = (bool)e.NewValue;
        }
    }

    private static void OnMasterVolumeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GrooveBoxControl control && control.GrooveBox != null)
        {
            control.GrooveBox.Volume = (float)e.NewValue;
        }
    }

    #endregion

    #region GrooveBox Event Handlers

    private void GrooveBox_PadTriggered(object? sender, PadTriggerEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            FlashPad(e.PadIndex);
        });
    }

    private void GrooveBox_StepChanged(object? sender, StepChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            UpdateStepIndicator(e.StepIndex);
            StepDisplay.Text = (e.StepIndex + 1).ToString();
        });
    }

    private void GrooveBox_PatternChanged(object? sender, PatternChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            CurrentPattern = e.NewPattern;
            _queuedPattern = -1;
            UpdateQueuedPatternDisplay();
        });
    }

    #endregion

    #region UI Event Handlers

    private void DrumPad_Click(object sender, RoutedEventArgs e)
    {
        // Click is handled for non-velocity-sensitive triggers
    }

    private void DrumPad_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is int padIndex)
        {
            // Calculate velocity based on mouse position (center = max velocity)
            int velocity = 100;

            TriggerPad(padIndex, velocity, true);
        }
    }

    private void DrumPad_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.Tag is int padIndex)
        {
            TriggerPad(padIndex, 0, false);
        }
    }

    private void PatternBank_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string tagStr && int.TryParse(tagStr, out int bankIndex))
        {
            _currentBank = bankIndex;
            UpdatePatternSelector();
            int newPattern = _currentBank * PatternsPerBank + _currentPatternInBank;

            if (IsPlaying)
            {
                QueuePatternChange(newPattern);
            }
            else
            {
                CurrentPattern = newPattern;
            }
        }
    }

    private void PatternNumber_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string tagStr && int.TryParse(tagStr, out int patternInBank))
        {
            _currentPatternInBank = patternInBank;
            UpdatePatternSelector();
            int newPattern = _currentBank * PatternsPerBank + _currentPatternInBank;

            if (IsPlaying)
            {
                QueuePatternChange(newPattern);
            }
            else
            {
                CurrentPattern = newPattern;
            }
        }
    }

    private void PatternChainToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsPatternChainEnabled = PatternChainToggle.IsChecked == true;
    }

    private void TempoLockToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsTempoLocked = TempoLockToggle.IsChecked == true;
    }

    private void Play_Click(object sender, RoutedEventArgs e)
    {
        if (IsPlaying)
        {
            Stop();
        }
        else
        {
            Play();
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        Stop();
    }

    private void Record_Click(object sender, RoutedEventArgs e)
    {
        _isRecording = !_isRecording;
        UpdateRecordingState();

        if (_isRecording && !IsPlaying)
        {
            Play();
        }
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        if (GrooveBox != null)
        {
            GrooveBox.FillMode = true;
            GrooveBox.ActiveFillType = FillType.Simple;
        }
        StatusText.Text = " - Fill";
    }

    private void VariationToggle_Changed(object sender, RoutedEventArgs e)
    {
        bool useVariationA = VariationToggle.IsChecked != true;
        GrooveBox?.SetVariation(useVariationA);
        VariationToggle.Content = useVariationA ? "VAR A" : "VAR B";
    }

    private void KitComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isInitialized) return;

        string? kitName = (KitComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        if (!string.IsNullOrEmpty(kitName))
        {
            KitSelected?.Invoke(this, kitName);
            UpdatePadNames(kitName);
        }
    }

    private void PrevKit_Click(object sender, RoutedEventArgs e)
    {
        if (KitComboBox.SelectedIndex > 0)
        {
            KitComboBox.SelectedIndex--;
        }
    }

    private void NextKit_Click(object sender, RoutedEventArgs e)
    {
        if (KitComboBox.SelectedIndex < KitComboBox.Items.Count - 1)
        {
            KitComboBox.SelectedIndex++;
        }
    }

    private void SwingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float swingValue = (float)(e.NewValue / 100.0);
        Swing = swingValue;
        UpdateSwingDisplay();
        SwingChanged?.Invoke(this, new GrooveBoxSwingEventArgs(swingValue));
    }

    private void BassEnableToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsBassEnabled = BassEnableToggle.IsChecked == true;
        // Bass synth enable/disable would be implemented here
    }

    private void BassCutoffSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        BassCutoffText.Text = $"{(int)e.NewValue}%";
        // Apply to bass synth
    }

    private void BassResonanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        BassResonanceText.Text = $"{(int)e.NewValue}%";
        // Apply to bass synth
    }

    private void BassDecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;
        BassDecayText.Text = $"{(int)e.NewValue}%";
        // Apply to bass synth
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized) return;

        float volume = (float)(e.NewValue / 100.0);
        MasterVolume = volume;
        MasterVolumeText.Text = $"{(int)e.NewValue}%";
        MasterVolumeChanged?.Invoke(this, volume);
    }

    #endregion

    #region Update Methods

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isInitialized || GrooveBox == null) return;

        // Update tempo display if not locked
        if (!IsTempoLocked && Math.Abs(Tempo - GrooveBox.Bpm) > 0.01)
        {
            Tempo = GrooveBox.Bpm;
        }

        // Update step display
        if (GrooveBox.IsPlaying)
        {
            UpdateStepIndicator(GrooveBox.CurrentStep);
        }

        // Update queued pattern display
        if (_queuedPattern >= 0)
        {
            UpdateQueuedPatternDisplay();
        }
    }

    private void UpdateTempoDisplay()
    {
        TempoDisplay.Text = $"{Tempo:F1}";
    }

    private void UpdateStepDisplay()
    {
        int currentStep = GrooveBox?.CurrentStep ?? 0;
        int totalSteps = GrooveBox?.CurrentPattern.StepCount ?? StepCount;
        StepDisplay.Text = (currentStep + 1).ToString();
        StepTotalDisplay.Text = $"/{totalSteps}";
    }

    private void UpdateSwingDisplay()
    {
        SwingValueText.Text = $"{(int)(Swing * 100)}%";
        SwingSlider.Value = Swing * 100;
    }

    private void UpdateCurrentPatternDisplay()
    {
        int bank = CurrentPattern / PatternsPerBank;
        int number = (CurrentPattern % PatternsPerBank) + 1;
        char bankChar = (char)('A' + bank);
        CurrentPatternText.Text = $"{bankChar}{number}";
    }

    private void UpdateQueuedPatternDisplay()
    {
        if (_queuedPattern >= 0)
        {
            int bank = _queuedPattern / PatternsPerBank;
            int number = (_queuedPattern % PatternsPerBank) + 1;
            char bankChar = (char)('A' + bank);
            QueuedPatternText.Text = $"Next: {bankChar}{number}";
        }
        else
        {
            QueuedPatternText.Text = "";
        }
    }

    private void UpdatePatternSelector()
    {
        // Update bank buttons
        foreach (ToggleButton button in PatternBankGrid.Children)
        {
            if (button.Tag is string tagStr && int.TryParse(tagStr, out int bankIndex))
            {
                button.IsChecked = bankIndex == _currentBank;
            }
        }

        // Update pattern number buttons
        foreach (ToggleButton button in PatternNumberGrid.Children)
        {
            if (button.Tag is string tagStr && int.TryParse(tagStr, out int patternIndex))
            {
                button.IsChecked = patternIndex == _currentPatternInBank;
            }
        }
    }

    private void UpdateStepIndicator(int currentStep)
    {
        for (int i = 0; i < _stepIndicators.Count; i++)
        {
            if (i == currentStep)
            {
                _stepIndicators[i].Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF));
            }
            else
            {
                // Check if step has active notes
                bool hasActiveNote = false;
                if (GrooveBox != null)
                {
                    foreach (var pad in GrooveBox.Pads)
                    {
                        if (i < pad.Steps.Length && pad.Steps[i].Active)
                        {
                            hasActiveNote = true;
                            break;
                        }
                    }
                }

                _stepIndicators[i].Fill = hasActiveNote
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
                    : new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            }
        }
    }

    private void UpdatePlaybackState()
    {
        if (IsPlaying)
        {
            StatusText.Text = " - Playing";
            // Update play button appearance could be done here
        }
        else
        {
            StatusText.Text = " - Stopped";
            // Reset step indicators
            foreach (var indicator in _stepIndicators)
            {
                indicator.Fill = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
            }
        }
    }

    private void UpdateRecordingState()
    {
        if (_isRecording)
        {
            StatusText.Text = " - Recording";
            RecordButton.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
        }
        else
        {
            StatusText.Text = IsPlaying ? " - Playing" : " - Ready";
            RecordButton.Background = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18));
        }
    }

    private void UpdatePadNames(string kitName)
    {
        // This would update pad names based on kit
        // For now, keep default names
    }

    #endregion

    #region Pad Operations

    private void TriggerPad(int padIndex, int velocity, bool isPressed)
    {
        if (padIndex < 0 || padIndex >= PadCount) return;

        if (isPressed)
        {
            GrooveBox?.TriggerPad(padIndex, velocity);
            FlashPad(padIndex);

            // If recording, add step to current position
            if (_isRecording && GrooveBox != null)
            {
                int currentStep = GrooveBox.CurrentStep;
                GrooveBox.SetStep(padIndex, currentStep, true);
                GrooveBox.SetStepProperties(padIndex, currentStep, velocity: velocity);
            }
        }
        else
        {
            GrooveBox?.ReleasePad(padIndex);
        }

        PadTriggered?.Invoke(this, new GrooveBoxPadEventArgs(padIndex, velocity, isPressed));
    }

    private void FlashPad(int padIndex)
    {
        if (padIndex < 0 || padIndex >= _padButtons.Length) return;

        var button = _padButtons[padIndex];
        if (button == null) return;

        // Flash effect using background color
        var originalBrush = button.Background;
        button.Background = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));

        var timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        timer.Tick += (s, e) =>
        {
            button.Background = originalBrush;
            timer.Stop();
        };
        timer.Start();
    }

    #endregion

    #region Pattern Operations

    private void QueuePatternChange(int patternIndex)
    {
        _queuedPattern = patternIndex;
        GrooveBox?.QueuePattern(patternIndex);
        UpdateQueuedPatternDisplay();

        int oldPattern = CurrentPattern;
        PatternChanged?.Invoke(this, new GrooveBoxPatternEventArgs(oldPattern, patternIndex, true));
    }

    #endregion

    #region Playback Control

    /// <summary>
    /// Starts playback.
    /// </summary>
    public void Play()
    {
        GrooveBox?.Start();
        IsPlaying = true;
        PlaybackStarted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Stops playback.
    /// </summary>
    public void Stop()
    {
        GrooveBox?.Stop();
        IsPlaying = false;
        _isRecording = false;
        UpdateRecordingState();
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Synchronization

    private void SyncFromGrooveBox(GrooveBox grooveBox)
    {
        if (!_isInitialized) return;

        Tempo = grooveBox.Bpm;
        Swing = grooveBox.Swing;
        IsPlaying = grooveBox.IsPlaying;
        CurrentPattern = grooveBox.CurrentPatternIndex;
        MasterVolume = grooveBox.Volume;
        IsPatternChainEnabled = grooveBox.SongMode;

        UpdateTempoDisplay();
        UpdateSwingDisplay();
        UpdateCurrentPatternDisplay();
        MasterVolumeSlider.Value = MasterVolume * 100;
        PatternChainToggle.IsChecked = IsPatternChainEnabled;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Triggers a pad by index.
    /// </summary>
    /// <param name="padIndex">Pad index (0-15).</param>
    /// <param name="velocity">Velocity (0-127).</param>
    public void TriggerPadByIndex(int padIndex, int velocity = 100)
    {
        TriggerPad(padIndex, velocity, true);
    }

    /// <summary>
    /// Releases a pad by index.
    /// </summary>
    /// <param name="padIndex">Pad index (0-15).</param>
    public void ReleasePadByIndex(int padIndex)
    {
        TriggerPad(padIndex, 0, false);
    }

    /// <summary>
    /// Selects a pattern by bank and number.
    /// </summary>
    /// <param name="bank">Bank (0-7 for A-H).</param>
    /// <param name="number">Pattern number (0-7 for 1-8).</param>
    public void SelectPattern(int bank, int number)
    {
        if (bank < 0 || bank >= PatternBankCount) return;
        if (number < 0 || number >= PatternsPerBank) return;

        _currentBank = bank;
        _currentPatternInBank = number;
        int newPattern = bank * PatternsPerBank + number;

        if (IsPlaying)
        {
            QueuePatternChange(newPattern);
        }
        else
        {
            CurrentPattern = newPattern;
        }
    }

    /// <summary>
    /// Sets the tempo.
    /// </summary>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SetTempo(double bpm)
    {
        if (!IsTempoLocked)
        {
            Tempo = Math.Clamp(bpm, 20, 300);
        }
    }

    /// <summary>
    /// Sets the swing amount.
    /// </summary>
    /// <param name="swing">Swing amount (0.0 to 1.0).</param>
    public void SetSwing(float swing)
    {
        Swing = Math.Clamp(swing, 0f, 1f);
    }

    /// <summary>
    /// Refreshes the control display from the engine state.
    /// </summary>
    public void RefreshFromEngine()
    {
        if (GrooveBox != null)
        {
            SyncFromGrooveBox(GrooveBox);
        }
    }

    #endregion
}
