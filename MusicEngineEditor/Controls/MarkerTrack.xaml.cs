using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using ColorConverter = System.Windows.Media.ColorConverter;
using MenuItem = System.Windows.Controls.MenuItem;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a marker type.
/// </summary>
public enum MarkerType
{
    /// <summary>
    /// Standard position marker.
    /// </summary>
    Standard,

    /// <summary>
    /// Locator marker for quick navigation.
    /// </summary>
    Locator,

    /// <summary>
    /// Cycle/loop start marker.
    /// </summary>
    CycleStart,

    /// <summary>
    /// Cycle/loop end marker.
    /// </summary>
    CycleEnd
}

/// <summary>
/// Represents a marker on the timeline.
/// </summary>
public partial class MarkerData : ObservableObject
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Gets or sets the marker label.
    /// </summary>
    [ObservableProperty]
    private string _label = "";

    /// <summary>
    /// Gets or sets the marker type.
    /// </summary>
    [ObservableProperty]
    private MarkerType _type = MarkerType.Standard;

    /// <summary>
    /// Gets or sets the marker color (hex).
    /// </summary>
    [ObservableProperty]
    private string _color = "#FFC107";

    /// <summary>
    /// Gets or sets whether the marker is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    public MarkerData()
    {
        Id = _nextId++;
    }

    public MarkerData(double position, string label, MarkerType type = MarkerType.Standard) : this()
    {
        Position = position;
        Label = label;
        Type = type;
        Color = type switch
        {
            MarkerType.Locator => "#4CAF50",
            MarkerType.CycleStart or MarkerType.CycleEnd => "#2196F3",
            _ => "#FFC107"
        };
    }
}

/// <summary>
/// Control for displaying timeline markers and grid.
/// </summary>
public partial class MarkerTrack : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty MarkersProperty =
        DependencyProperty.Register(nameof(Markers), typeof(ObservableCollection<MarkerData>), typeof(MarkerTrack),
            new PropertyMetadata(null, OnMarkersChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(50.0, OnRenderPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(0.0, OnRenderPropertyChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(0.0, OnPlayheadPositionChanged));

    public static readonly DependencyProperty CycleStartProperty =
        DependencyProperty.Register(nameof(CycleStart), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(-1.0, OnCycleChanged));

    public static readonly DependencyProperty CycleEndProperty =
        DependencyProperty.Register(nameof(CycleEnd), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(-1.0, OnCycleChanged));

    public static readonly DependencyProperty GridResolutionProperty =
        DependencyProperty.Register(nameof(GridResolution), typeof(double), typeof(MarkerTrack),
            new PropertyMetadata(1.0, OnRenderPropertyChanged));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool), typeof(MarkerTrack),
            new PropertyMetadata(true));

    public static readonly DependencyProperty BeatsPerBarProperty =
        DependencyProperty.Register(nameof(BeatsPerBar), typeof(int), typeof(MarkerTrack),
            new PropertyMetadata(4, OnRenderPropertyChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the collection of markers.
    /// </summary>
    public ObservableCollection<MarkerData>? Markers
    {
        get => (ObservableCollection<MarkerData>?)GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat (zoom level).
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll offset in beats.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the playhead position in beats.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the cycle start position (-1 for none).
    /// </summary>
    public double CycleStart
    {
        get => (double)GetValue(CycleStartProperty);
        set => SetValue(CycleStartProperty, value);
    }

    /// <summary>
    /// Gets or sets the cycle end position (-1 for none).
    /// </summary>
    public double CycleEnd
    {
        get => (double)GetValue(CycleEndProperty);
        set => SetValue(CycleEndProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid resolution in beats.
    /// </summary>
    public double GridResolution
    {
        get => (double)GetValue(GridResolutionProperty);
        set => SetValue(GridResolutionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to snap to grid.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets beats per bar (time signature numerator).
    /// </summary>
    public int BeatsPerBar
    {
        get => (int)GetValue(BeatsPerBarProperty);
        set => SetValue(BeatsPerBarProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a marker is added.
    /// </summary>
    public event EventHandler<MarkerData>? MarkerAdded;

    /// <summary>
    /// Event raised when a marker is removed.
    /// </summary>
    public event EventHandler<MarkerData>? MarkerRemoved;

    /// <summary>
    /// Event raised when a marker is moved.
    /// </summary>
    public event EventHandler<MarkerData>? MarkerMoved;

    /// <summary>
    /// Event raised when a marker is selected.
    /// </summary>
    public event EventHandler<MarkerData>? MarkerSelected;

    /// <summary>
    /// Event raised when the playhead position is requested to change.
    /// </summary>
    public event EventHandler<double>? PlayheadRequested;

    /// <summary>
    /// Event raised when the cycle region changes.
    /// </summary>
    public event EventHandler<CycleChangedEventArgs>? CycleChanged;

    #endregion

    #region Fields

    private bool _isDraggingMarker;
    private bool _isDraggingPlayhead;
    private MarkerData? _draggedMarker;
    private Point _dragStartPoint;
    private double _lastClickPosition;
    private bool _needsRender;

    #endregion

    public MarkerTrack()
    {
        InitializeComponent();
        Markers = [];
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InvalidateMarkers();
    }

    #region Property Changed Callbacks

    private static void OnMarkersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkerTrack control)
        {
            if (e.OldValue is ObservableCollection<MarkerData> oldCollection)
            {
                oldCollection.CollectionChanged -= control.OnMarkersCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<MarkerData> newCollection)
            {
                newCollection.CollectionChanged += control.OnMarkersCollectionChanged;
            }

            control.InvalidateMarkers();
        }
    }

    private void OnMarkersCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        InvalidateMarkers();
    }

    private static void OnRenderPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkerTrack control)
        {
            control.InvalidateMarkers();
        }
    }

    private static void OnPlayheadPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkerTrack control)
        {
            control.UpdatePlayhead();
        }
    }

    private static void OnCycleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkerTrack control)
        {
            control.UpdateCycleRegion();
        }
    }

    #endregion

    #region Rendering

    /// <summary>
    /// Invalidates and re-renders the markers.
    /// </summary>
    public void InvalidateMarkers()
    {
        if (_needsRender) return;
        _needsRender = true;

        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            _needsRender = false;
            RenderTimeline();
        });
    }

    private void RenderTimeline()
    {
        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        RenderGrid(width, height);
        RenderLabels(width, height);
        RenderMarkers(width, height);
        UpdatePlayhead();
        UpdateCycleRegion();
    }

    private void RenderGrid(double width, double height)
    {
        GridCanvas.Children.Clear();

        var startBeat = Math.Floor(ScrollOffset / GridResolution) * GridResolution;
        var endBeat = ScrollOffset + width / PixelsPerBeat;

        for (var beat = startBeat; beat <= endBeat; beat += GridResolution)
        {
            var x = (beat - ScrollOffset) * PixelsPerBeat;
            if (x < 0 || x > width) continue;

            var isBar = Math.Abs(beat % BeatsPerBar) < 0.001;
            var isBeat = Math.Abs(beat % 1) < 0.001;

            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = isBar ? 0 : (isBeat ? height * 0.5 : height * 0.7),
                Y2 = height,
                Stroke = isBar ? (SolidColorBrush)FindResource("BarGridBrush") :
                         isBeat ? (SolidColorBrush)FindResource("BeatGridBrush") :
                         (SolidColorBrush)FindResource("GridBrush"),
                StrokeThickness = isBar ? 1 : 0.5
            };

            GridCanvas.Children.Add(line);
        }
    }

    private void RenderLabels(double width, double height)
    {
        LabelsCanvas.Children.Clear();

        var startBeat = Math.Floor(ScrollOffset / BeatsPerBar) * BeatsPerBar;
        var endBeat = ScrollOffset + width / PixelsPerBeat;

        for (var beat = startBeat; beat <= endBeat; beat += BeatsPerBar)
        {
            var x = (beat - ScrollOffset) * PixelsPerBeat;
            if (x < -20 || x > width) continue;

            var barNumber = (int)(beat / BeatsPerBar) + 1;

            var label = new TextBlock
            {
                Text = barNumber.ToString(),
                FontSize = 10,
                Foreground = (SolidColorBrush)FindResource("SecondaryForegroundBrush")
            };

            Canvas.SetLeft(label, x + 2);
            Canvas.SetTop(label, 2);
            LabelsCanvas.Children.Add(label);
        }
    }

    private void RenderMarkers(double width, double height)
    {
        MarkersCanvas.Children.Clear();

        if (Markers == null) return;

        foreach (var marker in Markers)
        {
            var x = (marker.Position - ScrollOffset) * PixelsPerBeat;
            if (x < -50 || x > width + 50) continue;

            CreateMarkerVisual(marker, x, height);
        }
    }

    private void CreateMarkerVisual(MarkerData marker, double x, double height)
    {
        Color markerColor;
        try
        {
            markerColor = (Color)ColorConverter.ConvertFromString(marker.Color);
        }
        catch
        {
            markerColor = Color.FromRgb(0xFF, 0xC1, 0x07);
        }

        // Marker flag
        var flag = new Border
        {
            Background = new SolidColorBrush(markerColor),
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            Padding = new Thickness(4, 1, 4, 1),
            Cursor = Cursors.Hand,
            Tag = marker
        };

        var labelText = new TextBlock
        {
            Text = string.IsNullOrEmpty(marker.Label) ? $"M{marker.Id}" : marker.Label,
            FontSize = 9,
            Foreground = Brushes.Black,
            FontWeight = FontWeights.SemiBold
        };

        flag.Child = labelText;
        flag.MouseLeftButtonDown += MarkerFlag_MouseLeftButtonDown;
        flag.MouseLeftButtonUp += MarkerFlag_MouseLeftButtonUp;
        flag.MouseMove += MarkerFlag_MouseMove;

        // Selection highlight
        if (marker.IsSelected)
        {
            flag.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
            flag.BorderThickness = new Thickness(2);
        }

        Canvas.SetLeft(flag, x - 3);
        Canvas.SetTop(flag, 0);
        MarkersCanvas.Children.Add(flag);

        // Marker line
        var line = new Line
        {
            X1 = x,
            X2 = x,
            Y1 = flag.ActualHeight + 16,
            Y2 = height,
            Stroke = new SolidColorBrush(markerColor),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };

        MarkersCanvas.Children.Add(line);
    }

    private void UpdatePlayhead()
    {
        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            PlayheadIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var x = (PlayheadPosition - ScrollOffset) * PixelsPerBeat;

        if (x >= -6 && x <= width + 6)
        {
            Canvas.SetLeft(PlayheadIndicator, x - 3);
            Canvas.SetTop(PlayheadIndicator, height - 16);
            PlayheadIndicator.Visibility = Visibility.Visible;
        }
        else
        {
            PlayheadIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateCycleRegion()
    {
        var width = TimelineCanvas.ActualWidth;
        var height = TimelineCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || CycleStart < 0 || CycleEnd < 0 || CycleEnd <= CycleStart)
        {
            CycleRegion.Visibility = Visibility.Collapsed;
            return;
        }

        var startX = (CycleStart - ScrollOffset) * PixelsPerBeat;
        var endX = (CycleEnd - ScrollOffset) * PixelsPerBeat;

        startX = Math.Max(0, startX);
        endX = Math.Min(width, endX);

        if (endX > startX)
        {
            Canvas.SetLeft(CycleRegion, startX);
            Canvas.SetTop(CycleRegion, 0);
            CycleRegion.Width = endX - startX;
            CycleRegion.Height = height;
            CycleRegion.Visibility = Visibility.Visible;
        }
        else
        {
            CycleRegion.Visibility = Visibility.Collapsed;
        }
    }

    #endregion

    #region Mouse Interaction

    private void TimelineCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(TimelineCanvas);
        var beat = PixelToBeat(position.X);

        _lastClickPosition = beat;
        _isDraggingPlayhead = true;

        if (SnapToGrid)
        {
            beat = SnapToBeat(beat);
        }

        PlayheadRequested?.Invoke(this, beat);
        CaptureMouse();
        e.Handled = true;
    }

    private void TimelineCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
        ReleaseMouseCapture();
    }

    private void TimelineCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            var position = e.GetPosition(TimelineCanvas);
            var beat = PixelToBeat(position.X);

            if (SnapToGrid)
            {
                beat = SnapToBeat(beat);
            }

            PlayheadRequested?.Invoke(this, Math.Max(0, beat));
        }
    }

    private void TimelineCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(TimelineCanvas);
        _lastClickPosition = PixelToBeat(position.X);
    }

    private void MarkerFlag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border flag && flag.Tag is MarkerData marker)
        {
            // Deselect others
            if (Markers != null)
            {
                foreach (var m in Markers)
                {
                    m.IsSelected = false;
                }
            }

            marker.IsSelected = true;
            _isDraggingMarker = true;
            _draggedMarker = marker;
            _dragStartPoint = e.GetPosition(TimelineCanvas);

            MarkerSelected?.Invoke(this, marker);
            flag.CaptureMouse();
            e.Handled = true;
        }
    }

    private void MarkerFlag_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingMarker && _draggedMarker != null)
        {
            MarkerMoved?.Invoke(this, _draggedMarker);
        }

        _isDraggingMarker = false;
        _draggedMarker = null;

        if (sender is Border flag)
        {
            flag.ReleaseMouseCapture();
        }
    }

    private void MarkerFlag_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingMarker || _draggedMarker == null) return;

        var position = e.GetPosition(TimelineCanvas);
        var beat = PixelToBeat(position.X);

        if (SnapToGrid)
        {
            beat = SnapToBeat(beat);
        }

        _draggedMarker.Position = Math.Max(0, beat);
        InvalidateMarkers();
    }

    private void TimelineCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateMarkers();
    }

    #endregion

    #region Context Menu Handlers

    private void AddMarker_Click(object sender, RoutedEventArgs e)
    {
        var position = SnapToGrid ? SnapToBeat(_lastClickPosition) : _lastClickPosition;
        var marker = new MarkerData(position, "", MarkerType.Standard);

        Markers?.Add(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    private void SetCycleStart_Click(object sender, RoutedEventArgs e)
    {
        var position = SnapToGrid ? SnapToBeat(_lastClickPosition) : _lastClickPosition;
        CycleStart = position;
        CycleChanged?.Invoke(this, new CycleChangedEventArgs(CycleStart, CycleEnd));
    }

    private void SetCycleEnd_Click(object sender, RoutedEventArgs e)
    {
        var position = SnapToGrid ? SnapToBeat(_lastClickPosition) : _lastClickPosition;
        CycleEnd = position;
        CycleChanged?.Invoke(this, new CycleChangedEventArgs(CycleStart, CycleEnd));
    }

    private void ClearCycle_Click(object sender, RoutedEventArgs e)
    {
        CycleStart = -1;
        CycleEnd = -1;
        CycleChanged?.Invoke(this, new CycleChangedEventArgs(-1, -1));
    }

    private void SetGridResolution_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var resolution))
            {
                GridResolution = resolution;
            }
        }
    }

    #endregion

    #region Helper Methods

    private double PixelToBeat(double x)
    {
        return ScrollOffset + x / PixelsPerBeat;
    }

    private double BeatToPixel(double beat)
    {
        return (beat - ScrollOffset) * PixelsPerBeat;
    }

    private double SnapToBeat(double beat)
    {
        return Math.Round(beat / GridResolution) * GridResolution;
    }

    /// <summary>
    /// Adds a marker at the specified position.
    /// </summary>
    public void AddMarkerAt(double position, string label = "", MarkerType type = MarkerType.Standard)
    {
        if (SnapToGrid)
        {
            position = SnapToBeat(position);
        }

        var marker = new MarkerData(position, label, type);
        Markers?.Add(marker);
        MarkerAdded?.Invoke(this, marker);
    }

    /// <summary>
    /// Removes the specified marker.
    /// </summary>
    public void RemoveMarker(MarkerData marker)
    {
        if (Markers?.Remove(marker) == true)
        {
            MarkerRemoved?.Invoke(this, marker);
        }
    }

    /// <summary>
    /// Removes the selected marker.
    /// </summary>
    public void RemoveSelectedMarker()
    {
        if (Markers == null) return;

        var selected = Markers.FirstOrDefault(m => m.IsSelected);
        if (selected != null)
        {
            RemoveMarker(selected);
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for cycle region changes.
/// </summary>
public class CycleChangedEventArgs : EventArgs
{
    public double Start { get; }
    public double End { get; }
    public bool HasCycle => Start >= 0 && End >= 0 && End > Start;

    public CycleChangedEventArgs(double start, double end)
    {
        Start = start;
        End = end;
    }
}
