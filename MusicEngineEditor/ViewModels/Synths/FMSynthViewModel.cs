// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the FM Synthesizer Editor.

using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents the visual connection data for an FM algorithm.
/// </summary>
public class AlgorithmConnection
{
    /// <summary>
    /// Source operator index (0-5).
    /// </summary>
    public int FromOperator { get; set; }

    /// <summary>
    /// Destination operator index (0-5), or -1 for output.
    /// </summary>
    public int ToOperator { get; set; }

    /// <summary>
    /// Whether this is a feedback connection.
    /// </summary>
    public bool IsFeedback { get; set; }
}

/// <summary>
/// ViewModel for a single FM operator.
/// </summary>
public partial class FMOperatorViewModel : ObservableObject
{
    private readonly FMSynth _synth;
    private readonly int _operatorIndex;

    [ObservableProperty]
    private int _operatorNumber;

    [ObservableProperty]
    private float _ratio = 1.0f;

    [ObservableProperty]
    private float _level = 1.0f;

    [ObservableProperty]
    private float _detune = 0f;

    [ObservableProperty]
    private float _feedback = 0f;

    [ObservableProperty]
    private float _attack = 0.01f;

    [ObservableProperty]
    private float _decay = 0.1f;

    [ObservableProperty]
    private float _sustain = 0.7f;

    [ObservableProperty]
    private float _release = 0.3f;

    [ObservableProperty]
    private float _velocitySensitivity = 0.5f;

    [ObservableProperty]
    private float _keyScaling = 0f;

    [ObservableProperty]
    private float _modulationIndex = 1.0f;

    [ObservableProperty]
    private FMWaveform _waveform = FMWaveform.Sine;

    [ObservableProperty]
    private bool _isCarrier;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private Color _operatorColor;

    /// <summary>
    /// Gets the display name for the operator.
    /// </summary>
    public string DisplayName => $"OP{OperatorNumber}";

    /// <summary>
    /// Gets the available waveform options.
    /// </summary>
    public static IReadOnlyList<FMWaveform> AvailableWaveforms { get; } =
        Enum.GetValues<FMWaveform>();

    /// <summary>
    /// Gets the common ratio presets.
    /// </summary>
    public static IReadOnlyList<float> RatioPresets { get; } = new[]
    {
        0.5f, 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 4.0f,
        5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f, 11.0f, 12.0f,
        13.0f, 14.0f, 15.0f, 16.0f
    };

    public FMOperatorViewModel(FMSynth synth, int operatorIndex)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        _operatorIndex = operatorIndex;
        _operatorNumber = operatorIndex + 1;

        // Set operator color based on index
        _operatorColor = operatorIndex switch
        {
            0 => Color.FromRgb(0xFF, 0x6B, 0x6B),  // Red - Operator 1
            1 => Color.FromRgb(0xFF, 0xA5, 0x00),  // Orange - Operator 2
            2 => Color.FromRgb(0xFF, 0xD9, 0x3D),  // Yellow - Operator 3
            3 => Color.FromRgb(0x6B, 0xFF, 0x6B),  // Green - Operator 4
            4 => Color.FromRgb(0x00, 0xD9, 0xFF),  // Cyan - Operator 5
            5 => Color.FromRgb(0xBB, 0x6B, 0xFF),  // Purple - Operator 6
            _ => Color.FromRgb(0x80, 0x80, 0x80)
        };

        LoadFromSynth();
    }

    /// <summary>
    /// Loads parameters from the synth.
    /// </summary>
    public void LoadFromSynth()
    {
        var op = _synth.Operators[_operatorIndex];
        _ratio = op.Ratio;
        _level = op.Level;
        _detune = op.Detune;
        _feedback = op.Feedback;
        _velocitySensitivity = op.VelocitySensitivity;
        _keyScaling = op.KeyScaling;
        _waveform = op.Waveform;
        _isCarrier = op.IsCarrier;
        _isEnabled = op.Level > 0;
        _previousLevel = op.Level > 0 ? op.Level : 0.5f;

        if (op.Envelope != null)
        {
            _attack = (float)op.Envelope.Attack;
            _decay = (float)op.Envelope.Decay;
            _sustain = (float)op.Envelope.Sustain;
            _release = (float)op.Envelope.Release;
        }

        // Notify all properties changed
        OnPropertyChanged(nameof(Ratio));
        OnPropertyChanged(nameof(Level));
        OnPropertyChanged(nameof(Detune));
        OnPropertyChanged(nameof(Feedback));
        OnPropertyChanged(nameof(Attack));
        OnPropertyChanged(nameof(Decay));
        OnPropertyChanged(nameof(Sustain));
        OnPropertyChanged(nameof(Release));
        OnPropertyChanged(nameof(VelocitySensitivity));
        OnPropertyChanged(nameof(KeyScaling));
        OnPropertyChanged(nameof(Waveform));
        OnPropertyChanged(nameof(IsCarrier));
        OnPropertyChanged(nameof(IsEnabled));
    }

    partial void OnRatioChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_ratio", value);
    }

    partial void OnLevelChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_level", value);
    }

    partial void OnDetuneChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_detune", value);
    }

    partial void OnFeedbackChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_feedback", value);
    }

    partial void OnAttackChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_attack", value);
    }

    partial void OnDecayChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_decay", value);
    }

    partial void OnSustainChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_sustain", value);
    }

    partial void OnReleaseChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_release", value);
    }

    partial void OnVelocitySensitivityChanged(float value)
    {
        _synth.SetParameter($"op{_operatorNumber}_velocity", value);
    }

    partial void OnWaveformChanged(FMWaveform value)
    {
        _synth.Operators[_operatorIndex].Waveform = value;
    }

    partial void OnModulationIndexChanged(float value)
    {
        // Modulation index affects the effective modulation depth
        // This is a virtual parameter that scales the level for modulator operators
        // Store for UI display - the actual modulation is controlled by Level
    }

    partial void OnIsEnabledChanged(bool value)
    {
        // When disabled, set level to 0; when enabled, restore to previous or default
        if (!value)
        {
            _previousLevel = Level;
            Level = 0f;
        }
        else if (Level == 0f)
        {
            Level = _previousLevel > 0 ? _previousLevel : 0.5f;
        }
    }

    private float _previousLevel = 0.5f;

    [RelayCommand]
    private void SetRatioPreset(float ratio)
    {
        Ratio = ratio;
    }

    [RelayCommand]
    private void ResetEnvelope()
    {
        Attack = 0.01f;
        Decay = 0.1f;
        Sustain = 0.7f;
        Release = 0.3f;
    }

    [RelayCommand]
    private void ResetOperator()
    {
        Ratio = 1.0f;
        Level = _operatorIndex == 0 ? 1.0f : 0.5f;
        Detune = 0f;
        Feedback = 0f;
        VelocitySensitivity = 0.5f;
        KeyScaling = 0f;
        Waveform = FMWaveform.Sine;
        ResetEnvelope();
    }
}

/// <summary>
/// ViewModel for the FM Synthesizer Editor.
/// </summary>
public partial class FMSynthViewModel : ViewModelBase, IDisposable
{
    private FMSynth? _synth;
    private bool _disposed;

    [ObservableProperty]
    private FMAlgorithm _selectedAlgorithm = FMAlgorithm.Stack6;

    [ObservableProperty]
    private float _masterVolume = 0.5f;

    [ObservableProperty]
    private float _globalFeedback = 1.0f;

    [ObservableProperty]
    private float _pitchBendRange = 2.0f;

    [ObservableProperty]
    private float _vibratoDepth = 0f;

    [ObservableProperty]
    private float _masterDetune = 0f;

    [ObservableProperty]
    private string _presetName = "Init";

    [ObservableProperty]
    private FMOperatorViewModel? _selectedOperator;

    /// <summary>
    /// Gets the collection of operator view models.
    /// </summary>
    public ObservableCollection<FMOperatorViewModel> Operators { get; } = new();

    /// <summary>
    /// Gets the collection of connections for the current algorithm.
    /// </summary>
    public ObservableCollection<AlgorithmConnection> AlgorithmConnections { get; } = new();

    /// <summary>
    /// Gets the available FM algorithms.
    /// </summary>
    public static IReadOnlyList<FMAlgorithm> AvailableAlgorithms { get; } =
        Enum.GetValues<FMAlgorithm>();

    /// <summary>
    /// Gets the algorithm descriptions.
    /// </summary>
    public static IReadOnlyDictionary<FMAlgorithm, string> AlgorithmDescriptions { get; } =
        new Dictionary<FMAlgorithm, string>
        {
            { FMAlgorithm.Stack6, "6->5->4->3->2->1 (Series)" },
            { FMAlgorithm.Split2_4, "(6->5) + (4->3->2->1)" },
            { FMAlgorithm.Split3_3, "(6->5->4) + (3->2->1)" },
            { FMAlgorithm.Triple, "(6->5) + (4->3) + (2->1)" },
            { FMAlgorithm.ModSplit, "6->5->(4->3->2->1)" },
            { FMAlgorithm.Split4_2, "(6->5->4->3) + (2->1)" },
            { FMAlgorithm.TripleMod, "6->(5+4+3)->2->1" },
            { FMAlgorithm.DualPath, "4->3->2->1, 6->5->1" },
            { FMAlgorithm.AllParallel, "All Parallel (6 Carriers)" },
            { FMAlgorithm.DualStack, "(6->5->4) + (3->2->1)" },
            { FMAlgorithm.StackWithFB, "(6->5)->4->3->2->1" },
            { FMAlgorithm.OneToThree, "6->5->4->(3+2+1)" },
            { FMAlgorithm.TwoToThree, "6->(5+4)->(3+2+1)" },
            { FMAlgorithm.TwoByTwo, "(6+5)->(4+3)->(2+1)" },
            { FMAlgorithm.ThreePairs, "(6->5) + (4->3) + (2->1)" },
            { FMAlgorithm.EPiano, "Electric Piano" },
            { FMAlgorithm.Brass, "Brass" },
            { FMAlgorithm.Bells, "Bells/Chimes" },
            { FMAlgorithm.Organ, "Organ" },
            { FMAlgorithm.Bass, "Bass" }
        };

    /// <summary>
    /// Event raised when a note should be played for preview.
    /// </summary>
    public event EventHandler<int>? NotePreviewRequested;

    /// <summary>
    /// Event raised when note preview should stop.
    /// </summary>
    public event EventHandler? NotePreviewStopRequested;

    public FMSynthViewModel()
    {
        // Design-time constructor
    }

    public FMSynthViewModel(FMSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes with a new FMSynth instance.
    /// </summary>
    public void Initialize(int? sampleRate = null)
    {
        _synth = new FMSynth(sampleRate);
        LoadFromSynth();
    }

    /// <summary>
    /// Loads the current state from the synth.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        Operators.Clear();
        for (int i = 0; i < 6; i++)
        {
            var opVm = new FMOperatorViewModel(_synth, i);
            Operators.Add(opVm);
        }

        _selectedAlgorithm = _synth.Algorithm;
        _masterVolume = _synth.Volume;
        _globalFeedback = _synth.FeedbackAmount;
        _pitchBendRange = _synth.PitchBendRange;
        _vibratoDepth = _synth.VibratoDepth;
        _presetName = _synth.Name;

        OnPropertyChanged(nameof(SelectedAlgorithm));
        OnPropertyChanged(nameof(MasterVolume));
        OnPropertyChanged(nameof(GlobalFeedback));
        OnPropertyChanged(nameof(PitchBendRange));
        OnPropertyChanged(nameof(VibratoDepth));
        OnPropertyChanged(nameof(PresetName));

        UpdateAlgorithmConnections();

        if (Operators.Count > 0)
        {
            SelectedOperator = Operators[0];
        }
    }

    /// <summary>
    /// Updates the algorithm connection diagram data.
    /// </summary>
    private void UpdateAlgorithmConnections()
    {
        AlgorithmConnections.Clear();

        // Define connections based on algorithm
        var connections = GetAlgorithmConnections(SelectedAlgorithm);
        foreach (var conn in connections)
        {
            AlgorithmConnections.Add(conn);
        }

        // Update carrier status on operators
        foreach (var op in Operators)
        {
            op.LoadFromSynth();
        }
    }

    /// <summary>
    /// Gets the connection definitions for an algorithm.
    /// </summary>
    private static List<AlgorithmConnection> GetAlgorithmConnections(FMAlgorithm algorithm)
    {
        var connections = new List<AlgorithmConnection>();

        switch (algorithm)
        {
            case FMAlgorithm.Stack6:
                // 6->5->4->3->2->1
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 3 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 }); // Output
                break;

            case FMAlgorithm.Split2_4:
                // (6->5) + (4->3->2->1)
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.Split3_3:
                // (6->5->4) + (3->2->1)
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 3 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.Triple:
            case FMAlgorithm.ThreePairs:
                // (6->5) + (4->3) + (2->1)
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.AllParallel:
                // All carriers
                for (int i = 0; i < 6; i++)
                {
                    connections.Add(new AlgorithmConnection { FromOperator = i, ToOperator = -1 });
                }
                break;

            case FMAlgorithm.DualStack:
                // (6->5->4) + (3->2->1)
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 3 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.OneToThree:
                // 6->5->4->(3+2+1)
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 4 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 3 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.EPiano:
            case FMAlgorithm.Brass:
            case FMAlgorithm.Bass:
                // Two carriers with modulators
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 3 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.Bells:
                // Complex modulation for metallic sounds
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            case FMAlgorithm.Organ:
                // Mostly parallel with cross-modulation
                connections.Add(new AlgorithmConnection { FromOperator = 5, ToOperator = 2 });
                connections.Add(new AlgorithmConnection { FromOperator = 4, ToOperator = 1 });
                connections.Add(new AlgorithmConnection { FromOperator = 3, ToOperator = 0 });
                connections.Add(new AlgorithmConnection { FromOperator = 2, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 1, ToOperator = -1 });
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;

            default:
                // Default: series
                for (int i = 5; i > 0; i--)
                {
                    connections.Add(new AlgorithmConnection { FromOperator = i, ToOperator = i - 1 });
                }
                connections.Add(new AlgorithmConnection { FromOperator = 0, ToOperator = -1 });
                break;
        }

        return connections;
    }

    partial void OnSelectedAlgorithmChanged(FMAlgorithm value)
    {
        _synth?.SetAlgorithm(value);
        UpdateAlgorithmConnections();
    }

    partial void OnMasterVolumeChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Volume = value;
        }
    }

    partial void OnGlobalFeedbackChanged(float value)
    {
        if (_synth != null)
        {
            _synth.FeedbackAmount = value;
        }
    }

    partial void OnPitchBendRangeChanged(float value)
    {
        if (_synth != null)
        {
            _synth.PitchBendRange = value;
        }
    }

    partial void OnVibratoDepthChanged(float value)
    {
        if (_synth != null)
        {
            _synth.VibratoDepth = value;
        }
    }

    [RelayCommand]
    private void SelectOperator(FMOperatorViewModel? op)
    {
        SelectedOperator = op;
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        if (_synth == null) return;

        // Dispose old synth and create new preset
        var newSynth = presetName.ToLowerInvariant() switch
        {
            "epiano" or "e-piano" => FMSynth.CreateEPianoPreset(),
            "brass" => FMSynth.CreateBrassPreset(),
            "bell" or "bells" => FMSynth.CreateBellPreset(),
            "bass" => FMSynth.CreateBassPreset(),
            "organ" => FMSynth.CreateOrganPreset(),
            _ => new FMSynth()
        };

        _synth = newSynth;
        LoadFromSynth();
        PresetName = _synth.Name;
        StatusMessage = $"Loaded preset: {PresetName}";
    }

    [RelayCommand]
    private void InitPatch()
    {
        if (_synth == null)
        {
            Initialize();
        }
        else
        {
            _synth = new FMSynth();
            LoadFromSynth();
        }
        PresetName = "Init";
        StatusMessage = "Initialized patch";
    }

    [RelayCommand]
    private void RandomizePatch()
    {
        if (_synth == null) return;

        var random = new Random();

        // Randomize algorithm
        var algorithms = AvailableAlgorithms;
        SelectedAlgorithm = algorithms[random.Next(algorithms.Count)];

        // Randomize operators
        foreach (var op in Operators)
        {
            op.Ratio = FMOperatorViewModel.RatioPresets[random.Next(FMOperatorViewModel.RatioPresets.Count)];
            op.Level = (float)(random.NextDouble() * 0.8 + 0.2);
            op.Detune = (float)(random.NextDouble() * 20 - 10);
            op.Feedback = (float)(random.NextDouble() * 0.3);
            op.Attack = (float)(random.NextDouble() * 0.5 + 0.001);
            op.Decay = (float)(random.NextDouble() * 0.5 + 0.05);
            op.Sustain = (float)(random.NextDouble() * 0.7 + 0.2);
            op.Release = (float)(random.NextDouble() * 1.0 + 0.05);
        }

        PresetName = "Random";
        StatusMessage = "Randomized patch";
    }

    [RelayCommand]
    private void PreviewNote(int note)
    {
        NotePreviewRequested?.Invoke(this, note);
    }

    [RelayCommand]
    private void StopPreview()
    {
        NotePreviewStopRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CopyOperator(int sourceIndex)
    {
        if (SelectedOperator == null || sourceIndex < 0 || sourceIndex >= 6) return;

        var source = Operators[sourceIndex];
        SelectedOperator.Ratio = source.Ratio;
        SelectedOperator.Level = source.Level;
        SelectedOperator.Detune = source.Detune;
        SelectedOperator.Feedback = source.Feedback;
        SelectedOperator.Attack = source.Attack;
        SelectedOperator.Decay = source.Decay;
        SelectedOperator.Sustain = source.Sustain;
        SelectedOperator.Release = source.Release;
        SelectedOperator.VelocitySensitivity = source.VelocitySensitivity;
        SelectedOperator.Waveform = source.Waveform;

        StatusMessage = $"Copied OP{sourceIndex + 1} to OP{SelectedOperator.OperatorNumber}";
    }

    /// <summary>
    /// Gets the underlying FMSynth instance.
    /// </summary>
    public FMSynth? GetSynth() => _synth;

    /// <summary>
    /// Triggers a note on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
    }

    /// <summary>
    /// Triggers a note off.
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        AllNotesOff();
        GC.SuppressFinalize(this);
    }
}
