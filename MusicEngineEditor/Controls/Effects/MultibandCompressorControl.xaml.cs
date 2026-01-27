// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control for multiband compressor with frequency spectrum display and draggable crossover points.

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Effects.Dynamics;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Multiband Compressor editor control with frequency spectrum display,
/// draggable band split points, per-band controls, and gain reduction meters.
/// </summary>
public partial class MultibandCompressorControl : UserControl
{
    #region Constants

    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const double MaxGainReduction = 30.0;

    // Band colors
    private static readonly Color LowBandColor = Color.FromRgb(0xFF, 0x6B, 0x6B);
    private static readonly Color LowMidBandColor = Color.FromRgb(0xFF, 0xA5, 0x00);
    private static readonly Color HighMidBandColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color HighBandColor = Color.FromRgb(0x00, 0xD9, 0xFF);

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty CompressorProperty =
        DependencyProperty.Register(nameof(Compressor), typeof(MultibandCompressor), typeof(MultibandCompressorControl),
            new PropertyMetadata(null, OnCompressorChanged));

    /// <summary>
    /// Gets or sets the multiband compressor instance to edit.
    /// </summary>
    public MultibandCompressor? Compressor
    {
        get => (MultibandCompressor?)GetValue(CompressorProperty);
        set => SetValue(CompressorProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private bool _isDraggingCrossover;
    private int _draggingCrossoverIndex = -1;
    private double _crossoverLow = 200;
    private double _crossoverMid = 1000;
    private double _crossoverHigh = 5000;

    private DispatcherTimer? _updateTimer;
    private readonly double[] _gainReductions = new double[4];
    private readonly double[] _displayedGainReductions = new double[4];

    // Crossover handle rectangles for hit testing
    private Shapes.Rectangle? _crossoverLowHandle;
    private Shapes.Rectangle? _crossoverMidHandle;
    private Shapes.Rectangle? _crossoverHighHandle;

    #endregion

    #region Constructor

    public MultibandCompressorControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    #endregion

    #region Event Handlers - Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        DrawFrequencyScale();
        DrawDbScale();
        DrawBandRegions();
        DrawCrossoverHandles();
        StartUpdateTimer();
        UpdateFromCompressor();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopUpdateTimer();
        _isInitialized = false;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawFrequencyScale();
            DrawDbScale();
            DrawBandRegions();
            DrawCrossoverHandles();
        }
    }

    private static void OnCompressorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MultibandCompressorControl control && control._isInitialized)
        {
            control.UpdateFromCompressor();
        }
    }

    #endregion

    #region Event Handlers - Parameter Changes

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || Compressor == null) return;

        if (sender == LowThresholdSlider)
        {
            Compressor.LowBand.Threshold = (float)e.NewValue;
            LowThresholdText.Text = $"{e.NewValue:F0} dB";
        }
        else if (sender == LowMidThresholdSlider)
        {
            Compressor.LowMidBand.Threshold = (float)e.NewValue;
            LowMidThresholdText.Text = $"{e.NewValue:F0} dB";
        }
        else if (sender == HighMidThresholdSlider)
        {
            Compressor.HighMidBand.Threshold = (float)e.NewValue;
            HighMidThresholdText.Text = $"{e.NewValue:F0} dB";
        }
        else if (sender == HighThresholdSlider)
        {
            Compressor.HighBand.Threshold = (float)e.NewValue;
            HighThresholdText.Text = $"{e.NewValue:F0} dB";
        }
    }

    private void OnRatioChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || Compressor == null) return;

        if (sender == LowRatioSlider)
        {
            Compressor.LowBand.Ratio = (float)e.NewValue;
            LowRatioText.Text = $"{e.NewValue:F1}:1";
        }
        else if (sender == LowMidRatioSlider)
        {
            Compressor.LowMidBand.Ratio = (float)e.NewValue;
            LowMidRatioText.Text = $"{e.NewValue:F1}:1";
        }
        else if (sender == HighMidRatioSlider)
        {
            Compressor.HighMidBand.Ratio = (float)e.NewValue;
            HighMidRatioText.Text = $"{e.NewValue:F1}:1";
        }
        else if (sender == HighRatioSlider)
        {
            Compressor.HighBand.Ratio = (float)e.NewValue;
            HighRatioText.Text = $"{e.NewValue:F1}:1";
        }
    }

    private void OnAttackChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || Compressor == null) return;

        float attackSeconds = (float)(e.NewValue / 1000.0);

        if (sender == LowAttackSlider)
        {
            Compressor.LowBand.Attack = attackSeconds;
            LowAttackText.Text = $"{e.NewValue:F1} ms";
        }
        else if (sender == LowMidAttackSlider)
        {
            Compressor.LowMidBand.Attack = attackSeconds;
            LowMidAttackText.Text = $"{e.NewValue:F1} ms";
        }
        else if (sender == HighMidAttackSlider)
        {
            Compressor.HighMidBand.Attack = attackSeconds;
            HighMidAttackText.Text = $"{e.NewValue:F1} ms";
        }
        else if (sender == HighAttackSlider)
        {
            Compressor.HighBand.Attack = attackSeconds;
            HighAttackText.Text = $"{e.NewValue:F1} ms";
        }
    }

    private void OnReleaseChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || Compressor == null) return;

        float releaseSeconds = (float)(e.NewValue / 1000.0);

        if (sender == LowReleaseSlider)
        {
            Compressor.LowBand.Release = releaseSeconds;
            LowReleaseText.Text = $"{e.NewValue:F0} ms";
        }
        else if (sender == LowMidReleaseSlider)
        {
            Compressor.LowMidBand.Release = releaseSeconds;
            LowMidReleaseText.Text = $"{e.NewValue:F0} ms";
        }
        else if (sender == HighMidReleaseSlider)
        {
            Compressor.HighMidBand.Release = releaseSeconds;
            HighMidReleaseText.Text = $"{e.NewValue:F0} ms";
        }
        else if (sender == HighReleaseSlider)
        {
            Compressor.HighBand.Release = releaseSeconds;
            HighReleaseText.Text = $"{e.NewValue:F0} ms";
        }
    }

    private void OnGainChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_isInitialized || Compressor == null) return;

        if (sender == LowGainSlider)
        {
            Compressor.LowBand.Gain = (float)e.NewValue;
            LowGainText.Text = $"{e.NewValue:F1} dB";
        }
        else if (sender == LowMidGainSlider)
        {
            Compressor.LowMidBand.Gain = (float)e.NewValue;
            LowMidGainText.Text = $"{e.NewValue:F1} dB";
        }
        else if (sender == HighMidGainSlider)
        {
            Compressor.HighMidBand.Gain = (float)e.NewValue;
            HighMidGainText.Text = $"{e.NewValue:F1} dB";
        }
        else if (sender == HighGainSlider)
        {
            Compressor.HighBand.Gain = (float)e.NewValue;
            HighGainText.Text = $"{e.NewValue:F1} dB";
        }
    }

    private void OnSoloChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || Compressor == null) return;

        if (sender == LowSoloButton)
            Compressor.LowBand.Solo = LowSoloButton.IsChecked == true;
        else if (sender == LowMidSoloButton)
            Compressor.LowMidBand.Solo = LowMidSoloButton.IsChecked == true;
        else if (sender == HighMidSoloButton)
            Compressor.HighMidBand.Solo = HighMidSoloButton.IsChecked == true;
        else if (sender == HighSoloButton)
            Compressor.HighBand.Solo = HighSoloButton.IsChecked == true;
    }

    private void OnBypassChanged(object sender, RoutedEventArgs e)
    {
        if (!_isInitialized || Compressor == null) return;

        if (sender == LowBypassButton)
            Compressor.LowBand.Bypass = LowBypassButton.IsChecked == true;
        else if (sender == LowMidBypassButton)
            Compressor.LowMidBand.Bypass = LowMidBypassButton.IsChecked == true;
        else if (sender == HighMidBypassButton)
            Compressor.HighMidBand.Bypass = HighMidBypassButton.IsChecked == true;
        else if (sender == HighBypassButton)
            Compressor.HighBand.Bypass = HighBypassButton.IsChecked == true;
    }

    #endregion

    #region Event Handlers - Crossover Dragging

    private void OnBandRegionsMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (BandRegionsCanvas.ActualWidth <= 0) return;

        var pos = e.GetPosition(BandRegionsCanvas);
        double width = BandRegionsCanvas.ActualWidth;

        // Check if clicking near a crossover line
        double lowX = FrequencyToX(_crossoverLow, width);
        double midX = FrequencyToX(_crossoverMid, width);
        double highX = FrequencyToX(_crossoverHigh, width);

        const double hitTolerance = 8;

        if (Math.Abs(pos.X - lowX) < hitTolerance)
        {
            _isDraggingCrossover = true;
            _draggingCrossoverIndex = 0;
            BandRegionsCanvas.CaptureMouse();
        }
        else if (Math.Abs(pos.X - midX) < hitTolerance)
        {
            _isDraggingCrossover = true;
            _draggingCrossoverIndex = 1;
            BandRegionsCanvas.CaptureMouse();
        }
        else if (Math.Abs(pos.X - highX) < hitTolerance)
        {
            _isDraggingCrossover = true;
            _draggingCrossoverIndex = 2;
            BandRegionsCanvas.CaptureMouse();
        }
    }

    private void OnBandRegionsMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDraggingCrossover || BandRegionsCanvas.ActualWidth <= 0) return;

        var pos = e.GetPosition(BandRegionsCanvas);
        double width = BandRegionsCanvas.ActualWidth;
        double freq = XToFrequency(pos.X, width);

        // Clamp frequency and update crossover
        switch (_draggingCrossoverIndex)
        {
            case 0: // Low crossover
                freq = Math.Clamp(freq, MinFrequency + 10, _crossoverMid - 50);
                _crossoverLow = freq;
                if (Compressor != null)
                    Compressor.CrossoverLow = (float)freq;
                break;
            case 1: // Mid crossover
                freq = Math.Clamp(freq, _crossoverLow + 50, _crossoverHigh - 100);
                _crossoverMid = freq;
                if (Compressor != null)
                    Compressor.CrossoverMid = (float)freq;
                break;
            case 2: // High crossover
                freq = Math.Clamp(freq, _crossoverMid + 100, MaxFrequency - 100);
                _crossoverHigh = freq;
                if (Compressor != null)
                    Compressor.CrossoverHigh = (float)freq;
                break;
        }

        UpdateCrossoverDisplay();
        DrawBandRegions();
        DrawCrossoverHandles();
    }

    private void OnBandRegionsMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDraggingCrossover)
        {
            _isDraggingCrossover = false;
            _draggingCrossoverIndex = -1;
            BandRegionsCanvas.ReleaseMouseCapture();
        }
    }

    private void OnBandRegionsMouseLeave(object sender, MouseEventArgs e)
    {
        // Keep dragging if mouse is captured
        if (!BandRegionsCanvas.IsMouseCaptured)
        {
            _isDraggingCrossover = false;
            _draggingCrossoverIndex = -1;
        }
    }

    #endregion

    #region Private Methods - Drawing

    private void DrawFrequencyScale()
    {
        FrequencyScaleCanvas.Children.Clear();

        if (FrequencyScaleCanvas.ActualWidth <= 0) return;

        double width = FrequencyScaleCanvas.ActualWidth;
        var textBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));

        // Frequency markers (logarithmic)
        double[] freqMarkers = { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 };

        foreach (double freq in freqMarkers)
        {
            double x = FrequencyToX(freq, width);

            // Tick line
            var tick = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = 4,
                Stroke = textBrush,
                StrokeThickness = 1
            };
            FrequencyScaleCanvas.Children.Add(tick);

            // Label
            string label = freq >= 1000 ? $"{freq / 1000}k" : freq.ToString();
            var text = new TextBlock
            {
                Text = label,
                FontSize = 8,
                Foreground = textBrush
            };

            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, x - text.DesiredSize.Width / 2);
            Canvas.SetTop(text, 5);
            FrequencyScaleCanvas.Children.Add(text);
        }
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        if (DbScaleCanvas.ActualHeight <= 0) return;

        double height = DbScaleCanvas.ActualHeight;
        var textBrush = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
        var lineBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

        // dB markers for gain reduction (0 to -30 dB)
        double[] dbMarkers = { 0, -6, -12, -18, -24, -30 };

        foreach (double db in dbMarkers)
        {
            double y = (-db / MaxGainReduction) * height;

            // Grid line
            var line = new Shapes.Line
            {
                X1 = 30,
                Y1 = y,
                X2 = 35,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1
            };
            DbScaleCanvas.Children.Add(line);

            // Label
            var text = new TextBlock
            {
                Text = $"{db:F0}",
                FontSize = 8,
                Foreground = textBrush
            };

            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(text, 28 - text.DesiredSize.Width);
            Canvas.SetTop(text, y - text.DesiredSize.Height / 2);
            DbScaleCanvas.Children.Add(text);
        }
    }

    private void DrawBandRegions()
    {
        BandRegionsCanvas.Children.Clear();

        if (BandRegionsCanvas.ActualWidth <= 0 || BandRegionsCanvas.ActualHeight <= 0) return;

        double width = BandRegionsCanvas.ActualWidth;
        double height = BandRegionsCanvas.ActualHeight;

        // Calculate X positions for crossovers
        double lowX = FrequencyToX(_crossoverLow, width);
        double midX = FrequencyToX(_crossoverMid, width);
        double highX = FrequencyToX(_crossoverHigh, width);

        // Draw band regions with semi-transparent fills
        DrawBandRegion(0, lowX, height, LowBandColor, 0.15);
        DrawBandRegion(lowX, midX, height, LowMidBandColor, 0.15);
        DrawBandRegion(midX, highX, height, HighMidBandColor, 0.15);
        DrawBandRegion(highX, width, height, HighBandColor, 0.15);

        // Draw grid lines
        DrawGridLines(width, height);
    }

    private void DrawBandRegion(double x1, double x2, double height, Color color, double opacity)
    {
        var rect = new Shapes.Rectangle
        {
            Width = Math.Max(0, x2 - x1),
            Height = height,
            Fill = new SolidColorBrush(Color.FromArgb((byte)(opacity * 255), color.R, color.G, color.B))
        };

        Canvas.SetLeft(rect, x1);
        Canvas.SetTop(rect, 0);
        BandRegionsCanvas.Children.Add(rect);
    }

    private void DrawGridLines(double width, double height)
    {
        var lineBrush = new SolidColorBrush(Color.FromArgb(40, 0xFF, 0xFF, 0xFF));

        // Horizontal grid lines
        for (int i = 1; i < 6; i++)
        {
            double y = height * i / 6;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = lineBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            BandRegionsCanvas.Children.Add(line);
        }

        // Vertical frequency grid lines (octaves)
        double[] freqLines = { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };
        foreach (double freq in freqLines)
        {
            double x = FrequencyToX(freq, width);
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = lineBrush,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 2, 4 }
            };
            BandRegionsCanvas.Children.Add(line);
        }
    }

    private void DrawCrossoverHandles()
    {
        CrossoverHandlesCanvas.Children.Clear();

        if (CrossoverHandlesCanvas.ActualWidth <= 0 || CrossoverHandlesCanvas.ActualHeight <= 0) return;

        double width = CrossoverHandlesCanvas.ActualWidth;
        double height = CrossoverHandlesCanvas.ActualHeight;

        // Draw crossover lines and handles
        _crossoverLowHandle = DrawCrossoverHandle(_crossoverLow, width, height, LowBandColor, LowMidBandColor);
        _crossoverMidHandle = DrawCrossoverHandle(_crossoverMid, width, height, LowMidBandColor, HighMidBandColor);
        _crossoverHighHandle = DrawCrossoverHandle(_crossoverHigh, width, height, HighMidBandColor, HighBandColor);
    }

    private Shapes.Rectangle DrawCrossoverHandle(double freq, double width, double height, Color leftColor, Color rightColor)
    {
        double x = FrequencyToX(freq, width);

        // Gradient for crossover line
        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1)
        };
        gradientBrush.GradientStops.Add(new GradientStop(leftColor, 0));
        gradientBrush.GradientStops.Add(new GradientStop(rightColor, 1));

        // Crossover line
        var line = new Shapes.Line
        {
            X1 = x,
            Y1 = 0,
            X2 = x,
            Y2 = height,
            Stroke = gradientBrush,
            StrokeThickness = 2
        };
        CrossoverHandlesCanvas.Children.Add(line);

        // Draggable handle
        var handle = new Shapes.Rectangle
        {
            Width = 12,
            Height = 20,
            Fill = new SolidColorBrush(Color.FromRgb(0x18, 0x18, 0x18)),
            Stroke = gradientBrush,
            StrokeThickness = 2,
            RadiusX = 3,
            RadiusY = 3,
            Cursor = Cursors.SizeWE
        };

        Canvas.SetLeft(handle, x - 6);
        Canvas.SetTop(handle, height / 2 - 10);
        CrossoverHandlesCanvas.Children.Add(handle);

        // Frequency label above handle
        var label = new TextBlock
        {
            Text = FormatFrequency(freq),
            FontSize = 9,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0))
        };

        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(label, x - label.DesiredSize.Width / 2);
        Canvas.SetTop(label, 2);
        CrossoverHandlesCanvas.Children.Add(label);

        return handle;
    }

    #endregion

    #region Private Methods - Updates

    private void UpdateFromCompressor()
    {
        if (Compressor == null) return;

        // Update crossover frequencies
        _crossoverLow = Compressor.CrossoverLow;
        _crossoverMid = Compressor.CrossoverMid;
        _crossoverHigh = Compressor.CrossoverHigh;

        UpdateCrossoverDisplay();

        // Update Low band controls
        LowThresholdSlider.Value = Compressor.LowBand.Threshold;
        LowRatioSlider.Value = Compressor.LowBand.Ratio;
        LowAttackSlider.Value = Compressor.LowBand.Attack * 1000;
        LowReleaseSlider.Value = Compressor.LowBand.Release * 1000;
        LowGainSlider.Value = Compressor.LowBand.Gain;
        LowSoloButton.IsChecked = Compressor.LowBand.Solo;
        LowBypassButton.IsChecked = Compressor.LowBand.Bypass;

        // Update Low-Mid band controls
        LowMidThresholdSlider.Value = Compressor.LowMidBand.Threshold;
        LowMidRatioSlider.Value = Compressor.LowMidBand.Ratio;
        LowMidAttackSlider.Value = Compressor.LowMidBand.Attack * 1000;
        LowMidReleaseSlider.Value = Compressor.LowMidBand.Release * 1000;
        LowMidGainSlider.Value = Compressor.LowMidBand.Gain;
        LowMidSoloButton.IsChecked = Compressor.LowMidBand.Solo;
        LowMidBypassButton.IsChecked = Compressor.LowMidBand.Bypass;

        // Update High-Mid band controls
        HighMidThresholdSlider.Value = Compressor.HighMidBand.Threshold;
        HighMidRatioSlider.Value = Compressor.HighMidBand.Ratio;
        HighMidAttackSlider.Value = Compressor.HighMidBand.Attack * 1000;
        HighMidReleaseSlider.Value = Compressor.HighMidBand.Release * 1000;
        HighMidGainSlider.Value = Compressor.HighMidBand.Gain;
        HighMidSoloButton.IsChecked = Compressor.HighMidBand.Solo;
        HighMidBypassButton.IsChecked = Compressor.HighMidBand.Bypass;

        // Update High band controls
        HighThresholdSlider.Value = Compressor.HighBand.Threshold;
        HighRatioSlider.Value = Compressor.HighBand.Ratio;
        HighAttackSlider.Value = Compressor.HighBand.Attack * 1000;
        HighReleaseSlider.Value = Compressor.HighBand.Release * 1000;
        HighGainSlider.Value = Compressor.HighBand.Gain;
        HighSoloButton.IsChecked = Compressor.HighBand.Solo;
        HighBypassButton.IsChecked = Compressor.HighBand.Bypass;

        // Update text displays
        UpdateParameterDisplays();

        // Redraw band regions
        if (_isInitialized)
        {
            DrawBandRegions();
            DrawCrossoverHandles();
        }
    }

    private void UpdateCrossoverDisplay()
    {
        CrossoverLowText.Text = FormatFrequency(_crossoverLow);
        CrossoverMidText.Text = FormatFrequency(_crossoverMid);
        CrossoverHighText.Text = FormatFrequency(_crossoverHigh);
    }

    private void UpdateParameterDisplays()
    {
        // Low band
        LowThresholdText.Text = $"{LowThresholdSlider.Value:F0} dB";
        LowRatioText.Text = $"{LowRatioSlider.Value:F1}:1";
        LowAttackText.Text = $"{LowAttackSlider.Value:F1} ms";
        LowReleaseText.Text = $"{LowReleaseSlider.Value:F0} ms";
        LowGainText.Text = $"{LowGainSlider.Value:F1} dB";

        // Low-Mid band
        LowMidThresholdText.Text = $"{LowMidThresholdSlider.Value:F0} dB";
        LowMidRatioText.Text = $"{LowMidRatioSlider.Value:F1}:1";
        LowMidAttackText.Text = $"{LowMidAttackSlider.Value:F1} ms";
        LowMidReleaseText.Text = $"{LowMidReleaseSlider.Value:F0} ms";
        LowMidGainText.Text = $"{LowMidGainSlider.Value:F1} dB";

        // High-Mid band
        HighMidThresholdText.Text = $"{HighMidThresholdSlider.Value:F0} dB";
        HighMidRatioText.Text = $"{HighMidRatioSlider.Value:F1}:1";
        HighMidAttackText.Text = $"{HighMidAttackSlider.Value:F1} ms";
        HighMidReleaseText.Text = $"{HighMidReleaseSlider.Value:F0} ms";
        HighMidGainText.Text = $"{HighMidGainSlider.Value:F1} dB";

        // High band
        HighThresholdText.Text = $"{HighThresholdSlider.Value:F0} dB";
        HighRatioText.Text = $"{HighRatioSlider.Value:F1}:1";
        HighAttackText.Text = $"{HighAttackSlider.Value:F1} ms";
        HighReleaseText.Text = $"{HighReleaseSlider.Value:F0} ms";
        HighGainText.Text = $"{HighGainSlider.Value:F1} dB";
    }

    private void UpdateGainReductionMeters()
    {
        // Get gain reduction from compressor bands (simulated for now)
        // In real usage, you'd read the actual GR from the compressor's internal state

        // Smooth the display values
        for (int i = 0; i < 4; i++)
        {
            double target = _gainReductions[i];
            _displayedGainReductions[i] += (target - _displayedGainReductions[i]) * 0.3;
        }

        // Update meter heights (max height = 56 for 30dB GR)
        double maxHeight = 56;

        LowGRBar.Height = Math.Min(maxHeight, (_displayedGainReductions[0] / MaxGainReduction) * maxHeight);
        LowMidGRBar.Height = Math.Min(maxHeight, (_displayedGainReductions[1] / MaxGainReduction) * maxHeight);
        HighMidGRBar.Height = Math.Min(maxHeight, (_displayedGainReductions[2] / MaxGainReduction) * maxHeight);
        HighGRBar.Height = Math.Min(maxHeight, (_displayedGainReductions[3] / MaxGainReduction) * maxHeight);

        // Update text displays
        LowGRText.Text = _displayedGainReductions[0] < 0.1 ? "0.0 dB" : $"-{_displayedGainReductions[0]:F1} dB";
        LowMidGRText.Text = _displayedGainReductions[1] < 0.1 ? "0.0 dB" : $"-{_displayedGainReductions[1]:F1} dB";
        HighMidGRText.Text = _displayedGainReductions[2] < 0.1 ? "0.0 dB" : $"-{_displayedGainReductions[2]:F1} dB";
        HighGRText.Text = _displayedGainReductions[3] < 0.1 ? "0.0 dB" : $"-{_displayedGainReductions[3]:F1} dB";
    }

    #endregion

    #region Private Methods - Timer

    private void StartUpdateTimer()
    {
        _updateTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(33) // ~30 FPS
        };
        _updateTimer.Tick += OnUpdateTimerTick;
        _updateTimer.Start();
    }

    private void StopUpdateTimer()
    {
        if (_updateTimer != null)
        {
            _updateTimer.Stop();
            _updateTimer.Tick -= OnUpdateTimerTick;
            _updateTimer = null;
        }
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        UpdateGainReductionMeters();
    }

    #endregion

    #region Private Methods - Helpers

    private double FrequencyToX(double freq, double width)
    {
        // Logarithmic frequency scale
        double logMin = Math.Log10(MinFrequency);
        double logMax = Math.Log10(MaxFrequency);
        double logFreq = Math.Log10(Math.Clamp(freq, MinFrequency, MaxFrequency));

        return ((logFreq - logMin) / (logMax - logMin)) * width;
    }

    private double XToFrequency(double x, double width)
    {
        // Inverse logarithmic scale
        double logMin = Math.Log10(MinFrequency);
        double logMax = Math.Log10(MaxFrequency);
        double normalized = Math.Clamp(x / width, 0, 1);
        double logFreq = logMin + normalized * (logMax - logMin);

        return Math.Pow(10, logFreq);
    }

    private static string FormatFrequency(double freq)
    {
        if (freq >= 1000)
            return $"{freq / 1000:F1}k Hz";
        return $"{freq:F0} Hz";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the gain reduction values for each band (for external metering).
    /// </summary>
    /// <param name="low">Low band gain reduction in dB (positive value).</param>
    /// <param name="lowMid">Low-mid band gain reduction in dB.</param>
    /// <param name="highMid">High-mid band gain reduction in dB.</param>
    /// <param name="high">High band gain reduction in dB.</param>
    public void SetGainReductions(double low, double lowMid, double highMid, double high)
    {
        _gainReductions[0] = Math.Max(0, low);
        _gainReductions[1] = Math.Max(0, lowMid);
        _gainReductions[2] = Math.Max(0, highMid);
        _gainReductions[3] = Math.Max(0, high);
    }

    /// <summary>
    /// Resets all gain reduction meters to zero.
    /// </summary>
    public void ResetMeters()
    {
        for (int i = 0; i < 4; i++)
        {
            _gainReductions[i] = 0;
            _displayedGainReductions[i] = 0;
        }
        UpdateGainReductionMeters();
    }

    #endregion
}

#region Converters

/// <summary>
/// Converts boolean to Visibility for the multiband compressor control.
/// </summary>
public class MultibandCompressorBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
            return visibility == Visibility.Visible;
        return false;
    }
}

/// <summary>
/// Inverts a boolean value for the multiband compressor control.
/// </summary>
public class MultibandCompressorInverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        return false;
    }
}

#endregion
