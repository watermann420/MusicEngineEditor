using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Timeline editor for warp markers with drag support and grid overlay.
/// </summary>
public partial class WarpMarkerEditor : UserControl
{
    private readonly List<WarpMarker> _markers = [];
    private readonly Dictionary<WarpMarker, Border> _markerVisuals = new();

    private WarpMarker? _selectedMarker;
    private WarpMarker? _draggingMarker;
    private Point _dragStartPoint;
    private double _dragStartTime;

    private double _totalDuration = 10.0; // Default 10 seconds
    private double _bpm = 120.0;
    private bool _isDragging;
    private double _playheadPosition;

    /// <summary>
    /// Event raised when a marker is added.
    /// </summary>
    public event EventHandler<WarpMarker>? MarkerAdded;

    /// <summary>
    /// Event raised when a marker is deleted.
    /// </summary>
    public event EventHandler<WarpMarker>? MarkerDeleted;

    /// <summary>
    /// Event raised when a marker is moved.
    /// </summary>
    public event EventHandler<WarpMarker>? MarkerMoved;

    /// <summary>
    /// Event raised when markers are cleared.
    /// </summary>
    public event EventHandler? MarkersCleared;

    /// <summary>
    /// Event raised when auto-detect is requested.
    /// </summary>
    public event EventHandler? AutoDetectRequested;

    /// <summary>
    /// Gets or sets the total duration in seconds.
    /// </summary>
    public double TotalDuration
    {
        get => _totalDuration;
        set
        {
            _totalDuration = Math.Max(0.1, value);
            RedrawAll();
        }
    }

    /// <summary>
    /// Gets or sets the BPM for grid display.
    /// </summary>
    public double Bpm
    {
        get => _bpm;
        set
        {
            _bpm = Math.Clamp(value, 20, 300);
            TempoText.Text = $"BPM: {_bpm:F1}";
            if (ShowGridCheckBox.IsChecked == true)
            {
                DrawGrid();
            }
        }
    }

    /// <summary>
    /// Gets or sets the playhead position in seconds.
    /// </summary>
    public double PlayheadPosition
    {
        get => _playheadPosition;
        set
        {
            _playheadPosition = value;
            UpdatePlayhead();
        }
    }

    /// <summary>
    /// Gets the current markers.
    /// </summary>
    public IReadOnlyList<WarpMarker> Markers => _markers.AsReadOnly();

    /// <summary>
    /// Gets or sets whether snap to transients is enabled.
    /// </summary>
    public bool SnapToTransients
    {
        get => SnapToTransientsCheckBox.IsChecked == true;
        set => SnapToTransientsCheckBox.IsChecked = value;
    }

    public WarpMarkerEditor()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RedrawAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RedrawAll();
    }

    /// <summary>
    /// Sets the markers from an external source.
    /// </summary>
    public void SetMarkers(IEnumerable<WarpMarker> markers)
    {
        _markers.Clear();
        _markers.AddRange(markers.OrderBy(m => m.TimePosition));
        RedrawMarkers();
        UpdateMarkerCount();
    }

    /// <summary>
    /// Adds a single marker.
    /// </summary>
    public void AddMarker(WarpMarker marker)
    {
        _markers.Add(marker);
        _markers.Sort((a, b) => a.TimePosition.CompareTo(b.TimePosition));
        RedrawMarkers();
        UpdateMarkerCount();
        MarkerAdded?.Invoke(this, marker);
    }

    /// <summary>
    /// Removes a marker.
    /// </summary>
    public void RemoveMarker(WarpMarker marker)
    {
        if (_markers.Remove(marker))
        {
            if (_selectedMarker == marker)
            {
                _selectedMarker = null;
                DeleteMarkerButton.IsEnabled = false;
            }
            RedrawMarkers();
            UpdateMarkerCount();
            MarkerDeleted?.Invoke(this, marker);
        }
    }

    /// <summary>
    /// Clears all markers.
    /// </summary>
    public void ClearMarkers()
    {
        _markers.Clear();
        _selectedMarker = null;
        DeleteMarkerButton.IsEnabled = false;
        RedrawMarkers();
        UpdateMarkerCount();
        MarkersCleared?.Invoke(this, EventArgs.Empty);
    }

    private void RedrawAll()
    {
        DrawGrid();
        RedrawMarkers();
        UpdatePlayhead();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        if (ShowGridCheckBox.IsChecked != true || _bpm <= 0 || _totalDuration <= 0)
            return;

        double canvasWidth = MarkerCanvas.ActualWidth > 0 ? MarkerCanvas.ActualWidth : ActualWidth - 16;
        double canvasHeight = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 150;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;

        double beatDuration = 60.0 / _bpm;
        int totalBeats = (int)Math.Ceiling(_totalDuration / beatDuration);

        var beatLineBrush = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
        var barLineBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));

        for (int beat = 0; beat <= totalBeats; beat++)
        {
            double time = beat * beatDuration;
            double x = (time / _totalDuration) * canvasWidth;
            bool isBarLine = beat % 4 == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = canvasHeight,
                Stroke = isBarLine ? barLineBrush : beatLineBrush,
                StrokeThickness = isBarLine ? 1 : 0.5
            };

            GridCanvas.Children.Add(line);

            // Add bar numbers
            if (isBarLine && beat > 0)
            {
                int barNumber = beat / 4 + 1;
                var label = new TextBlock
                {
                    Text = barNumber.ToString(),
                    FontSize = 9,
                    Foreground = new SolidColorBrush(Color.FromArgb(128, 188, 190, 196))
                };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 2);
                GridCanvas.Children.Add(label);
            }
        }
    }

    private void RedrawMarkers()
    {
        MarkerCanvas.Children.Clear();
        _markerVisuals.Clear();

        double canvasWidth = MarkerCanvas.ActualWidth > 0 ? MarkerCanvas.ActualWidth : ActualWidth - 16;
        double canvasHeight = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 150;

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return;

        foreach (var marker in _markers)
        {
            double x = (marker.TimePosition / _totalDuration) * canvasWidth;

            // Marker handle
            var handle = new Border
            {
                Width = 8,
                Height = canvasHeight * 0.6,
                Background = GetMarkerBrush(marker),
                CornerRadius = new CornerRadius(2, 2, 0, 0),
                Cursor = Cursors.SizeWE,
                Tag = marker
            };

            if (marker == _selectedMarker)
            {
                handle.BorderBrush = Brushes.White;
                handle.BorderThickness = new Thickness(1);
            }

            // Marker line
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = canvasHeight * 0.4,
                X2 = x,
                Y2 = canvasHeight,
                Stroke = GetMarkerBrush(marker),
                StrokeThickness = 1,
                StrokeDashArray = marker.IsManual ? null : new DoubleCollection { 2, 2 }
            };

            Canvas.SetLeft(handle, x - 4);
            Canvas.SetTop(handle, 0);

            MarkerCanvas.Children.Add(line);
            MarkerCanvas.Children.Add(handle);
            _markerVisuals[marker] = handle;
        }
    }

    private Brush GetMarkerBrush(WarpMarker marker)
    {
        if (marker.IsManual)
            return FindResource("WarningBrush") as Brush ?? Brushes.Orange;
        if (marker.IsDownbeat)
            return FindResource("SuccessBrush") as Brush ?? Brushes.Green;
        return FindResource("AccentBrush") as Brush ?? Brushes.Blue;
    }

    private void UpdatePlayhead()
    {
        double canvasWidth = MarkerCanvas.ActualWidth > 0 ? MarkerCanvas.ActualWidth : ActualWidth - 16;
        double canvasHeight = MarkerCanvas.ActualHeight > 0 ? MarkerCanvas.ActualHeight : 150;

        if (canvasWidth <= 0)
            return;

        double x = (_playheadPosition / _totalDuration) * canvasWidth;

        PlayheadLine.X1 = x;
        PlayheadLine.X2 = x;
        PlayheadLine.Y2 = canvasHeight;
        PlayheadLine.Visibility = Visibility.Visible;

        PositionText.Text = $"Position: {_playheadPosition:F3}s";
    }

    private void UpdateMarkerCount()
    {
        MarkerCountText.Text = $"{_markers.Count} markers";
    }

    private WarpMarker? GetMarkerAtPoint(Point point)
    {
        double canvasWidth = MarkerCanvas.ActualWidth;
        if (canvasWidth <= 0)
            return null;

        double timeAtPoint = (point.X / canvasWidth) * _totalDuration;
        double tolerance = (_totalDuration / canvasWidth) * 10; // 10 pixels tolerance

        foreach (var marker in _markers)
        {
            if (Math.Abs(marker.TimePosition - timeAtPoint) <= tolerance)
            {
                return marker;
            }
        }

        return null;
    }

    private void MarkerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var point = e.GetPosition(MarkerCanvas);
        var marker = GetMarkerAtPoint(point);

        if (marker != null)
        {
            // Select and start dragging
            _selectedMarker = marker;
            _draggingMarker = marker;
            _dragStartPoint = point;
            _dragStartTime = marker.TimePosition;
            _isDragging = true;
            DeleteMarkerButton.IsEnabled = true;
            MarkerCanvas.CaptureMouse();
            StatusText.Text = $"Selected: {(marker.IsManual ? "Manual" : "Auto")} marker at {marker.TimePosition:F3}s";
        }
        else
        {
            // Deselect
            _selectedMarker = null;
            DeleteMarkerButton.IsEnabled = false;
            StatusText.Text = "Ready";
        }

        RedrawMarkers();
    }

    private void MarkerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(MarkerCanvas);
        double canvasWidth = MarkerCanvas.ActualWidth;

        if (_isDragging && _draggingMarker != null && canvasWidth > 0)
        {
            double deltaX = point.X - _dragStartPoint.X;
            double deltaTime = (deltaX / canvasWidth) * _totalDuration;
            double newTime = Math.Clamp(_dragStartTime + deltaTime, 0, _totalDuration);

            _draggingMarker.TimePosition = newTime;
            _draggingMarker.IsManual = true;

            RedrawMarkers();
            StatusText.Text = $"Moving to {newTime:F3}s";
        }
        else
        {
            // Update position display
            double time = (point.X / canvasWidth) * _totalDuration;
            PositionText.Text = $"Position: {time:F3}s";
        }
    }

    private void MarkerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging && _draggingMarker != null)
        {
            MarkerMoved?.Invoke(this, _draggingMarker);
            StatusText.Text = "Marker moved";
        }

        _isDragging = false;
        _draggingMarker = null;
        MarkerCanvas.ReleaseMouseCapture();
    }

    private void AddMarkerButton_Click(object sender, RoutedEventArgs e)
    {
        var newMarker = new WarpMarker
        {
            TimePosition = _playheadPosition,
            BeatPosition = _playheadPosition * (_bpm / 60.0),
            IsManual = true,
            IsDownbeat = false
        };

        AddMarker(newMarker);
        StatusText.Text = $"Added marker at {_playheadPosition:F3}s";
    }

    private void DeleteMarkerButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedMarker != null)
        {
            RemoveMarker(_selectedMarker);
            StatusText.Text = "Marker deleted";
        }
    }

    private void AutoDetectButton_Click(object sender, RoutedEventArgs e)
    {
        AutoDetectRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearAllButton_Click(object sender, RoutedEventArgs e)
    {
        if (_markers.Count > 0)
        {
            ClearMarkers();
            StatusText.Text = "All markers cleared";
        }
    }

    private void ShowGridCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        DrawGrid();
    }
}
