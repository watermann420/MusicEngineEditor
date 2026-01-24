using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Goniometer/Vectorscope display control for stereo image visualization.
/// Shows Lissajous pattern with L/R and M/S axis lines.
/// </summary>
public partial class GoniometerDisplay : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty PointsProperty =
        DependencyProperty.Register(nameof(Points), typeof(GoniometerPoint[]), typeof(GoniometerDisplay),
            new PropertyMetadata(null, OnPointsChanged));

    public static readonly DependencyProperty ShowGridLinesProperty =
        DependencyProperty.Register(nameof(ShowGridLines), typeof(bool), typeof(GoniometerDisplay),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ShowLabelsProperty =
        DependencyProperty.Register(nameof(ShowLabels), typeof(bool), typeof(GoniometerDisplay),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty UseCircularBoundsProperty =
        DependencyProperty.Register(nameof(UseCircularBounds), typeof(bool), typeof(GoniometerDisplay),
            new PropertyMetadata(false, OnLayoutPropertyChanged));

    public static readonly DependencyProperty PointColorProperty =
        DependencyProperty.Register(nameof(PointColor), typeof(Color), typeof(GoniometerDisplay),
            new PropertyMetadata(Color.FromRgb(0x00, 0xCE, 0xD1)));

    public static readonly DependencyProperty DecayFactorProperty =
        DependencyProperty.Register(nameof(DecayFactor), typeof(double), typeof(GoniometerDisplay),
            new PropertyMetadata(0.95));

    /// <summary>
    /// Gets or sets the goniometer points to display.
    /// </summary>
    public GoniometerPoint[] Points
    {
        get => (GoniometerPoint[])GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show grid lines.
    /// </summary>
    public bool ShowGridLines
    {
        get => (bool)GetValue(ShowGridLinesProperty);
        set => SetValue(ShowGridLinesProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show L/R and M/S labels.
    /// </summary>
    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use circular bounds (vs rectangular).
    /// </summary>
    public bool UseCircularBounds
    {
        get => (bool)GetValue(UseCircularBoundsProperty);
        set => SetValue(UseCircularBoundsProperty, value);
    }

    /// <summary>
    /// Gets or sets the point color.
    /// </summary>
    public Color PointColor
    {
        get => (Color)GetValue(PointColorProperty);
        set => SetValue(PointColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the point decay factor.
    /// </summary>
    public double DecayFactor
    {
        get => (double)GetValue(DecayFactorProperty);
        set => SetValue(DecayFactorProperty, value);
    }

    #endregion

    #region Private Fields

    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private byte[]? _pixelBuffer;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public GoniometerDisplay()
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
        InitializeBitmap();
        DrawGrid();
        DrawLabels();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _bitmap = null;
        _pixelBuffer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeBitmap();
            DrawGrid();
            DrawLabels();
        }
    }

    private static void OnPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GoniometerDisplay display && display._isInitialized)
        {
            display.UpdatePoints();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is GoniometerDisplay display && display._isInitialized)
        {
            display.DrawGrid();
            display.DrawLabels();
        }
    }

    #endregion

    #region Bitmap Initialization

    private void InitializeBitmap()
    {
        int width = (int)Math.Max(1, ActualWidth);
        int height = (int)Math.Max(1, ActualHeight);

        if (width == _bitmapWidth && height == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = width;
        _bitmapHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[width * height * 4];
        PointsImage.Source = _bitmap;

        ClearBitmap();
    }

    private void ClearBitmap()
    {
        if (_pixelBuffer == null || _bitmap == null) return;

        // Clear to transparent
        Array.Clear(_pixelBuffer, 0, _pixelBuffer.Length);
        UpdateBitmap();
    }

    private void UpdateBitmap()
    {
        if (_bitmap == null || _pixelBuffer == null) return;

        _bitmap.Lock();
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(
                _pixelBuffer, 0, _bitmap.BackBuffer, _pixelBuffer.Length);
            _bitmap.AddDirtyRect(new Int32Rect(0, 0, _bitmapWidth, _bitmapHeight));
        }
        finally
        {
            _bitmap.Unlock();
        }
    }

    #endregion

    #region Drawing

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        if (!ShowGridLines) return;

        double width = ActualWidth;
        double height = ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;

        Brush gridBrush = FindResource("GridBrush") as Brush ?? Brushes.DarkGray;
        Brush axisBrush = FindResource("AxisBrush") as Brush ?? Brushes.Gray;

        // Draw circular bounds if enabled
        if (UseCircularBounds)
        {
            double radius = Math.Min(width, height) / 2 - 2;
            var circle = new Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = gridBrush,
                StrokeThickness = 1,
                Fill = Brushes.Transparent
            };
            Canvas.SetLeft(circle, centerX - radius);
            Canvas.SetTop(circle, centerY - radius);
            GridCanvas.Children.Add(circle);

            // Inner circles at 50% and 25%
            foreach (double factor in new[] { 0.5, 0.25 })
            {
                var innerCircle = new Shapes.Ellipse
                {
                    Width = radius * 2 * factor,
                    Height = radius * 2 * factor,
                    Stroke = gridBrush,
                    StrokeThickness = 0.5,
                    StrokeDashArray = new DoubleCollection { 2, 2 },
                    Fill = Brushes.Transparent
                };
                Canvas.SetLeft(innerCircle, centerX - radius * factor);
                Canvas.SetTop(innerCircle, centerY - radius * factor);
                GridCanvas.Children.Add(innerCircle);
            }
        }

        // L/R axis (horizontal - at 45 degrees in standard goniometer)
        // In standard goniometer display, L is top-left, R is top-right
        double axisLength = Math.Min(width, height) / 2 - 4;

        // Left channel axis (45 degrees from vertical, going up-left)
        var lAxis = new Shapes.Line
        {
            X1 = centerX,
            Y1 = centerY,
            X2 = centerX - axisLength * 0.707,
            Y2 = centerY - axisLength * 0.707,
            Stroke = axisBrush,
            StrokeThickness = 1
        };
        GridCanvas.Children.Add(lAxis);

        // Right channel axis (45 degrees from vertical, going up-right)
        var rAxis = new Shapes.Line
        {
            X1 = centerX,
            Y1 = centerY,
            X2 = centerX + axisLength * 0.707,
            Y2 = centerY - axisLength * 0.707,
            Stroke = axisBrush,
            StrokeThickness = 1
        };
        GridCanvas.Children.Add(rAxis);

        // M axis (vertical - mono signal goes here)
        var mAxis = new Shapes.Line
        {
            X1 = centerX,
            Y1 = centerY - axisLength,
            X2 = centerX,
            Y2 = centerY + axisLength,
            Stroke = gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        GridCanvas.Children.Add(mAxis);

        // S axis (horizontal - side signal goes here)
        var sAxis = new Shapes.Line
        {
            X1 = centerX - axisLength,
            Y1 = centerY,
            X2 = centerX + axisLength,
            Y2 = centerY,
            Stroke = gridBrush,
            StrokeThickness = 0.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        GridCanvas.Children.Add(sAxis);

        // Center dot
        var centerDot = new Shapes.Ellipse
        {
            Width = 4,
            Height = 4,
            Fill = axisBrush
        };
        Canvas.SetLeft(centerDot, centerX - 2);
        Canvas.SetTop(centerDot, centerY - 2);
        GridCanvas.Children.Add(centerDot);
    }

    private void DrawLabels()
    {
        LabelsCanvas.Children.Clear();

        if (!ShowLabels) return;

        double width = ActualWidth;
        double height = ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double axisLength = Math.Min(width, height) / 2 - 4;

        Brush textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;

        // L label (top-left)
        var lLabel = new TextBlock
        {
            Text = "L",
            Foreground = textBrush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        Canvas.SetLeft(lLabel, centerX - axisLength * 0.707 - 12);
        Canvas.SetTop(lLabel, centerY - axisLength * 0.707 - 12);
        LabelsCanvas.Children.Add(lLabel);

        // R label (top-right)
        var rLabel = new TextBlock
        {
            Text = "R",
            Foreground = textBrush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        Canvas.SetLeft(rLabel, centerX + axisLength * 0.707 + 4);
        Canvas.SetTop(rLabel, centerY - axisLength * 0.707 - 12);
        LabelsCanvas.Children.Add(rLabel);

        // M label (top center)
        var mLabel = new TextBlock
        {
            Text = "M",
            Foreground = textBrush,
            FontSize = 9
        };
        Canvas.SetLeft(mLabel, centerX + 4);
        Canvas.SetTop(mLabel, 2);
        LabelsCanvas.Children.Add(mLabel);

        // S label (right center)
        var sLabel = new TextBlock
        {
            Text = "S",
            Foreground = textBrush,
            FontSize = 9
        };
        Canvas.SetLeft(sLabel, width - 12);
        Canvas.SetTop(sLabel, centerY - 6);
        LabelsCanvas.Children.Add(sLabel);

        // +/- labels for correlation indication
        var plusLabel = new TextBlock
        {
            Text = "+",
            Foreground = textBrush,
            FontSize = 9
        };
        Canvas.SetLeft(plusLabel, centerX - 6);
        Canvas.SetTop(plusLabel, 2);
        LabelsCanvas.Children.Add(plusLabel);

        var minusLabel = new TextBlock
        {
            Text = "-",
            Foreground = textBrush,
            FontSize = 9
        };
        Canvas.SetLeft(minusLabel, centerX - 4);
        Canvas.SetTop(minusLabel, height - 14);
        LabelsCanvas.Children.Add(minusLabel);
    }

    private void UpdatePoints()
    {
        if (_bitmap == null || _pixelBuffer == null || Points == null) return;

        double width = _bitmapWidth;
        double height = _bitmapHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double scale = Math.Min(width, height) / 2 - 4;

        // Apply decay to existing pixels
        ApplyDecay();

        Color pointColor = PointColor;

        // Draw new points
        foreach (var point in Points)
        {
            if (point.Intensity < 0.01f) continue;

            // Convert goniometer coordinates to screen coordinates
            // X is side (L-R), Y is mid (correlation)
            // Standard goniometer: L is top-left, R is top-right
            double screenX = centerX + point.X * scale;
            double screenY = centerY - point.Y * scale; // Invert Y for screen coordinates

            // Clamp to bounds
            int px = (int)Math.Clamp(screenX, 0, width - 1);
            int py = (int)Math.Clamp(screenY, 0, height - 1);

            // Calculate color with intensity
            byte alpha = (byte)(255 * Math.Min(1.0, point.Intensity * 1.5));
            byte r = (byte)(pointColor.R * point.Intensity);
            byte g = (byte)(pointColor.G * point.Intensity);
            byte b = (byte)(pointColor.B * point.Intensity);

            // Draw point (and surrounding pixels for visibility)
            SetPixel(px, py, b, g, r, alpha);
            if (point.Intensity > 0.5f)
            {
                SetPixelBlend(px - 1, py, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px + 1, py, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px, py - 1, b, g, r, (byte)(alpha / 2));
                SetPixelBlend(px, py + 1, b, g, r, (byte)(alpha / 2));
            }
        }

        UpdateBitmap();
    }

    private void ApplyDecay()
    {
        if (_pixelBuffer == null) return;

        double decay = DecayFactor;
        int length = _pixelBuffer.Length;

        for (int i = 0; i < length; i += 4)
        {
            // Decay alpha channel
            _pixelBuffer[i + 3] = (byte)(_pixelBuffer[i + 3] * decay);

            // Also decay RGB slightly
            _pixelBuffer[i] = (byte)(_pixelBuffer[i] * decay);
            _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * decay);
            _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * decay);
        }
    }

    private void SetPixel(int x, int y, byte b, byte g, byte r, byte a)
    {
        if (_pixelBuffer == null) return;
        if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight) return;

        int index = (y * _bitmapWidth + x) * 4;
        _pixelBuffer[index] = b;
        _pixelBuffer[index + 1] = g;
        _pixelBuffer[index + 2] = r;
        _pixelBuffer[index + 3] = a;
    }

    private void SetPixelBlend(int x, int y, byte b, byte g, byte r, byte a)
    {
        if (_pixelBuffer == null) return;
        if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight) return;

        int index = (y * _bitmapWidth + x) * 4;

        // Additive blend
        _pixelBuffer[index] = (byte)Math.Min(255, _pixelBuffer[index] + b);
        _pixelBuffer[index + 1] = (byte)Math.Min(255, _pixelBuffer[index + 1] + g);
        _pixelBuffer[index + 2] = (byte)Math.Min(255, _pixelBuffer[index + 2] + r);
        _pixelBuffer[index + 3] = (byte)Math.Min(255, _pixelBuffer[index + 3] + a);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        ClearBitmap();
    }

    #endregion
}
