// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Mix Radar analysis control for frequency balance visualization.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Spider/radar chart control for visualizing mix frequency balance across 8 bands.
/// Displays current mix curve (cyan fill) with optional reference curve overlay (white outline).
/// </summary>
public partial class MixRadarControl : UserControl
{
    #region Constants

    private const int BandCount = 8;
    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;
    private const int GridCircleCount = 4;

    /// <summary>Band labels displayed around the radar chart.</summary>
    private static readonly string[] BandLabels =
    {
        "Sub", "Bass", "Low-Mid", "Mid",
        "High-Mid", "Presence", "Brilliance", "Air"
    };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BandLevelsProperty =
        DependencyProperty.Register(nameof(BandLevels), typeof(double[]), typeof(MixRadarControl),
            new PropertyMetadata(null, OnBandLevelsChanged));

    public static readonly DependencyProperty ReferenceLevelsProperty =
        DependencyProperty.Register(nameof(ReferenceLevels), typeof(double[]), typeof(MixRadarControl),
            new PropertyMetadata(null, OnReferenceLevelsChanged));

    public static readonly DependencyProperty ShowReferenceProperty =
        DependencyProperty.Register(nameof(ShowReference), typeof(bool), typeof(MixRadarControl),
            new PropertyMetadata(true, OnShowReferenceChanged));

    public static readonly DependencyProperty OverallBalanceScoreProperty =
        DependencyProperty.Register(nameof(OverallBalanceScore), typeof(double), typeof(MixRadarControl),
            new PropertyMetadata(0.0, OnBalanceScoreChanged));

    /// <summary>
    /// Gets or sets the current mix band levels in dB (8 values).
    /// </summary>
    public double[]? BandLevels
    {
        get => (double[]?)GetValue(BandLevelsProperty);
        set => SetValue(BandLevelsProperty, value);
    }

    /// <summary>
    /// Gets or sets the reference curve band levels in dB (8 values).
    /// </summary>
    public double[]? ReferenceLevels
    {
        get => (double[]?)GetValue(ReferenceLevelsProperty);
        set => SetValue(ReferenceLevelsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether to show the reference curve overlay.
    /// </summary>
    public bool ShowReference
    {
        get => (bool)GetValue(ShowReferenceProperty);
        set => SetValue(ShowReferenceProperty, value);
    }

    /// <summary>
    /// Gets or sets the overall balance score (0-100).
    /// </summary>
    public double OverallBalanceScore
    {
        get => (double)GetValue(OverallBalanceScoreProperty);
        set => SetValue(OverallBalanceScoreProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private Shapes.Polygon? _mixPolygon;
    private Shapes.Polygon? _referencePolygon;

    // Theme colors
    private readonly Color _mixFillColor = Color.FromArgb(0x40, 0x00, 0xD9, 0xFF);
    private readonly Color _mixStrokeColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private readonly Color _referenceFillColor = Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF);
    private readonly Color _referenceStrokeColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
    private readonly Color _gridColor = Color.FromRgb(0x2A, 0x2A, 0x2A);
    private readonly Color _axisColor = Color.FromRgb(0x3A, 0x3A, 0x3A);
    private readonly Color _labelColor = Color.FromRgb(0x80, 0x80, 0x80);
    private readonly Color _accentColor = Color.FromRgb(0x00, 0xD9, 0xFF);

    #endregion

    #region Constructor

    public MixRadarControl()
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

    private static void OnBandLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarControl control && control._isInitialized)
        {
            control.UpdateMixCurve();
            control.UpdateBandValueDisplays();
        }
    }

    private static void OnReferenceLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarControl control && control._isInitialized)
        {
            control.UpdateReferenceCurve();
        }
    }

    private static void OnShowReferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarControl control && control._isInitialized)
        {
            control.UpdateReferenceVisibility();
        }
    }

    private static void OnBalanceScoreChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarControl control && control._isInitialized)
        {
            control.UpdateBalanceDisplay();
        }
    }

    private void OnShowReferenceToggleChanged(object sender, RoutedEventArgs e)
    {
        ShowReference = ShowReferenceToggle.IsChecked ?? false;
    }

    #endregion

    #region Drawing Methods

    private void DrawAll()
    {
        DrawGrid();
        DrawLabels();
        UpdateMixCurve();
        UpdateReferenceCurve();
        UpdateReferenceVisibility();
        UpdateBandValueDisplays();
        UpdateBalanceDisplay();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 35;

        if (maxRadius <= 0) return;

        var gridBrush = new SolidColorBrush(_gridColor);
        var axisBrush = new SolidColorBrush(_axisColor);

        // Draw concentric grid circles
        for (int i = 1; i <= GridCircleCount; i++)
        {
            double factor = (double)i / GridCircleCount;
            double radius = maxRadius * factor;

            var circle = new Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Stroke = i == GridCircleCount ? axisBrush : gridBrush,
                StrokeThickness = i == GridCircleCount ? 1.0 : 0.5,
                StrokeDashArray = i == GridCircleCount ? null : new DoubleCollection { 3, 3 }
            };

            Canvas.SetLeft(circle, centerX - radius);
            Canvas.SetTop(circle, centerY - radius);
            GridCanvas.Children.Add(circle);
        }

        // Draw axis lines from center to each band position
        for (int i = 0; i < BandCount; i++)
        {
            double angle = GetAngleForBand(i);
            double x2 = centerX + Math.Cos(angle) * maxRadius;
            double y2 = centerY + Math.Sin(angle) * maxRadius;

            var line = new Shapes.Line
            {
                X1 = centerX,
                Y1 = centerY,
                X2 = x2,
                Y2 = y2,
                Stroke = axisBrush,
                StrokeThickness = 0.5
            };

            GridCanvas.Children.Add(line);
        }

        // Draw center dot (balance indicator base)
        var centerDot = new Shapes.Ellipse
        {
            Width = 6,
            Height = 6,
            Fill = axisBrush
        };

        Canvas.SetLeft(centerDot, centerX - 3);
        Canvas.SetTop(centerDot, centerY - 3);
        GridCanvas.Children.Add(centerDot);
    }

    private void DrawLabels()
    {
        LabelsCanvas.Children.Clear();

        double width = LabelsCanvas.ActualWidth;
        double height = LabelsCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 35;
        double labelRadius = maxRadius + 18;

        if (maxRadius <= 0) return;

        var labelBrush = new SolidColorBrush(_labelColor);

        for (int i = 0; i < BandCount; i++)
        {
            double angle = GetAngleForBand(i);
            double x = centerX + Math.Cos(angle) * labelRadius;
            double y = centerY + Math.Sin(angle) * labelRadius;

            var label = new TextBlock
            {
                Text = BandLabels[i],
                Foreground = labelBrush,
                FontSize = 9,
                FontWeight = FontWeights.Medium
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            LabelsCanvas.Children.Add(label);
        }
    }

    private void UpdateMixCurve()
    {
        MixCanvas.Children.Clear();

        if (BandLevels == null || BandLevels.Length < BandCount) return;

        double width = MixCanvas.ActualWidth;
        double height = MixCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 35;

        if (maxRadius <= 0) return;

        var points = new PointCollection();

        for (int i = 0; i < BandCount; i++)
        {
            double level = BandLevels[i];
            double normalizedLevel = NormalizeDbLevel(level);
            double radius = normalizedLevel * maxRadius;

            double angle = GetAngleForBand(i);
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;

            points.Add(new Point(x, y));
        }

        _mixPolygon = new Shapes.Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(_mixFillColor),
            Stroke = new SolidColorBrush(_mixStrokeColor),
            StrokeThickness = 2.0,
            StrokeLineJoin = PenLineJoin.Round
        };

        MixCanvas.Children.Add(_mixPolygon);
    }

    private void UpdateReferenceCurve()
    {
        ReferenceCanvas.Children.Clear();

        if (!ShowReference || ReferenceLevels == null || ReferenceLevels.Length < BandCount) return;

        double width = ReferenceCanvas.ActualWidth;
        double height = ReferenceCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 35;

        if (maxRadius <= 0) return;

        var points = new PointCollection();

        for (int i = 0; i < BandCount; i++)
        {
            double level = ReferenceLevels[i];
            double normalizedLevel = NormalizeDbLevel(level);
            double radius = normalizedLevel * maxRadius;

            double angle = GetAngleForBand(i);
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;

            points.Add(new Point(x, y));
        }

        _referencePolygon = new Shapes.Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(_referenceFillColor),
            Stroke = new SolidColorBrush(_referenceStrokeColor),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            StrokeLineJoin = PenLineJoin.Round
        };

        ReferenceCanvas.Children.Add(_referencePolygon);
    }

    private void UpdateReferenceVisibility()
    {
        ReferenceCanvas.Visibility = ShowReference ? Visibility.Visible : Visibility.Collapsed;
        ReferenceLegendPanel.Visibility = ShowReference ? Visibility.Visible : Visibility.Collapsed;

        // Sync toggle button
        if (ShowReferenceToggle.IsChecked != ShowReference)
        {
            ShowReferenceToggle.IsChecked = ShowReference;
        }

        // Redraw reference if visible
        if (ShowReference)
        {
            UpdateReferenceCurve();
        }
    }

    private void UpdateBandValueDisplays()
    {
        if (BandLevels == null || BandLevels.Length < BandCount)
        {
            SubBandText.Text = "--";
            BassBandText.Text = "--";
            LowMidBandText.Text = "--";
            MidBandText.Text = "--";
            HighMidBandText.Text = "--";
            PresenceBandText.Text = "--";
            BrillianceBandText.Text = "--";
            AirBandText.Text = "--";
            return;
        }

        SubBandText.Text = FormatDbValue(BandLevels[0]);
        BassBandText.Text = FormatDbValue(BandLevels[1]);
        LowMidBandText.Text = FormatDbValue(BandLevels[2]);
        MidBandText.Text = FormatDbValue(BandLevels[3]);
        HighMidBandText.Text = FormatDbValue(BandLevels[4]);
        PresenceBandText.Text = FormatDbValue(BandLevels[5]);
        BrillianceBandText.Text = FormatDbValue(BandLevels[6]);
        AirBandText.Text = FormatDbValue(BandLevels[7]);
    }

    private void UpdateBalanceDisplay()
    {
        double score = OverallBalanceScore;

        // Determine balance description and color
        string description;
        Color indicatorColor;

        if (score >= 80)
        {
            description = "Balanced";
            indicatorColor = Color.FromRgb(0x00, 0xCC, 0x66); // Green
        }
        else if (score >= 60)
        {
            description = "Good";
            indicatorColor = Color.FromRgb(0x8B, 0xC3, 0x4A); // Light green
        }
        else if (score >= 40)
        {
            description = "Fair";
            indicatorColor = Color.FromRgb(0xFF, 0xC1, 0x07); // Yellow
        }
        else if (score >= 20)
        {
            description = "Uneven";
            indicatorColor = Color.FromRgb(0xFF, 0x98, 0x00); // Orange
        }
        else if (score > 0)
        {
            description = "Imbalanced";
            indicatorColor = Color.FromRgb(0xFF, 0x47, 0x57); // Red
        }
        else
        {
            description = "No Data";
            indicatorColor = _labelColor;
        }

        BalanceText.Text = description;
        BalanceText.Foreground = new SolidColorBrush(indicatorColor);
        BalanceIndicator.Fill = new SolidColorBrush(indicatorColor);
        ScoreText.Text = score > 0 ? $"Score: {score:F0}" : "Score: --";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the angle in radians for a band index (starting from top, going clockwise).
    /// </summary>
    private static double GetAngleForBand(int bandIndex)
    {
        // Start from top (-PI/2) and go clockwise
        return bandIndex * 2 * Math.PI / BandCount - Math.PI / 2;
    }

    /// <summary>
    /// Normalizes a dB level to a 0-1 range.
    /// </summary>
    private static double NormalizeDbLevel(double db)
    {
        if (double.IsNegativeInfinity(db) || double.IsNaN(db))
            return 0;

        return Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0, 1);
    }

    /// <summary>
    /// Formats a dB value for display.
    /// </summary>
    private static string FormatDbValue(double db)
    {
        if (double.IsNegativeInfinity(db) || double.IsNaN(db) || db <= MinDb)
            return "--";

        return $"{db:F1} dB";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the display from a MixAnalysisResult from MixRadarAnalyzer.
    /// </summary>
    /// <param name="result">The analysis result containing band data.</param>
    public void UpdateFromAnalysisResult(MixAnalysisResult? result)
    {
        if (result == null || result.Bands == null || result.Bands.Length < BandCount)
        {
            BandLevels = null;
            OverallBalanceScore = 0;
            return;
        }

        var levels = new double[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            levels[i] = result.Bands[i].RmsDb;
        }

        BandLevels = levels;
        OverallBalanceScore = result.OverallBalanceScore;
    }

    /// <summary>
    /// Sets the reference curve from a ReferenceCurveType.
    /// </summary>
    /// <param name="curveType">The reference curve type.</param>
    /// <param name="baseLevel">The base level in dB to add to the curve offsets.</param>
    public void SetReferenceCurve(ReferenceCurveType curveType, double baseLevel = -20)
    {
        float[] curveOffsets = MixRadarAnalyzer.GetReferenceCurveValues(curveType);

        var levels = new double[BandCount];
        for (int i = 0; i < BandCount; i++)
        {
            levels[i] = baseLevel + curveOffsets[i];
        }

        ReferenceLevels = levels;
    }

    /// <summary>
    /// Sets custom reference levels directly.
    /// </summary>
    /// <param name="levels">Array of 8 dB values for each band.</param>
    public void SetReferenceLevels(double[] levels)
    {
        if (levels != null && levels.Length >= BandCount)
        {
            var copy = new double[BandCount];
            Array.Copy(levels, copy, BandCount);
            ReferenceLevels = copy;
        }
    }

    /// <summary>
    /// Updates the mix curve from spectrum data.
    /// </summary>
    /// <param name="spectrum">Full spectrum magnitude array.</param>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public void UpdateFromSpectrum(float[] spectrum, int sampleRate)
    {
        if (spectrum == null || spectrum.Length == 0) return;

        // Band frequency boundaries
        (int low, int high)[] bandRanges =
        {
            (20, 60), (60, 250), (250, 500), (500, 2000),
            (2000, 4000), (4000, 6000), (6000, 12000), (12000, 20000)
        };

        var levels = new double[BandCount];
        int spectrumLength = spectrum.Length;
        double binWidth = (double)sampleRate / (2 * spectrumLength);

        for (int band = 0; band < BandCount; band++)
        {
            var (low, high) = bandRanges[band];
            int startBin = (int)(low / binWidth);
            int endBin = Math.Min((int)(high / binWidth), spectrumLength - 1);

            double sum = 0;
            int count = 0;

            for (int bin = startBin; bin <= endBin; bin++)
            {
                if (bin >= 0 && bin < spectrumLength)
                {
                    sum += spectrum[bin] * spectrum[bin];
                    count++;
                }
            }

            if (count > 0)
            {
                double rms = Math.Sqrt(sum / count);
                levels[band] = rms > 0 ? 20 * Math.Log10(rms) : MinDb;
            }
            else
            {
                levels[band] = MinDb;
            }
        }

        BandLevels = levels;

        // Calculate simple balance score
        CalculateBalanceScore(levels);
    }

    /// <summary>
    /// Clears all data from the display.
    /// </summary>
    public void Clear()
    {
        BandLevels = null;
        ReferenceLevels = null;
        OverallBalanceScore = 0;

        MixCanvas.Children.Clear();
        ReferenceCanvas.Children.Clear();

        UpdateBandValueDisplays();
        UpdateBalanceDisplay();
    }

    #endregion

    #region Private Calculation Methods

    private void CalculateBalanceScore(double[] levels)
    {
        if (levels == null || levels.Length < BandCount)
        {
            OverallBalanceScore = 0;
            return;
        }

        // Calculate average level
        double sum = 0;
        int validCount = 0;

        for (int i = 0; i < BandCount; i++)
        {
            if (!double.IsNegativeInfinity(levels[i]) && !double.IsNaN(levels[i]) && levels[i] > MinDb)
            {
                sum += levels[i];
                validCount++;
            }
        }

        if (validCount == 0)
        {
            OverallBalanceScore = 0;
            return;
        }

        double average = sum / validCount;

        // Calculate standard deviation
        double sumSquares = 0;
        for (int i = 0; i < BandCount; i++)
        {
            if (!double.IsNegativeInfinity(levels[i]) && !double.IsNaN(levels[i]) && levels[i] > MinDb)
            {
                double diff = levels[i] - average;
                sumSquares += diff * diff;
            }
        }

        double stdDev = Math.Sqrt(sumSquares / validCount);

        // Score from 0-100 based on stdDev (lower deviation = better score)
        // Using 10dB as reference for "bad" balance
        OverallBalanceScore = Math.Max(0, 100 - stdDev * 10);
    }

    #endregion
}
