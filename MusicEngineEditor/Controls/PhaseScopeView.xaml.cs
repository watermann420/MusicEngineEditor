using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Lissajous-style phase scope display with correlation meter.
/// Shows stereo phase relationship and correlation coefficient.
/// </summary>
public partial class PhaseScopeView : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty LeftSamplesProperty =
        DependencyProperty.Register(nameof(LeftSamples), typeof(float[]), typeof(PhaseScopeView),
            new PropertyMetadata(null, OnSamplesChanged));

    public static readonly DependencyProperty RightSamplesProperty =
        DependencyProperty.Register(nameof(RightSamples), typeof(float[]), typeof(PhaseScopeView),
            new PropertyMetadata(null, OnSamplesChanged));

    public static readonly DependencyProperty CorrelationProperty =
        DependencyProperty.Register(nameof(Correlation), typeof(double), typeof(PhaseScopeView),
            new PropertyMetadata(0.0, OnCorrelationChanged));

    public static readonly DependencyProperty PhaseDifferenceProperty =
        DependencyProperty.Register(nameof(PhaseDifference), typeof(double), typeof(PhaseScopeView),
            new PropertyMetadata(0.0, OnPhaseDifferenceChanged));

    public static readonly DependencyProperty IsMonoModeProperty =
        DependencyProperty.Register(nameof(IsMonoMode), typeof(bool), typeof(PhaseScopeView),
            new PropertyMetadata(false));

    public static readonly DependencyProperty DecayFactorProperty =
        DependencyProperty.Register(nameof(DecayFactor), typeof(double), typeof(PhaseScopeView),
            new PropertyMetadata(0.95));

    public float[] LeftSamples
    {
        get => (float[])GetValue(LeftSamplesProperty);
        set => SetValue(LeftSamplesProperty, value);
    }

    public float[] RightSamples
    {
        get => (float[])GetValue(RightSamplesProperty);
        set => SetValue(RightSamplesProperty, value);
    }

    public double Correlation
    {
        get => (double)GetValue(CorrelationProperty);
        set => SetValue(CorrelationProperty, value);
    }

    public double PhaseDifference
    {
        get => (double)GetValue(PhaseDifferenceProperty);
        set => SetValue(PhaseDifferenceProperty, value);
    }

    public bool IsMonoMode
    {
        get => (bool)GetValue(IsMonoModeProperty);
        set => SetValue(IsMonoModeProperty, value);
    }

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

    // Level tracking
    private double _leftPeak;
    private double _rightPeak;

    // Colors
    private readonly Color _traceColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _monoColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private readonly Color _stereoColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private readonly Color _outOfPhaseColor = Color.FromRgb(0xF4, 0x43, 0x36);

    #endregion

    #region Constructor

    public PhaseScopeView()
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

    private static void OnSamplesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseScopeView view && view._isInitialized)
        {
            view.UpdateLissajous();
        }
    }

    private static void OnCorrelationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseScopeView view && view._isInitialized)
        {
            view.UpdateCorrelationMeter();
        }
    }

    private static void OnPhaseDifferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseScopeView view && view._isInitialized)
        {
            view.UpdatePhaseDifference();
        }
    }

    private void MonoModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsMonoMode = MonoModeToggle.IsChecked ?? false;
    }

    private void DecaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DecayFactor = e.NewValue;
    }

    #endregion

    #region Bitmap Management

    private void InitializeBitmap()
    {
        int width = (int)Math.Max(1, TraceImage.ActualWidth > 0 ? TraceImage.ActualWidth : 200);
        int height = (int)Math.Max(1, TraceImage.ActualHeight > 0 ? TraceImage.ActualHeight : 200);

        if (width == _bitmapWidth && height == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = width;
        _bitmapHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[width * height * 4];
        TraceImage.Source = _bitmap;

        ClearBitmap();
    }

    private void ClearBitmap()
    {
        if (_pixelBuffer == null) return;
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

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        var axisBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4D, 0x52));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 4;

        // Draw circular grid
        double[] radii = { 0.25, 0.5, 0.75, 1.0 };
        foreach (var r in radii)
        {
            var circle = new Shapes.Ellipse
            {
                Width = radius * 2 * r,
                Height = radius * 2 * r,
                Stroke = gridBrush,
                StrokeThickness = r == 1.0 ? 1 : 0.5,
                StrokeDashArray = r == 1.0 ? null : new DoubleCollection { 2, 2 }
            };
            Canvas.SetLeft(circle, centerX - radius * r);
            Canvas.SetTop(circle, centerY - radius * r);
            GridCanvas.Children.Add(circle);
        }

        // Draw L/R axes (diagonal)
        double axisLength = radius * 0.95;

        // L axis (top-left)
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

        // R axis (top-right)
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

        // M axis (vertical)
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

        // S axis (horizontal)
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

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double width = LabelsCanvas.ActualWidth;
        double height = LabelsCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double radius = Math.Min(width, height) / 2 - 4;

        // L label
        var lLabel = new TextBlock
        {
            Text = "L",
            Foreground = textBrush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        Canvas.SetLeft(lLabel, centerX - radius * 0.707 - 12);
        Canvas.SetTop(lLabel, centerY - radius * 0.707 - 12);
        LabelsCanvas.Children.Add(lLabel);

        // R label
        var rLabel = new TextBlock
        {
            Text = "R",
            Foreground = textBrush,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold
        };
        Canvas.SetLeft(rLabel, centerX + radius * 0.707 + 4);
        Canvas.SetTop(rLabel, centerY - radius * 0.707 - 12);
        LabelsCanvas.Children.Add(rLabel);

        // M/S labels
        var mLabel = new TextBlock { Text = "M", Foreground = textBrush, FontSize = 9 };
        Canvas.SetLeft(mLabel, centerX + 4);
        Canvas.SetTop(mLabel, 4);
        LabelsCanvas.Children.Add(mLabel);

        var sLabel = new TextBlock { Text = "S", Foreground = textBrush, FontSize = 9 };
        Canvas.SetLeft(sLabel, width - 12);
        Canvas.SetTop(sLabel, centerY - 6);
        LabelsCanvas.Children.Add(sLabel);
    }

    private void UpdateLissajous()
    {
        if (_bitmap == null || _pixelBuffer == null || LeftSamples == null || RightSamples == null)
            return;

        // Apply decay
        ApplyDecay();

        double width = _bitmapWidth;
        double height = _bitmapHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double scale = Math.Min(width, height) / 2 - 4;

        int sampleCount = Math.Min(LeftSamples.Length, RightSamples.Length);
        _leftPeak = 0;
        _rightPeak = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            float left = LeftSamples[i];
            float right = RightSamples[i];

            // Track peaks
            if (Math.Abs(left) > _leftPeak) _leftPeak = Math.Abs(left);
            if (Math.Abs(right) > _rightPeak) _rightPeak = Math.Abs(right);

            // Lissajous coordinates
            // X = (R - L) / 2 (Side signal)
            // Y = (L + R) / 2 (Mid signal)
            double x, y;

            if (IsMonoMode)
            {
                // Show L vs R directly
                x = right * scale;
                y = -left * scale; // Invert for screen coordinates
            }
            else
            {
                // Standard M/S Lissajous
                x = (right - left) / 2 * scale;
                y = -(left + right) / 2 * scale; // Invert for screen coordinates
            }

            int px = (int)Math.Clamp(centerX + x, 0, width - 1);
            int py = (int)Math.Clamp(centerY + y, 0, height - 1);

            // Intensity based on sample amplitude
            double intensity = Math.Sqrt(left * left + right * right);
            byte alpha = (byte)(200 * Math.Min(1.0, intensity * 2));

            SetPixel(px, py, _traceColor.B, _traceColor.G, _traceColor.R, alpha);

            // Draw slightly larger for visibility
            SetPixelBlend(px - 1, py, _traceColor.B, _traceColor.G, _traceColor.R, (byte)(alpha / 2));
            SetPixelBlend(px + 1, py, _traceColor.B, _traceColor.G, _traceColor.R, (byte)(alpha / 2));
            SetPixelBlend(px, py - 1, _traceColor.B, _traceColor.G, _traceColor.R, (byte)(alpha / 2));
            SetPixelBlend(px, py + 1, _traceColor.B, _traceColor.G, _traceColor.R, (byte)(alpha / 2));
        }

        UpdateBitmap();
        UpdateLevelDisplays();
    }

    private void ApplyDecay()
    {
        if (_pixelBuffer == null) return;

        double decay = DecayFactor;
        for (int i = 0; i < _pixelBuffer.Length; i += 4)
        {
            _pixelBuffer[i] = (byte)(_pixelBuffer[i] * decay);
            _pixelBuffer[i + 1] = (byte)(_pixelBuffer[i + 1] * decay);
            _pixelBuffer[i + 2] = (byte)(_pixelBuffer[i + 2] * decay);
            _pixelBuffer[i + 3] = (byte)(_pixelBuffer[i + 3] * decay);
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
        _pixelBuffer[index] = (byte)Math.Min(255, _pixelBuffer[index] + b);
        _pixelBuffer[index + 1] = (byte)Math.Min(255, _pixelBuffer[index + 1] + g);
        _pixelBuffer[index + 2] = (byte)Math.Min(255, _pixelBuffer[index + 2] + r);
        _pixelBuffer[index + 3] = (byte)Math.Min(255, _pixelBuffer[index + 3] + a);
    }

    private void UpdateCorrelationMeter()
    {
        double correlation = Correlation;

        // Update text
        CorrelationText.Text = $"{correlation:+0.00;-0.00}";

        // Update color based on correlation
        Color color;
        if (correlation > 0.5)
        {
            color = _monoColor; // Good mono compatibility
        }
        else if (correlation > 0)
        {
            color = _stereoColor; // Normal stereo
        }
        else
        {
            color = _outOfPhaseColor; // Out of phase warning
        }

        CorrelationText.Foreground = new SolidColorBrush(color);
        CorrelationFill.Background = new SolidColorBrush(color);
        CorrelationIndicator.Background = new SolidColorBrush(color);

        // Update indicator position and fill width
        double meterWidth = CorrelationFill.Parent is Border parent ? parent.ActualWidth : 100;
        double normalizedCorr = (correlation + 1) / 2; // Map -1..1 to 0..1
        double indicatorPos = normalizedCorr * meterWidth;

        CorrelationIndicator.Margin = new Thickness(indicatorPos - meterWidth / 2 - 1.5, 0, 0, 0);

        // Fill from center
        double fillWidth = Math.Abs(correlation) * meterWidth / 2;
        CorrelationFill.Width = fillWidth;
        CorrelationFill.HorizontalAlignment = correlation >= 0
            ? System.Windows.HorizontalAlignment.Right
            : System.Windows.HorizontalAlignment.Left;
        CorrelationFill.Margin = correlation >= 0
            ? new Thickness(0, 0, meterWidth / 2 - fillWidth, 0)
            : new Thickness(meterWidth / 2 - fillWidth, 0, 0, 0);
    }

    private void UpdatePhaseDifference()
    {
        double phase = PhaseDifference;
        PhaseDifferenceText.Text = $"{phase:F1}";

        // Color based on phase difference
        Color color;
        if (Math.Abs(phase) < 30)
        {
            color = _monoColor;
        }
        else if (Math.Abs(phase) < 90)
        {
            color = _stereoColor;
        }
        else
        {
            color = _outOfPhaseColor;
        }

        PhaseDifferenceText.Foreground = new SolidColorBrush(color);
    }

    private void UpdateLevelDisplays()
    {
        double leftDb = _leftPeak > 0 ? 20 * Math.Log10(_leftPeak) : double.NegativeInfinity;
        double rightDb = _rightPeak > 0 ? 20 * Math.Log10(_rightPeak) : double.NegativeInfinity;

        LeftLevelText.Text = double.IsNegativeInfinity(leftDb) ? "-inf" : $"{leftDb:F1}";
        RightLevelText.Text = double.IsNegativeInfinity(rightDb) ? "-inf" : $"{rightDb:F1}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the display.
    /// </summary>
    public void Clear()
    {
        ClearBitmap();
        _leftPeak = 0;
        _rightPeak = 0;
        UpdateLevelDisplays();
    }

    /// <summary>
    /// Updates the display with new audio samples.
    /// </summary>
    public void UpdateSamples(float[] leftSamples, float[] rightSamples)
    {
        LeftSamples = leftSamples;
        RightSamples = rightSamples;
    }

    #endregion
}
