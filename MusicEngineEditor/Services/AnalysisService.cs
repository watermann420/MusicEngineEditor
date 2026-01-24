// MusicEngineEditor - Analysis Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Threading;
using System.Windows.Threading;
using MusicEngine.Core;
using MusicEngine.Core.Analysis;
using NAudio.Wave;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service managing the audio analysis chain.
/// Connects to AudioEngine output and distributes analysis data to UI components.
/// </summary>
public sealed class AnalysisService : IDisposable
{
    #region Singleton

    private static readonly Lazy<AnalysisService> _instance = new(
        () => new AnalysisService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the AnalysisService.
    /// </summary>
    public static AnalysisService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private AnalysisChain? _analysisChain;
    private SpectrumAnalyzer? _spectrumAnalyzer;
    private CorrelationMeter? _correlationMeter;
    private EnhancedPeakDetector? _peakDetector;
    private GoniometerDataProvider? _goniometer;

    private readonly Dispatcher _dispatcher;
    private readonly object _lock = new();
    private bool _disposed;
    private bool _isRunning;

    // Refresh rate control
    private int _refreshRateMs = 33; // ~30 fps default
    private DateTime _lastSpectrumUpdate;
    private DateTime _lastCorrelationUpdate;
    private DateTime _lastPeakUpdate;
    private DateTime _lastGoniometerUpdate;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether the analysis service is currently running.
    /// </summary>
    public bool IsRunning => _isRunning;

    /// <summary>
    /// Gets or sets whether spectrum analysis is enabled.
    /// </summary>
    public bool SpectrumEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether correlation metering is enabled.
    /// </summary>
    public bool CorrelationEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether peak detection is enabled.
    /// </summary>
    public bool PeakEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets whether goniometer visualization is enabled.
    /// </summary>
    public bool GoniometerEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UI refresh rate in milliseconds.
    /// </summary>
    public int RefreshRateMs
    {
        get => _refreshRateMs;
        set => _refreshRateMs = Math.Clamp(value, 16, 200);
    }

    /// <summary>
    /// Gets the spectrum analyzer instance.
    /// </summary>
    public SpectrumAnalyzer? SpectrumAnalyzer => _spectrumAnalyzer;

    /// <summary>
    /// Gets the correlation meter instance.
    /// </summary>
    public CorrelationMeter? CorrelationMeter => _correlationMeter;

    /// <summary>
    /// Gets the peak detector instance.
    /// </summary>
    public EnhancedPeakDetector? PeakDetector => _peakDetector;

    /// <summary>
    /// Gets the goniometer data provider instance.
    /// </summary>
    public GoniometerDataProvider? Goniometer => _goniometer;

    #endregion

    #region Events

    /// <summary>
    /// Raised when spectrum data is updated (on UI thread).
    /// </summary>
    public event EventHandler<SpectrumEventArgs>? SpectrumUpdated;

    /// <summary>
    /// Raised when correlation data is updated (on UI thread).
    /// </summary>
    public event EventHandler<CorrelationEventArgs>? CorrelationUpdated;

    /// <summary>
    /// Raised when peak data is updated (on UI thread).
    /// </summary>
    public event EventHandler<PeakEventArgs>? PeakUpdated;

    /// <summary>
    /// Raised when goniometer data is updated (on UI thread).
    /// </summary>
    public event EventHandler<GoniometerEventArgs>? GoniometerUpdated;

    /// <summary>
    /// Raised when the analysis service starts.
    /// </summary>
    public event EventHandler? Started;

    /// <summary>
    /// Raised when the analysis service stops.
    /// </summary>
    public event EventHandler? Stopped;

    #endregion

    #region Constructor

    private AnalysisService()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the analysis service with an audio source.
    /// </summary>
    /// <param name="source">The audio source to analyze (stereo recommended).</param>
    /// <param name="sampleRate">Sample rate of the audio source.</param>
    public void Initialize(ISampleProvider source, int sampleRate = 44100)
    {
        lock (_lock)
        {
            // Clean up existing analyzers
            Cleanup();

            // Create standalone analyzers for external sample feeding
            _spectrumAnalyzer = new SpectrumAnalyzer(
                bandCount: 31,
                fftLength: 4096,
                sampleRate: sampleRate);

            // Create analysis chain if we have a valid source
            if (source != null)
            {
                _analysisChain = new AnalysisChain(source);

                // Access built-in analyzers to initialize them
                _ = _analysisChain.SpectrumAnalyzer;
                _correlationMeter = _analysisChain.CorrelationMeter;
                _peakDetector = _analysisChain.PeakDetector;
                _goniometer = _analysisChain.Goniometer;
            }
            else
            {
                // Create standalone analyzers for manual sample feeding
                _peakDetector = null;
                _correlationMeter = null;
                _goniometer = null;
            }

            // Subscribe to analyzer events
            SubscribeToEvents();
        }
    }

    /// <summary>
    /// Initializes with standalone analyzers (for manual sample feeding).
    /// </summary>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    /// <param name="channels">Number of audio channels (1 or 2).</param>
    public void InitializeStandalone(int sampleRate = 44100, int channels = 2)
    {
        lock (_lock)
        {
            Cleanup();

            // Create standalone spectrum analyzer
            _spectrumAnalyzer = new SpectrumAnalyzer(
                bandCount: 31,
                fftLength: 4096,
                sampleRate: sampleRate);

            // Subscribe to spectrum events
            _spectrumAnalyzer.SpectrumUpdated += OnSpectrumUpdatedInternal;
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts the analysis service.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;
            _isRunning = true;
            Started?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Stops the analysis service.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Processes audio samples manually (for standalone mode).
    /// </summary>
    /// <param name="samples">Audio samples (interleaved if stereo).</param>
    /// <param name="count">Number of samples.</param>
    /// <param name="channels">Number of channels.</param>
    public void ProcessSamples(float[] samples, int count, int channels = 2)
    {
        if (!_isRunning) return;

        lock (_lock)
        {
            if (SpectrumEnabled && _spectrumAnalyzer != null)
            {
                _spectrumAnalyzer.ProcessSamples(samples, count, channels);
            }

            if (CorrelationEnabled && _correlationMeter != null && channels == 2)
            {
                _correlationMeter.AnalyzeSamples(samples, count);
            }

            if (PeakEnabled && _peakDetector != null)
            {
                _peakDetector.AnalyzeSamples(samples, count);
            }

            if (GoniometerEnabled && _goniometer != null && channels == 2)
            {
                _goniometer.AnalyzeSamples(samples, count);
            }
        }
    }

    /// <summary>
    /// Resets all analyzers.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _spectrumAnalyzer?.Reset();
            _correlationMeter?.Reset();
            _peakDetector?.Reset();
            _goniometer?.Clear();
            _analysisChain?.Reset();
        }
    }

    /// <summary>
    /// Resets peak hold values.
    /// </summary>
    public void ResetPeaks()
    {
        lock (_lock)
        {
            _spectrumAnalyzer?.ResetPeaks();
            _peakDetector?.ResetMaxPeaks();
        }
    }

    #endregion

    #region Event Subscription

    private void SubscribeToEvents()
    {
        if (_spectrumAnalyzer != null)
        {
            _spectrumAnalyzer.SpectrumUpdated += OnSpectrumUpdatedInternal;
        }

        if (_correlationMeter != null)
        {
            _correlationMeter.CorrelationUpdated += OnCorrelationUpdatedInternal;
        }

        if (_peakDetector != null)
        {
            _peakDetector.PeakUpdated += OnPeakUpdatedInternal;
        }

        if (_goniometer != null)
        {
            _goniometer.DataUpdated += OnGoniometerUpdatedInternal;
        }

        // Also subscribe to analysis chain analyzers if available
        if (_analysisChain != null)
        {
            _analysisChain.SpectrumAnalyzer.SpectrumUpdated += OnSpectrumUpdatedInternal;
        }
    }

    private void UnsubscribeFromEvents()
    {
        if (_spectrumAnalyzer != null)
        {
            _spectrumAnalyzer.SpectrumUpdated -= OnSpectrumUpdatedInternal;
        }

        if (_correlationMeter != null)
        {
            _correlationMeter.CorrelationUpdated -= OnCorrelationUpdatedInternal;
        }

        if (_peakDetector != null)
        {
            _peakDetector.PeakUpdated -= OnPeakUpdatedInternal;
        }

        if (_goniometer != null)
        {
            _goniometer.DataUpdated -= OnGoniometerUpdatedInternal;
        }

        if (_analysisChain != null)
        {
            _analysisChain.SpectrumAnalyzer.SpectrumUpdated -= OnSpectrumUpdatedInternal;
        }
    }

    #endregion

    #region Internal Event Handlers

    private void OnSpectrumUpdatedInternal(object? sender, SpectrumEventArgs e)
    {
        if (!_isRunning || !SpectrumEnabled) return;

        var now = DateTime.UtcNow;
        if ((now - _lastSpectrumUpdate).TotalMilliseconds < _refreshRateMs) return;
        _lastSpectrumUpdate = now;

        _dispatcher.BeginInvoke(() =>
        {
            SpectrumUpdated?.Invoke(this, e);
        }, DispatcherPriority.Render);
    }

    private void OnCorrelationUpdatedInternal(object? sender, CorrelationEventArgs e)
    {
        if (!_isRunning || !CorrelationEnabled) return;

        var now = DateTime.UtcNow;
        if ((now - _lastCorrelationUpdate).TotalMilliseconds < _refreshRateMs) return;
        _lastCorrelationUpdate = now;

        _dispatcher.BeginInvoke(() =>
        {
            CorrelationUpdated?.Invoke(this, e);
        }, DispatcherPriority.Render);
    }

    private void OnPeakUpdatedInternal(object? sender, PeakEventArgs e)
    {
        if (!_isRunning || !PeakEnabled) return;

        var now = DateTime.UtcNow;
        if ((now - _lastPeakUpdate).TotalMilliseconds < _refreshRateMs) return;
        _lastPeakUpdate = now;

        _dispatcher.BeginInvoke(() =>
        {
            PeakUpdated?.Invoke(this, e);
        }, DispatcherPriority.Render);
    }

    private void OnGoniometerUpdatedInternal(object? sender, GoniometerEventArgs e)
    {
        if (!_isRunning || !GoniometerEnabled) return;

        var now = DateTime.UtcNow;
        if ((now - _lastGoniometerUpdate).TotalMilliseconds < _refreshRateMs) return;
        _lastGoniometerUpdate = now;

        _dispatcher.BeginInvoke(() =>
        {
            GoniometerUpdated?.Invoke(this, e);
        }, DispatcherPriority.Render);
    }

    #endregion

    #region Cleanup

    private void Cleanup()
    {
        UnsubscribeFromEvents();
        _analysisChain = null;
        _spectrumAnalyzer = null;
        _correlationMeter = null;
        _peakDetector = null;
        _goniometer = null;
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            Cleanup();
        }
    }

    #endregion
}
