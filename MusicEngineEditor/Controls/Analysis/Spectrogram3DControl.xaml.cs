// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: 3D waterfall spectrogram visualization control.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// 3D waterfall spectrogram display control with rotation, zoom, and color mapping.
/// Visualizes frequency content over time in a 3D perspective view.
/// </summary>
public partial class Spectrogram3DControl : UserControl
{
    #region Constants

    private const double DefaultRotationX = 45.0;
    private const double DefaultRotationY = -30.0;
    private const double DefaultZoom = 1.0;
    private const double MinZoom = 0.25;
    private const double MaxZoom = 4.0;
    private const double ZoomSensitivity = 0.001;
    private const double RotationSensitivity = 0.5;
    private const int DefaultHistoryFrames = 100;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SpectrogramAnalyzerProperty =
        DependencyProperty.Register(nameof(SpectrogramAnalyzer), typeof(Spectrogram3D), typeof(Spectrogram3DControl),
            new PropertyMetadata(null, OnAnalyzerChanged));

    public static readonly DependencyProperty IsActiveProperty =
        DependencyProperty.Register(nameof(IsActive), typeof(bool), typeof(Spectrogram3DControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ColorMapProperty =
        DependencyProperty.Register(nameof(ColorMap), typeof(SpectrogramColorMap), typeof(Spectrogram3DControl),
            new PropertyMetadata(SpectrogramColorMap.HeatMap, OnColorMapChanged));

    public static readonly DependencyProperty MinFrequencyProperty =
        DependencyProperty.Register(nameof(MinFrequency), typeof(float), typeof(Spectrogram3DControl),
            new PropertyMetadata(20f, OnFrequencyRangeChanged));

    public static readonly DependencyProperty MaxFrequencyProperty =
        DependencyProperty.Register(nameof(MaxFrequency), typeof(float), typeof(Spectrogram3DControl),
            new PropertyMetadata(20000f, OnFrequencyRangeChanged));

    public static readonly DependencyProperty HistorySecondsProperty =
        DependencyProperty.Register(nameof(HistorySeconds), typeof(float), typeof(Spectrogram3DControl),
            new PropertyMetadata(10f, OnHistorySecondsChanged));

    public static readonly DependencyProperty DbFloorProperty =
        DependencyProperty.Register(nameof(DbFloor), typeof(float), typeof(Spectrogram3DControl),
            new PropertyMetadata(-90f, OnDbRangeChanged));

    public static readonly DependencyProperty DbCeilingProperty =
        DependencyProperty.Register(nameof(DbCeiling), typeof(float), typeof(Spectrogram3DControl),
            new PropertyMetadata(0f, OnDbRangeChanged));

    public static readonly DependencyProperty EnablePeakHoldProperty =
        DependencyProperty.Register(nameof(EnablePeakHold), typeof(bool), typeof(Spectrogram3DControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty UseLogFrequencyProperty =
        DependencyProperty.Register(nameof(UseLogFrequency), typeof(bool), typeof(Spectrogram3DControl),
            new PropertyMetadata(true));

    /// <summary>
    /// Gets or sets the Spectrogram3D analyzer from MusicEngine.
    /// </summary>
    public Spectrogram3D? SpectrogramAnalyzer
    {
        get => (Spectrogram3D?)GetValue(SpectrogramAnalyzerProperty);
        set => SetValue(SpectrogramAnalyzerProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the control is actively processing.
    /// </summary>
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <summary>
    /// Gets or sets the color map for visualization.
    /// </summary>
    public SpectrogramColorMap ColorMap
    {
        get => (SpectrogramColorMap)GetValue(ColorMapProperty);
        set => SetValue(ColorMapProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum frequency in Hz.
    /// </summary>
    public float MinFrequency
    {
        get => (float)GetValue(MinFrequencyProperty);
        set => SetValue(MinFrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum frequency in Hz.
    /// </summary>
    public float MaxFrequency
    {
        get => (float)GetValue(MaxFrequencyProperty);
        set => SetValue(MaxFrequencyProperty, value);
    }

    /// <summary>
    /// Gets or sets the history length in seconds.
    /// </summary>
    public float HistorySeconds
    {
        get => (float)GetValue(HistorySecondsProperty);
        set => SetValue(HistorySecondsProperty, value);
    }

    /// <summary>
    /// Gets or sets the dB floor (minimum level).
    /// </summary>
    public float DbFloor
    {
        get => (float)GetValue(DbFloorProperty);
        set => SetValue(DbFloorProperty, value);
    }

    /// <summary>
    /// Gets or sets the dB ceiling (maximum level).
    /// </summary>
    public float DbCeiling
    {
        get => (float)GetValue(DbCeilingProperty);
        set => SetValue(DbCeilingProperty, value);
    }

    /// <summary>
    /// Gets or sets whether peak hold is enabled.
    /// </summary>
    public bool EnablePeakHold
    {
        get => (bool)GetValue(EnablePeakHoldProperty);
        set => SetValue(EnablePeakHoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use logarithmic frequency scale.
    /// </summary>
    public bool UseLogFrequency
    {
        get => (bool)GetValue(UseLogFrequencyProperty);
        set => SetValue(UseLogFrequencyProperty, value);
    }

    #endregion

    #region Private Fields

    private WriteableBitmap? _bitmap;
    private int _bitmapWidth;
    private int _bitmapHeight;
    private byte[]? _pixelBuffer;
    private bool _isInitialized;

    // Camera/view state
    private double _rotationX = DefaultRotationX;
    private double _rotationY = DefaultRotationY;
    private double _zoom = DefaultZoom;

    // Mouse interaction
    private bool _isDragging;
    private Point _lastMousePosition;

    // Frame history for rendering
    private readonly List<SpectrogramFrame> _frameHistory = new();
    private readonly object _frameLock = new();
    private int _maxHistoryFrames = DefaultHistoryFrames;

    // Colors
    private static readonly Color BackgroundColor = Color.FromRgb(0x0A, 0x0A, 0x0A);
    private static readonly Color GridColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private static readonly Color AxisColor = Color.FromRgb(0x4A, 0x4D, 0x52);
    private static readonly Color TextColor = Color.FromRgb(0x80, 0x80, 0x80);

    #endregion

    #region Constructor

    public Spectrogram3DControl()
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
        UpdateFrequencyRangeDisplay();
        Render();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        UnsubscribeFromAnalyzer();
        _bitmap = null;
        _pixelBuffer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            InitializeBitmap();
            Render();
        }
    }

    private static void OnAnalyzerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DControl control)
        {
            if (e.OldValue is Spectrogram3D oldAnalyzer)
            {
                oldAnalyzer.FrameGenerated -= control.OnFrameGenerated;
            }

            if (e.NewValue is Spectrogram3D newAnalyzer)
            {
                newAnalyzer.FrameGenerated += control.OnFrameGenerated;
                control.StatusText.Text = "Connected";
                control.InstructionsOverlay.Visibility = Visibility.Collapsed;
            }
            else
            {
                control.StatusText.Text = "No analyzer";
                control.InstructionsOverlay.Visibility = Visibility.Visible;
            }
        }
    }

    private static void OnColorMapChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DControl control && control._isInitialized)
        {
            control.Render();
        }
    }

    private static void OnFrequencyRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DControl control && control._isInitialized)
        {
            control.UpdateFrequencyRangeDisplay();
            control.Render();
        }
    }

    private static void OnHistorySecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DControl control)
        {
            control._maxHistoryFrames = (int)(control.HistorySeconds * 30f); // Assuming 30 fps
            control.TrimFrameHistory();
        }
    }

    private static void OnDbRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is Spectrogram3DControl control && control._isInitialized)
        {
            control.Render();
        }
    }

    private void OnFrameGenerated(object? sender, SpectrogramFrameEventArgs e)
    {
        if (!IsActive) return;

        lock (_frameLock)
        {
            _frameHistory.Add(e.Frame);
            TrimFrameHistory();
        }

        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_isInitialized)
            {
                Render();
            }
        }));
    }

    #endregion

    #region UI Event Handlers

    private void ColorMapComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ColorMapComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tagValue)
        {
            if (Enum.TryParse<SpectrogramColorMap>(tagValue, out var colorMap))
            {
                ColorMap = colorMap;
            }
        }
    }

    private void ResetViewButton_Click(object sender, RoutedEventArgs e)
    {
        ResetView();
    }

    private void MinFrequencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MinFrequency = (float)e.NewValue;
        MinFrequencyText.Text = FormatFrequency(MinFrequency);
        UpdateFrequencyRangeDisplay();
    }

    private void MaxFrequencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        MaxFrequency = (float)e.NewValue;
        MaxFrequencyText.Text = FormatFrequency(MaxFrequency);
        UpdateFrequencyRangeDisplay();
    }

    private void HistoryLengthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        HistorySeconds = (float)e.NewValue;
        HistoryLengthText.Text = $"{HistorySeconds:F1} s";
    }

    private void DbFloorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DbFloor = (float)e.NewValue;
        DbFloorText.Text = $"{DbFloor:F0} dB";
    }

    private void DbCeilingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        DbCeiling = (float)e.NewValue;
        DbCeilingText.Text = $"{DbCeiling:F0} dB";
    }

    private void PeakHoldCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        EnablePeakHold = PeakHoldCheckBox.IsChecked ?? false;
    }

    private void LogFrequencyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        UseLogFrequency = LogFrequencyCheckBox.IsChecked ?? true;
        if (_isInitialized) Render();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        Clear();
    }

    #endregion

    #region Mouse Interaction

    private void RenderImage_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _lastMousePosition = e.GetPosition(RenderImage);
            RenderImage.CaptureMouse();
        }
    }

    private void RenderImage_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            var currentPosition = e.GetPosition(RenderImage);
            double deltaX = currentPosition.X - _lastMousePosition.X;
            double deltaY = currentPosition.Y - _lastMousePosition.Y;

            _rotationY += deltaX * RotationSensitivity;
            _rotationX -= deltaY * RotationSensitivity;

            // Clamp rotation
            _rotationX = Math.Clamp(_rotationX, -89, 89);
            _rotationY = _rotationY % 360;

            _lastMousePosition = currentPosition;
            UpdateViewInfoDisplay();
            Render();
        }
    }

    private void RenderImage_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            RenderImage.ReleaseMouseCapture();
        }
    }

    private void RenderImage_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            RenderImage.ReleaseMouseCapture();
        }
    }

    private void RenderImage_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        double zoomDelta = e.Delta * ZoomSensitivity;
        _zoom = Math.Clamp(_zoom + zoomDelta, MinZoom, MaxZoom);
        UpdateViewInfoDisplay();
        Render();
    }

    #endregion

    #region Bitmap Management

    private void InitializeBitmap()
    {
        int width = (int)Math.Max(1, RenderImage.ActualWidth > 0 ? RenderImage.ActualWidth : 400);
        int height = (int)Math.Max(1, RenderImage.ActualHeight > 0 ? RenderImage.ActualHeight : 300);

        if (width == _bitmapWidth && height == _bitmapHeight && _bitmap != null)
        {
            return;
        }

        _bitmapWidth = width;
        _bitmapHeight = height;
        _bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        _pixelBuffer = new byte[width * height * 4];
        RenderImage.Source = _bitmap;

        ClearBitmap();
    }

    private void ClearBitmap()
    {
        if (_pixelBuffer == null) return;

        // Fill with background color
        for (int i = 0; i < _pixelBuffer.Length; i += 4)
        {
            _pixelBuffer[i] = BackgroundColor.B;
            _pixelBuffer[i + 1] = BackgroundColor.G;
            _pixelBuffer[i + 2] = BackgroundColor.R;
            _pixelBuffer[i + 3] = 255;
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

    private void SetPixel(int x, int y, byte r, byte g, byte b, byte a = 255)
    {
        if (_pixelBuffer == null) return;
        if (x < 0 || x >= _bitmapWidth || y < 0 || y >= _bitmapHeight) return;

        int index = (y * _bitmapWidth + x) * 4;
        _pixelBuffer[index] = b;
        _pixelBuffer[index + 1] = g;
        _pixelBuffer[index + 2] = r;
        _pixelBuffer[index + 3] = a;
    }

    private void DrawLine(int x0, int y0, int x1, int y1, byte r, byte g, byte b, byte a = 255)
    {
        // Bresenham's line algorithm
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            SetPixel(x0, y0, r, g, b, a);

            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    #endregion

    #region 3D Rendering

    private void Render()
    {
        if (_pixelBuffer == null || _bitmap == null) return;

        ClearBitmap();

        SpectrogramFrame[] frames;
        lock (_frameLock)
        {
            frames = _frameHistory.ToArray();
        }

        if (frames.Length == 0)
        {
            DrawGrid3D();
            UpdateBitmap();
            return;
        }

        // Hide instructions when we have data
        if (InstructionsOverlay.Visibility == Visibility.Visible)
        {
            InstructionsOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = "Receiving";
        }

        DrawWaterfall(frames);
        DrawGrid3D();
        UpdateBitmap();
    }

    private void DrawWaterfall(SpectrogramFrame[] frames)
    {
        if (frames.Length == 0) return;

        int frameCount = frames.Length;
        int maxFrames = _maxHistoryFrames > 0 ? _maxHistoryFrames : DefaultHistoryFrames;

        // Calculate transformation matrices
        double cosX = Math.Cos(_rotationX * Math.PI / 180);
        double sinX = Math.Sin(_rotationX * Math.PI / 180);
        double cosY = Math.Cos(_rotationY * Math.PI / 180);
        double sinY = Math.Sin(_rotationY * Math.PI / 180);

        double centerX = _bitmapWidth / 2.0;
        double centerY = _bitmapHeight / 2.0;
        double scale = Math.Min(_bitmapWidth, _bitmapHeight) * 0.35 * _zoom;

        // Create a Z-buffer for proper depth sorting
        var zBuffer = new double[_bitmapWidth * _bitmapHeight];
        Array.Fill(zBuffer, double.MaxValue);

        // Render frames from back to front (oldest first)
        for (int frameIndex = 0; frameIndex < frameCount; frameIndex++)
        {
            var frame = frames[frameIndex];
            if (frame.MagnitudesLinear == null || frame.FrequencyBins == null) continue;

            int binCount = Math.Min(frame.MagnitudesLinear.Length, frame.FrequencyBins.Length);
            if (binCount == 0) continue;

            // Time position (0 = oldest/back, 1 = newest/front)
            double timePos = (double)frameIndex / Math.Max(1, frameCount - 1);
            double z3D = 1.0 - timePos; // Back to front

            for (int binIndex = 0; binIndex < binCount - 1; binIndex++)
            {
                // Get frequency position
                float freq = frame.FrequencyBins[binIndex];
                if (freq < MinFrequency || freq > MaxFrequency) continue;

                double freqPos;
                if (UseLogFrequency)
                {
                    freqPos = Math.Log10(freq / MinFrequency) / Math.Log10(MaxFrequency / MinFrequency);
                }
                else
                {
                    freqPos = (freq - MinFrequency) / (MaxFrequency - MinFrequency);
                }

                // Map to -1 to 1 range
                double x3D = freqPos * 2.0 - 1.0;

                // Get magnitude (height)
                float magnitude = frame.MagnitudesLinear[binIndex];
                double y3D = magnitude * 0.8; // Scale height

                // Get color from frame
                var (r, g, b) = frame.Colors[binIndex];

                // Apply 3D rotation
                // Rotate around Y axis
                double x1 = x3D * cosY - z3D * sinY;
                double z1 = x3D * sinY + z3D * cosY;

                // Rotate around X axis
                double y1 = y3D * cosX - z1 * sinX;
                double z2 = y3D * sinX + z1 * cosX;

                // Project to 2D (simple perspective)
                double perspective = 1.0 / (1.0 + z2 * 0.3);
                int screenX = (int)(centerX + x1 * scale * perspective);
                int screenY = (int)(centerY - y1 * scale * perspective);

                // Check Z-buffer
                if (screenX >= 0 && screenX < _bitmapWidth && screenY >= 0 && screenY < _bitmapHeight)
                {
                    int bufferIndex = screenY * _bitmapWidth + screenX;
                    if (z2 < zBuffer[bufferIndex])
                    {
                        zBuffer[bufferIndex] = z2;

                        // Apply depth fading
                        double depthFade = 0.3 + 0.7 * (1.0 - z3D);
                        byte finalR = (byte)(r * depthFade);
                        byte finalG = (byte)(g * depthFade);
                        byte finalB = (byte)(b * depthFade);

                        SetPixel(screenX, screenY, finalR, finalG, finalB);

                        // Draw vertical line from base to point for waterfall effect
                        double baseY3D = 0;
                        double baseY1 = baseY3D * cosX - z1 * sinX;
                        int baseScreenY = (int)(centerY - baseY1 * scale * perspective);

                        if (baseScreenY > screenY)
                        {
                            for (int py = screenY; py <= Math.Min(baseScreenY, _bitmapHeight - 1); py++)
                            {
                                int pIndex = py * _bitmapWidth + screenX;
                                if (z2 < zBuffer[pIndex])
                                {
                                    zBuffer[pIndex] = z2;
                                    double lineFade = depthFade * (1.0 - (double)(py - screenY) / Math.Max(1, baseScreenY - screenY) * 0.5);
                                    SetPixel(screenX, py, (byte)(r * lineFade), (byte)(g * lineFade), (byte)(b * lineFade));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private void DrawGrid3D()
    {
        double cosX = Math.Cos(_rotationX * Math.PI / 180);
        double sinX = Math.Sin(_rotationX * Math.PI / 180);
        double cosY = Math.Cos(_rotationY * Math.PI / 180);
        double sinY = Math.Sin(_rotationY * Math.PI / 180);

        double centerX = _bitmapWidth / 2.0;
        double centerY = _bitmapHeight / 2.0;
        double scale = Math.Min(_bitmapWidth, _bitmapHeight) * 0.35 * _zoom;

        // Draw base grid lines
        byte gridR = GridColor.R, gridG = GridColor.G, gridB = GridColor.B;
        byte axisR = AxisColor.R, axisG = AxisColor.G, axisB = AxisColor.B;

        // Transform and draw grid lines along frequency axis
        for (int i = 0; i <= 4; i++)
        {
            double z = i / 4.0 * 2.0 - 1.0;
            var (x0, y0) = Project3DTo2D(-1, 0, z, cosX, sinX, cosY, sinY, centerX, centerY, scale);
            var (x1, y1) = Project3DTo2D(1, 0, z, cosX, sinX, cosY, sinY, centerX, centerY, scale);
            DrawLine(x0, y0, x1, y1, gridR, gridG, gridB, 100);
        }

        // Draw grid lines along time axis
        for (int i = 0; i <= 4; i++)
        {
            double x = i / 4.0 * 2.0 - 1.0;
            var (x0, y0) = Project3DTo2D(x, 0, -1, cosX, sinX, cosY, sinY, centerX, centerY, scale);
            var (x1, y1) = Project3DTo2D(x, 0, 1, cosX, sinX, cosY, sinY, centerX, centerY, scale);
            DrawLine(x0, y0, x1, y1, gridR, gridG, gridB, 100);
        }

        // Draw corner axes
        var origin = Project3DTo2D(-1, 0, 1, cosX, sinX, cosY, sinY, centerX, centerY, scale);
        var freqEnd = Project3DTo2D(1, 0, 1, cosX, sinX, cosY, sinY, centerX, centerY, scale);
        var timeEnd = Project3DTo2D(-1, 0, -1, cosX, sinX, cosY, sinY, centerX, centerY, scale);
        var heightEnd = Project3DTo2D(-1, 0.8, 1, cosX, sinX, cosY, sinY, centerX, centerY, scale);

        // Frequency axis
        DrawLine(origin.x, origin.y, freqEnd.x, freqEnd.y, axisR, axisG, axisB, 180);

        // Time axis
        DrawLine(origin.x, origin.y, timeEnd.x, timeEnd.y, axisR, axisG, axisB, 180);

        // Height axis
        DrawLine(origin.x, origin.y, heightEnd.x, heightEnd.y, axisR, axisG, axisB, 180);
    }

    private (int x, int y) Project3DTo2D(double x3D, double y3D, double z3D,
        double cosX, double sinX, double cosY, double sinY,
        double centerX, double centerY, double scale)
    {
        // Rotate around Y axis
        double x1 = x3D * cosY - z3D * sinY;
        double z1 = x3D * sinY + z3D * cosY;

        // Rotate around X axis
        double y1 = y3D * cosX - z1 * sinX;
        double z2 = y3D * sinX + z1 * cosX;

        // Project to 2D
        double perspective = 1.0 / (1.0 + z2 * 0.3);
        int screenX = (int)(centerX + x1 * scale * perspective);
        int screenY = (int)(centerY - y1 * scale * perspective);

        return (screenX, screenY);
    }

    #endregion

    #region Helper Methods

    private void TrimFrameHistory()
    {
        lock (_frameLock)
        {
            while (_frameHistory.Count > _maxHistoryFrames && _frameHistory.Count > 0)
            {
                _frameHistory.RemoveAt(0);
            }
        }
    }

    private void UnsubscribeFromAnalyzer()
    {
        if (SpectrogramAnalyzer != null)
        {
            SpectrogramAnalyzer.FrameGenerated -= OnFrameGenerated;
        }
    }

    private void UpdateFrequencyRangeDisplay()
    {
        FrequencyRangeText.Text = $"{FormatFrequency(MinFrequency)} - {FormatFrequency(MaxFrequency)}";
    }

    private void UpdateViewInfoDisplay()
    {
        RotationXText.Text = $"{_rotationX:F0}";
        RotationYText.Text = $"{_rotationY:F0}";
        ZoomText.Text = $"{_zoom:F1}x";
    }

    private static string FormatFrequency(float hz)
    {
        if (hz >= 1000)
        {
            return $"{hz / 1000:F1} kHz";
        }
        return $"{hz:F0} Hz";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears the spectrogram history and display.
    /// </summary>
    public void Clear()
    {
        lock (_frameLock)
        {
            _frameHistory.Clear();
        }

        SpectrogramAnalyzer?.Reset();
        ClearBitmap();
        StatusText.Text = "Cleared";
        InstructionsOverlay.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Resets the view to default rotation and zoom.
    /// </summary>
    public void ResetView()
    {
        _rotationX = DefaultRotationX;
        _rotationY = DefaultRotationY;
        _zoom = DefaultZoom;
        UpdateViewInfoDisplay();
        Render();
    }

    /// <summary>
    /// Adds a spectrogram frame directly (for manual feeding).
    /// </summary>
    public void AddFrame(SpectrogramFrame frame)
    {
        if (!IsActive) return;

        lock (_frameLock)
        {
            _frameHistory.Add(frame);
            TrimFrameHistory();
        }

        if (_isInitialized)
        {
            Render();
        }
    }

    /// <summary>
    /// Adds multiple frames from a SpectrogramResult.
    /// </summary>
    public void LoadResult(SpectrogramResult result)
    {
        if (result.Frames == null || result.Frames.Length == 0) return;

        lock (_frameLock)
        {
            _frameHistory.Clear();
            foreach (var frame in result.Frames)
            {
                _frameHistory.Add(frame);
            }
            TrimFrameHistory();
        }

        if (_isInitialized)
        {
            InstructionsOverlay.Visibility = Visibility.Collapsed;
            StatusText.Text = $"Loaded {result.Frames.Length} frames";
            Render();
        }
    }

    /// <summary>
    /// Captures the current view as a BitmapSource.
    /// </summary>
    public BitmapSource? CaptureImage()
    {
        return _bitmap?.Clone();
    }

    #endregion
}
