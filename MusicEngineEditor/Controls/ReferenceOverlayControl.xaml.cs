// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for overlaying reference track spectrum on mix spectrum.
/// Shows frequency band comparison with difference highlighting.
/// </summary>
public partial class ReferenceOverlayControl : UserControl
{
    #region Constants

    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;

    // Frequency band boundaries (Hz)
    private static readonly double[] BandFrequencies = { 60, 250, 500, 1000, 2000, 4000, 8000, 12000 };
    private static readonly string[] BandNames = { "Sub", "Bass", "Low", "LMid", "Mid", "HMid", "Pres", "Air" };

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty MixSpectrumProperty =
        DependencyProperty.Register(nameof(MixSpectrum), typeof(float[]), typeof(ReferenceOverlayControl),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty ReferenceSpectrumProperty =
        DependencyProperty.Register(nameof(ReferenceSpectrum), typeof(float[]), typeof(ReferenceOverlayControl),
            new PropertyMetadata(null, OnSpectrumChanged));

    public static readonly DependencyProperty FrequenciesProperty =
        DependencyProperty.Register(nameof(Frequencies), typeof(float[]), typeof(ReferenceOverlayControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsReferenceModeProperty =
        DependencyProperty.Register(nameof(IsReferenceMode), typeof(bool), typeof(ReferenceOverlayControl),
            new PropertyMetadata(false));

    public static readonly DependencyProperty AutoLevelMatchProperty =
        DependencyProperty.Register(nameof(AutoLevelMatch), typeof(bool), typeof(ReferenceOverlayControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ShowDifferenceProperty =
        DependencyProperty.Register(nameof(ShowDifference), typeof(bool), typeof(ReferenceOverlayControl),
            new PropertyMetadata(true));

    public float[]? MixSpectrum
    {
        get => (float[]?)GetValue(MixSpectrumProperty);
        set => SetValue(MixSpectrumProperty, value);
    }

    public float[]? ReferenceSpectrum
    {
        get => (float[]?)GetValue(ReferenceSpectrumProperty);
        set => SetValue(ReferenceSpectrumProperty, value);
    }

    public float[]? Frequencies
    {
        get => (float[]?)GetValue(FrequenciesProperty);
        set => SetValue(FrequenciesProperty, value);
    }

    public bool IsReferenceMode
    {
        get => (bool)GetValue(IsReferenceModeProperty);
        set => SetValue(IsReferenceModeProperty, value);
    }

    public bool AutoLevelMatch
    {
        get => (bool)GetValue(AutoLevelMatchProperty);
        set => SetValue(AutoLevelMatchProperty, value);
    }

    public bool ShowDifference
    {
        get => (bool)GetValue(ShowDifferenceProperty);
        set => SetValue(ShowDifferenceProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private double[]? _bandDifferences;

    // Colors
    private readonly Color _mixColor = Color.FromRgb(0x00, 0xCE, 0xD1);
    private readonly Color _referenceColor = Color.FromRgb(0xFF, 0x98, 0x00);
    private readonly Color _positiveColor = Color.FromRgb(0x4C, 0xAF, 0x50);
    private readonly Color _negativeColor = Color.FromRgb(0xF4, 0x43, 0x36);
    private readonly Color _neutralColor = Color.FromRgb(0x55, 0x55, 0x55);

    #endregion

    #region Constructor

    public ReferenceOverlayControl()
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

    private static void OnSpectrumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ReferenceOverlayControl control && control._isInitialized)
        {
            control.UpdateSpectrumDisplay();
        }
    }

    private void ABToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsReferenceMode = ABToggle.IsChecked ?? false;
    }

    private void LevelMatchToggle_Changed(object sender, RoutedEventArgs e)
    {
        AutoLevelMatch = LevelMatchToggle.IsChecked ?? false;
        UpdateSpectrumDisplay();
    }

    private void ShowDifferenceToggle_Changed(object sender, RoutedEventArgs e)
    {
        ShowDifference = ShowDifferenceToggle.IsChecked ?? false;
        UpdateSpectrumDisplay();
    }

    #endregion

    #region Drawing

    private void DrawAll()
    {
        DrawGrid();
        DrawDbScale();
        DrawFrequencyLabels();
        UpdateSpectrumDisplay();
    }

    private void DrawGrid()
    {
        GridCanvas.Children.Clear();

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        double width = GridCanvas.ActualWidth;
        double height = GridCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Horizontal grid lines (dB)
        double[] dbLines = { -12, -24, -36, -48, -60, -72 };
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
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }

        // Vertical grid lines (frequency band boundaries)
        foreach (var freq in BandFrequencies)
        {
            double x = FrequencyToX(freq, width);
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            GridCanvas.Children.Add(line);
        }
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A));
        double height = GridCanvas.ActualHeight > 0 ? GridCanvas.ActualHeight : 150;

        double[] dbMarks = { 0, -12, -24, -36, -48, -60 };
        foreach (var db in dbMarks)
        {
            double y = DbToY(db, height);

            var label = new TextBlock
            {
                Text = db.ToString(),
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

    private void UpdateSpectrumDisplay()
    {
        MixSpectrumCanvas.Children.Clear();
        ReferenceSpectrumCanvas.Children.Clear();
        DifferenceCanvas.Children.Clear();

        double width = MixSpectrumCanvas.ActualWidth;
        double height = MixSpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        bool hasReference = ReferenceSpectrum != null && ReferenceSpectrum.Length > 0;
        NoReferenceText.Visibility = hasReference ? Visibility.Collapsed : Visibility.Visible;

        // Draw mix spectrum
        if (MixSpectrum != null && MixSpectrum.Length > 0)
        {
            DrawSpectrum(MixSpectrumCanvas, MixSpectrum, _mixColor, width, height);
        }

        // Draw reference spectrum
        if (hasReference)
        {
            float[] adjustedReference = ReferenceSpectrum!;

            // Apply level matching if enabled
            if (AutoLevelMatch && MixSpectrum != null && ReferenceSpectrum != null)
            {
                adjustedReference = ApplyLevelMatching(ReferenceSpectrum, MixSpectrum);
            }

            DrawSpectrum(ReferenceSpectrumCanvas, adjustedReference, _referenceColor, width, height);

            // Draw difference fill
            if (ShowDifference && MixSpectrum != null)
            {
                DrawDifference(MixSpectrum, adjustedReference, width, height);
            }

            // Update band indicators
            UpdateBandIndicators(MixSpectrum, adjustedReference);
        }
    }

    private void DrawSpectrum(Canvas canvas, float[] spectrum, Color color, double width, double height)
    {
        if (spectrum.Length < 2) return;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            bool started = false;

            for (int i = 0; i < spectrum.Length; i++)
            {
                double freqRatio = (double)i / (spectrum.Length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                double db = spectrum[i] > 0 ? 20 * Math.Log10(spectrum[i]) : MinDb;
                double y = DbToY(db, height);
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
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round
        };

        canvas.Children.Add(path);
    }

    private void DrawDifference(float[] mix, float[] reference, double width, double height)
    {
        if (mix.Length < 2 || reference.Length < 2) return;

        int length = Math.Min(mix.Length, reference.Length);

        // Create filled area between curves where there's significant difference
        var positiveGeometry = new StreamGeometry();
        var negativeGeometry = new StreamGeometry();

        using (var posContext = positiveGeometry.Open())
        using (var negContext = negativeGeometry.Open())
        {
            bool posStarted = false;
            bool negStarted = false;
            double lastX = 0;
            double lastMixY = height;
            double lastRefY = height;

            for (int i = 0; i < length; i++)
            {
                double freqRatio = (double)i / (length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);
                double x = FrequencyToX(freq, width);

                double mixDb = mix[i] > 0 ? 20 * Math.Log10(mix[i]) : MinDb;
                double refDb = reference[i] > 0 ? 20 * Math.Log10(reference[i]) : MinDb;

                double mixY = Math.Clamp(DbToY(mixDb, height), 0, height);
                double refY = Math.Clamp(DbToY(refDb, height), 0, height);

                double diff = refDb - mixDb;

                // Only show significant differences (> 3dB)
                if (Math.Abs(diff) > 3)
                {
                    if (diff > 0) // Mix is louder (reference is higher on screen, lower level)
                    {
                        if (!negStarted)
                        {
                            negContext.BeginFigure(new Point(x, mixY), true, true);
                            negStarted = true;
                        }
                        negContext.LineTo(new Point(x, refY), true, false);
                    }
                    else // Reference is louder
                    {
                        if (!posStarted)
                        {
                            posContext.BeginFigure(new Point(x, mixY), true, true);
                            posStarted = true;
                        }
                        posContext.LineTo(new Point(x, refY), true, false);
                    }
                }

                lastX = x;
                lastMixY = mixY;
                lastRefY = refY;
            }
        }

        positiveGeometry.Freeze();
        negativeGeometry.Freeze();

        // Add positive (reference louder) fill
        var positivePath = new Shapes.Path
        {
            Data = positiveGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, _positiveColor.R, _positiveColor.G, _positiveColor.B)),
            Stroke = null
        };
        DifferenceCanvas.Children.Add(positivePath);

        // Add negative (mix louder) fill
        var negativePath = new Shapes.Path
        {
            Data = negativeGeometry,
            Fill = new SolidColorBrush(Color.FromArgb(0x40, _negativeColor.R, _negativeColor.G, _negativeColor.B)),
            Stroke = null
        };
        DifferenceCanvas.Children.Add(negativePath);
    }

    private float[] ApplyLevelMatching(float[] reference, float[] mix)
    {
        // Calculate RMS of both
        double mixRms = 0;
        double refRms = 0;

        for (int i = 0; i < mix.Length; i++)
        {
            mixRms += mix[i] * mix[i];
        }
        mixRms = Math.Sqrt(mixRms / mix.Length);

        for (int i = 0; i < reference.Length; i++)
        {
            refRms += reference[i] * reference[i];
        }
        refRms = Math.Sqrt(refRms / reference.Length);

        // Calculate gain factor
        float gain = mixRms > 0 && refRms > 0 ? (float)(mixRms / refRms) : 1.0f;

        // Apply gain
        var adjusted = new float[reference.Length];
        for (int i = 0; i < reference.Length; i++)
        {
            adjusted[i] = reference[i] * gain;
        }

        return adjusted;
    }

    private void UpdateBandIndicators(float[]? mix, float[]? reference)
    {
        _bandDifferences = new double[8];

        if (mix == null || reference == null || mix.Length == 0 || reference.Length == 0)
        {
            // Reset all indicators to neutral
            UpdateBandIndicator(Band1Indicator, 0);
            UpdateBandIndicator(Band2Indicator, 0);
            UpdateBandIndicator(Band3Indicator, 0);
            UpdateBandIndicator(Band4Indicator, 0);
            UpdateBandIndicator(Band5Indicator, 0);
            UpdateBandIndicator(Band6Indicator, 0);
            UpdateBandIndicator(Band7Indicator, 0);
            UpdateBandIndicator(Band8Indicator, 0);
            return;
        }

        // Calculate average difference in each band
        double[] bandStartFreqs = { 20, 60, 250, 500, 1000, 2000, 4000, 8000 };
        double[] bandEndFreqs = { 60, 250, 500, 1000, 2000, 4000, 8000, 20000 };
        Border[] indicators = { Band1Indicator, Band2Indicator, Band3Indicator, Band4Indicator,
                               Band5Indicator, Band6Indicator, Band7Indicator, Band8Indicator };

        int length = Math.Min(mix.Length, reference.Length);

        for (int band = 0; band < 8; band++)
        {
            double sumDiff = 0;
            int count = 0;

            for (int i = 0; i < length; i++)
            {
                double freqRatio = (double)i / (length - 1);
                double freq = MinFrequency * Math.Pow(MaxFrequency / MinFrequency, freqRatio);

                if (freq >= bandStartFreqs[band] && freq < bandEndFreqs[band])
                {
                    double mixDb = mix[i] > 0 ? 20 * Math.Log10(mix[i]) : MinDb;
                    double refDb = reference[i] > 0 ? 20 * Math.Log10(reference[i]) : MinDb;
                    sumDiff += mixDb - refDb;
                    count++;
                }
            }

            double avgDiff = count > 0 ? sumDiff / count : 0;
            _bandDifferences[band] = avgDiff;
            UpdateBandIndicator(indicators[band], avgDiff);
        }
    }

    private void UpdateBandIndicator(Border indicator, double difference)
    {
        Color color;
        if (Math.Abs(difference) < 3)
        {
            color = _neutralColor;
        }
        else if (difference > 0)
        {
            color = _positiveColor; // Mix is louder
        }
        else
        {
            color = _negativeColor; // Reference is louder
        }

        indicator.Background = new SolidColorBrush(color);
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

    private static double DbToY(double db, double height)
    {
        double normalized = (db - MinDb) / (MaxDb - MinDb);
        return height * (1 - normalized);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the spectrum data.
    /// </summary>
    public void UpdateSpectrums(float[] mix, float[]? reference, float[]? frequencies = null)
    {
        MixSpectrum = mix;
        ReferenceSpectrum = reference;
        if (frequencies != null)
        {
            Frequencies = frequencies;
        }
    }

    /// <summary>
    /// Gets the frequency band differences (mix - reference in dB).
    /// </summary>
    public double[]? GetBandDifferences()
    {
        return _bandDifferences;
    }

    /// <summary>
    /// Clears all spectrum data.
    /// </summary>
    public void Clear()
    {
        MixSpectrum = null;
        ReferenceSpectrum = null;
    }

    #endregion
}
