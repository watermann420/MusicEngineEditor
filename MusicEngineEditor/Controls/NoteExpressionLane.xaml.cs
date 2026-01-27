// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Note Expression Lane for per-note modulation editing (MPE style).
/// Supports Pitch Bend, Pressure, and Slide (CC74) expression types.
/// </summary>
public partial class NoteExpressionLane : UserControl, INotifyPropertyChanged
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const int PitchBendMin = -8192;
    private const int PitchBendMax = 8191;
    private const int PressureMin = 0;
    private const int PressureMax = 127;
    private const int SlideMin = 0;
    private const int SlideMax = 127;
    private const double PointRadius = 4;
    private const double HandleRadius = 6;

    #endregion

    #region Private Fields

    private PianoRollNote? _selectedNote;
    private NoteExpressionType _expressionType = NoteExpressionType.PitchBend;
    private ExpressionDrawTool _currentTool = ExpressionDrawTool.Draw;
    private readonly List<ExpressionPoint> _expressionPoints = [];
    private readonly List<ExpressionPoint> _clipboard = [];

    private double _scrollX;
    private bool _isDrawing;
    private Point _lastDrawPoint;
    private Point? _lineStart;

    // Colors
    private static readonly Color PitchBendColor = Color.FromRgb(0x21, 0x96, 0xF3);
    private static readonly Color PressureColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private static readonly Color SlideColor = Color.FromRgb(0x9C, 0x27, 0xB0);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty NotesProperty =
        DependencyProperty.Register(nameof(Notes), typeof(ObservableCollection<PianoRollNote>), typeof(NoteExpressionLane),
            new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedNoteProperty =
        DependencyProperty.Register(nameof(SelectedNote), typeof(PianoRollNote), typeof(NoteExpressionLane),
            new PropertyMetadata(null, OnSelectedNoteChanged));

    public static readonly DependencyProperty ExpressionTypeProperty =
        DependencyProperty.Register(nameof(ExpressionType), typeof(NoteExpressionType), typeof(NoteExpressionLane),
            new PropertyMetadata(NoteExpressionType.PitchBend, OnExpressionTypeChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(NoteExpressionLane),
            new PropertyMetadata(DefaultBeatWidth, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.Register(nameof(ScrollOffset), typeof(double), typeof(NoteExpressionLane),
            new PropertyMetadata(0.0, OnScrollChanged));

    public static readonly DependencyProperty ZoomXProperty =
        DependencyProperty.Register(nameof(ZoomX), typeof(double), typeof(NoteExpressionLane),
            new PropertyMetadata(1.0, OnLayoutPropertyChanged));

    /// <summary>
    /// Gets or sets the notes collection.
    /// </summary>
    public ObservableCollection<PianoRollNote>? Notes
    {
        get => (ObservableCollection<PianoRollNote>?)GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }

    /// <summary>
    /// Gets or sets the selected note for expression editing.
    /// </summary>
    public PianoRollNote? SelectedNote
    {
        get => (PianoRollNote?)GetValue(SelectedNoteProperty);
        set => SetValue(SelectedNoteProperty, value);
    }

    /// <summary>
    /// Gets or sets the expression type being edited.
    /// </summary>
    public NoteExpressionType ExpressionType
    {
        get => (NoteExpressionType)GetValue(ExpressionTypeProperty);
        set => SetValue(ExpressionTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the pixels per beat.
    /// </summary>
    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    /// <summary>
    /// Gets or sets the scroll offset.
    /// </summary>
    public double ScrollOffset
    {
        get => (double)GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// Gets or sets the horizontal zoom factor.
    /// </summary>
    public double ZoomX
    {
        get => (double)GetValue(ZoomXProperty);
        set => SetValue(ZoomXProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when expression data changes.
    /// </summary>
    public event EventHandler<NoteExpressionChangedEventArgs>? ExpressionChanged;

    #endregion

    #region Constructor

    public NoteExpressionLane()
    {
        InitializeComponent();
        DataContext = this;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RenderAll();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderAll();
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnSelectedNoteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteExpressionLane lane)
        {
            lane.OnSelectedNoteUpdated(e.NewValue as PianoRollNote);
        }
    }

    private static void OnExpressionTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteExpressionLane lane)
        {
            lane.OnExpressionTypeUpdated((NoteExpressionType)e.NewValue);
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteExpressionLane lane)
        {
            lane.RenderAll();
        }
    }

    private static void OnScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NoteExpressionLane lane)
        {
            lane._scrollX = (double)e.NewValue;
            lane.ApplyScrollTransform();
        }
    }

    private void OnSelectedNoteUpdated(PianoRollNote? note)
    {
        _selectedNote = note;
        LoadExpressionData();

        if (note != null)
        {
            NoSelectionOverlay.Visibility = Visibility.Collapsed;
            SelectedNoteText.Text = $"Note: {PianoRollNote.GetNoteName(note.Note)} ({note.StartBeat:F2} - {note.GetEndBeat():F2})";
        }
        else
        {
            NoSelectionOverlay.Visibility = Visibility.Visible;
            SelectedNoteText.Text = "No note selected";
        }

        RenderAll();
    }

    private void OnExpressionTypeUpdated(NoteExpressionType type)
    {
        _expressionType = type;

        // Update toggle buttons
        PitchBendToggle.IsChecked = type == NoteExpressionType.PitchBend;
        PressureToggle.IsChecked = type == NoteExpressionType.Pressure;
        SlideToggle.IsChecked = type == NoteExpressionType.Slide;

        // Update center line visibility (only for bipolar pitch bend)
        CenterLine.Visibility = type == NoteExpressionType.PitchBend ? Visibility.Visible : Visibility.Collapsed;

        LoadExpressionData();
        RenderAll();
    }

    #endregion

    #region Expression Data Management

    private void LoadExpressionData()
    {
        _expressionPoints.Clear();

        if (_selectedNote == null) return;

        // Load expression data from note (this would typically be stored in a NoteExpression property)
        // For now, create default values
        var data = GetNoteExpressionData(_selectedNote, _expressionType);
        _expressionPoints.AddRange(data);
    }

    private void SaveExpressionData()
    {
        if (_selectedNote == null) return;

        // Save expression data to note
        SetNoteExpressionData(_selectedNote, _expressionType, _expressionPoints);

        ExpressionChanged?.Invoke(this, new NoteExpressionChangedEventArgs(
            _selectedNote, _expressionType, _expressionPoints.ToList()));
    }

    private static List<ExpressionPoint> GetNoteExpressionData(PianoRollNote note, NoteExpressionType type)
    {
        // In a real implementation, this would read from note.Expression property
        // For demonstration, return empty list (flat line at default)
        return [];
    }

    private static void SetNoteExpressionData(PianoRollNote note, NoteExpressionType type, List<ExpressionPoint> points)
    {
        // In a real implementation, this would write to note.Expression property
    }

    #endregion

    #region Rendering

    private void RenderAll()
    {
        if (!IsLoaded) return;

        RenderGrid();
        RenderNoteBackground();
        RenderExpressionCurve();
        UpdateCenterLine();
        ApplyScrollTransform();
    }

    private void RenderGrid()
    {
        GridCanvas.Children.Clear();

        var height = ExpressionCanvas.ActualHeight > 0 ? ExpressionCanvas.ActualHeight : 100;
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;

        // Determine width based on selected note or total beats
        double totalWidth;
        if (_selectedNote != null)
        {
            totalWidth = (_selectedNote.Duration + 2) * effectiveBeatWidth;
        }
        else
        {
            totalWidth = 16 * effectiveBeatWidth;
        }

        GridCanvas.Width = totalWidth;
        GridCanvas.Height = height;

        // Draw horizontal guide lines (25%, 50%, 75%)
        var gridLineColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
        foreach (var fraction in new[] { 0.25, 0.5, 0.75 })
        {
            var y = height * fraction;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = totalWidth,
                Y2 = y,
                Stroke = new SolidColorBrush(gridLineColor),
                StrokeThickness = 0.5
            };
            GridCanvas.Children.Add(line);
        }

        // Draw vertical beat lines
        var beatsToShow = (int)Math.Ceiling(totalWidth / effectiveBeatWidth);
        for (int beat = 0; beat <= beatsToShow; beat++)
        {
            var x = beat * effectiveBeatWidth;
            var isBarLine = beat % 4 == 0;

            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = new SolidColorBrush(isBarLine ? Color.FromRgb(0x3A, 0x3A, 0x3A) : gridLineColor),
                StrokeThickness = isBarLine ? 1 : 0.5
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void RenderNoteBackground()
    {
        NoteBackgroundCanvas.Children.Clear();

        if (_selectedNote == null) return;

        var height = ExpressionCanvas.ActualHeight > 0 ? ExpressionCanvas.ActualHeight : 100;
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;

        var x = _selectedNote.StartBeat * effectiveBeatWidth;
        var width = _selectedNote.Duration * effectiveBeatWidth;

        var color = GetExpressionColor();
        var noteRect = new Shapes.Rectangle
        {
            Width = width,
            Height = height,
            Fill = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1
        };

        Canvas.SetLeft(noteRect, x);
        Canvas.SetTop(noteRect, 0);
        NoteBackgroundCanvas.Children.Add(noteRect);
    }

    private void RenderExpressionCurve()
    {
        ExpressionCanvas.Children.Clear();

        if (_selectedNote == null) return;

        var height = ExpressionCanvas.ActualHeight > 0 ? ExpressionCanvas.ActualHeight : 100;
        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var color = GetExpressionColor();

        // Get range based on expression type
        var (minValue, maxValue) = GetValueRange();

        // If no points, draw default line
        if (_expressionPoints.Count == 0)
        {
            var defaultValue = _expressionType == NoteExpressionType.PitchBend ? 0 : 0;
            var defaultY = ValueToY(defaultValue, height, minValue, maxValue);

            var startX = _selectedNote.StartBeat * effectiveBeatWidth;
            var endX = _selectedNote.GetEndBeat() * effectiveBeatWidth;

            var defaultLine = new Shapes.Line
            {
                X1 = startX,
                Y1 = defaultY,
                X2 = endX,
                Y2 = defaultY,
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection([4, 2])
            };
            ExpressionCanvas.Children.Add(defaultLine);
            return;
        }

        // Sort points by position
        var sortedPoints = _expressionPoints.OrderBy(p => p.Position).ToList();

        // Draw curve connecting points
        if (sortedPoints.Count > 1)
        {
            var pathFigure = new PathFigure();
            var firstPoint = sortedPoints[0];
            var startX = PositionToX(firstPoint.Position, effectiveBeatWidth);
            var startY = ValueToY(firstPoint.Value, height, minValue, maxValue);
            pathFigure.StartPoint = new Point(startX, startY);

            for (int i = 1; i < sortedPoints.Count; i++)
            {
                var point = sortedPoints[i];
                var x = PositionToX(point.Position, effectiveBeatWidth);
                var y = ValueToY(point.Value, height, minValue, maxValue);

                // Use bezier for smooth curves
                if (i > 0 && i < sortedPoints.Count - 1)
                {
                    var prev = sortedPoints[i - 1];
                    var cp1X = (PositionToX(prev.Position, effectiveBeatWidth) + x) / 2;
                    var cp1Y = ValueToY(prev.Value, height, minValue, maxValue);
                    var cp2X = cp1X;
                    var cp2Y = y;
                    pathFigure.Segments.Add(new BezierSegment(
                        new Point(cp1X, cp1Y),
                        new Point(cp2X, cp2Y),
                        new Point(x, y),
                        true));
                }
                else
                {
                    pathFigure.Segments.Add(new LineSegment(new Point(x, y), true));
                }
            }

            var pathGeometry = new PathGeometry { Figures = { pathFigure } };
            var path = new Shapes.Path
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                Data = pathGeometry
            };
            ExpressionCanvas.Children.Add(path);
        }

        // Draw points/handles
        foreach (var point in sortedPoints)
        {
            var x = PositionToX(point.Position, effectiveBeatWidth);
            var y = ValueToY(point.Value, height, minValue, maxValue);

            var handle = new Shapes.Ellipse
            {
                Width = HandleRadius * 2,
                Height = HandleRadius * 2,
                Fill = new SolidColorBrush(point.IsSelected ? Colors.White : color),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.Hand,
                Tag = point
            };

            Canvas.SetLeft(handle, x - HandleRadius);
            Canvas.SetTop(handle, y - HandleRadius);

            handle.MouseLeftButtonDown += Handle_MouseLeftButtonDown;
            handle.MouseRightButtonDown += Handle_MouseRightButtonDown;

            ExpressionCanvas.Children.Add(handle);
        }
    }

    private void UpdateCenterLine()
    {
        var height = ExpressionCanvas.ActualHeight > 0 ? ExpressionCanvas.ActualHeight : 100;
        CenterLine.Y1 = height / 2;
        CenterLine.Y2 = height / 2;
        CenterLine.X2 = ExpressionCanvas.ActualWidth;
    }

    private void ApplyScrollTransform()
    {
        var transform = new TranslateTransform(-_scrollX, 0);
        GridCanvas.RenderTransform = transform;
        NoteBackgroundCanvas.RenderTransform = transform;
        ExpressionCanvas.RenderTransform = transform;
    }

    private Color GetExpressionColor()
    {
        return _expressionType switch
        {
            NoteExpressionType.PitchBend => PitchBendColor,
            NoteExpressionType.Pressure => PressureColor,
            NoteExpressionType.Slide => SlideColor,
            _ => PitchBendColor
        };
    }

    private (int Min, int Max) GetValueRange()
    {
        return _expressionType switch
        {
            NoteExpressionType.PitchBend => (PitchBendMin, PitchBendMax),
            NoteExpressionType.Pressure => (PressureMin, PressureMax),
            NoteExpressionType.Slide => (SlideMin, SlideMax),
            _ => (0, 127)
        };
    }

    private double PositionToX(double position, double effectiveBeatWidth)
    {
        if (_selectedNote == null) return 0;
        var relativePosition = position * _selectedNote.Duration;
        return (_selectedNote.StartBeat + relativePosition) * effectiveBeatWidth;
    }

    private double XToPosition(double x, double effectiveBeatWidth)
    {
        if (_selectedNote == null) return 0;
        var beat = x / effectiveBeatWidth;
        var relativePosition = (beat - _selectedNote.StartBeat) / _selectedNote.Duration;
        return Math.Clamp(relativePosition, 0, 1);
    }

    private static double ValueToY(double value, double height, int minValue, int maxValue)
    {
        var normalized = (value - minValue) / (maxValue - minValue);
        return height * (1 - normalized);
    }

    private static double YToValue(double y, double height, int minValue, int maxValue)
    {
        var normalized = 1 - (y / height);
        return minValue + normalized * (maxValue - minValue);
    }

    #endregion

    #region Mouse Event Handlers

    private void ExpressionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_selectedNote == null) return;

        var position = e.GetPosition(ExpressionCanvas);
        position.X += _scrollX;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = ExpressionCanvas.ActualHeight;
        var (minValue, maxValue) = GetValueRange();

        var relativePosition = XToPosition(position.X, effectiveBeatWidth);
        var value = YToValue(position.Y, height, minValue, maxValue);

        if (_currentTool == ExpressionDrawTool.Line)
        {
            if (_lineStart == null)
            {
                _lineStart = position;
            }
            else
            {
                // Create line from start to current point
                var startPos = XToPosition(_lineStart.Value.X, effectiveBeatWidth);
                var startVal = YToValue(_lineStart.Value.Y, height, minValue, maxValue);

                // Add interpolated points
                const int numPoints = 10;
                for (int i = 0; i <= numPoints; i++)
                {
                    var t = i / (double)numPoints;
                    var linePos = startPos + t * (relativePosition - startPos);
                    var lineVal = startVal + t * (value - startVal);

                    AddOrUpdatePoint(linePos, lineVal);
                }

                _lineStart = null;
            }
        }
        else if (_currentTool == ExpressionDrawTool.Erase)
        {
            // Find and remove nearby point
            var pointToRemove = _expressionPoints.FirstOrDefault(p =>
                Math.Abs(p.Position - relativePosition) < 0.05);

            if (pointToRemove != null)
            {
                _expressionPoints.Remove(pointToRemove);
            }
        }
        else // Draw tool
        {
            _isDrawing = true;
            _lastDrawPoint = position;
            AddOrUpdatePoint(relativePosition, value);
        }

        ExpressionCanvas.CaptureMouse();
        RenderExpressionCurve();
        ShowValueTooltip(position, value);
    }

    private void ExpressionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            ExpressionCanvas.ReleaseMouseCapture();
            SaveExpressionData();
        }

        ValueTooltip.Visibility = Visibility.Collapsed;
    }

    private void ExpressionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing || _selectedNote == null) return;

        var position = e.GetPosition(ExpressionCanvas);
        position.X += _scrollX;

        var effectiveBeatWidth = PixelsPerBeat * ZoomX;
        var height = ExpressionCanvas.ActualHeight;
        var (minValue, maxValue) = GetValueRange();

        var relativePosition = XToPosition(position.X, effectiveBeatWidth);
        var value = YToValue(position.Y, height, minValue, maxValue);

        if (_currentTool == ExpressionDrawTool.Draw)
        {
            // Interpolate between last point and current
            var lastPos = XToPosition(_lastDrawPoint.X, effectiveBeatWidth);
            var lastVal = YToValue(_lastDrawPoint.Y, height, minValue, maxValue);

            var distance = Math.Sqrt(
                Math.Pow(position.X - _lastDrawPoint.X, 2) +
                Math.Pow(position.Y - _lastDrawPoint.Y, 2));

            if (distance > 5)
            {
                AddOrUpdatePoint(relativePosition, value);
                _lastDrawPoint = position;
            }
        }
        else if (_currentTool == ExpressionDrawTool.Erase)
        {
            var pointToRemove = _expressionPoints.FirstOrDefault(p =>
                Math.Abs(p.Position - relativePosition) < 0.05);

            if (pointToRemove != null)
            {
                _expressionPoints.Remove(pointToRemove);
            }
        }

        RenderExpressionCurve();
        ShowValueTooltip(position, value);
    }

    private void ExpressionCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        ValueTooltip.Visibility = Visibility.Collapsed;
    }

    private void Handle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Shapes.Ellipse handle && handle.Tag is ExpressionPoint point)
        {
            // Select point
            foreach (var p in _expressionPoints)
            {
                p.IsSelected = false;
            }
            point.IsSelected = true;
            RenderExpressionCurve();
            e.Handled = true;
        }
    }

    private void Handle_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Shapes.Ellipse handle && handle.Tag is ExpressionPoint point)
        {
            _expressionPoints.Remove(point);
            RenderExpressionCurve();
            SaveExpressionData();
            e.Handled = true;
        }
    }

    #endregion

    #region Expression Point Management

    private void AddOrUpdatePoint(double position, double value)
    {
        var (minValue, maxValue) = GetValueRange();
        value = Math.Clamp(value, minValue, maxValue);

        // Find existing point at similar position
        var existing = _expressionPoints.FirstOrDefault(p =>
            Math.Abs(p.Position - position) < 0.02);

        if (existing != null)
        {
            existing.Value = value;
        }
        else
        {
            _expressionPoints.Add(new ExpressionPoint
            {
                Position = position,
                Value = value
            });
        }
    }

    private void ShowValueTooltip(Point position, double value)
    {
        var displayValue = _expressionType switch
        {
            NoteExpressionType.PitchBend => $"{value:+0;-0;0}",
            _ => $"{(int)value}"
        };

        ValueText.Text = displayValue;
        ValueTooltip.Margin = new Thickness(position.X - _scrollX + 10, position.Y - 20, 0, 0);
        ValueTooltip.Visibility = Visibility.Visible;
    }

    #endregion

    #region Toolbar Event Handlers

    private void ExpressionType_Checked(object sender, RoutedEventArgs e)
    {
        if (sender == PitchBendToggle && PitchBendToggle.IsChecked == true)
        {
            PressureToggle.IsChecked = false;
            SlideToggle.IsChecked = false;
            ExpressionType = NoteExpressionType.PitchBend;
        }
        else if (sender == PressureToggle && PressureToggle.IsChecked == true)
        {
            PitchBendToggle.IsChecked = false;
            SlideToggle.IsChecked = false;
            ExpressionType = NoteExpressionType.Pressure;
        }
        else if (sender == SlideToggle && SlideToggle.IsChecked == true)
        {
            PitchBendToggle.IsChecked = false;
            PressureToggle.IsChecked = false;
            ExpressionType = NoteExpressionType.Slide;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _expressionPoints.Clear();
        RenderExpressionCurve();
        SaveExpressionData();
    }

    #endregion

    #region Context Menu Handlers

    private void ResetToDefault_Click(object sender, RoutedEventArgs e)
    {
        _expressionPoints.Clear();
        RenderExpressionCurve();
        SaveExpressionData();
    }

    private void CopyExpression_Click(object sender, RoutedEventArgs e)
    {
        _clipboard.Clear();
        _clipboard.AddRange(_expressionPoints.Select(p => new ExpressionPoint
        {
            Position = p.Position,
            Value = p.Value
        }));
    }

    private void PasteExpression_Click(object sender, RoutedEventArgs e)
    {
        if (_clipboard.Count == 0) return;

        _expressionPoints.Clear();
        _expressionPoints.AddRange(_clipboard.Select(p => new ExpressionPoint
        {
            Position = p.Position,
            Value = p.Value
        }));

        RenderExpressionCurve();
        SaveExpressionData();
    }

    private void Smooth_Click(object sender, RoutedEventArgs e)
    {
        if (_expressionPoints.Count < 3) return;

        // Simple moving average smoothing
        var sorted = _expressionPoints.OrderBy(p => p.Position).ToList();
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            sorted[i].Value = (sorted[i - 1].Value + sorted[i].Value + sorted[i + 1].Value) / 3;
        }

        RenderExpressionCurve();
        SaveExpressionData();
    }

    private void QuantizeExpression_Click(object sender, RoutedEventArgs e)
    {
        // Quantize values to nearest step
        var (minValue, maxValue) = GetValueRange();
        var step = (maxValue - minValue) / 16.0;

        foreach (var point in _expressionPoints)
        {
            point.Value = Math.Round(point.Value / step) * step;
        }

        RenderExpressionCurve();
        SaveExpressionData();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the expression lane display.
    /// </summary>
    public void Refresh()
    {
        RenderAll();
    }

    /// <summary>
    /// Sets the expression data for the selected note.
    /// </summary>
    public void SetExpressionData(List<ExpressionPoint> points)
    {
        _expressionPoints.Clear();
        _expressionPoints.AddRange(points);
        RenderExpressionCurve();
    }

    /// <summary>
    /// Gets the current expression data.
    /// </summary>
    public List<ExpressionPoint> GetExpressionData()
    {
        return _expressionPoints.ToList();
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
/// Note expression type enumeration.
/// </summary>
public enum NoteExpressionType
{
    /// <summary>
    /// Pitch bend (-8192 to +8191).
    /// </summary>
    PitchBend,

    /// <summary>
    /// Polyphonic aftertouch / pressure (0-127).
    /// </summary>
    Pressure,

    /// <summary>
    /// Slide / CC74 (0-127).
    /// </summary>
    Slide
}

/// <summary>
/// Expression drawing tool enumeration.
/// </summary>
public enum ExpressionDrawTool
{
    Draw,
    Line,
    Erase
}

/// <summary>
/// Represents a point on an expression curve.
/// </summary>
public partial class ExpressionPoint : ObservableObject
{
    /// <summary>
    /// Unique identifier.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Position within the note (0.0 = start, 1.0 = end).
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Expression value.
    /// </summary>
    [ObservableProperty]
    private double _value;

    /// <summary>
    /// Indicates whether this point is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// Event arguments for expression changed events.
/// </summary>
public class NoteExpressionChangedEventArgs : EventArgs
{
    /// <summary>
    /// The note whose expression was changed.
    /// </summary>
    public PianoRollNote Note { get; }

    /// <summary>
    /// The type of expression that changed.
    /// </summary>
    public NoteExpressionType ExpressionType { get; }

    /// <summary>
    /// The new expression points.
    /// </summary>
    public List<ExpressionPoint> Points { get; }

    public NoteExpressionChangedEventArgs(PianoRollNote note, NoteExpressionType type, List<ExpressionPoint> points)
    {
        Note = note;
        ExpressionType = type;
        Points = points;
    }
}
