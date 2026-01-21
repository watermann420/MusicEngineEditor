using System;
using System.Windows.Controls;
using System.Windows.Threading;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Code-behind for the MixerView user control.
/// Manages the mixer interface with channel strips, volume faders, and level meters.
/// </summary>
public partial class MixerView : UserControl
{
    private readonly MixerViewModel _viewModel;
    private readonly DispatcherTimer? _meterTimer;
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new MixerView and initializes the MixerViewModel.
    /// </summary>
    public MixerView()
    {
        InitializeComponent();

        // Initialize ViewModel and set as DataContext
        _viewModel = new MixerViewModel();
        DataContext = _viewModel;

        // Optional: Start a DispatcherTimer to simulate meter levels for demo
        _meterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50) // ~20 FPS for smooth meter animation
        };
        _meterTimer.Tick += OnMeterTimerTick;
        _meterTimer.Start();

        // Clean up timer when unloaded
        Unloaded += (s, e) => StopMeterSimulation();
    }

    /// <summary>
    /// Timer tick handler that simulates meter levels for demonstration purposes.
    /// </summary>
    private void OnMeterTimerTick(object? sender, EventArgs e)
    {
        // Simulate meter levels for each channel
        for (int i = 0; i < _viewModel.Channels.Count; i++)
        {
            var channel = _viewModel.Channels[i];

            // Skip muted channels
            if (channel.IsEffectivelyMuted)
            {
                channel.UpdateMeters(0f, 0f);
                continue;
            }

            // Generate simulated audio levels based on volume
            float baseLevel = channel.Volume * 0.7f;
            float variation = (float)(_random.NextDouble() * 0.3);

            // Apply pan to left/right distribution
            float panLeft = Math.Max(0, -channel.Pan + 1) / 2;
            float panRight = Math.Max(0, channel.Pan + 1) / 2;

            float left = Math.Min(1.1f, (baseLevel + variation) * panLeft);
            float right = Math.Min(1.1f, (baseLevel + variation) * panRight);

            channel.UpdateMeters(left, right);
        }

        // Simulate master meter levels (sum of all channels, simplified)
        float masterLeft = 0f;
        float masterRight = 0f;

        foreach (var channel in _viewModel.Channels)
        {
            if (!channel.IsEffectivelyMuted)
            {
                masterLeft += channel.MeterLeft * 0.15f;
                masterRight += channel.MeterRight * 0.15f;
            }
        }

        // Apply master volume and clamp
        masterLeft = Math.Min(1.2f, masterLeft * _viewModel.MasterChannel.Volume);
        masterRight = Math.Min(1.2f, masterRight * _viewModel.MasterChannel.Volume);

        _viewModel.UpdateMasterMeters(masterLeft, masterRight);
    }

    /// <summary>
    /// Starts the meter level simulation.
    /// </summary>
    public void StartMeterSimulation()
    {
        _meterTimer?.Start();
    }

    /// <summary>
    /// Stops the meter level simulation.
    /// </summary>
    public void StopMeterSimulation()
    {
        _meterTimer?.Stop();
    }

    /// <summary>
    /// Gets the MixerViewModel associated with this view.
    /// </summary>
    public MixerViewModel ViewModel => _viewModel;
}
