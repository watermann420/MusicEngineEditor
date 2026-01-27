// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the PadSynth Editor control.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a single harmonic bar in the harmonic profile editor.
/// </summary>
public partial class HarmonicBarViewModel : ObservableObject
{
    private readonly int _harmonicNumber;
    private readonly Action<int, float> _onAmplitudeChanged;

    [ObservableProperty]
    private float _amplitude;

    [ObservableProperty]
    private bool _isHovered;

    /// <summary>
    /// Gets the harmonic number (1-based).
    /// </summary>
    public int HarmonicNumber => _harmonicNumber;

    /// <summary>
    /// Gets the display label for this harmonic.
    /// </summary>
    public string Label => _harmonicNumber.ToString();

    /// <summary>
    /// Gets the bar color based on harmonic position (cyan to purple gradient).
    /// </summary>
    public Brush BarColor
    {
        get
        {
            // Gradient from cyan (#00D9FF) to purple (#9B59B6)
            float t = Math.Min(1f, (_harmonicNumber - 1) / 31f);
            byte r = (byte)(0x00 + t * (0x9B - 0x00));
            byte g = (byte)(0xD9 + t * (0x59 - 0xD9));
            byte b = (byte)(0xFF + t * (0xB6 - 0xFF));
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }
    }

    /// <summary>
    /// Creates a new harmonic bar view model.
    /// </summary>
    public HarmonicBarViewModel(int harmonicNumber, float amplitude, Action<int, float> onAmplitudeChanged)
    {
        _harmonicNumber = harmonicNumber;
        _amplitude = amplitude;
        _onAmplitudeChanged = onAmplitudeChanged;
    }

    partial void OnAmplitudeChanged(float value)
    {
        _onAmplitudeChanged?.Invoke(_harmonicNumber, value);
    }
}

/// <summary>
/// ViewModel for the PadSynth Editor control.
/// Provides bindings for the PadSynth parameter editing UI.
/// </summary>
public partial class PadSynthViewModel : ViewModelBase, IDisposable
{
    private PadSynth? _synth;
    private CancellationTokenSource? _generationCts;
    private bool _disposed;
    private const int DisplayedHarmonics = 32;

    // Bandwidth
    [ObservableProperty]
    private float _bandwidth = 50f;

    [ObservableProperty]
    private float _bandwidthScale = 1f;

    // Detune / Spread
    [ObservableProperty]
    private float _detune;

    [ObservableProperty]
    private int _unisonVoices = 1;

    [ObservableProperty]
    private float _unisonDetune = 10f;

    [ObservableProperty]
    private float _unisonSpread = 0.5f;

    // Evolution (modulation over time)
    [ObservableProperty]
    private float _evolutionSpeed = 0.5f;

    // Envelope
    [ObservableProperty]
    private double _attack = 0.5;

    [ObservableProperty]
    private double _decay = 0.5;

    [ObservableProperty]
    private double _sustain = 0.8;

    [ObservableProperty]
    private double _release = 1.0;

    // Volume
    [ObservableProperty]
    private float _volume = 0.5f;

    // Random seed
    [ObservableProperty]
    private int _seed = 42;

    // Profile selection
    [ObservableProperty]
    private PadHarmonicProfile _selectedProfile = PadHarmonicProfile.Saw;

    // Generation state
    [ObservableProperty]
    private bool _isGenerating;

    [ObservableProperty]
    private double _generationProgress;

    [ObservableProperty]
    private string _generationStatus = "Ready";

    // Waveform preview data
    [ObservableProperty]
    private PointCollection? _waveformPoints;

    [ObservableProperty]
    private bool _hasWaveform;

    /// <summary>
    /// Gets the collection of harmonic bars for the profile editor.
    /// </summary>
    public ObservableCollection<HarmonicBarViewModel> Harmonics { get; } = new();

    /// <summary>
    /// Gets the available harmonic profiles.
    /// </summary>
    public PadHarmonicProfile[] AvailableProfiles { get; } = Enum.GetValues<PadHarmonicProfile>();

    /// <summary>
    /// Event raised when the wavetable needs to be regenerated.
    /// </summary>
    public event EventHandler? WavetableChanged;

    /// <summary>
    /// Creates a new PadSynthViewModel with default settings.
    /// </summary>
    public PadSynthViewModel()
    {
        InitializeHarmonics();
    }

    /// <summary>
    /// Creates a new PadSynthViewModel bound to an existing PadSynth.
    /// </summary>
    public PadSynthViewModel(PadSynth synth) : this()
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes the synth with optional sample rate.
    /// </summary>
    public void Initialize(int? sampleRate = null)
    {
        _synth?.AllNotesOff();
        _synth = new PadSynth(sampleRate);
        LoadFromSynth();
    }

    /// <summary>
    /// Gets the underlying PadSynth instance.
    /// </summary>
    public PadSynth? GetSynth() => _synth;

    /// <summary>
    /// Sets the underlying PadSynth instance.
    /// </summary>
    public void SetSynth(PadSynth synth)
    {
        _synth = synth;
        LoadFromSynth();
    }

    private void InitializeHarmonics()
    {
        Harmonics.Clear();
        for (int i = 1; i <= DisplayedHarmonics; i++)
        {
            float amplitude = i == 1 ? 1f : 1f / i; // Default saw profile
            Harmonics.Add(new HarmonicBarViewModel(i, amplitude, OnHarmonicAmplitudeChanged));
        }
    }

    private void LoadFromSynth()
    {
        if (_synth == null) return;

        // Load parameters from synth
        Bandwidth = _synth.Bandwidth;
        BandwidthScale = _synth.BandwidthScale;
        Detune = _synth.Detune;
        UnisonVoices = _synth.UnisonVoices;
        UnisonDetune = _synth.UnisonDetune;
        UnisonSpread = _synth.UnisonSpread;
        Attack = _synth.AmpEnvelope.Attack;
        Decay = _synth.AmpEnvelope.Decay;
        Sustain = _synth.AmpEnvelope.Sustain;
        Release = _synth.AmpEnvelope.Release;
        Volume = _synth.Volume;
        Seed = _synth.Seed;
        SelectedProfile = _synth.Profile;

        // Load harmonic amplitudes
        var amplitudes = _synth.GetHarmonics();
        for (int i = 0; i < Math.Min(DisplayedHarmonics, amplitudes.Length); i++)
        {
            Harmonics[i].Amplitude = amplitudes[i];
        }

        UpdateWaveformPreview();
        GenerationStatus = "Loaded";
    }

    private void OnHarmonicAmplitudeChanged(int harmonic, float amplitude)
    {
        _synth?.SetHarmonic(harmonic, amplitude);
        SelectedProfile = PadHarmonicProfile.Custom;
    }

    partial void OnBandwidthChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Bandwidth = value;
        }
    }

    partial void OnBandwidthScaleChanged(float value)
    {
        if (_synth != null)
        {
            _synth.BandwidthScale = value;
        }
    }

    partial void OnDetuneChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Detune = value;
        }
    }

    partial void OnUnisonVoicesChanged(int value)
    {
        if (_synth != null)
        {
            _synth.UnisonVoices = value;
        }
    }

    partial void OnUnisonDetuneChanged(float value)
    {
        if (_synth != null)
        {
            _synth.UnisonDetune = value;
        }
    }

    partial void OnUnisonSpreadChanged(float value)
    {
        if (_synth != null)
        {
            _synth.UnisonSpread = value;
        }
    }

    partial void OnAttackChanged(double value)
    {
        if (_synth != null)
        {
            _synth.AmpEnvelope.Attack = value;
        }
    }

    partial void OnDecayChanged(double value)
    {
        if (_synth != null)
        {
            _synth.AmpEnvelope.Decay = value;
        }
    }

    partial void OnSustainChanged(double value)
    {
        if (_synth != null)
        {
            _synth.AmpEnvelope.Sustain = value;
        }
    }

    partial void OnReleaseChanged(double value)
    {
        if (_synth != null)
        {
            _synth.AmpEnvelope.Release = value;
        }
    }

    partial void OnVolumeChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Volume = value;
        }
    }

    partial void OnSeedChanged(int value)
    {
        if (_synth != null)
        {
            _synth.Seed = value;
        }
    }

    partial void OnSelectedProfileChanged(PadHarmonicProfile value)
    {
        if (_synth != null && value != PadHarmonicProfile.Custom)
        {
            _synth.Profile = value;
            // Reload harmonic amplitudes after profile change
            var amplitudes = _synth.GetHarmonics();
            for (int i = 0; i < Math.Min(DisplayedHarmonics, amplitudes.Length); i++)
            {
                // Temporarily detach handler
                Harmonics[i].Amplitude = amplitudes[i];
            }
        }
    }

    /// <summary>
    /// Generates the wavetable with progress indication.
    /// </summary>
    [RelayCommand]
    private async Task GenerateWavetableAsync()
    {
        if (_synth == null)
        {
            Initialize();
        }

        // Cancel any existing generation
        _generationCts?.Cancel();
        _generationCts = new CancellationTokenSource();

        IsGenerating = true;
        GenerationProgress = 0;
        GenerationStatus = "Generating wavetable...";

        try
        {
            var token = _generationCts.Token;

            // Simulate progress updates during generation
            var progressTask = Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 2)
                {
                    if (token.IsCancellationRequested) break;
                    await Task.Delay(10, token);
                    GenerationProgress = i;
                }
            }, token);

            // Perform actual generation on background thread
            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                _synth!.GenerateWavetable();
            }, token);

            await progressTask;

            GenerationProgress = 100;
            GenerationStatus = "Wavetable generated successfully";
            UpdateWaveformPreview();
            WavetableChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            GenerationStatus = "Generation cancelled";
        }
        catch (Exception ex)
        {
            GenerationStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsGenerating = false;
        }
    }

    /// <summary>
    /// Cancels the current wavetable generation.
    /// </summary>
    [RelayCommand]
    private void CancelGeneration()
    {
        _generationCts?.Cancel();
        GenerationStatus = "Cancelled";
    }

    /// <summary>
    /// Resets all harmonics to the selected profile.
    /// </summary>
    [RelayCommand]
    private void ResetHarmonics()
    {
        if (_synth != null)
        {
            _synth.Profile = SelectedProfile;
            var amplitudes = _synth.GetHarmonics();
            for (int i = 0; i < Math.Min(DisplayedHarmonics, amplitudes.Length); i++)
            {
                Harmonics[i].Amplitude = amplitudes[i];
            }
        }
        GenerationStatus = "Harmonics reset to profile";
    }

    /// <summary>
    /// Clears all harmonics (sets to zero).
    /// </summary>
    [RelayCommand]
    private void ClearHarmonics()
    {
        foreach (var harmonic in Harmonics)
        {
            harmonic.Amplitude = 0;
        }
        SelectedProfile = PadHarmonicProfile.Custom;
        GenerationStatus = "Harmonics cleared";
    }

    /// <summary>
    /// Randomizes the seed for different variations.
    /// </summary>
    [RelayCommand]
    private void RandomizeSeed()
    {
        Seed = Random.Shared.Next(1, 10000);
        GenerationStatus = $"New seed: {Seed}";
    }

    /// <summary>
    /// Loads a preset configuration.
    /// </summary>
    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        PadSynth preset = presetName switch
        {
            "Evolving" => PadSynth.CreateEvolvingPad(),
            "Chorus" => PadSynth.CreateChorusPad(),
            "Choir" => PadSynth.CreateChoirPad(),
            "Ambient" => PadSynth.CreateAmbientPad(),
            _ => new PadSynth()
        };

        _synth = preset;
        LoadFromSynth();
        GenerationStatus = $"Loaded preset: {presetName}";
    }

    /// <summary>
    /// Triggers a note for preview.
    /// </summary>
    [RelayCommand]
    private void PreviewNote(int? noteParameter)
    {
        int note = noteParameter ?? 60; // Default to C4
        _synth?.AllNotesOff();
        _synth?.NoteOn(note, 100);
    }

    /// <summary>
    /// Stops the preview note.
    /// </summary>
    [RelayCommand]
    private void StopPreview()
    {
        _synth?.AllNotesOff();
    }

    private void UpdateWaveformPreview()
    {
        if (_synth == null)
        {
            HasWaveform = false;
            return;
        }

        // Generate waveform display points
        var points = new PointCollection();
        const int displayWidth = 400;
        const int displayHeight = 100;
        const double centerY = displayHeight / 2.0;

        // Read a sample of the wavetable for display
        var buffer = new float[displayWidth * 2];
        int samplesRead = _synth.Read(buffer, 0, buffer.Length);

        if (samplesRead > 0)
        {
            for (int i = 0; i < displayWidth; i++)
            {
                int sampleIndex = i * 2; // Stereo, take left channel
                float sample = sampleIndex < buffer.Length ? buffer[sampleIndex] : 0;
                double y = centerY - (sample * centerY * 0.9);
                points.Add(new System.Windows.Point(i, y));
            }
        }

        WaveformPoints = points;
        HasWaveform = points.Count > 0;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _generationCts?.Cancel();
        _generationCts?.Dispose();
        _synth?.AllNotesOff();
    }
}
