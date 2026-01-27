// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Granular Synth Editor.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a single grain visualization in the scatter display.
/// </summary>
public partial class GrainVisualization : ObservableObject
{
    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _size;

    [ObservableProperty]
    private double _opacity;

    [ObservableProperty]
    private bool _isReverse;

    /// <summary>
    /// Time when this grain was spawned (for animation/fading).
    /// </summary>
    public DateTime SpawnTime { get; set; } = DateTime.Now;

    /// <summary>
    /// Grain lifetime in milliseconds.
    /// </summary>
    public double LifetimeMs { get; set; }
}

/// <summary>
/// ViewModel for the Granular Synth Editor control.
/// </summary>
public partial class GranularSynthViewModel : ViewModelBase, IDisposable
{
    private GranularSynth? _synth;
    private bool _disposed;
    private float[]? _sourceWaveform;
    private System.Timers.Timer? _grainVisualizationTimer;
    private readonly Random _random = new();

    #region Observable Properties

    [ObservableProperty]
    private string _synthName = "GranularSynth";

    [ObservableProperty]
    private string? _loadedFileName;

    [ObservableProperty]
    private bool _hasLoadedSample;

    [ObservableProperty]
    private float _volume = 0.5f;

    [ObservableProperty]
    private float _position;

    [ObservableProperty]
    private float _positionRandom = 0.05f;

    [ObservableProperty]
    private float _grainSizeMs = 50f;

    [ObservableProperty]
    private float _grainSizeRandom = 0.2f;

    [ObservableProperty]
    private float _density = 30f;

    [ObservableProperty]
    private float _densityRandom = 0.1f;

    [ObservableProperty]
    private float _pitchShift;

    [ObservableProperty]
    private float _pitchRandom;

    [ObservableProperty]
    private float _panSpread = 0.5f;

    [ObservableProperty]
    private GrainEnvelope _selectedEnvelope = GrainEnvelope.Gaussian;

    [ObservableProperty]
    private GranularPlayMode _selectedPlayMode = GranularPlayMode.Forward;

    [ObservableProperty]
    private float _reverseProbability = 0.3f;

    [ObservableProperty]
    private bool _pitchTracking = true;

    [ObservableProperty]
    private int _maxGrains = 64;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _activeGrainCount;

    [ObservableProperty]
    private double _waveformWidth = 400;

    [ObservableProperty]
    private double _waveformHeight = 100;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available envelope shapes.
    /// </summary>
    public ObservableCollection<GrainEnvelope> AvailableEnvelopes { get; } = new(Enum.GetValues<GrainEnvelope>());

    /// <summary>
    /// Gets the available play modes.
    /// </summary>
    public ObservableCollection<GranularPlayMode> AvailablePlayModes { get; } = new(Enum.GetValues<GranularPlayMode>());

    /// <summary>
    /// Gets the grain visualization collection for the scatter display.
    /// </summary>
    public ObservableCollection<GrainVisualization> GrainVisualizations { get; } = new();

    /// <summary>
    /// Gets the waveform sample data for display.
    /// </summary>
    public float[]? SourceWaveform => _sourceWaveform;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when waveform data changes.
    /// </summary>
    public event EventHandler? WaveformChanged;

    /// <summary>
    /// Event raised when a parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    #endregion

    public GranularSynthViewModel()
    {
        // Design-time constructor
    }

    public GranularSynthViewModel(GranularSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
        StartVisualizationTimer();
    }

    /// <summary>
    /// Initializes with a new GranularSynth instance.
    /// </summary>
    public void Initialize(int sampleRate = 44100)
    {
        _synth = new GranularSynth(sampleRate);
        LoadFromSynth();
        StartVisualizationTimer();
        StatusMessage = "Granular synth initialized";
    }

    /// <summary>
    /// Loads settings from the synth instance.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        SynthName = _synth.Name;
        Volume = _synth.Volume;
        Position = _synth.Position;
        PositionRandom = _synth.PositionRandom;
        GrainSizeMs = _synth.GrainSize;
        GrainSizeRandom = _synth.GrainSizeRandom;
        Density = _synth.Density;
        DensityRandom = _synth.DensityRandom;
        PitchShift = _synth.PitchShift;
        PitchRandom = _synth.PitchRandom;
        PanSpread = _synth.PanSpread;
        SelectedEnvelope = _synth.Envelope;
        SelectedPlayMode = _synth.PlayMode;
        ReverseProbability = _synth.ReverseProbability;
        PitchTracking = _synth.PitchTracking;
        MaxGrains = _synth.MaxGrains;
    }

    /// <summary>
    /// Starts the grain visualization timer.
    /// </summary>
    private void StartVisualizationTimer()
    {
        _grainVisualizationTimer = new System.Timers.Timer(50); // 20 FPS
        _grainVisualizationTimer.Elapsed += (s, e) => UpdateGrainVisualization();
        _grainVisualizationTimer.AutoReset = true;
        _grainVisualizationTimer.Start();
    }

    /// <summary>
    /// Updates the grain visualization (called from timer).
    /// </summary>
    private void UpdateGrainVisualization()
    {
        if (!IsPlaying) return;

        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Remove expired grains
                var now = DateTime.Now;
                for (int i = GrainVisualizations.Count - 1; i >= 0; i--)
                {
                    var grain = GrainVisualizations[i];
                    var age = (now - grain.SpawnTime).TotalMilliseconds;
                    if (age > grain.LifetimeMs)
                    {
                        GrainVisualizations.RemoveAt(i);
                    }
                    else
                    {
                        // Update opacity based on age
                        var progress = age / grain.LifetimeMs;
                        grain.Opacity = GetEnvelopeValue(progress);
                    }
                }

                // Spawn new grains based on density
                if (GrainVisualizations.Count < MaxGrains)
                {
                    double grainsPerSecond = Density * (1 + (_random.NextDouble() * 2 - 1) * DensityRandom);
                    double grainsPerFrame = grainsPerSecond * 0.05; // 50ms frame

                    if (_random.NextDouble() < grainsPerFrame)
                    {
                        SpawnVisualizationGrain();
                    }
                }

                ActiveGrainCount = GrainVisualizations.Count;
            });
        }
        catch
        {
            // Ignore dispatcher exceptions during shutdown
        }
    }

    /// <summary>
    /// Spawns a new grain visualization.
    /// </summary>
    private void SpawnVisualizationGrain()
    {
        if (WaveformWidth <= 0 || WaveformHeight <= 0) return;

        // Calculate position with randomization
        float effectivePosition = Position + (float)(_random.NextDouble() * 2 - 1) * PositionRandom;
        effectivePosition = Math.Clamp(effectivePosition, 0f, 1f);

        // Calculate size with randomization
        float sizeRand = 1f + (float)(_random.NextDouble() * 2 - 1) * GrainSizeRandom;
        float grainSize = GrainSizeMs * sizeRand;

        // Calculate Y position based on pan spread
        float pan = (float)(_random.NextDouble() * 2 - 1) * PanSpread;
        float yNormalized = (pan + 1f) / 2f;

        // Determine if reversed
        bool isReverse = SelectedPlayMode switch
        {
            GranularPlayMode.Reverse => true,
            GranularPlayMode.Random => _random.NextDouble() < ReverseProbability,
            GranularPlayMode.PingPong => GrainVisualizations.Count % 2 == 1,
            _ => false
        };

        var grain = new GrainVisualization
        {
            X = effectivePosition * WaveformWidth,
            Y = yNormalized * WaveformHeight,
            Size = Math.Clamp(grainSize / 5, 4, 30), // Scale size for visualization
            Opacity = 1.0,
            IsReverse = isReverse,
            SpawnTime = DateTime.Now,
            LifetimeMs = grainSize
        };

        GrainVisualizations.Add(grain);
    }

    /// <summary>
    /// Gets the envelope value at the given progress (0-1).
    /// </summary>
    private double GetEnvelopeValue(double progress)
    {
        return SelectedEnvelope switch
        {
            GrainEnvelope.Gaussian => Math.Exp(-18 * Math.Pow(progress - 0.5, 2)),
            GrainEnvelope.Hann => 0.5 * (1 - Math.Cos(2 * Math.PI * progress)),
            GrainEnvelope.Trapezoid => progress < 0.1 ? progress * 10 :
                                       progress > 0.9 ? (1 - progress) * 10 : 1,
            GrainEnvelope.Triangle => progress < 0.5 ? progress * 2 : (1 - progress) * 2,
            GrainEnvelope.Rectangle => 1,
            _ => 1
        };
    }

    #region Property Changed Handlers

    partial void OnVolumeChanged(float value)
    {
        _synth?.SetParameter("volume", value);
        ParameterChanged?.Invoke(this, nameof(Volume));
    }

    partial void OnPositionChanged(float value)
    {
        _synth?.SetParameter("position", value);
        ParameterChanged?.Invoke(this, nameof(Position));
    }

    partial void OnPositionRandomChanged(float value)
    {
        _synth?.SetParameter("positionrandom", value);
        ParameterChanged?.Invoke(this, nameof(PositionRandom));
    }

    partial void OnGrainSizeMsChanged(float value)
    {
        _synth?.SetParameter("grainsize", value);
        ParameterChanged?.Invoke(this, nameof(GrainSizeMs));
    }

    partial void OnGrainSizeRandomChanged(float value)
    {
        _synth?.SetParameter("grainsizerand", value);
        ParameterChanged?.Invoke(this, nameof(GrainSizeRandom));
    }

    partial void OnDensityChanged(float value)
    {
        _synth?.SetParameter("density", value);
        ParameterChanged?.Invoke(this, nameof(Density));
    }

    partial void OnDensityRandomChanged(float value)
    {
        _synth?.SetParameter("densityrandom", value);
        ParameterChanged?.Invoke(this, nameof(DensityRandom));
    }

    partial void OnPitchShiftChanged(float value)
    {
        _synth?.SetParameter("pitchshift", value);
        ParameterChanged?.Invoke(this, nameof(PitchShift));
    }

    partial void OnPitchRandomChanged(float value)
    {
        _synth?.SetParameter("pitchrandom", value);
        ParameterChanged?.Invoke(this, nameof(PitchRandom));
    }

    partial void OnPanSpreadChanged(float value)
    {
        _synth?.SetParameter("panspread", value);
        ParameterChanged?.Invoke(this, nameof(PanSpread));
    }

    partial void OnSelectedEnvelopeChanged(GrainEnvelope value)
    {
        if (_synth != null)
        {
            _synth.Envelope = value;
        }
        ParameterChanged?.Invoke(this, nameof(SelectedEnvelope));
    }

    partial void OnSelectedPlayModeChanged(GranularPlayMode value)
    {
        if (_synth != null)
        {
            _synth.PlayMode = value;
        }
        ParameterChanged?.Invoke(this, nameof(SelectedPlayMode));
    }

    partial void OnReverseProbabilityChanged(float value)
    {
        _synth?.SetParameter("reverseprobability", value);
        ParameterChanged?.Invoke(this, nameof(ReverseProbability));
    }

    partial void OnPitchTrackingChanged(bool value)
    {
        if (_synth != null)
        {
            _synth.PitchTracking = value;
        }
        ParameterChanged?.Invoke(this, nameof(PitchTracking));
    }

    partial void OnMaxGrainsChanged(int value)
    {
        if (_synth != null)
        {
            _synth.MaxGrains = value;
        }
        ParameterChanged?.Invoke(this, nameof(MaxGrains));
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void LoadSample()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load Sample",
            Filter = "Audio Files|*.wav;*.mp3;*.aiff;*.flac;*.ogg|WAV Files|*.wav|MP3 Files|*.mp3|All Files|*.*",
            DefaultExt = ".wav"
        };

        if (dialog.ShowDialog() == true)
        {
            LoadSampleFromFile(dialog.FileName);
        }
    }

    /// <summary>
    /// Loads a sample from the specified file path.
    /// </summary>
    public void LoadSampleFromFile(string filePath)
    {
        try
        {
            IsBusy = true;
            StatusMessage = $"Loading {Path.GetFileName(filePath)}...";

            _synth?.LoadSample(filePath);
            LoadedFileName = Path.GetFileName(filePath);
            HasLoadedSample = true;

            // Generate waveform data for display
            GenerateWaveformData(filePath);

            StatusMessage = $"Loaded: {LoadedFileName}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading sample: {ex.Message}";
            HasLoadedSample = false;
            LoadedFileName = null;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Generates waveform data for display from the loaded file.
    /// </summary>
    private void GenerateWaveformData(string filePath)
    {
        try
        {
            using var reader = new NAudio.Wave.AudioFileReader(filePath);
            var samples = new System.Collections.Generic.List<float>();
            var buffer = new float[4096];
            int read;

            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    samples.Add(buffer[i]);
                }
            }

            // Convert to mono if stereo
            if (reader.WaveFormat.Channels == 2)
            {
                var monoSamples = new System.Collections.Generic.List<float>();
                for (int i = 0; i < samples.Count - 1; i += 2)
                {
                    monoSamples.Add((samples[i] + samples[i + 1]) * 0.5f);
                }
                _sourceWaveform = monoSamples.ToArray();
            }
            else
            {
                _sourceWaveform = samples.ToArray();
            }

            WaveformChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            _sourceWaveform = null;
        }
    }

    [RelayCommand]
    private void GenerateWaveform(string waveType)
    {
        if (_synth == null)
        {
            Initialize();
        }

        try
        {
            var type = Enum.Parse<WaveType>(waveType, true);
            _synth!.GenerateSource(type, 440f, 2f); // 2 seconds at 440Hz

            LoadedFileName = $"Generated: {waveType}";
            HasLoadedSample = true;

            // Generate waveform data for display
            GenerateSyntheticWaveformData(type);

            StatusMessage = $"Generated {waveType} waveform";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error generating waveform: {ex.Message}";
        }
    }

    /// <summary>
    /// Generates synthetic waveform data for display.
    /// </summary>
    private void GenerateSyntheticWaveformData(WaveType waveType)
    {
        int length = 44100 * 2; // 2 seconds at 44.1kHz
        _sourceWaveform = new float[length];

        for (int i = 0; i < length; i++)
        {
            float phase = (float)i / 44100 * 440f * 2f * MathF.PI;

            _sourceWaveform[i] = waveType switch
            {
                WaveType.Sine => MathF.Sin(phase),
                WaveType.Square => phase % (2f * MathF.PI) < MathF.PI ? 1f : -1f,
                WaveType.Sawtooth => (phase % (2f * MathF.PI)) / MathF.PI - 1f,
                WaveType.Triangle => MathF.Abs((phase % (2f * MathF.PI)) / MathF.PI - 1f) * 2f - 1f,
                WaveType.Noise => (float)(_random.NextDouble() * 2.0 - 1.0),
                _ => MathF.Sin(phase)
            };
        }

        WaveformChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ApplyPreset(string presetName)
    {
        try
        {
            GranularSynth preset = presetName switch
            {
                "Pad" => GranularSynth.CreatePadPreset(),
                "Texture" => GranularSynth.CreateTexturePreset(),
                "Freeze" => GranularSynth.CreateFreezePreset(),
                _ => throw new ArgumentException($"Unknown preset: {presetName}")
            };

            // Apply preset values
            GrainSizeMs = preset.GrainSize;
            GrainSizeRandom = preset.GrainSizeRandom;
            Density = preset.Density;
            PositionRandom = preset.PositionRandom;
            PitchRandom = preset.PitchRandom;
            PanSpread = preset.PanSpread;
            SelectedEnvelope = preset.Envelope;
            SelectedPlayMode = preset.PlayMode;
            ReverseProbability = preset.ReverseProbability;
            PitchTracking = preset.PitchTracking;

            StatusMessage = $"Applied {presetName} preset";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error applying preset: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ResetParameters()
    {
        Volume = 0.5f;
        Position = 0f;
        PositionRandom = 0.05f;
        GrainSizeMs = 50f;
        GrainSizeRandom = 0.2f;
        Density = 30f;
        DensityRandom = 0.1f;
        PitchShift = 0f;
        PitchRandom = 0f;
        PanSpread = 0.5f;
        SelectedEnvelope = GrainEnvelope.Gaussian;
        SelectedPlayMode = GranularPlayMode.Forward;
        ReverseProbability = 0.3f;
        PitchTracking = true;
        MaxGrains = 64;

        StatusMessage = "Parameters reset to defaults";
    }

    [RelayCommand]
    private void TogglePlay()
    {
        IsPlaying = !IsPlaying;

        if (IsPlaying)
        {
            // Trigger a test note
            _synth?.NoteOn(60, 100); // Middle C, velocity 100
            StatusMessage = "Playing...";
        }
        else
        {
            _synth?.AllNotesOff();
            GrainVisualizations.Clear();
            ActiveGrainCount = 0;
            StatusMessage = "Stopped";
        }
    }

    /// <summary>
    /// Triggers a note on the synth.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
        IsPlaying = true;
    }

    /// <summary>
    /// Triggers a note off on the synth.
    /// </summary>
    public void NoteOff(int note)
    {
        _synth?.NoteOff(note);
    }

    /// <summary>
    /// Stops all notes.
    /// </summary>
    public void AllNotesOff()
    {
        _synth?.AllNotesOff();
        IsPlaying = false;
        GrainVisualizations.Clear();
        ActiveGrainCount = 0;
    }

    #endregion

    /// <summary>
    /// Gets the underlying GranularSynth instance.
    /// </summary>
    public GranularSynth? GetSynth() => _synth;

    /// <summary>
    /// Sets the synth instance.
    /// </summary>
    public void SetSynth(GranularSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _grainVisualizationTimer?.Stop();
        _grainVisualizationTimer?.Dispose();
        _grainVisualizationTimer = null;

        _synth?.AllNotesOff();
    }
}
