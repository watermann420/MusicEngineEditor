// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for managing a reference track for A/B comparison during mixing.
/// </summary>
public partial class ReferenceTrackControl : UserControl
{
    private ReferenceTrack? _referenceTrack;
    private readonly DispatcherTimer _positionTimer;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isDraggingLoop;
    private bool _isDraggingLoopStart;
    private bool _isDraggingLoopEnd;
#pragma warning restore CS0414
    private float[]? _waveformPeaks;
    private bool _isAnalyzing;

    /// <summary>
    /// Event raised when the A/B state changes.
    /// </summary>
    public event EventHandler<ReferenceActiveEventArgs>? ActiveStateChanged;

    /// <summary>
    /// Event raised when a reference file is loaded.
    /// </summary>
    public event EventHandler<ReferenceFileEventArgs>? FileLoaded;

    /// <summary>
    /// Event raised when the reference file is unloaded.
    /// </summary>
    public event EventHandler? FileUnloaded;

    /// <summary>
    /// Gets or sets the reference track instance.
    /// </summary>
    public ReferenceTrack? ReferenceTrack
    {
        get => _referenceTrack;
        set
        {
            if (_referenceTrack != null)
            {
                _referenceTrack.FileLoaded -= OnReferenceFileLoaded;
                _referenceTrack.FileUnloaded -= OnReferenceFileUnloaded;
                _referenceTrack.LoudnessAnalyzed -= OnLoudnessAnalyzed;
                _referenceTrack.ActiveStateChanged -= OnActiveStateChanged;
            }

            _referenceTrack = value;

            if (_referenceTrack != null)
            {
                _referenceTrack.FileLoaded += OnReferenceFileLoaded;
                _referenceTrack.FileUnloaded += OnReferenceFileUnloaded;
                _referenceTrack.LoudnessAnalyzed += OnLoudnessAnalyzed;
                _referenceTrack.ActiveStateChanged += OnActiveStateChanged;

                // Sync UI with current state
                UpdateUIFromReferenceTrack();
            }
        }
    }

    /// <summary>
    /// Gets or sets the target LUFS for level matching.
    /// </summary>
    public float TargetLufs
    {
        get => _referenceTrack?.TargetLufs ?? -14.0f;
        set
        {
            if (_referenceTrack != null)
            {
                _referenceTrack.TargetLufs = value;
                TargetLufsText.Text = $"{value:F0} LUFS";
            }
        }
    }

    public ReferenceTrackControl()
    {
        InitializeComponent();

        // Set up position update timer
        _positionTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _positionTimer.Tick += OnPositionTimerTick;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start position timer
        _positionTimer.Start();

        // Focus for keyboard shortcuts
        Focus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _positionTimer.Stop();
    }

    private void OnPositionTimerTick(object? sender, EventArgs e)
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded)
            return;

        UpdatePositionDisplay();
        UpdatePlayhead();
    }

    private void UpdatePositionDisplay()
    {
        if (_referenceTrack == null)
            return;

        var position = TimeSpan.FromSeconds(_referenceTrack.Position);
        var duration = _referenceTrack.Duration;

        PositionText.Text = FormatTime(position);
        TotalTimeText.Text = FormatTime(duration);
    }

    private void UpdatePlayhead()
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded)
        {
            PlayheadRect.Visibility = Visibility.Collapsed;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var positionRatio = _referenceTrack.Position / _referenceTrack.Duration.TotalSeconds;
        var x = positionRatio * width;

        Canvas.SetLeft(PlayheadRect, x - 1);
        Canvas.SetTop(PlayheadRect, 0);
        PlayheadRect.Height = height;
        PlayheadRect.Visibility = Visibility.Visible;
    }

    private void UpdateUIFromReferenceTrack()
    {
        if (_referenceTrack == null)
            return;

        Dispatcher.Invoke(() =>
        {
            if (_referenceTrack.IsLoaded)
            {
                var fileName = Path.GetFileName(_referenceTrack.FilePath ?? "Unknown");
                FileNameText.Text = fileName;
                DurationText.Text = FormatTime(_referenceTrack.Duration);
                DurationText.Visibility = Visibility.Visible;
                NoFileText.Visibility = Visibility.Collapsed;

                // Enable controls
                ABToggle.IsEnabled = true;
                VolumeSlider.IsEnabled = true;
                AutoLevelToggle.IsEnabled = true;
                SyncToggle.IsEnabled = true;
                LoopToggle.IsEnabled = true;
                UnloadButton.IsEnabled = true;

                // Update A/B state
                ABToggle.IsChecked = _referenceTrack.IsActive;

                // Update volume
                var volumeDb = 20.0 * Math.Log10(Math.Max(0.001, _referenceTrack.Volume));
                VolumeSlider.Value = volumeDb;
                VolumeDbText.Text = $"{volumeDb:F1} dB";

                // Update toggles
                AutoLevelToggle.IsChecked = _referenceTrack.AutoLevelMatch;
                SyncToggle.IsChecked = _referenceTrack.SyncWithTransport;
                LoopToggle.IsChecked = _referenceTrack.LoopEnabled;

                // Update LUFS display
                UpdateLufsDisplay();

                // Update loop region
                UpdateLoopRegion();

                // Generate waveform
                GenerateWaveform();
            }
            else
            {
                FileNameText.Text = "No reference loaded";
                DurationText.Visibility = Visibility.Collapsed;
                NoFileText.Visibility = Visibility.Visible;

                // Disable controls
                ABToggle.IsEnabled = false;
                ABToggle.IsChecked = false;
                VolumeSlider.IsEnabled = false;
                AutoLevelToggle.IsEnabled = false;
                SyncToggle.IsEnabled = false;
                LoopToggle.IsEnabled = false;
                UnloadButton.IsEnabled = false;

                // Clear waveform
                WaveformPath.Data = null;
                _waveformPeaks = null;

                // Reset LUFS
                MeasuredLufsText.Text = "-- LUFS";
            }
        });
    }

    private void UpdateLufsDisplay()
    {
        if (_referenceTrack == null)
            return;

        if (float.IsNegativeInfinity(_referenceTrack.MeasuredLufs))
        {
            MeasuredLufsText.Text = "-- LUFS";
        }
        else
        {
            MeasuredLufsText.Text = $"{_referenceTrack.MeasuredLufs:F1} LUFS";
        }

        TargetLufsText.Text = $"{_referenceTrack.TargetLufs:F0} LUFS";
    }

    private void OnReferenceFileLoaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateUIFromReferenceTrack();
            FileLoaded?.Invoke(this, new ReferenceFileEventArgs(_referenceTrack?.FilePath ?? string.Empty));

            // Start loudness analysis
            AnalyzeLoudnessAsync();
        });
    }

    private void OnReferenceFileUnloaded(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateUIFromReferenceTrack();
            FileUnloaded?.Invoke(this, EventArgs.Empty);
        });
    }

    private void OnLoudnessAnalyzed(object? sender, LoudnessAnalysisEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isAnalyzing = false;
            UpdateLufsDisplay();
        });
    }

    private void OnActiveStateChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            ABToggle.IsChecked = _referenceTrack?.IsActive ?? false;
            ActiveStateChanged?.Invoke(this, new ReferenceActiveEventArgs(_referenceTrack?.IsActive ?? false));
        });
    }

    private async void AnalyzeLoudnessAsync()
    {
        if (_referenceTrack == null || _isAnalyzing)
            return;

        _isAnalyzing = true;
        MeasuredLufsText.Text = "Analyzing...";

        try
        {
            await _referenceTrack.AnalyzeLoudnessAsync();
        }
        catch (Exception)
        {
            MeasuredLufsText.Text = "Error";
            _isAnalyzing = false;
        }
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.wav;*.mp3;*.flac;*.ogg;*.aiff;*.aif|" +
                     "WAV Files|*.wav|" +
                     "MP3 Files|*.mp3|" +
                     "FLAC Files|*.flac|" +
                     "OGG Files|*.ogg|" +
                     "AIFF Files|*.aiff;*.aif|" +
                     "All Files|*.*",
            Title = "Load Reference Track"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadReferenceFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Loads a reference audio file.
    /// </summary>
    /// <param name="filePath">The path to the audio file.</param>
    /// <returns>True if the file was loaded successfully.</returns>
    public bool LoadReferenceFile(string filePath)
    {
        if (_referenceTrack == null)
        {
            _referenceTrack = new ReferenceTrack();
            ReferenceTrack = _referenceTrack; // Wire up events
        }

        return _referenceTrack.LoadFile(filePath);
    }

    private void UnloadButton_Click(object sender, RoutedEventArgs e)
    {
        _referenceTrack?.Unload();
    }

    private void ABToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_referenceTrack != null)
        {
            _referenceTrack.IsActive = true;
            ActiveStateChanged?.Invoke(this, new ReferenceActiveEventArgs(true));
        }
    }

    private void ABToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_referenceTrack != null)
        {
            _referenceTrack.IsActive = false;
            ActiveStateChanged?.Invoke(this, new ReferenceActiveEventArgs(false));
        }
    }

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_referenceTrack == null)
            return;

        var volumeDb = e.NewValue;
        var volumeLinear = (float)Math.Pow(10.0, volumeDb / 20.0);
        _referenceTrack.Volume = volumeLinear;
        VolumeDbText.Text = $"{volumeDb:F1} dB";
    }

    private void AutoLevelToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_referenceTrack != null)
        {
            _referenceTrack.AutoLevelMatch = AutoLevelToggle.IsChecked ?? false;
        }
    }

    private void SyncToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_referenceTrack != null)
        {
            _referenceTrack.SyncWithTransport = SyncToggle.IsChecked ?? false;
        }
    }

    private void LoopToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_referenceTrack != null)
        {
            _referenceTrack.LoopEnabled = LoopToggle.IsChecked ?? false;
            UpdateLoopRegion();
        }
    }

    private void UpdateLoopRegion()
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded || !_referenceTrack.LoopEnabled)
        {
            LoopRegionRect.Visibility = Visibility.Collapsed;
            LoopInfoText.Visibility = Visibility.Collapsed;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var duration = _referenceTrack.Duration.TotalSeconds;
        var startX = (_referenceTrack.LoopStart / duration) * width;
        var endX = (_referenceTrack.LoopEnd / duration) * width;

        Canvas.SetLeft(LoopRegionRect, startX);
        Canvas.SetTop(LoopRegionRect, 0);
        LoopRegionRect.Width = Math.Max(2, endX - startX);
        LoopRegionRect.Height = height;
        LoopRegionRect.Visibility = Visibility.Visible;

        // Update loop info text
        LoopInfoText.Text = $"Loop: {FormatTime(TimeSpan.FromSeconds(_referenceTrack.LoopStart))} - {FormatTime(TimeSpan.FromSeconds(_referenceTrack.LoopEnd))}";
        LoopInfoText.Visibility = Visibility.Visible;
    }

    private void WaveformCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded)
            return;

        var position = e.GetPosition(WaveformCanvas);
        var width = WaveformCanvas.ActualWidth;

        // Calculate position in seconds
        var positionRatio = position.X / width;
        var positionSeconds = positionRatio * _referenceTrack.Duration.TotalSeconds;

        // Seek to position
        _referenceTrack.Seek(positionSeconds);

        WaveformCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void WaveformCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingLoop = false;
        _isDraggingLoopStart = false;
        _isDraggingLoopEnd = false;
        WaveformCanvas.ReleaseMouseCapture();
    }

    private void WaveformCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded)
            return;

        if (e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(WaveformCanvas);
        var width = WaveformCanvas.ActualWidth;

        // Calculate position in seconds
        var positionRatio = Math.Max(0, Math.Min(1, position.X / width));
        var positionSeconds = positionRatio * _referenceTrack.Duration.TotalSeconds;

        // Seek to position
        _referenceTrack.Seek(positionSeconds);
    }

    private void WaveformCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Update center line
        CenterLine.X1 = 0;
        CenterLine.X2 = e.NewSize.Width;
        CenterLine.Y1 = e.NewSize.Height / 2;
        CenterLine.Y2 = e.NewSize.Height / 2;

        // Redraw waveform
        if (_waveformPeaks != null)
        {
            RenderWaveform();
        }

        // Update loop region
        UpdateLoopRegion();
    }

    private void GenerateWaveform()
    {
        if (_referenceTrack == null || !_referenceTrack.IsLoaded || string.IsNullOrEmpty(_referenceTrack.FilePath))
        {
            _waveformPeaks = null;
            WaveformPath.Data = null;
            return;
        }

        // Generate waveform peaks in background
        System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                using var reader = new NAudio.Wave.AudioFileReader(_referenceTrack.FilePath);
                var totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
                var channels = reader.WaveFormat.Channels;
                var samplesPerChannel = totalSamples / channels;

                // Target around 1000 peaks for the display
                var peakCount = Math.Min(samplesPerChannel, 2000);
                var samplesPerPeak = samplesPerChannel / peakCount;

                var peaks = new float[peakCount * 2]; // min and max for each peak
                var buffer = new float[samplesPerPeak * channels];

                for (int i = 0; i < peakCount; i++)
                {
                    int samplesRead = reader.Read(buffer, 0, buffer.Length);
                    if (samplesRead == 0)
                        break;

                    float min = 0, max = 0;
                    for (int j = 0; j < samplesRead; j++)
                    {
                        var sample = buffer[j];
                        if (sample < min) min = sample;
                        if (sample > max) max = sample;
                    }

                    peaks[i * 2] = min;
                    peaks[i * 2 + 1] = max;
                }

                _waveformPeaks = peaks;

                Dispatcher.Invoke(RenderWaveform);
            }
            catch (Exception)
            {
                // Ignore waveform generation errors
            }
        });
    }

    private void RenderWaveform()
    {
        if (_waveformPeaks == null || _waveformPeaks.Length == 0)
        {
            WaveformPath.Data = null;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        var peakCount = _waveformPeaks.Length / 2;
        var geometry = new StreamGeometry();

        using (var context = geometry.Open())
        {
            var topPoints = new Point[peakCount];
            var bottomPoints = new Point[peakCount];

            for (int i = 0; i < peakCount; i++)
            {
                var x = (double)i / peakCount * width;
                var min = _waveformPeaks[i * 2];
                var max = _waveformPeaks[i * 2 + 1];

                topPoints[i] = new Point(x, centerY - max * halfHeight);
                bottomPoints[i] = new Point(x, centerY - min * halfHeight);
            }

            // Start at first top point
            context.BeginFigure(topPoints[0], true, true);

            // Draw top line
            for (int i = 1; i < topPoints.Length; i++)
            {
                context.LineTo(topPoints[i], true, false);
            }

            // Draw bottom line (reversed)
            for (int i = bottomPoints.Length - 1; i >= 0; i--)
            {
                context.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();
        WaveformPath.Data = geometry;
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.R && _referenceTrack != null && _referenceTrack.IsLoaded)
        {
            // Toggle A/B
            _referenceTrack.Toggle();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Synchronizes the reference track position with the transport.
    /// </summary>
    /// <param name="transportPositionSeconds">The transport position in seconds.</param>
    public void SyncWithTransport(double transportPositionSeconds)
    {
        _referenceTrack?.SyncPosition(transportPositionSeconds);
    }

    /// <summary>
    /// Sets the loop region.
    /// </summary>
    /// <param name="startSeconds">Loop start in seconds.</param>
    /// <param name="endSeconds">Loop end in seconds.</param>
    public void SetLoopRegion(double startSeconds, double endSeconds)
    {
        _referenceTrack?.SetLoopRegion(startSeconds, endSeconds);
        UpdateLoopRegion();
    }

    private static string FormatTime(TimeSpan time)
    {
        if (time.TotalHours >= 1)
        {
            return $"{(int)time.TotalHours}:{time.Minutes:D2}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
        }
        return $"{(int)time.TotalMinutes}:{time.Seconds:D2}.{time.Milliseconds / 10:D2}";
    }
}

/// <summary>
/// Event arguments for reference track active state changes.
/// </summary>
public class ReferenceActiveEventArgs : EventArgs
{
    /// <summary>
    /// Gets whether the reference track is active (playing instead of mix).
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Creates new reference active event arguments.
    /// </summary>
    /// <param name="isActive">Whether the reference is active.</param>
    public ReferenceActiveEventArgs(bool isActive)
    {
        IsActive = isActive;
    }
}

/// <summary>
/// Event arguments for reference file loading.
/// </summary>
public class ReferenceFileEventArgs : EventArgs
{
    /// <summary>
    /// Gets the file path of the loaded reference.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Creates new reference file event arguments.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public ReferenceFileEventArgs(string filePath)
    {
        FilePath = filePath;
    }
}
