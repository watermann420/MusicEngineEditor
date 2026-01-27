// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Information about a microphone track for phase alignment.
/// </summary>
public class PhaseMicInfo
{
    /// <summary>
    /// Mic/track identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Mic/track name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Audio samples.
    /// </summary>
    public float[]? Samples { get; set; }

    /// <summary>
    /// Sample rate.
    /// </summary>
    public int SampleRate { get; set; } = 44100;

    /// <summary>
    /// Number of channels.
    /// </summary>
    public int Channels { get; set; } = 1;
}

/// <summary>
/// Result of phase alignment analysis.
/// </summary>
public class PhaseAlignmentResult
{
    /// <summary>
    /// Track ID that was analyzed.
    /// </summary>
    public Guid TrackId { get; set; }

    /// <summary>
    /// Offset in samples for best alignment.
    /// </summary>
    public int OffsetSamples { get; set; }

    /// <summary>
    /// Offset in milliseconds.
    /// </summary>
    public double OffsetMs { get; set; }

    /// <summary>
    /// Phase correlation (-1 to +1).
    /// </summary>
    public double Correlation { get; set; }

    /// <summary>
    /// Whether polarity should be flipped.
    /// </summary>
    public bool FlipPolarity { get; set; }

    /// <summary>
    /// Whether alignment was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Phase status description.
    /// </summary>
    public string StatusDescription => Correlation switch
    {
        > 0.9 => "In Phase",
        > 0.5 => "Mostly In Phase",
        > 0 => "Partial Phase",
        > -0.5 => "Phase Issues",
        _ => "Out of Phase"
    };
}

/// <summary>
/// Event args for phase alignment result.
/// </summary>
public class PhaseAlignmentCompletedEventArgs : EventArgs
{
    public List<PhaseAlignmentResult> Results { get; set; } = [];
}

/// <summary>
/// Control for fixing phase coherence between multi-mic recordings.
/// </summary>
public partial class PhaseAlignmentControl : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty ReferenceMicProperty =
        DependencyProperty.Register(nameof(ReferenceMic), typeof(PhaseMicInfo), typeof(PhaseAlignmentControl),
            new PropertyMetadata(null, OnReferenceMicChanged));

    public static readonly DependencyProperty TargetMicsProperty =
        DependencyProperty.Register(nameof(TargetMics), typeof(List<PhaseMicInfo>), typeof(PhaseAlignmentControl),
            new PropertyMetadata(new List<PhaseMicInfo>(), OnTargetMicsChanged));

    /// <summary>
    /// Gets or sets the reference microphone.
    /// </summary>
    public PhaseMicInfo? ReferenceMic
    {
        get => (PhaseMicInfo?)GetValue(ReferenceMicProperty);
        set => SetValue(ReferenceMicProperty, value);
    }

    /// <summary>
    /// Gets or sets the target microphones to align.
    /// </summary>
    public List<PhaseMicInfo> TargetMics
    {
        get => (List<PhaseMicInfo>)GetValue(TargetMicsProperty);
        set => SetValue(TargetMicsProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when phase alignment analysis is complete.
    /// </summary>
    public event EventHandler<PhaseAlignmentCompletedEventArgs>? AlignmentCompleted;

    /// <summary>
    /// Raised when alignment should be applied.
    /// </summary>
    public event EventHandler<List<PhaseAlignmentResult>>? ApplyRequested;

    /// <summary>
    /// Raised when preview is requested.
    /// </summary>
    public event EventHandler<List<PhaseAlignmentResult>>? PreviewRequested;

    /// <summary>
    /// Raised when correlation value changes (for live monitoring).
    /// </summary>
    public event EventHandler<double>? CorrelationChanged;

    #endregion

    #region Private Fields

    private readonly List<PhaseAlignmentResult> _results = [];
    private CancellationTokenSource? _analysisCts;
    private DispatcherTimer? _liveMonitorTimer;
    private bool _isLiveMonitoring;
    private double _currentCorrelation;

    #endregion

    #region Constructor

    public PhaseAlignmentControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateMicSelectors();
        UpdateDisplay();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopLiveMonitor();
    }

    private static void OnReferenceMicChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseAlignmentControl control)
        {
            control.UpdateMicSelectors();
            control.UpdateDisplay();
        }
    }

    private static void OnTargetMicsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PhaseAlignmentControl control)
        {
            control.UpdateMicSelectors();
            control.UpdateDisplay();
        }
    }

    private void ReferenceMic_Changed(object sender, SelectionChangedEventArgs e)
    {
        // Handle reference mic change
        UpdateDisplay();
    }

    private void ManualOffset_Changed(object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(ManualOffsetBox.Text, out int offset))
        {
            // Update display with manual offset
            UpdateWaveformWithOffset(offset);
            UpdateCorrelationMeter();
        }
    }

    private void PolarityFlip_Changed(object sender, RoutedEventArgs e)
    {
        UpdateDisplay();
        UpdateCorrelationMeter();
    }

    private async void AutoAlign_Click(object sender, RoutedEventArgs e)
    {
        await AnalyzePhaseAsync();
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (_results.Count > 0)
        {
            PreviewRequested?.Invoke(this, _results.ToList());
        }
    }

    private void LiveMonitor_Click(object sender, RoutedEventArgs e)
    {
        if (LiveMonitorButton.IsChecked == true)
        {
            StartLiveMonitor();
        }
        else
        {
            StopLiveMonitor();
        }
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        BuildResultsFromUI();
        if (_results.Count > 0)
        {
            ApplyRequested?.Invoke(this, _results.ToList());
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _results.Clear();
        ManualOffsetBox.Text = "0";
        PolarityFlipCheck.IsChecked = false;
        UpdateResultsDisplay(null);
        UpdateDisplay();
    }

    #endregion

    #region Analysis Methods

    /// <summary>
    /// Performs phase alignment analysis.
    /// </summary>
    public async Task AnalyzePhaseAsync()
    {
        if (ReferenceMic?.Samples == null || TargetMics.Count == 0)
        {
            MessageBox.Show("Please select a reference mic and target mics.", "Missing Selection",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();

        AutoAlignButton.IsEnabled = false;
        AutoAlignButton.Content = "Analyzing...";

        try
        {
            int maxOffset = int.TryParse(MaxOffsetBox.Text, out int val) ? val : 1000;
            _results.Clear();

            foreach (var target in TargetMics)
            {
                if (target.Samples == null) continue;

                var result = await Task.Run(() =>
                    AnalyzePhase(ReferenceMic.Samples, target.Samples,
                        ReferenceMic.SampleRate, maxOffset, _analysisCts.Token),
                    _analysisCts.Token);

                result.TrackId = target.Id;
                result.OffsetMs = result.OffsetSamples * 1000.0 / ReferenceMic.SampleRate;
                _results.Add(result);
            }

            // Update UI with first result
            var firstResult = _results.FirstOrDefault(r => r.Success);
            if (firstResult != null)
            {
                ManualOffsetBox.Text = firstResult.OffsetSamples.ToString();
                PolarityFlipCheck.IsChecked = firstResult.FlipPolarity;
                UpdateResultsDisplay(firstResult);
            }

            UpdateDisplay();
            AlignmentCompleted?.Invoke(this, new PhaseAlignmentCompletedEventArgs { Results = _results.ToList() });
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            AutoAlignButton.IsEnabled = true;
            AutoAlignButton.Content = "Auto-Align";
        }
    }

    private static PhaseAlignmentResult AnalyzePhase(float[] reference, float[] target, int sampleRate, int maxOffset, CancellationToken ct)
    {
        var result = new PhaseAlignmentResult();

        try
        {
            // Convert to mono
            float[] refMono = ConvertToMono(reference);
            float[] targetMono = ConvertToMono(target);

            // Find best correlation with offset
            double bestCorrelation = -2;
            int bestOffset = 0;
            bool bestFlip = false;

            // Test both polarities
            for (int flip = 0; flip <= 1; flip++)
            {
                float[] testTarget = flip == 1 ? InvertPolarity(targetMono) : targetMono;

                for (int offset = -maxOffset; offset <= maxOffset; offset++)
                {
                    ct.ThrowIfCancellationRequested();

                    double correlation = CalculatePhaseCorrelation(refMono, testTarget, offset);

                    if (correlation > bestCorrelation)
                    {
                        bestCorrelation = correlation;
                        bestOffset = offset;
                        bestFlip = flip == 1;
                    }
                }
            }

            result.OffsetSamples = bestOffset;
            result.Correlation = bestCorrelation;
            result.FlipPolarity = bestFlip;
            result.Success = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            result.Success = false;
            System.Diagnostics.Debug.WriteLine($"Phase analysis error: {ex.Message}");
        }

        return result;
    }

    private static float[] ConvertToMono(float[] samples)
    {
        // Simple detection: if it seems like interleaved stereo
        if (samples.Length > 2 && samples.Length % 2 == 0)
        {
            var mono = new float[samples.Length / 2];
            for (int i = 0; i < mono.Length; i++)
            {
                mono[i] = (samples[i * 2] + samples[i * 2 + 1]) * 0.5f;
            }
            return mono;
        }
        return samples;
    }

    private static float[] InvertPolarity(float[] samples)
    {
        var inverted = new float[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            inverted[i] = -samples[i];
        }
        return inverted;
    }

    private static double CalculatePhaseCorrelation(float[] reference, float[] target, int offset)
    {
        int refStart = Math.Max(0, offset);
        int targetStart = Math.Max(0, -offset);
        int length = Math.Min(reference.Length - refStart, target.Length - targetStart);

        if (length <= 0) return -1;

        double sumRef = 0, sumTarget = 0, sumProduct = 0;
        double sumRefSq = 0, sumTargetSq = 0;

        for (int i = 0; i < length; i++)
        {
            float r = reference[refStart + i];
            float t = target[targetStart + i];

            sumRef += r;
            sumTarget += t;
            sumProduct += r * t;
            sumRefSq += r * r;
            sumTargetSq += t * t;
        }

        double meanRef = sumRef / length;
        double meanTarget = sumTarget / length;

        double numerator = sumProduct / length - meanRef * meanTarget;
        double denominator = Math.Sqrt((sumRefSq / length - meanRef * meanRef) *
                                       (sumTargetSq / length - meanTarget * meanTarget));

        return denominator > 0 ? numerator / denominator : 0;
    }

    #endregion

    #region Live Monitor

    private void StartLiveMonitor()
    {
        if (_liveMonitorTimer != null) return;

        _isLiveMonitoring = true;
        _liveMonitorTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _liveMonitorTimer.Tick += LiveMonitor_Tick;
        _liveMonitorTimer.Start();

        LiveMonitorButton.Content = "Stop Monitor";
    }

    private void StopLiveMonitor()
    {
        _isLiveMonitoring = false;
        _liveMonitorTimer?.Stop();
        _liveMonitorTimer = null;

        LiveMonitorButton.IsChecked = false;
        LiveMonitorButton.Content = "Live Monitor";
    }

    private void LiveMonitor_Tick(object? sender, EventArgs e)
    {
        if (!_isLiveMonitoring || ReferenceMic?.Samples == null || TargetMics.Count == 0)
            return;

        // Calculate current correlation in real-time
        var target = TargetMics.FirstOrDefault();
        if (target?.Samples == null) return;

        int offset = int.TryParse(ManualOffsetBox.Text, out int o) ? o : 0;
        bool flip = PolarityFlipCheck.IsChecked == true;

        float[] refMono = ConvertToMono(ReferenceMic.Samples);
        float[] targetMono = ConvertToMono(target.Samples);
        if (flip) targetMono = InvertPolarity(targetMono);

        _currentCorrelation = CalculatePhaseCorrelation(refMono, targetMono, offset);

        UpdateCorrelationMeter();
        CorrelationChanged?.Invoke(this, _currentCorrelation);
    }

    #endregion

    #region Display Methods

    private void UpdateMicSelectors()
    {
        ReferenceMicCombo.Items.Clear();
        ReferenceMicCombo.Items.Add(new ComboBoxItem { Content = "(Select reference mic)" });

        if (ReferenceMic != null)
        {
            ReferenceMicCombo.Items.Add(new ComboBoxItem
            {
                Content = ReferenceMic.Name,
                Tag = ReferenceMic.Id,
                IsSelected = true
            });
        }

        TargetMicsText.Text = $"{TargetMics.Count} mics selected";
    }

    private void UpdateResultsDisplay(PhaseAlignmentResult? result)
    {
        if (result == null)
        {
            OffsetResultText.Text = "-- samples";
            TimeResultText.Text = "-- ms";
            CorrelationResultText.Text = "--";
            StatusResultText.Text = "--";
            return;
        }

        string sign = result.OffsetSamples >= 0 ? "+" : "";
        OffsetResultText.Text = $"{sign}{result.OffsetSamples} samples";
        TimeResultText.Text = $"{sign}{result.OffsetMs:F3} ms";
        CorrelationResultText.Text = $"{result.Correlation:F3}";
        StatusResultText.Text = result.StatusDescription;

        // Color based on correlation
        StatusResultText.Foreground = result.Correlation switch
        {
            > 0.9 => (Brush)FindResource("InPhaseBrush"),
            > 0.5 => (Brush)FindResource("AccentBrush"),
            > 0 => (Brush)FindResource("TargetBrush"),
            _ => (Brush)FindResource("OutOfPhaseBrush")
        };
    }

    private void UpdateDisplay()
    {
        DrawReferenceWaveform();
        DrawTargetWaveform();
        UpdateCorrelationMeter();
    }

    private void DrawReferenceWaveform()
    {
        ReferenceWaveformCanvas.Children.Clear();

        if (ReferenceMic?.Samples == null) return;

        double width = ReferenceWaveformCanvas.ActualWidth;
        double height = ReferenceWaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        DrawWaveform(ReferenceWaveformCanvas, ReferenceMic.Samples, (Color)FindResource("ReferenceColor"));
    }

    private void DrawTargetWaveform()
    {
        TargetWaveformCanvas.Children.Clear();

        if (TargetMics.Count == 0) return;

        var target = TargetMics.FirstOrDefault();
        if (target?.Samples == null) return;

        int offset = int.TryParse(ManualOffsetBox.Text, out int o) ? o : 0;
        bool flip = PolarityFlipCheck.IsChecked == true;

        float[] samples = target.Samples;
        if (flip)
        {
            samples = InvertPolarity(samples);
        }

        DrawWaveform(TargetWaveformCanvas, samples, (Color)FindResource("TargetColor"), offset);

        string label = flip ? "Target (inverted)" : "Target";
        if (offset != 0)
        {
            label += $" ({(offset >= 0 ? "+" : "")}{offset} samples)";
        }
        TargetWaveformLabel.Text = label;
    }

    private void UpdateWaveformWithOffset(int offset)
    {
        DrawTargetWaveform();
    }

    private static void DrawWaveform(Canvas canvas, float[] samples, Color color, int offsetSamples = 0)
    {
        double width = canvas.ActualWidth;
        double height = canvas.ActualHeight;

        if (width <= 0 || height <= 0 || samples.Length == 0) return;

        int samplesPerPixel = Math.Max(1, samples.Length / (int)width);
        var points = new PointCollection();

        for (int x = 0; x < (int)width; x++)
        {
            int sampleIndex = x * samplesPerPixel + offsetSamples;
            if (sampleIndex < 0 || sampleIndex >= samples.Length) continue;

            float val = 0;
            int count = 0;
            for (int i = 0; i < samplesPerPixel && (sampleIndex + i) < samples.Length && (sampleIndex + i) >= 0; i++)
            {
                val += samples[sampleIndex + i];
                count++;
            }
            if (count > 0) val /= count;

            double y = height / 2 - (val * height / 2 * 0.8);
            points.Add(new Point(x, y));
        }

        var polyline = new Shapes.Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color) { Opacity = 0.8 },
            StrokeThickness = 1.5
        };
        canvas.Children.Add(polyline);
    }

    private void UpdateCorrelationMeter()
    {
        CorrelationMeterCanvas.Children.Clear();

        double correlation = _currentCorrelation;
        if (_results.Count > 0)
        {
            var result = _results.FirstOrDefault(r => r.Success);
            if (result != null)
            {
                correlation = result.Correlation;
            }
        }

        double width = CorrelationMeterCanvas.ActualWidth;
        double height = CorrelationMeterCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Position indicator at correlation value
        double indicatorX = (correlation + 1) / 2 * width;

        var indicator = new Shapes.Ellipse
        {
            Width = 8,
            Height = height - 4,
            Fill = Brushes.White,
            Stroke = Brushes.Black,
            StrokeThickness = 1
        };
        Canvas.SetLeft(indicator, indicatorX - 4);
        Canvas.SetTop(indicator, 2);
        CorrelationMeterCanvas.Children.Add(indicator);

        CorrelationValueText.Text = $"{correlation:F2}";
    }

    private void BuildResultsFromUI()
    {
        if (TargetMics.Count == 0) return;

        int offset = int.TryParse(ManualOffsetBox.Text, out int o) ? o : 0;
        bool flip = PolarityFlipCheck.IsChecked == true;

        _results.Clear();

        foreach (var target in TargetMics)
        {
            _results.Add(new PhaseAlignmentResult
            {
                TrackId = target.Id,
                OffsetSamples = offset,
                OffsetMs = offset * 1000.0 / (ReferenceMic?.SampleRate ?? 44100),
                FlipPolarity = flip,
                Correlation = _currentCorrelation,
                Success = true
            });
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the microphones for phase alignment.
    /// </summary>
    /// <param name="reference">Reference microphone.</param>
    /// <param name="targets">Target microphones to align.</param>
    public void SetMics(PhaseMicInfo reference, List<PhaseMicInfo> targets)
    {
        ReferenceMic = reference;
        TargetMics = targets;
    }

    /// <summary>
    /// Gets the alignment results.
    /// </summary>
    public List<PhaseAlignmentResult> GetResults() => _results.ToList();

    /// <summary>
    /// Sets the correlation value for display (for live input).
    /// </summary>
    public void SetCorrelation(double correlation)
    {
        _currentCorrelation = correlation;
        UpdateCorrelationMeter();
    }

    #endregion
}
