// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Circular frequency balance radar display showing 8 frequency bands.
/// Provides visual representation of mix frequency balance with optional reference overlay.
/// </summary>
public partial class MixRadarView : UserControl
{
    #region Constants

    private const int BandCount = 8;
    private const double MinDb = -60.0;
    private const double MaxDb = 0.0;

    // Band names and frequency ranges
    private static readonly string[] BandNames = { "Sub", "Bass", "LowMid", "Mid", "HiMid", "Presence", "Brilliance", "Air" };
    private static readonly (int low, int high)[] BandRanges =
    {
        (20, 60), (60, 250), (250, 500), (500, 2000),
        (2000, 4000), (4000, 8000), (8000, 12000), (12000, 20000)
    };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty BandLevelsProperty =
        DependencyProperty.Register(nameof(BandLevels), typeof(double[]), typeof(MixRadarView),
            new PropertyMetadata(null, OnBandLevelsChanged));

    public static readonly DependencyProperty ReferenceLevelsProperty =
        DependencyProperty.Register(nameof(ReferenceLevels), typeof(double[]), typeof(MixRadarView),
            new PropertyMetadata(null, OnReferenceLevelsChanged));

    public static readonly DependencyProperty ShowReferenceProperty =
        DependencyProperty.Register(nameof(ShowReference), typeof(bool), typeof(MixRadarView),
            new PropertyMetadata(false, OnShowReferenceChanged));

    public static readonly DependencyProperty AnimateOnUpdateProperty =
        DependencyProperty.Register(nameof(AnimateOnUpdate), typeof(bool), typeof(MixRadarView),
            new PropertyMetadata(true));

    public double[]? BandLevels
    {
        get => (double[]?)GetValue(BandLevelsProperty);
        set => SetValue(BandLevelsProperty, value);
    }

    public double[]? ReferenceLevels
    {
        get => (double[]?)GetValue(ReferenceLevelsProperty);
        set => SetValue(ReferenceLevelsProperty, value);
    }

    public bool ShowReference
    {
        get => (bool)GetValue(ShowReferenceProperty);
        set => SetValue(ShowReferenceProperty, value);
    }

    public bool AnimateOnUpdate
    {
        get => (bool)GetValue(AnimateOnUpdateProperty);
        set => SetValue(AnimateOnUpdateProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private double[] _displayedLevels = new double[BandCount];
    private Shapes.Polygon? _mixPolygon;
    private Shapes.Polygon? _referencePolygon;

    // Colors
    private readonly Color _mixFillColor = Color.FromArgb(0x60, 0x00, 0xCE, 0xD1);
    private readonly Color _mixStrokeColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _referenceFillColor = Color.FromArgb(0x40, 0xFF, 0x98, 0x00);
    private readonly Color _referenceStrokeColor = Color.FromRgb(0xFF, 0x98, 0x00);

    #endregion

    #region Constructor

    public MixRadarView()
    {
        InitializeComponent();

        // Initialize displayed levels
        for (int i = 0; i < BandCount; i++)
        {
            _displayedLevels[i] = MinDb;
        }

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
        if (d is MixRadarView view && view._isInitialized)
        {
            view.UpdateMixRadar();
        }
    }

    private static void OnReferenceLevelsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarView view && view._isInitialized)
        {
            view.UpdateReferenceRadar();
        }
    }

    private static void OnShowReferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MixRadarView view && view._isInitialized)
        {
            view.UpdateReferenceVisibility();
        }
    }

    private void ShowReferenceToggle_Changed(object sender, RoutedEventArgs e)
    {
        ShowReference = ShowReferenceToggle.IsChecked ?? false;
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawLabels();
        UpdateMixRadar();
        UpdateReferenceRadar();
        UpdateReferenceVisibility();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        var axisBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4D, 0x52));

        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 30;

        if (maxRadius <= 0) return;

        // Draw concentric circles
        double[] radiusFactors = { 0.25, 0.5, 0.75, 1.0 };
        foreach (var factor in radiusFactors)
        {
            var circle = new Shapes.Ellipse
            {
                Width = maxRadius * 2 * factor,
                Height = maxRadius * 2 * factor,
                Stroke = gridBrush,
                StrokeThickness = factor == 1.0 ? 1 : 0.5,
                StrokeDashArray = factor == 1.0 ? null : new DoubleCollection { 2, 2 }
            };
            Canvas.SetLeft(circle, centerX - maxRadius * factor);
            Canvas.SetTop(circle, centerY - maxRadius * factor);
            GridCanvas.Children.Add(circle);
        }

        // Draw axis lines for each band
        for (int i = 0; i < BandCount; i++)
        {
            double angle = i * 2 * Math.PI / BandCount - Math.PI / 2;
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

        // Draw center dot
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
        double maxRadius = Math.Min(width, height) / 2 - 30;
        double labelRadius = maxRadius + 15;

        if (maxRadius <= 0) return;

        for (int i = 0; i < BandCount; i++)
        {
            double angle = i * 2 * Math.PI / BandCount - Math.PI / 2;
            double x = centerX + Math.Cos(angle) * labelRadius;
            double y = centerY + Math.Sin(angle) * labelRadius;

            var label = new TextBlock
            {
                Text = BandNames[i],
                Foreground = textBrush,
                FontSize = 9
            };

            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
            Canvas.SetTop(label, y - label.DesiredSize.Height / 2);
            LabelsCanvas.Children.Add(label);
        }
    }

    private void UpdateMixRadar()
    {
        MixCanvas.Children.Clear();

        double width = MixCanvas.ActualWidth;
        double height = MixCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 30;

        if (maxRadius <= 0 || BandLevels == null || BandLevels.Length < BandCount) return;

        // Calculate points
        var points = new PointCollection();
        for (int i = 0; i < BandCount; i++)
        {
            double level = BandLevels[i];
            double normalizedLevel = Math.Clamp((level - MinDb) / (MaxDb - MinDb), 0, 1);
            double radius = normalizedLevel * maxRadius;

            double angle = i * 2 * Math.PI / BandCount - Math.PI / 2;
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;

            points.Add(new Point(x, y));

            // Update displayed levels for animation
            _displayedLevels[i] = level;
        }

        // Create polygon
        _mixPolygon = new Shapes.Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(_mixFillColor),
            Stroke = new SolidColorBrush(_mixStrokeColor),
            StrokeThickness = 2
        };

        MixCanvas.Children.Add(_mixPolygon);

        // Update band value displays
        UpdateBandDisplays();
        UpdateBalanceScore();
    }

    private void UpdateReferenceRadar()
    {
        ReferenceCanvas.Children.Clear();

        if (!ShowReference || ReferenceLevels == null || ReferenceLevels.Length < BandCount) return;

        double width = ReferenceCanvas.ActualWidth;
        double height = ReferenceCanvas.ActualHeight;
        double centerX = width / 2;
        double centerY = height / 2;
        double maxRadius = Math.Min(width, height) / 2 - 30;

        if (maxRadius <= 0) return;

        // Calculate points
        var points = new PointCollection();
        for (int i = 0; i < BandCount; i++)
        {
            double level = ReferenceLevels[i];
            double normalizedLevel = Math.Clamp((level - MinDb) / (MaxDb - MinDb), 0, 1);
            double radius = normalizedLevel * maxRadius;

            double angle = i * 2 * Math.PI / BandCount - Math.PI / 2;
            double x = centerX + Math.Cos(angle) * radius;
            double y = centerY + Math.Sin(angle) * radius;

            points.Add(new Point(x, y));
        }

        // Create polygon
        _referencePolygon = new Shapes.Polygon
        {
            Points = points,
            Fill = new SolidColorBrush(_referenceFillColor),
            Stroke = new SolidColorBrush(_referenceStrokeColor),
            StrokeThickness = 1.5,
            StrokeDashArray = new DoubleCollection { 4, 2 }
        };

        ReferenceCanvas.Children.Add(_referencePolygon);
    }

    private void UpdateReferenceVisibility()
    {
        ReferenceCanvas.Visibility = ShowReference ? Visibility.Visible : Visibility.Collapsed;
        ReferenceLegend.Visibility = ShowReference ? Visibility.Visible : Visibility.Collapsed;

        UpdateReferenceRadar();
    }

    private void UpdateBandDisplays()
    {
        if (BandLevels == null || BandLevels.Length < BandCount) return;

        SubBandText.Text = FormatDb(BandLevels[0]);
        BassBandText.Text = FormatDb(BandLevels[1]);
        LowMidBandText.Text = FormatDb(BandLevels[2]);
        MidBandText.Text = FormatDb(BandLevels[3]);
        HighMidBandText.Text = FormatDb(BandLevels[4]);
        PresenceBandText.Text = FormatDb(BandLevels[5]);
        BrillianceBandText.Text = FormatDb(BandLevels[6]);
        AirBandText.Text = FormatDb(BandLevels[7]);
    }

    private void UpdateBalanceScore()
    {
        if (BandLevels == null || BandLevels.Length < BandCount)
        {
            BalanceText.Text = "No data";
            ScoreText.Text = "Score: --";
            return;
        }

        // Calculate balance score based on deviation from average
        double average = 0;
        int validCount = 0;
        for (int i = 0; i < BandCount; i++)
        {
            if (!double.IsNegativeInfinity(BandLevels[i]))
            {
                average += BandLevels[i];
                validCount++;
            }
        }

        if (validCount == 0)
        {
            BalanceText.Text = "Silent";
            ScoreText.Text = "Score: --";
            return;
        }

        average /= validCount;

        // Calculate standard deviation
        double sumSquares = 0;
        for (int i = 0; i < BandCount; i++)
        {
            if (!double.IsNegativeInfinity(BandLevels[i]))
            {
                double diff = BandLevels[i] - average;
                sumSquares += diff * diff;
            }
        }
        double stdDev = Math.Sqrt(sumSquares / validCount);

        // Score from 0-100 based on stdDev (lower is better)
        double score = Math.Max(0, 100 - stdDev * 10);

        // Determine balance description
        string balanceDescription;
        Color balanceColor;

        if (score >= 80)
        {
            balanceDescription = "Balanced";
            balanceColor = Color.FromRgb(0x4C, 0xAF, 0x50); // Green
        }
        else if (score >= 60)
        {
            balanceDescription = "Good";
            balanceColor = Color.FromRgb(0x8B, 0xC3, 0x4A); // Light green
        }
        else if (score >= 40)
        {
            balanceDescription = "Fair";
            balanceColor = Color.FromRgb(0xFF, 0xC1, 0x07); // Yellow
        }
        else if (score >= 20)
        {
            balanceDescription = "Uneven";
            balanceColor = Color.FromRgb(0xFF, 0x98, 0x00); // Orange
        }
        else
        {
            balanceDescription = "Imbalanced";
            balanceColor = Color.FromRgb(0xF4, 0x43, 0x36); // Red
        }

        BalanceText.Text = balanceDescription;
        BalanceText.Foreground = new SolidColorBrush(balanceColor);
        ScoreText.Text = $"Score: {score:F0}";
    }

    #endregion

    #region Helpers

    private static string FormatDb(double db)
    {
        if (double.IsNegativeInfinity(db) || double.IsNaN(db))
            return "--";
        return $"{db:F1}";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the band levels from spectrum data.
    /// </summary>
    /// <param name="spectrum">Full spectrum magnitude array</param>
    /// <param name="sampleRate">Sample rate in Hz</param>
    public void UpdateFromSpectrum(float[] spectrum, int sampleRate)
    {
        if (spectrum == null || spectrum.Length == 0) return;

        var levels = new double[BandCount];
        int spectrumLength = spectrum.Length;
        double binWidth = (double)sampleRate / (2 * spectrumLength);

        for (int band = 0; band < BandCount; band++)
        {
            var (low, high) = BandRanges[band];
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
    }

    /// <summary>
    /// Sets the reference levels for comparison.
    /// </summary>
    public void SetReferenceLevels(double[] levels)
    {
        if (levels != null && levels.Length >= BandCount)
        {
            ReferenceLevels = levels;
        }
    }

    /// <summary>
    /// Clears all data.
    /// </summary>
    public void Clear()
    {
        BandLevels = null;
        ReferenceLevels = null;
        MixCanvas.Children.Clear();
        ReferenceCanvas.Children.Clear();
    }

    /// <summary>
    /// Gets the current band levels.
    /// </summary>
    public double[]? GetBandLevels() => BandLevels;

    #endregion
}
