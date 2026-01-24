using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using NAudio.Wave;
using CoreInputDeviceInfo = MusicEngine.Core.InputDeviceInfo;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the recording dialog, managing audio input recording settings and state.
/// </summary>
public partial class RecordingViewModel : ViewModelBase, IDisposable
{
    private readonly AudioInputRecorder _recorder;
    private readonly DispatcherTimer _updateTimer;
    private CancellationTokenSource? _recordingCts;
    private bool _isDisposed;

    // Device Selection
    [ObservableProperty]
    private CoreInputDeviceInfo? _selectedDevice;

    [ObservableProperty]
    private ObservableCollection<CoreInputDeviceInfo> _availableDevices = [];

    // Recording Format
    [ObservableProperty]
    private int _selectedSampleRate = 44100;

    [ObservableProperty]
    private int _selectedBitDepth = 16;

    [ObservableProperty]
    private int _selectedChannels = 2;

    // Recording State
    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private bool _isArmed;

    [ObservableProperty]
    private bool _isMonitoring;

    [ObservableProperty]
    private string _recordingDuration = "00:00:00";

    [ObservableProperty]
    private string _recordingStatus = "Ready";

    // Level Meters
    [ObservableProperty]
    private float _leftLevel;

    [ObservableProperty]
    private float _rightLevel;

    [ObservableProperty]
    private float _leftPeakDb = -96f;

    [ObservableProperty]
    private float _rightPeakDb = -96f;

    [ObservableProperty]
    private bool _isClipping;

    // Monitoring
    [ObservableProperty]
    private float _monitoringVolume = 1.0f;

    [ObservableProperty]
    private float _monitoringPan;

    // Punch-In/Out
    [ObservableProperty]
    private bool _punchEnabled;

    [ObservableProperty]
    private TimeSpan _punchInTime;

    [ObservableProperty]
    private TimeSpan _punchOutTime;

    [ObservableProperty]
    private bool _isPunchedIn;

    [ObservableProperty]
    private string _punchInTimeText = "00:00:00.000";

    [ObservableProperty]
    private string _punchOutTimeText = "00:00:00.000";

    // Output Settings
    [ObservableProperty]
    private string _outputPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    [ObservableProperty]
    private string _outputFileName = "Recording";

    [ObservableProperty]
    private bool _appendTimestamp = true;

    // Collections
    public ObservableCollection<int> SampleRates { get; } = [8000, 11025, 16000, 22050, 44100, 48000, 88200, 96000];
    public ObservableCollection<int> BitDepths { get; } = [8, 16, 24, 32];
    public ObservableCollection<int> ChannelOptions { get; } = [1, 2];

    /// <summary>
    /// Gets whether recording can be started.
    /// </summary>
    public bool CanRecord => SelectedDevice != null && !IsRecording;

    /// <summary>
    /// Gets the full output file path.
    /// </summary>
    public string FullOutputPath
    {
        get
        {
            string fileName = OutputFileName;
            if (AppendTimestamp)
            {
                fileName += $"_{DateTime.Now:yyyyMMdd_HHmmss}";
            }
            return Path.Combine(OutputPath, $"{fileName}.wav");
        }
    }

    /// <summary>
    /// Event raised when the dialog should be closed.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    /// Event raised when recording is complete with the output file path.
    /// </summary>
    public event EventHandler<string>? RecordingCompleted;

    /// <summary>
    /// Creates a new RecordingViewModel.
    /// </summary>
    public RecordingViewModel()
    {
        _recorder = new AudioInputRecorder();
        _recorder.LevelUpdated += OnLevelUpdated;
        _recorder.RecordingStopped += OnRecordingStopped;
        _recorder.PunchedIn += OnPunchedIn;
        _recorder.PunchedOut += OnPunchedOut;

        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _updateTimer.Tick += OnUpdateTimerTick;

        LoadDevices();
    }

    /// <summary>
    /// Loads available input devices.
    /// </summary>
    private void LoadDevices()
    {
        AvailableDevices.Clear();

        foreach (var device in CoreInputDeviceInfo.GetAvailableDevices())
        {
            AvailableDevices.Add(device);
        }

        SelectedDevice = CoreInputDeviceInfo.GetDefaultDevice();

        if (AvailableDevices.Count == 0)
        {
            RecordingStatus = "No input devices found";
        }
    }

    /// <summary>
    /// Refreshes the device list.
    /// </summary>
    [RelayCommand]
    private void RefreshDevices()
    {
        LoadDevices();
        StatusMessage = $"Found {AvailableDevices.Count} input device(s)";
    }

    /// <summary>
    /// Toggles input monitoring on/off.
    /// </summary>
    [RelayCommand]
    private void ToggleMonitoring()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("Please select an input device first.", "No Device",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsMonitoring)
        {
            _recorder.StopMonitoring();
            IsMonitoring = false;
            RecordingStatus = IsArmed ? "Armed" : "Ready";
        }
        else
        {
            try
            {
                _recorder.SetDevice(SelectedDevice);
                _recorder.SetFormat(SelectedSampleRate, SelectedBitDepth, SelectedChannels);
                _recorder.StartMonitoring();
                IsMonitoring = true;
                RecordingStatus = "Monitoring";
                _updateTimer.Start();

                if (_recorder.MonitoringProvider != null)
                {
                    _recorder.MonitoringProvider.Volume = MonitoringVolume;
                    _recorder.MonitoringProvider.Pan = MonitoringPan;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start monitoring: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Arms or disarms the recorder.
    /// </summary>
    [RelayCommand]
    private void ToggleArm()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("Please select an input device first.", "No Device",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (IsArmed)
        {
            _recorder.Disarm();
            IsArmed = false;
            RecordingStatus = IsMonitoring ? "Monitoring" : "Ready";
        }
        else
        {
            _recorder.SetDevice(SelectedDevice);
            _recorder.SetFormat(SelectedSampleRate, SelectedBitDepth, SelectedChannels);
            _recorder.Arm();
            IsArmed = true;
            RecordingStatus = "Armed";
        }
    }

    /// <summary>
    /// Starts or stops recording.
    /// </summary>
    [RelayCommand]
    private void ToggleRecording()
    {
        if (IsRecording)
        {
            StopRecording();
        }
        else
        {
            StartRecording();
        }
    }

    /// <summary>
    /// Starts recording.
    /// </summary>
    private void StartRecording()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("Please select an input device first.", "No Device",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _recorder.SetDevice(SelectedDevice);
            _recorder.SetFormat(SelectedSampleRate, SelectedBitDepth, SelectedChannels);

            if (PunchEnabled)
            {
                _recorder.SetPunchPoints(PunchInTime, PunchOutTime);
            }
            else
            {
                _recorder.ClearPunchPoints();
            }

            string outputFile = FullOutputPath;
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

            _recorder.StartRecording(outputFile);
            IsRecording = true;
            IsArmed = false;
            RecordingStatus = "Recording";
            _updateTimer.Start();

            _recordingCts = new CancellationTokenSource();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to start recording: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Stops recording.
    /// </summary>
    private void StopRecording()
    {
        if (!IsRecording) return;

        try
        {
            _recordingCts?.Cancel();
            _recorder.StopRecording();
            IsRecording = false;
            IsPaused = false;
            RecordingStatus = IsMonitoring ? "Monitoring" : "Ready";

            if (!IsMonitoring)
            {
                _updateTimer.Stop();
            }

            RecordingCompleted?.Invoke(this, FullOutputPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error stopping recording: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Pauses or resumes recording.
    /// </summary>
    [RelayCommand]
    private void TogglePause()
    {
        if (!IsRecording) return;

        if (IsPaused)
        {
            _recorder.ResumeRecording();
            IsPaused = false;
            RecordingStatus = "Recording";
        }
        else
        {
            _recorder.PauseRecording();
            IsPaused = true;
            RecordingStatus = "Paused";
        }
    }

    /// <summary>
    /// Manually triggers punch-in.
    /// </summary>
    [RelayCommand]
    private void ManualPunchIn()
    {
        if (IsRecording)
        {
            _recorder.ManualPunchIn();
        }
    }

    /// <summary>
    /// Manually triggers punch-out.
    /// </summary>
    [RelayCommand]
    private void ManualPunchOut()
    {
        if (IsRecording)
        {
            _recorder.ManualPunchOut();
        }
    }

    /// <summary>
    /// Sets punch-in time from the current text.
    /// </summary>
    [RelayCommand]
    private void SetPunchInTime()
    {
        if (TimeSpan.TryParse(PunchInTimeText, out var time))
        {
            PunchInTime = time;
        }
    }

    /// <summary>
    /// Sets punch-out time from the current text.
    /// </summary>
    [RelayCommand]
    private void SetPunchOutTime()
    {
        if (TimeSpan.TryParse(PunchOutTimeText, out var time))
        {
            PunchOutTime = time;
        }
    }

    /// <summary>
    /// Browses for output directory.
    /// </summary>
    [RelayCommand]
    private void BrowseOutputPath()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select output directory",
            SelectedPath = OutputPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputPath = dialog.SelectedPath;
        }
    }

    /// <summary>
    /// Opens the output folder in explorer.
    /// </summary>
    [RelayCommand]
    private void OpenOutputFolder()
    {
        try
        {
            if (Directory.Exists(OutputPath))
            {
                System.Diagnostics.Process.Start("explorer.exe", OutputPath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Closes the dialog.
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        if (IsRecording)
        {
            var result = MessageBox.Show(
                "Recording is in progress. Stop recording and close?",
                "Recording Active",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            StopRecording();
        }

        if (IsMonitoring)
        {
            _recorder.StopMonitoring();
        }

        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Resets peak indicators.
    /// </summary>
    [RelayCommand]
    private void ResetPeaks()
    {
        _recorder.ResetLevels();
        LeftPeakDb = -96f;
        RightPeakDb = -96f;
        IsClipping = false;
    }

    partial void OnSelectedDeviceChanged(CoreInputDeviceInfo? value)
    {
        OnPropertyChanged(nameof(CanRecord));

        if (value != null)
        {
            SelectedChannels = Math.Min(value.Channels, 2);
        }
    }

    partial void OnMonitoringVolumeChanged(float value)
    {
        if (_recorder.MonitoringProvider != null)
        {
            _recorder.MonitoringProvider.Volume = value;
        }
    }

    partial void OnMonitoringPanChanged(float value)
    {
        if (_recorder.MonitoringProvider != null)
        {
            _recorder.MonitoringProvider.Pan = value;
        }
    }

    partial void OnPunchInTimeChanged(TimeSpan value)
    {
        PunchInTimeText = value.ToString(@"hh\:mm\:ss\.fff");
    }

    partial void OnPunchOutTimeChanged(TimeSpan value)
    {
        PunchOutTimeText = value.ToString(@"hh\:mm\:ss\.fff");
    }

    private void OnLevelUpdated(object? sender, LevelMeterEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LeftLevel = e.LeftPeak;
            RightLevel = e.RightPeak;
            LeftPeakDb = e.LeftPeakDb;
            RightPeakDb = e.RightPeakDb;

            if (e.LeftPeak >= 0.99f || e.RightPeak >= 0.99f)
            {
                IsClipping = true;
            }
        });
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (e.Exception != null)
            {
                MessageBox.Show($"Recording stopped with error: {e.Exception.Message}", "Recording Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            IsRecording = false;
            RecordingStatus = IsMonitoring ? "Monitoring" : "Ready";
        });
    }

    private void OnPunchedIn(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsPunchedIn = true;
            RecordingStatus = "Recording (Punched In)";
        });
    }

    private void OnPunchedOut(object? sender, EventArgs e)
    {
        Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            IsPunchedIn = false;
            RecordingStatus = "Recording (Punched Out)";
        });
    }

    private void OnUpdateTimerTick(object? sender, EventArgs e)
    {
        if (IsRecording)
        {
            RecordingDuration = _recorder.RecordingDuration.ToString(@"hh\:mm\:ss");
        }

        // Decay levels for visual feedback
        LeftLevel *= 0.9f;
        RightLevel *= 0.9f;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _updateTimer.Stop();
        _recordingCts?.Cancel();
        _recordingCts?.Dispose();

        _recorder.LevelUpdated -= OnLevelUpdated;
        _recorder.RecordingStopped -= OnRecordingStopped;
        _recorder.PunchedIn -= OnPunchedIn;
        _recorder.PunchedOut -= OnPunchedOut;
        _recorder.Dispose();
    }
}
