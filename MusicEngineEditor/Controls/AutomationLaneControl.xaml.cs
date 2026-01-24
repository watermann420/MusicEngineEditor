using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core.Automation;
using MusicEngineEditor.ViewModels;
using MenuItem = System.Windows.Controls.MenuItem;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A control for editing automation curves with point manipulation.
/// </summary>
public partial class AutomationLaneControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty LaneProperty =
        DependencyProperty.Register(nameof(Lane), typeof(AutomationLane), typeof(AutomationLaneControl),
            new PropertyMetadata(null, OnLaneChanged));

    public static readonly DependencyProperty CurrentTimeProperty =
        DependencyProperty.Register(nameof(CurrentTime), typeof(double), typeof(AutomationLaneControl),
            new PropertyMetadata(0.0, OnCurrentTimeChanged));

    public static readonly DependencyProperty TimeScaleProperty =
        DependencyProperty.Register(nameof(TimeScale), typeof(double), typeof(AutomationLaneControl),
            new PropertyMetadata(50.0, OnTimeScaleChanged));

    public static readonly DependencyProperty TimeOffsetProperty =
        DependencyProperty.Register(nameof(TimeOffset), typeof(double), typeof(AutomationLaneControl),
            new PropertyMetadata(0.0, OnTimeOffsetChanged));

    public static readonly DependencyProperty ShowPlayheadProperty =
        DependencyProperty.Register(nameof(ShowPlayhead), typeof(bool), typeof(AutomationLaneControl),
            new PropertyMetadata(true, OnShowPlayheadChanged));

    public static readonly DependencyProperty SnapToGridProperty =
        DependencyProperty.Register(nameof(SnapToGrid), typeof(bool), typeof(AutomationLaneControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty GridSubdivisionProperty =
        DependencyProperty.Register(nameof(GridSubdivision), typeof(double), typeof(AutomationLaneControl),
            new PropertyMetadata(0.25, OnGridSubdivisionChanged));

    public static readonly DependencyProperty IsCollapsedProperty =
        DependencyProperty.Register(nameof(IsCollapsed), typeof(bool), typeof(AutomationLaneControl),
            new PropertyMetadata(false, OnIsCollapsedChanged));

    public static readonly DependencyProperty AvailableParametersProperty =
        DependencyProperty.Register(nameof(AvailableParameters), typeof(ObservableCollection<AutomationParameterInfo>), typeof(AutomationLaneControl),
            new PropertyMetadata(null, OnAvailableParametersChanged));

    /// <summary>
    /// Gets or sets the automation lane being edited.
    /// </summary>
    public AutomationLane? Lane
    {
        get => (AutomationLane?)GetValue(LaneProperty);
        set => SetValue(LaneProperty, value);
    }

    /// <summary>
    /// Gets or sets the current playback time for the playhead.
    /// </summary>
    public double CurrentTime
    {
        get => (double)GetValue(CurrentTimeProperty);
        set => SetValue(CurrentTimeProperty, value);
    }

    /// <summary>
    /// Gets or sets the time scale (pixels per beat).
    /// </summary>
    public double TimeScale
    {
        get => (double)GetValue(TimeScaleProperty);
        set => SetValue(TimeScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets the time offset for scrolling.
    /// </summary>
    public double TimeOffset
    {
        get => (double)GetValue(TimeOffsetProperty);
        set => SetValue(TimeOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the playhead.
    /// </summary>
    public bool ShowPlayhead
    {
        get => (bool)GetValue(ShowPlayheadProperty);
        set => SetValue(ShowPlayheadProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to snap points to the grid.
    /// </summary>
    public bool SnapToGrid
    {
        get => (bool)GetValue(SnapToGridProperty);
        set => SetValue(SnapToGridProperty, value);
    }

    /// <summary>
    /// Gets or sets the grid subdivision (fraction of a beat).
    /// </summary>
    public double GridSubdivision
    {
        get => (double)GetValue(GridSubdivisionProperty);
        set => SetValue(GridSubdivisionProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the lane is collapsed (header only).
    /// </summary>
    public bool IsCollapsed
    {
        get => (bool)GetValue(IsCollapsedProperty);
        set => SetValue(IsCollapsedProperty, value);
    }

    /// <summary>
    /// Gets or sets the available parameters for selection.
    /// </summary>
    public ObservableCollection<AutomationParameterInfo>? AvailableParameters
    {
        get => (ObservableCollection<AutomationParameterInfo>?)GetValue(AvailableParametersProperty);
        set => SetValue(AvailableParametersProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when a point is added.
    /// </summary>
    public event EventHandler<AutomationPoint>? PointAdded;

    /// <summary>
    /// Fired when a point is removed.
    /// </summary>
    public event EventHandler<AutomationPoint>? PointRemoved;

    /// <summary>
    /// Fired when points are modified.
    /// </summary>
    public event EventHandler? PointsModified;

    /// <summary>
    /// Fired when lane settings change (mute, solo, arm).
    /// </summary>
    public event EventHandler? LaneSettingsChanged;

    /// <summary>
    /// Fired when the selected parameter changes.
    /// </summary>
    public event EventHandler<AutomationParameterInfo?>? ParameterChanged;

    /// <summary>
    /// Fired when visibility is toggled.
    /// </summary>
    public event EventHandler<bool>? VisibilityToggled;

    #endregion

    #region Private Fields

    private const double PointRadius = 5.0;
    private const double PointHitRadius = 8.0;

    private readonly List<AutomationPoint> _selectedPoints = [];
    private readonly Dictionary<AutomationPoint, Ellipse> _pointVisuals = [];

    private bool _isDragging;
    private bool _isSelecting;
    private Point _dragStart;
    private Point _lastMousePos;
    private AutomationPoint? _draggedPoint;
    private AutomationCurveType _defaultCurveType = AutomationCurveType.Linear;

    private readonly SolidColorBrush _curveColor = new(Color.FromRgb(0x6A, 0xAB, 0x73));
    private readonly SolidColorBrush _pointColor = new(Color.FromRgb(0xE8, 0xE8, 0xE8));
    private readonly SolidColorBrush _pointSelectedColor = new(Color.FromRgb(0xFF, 0x9B, 0x4B));
    private readonly SolidColorBrush _gridLineColor = new(Color.FromRgb(0x2B, 0x2D, 0x30));
    private readonly SolidColorBrush _gridLineStrongColor = new(Color.FromRgb(0x39, 0x3B, 0x40));

    #endregion

    public AutomationLaneControl()
    {
        InitializeComponent();
    }

    #region Property Changed Handlers

    private static void OnLaneChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.OnLaneChanged(e.OldValue as AutomationLane, e.NewValue as AutomationLane);
        }
    }

    private void OnLaneChanged(AutomationLane? oldLane, AutomationLane? newLane)
    {
        if (oldLane != null)
        {
            oldLane.Curve.CurveChanged -= OnCurveChanged;
            oldLane.LaneChanged -= OnLaneConfigChanged;
        }

        if (newLane != null)
        {
            newLane.Curve.CurveChanged += OnCurveChanged;
            newLane.LaneChanged += OnLaneConfigChanged;
        }

        UpdateLaneHeader();
        Redraw();
    }

    private void OnCurveChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(Redraw);
    }

    private void OnLaneConfigChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(UpdateLaneHeader);
    }

    private static void OnCurrentTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.UpdatePlayhead();
            control.UpdateCurrentValueDisplay();
        }
    }

    private static void OnTimeScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.Redraw();
        }
    }

    private static void OnTimeOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.Redraw();
        }
    }

    private static void OnShowPlayheadChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.UpdatePlayhead();
        }
    }

    private static void OnGridSubdivisionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.DrawGrid();
        }
    }

    private static void OnIsCollapsedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.UpdateCollapsedState();
        }
    }

    private static void OnAvailableParametersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AutomationLaneControl control)
        {
            control.UpdateParameterSelector();
        }
    }

    #endregion

    #region UI Event Handlers

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        Redraw();
    }

    private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        Redraw();
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Lane != null)
        {
            Lane.IsMuted = MuteButton.IsChecked == true;
            LaneSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void SoloButton_Click(object sender, RoutedEventArgs e)
    {
        if (Lane != null)
        {
            Lane.IsSoloed = SoloButton.IsChecked == true;
            LaneSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ArmButton_Click(object sender, RoutedEventArgs e)
    {
        if (Lane != null)
        {
            Lane.IsArmed = ArmButton.IsChecked == true;
            LaneSettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ShowHideButton_Click(object sender, RoutedEventArgs e)
    {
        bool isVisible = ShowHideButton.IsChecked == true;
        VisibilityToggled?.Invoke(this, isVisible);
    }

    private void ParameterSelectorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ParameterSelectorCombo.SelectedItem is AutomationParameterInfo param && Lane != null)
        {
            Lane.ParameterName = param.Name;
            Lane.MinValue = param.MinValue;
            Lane.MaxValue = param.MaxValue;
            Lane.DefaultValue = param.DefaultValue;
            Lane.Name = param.DisplayName;

            UpdateLaneHeader();
            Redraw();

            ParameterChanged?.Invoke(this, param);
        }
    }

    private void ColorIndicator_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // TODO: Show color picker dialog
    }

    private void CurveTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _defaultCurveType = CurveTypeCombo.SelectedIndex switch
        {
            0 => AutomationCurveType.Linear,
            1 => AutomationCurveType.Bezier,
            2 => AutomationCurveType.Step,
            3 => AutomationCurveType.Exponential,
            4 => AutomationCurveType.SCurve,
            _ => AutomationCurveType.Linear
        };

        // Apply to selected points
        foreach (var point in _selectedPoints)
        {
            point.CurveType = _defaultCurveType;
        }

        if (_selectedPoints.Count > 0)
        {
            PointsModified?.Invoke(this, EventArgs.Empty);
            DrawCurve();
        }
    }

    private void PointsCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PointsCanvas);
        PointsCanvas.CaptureMouse();

        // Check if clicking on a point
        var hitPoint = HitTestPoint(pos);

        if (hitPoint != null)
        {
            // Start dragging
            if (!_selectedPoints.Contains(hitPoint))
            {
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    ClearSelection();
                }
                SelectPoint(hitPoint);
            }

            _isDragging = true;
            _draggedPoint = hitPoint;
            _dragStart = pos;
        }
        else if (e.ClickCount == 2)
        {
            // Double-click to add point
            AddPointAtPosition(pos);
        }
        else
        {
            // Start selection rectangle
            if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            {
                ClearSelection();
            }
            _isSelecting = true;
            _dragStart = pos;
            Canvas.SetLeft(SelectionRect, pos.X);
            Canvas.SetTop(SelectionRect, pos.Y);
            SelectionRect.Width = 0;
            SelectionRect.Height = 0;
            SelectionRect.Visibility = Visibility.Visible;
        }

        _lastMousePos = pos;
        UpdateCursorInfo(pos);
        e.Handled = true;
    }

    private void PointsCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            // Finish selection
            var rect = new Rect(
                Canvas.GetLeft(SelectionRect),
                Canvas.GetTop(SelectionRect),
                SelectionRect.Width,
                SelectionRect.Height);

            SelectPointsInRect(rect);
            SelectionRect.Visibility = Visibility.Collapsed;
        }

        if (_isDragging)
        {
            PointsModified?.Invoke(this, EventArgs.Empty);
        }

        _isDragging = false;
        _isSelecting = false;
        _draggedPoint = null;
        PointsCanvas.ReleaseMouseCapture();
    }

    private void PointsCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(PointsCanvas);
        var hitPoint = HitTestPoint(pos);

        if (hitPoint != null && !_selectedPoints.Contains(hitPoint))
        {
            ClearSelection();
            SelectPoint(hitPoint);
        }
    }

    private void PointsCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var pos = e.GetPosition(PointsCanvas);

        if (_isDragging && _draggedPoint != null)
        {
            var delta = new Point(pos.X - _lastMousePos.X, pos.Y - _lastMousePos.Y);
            MoveSelectedPoints(delta);
            _lastMousePos = pos;
        }
        else if (_isSelecting)
        {
            var left = Math.Min(_dragStart.X, pos.X);
            var top = Math.Min(_dragStart.Y, pos.Y);
            var width = Math.Abs(pos.X - _dragStart.X);
            var height = Math.Abs(pos.Y - _dragStart.Y);

            Canvas.SetLeft(SelectionRect, left);
            Canvas.SetTop(SelectionRect, top);
            SelectionRect.Width = width;
            SelectionRect.Height = height;
        }

        UpdateCursorInfo(pos);
    }

    private void PointsCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom with mouse wheel
        double factor = e.Delta > 0 ? 1.1 : 0.9;
        var pos = e.GetPosition(PointsCanvas);
        double timeAtCursor = PositionToTime(pos.X);

        TimeScale *= factor;
        TimeScale = Math.Clamp(TimeScale, 10, 500);

        // Adjust offset to keep time at cursor position stable
        TimeOffset = timeAtCursor - (pos.X / TimeScale);
        TimeOffset = Math.Max(0, TimeOffset);

        Redraw();
        e.Handled = true;
    }

    private void AddPoint_Click(object sender, RoutedEventArgs e)
    {
        AddPointAtPosition(_lastMousePos);
    }

    private void DeleteSelectedPoints_Click(object sender, RoutedEventArgs e)
    {
        DeleteSelectedPoints();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (Lane == null) return;

        ClearSelection();
        foreach (var point in Lane.Curve.Points)
        {
            SelectPoint(point);
        }
    }

    private void ClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    private void SetCurveType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string typeStr)
        {
            var curveType = typeStr switch
            {
                "Linear" => AutomationCurveType.Linear,
                "Bezier" => AutomationCurveType.Bezier,
                "Step" => AutomationCurveType.Step,
                "Exponential" => AutomationCurveType.Exponential,
                "SCurve" => AutomationCurveType.SCurve,
                _ => AutomationCurveType.Linear
            };

            foreach (var point in _selectedPoints)
            {
                point.CurveType = curveType;
            }

            if (_selectedPoints.Count > 0)
            {
                PointsModified?.Invoke(this, EventArgs.Empty);
                DrawCurve();
            }
        }
    }

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        Lane?.ResetToDefault();
    }

    private void ClearAllPoints_Click(object sender, RoutedEventArgs e)
    {
        if (Lane == null) return;

        var result = MessageBox.Show(
            "Are you sure you want to clear all automation points?",
            "Clear Points",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            Lane.ClearPoints();
            ClearSelection();
            Redraw();
        }
    }

    #endregion

    #region Drawing Methods

    public void Redraw()
    {
        DrawGrid();
        DrawCurve();
        DrawPoints();
        UpdatePlayhead();
        UpdateCurrentValueDisplay();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        double width = PointsCanvas.ActualWidth;
        double height = PointsCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Vertical grid lines (time)
        double startTime = TimeOffset;
        double endTime = TimeOffset + (width / TimeScale);
        double gridStep = GridSubdivision;

        // Adjust grid step for zoom level
        while (gridStep * TimeScale < 20) gridStep *= 2;

        double firstLine = Math.Ceiling(startTime / gridStep) * gridStep;

        for (double t = firstLine; t <= endTime; t += gridStep)
        {
            double x = TimeToPosition(t);
            bool isStrongLine = Math.Abs(t % 1.0) < 0.001; // Full beat

            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = isStrongLine ? _gridLineStrongColor : _gridLineColor,
                StrokeThickness = isStrongLine ? 1 : 0.5
            };
            GridCanvas.Children.Add(line);
        }

        // Horizontal grid lines (value)
        int numHLines = 5;
        for (int i = 0; i <= numHLines; i++)
        {
            double y = (i / (double)numHLines) * height;
            bool isStrongLine = i == 0 || i == numHLines || i == numHLines / 2;

            var line = new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = isStrongLine ? _gridLineStrongColor : _gridLineColor,
                StrokeThickness = isStrongLine ? 1 : 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawCurve()
    {
        CurveCanvas.Children.Clear();

        if (Lane == null) return;

        double width = PointsCanvas.ActualWidth;
        double height = PointsCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        var points = Lane.Curve.Points;
        if (points.Count == 0) return;

        // Create path geometry
        var pathGeometry = new PathGeometry();
        var pathFigure = new PathFigure();

        // Start from left edge or first point
        double startTime = TimeOffset;
        float startValue = Lane.GetValueAtTime(startTime);
        pathFigure.StartPoint = new Point(0, ValueToPosition(startValue, height));

        // Sample the curve at regular intervals for smooth rendering
        double sampleStep = 1.0 / TimeScale; // One pixel
        double endTime = TimeOffset + (width / TimeScale);

        var polySegment = new PolyLineSegment();

        for (double t = startTime; t <= endTime; t += sampleStep)
        {
            float value = Lane.GetValueAtTime(t);
            double x = TimeToPosition(t);
            double y = ValueToPosition(value, height);
            polySegment.Points.Add(new Point(x, y));
        }

        pathFigure.Segments.Add(polySegment);
        pathGeometry.Figures.Add(pathFigure);

        // Create the path
        var path = new Path
        {
            Data = pathGeometry,
            Stroke = _curveColor,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        CurveCanvas.Children.Add(path);

        // Draw filled area under curve (optional)
        var fillGeometry = pathGeometry.Clone();
        var fillFigure = fillGeometry.Figures[0];

        // Close the path at the bottom
        var closingSegment = new PolyLineSegment();
        closingSegment.Points.Add(new Point(width, height));
        closingSegment.Points.Add(new Point(0, height));
        fillFigure.Segments.Add(closingSegment);
        fillFigure.IsClosed = true;

        var fillPath = new Path
        {
            Data = fillGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(30, 0x6A, 0xAB, 0x73)),
            Stroke = null
        };

        CurveCanvas.Children.Insert(0, fillPath);
    }

    private void DrawPoints()
    {
        PointsCanvas.Children.Clear();
        _pointVisuals.Clear();

        if (Lane == null) return;

        double height = PointsCanvas.ActualHeight;
        if (height <= 0) return;

        foreach (var point in Lane.Curve.Points)
        {
            double x = TimeToPosition(point.Time);
            double y = ValueToPosition(point.Value, height);

            // Skip points outside visible area
            if (x < -PointRadius || x > PointsCanvas.ActualWidth + PointRadius)
                continue;

            var ellipse = new Ellipse
            {
                Width = PointRadius * 2,
                Height = PointRadius * 2,
                Fill = _selectedPoints.Contains(point) ? _pointSelectedColor : _pointColor,
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 1,
                Tag = point,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(ellipse, x - PointRadius);
            Canvas.SetTop(ellipse, y - PointRadius);

            PointsCanvas.Children.Add(ellipse);
            _pointVisuals[point] = ellipse;
        }
    }

    private void UpdatePlayhead()
    {
        if (!ShowPlayhead)
        {
            Playhead.Visibility = Visibility.Collapsed;
            return;
        }

        double x = TimeToPosition(CurrentTime);

        if (x >= 0 && x <= PointsCanvas.ActualWidth)
        {
            Playhead.X1 = x;
            Playhead.X2 = x;
            Playhead.Y2 = PointsCanvas.ActualHeight;
            Playhead.Visibility = Visibility.Visible;
        }
        else
        {
            Playhead.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateLaneHeader()
    {
        if (Lane == null)
        {
            LaneNameText.Text = "No Lane";
            TargetInfoText.Text = "Target: None";
            return;
        }

        LaneNameText.Text = string.IsNullOrEmpty(Lane.Name) ? Lane.ParameterName : Lane.Name;
        TargetInfoText.Text = $"Target: {Lane.TargetId}";
        ValueRangeText.Text = $"{Lane.MinValue:F1} - {Lane.MaxValue:F1}";

        MuteButton.IsChecked = Lane.IsMuted;
        SoloButton.IsChecked = Lane.IsSoloed;
        ArmButton.IsChecked = Lane.IsArmed;

        // Update color indicator
        try
        {
            ColorIndicator.Background = new SolidColorBrush(
                (Color)ColorConverter.ConvertFromString(Lane.Color));
        }
        catch
        {
            ColorIndicator.Background = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF));
        }
    }

    private void UpdateCurrentValueDisplay()
    {
        if (Lane == null) return;

        float value = Lane.GetValueAtTime(CurrentTime);
        CurrentValueText.Text = value.ToString("F3");
    }

    private void UpdateCursorInfo(Point pos)
    {
        double time = PositionToTime(pos.X);
        double height = PointsCanvas.ActualHeight;
        float value = PositionToValue(pos.Y, height);

        CursorTimeText.Text = $"Time: {time:F3}";
        CursorValueText.Text = $"Value: {value:F3}";

        // Position popup near cursor but not under it
        double popupX = pos.X + 15;
        double popupY = pos.Y - 40;

        if (popupX + 80 > PointsCanvas.ActualWidth)
            popupX = pos.X - 95;
        if (popupY < 0)
            popupY = pos.Y + 15;

        Canvas.SetLeft(CursorValuePopup, popupX);
        Canvas.SetTop(CursorValuePopup, popupY);
        CursorValuePopup.Visibility = Visibility.Visible;
    }

    #endregion

    #region Point Manipulation

    private AutomationPoint? HitTestPoint(Point pos)
    {
        if (Lane == null) return null;

        double height = PointsCanvas.ActualHeight;

        foreach (var point in Lane.Curve.Points)
        {
            double px = TimeToPosition(point.Time);
            double py = ValueToPosition(point.Value, height);

            double dist = Math.Sqrt(Math.Pow(pos.X - px, 2) + Math.Pow(pos.Y - py, 2));
            if (dist <= PointHitRadius)
            {
                return point;
            }
        }

        return null;
    }

    private void AddPointAtPosition(Point pos)
    {
        if (Lane == null) return;

        double time = PositionToTime(pos.X);
        double height = PointsCanvas.ActualHeight;
        float value = PositionToValue(pos.Y, height);

        if (SnapToGrid)
        {
            time = Math.Round(time / GridSubdivision) * GridSubdivision;
        }

        time = Math.Max(0, time);
        value = Math.Clamp(value, Lane.MinValue, Lane.MaxValue);

        var point = Lane.AddPoint(time, value, _defaultCurveType);
        PointAdded?.Invoke(this, point);
        Redraw();
    }

    private void SelectPoint(AutomationPoint point)
    {
        if (!_selectedPoints.Contains(point))
        {
            point.IsSelected = true;
            _selectedPoints.Add(point);

            if (_pointVisuals.TryGetValue(point, out var ellipse))
            {
                ellipse.Fill = _pointSelectedColor;
            }
        }
    }

    private void DeselectPoint(AutomationPoint point)
    {
        point.IsSelected = false;
        _selectedPoints.Remove(point);

        if (_pointVisuals.TryGetValue(point, out var ellipse))
        {
            ellipse.Fill = _pointColor;
        }
    }

    private void ClearSelection()
    {
        foreach (var point in _selectedPoints.ToList())
        {
            DeselectPoint(point);
        }
    }

    private void SelectPointsInRect(Rect rect)
    {
        if (Lane == null) return;

        double height = PointsCanvas.ActualHeight;

        foreach (var point in Lane.Curve.Points)
        {
            double px = TimeToPosition(point.Time);
            double py = ValueToPosition(point.Value, height);

            if (rect.Contains(new Point(px, py)))
            {
                SelectPoint(point);
            }
        }
    }

    private void MoveSelectedPoints(Point delta)
    {
        if (Lane == null) return;

        double height = PointsCanvas.ActualHeight;
        double timeDelta = delta.X / TimeScale;
        double valueDelta = -delta.Y / height * (Lane.MaxValue - Lane.MinValue);

        foreach (var point in _selectedPoints)
        {
            double newTime = point.Time + timeDelta;
            float newValue = point.Value + (float)valueDelta;

            if (SnapToGrid)
            {
                newTime = Math.Round(newTime / GridSubdivision) * GridSubdivision;
            }

            point.Time = Math.Max(0, newTime);
            point.Value = Math.Clamp(newValue, Lane.MinValue, Lane.MaxValue);
        }

        Redraw();
    }

    private void DeleteSelectedPoints()
    {
        if (Lane == null) return;

        foreach (var point in _selectedPoints.ToList())
        {
            Lane.RemovePoint(point);
            PointRemoved?.Invoke(this, point);
        }

        _selectedPoints.Clear();
        PointsModified?.Invoke(this, EventArgs.Empty);
        Redraw();
    }

    #endregion

    #region Coordinate Conversion

    private double TimeToPosition(double time)
    {
        return (time - TimeOffset) * TimeScale;
    }

    private double PositionToTime(double x)
    {
        return TimeOffset + (x / TimeScale);
    }

    private double ValueToPosition(float value, double height)
    {
        if (Lane == null) return height / 2;

        float normalized = (value - Lane.MinValue) / (Lane.MaxValue - Lane.MinValue);
        return height * (1 - normalized);
    }

    private float PositionToValue(double y, double height)
    {
        if (Lane == null) return 0;

        float normalized = 1 - (float)(y / height);
        return Lane.MinValue + normalized * (Lane.MaxValue - Lane.MinValue);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Scrolls to show the specified time.
    /// </summary>
    /// <param name="time">The time to scroll to.</param>
    public void ScrollToTime(double time)
    {
        double width = PointsCanvas.ActualWidth;
        if (width <= 0) return;

        double margin = width * 0.1;
        double currentX = TimeToPosition(time);

        if (currentX < margin)
        {
            TimeOffset = time - (margin / TimeScale);
        }
        else if (currentX > width - margin)
        {
            TimeOffset = time - ((width - margin) / TimeScale);
        }

        TimeOffset = Math.Max(0, TimeOffset);
        Redraw();
    }

    /// <summary>
    /// Zooms to fit all points in view.
    /// </summary>
    public void ZoomToFit()
    {
        if (Lane == null || Lane.Curve.Count == 0) return;

        double minTime = Lane.Curve.MinTime;
        double maxTime = Lane.Curve.MaxTime;
        double duration = maxTime - minTime;

        if (duration <= 0) duration = 4;

        double width = PointsCanvas.ActualWidth;
        if (width <= 0) return;

        TimeScale = (width * 0.9) / duration;
        TimeOffset = minTime - (width * 0.05 / TimeScale);
        TimeOffset = Math.Max(0, TimeOffset);

        Redraw();
    }

    /// <summary>
    /// Sets the available parameters for the lane.
    /// </summary>
    /// <param name="parameters">The list of available parameters.</param>
    public void SetAvailableParameters(IEnumerable<AutomationParameterInfo> parameters)
    {
        AvailableParameters = new ObservableCollection<AutomationParameterInfo>(parameters);
    }

    /// <summary>
    /// Sets the selected parameter by name.
    /// </summary>
    /// <param name="parameterName">The parameter name to select.</param>
    public void SetSelectedParameter(string parameterName)
    {
        if (AvailableParameters == null) return;

        var param = AvailableParameters.FirstOrDefault(p => p.Name == parameterName);
        if (param != null)
        {
            ParameterSelectorCombo.SelectedItem = param;
        }
    }

    private void UpdateParameterSelector()
    {
        if (AvailableParameters != null)
        {
            ParameterSelectorCombo.ItemsSource = AvailableParameters;

            // Select the current parameter if the lane already has one set
            if (Lane != null && !string.IsNullOrEmpty(Lane.ParameterName))
            {
                var currentParam = AvailableParameters.FirstOrDefault(p => p.Name == Lane.ParameterName);
                if (currentParam != null)
                {
                    ParameterSelectorCombo.SelectedItem = currentParam;
                }
            }
        }
    }

    private void UpdateCollapsedState()
    {
        // When collapsed, reduce height to show only header
        if (IsCollapsed)
        {
            Height = 32;
        }
        else
        {
            Height = double.NaN; // Auto
        }
    }

    #endregion
}
