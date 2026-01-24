using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Spectrum analyzer display control with bar graph visualization.
/// Features configurable band count, peak hold indicators, and frequency labels.
/// </summary>
public partial class SpectrumDisplay : UserControl
{
    #region Constants

    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;
    private const double BarSpacing = 1.0;
    private const double PeakIndicatorHeight = 2.0;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MagnitudesProperty =
        DependencyProperty.Register(nameof(Magnitudes), typeof(float[]), typeof(SpectrumDisplay),
            new PropertyMetadata(null, OnMagnitudesChanged));

    public static readonly DependencyProperty PeakMagnitudesProperty =
        DependencyProperty.Register(nameof(PeakMagnitudes), typeof(float[]), typeof(SpectrumDisplay),
            new PropertyMetadata(null, OnMagnitudesChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(float[]), typeof(SpectrumDisplay),
            new PropertyMetadata(null, OnFrequenciesChanged));

    public static readonly DependencyProperty BandCountProperty =
        DependencyProperty.Register(nameof(BandCount), typeof(int), typeof(SpectrumDisplay),
            new PropertyMetadata(31, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ShowPeakHoldProperty =
        DependencyProperty.Register(nameof(ShowPeakHold), typeof(bool), typeof(SpectrumDisplay),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowFrequencyLabelsProperty =
        DependencyProperty.Register(nameof(ShowFrequencyLabels), typeof(bool), typeof(SpectrumDisplay),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty ShowDbScaleProperty =
        DependencyProperty.Register(nameof(ShowDbScale), typeof(bool), typeof(SpectrumDisplay),
            new PropertyMetadata(true, OnLayoutPropertyChanged));

    public static readonly DependencyProperty UseOutlineStyleProperty =
        DependencyProperty.Register(nameof(UseOutlineStyle), typeof(bool), typeof(SpectrumDisplay),
            new PropertyMetadata(false, OnStyleChanged));

    public static readonly DependencyProperty BarColorProperty =
        DependencyProperty.Register(nameof(BarColor), typeof(Brush), typeof(SpectrumDisplay),
            new PropertyMetadata(null, OnStyleChanged));

    public static readonly DependencyProperty SmoothingFactorProperty =
        DependencyProperty.Register(nameof(SmoothingFactor), typeof(double), typeof(SpectrumDisplay),
            new PropertyMetadata(0.3));

    /// <summary>
    /// Gets or sets the spectrum band magnitudes (0.0 to 1.0 range).
    /// </summary>
    public float[] Magnitudes
    {
        get => (float[])GetValue(MagnitudesProperty);
        set => SetValue(MagnitudesProperty, value);
    }

    /// <summary>
    /// Gets or sets the peak hold magnitudes.
    /// </summary>
    public float[] PeakMagnitudes
    {
        get => (float[])GetValue(PeakMagnitudesProperty);
        set => SetValue(PeakMagnitudesProperty, value);
    }

    /// <summary>
    /// Gets or sets the center frequencies for each band.
    /// </summary>
    public float[] Frequencies
    {
        get => (float[])GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of frequency bands.
    /// </summary>
    public int BandCount
    {
        get => (int)GetValue(BandCountProperty);
        set => SetValue(BandCountProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show peak hold indicators.
    /// </summary>
    public bool ShowPeakHold
    {
        get => (bool)GetValue(ShowPeakHoldProperty);
        set => SetValue(ShowPeakHoldProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show frequency labels.
    /// </summary>
    public bool ShowFrequencyLabels
    {
        get => (bool)GetValue(ShowFrequencyLabelsProperty);
        set => SetValue(ShowFrequencyLabelsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the dB scale.
    /// </summary>
    public bool ShowDbScale
    {
        get => (bool)GetValue(ShowDbScaleProperty);
        set => SetValue(ShowDbScaleProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to use outline style instead of filled bars.
    /// </summary>
    public bool UseOutlineStyle
    {
        get => (bool)GetValue(UseOutlineStyleProperty);
        set => SetValue(UseOutlineStyleProperty, value);
    }

    /// <summary>
    /// Gets or sets the bar fill color/brush.
    /// </summary>
    public Brush BarColor
    {
        get => (Brush)GetValue(BarColorProperty);
        set => SetValue(BarColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the visual smoothing factor.
    /// </summary>
    public double SmoothingFactor
    {
        get => (double)GetValue(SmoothingFactorProperty);
        set => SetValue(SmoothingFactorProperty, value);
    }

    #endregion

    #region Private Fields

    private Shapes.Rectangle[]? _bars;
    private Shapes.Rectangle[]? _peakIndicators;
    private double[]? _displayedMagnitudes;
    private bool _isInitialized;

    #endregion

    #region Constructor

    public SpectrumDisplay()
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
        BuildVisualTree();
        _isInitialized = true;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateDisplayLayout();
        }
    }

    private static void OnMagnitudesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumDisplay display && display._isInitialized)
        {
            display.UpdateBars();
        }
    }

    private static void OnFrequenciesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumDisplay display && display._isInitialized)
        {
            display.UpdateFrequencyLabels();
        }
    }

    private static void OnLayoutPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumDisplay display && display._isInitialized)
        {
            display.BuildVisualTree();
        }
    }

    private static void OnStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SpectrumDisplay display && display._isInitialized)
        {
            display.UpdateBarStyles();
        }
    }

    #endregion

    #region Visual Tree Building

    private void BuildVisualTree()
    {
        SpectrumCanvas.Children.Clear();
        DbScaleCanvas.Children.Clear();
        FrequencyCanvas.Children.Clear();

        int bandCount = BandCount;
        _bars = new Shapes.Rectangle[bandCount];
        _peakIndicators = new Shapes.Rectangle[bandCount];
        _displayedMagnitudes = new double[bandCount];

        // Get the brush for bars
        Brush barBrush = BarColor ?? FindResource("BarGradientBrush") as Brush ?? Brushes.Cyan;
        Brush peakBrush = FindResource("PeakBrush") as Brush ?? Brushes.White;

        // Create bars
        for (int i = 0; i < bandCount; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Fill = UseOutlineStyle ? Brushes.Transparent : barBrush,
                Stroke = UseOutlineStyle ? barBrush : null,
                StrokeThickness = UseOutlineStyle ? 1 : 0,
                RadiusX = 1,
                RadiusY = 1
            };
            _bars[i] = bar;
            SpectrumCanvas.Children.Add(bar);

            // Peak indicator
            var peak = new Shapes.Rectangle
            {
                Fill = peakBrush,
                Height = PeakIndicatorHeight,
                Visibility = ShowPeakHold ? Visibility.Visible : Visibility.Collapsed
            };
            _peakIndicators[i] = peak;
            SpectrumCanvas.Children.Add(peak);
        }

        // Draw dB scale
        if (ShowDbScale)
        {
            DrawDbScale();
        }

        // Update display layout
        UpdateDisplayLayout();
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        Brush textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;
        Brush gridBrush = FindResource("GridBrush") as Brush ?? Brushes.DarkGray;

        double[] dbMarks = { 0, -6, -12, -24, -36, -48, -60 };
        double height = SpectrumCanvas.ActualHeight > 0 ? SpectrumCanvas.ActualHeight : 100;

        foreach (var db in dbMarks)
        {
            double normalizedLevel = (db - MinDb) / (MaxDb - MinDb);
            double y = height * (1 - normalizedLevel);

            // Tick mark
            var tick = new Shapes.Line
            {
                X1 = 22,
                Y1 = y,
                X2 = 26,
                Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            DbScaleCanvas.Children.Add(tick);

            // Label
            var label = new TextBlock
            {
                Text = db == 0 ? "0" : db.ToString(),
                Foreground = textBrush,
                FontSize = 8,
                TextAlignment = TextAlignment.Right
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetRight(label, 6);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(label);
        }
    }

    private void UpdateFrequencyLabels()
    {
        FrequencyCanvas.Children.Clear();

        if (!ShowFrequencyLabels || Frequencies == null || Frequencies.Length == 0) return;

        Brush textBrush = FindResource("TextBrush") as Brush ?? Brushes.Gray;
        double width = SpectrumCanvas.ActualWidth;
        int bandCount = Math.Min(Frequencies.Length, BandCount);

        // Show labels at specific intervals
        int[] labelIndices = bandCount switch
        {
            <= 10 => Enumerable.Range(0, bandCount).ToArray(),
            <= 20 => new[] { 0, 4, 9, 14, 19 }.Where(i => i < bandCount).ToArray(),
            _ => new[] { 0, 5, 10, 15, 20, 25, 30 }.Where(i => i < bandCount).ToArray()
        };

        double barWidth = (width - (bandCount - 1) * BarSpacing) / bandCount;

        foreach (int i in labelIndices)
        {
            if (i >= Frequencies.Length) continue;

            double x = i * (barWidth + BarSpacing) + barWidth / 2;
            string text = FormatFrequency(Frequencies[i]);

            var label = new TextBlock
            {
                Text = text,
                Foreground = textBrush,
                FontSize = 8,
                TextAlignment = TextAlignment.Center
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, 2);
            FrequencyCanvas.Children.Add(label);
        }
    }

    #endregion

    #region Update Methods

    private void UpdateDisplayLayout()
    {
        if (_bars == null) return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        int bandCount = _bars.Length;
        double barWidth = (width - (bandCount - 1) * BarSpacing) / bandCount;

        for (int i = 0; i < bandCount; i++)
        {
            double x = i * (barWidth + BarSpacing);

            _bars[i].Width = Math.Max(1, barWidth);
            Canvas.SetLeft(_bars[i], x);
            Canvas.SetBottom(_bars[i], 0);

            if (_peakIndicators != null && i < _peakIndicators.Length)
            {
                _peakIndicators[i].Width = Math.Max(1, barWidth);
                Canvas.SetLeft(_peakIndicators[i], x);
            }
        }

        // Redraw scales
        if (ShowDbScale)
        {
            DrawDbScale();
        }

        UpdateFrequencyLabels();
        UpdateBars();
    }

    private void UpdateBars()
    {
        if (_bars == null || Magnitudes == null) return;

        double height = SpectrumCanvas.ActualHeight;
        if (height <= 0) return;

        int count = Math.Min(_bars.Length, Magnitudes.Length);
        float[] peaks = PeakMagnitudes;

        for (int i = 0; i < count; i++)
        {
            // Apply visual smoothing
            double target = Magnitudes[i];
            if (_displayedMagnitudes != null)
            {
                _displayedMagnitudes[i] = _displayedMagnitudes[i] * SmoothingFactor +
                                          target * (1 - SmoothingFactor);
                target = _displayedMagnitudes[i];
            }

            // Update bar height
            double barHeight = height * Math.Clamp(target, 0, 1);
            _bars[i].Height = Math.Max(0, barHeight);

            // Update peak indicator
            if (ShowPeakHold && peaks != null && i < peaks.Length && _peakIndicators != null)
            {
                double peakHeight = height * Math.Clamp(peaks[i], 0, 1);
                Canvas.SetBottom(_peakIndicators[i], peakHeight);
                _peakIndicators[i].Visibility = Visibility.Visible;
            }
        }
    }

    private void UpdateBarStyles()
    {
        if (_bars == null) return;

        Brush barBrush = BarColor ?? FindResource("BarGradientBrush") as Brush ?? Brushes.Cyan;

        foreach (var bar in _bars)
        {
            if (UseOutlineStyle)
            {
                bar.Fill = Brushes.Transparent;
                bar.Stroke = barBrush;
                bar.StrokeThickness = 1;
            }
            else
            {
                bar.Fill = barBrush;
                bar.Stroke = null;
                bar.StrokeThickness = 0;
            }
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets the display.
    /// </summary>
    public void Reset()
    {
        if (_displayedMagnitudes != null)
        {
            Array.Clear(_displayedMagnitudes, 0, _displayedMagnitudes.Length);
        }

        if (_bars != null)
        {
            foreach (var bar in _bars)
            {
                bar.Height = 0;
            }
        }

        if (_peakIndicators != null)
        {
            foreach (var peak in _peakIndicators)
            {
                Canvas.SetBottom(peak, 0);
            }
        }
    }

    #endregion

    #region Helper Methods

    private static string FormatFrequency(float hz)
    {
        if (hz >= 1000)
        {
            return $"{hz / 1000:F0}k";
        }
        return $"{hz:F0}";
    }

    #endregion
}
