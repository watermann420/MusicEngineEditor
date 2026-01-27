// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Wavetable Synthesizer Editor.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Warp modes for wavetable playback modification.
/// </summary>
public enum WarpMode
{
    /// <summary>No warping applied.</summary>
    None,
    /// <summary>Sync warp - resets phase at frequency.</summary>
    Sync,
    /// <summary>Bend warp - frequency modulation of read position.</summary>
    Bend,
    /// <summary>Mirror warp - reflects waveform at boundaries.</summary>
    Mirror
}

/// <summary>
/// Represents a wavetable preset for the browser.
/// </summary>
public partial class WavetablePreset : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private WavetableType? _builtInType;

    [ObservableProperty]
    private bool _isBuiltIn;

    /// <summary>
    /// Creates a built-in wavetable preset.
    /// </summary>
    public static WavetablePreset CreateBuiltIn(string name, WavetableType type)
    {
        return new WavetablePreset
        {
            Name = name,
            Category = "Built-in",
            BuiltInType = type,
            IsBuiltIn = true
        };
    }

    /// <summary>
    /// Creates a file-based wavetable preset.
    /// </summary>
    public static WavetablePreset CreateFromFile(string filePath)
    {
        return new WavetablePreset
        {
            Name = Path.GetFileNameWithoutExtension(filePath),
            Category = "Custom",
            FilePath = filePath,
            IsBuiltIn = false
        };
    }
}

/// <summary>
/// Represents a single waveform frame in the 3D visualization.
/// </summary>
public class WaveformFrame
{
    /// <summary>
    /// Gets or sets the frame index (0 to FrameCount-1).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// Gets or sets the normalized position (0.0 to 1.0).
    /// </summary>
    public float Position { get; set; }

    /// <summary>
    /// Gets or sets the waveform samples for this frame.
    /// </summary>
    public float[] Samples { get; set; } = Array.Empty<float>();

    /// <summary>
    /// Gets or sets the color for this frame in the visualization.
    /// </summary>
    public Color Color { get; set; }
}

/// <summary>
/// ViewModel for the Wavetable Synthesizer Editor control.
/// </summary>
public partial class WavetableSynthViewModel : ViewModelBase, IDisposable
{
    private WavetableSynth? _synth;
    private bool _disposed;
    private const int VisualizationFrameCount = 32;
    private const int VisualizationSampleCount = 128;

    #region Observable Properties - Synth Parameters

    [ObservableProperty]
    private float _position;

    [ObservableProperty]
    private float _volume = 0.5f;

    [ObservableProperty]
    private float _filterCutoff = 1.0f;

    [ObservableProperty]
    private float _filterResonance;

    [ObservableProperty]
    private float _detune;

    [ObservableProperty]
    private int _unisonVoices = 1;

    [ObservableProperty]
    private float _unisonDetune = 10f;

    [ObservableProperty]
    private float _unisonSpread = 0.5f;

    [ObservableProperty]
    private float _positionEnvAmount;

    [ObservableProperty]
    private float _positionLfoDepth;

    /// <summary>
    /// Warp mode for wavetable playback (Sync, Bend, Mirror, None).
    /// </summary>
    [ObservableProperty]
    private WarpMode _warpMode = WarpMode.None;

    /// <summary>
    /// Warp amount (0-1) controlling intensity of the warp effect.
    /// </summary>
    [ObservableProperty]
    private float _warpAmount;

    /// <summary>
    /// Sub-oscillator level (0-1) for adding low-frequency depth.
    /// </summary>
    [ObservableProperty]
    private float _subOscillatorLevel;

    /// <summary>
    /// Sub-oscillator octave offset (-2 to 0).
    /// </summary>
    [ObservableProperty]
    private int _subOscillatorOctave = -1;

    #endregion

    #region Observable Properties - Envelope

    [ObservableProperty]
    private double _attack = 0.01;

    [ObservableProperty]
    private double _decay = 0.1;

    [ObservableProperty]
    private double _sustain = 0.7;

    [ObservableProperty]
    private double _release = 0.3;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty]
    private string _synthName = "Wavetable Synth";

    [ObservableProperty]
    private WavetablePreset? _selectedPreset;

    [ObservableProperty]
    private string _currentWavetableName = "Basic";

    [ObservableProperty]
    private int _frameCount = 256;

    [ObservableProperty]
    private int _frameSize = 2048;

    [ObservableProperty]
    private bool _is3DViewEnabled = true;

    [ObservableProperty]
    private double _visualizationRotationX = 30;

    [ObservableProperty]
    private double _visualizationRotationY = 15;

    [ObservableProperty]
    private double _visualizationZoom = 1.0;

    [ObservableProperty]
    private bool _showPositionIndicator = true;

    [ObservableProperty]
    private bool _isPlaying;

    #endregion

    /// <summary>
    /// Gets the collection of available wavetable presets.
    /// </summary>
    public ObservableCollection<WavetablePreset> Presets { get; } = new();

    /// <summary>
    /// Gets the collection of preset categories.
    /// </summary>
    public ObservableCollection<string> Categories { get; } = new();

    /// <summary>
    /// Gets the available warp modes.
    /// </summary>
    public WarpMode[] WarpModes { get; } = (WarpMode[])Enum.GetValues(typeof(WarpMode));

    /// <summary>
    /// Gets the available sub-oscillator octave options.
    /// </summary>
    public int[] SubOscillatorOctaves { get; } = new[] { -2, -1, 0 };

    /// <summary>
    /// Gets the waveform frames for 3D visualization.
    /// </summary>
    public ObservableCollection<WaveformFrame> WaveformFrames { get; } = new();

    /// <summary>
    /// Gets the current waveform samples at the current position.
    /// </summary>
    public float[] CurrentWaveform { get; private set; } = new float[VisualizationSampleCount];

    /// <summary>
    /// Event raised when the waveform data changes.
    /// </summary>
    public event EventHandler? WaveformDataChanged;

    /// <summary>
    /// Event raised when the current position waveform changes.
    /// </summary>
    public event EventHandler? CurrentWaveformChanged;

    public WavetableSynthViewModel()
    {
        InitializePresets();
    }

    public WavetableSynthViewModel(WavetableSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        InitializePresets();
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes or sets the synth instance.
    /// </summary>
    public void Initialize(WavetableSynth? synth = null)
    {
        _synth = synth ?? new WavetableSynth(WavetableType.Basic);
        LoadFromSynth();
        UpdateVisualization();
    }

    private void InitializePresets()
    {
        // Add built-in wavetables
        Presets.Add(WavetablePreset.CreateBuiltIn("Basic", WavetableType.Basic));
        Presets.Add(WavetablePreset.CreateBuiltIn("PWM", WavetableType.PWM));
        Presets.Add(WavetablePreset.CreateBuiltIn("Vocal", WavetableType.Vocal));
        Presets.Add(WavetablePreset.CreateBuiltIn("Digital", WavetableType.Digital));
        Presets.Add(WavetablePreset.CreateBuiltIn("Analog", WavetableType.Analog));
        Presets.Add(WavetablePreset.CreateBuiltIn("Harmonic", WavetableType.Harmonic));

        // Update categories
        Categories.Clear();
        Categories.Add("All");
        Categories.Add("Built-in");
        Categories.Add("Custom");

        // Set default selection
        if (Presets.Count > 0)
        {
            SelectedPreset = Presets[0];
        }
    }

    private void LoadFromSynth()
    {
        if (_synth == null) return;

        Position = _synth.Position;
        Volume = _synth.Volume;
        FilterCutoff = _synth.FilterCutoff;
        FilterResonance = _synth.FilterResonance;
        Detune = _synth.Detune;
        UnisonVoices = _synth.UnisonVoices;
        UnisonDetune = _synth.UnisonDetune;
        UnisonSpread = _synth.UnisonSpread;
        PositionEnvAmount = _synth.PositionEnvAmount;
        PositionLfoDepth = _synth.PositionLFODepth;

        Attack = _synth.AmpEnvelope.Attack;
        Decay = _synth.AmpEnvelope.Decay;
        Sustain = _synth.AmpEnvelope.Sustain;
        Release = _synth.AmpEnvelope.Release;

        SynthName = _synth.Name;
    }

    #region Property Change Handlers

    partial void OnPositionChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Position = value;
            UpdateCurrentWaveform();
        }
    }

    partial void OnVolumeChanged(float value)
    {
        if (_synth != null)
            _synth.Volume = value;
    }

    partial void OnFilterCutoffChanged(float value)
    {
        if (_synth != null)
            _synth.FilterCutoff = value;
    }

    partial void OnFilterResonanceChanged(float value)
    {
        if (_synth != null)
            _synth.FilterResonance = value;
    }

    partial void OnDetuneChanged(float value)
    {
        if (_synth != null)
            _synth.Detune = value;
    }

    partial void OnUnisonVoicesChanged(int value)
    {
        if (_synth != null)
            _synth.UnisonVoices = value;
    }

    partial void OnUnisonDetuneChanged(float value)
    {
        if (_synth != null)
            _synth.UnisonDetune = value;
    }

    partial void OnUnisonSpreadChanged(float value)
    {
        if (_synth != null)
            _synth.UnisonSpread = value;
    }

    partial void OnPositionEnvAmountChanged(float value)
    {
        if (_synth != null)
            _synth.PositionEnvAmount = value;
    }

    partial void OnPositionLfoDepthChanged(float value)
    {
        if (_synth != null)
            _synth.PositionLFODepth = value;
    }

    partial void OnAttackChanged(double value)
    {
        if (_synth != null)
            _synth.AmpEnvelope.Attack = value;
    }

    partial void OnDecayChanged(double value)
    {
        if (_synth != null)
            _synth.AmpEnvelope.Decay = value;
    }

    partial void OnSustainChanged(double value)
    {
        if (_synth != null)
            _synth.AmpEnvelope.Sustain = value;
    }

    partial void OnReleaseChanged(double value)
    {
        if (_synth != null)
            _synth.AmpEnvelope.Release = value;
    }

    partial void OnSelectedPresetChanged(WavetablePreset? value)
    {
        if (value != null)
        {
            LoadPreset(value);
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void LoadPreset(WavetablePreset preset)
    {
        if (_synth == null || preset == null) return;

        try
        {
            if (preset.IsBuiltIn && preset.BuiltInType.HasValue)
            {
                _synth.GenerateBuiltInWavetable(preset.BuiltInType.Value);
                CurrentWavetableName = preset.Name;
            }
            else if (!string.IsNullOrEmpty(preset.FilePath) && File.Exists(preset.FilePath))
            {
                _synth.LoadWavetable(preset.FilePath);
                CurrentWavetableName = preset.Name;
            }

            UpdateVisualization();
            StatusMessage = $"Loaded wavetable: {preset.Name}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load wavetable: {ex.Message}";
        }
    }

    [RelayCommand]
    private void BrowseWavetable()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Load Wavetable",
            Filter = "WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
            DefaultExt = ".wav"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _synth?.LoadWavetable(dialog.FileName);

                var preset = WavetablePreset.CreateFromFile(dialog.FileName);
                Presets.Add(preset);
                SelectedPreset = preset;

                CurrentWavetableName = preset.Name;
                UpdateVisualization();
                StatusMessage = $"Loaded custom wavetable: {preset.Name}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to load wavetable: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void PlayNote(int note)
    {
        if (_synth == null) return;

        _synth.NoteOn(note, 100);
        IsPlaying = true;
    }

    [RelayCommand]
    private void StopNote(int note)
    {
        if (_synth == null) return;

        _synth.NoteOff(note);
        IsPlaying = false;
    }

    [RelayCommand]
    private void StopAllNotes()
    {
        _synth?.AllNotesOff();
        IsPlaying = false;
    }

    [RelayCommand]
    private void ResetParameters()
    {
        Position = 0f;
        Volume = 0.5f;
        FilterCutoff = 1.0f;
        FilterResonance = 0f;
        Detune = 0f;
        UnisonVoices = 1;
        UnisonDetune = 10f;
        UnisonSpread = 0.5f;
        PositionEnvAmount = 0f;
        PositionLfoDepth = 0f;

        WarpMode = WarpMode.None;
        WarpAmount = 0f;
        SubOscillatorLevel = 0f;
        SubOscillatorOctave = -1;

        Attack = 0.01;
        Decay = 0.1;
        Sustain = 0.7;
        Release = 0.3;

        StatusMessage = "Parameters reset to defaults";
    }

    [RelayCommand]
    private void LoadPadPreset()
    {
        if (_synth == null)
        {
            _synth = WavetableSynth.CreatePadPreset();
        }
        else
        {
            _synth.GenerateBuiltInWavetable(WavetableType.Analog);
        }

        Position = 0.3f;
        Attack = 0.5;
        Decay = 0.5;
        Sustain = 0.8;
        Release = 1.0;
        UnisonVoices = 4;
        UnisonDetune = 15f;
        UnisonSpread = 0.7f;
        FilterCutoff = 0.6f;

        CurrentWavetableName = "Pad Preset";
        UpdateVisualization();
        StatusMessage = "Loaded Pad preset";
    }

    [RelayCommand]
    private void LoadLeadPreset()
    {
        if (_synth == null)
        {
            _synth = WavetableSynth.CreateLeadPreset();
        }
        else
        {
            _synth.GenerateBuiltInWavetable(WavetableType.Digital);
        }

        Position = 0.5f;
        Attack = 0.01;
        Decay = 0.2;
        Sustain = 0.7;
        Release = 0.2;
        FilterCutoff = 0.8f;
        FilterResonance = 0.3f;

        CurrentWavetableName = "Lead Preset";
        UpdateVisualization();
        StatusMessage = "Loaded Lead preset";
    }

    [RelayCommand]
    private void LoadVocalPreset()
    {
        if (_synth == null)
        {
            _synth = WavetableSynth.CreateVocalPreset();
        }
        else
        {
            _synth.GenerateBuiltInWavetable(WavetableType.Vocal);
        }

        Position = 0f;
        PositionLfoDepth = 0.3f;
        Attack = 0.1;
        Decay = 0.3;
        Sustain = 0.6;
        Release = 0.4;

        CurrentWavetableName = "Vocal Preset";
        UpdateVisualization();
        StatusMessage = "Loaded Vocal preset";
    }

    [RelayCommand]
    private void Toggle3DView()
    {
        Is3DViewEnabled = !Is3DViewEnabled;
    }

    [RelayCommand]
    private void ResetVisualization()
    {
        VisualizationRotationX = 30;
        VisualizationRotationY = 15;
        VisualizationZoom = 1.0;
    }

    [RelayCommand]
    private void ZoomIn()
    {
        VisualizationZoom = Math.Min(3.0, VisualizationZoom + 0.1);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        VisualizationZoom = Math.Max(0.5, VisualizationZoom - 0.1);
    }

    #endregion

    #region Visualization

    /// <summary>
    /// Updates the 3D visualization data from the synth.
    /// </summary>
    public void UpdateVisualization()
    {
        if (_synth == null) return;

        WaveformFrames.Clear();

        // Generate visualization frames at evenly spaced positions
        for (int i = 0; i < VisualizationFrameCount; i++)
        {
            float position = (float)i / (VisualizationFrameCount - 1);
            var samples = new float[VisualizationSampleCount];

            // Sample the waveform at this position using our local generation
            for (int j = 0; j < VisualizationSampleCount; j++)
            {
                float phase = (float)j / VisualizationSampleCount * 2f * MathF.PI;
                samples[j] = GenerateWavetableSample(phase, position, SelectedPreset?.BuiltInType ?? WavetableType.Basic);
            }

            // Calculate color gradient from cyan to green
            var color = InterpolateColor(
                Color.FromRgb(0x00, 0xD9, 0xFF),  // Cyan
                Color.FromRgb(0x00, 0xFF, 0x88),  // Green
                position);

            WaveformFrames.Add(new WaveformFrame
            {
                Index = i,
                Position = position,
                Samples = samples,
                Color = color
            });
        }

        UpdateCurrentWaveform();
        WaveformDataChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateCurrentWaveform()
    {
        if (_synth == null) return;

        // Sample the waveform at the current position using our local generation
        for (int i = 0; i < VisualizationSampleCount; i++)
        {
            float phase = (float)i / VisualizationSampleCount * 2f * MathF.PI;
            CurrentWaveform[i] = GenerateWavetableSample(phase, Position, SelectedPreset?.BuiltInType ?? WavetableType.Basic);
        }

        CurrentWaveformChanged?.Invoke(this, EventArgs.Empty);
    }

    private static Color InterpolateColor(Color c1, Color c2, float t)
    {
        byte r = (byte)(c1.R + (c2.R - c1.R) * t);
        byte g = (byte)(c1.G + (c2.G - c1.G) * t);
        byte b = (byte)(c1.B + (c2.B - c1.B) * t);
        return Color.FromRgb(r, g, b);
    }

    /// <summary>
    /// Generates a wavetable sample for visualization purposes.
    /// Mirrors the generation algorithm from WavetableSynth.
    /// </summary>
    private static float GenerateWavetableSample(float phase, float morph, WavetableType type)
    {
        return type switch
        {
            WavetableType.Basic => GenerateBasicSample(phase, morph),
            WavetableType.PWM => GeneratePWMSample(phase, morph),
            WavetableType.Vocal => GenerateVocalSample(phase, morph),
            WavetableType.Digital => GenerateDigitalSample(phase, morph),
            WavetableType.Analog => GenerateAnalogSample(phase, morph),
            WavetableType.Harmonic => GenerateHarmonicSample(phase, morph),
            _ => MathF.Sin(phase)
        };
    }

    private static float GenerateBasicSample(float phase, float morph)
    {
        // Morph: 0=Sine, 0.33=Triangle, 0.66=Saw, 1.0=Square
        float sine = MathF.Sin(phase);
        float triangle = 2f * MathF.Abs(phase / MathF.PI - 1f) - 1f;
        float saw = 1f - phase / MathF.PI;
        float square = phase < MathF.PI ? 1f : -1f;

        if (morph < 0.33f)
            return Lerp(sine, triangle, morph / 0.33f);
        if (morph < 0.66f)
            return Lerp(triangle, saw, (morph - 0.33f) / 0.33f);
        return Lerp(saw, square, (morph - 0.66f) / 0.34f);
    }

    private static float GeneratePWMSample(float phase, float morph)
    {
        float pulseWidth = 0.1f + morph * 0.8f;
        return (phase / (2f * MathF.PI)) < pulseWidth ? 1f : -1f;
    }

    private static float GenerateVocalSample(float phase, float morph)
    {
        float[][] formants = {
            new[] { 800f, 1200f, 2500f }, // A
            new[] { 350f, 2000f, 2800f }, // E
            new[] { 270f, 2300f, 3000f }, // I
            new[] { 450f, 800f, 2800f },  // O
            new[] { 325f, 700f, 2500f }   // U
        };
        int vowelIndex = (int)(morph * 4.99f);
        float blend = morph * 5f - vowelIndex;
        int nextIndex = Math.Min(vowelIndex + 1, 4);
        float result = 0f;
        for (int f = 0; f < 3; f++)
        {
            float freq1 = formants[vowelIndex][f];
            float freq2 = formants[nextIndex][f];
            float freq = Lerp(freq1, freq2, blend);
            result += MathF.Sin(phase * freq / 440f) * (1f / (f + 1));
        }
        return result * 0.5f;
    }

    private static float GenerateDigitalSample(float phase, float morph)
    {
        int bits = 2 + (int)(morph * 6);
        float levels = MathF.Pow(2, bits);
        float sine = MathF.Sin(phase);
        return MathF.Round(sine * levels) / levels;
    }

    private static float GenerateAnalogSample(float phase, float morph)
    {
        float saw = 1f - phase / MathF.PI;
        float harmonics = 1f + morph * 7f;
        float result = 0f;
        for (int h = 1; h <= (int)harmonics; h++)
            result += MathF.Sin(phase * h) / h;
        return result * 0.5f * (1f - morph * 0.3f) + saw * morph * 0.3f;
    }

    private static float GenerateHarmonicSample(float phase, float morph)
    {
        float result = 0f;
        int numHarmonics = 1 + (int)(morph * 15);
        for (int h = 1; h <= numHarmonics; h++)
            result += MathF.Sin(phase * h) / h;
        return result * 0.5f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    #endregion

    /// <summary>
    /// Gets the underlying WavetableSynth instance.
    /// </summary>
    public WavetableSynth? GetSynth() => _synth;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAllNotes();
        // Note: We don't dispose the synth here as it may be owned externally
    }
}
