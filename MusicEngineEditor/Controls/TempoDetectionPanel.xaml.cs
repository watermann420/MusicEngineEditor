using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using MusicEngine.Core.Analysis;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Tempo detection panel with BPM display, tap tempo, and beat visualization.
/// Supports both direct usage and ViewModel binding.
/// </summary>
public partial class TempoDetectionPanel : UserControl
{
    private readonly TempoAnalysisService _tempoService;
    private readonly DispatcherTimer _tapResetTimer;
    private TempoDetectionViewModel? _viewModel;
    private BeatAnalysisResult? _lastAnalysisResult;
    private double _detectedBpm;
    private double _confidence;

    /// <summary>
    /// Event raised when BPM should be applied to the project.
    /// </summary>
    public event EventHandler<double>? ApplyBpmRequested;

    /// <summary>
    /// Event raised when detection from selection is requested.
    /// </summary>
    public event EventHandler? DetectFromSelectionRequested;

    /// <summary>
    /// Gets or sets the detected BPM.
    /// </summary>
    public double DetectedBpm
    {
        get => _detectedBpm;
        set
        {
            _detectedBpm = value;
            UpdateBpmDisplay();
        }
    }

    /// <summary>
    /// Gets or sets the detection confidence (0-1).
    /// </summary>
    public double Confidence
    {
        get => _confidence;
        set
        {
            _confidence = Math.Clamp(value, 0, 1);
            UpdateConfidenceDisplay();
        }
    }

    public TempoDetectionPanel()
    {
        InitializeComponent();

        _tempoService = TempoAnalysisService.Instance;
        _tempoService.TempoDetected += OnTempoDetected;
        _tempoService.TapTempoUpdated += OnTapTempoUpdated;

        // Timer to reset tap tempo after inactivity
        _tapResetTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _tapResetTimer.Tick += OnTapResetTimerTick;

        // Handle spacebar for tap tempo when focused
        KeyDown += OnKeyDown;
        Focusable = true;
    }

    private void OnTempoDetected(object? sender, TempoEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            DetectedBpm = e.Bpm;
            Confidence = e.Confidence;
            ApplyButton.IsEnabled = e.Bpm > 0;
        });
    }

    private void OnTapTempoUpdated(object? sender, double bpm)
    {
        Dispatcher.Invoke(() =>
        {
            if (bpm > 0)
            {
                DetectedBpm = bpm;
                Confidence = Math.Min(1.0, _tempoService.TapCount / 8.0);
                ApplyButton.IsEnabled = true;
            }
            TapCountText.Text = $"{_tempoService.TapCount} taps";

            // Reset timer
            _tapResetTimer.Stop();
            _tapResetTimer.Start();
        });
    }

    private void OnTapResetTimerTick(object? sender, EventArgs e)
    {
        _tapResetTimer.Stop();
        _tempoService.ResetTapTempo();
        TapCountText.Text = "0 taps";
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
        {
            PerformTap();
            e.Handled = true;
        }
    }

    private void TapButton_Click(object sender, RoutedEventArgs e)
    {
        PerformTap();
    }

    private void PerformTap()
    {
        _tempoService.Tap();
    }

    private async void DetectButton_Click(object sender, RoutedEventArgs e)
    {
        DetectFromSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (DetectedBpm > 0)
        {
            ApplyBpmRequested?.Invoke(this, DetectedBpm);
        }
    }

    /// <summary>
    /// Analyzes audio samples for tempo detection.
    /// </summary>
    public async void AnalyzeAudio(float[] samples, int sampleRate = 44100)
    {
        StatusText.Text = "Analyzing...";
        DetectButton.IsEnabled = false;

        try
        {
            _lastAnalysisResult = await _tempoService.AnalyzeAudioAsync(samples, sampleRate);

            DetectedBpm = _lastAnalysisResult.DetectedBpm;
            Confidence = _lastAnalysisResult.Confidence;
            ApplyButton.IsEnabled = DetectedBpm > 0;

            // Draw beat markers
            DrawBeatMarkers(_lastAnalysisResult.Beats, _lastAnalysisResult.DurationSeconds);

            StatusText.Text = _lastAnalysisResult.IsReliable
                ? "Detection complete"
                : "Low confidence - verify manually";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
        finally
        {
            DetectButton.IsEnabled = true;
        }
    }

    /// <summary>
    /// Sets the analysis result directly.
    /// </summary>
    public void SetAnalysisResult(BeatAnalysisResult result)
    {
        _lastAnalysisResult = result;
        DetectedBpm = result.DetectedBpm;
        Confidence = result.Confidence;
        ApplyButton.IsEnabled = DetectedBpm > 0;
        DrawBeatMarkers(result.Beats, result.DurationSeconds);
    }

    private void UpdateBpmDisplay()
    {
        if (DetectedBpm > 0)
        {
            BpmDisplay.Text = DetectedBpm.ToString("F1");
        }
        else
        {
            BpmDisplay.Text = "---";
        }
    }

    private void UpdateConfidenceDisplay()
    {
        double percentage = Confidence * 100;
        ConfidenceBar.Value = percentage;
        ConfidenceText.Text = $"{percentage:F0}%";

        // Update color based on confidence
        if (Confidence >= 0.7)
        {
            ConfidenceBar.Foreground = FindResource("SuccessBrush") as Brush ?? Brushes.Green;
        }
        else if (Confidence >= 0.4)
        {
            ConfidenceBar.Foreground = FindResource("WarningBrush") as Brush ?? Brushes.Orange;
        }
        else
        {
            ConfidenceBar.Foreground = FindResource("ErrorBrush") as Brush ?? Brushes.Red;
        }
    }

    private void DrawBeatMarkers(List<double> beats, double totalDuration)
    {
        BeatCanvas.Children.Clear();

        if (beats.Count == 0 || totalDuration <= 0)
            return;

        double canvasWidth = BeatCanvas.ActualWidth > 0 ? BeatCanvas.ActualWidth : 300;
        double canvasHeight = BeatCanvas.ActualHeight > 0 ? BeatCanvas.ActualHeight : 40;

        // Draw timeline background
        var timeline = new Shapes.Rectangle
        {
            Width = canvasWidth,
            Height = 2,
            Fill = FindResource("BorderBrush") as Brush ?? Brushes.Gray
        };
        Canvas.SetTop(timeline, canvasHeight / 2 - 1);
        BeatCanvas.Children.Add(timeline);

        // Draw beat markers
        var beatBrush = FindResource("AccentBrush") as Brush ?? Brushes.Blue;
        var strongBeatBrush = FindResource("SuccessBrush") as Brush ?? Brushes.Green;

        int barCounter = 0;
        foreach (var beat in beats)
        {
            double x = (beat / totalDuration) * canvasWidth;
            bool isDownbeat = barCounter % 4 == 0;

            var marker = new Shapes.Ellipse
            {
                Width = isDownbeat ? 8 : 5,
                Height = isDownbeat ? 8 : 5,
                Fill = isDownbeat ? strongBeatBrush : beatBrush
            };

            Canvas.SetLeft(marker, x - marker.Width / 2);
            Canvas.SetTop(marker, canvasHeight / 2 - marker.Height / 2);
            BeatCanvas.Children.Add(marker);

            barCounter++;
        }
    }

    /// <summary>
    /// Resets the panel state.
    /// </summary>
    public void Reset()
    {
        _tempoService.Reset();
        DetectedBpm = 0;
        Confidence = 0;
        _lastAnalysisResult = null;
        ApplyButton.IsEnabled = false;
        TapCountText.Text = "0 taps";
        StatusText.Text = "Ready";
        BeatCanvas.Children.Clear();

        _viewModel?.ResetCommand.Execute(null);
    }

    /// <summary>
    /// Binds to a TempoDetectionViewModel.
    /// </summary>
    public void BindViewModel(TempoDetectionViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.AnalysisCompleted -= OnViewModelAnalysisCompleted;
        }

        _viewModel = viewModel;
        DataContext = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.AnalysisCompleted += OnViewModelAnalysisCompleted;

            // Sync initial values
            DetectedBpm = _viewModel.DetectedBpm;
            Confidence = _viewModel.Confidence;
            TapCountText.Text = _viewModel.TapCountText;
            ApplyButton.IsEnabled = _viewModel.CanApplyBpm;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(TempoDetectionViewModel.DetectedBpm):
                    DetectedBpm = _viewModel.DetectedBpm;
                    break;
                case nameof(TempoDetectionViewModel.Confidence):
                    Confidence = _viewModel.Confidence;
                    break;
                case nameof(TempoDetectionViewModel.TapCount):
                    TapCountText.Text = _viewModel.TapCountText;
                    break;
                case nameof(TempoDetectionViewModel.CanApplyBpm):
                    ApplyButton.IsEnabled = _viewModel.CanApplyBpm;
                    break;
                case nameof(TempoDetectionViewModel.IsAnalyzing):
                    DetectButton.IsEnabled = !_viewModel.IsAnalyzing;
                    StatusText.Text = _viewModel.IsAnalyzing ? "Analyzing..." : "Ready";
                    break;
                case nameof(TempoDetectionViewModel.StatusMessage):
                    if (!string.IsNullOrEmpty(_viewModel.StatusMessage))
                    {
                        StatusText.Text = _viewModel.StatusMessage;
                    }
                    break;
            }
        });
    }

    private void OnViewModelAnalysisCompleted(object? sender, BeatAnalysisResult result)
    {
        Dispatcher.Invoke(() =>
        {
            _lastAnalysisResult = result;
            DrawBeatMarkers(result.Beats, result.DurationSeconds);
        });
    }

    /// <summary>
    /// Gets the associated ViewModel.
    /// </summary>
    public TempoDetectionViewModel? ViewModel => _viewModel;
}
