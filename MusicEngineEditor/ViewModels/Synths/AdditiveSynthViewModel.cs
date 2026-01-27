// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Additive Synth Editor control.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a single harmonic partial in the additive synth editor.
/// </summary>
public partial class HarmonicSliderViewModel : ObservableObject
{
    private readonly AdditiveSynthViewModel _parent;

    [ObservableProperty]
    private int _harmonicNumber;

    [ObservableProperty]
    private float _amplitude;

    [ObservableProperty]
    private float _phase;

    [ObservableProperty]
    private float _detune;

    [ObservableProperty]
    private bool _hasCustomEnvelope;

    [ObservableProperty]
    private double _attack = 0.01;

    [ObservableProperty]
    private double _decay = 0.1;

    [ObservableProperty]
    private double _sustain = 0.8;

    [ObservableProperty]
    private double _release = 0.3;

    /// <summary>
    /// Gets the display label for this harmonic.
    /// </summary>
    public string Label => HarmonicNumber.ToString();

    public HarmonicSliderViewModel(int harmonicNumber, AdditiveSynthViewModel parent)
    {
        _harmonicNumber = harmonicNumber;
        _parent = parent;
    }

    partial void OnAmplitudeChanged(float value)
    {
        _parent.UpdateHarmonic(HarmonicNumber, value);
        _parent.UpdateWaveformPreview();
    }

    partial void OnPhaseChanged(float value)
    {
        _parent.UpdateHarmonicPhase(HarmonicNumber, value);
        _parent.UpdateWaveformPreview();
    }

    partial void OnDetuneChanged(float value)
    {
        _parent.UpdateHarmonicDetune(HarmonicNumber, value);
    }

    partial void OnHasCustomEnvelopeChanged(bool value)
    {
        _parent.UpdateHarmonicEnvelope(HarmonicNumber, value ? new Envelope(Attack, Decay, Sustain, Release) : null);
    }

    partial void OnAttackChanged(double value)
    {
        if (HasCustomEnvelope)
            _parent.UpdateHarmonicEnvelope(HarmonicNumber, new Envelope(value, Decay, Sustain, Release));
    }

    partial void OnDecayChanged(double value)
    {
        if (HasCustomEnvelope)
            _parent.UpdateHarmonicEnvelope(HarmonicNumber, new Envelope(Attack, value, Sustain, Release));
    }

    partial void OnSustainChanged(double value)
    {
        if (HasCustomEnvelope)
            _parent.UpdateHarmonicEnvelope(HarmonicNumber, new Envelope(Attack, Decay, value, Release));
    }

    partial void OnReleaseChanged(double value)
    {
        if (HasCustomEnvelope)
            _parent.UpdateHarmonicEnvelope(HarmonicNumber, new Envelope(Attack, Decay, Sustain, value));
    }
}

/// <summary>
/// Represents a Hammond-style drawbar in the editor.
/// </summary>
public partial class DrawbarViewModel : ObservableObject
{
    private readonly AdditiveSynthViewModel _parent;
    private readonly int _index;

    [ObservableProperty]
    private int _value;

    [ObservableProperty]
    private string _footage = "";

    [ObservableProperty]
    private string _colorHex = "#FFFFFF";

    /// <summary>
    /// Gets the drawbar index (0-8).
    /// </summary>
    public int Index => _index;

    public DrawbarViewModel(int index, string footage, string colorHex, AdditiveSynthViewModel parent)
    {
        _index = index;
        _footage = footage;
        _colorHex = colorHex;
        _parent = parent;
        _value = 0;
    }

    partial void OnValueChanged(int value)
    {
        _parent.UpdateDrawbar(_index, value);
    }
}

/// <summary>
/// Represents a preset button for common harmonic patterns.
/// </summary>
public partial class HarmonicPresetViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    /// <summary>
    /// The harmonic amplitudes for this preset (index 0 = fundamental).
    /// </summary>
    public float[] Harmonics { get; set; } = Array.Empty<float>();

    public HarmonicPresetViewModel(string name, string description, float[] harmonics)
    {
        _name = name;
        _description = description;
        Harmonics = harmonics;
    }
}

/// <summary>
/// ViewModel for the Additive Synth Editor control.
/// Provides UI bindings for harmonic editing, drawbar mode, and waveform preview.
/// </summary>
public partial class AdditiveSynthViewModel : ViewModelBase, IDisposable
{
    private AdditiveSynth? _synth;
    private bool _disposed;
    private bool _isUpdating;

    #region Observable Properties

    [ObservableProperty]
    private float _masterVolume = 0.5f;

    [ObservableProperty]
    private double _attack = 0.01;

    [ObservableProperty]
    private double _decay = 0.01;

    [ObservableProperty]
    private double _sustain = 1.0;

    [ObservableProperty]
    private double _release = 0.05;

    [ObservableProperty]
    private float _filterCutoff = 1.0f;

    [ObservableProperty]
    private float _filterResonance = 0.0f;

    [ObservableProperty]
    private bool _isDrawbarMode;

    [ObservableProperty]
    private int _harmonicCount = 32;

    [ObservableProperty]
    private float _pitchBend;

    [ObservableProperty]
    private float _pitchBendRange = 2.0f;

    [ObservableProperty]
    private float[] _waveformData = Array.Empty<float>();

    [ObservableProperty]
    private string _currentPresetName = "Custom";

    [ObservableProperty]
    private HarmonicSliderViewModel? _selectedHarmonic;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of harmonic sliders.
    /// </summary>
    public ObservableCollection<HarmonicSliderViewModel> Harmonics { get; } = new();

    /// <summary>
    /// Gets the collection of Hammond-style drawbars.
    /// </summary>
    public ObservableCollection<DrawbarViewModel> Drawbars { get; } = new();

    /// <summary>
    /// Gets the collection of harmonic presets.
    /// </summary>
    public ObservableCollection<HarmonicPresetViewModel> Presets { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when the waveform preview needs to be redrawn.
    /// </summary>
    public event EventHandler? WaveformChanged;

    /// <summary>
    /// Raised when a note should be previewed.
    /// </summary>
    public event EventHandler<int>? PreviewNote;

    #endregion

    #region Constructor and Initialization

    public AdditiveSynthViewModel()
    {
        InitializePresets();
        InitializeDrawbars();
    }

    public AdditiveSynthViewModel(AdditiveSynth synth) : this()
    {
        SetSynth(synth);
    }

    /// <summary>
    /// Sets or replaces the underlying AdditiveSynth instance.
    /// </summary>
    public void SetSynth(AdditiveSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Creates a new AdditiveSynth instance and initializes the editor.
    /// </summary>
    public void Initialize(int? sampleRate = null)
    {
        _synth = new AdditiveSynth(sampleRate);
        LoadFromSynth();
    }

    private void LoadFromSynth()
    {
        if (_synth == null) return;

        _isUpdating = true;

        try
        {
            // Load master settings
            MasterVolume = _synth.Volume;
            Attack = _synth.AmpEnvelope.Attack;
            Decay = _synth.AmpEnvelope.Decay;
            Sustain = _synth.AmpEnvelope.Sustain;
            Release = _synth.AmpEnvelope.Release;
            PitchBend = _synth.PitchBend;
            PitchBendRange = _synth.PitchBendRange;

            // Load harmonics
            Harmonics.Clear();
            for (int i = 1; i <= HarmonicCount; i++)
            {
                var slider = new HarmonicSliderViewModel(i, this);
                var partial = _synth.Partials.FirstOrDefault(p => p.HarmonicNumber == i);
                if (partial != null)
                {
                    slider.Amplitude = partial.Amplitude;
                    slider.Phase = partial.Phase;
                    slider.Detune = partial.Detune;
                    if (partial.Envelope != null)
                    {
                        slider.HasCustomEnvelope = true;
                        slider.Attack = partial.Envelope.Attack;
                        slider.Decay = partial.Envelope.Decay;
                        slider.Sustain = partial.Envelope.Sustain;
                        slider.Release = partial.Envelope.Release;
                    }
                }
                Harmonics.Add(slider);
            }

            // Load drawbars
            for (int i = 0; i < 9 && i < Drawbars.Count; i++)
            {
                Drawbars[i].Value = _synth.Drawbars[i];
            }

            UpdateWaveformPreview();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void InitializePresets()
    {
        Presets.Clear();

        // Sine wave
        Presets.Add(new HarmonicPresetViewModel(
            "Sine",
            "Pure sine wave - fundamental only",
            new[] { 1.0f }));

        // Sawtooth approximation
        var sawHarmonics = new float[32];
        for (int i = 0; i < 32; i++)
            sawHarmonics[i] = 1.0f / (i + 1);
        Presets.Add(new HarmonicPresetViewModel(
            "Sawtooth",
            "All harmonics at 1/n amplitude",
            sawHarmonics));

        // Square wave approximation
        var squareHarmonics = new float[32];
        for (int i = 0; i < 32; i++)
            squareHarmonics[i] = (i % 2 == 0) ? 1.0f / (i + 1) : 0f;
        Presets.Add(new HarmonicPresetViewModel(
            "Square",
            "Odd harmonics at 1/n amplitude",
            squareHarmonics));

        // Triangle approximation
        var triangleHarmonics = new float[32];
        for (int i = 0; i < 32; i++)
        {
            if (i % 2 == 0)
            {
                int n = i + 1;
                triangleHarmonics[i] = 1.0f / (n * n) * ((i / 2 % 2 == 0) ? 1 : -1);
            }
        }
        // Normalize
        float maxTri = triangleHarmonics.Max(MathF.Abs);
        if (maxTri > 0)
            for (int i = 0; i < triangleHarmonics.Length; i++)
                triangleHarmonics[i] = MathF.Abs(triangleHarmonics[i] / maxTri);
        Presets.Add(new HarmonicPresetViewModel(
            "Triangle",
            "Odd harmonics at 1/n^2 amplitude",
            triangleHarmonics));

        // Bell tone
        Presets.Add(new HarmonicPresetViewModel(
            "Bell",
            "Rich bell-like harmonics",
            new[] { 1.0f, 0.8f, 0.6f, 0.5f, 0.4f, 0.3f, 0.2f, 0.15f, 0.1f, 0.08f }));

        // Organ-like
        Presets.Add(new HarmonicPresetViewModel(
            "Organ",
            "Classic organ tone",
            new[] { 1.0f, 0.0f, 0.5f, 0.0f, 0.3f, 0.0f, 0.2f, 0.0f }));

        // Bright
        var brightHarmonics = new float[16];
        for (int i = 0; i < 16; i++)
            brightHarmonics[i] = 0.8f;
        brightHarmonics[0] = 1.0f;
        Presets.Add(new HarmonicPresetViewModel(
            "Bright",
            "Equal harmonics for maximum brightness",
            brightHarmonics));

        // Hollow
        Presets.Add(new HarmonicPresetViewModel(
            "Hollow",
            "Fundamental with sparse upper harmonics",
            new[] { 1.0f, 0.0f, 0.3f, 0.0f, 0.0f, 0.2f, 0.0f, 0.0f, 0.0f, 0.1f }));

        // Pulse 25%
        var pulse25Harmonics = new float[32];
        for (int i = 0; i < 32; i++)
        {
            int n = i + 1;
            pulse25Harmonics[i] = MathF.Abs(MathF.Sin(n * MathF.PI * 0.25f)) / n;
        }
        Presets.Add(new HarmonicPresetViewModel(
            "Pulse 25%",
            "25% duty cycle pulse wave",
            pulse25Harmonics));

        // Vocal "Ah"
        Presets.Add(new HarmonicPresetViewModel(
            "Vocal Ah",
            "Vowel-like formant structure",
            new[] { 1.0f, 0.7f, 0.5f, 0.6f, 0.4f, 0.3f, 0.2f, 0.15f, 0.1f, 0.08f, 0.06f, 0.04f }));
    }

    private void InitializeDrawbars()
    {
        // Hammond drawbar configuration
        // Index, Footage, Color (traditional Hammond colors)
        var drawbarConfig = new[]
        {
            (0, "16'", "#8B4513"),   // Brown - sub-fundamental
            (1, "5 1/3'", "#8B4513"), // Brown - third harmonic
            (2, "8'", "#FFFFFF"),    // White - fundamental
            (3, "4'", "#FFFFFF"),    // White - second harmonic
            (4, "2 2/3'", "#000000"), // Black - third harmonic
            (5, "2'", "#FFFFFF"),    // White - fourth harmonic
            (6, "1 3/5'", "#000000"), // Black - fifth harmonic
            (7, "1 1/3'", "#000000"), // Black - sixth harmonic
            (8, "1'", "#FFFFFF"),    // White - eighth harmonic
        };

        Drawbars.Clear();
        foreach (var (index, footage, color) in drawbarConfig)
        {
            Drawbars.Add(new DrawbarViewModel(index, footage, color, this));
        }
    }

    #endregion

    #region Partial Update Methods (called by child ViewModels)

    internal void UpdateHarmonic(int harmonicNumber, float amplitude)
    {
        if (_synth == null || _isUpdating) return;
        _synth.SetHarmonic(harmonicNumber, amplitude);
        CurrentPresetName = "Custom";
    }

    internal void UpdateHarmonicPhase(int harmonicNumber, float phase)
    {
        if (_synth == null || _isUpdating) return;
        var partial = _synth.Partials.FirstOrDefault(p => p.HarmonicNumber == harmonicNumber);
        if (partial != null)
        {
            partial.Phase = phase;
        }
        CurrentPresetName = "Custom";
    }

    internal void UpdateHarmonicDetune(int harmonicNumber, float detune)
    {
        if (_synth == null || _isUpdating) return;
        var partial = _synth.Partials.FirstOrDefault(p => p.HarmonicNumber == harmonicNumber);
        if (partial != null)
        {
            partial.Detune = detune;
        }
    }

    internal void UpdateHarmonicEnvelope(int harmonicNumber, Envelope? envelope)
    {
        if (_synth == null || _isUpdating) return;
        var partial = _synth.Partials.FirstOrDefault(p => p.HarmonicNumber == harmonicNumber);
        if (partial != null)
        {
            partial.Envelope = envelope;
        }
    }

    internal void UpdateDrawbar(int index, int value)
    {
        if (_synth == null || _isUpdating) return;
        _synth.SetDrawbar(index, value);
        UpdateWaveformPreview();
        CurrentPresetName = "Custom";
    }

    #endregion

    #region Property Change Handlers

    partial void OnMasterVolumeChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.Volume = value;
        }
    }

    partial void OnAttackChanged(double value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.AmpEnvelope.Attack = value;
        }
    }

    partial void OnDecayChanged(double value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.AmpEnvelope.Decay = value;
        }
    }

    partial void OnSustainChanged(double value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.AmpEnvelope.Sustain = value;
        }
    }

    partial void OnReleaseChanged(double value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.AmpEnvelope.Release = value;
        }
    }

    partial void OnPitchBendChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.PitchBend = value;
        }
    }

    partial void OnPitchBendRangeChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.PitchBendRange = value;
        }
    }

    partial void OnHarmonicCountChanged(int value)
    {
        // Rebuild harmonics collection
        if (_synth != null)
        {
            _isUpdating = true;
            try
            {
                while (Harmonics.Count < value)
                {
                    int num = Harmonics.Count + 1;
                    var slider = new HarmonicSliderViewModel(num, this);
                    var partial = _synth.Partials.FirstOrDefault(p => p.HarmonicNumber == num);
                    if (partial != null)
                    {
                        slider.Amplitude = partial.Amplitude;
                    }
                    Harmonics.Add(slider);
                }
                while (Harmonics.Count > value)
                {
                    Harmonics.RemoveAt(Harmonics.Count - 1);
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }

    partial void OnIsDrawbarModeChanged(bool value)
    {
        if (value && _synth != null)
        {
            // When switching to drawbar mode, apply current drawbar settings
            _synth.ApplyDrawbars();
            UpdateWaveformPreview();
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void ApplyPreset(HarmonicPresetViewModel? preset)
    {
        if (preset == null || _synth == null) return;

        _isUpdating = true;
        try
        {
            // Clear all harmonics first
            for (int i = 0; i < Harmonics.Count; i++)
            {
                Harmonics[i].Amplitude = 0f;
            }

            // Apply preset harmonics
            for (int i = 0; i < preset.Harmonics.Length && i < Harmonics.Count; i++)
            {
                Harmonics[i].Amplitude = preset.Harmonics[i];
            }

            // Update synth
            var harmonicTuples = preset.Harmonics
                .Select((amp, i) => (i + 1, amp))
                .Where(h => h.amp > 0)
                .ToArray();
            _synth.SetHarmonics(harmonicTuples);

            CurrentPresetName = preset.Name;
            IsDrawbarMode = false;
        }
        finally
        {
            _isUpdating = false;
        }

        UpdateWaveformPreview();
        StatusMessage = $"Applied preset: {preset.Name}";
    }

    [RelayCommand]
    private void ApplyDrawbarPreset(string presetCode)
    {
        if (_synth == null || string.IsNullOrEmpty(presetCode)) return;

        _synth.SetDrawbars(presetCode);

        // Update drawbar view models
        _isUpdating = true;
        try
        {
            for (int i = 0; i < 9 && i < Drawbars.Count; i++)
            {
                Drawbars[i].Value = _synth.Drawbars[i];
            }
        }
        finally
        {
            _isUpdating = false;
        }

        IsDrawbarMode = true;
        CurrentPresetName = $"Drawbar {presetCode}";
        UpdateWaveformPreview();
    }

    [RelayCommand]
    private void ResetAll()
    {
        if (_synth == null) return;

        _isUpdating = true;
        try
        {
            // Reset harmonics
            foreach (var harmonic in Harmonics)
            {
                harmonic.Amplitude = harmonic.HarmonicNumber == 1 ? 1.0f : 0f;
                harmonic.Phase = 0f;
                harmonic.Detune = 0f;
                harmonic.HasCustomEnvelope = false;
            }

            // Reset drawbars
            foreach (var drawbar in Drawbars)
            {
                drawbar.Value = 0;
            }

            // Reset envelope
            Attack = 0.01;
            Decay = 0.01;
            Sustain = 1.0;
            Release = 0.05;

            // Reset volume
            MasterVolume = 0.5f;

            // Apply to synth
            _synth.SetHarmonics((1, 1.0f));
            _synth.Volume = 0.5f;
            _synth.AmpEnvelope.Attack = 0.01;
            _synth.AmpEnvelope.Decay = 0.01;
            _synth.AmpEnvelope.Sustain = 1.0;
            _synth.AmpEnvelope.Release = 0.05;

            CurrentPresetName = "Sine";
            IsDrawbarMode = false;
        }
        finally
        {
            _isUpdating = false;
        }

        UpdateWaveformPreview();
        StatusMessage = "Reset to default sine wave";
    }

    [RelayCommand]
    private void RandomizeHarmonics()
    {
        if (_synth == null) return;

        var random = new Random();
        _isUpdating = true;
        try
        {
            foreach (var harmonic in Harmonics)
            {
                // Higher harmonics tend to have lower amplitudes
                float maxAmp = 1.0f / MathF.Sqrt(harmonic.HarmonicNumber);
                harmonic.Amplitude = (float)random.NextDouble() * maxAmp;
            }

            // Update synth
            var harmonicTuples = Harmonics
                .Where(h => h.Amplitude > 0.001f)
                .Select(h => (h.HarmonicNumber, h.Amplitude))
                .ToArray();
            _synth.SetHarmonics(harmonicTuples);

            CurrentPresetName = "Random";
        }
        finally
        {
            _isUpdating = false;
        }

        UpdateWaveformPreview();
        StatusMessage = "Randomized harmonics";
    }

    [RelayCommand]
    private void NormalizeHarmonics()
    {
        if (_synth == null) return;

        float maxAmp = Harmonics.Max(h => h.Amplitude);
        if (maxAmp <= 0) return;

        _isUpdating = true;
        try
        {
            foreach (var harmonic in Harmonics)
            {
                harmonic.Amplitude /= maxAmp;
            }

            // Update synth
            var harmonicTuples = Harmonics
                .Where(h => h.Amplitude > 0.001f)
                .Select(h => (h.HarmonicNumber, h.Amplitude))
                .ToArray();
            _synth.SetHarmonics(harmonicTuples);
        }
        finally
        {
            _isUpdating = false;
        }

        UpdateWaveformPreview();
        StatusMessage = "Normalized harmonics";
    }

    [RelayCommand]
    private void PlayPreviewNote(int? midiNote = null)
    {
        int note = midiNote ?? 60; // Default to middle C
        PreviewNote?.Invoke(this, note);
    }

    [RelayCommand]
    private void SelectHarmonic(HarmonicSliderViewModel? harmonic)
    {
        SelectedHarmonic = harmonic;
    }

    [RelayCommand]
    private void SetHarmonicCount(int count)
    {
        HarmonicCount = Math.Clamp(count, 1, 64);
    }

    #endregion

    #region Waveform Preview

    /// <summary>
    /// Updates the waveform preview data.
    /// </summary>
    public void UpdateWaveformPreview()
    {
        if (_synth == null) return;

        const int samples = 256;
        var waveform = new float[samples];

        // Generate one cycle of the waveform
        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)samples;
            float sample = 0f;

            foreach (var partial in _synth.Partials)
            {
                if (partial.Amplitude > 0.001f)
                {
                    float phase = (t * partial.HarmonicNumber + partial.Phase) * 2 * MathF.PI;
                    sample += MathF.Sin(phase) * partial.Amplitude;
                }
            }

            waveform[i] = sample;
        }

        // Normalize
        float maxVal = waveform.Max(MathF.Abs);
        if (maxVal > 0)
        {
            for (int i = 0; i < samples; i++)
            {
                waveform[i] /= maxVal;
            }
        }

        WaveformData = waveform;
        WaveformChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the underlying AdditiveSynth instance.
    /// </summary>
    public AdditiveSynth? GetSynth() => _synth;

    /// <summary>
    /// Triggers a note on the synth.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
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
    }

    /// <summary>
    /// Gets the current drawbar settings as a string.
    /// </summary>
    public string GetDrawbarString()
    {
        return string.Concat(Drawbars.Select(d => d.Value.ToString()));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _synth = null;
        GC.SuppressFinalize(this);
    }

    #endregion
}
