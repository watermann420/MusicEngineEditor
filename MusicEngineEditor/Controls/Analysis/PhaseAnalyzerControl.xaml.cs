// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Phase Analyzer control for detailed stereo phase analysis.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Phase Analyzer control providing detailed stereo phase analysis visualization.
/// Features phase correlation vs frequency graph, mono compatibility scoring,
/// problem area highlighting, mid/side balance meter, and correlation history.
/// </summary>
public partial class PhaseAnalyzerControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty AnalysisResultProperty =
        DependencyProperty.Register(nameof(AnalysisResult), typeof(PhaseAnalysisResult), typeof(PhaseAnalyzerControl),
            new PropertyMetadata(null, OnAnalysisResultChanged));

    public static readonly DependencyProperty IsLiveModeProperty =
        DependencyProperty.Register(nameof(IsLiveMode), typeof(bool), typeof(PhaseAnalyzerControl),
            new PropertyMetadata(true));

    public static readonly DependencyProperty HistoryLengthProperty =
        DependencyProperty.Register(nameof(HistoryLength), typeof(int), typeof(PhaseAnalyzerControl),
            new PropertyMetadata(100));

    public static readonly DependencyProperty PhaseIssueThresholdProperty =
        DependencyProperty.Register(nameof(PhaseIssueThreshold), typeof(float), typeof(PhaseAnalyzerControl),
            new PropertyMetadata(0.0f));

    /// <summary>
    /// Gets or sets the current phase analysis result from the engine.
    /// </summary>
    public PhaseAnalysisResult? AnalysisResult
    {
        get => (PhaseAnalysisResult?)GetValue(AnalysisResultProperty);
        set => SetValue(AnalysisResultProperty, value);
    }

    /// <summary>
    /// Gets or sets whether live analysis mode is enabled.
    /// </summary>
    public bool IsLiveMode
    {
        get => (bool)GetValue(IsLiveModeProperty);
        set => SetValue(IsLiveModeProperty, value);
    }

    /// <summary>
    /// Gets or sets the number of history samples to keep.
    /// </summary>
    public int HistoryLength
    {
        get => (int)GetValue(HistoryLengthProperty);
        set => SetValue(HistoryLengthProperty, value);
    }

    /// <summary>
    /// Gets or sets the correlation threshold below which a band is flagged as having issues.
    /// </summary>
    public float PhaseIssueThreshold
    {
        get => (float)GetValue(PhaseIssueThresholdProperty);
        set => SetValue(PhaseIssueThresholdProperty, value);
    }

    #endregion

    #region Private Fields

    private bool _isInitialized;
    private readonly List<float> _correlationHistory = new();
    private readonly ObservableCollection<ProblemAreaViewModel> _problemAreas = new();

    // Graph elements
    private Shapes.Polyline? _phaseGraphLine;
    private readonly List<Shapes.Ellipse> _bandDots = new();
    private Shapes.Polyline? _historyLine;

    // Statistics
    private float _minCorrelation = 1.0f;
    private float _maxCorrelation = -1.0f;
    private float _avgCorrelation;

    // Colors
    private static readonly Color AccentColor = Color.FromRgb(0x00, 0xD9, 0xFF);
    private static readonly Color SuccessColor = Color.FromRgb(0x00, 0xFF, 0x88);
    private static readonly Color WarningColor = Color.FromRgb(0xFF, 0xB8, 0x00);
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x47, 0x57);

    #endregion

    #region Constructor

    public PhaseAnalyzerControl()
    {
        InitializeComponent();

        ProblemAreasItemsControl.ItemsSource = _problemAreas;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = true;
        InitializeGraphElements();
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
    }

    private static void OnAnalysisResultChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseAnalyzerControl control && control._isInitialized && control.IsLiveMode)
        {
            control.UpdateDisplay();
        }
    }

    private void LiveModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        IsLiveMode = LiveModeToggle.IsChecked ?? true;
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        Reset();
    }

    private void PhaseGraphCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            DrawPhaseGraph();
        }
    }

    private void HistoryCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateHistoryZeroLine();
            DrawHistoryGraph();
        }
    }

    #endregion

    #region Initialization

    private void InitializeGraphElements()
    {
        // Initialize phase graph line
        _phaseGraphLine = new Shapes.Polyline
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        };
        PhaseGraphCanvas.Children.Add(_phaseGraphLine);

        // Initialize history line
        _historyLine = new Shapes.Polyline
        {
            Stroke = new SolidColorBrush(AccentColor),
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        HistoryCanvas.Children.Add(_historyLine);

        UpdateHistoryZeroLine();
    }

    private void UpdateHistoryZeroLine()
    {
        double width = HistoryCanvas.ActualWidth;
        double height = HistoryCanvas.ActualHeight;

        if (width > 0 && height > 0)
        {
            HistoryZeroLine.X1 = 0;
            HistoryZeroLine.Y1 = height / 2;
            HistoryZeroLine.X2 = width;
            HistoryZeroLine.Y2 = height / 2;
        }
    }

    #endregion

    #region Update Methods

    private void UpdateDisplay()
    {
        var result = AnalysisResult;
        if (result == null)
        {
            UpdateEmptyState();
            return;
        }

        UpdateMonoCompatibility(result);
        UpdatePhaseGraph(result);
        UpdateProblemAreas(result);
        UpdateMidSideBalance(result);
        UpdateCorrelationHistory(result);
    }

    private void UpdateEmptyState()
    {
        MonoCompatibilityText.Text = "--";
        MonoStatusText.Text = "No Data";
        MonoCompatibilityFill.Width = 0;

        NoProblemText.Visibility = Visibility.Visible;
        _problemAreas.Clear();

        OverallCorrelationText.Text = "Avg: --";
    }

    private void UpdateMonoCompatibility(PhaseAnalysisResult result)
    {
        float score = result.MonoCompatibilityScore;

        // Update percentage text
        MonoCompatibilityText.Text = $"{score:F0}%";

        // Update fill bar
        double maxWidth = ((Grid)MonoCompatibilityFill.Parent).ActualWidth;
        if (maxWidth > 0)
        {
            MonoCompatibilityFill.Width = maxWidth * (score / 100.0);
        }

        // Update color and status based on score
        Color color;
        string status;

        if (score >= 80)
        {
            color = SuccessColor;
            status = "Excellent";
        }
        else if (score >= 60)
        {
            color = Color.FromRgb(0x7F, 0xDB, 0xC4); // Blend of success and warning
            status = "Good";
        }
        else if (score >= 40)
        {
            color = WarningColor;
            status = "Moderate";
        }
        else if (score >= 20)
        {
            color = Color.FromRgb(0xFF, 0x7F, 0x50); // Blend of warning and error
            status = "Poor";
        }
        else
        {
            color = ErrorColor;
            status = "Critical";
        }

        MonoCompatibilityText.Foreground = new SolidColorBrush(color);
        MonoCompatibilityFill.Background = new SolidColorBrush(color);
        MonoStatusText.Text = status;

        // Add warnings
        if (result.LikelyPolarityInverted)
        {
            MonoStatusText.Text = "POLARITY INVERTED";
            MonoStatusText.Foreground = new SolidColorBrush(ErrorColor);
        }
        else if (result.SignificantMonoCancellation)
        {
            MonoStatusText.Text = "Phase Issues";
            MonoStatusText.Foreground = new SolidColorBrush(WarningColor);
        }
        else
        {
            MonoStatusText.Foreground = FindResource("DimTextBrush") as Brush ?? Brushes.Gray;
        }
    }

    private void UpdatePhaseGraph(PhaseAnalysisResult result)
    {
        DrawPhaseGraph();
    }

    private void DrawPhaseGraph()
    {
        if (_phaseGraphLine == null) return;

        var result = AnalysisResult;
        double width = PhaseGraphCanvas.ActualWidth;
        double height = PhaseGraphCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Clear existing dots
        foreach (var dot in _bandDots)
        {
            PhaseGraphCanvas.Children.Remove(dot);
        }
        _bandDots.Clear();

        // Draw grid lines
        DrawPhaseGraphGrid(width, height);

        if (result?.BandCorrelations == null || result.BandCorrelations.Length == 0)
        {
            _phaseGraphLine.Points.Clear();
            return;
        }

        var points = new PointCollection();
        var bands = result.BandCorrelations;

        // Logarithmic frequency mapping (20Hz to 20kHz)
        const float minFreq = 20f;
        const float maxFreq = 20000f;
        double logMin = Math.Log10(minFreq);
        double logMax = Math.Log10(maxFreq);
        double logRange = logMax - logMin;

        for (int i = 0; i < bands.Length; i++)
        {
            var band = bands[i];

            // X position: logarithmic frequency scale
            double logFreq = Math.Log10(band.CenterFrequency);
            double x = ((logFreq - logMin) / logRange) * width;

            // Y position: correlation (-1 to +1) mapped to canvas
            double y = height - ((band.Correlation + 1) / 2) * height;

            points.Add(new Point(x, y));

            // Add dot marker
            var dot = new Shapes.Ellipse
            {
                Width = 8,
                Height = 8,
                Fill = new SolidColorBrush(band.HasPhaseIssue ? ErrorColor : SuccessColor),
                Stroke = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0)),
                StrokeThickness = 1
            };

            Canvas.SetLeft(dot, x - 4);
            Canvas.SetTop(dot, y - 4);
            PhaseGraphCanvas.Children.Add(dot);
            _bandDots.Add(dot);

            // Tooltip
            dot.ToolTip = $"{band.LowFrequency:F0}-{band.HighFrequency:F0} Hz\n" +
                          $"Correlation: {band.Correlation:F2}\n" +
                          $"Phase Diff: {band.AveragePhaseDifferenceDegrees:F1} deg\n" +
                          $"Energy: {band.EnergyDb:F1} dB";
        }

        _phaseGraphLine.Points = points;
    }

    private void DrawPhaseGraphGrid(double width, double height)
    {
        // Remove old grid lines (keep the polyline and dots)
        var elementsToRemove = new List<UIElement>();
        foreach (UIElement child in PhaseGraphCanvas.Children)
        {
            if (child is Shapes.Line)
            {
                elementsToRemove.Add(child);
            }
        }
        foreach (var element in elementsToRemove)
        {
            PhaseGraphCanvas.Children.Remove(element);
        }

        var gridBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));
        var zeroBrush = new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A));

        // Horizontal lines at correlation levels
        double[] correlationLevels = { -1, -0.5, 0, 0.5, 1 };
        foreach (double level in correlationLevels)
        {
            double y = height - ((level + 1) / 2) * height;
            var line = new Shapes.Line
            {
                X1 = 0,
                Y1 = y,
                X2 = width,
                Y2 = y,
                Stroke = level == 0 ? zeroBrush : gridBrush,
                StrokeThickness = level == 0 ? 1 : 0.5,
                StrokeDashArray = level == 0 ? null : new DoubleCollection { 4, 4 }
            };
            PhaseGraphCanvas.Children.Insert(0, line);
        }

        // Vertical lines at frequency markers
        double[] frequencies = { 100, 1000, 10000 };
        const float minFreq = 20f;
        const float maxFreq = 20000f;
        double logMin = Math.Log10(minFreq);
        double logMax = Math.Log10(maxFreq);
        double logRange = logMax - logMin;

        foreach (double freq in frequencies)
        {
            double logFreq = Math.Log10(freq);
            double x = ((logFreq - logMin) / logRange) * width;
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = height,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            PhaseGraphCanvas.Children.Insert(0, line);
        }
    }

    private void UpdateProblemAreas(PhaseAnalysisResult result)
    {
        _problemAreas.Clear();

        if (result.ProblemFrequencies == null || result.ProblemFrequencies.Length == 0)
        {
            NoProblemText.Visibility = Visibility.Visible;
            return;
        }

        NoProblemText.Visibility = Visibility.Collapsed;

        foreach (var problem in result.ProblemFrequencies)
        {
            _problemAreas.Add(new ProblemAreaViewModel
            {
                LowFrequency = problem.LowHz,
                HighFrequency = problem.HighHz,
                Severity = problem.Severity
            });
        }
    }

    private void UpdateMidSideBalance(PhaseAnalysisResult result)
    {
        float stereoWidth = result.StereoWidth;

        // Update stereo width text
        StereoWidthText.Text = $"Width: {stereoWidth * 100:F0}%";

        // Update indicator position
        // 0 = mono (center-left), 1 = wide stereo (center-right)
        double meterWidth = ((Grid)MidSideIndicator.Parent).ActualWidth;
        if (meterWidth > 0)
        {
            double position = stereoWidth * meterWidth;
            MidSideIndicator.Margin = new Thickness(position - meterWidth / 2, 2, 0, 2);
        }

        // Estimate M/S levels from stereo width
        // Higher correlation = more Mid, lower correlation = more Side
        float correlation = result.OverallCorrelation;
        float midLevel = (1 + correlation) / 2; // 0 to 1
        float sideLevel = (1 - correlation) / 2; // 0 to 1

        // Convert to dB (approximate)
        float midDb = midLevel > 0.001f ? 20 * (float)Math.Log10(midLevel) : -60;
        float sideDb = sideLevel > 0.001f ? 20 * (float)Math.Log10(sideLevel) : -60;

        MidLevelText.Text = midDb > -60 ? $"{midDb:F1} dB" : "-inf dB";
        SideLevelText.Text = sideDb > -60 ? $"{sideDb:F1} dB" : "-inf dB";
    }

    private void UpdateCorrelationHistory(PhaseAnalysisResult result)
    {
        float correlation = result.OverallCorrelation;

        // Add to history
        _correlationHistory.Add(correlation);

        // Trim to max length
        while (_correlationHistory.Count > HistoryLength)
        {
            _correlationHistory.RemoveAt(0);
        }

        // Update statistics
        _minCorrelation = Math.Min(_minCorrelation, correlation);
        _maxCorrelation = Math.Max(_maxCorrelation, correlation);

        float sum = 0;
        foreach (var c in _correlationHistory)
        {
            sum += c;
        }
        _avgCorrelation = _correlationHistory.Count > 0 ? sum / _correlationHistory.Count : 0;

        // Update UI
        OverallCorrelationText.Text = $"Avg: {_avgCorrelation:+0.00;-0.00}";
        OverallCorrelationText.Foreground = new SolidColorBrush(GetCorrelationColor(_avgCorrelation));

        MinCorrelationText.Text = $"{_minCorrelation:+0.00;-0.00}";
        AvgCorrelationText.Text = $"{_avgCorrelation:+0.00;-0.00}";
        MaxCorrelationText.Text = $"{_maxCorrelation:+0.00;-0.00}";

        DrawHistoryGraph();
    }

    private void DrawHistoryGraph()
    {
        if (_historyLine == null) return;

        double width = HistoryCanvas.ActualWidth;
        double height = HistoryCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || _correlationHistory.Count == 0)
        {
            _historyLine.Points.Clear();
            return;
        }

        var points = new PointCollection();
        int count = _correlationHistory.Count;

        for (int i = 0; i < count; i++)
        {
            double x = (double)i / Math.Max(1, count - 1) * width;
            double y = height - ((_correlationHistory[i] + 1) / 2) * height;
            points.Add(new Point(x, y));
        }

        _historyLine.Points = points;

        // Color based on average correlation
        _historyLine.Stroke = new SolidColorBrush(GetCorrelationColor(_avgCorrelation));
    }

    #endregion

    #region Helper Methods

    private static Color GetCorrelationColor(float correlation)
    {
        if (correlation < -0.3)
        {
            return ErrorColor;
        }
        else if (correlation < 0.3)
        {
            return WarningColor;
        }
        else if (correlation < 0.7)
        {
            return SuccessColor;
        }
        else
        {
            return AccentColor;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Resets all analysis data and history.
    /// </summary>
    public void Reset()
    {
        _correlationHistory.Clear();
        _problemAreas.Clear();
        _minCorrelation = 1.0f;
        _maxCorrelation = -1.0f;
        _avgCorrelation = 0;

        if (_phaseGraphLine != null)
        {
            _phaseGraphLine.Points.Clear();
        }

        foreach (var dot in _bandDots)
        {
            PhaseGraphCanvas.Children.Remove(dot);
        }
        _bandDots.Clear();

        if (_historyLine != null)
        {
            _historyLine.Points.Clear();
        }

        UpdateEmptyState();
    }

    /// <summary>
    /// Updates the control with new analysis result data.
    /// </summary>
    /// <param name="result">The phase analysis result from the engine.</param>
    public void Update(PhaseAnalysisResult result)
    {
        AnalysisResult = result;
    }

    /// <summary>
    /// Updates the control with raw stereo audio samples for analysis.
    /// Creates a PhaseAnalyzer instance internally to process the samples.
    /// </summary>
    /// <param name="leftSamples">Left channel samples.</param>
    /// <param name="rightSamples">Right channel samples.</param>
    /// <param name="sampleRate">Audio sample rate (default: 44100).</param>
    public void AnalyzeSamples(float[] leftSamples, float[] rightSamples, int sampleRate = 44100)
    {
        if (leftSamples == null || rightSamples == null || leftSamples.Length == 0)
            return;

        try
        {
            var analyzer = new PhaseAnalyzer(sampleRate);
            analyzer.PhaseIssueThreshold = PhaseIssueThreshold;
            var result = analyzer.AnalyzeBuffer(leftSamples, rightSamples);
            AnalysisResult = result;
        }
        catch (Exception)
        {
            // Silently ignore analysis errors
        }
    }

    #endregion
}

#region View Models

/// <summary>
/// View model for displaying problem frequency areas.
/// </summary>
public class ProblemAreaViewModel : INotifyPropertyChanged
{
    private float _lowFrequency;
    private float _highFrequency;
    private float _severity;

    public float LowFrequency
    {
        get => _lowFrequency;
        set { _lowFrequency = value; OnPropertyChanged(nameof(LowFrequency)); OnPropertyChanged(nameof(FrequencyRange)); }
    }

    public float HighFrequency
    {
        get => _highFrequency;
        set { _highFrequency = value; OnPropertyChanged(nameof(HighFrequency)); OnPropertyChanged(nameof(FrequencyRange)); }
    }

    public float Severity
    {
        get => _severity;
        set { _severity = value; OnPropertyChanged(nameof(Severity)); OnPropertyChanged(nameof(SeverityText)); OnPropertyChanged(nameof(SeverityBrush)); }
    }

    public string FrequencyRange => $"{FormatFrequency(_lowFrequency)} - {FormatFrequency(_highFrequency)}";

    public string SeverityText
    {
        get
        {
            if (_severity >= 0.7f) return "Severe";
            if (_severity >= 0.4f) return "Moderate";
            return "Mild";
        }
    }

    public Brush SeverityBrush
    {
        get
        {
            if (_severity >= 0.7f) return new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57));
            if (_severity >= 0.4f) return new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00));
            return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
        }
    }

    private static string FormatFrequency(float hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:F1}k Hz";
        return $"{hz:F0} Hz";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion

#region Converters

/// <summary>
/// Converts a correlation value to a color for visualization.
/// </summary>
public class PhaseAnalyzerCorrelationToColorConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is float correlation)
        {
            return GetBrush(correlation);
        }
        if (value is double correlationDouble)
        {
            return GetBrush((float)correlationDouble);
        }
        return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    }

    private static SolidColorBrush GetBrush(float correlation)
    {
        if (correlation < -0.3f)
            return new SolidColorBrush(Color.FromRgb(0xFF, 0x47, 0x57)); // Error
        if (correlation < 0.3f)
            return new SolidColorBrush(Color.FromRgb(0xFF, 0xB8, 0x00)); // Warning
        if (correlation < 0.7f)
            return new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)); // Success
        return new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)); // Accent
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a mono compatibility score to a status string.
/// </summary>
public class PhaseAnalyzerScoreToStatusConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is float score)
        {
            if (score >= 80) return "Excellent";
            if (score >= 60) return "Good";
            if (score >= 40) return "Moderate";
            if (score >= 20) return "Poor";
            return "Critical";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts a severity value (0-1) to a descriptive string.
/// </summary>
public class PhaseAnalyzerSeverityToTextConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is float severity)
        {
            if (severity >= 0.7f) return "Severe";
            if (severity >= 0.4f) return "Moderate";
            return "Mild";
        }
        return "Unknown";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts stereo width (0-1) to a percentage string.
/// </summary>
public class PhaseAnalyzerWidthToPercentConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is float width)
        {
            return $"{width * 100:F0}%";
        }
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
