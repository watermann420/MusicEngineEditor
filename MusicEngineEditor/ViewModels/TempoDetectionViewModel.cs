using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Analysis;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for tempo detection panel with BPM display, tap tempo, and beat analysis.
/// </summary>
public partial class TempoDetectionViewModel : ViewModelBase
{
    private readonly TempoAnalysisService _tempoService;
    private CancellationTokenSource? _analysisCts;
    private DateTime _lastTapReset = DateTime.MinValue;
    private const double TapResetTimeoutSeconds = 3.0;

    [ObservableProperty]
    private double _detectedBpm;

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private int _tapCount;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _canApplyBpm;

    [ObservableProperty]
    private string _confidenceLevel = "Low";

    [ObservableProperty]
    private BeatAnalysisResult? _lastAnalysisResult;

    [ObservableProperty]
    private List<double> _beatPositions = [];

    [ObservableProperty]
    private double _audioDuration;

    /// <summary>
    /// Event raised when BPM should be applied to the project.
    /// </summary>
    public event EventHandler<double>? ApplyBpmRequested;

    /// <summary>
    /// Event raised when detection from audio selection is requested.
    /// </summary>
    public event EventHandler? DetectFromSelectionRequested;

    /// <summary>
    /// Event raised when tap tempo is performed.
    /// </summary>
    public event EventHandler<double>? TapPerformed;

    /// <summary>
    /// Event raised when analysis completes.
    /// </summary>
    public event EventHandler<BeatAnalysisResult>? AnalysisCompleted;

    public TempoDetectionViewModel()
    {
        _tempoService = TempoAnalysisService.Instance;
        _tempoService.TempoDetected += OnTempoDetected;
        _tempoService.TapTempoUpdated += OnTapTempoUpdated;
    }

    private void OnTempoDetected(object? sender, TempoEventArgs e)
    {
        DetectedBpm = e.Bpm;
        Confidence = e.Confidence;
        UpdateConfidenceLevel();
        CanApplyBpm = e.Bpm > 0;
    }

    private void OnTapTempoUpdated(object? sender, double bpm)
    {
        if (bpm > 0)
        {
            DetectedBpm = bpm;
            Confidence = Math.Min(1.0, _tempoService.TapCount / 8.0);
            UpdateConfidenceLevel();
            CanApplyBpm = true;
        }
        TapCount = _tempoService.TapCount;
        TapPerformed?.Invoke(this, bpm);
    }

    partial void OnConfidenceChanged(double value)
    {
        UpdateConfidenceLevel();
    }

    partial void OnDetectedBpmChanged(double value)
    {
        CanApplyBpm = value > 0;
    }

    private void UpdateConfidenceLevel()
    {
        ConfidenceLevel = Confidence switch
        {
            >= 0.7 => "High",
            >= 0.4 => "Medium",
            _ => "Low"
        };
    }

    /// <summary>
    /// Gets the confidence percentage (0-100).
    /// </summary>
    public double ConfidencePercent => Confidence * 100;

    /// <summary>
    /// Gets the BPM display text.
    /// </summary>
    public string BpmDisplayText => DetectedBpm > 0 ? DetectedBpm.ToString("F1") : "---";

    /// <summary>
    /// Gets the tap count display text.
    /// </summary>
    public string TapCountText => $"{TapCount} taps";

    [RelayCommand]
    private void Tap()
    {
        // Check if we need to reset due to timeout
        if ((DateTime.UtcNow - _lastTapReset).TotalSeconds > TapResetTimeoutSeconds && TapCount > 0)
        {
            // Last tap was too long ago, but we just tapped, so it's handled by service
        }

        _tempoService.Tap();
        _lastTapReset = DateTime.UtcNow;
    }

    [RelayCommand]
    private void ResetTapTempo()
    {
        _tempoService.ResetTapTempo();
        TapCount = 0;
    }

    [RelayCommand]
    private void DetectFromSelection()
    {
        DetectFromSelectionRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanApplyBpm))]
    private void ApplyBpm()
    {
        if (DetectedBpm > 0)
        {
            ApplyBpmRequested?.Invoke(this, DetectedBpm);
        }
    }

    [RelayCommand]
    private async Task AnalyzeAudioAsync(float[]? samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        _analysisCts?.Cancel();
        _analysisCts = new CancellationTokenSource();

        IsAnalyzing = true;
        IsBusy = true;
        StatusMessage = "Analyzing audio...";

        try
        {
            LastAnalysisResult = await _tempoService.AnalyzeAudioAsync(
                samples,
                44100,
                _analysisCts.Token);

            DetectedBpm = LastAnalysisResult.DetectedBpm;
            Confidence = LastAnalysisResult.Confidence;
            BeatPositions = [.. LastAnalysisResult.Beats];
            AudioDuration = LastAnalysisResult.DurationSeconds;
            CanApplyBpm = DetectedBpm > 0;

            StatusMessage = LastAnalysisResult.IsReliable
                ? "Detection complete"
                : "Low confidence - verify manually";

            AnalysisCompleted?.Invoke(this, LastAnalysisResult);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Analysis cancelled";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Analyzes audio samples synchronously (for use from UI code-behind).
    /// </summary>
    public void AnalyzeAudio(float[] samples, int sampleRate = 44100)
    {
        _ = AnalyzeAudioAsync(samples);
    }

    /// <summary>
    /// Sets the analysis result directly (for external sources).
    /// </summary>
    public void SetAnalysisResult(BeatAnalysisResult result)
    {
        LastAnalysisResult = result;
        DetectedBpm = result.DetectedBpm;
        Confidence = result.Confidence;
        BeatPositions = [.. result.Beats];
        AudioDuration = result.DurationSeconds;
        CanApplyBpm = DetectedBpm > 0;
        AnalysisCompleted?.Invoke(this, result);
    }

    /// <summary>
    /// Sets the detected BPM manually (e.g., from user input).
    /// </summary>
    public void SetManualBpm(double bpm)
    {
        if (bpm > 0)
        {
            DetectedBpm = bpm;
            Confidence = 1.0; // Manual input has full confidence
            CanApplyBpm = true;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        _tempoService.Reset();
        DetectedBpm = 0;
        Confidence = 0;
        TapCount = 0;
        LastAnalysisResult = null;
        BeatPositions = [];
        AudioDuration = 0;
        CanApplyBpm = false;
        StatusMessage = "Ready";
    }

    /// <summary>
    /// Cancels any ongoing analysis.
    /// </summary>
    public void CancelAnalysis()
    {
        _analysisCts?.Cancel();
    }

    /// <summary>
    /// Generates warp markers from the current analysis.
    /// </summary>
    public List<WarpMarker> GetWarpMarkers()
    {
        if (LastAnalysisResult == null)
            return [];

        return LastAnalysisResult.ToWarpMarkers();
    }

    /// <summary>
    /// Gets a simple beat grid based on detected BPM.
    /// </summary>
    public List<WarpMarker> GetSimpleBeatGrid(double durationSeconds, double startOffset = 0)
    {
        if (DetectedBpm <= 0)
            return [];

        return _tempoService.GenerateSimpleGrid(DetectedBpm, durationSeconds, startOffset);
    }
}
