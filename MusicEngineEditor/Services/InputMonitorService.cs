// MusicEngineEditor - Input Monitor Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Threading;
using MusicEngine.Core;

namespace MusicEngineEditor.Services;

/// <summary>
/// Event arguments for input level updates.
/// </summary>
public class InputLevelEventArgs : EventArgs
{
    /// <summary>
    /// Gets the left channel level (0.0 to 1.0).
    /// </summary>
    public float LeftLevel { get; }

    /// <summary>
    /// Gets the right channel level (0.0 to 1.0).
    /// </summary>
    public float RightLevel { get; }

    /// <summary>
    /// Gets the left channel peak (0.0 to 1.0).
    /// </summary>
    public float LeftPeak { get; }

    /// <summary>
    /// Gets the right channel peak (0.0 to 1.0).
    /// </summary>
    public float RightPeak { get; }

    /// <summary>
    /// Creates new InputLevelEventArgs.
    /// </summary>
    public InputLevelEventArgs(float leftLevel, float rightLevel, float leftPeak, float rightPeak)
    {
        LeftLevel = leftLevel;
        RightLevel = rightLevel;
        LeftPeak = leftPeak;
        RightPeak = rightPeak;
    }
}

/// <summary>
/// Singleton service for managing audio input monitoring.
/// Provides device enumeration, level metering, and recording buffer access.
/// </summary>
public sealed class InputMonitorService : IDisposable
{
    #region Singleton

    private static readonly Lazy<InputMonitorService> _instance = new(
        () => new InputMonitorService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance of the InputMonitorService.
    /// </summary>
    public static InputMonitorService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private readonly DispatcherTimer _levelUpdateTimer;
    private InputMonitor? _inputMonitor;
    private MonitoringSampleProvider? _monitoringProvider;
    private bool _isInitialized;
    private bool _disposed;

    // Level state
    private float _currentLeftLevel;
    private float _currentRightLevel;
    private float _peakLeftLevel;
    private float _peakRightLevel;
    private DateTime _lastPeakUpdate;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the InputMonitor instance.
    /// </summary>
    public InputMonitor? InputMonitor => _inputMonitor;

    /// <summary>
    /// Gets the MonitoringSampleProvider instance.
    /// </summary>
    public MonitoringSampleProvider? MonitoringProvider => _monitoringProvider;

    /// <summary>
    /// Gets whether the service is initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets whether input capture is currently active.
    /// </summary>
    public bool IsInputActive => _inputMonitor?.IsInputActive ?? false;

    /// <summary>
    /// Gets or sets whether monitoring is enabled.
    /// </summary>
    public bool IsMonitoringEnabled
    {
        get => _inputMonitor?.MonitoringEnabled ?? false;
        set
        {
            if (_inputMonitor != null)
            {
                _inputMonitor.MonitoringEnabled = value;
            }
            if (_monitoringProvider != null)
            {
                _monitoringProvider.IsEnabled = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets the monitoring volume (0.0 to 2.0).
    /// </summary>
    public float MonitoringVolume
    {
        get => _inputMonitor?.MonitoringVolume ?? 1.0f;
        set
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
    }

    /// <summary>
    /// Gets or sets whether direct (low-latency) monitoring is enabled.
    /// </summary>
    public bool IsDirectMonitoring
    {
        get => _monitoringProvider?.DirectMonitoring ?? false;
        set
        {
            if (_monitoringProvider != null)
            {
                _monitoringProvider.DirectMonitoring = value;
            }
        }
    }

    /// <summary>
    /// Gets the current left channel level.
    /// </summary>
    public float LeftLevel => _currentLeftLevel;

    /// <summary>
    /// Gets the current right channel level.
    /// </summary>
    public float RightLevel => _currentRightLevel;

    /// <summary>
    /// Gets the peak left channel level.
    /// </summary>
    public float LeftPeak => _peakLeftLevel;

    /// <summary>
    /// Gets the peak right channel level.
    /// </summary>
    public float RightPeak => _peakRightLevel;

    /// <summary>
    /// Gets the monitoring latency in milliseconds.
    /// </summary>
    public double MonitoringLatencyMs => _inputMonitor?.MonitoringLatencyMs ?? 0;

    /// <summary>
    /// Gets the sample rate.
    /// </summary>
    public int SampleRate => _inputMonitor?.WaveFormat.SampleRate ?? 44100;

    /// <summary>
    /// Gets the number of channels.
    /// </summary>
    public int Channels => _inputMonitor?.WaveFormat.Channels ?? 2;

    /// <summary>
    /// Gets or sets the current input device number.
    /// </summary>
    public int InputDevice
    {
        get => _inputMonitor?.InputDevice ?? -1;
        set
        {
            if (_inputMonitor != null)
            {
                _inputMonitor.InputDevice = value;
            }
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Raised when input levels are updated (~60 times per second).
    /// </summary>
    public event EventHandler<InputLevelEventArgs>? LevelUpdated;

    /// <summary>
    /// Raised when audio is received from the input device.
    /// </summary>
    public event EventHandler<InputAudioEventArgs>? AudioReceived;

    /// <summary>
    /// Raised when the input device changes.
    /// </summary>
    public event EventHandler<int>? DeviceChanged;

    /// <summary>
    /// Raised when monitoring state changes.
    /// </summary>
    public event EventHandler<bool>? MonitoringStateChanged;

    #endregion

    #region Constructor

    private InputMonitorService()
    {
        _levelUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16) // ~60fps
        };
        _levelUpdateTimer.Tick += OnLevelUpdateTick;
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the service with default settings.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    /// <param name="channels">Number of channels (1 or 2).</param>
    public void Initialize(int sampleRate = 44100, int channels = 2)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            _inputMonitor = new InputMonitor(sampleRate, channels, monitorBufferMs: 100, recordingBufferMs: 60000);
            _inputMonitor.LevelUpdated += OnInputLevelUpdated;
            _inputMonitor.AudioReceived += OnInputAudioReceived;

            _monitoringProvider = new MonitoringSampleProvider(sampleRate, channels, bufferMs: 200);
            _monitoringProvider.LevelUpdated += OnMonitoringLevelUpdated;

            _levelUpdateTimer.Start();
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Initializes the service with existing InputMonitor and MonitoringSampleProvider instances.
    /// </summary>
    /// <param name="inputMonitor">The input monitor to use.</param>
    /// <param name="monitoringProvider">Optional monitoring provider.</param>
    public void Initialize(InputMonitor inputMonitor, MonitoringSampleProvider? monitoringProvider = null)
    {
        lock (_lock)
        {
            if (_isInitialized)
                return;

            _inputMonitor = inputMonitor ?? throw new ArgumentNullException(nameof(inputMonitor));
            _inputMonitor.LevelUpdated += OnInputLevelUpdated;
            _inputMonitor.AudioReceived += OnInputAudioReceived;

            _monitoringProvider = monitoringProvider;
            if (_monitoringProvider != null)
            {
                _monitoringProvider.LevelUpdated += OnMonitoringLevelUpdated;
            }

            _levelUpdateTimer.Start();
            _isInitialized = true;
        }
    }

    #endregion

    #region Device Management

    /// <summary>
    /// Gets the number of available input devices.
    /// </summary>
    public static int GetDeviceCount()
    {
        return MusicEngine.Core.InputMonitor.GetInputDeviceCount();
    }

    /// <summary>
    /// Gets the name of an input device.
    /// </summary>
    /// <param name="deviceNumber">Device number (0-based).</param>
    /// <returns>Device name or empty string if not found.</returns>
    public static string GetDeviceName(int deviceNumber)
    {
        return MusicEngine.Core.InputMonitor.GetInputDeviceName(deviceNumber);
    }

    /// <summary>
    /// Gets all available input device names.
    /// </summary>
    /// <returns>List of device names with their indices.</returns>
    public static List<(int Index, string Name)> GetAllDevices()
    {
        var devices = new List<(int, string)>();
        var count = GetDeviceCount();

        for (int i = 0; i < count; i++)
        {
            var name = GetDeviceName(i);
            if (!string.IsNullOrEmpty(name))
            {
                devices.Add((i, name));
            }
        }

        return devices;
    }

    /// <summary>
    /// Selects an input device by index.
    /// </summary>
    /// <param name="deviceIndex">The device index.</param>
    public void SelectDevice(int deviceIndex)
    {
        lock (_lock)
        {
            if (_inputMonitor != null && _inputMonitor.InputDevice != deviceIndex)
            {
                _inputMonitor.InputDevice = deviceIndex;
                DeviceChanged?.Invoke(this, deviceIndex);
            }
        }
    }

    #endregion

    #region Capture Control

    /// <summary>
    /// Starts capturing audio from the input device.
    /// </summary>
    public void StartCapture()
    {
        lock (_lock)
        {
            if (_inputMonitor != null && !_inputMonitor.IsInputActive)
            {
                _inputMonitor.StartCapture();
            }
        }
    }

    /// <summary>
    /// Stops capturing audio from the input device.
    /// </summary>
    public void StopCapture()
    {
        lock (_lock)
        {
            _inputMonitor?.StopCapture();
        }
    }

    /// <summary>
    /// Toggles monitoring on/off.
    /// </summary>
    public void ToggleMonitoring()
    {
        IsMonitoringEnabled = !IsMonitoringEnabled;
        MonitoringStateChanged?.Invoke(this, IsMonitoringEnabled);
    }

    /// <summary>
    /// Resets peak level indicators.
    /// </summary>
    public void ResetPeaks()
    {
        _peakLeftLevel = 0;
        _peakRightLevel = 0;
        _inputMonitor?.ResetLevels();
        _monitoringProvider?.ResetLevels();
    }

    #endregion

    #region Recording Buffer Access

    /// <summary>
    /// Gets recorded samples and clears the recording buffer.
    /// </summary>
    /// <returns>Array of recorded samples.</returns>
    public float[] GetRecordedSamples()
    {
        return _inputMonitor?.GetRecordedSamples() ?? [];
    }

    /// <summary>
    /// Gets recorded samples without clearing the buffer.
    /// </summary>
    /// <returns>Array of recorded samples.</returns>
    public float[] PeekRecordedSamples()
    {
        return _inputMonitor?.PeekRecordedSamples() ?? [];
    }

    /// <summary>
    /// Clears the recording buffer.
    /// </summary>
    public void ClearRecordingBuffer()
    {
        _inputMonitor?.ClearRecordingBuffer();
    }

    /// <summary>
    /// Sets recording state on the input monitor.
    /// </summary>
    /// <param name="isRecording">Whether recording is active.</param>
    public void SetRecording(bool isRecording)
    {
        if (_inputMonitor != null)
        {
            _inputMonitor.IsRecording = isRecording;
        }
    }

    #endregion

    #region Private Methods

    private void OnLevelUpdateTick(object? sender, EventArgs e)
    {
        if (!_isInitialized || _inputMonitor == null)
            return;

        _currentLeftLevel = _inputMonitor.LeftPeak;
        _currentRightLevel = _inputMonitor.RightPeak;

        // Update peaks with hold/decay
        var now = DateTime.UtcNow;
        var peakHoldTime = TimeSpan.FromSeconds(2);

        if (_currentLeftLevel > _peakLeftLevel)
        {
            _peakLeftLevel = _currentLeftLevel;
            _lastPeakUpdate = now;
        }
        else if (now - _lastPeakUpdate > peakHoldTime)
        {
            _peakLeftLevel = Math.Max(0, _peakLeftLevel - 0.02f);
        }

        if (_currentRightLevel > _peakRightLevel)
        {
            _peakRightLevel = _currentRightLevel;
            _lastPeakUpdate = now;
        }
        else if (now - _lastPeakUpdate > peakHoldTime)
        {
            _peakRightLevel = Math.Max(0, _peakRightLevel - 0.02f);
        }

        LevelUpdated?.Invoke(this, new InputLevelEventArgs(
            _currentLeftLevel, _currentRightLevel,
            _peakLeftLevel, _peakRightLevel));
    }

    private void OnInputLevelUpdated(object? sender, LevelMeterEventArgs e)
    {
        // Handled by timer for smoother updates
    }

    private void OnMonitoringLevelUpdated(object? sender, LevelMeterEventArgs e)
    {
        // Handled by timer for smoother updates
    }

    private void OnInputAudioReceived(object? sender, InputAudioEventArgs e)
    {
        // Forward to monitoring provider if enabled
        if (_monitoringProvider != null && IsMonitoringEnabled)
        {
            _monitoringProvider.AddSamples(e.Samples);
        }

        AudioReceived?.Invoke(this, e);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the service and releases all resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            _disposed = true;
            _levelUpdateTimer.Stop();

            if (_inputMonitor != null)
            {
                _inputMonitor.LevelUpdated -= OnInputLevelUpdated;
                _inputMonitor.AudioReceived -= OnInputAudioReceived;
                _inputMonitor.Dispose();
                _inputMonitor = null;
            }

            if (_monitoringProvider != null)
            {
                _monitoringProvider.LevelUpdated -= OnMonitoringLevelUpdated;
                _monitoringProvider.Dispose();
                _monitoringProvider = null;
            }

            _isInitialized = false;
        }
    }

    #endregion
}
