// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing time signature markers on a timeline ruler.
/// </summary>
public partial class TimeSignatureMarker : UserControl
{
    private TimeSignatureTrack? _timeSignatureTrack;
    private readonly Dictionary<int, UIElement> _markerElements = [];
    private TimeSignaturePoint? _selectedPoint;
    private TimeSignaturePoint? _draggingPoint;
    private Point _dragStartPoint;
    private int _dragStartBar;
    private bool _isDragging;
    private double _contextMenuPosition;
    private int _editingBar = -1;

    /// <summary>
    /// Gets or sets the time signature track to display.
    /// </summary>
    public TimeSignatureTrack? TimeSignatureTrack
    {
        get => _timeSignatureTrack;
        set
        {
            if (_timeSignatureTrack != null)
            {
                _timeSignatureTrack.TimeSignatureChanged -= OnTimeSignatureChanged;
            }

            _timeSignatureTrack = value;

            if (_timeSignatureTrack != null)
            {
                _timeSignatureTrack.TimeSignatureChanged += OnTimeSignatureChanged;
            }

            RefreshMarkers();
        }
    }

    /// <summary>
    /// Gets or sets the number of beats visible in the view.
    /// </summary>
    public double VisibleBeats { get; set; } = 32;

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset { get; set; }

    /// <summary>
    /// Gets or sets the current playback position in beats.
    /// </summary>
    public double PlaybackPosition
    {
        get => _playbackPosition;
        set
        {
            _playbackPosition = value;
            UpdateCurrentTimeSignatureDisplay();
        }
    }
    private double _playbackPosition;

    /// <summary>
    /// Event raised when a time signature change is requested.
    /// </summary>
    public event EventHandler<TimeSignatureChangeRequestedEventArgs>? TimeSignatureChangeRequested;

    /// <summary>
    /// Event raised when a time signature is removed.
    /// </summary>
    public event EventHandler<int>? TimeSignatureRemoved;

    /// <summary>
    /// Event raised when a jump to bar is requested.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for external jump handling
    public event EventHandler<int>? JumpToBarRequested;
#pragma warning restore CS0067

    public TimeSignatureMarker()
    {
        InitializeComponent();
        DataContext = this;
        SizeChanged += (_, _) => RefreshMarkers();
        Loaded += (_, _) => RefreshMarkers();
    }

    private void OnTimeSignatureChanged(object? sender, TimeSignatureTrackChangedEventArgs e)
    {
        Dispatcher.Invoke(RefreshMarkers);
    }

    /// <summary>
    /// Refreshes the visual display of all time signature markers.
    /// </summary>
    public void RefreshMarkers()
    {
        // Clear existing marker elements
        foreach (var element in _markerElements.Values)
        {
            TimeSignatureCanvas.Children.Remove(element);
        }
        _markerElements.Clear();

        if (_timeSignatureTrack == null)
            return;

        // Always add the initial time signature at bar 0
        AddMarkerElementForBar(0, _timeSignatureTrack.DefaultTimeSignature);

        // Add marker elements for each change point
        foreach (var point in _timeSignatureTrack.GetTimeSignaturePoints())
        {
            if (point.BarNumber > 0) // Skip bar 0 as we already added default
            {
                AddMarkerElementForBar(point.BarNumber, point.TimeSignature);
            }
            else
            {
                // Update bar 0 if there's an explicit change
                if (_markerElements.TryGetValue(0, out var element))
                {
                    TimeSignatureCanvas.Children.Remove(element);
                }
                AddMarkerElementForBar(0, point.TimeSignature);
            }
        }

        UpdateCurrentTimeSignatureDisplay();
    }

    private void AddMarkerElementForBar(int barNumber, TimeSignature timeSignature)
    {
        var container = new Canvas
        {
            Tag = barNumber,
            Cursor = Cursors.Hand
        };

        // Create the time signature label
        var label = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = timeSignature.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            }
        };

        // Create the vertical line
        var line = new Rectangle
        {
            Width = 1,
            Fill = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC)),
            Height = Math.Max(1, TimeSignatureCanvas.ActualHeight),
            Opacity = 0.5
        };

        Canvas.SetTop(label, 2);
        Canvas.SetLeft(label, 2);
        Canvas.SetTop(line, 0);
        Canvas.SetLeft(line, 0);

        container.Children.Add(line);
        container.Children.Add(label);

        // Event handlers
        container.MouseLeftButtonDown += MarkerElement_MouseLeftButtonDown;
        container.MouseEnter += MarkerElement_MouseEnter;
        container.MouseLeave += MarkerElement_MouseLeave;

        _markerElements[barNumber] = container;
        TimeSignatureCanvas.Children.Add(container);
        PositionMarkerElement(barNumber, container);
    }

    private void PositionMarkerElement(int barNumber, UIElement element)
    {
        if (element is not Canvas container)
            return;

        var canvasWidth = TimeSignatureCanvas.ActualWidth;
        if (canvasWidth <= 0 || VisibleBeats <= 0)
            return;

        // Calculate beat position for this bar
        double beatPosition = _timeSignatureTrack?.GetBarStartBeat(barNumber) ?? (barNumber * 4.0);

        var pixelsPerBeat = canvasWidth / VisibleBeats;
        var x = (beatPosition - ScrollOffset) * pixelsPerBeat;

        Canvas.SetLeft(container, x);

        // Update line height
        foreach (var child in container.Children)
        {
            if (child is Rectangle rect)
            {
                rect.Height = TimeSignatureCanvas.ActualHeight;
            }
        }
    }

    private void UpdateCurrentTimeSignatureDisplay()
    {
        if (_timeSignatureTrack == null)
        {
            CurrentTimeSignatureText.Text = "4/4";
            return;
        }

        var currentTimeSignature = _timeSignatureTrack.GetTimeSignatureAtBeat(_playbackPosition);
        CurrentTimeSignatureText.Text = currentTimeSignature.ToString();
    }

    private double PositionToBeats(double x)
    {
        if (TimeSignatureCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return 0;

        var pixelsPerBeat = TimeSignatureCanvas.ActualWidth / VisibleBeats;
        return (x / pixelsPerBeat) + ScrollOffset;
    }

    private int BeatsToBar(double beats)
    {
        if (_timeSignatureTrack == null)
        {
            return (int)(beats / 4.0);
        }
        var (bar, _) = _timeSignatureTrack.AbsoluteBeatToBarBeat(beats);
        return bar;
    }

    private (int Numerator, int Denominator) GetSelectedTimeSignature()
    {
        if (TimeSignatureComboBox.SelectedItem is ComboBoxItem item)
        {
            var tag = item.Tag?.ToString();
            if (tag == "custom")
            {
                return (4, 4); // Default for custom, will open dialog
            }

            if (!string.IsNullOrEmpty(tag))
            {
                var parts = tag.Split(',');
                if (parts.Length == 2 &&
                    int.TryParse(parts[0], out int num) &&
                    int.TryParse(parts[1], out int denom))
                {
                    return (num, denom);
                }
            }
        }
        return (4, 4);
    }

    #region Event Handlers

    private void MarkerElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas container && container.Tag is int barNumber)
        {
            _selectedPoint = _timeSignatureTrack?.GetPointAtBar(barNumber);
            _draggingPoint = _selectedPoint;
            _dragStartPoint = e.GetPosition(TimeSignatureCanvas);
            _dragStartBar = barNumber;
            _isDragging = false;

            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MarkerElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Canvas container)
        {
            container.Opacity = 0.85;
        }
    }

    private void MarkerElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Canvas container)
        {
            container.Opacity = 1.0;
        }
    }

    private void TimeSignatureCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to add time signature
        if (e.ClickCount == 2)
        {
            var beats = PositionToBeats(e.GetPosition(TimeSignatureCanvas).X);
            var bar = BeatsToBar(beats);
            _editingBar = bar;
            ShowEditPopup(true);
        }
    }

    private void TimeSignatureCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = PositionToBeats(e.GetPosition(TimeSignatureCanvas).X);

        // Check if clicking on a marker
        var bar = BeatsToBar(_contextMenuPosition);
        _selectedPoint = _timeSignatureTrack?.GetPointAtBar(bar);

        // Enable/disable context menu items based on selection
        var hasMarkerAtBar = _timeSignatureTrack?.HasChangeAtBar(bar) == true;
        EditTimeSignatureMenuItem.IsEnabled = hasMarkerAtBar || bar == 0;
        DeleteTimeSignatureMenuItem.IsEnabled = hasMarkerAtBar && bar > 0; // Cannot delete bar 0
    }

    private void TimeSignatureCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPoint != null && e.LeftButton == MouseButtonState.Pressed && _dragStartBar > 0)
        {
            var currentPoint = e.GetPosition(TimeSignatureCanvas);
            var delta = currentPoint.X - _dragStartPoint.X;

            if (!_isDragging && Math.Abs(delta) > 5)
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                // Moving time signature changes is complex and not typically supported
                // For now, just show visual feedback
            }
        }
    }

    private void TimeSignatureCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPoint != null)
        {
            if (!_isDragging && _dragStartBar >= 0)
            {
                // It was a click, not a drag - show edit popup
                _editingBar = _dragStartBar;
                ShowEditPopup(false);
            }

            // Find and release the container
            if (_markerElements.TryGetValue(_dragStartBar, out var element) && element is Canvas container)
            {
                container.ReleaseMouseCapture();
            }

            _draggingPoint = null;
            _isDragging = false;
        }
    }

    private void ShowEditPopup(bool isNew)
    {
        if (_timeSignatureTrack == null)
            return;

        var ts = _timeSignatureTrack.GetTimeSignatureAtBar(_editingBar);
        NumeratorTextBox.Text = ts.Numerator.ToString();

        // Set denominator combo
        int denomIndex = ts.Denominator switch
        {
            1 => 0,
            2 => 1,
            4 => 2,
            8 => 3,
            16 => 4,
            32 => 5,
            _ => 2
        };
        DenominatorComboBox.SelectedIndex = denomIndex;

        EditPopup.IsOpen = true;
    }

    private void AddTimeSignatureAtBar_Click(object sender, RoutedEventArgs e)
    {
        if (_timeSignatureTrack == null)
            return;

        var bar = BeatsToBar(_playbackPosition);
        var (numerator, denominator) = GetSelectedTimeSignature();

        if (TimeSignatureComboBox.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "custom")
        {
            _editingBar = bar;
            ShowEditPopup(true);
            return;
        }

        var timeSignature = new TimeSignature(numerator, denominator);
        _timeSignatureTrack.AddTimeSignatureChange(bar, timeSignature);
        TimeSignatureChangeRequested?.Invoke(this, new TimeSignatureChangeRequestedEventArgs(bar, timeSignature));
    }

    private void AddCommonTime_Click(object sender, RoutedEventArgs e)
    {
        AddTimeSignatureAtPosition(4, 4);
    }

    private void AddWaltzTime_Click(object sender, RoutedEventArgs e)
    {
        AddTimeSignatureAtPosition(3, 4);
    }

    private void AddCompoundTime_Click(object sender, RoutedEventArgs e)
    {
        AddTimeSignatureAtPosition(6, 8);
    }

    private void AddCustomTime_Click(object sender, RoutedEventArgs e)
    {
        var bar = BeatsToBar(_contextMenuPosition);
        _editingBar = bar;
        ShowEditPopup(true);
    }

    private void AddTimeSignatureAtPosition(int numerator, int denominator)
    {
        if (_timeSignatureTrack == null)
            return;

        var bar = BeatsToBar(_contextMenuPosition);
        var timeSignature = new TimeSignature(numerator, denominator);
        _timeSignatureTrack.AddTimeSignatureChange(bar, timeSignature);
        TimeSignatureChangeRequested?.Invoke(this, new TimeSignatureChangeRequestedEventArgs(bar, timeSignature));
    }

    private void EditTimeSignature_Click(object sender, RoutedEventArgs e)
    {
        var bar = BeatsToBar(_contextMenuPosition);
        _editingBar = bar;
        ShowEditPopup(false);
    }

    private void DeleteTimeSignature_Click(object sender, RoutedEventArgs e)
    {
        if (_timeSignatureTrack == null)
            return;

        var bar = BeatsToBar(_contextMenuPosition);

        if (bar == 0)
        {
            MessageBox.Show("Cannot delete the initial time signature at bar 1.",
                "Delete Time Signature", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Delete time signature change at bar {bar + 1}?",
            "Delete Time Signature",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _timeSignatureTrack.RemoveTimeSignatureChange(bar);
            TimeSignatureRemoved?.Invoke(this, bar);
        }
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e)
    {
        EditPopup.IsOpen = false;
        _editingBar = -1;
    }

    private void ApplyEdit_Click(object sender, RoutedEventArgs e)
    {
        if (_timeSignatureTrack == null || _editingBar < 0)
        {
            EditPopup.IsOpen = false;
            return;
        }

        if (!int.TryParse(NumeratorTextBox.Text, out int numerator) || numerator < 1 || numerator > 32)
        {
            MessageBox.Show("Please enter a valid numerator (1-32).",
                "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        int denominator = DenominatorComboBox.SelectedIndex switch
        {
            0 => 1,
            1 => 2,
            2 => 4,
            3 => 8,
            4 => 16,
            5 => 32,
            _ => 4
        };

        var timeSignature = new TimeSignature(numerator, denominator);
        _timeSignatureTrack.AddTimeSignatureChange(_editingBar, timeSignature);
        TimeSignatureChangeRequested?.Invoke(this, new TimeSignatureChangeRequestedEventArgs(_editingBar, timeSignature));

        EditPopup.IsOpen = false;
        _editingBar = -1;
    }

    #endregion

    /// <summary>
    /// Updates the scroll position and refreshes markers.
    /// </summary>
    /// <param name="scrollOffset">The new scroll offset in beats.</param>
    public void SetScrollOffset(double scrollOffset)
    {
        ScrollOffset = scrollOffset;
        RefreshMarkerPositions();
    }

    /// <summary>
    /// Updates the zoom level and refreshes markers.
    /// </summary>
    /// <param name="visibleBeats">The new number of visible beats.</param>
    public void SetVisibleBeats(double visibleBeats)
    {
        VisibleBeats = visibleBeats;
        RefreshMarkerPositions();
    }

    private void RefreshMarkerPositions()
    {
        foreach (var kvp in _markerElements)
        {
            PositionMarkerElement(kvp.Key, kvp.Value);
        }
    }
}

/// <summary>
/// Event arguments for time signature change requests.
/// </summary>
public class TimeSignatureChangeRequestedEventArgs : EventArgs
{
    /// <summary>Gets the bar number where the time signature should change.</summary>
    public int BarNumber { get; }

    /// <summary>Gets the requested time signature.</summary>
    public TimeSignature TimeSignature { get; }

    public TimeSignatureChangeRequestedEventArgs(int barNumber, TimeSignature timeSignature)
    {
        BarNumber = barNumber;
        TimeSignature = timeSignature;
    }
}
