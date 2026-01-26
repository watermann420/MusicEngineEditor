using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// LUFS loudness history graph showing integrated, short-term, and momentary loudness over time.
/// </summary>
public partial class LoudnessGraphView : UserControl
{
    #region Constants

    private const double MinLufs = -60.0;
    private const double MaxLufs = 0.0;
    private const int MaxHistoryPoints = 10000;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty TargetLufsProperty =
        DependencyProperty.Register(nameof(TargetLufs), typeof(double), typeof(LoudnessGraphView),
            new PropertyMetadata(-14.0, OnTargetLufsChanged));

    public static readonly DependencyProperty IntegratedLoudnessProperty =
        DependencyProperty.Register(nameof(IntegratedLoudness), typeof(double), typeof(LoudnessGraphView),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty ShortTermLoudnessProperty =
        DependencyProperty.Register(nameof(ShortTermLoudness), typeof(double), typeof(LoudnessGraphView),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty MomentaryLoudnessProperty =
        DependencyProperty.Register(nameof(MomentaryLoudness), typeof(double), typeof(LoudnessGraphView),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public static readonly DependencyProperty TruePeakProperty =
        DependencyProperty.Register(nameof(TruePeak), typeof(double), typeof(LoudnessGraphView),
            new PropertyMetadata(double.NegativeInfinity, OnLoudnessChanged));

    public double TargetLufs
    {
        get => (double)GetValue(TargetLufsProperty);
        set => SetValue(TargetLufsProperty, value);
    }

    public double IntegratedLoudness
    {
        get => (double)GetValue(IntegratedLoudnessProperty);
        set => SetValue(IntegratedLoudnessProperty, value);
    }

    public double ShortTermLoudness
    {
        get => (double)GetValue(ShortTermLoudnessProperty);
        set => SetValue(ShortTermLoudnessProperty, value);
    }

    public double MomentaryLoudness
    {
        get => (double)GetValue(MomentaryLoudnessProperty);
        set => SetValue(MomentaryLoudnessProperty, value);
    }

    public double TruePeak
    {
        get => (double)GetValue(TruePeakProperty);
        set => SetValue(TruePeakProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private readonly List<LoudnessDataPoint> _history = new();
    private double _zoomLevel = 1.0;
    private double _scrollOffset;
    private bool _isDragging;
    private Point _lastDragPoint;
    private DateTime _startTime;

    // Colors
    private readonly Color _integratedColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private readonly Color _shortTermColor = Color.FromRgb(0x21, 0x96, 0xF3);
    private readonly Color _momentaryColor = Color.FromRgb(0x9C, 0x27, 0xB0);
    private readonly Color _truePeakColor = Color.FromRgb(0xF4, 0x43, 0x36);
    private readonly Color _targetColor = Color.FromRgb(0xFF, 0x98, 0x00);

    #endregion

    #region Constructor

    public LoudnessGraphView()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        _startTime = DateTime.Now;
        DrawAll();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawAll();
        }
    }

    private static void OnTargetLufsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoudnessGraphView view && view._isInitialized)
        {
            view.DrawTargetLine();
        }
    }

    private static void OnLoudnessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LoudnessGraphView view && view._isInitialized)
        {
            view.AddDataPoint();
            view.UpdateCurrentValues();
        }
    }

    private void TargetLufsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(TargetLufsTextBox.Text, out double value))
        {
            TargetLufs = Math.Clamp(value, -60, 0);
        }
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        ExportData();
    }

    private void CurveToggle_Changed(object sender, RoutedEventArgs e)
    {
        UpdateCurveVisibility();
        DrawCurves();
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _zoomLevel = e.NewValue;
        DrawCurves();
        DrawTimeAxis();
    }

    private void ZoomFitButton_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = 1.0;
        _scrollOffset = 0;
        DrawCurves();
        DrawTimeAxis();
    }

    private void GraphArea_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = e.Delta > 0 ? 0.5 : -0.5;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + delta, 1, 10);
        e.Handled = true;
    }

    private void GraphArea_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragPoint = e.GetPosition(sender as IInputElement);
        (sender as UIElement)?.CaptureMouse();
        e.Handled = true;
    }

    private void GraphArea_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(sender as IInputElement);
        var deltaX = currentPoint.X - _lastDragPoint.X;

        _scrollOffset = Math.Max(0, _scrollOffset - deltaX / _zoomLevel);
        _lastDragPoint = currentPoint;

        DrawCurves();
        DrawTimeAxis();
    }

    private void GraphArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        (sender as UIElement)?.ReleaseMouseCapture();
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawLufsScale();
        DrawTargetLine();
        DrawCurves();
        DrawTimeAxis();
        UpdateCurrentValues();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (LUFS)
        double[] lufsLines = { -6, -12, -18, -24, -36, -48 };
        foreach (var lufs in lufsLines)
        {
            double y = LufsToY(lufs, height);
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawLufsScale()
    {
        LufsScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 150;

        double[] lufsMarks = { 0, -6, -12, -18, -24, -36, -48, -60 };
        foreach (var lufs in lufsMarks)
        {
            double y = LufsToY(lufs, height);

            var label = new TextBlock
            {
                Text = lufs.ToString(),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            LufsScaleCanvas.Children.Add(label);
        }
    }

    private void DrawTargetLine()
    {
        TargetLineCanvas.Children.Clear();

        double width = TargetLineCanvas.ActualWidth;
        double height = TargetLineCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double y = LufsToY(TargetLufs, height);

        // Dashed target line
        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = y,
            X2 = width,
            Y2 = y,
            Stroke = new SolidColorBrush(_targetColor),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 8, 4 }
        };
        TargetLineCanvas.Children.Add(line);

        // Target label
        var label = new TextBlock
        {
            Text = $"Target: {TargetLufs:F0} LUFS",
            Foreground = new SolidColorBrush(_targetColor),
            FontSize = 9,
            Background = new SolidColorBrush(Color.FromArgb(0xC0, 0x1A, 0x1A, 0x1A)),
            Padding = new Thickness(4, 2, 4, 2)
        };
        Canvas.SetLeft(label, 8);
        Canvas.SetTop(label, y - 10);
        TargetLineCanvas.Children.Add(label);
    }

    private void DrawCurves()
    {
        IntegratedCurveCanvas.Children.Clear();
        ShortTermCurveCanvas.Children.Clear();
        MomentaryCurveCanvas.Children.Clear();
        TruePeakCanvas.Children.Clear();

        if (_history.Count < 2) return;

        double width = IntegratedCurveCanvas.ActualWidth;
        double height = IntegratedCurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate visible range
        int visiblePoints = (int)(width / _zoomLevel);
        int startIndex = Math.Max(0, _history.Count - visiblePoints - (int)_scrollOffset);
        int endIndex = Math.Min(_history.Count, startIndex + visiblePoints);

        // Draw curves
        if (ShowIntegratedToggle.IsChecked == true)
        {
            DrawCurve(IntegratedCurveCanvas, _integratedColor, startIndex, endIndex, p => p.Integrated, width, height);
        }

        if (ShowShortTermToggle.IsChecked == true)
        {
            DrawCurve(ShortTermCurveCanvas, _shortTermColor, startIndex, endIndex, p => p.ShortTerm, width, height);
        }

        if (ShowMomentaryToggle.IsChecked == true)
        {
            DrawCurve(MomentaryCurveCanvas, _momentaryColor, startIndex, endIndex, p => p.Momentary, width, height);
        }

        // Draw true peak markers
        if (ShowTruePeakToggle.IsChecked == true)
        {
            DrawTruePeakMarkers(startIndex, endIndex, width, height);
        }
    }

    private void DrawCurve(Canvas canvas, Color color, int startIndex, int endIndex,
        Func<LoudnessDataPoint, double> valueSelector, double width, double height)
    {
        if (endIndex - startIndex < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = startIndex; i < endIndex; i++)
            {
                double x = ((double)(i - startIndex) / (endIndex - startIndex - 1)) * width;
                double value = valueSelector(_history[i]);

                if (double.IsNegativeInfinity(value) || double.IsNaN(value))
                {
                    started = false;
                    continue;
                }

                double y = LufsToY(value, height);
                y = Math.Clamp(y, 0, height);

                if (!started)
                {
                    context.BeginFigure(new Point(x, y), false, false);
                    started = true;
                }
                else
                {
                    context.LineTo(new Point(x, y), true, true);
                }
            }
        }

        geometry.Freeze();

        var path = new Shapes.Path
        {
            Data = geometry,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round
        };

        canvas.Children.Add(path);
    }

    private void DrawTruePeakMarkers(int startIndex, int endIndex, double width, double height)
    {
        for (int i = startIndex; i < endIndex; i++)
        {
            var point = _history[i];
            if (point.TruePeak > -1.0) // Mark peaks above -1 dBTP
            {
                double x = ((double)(i - startIndex) / (endIndex - startIndex - 1)) * width;
                double y = LufsToY(Math.Max(-60, point.TruePeak * 2 - 60), height); // Scale peak to LUFS range

                var marker = new Shapes.Ellipse
                {
                    Width = 6,
                    Height = 6,
                    Fill = new SolidColorBrush(_truePeakColor)
                };

                Canvas.SetLeft(marker, x - 3);
                Canvas.SetTop(marker, Math.Min(height - 6, Math.Max(0, y - 3)));
                TruePeakCanvas.Children.Add(marker);
            }
        }
    }

    private void DrawTimeAxis()
    {
        TimeAxisCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double width = TimeAxisCanvas.ActualWidth;

        if (width <= 0 || _history.Count < 2) return;

        // Calculate time range
        var startTime = _history[0].Timestamp;
        var endTime = _history[^1].Timestamp;
        var totalDuration = endTime - startTime;

        // Draw time labels
        int labelCount = 5;
        for (int i = 0; i <= labelCount; i++)
        {
            double x = width * i / labelCount;
            var time = startTime + TimeSpan.FromTicks((long)(totalDuration.Ticks * i / labelCount));

            var label = new TextBlock
            {
                Text = FormatTime(time - _startTime),
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 0);
            TimeAxisCanvas.Children.Add(label);
        }
    }

    private void UpdateCurrentValues()
    {
        // Update value displays
        IntegratedValueText.Text = FormatLufs(IntegratedLoudness);
        ShortTermValueText.Text = FormatLufs(ShortTermLoudness);
        MomentaryValueText.Text = FormatLufs(MomentaryLoudness);
        TruePeakValueText.Text = double.IsNegativeInfinity(TruePeak) ? "-- dBTP" : $"{TruePeak:F1} dBTP";
    }

    private void UpdateCurveVisibility()
    {
        IntegratedCurveCanvas.Visibility = ShowIntegratedToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        ShortTermCurveCanvas.Visibility = ShowShortTermToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        MomentaryCurveCanvas.Visibility = ShowMomentaryToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
        TruePeakCanvas.Visibility = ShowTruePeakToggle.IsChecked == true
            ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region Data Management

    private void AddDataPoint()
    {
        var point = new LoudnessDataPoint
        {
            Timestamp = DateTime.Now,
            Integrated = IntegratedLoudness,
            ShortTerm = ShortTermLoudness,
            Momentary = MomentaryLoudness,
            TruePeak = TruePeak
        };

        _history.Add(point);

        // Limit history size
        while (_history.Count > MaxHistoryPoints)
        {
            _history.RemoveAt(0);
        }

        DrawCurves();
        DrawTimeAxis();
    }

    #endregion

    #region Coordinate Conversions

    private static double LufsToY(double lufs, double height)
    {
        double normalized = (lufs - MinLufs) / (MaxLufs - MinLufs);
        return height * (1 - normalized);
    }

    #endregion

    #region Helpers

    private static string FormatLufs(double lufs)
    {
        if (double.IsNegativeInfinity(lufs) || double.IsNaN(lufs))
            return "-- LUFS";
        return $"{lufs:F1} LUFS";
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}";
        return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the loudness history.
    /// </summary>
    public void Reset()
    {
        _history.Clear();
        _startTime = DateTime.Now;
        _scrollOffset = 0;
        DrawCurves();
        DrawTimeAxis();
    }

    /// <summary>
    /// Adds a loudness measurement point.
    /// </summary>
    public void AddMeasurement(double integrated, double shortTerm, double momentary, double truePeak)
    {
        IntegratedLoudness = integrated;
        ShortTermLoudness = shortTerm;
        MomentaryLoudness = momentary;
        TruePeak = truePeak;
    }

    /// <summary>
    /// Gets the loudness history data.
    /// </summary>
    public IReadOnlyList<LoudnessDataPoint> GetHistory() => _history.AsReadOnly();

    /// <summary>
    /// Exports the loudness data to a CSV file.
    /// </summary>
    private void ExportData()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Loudness Data",
            Filter = "CSV Files|*.csv|Text Files|*.txt",
            DefaultExt = "csv",
            FileName = "loudness_data"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Time,Integrated (LUFS),Short-term (LUFS),Momentary (LUFS),True Peak (dBTP)");

            foreach (var point in _history)
            {
                var time = (point.Timestamp - _startTime).TotalSeconds;
                sb.AppendLine($"{time:F2},{FormatValue(point.Integrated)},{FormatValue(point.ShortTerm)},{FormatValue(point.Momentary)},{FormatValue(point.TruePeak)}");
            }

            File.WriteAllText(dialog.FileName, sb.ToString());

            MessageBox.Show($"Data exported to:\n{dialog.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export data:\n{ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string FormatValue(double value)
    {
        if (double.IsNegativeInfinity(value) || double.IsNaN(value))
            return "";
        return value.ToString("F2");
    }

    #endregion
}

/// <summary>
/// Represents a single loudness measurement point.
/// </summary>
public class LoudnessDataPoint
{
    public DateTime Timestamp { get; set; }
    public double Integrated { get; set; }
    public double ShortTerm { get; set; }
    public double Momentary { get; set; }
    public double TruePeak { get; set; }
}
