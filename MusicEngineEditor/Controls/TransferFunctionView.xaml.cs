// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Transfer function display showing EQ/compressor frequency response curves.
/// Supports before/after comparison and phase display.
/// </summary>
public partial class TransferFunctionView : UserControl
{
    #region Constants

    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const double MinDb = -24.0;
    private const double MaxDb = 24.0;
    private const double MinPhase = -180.0;
    private const double MaxPhase = 180.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BeforeCurveProperty =
        DependencyProperty.Register(nameof(BeforeCurve), typeof(double[]), typeof(TransferFunctionView),
            new PropertyMetadata(null, OnCurveChanged));

    public static readonly DependencyProperty AfterCurveProperty =
        DependencyProperty.Register(nameof(AfterCurve), typeof(double[]), typeof(TransferFunctionView),
            new PropertyMetadata(null, OnCurveChanged));

    public static readonly DependencyProperty PhaseCurveProperty =
        DependencyProperty.Register(nameof(PhaseCurve), typeof(double[]), typeof(TransferFunctionView),
            new PropertyMetadata(null, OnCurveChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(double[]), typeof(TransferFunctionView),
            new PropertyMetadata(null));

    public double[]? BeforeCurve
    {
        get => (double[]?)GetValue(BeforeCurveProperty);
        set => SetValue(BeforeCurveProperty, value);
    }

    public double[]? AfterCurve
    {
        get => (double[]?)GetValue(AfterCurveProperty);
        set => SetValue(AfterCurveProperty, value);
    }

    public double[]? PhaseCurve
    {
        get => (double[]?)GetValue(PhaseCurveProperty);
        set => SetValue(PhaseCurveProperty, value);
    }

    public double[]? Frequencies
    {
        get => (double[]?)GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _showBefore = true;
    private bool _showAfter = true;
    private bool _showPhase;

    // Colors
    private readonly Color _beforeColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private readonly Color _afterColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _phaseColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private readonly Color _unityColor = Color.FromRgb(0x5A, 0x5A, 0x5A);

    #endregion

    #region Constructor

    public TransferFunctionView()
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

    private static void OnCurveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TransferFunctionView view && view._isInitialized)
        {
            view.DrawCurves();
        }
    }

    private void ShowBeforeToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showBefore = ShowBeforeToggle.IsChecked ?? false;
        DrawCurves();
    }

    private void ShowAfterToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showAfter = ShowAfterToggle.IsChecked ?? false;
        DrawCurves();
    }

    private void ShowPhaseToggle_Changed(object sender, RoutedEventArgs e)
    {
        _showPhase = ShowPhaseToggle.IsChecked ?? false;
        PhaseCurveCanvas.Visibility = _showPhase ? Visibility.Visible : Visibility.Collapsed;
        PhaseLegend.Visibility = _showPhase ? Visibility.Visible : Visibility.Collapsed;
        DrawCurves();
    }

    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        ExportAsImage();
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawUnityLine();
        DrawDbScale();
        DrawFrequencyLabels();
        DrawCurves();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (dB)
        double[] dbLines = { -24, -18, -12, -6, 0, 6, 12, 18, 24 };
        foreach (var db in dbLines)
        {
            double y = DbToY(db, height);
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = db == 0 ? 1 : 0.5,
                StrokeDashArray = db == 0 ? null : new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines (frequency, log scale)
        double[] freqLines = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in freqLines)
        {
            double x = FrequencyToX(freq, width);
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = freq == 1000 ? 0.75 : 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawUnityLine()
    {
        UnityLineCanvas.Children.Clear();

        double width = UnityLineCanvas.ActualWidth;
        double height = UnityLineCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double y = DbToY(0, height);
        var line = new Shapes.Line
        {
            X1 = 0,
            Y1 = y,
            X2 = width,
            Y2 = y,
            Stroke = new SolidColorBrush(_unityColor),
            StrokeThickness = 1.5
        };
        UnityLineCanvas.Children.Add(line);
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 150;

        double[] dbMarks = { 24, 12, 6, 0, -6, -12, -24 };
        foreach (var db in dbMarks)
        {
            double y = DbToY(db, height);

            var label = new TextBlock
            {
                Text = db >= 0 ? $"+{db}" : db.ToString(),
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }
    }

    private void DrawFrequencyLabels()
    {
        FrequencyLabelsCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double width = GridCanvas.ActualWidth;

        if (width <= 0) return;

        double[] frequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in frequencies)
        {
            double x = FrequencyToX(freq, width);
            string text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString();

            var label = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            FrequencyLabelsCanvas.Children.Add(label);
        }
    }

    private void DrawCurves()
    {
        BeforeCurveCanvas.Children.Clear();
        AfterCurveCanvas.Children.Clear();
        PhaseCurveCanvas.Children.Clear();

        double width = BeforeCurveCanvas.ActualWidth;
        double height = BeforeCurveCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Draw before curve
        if (_showBefore && BeforeCurve != null && BeforeCurve.Length > 0)
        {
            DrawCurve(BeforeCurveCanvas, BeforeCurve, _beforeColor, width, height, false);
        }

        // Draw after curve
        if (_showAfter && AfterCurve != null && AfterCurve.Length > 0)
        {
            DrawCurve(AfterCurveCanvas, AfterCurve, _afterColor, width, height, false);
        }

        // Draw phase curve
        if (_showPhase && PhaseCurve != null && PhaseCurve.Length > 0)
        {
            DrawCurve(PhaseCurveCanvas, PhaseCurve, _phaseColor, width, height, true);
        }
    }

    private void DrawCurve(Canvas canvas, double[] values, Color color, double width, double height, bool isPhase)
    {
        if (values.Length < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = 0; i < values.Length; i++)
            {
                // Calculate frequency (log scale)
                double freqRatio = (double)i / (values.Length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                // Calculate Y position
                double y;
                if (isPhase)
                {
                    y = PhaseToY(values[i], height);
                }
                else
                {
                    y = DbToY(values[i], height);
                }

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
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };

        if (isPhase)
        {
            path.StrokeDashArray = new DoubleCollection { 4, 2 };
        }

        canvas.Children.Add(path);
    }

    #endregion

    #region Coordinate Conversions

    private static double FrequencyToX(double frequency, double width)
    {
        double logMin = Math.Log10(MinFrequency);
        double logMax = Math.Log10(MaxFrequency);
        double logFreq = Math.Log10(Math.Clamp(frequency, MinFrequency, MaxFrequency));
        return ((logFreq - logMin) / (logMax - logMin)) * width;
    }

    private static double XToFrequency(double x, double width)
    {
        double logMin = Math.Log10(MinFrequency);
        double logMax = Math.Log10(MaxFrequency);
        double logFreq = logMin + (x / width) * (logMax - logMin);
        return Math.Pow(10, logFreq);
    }

    private static double DbToY(double db, double height)
    {
        double normalized = (db - MinDb) / (MaxDb - MinDb);
        return height * (1 - normalized);
    }

    private static double YToDb(double y, double height)
    {
        double normalized = 1 - (y / height);
        return MinDb + normalized * (MaxDb - MinDb);
    }

    private static double PhaseToY(double phase, double height)
    {
        double normalized = (phase - MinPhase) / (MaxPhase - MinPhase);
        return height * (1 - normalized);
    }

    #endregion

    #region Export

    private void ExportAsImage()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Transfer Function",
            Filter = "PNG Image|*.png|JPEG Image|*.jpg|BMP Image|*.bmp",
            DefaultExt = "png",
            FileName = "transfer_function"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            // Create a render target bitmap
            var grid = BeforeCurveCanvas.Parent as Grid;
            if (grid == null) return;

            var bounds = new Rect(0, 0, grid.ActualWidth, grid.ActualHeight);
            var dpi = 96.0;

            var renderBitmap = new RenderTargetBitmap(
                (int)(bounds.Width * dpi / 96),
                (int)(bounds.Height * dpi / 96),
                dpi, dpi, PixelFormats.Pbgra32);

            var visual = new DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                var brush = new VisualBrush(grid);
                context.DrawRectangle(brush, null, bounds);
            }

            renderBitmap.Render(visual);

            // Encode based on file extension
            BitmapEncoder encoder = Path.GetExtension(dialog.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
                ".bmp" => new BmpBitmapEncoder(),
                _ => new PngBitmapEncoder()
            };

            encoder.Frames.Add(BitmapFrame.Create(renderBitmap));

            using var stream = File.Create(dialog.FileName);
            encoder.Save(stream);

            MessageBox.Show($"Image exported to:\n{dialog.FileName}", "Export Complete",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export image:\n{ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the frequency response curves for display.
    /// </summary>
    /// <param name="frequencies">Array of frequencies (Hz)</param>
    /// <param name="beforeMagnitudes">Before processing magnitudes (dB)</param>
    /// <param name="afterMagnitudes">After processing magnitudes (dB)</param>
    /// <param name="phases">Phase response (degrees, optional)</param>
    public void SetCurves(double[] frequencies, double[] beforeMagnitudes, double[] afterMagnitudes, double[]? phases = null)
    {
        Frequencies = frequencies;
        BeforeCurve = beforeMagnitudes;
        AfterCurve = afterMagnitudes;
        PhaseCurve = phases;
    }

    /// <summary>
    /// Clears all curves.
    /// </summary>
    public void Clear()
    {
        BeforeCurve = null;
        AfterCurve = null;
        PhaseCurve = null;
    }

    #endregion
}
