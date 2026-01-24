// MusicEngineEditor - Automation Recording Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using MusicEngine.Core.Automation;
using MusicEngineEditor.Controls;

namespace MusicEngineEditor.Services;

/// <summary>
/// Singleton service for managing automation recording with Touch/Latch/Write modes.
/// </summary>
public sealed class AutomationRecordingService : IDisposable
{
    #region Singleton

    private static readonly Lazy<AutomationRecordingService> _instance =
        new(() => new AutomationRecordingService());

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static AutomationRecordingService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private readonly Dictionary<AutomationLane, RecordingSession> _activeSessions = [];
    private readonly Dictionary<AutomationLane, float> _lastKnownValues = [];

    private bool _isRecording;
    private bool _isPlaying;
    private double _currentTime;
    private AutomationRecordingMode _globalMode = AutomationRecordingMode.Off;

    // Recording settings
    private float _valueThreshold = 0.001f;
    private double _minTimeBetweenPoints = 0.02; // 20ms minimum
    private double _touchReleaseDelay = 0.1; // 100ms delay before returning to curve

    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Gets whether recording is currently active.
    /// </summary>
    public bool IsRecording
    {
        get
        {
            lock (_lock)
            {
                return _isRecording;
            }
        }
    }

    /// <summary>
    /// Gets or sets the global recording mode.
    /// </summary>
    public AutomationRecordingMode GlobalMode
    {
        get => _globalMode;
        set
        {
            if (_globalMode != value)
            {
                _globalMode = value;
                ModeChanged?.Invoke(this, value);
            }
        }
    }

    /// <summary>
    /// Gets or sets the value change threshold for recording.
    /// </summary>
    public float ValueThreshold
    {
        get => _valueThreshold;
        set => _valueThreshold = Math.Max(0f, value);
    }

    /// <summary>
    /// Gets or sets the minimum time between recorded points.
    /// </summary>
    public double MinTimeBetweenPoints
    {
        get => _minTimeBetweenPoints;
        set => _minTimeBetweenPoints = Math.Max(0.001, value);
    }

    /// <summary>
    /// Gets or sets the touch release delay before returning to existing curve.
    /// </summary>
    public double TouchReleaseDelay
    {
        get => _touchReleaseDelay;
        set => _touchReleaseDelay = Math.Max(0, value);
    }

    /// <summary>
    /// Gets the number of active recording sessions.
    /// </summary>
    public int ActiveSessionCount
    {
        get
        {
            lock (_lock)
            {
                return _activeSessions.Count;
            }
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Fired when recording starts.
    /// </summary>
    public event EventHandler? RecordingStarted;

    /// <summary>
    /// Fired when recording stops.
    /// </summary>
    public event EventHandler? RecordingStopped;

    /// <summary>
    /// Fired when a point is recorded.
    /// </summary>
    public event EventHandler<AutomationPointRecordedEventArgs>? PointRecorded;

    /// <summary>
    /// Fired when the global mode changes.
    /// </summary>
    public event EventHandler<AutomationRecordingMode>? ModeChanged;

    /// <summary>
    /// Fired when a touch gesture begins on a lane.
    /// </summary>
    public event EventHandler<AutomationLane>? TouchBegan;

    /// <summary>
    /// Fired when a touch gesture ends on a lane.
    /// </summary>
    public event EventHandler<AutomationLane>? TouchEnded;

    #endregion

    #region Constructor

    private AutomationRecordingService()
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts recording for the specified lane.
    /// </summary>
    /// <param name="lane">The automation lane to record.</param>
    /// <param name="mode">The recording mode.</param>
    public void StartRecording(AutomationLane lane, AutomationRecordingMode mode)
    {
        if (mode == AutomationRecordingMode.Off) return;

        lock (_lock)
        {
            if (!_activeSessions.ContainsKey(lane))
            {
                var session = new RecordingSession
                {
                    Lane = lane,
                    Mode = mode,
                    StartTime = _currentTime,
                    LastRecordedTime = double.NegativeInfinity,
                    LastRecordedValue = float.NaN,
                    IsTouching = mode == AutomationRecordingMode.Write, // Write mode starts immediately
                    OriginalCurve = lane.Curve.Clone()
                };

                _activeSessions[lane] = session;
                _lastKnownValues[lane] = lane.GetCurrentValue();

                if (!_isRecording)
                {
                    _isRecording = true;
                    RecordingStarted?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    /// <summary>
    /// Stops recording for the specified lane.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    public void StopRecording(AutomationLane lane)
    {
        lock (_lock)
        {
            if (_activeSessions.Remove(lane))
            {
                _lastKnownValues.Remove(lane);

                if (_activeSessions.Count == 0)
                {
                    _isRecording = false;
                    RecordingStopped?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    /// <summary>
    /// Stops all recording.
    /// </summary>
    public void StopRecording()
    {
        lock (_lock)
        {
            _activeSessions.Clear();
            _lastKnownValues.Clear();

            if (_isRecording)
            {
                _isRecording = false;
                RecordingStopped?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Notifies that a touch gesture has begun on a control.
    /// </summary>
    /// <param name="lane">The automation lane being touched.</param>
    public void BeginTouch(AutomationLane lane)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(lane, out var session))
            {
                if (session.Mode == AutomationRecordingMode.Touch ||
                    session.Mode == AutomationRecordingMode.Latch)
                {
                    session.IsTouching = true;
                    session.TouchStartTime = _currentTime;

                    // For Latch mode, clear existing automation from this point forward
                    if (session.Mode == AutomationRecordingMode.Latch)
                    {
                        lane.Curve.RemovePointsInRange(_currentTime, double.MaxValue);
                    }
                }
            }
        }

        TouchBegan?.Invoke(this, lane);
    }

    /// <summary>
    /// Notifies that a touch gesture has ended on a control.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    public void EndTouch(AutomationLane lane)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(lane, out var session))
            {
                if (session.Mode == AutomationRecordingMode.Touch)
                {
                    session.IsTouching = false;
                    session.TouchEndTime = _currentTime;
                }
                // Latch mode continues recording after touch ends
            }
        }

        TouchEnded?.Invoke(this, lane);
    }

    /// <summary>
    /// Records a value for the specified lane.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <param name="value">The value to record.</param>
    public void RecordValue(AutomationLane lane, float value)
    {
        if (GlobalMode == AutomationRecordingMode.Off) return;

        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(lane, out var session)) return;

            // Check if we should record based on mode
            bool shouldRecord = session.Mode switch
            {
                AutomationRecordingMode.Touch => session.IsTouching,
                AutomationRecordingMode.Latch => session.IsTouching || session.TouchStartTime > 0,
                AutomationRecordingMode.Write => true,
                _ => false
            };

            if (!shouldRecord) return;

            // Check value threshold
            float lastValue = _lastKnownValues.GetValueOrDefault(lane, float.NaN);
            bool valueChanged = float.IsNaN(lastValue) ||
                               Math.Abs(value - lastValue) >= _valueThreshold;

            // Check time threshold
            bool timeOk = (_currentTime - session.LastRecordedTime) >= _minTimeBetweenPoints;

            if (valueChanged && timeOk)
            {
                // Remove existing points at this time for Write mode
                if (session.Mode == AutomationRecordingMode.Write)
                {
                    lane.Curve.RemovePointAtTime(_currentTime, _minTimeBetweenPoints);
                }

                // Add the point
                var point = lane.AddPoint(_currentTime, value, AutomationCurveType.Linear);

                session.LastRecordedTime = _currentTime;
                session.LastRecordedValue = value;
                session.RecordedPointCount++;
                _lastKnownValues[lane] = value;

                PointRecorded?.Invoke(this, new AutomationPointRecordedEventArgs(lane, point, _currentTime, value));
            }
        }
    }

    /// <summary>
    /// Updates the current playback time.
    /// </summary>
    /// <param name="time">The current time.</param>
    public void UpdateTime(double time)
    {
        lock (_lock)
        {
            _currentTime = time;
        }
    }

    /// <summary>
    /// Sets the playback state.
    /// </summary>
    /// <param name="isPlaying">Whether playback is active.</param>
    public void SetPlaybackState(bool isPlaying)
    {
        lock (_lock)
        {
            bool wasPlaying = _isPlaying;
            _isPlaying = isPlaying;

            // Stop recording when playback stops
            if (wasPlaying && !isPlaying && _isRecording)
            {
                StopRecording();
            }
        }
    }

    /// <summary>
    /// Gets whether a lane is currently being recorded.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <returns>True if recording is active for this lane.</returns>
    public bool IsRecordingLane(AutomationLane lane)
    {
        lock (_lock)
        {
            return _activeSessions.ContainsKey(lane);
        }
    }

    /// <summary>
    /// Gets the recording session info for a lane.
    /// </summary>
    /// <param name="lane">The automation lane.</param>
    /// <returns>Session info, or null if not recording.</returns>
    public RecordingSessionInfo? GetSessionInfo(AutomationLane lane)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(lane, out var session))
            {
                return new RecordingSessionInfo
                {
                    Mode = session.Mode,
                    StartTime = session.StartTime,
                    IsTouching = session.IsTouching,
                    RecordedPointCount = session.RecordedPointCount
                };
            }
            return null;
        }
    }

    /// <summary>
    /// Arms a lane for recording with the global mode.
    /// </summary>
    /// <param name="lane">The automation lane to arm.</param>
    public void ArmLane(AutomationLane lane)
    {
        lane.IsArmed = true;
        if (_isPlaying && _globalMode != AutomationRecordingMode.Off)
        {
            StartRecording(lane, _globalMode);
        }
    }

    /// <summary>
    /// Disarms a lane.
    /// </summary>
    /// <param name="lane">The automation lane to disarm.</param>
    public void DisarmLane(AutomationLane lane)
    {
        lane.IsArmed = false;
        StopRecording(lane);
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopRecording();
    }

    #endregion

    #region Private Classes

    private class RecordingSession
    {
        public AutomationLane Lane { get; set; } = null!;
        public AutomationRecordingMode Mode { get; set; }
        public double StartTime { get; set; }
        public double LastRecordedTime { get; set; }
        public float LastRecordedValue { get; set; }
        public bool IsTouching { get; set; }
        public double TouchStartTime { get; set; }
        public double TouchEndTime { get; set; }
        public int RecordedPointCount { get; set; }
        public AutomationCurve? OriginalCurve { get; set; }
    }

    #endregion
}

/// <summary>
/// Information about an active recording session.
/// </summary>
public class RecordingSessionInfo
{
    /// <summary>
    /// Gets the recording mode.
    /// </summary>
    public AutomationRecordingMode Mode { get; init; }

    /// <summary>
    /// Gets the start time.
    /// </summary>
    public double StartTime { get; init; }

    /// <summary>
    /// Gets whether a touch gesture is active.
    /// </summary>
    public bool IsTouching { get; init; }

    /// <summary>
    /// Gets the number of recorded points.
    /// </summary>
    public int RecordedPointCount { get; init; }
}

/// <summary>
/// Event arguments for point recorded events.
/// </summary>
public class AutomationPointRecordedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the automation lane.
    /// </summary>
    public AutomationLane Lane { get; }

    /// <summary>
    /// Gets the recorded point.
    /// </summary>
    public AutomationPoint Point { get; }

    /// <summary>
    /// Gets the time at which the point was recorded.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Gets the recorded value.
    /// </summary>
    public float Value { get; }

    /// <summary>
    /// Creates a new AutomationPointRecordedEventArgs.
    /// </summary>
    public AutomationPointRecordedEventArgs(AutomationLane lane, AutomationPoint point, double time, float value)
    {
        Lane = lane;
        Point = point;
        Time = time;
        Value = value;
    }
}
