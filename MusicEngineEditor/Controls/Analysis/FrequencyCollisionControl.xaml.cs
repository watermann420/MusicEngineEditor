// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Frequency Collision Detector control with multi-track spectrum overlay and EQ suggestions.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls.Analysis;

/// <summary>
/// Frequency Collision Detector control with multi-track spectrum overlay,
/// collision zone highlighting, and suggested EQ cuts.
/// </summary>
public partial class FrequencyCollisionControl : UserControl
{
    #region Constants

    private const double MinDb = -80.0;
    private const double MaxDb = 0.0;
    private const double MinFrequency = 20.0;
    private const double MaxFrequency = 20000.0;
    private const int DefaultBandCount = 31;

    private static readonly Color[] TrackColors =
    {
        Color.FromRgb(0, 217, 255),    // Cyan
        Color.FromRgb(255, 107, 157),  // Pink
        Color.FromRgb(127, 255, 0),    // Green
        Color.FromRgb(255, 217, 61),   // Yellow
        Color.FromRgb(199, 125, 255),  // Purple
        Color.FromRgb(255, 140, 66),   // Orange
        Color.FromRgb(100, 255, 218),  // Teal
        Color.FromRgb(255, 99, 99),    // Red
    };

    #endregion

    #region Private Fields

    private readonly List<TrackAnalysisData> _tracks = new();
    private readonly List<System.Windows.Controls.CheckBox> _trackCheckboxes = new();
    private readonly ObservableCollection<EqSuggestionItem> _eqSuggestions = new();
    private FrequencyCollisionDetector? _detector;
    private CollisionAnalysisResult? _lastResult;

    private Shapes.Path[]? _spectrumPaths;
    private Shapes.Rectangle[]? _collisionBars;
    private bool _isInitialized;
    private DispatcherTimer? _updateTimer;

    private int _bandCount = DefaultBandCount;
    private float[] _bandFrequencies = Array.Empty<float>();

    #endregion

    #region Events

    /// <summary>
    /// Raised when an EQ suggestion is requested to be applied.
    /// </summary>
    public event EventHandler<EqSuggestionAppliedEventArgs>? EqSuggestionApplied;

    /// <summary>
    /// Raised when analysis parameters change.
    /// </summary>
    public event EventHandler<FrequencyCollisionParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Raised when collision analysis is updated.
    /// </summary>
    public event EventHandler<CollisionAnalysisResult>? CollisionAnalysisUpdated;

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty SampleRateProperty =
        DependencyProperty.Register(nameof(SampleRate), typeof(int), typeof(FrequencyCollisionControl),
            new PropertyMetadata(44100, OnSampleRateChanged));

    public static readonly DependencyProperty IsRealTimeProperty =
        DependencyProperty.Register(nameof(IsRealTime), typeof(bool), typeof(FrequencyCollisionControl),
            new PropertyMetadata(false, OnIsRealTimeChanged));

    /// <summary>
    /// Gets or sets the sample rate for analysis.
    /// </summary>
    public int SampleRate
    {
        get => (int)GetValue(SampleRateProperty);
        set => SetValue(SampleRateProperty, value);
    }

    /// <summary>
    /// Gets or sets whether real-time analysis is enabled.
    /// </summary>
    public bool IsRealTime
    {
        get => (bool)GetValue(IsRealTimeProperty);
        set => SetValue(IsRealTimeProperty, value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the collision threshold (0.0 to 1.0).
    /// </summary>
    public float CollisionThreshold
    {
        get => (float)(ThresholdSlider.Value / 100.0);
        set => ThresholdSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the masking threshold in dB.
    /// </summary>
    public float MaskingThresholdDb
    {
        get => (float)MaskingThresholdSlider.Value;
        set => MaskingThresholdSlider.Value = value;
    }

    /// <summary>
    /// Gets or sets the smoothing factor.
    /// </summary>
    public float SmoothingFactor
    {
        get => (float)(SmoothingSlider.Value / 100.0);
        set => SmoothingSlider.Value = value * 100.0;
    }

    /// <summary>
    /// Gets or sets the maximum suggested EQ cut in dB.
    /// </summary>
    public float MaxSuggestedCutDb
    {
        get => (float)MaxCutSlider.Value;
        set => MaxCutSlider.Value = value;
    }

    /// <summary>
    /// Gets the current EQ suggestions.
    /// </summary>
    public IReadOnlyList<EqSuggestionItem> EqSuggestions => _eqSuggestions;

    /// <summary>
    /// Gets the last analysis result.
    /// </summary>
    public CollisionAnalysisResult? LastResult => _lastResult;

    #endregion

    #region Constructor

    public FrequencyCollisionControl()
    {
        InitializeComponent();

        EqSuggestionsListBox.ItemsSource = _eqSuggestions;

        InitializeBandFrequencies();
        InitializeDetector();

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

        if (IsRealTime)
        {
            StartRealTimeAnalysis();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _isInitialized = false;
        StopRealTimeAnalysis();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_isInitialized)
        {
            UpdateLayout();
        }
    }

    #endregion

    #region Initialization

    private void InitializeBandFrequencies()
    {
        _bandFrequencies = new float[_bandCount];
        float logMin = MathF.Log10((float)MinFrequency);
        float logMax = MathF.Log10((float)MaxFrequency);
        float logStep = (logMax - logMin) / (_bandCount - 1);

        for (int i = 0; i < _bandCount; i++)
        {
            _bandFrequencies[i] = MathF.Pow(10, logMin + i * logStep);
        }
    }

    private void InitializeDetector()
    {
        _detector = new FrequencyCollisionDetector(
            SampleRate,
            _bandCount,
            4096,
            (float)MinFrequency,
            (float)MaxFrequency)
        {
            CollisionThreshold = CollisionThreshold,
            MaskingThresholdDb = MaskingThresholdDb,
            SmoothingFactor = SmoothingFactor,
            MaxSuggestedCutDb = MaxSuggestedCutDb
        };

        _detector.CollisionUpdated += OnDetectorCollisionUpdated;
    }

    #endregion

    #region Visual Tree Building

    private void BuildVisualTree()
    {
        SpectrumCanvas.Children.Clear();
        DbScaleCanvas.Children.Clear();
        FrequencyLabelCanvas.Children.Clear();
        LegendPanel.Children.Clear();

        // Create collision highlight bars
        _collisionBars = new Shapes.Rectangle[_bandCount];
        for (int i = 0; i < _bandCount; i++)
        {
            var bar = new Shapes.Rectangle
            {
                Fill = new SolidColorBrush(Color.FromArgb(0, 255, 71, 87)),
                RadiusX = 1,
                RadiusY = 1
            };
            _collisionBars[i] = bar;
            SpectrumCanvas.Children.Add(bar);
        }

        // Create spectrum paths for each track
        _spectrumPaths = new Shapes.Path[_tracks.Count];
        for (int i = 0; i < _tracks.Count; i++)
        {
            var path = new Shapes.Path
            {
                Stroke = new SolidColorBrush(GetTrackColor(i)),
                StrokeThickness = 2,
                Fill = new SolidColorBrush(Color.FromArgb(40, GetTrackColor(i).R, GetTrackColor(i).G, GetTrackColor(i).B)),
                Opacity = _tracks[i].IsSelected ? 1.0 : 0.3
            };
            _spectrumPaths[i] = path;
            SpectrumCanvas.Children.Add(path);
        }

        DrawDbScale();
        DrawFrequencyLabels();
        UpdateLegend();
        UpdateLayout();
    }

    private void DrawDbScale()
    {
        DbScaleCanvas.Children.Clear();

        var textBrush = FindResource("FrequencyCollisionSecondaryTextBrush") as Brush ?? Brushes.Gray;
        var gridBrush = FindResource("FrequencyCollisionBorderBrush") as Brush ?? Brushes.DarkGray;

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

        var textBrush = FindResource("FrequencyCollisionSecondaryTextBrush") as Brush ?? Brushes.Gray;
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

    private void UpdateLegend()
    {
        LegendPanel.Children.Clear();

        for (int i = 0; i < _tracks.Count; i++)
        {
            if (!_tracks[i].IsSelected) continue;

            var legendItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };

            var colorBox = new Shapes.Rectangle
            {
                Width = 12,
                Height = 12,
                Fill = new SolidColorBrush(GetTrackColor(i)),
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 6, 0)
            };
            legendItem.Children.Add(colorBox);

            var textBlock = new TextBlock
            {
                Text = _tracks[i].Name,
                Foreground = FindResource("FrequencyCollisionTextBrush") as Brush ?? Brushes.White,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            legendItem.Children.Add(textBlock);

            LegendPanel.Children.Add(legendItem);
        }

        // Add collision legend item
        if (_tracks.Count > 1)
        {
            var collisionItem = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 2) };

            var collisionBox = new Shapes.Rectangle
            {
                Width = 12,
                Height = 12,
                Fill = FindResource("FrequencyCollisionErrorBrush") as Brush ?? Brushes.Red,
                RadiusX = 2,
                RadiusY = 2,
                Margin = new Thickness(0, 0, 6, 0),
                Opacity = 0.6
            };
            collisionItem.Children.Add(collisionBox);

            var collisionText = new TextBlock
            {
                Text = "Collision Zone",
                Foreground = FindResource("FrequencyCollisionSecondaryTextBrush") as Brush ?? Brushes.Gray,
                FontSize = 10,
                VerticalAlignment = VerticalAlignment.Center
            };
            collisionItem.Children.Add(collisionText);

            LegendPanel.Children.Add(collisionItem);
        }
    }

    private new void UpdateLayout()
    {
        if (_collisionBars == null) return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        double barWidth = width / _bandCount;

        for (int i = 0; i < _bandCount; i++)
        {
            _collisionBars[i].Width = Math.Max(1, barWidth - 1);
            _collisionBars[i].Height = height;
            Canvas.SetLeft(_collisionBars[i], i * barWidth);
            Canvas.SetTop(_collisionBars[i], 0);
        }

        DrawDbScale();
        DrawFrequencyLabels();
        UpdateSpectrumDisplay();
    }

    #endregion

    #region Display Updates

    private void UpdateSpectrumDisplay()
    {
        if (_spectrumPaths == null || !_isInitialized) return;

        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        for (int trackIndex = 0; trackIndex < _tracks.Count && trackIndex < _spectrumPaths.Length; trackIndex++)
        {
            var track = _tracks[trackIndex];
            var path = _spectrumPaths[trackIndex];

            if (!track.IsSelected || track.Magnitudes == null || track.Magnitudes.Length == 0)
            {
                path.Data = null;
                continue;
            }

            var geometry = new PathGeometry();
            var figure = new PathFigure { IsClosed = true };

            // Start at bottom left
            figure.StartPoint = new Point(0, height);

            // Draw spectrum line
            int count = Math.Min(_bandCount, track.Magnitudes.Length);
            for (int i = 0; i < count; i++)
            {
                double x = (double)i / (count - 1) * width;
                double db = track.Magnitudes[i];
                double normalizedDb = Math.Clamp((db - MinDb) / (MaxDb - MinDb), 0, 1);
                double y = height * (1 - normalizedDb);

                figure.Segments.Add(new LineSegment(new Point(x, y), true));
            }

            // Close path to bottom right
            figure.Segments.Add(new LineSegment(new Point(width, height), true));

            geometry.Figures.Add(figure);
            path.Data = geometry;
            path.Opacity = track.IsSelected ? 1.0 : 0.3;
        }

        UpdateCollisionHighlighting();
    }

    private void UpdateCollisionHighlighting()
    {
        if (_collisionBars == null || _lastResult == null) return;

        foreach (var band in _lastResult.Bands)
        {
            int bandIndex = FrequencyToBandIndex(band.CenterFrequency);
            if (bandIndex < 0 || bandIndex >= _bandCount) continue;

            byte alpha = (byte)(band.CollisionScore * 180);
            _collisionBars[bandIndex].Fill = new SolidColorBrush(Color.FromArgb(alpha, 255, 71, 87));
        }
    }

    private void UpdateSeverityIndicator()
    {
        if (_lastResult == null)
        {
            SeverityScoreText.Text = "0%";
            SeverityBar.Width = 0;
            PrimaryCollisionText.Text = "None";
            CollisionIndicatorText.Text = "No Collision";
            CollisionIndicatorBorder.Background = FindResource("FrequencyCollisionSuccessBrush") as Brush;
            return;
        }

        float score = _lastResult.OverallCollisionScore;
        SeverityScoreText.Text = $"{score * 100:F0}%";

        // Animate severity bar
        double maxWidth = ((Border)SeverityBar.Parent).ActualWidth - SeverityBar.Margin.Left - SeverityBar.Margin.Right;
        SeverityBar.Width = maxWidth * score;

        // Update severity bar color based on score
        if (score < 0.3f)
        {
            SeverityBar.Background = FindResource("FrequencyCollisionSuccessBrush") as Brush;
            CollisionIndicatorBorder.Background = FindResource("FrequencyCollisionSuccessBrush") as Brush;
            CollisionIndicatorText.Text = "No Collision";
        }
        else if (score < 0.6f)
        {
            SeverityBar.Background = FindResource("FrequencyCollisionWarningBrush") as Brush;
            CollisionIndicatorBorder.Background = FindResource("FrequencyCollisionWarningBrush") as Brush;
            CollisionIndicatorText.Text = "Minor Collision";
        }
        else
        {
            SeverityBar.Background = FindResource("FrequencyCollisionErrorBrush") as Brush;
            CollisionIndicatorBorder.Background = FindResource("FrequencyCollisionErrorBrush") as Brush;
            CollisionIndicatorText.Text = "Significant Collision";
        }

        // Update primary collision frequency
        if (_lastResult.PrimaryCollisionFrequency > 0)
        {
            PrimaryCollisionText.Text = FormatFrequency(_lastResult.PrimaryCollisionFrequency);
        }
        else
        {
            PrimaryCollisionText.Text = "None";
        }
    }

    private void UpdateEqSuggestions()
    {
        _eqSuggestions.Clear();

        if (_lastResult == null) return;

        var suggestions = _lastResult.GetEqSuggestions();

        foreach (var (frequency, cutDb, source) in suggestions)
        {
            var bandIndex = FrequencyToBandIndex(frequency);
            var band = _lastResult.Bands.FirstOrDefault(b => Math.Abs(b.CenterFrequency - frequency) < 1f);
            string severity = band?.Severity ?? "Unknown";

            string trackName = source == "A" && _tracks.Count > 0 ? _tracks[0].Name :
                              source == "B" && _tracks.Count > 1 ? _tracks[1].Name :
                              $"Track {source}";

            _eqSuggestions.Add(new EqSuggestionItem
            {
                Frequency = frequency,
                CutDb = cutDb,
                SourceTrack = source,
                TrackName = trackName,
                Severity = severity,
                FrequencyText = FormatFrequency(frequency),
                CutText = $"{cutDb:F1} dB",
                SourceText = trackName,
                SeverityText = severity
            });
        }

        SuggestionCountText.Text = _eqSuggestions.Count == 1
            ? "1 suggestion"
            : $"{_eqSuggestions.Count} suggestions";
    }

    #endregion

    #region Track Management

    /// <summary>
    /// Adds a track to the collision analysis.
    /// </summary>
    public void AddTrack(string name, int trackId)
    {
        var trackData = new TrackAnalysisData
        {
            Name = name,
            TrackId = trackId,
            IsSelected = _tracks.Count < 2, // Auto-select first two tracks
            Magnitudes = new float[_bandCount]
        };

        _tracks.Add(trackData);

        // Add checkbox to panel
        var checkbox = new System.Windows.Controls.CheckBox
        {
            Content = CreateTrackCheckboxContent(name, _tracks.Count - 1),
            IsChecked = trackData.IsSelected,
            Style = FindResource("FrequencyCollisionCheckBoxStyle") as Style,
            Tag = trackId
        };
        checkbox.Checked += TrackCheckbox_CheckedChanged;
        checkbox.Unchecked += TrackCheckbox_CheckedChanged;
        _trackCheckboxes.Add(checkbox);
        TrackSelectorPanel.Children.Add(checkbox);

        if (_isInitialized)
        {
            BuildVisualTree();
        }
    }

    /// <summary>
    /// Removes a track from the collision analysis.
    /// </summary>
    public void RemoveTrack(int trackId)
    {
        int index = _tracks.FindIndex(t => t.TrackId == trackId);
        if (index < 0) return;

        _tracks.RemoveAt(index);

        if (index < _trackCheckboxes.Count)
        {
            TrackSelectorPanel.Children.Remove(_trackCheckboxes[index]);
            _trackCheckboxes.RemoveAt(index);
        }

        if (_isInitialized)
        {
            BuildVisualTree();
        }
    }

    /// <summary>
    /// Updates the spectrum data for a track.
    /// </summary>
    public void UpdateTrackSpectrum(int trackId, float[] magnitudes)
    {
        var track = _tracks.FirstOrDefault(t => t.TrackId == trackId);
        if (track == null) return;

        if (magnitudes.Length != _bandCount)
        {
            var resized = track.Magnitudes;
            Array.Resize(ref resized, magnitudes.Length);
            track.Magnitudes = resized;
        }

        Array.Copy(magnitudes, track.Magnitudes, Math.Min(magnitudes.Length, track.Magnitudes.Length));

        if (_isInitialized && IsRealTime)
        {
            UpdateSpectrumDisplay();
        }
    }

    /// <summary>
    /// Clears all tracks.
    /// </summary>
    public void ClearTracks()
    {
        _tracks.Clear();
        _trackCheckboxes.Clear();
        TrackSelectorPanel.Children.Clear();
        _eqSuggestions.Clear();
        _lastResult = null;

        if (_isInitialized)
        {
            BuildVisualTree();
            UpdateSeverityIndicator();
        }
    }

    private StackPanel CreateTrackCheckboxContent(string name, int index)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        var colorIndicator = new Shapes.Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(GetTrackColor(index)),
            RadiusX = 2,
            RadiusY = 2,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(colorIndicator);

        var textBlock = new TextBlock
        {
            Text = name,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(textBlock);

        return panel;
    }

    private void TrackCheckbox_CheckedChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.CheckBox checkbox || checkbox.Tag is not int trackId) return;

        var track = _tracks.FirstOrDefault(t => t.TrackId == trackId);
        if (track != null)
        {
            track.IsSelected = checkbox.IsChecked == true;
        }

        if (_isInitialized)
        {
            BuildVisualTree();
            RunAnalysis();
        }
    }

    #endregion

    #region Analysis

    /// <summary>
    /// Runs collision analysis on the selected tracks.
    /// </summary>
    public void RunAnalysis()
    {
        var selectedTracks = _tracks.Where(t => t.IsSelected).ToList();
        if (selectedTracks.Count < 2)
        {
            _lastResult = null;
            _eqSuggestions.Clear();
            UpdateSeverityIndicator();
            UpdateSpectrumDisplay();
            return;
        }

        // For simplicity, analyze first two selected tracks
        var trackA = selectedTracks[0];
        var trackB = selectedTracks[1];

        if (trackA.Magnitudes == null || trackB.Magnitudes == null) return;

        // Convert dB magnitudes to linear for analysis
        var samplesA = ConvertDbToLinear(trackA.Magnitudes);
        var samplesB = ConvertDbToLinear(trackB.Magnitudes);

        _lastResult = _detector?.AnalyzeBuffers(samplesA, samplesB);

        if (_lastResult != null)
        {
            UpdateSeverityIndicator();
            UpdateEqSuggestions();
            UpdateSpectrumDisplay();
            CollisionAnalysisUpdated?.Invoke(this, _lastResult);
        }
    }

    private float[] ConvertDbToLinear(float[] dbValues)
    {
        var linear = new float[dbValues.Length];
        for (int i = 0; i < dbValues.Length; i++)
        {
            linear[i] = MathF.Pow(10, dbValues[i] / 20f);
        }
        return linear;
    }

    private void OnDetectorCollisionUpdated(object? sender, CollisionAnalysisResult result)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _lastResult = result;
            UpdateSeverityIndicator();
            UpdateEqSuggestions();
            UpdateSpectrumDisplay();
            CollisionAnalysisUpdated?.Invoke(this, result);
        });
    }

    #endregion

    #region Real-Time Analysis

    private void StartRealTimeAnalysis()
    {
        if (_updateTimer != null) return;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // 20 FPS
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        _updateTimer.Start();
    }

    private void StopRealTimeAnalysis()
    {
        _updateTimer?.Stop();
        _updateTimer = null;
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        if (_isInitialized)
        {
            UpdateSpectrumDisplay();
        }
    }

    #endregion

    #region Event Handlers

    private void PresetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PresetComboBox.SelectedItem is not ComboBoxItem item) return;

        string preset = item.Content?.ToString() ?? "Full Range";
        ApplyPreset(preset);
    }

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        RunAnalysis();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _lastResult = null;
        _eqSuggestions.Clear();

        // Reset collision bars
        if (_collisionBars != null)
        {
            foreach (var bar in _collisionBars)
            {
                bar.Fill = new SolidColorBrush(Color.FromArgb(0, 255, 71, 87));
            }
        }

        UpdateSeverityIndicator();
        UpdateSpectrumDisplay();
    }

    private void ThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ThresholdValue == null) return;
        ThresholdValue.Text = $"{e.NewValue:F0}%";

        if (_detector != null)
        {
            _detector.CollisionThreshold = (float)(e.NewValue / 100.0);
        }

        ParameterChanged?.Invoke(this, new FrequencyCollisionParameterChangedEventArgs("CollisionThreshold", (float)(e.NewValue / 100.0)));
    }

    private void MaskingThresholdSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaskingThresholdValue == null) return;
        MaskingThresholdValue.Text = $"{e.NewValue:F0} dB";

        if (_detector != null)
        {
            _detector.MaskingThresholdDb = (float)e.NewValue;
        }

        ParameterChanged?.Invoke(this, new FrequencyCollisionParameterChangedEventArgs("MaskingThresholdDb", (float)e.NewValue));
    }

    private void SmoothingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SmoothingValue == null) return;
        SmoothingValue.Text = $"{e.NewValue:F0}%";

        if (_detector != null)
        {
            _detector.SmoothingFactor = (float)(e.NewValue / 100.0);
        }

        ParameterChanged?.Invoke(this, new FrequencyCollisionParameterChangedEventArgs("SmoothingFactor", (float)(e.NewValue / 100.0)));
    }

    private void MaxCutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MaxCutValue == null) return;
        MaxCutValue.Text = $"{e.NewValue:F0} dB";

        if (_detector != null)
        {
            _detector.MaxSuggestedCutDb = (float)e.NewValue;
        }

        ParameterChanged?.Invoke(this, new FrequencyCollisionParameterChangedEventArgs("MaxSuggestedCutDb", (float)e.NewValue));
    }

    private void EqSuggestionsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Visual feedback only - actual apply happens on button click
    }

    private void ApplySelectedButton_Click(object sender, RoutedEventArgs e)
    {
        if (EqSuggestionsListBox.SelectedItem is not EqSuggestionItem suggestion) return;

        EqSuggestionApplied?.Invoke(this, new EqSuggestionAppliedEventArgs(
            suggestion.Frequency,
            suggestion.CutDb,
            suggestion.SourceTrack,
            suggestion.TrackName));
    }

    private void ApplyAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var suggestion in _eqSuggestions)
        {
            EqSuggestionApplied?.Invoke(this, new EqSuggestionAppliedEventArgs(
                suggestion.Frequency,
                suggestion.CutDb,
                suggestion.SourceTrack,
                suggestion.TrackName));
        }
    }

    private void SpectrumCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        Point position = e.GetPosition(SpectrumCanvas);
        double width = SpectrumCanvas.ActualWidth;
        double height = SpectrumCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Calculate frequency from X position
        double freq = XToFrequency(position.X, width);

        // Update hover info
        HoverFrequencyText.Text = FormatFrequency(freq);

        // Find collision at this frequency
        if (_lastResult != null)
        {
            var band = _lastResult.Bands.FirstOrDefault(b =>
                freq >= b.LowFrequency && freq <= b.HighFrequency);

            if (band != null && band.HasCollision)
            {
                HoverCollisionText.Text = $"Collision: {band.Severity} ({band.CollisionScore * 100:F0}%)";
                HoverCollisionText.Foreground = FindResource("FrequencyCollisionErrorBrush") as Brush;
            }
            else
            {
                HoverCollisionText.Text = "Collision: None";
                HoverCollisionText.Foreground = FindResource("FrequencyCollisionSecondaryTextBrush") as Brush;
            }
        }
        else
        {
            HoverCollisionText.Text = "Collision: N/A";
            HoverCollisionText.Foreground = FindResource("FrequencyCollisionSecondaryTextBrush") as Brush;
        }

        // Position tooltip
        Canvas.SetLeft(HoverInfoBorder, Math.Min(position.X + 10, width - HoverInfoBorder.ActualWidth - 10));
        Canvas.SetTop(HoverInfoBorder, Math.Max(position.Y - HoverInfoBorder.ActualHeight - 10, 10));
        HoverInfoBorder.Visibility = Visibility.Visible;
    }

    private void SpectrumCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        HoverInfoBorder.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region Presets

    private void ApplyPreset(string preset)
    {
        switch (preset)
        {
            case "Kick/Bass":
                ThresholdSlider.Value = 25;
                MaskingThresholdSlider.Value = -36;
                SmoothingSlider.Value = 30;
                MaxCutSlider.Value = -9;
                break;

            case "Vocal/Instrument":
                ThresholdSlider.Value = 30;
                MaskingThresholdSlider.Value = -42;
                SmoothingSlider.Value = 35;
                MaxCutSlider.Value = -6;
                break;

            case "Full Range":
            default:
                ThresholdSlider.Value = 35;
                MaskingThresholdSlider.Value = -48;
                SmoothingSlider.Value = 30;
                MaxCutSlider.Value = -12;
                break;
        }

        if (_isInitialized)
        {
            RunAnalysis();
        }
    }

    #endregion

    #region Helper Methods

    private static Color GetTrackColor(int index)
    {
        return TrackColors[index % TrackColors.Length];
    }

    private double FrequencyToX(double frequency, double width)
    {
        double t = Math.Log(frequency / MinFrequency) / Math.Log(MaxFrequency / MinFrequency);
        return t * width;
    }

    private double XToFrequency(double x, double width)
    {
        double t = x / width;
        return MinFrequency * Math.Pow(MaxFrequency / MinFrequency, t);
    }

    private int FrequencyToBandIndex(float frequency)
    {
        if (_bandFrequencies.Length == 0) return -1;

        for (int i = 0; i < _bandFrequencies.Length; i++)
        {
            if (i == _bandFrequencies.Length - 1 || frequency < (_bandFrequencies[i] + _bandFrequencies[i + 1]) / 2)
            {
                return i;
            }
        }

        return _bandFrequencies.Length - 1;
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

    private static void OnSampleRateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrequencyCollisionControl control)
        {
            control.InitializeDetector();
        }
    }

    private static void OnIsRealTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrequencyCollisionControl control) return;

        if ((bool)e.NewValue)
        {
            control.StartRealTimeAnalysis();
        }
        else
        {
            control.StopRealTimeAnalysis();
        }
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// Holds analysis data for a single track.
/// </summary>
public class TrackAnalysisData
{
    /// <summary>Track name for display.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique track identifier.</summary>
    public int TrackId { get; set; }

    /// <summary>Whether this track is selected for analysis.</summary>
    public bool IsSelected { get; set; }

    /// <summary>Spectrum magnitude data in dB.</summary>
    public float[] Magnitudes { get; set; } = Array.Empty<float>();
}

/// <summary>
/// Represents an EQ suggestion item for the list.
/// </summary>
public class EqSuggestionItem
{
    /// <summary>Center frequency in Hz.</summary>
    public float Frequency { get; set; }

    /// <summary>Suggested cut amount in dB.</summary>
    public float CutDb { get; set; }

    /// <summary>Source track identifier (A/B).</summary>
    public string SourceTrack { get; set; } = string.Empty;

    /// <summary>Source track name.</summary>
    public string TrackName { get; set; } = string.Empty;

    /// <summary>Collision severity description.</summary>
    public string Severity { get; set; } = string.Empty;

    /// <summary>Formatted frequency text for display.</summary>
    public string FrequencyText { get; set; } = string.Empty;

    /// <summary>Formatted cut text for display.</summary>
    public string CutText { get; set; } = string.Empty;

    /// <summary>Source track text for display.</summary>
    public string SourceText { get; set; } = string.Empty;

    /// <summary>Severity text for display.</summary>
    public string SeverityText { get; set; } = string.Empty;
}

/// <summary>
/// Event arguments for EQ suggestion applied events.
/// </summary>
public class EqSuggestionAppliedEventArgs : EventArgs
{
    /// <summary>Frequency in Hz.</summary>
    public float Frequency { get; }

    /// <summary>Suggested cut in dB.</summary>
    public float CutDb { get; }

    /// <summary>Source track identifier.</summary>
    public string SourceTrack { get; }

    /// <summary>Track name.</summary>
    public string TrackName { get; }

    public EqSuggestionAppliedEventArgs(float frequency, float cutDb, string sourceTrack, string trackName)
    {
        Frequency = frequency;
        CutDb = cutDb;
        SourceTrack = sourceTrack;
        TrackName = trackName;
    }
}

/// <summary>
/// Event arguments for parameter change events.
/// </summary>
public class FrequencyCollisionParameterChangedEventArgs : EventArgs
{
    /// <summary>Parameter name.</summary>
    public string ParameterName { get; }

    /// <summary>New value.</summary>
    public float Value { get; }

    public FrequencyCollisionParameterChangedEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Converter for collision severity to color.
/// </summary>
public class FrequencyCollisionSeverityToColorConverter : IValueConverter
{
    public static readonly FrequencyCollisionSeverityToColorConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not float score) return Brushes.Gray;

        return score switch
        {
            < 0.3f => new SolidColorBrush(Color.FromRgb(0, 255, 136)),   // Green
            < 0.6f => new SolidColorBrush(Color.FromRgb(255, 165, 2)),   // Orange
            _ => new SolidColorBrush(Color.FromRgb(255, 71, 87))         // Red
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converter for boolean to visibility.
/// </summary>
public class FrequencyCollisionBoolToVisibilityConverter : IValueConverter
{
    public static readonly FrequencyCollisionBoolToVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

#endregion
