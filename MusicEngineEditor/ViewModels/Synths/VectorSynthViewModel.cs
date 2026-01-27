// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Vector Synth Editor.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a point in the vector envelope path.
/// </summary>
public partial class VectorPathPointViewModel : ObservableObject
{
    [ObservableProperty]
    private double _time;

    [ObservableProperty]
    private float _x;

    [ObservableProperty]
    private float _y;

    public VectorPathPointViewModel(double time, float x, float y)
    {
        _time = time;
        _x = x;
        _y = y;
    }
}

/// <summary>
/// ViewModel for an individual oscillator corner in the vector synth.
/// </summary>
public partial class VectorOscillatorViewModel : ObservableObject
{
    private readonly VectorOscillator _oscillator;
    private readonly string _label;

    [ObservableProperty]
    private VectorWaveform _waveform;

    [ObservableProperty]
    private float _detune;

    [ObservableProperty]
    private int _octave;

    [ObservableProperty]
    private float _level;

    /// <summary>
    /// Gets the corner label (A, B, C, D).
    /// </summary>
    public string Label => _label;

    /// <summary>
    /// Gets the available waveforms.
    /// </summary>
    public IReadOnlyList<VectorWaveform> AvailableWaveforms { get; } = Enum.GetValues<VectorWaveform>();

    /// <summary>
    /// Gets the color for this oscillator corner.
    /// </summary>
    public Color CornerColor { get; }

    public VectorOscillatorViewModel(VectorOscillator oscillator, string label, Color cornerColor)
    {
        _oscillator = oscillator ?? throw new ArgumentNullException(nameof(oscillator));
        _label = label;
        CornerColor = cornerColor;

        // Initialize from oscillator
        _waveform = oscillator.Waveform;
        _detune = oscillator.Detune;
        _octave = oscillator.Octave;
        _level = oscillator.Level;
    }

    partial void OnWaveformChanged(VectorWaveform value)
    {
        _oscillator.Waveform = value;
    }

    partial void OnDetuneChanged(float value)
    {
        _oscillator.Detune = Math.Clamp(value, -100f, 100f);
    }

    partial void OnOctaveChanged(int value)
    {
        _oscillator.Octave = Math.Clamp(value, -2, 2);
    }

    partial void OnLevelChanged(float value)
    {
        _oscillator.Level = Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Refreshes the view model from the underlying oscillator.
    /// </summary>
    public void Refresh()
    {
        Waveform = _oscillator.Waveform;
        Detune = _oscillator.Detune;
        Octave = _oscillator.Octave;
        Level = _oscillator.Level;
    }
}

/// <summary>
/// ViewModel for the Vector Synth Editor.
/// </summary>
public partial class VectorSynthViewModel : ViewModelBase, IDisposable
{
    private VectorSynth? _synth;
    private bool _disposed;
    private readonly DispatcherTimer _pathRecordTimer;
    private DateTime _recordStartTime;
    private readonly List<VectorPathPointViewModel> _recordedPath = new();

    #region Observable Properties

    [ObservableProperty]
    private float _vectorX = 0.5f;

    [ObservableProperty]
    private float _vectorY = 0.5f;

    [ObservableProperty]
    private float _volume = 0.5f;

    [ObservableProperty]
    private float _filterCutoff = 1.0f;

    [ObservableProperty]
    private float _filterResonance = 0f;

    [ObservableProperty]
    private double _attack = 0.01;

    [ObservableProperty]
    private double _decay = 0.1;

    [ObservableProperty]
    private double _sustain = 0.7;

    [ObservableProperty]
    private double _release = 0.3;

    [ObservableProperty]
    private bool _vectorEnvelopeEnabled;

    [ObservableProperty]
    private double _vectorEnvelopeTime = 1.0;

    [ObservableProperty]
    private float _envelopeAttackX = 0.5f;

    [ObservableProperty]
    private float _envelopeAttackY = 0.5f;

    [ObservableProperty]
    private float _envelopeSustainX = 0.5f;

    [ObservableProperty]
    private float _envelopeSustainY = 0.5f;

    [ObservableProperty]
    private bool _isRecordingPath;

    [ObservableProperty]
    private VectorOscillatorViewModel? _oscillatorA;

    [ObservableProperty]
    private VectorOscillatorViewModel? _oscillatorB;

    [ObservableProperty]
    private VectorOscillatorViewModel? _oscillatorC;

    [ObservableProperty]
    private VectorOscillatorViewModel? _oscillatorD;

    [ObservableProperty]
    private VectorOscillatorViewModel? _selectedOscillator;

    #endregion

    /// <summary>
    /// Gets the vector envelope path points.
    /// </summary>
    public ObservableCollection<VectorPathPointViewModel> PathPoints { get; } = new();

    /// <summary>
    /// Gets the corner colors for the XY pad gradient.
    /// </summary>
    public static Color CornerAColor => Color.FromRgb(0x00, 0xD9, 0xFF); // Cyan (top-left)
    public static Color CornerBColor => Color.FromRgb(0xFF, 0x6B, 0x6B); // Red (top-right)
    public static Color CornerCColor => Color.FromRgb(0x00, 0xFF, 0x88); // Green (bottom-left)
    public static Color CornerDColor => Color.FromRgb(0xFF, 0xA5, 0x00); // Orange (bottom-right)

    /// <summary>
    /// Event raised when the vector position changes.
    /// </summary>
    public event EventHandler<(float X, float Y)>? VectorPositionChanged;

    /// <summary>
    /// Event raised when path recording completes.
    /// </summary>
    public event EventHandler? PathRecordingCompleted;

    public VectorSynthViewModel()
    {
        _pathRecordTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _pathRecordTimer.Tick += OnPathRecordTick;
    }

    public VectorSynthViewModel(VectorSynth synth) : this()
    {
        SetSynth(synth);
    }

    /// <summary>
    /// Sets the synth instance to edit.
    /// </summary>
    public void SetSynth(VectorSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Creates a new VectorSynth instance.
    /// </summary>
    public void CreateNewSynth(int maxVoices = 16, int? sampleRate = null)
    {
        _synth = new VectorSynth(maxVoices, sampleRate);
        LoadFromSynth();
    }

    /// <summary>
    /// Loads all values from the synth.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        // Load main parameters
        VectorX = _synth.VectorX;
        VectorY = _synth.VectorY;
        Volume = _synth.Volume;
        FilterCutoff = _synth.FilterCutoff;
        FilterResonance = _synth.FilterResonance;

        // Load ADSR
        Attack = _synth.Attack;
        Decay = _synth.Decay;
        Sustain = _synth.Sustain;
        Release = _synth.Release;

        // Load vector envelope
        VectorEnvelopeEnabled = _synth.VectorEnvelope.Enabled;
        VectorEnvelopeTime = _synth.VectorEnvelope.EnvelopeTime;
        var attackPos = _synth.VectorEnvelope.AttackPosition;
        var sustainPos = _synth.VectorEnvelope.SustainPosition;
        EnvelopeAttackX = attackPos.X;
        EnvelopeAttackY = attackPos.Y;
        EnvelopeSustainX = sustainPos.X;
        EnvelopeSustainY = sustainPos.Y;

        // Create oscillator view models
        OscillatorA = new VectorOscillatorViewModel(_synth.OscillatorA, "A", CornerAColor);
        OscillatorB = new VectorOscillatorViewModel(_synth.OscillatorB, "B", CornerBColor);
        OscillatorC = new VectorOscillatorViewModel(_synth.OscillatorC, "C", CornerCColor);
        OscillatorD = new VectorOscillatorViewModel(_synth.OscillatorD, "D", CornerDColor);

        // Load envelope path points
        LoadPathPoints();

        StatusMessage = $"Loaded {_synth.Name}";
    }

    private void LoadPathPoints()
    {
        PathPoints.Clear();

        if (_synth == null) return;

        foreach (var point in _synth.VectorEnvelope.Points)
        {
            PathPoints.Add(new VectorPathPointViewModel(point.Time, point.X, point.Y));
        }
    }

    #region Property Changed Handlers

    partial void OnVectorXChanged(float value)
    {
        if (_synth != null)
        {
            _synth.VectorX = value;
            VectorPositionChanged?.Invoke(this, (value, VectorY));
        }
    }

    partial void OnVectorYChanged(float value)
    {
        if (_synth != null)
        {
            _synth.VectorY = value;
            VectorPositionChanged?.Invoke(this, (VectorX, value));
        }
    }

    partial void OnVolumeChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Volume = Math.Clamp(value, 0f, 1f);
        }
    }

    partial void OnFilterCutoffChanged(float value)
    {
        if (_synth != null)
        {
            _synth.FilterCutoff = Math.Clamp(value, 0f, 1f);
        }
    }

    partial void OnFilterResonanceChanged(float value)
    {
        if (_synth != null)
        {
            _synth.FilterResonance = Math.Clamp(value, 0f, 1f);
        }
    }

    partial void OnAttackChanged(double value)
    {
        if (_synth != null)
        {
            _synth.Attack = Math.Max(0.001, value);
        }
    }

    partial void OnDecayChanged(double value)
    {
        if (_synth != null)
        {
            _synth.Decay = Math.Max(0.001, value);
        }
    }

    partial void OnSustainChanged(double value)
    {
        if (_synth != null)
        {
            _synth.Sustain = Math.Clamp(value, 0, 1);
        }
    }

    partial void OnReleaseChanged(double value)
    {
        if (_synth != null)
        {
            _synth.Release = Math.Max(0.001, value);
        }
    }

    partial void OnVectorEnvelopeEnabledChanged(bool value)
    {
        if (_synth != null)
        {
            _synth.VectorEnvelope.Enabled = value;
        }
    }

    partial void OnVectorEnvelopeTimeChanged(double value)
    {
        if (_synth != null)
        {
            _synth.VectorEnvelope.EnvelopeTime = Math.Max(0.01, value);
        }
    }

    partial void OnEnvelopeAttackXChanged(float value)
    {
        if (_synth != null)
        {
            var pos = _synth.VectorEnvelope.AttackPosition;
            _synth.VectorEnvelope.AttackPosition = (Math.Clamp(value, 0f, 1f), pos.Y);
            LoadPathPoints();
        }
    }

    partial void OnEnvelopeAttackYChanged(float value)
    {
        if (_synth != null)
        {
            var pos = _synth.VectorEnvelope.AttackPosition;
            _synth.VectorEnvelope.AttackPosition = (pos.X, Math.Clamp(value, 0f, 1f));
            LoadPathPoints();
        }
    }

    partial void OnEnvelopeSustainXChanged(float value)
    {
        if (_synth != null)
        {
            var pos = _synth.VectorEnvelope.SustainPosition;
            _synth.VectorEnvelope.SustainPosition = (Math.Clamp(value, 0f, 1f), pos.Y);
            LoadPathPoints();
        }
    }

    partial void OnEnvelopeSustainYChanged(float value)
    {
        if (_synth != null)
        {
            var pos = _synth.VectorEnvelope.SustainPosition;
            _synth.VectorEnvelope.SustainPosition = (pos.X, Math.Clamp(value, 0f, 1f));
            LoadPathPoints();
        }
    }

    #endregion

    #region Commands

    /// <summary>
    /// Sets the vector position from normalized coordinates.
    /// </summary>
    [RelayCommand]
    private void SetVectorPosition(Point normalizedPosition)
    {
        VectorX = (float)Math.Clamp(normalizedPosition.X, 0, 1);
        VectorY = (float)Math.Clamp(normalizedPosition.Y, 0, 1);
    }

    /// <summary>
    /// Centers the vector position.
    /// </summary>
    [RelayCommand]
    private void CenterVector()
    {
        VectorX = 0.5f;
        VectorY = 0.5f;
    }

    /// <summary>
    /// Sets the vector to a corner position.
    /// </summary>
    [RelayCommand]
    private void SetCorner(string corner)
    {
        switch (corner.ToUpperInvariant())
        {
            case "A":
                VectorX = 0f;
                VectorY = 0f;
                break;
            case "B":
                VectorX = 1f;
                VectorY = 0f;
                break;
            case "C":
                VectorX = 0f;
                VectorY = 1f;
                break;
            case "D":
                VectorX = 1f;
                VectorY = 1f;
                break;
        }
    }

    /// <summary>
    /// Starts recording the vector path.
    /// </summary>
    [RelayCommand]
    private void StartRecordingPath()
    {
        if (IsRecordingPath) return;

        _recordedPath.Clear();
        _recordStartTime = DateTime.Now;
        IsRecordingPath = true;
        _pathRecordTimer.Start();

        // Add the starting point
        _recordedPath.Add(new VectorPathPointViewModel(0, VectorX, VectorY));

        StatusMessage = "Recording path... Move the XY position";
    }

    /// <summary>
    /// Stops recording the vector path.
    /// </summary>
    [RelayCommand]
    private void StopRecordingPath()
    {
        if (!IsRecordingPath) return;

        _pathRecordTimer.Stop();
        IsRecordingPath = false;

        if (_synth != null && _recordedPath.Count >= 2)
        {
            // Apply recorded path to envelope
            _synth.VectorEnvelope.Clear();

            // Set attack position from first point
            var firstPoint = _recordedPath[0];
            _synth.VectorEnvelope.AttackPosition = (firstPoint.X, firstPoint.Y);

            // Add intermediate points
            for (int i = 1; i < _recordedPath.Count - 1; i++)
            {
                var point = _recordedPath[i];
                _synth.VectorEnvelope.AddPoint(point.Time, point.X, point.Y);
            }

            // Set sustain position from last point
            var lastPoint = _recordedPath[^1];
            _synth.VectorEnvelope.SustainPosition = (lastPoint.X, lastPoint.Y);
            _synth.VectorEnvelope.EnvelopeTime = lastPoint.Time;

            // Update UI
            EnvelopeAttackX = firstPoint.X;
            EnvelopeAttackY = firstPoint.Y;
            EnvelopeSustainX = lastPoint.X;
            EnvelopeSustainY = lastPoint.Y;
            VectorEnvelopeTime = lastPoint.Time;
            VectorEnvelopeEnabled = true;

            LoadPathPoints();
            StatusMessage = $"Recorded path with {_recordedPath.Count} points over {lastPoint.Time:F2}s";
        }
        else
        {
            StatusMessage = "Recording cancelled - not enough points";
        }

        PathRecordingCompleted?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Clears the vector envelope path.
    /// </summary>
    [RelayCommand]
    private void ClearPath()
    {
        if (_synth == null) return;

        _synth.VectorEnvelope.Clear();
        LoadPathPoints();

        EnvelopeAttackX = 0.5f;
        EnvelopeAttackY = 0.5f;
        EnvelopeSustainX = 0.5f;
        EnvelopeSustainY = 0.5f;
        VectorEnvelopeTime = 1.0;

        StatusMessage = "Path cleared";
    }

    /// <summary>
    /// Adds a point to the path at the current position.
    /// </summary>
    [RelayCommand]
    private void AddPathPoint()
    {
        if (_synth == null) return;

        double time = VectorEnvelopeTime * 0.5; // Add at midpoint by default
        _synth.VectorEnvelope.AddPoint(time, VectorX, VectorY);
        LoadPathPoints();

        StatusMessage = $"Added point at ({VectorX:F2}, {VectorY:F2})";
    }

    /// <summary>
    /// Loads a preset.
    /// </summary>
    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        VectorSynth? newSynth = presetName.ToLowerInvariant() switch
        {
            "pad" => VectorSynth.CreatePadPreset(),
            "lead" => VectorSynth.CreateLeadPreset(),
            "texture" => VectorSynth.CreateTexturePreset(),
            "bass" => VectorSynth.CreateBassPreset(),
            _ => null
        };

        if (newSynth != null)
        {
            _synth = newSynth;
            LoadFromSynth();
            StatusMessage = $"Loaded preset: {presetName}";
        }
    }

    /// <summary>
    /// Triggers a note on.
    /// </summary>
    [RelayCommand]
    private void NoteOn(int note)
    {
        _synth?.NoteOn(note, 100);
    }

    /// <summary>
    /// Triggers a note off.
    /// </summary>
    [RelayCommand]
    private void NoteOff(int note)
    {
        _synth?.NoteOff(note);
    }

    /// <summary>
    /// Stops all notes.
    /// </summary>
    [RelayCommand]
    private void AllNotesOff()
    {
        _synth?.AllNotesOff();
    }

    /// <summary>
    /// Selects an oscillator for detailed editing.
    /// </summary>
    [RelayCommand]
    private void SelectOscillator(string corner)
    {
        SelectedOscillator = corner.ToUpperInvariant() switch
        {
            "A" => OscillatorA,
            "B" => OscillatorB,
            "C" => OscillatorC,
            "D" => OscillatorD,
            _ => null
        };
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    [RelayCommand]
    private void ResetToDefaults()
    {
        if (_synth == null) return;

        _synth = new VectorSynth();
        LoadFromSynth();
        StatusMessage = "Reset to defaults";
    }

    #endregion

    #region Path Recording

    private void OnPathRecordTick(object? sender, EventArgs e)
    {
        if (!IsRecordingPath) return;

        double elapsedTime = (DateTime.Now - _recordStartTime).TotalSeconds;
        _recordedPath.Add(new VectorPathPointViewModel(elapsedTime, VectorX, VectorY));

        // Limit recording to 10 seconds
        if (elapsedTime > 10.0)
        {
            StopRecordingPath();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the underlying VectorSynth instance.
    /// </summary>
    public VectorSynth? GetSynth() => _synth;

    /// <summary>
    /// Calculates the mix gains for each corner at the current position.
    /// </summary>
    public (float A, float B, float C, float D) GetMixGains()
    {
        float gainA = (1f - VectorX) * (1f - VectorY);
        float gainB = VectorX * (1f - VectorY);
        float gainC = (1f - VectorX) * VectorY;
        float gainD = VectorX * VectorY;
        return (gainA, gainB, gainC, gainD);
    }

    /// <summary>
    /// Refreshes all oscillator view models.
    /// </summary>
    public void RefreshOscillators()
    {
        OscillatorA?.Refresh();
        OscillatorB?.Refresh();
        OscillatorC?.Refresh();
        OscillatorD?.Refresh();
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _pathRecordTimer.Stop();
        _synth = null;
    }
}
