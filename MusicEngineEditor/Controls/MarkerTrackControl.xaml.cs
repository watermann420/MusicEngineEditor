using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;
using ColorConverter = System.Windows.Media.ColorConverter;
using Rectangle = System.Windows.Shapes.Rectangle;
using CoreMarkerTrack = MusicEngine.Core.MarkerTrack;
using CoreMarkerType = MusicEngine.Core.MarkerType;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying and editing markers on a timeline.
/// </summary>
public partial class MarkerTrackControl : UserControl
{
    private CoreMarkerTrack? _markerTrack;
    private readonly Dictionary<Guid, UIElement> _markerElements = [];
    private Marker? _selectedMarker;
    private Marker? _draggingMarker;
    private Point _dragStartPoint;
    private double _dragStartPosition;
    private bool _isDragging;
    private double _contextMenuPosition;

    /// <summary>
    /// Gets or sets the marker track to display.
    /// </summary>
    public CoreMarkerTrack? MarkerTrack
    {
        get => _markerTrack;
        set
        {
            if (_markerTrack != null)
            {
                _markerTrack.MarkersChanged -= OnMarkersChanged;
            }

            _markerTrack = value;

            if (_markerTrack != null)
            {
                _markerTrack.MarkersChanged += OnMarkersChanged;
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
            UpdatePlayhead();
        }
    }
    private double _playbackPosition;

    /// <summary>
    /// Gets the number of markers.
    /// </summary>
    public int MarkerCount => _markerTrack?.Count ?? 0;

    /// <summary>
    /// Event raised when a marker is selected.
    /// </summary>
    public event EventHandler<Marker>? MarkerSelected;

    /// <summary>
    /// Event raised when a jump to position is requested.
    /// </summary>
    public event EventHandler<double>? JumpRequested;

    /// <summary>
    /// Event raised when a marker is added.
    /// </summary>
    public event EventHandler<Marker>? MarkerAdded;

    /// <summary>
    /// Event raised when a marker is removed.
    /// </summary>
    public event EventHandler<Marker>? MarkerRemoved;

    public MarkerTrackControl()
    {
        InitializeComponent();
        DataContext = this;
        SizeChanged += (_, _) => RefreshMarkers();
    }

    private void OnMarkersChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshMarkers);
    }

    /// <summary>
    /// Refreshes the visual display of all markers.
    /// </summary>
    public void RefreshMarkers()
    {
        // Clear existing marker elements
        foreach (var element in _markerElements.Values)
        {
            MarkerCanvas.Children.Remove(element);
        }
        _markerElements.Clear();

        if (_markerTrack == null)
            return;

        // Add marker elements
        foreach (var marker in _markerTrack.Markers)
        {
            var element = CreateMarkerElement(marker);
            _markerElements[marker.Id] = element;
            MarkerCanvas.Children.Add(element);
            PositionMarkerElement(marker, element);
        }

        UpdatePlayhead();
    }

    private UIElement CreateMarkerElement(Marker marker)
    {
        var color = ParseColor(marker.Color);
        var brush = new SolidColorBrush(color);

        var container = new Canvas
        {
            Tag = marker,
            Cursor = Cursors.Hand
        };

        // Create the flag
        var flag = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = marker.Name,
                FontSize = 10,
                Foreground = Brushes.White,
                FontWeight = FontWeights.Medium
            }
        };

        // Create the line
        var line = new Rectangle
        {
            Width = 2,
            Fill = brush,
            Height = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 40
        };

        // Position elements
        Canvas.SetTop(flag, 0);
        Canvas.SetLeft(flag, -1);
        Canvas.SetTop(line, flag.ActualHeight > 0 ? flag.ActualHeight : 18);
        Canvas.SetLeft(line, 0);

        container.Children.Add(line);
        container.Children.Add(flag);

        // Event handlers
        container.MouseLeftButtonDown += MarkerElement_MouseLeftButtonDown;
        container.MouseEnter += MarkerElement_MouseEnter;
        container.MouseLeave += MarkerElement_MouseLeave;

        // For loop markers, add end marker
        if (marker.Type == CoreMarkerType.Loop && marker.EndPosition.HasValue)
        {
            var endLine = new Rectangle
            {
                Width = 2,
                Fill = brush,
                Height = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 40,
                Tag = "EndMarker"
            };

            var loopRect = new Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(40, color.R, color.G, color.B)),
                Height = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 40,
                Tag = "LoopRegion"
            };

            container.Children.Insert(0, loopRect);
            container.Children.Add(endLine);
        }

        return container;
    }

    private void PositionMarkerElement(Marker marker, UIElement element)
    {
        if (element is not Canvas container)
            return;

        var canvasWidth = MarkerCanvas.ActualWidth;
        if (canvasWidth <= 0 || VisibleBeats <= 0)
            return;

        var pixelsPerBeat = canvasWidth / VisibleBeats;
        var x = (marker.Position - ScrollOffset) * pixelsPerBeat;

        Canvas.SetLeft(container, x);

        // Update line height
        foreach (var child in container.Children)
        {
            if (child is Rectangle rect && rect.Tag?.ToString() != "LoopRegion")
            {
                rect.Height = MarkerCanvas.ActualHeight;
            }
        }

        // Position loop region and end marker
        if (marker.Type == CoreMarkerType.Loop && marker.EndPosition.HasValue)
        {
            var endX = (marker.EndPosition.Value - ScrollOffset) * pixelsPerBeat;
            var width = endX - x;

            foreach (var child in container.Children)
            {
                if (child is Rectangle rect)
                {
                    if (rect.Tag?.ToString() == "LoopRegion")
                    {
                        rect.Width = Math.Max(0, width);
                        rect.Height = MarkerCanvas.ActualHeight;
                    }
                    else if (rect.Tag?.ToString() == "EndMarker")
                    {
                        Canvas.SetLeft(rect, width);
                        rect.Height = MarkerCanvas.ActualHeight;
                    }
                }
            }
        }
    }

    private void UpdatePlayhead()
    {
        if (MarkerCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return;

        var pixelsPerBeat = MarkerCanvas.ActualWidth / VisibleBeats;
        var x = (_playbackPosition - ScrollOffset) * pixelsPerBeat;

        if (x >= 0 && x <= MarkerCanvas.ActualWidth)
        {
            Playhead.Visibility = Visibility.Visible;
            Canvas.SetLeft(Playhead, x);
            Playhead.Height = MarkerCanvas.ActualHeight;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private double PositionToBeats(double x)
    {
        if (MarkerCanvas.ActualWidth <= 0 || VisibleBeats <= 0)
            return 0;

        var pixelsPerBeat = MarkerCanvas.ActualWidth / VisibleBeats;
        return (x / pixelsPerBeat) + ScrollOffset;
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.Orange;
        }
    }

    #region Event Handlers

    private void MarkerElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Canvas container && container.Tag is Marker marker)
        {
            _selectedMarker = marker;
            _draggingMarker = marker;
            _dragStartPoint = e.GetPosition(MarkerCanvas);
            _dragStartPosition = marker.Position;
            _isDragging = false;

            MarkerSelected?.Invoke(this, marker);
            container.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MarkerElement_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Canvas container)
        {
            container.Opacity = 0.8;
        }
    }

    private void MarkerElement_MouseLeave(object sender, MouseEventArgs e)
    {
        if (sender is Canvas container)
        {
            container.Opacity = 1.0;
        }
    }

    private void MarkerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Double-click to add marker
        if (e.ClickCount == 2)
        {
            var position = PositionToBeats(e.GetPosition(MarkerCanvas).X);
            AddMarkerAtBeats(position);
        }
    }

    private void MarkerCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        _contextMenuPosition = PositionToBeats(e.GetPosition(MarkerCanvas).X);

        // Check if clicking on a marker
        var hitMarker = GetMarkerAtPosition(e.GetPosition(MarkerCanvas));
        _selectedMarker = hitMarker;

        // Enable/disable context menu items based on selection
        EditMarkerMenuItem.IsEnabled = hitMarker != null;
        DeleteMarkerMenuItem.IsEnabled = hitMarker != null && !hitMarker.IsLocked;
        JumpToMarkerMenuItem.IsEnabled = hitMarker != null;
        LockMarkerMenuItem.IsEnabled = hitMarker != null;
        LockMarkerMenuItem.Header = hitMarker?.IsLocked == true ? "Unlock Marker" : "Lock Marker";
    }

    private void MarkerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingMarker != null && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPoint = e.GetPosition(MarkerCanvas);
            var delta = currentPoint.X - _dragStartPoint.X;

            if (!_isDragging && Math.Abs(delta) > 5)
            {
                _isDragging = true;
            }

            if (_isDragging && !_draggingMarker.IsLocked)
            {
                var newPosition = PositionToBeats(currentPoint.X);
                newPosition = Math.Max(0, newPosition);

                _markerTrack?.MoveMarker(_draggingMarker, newPosition);
            }
        }
    }

    private void MarkerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingMarker != null)
        {
            if (!_isDragging)
            {
                // It was a click, not a drag - jump to marker
                JumpRequested?.Invoke(this, _draggingMarker.Position);
            }

            // Find and release the container
            if (_markerElements.TryGetValue(_draggingMarker.Id, out var element) && element is Canvas container)
            {
                container.ReleaseMouseCapture();
            }

            _draggingMarker = null;
            _isDragging = false;
        }
    }

    private Marker? GetMarkerAtPosition(Point point)
    {
        var beats = PositionToBeats(point.X);
        return _markerTrack?.GetClosestMarker(beats);
    }

    private void AddMarkerAtBeats(double position)
    {
        if (_markerTrack == null)
            return;

        var typeItem = MarkerTypeComboBox.SelectedItem as ComboBoxItem;
        var typeString = typeItem?.Tag?.ToString() ?? "Cue";
        var type = typeString switch
        {
            "Loop" => CoreMarkerType.Loop,
            "Section" => CoreMarkerType.Section,
            _ => CoreMarkerType.Cue
        };

        var marker = new Marker(position, $"{type} {_markerTrack.Count + 1}", type);

        if (type == CoreMarkerType.Loop)
        {
            marker.EndPosition = position + 4; // Default 4 beats loop
        }

        _markerTrack.AddMarker(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    private void AddMarkerAtPosition_Click(object sender, RoutedEventArgs e)
    {
        AddMarkerAtBeats(_playbackPosition);
    }

    private void AddMarkerAtCursor_Click(object sender, RoutedEventArgs e)
    {
        // This would need to be connected to actual cursor position from parent
        AddMarkerAtBeats(_playbackPosition);
    }

    private void PreviousMarker_Click(object sender, RoutedEventArgs e)
    {
        _markerTrack?.JumpToPreviousMarker(_playbackPosition);
    }

    private void NextMarker_Click(object sender, RoutedEventArgs e)
    {
        _markerTrack?.JumpToNextMarker(_playbackPosition);
    }

    private void AddCueMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_markerTrack == null) return;

        var marker = Marker.CreateCue(_contextMenuPosition, $"Cue {_markerTrack.Count + 1}");
        _markerTrack.AddMarker(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    private void AddLoopMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_markerTrack == null) return;

        var marker = Marker.CreateLoop(_contextMenuPosition, _contextMenuPosition + 4, $"Loop {_markerTrack.Count + 1}");
        _markerTrack.AddMarker(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    private void AddSectionMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_markerTrack == null) return;

        var marker = Marker.CreateSection(_contextMenuPosition, $"Section {_markerTrack.Count + 1}");
        _markerTrack.AddMarker(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    private void EditMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker == null) return;

        // In a full implementation, this would open an edit dialog
        // For now, we'll just show a message
        MessageBox.Show(
            $"Edit marker: {_selectedMarker.Name}\n" +
            $"Position: {_selectedMarker.Position:F2} beats\n" +
            $"Type: {_selectedMarker.Type}\n" +
            $"Color: {_selectedMarker.Color}",
            "Edit Marker",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void DeleteMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker == null || _markerTrack == null) return;

        if (_selectedMarker.IsLocked)
        {
            MessageBox.Show("Cannot delete a locked marker.", "Marker Locked",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Delete marker '{_selectedMarker.Name}'?",
            "Delete Marker",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            var marker = _selectedMarker;
            _markerTrack.RemoveMarker(marker);
            MarkerRemoved?.Invoke(this, marker);
            _selectedMarker = null;
        }
    }

    private void JumpToMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker != null)
        {
            JumpRequested?.Invoke(this, _selectedMarker.Position);
        }
    }

    private void LockMarker_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker != null)
        {
            _selectedMarker.IsLocked = !_selectedMarker.IsLocked;
            _selectedMarker.Touch();
        }
    }

    #endregion
}
