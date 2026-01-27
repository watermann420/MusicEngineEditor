// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for visualizing and editing fade curves with interactive handles.
/// </summary>
public partial class FadeCurveEditor : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty FadeTypeProperty =
        DependencyProperty.Register(nameof(FadeType), typeof(FadeType), typeof(FadeCurveEditor),
            new PropertyMetadata(FadeType.Linear, OnFadeTypeChanged));

    public static readonly DependencyProperty DurationProperty =
        DependencyProperty.Register(nameof(Duration), typeof(double), typeof(FadeCurveEditor),
            new PropertyMetadata(0.0, OnDurationChanged));

    public static readonly DependencyProperty IsFadeInProperty =
        DependencyProperty.Register(nameof(IsFadeIn), typeof(bool), typeof(FadeCurveEditor),
            new PropertyMetadata(true, OnFadeDirectionChanged));

    public static readonly DependencyProperty ShowHandlesProperty =
        DependencyProperty.Register(nameof(ShowHandles), typeof(bool), typeof(FadeCurveEditor),
            new PropertyMetadata(false, OnShowHandlesChanged));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(FadeCurveEditor),
            new PropertyMetadata("Fade Curve", OnTitleChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the fade type.
    /// </summary>
    public FadeType FadeType
    {
        get => (FadeType)GetValue(FadeTypeProperty);
        set => SetValue(FadeTypeProperty, value);
    }

    /// <summary>
    /// Gets or sets the fade duration in beats.
    /// </summary>
    public double Duration
    {
        get => (double)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Gets or sets whether this is a fade-in (true) or fade-out (false).
    /// </summary>
    public bool IsFadeIn
    {
        get => (bool)GetValue(IsFadeInProperty);
        set => SetValue(IsFadeInProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show interactive handles.
    /// </summary>
    public bool ShowHandles
    {
        get => (bool)GetValue(ShowHandlesProperty);
        set => SetValue(ShowHandlesProperty, value);
    }

    /// <summary>
    /// Gets or sets the title text.
    /// </summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the fade type changes.
    /// </summary>
    public event EventHandler<FadeType>? FadeTypeChanged;

    /// <summary>
    /// Event raised when the duration changes.
    /// </summary>
    public event EventHandler<double>? DurationChanged;

    #endregion

    #region Fields

    private bool _isDragging;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isDraggingStart;
#pragma warning restore CS0414
    private Point _dragStartPoint;
    private bool _isUpdatingUI;
    private readonly Line[] _gridLines = new Line[5];

    #endregion

    public FadeCurveEditor()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeGridLines();
        UpdateFadeTypeComboBox();
        UpdateDurationTextBox();
        InvalidateCurve();
    }

    #region Property Changed Callbacks

    private static void OnFadeTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.UpdateFadeTypeComboBox();
            editor.InvalidateCurve();
            editor.FadeTypeChanged?.Invoke(editor, editor.FadeType);
        }
    }

    private static void OnDurationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.UpdateDurationTextBox();
            editor.InvalidateCurve();
            editor.DurationChanged?.Invoke(editor, editor.Duration);
        }
    }

    private static void OnFadeDirectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.InvalidateCurve();
        }
    }

    private static void OnShowHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            var visibility = editor.ShowHandles ? Visibility.Visible : Visibility.Collapsed;
            editor.StartHandle.Visibility = visibility;
            editor.EndHandle.Visibility = visibility;
        }
    }

    private static void OnTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FadeCurveEditor editor)
        {
            editor.TitleText.Text = editor.Title;
        }
    }

    #endregion

    #region Rendering

    private void InitializeGridLines()
    {
        var gridBrush = (SolidColorBrush)Resources["GridLineBrush"];

        for (var i = 0; i < _gridLines.Length; i++)
        {
            _gridLines[i] = new Line
            {
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection([2, 2]),
                IsHitTestVisible = false
            };
            CurveCanvas.Children.Insert(0, _gridLines[i]);
        }
    }

    private void InvalidateCurve()
    {
        if (!IsLoaded) return;

        var width = CurveCanvas.ActualWidth;
        var height = CurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        UpdateGridLines(width, height);
        RenderCurve(width, height);
        UpdateHandlePositions(width, height);
    }

    private void UpdateGridLines(double width, double height)
    {
        // Horizontal grid lines (25%, 50%, 75%)
        for (var i = 0; i < 3; i++)
        {
            var y = height * (i + 1) / 4;
            _gridLines[i].X1 = 0;
            _gridLines[i].X2 = width;
            _gridLines[i].Y1 = y;
            _gridLines[i].Y2 = y;
        }

        // Vertical grid lines (33%, 66%)
        for (var i = 0; i < 2; i++)
        {
            var x = width * (i + 1) / 3;
            _gridLines[i + 3].X1 = x;
            _gridLines[i + 3].X2 = x;
            _gridLines[i + 3].Y1 = 0;
            _gridLines[i + 3].Y2 = height;
        }
    }

    private void RenderCurve(double width, double height)
    {
        const int segments = 50;
        var points = new Point[segments + 1];

        for (var i = 0; i <= segments; i++)
        {
            var t = (double)i / segments;
            var curveValue = CalculateCurveValue(t, FadeType);

            // For fade-out, invert the curve
            if (!IsFadeIn)
            {
                curveValue = 1.0 - curveValue;
                t = 1.0 - t;
            }

            var x = t * width;
            var y = (1.0 - curveValue) * height; // Invert Y since canvas origin is top-left

            points[IsFadeIn ? i : segments - i] = new Point(x, y);
        }

        // Create fill geometry
        var fillGeometry = new StreamGeometry();
        using (var context = fillGeometry.Open())
        {
            context.BeginFigure(new Point(0, height), true, true);

            foreach (var point in points)
            {
                context.LineTo(point, true, false);
            }

            context.LineTo(new Point(width, height), true, false);
        }
        fillGeometry.Freeze();
        CurveFillPath.Data = fillGeometry;

        // Create stroke geometry
        var strokeGeometry = new StreamGeometry();
        using (var context = strokeGeometry.Open())
        {
            context.BeginFigure(points[0], false, false);

            for (var i = 1; i < points.Length; i++)
            {
                context.LineTo(points[i], true, false);
            }
        }
        strokeGeometry.Freeze();
        CurveStrokePath.Data = strokeGeometry;
    }

    private static double CalculateCurveValue(double t, FadeType fadeType)
    {
        t = Math.Clamp(t, 0, 1);

        return fadeType switch
        {
            FadeType.Linear => t,
            FadeType.Exponential => t * t,
            FadeType.Logarithmic => Math.Sqrt(t),
            FadeType.SCurve => t * t * (3 - 2 * t),
            FadeType.EqualPower => Math.Sin(t * Math.PI / 2),
            _ => t
        };
    }

    private void UpdateHandlePositions(double width, double height)
    {
        if (!ShowHandles) return;

        // Start handle at (0, bottom for fade-in or top for fade-out)
        var startY = IsFadeIn ? height - 5 : 5;
        Canvas.SetLeft(StartHandle, -5);
        Canvas.SetTop(StartHandle, startY - 5);

        // End handle at (width, top for fade-in or bottom for fade-out)
        var endY = IsFadeIn ? 5 : height - 5;
        Canvas.SetLeft(EndHandle, width - 5);
        Canvas.SetTop(EndHandle, endY - 5);
    }

    #endregion

    #region UI Updates

    private void UpdateFadeTypeComboBox()
    {
        if (_isUpdatingUI) return;
        _isUpdatingUI = true;

        var index = FadeType switch
        {
            FadeType.Linear => 0,
            FadeType.Exponential => 1,
            FadeType.SCurve => 2,
            FadeType.Logarithmic => 3,
            FadeType.EqualPower => 4,
            _ => 0
        };

        FadeTypeComboBox.SelectedIndex = index;
        _isUpdatingUI = false;
    }

    private void UpdateDurationTextBox()
    {
        if (_isUpdatingUI) return;
        _isUpdatingUI = true;

        DurationTextBox.Text = Duration.ToString("F2");
        _isUpdatingUI = false;
    }

    #endregion

    #region Event Handlers

    private void CurveCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCurve();
    }

    private void FadeTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (FadeTypeComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagString)
        {
            if (Enum.TryParse<FadeType>(tagString, out var fadeType))
            {
                FadeType = fadeType;
            }
        }
    }

    private void DurationTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingUI) return;

        if (double.TryParse(DurationTextBox.Text, out var duration) && duration >= 0)
        {
            Duration = duration;
        }
    }

    private void CurveCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShowHandles) return;

        var position = e.GetPosition(CurveCanvas);
        _dragStartPoint = position;

        // Check if clicking on handles
        var startHandlePos = new Point(Canvas.GetLeft(StartHandle) + 5, Canvas.GetTop(StartHandle) + 5);
        var endHandlePos = new Point(Canvas.GetLeft(EndHandle) + 5, Canvas.GetTop(EndHandle) + 5);

        if (IsNearPoint(position, startHandlePos, 10))
        {
            _isDragging = true;
            _isDraggingStart = true;
            CurveCanvas.CaptureMouse();
        }
        else if (IsNearPoint(position, endHandlePos, 10))
        {
            _isDragging = true;
            _isDraggingStart = false;
            CurveCanvas.CaptureMouse();
        }
    }

    private void CurveCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var position = e.GetPosition(CurveCanvas);

        if (_isDragging)
        {
            // Handle dragging logic - could modify curve shape if needed
            // For now, just update preview line
            PreviewLine.X1 = _dragStartPoint.X;
            PreviewLine.Y1 = _dragStartPoint.Y;
            PreviewLine.X2 = position.X;
            PreviewLine.Y2 = position.Y;
            PreviewLine.Visibility = Visibility.Visible;
        }
        else if (ShowHandles)
        {
            // Hover feedback
            var startHandlePos = new Point(Canvas.GetLeft(StartHandle) + 5, Canvas.GetTop(StartHandle) + 5);
            var endHandlePos = new Point(Canvas.GetLeft(EndHandle) + 5, Canvas.GetTop(EndHandle) + 5);

            StartHandle.Fill = IsNearPoint(position, startHandlePos, 10)
                ? (SolidColorBrush)Resources["HandleHoverBrush"]
                : (SolidColorBrush)Resources["HandleBrush"];

            EndHandle.Fill = IsNearPoint(position, endHandlePos, 10)
                ? (SolidColorBrush)Resources["HandleHoverBrush"]
                : (SolidColorBrush)Resources["HandleBrush"];
        }
    }

    private void CurveCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            PreviewLine.Visibility = Visibility.Collapsed;
            CurveCanvas.ReleaseMouseCapture();
        }
    }

    private void CurveCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (ShowHandles)
        {
            StartHandle.Fill = (SolidColorBrush)Resources["HandleBrush"];
            EndHandle.Fill = (SolidColorBrush)Resources["HandleBrush"];
        }
    }

    private static bool IsNearPoint(Point a, Point b, double threshold)
    {
        return Math.Abs(a.X - b.X) < threshold && Math.Abs(a.Y - b.Y) < threshold;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the fade parameters.
    /// </summary>
    /// <param name="fadeType">The fade type.</param>
    /// <param name="duration">The duration in beats.</param>
    /// <param name="isFadeIn">Whether this is a fade-in.</param>
    public void SetFade(FadeType fadeType, double duration, bool isFadeIn)
    {
        _isUpdatingUI = true;

        FadeType = fadeType;
        Duration = duration;
        IsFadeIn = isFadeIn;

        UpdateFadeTypeComboBox();
        UpdateDurationTextBox();

        _isUpdatingUI = false;
        InvalidateCurve();
    }

    #endregion
}
