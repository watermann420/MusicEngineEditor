// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Drum Synth Editor control.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;
using NAudio.Wave;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a drum pad in the UI.
/// </summary>
public partial class DrumPadViewModel : ObservableObject
{
    /// <summary>
    /// The drum sound type.
    /// </summary>
    [ObservableProperty]
    private DrumSound _drumSound;

    /// <summary>
    /// Display name.
    /// </summary>
    [ObservableProperty]
    private string _name = "Kick";

    /// <summary>
    /// MIDI note number.
    /// </summary>
    [ObservableProperty]
    private int _midiNote = 36;

    /// <summary>
    /// Pad color for UI.
    /// </summary>
    [ObservableProperty]
    private string _color = "#DC143C";

    /// <summary>
    /// Whether this pad is currently triggered (for visual feedback).
    /// </summary>
    [ObservableProperty]
    private bool _isTriggered;

    /// <summary>
    /// Whether this pad is selected for editing.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    public DrumPadViewModel(DrumSound sound, string name, int midiNote, string color)
    {
        DrumSound = sound;
        Name = name;
        MidiNote = midiNote;
        Color = color;
    }
}

/// <summary>
/// ViewModel for the Drum Synth Editor control.
/// Provides a visual editor for the MusicEngine DrumSynth with per-type controls,
/// preset buttons, waveform preview, and pitch envelope display.
/// </summary>
public partial class DrumSynthViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private DrumSynth? _drumSynth;
    private WaveOutEvent? _waveOut;
    private bool _isInitialized;
    private bool _disposed;
    private readonly object _lock = new();
    private CancellationTokenSource? _triggerAnimationCts;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the drum pad collection.
    /// </summary>
    public ObservableCollection<DrumPadViewModel> DrumPads { get; } = new();

    /// <summary>
    /// Gets the waveform preview data (samples).
    /// </summary>
    [ObservableProperty]
    private float[] _waveformData = Array.Empty<float>();

    /// <summary>
    /// Gets the pitch envelope preview data.
    /// </summary>
    [ObservableProperty]
    private float[] _pitchEnvelopeData = Array.Empty<float>();

    #endregion

    #region Selected Drum Properties

    /// <summary>
    /// Currently selected drum pad.
    /// </summary>
    [ObservableProperty]
    private DrumPadViewModel? _selectedPad;

    /// <summary>
    /// Name of the currently selected drum sound.
    /// </summary>
    [ObservableProperty]
    private string _selectedDrumName = "Kick";

    #endregion

    #region Output Properties

    /// <summary>
    /// Master volume (0-1).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.8f;

    /// <summary>
    /// Master pan (-1 to 1).
    /// </summary>
    [ObservableProperty]
    private float _pan;

    #endregion

    #region Kick Parameters

    /// <summary>
    /// Kick pitch in Hz.
    /// </summary>
    [ObservableProperty]
    private float _kickPitch = 55f;

    /// <summary>
    /// Kick pitch envelope decay rate.
    /// </summary>
    [ObservableProperty]
    private float _kickPitchDecay = 30f;

    /// <summary>
    /// Kick pitch envelope amount.
    /// </summary>
    [ObservableProperty]
    private float _kickPitchAmount = 3f;

    /// <summary>
    /// Kick click level.
    /// </summary>
    [ObservableProperty]
    private float _kickClick = 0.3f;

    /// <summary>
    /// Kick sub oscillator level.
    /// </summary>
    [ObservableProperty]
    private float _kickSub = 0.5f;

    /// <summary>
    /// Kick decay time.
    /// </summary>
    [ObservableProperty]
    private float _kickDecay = 8f;

    /// <summary>
    /// Kick drive/saturation.
    /// </summary>
    [ObservableProperty]
    private float _kickDrive = 0.2f;

    #endregion

    #region Snare Parameters

    /// <summary>
    /// Snare body tone frequency.
    /// </summary>
    [ObservableProperty]
    private float _snareBody = 200f;

    /// <summary>
    /// Snare snap/noise level.
    /// </summary>
    [ObservableProperty]
    private float _snareSnap = 0.6f;

    /// <summary>
    /// Snare tone level.
    /// </summary>
    [ObservableProperty]
    private float _snareTone = 0.5f;

    /// <summary>
    /// Snare decay time.
    /// </summary>
    [ObservableProperty]
    private float _snareDecay = 15f;

    #endregion

    #region HiHat Parameters

    /// <summary>
    /// Hi-hat metallic tone frequency.
    /// </summary>
    [ObservableProperty]
    private float _hiHatTone = 8000f;

    /// <summary>
    /// Closed hi-hat decay.
    /// </summary>
    [ObservableProperty]
    private float _hiHatClosedDecay = 40f;

    /// <summary>
    /// Open hi-hat decay.
    /// </summary>
    [ObservableProperty]
    private float _hiHatOpenDecay = 8f;

    /// <summary>
    /// Whether hi-hat is open (true) or closed (false).
    /// </summary>
    [ObservableProperty]
    private bool _hiHatIsOpen;

    /// <summary>
    /// Hi-hat noise color (0 = dark/metallic, 1 = bright/white).
    /// </summary>
    [ObservableProperty]
    private float _hiHatNoiseColor = 0.5f;

    #endregion

    #region Clap Parameters

    /// <summary>
    /// Clap noise decay.
    /// </summary>
    [ObservableProperty]
    private float _clapDecay = 18f;

    /// <summary>
    /// Clap spread (number of layers, 1-8).
    /// </summary>
    [ObservableProperty]
    private float _clapSpread = 4f;

    /// <summary>
    /// Clap room amount (reverb/ambience).
    /// </summary>
    [ObservableProperty]
    private float _clapRoom = 0.3f;

    #endregion

    #region Cymbal Parameters

    /// <summary>
    /// Cymbal tone frequency.
    /// </summary>
    [ObservableProperty]
    private float _cymbalTone = 6000f;

    /// <summary>
    /// Cymbal decay time.
    /// </summary>
    [ObservableProperty]
    private float _cymbalDecay = 2f;

    /// <summary>
    /// Cymbal brightness (noise filter).
    /// </summary>
    [ObservableProperty]
    private float _cymbalBrightness = 0.5f;

    /// <summary>
    /// Whether cymbal type is Crash.
    /// </summary>
    [ObservableProperty]
    private bool _cymbalIsCrash = true;

    /// <summary>
    /// Whether cymbal type is Ride.
    /// </summary>
    [ObservableProperty]
    private bool _cymbalIsRide;

    #endregion

    #region Tom Parameters

    /// <summary>
    /// Base tom frequency.
    /// </summary>
    [ObservableProperty]
    private float _tomPitch = 100f;

    /// <summary>
    /// Tom decay time.
    /// </summary>
    [ObservableProperty]
    private float _tomDecay = 10f;

    #endregion

    #region Preset Properties

    /// <summary>
    /// Currently active preset name.
    /// </summary>
    [ObservableProperty]
    private string _currentPreset = "Default";

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new DrumSynthViewModel.
    /// </summary>
    public DrumSynthViewModel()
    {
        InitializeDrumPads();
        Initialize();
    }

    #endregion

    #region Initialization

    private void InitializeDrumPads()
    {
        DrumPads.Clear();

        // Main drum sounds with distinct colors
        DrumPads.Add(new DrumPadViewModel(DrumSound.Kick, "Kick", 36, "#DC143C"));          // Crimson
        DrumPads.Add(new DrumPadViewModel(DrumSound.Snare, "Snare", 38, "#FFD700"));        // Gold
        DrumPads.Add(new DrumPadViewModel(DrumSound.HiHatClosed, "Closed HH", 42, "#00D9FF")); // Cyan
        DrumPads.Add(new DrumPadViewModel(DrumSound.HiHatOpen, "Open HH", 46, "#00BFFF"));  // Deep Sky Blue
        DrumPads.Add(new DrumPadViewModel(DrumSound.Clap, "Clap", 39, "#FF69B4"));          // Hot Pink
        DrumPads.Add(new DrumPadViewModel(DrumSound.TomLow, "Low Tom", 45, "#FF8C00"));     // Dark Orange
        DrumPads.Add(new DrumPadViewModel(DrumSound.TomMid, "Mid Tom", 47, "#FFA500"));     // Orange
        DrumPads.Add(new DrumPadViewModel(DrumSound.TomHigh, "Hi Tom", 50, "#00FF88"));     // Spring Green

        // Select kick by default
        if (DrumPads.Count > 0)
        {
            SelectPad(DrumPads[0]);
        }
    }

    private void Initialize()
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                // Create the drum synth
                _drumSynth = new DrumSynth();
                _drumSynth.Volume = Volume;

                // Create audio output
                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50
                };
                _waveOut.Init(_drumSynth);
                _waveOut.Play();

                _isInitialized = true;
                UpdateWaveformPreview();

                System.Diagnostics.Debug.WriteLine("[DrumSynthViewModel] Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DrumSynthViewModel] Initialization failed: {ex.Message}");
                Cleanup();
            }
        }
    }

    #endregion

    #region Pad Selection

    /// <summary>
    /// Selects a drum pad for editing.
    /// </summary>
    [RelayCommand]
    private void SelectPad(DrumPadViewModel? pad)
    {
        if (pad == null) return;

        // Deselect previous
        if (SelectedPad != null)
        {
            SelectedPad.IsSelected = false;
        }

        SelectedPad = pad;
        pad.IsSelected = true;
        SelectedDrumName = pad.Name;

        UpdateWaveformPreview();
    }

    #endregion

    #region Trigger Commands

    /// <summary>
    /// Triggers a drum sound.
    /// </summary>
    [RelayCommand]
    private async Task TriggerDrum(DrumPadViewModel? pad)
    {
        if (pad == null || _drumSynth == null) return;

        // Visual feedback
        pad.IsTriggered = true;

        // Play the sound
        _drumSynth.Play(pad.DrumSound, 100);

        // Reset visual state after short delay
        _triggerAnimationCts?.Cancel();
        _triggerAnimationCts = new CancellationTokenSource();

        try
        {
            await Task.Delay(100, _triggerAnimationCts.Token);
            pad.IsTriggered = false;
        }
        catch (TaskCanceledException)
        {
            // Expected when cancelled
        }
    }

    /// <summary>
    /// Triggers the currently selected drum sound.
    /// </summary>
    [RelayCommand]
    private async Task TriggerSelected()
    {
        if (SelectedPad != null)
        {
            await TriggerDrum(SelectedPad);
        }
    }

    /// <summary>
    /// Stops all playing sounds.
    /// </summary>
    [RelayCommand]
    private void StopAll()
    {
        _drumSynth?.AllNotesOff();
    }

    #endregion

    #region Preset Commands

    /// <summary>
    /// Loads the 808 preset.
    /// </summary>
    [RelayCommand]
    private void Load808Preset()
    {
        // Apply 808 parameters
        KickPitch = 45f;
        KickPitchDecay = 25f;
        KickPitchAmount = 4f;
        KickClick = 0.2f;
        KickSub = 0.7f;
        KickDecay = 6f;
        KickDrive = 0.1f;

        SnareBody = 180f;
        SnareSnap = 0.7f;
        SnareTone = 0.4f;
        SnareDecay = 12f;

        HiHatTone = 9000f;
        HiHatClosedDecay = 50f;
        HiHatOpenDecay = 10f;

        ClapDecay = 20f;

        CurrentPreset = "808";
        ApplyAllParameters();
        UpdateWaveformPreview();
        StatusMessage = "Loaded 808 preset";
    }

    /// <summary>
    /// Loads the 909 preset.
    /// </summary>
    [RelayCommand]
    private void Load909Preset()
    {
        // Apply 909 parameters
        KickPitch = 55f;
        KickPitchDecay = 35f;
        KickPitchAmount = 3f;
        KickClick = 0.4f;
        KickSub = 0.4f;
        KickDecay = 8f;
        KickDrive = 0.3f;

        SnareBody = 220f;
        SnareSnap = 0.5f;
        SnareTone = 0.6f;
        SnareDecay = 18f;

        HiHatTone = 7500f;
        HiHatClosedDecay = 35f;
        HiHatOpenDecay = 6f;

        ClapDecay = 15f;

        CurrentPreset = "909";
        ApplyAllParameters();
        UpdateWaveformPreview();
        StatusMessage = "Loaded 909 preset";
    }

    /// <summary>
    /// Loads the LinnDrum preset.
    /// </summary>
    [RelayCommand]
    private void LoadLinnDrumPreset()
    {
        // Apply LinnDrum parameters (digital, punchy, 80s style)
        KickPitch = 60f;
        KickPitchDecay = 40f;
        KickPitchAmount = 2.5f;
        KickClick = 0.6f;
        KickSub = 0.35f;
        KickDecay = 7f;
        KickDrive = 0.15f;

        SnareBody = 240f;
        SnareSnap = 0.55f;
        SnareTone = 0.65f;
        SnareDecay = 16f;

        HiHatTone = 8500f;
        HiHatClosedDecay = 45f;
        HiHatOpenDecay = 7f;
        HiHatNoiseColor = 0.6f;

        ClapDecay = 16f;
        ClapSpread = 3f;
        ClapRoom = 0.4f;

        TomPitch = 110f;
        TomDecay = 11f;

        CymbalTone = 7000f;
        CymbalDecay = 2.5f;
        CymbalBrightness = 0.55f;

        CurrentPreset = "LinnDrum";
        ApplyAllParameters();
        UpdateWaveformPreview();
        StatusMessage = "Loaded LinnDrum preset";
    }

    /// <summary>
    /// Resets to default parameters.
    /// </summary>
    [RelayCommand]
    private void ResetToDefault()
    {
        KickPitch = 55f;
        KickPitchDecay = 30f;
        KickPitchAmount = 3f;
        KickClick = 0.3f;
        KickSub = 0.5f;
        KickDecay = 8f;
        KickDrive = 0.2f;

        SnareBody = 200f;
        SnareSnap = 0.6f;
        SnareTone = 0.5f;
        SnareDecay = 15f;

        HiHatTone = 8000f;
        HiHatClosedDecay = 40f;
        HiHatOpenDecay = 8f;

        ClapDecay = 18f;

        TomPitch = 100f;
        TomDecay = 10f;

        Volume = 0.8f;
        Pan = 0f;

        CurrentPreset = "Default";
        ApplyAllParameters();
        UpdateWaveformPreview();
        StatusMessage = "Reset to default";
    }

    #endregion

    #region Parameter Application

    private void ApplyAllParameters()
    {
        if (_drumSynth == null) return;

        // Kick
        _drumSynth.KickPitch = KickPitch;
        _drumSynth.KickPitchDecay = KickPitchDecay;
        _drumSynth.KickPitchAmount = KickPitchAmount;
        _drumSynth.KickClick = KickClick;
        _drumSynth.KickSub = KickSub;
        _drumSynth.KickDecay = KickDecay;
        _drumSynth.KickDrive = KickDrive;

        // Snare
        _drumSynth.SnareBody = SnareBody;
        _drumSynth.SnareSnap = SnareSnap;
        _drumSynth.SnareTone = SnareTone;
        _drumSynth.SnareDecay = SnareDecay;

        // Hi-hat
        _drumSynth.HiHatTone = HiHatTone;
        _drumSynth.HiHatClosedDecay = HiHatClosedDecay;
        _drumSynth.HiHatOpenDecay = HiHatOpenDecay;

        // Clap
        _drumSynth.ClapDecay = ClapDecay;

        // Tom
        _drumSynth.TomPitch = TomPitch;
        _drumSynth.TomDecay = TomDecay;

        // Output
        _drumSynth.Volume = Volume;
    }

    #endregion

    #region Property Change Handlers

    partial void OnVolumeChanged(float value)
    {
        if (_drumSynth != null)
        {
            _drumSynth.Volume = value;
        }
    }

    partial void OnKickPitchChanged(float value)
    {
        _drumSynth?.SetParameter("kickpitch", value);
        UpdateWaveformPreview();
    }

    partial void OnKickPitchDecayChanged(float value)
    {
        _drumSynth?.SetParameter("kickpitchdecay", value);
        UpdateWaveformPreview();
    }

    partial void OnKickPitchAmountChanged(float value)
    {
        _drumSynth?.SetParameter("kickpitchamount", value);
        UpdateWaveformPreview();
    }

    partial void OnKickClickChanged(float value)
    {
        _drumSynth?.SetParameter("kickclick", value);
        UpdateWaveformPreview();
    }

    partial void OnKickSubChanged(float value)
    {
        _drumSynth?.SetParameter("kicksub", value);
        UpdateWaveformPreview();
    }

    partial void OnKickDecayChanged(float value)
    {
        _drumSynth?.SetParameter("kickdecay", value);
        UpdateWaveformPreview();
    }

    partial void OnKickDriveChanged(float value)
    {
        _drumSynth?.SetParameter("kickdrive", value);
        UpdateWaveformPreview();
    }

    partial void OnSnareBodyChanged(float value)
    {
        _drumSynth?.SetParameter("snarebody", value);
        UpdateWaveformPreview();
    }

    partial void OnSnareSnapChanged(float value)
    {
        _drumSynth?.SetParameter("snaresnap", value);
        UpdateWaveformPreview();
    }

    partial void OnSnareToneChanged(float value)
    {
        _drumSynth?.SetParameter("snaretone", value);
        UpdateWaveformPreview();
    }

    partial void OnSnareDecayChanged(float value)
    {
        _drumSynth?.SetParameter("snaredecay", value);
        UpdateWaveformPreview();
    }

    partial void OnHiHatToneChanged(float value)
    {
        _drumSynth?.SetParameter("hihattone", value);
        UpdateWaveformPreview();
    }

    partial void OnHiHatClosedDecayChanged(float value)
    {
        _drumSynth?.SetParameter("hihatcloseddecay", value);
        UpdateWaveformPreview();
    }

    partial void OnHiHatOpenDecayChanged(float value)
    {
        _drumSynth?.SetParameter("hihatopendecay", value);
        UpdateWaveformPreview();
    }

    partial void OnClapDecayChanged(float value)
    {
        _drumSynth?.SetParameter("clapdecay", value);
        UpdateWaveformPreview();
    }

    partial void OnTomPitchChanged(float value)
    {
        _drumSynth?.SetParameter("tompitch", value);
        UpdateWaveformPreview();
    }

    partial void OnTomDecayChanged(float value)
    {
        _drumSynth?.SetParameter("tomdecay", value);
        UpdateWaveformPreview();
    }

    #endregion

    #region Waveform Preview

    /// <summary>
    /// Updates the waveform preview for the selected drum sound.
    /// </summary>
    private void UpdateWaveformPreview()
    {
        if (_drumSynth == null || SelectedPad == null) return;

        try
        {
            // Create a temporary synth to render the preview
            var tempSynth = new DrumSynth();

            // Copy current parameters
            tempSynth.KickPitch = KickPitch;
            tempSynth.KickPitchDecay = KickPitchDecay;
            tempSynth.KickPitchAmount = KickPitchAmount;
            tempSynth.KickClick = KickClick;
            tempSynth.KickSub = KickSub;
            tempSynth.KickDecay = KickDecay;
            tempSynth.KickDrive = KickDrive;
            tempSynth.SnareBody = SnareBody;
            tempSynth.SnareSnap = SnareSnap;
            tempSynth.SnareTone = SnareTone;
            tempSynth.SnareDecay = SnareDecay;
            tempSynth.HiHatTone = HiHatTone;
            tempSynth.HiHatClosedDecay = HiHatClosedDecay;
            tempSynth.HiHatOpenDecay = HiHatOpenDecay;
            tempSynth.ClapDecay = ClapDecay;
            tempSynth.TomPitch = TomPitch;
            tempSynth.TomDecay = TomDecay;
            tempSynth.Volume = 1.0f;

            // Trigger the selected drum
            tempSynth.Play(SelectedPad.DrumSound, 100);

            // Render samples for preview (0.5 seconds at 44100 Hz)
            int sampleCount = 22050;
            float[] buffer = new float[sampleCount * 2]; // Stereo
            tempSynth.Read(buffer, 0, buffer.Length);

            // Downsample to display resolution (512 points)
            int displayPoints = 512;
            float[] displayData = new float[displayPoints];
            int samplesPerPoint = sampleCount / displayPoints;

            for (int i = 0; i < displayPoints; i++)
            {
                float maxVal = 0;
                for (int j = 0; j < samplesPerPoint; j++)
                {
                    int idx = (i * samplesPerPoint + j) * 2;
                    if (idx < buffer.Length)
                    {
                        float absVal = Math.Abs(buffer[idx]);
                        if (absVal > maxVal) maxVal = absVal;
                    }
                }
                displayData[i] = maxVal;
            }

            WaveformData = displayData;

            // Generate pitch envelope data
            GeneratePitchEnvelopePreview();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DrumSynthViewModel] Waveform preview failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates pitch envelope preview data.
    /// </summary>
    private void GeneratePitchEnvelopePreview()
    {
        if (SelectedPad == null) return;

        int displayPoints = 256;
        float[] envelopeData = new float[displayPoints];

        // Get decay rate based on selected drum
        float pitchDecay = SelectedPad.DrumSound switch
        {
            DrumSound.Kick => KickPitchDecay,
            DrumSound.Snare => 20f,
            DrumSound.TomHigh or DrumSound.TomMid or DrumSound.TomLow => 15f,
            _ => 0f
        };

        float pitchAmount = SelectedPad.DrumSound switch
        {
            DrumSound.Kick => KickPitchAmount,
            DrumSound.Snare => 0.5f,
            DrumSound.TomHigh or DrumSound.TomMid or DrumSound.TomLow => 1.5f,
            _ => 0f
        };

        // Generate exponential decay curve
        float duration = 0.5f; // 500ms preview
        for (int i = 0; i < displayPoints; i++)
        {
            float t = (float)i / displayPoints * duration;
            float env = (float)Math.Exp(-t * pitchDecay);
            envelopeData[i] = env * pitchAmount / Math.Max(1f, pitchAmount);
        }

        PitchEnvelopeData = envelopeData;
    }

    #endregion

    #region Cleanup

    private void Cleanup()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _drumSynth = null;
        _isInitialized = false;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            _triggerAnimationCts?.Cancel();
            _triggerAnimationCts?.Dispose();

            Cleanup();
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}
