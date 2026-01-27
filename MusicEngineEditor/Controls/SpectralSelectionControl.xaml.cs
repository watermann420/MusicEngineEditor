// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Shapes = System.Windows.Shapes;
using Image = System.Windows.Controls.Image;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Selection tool type for spectral editing.
/// </summary>
public enum SpectralSelectionTool
{
    Rectangle,
    Lasso,
    Brush
}

/// <summary>
/// Selection mode for combining selections.
/// </summary>
public enum SpectralSelectionMode
{
    Replace,
    Add,
    Subtract,
    Intersect
}

/// <summary>
/// Represents a rectangular selection in spectral data.
/// </summary>
public class SpectralSelection
{
    /// <summary>
    /// Minimum frequency in Hz.
    /// </summary>
    public double MinFrequency { get; set; }

    /// <summary>
    /// Maximum frequency in Hz.
    /// </summary>
    public double MaxFrequency { get; set; }

    /// <summary>
    /// Start time in seconds.
    /// </summary>
    public double StartTime { get; set; }

    /// <summary>
    /// End time in seconds.
    /// </summary>
    public double EndTime { get; set; }

    /// <summary>
    /// Selection mask for lasso/brush selections (optional).
    /// </summary>
    public bool[,]? Mask { get; set; }

    /// <summary>
    /// Gets the frequency range.
    /// </summary>
    public double FrequencyRange => MaxFrequency - MinFrequency;

    /// <summary>
    /// Gets the time range.
    /// </summary>
    public double TimeRange => EndTime - StartTime;

    /// <summary>
    /// Whether this is a valid selection.
    /// </summary>
    public bool IsValid => FrequencyRange > 0 && TimeRange > 0;
}

/// <summary>
/// Event args for spectral selection changes.
/// </summary>
public class SpectralSelectionChangedEventArgs : EventArgs
{
    public SpectralSelection? Selection { get; set; }
    public bool HasSelection => Selection?.IsValid == true;
}

/// <summary>
/// Event args for spectral processing requests.
/// </summary>
public class SpectralProcessRequestEventArgs : EventArgs
{
    public string Operation { get; set; } = string.Empty;
    public SpectralSelection? Selection { get; set; }
    public double FillValue { get; set; } = -120; // dB
}

/// <summary>
/// Control for selecting frequency/time regions in a spectrogram display.
/// </summary>
public partial class SpectralSelectionControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty SpectrogramDataProperty =
        DependencyProperty.Register(nameof(SpectrogramData), typeof(float[,]), typeof(SpectralSelectionControl),
            new PropertyMetadata(null, OnSpectrogramDataChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(SpectralSelectionControl),
            new PropertyMetadata(44100, OnDisplayPropertyChanged));

    public static readonly DependencyProperty FftSizeProperty =
        DependencyProperty.Register(nameof(FftSize), typeof(int), typeof(SpectralSelectionControl),
            new PropertyMetadata(2048, OnDisplayPropertyChanged));

    public static readonly DependencyProperty HopSizeProperty =
        DependencyProperty.Register(nameof(HopSize), typeof(int), typeof(SpectralSelectionControl),
            new PropertyMetadata(512, OnDisplayPropertyChanged));

    public static readonly DependencyProperty MinDbProperty =
        DependencyProperty.Register(nameof(MinDb), typeof(double), typeof(SpectralSelectionControl),
            new PropertyMetadata(-80.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty MaxDbProperty =
        DependencyProperty.Register(nameof(MaxDb), typeof(double), typeof(SpectralSelectionControl),
            new PropertyMetadata(0.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty CurrentSelectionProperty =
        DependencyProperty.Register(nameof(CurrentSelection), typeof(SpectralSelection), typeof(SpectralSelectionControl),
            new PropertyMetadata(null, OnSelectionChanged));

    /// <summary>
    /// Gets or sets the spectrogram magnitude data [frequency bins, time frames].
    /// </summary>
    public float[,]? SpectrogramData
    {
        get => (float[,]?)GetValue(SpectrogramDataProperty);
        set => SetValue(SpectrogramDataProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the FFT size.
    /// </summary>
    public int FftSize
    {
        get => (int)GetValue(FftSizeProperty);
        set => SetValue(FftSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the hop size between FFT frames.
    /// </summary>
    public int HopSize
    {
        get => (int)GetValue(HopSizeProperty);
        set => SetValue(HopSizeProperty, value);
    }

    /// <summary>
    /// Gets or sets the minimum dB value for display.
    /// </summary>
    public double MinDb
    {
        get => (double)GetValue(MinDbProperty);
        set => SetValue(MinDbProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum dB value for display.
    /// </summary>
    public double MaxDb
    {
        get => (double)GetValue(MaxDbProperty);
        set => SetValue(MaxDbProperty, value);
    }

    /// <summary>
    /// Gets or sets the current selection.
    /// </summary>
    public SpectralSelection? CurrentSelection
    {
        get => (SpectralSelection?)GetValue(CurrentSelectionProperty);
        set => SetValue(CurrentSelectionProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when the selection changes.
    /// </summary>
    public event EventHandler<SpectralSelectionChangedEventArgs>? SelectionChanged;

    /// <summary>
    /// Raised when a process operation is requested.
    /// </summary>
    public event EventHandler<SpectralProcessRequestEventArgs>? ProcessRequested;

    #endregion

    #region Private Fields

    private SpectralSelectionTool _currentTool = SpectralSelectionTool.Rectangle;
    private SpectralSelectionMode _selectionMode = SpectralSelectionMode.Replace;
    private bool _isSelecting;
    private Point _selectionStart;
    private Point _selectionEnd;
    private WriteableBitmap? _spectrogramBitmap;
    private Shapes.Rectangle? _selectionRectangle;
    private Shapes.Polyline? _lassoPath;
    private List<Point> _lassoPoints = [];
    private Image? _spectrogramImage;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private double _brushRadius = 20;
#pragma warning restore CS0414

    // Cached calculations
    private double _frequencyPerBin;
    private double _timePerFrame;
    private int _frequencyBins;
    private int _timeFrames;

    #endregion

    #region Constructor

    public SpectralSelectionControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateSpectrogramDisplay();
        UpdateAxes();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateSpectrogramDisplay();
        UpdateAxes();
        UpdateSelectionVisual();
    }

    private static void OnSpectrogramDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralSelectionControl control)
        {
            control.UpdateCachedCalculations();
            control.UpdateSpectrogramDisplay();
            control.UpdateAxes();
        }
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralSelectionControl control)
        {
            control.UpdateCachedCalculations();
            control.UpdateSpectrogramDisplay();
            control.UpdateAxes();
        }
    }

    private static void OnSelectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralSelectionControl control)
        {
            control.UpdateSelectionVisual();
            control.UpdateSelectionInfo();
        }
    }

    private void SelectionTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender == RectangleToolButton)
        {
            _currentTool = SpectralSelectionTool.Rectangle;
            RectangleToolButton.IsChecked = true;
            LassoToolButton.IsChecked = false;
            BrushToolButton.IsChecked = false;
        }
        else if (sender == LassoToolButton)
        {
            _currentTool = SpectralSelectionTool.Lasso;
            RectangleToolButton.IsChecked = false;
            LassoToolButton.IsChecked = true;
            BrushToolButton.IsChecked = false;
        }
        else if (sender == BrushToolButton)
        {
            _currentTool = SpectralSelectionTool.Brush;
            RectangleToolButton.IsChecked = false;
            LassoToolButton.IsChecked = false;
            BrushToolButton.IsChecked = true;
        }
    }

    private void SelectionMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        _selectionMode = (SpectralSelectionMode)SelectionModeCombo.SelectedIndex;
    }

    private void Spectrogram_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isSelecting = true;
        _selectionStart = e.GetPosition(SpectrogramCanvas);
        _selectionEnd = _selectionStart;

        if (_currentTool == SpectralSelectionTool.Lasso)
        {
            _lassoPoints.Clear();
            _lassoPoints.Add(_selectionStart);
            CreateLassoVisual();
        }
        else
        {
            CreateSelectionRectangle();
        }

        SpectrogramCanvas.CaptureMouse();
    }

    private void Spectrogram_MouseMove(object sender, MouseEventArgs e)
    {
        Point pos = e.GetPosition(SpectrogramCanvas);
        UpdateCursorInfo(pos);

        if (!_isSelecting) return;

        _selectionEnd = pos;

        if (_currentTool == SpectralSelectionTool.Lasso)
        {
            _lassoPoints.Add(pos);
            UpdateLassoVisual();
        }
        else if (_currentTool == SpectralSelectionTool.Brush)
        {
            // For brush, we accumulate the selection
            AddBrushSelection(pos);
        }
        else
        {
            UpdateSelectionRectangle();
        }
    }

    private void Spectrogram_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isSelecting) return;

        _isSelecting = false;
        SpectrogramCanvas.ReleaseMouseCapture();

        // Finalize selection
        FinalizeSelection();
    }

    private void Spectrogram_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Clear selection on right-click
        ClearSelection();
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSelection?.IsValid == true)
        {
            ProcessRequested?.Invoke(this, new SpectralProcessRequestEventArgs
            {
                Operation = "Delete",
                Selection = CurrentSelection,
                FillValue = -120
            });
        }
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSelection?.IsValid == true)
        {
            ProcessRequested?.Invoke(this, new SpectralProcessRequestEventArgs
            {
                Operation = "Copy",
                Selection = CurrentSelection
            });
        }
    }

    private void Paste_Click(object sender, RoutedEventArgs e)
    {
        ProcessRequested?.Invoke(this, new SpectralProcessRequestEventArgs
        {
            Operation = "Paste",
            Selection = CurrentSelection
        });
    }

    private void Fill_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentSelection?.IsValid == true)
        {
            ProcessRequested?.Invoke(this, new SpectralProcessRequestEventArgs
            {
                Operation = "Fill",
                Selection = CurrentSelection,
                FillValue = -60 // Default fill value
            });
        }
    }

    private void Invert_Click(object sender, RoutedEventArgs e)
    {
        ProcessRequested?.Invoke(this, new SpectralProcessRequestEventArgs
        {
            Operation = "Invert",
            Selection = CurrentSelection
        });
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        ClearSelection();
    }

    #endregion

    #region Selection Methods

    private void CreateSelectionRectangle()
    {
        if (_selectionRectangle != null)
        {
            SpectrogramCanvas.Children.Remove(_selectionRectangle);
        }

        _selectionRectangle = new Shapes.Rectangle
        {
            Stroke = (Brush)FindResource("SelectionBorderBrush"),
            StrokeThickness = 1,
            Fill = (Brush)FindResource("SelectionBrush"),
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        SpectrogramCanvas.Children.Add(_selectionRectangle);
    }

    private void UpdateSelectionRectangle()
    {
        if (_selectionRectangle == null) return;

        double x = Math.Min(_selectionStart.X, _selectionEnd.X);
        double y = Math.Min(_selectionStart.Y, _selectionEnd.Y);
        double width = Math.Abs(_selectionEnd.X - _selectionStart.X);
        double height = Math.Abs(_selectionEnd.Y - _selectionStart.Y);

        Canvas.SetLeft(_selectionRectangle, x);
        Canvas.SetTop(_selectionRectangle, y);
        _selectionRectangle.Width = width;
        _selectionRectangle.Height = height;
    }

    private void CreateLassoVisual()
    {
        if (_lassoPath != null)
        {
            SpectrogramCanvas.Children.Remove(_lassoPath);
        }

        _lassoPath = new Shapes.Polyline
        {
            Stroke = (Brush)FindResource("SelectionBorderBrush"),
            StrokeThickness = 2,
            Fill = (Brush)FindResource("SelectionBrush")
        };
        SpectrogramCanvas.Children.Add(_lassoPath);
    }

    private void UpdateLassoVisual()
    {
        if (_lassoPath == null || _lassoPoints.Count == 0) return;

        var points = new PointCollection(_lassoPoints);
        _lassoPath.Points = points;
    }

    private void AddBrushSelection(Point center)
    {
        // Add a circle at the brush position to the selection
        // This would be accumulated into a mask
    }

    private void FinalizeSelection()
    {
        if (SpectrogramData == null) return;

        double canvasWidth = SpectrogramCanvas.ActualWidth;
        double canvasHeight = SpectrogramCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        // Convert pixel coordinates to spectral coordinates
        double x1 = Math.Min(_selectionStart.X, _selectionEnd.X);
        double y1 = Math.Min(_selectionStart.Y, _selectionEnd.Y);
        double x2 = Math.Max(_selectionStart.X, _selectionEnd.X);
        double y2 = Math.Max(_selectionStart.Y, _selectionEnd.Y);

        // Calculate frequency and time from pixel positions
        // Note: Y axis is inverted (0 at top = max frequency)
        double maxFreq = SampleRate / 2.0;
        double totalTime = _timeFrames * _timePerFrame;

        double startTime = (x1 / canvasWidth) * totalTime;
        double endTime = (x2 / canvasWidth) * totalTime;
        double maxSelectedFreq = (1.0 - y1 / canvasHeight) * maxFreq;
        double minSelectedFreq = (1.0 - y2 / canvasHeight) * maxFreq;

        CurrentSelection = new SpectralSelection
        {
            MinFrequency = Math.Max(0, minSelectedFreq),
            MaxFrequency = Math.Min(maxFreq, maxSelectedFreq),
            StartTime = Math.Max(0, startTime),
            EndTime = Math.Min(totalTime, endTime)
        };

        SelectionChanged?.Invoke(this, new SpectralSelectionChangedEventArgs
        {
            Selection = CurrentSelection
        });

        UpdateSelectionInfo();
    }

    private void ClearSelection()
    {
        CurrentSelection = null;

        if (_selectionRectangle != null)
        {
            SpectrogramCanvas.Children.Remove(_selectionRectangle);
            _selectionRectangle = null;
        }

        if (_lassoPath != null)
        {
            SpectrogramCanvas.Children.Remove(_lassoPath);
            _lassoPath = null;
        }

        _lassoPoints.Clear();

        SelectionChanged?.Invoke(this, new SpectralSelectionChangedEventArgs
        {
            Selection = null
        });

        UpdateSelectionInfo();
    }

    private void UpdateSelectionVisual()
    {
        if (CurrentSelection == null || !CurrentSelection.IsValid)
        {
            if (_selectionRectangle != null)
            {
                SpectrogramCanvas.Children.Remove(_selectionRectangle);
                _selectionRectangle = null;
            }
            return;
        }

        double canvasWidth = SpectrogramCanvas.ActualWidth;
        double canvasHeight = SpectrogramCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double maxFreq = SampleRate / 2.0;
        double totalTime = _timeFrames * _timePerFrame;

        // Convert selection to pixel coordinates
        double x1 = (CurrentSelection.StartTime / totalTime) * canvasWidth;
        double x2 = (CurrentSelection.EndTime / totalTime) * canvasWidth;
        double y1 = (1.0 - CurrentSelection.MaxFrequency / maxFreq) * canvasHeight;
        double y2 = (1.0 - CurrentSelection.MinFrequency / maxFreq) * canvasHeight;

        if (_selectionRectangle == null)
        {
            CreateSelectionRectangle();
        }

        if (_selectionRectangle != null)
        {
            Canvas.SetLeft(_selectionRectangle, x1);
            Canvas.SetTop(_selectionRectangle, y1);
            _selectionRectangle.Width = Math.Max(1, x2 - x1);
            _selectionRectangle.Height = Math.Max(1, y2 - y1);
        }
    }

    #endregion

    #region Display Methods

    private void UpdateCachedCalculations()
    {
        if (SpectrogramData == null) return;

        _frequencyBins = SpectrogramData.GetLength(0);
        _timeFrames = SpectrogramData.GetLength(1);
        _frequencyPerBin = (SampleRate / 2.0) / _frequencyBins;
        _timePerFrame = (double)HopSize / SampleRate;
    }

    private void UpdateSpectrogramDisplay()
    {
        if (SpectrogramData == null) return;

        double width = SpectrogramCanvas.ActualWidth;
        double height = SpectrogramCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int pixelWidth = (int)width;
        int pixelHeight = (int)height;

        // Create bitmap
        _spectrogramBitmap = new WriteableBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Bgra32, null);

        int stride = pixelWidth * 4;
        byte[] pixels = new byte[pixelHeight * stride];

        double dbRange = MaxDb - MinDb;

        // Render spectrogram
        for (int py = 0; py < pixelHeight; py++)
        {
            for (int px = 0; px < pixelWidth; px++)
            {
                // Map pixel to spectrogram coordinates
                int freqBin = (int)((_frequencyBins - 1) * (1.0 - (double)py / pixelHeight));
                int timeFrame = (int)((_timeFrames - 1) * (double)px / pixelWidth);

                freqBin = Math.Clamp(freqBin, 0, _frequencyBins - 1);
                timeFrame = Math.Clamp(timeFrame, 0, _timeFrames - 1);

                float magnitude = SpectrogramData[freqBin, timeFrame];

                // Convert to dB and normalize
                double db = magnitude > 0 ? 20 * Math.Log10(magnitude) : MinDb;
                double normalized = Math.Clamp((db - MinDb) / dbRange, 0, 1);

                // Map to color
                Color color = GetSpectrogramColor(normalized);

                int index = py * stride + px * 4;
                pixels[index] = color.B;
                pixels[index + 1] = color.G;
                pixels[index + 2] = color.R;
                pixels[index + 3] = 255;
            }
        }

        _spectrogramBitmap.WritePixels(new Int32Rect(0, 0, pixelWidth, pixelHeight), pixels, stride, 0);

        // Update or create image
        if (_spectrogramImage == null)
        {
            _spectrogramImage = new Image
            {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false
            };
            SpectrogramCanvas.Children.Insert(0, _spectrogramImage);
        }

        _spectrogramImage.Source = _spectrogramBitmap;
        _spectrogramImage.Width = width;
        _spectrogramImage.Height = height;
    }

    private static Color GetSpectrogramColor(double value)
    {
        // Cool to warm gradient
        if (value < 0.2)
        {
            // Black to blue
            byte b = (byte)(value * 5 * 255);
            return Color.FromRgb(0, 0, b);
        }
        else if (value < 0.4)
        {
            // Blue to cyan
            double t = (value - 0.2) * 5;
            return Color.FromRgb(0, (byte)(t * 255), 255);
        }
        else if (value < 0.6)
        {
            // Cyan to green
            double t = (value - 0.4) * 5;
            return Color.FromRgb(0, 255, (byte)((1 - t) * 255));
        }
        else if (value < 0.8)
        {
            // Green to yellow
            double t = (value - 0.6) * 5;
            return Color.FromRgb((byte)(t * 255), 255, 0);
        }
        else
        {
            // Yellow to red
            double t = (value - 0.8) * 5;
            return Color.FromRgb(255, (byte)((1 - t) * 255), 0);
        }
    }

    private void UpdateAxes()
    {
        UpdateFrequencyAxis();
        UpdateTimeAxis();
    }

    private void UpdateFrequencyAxis()
    {
        FrequencyAxisCanvas.Children.Clear();

        double height = SpectrogramCanvas.ActualHeight;
        if (height <= 0) return;

        double maxFreq = SampleRate / 2.0;
        var textBrush = (Brush)FindResource("TextSecondaryBrush");

        // Draw frequency markers
        double[] freqMarkers = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

        foreach (double freq in freqMarkers)
        {
            if (freq > maxFreq) break;

            double y = height * (1.0 - freq / maxFreq);

            var label = new TextBlock
            {
                Text = freq >= 1000 ? $"{freq / 1000:F0}k" : $"{freq:F0}",
                FontSize = 9,
                Foreground = textBrush
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 4);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            FrequencyAxisCanvas.Children.Add(label);
        }
    }

    private void UpdateTimeAxis()
    {
        TimeAxisCanvas.Children.Clear();

        double width = SpectrogramCanvas.ActualWidth;
        if (width <= 0 || _timeFrames <= 0) return;

        double totalTime = _timeFrames * _timePerFrame;
        var textBrush = (Brush)FindResource("TextSecondaryBrush");

        // Calculate appropriate time interval
        double interval = 1.0;
        if (totalTime < 5) interval = 0.5;
        if (totalTime < 2) interval = 0.25;
        if (totalTime > 30) interval = 5;
        if (totalTime > 60) interval = 10;

        for (double t = 0; t <= totalTime; t += interval)
        {
            double x = (t / totalTime) * width;

            var label = new TextBlock
            {
                Text = FormatTime(t),
                FontSize = 9,
                Foreground = textBrush
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 4);
            TimeAxisCanvas.Children.Add(label);
        }
    }

    private void UpdateSelectionInfo()
    {
        if (CurrentSelection == null || !CurrentSelection.IsValid)
        {
            FrequencyRangeText.Text = "-- Hz - -- Hz";
            TimeRangeText.Text = "-- s - -- s";
            SelectionSizeText.Text = "-- bins x -- frames";
            return;
        }

        FrequencyRangeText.Text = $"{FormatFrequency(CurrentSelection.MinFrequency)} - {FormatFrequency(CurrentSelection.MaxFrequency)}";
        TimeRangeText.Text = $"{CurrentSelection.StartTime:F3}s - {CurrentSelection.EndTime:F3}s";

        // Calculate bin/frame counts
        int minBin = (int)(CurrentSelection.MinFrequency / _frequencyPerBin);
        int maxBin = (int)(CurrentSelection.MaxFrequency / _frequencyPerBin);
        int startFrame = (int)(CurrentSelection.StartTime / _timePerFrame);
        int endFrame = (int)(CurrentSelection.EndTime / _timePerFrame);

        SelectionSizeText.Text = $"{maxBin - minBin} bins x {endFrame - startFrame} frames";
    }

    private void UpdateCursorInfo(Point pos)
    {
        double canvasWidth = SpectrogramCanvas.ActualWidth;
        double canvasHeight = SpectrogramCanvas.ActualHeight;

        if (canvasWidth <= 0 || canvasHeight <= 0) return;

        double maxFreq = SampleRate / 2.0;
        double totalTime = _timeFrames * _timePerFrame;

        double freq = (1.0 - pos.Y / canvasHeight) * maxFreq;
        double time = (pos.X / canvasWidth) * totalTime;

        CursorPositionText.Text = $"{FormatFrequency(freq)} @ {time:F3}s";
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:F1} kHz";
        return $"{hz:F0} Hz";
    }

    private static string FormatTime(double seconds)
    {
        if (seconds < 60)
            return $"{seconds:F1}s";
        int minutes = (int)(seconds / 60);
        double secs = seconds % 60;
        return $"{minutes}:{secs:00.0}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the spectrogram data from magnitude values.
    /// </summary>
    /// <param name="magnitudes">2D array of magnitudes [frequency bins, time frames].</param>
    public void SetSpectrogramData(float[,] magnitudes)
    {
        SpectrogramData = magnitudes;
    }

    /// <summary>
    /// Sets a rectangular selection programmatically.
    /// </summary>
    public void SetSelection(double minFreq, double maxFreq, double startTime, double endTime)
    {
        CurrentSelection = new SpectralSelection
        {
            MinFrequency = minFreq,
            MaxFrequency = maxFreq,
            StartTime = startTime,
            EndTime = endTime
        };
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearCurrentSelection()
    {
        ClearSelection();
    }

    #endregion
}
