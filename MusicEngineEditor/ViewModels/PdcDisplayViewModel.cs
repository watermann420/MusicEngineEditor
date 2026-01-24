// MusicEngineEditor - PDC Display ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.PDC;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents latency information for a single track.
/// </summary>
public partial class TrackLatencyInfo : ObservableObject
{
    [ObservableProperty]
    private string _trackId = string.Empty;

    [ObservableProperty]
    private string _trackName = string.Empty;

    [ObservableProperty]
    private int _latencySamples;

    [ObservableProperty]
    private double _latencyMs;

    [ObservableProperty]
    private int _compensationSamples;

    [ObservableProperty]
    private double _compensationMs;

    [ObservableProperty]
    private bool _isCompensated;

    [ObservableProperty]
    private string _latencyLevel = "Low";

    [ObservableProperty]
    private string _pluginNames = string.Empty;

    /// <summary>
    /// Gets the latency as a normalized value (0-1) for display.
    /// Based on max typical latency of ~10000 samples.
    /// </summary>
    public double LatencyNormalized => Math.Min(1.0, LatencySamples / 10000.0);

    /// <summary>
    /// Updates latency level classification based on sample count.
    /// </summary>
    public void UpdateLatencyLevel()
    {
        LatencyLevel = LatencySamples switch
        {
            < 256 => "Low",
            < 1024 => "Medium",
            < 4096 => "High",
            _ => "VeryHigh"
        };
    }
}

/// <summary>
/// ViewModel for the PDC (Plugin Delay Compensation) display control.
/// Provides real-time latency information across all tracks.
/// </summary>
public partial class PdcDisplayViewModel : ViewModelBase
{
    private readonly DispatcherTimer _refreshTimer;
    private PdcManager? _pdcManager;
    private int _sampleRate = 44100;

    #region Observable Properties

    [ObservableProperty]
    private int _totalLatencySamples;

    [ObservableProperty]
    private double _totalLatencyMs;

    [ObservableProperty]
    private bool _isPdcEnabled = true;

    [ObservableProperty]
    private bool _isCompensationActive;

    [ObservableProperty]
    private int _trackCount;

    [ObservableProperty]
    private string _statusText = "No PDC Manager";

    [ObservableProperty]
    private string _detailedBreakdown = string.Empty;

    #endregion

    /// <summary>
    /// Collection of per-track latency information.
    /// </summary>
    public ObservableCollection<TrackLatencyInfo> TrackLatencies { get; } = [];

    /// <summary>
    /// Creates a new PdcDisplayViewModel.
    /// </summary>
    public PdcDisplayViewModel()
    {
        _refreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _refreshTimer.Tick += OnRefreshTimerTick;
    }

    /// <summary>
    /// Initializes the view model with a PDC manager.
    /// </summary>
    /// <param name="pdcManager">The PDC manager to monitor.</param>
    /// <param name="sampleRate">The sample rate for ms calculations.</param>
    public void Initialize(PdcManager pdcManager, int sampleRate = 44100)
    {
        if (_pdcManager != null)
        {
            _pdcManager.TotalLatencyChanged -= OnTotalLatencyChanged;
            _pdcManager.CompensationRecalculated -= OnCompensationRecalculated;
        }

        _pdcManager = pdcManager;
        _sampleRate = sampleRate > 0 ? sampleRate : 44100;

        if (_pdcManager != null)
        {
            _pdcManager.TotalLatencyChanged += OnTotalLatencyChanged;
            _pdcManager.CompensationRecalculated += OnCompensationRecalculated;
            IsPdcEnabled = _pdcManager.Enabled;
        }

        RefreshLatencyInfo();
        _refreshTimer.Start();
    }

    /// <summary>
    /// Stops monitoring and cleans up.
    /// </summary>
    public void Shutdown()
    {
        _refreshTimer.Stop();

        if (_pdcManager != null)
        {
            _pdcManager.TotalLatencyChanged -= OnTotalLatencyChanged;
            _pdcManager.CompensationRecalculated -= OnCompensationRecalculated;
        }
    }

    #region Commands

    [RelayCommand]
    private void Refresh()
    {
        RefreshLatencyInfo();
    }

    [RelayCommand]
    private void TogglePdc()
    {
        if (_pdcManager != null)
        {
            _pdcManager.Enabled = !_pdcManager.Enabled;
            IsPdcEnabled = _pdcManager.Enabled;
            RefreshLatencyInfo();
        }
    }

    #endregion

    #region Private Methods

    private void RefreshLatencyInfo()
    {
        if (_pdcManager == null)
        {
            TotalLatencySamples = 0;
            TotalLatencyMs = 0;
            TrackCount = 0;
            IsCompensationActive = false;
            StatusText = "No PDC Manager";
            TrackLatencies.Clear();
            return;
        }

        TotalLatencySamples = _pdcManager.MaxLatencySamples;
        TotalLatencyMs = _pdcManager.MaxLatencyMs;
        TrackCount = _pdcManager.TrackCount;
        IsPdcEnabled = _pdcManager.Enabled;
        IsCompensationActive = IsPdcEnabled && TotalLatencySamples > 0;

        // Update status text
        if (!IsPdcEnabled)
        {
            StatusText = "PDC Disabled";
        }
        else if (TotalLatencySamples == 0)
        {
            StatusText = "No Latency";
        }
        else
        {
            StatusText = $"{TotalLatencyMs:F1} ms ({TotalLatencySamples} samples)";
        }

        // Update track latencies
        var summary = _pdcManager.GetLatencySummary();
        UpdateTrackLatencies(summary);

        // Build detailed breakdown
        BuildDetailedBreakdown(summary);
    }

    private void UpdateTrackLatencies(Dictionary<string, (int Latency, int Compensation)> summary)
    {
        // Update existing or add new entries
        foreach (var kvp in summary)
        {
            var existing = TrackLatencies.FirstOrDefault(t => t.TrackId == kvp.Key);
            if (existing != null)
            {
                UpdateTrackLatencyInfo(existing, kvp.Key, kvp.Value.Latency, kvp.Value.Compensation);
            }
            else
            {
                var newInfo = new TrackLatencyInfo();
                UpdateTrackLatencyInfo(newInfo, kvp.Key, kvp.Value.Latency, kvp.Value.Compensation);
                TrackLatencies.Add(newInfo);
            }
        }

        // Remove entries no longer in summary
        var toRemove = TrackLatencies.Where(t => !summary.ContainsKey(t.TrackId)).ToList();
        foreach (var item in toRemove)
        {
            TrackLatencies.Remove(item);
        }
    }

    private void UpdateTrackLatencyInfo(TrackLatencyInfo info, string trackId, int latency, int compensation)
    {
        info.TrackId = trackId;
        info.TrackName = trackId;
        info.LatencySamples = latency;
        info.LatencyMs = SamplesToMs(latency);
        info.CompensationSamples = compensation;
        info.CompensationMs = SamplesToMs(compensation);
        info.IsCompensated = compensation > 0 || latency == TotalLatencySamples;
        info.UpdateLatencyLevel();
    }

    private void BuildDetailedBreakdown(Dictionary<string, (int Latency, int Compensation)> summary)
    {
        var lines = new List<string>
        {
            $"Total PDC Latency: {TotalLatencyMs:F2} ms ({TotalLatencySamples} samples)",
            $"Sample Rate: {_sampleRate} Hz",
            $"Tracks: {TrackCount}",
            "",
            "Per-Track Breakdown:"
        };

        foreach (var kvp in summary.OrderByDescending(x => x.Value.Latency))
        {
            var latencyMs = SamplesToMs(kvp.Value.Latency);
            var compMs = SamplesToMs(kvp.Value.Compensation);
            lines.Add($"  {kvp.Key}: {latencyMs:F2} ms latency, {compMs:F2} ms compensation");
        }

        DetailedBreakdown = string.Join(Environment.NewLine, lines);
    }

    private double SamplesToMs(int samples)
    {
        return _sampleRate > 0 ? (samples * 1000.0) / _sampleRate : 0;
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshLatencyInfo();
    }

    private void OnTotalLatencyChanged(object? sender, LatencyChangedEventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(RefreshLatencyInfo);
    }

    private void OnCompensationRecalculated(object? sender, EventArgs e)
    {
        System.Windows.Application.Current?.Dispatcher.Invoke(RefreshLatencyInfo);
    }

    #endregion
}
