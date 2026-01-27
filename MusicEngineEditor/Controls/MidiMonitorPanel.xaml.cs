// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// MIDI Monitor Panel for real-time MIDI message visualization.
/// Displays incoming and outgoing MIDI messages with filtering capabilities.
/// </summary>
public partial class MidiMonitorPanel : UserControl, INotifyPropertyChanged
{
    #region Constants

    private const int DefaultMaxMessages = 1000;
    private const double ChannelActivityDecayMs = 500;

    #endregion

    #region Private Fields

    private readonly ObservableCollection<MidiMessageDisplay> _messages = [];
    private readonly ObservableCollection<MidiMessageDisplay> _filteredMessages = [];
    private readonly Border[] _channelIndicators = new Border[16];
    private readonly DispatcherTimer[] _channelDecayTimers = new DispatcherTimer[16];
    private readonly DateTime[] _lastChannelActivity = new DateTime[16];

    // Note: IsRecording and IsPaused are managed via dependency properties
    private int _maxMessages = DefaultMaxMessages;
    private int _selectedChannelFilter = -1; // -1 = All
    private MidiDirection _selectedDirectionFilter = MidiDirection.All;
    private bool _autoScroll = true;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MessagesProperty =
        DependencyProperty.Register(nameof(Messages), typeof(ObservableCollection<MidiMessageDisplay>), typeof(MidiMonitorPanel),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsRecordingProperty =
        DependencyProperty.Register(nameof(IsRecording), typeof(bool), typeof(MidiMonitorPanel),
            new PropertyMetadata(true, OnIsRecordingChanged));

    public static readonly DependencyProperty IsPausedProperty =
        DependencyProperty.Register(nameof(IsPaused), typeof(bool), typeof(MidiMonitorPanel),
            new PropertyMetadata(false));

    /// <summary>
    /// Gets the collection of MIDI messages.
    /// </summary>
    public ObservableCollection<MidiMessageDisplay> Messages
    {
        get => (ObservableCollection<MidiMessageDisplay>)GetValue(MessagesProperty);
        private set => SetValue(MessagesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether recording is enabled.
    /// </summary>
    public bool IsRecording
    {
        get => (bool)GetValue(IsRecordingProperty);
        set => SetValue(IsRecordingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether display is paused.
    /// </summary>
    public bool IsPaused
    {
        get => (bool)GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when a MIDI message is received.
    /// </summary>
    public event EventHandler<MidiMessageDisplay>? MessageReceived;

    #endregion

    #region Constructor

    public MidiMonitorPanel()
    {
        InitializeComponent();
        Messages = _messages;
        DataContext = this;

        InitializeChannelIndicators();
        InitializeFilterEvents();

        MessageListBox.ItemsSource = _filteredMessages;
        UpdateMessageCount();
    }

    #endregion

    #region Initialization

    private void InitializeChannelIndicators()
    {
        ChannelIndicators.Items.Clear();

        for (int i = 0; i < 16; i++)
        {
            int channel = i + 1;
            var indicator = new Border
            {
                Width = 18,
                Height = 18,
                CornerRadius = new CornerRadius(3),
                Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(1),
                ToolTip = $"Channel {channel}: No Activity",
                Child = new TextBlock
                {
                    Text = channel.ToString(),
                    FontSize = 8,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };

            _channelIndicators[i] = indicator;
            ChannelIndicators.Items.Add(indicator);

            // Initialize decay timer for this channel
            _channelDecayTimers[i] = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(ChannelActivityDecayMs)
            };
            int channelIndex = i;
            _channelDecayTimers[i].Tick += (s, e) => DecayChannelActivity(channelIndex);
        }
    }

    private void InitializeFilterEvents()
    {
        FilterNotes.Checked += OnFilterChanged;
        FilterNotes.Unchecked += OnFilterChanged;
        FilterCC.Checked += OnFilterChanged;
        FilterCC.Unchecked += OnFilterChanged;
        FilterPitchBend.Checked += OnFilterChanged;
        FilterPitchBend.Unchecked += OnFilterChanged;
        FilterAftertouch.Checked += OnFilterChanged;
        FilterAftertouch.Unchecked += OnFilterChanged;
        FilterProgram.Checked += OnFilterChanged;
        FilterProgram.Unchecked += OnFilterChanged;
        FilterSysEx.Checked += OnFilterChanged;
        FilterSysEx.Unchecked += OnFilterChanged;
        FilterClock.Checked += OnFilterChanged;
        FilterClock.Unchecked += OnFilterChanged;

        ChannelFilterComboBox.SelectionChanged += OnChannelFilterChanged;
        DirectionFilterComboBox.SelectionChanged += OnDirectionFilterChanged;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a MIDI message to the monitor.
    /// </summary>
    /// <param name="message">The MIDI message to add.</param>
    public void AddMessage(MidiMessageDisplay message)
    {
        if (!IsRecording) return;

        Dispatcher.BeginInvoke(() =>
        {
            // Add to main collection
            _messages.Add(message);

            // Enforce max messages limit
            while (_messages.Count > _maxMessages)
            {
                _messages.RemoveAt(0);
            }

            // Update channel activity
            if (message.Channel >= 1 && message.Channel <= 16)
            {
                UpdateChannelActivity(message.Channel - 1);
            }

            // Apply filter and add to filtered collection if not paused
            if (!IsPaused && PassesFilter(message))
            {
                _filteredMessages.Add(message);

                // Enforce max on filtered too
                while (_filteredMessages.Count > _maxMessages)
                {
                    _filteredMessages.RemoveAt(0);
                }

                // Auto-scroll to bottom
                if (_autoScroll && MessageListBox.Items.Count > 0)
                {
                    MessageListBox.ScrollIntoView(MessageListBox.Items[^1]);
                }
            }

            UpdateMessageCount();
            MessageReceived?.Invoke(this, message);
        });
    }

    /// <summary>
    /// Adds a Note On message.
    /// </summary>
    public void AddNoteOn(int channel, int note, int velocity, MidiDirection direction = MidiDirection.In)
    {
        var noteName = GetNoteName(note);
        AddMessage(new MidiMessageDisplay
        {
            MessageType = MidiMessageType.NoteOn,
            Channel = channel,
            Data1 = note,
            Data2 = velocity,
            Direction = direction,
            DataDisplay = $"{noteName} Vel:{velocity}"
        });
    }

    /// <summary>
    /// Adds a Note Off message.
    /// </summary>
    public void AddNoteOff(int channel, int note, int velocity, MidiDirection direction = MidiDirection.In)
    {
        var noteName = GetNoteName(note);
        AddMessage(new MidiMessageDisplay
        {
            MessageType = MidiMessageType.NoteOff,
            Channel = channel,
            Data1 = note,
            Data2 = velocity,
            Direction = direction,
            DataDisplay = $"{noteName}"
        });
    }

    /// <summary>
    /// Adds a Control Change message.
    /// </summary>
    public void AddControlChange(int channel, int controller, int value, MidiDirection direction = MidiDirection.In)
    {
        var ccName = GetCCName(controller);
        AddMessage(new MidiMessageDisplay
        {
            MessageType = MidiMessageType.ControlChange,
            Channel = channel,
            Data1 = controller,
            Data2 = value,
            Direction = direction,
            DataDisplay = $"{ccName} = {value}"
        });
    }

    /// <summary>
    /// Adds a Pitch Bend message.
    /// </summary>
    public void AddPitchBend(int channel, int value, MidiDirection direction = MidiDirection.In)
    {
        int centered = value - 8192;
        string sign = centered >= 0 ? "+" : "";
        AddMessage(new MidiMessageDisplay
        {
            MessageType = MidiMessageType.PitchBend,
            Channel = channel,
            Data1 = value,
            Direction = direction,
            DataDisplay = $"{sign}{centered}"
        });
    }

    /// <summary>
    /// Clears all messages.
    /// </summary>
    public void Clear()
    {
        _messages.Clear();
        _filteredMessages.Clear();
        UpdateMessageCount();
    }

    /// <summary>
    /// Refreshes the filtered message list based on current filter settings.
    /// </summary>
    public void RefreshFilter()
    {
        _filteredMessages.Clear();

        foreach (var message in _messages)
        {
            if (PassesFilter(message))
            {
                _filteredMessages.Add(message);
            }
        }

        UpdateMessageCount();
    }

    #endregion

    #region Private Methods

    private bool PassesFilter(MidiMessageDisplay message)
    {
        // Check message type filter
        bool typePass = message.MessageType switch
        {
            MidiMessageType.NoteOn or MidiMessageType.NoteOff => FilterNotes.IsChecked == true,
            MidiMessageType.ControlChange => FilterCC.IsChecked == true,
            MidiMessageType.PitchBend => FilterPitchBend.IsChecked == true,
            MidiMessageType.Aftertouch or MidiMessageType.ChannelPressure => FilterAftertouch.IsChecked == true,
            MidiMessageType.ProgramChange => FilterProgram.IsChecked == true,
            MidiMessageType.SysEx => FilterSysEx.IsChecked == true,
            MidiMessageType.Clock => FilterClock.IsChecked == true,
            _ => true
        };

        if (!typePass) return false;

        // Check channel filter
        if (_selectedChannelFilter >= 0 && message.Channel != _selectedChannelFilter + 1)
        {
            return false;
        }

        // Check direction filter
        if (_selectedDirectionFilter != MidiDirection.All && message.Direction != _selectedDirectionFilter)
        {
            return false;
        }

        return true;
    }

    private void UpdateChannelActivity(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= 16) return;

        var indicator = _channelIndicators[channelIndex];
        indicator.Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // Green
        indicator.ToolTip = $"Channel {channelIndex + 1}: Active";

        _lastChannelActivity[channelIndex] = DateTime.Now;

        // Reset and start decay timer
        _channelDecayTimers[channelIndex].Stop();
        _channelDecayTimers[channelIndex].Start();
    }

    private void DecayChannelActivity(int channelIndex)
    {
        if (channelIndex < 0 || channelIndex >= 16) return;

        _channelDecayTimers[channelIndex].Stop();

        var indicator = _channelIndicators[channelIndex];
        indicator.Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        indicator.ToolTip = $"Channel {channelIndex + 1}: No Activity";
    }

    private void UpdateMessageCount()
    {
        MessageCountText.Text = $"{_filteredMessages.Count} messages ({_messages.Count} total)";
    }

    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{noteNames[noteIndex]}{octave}";
    }

    private static string GetCCName(int controller)
    {
        return controller switch
        {
            1 => "Mod",
            7 => "Vol",
            10 => "Pan",
            11 => "Expr",
            64 => "Sustain",
            74 => "Filter",
            _ => $"CC{controller}"
        };
    }

    #endregion

    #region Event Handlers

    private static void OnIsRecordingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiMonitorPanel panel)
        {
            panel.RecordingIndicator.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        RefreshFilter();
    }

    private void OnChannelFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedChannelFilter = ChannelFilterComboBox.SelectedIndex - 1; // -1 for "All"
        RefreshFilter();
    }

    private void OnDirectionFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedDirectionFilter = DirectionFilterComboBox.SelectedIndex switch
        {
            1 => MidiDirection.In,
            2 => MidiDirection.Out,
            _ => MidiDirection.All
        };
        RefreshFilter();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Clear();
    }

    private void MaxMessages_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MaxMessagesComboBox.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out int max))
        {
            _maxMessages = max;

            // Trim existing messages if needed
            while (_messages.Count > _maxMessages)
            {
                _messages.RemoveAt(0);
            }
            while (_filteredMessages.Count > _maxMessages)
            {
                _filteredMessages.RemoveAt(0);
            }

            UpdateMessageCount();
        }
    }

    #endregion

    #region INotifyPropertyChanged

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

/// <summary>
/// Represents a MIDI message for display in the monitor.
/// </summary>
public partial class MidiMessageDisplay : ObservableObject
{
    /// <summary>
    /// Timestamp when the message was received.
    /// </summary>
    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    /// <summary>
    /// MIDI message type.
    /// </summary>
    [ObservableProperty]
    private MidiMessageType _messageType;

    /// <summary>
    /// MIDI channel (1-16).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    /// <summary>
    /// First data byte.
    /// </summary>
    [ObservableProperty]
    private int _data1;

    /// <summary>
    /// Second data byte.
    /// </summary>
    [ObservableProperty]
    private int _data2;

    /// <summary>
    /// Message direction (In/Out).
    /// </summary>
    [ObservableProperty]
    private MidiDirection _direction = MidiDirection.In;

    /// <summary>
    /// Formatted display string for the data.
    /// </summary>
    [ObservableProperty]
    private string _dataDisplay = string.Empty;

    /// <summary>
    /// Raw bytes for SysEx messages.
    /// </summary>
    [ObservableProperty]
    private byte[]? _rawData;

    /// <summary>
    /// Gets the type name for display.
    /// </summary>
    public string TypeName => MessageType switch
    {
        MidiMessageType.NoteOn => "Note On",
        MidiMessageType.NoteOff => "Note Off",
        MidiMessageType.ControlChange => "CC",
        MidiMessageType.PitchBend => "Bend",
        MidiMessageType.Aftertouch => "Poly AT",
        MidiMessageType.ChannelPressure => "Chan AT",
        MidiMessageType.ProgramChange => "PC",
        MidiMessageType.SysEx => "SysEx",
        MidiMessageType.Clock => "Clock",
        MidiMessageType.Start => "Start",
        MidiMessageType.Stop => "Stop",
        MidiMessageType.Continue => "Cont",
        _ => "Other"
    };
}

/// <summary>
/// MIDI message type enumeration.
/// </summary>
public enum MidiMessageType
{
    NoteOff,
    NoteOn,
    Aftertouch,
    ControlChange,
    ProgramChange,
    ChannelPressure,
    PitchBend,
    SysEx,
    Clock,
    Start,
    Stop,
    Continue,
    Other
}

/// <summary>
/// MIDI message direction.
/// </summary>
public enum MidiDirection
{
    All,
    In,
    Out
}
