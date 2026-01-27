// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Spectral Gate effect editor control with drawable threshold curve.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Spectral gate editor control with frequency spectrum display, drawable threshold curve,
/// and comprehensive parameter controls for the SpectralGateEffect.
/// </summary>
public partial class SpectralGateControl : UserControl
{
    #region Constants

    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;
    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const int DefaultNumBins = 128;
    private const double BarSpacing = 1.0;

    #endregion

    #region Private Fields

    private int _numBins = DefaultNumBins;
    private float[] _spectrumMagnitudes = new float[DefaultNumBins];
    private float[] _binGains = new float[DefaultNumBins];
    private float[] _thresholdCurve = new float[DefaultNumBins];
    private float[] _customThresholdCurve = new float[DefaultNumBins];
    private bool _hasCustomCurve;

    private Shapes.Rectangle[]? _spectrumBars;
    private Shapes.Rectangle[]? _gateActivityBars;
    private Shapes.Line? _thresholdLine;
    private PathGeometry? _thresholdCurveGeometry;
    private Shapes.Path? _thresholdCurvePath;

    private bool _isDrawing;
    private Point _lastDrawPoint;
    private bool _isInitialized;

    private DispatcherTimer? _updateTimer;
    private bool _isBypassed;
    private bool _isLearning;

    #endregion

    #region Events

    /// <summary>
    /// Raised when a parameter value changes.
    /// </summary>
    public event EventHandler<SpectralGateParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when the bypass state changes.
    /// </summary>
    public event EventHandler<bool>? BypassChanged;

    /// <summary>
    /// Raised when noise learning is requested.
    /// </summary>
    public event EventHandler? LearnNoiseRequested;

    /// <summary>
    /// Raised when the custom threshold curve changes.
    /// </summary>
    public event EventHandler<float[]>? ThresholdCurveChanged;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SpectrumMagnitudesProperty =
        DependencyProperty.Register(nameof(SpectrumMagnitudes), typeof(float[]), typeof(SpectralGateControl),
            new PropertyMetadata(null, OnSpectrumMagnitudesChanged));

    public static readonly DependencyProperty BinGainsProperty =
        DependencyProperty.Register(nameof(BinGains), typeof(float[]), typeof(SpectralGateControl),
            new PropertyMetadata(null, OnBinGainsChanged));

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(SpectralGateControl),
            new PropertyMetadata(44100, OnSampleRateChanged));

    public static readonly DependencyProperty FftSizeProperty =
        DependencyProperty.Register(nameof(FftSize), typeof(int), typeof(SpectralGateControl),
            new PropertyMetadata(2048, OnFftSizeChanged));

    /// <summary>
    /// Gets or sets the spectrum magnitude data for display.
    /// </summary>
    public float[]? SpectrumMagnitudes
    {
        get => (float[]?)GetValue(SpectrumMagnitudesProperty);
        set => SetValue(SpectrumMagnitudesProperty, value);
    }

    /// <summary>
    /// Gets or sets the current bin gains (gate activity) for visualization.
    /// </summary>
    public float[]? BinGains
    {
        get => (float[]?)GetValue(BinGainsProperty);
        set => SetValue(BinGainsProperty, value);
    }

    /// <summary>
    /// Gets or sets the sample rate used for frequency calculations.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets the FFT size used by the spectral gate.
    /// </summary>
    public int FftSize
    {
        get => (int)GetValue(FftSizeProperty);
        set => SetValue(FftSizeProperty, value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the threshold level in dB.
    /// </summary>
    public double Threshold
    {
        get => ThresholdSlider.Value;
        set => ThresholdSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the attack time in milliseconds.
    /// </summary>
    public double AttackMs
    {
        get => AttackSlider.Value;
        set => AttackSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds.
    /// </summary>
    public double ReleaseMs
    {
        get => ReleaseSlider.Value;
        set => ReleaseSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the hold time in milliseconds.
    /// </summary>
    public double HoldMs
    {
        get => HoldSlider.Value;
        set => HoldSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the range (attenuation) in dB.
    /// </summary>
    public double Range
    {
        get => RangeSlider.Value;
        set => RangeSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the low frequency limit in Hz.
    /// </summary>
    public double FrequencyLow
    {
        get => FreqLowSlider.Value;
        set => FreqLowSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the high frequency limit in Hz.
    /// </summary>
    public double FrequencyHigh
    {
        get => FreqHighSlider.Value;
        set => FreqHighSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the curve slope in dB/octave.
    /// </summary>
    public double CurveSlope
    {
        get => CurveSlopeSlider.Value;
        set => CurveSlopeSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the lookahead time in milliseconds.
    /// </summary>
    public double LookaheadMs
    {
        get => LookaheadSlider.Value;
        set => LookaheadSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the adaptive ratio (0-1).
    /// </summary>
    public double AdaptiveRatio
    {
        get => AdaptiveRatioSlider.Value / 100.0;
        set => AdaptiveRatioSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the wet/dry mix (0-1).
    /// </summary>
    public double Mix
    {
        get => MixSlider.Value / 100.0;
        set => MixSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets whether the effect is bypassed.
    /// </summary>
    public bool IsBypassed
    {
        get => _isBypassed;
        set
        {
            _isBypassed = value;
            BypassToggle.IsChecked = value;
        }
    }

    /// <summary>
    /// Gets the current threshold mode.
    /// </summary>
    public int ThresholdMode => ThresholdModeComboBox.SelectedIndex;

    /// <summary>
    /// Gets the custom threshold curve if set.
    /// </summary>
    public float[]? CustomThresholdCurve => _hasCustomCurve ? _customThresholdCurve : null;

    #endregion

    #region Constructor

    public SpectralGateControl()
    {
        InitializeComponent();

        // Initialize threshold curves
        for (int i = 0; i < _numBins; i++)
        {
            _thresholdCurve[i] = -40f;
            _customThresholdCurve[i] = -40f;
            _binGains[i] = 1f;
        }

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Lifecycle Events

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        BuildVisualTree();
        _isInitialized = true;

        // Start update timer for animations
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateLayout();
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            UpdateSpectrumDisplay();
            UpdateGateActivityDisplay();
        }
    }

    #endregion

    #region Visual Tree Building

    private void BuildVisualTree()
    {
        SpectrumCanvas.Children.Clear();
        DbScaleCanvas.Children.Clear();
        FrequencyLabelCanvas.Children.Clear();
        GateActivityCanvas.Children.Clear();

        _spectrumBars = new Shapes.Rectangle[_numBins];
        _gateActivityBars = new Shapes.Rectangle[_numBins];

        var spectrumBrush = FindResource("SpectralGateSpectrumGradient") as Brush ?? Brushes.Cyan;
        var openBrush = FindResource("SpectralGateOpenBrush") as Brush ?? Brushes.Green;
        var gatedBrush = FindResource("SpectralGateGatedBrush") as Brush ?? Brushes.Red;

        // Create spectrum bars
        for (int i = 0; i < _numBins; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Fill = spectrumBrush,
                RadiusX = 1,
                RadiusY = 1
            };
            _spectrumBars[i] = bar;
            SpectrumCanvas.Children.Add(bar);

            // Gate activity bars
            var activityBar = new Shapes.Rectangle
            {
                Fill = openBrush,
                RadiusX = 1,
                RadiusY = 1
            };
            _gateActivityBars[i] = activityBar;
            GateActivityCanvas.Children.Add(activityBar);
        }

        // Create threshold curve path
        _thresholdCurvePath = new Shapes.Path
        {
            Stroke = new SolidColorBrush(Color.FromRgb(255, 71, 87)), // Error/Red color
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };
        SpectrumCanvas.Children.Add(_thresholdCurvePath);

        // Draw dB scale
        DrawDbScale();

        // Draw frequency labels
        DrawFrequencyLabels();

        // Update layout
        UpdateLayout();
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = FindResource("SpectralGateSecondaryTextBrush") as Brush ?? Brushes.Gray;
        var gridBrush = FindResource("SpectralGateBorderBrush") as Brush ?? Brushes.DarkGray;

        double height = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 200;
        double[] dbMarks = { 0, -12, -24, -36, -48, -60, -72 };

        foreach (var db in dbMarks)
        {
            double normalizedLevel = (db - MinDb) / (MaxDb - MinDb);
            double y = height * (1 - normalizedLevel);

            // Tick mark
            var tick = new Shapes.Line
            {
                X1 = 34,
                Y1 = y,
                X2 = 38,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            DbScaleCanvas.Children.Add(tick);

            // Horizontal grid line on spectrum canvas
            var gridLine = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = SpectrumCanvas.ActualWidth,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                Opacity = 0.3
            };
            SpectrumCanvas.Children.Insert(0, gridLine);

            // Label
            var label = new TextBlock
            {
                Text = db == 0 ? "0" : $"{db}",
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 6);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }
    }

    private void DrawFrequencyLabels()
    {
        FrequencyLabelCanvas.Children.Clear();

        var textBrush = FindResource("SpectralGateSecondaryTextBrush") as Brush ?? Brushes.Gray;
        double width = SpectrumCanvas.ActualWidth;
        if (width <= 0) return;

        double[] frequencies = { 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

        foreach (var freq in frequencies)
        {
            double x = FrequencyToX(freq, width);
            if (x < 0 || x > width) continue;

            string text = freq >= 1000 ? $"{freq / 1000}k" : $"{freq}";

            var label = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontSize = 9,
                TextAlignment = TextAlignment.Center
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            FrequencyLabelCanvas.Children.Add(label);
        }
    }

    private new void UpdateLayout()
    {
        if (_spectrumBars == null || _gateActivityBars == null) return;

        double spectrumWidth = SpectrumCanvas.ActualWidth;
        double spectrumHeight = SpectrumCanvas.ActualHeight;
        double activityWidth = GateActivityCanvas.ActualWidth;
        double activityHeight = GateActivityCanvas.ActualHeight;

        if (spectrumWidth <= 0 || spectrumHeight <= 0) return;

        double barWidth = Math.Max(1, (spectrumWidth - (_numBins - 1) * BarSpacing) / _numBins);
        double activityBarWidth = Math.Max(1, (activityWidth - (_numBins - 1) * BarSpacing) / _numBins);

        for (int i = 0; i < _numBins; i++)
        {
            double x = i * (barWidth + BarSpacing);

            // Spectrum bars
            _spectrumBars[i].Width = Math.Max(1, barWidth);
            Canvas.SetLeft(_spectrumBars[i], x);
            Canvas.SetBottom(_spectrumBars[i], 0);

            // Gate activity bars
            double activityX = i * (activityBarWidth + BarSpacing);
            _gateActivityBars[i].Width = Math.Max(1, activityBarWidth);
            _gateActivityBars[i].Height = activityHeight - 4;
            Canvas.SetLeft(_gateActivityBars[i], activityX);
            Canvas.SetTop(_gateActivityBars[i], 2);
        }

        // Redraw scales
        DrawDbScale();
        DrawFrequencyLabels();
        UpdateThresholdCurveDisplay();
    }

    #endregion

    #region Display Updates

    private void UpdateSpectrumDisplay()
    {
        if (_spectrumBars == null) return;

        double height = SpectrumCanvas.ActualHeight;
        if (height <= 0) return;

        var magnitudes = SpectrumMagnitudes ?? _spectrumMagnitudes;
        int count = Math.Min(_numBins, magnitudes.Length);

        for (int i = 0; i < count; i++)
        {
            double magnitude = Math.Clamp(magnitudes[i], 0, 1);
            double barHeight = height * magnitude;
            _spectrumBars[i].Height = Math.Max(0, barHeight);
        }
    }

    private void UpdateGateActivityDisplay()
    {
        if (_gateActivityBars == null) return;

        var gains = BinGains ?? _binGains;
        int count = Math.Min(_numBins, gains.Length);

        var openBrush = FindResource("SpectralGateOpenBrush") as Brush ?? Brushes.Green;
        var gatedBrush = FindResource("SpectralGateGatedBrush") as Brush ?? Brushes.Red;
        var partialBrush = FindResource("SpectralGateAccentBrush") as Brush ?? Brushes.Cyan;

        for (int i = 0; i < count; i++)
        {
            float gain = gains[i];

            // Color based on gate state
            if (gain > 0.95f)
            {
                _gateActivityBars[i].Fill = openBrush;
            }
            else if (gain < 0.05f)
            {
                _gateActivityBars[i].Fill = gatedBrush;
            }
            else
            {
                _gateActivityBars[i].Fill = partialBrush;
            }

            _gateActivityBars[i].Opacity = 0.3 + gain * 0.7;
        }
    }

    private void UpdateThresholdCurveDisplay()
    {
        if (_thresholdCurvePath == null) return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        var geometry = new PathGeometry();
        var figure = new PathFigure();

        bool isFirst = true;
        float[] curve = _hasCustomCurve ? _customThresholdCurve : _thresholdCurve;

        for (int i = 0; i < _numBins; i++)
        {
            double x = (double)i / (_numBins - 1) * width;
            double db = curve[i];
            double normalizedDb = (db - MinDb) / (MaxDb - MinDb);
            double y = height * (1 - normalizedDb);

            if (isFirst)
            {
                figure.StartPoint = new Point(x, y);
                isFirst = false;
            }
            else
            {
                figure.Segments.Add(new LineSegment(new Point(x, y), true));
            }
        }

        geometry.Figures.Add(figure);
        _thresholdCurvePath.Data = geometry;
    }

    private void RecalculateThresholdCurve()
    {
        float baseThreshold = (float)ThresholdSlider.Value;
        float slope = (float)CurveSlopeSlider.Value;
        float refFreq = 1000f;

        for (int i = 0; i < _numBins; i++)
        {
            float freq = BinToFrequency(i);
            if (freq < 20f) freq = 20f;

            if (ThresholdModeComboBox.SelectedIndex == 1) // Frequency Curve
            {
                float octaves = MathF.Log2(freq / refFreq);
                _thresholdCurve[i] = baseThreshold + octaves * slope;
            }
            else
            {
                _thresholdCurve[i] = baseThreshold;
            }
        }

        if (!_hasCustomCurve)
        {
            UpdateThresholdCurveDisplay();
        }
    }

    #endregion

    #region Drawing Threshold Curve

    private void SpectrumCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ThresholdModeComboBox.SelectedIndex != 1) // Only allow drawing in Frequency Curve mode
        {
            return;
        }

        _isDrawing = true;
        _lastDrawPoint = e.GetPosition(SpectrumCanvas);
        SpectrumCanvas.CaptureMouse();

        // Apply first point
        ApplyDrawPoint(_lastDrawPoint);

        DrawModeIndicator.Text = "Drawing threshold curve...";
    }

    private void SpectrumCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDrawing) return;

        Point currentPoint = e.GetPosition(SpectrumCanvas);

        // Interpolate between last and current point for smooth drawing
        InterpolateAndApply(_lastDrawPoint, currentPoint);

        _lastDrawPoint = currentPoint;
        UpdateThresholdCurveDisplay();
    }

    private void SpectrumCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            SpectrumCanvas.ReleaseMouseCapture();
            _hasCustomCurve = true;

            DrawModeIndicator.Text = "Click and drag to draw threshold curve";
            StatusText.Text = "Custom threshold curve applied";

            // Notify listeners
            ThresholdCurveChanged?.Invoke(this, (float[])_customThresholdCurve.Clone());
        }
    }

    private void SpectrumCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDrawing)
        {
            _isDrawing = false;
            SpectrumCanvas.ReleaseMouseCapture();
            DrawModeIndicator.Text = "Click and drag to draw threshold curve";
        }
    }

    private void ApplyDrawPoint(Point point)
    {
        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        int binIndex = (int)(point.X / width * _numBins);
        binIndex = Math.Clamp(binIndex, 0, _numBins - 1);

        double normalizedY = 1 - (point.Y / height);
        float db = (float)(MinDb + normalizedY * (MaxDb - MinDb));
        db = Math.Clamp(db, (float)MinDb, (float)MaxDb);

        _customThresholdCurve[binIndex] = db;
    }

    private void InterpolateAndApply(Point start, Point end)
    {
        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        int startBin = (int)(start.X / width * _numBins);
        int endBin = (int)(end.X / width * _numBins);

        startBin = Math.Clamp(startBin, 0, _numBins - 1);
        endBin = Math.Clamp(endBin, 0, _numBins - 1);

        if (startBin == endBin)
        {
            ApplyDrawPoint(end);
            return;
        }

        int minBin = Math.Min(startBin, endBin);
        int maxBin = Math.Max(startBin, endBin);

        double startY = 1 - (start.Y / height);
        double endY = 1 - (end.Y / height);

        float startDb = (float)(MinDb + startY * (MaxDb - MinDb));
        float endDb = (float)(MinDb + endY * (MaxDb - MinDb));

        for (int bin = minBin; bin <= maxBin; bin++)
        {
            float t = (float)(bin - minBin) / (maxBin - minBin);
            if (startBin > endBin) t = 1 - t;

            float db = startDb + t * (endDb - startDb);
            db = Math.Clamp(db, (float)MinDb, (float)MaxDb);
            _customThresholdCurve[bin] = db;
        }
    }

    #endregion

    #region Parameter Event Handlers

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValue == null) return;
        ThresholdValue.Text = $"{e.NewValue:F0} dB";
        RecalculateThresholdCurve();
        RaiseParameterChanged("Threshold", (float)e.NewValue);
    }

    private void AttackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AttackValue == null) return;
        AttackValue.Text = FormatTime(e.NewValue);
        RaiseParameterChanged("Attack", (float)(e.NewValue / 1000.0)); // Convert to seconds
    }

    private void ReleaseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ReleaseValue == null) return;
        ReleaseValue.Text = FormatTime(e.NewValue);
        RaiseParameterChanged("Release", (float)(e.NewValue / 1000.0)); // Convert to seconds
    }

    private void HoldSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HoldValue == null) return;
        HoldValue.Text = FormatTime(e.NewValue);
        RaiseParameterChanged("Hold", (float)(e.NewValue / 1000.0)); // Convert to seconds
    }

    private void RangeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RangeValue == null) return;
        RangeValue.Text = $"{e.NewValue:F0} dB";
        RaiseParameterChanged("Range", (float)e.NewValue);
    }

    private void FreqLowSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FreqLowValue == null) return;
        FreqLowValue.Text = FormatFrequency(e.NewValue);
        RaiseParameterChanged("FreqLow", (float)e.NewValue);
    }

    private void FreqHighSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FreqHighValue == null) return;
        FreqHighValue.Text = FormatFrequency(e.NewValue);
        RaiseParameterChanged("FreqHigh", (float)e.NewValue);
    }

    private void CurveSlopeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CurveSlopeValue == null) return;
        CurveSlopeValue.Text = $"{e.NewValue:F0} dB/oct";
        RecalculateThresholdCurve();
        RaiseParameterChanged("CurveSlope", (float)e.NewValue);
    }

    private void LookaheadSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LookaheadValue == null) return;
        LookaheadValue.Text = $"{e.NewValue:F0} ms";
        RaiseParameterChanged("Lookahead", (float)e.NewValue);
    }

    private void AdaptiveRatioSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (AdaptiveRatioValue == null) return;
        AdaptiveRatioValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("AdaptiveRatio", (float)(e.NewValue / 100.0));
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Mix", (float)(e.NewValue / 100.0));
    }

    private void ThresholdModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        int mode = ThresholdModeComboBox.SelectedIndex;

        // Show/hide mode-specific controls
        if (CurveSlopePanel != null)
        {
            CurveSlopePanel.Visibility = mode == 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        if (AdaptiveRatioPanel != null)
        {
            AdaptiveRatioPanel.Visibility = mode == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // Update draw mode indicator
        if (DrawModeIndicator != null)
        {
            DrawModeIndicator.Text = mode == 1
                ? "Click and drag to draw threshold curve"
                : "Select 'Frequency Curve' mode to draw custom threshold";
        }

        RecalculateThresholdCurve();
        RaiseParameterChanged("ThresholdMode", mode);
    }

    private void BypassToggle_Click(object sender, RoutedEventArgs e)
    {
        _isBypassed = BypassToggle.IsChecked == true;
        StatusText.Text = _isBypassed ? "Effect bypassed" : "Effect active";
        BypassChanged?.Invoke(this, _isBypassed);
    }

    private void LearnButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isLearning)
        {
            _isLearning = false;
            LearnButton.Content = "Learn Noise";
            StatusText.Text = "Learning cancelled";
            return;
        }

        _isLearning = true;
        LearnButton.Content = "Stop Learning";
        StatusText.Text = "Learning noise profile...";

        LearnNoiseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearThresholdButton_Click(object sender, RoutedEventArgs e)
    {
        _hasCustomCurve = false;

        // Reset custom curve to match calculated curve
        for (int i = 0; i < _numBins; i++)
        {
            _customThresholdCurve[i] = _thresholdCurve[i];
        }

        RecalculateThresholdCurve();
        UpdateThresholdCurveDisplay();
        StatusText.Text = "Threshold curve reset";
    }

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedIndex <= 0) return;

        var item = PresetComboBox.SelectedItem as ComboBoxItem;
        if (item == null) return;

        string preset = item.Content?.ToString() ?? "";
        ApplyPreset(preset);

        // Reset to placeholder
        PresetComboBox.SelectedIndex = 0;
    }

    #endregion

    #region Presets

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Noise Reduction":
                ThresholdSlider.Value = -50;
                ThresholdModeComboBox.SelectedIndex = 1; // Frequency Curve
                CurveSlopeSlider.Value = 3;
                AttackSlider.Value = 10;
                ReleaseSlider.Value = 100;
                HoldSlider.Value = 50;
                RangeSlider.Value = -40;
                FreqLowSlider.Value = 100;
                FreqHighSlider.Value = 16000;
                break;

            case "Low Pass Gate":
                ThresholdSlider.Value = -60;
                ThresholdModeComboBox.SelectedIndex = 0; // Global
                AttackSlider.Value = 1;
                ReleaseSlider.Value = 20;
                HoldSlider.Value = 10;
                RangeSlider.Value = -80;
                FreqLowSlider.Value = 20;
                FreqHighSlider.Value = 500;
                break;

            case "Creative":
                ThresholdSlider.Value = -30;
                ThresholdModeComboBox.SelectedIndex = 2; // Adaptive
                AdaptiveRatioSlider.Value = 50;
                AttackSlider.Value = 50;
                ReleaseSlider.Value = 200;
                HoldSlider.Value = 100;
                RangeSlider.Value = -60;
                FreqLowSlider.Value = 20;
                FreqHighSlider.Value = 20000;
                break;

            case "Aggressive":
                ThresholdSlider.Value = -20;
                ThresholdModeComboBox.SelectedIndex = 0; // Global
                AttackSlider.Value = 0.5;
                ReleaseSlider.Value = 10;
                HoldSlider.Value = 5;
                RangeSlider.Value = -80;
                LookaheadSlider.Value = 5;
                break;
        }

        _hasCustomCurve = false;
        RecalculateThresholdCurve();
        StatusText.Text = $"Preset applied: {preset}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the spectrum display with new magnitude data.
    /// </summary>
    public void UpdateSpectrum(float[] magnitudes)
    {
        if (magnitudes.Length != _numBins)
        {
            _numBins = magnitudes.Length;
            Array.Resize(ref _spectrumMagnitudes, _numBins);
            Array.Resize(ref _binGains, _numBins);
            Array.Resize(ref _thresholdCurve, _numBins);
            Array.Resize(ref _customThresholdCurve, _numBins);

            if (_isInitialized)
            {
                BuildVisualTree();
            }
        }

        Array.Copy(magnitudes, _spectrumMagnitudes, _numBins);
    }

    /// <summary>
    /// Updates the gate activity display with new bin gain data.
    /// </summary>
    public void UpdateGateActivity(float[] gains)
    {
        if (gains.Length == _binGains.Length)
        {
            Array.Copy(gains, _binGains, _binGains.Length);
        }
    }

    /// <summary>
    /// Sets the custom threshold curve from external source.
    /// </summary>
    public void SetCustomThresholdCurve(float[] curve)
    {
        if (curve.Length != _numBins)
        {
            Array.Resize(ref _customThresholdCurve, curve.Length);
            _numBins = curve.Length;
        }

        Array.Copy(curve, _customThresholdCurve, curve.Length);
        _hasCustomCurve = true;
        UpdateThresholdCurveDisplay();
    }

    /// <summary>
    /// Called when noise learning is complete.
    /// </summary>
    public void OnNoiseLearnComplete(float[] noiseProfile)
    {
        _isLearning = false;
        LearnButton.Content = "Learn Noise";
        StatusText.Text = "Noise profile learned";

        // Use noise profile to set threshold curve
        if (noiseProfile.Length == _numBins)
        {
            for (int i = 0; i < _numBins; i++)
            {
                // Set threshold slightly above noise floor
                float noiseDb = 20f * MathF.Log10(noiseProfile[i] + 1e-10f);
                _customThresholdCurve[i] = noiseDb + 6f; // 6dB above noise
            }

            _hasCustomCurve = true;
            ThresholdModeComboBox.SelectedIndex = 1; // Switch to Frequency Curve mode
            UpdateThresholdCurveDisplay();
        }
    }

    /// <summary>
    /// Resets all parameters to default values.
    /// </summary>
    public void Reset()
    {
        ThresholdSlider.Value = -40;
        AttackSlider.Value = 5;
        ReleaseSlider.Value = 50;
        HoldSlider.Value = 20;
        RangeSlider.Value = -60;
        FreqLowSlider.Value = 20;
        FreqHighSlider.Value = 20000;
        CurveSlopeSlider.Value = 6;
        LookaheadSlider.Value = 0;
        AdaptiveRatioSlider.Value = 70;
        MixSlider.Value = 100;

        ThresholdModeComboBox.SelectedIndex = 0;
        BypassToggle.IsChecked = false;
        _isBypassed = false;
        _hasCustomCurve = false;
        _isLearning = false;

        RecalculateThresholdCurve();
        StatusText.Text = "Reset to defaults";
    }

    #endregion

    #region Helper Methods

    private void RaiseParameterChanged(string name, float value)
    {
        ParameterChanged?.Invoke(this, new SpectralGateParameterChangedEventArgs(name, value));
    }

    private float BinToFrequency(int bin)
    {
        // Logarithmic frequency mapping
        double t = (double)bin / (_numBins - 1);
        return (float)(MinFrequency * Math.Pow(MaxFrequency / MinFrequency, t));
    }

    private int FrequencyToBin(double frequency)
    {
        double t = Math.Log(frequency / MinFrequency) / Math.Log(MaxFrequency / MinFrequency);
        return (int)(t * (_numBins - 1));
    }

    private double FrequencyToX(double frequency, double width)
    {
        double t = Math.Log(frequency / MinFrequency) / Math.Log(MaxFrequency / MinFrequency);
        return t * width;
    }

    private static string FormatTime(double ms)
    {
        if (ms >= 1000)
        {
            return $"{ms / 1000:F1} s";
        }
        return $"{ms:F0} ms";
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
        {
            return $"{hz / 1000:F1} kHz";
        }
        return $"{hz:F0} Hz";
    }

    #endregion

    #region Dependency Property Callbacks

    private static void OnSpectrumMagnitudesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralGateControl control && e.NewValue is float[] magnitudes)
        {
            control.UpdateSpectrum(magnitudes);
        }
    }

    private static void OnBinGainsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralGateControl control && e.NewValue is float[] gains)
        {
            control.UpdateGateActivity(gains);
        }
    }

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralGateControl control)
        {
            control.RecalculateThresholdCurve();
        }
    }

    private static void OnFftSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectralGateControl control && e.NewValue is int fftSize)
        {
            control._numBins = fftSize / 2 + 1;
            if (control._isInitialized)
            {
                control.BuildVisualTree();
            }
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for spectral gate parameter changes.
/// </summary>
public class SpectralGateParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public SpectralGateParameterChangedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Converter for slider percentage width (used in slider template).
/// </summary>
public class SpectralGatePercentageConverter : IValueConverter
{
    public static readonly SpectralGatePercentageConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // This is a placeholder - actual slider track is handled by WPF
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
