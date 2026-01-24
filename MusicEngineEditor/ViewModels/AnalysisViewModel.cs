// MusicEngineEditor - Analysis ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Analysis;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for audio analysis visualizers.
/// Manages spectrum, correlation, goniometer, and peak data.
/// </summary>
public partial class AnalysisViewModel : ViewModelBase
{
    #region Private Fields

    private readonly AnalysisService _analysisService;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Spectrum band magnitudes (0.0 to 1.0 range).
    /// </summary>
    [ObservableProperty]
    private float[] _spectrumData = Array.Empty<float>();

    /// <summary>
    /// Spectrum peak hold magnitudes.
    /// </summary>
    [ObservableProperty]
    private float[] _spectrumPeaks = Array.Empty<float>();

    /// <summary>
    /// Frequency labels for each spectrum band.
    /// </summary>
    [ObservableProperty]
    private float[] _spectrumFrequencies = Array.Empty<float>();

    /// <summary>
    /// Stereo correlation value (-1.0 to +1.0).
    /// </summary>
    [ObservableProperty]
    private double _correlation;

    /// <summary>
    /// Mid level (L+R).
    /// </summary>
    [ObservableProperty]
    private double _midLevel;

    /// <summary>
    /// Side level (L-R).
    /// </summary>
    [ObservableProperty]
    private double _sideLevel;

    /// <summary>
    /// Mid/Side ratio (0 = all side, 1 = all mid).
    /// </summary>
    [ObservableProperty]
    private double _msRatio = 0.5;

    /// <summary>
    /// Goniometer display points.
    /// </summary>
    [ObservableProperty]
    private GoniometerPoint[] _goniometerPoints = Array.Empty<GoniometerPoint>();

    /// <summary>
    /// Left channel true peak level (linear).
    /// </summary>
    [ObservableProperty]
    private float _leftPeak;

    /// <summary>
    /// Right channel true peak level (linear).
    /// </summary>
    [ObservableProperty]
    private float _rightPeak;

    /// <summary>
    /// Left channel true peak in dBTP.
    /// </summary>
    [ObservableProperty]
    private float _leftPeakDbtp = -60f;

    /// <summary>
    /// Right channel true peak in dBTP.
    /// </summary>
    [ObservableProperty]
    private float _rightPeakDbtp = -60f;

    /// <summary>
    /// Left channel maximum true peak (linear).
    /// </summary>
    [ObservableProperty]
    private float _leftMaxPeak;

    /// <summary>
    /// Right channel maximum true peak (linear).
    /// </summary>
    [ObservableProperty]
    private float _rightMaxPeak;

    /// <summary>
    /// Maximum true peak in dBTP.
    /// </summary>
    [ObservableProperty]
    private float _maxTruePeakDbtp = -60f;

    /// <summary>
    /// Whether the signal has clipped (exceeded 0 dBTP).
    /// </summary>
    [ObservableProperty]
    private bool _hasClipped;

    /// <summary>
    /// Whether spectrum analysis is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isSpectrumEnabled = true;

    /// <summary>
    /// Whether correlation metering is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isCorrelationEnabled = true;

    /// <summary>
    /// Whether goniometer visualization is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isGoniometerEnabled = true;

    /// <summary>
    /// Whether peak detection is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isPeakEnabled = true;

    /// <summary>
    /// Whether the analysis is running.
    /// </summary>
    [ObservableProperty]
    private bool _isAnalysisRunning;

    /// <summary>
    /// UI refresh rate in milliseconds.
    /// </summary>
    [ObservableProperty]
    private int _refreshRateMs = 33;

    /// <summary>
    /// Spectrum smoothing factor (0 = no smoothing, 1 = max).
    /// </summary>
    [ObservableProperty]
    private float _spectrumSmoothing = 0.3f;

    /// <summary>
    /// Peak hold decay rate.
    /// </summary>
    [ObservableProperty]
    private float _peakDecayRate = 0.95f;

    /// <summary>
    /// Number of spectrum bands.
    /// </summary>
    [ObservableProperty]
    private int _bandCount = 31;

    /// <summary>
    /// Whether to use compact display mode.
    /// </summary>
    [ObservableProperty]
    private bool _isCompactMode;

    /// <summary>
    /// Selected analyzer tab index.
    /// </summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    #endregion

    #region Constructor

    public AnalysisViewModel()
    {
        _analysisService = AnalysisService.Instance;
        SubscribeToEvents();
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void StartAnalysis()
    {
        _analysisService.Start();
        IsAnalysisRunning = true;
    }

    [RelayCommand]
    private void StopAnalysis()
    {
        _analysisService.Stop();
        IsAnalysisRunning = false;
    }

    [RelayCommand]
    private void ToggleAnalysis()
    {
        if (IsAnalysisRunning)
        {
            StopAnalysis();
        }
        else
        {
            StartAnalysis();
        }
    }

    [RelayCommand]
    private void ResetPeaks()
    {
        _analysisService.ResetPeaks();
        LeftMaxPeak = 0;
        RightMaxPeak = 0;
        MaxTruePeakDbtp = -60f;
        HasClipped = false;
    }

    [RelayCommand]
    private void ResetAll()
    {
        _analysisService.Reset();
        ResetPeaks();
        SpectrumData = Array.Empty<float>();
        SpectrumPeaks = Array.Empty<float>();
        GoniometerPoints = Array.Empty<GoniometerPoint>();
        Correlation = 0;
        MidLevel = 0;
        SideLevel = 0;
        MsRatio = 0.5;
    }

    [RelayCommand]
    private void ToggleSpectrum()
    {
        IsSpectrumEnabled = !IsSpectrumEnabled;
        _analysisService.SpectrumEnabled = IsSpectrumEnabled;
    }

    [RelayCommand]
    private void ToggleCorrelation()
    {
        IsCorrelationEnabled = !IsCorrelationEnabled;
        _analysisService.CorrelationEnabled = IsCorrelationEnabled;
    }

    [RelayCommand]
    private void ToggleGoniometer()
    {
        IsGoniometerEnabled = !IsGoniometerEnabled;
        _analysisService.GoniometerEnabled = IsGoniometerEnabled;
    }

    [RelayCommand]
    private void TogglePeak()
    {
        IsPeakEnabled = !IsPeakEnabled;
        _analysisService.PeakEnabled = IsPeakEnabled;
    }

    [RelayCommand]
    private void ToggleCompactMode()
    {
        IsCompactMode = !IsCompactMode;
    }

    #endregion

    #region Event Handlers

    private void SubscribeToEvents()
    {
        _analysisService.SpectrumUpdated += OnSpectrumUpdated;
        _analysisService.CorrelationUpdated += OnCorrelationUpdated;
        _analysisService.PeakUpdated += OnPeakUpdated;
        _analysisService.GoniometerUpdated += OnGoniometerUpdated;
        _analysisService.Started += OnAnalysisStarted;
        _analysisService.Stopped += OnAnalysisStopped;
    }

    private void OnSpectrumUpdated(object? sender, SpectrumEventArgs e)
    {
        if (!IsSpectrumEnabled) return;

        SpectrumData = e.Magnitudes;
        SpectrumPeaks = e.Peaks;
        SpectrumFrequencies = e.Frequencies;
    }

    private void OnCorrelationUpdated(object? sender, CorrelationEventArgs e)
    {
        if (!IsCorrelationEnabled) return;

        Correlation = e.Correlation;
        MidLevel = e.MidLevel;
        SideLevel = e.SideLevel;
        MsRatio = e.MSRatio;
    }

    private void OnPeakUpdated(object? sender, PeakEventArgs e)
    {
        if (!IsPeakEnabled) return;

        if (e.CurrentPeaks.Length >= 1)
        {
            LeftPeak = e.CurrentPeaks[0];
            LeftPeakDbtp = 20f * MathF.Log10(Math.Max(LeftPeak, 1e-10f));
        }

        if (e.CurrentPeaks.Length >= 2)
        {
            RightPeak = e.CurrentPeaks[1];
            RightPeakDbtp = 20f * MathF.Log10(Math.Max(RightPeak, 1e-10f));
        }

        if (e.MaxPeaks.Length >= 1)
        {
            LeftMaxPeak = e.MaxPeaks[0];
        }

        if (e.MaxPeaks.Length >= 2)
        {
            RightMaxPeak = e.MaxPeaks[1];
        }

        MaxTruePeakDbtp = e.MaxTruePeakDbtp;
        HasClipped = e.MaxTruePeak >= 1.0f;
    }

    private void OnGoniometerUpdated(object? sender, GoniometerEventArgs e)
    {
        if (!IsGoniometerEnabled) return;

        GoniometerPoints = e.Points;
    }

    private void OnAnalysisStarted(object? sender, EventArgs e)
    {
        IsAnalysisRunning = true;
    }

    private void OnAnalysisStopped(object? sender, EventArgs e)
    {
        IsAnalysisRunning = false;
    }

    #endregion

    #region Property Change Handlers

    partial void OnRefreshRateMsChanged(int value)
    {
        _analysisService.RefreshRateMs = value;
    }

    partial void OnSpectrumSmoothingChanged(float value)
    {
        if (_analysisService.SpectrumAnalyzer != null)
        {
            _analysisService.SpectrumAnalyzer.SmoothingFactor = value;
        }
    }

    partial void OnPeakDecayRateChanged(float value)
    {
        if (_analysisService.SpectrumAnalyzer != null)
        {
            _analysisService.SpectrumAnalyzer.PeakDecayRate = value;
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Formats a frequency value to a readable string.
    /// </summary>
    public static string FormatFrequency(float hz)
    {
        if (hz >= 1000)
        {
            return $"{hz / 1000:F1}k";
        }
        return $"{hz:F0}";
    }

    /// <summary>
    /// Formats a dB value to a readable string.
    /// </summary>
    public static string FormatDb(float db)
    {
        if (db <= -60)
        {
            return "-inf";
        }
        return $"{db:F1}";
    }

    /// <summary>
    /// Gets a color for the correlation value.
    /// </summary>
    public static Color GetCorrelationColor(double correlation)
    {
        // Green at center (0), transitioning to red at extremes (-1, +1)
        // But slight out-of-phase (negative) should be more red than in-phase
        if (correlation >= 0)
        {
            // 0 to 1: green to cyan
            byte g = (byte)(255 * (1 - correlation * 0.3));
            byte b = (byte)(255 * correlation * 0.5);
            return Color.FromRgb(0, g, b);
        }
        else
        {
            // -1 to 0: red to green
            double absCorr = Math.Abs(correlation);
            byte r = (byte)(255 * absCorr);
            byte g = (byte)(255 * (1 - absCorr));
            return Color.FromRgb(r, g, 0);
        }
    }

    #endregion
}
