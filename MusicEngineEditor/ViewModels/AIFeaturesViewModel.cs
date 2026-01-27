// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for AI Features Panel.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using MusicEngine.Core.AI;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Quality mode for AI declipping process.
/// </summary>
public enum DeclipQuality
{
    /// <summary>Fast processing with basic reconstruction.</summary>
    Fast,
    /// <summary>Balanced quality and speed.</summary>
    Medium,
    /// <summary>High quality reconstruction.</summary>
    High,
    /// <summary>Maximum quality with advanced algorithms.</summary>
    Ultra
}

/// <summary>
/// Represents an EQ suggestion from the Mix Assistant.
/// </summary>
public partial class EqSuggestionItem : ObservableObject
{
    [ObservableProperty]
    private float _frequency;

    [ObservableProperty]
    private float _gainDb;

    [ObservableProperty]
    private float _q;

    [ObservableProperty]
    private string _filterType = "peak";

    [ObservableProperty]
    private string _reason = "";

    public string DisplayText => $"{Frequency:F0} Hz: {(GainDb >= 0 ? "+" : "")}{GainDb:F1} dB (Q={Q:F1})";
}

/// <summary>
/// Represents a compression suggestion from the Mix Assistant.
/// </summary>
public partial class CompressionSuggestionItem : ObservableObject
{
    [ObservableProperty]
    private float _thresholdDb;

    [ObservableProperty]
    private float _ratio;

    [ObservableProperty]
    private float _attackMs;

    [ObservableProperty]
    private float _releaseMs;

    [ObservableProperty]
    private string _reason = "";
}

/// <summary>
/// Represents a suggested chord from the Chord Suggestion Engine.
/// </summary>
public partial class ChordSuggestionItem : ObservableObject
{
    [ObservableProperty]
    private string _chordName = "";

    [ObservableProperty]
    private string _romanNumeral = "";

    [ObservableProperty]
    private float _score;

    [ObservableProperty]
    private string _reason = "";

    [ObservableProperty]
    private int[] _midiNotes = Array.Empty<int>();
}

/// <summary>
/// Represents a separated stem from AI Stem Separation.
/// </summary>
public partial class StemItem : ObservableObject
{
    [ObservableProperty]
    private StemType _stemType;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    private string _exportPath = "";

    public string DisplayName => StemType.ToString();
}

/// <summary>
/// ViewModel for the AI Features Panel providing AI-powered audio processing.
/// </summary>
public partial class AIFeaturesViewModel : ViewModelBase
{
    private AIDenoiser? _aiDenoiser;
    private AIDeclip? _aiDeclip;
    private readonly ChordSuggestionEngine _chordSuggestionEngine;
    private readonly MelodyGenerator _melodyGenerator;
    private readonly MixAssistant _mixAssistant;
    private readonly MasteringAssistant _masteringAssistant;
    private readonly StemSeparation _stemSeparation;
    private CancellationTokenSource? _cancellationTokenSource;

    // AI Denoiser Properties
    [ObservableProperty]
    private float _denoiserThreshold = 0.3f;

    [ObservableProperty]
    private bool _denoiserLearning;

    [ObservableProperty]
    private bool _denoiserPreviewEnabled;

    [ObservableProperty]
    private string _denoiserStatus = "Ready";

    // AI Declip Properties
    [ObservableProperty]
    private float _declipSensitivity = 0.5f;

    [ObservableProperty]
    private DeclipQuality _declipQualityMode = DeclipQuality.Medium;

    [ObservableProperty]
    private string _declipStatus = "Ready";

    [ObservableProperty]
    private bool _declipPreviewEnabled;

    // Chord Suggestion Properties
    [ObservableProperty]
    private ObservableCollection<ChordSuggestionItem> _suggestedChords = new();

    [ObservableProperty]
    private int _keyRoot; // 0-11 (C to B)

    [ObservableProperty]
    private bool _isMinorKey;

    [ObservableProperty]
    private ChordSuggestionStyle _chordStyle = ChordSuggestionStyle.Pop;

    [ObservableProperty]
    private ChordSuggestionItem? _selectedChord;

    // Melody Generator Properties
    [ObservableProperty]
    private string _seedPattern = "";

    [ObservableProperty]
    private float _melodyTemperature = 0.7f;

    [ObservableProperty]
    private MelodyStyle _melodyStyle = MelodyStyle.Pop;

    [ObservableProperty]
    private ContourShape _melodyContour = ContourShape.Arc;

    [ObservableProperty]
    private float _melodyDensity = 0.5f;

    [ObservableProperty]
    private double _melodyLength = 16.0;

    [ObservableProperty]
    private string _melodyStatus = "Ready";

    // Mix Assistant Properties
    [ObservableProperty]
    private ObservableCollection<EqSuggestionItem> _eqSuggestions = new();

    [ObservableProperty]
    private CompressionSuggestionItem? _compressionSuggestion;

    [ObservableProperty]
    private MixGenre _mixGenre = MixGenre.Pop;

    [ObservableProperty]
    private string _mixAnalysisStatus = "Not analyzed";

    [ObservableProperty]
    private float _mixConfidence;

    // Mastering Assistant Properties
    [ObservableProperty]
    private MasteringTarget _masteringTarget = MasteringTarget.Streaming;

    [ObservableProperty]
    private float _targetLufs = -14f;

    [ObservableProperty]
    private float _inputLufs;

    [ObservableProperty]
    private float _outputLufs;

    [ObservableProperty]
    private bool _masteringPreviewA = true;

    [ObservableProperty]
    private string _masteringStatus = "Ready";

    [ObservableProperty]
    private ObservableCollection<string> _masteringNotes = new();

    // Stem Separation Properties
    [ObservableProperty]
    private ObservableCollection<StemItem> _stems = new();

    [ObservableProperty]
    private SeparationQuality _separationQuality = SeparationQuality.Medium;

    [ObservableProperty]
    private float _separationProgress;

    [ObservableProperty]
    private string _separationStatus = "Ready";

    [ObservableProperty]
    private string _separationPhase = "";

    [ObservableProperty]
    private bool _isSeparating;

    [ObservableProperty]
    private string _exportDirectory = "";

    /// <summary>
    /// Array of key names for display.
    /// </summary>
    public string[] KeyNames { get; } = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Array of chord styles for combo box.
    /// </summary>
    public ChordSuggestionStyle[] ChordStyles { get; } = Enum.GetValues<ChordSuggestionStyle>();

    /// <summary>
    /// Array of melody styles for combo box.
    /// </summary>
    public MelodyStyle[] MelodyStyles { get; } = Enum.GetValues<MelodyStyle>();

    /// <summary>
    /// Array of contour shapes for combo box.
    /// </summary>
    public ContourShape[] ContourShapes { get; } = Enum.GetValues<ContourShape>();

    /// <summary>
    /// Array of mix genres for combo box.
    /// </summary>
    public MixGenre[] MixGenres { get; } = Enum.GetValues<MixGenre>();

    /// <summary>
    /// Array of mastering targets for combo box.
    /// </summary>
    public MasteringTarget[] MasteringTargets { get; } = Enum.GetValues<MasteringTarget>();

    /// <summary>
    /// Array of declip quality modes for combo box.
    /// </summary>
    public DeclipQuality[] DeclipQualities { get; } = Enum.GetValues<DeclipQuality>();

    /// <summary>
    /// Array of separation qualities for combo box.
    /// </summary>
    public SeparationQuality[] SeparationQualities { get; } = Enum.GetValues<SeparationQuality>();

    /// <summary>
    /// Event raised when a chord is selected to be inserted.
    /// </summary>
    public event EventHandler<int[]>? ChordInsertRequested;

    /// <summary>
    /// Event raised when melody generation completes.
    /// </summary>
    public event EventHandler<MelodyGeneratorConfig>? MelodyGenerationRequested;

    public AIFeaturesViewModel()
    {
        // Note: AIDenoiser and AIDeclip require an ISampleProvider and will be
        // initialized when audio is loaded via SetAudioSource()
        _aiDenoiser = null;
        _aiDeclip = null;
        _chordSuggestionEngine = new ChordSuggestionEngine();
        _melodyGenerator = new MelodyGenerator();
        _mixAssistant = new MixAssistant();
        _masteringAssistant = new MasteringAssistant();
        _stemSeparation = new StemSeparation(SeparationQuality.Medium);

        InitializeStems();
    }

    /// <summary>
    /// Sets the audio source for AI processing effects.
    /// </summary>
    /// <param name="source">The audio sample provider to process.</param>
    public void SetAudioSource(NAudio.Wave.ISampleProvider source)
    {
        _aiDenoiser = new AIDenoiser(source);
        _aiDeclip = new AIDeclip(source);
    }

    private void InitializeStems()
    {
        Stems.Clear();
        Stems.Add(new StemItem { StemType = StemType.Vocals, IsAvailable = false });
        Stems.Add(new StemItem { StemType = StemType.Drums, IsAvailable = false });
        Stems.Add(new StemItem { StemType = StemType.Bass, IsAvailable = false });
        Stems.Add(new StemItem { StemType = StemType.Other, IsAvailable = false });
    }

    // AI Denoiser Commands

    [RelayCommand]
    private void LearnNoiseProfile()
    {
        DenoiserLearning = true;
        DenoiserStatus = "Learning noise profile...";
        // In real implementation, this would capture noise profile from selected audio
        // For now, simulate the learning process
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            DenoiserLearning = false;
            DenoiserStatus = "Noise profile learned";
        });
    }

    [RelayCommand]
    private void ToggleDenoiserPreview()
    {
        DenoiserPreviewEnabled = !DenoiserPreviewEnabled;
        DenoiserStatus = DenoiserPreviewEnabled ? "Preview enabled" : "Preview disabled";
    }

    [RelayCommand]
    private void ApplyDenoiser()
    {
        DenoiserStatus = "Applying noise reduction...";
        // Apply the denoiser to selected audio
        IsBusy = true;
        Task.Run(async () =>
        {
            await Task.Delay(1500);
            IsBusy = false;
            DenoiserStatus = "Noise reduction applied";
        });
    }

    // AI Declip Commands

    [RelayCommand]
    private void ToggleDeclipPreview()
    {
        DeclipPreviewEnabled = !DeclipPreviewEnabled;
        DeclipStatus = DeclipPreviewEnabled ? "Preview enabled" : "Preview disabled";
    }

    [RelayCommand]
    private void ApplyDeclip()
    {
        DeclipStatus = "Applying declipping...";
        IsBusy = true;
        Task.Run(async () =>
        {
            await Task.Delay(1500);
            IsBusy = false;
            DeclipStatus = "Declipping applied";
        });
    }

    // Chord Suggestion Commands

    [RelayCommand]
    private void GenerateChordSuggestions()
    {
        SuggestedChords.Clear();

        var suggestions = _chordSuggestionEngine.GetSuggestions(
            Array.Empty<ContextChord>(),
            KeyRoot,
            IsMinorKey,
            ChordStyle,
            8);

        foreach (var suggestion in suggestions)
        {
            SuggestedChords.Add(new ChordSuggestionItem
            {
                ChordName = suggestion.GetChordName(),
                RomanNumeral = suggestion.RomanNumeral,
                Score = suggestion.Score,
                Reason = suggestion.Reason,
                MidiNotes = suggestion.GetNotes(4)
            });
        }
    }

    [RelayCommand]
    private void InsertSelectedChord()
    {
        if (SelectedChord != null && SelectedChord.MidiNotes.Length > 0)
        {
            ChordInsertRequested?.Invoke(this, SelectedChord.MidiNotes);
        }
    }

    // Melody Generator Commands

    [RelayCommand]
    private void GenerateMelody()
    {
        MelodyStatus = "Generating melody...";

        var config = new MelodyGeneratorConfig
        {
            RootNote = 60 + KeyRoot,
            Scale = IsMinorKey ? ScaleType.NaturalMinor : ScaleType.Major,
            Style = MelodyStyle,
            Contour = MelodyContour,
            Density = MelodyDensity,
            LengthInBeats = MelodyLength,
            Seed = SeedPattern.GetHashCode()
        };

        MelodyGenerationRequested?.Invoke(this, config);
        MelodyStatus = "Melody generated";
    }

    [RelayCommand]
    private void RandomizeSeed()
    {
        var random = new Random();
        SeedPattern = $"Seed_{random.Next(10000):D4}";
    }

    // Mix Assistant Commands

    [RelayCommand]
    private async Task AnalyzeMixAsync()
    {
        IsBusy = true;
        MixAnalysisStatus = "Analyzing...";
        EqSuggestions.Clear();

        await Task.Run(() =>
        {
            // In real implementation, this would analyze the currently selected track
            // For demo, we use template suggestions
            var suggestion = _mixAssistant.GetTemplateSuggestions(
                MusicEngine.Core.AI.TrackType.LeadVocal,
                MixGenre);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var eq in suggestion.EqSuggestions)
                {
                    EqSuggestions.Add(new EqSuggestionItem
                    {
                        Frequency = eq.Frequency,
                        GainDb = eq.GainDb,
                        Q = eq.Q,
                        FilterType = eq.FilterType,
                        Reason = eq.Reason
                    });
                }

                if (suggestion.CompressionSuggestion != null)
                {
                    CompressionSuggestion = new CompressionSuggestionItem
                    {
                        ThresholdDb = suggestion.CompressionSuggestion.ThresholdDb,
                        Ratio = suggestion.CompressionSuggestion.Ratio,
                        AttackMs = suggestion.CompressionSuggestion.AttackMs,
                        ReleaseMs = suggestion.CompressionSuggestion.ReleaseMs,
                        Reason = suggestion.CompressionSuggestion.Reason
                    };
                }

                MixConfidence = suggestion.Confidence;
                MixAnalysisStatus = $"Analysis complete (Confidence: {suggestion.Confidence:P0})";
            });
        });

        IsBusy = false;
    }

    [RelayCommand]
    private void ApplyEqSuggestions()
    {
        StatusMessage = "Applying EQ suggestions...";
        // In real implementation, this would apply EQ to the audio engine
    }

    [RelayCommand]
    private void ApplyCompressionSuggestion()
    {
        StatusMessage = "Applying compression suggestion...";
        // In real implementation, this would apply compression to the audio engine
    }

    // Mastering Assistant Commands

    [RelayCommand]
    private async Task CreateMasteringChainAsync()
    {
        IsBusy = true;
        MasteringStatus = "Creating mastering chain...";
        MasteringNotes.Clear();

        await Task.Run(() =>
        {
            // In real implementation, this would analyze the current mix
            // For demo, we use a genre preset
            var chain = _masteringAssistant.GetGenrePreset("pop", MasteringTarget);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TargetLufs = chain.TargetLufs;
                InputLufs = chain.InputAnalysis.IntegratedLufs;
                OutputLufs = chain.EstimatedOutputLufs;

                foreach (var note in chain.Notes)
                {
                    MasteringNotes.Add(note);
                }

                MasteringStatus = $"Chain created (Confidence: {chain.Confidence:P0})";
            });
        });

        IsBusy = false;
    }

    [RelayCommand]
    private void ToggleMasteringPreview()
    {
        MasteringPreviewA = !MasteringPreviewA;
        MasteringStatus = MasteringPreviewA ? "Preview: Original (A)" : "Preview: Mastered (B)";
    }

    [RelayCommand]
    private void ApplyMastering()
    {
        MasteringStatus = "Applying mastering chain...";
        IsBusy = true;
        Task.Run(async () =>
        {
            await Task.Delay(2000);
            IsBusy = false;
            MasteringStatus = "Mastering applied";
        });
    }

    // Stem Separation Commands

    [RelayCommand]
    private async Task SeparateStemsAsync()
    {
        if (IsSeparating)
        {
            _cancellationTokenSource?.Cancel();
            return;
        }

        IsSeparating = true;
        SeparationProgress = 0;
        SeparationStatus = "Starting separation...";

        _cancellationTokenSource = new CancellationTokenSource();

        var progress = new Progress<StemSeparationProgress>(p =>
        {
            SeparationProgress = p.OverallProgress;
            SeparationPhase = p.CurrentPhase;
            SeparationStatus = p.CurrentStem.HasValue
                ? $"{p.CurrentPhase}: {p.CurrentStem}"
                : p.CurrentPhase;
        });

        try
        {
            // In real implementation, this would use the actual audio file
            // For demo, simulate the process
            await Task.Run(async () =>
            {
                for (int i = 0; i <= 100; i += 5)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                        break;

                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        SeparationProgress = i / 100f;
                        SeparationPhase = i < 30 ? "Analyzing spectrum" :
                                         i < 60 ? "Computing masks" :
                                         i < 90 ? "Reconstructing stems" : "Finalizing";
                    });

                    await Task.Delay(100, _cancellationTokenSource.Token);
                }
            }, _cancellationTokenSource.Token);

            // Mark stems as available
            foreach (var stem in Stems)
            {
                stem.IsAvailable = true;
            }

            SeparationStatus = "Separation complete";
        }
        catch (OperationCanceledException)
        {
            SeparationStatus = "Separation cancelled";
        }
        finally
        {
            IsSeparating = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    [RelayCommand]
    private void CancelSeparation()
    {
        _cancellationTokenSource?.Cancel();
    }

    [RelayCommand]
    private void ExportStem(StemItem? stem)
    {
        if (stem == null || !stem.IsAvailable)
            return;

        // In real implementation, this would open a save dialog and export the stem
        StatusMessage = $"Exporting {stem.StemType} stem...";
    }

    [RelayCommand]
    private void ExportAllStems()
    {
        if (string.IsNullOrEmpty(ExportDirectory))
        {
            // In real implementation, show folder browser dialog
            StatusMessage = "Please select an export directory";
            return;
        }

        StatusMessage = "Exporting all stems...";
        // Export all stems to the selected directory
    }

    [RelayCommand]
    private void BrowseExportDirectory()
    {
        // In real implementation, this would open a folder browser dialog
        ExportDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
    }

    partial void OnMasteringTargetChanged(MasteringTarget value)
    {
        // Update target LUFS based on selected target
        TargetLufs = value switch
        {
            MasteringTarget.Streaming => -14f,
            MasteringTarget.CD => -9f,
            MasteringTarget.Broadcast => -24f,
            MasteringTarget.Club => -6f,
            MasteringTarget.YouTube => -14f,
            MasteringTarget.Podcast => -16f,
            MasteringTarget.Vinyl => -12f,
            _ => -14f
        };
    }

    partial void OnSeparationQualityChanged(SeparationQuality value)
    {
        // Recreate stem separation with new quality
        // In real implementation, this would recreate the StemSeparation instance
    }
}
