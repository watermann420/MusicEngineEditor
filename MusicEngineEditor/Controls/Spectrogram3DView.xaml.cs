using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// 3D waterfall spectrogram display control.
/// Shows frequency spectrum over time with color gradient visualization.
/// </summary>
public partial class Spectrogram3DView : UserControl
{
    #region Constants

    private const int DefaultHistoryLength = 200;
    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;
    private const int FrequencyBands = 256;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MagnitudesProperty =
        DependencyProperty.Register(nameof(Magnitudes), typeof(float[]), typeof(Spectrogram3DView),
            new PropertyMetadata(null, OnMagnitudesChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(float[]), typeof(Spectrogram3DView),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HistoryLengthProperty =
        DependencyProperty.Register(nameof(HistoryLength), typeof(int), typeof(Spectrogram3DView),
            new PropertyMetadata(DefaultHistoryLength, OnHistoryLengthChanged));

    public static readonly DependencyProperty ZoomLevelProperty =
        DependencyProperty.Register(nameof(ZoomLevel), typeof(double), typeof(Spectrogram3DView),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty RotationAngleProperty =
        DependencyProperty.Register(nameof(RotationAngle), typeof(double), typeof(Spectrogram3DView),
            new PropertyMetadata(20.0));

    public float[] Magnitudes
    {
        get => (float[])GetValue(MagnitudesProperty);
        set => SetValue(MagnitudesProperty, value);
    }

    public float[] Frequencies
    {
        get => (float[])GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    public int HistoryLength
    {
        get => (int)GetValue(HistoryLengthProperty);
        set => SetValue(HistoryLengthProperty, value);
    }

    public double ZoomLevel
    {
        get => (double)GetValue(ZoomLevelProperty);
        set => SetValue(ZoomLevelProperty, value);
    }

    public double RotationAngle
    {
        get => (double)GetValue(RotationAngleProperty);
        set => SetValue(RotationAngleProperty, value);
    }

    #endregion

    #region Private Fields

    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private byte[]? _pixelBuffer;
    private Queue<float[]>? _history;
    private bool _isInitialized;
    private bool _isDragging;
    private Point _lastDragPoint;
    private double _scrollOffset;
    private readonly List<(int frequency, double magnitude)> _peakMarkers = new();

    // Color gradient cache (256 colors from blue to red)
    private readonly Color[] _colorGradient = new Color[256];

    #endregion

    #region Constructor

    public Spectrogram3DView()
    {
        InitializeComponent();
        InitializeColorGradient();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Initialization

    private void InitializeColorGradient()
    {
        // Create smooth gradient from blue to cyan to green to yellow to orange to red
        for (int i = 0; i < 256; i++)
        {
            double t = i / 255.0;
            Color color;

            if (t < 0.2)
            {
                // Dark blue to blue
                double localT = t / 0.2;
                color = InterpolateColor(Color.FromRgb(0, 0, 64), Color.FromRgb(0, 0, 255), localT);
            }
            else if (t < 0.4)
            {
                // Blue to cyan
                double localT = (t - 0.2) / 0.2;
                color = InterpolateColor(Color.FromRgb(0, 0, 255), Color.FromRgb(0, 255, 255), localT);
            }
            else if (t < 0.5)
            {
                // Cyan to green
                double localT = (t - 0.4) / 0.1;
                color = InterpolateColor(Color.FromRgb(0, 255, 255), Color.FromRgb(0, 255, 0), localT);
            }
            else if (t < 0.7)
            {
                // Green to yellow
                double localT = (t - 0.5) / 0.2;
                color = InterpolateColor(Color.FromRgb(0, 255, 0), Color.FromRgb(255, 255, 0), localT);
            }
            else if (t < 0.85)
            {
                // Yellow to orange
                double localT = (t - 0.7) / 0.15;
                color = InterpolateColor(Color.FromRgb(255, 255, 0), Color.FromRgb(255, 128, 0), localT);
            }
            else
            {
                // Orange to red
                double localT = (t - 0.85) / 0.15;
                color = InterpolateColor(Color.FromRgb(255, 128, 0), Color.FromRgb(255, 0, 0), localT);
            }

            _colorGradient[i] = color;
        }
    }

    private static Color InterpolateColor(Color a, Color b, double t)
    {
        return Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t));
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeBitmap();
        InitializeHistory();
        DrawGrid();
        DrawFrequencyLabels();
        DrawTimeLabels();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _bitmap = null;
        _pixelBuffer = null;
        _history = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeBitmap();
            RenderSpectrogram();
            DrawGrid();
            DrawFrequencyLabels();
            DrawTimeLabels();
        }
    }

    private static void OnMagnitudesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DView view && view._isInitialized && e.NewValue is float[] magnitudes)
        {
            view.AddFrame(magnitudes);
        }
    }

    private static void OnHistoryLengthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DView view && view._isInitialized)
        {
            view.InitializeHistory();
            view.RenderSpectrogram();
        }
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ZoomLevel = e.NewValue;
        if (_isInitialized)
        {
            RenderSpectrogram();
        }
    }

    private void RotateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        RotationAngle = e.NewValue;
        if (_isInitialized)
        {
            RenderSpectrogram();
        }
    }

    private void ResetViewButton_Click(object sender, RoutedEventArgs e)
    {
        ZoomSlider.Value = 1.0;
        RotateSlider.Value = 20.0;
        _scrollOffset = 0;
        RenderSpectrogram();
    }

    private void ScrollLeftButton_Click(object sender, RoutedEventArgs e)
    {
        _scrollOffset = Math.Max(0, _scrollOffset - 10);
        RenderSpectrogram();
    }

    private void SpectrogramImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double delta = e.Delta > 0 ? 0.1 : -0.1;
        ZoomSlider.Value = Math.Clamp(ZoomSlider.Value + delta, 0.5, 3.0);
        e.Handled = true;
    }

    private void SpectrogramImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _lastDragPoint = e.GetPosition(SpectrogramImage);
        SpectrogramImage.CaptureMouse();
        e.Handled = true;
    }

    private void SpectrogramImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;

        var currentPoint = e.GetPosition(SpectrogramImage);
        var deltaX = currentPoint.X - _lastDragPoint.X;

        _scrollOffset = Math.Max(0, _scrollOffset - deltaX * 0.5);
        _lastDragPoint = currentPoint;

        RenderSpectrogram();
    }

    private void SpectrogramImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        SpectrogramImage.ReleaseMouseCapture();
    }

    #endregion

    #region Bitmap Management

    private void InitializeBitmap()
    {
        int width = (int)Math.Max(1, SpectrogramImage.ActualWidth > 0 ? SpectrogramImage.ActualWidth : 400);
        int height = (int)Math.Max(1, SpectrogramImage.ActualHeight > 0 ? SpectrogramImage.ActualHeight : 200);

        if (width == _bitmapWidth && height == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = width;
        _bitmapHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[width * height * 4];
        SpectrogramImage.Source = _bitmap;

        ClearBitmap();
    }

    private void InitializeHistory()
    {
        _history = new Queue<float[]>(HistoryLength);
    }

    private void ClearBitmap()
    {
        if (_pixelBuffer == null) return;

        // Fill with background color
        for (int i = 0; i < _pixelBuffer.Length; i += 4)
        {
            _pixelBuffer[i] = 0x1A;     // B
            _pixelBuffer[i + 1] = 0x1A; // G
            _pixelBuffer[i + 2] = 0x1A; // R
            _pixelBuffer[i + 3] = 0xFF; // A
        }

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

    #region Spectrogram Rendering

    private void AddFrame(float[] magnitudes)
    {
        if (_history == null) return;

        // Add new frame to history
        var frame = new float[magnitudes.Length];
        Array.Copy(magnitudes, frame, magnitudes.Length);

        if (_history.Count >= HistoryLength)
        {
            _history.Dequeue();
        }
        _history.Enqueue(frame);

        // Find peaks for markers
        UpdatePeakMarkers(magnitudes);

        // Render
        RenderSpectrogram();
    }

    private void RenderSpectrogram()
    {
        if (_bitmap == null || _pixelBuffer == null || _history == null || _history.Count == 0)
            return;

        ClearBitmap();

        var frames = _history.ToArray();
        int frameCount = frames.Length;
        int startFrame = (int)Math.Max(0, _scrollOffset);
        int visibleFrames = (int)(frameCount / ZoomLevel);

        double xStep = (double)_bitmapWidth / visibleFrames;
        double rotationRad = RotationAngle * Math.PI / 180.0;
        double perspective = Math.Tan(rotationRad);

        // Render from back to front for proper 3D effect
        for (int f = startFrame; f < Math.Min(frameCount, startFrame + visibleFrames); f++)
        {
            var frame = frames[f];
            int x = (int)((f - startFrame) * xStep);

            // Apply perspective transformation
            double depth = (double)(f - startFrame) / visibleFrames;
            int yOffset = (int)(depth * perspective * 30);

            for (int band = 0; band < Math.Min(frame.Length, FrequencyBands); band++)
            {
                double magnitude = frame[band];
                if (magnitude < 0.001) continue;

                // Convert to dB and normalize
                double db = 20 * Math.Log10(Math.Max(0.0001, magnitude));
                double normalized = Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0, 1);

                // Get color from gradient
                int colorIndex = (int)(normalized * 255);
                var color = _colorGradient[colorIndex];

                // Calculate Y position (frequency axis, log scale)
                double freqRatio = (double)band / Math.Min(frame.Length, FrequencyBands);
                int y = _bitmapHeight - 1 - (int)(freqRatio * _bitmapHeight) + yOffset;

                if (y >= 0 && y < _bitmapHeight && x >= 0 && x < _bitmapWidth)
                {
                    SetPixel(x, y, color.B, color.G, color.R, (byte)(255 * Math.Min(1.0, normalized + 0.3)));
                }
            }
        }

        UpdateBitmap();
        DrawPeakMarkers();
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

    #endregion

    #region Peak Detection

    private void UpdatePeakMarkers(float[] magnitudes)
    {
        _peakMarkers.Clear();

        // Simple peak detection - find local maxima above threshold
        for (int i = 2; i < magnitudes.Length - 2; i++)
        {
            if (magnitudes[i] > magnitudes[i - 1] &&
                magnitudes[i] > magnitudes[i - 2] &&
                magnitudes[i] > magnitudes[i + 1] &&
                magnitudes[i] > magnitudes[i + 2] &&
                magnitudes[i] > 0.1f)
            {
                int frequency = Frequencies != null && i < Frequencies.Length
                    ? (int)Frequencies[i]
                    : i * 44100 / (2 * magnitudes.Length);
                _peakMarkers.Add((frequency, magnitudes[i]));
            }
        }

        // Keep only top 5 peaks
        _peakMarkers.Sort((a, b) => b.magnitude.CompareTo(a.magnitude));
        while (_peakMarkers.Count > 5)
        {
            _peakMarkers.RemoveAt(_peakMarkers.Count - 1);
        }
    }

    private void DrawPeakMarkers()
    {
        PeakMarkersCanvas.Children.Clear();

        foreach (var (frequency, magnitude) in _peakMarkers)
        {
            // Calculate position based on frequency
            double freqRatio = Math.Log10(frequency / 20.0) / Math.Log10(20000.0 / 20.0);
            double y = (1 - freqRatio) * PeakMarkersCanvas.ActualHeight;

            var marker = new Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = new SolidColorBrush(Color.FromRgb(255, 255, 255)),
                Opacity = magnitude
            };

            Canvas.SetLeft(marker, PeakMarkersCanvas.ActualWidth - 10);
            Canvas.SetTop(marker, y - 3);
            PeakMarkersCanvas.Children.Add(marker);
        }
    }

    #endregion

    #region Drawing

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        // Horizontal grid lines (frequency)
        for (int i = 1; i < 5; i++)
        {
            double y = height * i / 5;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines (time)
        for (int i = 1; i < 5; i++)
        {
            double x = width * i / 5;
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawFrequencyLabels()
    {
        FrequencyLabelsCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = FrequencyLabelsCanvas.ActualHeight > 0 ? FrequencyLabelsCanvas.ActualHeight : 200;

        // Log scale frequency labels
        int[] frequencies = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };
        foreach (var freq in frequencies)
        {
            double freqRatio = Math.Log10(freq / 20.0) / Math.Log10(20000.0 / 20.0);
            double y = (1 - freqRatio) * height;

            var label = new TextBlock
            {
                Text = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString(),
                Foreground = textBrush,
                FontSize = 8,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            FrequencyLabelsCanvas.Children.Add(label);
        }
    }

    private void DrawTimeLabels()
    {
        TimeLabelsCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double width = TimeLabelsCanvas.ActualWidth;

        // Time labels based on history length (assuming ~20fps update rate)
        double totalSeconds = HistoryLength / 20.0;

        for (int i = 0; i <= 4; i++)
        {
            double x = width * i / 4;
            double seconds = totalSeconds * (1 - (double)i / 4);

            var label = new TextBlock
            {
                Text = $"-{seconds:F1}s",
                Foreground = textBrush,
                FontSize = 8
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            TimeLabelsCanvas.Children.Add(label);
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the spectrogram history.
    /// </summary>
    public void Clear()
    {
        _history?.Clear();
        _peakMarkers.Clear();
        ClearBitmap();
        PeakMarkersCanvas.Children.Clear();
    }

    /// <summary>
    /// Gets the current spectrogram as a BitmapSource for export.
    /// </summary>
    public BitmapSource? GetImage()
    {
        return _bitmap;
    }

    #endregion
}
