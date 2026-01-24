// MusicEngineEditor - Input Monitor ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents an audio input device.
/// </summary>
public partial class InputDeviceInfo : ObservableObject
{
    [ObservableProperty]
    private int _deviceIndex;

    [ObservableProperty]
    private string _deviceName = string.Empty;

    [ObservableProperty]
    private int _channelCount;

    [ObservableProperty]
    private bool _isDefault;

    /// <summary>
    /// Gets the display name including channel count.
    /// </summary>
    public string DisplayName => $"{DeviceName} ({ChannelCount}ch)";

    public override string ToString() => DisplayName;
}

/// <summary>
/// ViewModel for the Input Monitor panel.
/// Manages audio input monitoring, device selection, and level metering.
/// </summary>
public partial class InputMonitorViewModel : ViewModelBase
{
    private readonly DispatcherTimer _levelUpdateTimer;
    private InputMonitor? _inputMonitor;
    private MonitoringSampleProvider? _monitoringProvider;
    private bool _isInitialized;
    private float _peakLeftHold;
    private float _peakRightHold;
    private DateTime _peakLeftTime;
    private DateTime _peakRightTime;

    #region Observable Properties

    [ObservableProperty]
    private InputDeviceInfo? _selectedDevice;

    [ObservableProperty]
    private bool _isMonitoringEnabled;

    [ObservableProperty]
    private float _monitoringVolume = 0.8f;

    [ObservableProperty]
    private bool _isDirectMonitoring;

    [ObservableProperty]
    private float _leftLevel;

    [ObservableProperty]
    private float _rightLevel;

    [ObservableProperty]
    private float _leftPeak;

    [ObservableProperty]
    private float _rightPeak;

    [ObservableProperty]
    private bool _leftClipping;

    [ObservableProperty]
    private bool _rightClipping;

    [ObservableProperty]
    private double _monitoringLatencyMs;

    [ObservableProperty]
    private int _sampleRate = 44100;

    [ObservableProperty]
    private int _channelCount = 2;

    [ObservableProperty]
    private bool _isInputActive;

    [ObservableProperty]
    private string _statusText = "Not initialized";

    [ObservableProperty]
    private bool _hasDevices;

    #endregion

    /// <summary>
    /// Collection of available input devices.
    /// </summary>
    public ObservableCollection<InputDeviceInfo> AvailableDevices { get; } = [];

    /// <summary>
    /// Creates a new InputMonitorViewModel.
    /// </summary>
    public InputMonitorViewModel()
    {
        _levelUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _levelUpdateTimer.Tick += OnLevelUpdateTick;
    }

    /// <summary>
    /// Initializes the view model with an InputMonitor instance.
    /// </summary>
    /// <param name="inputMonitor">The input monitor to use.</param>
    /// <param name="monitoringProvider">Optional monitoring sample provider for direct monitoring.</param>
    public void Initialize(InputMonitor? inputMonitor = null, MonitoringSampleProvider? monitoringProvider = null)
    {
        _inputMonitor = inputMonitor;
        _monitoringProvider = monitoringProvider;

        if (_inputMonitor != null)
        {
            _inputMonitor.LevelUpdated += OnInputLevelUpdated;
            SampleRate = _inputMonitor.WaveFormat.SampleRate;
            ChannelCount = _inputMonitor.WaveFormat.Channels;
            IsInputActive = _inputMonitor.IsInputActive;
        }

        if (_monitoringProvider != null)
        {
            _monitoringProvider.LevelUpdated += OnMonitoringLevelUpdated;
            IsDirectMonitoring = _monitoringProvider.DirectMonitoring;
        }

        RefreshDevices();
        _levelUpdateTimer.Start();
        _isInitialized = true;

        UpdateStatusText();
    }

    /// <summary>
    /// Shuts down the view model and cleans up resources.
    /// </summary>
    public void Shutdown()
    {
        _levelUpdateTimer.Stop();

        if (_inputMonitor != null)
        {
            _inputMonitor.LevelUpdated -= OnInputLevelUpdated;
        }

        if (_monitoringProvider != null)
        {
            _monitoringProvider.LevelUpdated -= OnMonitoringLevelUpdated;
        }

        _isInitialized = false;
    }

    #region Commands

    [RelayCommand]
    private void RefreshDevices()
    {
        AvailableDevices.Clear();

        var deviceCount = InputMonitor.GetInputDeviceCount();
        HasDevices = deviceCount > 0;

        for (int i = 0; i < deviceCount; i++)
        {
            var deviceName = InputMonitor.GetInputDeviceName(i);
            if (!string.IsNullOrEmpty(deviceName))
            {
                AvailableDevices.Add(new InputDeviceInfo
                {
                    DeviceIndex = i,
                    DeviceName = deviceName,
                    ChannelCount = 2, // NAudio WaveInEvent typically uses stereo
                    IsDefault = i == 0
                });
            }
        }

        // Select first device if none selected
        if (SelectedDevice == null && AvailableDevices.Count > 0)
        {
            SelectedDevice = AvailableDevices[0];
        }

        StatusText = HasDevices
            ? $"{AvailableDevices.Count} device(s) found"
            : "No input devices found";
    }

    [RelayCommand]
    private void ResetPeaks()
    {
        LeftPeak = 0;
        RightPeak = 0;
        LeftClipping = false;
        RightClipping = false;
        _peakLeftHold = 0;
        _peakRightHold = 0;

        _inputMonitor?.ResetLevels();
        _monitoringProvider?.ResetLevels();
    }

    [RelayCommand]
    private void ToggleMonitoring()
    {
        IsMonitoringEnabled = !IsMonitoringEnabled;
        ApplyMonitoringState();
    }

    [RelayCommand]
    private void ToggleDirectMonitoring()
    {
        IsDirectMonitoring = !IsDirectMonitoring;
        ApplyDirectMonitoringState();
    }

    [RelayCommand]
    private void StartCapture()
    {
        if (_inputMonitor != null && !_inputMonitor.IsInputActive)
        {
            _inputMonitor.StartCapture();
            IsInputActive = true;
            UpdateStatusText();
        }
    }

    [RelayCommand]
    private void StopCapture()
    {
        if (_inputMonitor != null && _inputMonitor.IsInputActive)
        {
            _inputMonitor.StopCapture();
            IsInputActive = false;
            UpdateStatusText();
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnSelectedDeviceChanged(InputDeviceInfo? value)
    {
        if (_inputMonitor != null && value != null)
        {
            _inputMonitor.InputDevice = value.DeviceIndex;
            ChannelCount = value.ChannelCount;
        }
        UpdateStatusText();
    }

    partial void OnIsMonitoringEnabledChanged(bool value)
    {
        ApplyMonitoringState();
        UpdateStatusText();
    }

    partial void OnMonitoringVolumeChanged(float value)
    {
        if (_inputMonitor != null)
        {
            _inputMonitor.MonitoringVolume = value;
        }
        if (_monitoringProvider != null)
        {
            _monitoringProvider.Volume = value;
        }
    }

    partial void OnIsDirectMonitoringChanged(bool value)
    {
        ApplyDirectMonitoringState();
    }

    #endregion

    #region Private Methods

    private void ApplyMonitoringState()
    {
        if (_inputMonitor != null)
        {
            _inputMonitor.MonitoringEnabled = IsMonitoringEnabled;
        }
        if (_monitoringProvider != null)
        {
            _monitoringProvider.IsEnabled = IsMonitoringEnabled;
        }
    }

    private void ApplyDirectMonitoringState()
    {
        if (_monitoringProvider != null)
        {
            _monitoringProvider.DirectMonitoring = IsDirectMonitoring;
        }
    }

    private void OnLevelUpdateTick(object? sender, EventArgs e)
    {
        if (!_isInitialized) return;

        // Update levels from input monitor
        if (_inputMonitor != null)
        {
            LeftLevel = _inputMonitor.LeftPeak;
            RightLevel = _inputMonitor.RightPeak;
            MonitoringLatencyMs = _inputMonitor.MonitoringLatencyMs;
            IsInputActive = _inputMonitor.IsInputActive;
        }

        // Update peak hold with decay
        var now = DateTime.UtcNow;
        var peakHoldTime = TimeSpan.FromSeconds(1.5);

        // Left peak
        if (LeftLevel > _peakLeftHold)
        {
            _peakLeftHold = LeftLevel;
            _peakLeftTime = now;
            LeftPeak = _peakLeftHold;
        }
        else if (now - _peakLeftTime > peakHoldTime)
        {
            _peakLeftHold = Math.Max(0, _peakLeftHold - 0.02f);
            LeftPeak = _peakLeftHold;
        }

        // Right peak
        if (RightLevel > _peakRightHold)
        {
            _peakRightHold = RightLevel;
            _peakRightTime = now;
            RightPeak = _peakRightHold;
        }
        else if (now - _peakRightTime > peakHoldTime)
        {
            _peakRightHold = Math.Max(0, _peakRightHold - 0.02f);
            RightPeak = _peakRightHold;
        }

        // Clipping detection
        if (LeftLevel >= 1.0f) LeftClipping = true;
        if (RightLevel >= 1.0f) RightClipping = true;
    }

    private void OnInputLevelUpdated(object? sender, LevelMeterEventArgs e)
    {
        // Levels are updated via timer for smoother display
    }

    private void OnMonitoringLevelUpdated(object? sender, LevelMeterEventArgs e)
    {
        // Levels are updated via timer for smoother display
    }

    private void UpdateStatusText()
    {
        if (!_isInitialized)
        {
            StatusText = "Not initialized";
            return;
        }

        if (!HasDevices)
        {
            StatusText = "No input devices";
            return;
        }

        var deviceName = SelectedDevice?.DeviceName ?? "Unknown";
        var state = IsInputActive ? "Active" : "Inactive";
        var monitoring = IsMonitoringEnabled ? ", Monitoring" : "";
        var direct = IsDirectMonitoring ? ", Direct" : "";

        StatusText = $"{deviceName} - {state}{monitoring}{direct}";
    }

    #endregion
}
