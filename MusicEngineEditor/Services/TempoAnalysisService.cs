using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service for tempo detection, beat analysis, and tap tempo tracking.
/// </summary>
public class TempoAnalysisService
{
    private static TempoAnalysisService? _instance;
    private static readonly object _lock = new();

    private readonly TempoDetector _tempoDetector;
    private readonly TransientDetector _transientDetector;
    private readonly WarpMarkerGenerator _warpMarkerGenerator;

    // Tap tempo tracking
    private readonly List<DateTime> _tapTimes = [];
    private const int MaxTapHistory = 8;
    private const double TapTimeoutSeconds = 2.0;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static TempoAnalysisService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new TempoAnalysisService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Event raised when tempo is detected or updated.
    /// </summary>
    public event EventHandler<TempoEventArgs>? TempoDetected;

    /// <summary>
    /// Event raised when tap tempo BPM is updated.
    /// </summary>
    public event EventHandler<double>? TapTempoUpdated;

    /// <summary>
    /// Gets the last detected BPM from audio analysis.
    /// </summary>
    public double LastDetectedBpm { get; private set; }

    /// <summary>
    /// Gets the confidence level of the last detection.
    /// </summary>
    public double LastConfidence { get; private set; }

    /// <summary>
    /// Gets the current tap tempo BPM.
    /// </summary>
    public double TapTempoBpm { get; private set; }

    private TempoAnalysisService(int sampleRate = 44100)
    {
        _tempoDetector = new TempoDetector(sampleRate, minBpm: 60, maxBpm: 200);
        _transientDetector = new TransientDetector(sampleRate);
        _warpMarkerGenerator = new WarpMarkerGenerator(sampleRate);

        _tempoDetector.TempoDetected += OnTempoDetected;
    }

    private void OnTempoDetected(object? sender, TempoEventArgs e)
    {
        LastDetectedBpm = e.Bpm;
        LastConfidence = e.Confidence;
        TempoDetected?.Invoke(this, e);
    }

    /// <summary>
    /// Analyzes an audio buffer and returns beat analysis results.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>BeatAnalysisResult with detected BPM, confidence, and beat positions.</returns>
    public Task<BeatAnalysisResult> AnalyzeAudioAsync(
        float[] samples,
        int sampleRate = 44100,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = _tempoDetector.AnalyzeBuffer(samples, sampleRate);

            // Also detect transients for beat positions
            var transients = _transientDetector.AnalyzeBuffer(samples, sampleRate);

            // Populate beats list from transients
            result.Beats = [];
            foreach (var transient in transients)
            {
                if (transient.IsStrong || transient.Strength > 0.5f)
                {
                    result.Beats.Add(transient.TimeSeconds);
                }
            }

            // Find downbeats (strong transients)
            result.Downbeats = [];
            foreach (var transient in transients)
            {
                if (transient.IsStrong)
                {
                    result.Downbeats.Add(transient.TimeSeconds);
                }
            }

            result.DurationSeconds = samples.Length / (double)sampleRate;
            result.StartOffset = result.Beats.Count > 0 ? result.Beats[0] : 0;

            LastDetectedBpm = result.DetectedBpm;
            LastConfidence = result.Confidence;

            return result;
        }, cancellationToken);
    }

    /// <summary>
    /// Detects transients/beats in an audio buffer.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <param name="threshold">Detection threshold (0-1).</param>
    /// <param name="sensitivity">Detection sensitivity (0.1-10).</param>
    /// <returns>List of detected transient events.</returns>
    public List<TransientEvent> DetectTransients(
        float[] samples,
        int sampleRate = 44100,
        float threshold = 0.5f,
        float sensitivity = 1.0f)
    {
        _transientDetector.Threshold = threshold;
        _transientDetector.Sensitivity = sensitivity;
        return _transientDetector.AnalyzeBuffer(samples, sampleRate);
    }

    /// <summary>
    /// Generates warp markers from audio analysis.
    /// </summary>
    /// <param name="samples">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate.</param>
    /// <returns>List of generated warp markers.</returns>
    public List<WarpMarker> GenerateWarpMarkers(float[] samples, int sampleRate = 44100)
    {
        return _warpMarkerGenerator.GenerateMarkers(samples, sampleRate);
    }

    /// <summary>
    /// Generates a simple beat grid from a known BPM.
    /// </summary>
    /// <param name="bpm">Tempo in BPM.</param>
    /// <param name="durationSeconds">Total duration.</param>
    /// <param name="startOffset">Start offset in seconds.</param>
    /// <returns>List of warp markers on a regular grid.</returns>
    public List<WarpMarker> GenerateSimpleGrid(double bpm, double durationSeconds, double startOffset = 0)
    {
        return _warpMarkerGenerator.GenerateSimpleGrid(bpm, durationSeconds, startOffset);
    }

    /// <summary>
    /// Records a tap for tap tempo calculation.
    /// </summary>
    /// <returns>Current calculated BPM from taps, or 0 if not enough taps.</returns>
    public double Tap()
    {
        var now = DateTime.UtcNow;

        // Clear old taps if timeout exceeded
        if (_tapTimes.Count > 0)
        {
            var lastTap = _tapTimes[^1];
            if ((now - lastTap).TotalSeconds > TapTimeoutSeconds)
            {
                _tapTimes.Clear();
            }
        }

        // Add new tap
        _tapTimes.Add(now);

        // Keep only recent taps
        while (_tapTimes.Count > MaxTapHistory)
        {
            _tapTimes.RemoveAt(0);
        }

        // Calculate BPM if we have at least 2 taps
        if (_tapTimes.Count >= 2)
        {
            var intervals = new List<double>();
            for (int i = 1; i < _tapTimes.Count; i++)
            {
                intervals.Add((_tapTimes[i] - _tapTimes[i - 1]).TotalSeconds);
            }

            // Calculate average interval
            double avgInterval = 0;
            foreach (var interval in intervals)
            {
                avgInterval += interval;
            }
            avgInterval /= intervals.Count;

            // Convert to BPM
            TapTempoBpm = avgInterval > 0 ? 60.0 / avgInterval : 0;
            TapTempoUpdated?.Invoke(this, TapTempoBpm);
            return TapTempoBpm;
        }

        return 0;
    }

    /// <summary>
    /// Resets tap tempo tracking.
    /// </summary>
    public void ResetTapTempo()
    {
        _tapTimes.Clear();
        TapTempoBpm = 0;
    }

    /// <summary>
    /// Gets the number of taps recorded.
    /// </summary>
    public int TapCount => _tapTimes.Count;

    /// <summary>
    /// Resets all analysis state.
    /// </summary>
    public void Reset()
    {
        _tempoDetector.Reset();
        _transientDetector.Reset();
        ResetTapTempo();
        LastDetectedBpm = 0;
        LastConfidence = 0;
    }
}
