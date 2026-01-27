// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Polyphonic Pitch Editor (Melodyne DNA-style).

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Analysis;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Defines the available editing tools for the polyphonic pitch editor.
/// </summary>
public enum PolyphonicPitchTool
{
    /// <summary>Selection tool for selecting and moving note blobs.</summary>
    Select,
    /// <summary>Pitch correction tool for dragging pitches vertically.</summary>
    PitchCorrect,
    /// <summary>Time correction tool for dragging notes horizontally.</summary>
    TimeCorrect,
    /// <summary>Formant tool for adjusting formant shift.</summary>
    Formant,
    /// <summary>Split tool for dividing notes.</summary>
    Split,
    /// <summary>Merge tool for combining adjacent notes.</summary>
    Merge
}

/// <summary>
/// Represents a note blob displayed in the polyphonic pitch editor.
/// Wraps a PolyphonicNote with additional UI-specific properties.
/// </summary>
public partial class NoteBlobViewModel : ObservableObject
{
    private readonly PolyphonicNote _note;

    /// <summary>
    /// Gets the underlying PolyphonicNote.
    /// </summary>
    public PolyphonicNote Note => _note;

    /// <summary>
    /// Gets the unique identifier for this note.
    /// </summary>
    public Guid Id => _note.Id;

    /// <summary>
    /// Gets or sets the current pitch in MIDI note number.
    /// </summary>
    public float Pitch
    {
        get => _note.Pitch;
        set
        {
            if (Math.Abs(_note.Pitch - value) > 0.001f)
            {
                _note.Pitch = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PitchDeviation));
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    /// <summary>
    /// Gets the original detected pitch.
    /// </summary>
    public float OriginalPitch => _note.OriginalPitch;

    /// <summary>
    /// Gets the pitch deviation from original in semitones.
    /// </summary>
    public float PitchDeviation => _note.PitchDeviation;

    /// <summary>
    /// Gets or sets the start time in seconds.
    /// </summary>
    public double StartTime
    {
        get => _note.StartTime;
        set
        {
            if (Math.Abs(_note.StartTime - value) > 0.001)
            {
                _note.StartTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Duration));
            }
        }
    }

    /// <summary>
    /// Gets or sets the end time in seconds.
    /// </summary>
    public double EndTime
    {
        get => _note.EndTime;
        set
        {
            if (Math.Abs(_note.EndTime - value) > 0.001)
            {
                _note.EndTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Duration));
            }
        }
    }

    /// <summary>
    /// Gets the duration in seconds.
    /// </summary>
    public double Duration => _note.Duration;

    /// <summary>
    /// Gets the amplitude (0.0 to 1.0).
    /// </summary>
    public float Amplitude => _note.Amplitude;

    /// <summary>
    /// Gets or sets the formant shift in semitones.
    /// </summary>
    public float Formant
    {
        get => _note.Formant;
        set
        {
            if (Math.Abs(_note.Formant - value) > 0.001f)
            {
                _note.Formant = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsModified));
            }
        }
    }

    /// <summary>
    /// Gets the vibrato amount (0.0 to 1.0).
    /// </summary>
    public float Vibrato => _note.Vibrato;

    /// <summary>
    /// Gets the voice index this note belongs to.
    /// </summary>
    public int VoiceIndex => _note.VoiceIndex;

    /// <summary>
    /// Gets the pitch contour for drift visualization.
    /// </summary>
    public float[]? PitchContour => _note.PitchContour;

    /// <summary>
    /// Gets the amplitude contour.
    /// </summary>
    public float[]? AmplitudeContour => _note.AmplitudeContour;

    /// <summary>
    /// Gets whether this note has been modified from its original state.
    /// </summary>
    public bool IsModified => _note.IsModified;

    /// <summary>
    /// Gets or sets whether this note is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the detection confidence (0.0 to 1.0).
    /// </summary>
    public float Confidence => _note.Confidence;

    /// <summary>
    /// Gets the display color for this note based on voice index.
    /// </summary>
    public Color DisplayColor { get; }

    /// <summary>
    /// Creates a new NoteBlobViewModel wrapping the specified note.
    /// </summary>
    /// <param name="note">The PolyphonicNote to wrap.</param>
    /// <param name="voiceColor">The color for this voice.</param>
    public NoteBlobViewModel(PolyphonicNote note, Color voiceColor)
    {
        _note = note;
        DisplayColor = voiceColor;
        _isSelected = note.IsSelected;
    }

    /// <summary>
    /// Updates the selection state on the underlying note.
    /// </summary>
    partial void OnIsSelectedChanged(bool value)
    {
        _note.IsSelected = value;
    }

    /// <summary>
    /// Gets the note name (e.g., "C4", "D#5").
    /// </summary>
    public string NoteName => GetNoteName((int)Math.Round(Pitch));

    /// <summary>
    /// Notifies the UI that a property has changed. Used for external refresh.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed.</param>
    public new void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// Converts MIDI note number to note name.
    /// </summary>
    private static string GetNoteName(int midiNote)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = midiNote / 12 - 1;
        int noteIndex = midiNote % 12;
        return $"{noteNames[noteIndex]}{octave}";
    }
}

/// <summary>
/// Represents a voice in the polyphonic pitch editor with UI properties.
/// </summary>
public partial class VoiceViewModel : ObservableObject
{
    private readonly PolyphonicVoice _voice;

    /// <summary>
    /// Gets the underlying PolyphonicVoice.
    /// </summary>
    public PolyphonicVoice Voice => _voice;

    /// <summary>
    /// Gets the voice index.
    /// </summary>
    public int Index => _voice.Index;

    /// <summary>
    /// Gets or sets the voice name.
    /// </summary>
    public string Name
    {
        get => _voice.Name;
        set
        {
            if (_voice.Name != value)
            {
                _voice.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this voice is muted.
    /// </summary>
    public bool IsMuted
    {
        get => _voice.IsMuted;
        set
        {
            if (_voice.IsMuted != value)
            {
                _voice.IsMuted = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this voice is soloed.
    /// </summary>
    public bool IsSoloed
    {
        get => _voice.IsSoloed;
        set
        {
            if (_voice.IsSoloed != value)
            {
                _voice.IsSoloed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the volume (0.0 to 2.0).
    /// </summary>
    public float Volume
    {
        get => _voice.Volume;
        set
        {
            if (Math.Abs(_voice.Volume - value) > 0.001f)
            {
                _voice.Volume = Math.Clamp(value, 0f, 2f);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the display color for this voice.
    /// </summary>
    public Color DisplayColor { get; }

    /// <summary>
    /// Gets the number of notes in this voice.
    /// </summary>
    public int NoteCount => _voice.NoteCount;

    /// <summary>
    /// Creates a new VoiceViewModel wrapping the specified voice.
    /// </summary>
    public VoiceViewModel(PolyphonicVoice voice)
    {
        _voice = voice;
        DisplayColor = ConvertToMediaColor(voice.Color);
    }

    /// <summary>
    /// Converts System.Drawing.Color to System.Windows.Media.Color.
    /// </summary>
    private static Color ConvertToMediaColor(System.Drawing.Color color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }
}

/// <summary>
/// ViewModel for the Polyphonic Pitch Editor, providing Melodyne DNA-style
/// polyphonic pitch editing capabilities.
/// </summary>
public partial class PolyphonicPitchViewModel : ViewModelBase, IDisposable
{
    #region Constants

    private const double MinZoom = 0.1;
    private const double MaxZoom = 20.0;
    private const double ZoomStep = 0.25;
    private const int MinVisibleNote = 24;  // C1
    private const int MaxVisibleNote = 108; // C8
    private const double NoteHeight = 20.0;
    private const double PixelsPerSecond = 100.0;

    #endregion

    #region Private Fields

    private PolyphonicPitchEdit? _editor;
    private readonly AudioEngineService _audioEngineService;
    private bool _disposed;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of note blobs (visual representations of notes).
    /// </summary>
    public ObservableCollection<NoteBlobViewModel> NoteBlobs { get; } = new();

    /// <summary>
    /// Gets the collection of currently selected note blobs.
    /// </summary>
    public ObservableCollection<NoteBlobViewModel> SelectedNotes { get; } = new();

    /// <summary>
    /// Gets the collection of voices.
    /// </summary>
    public ObservableCollection<VoiceViewModel> Voices { get; } = new();

    /// <summary>
    /// Gets the available scales for quantization.
    /// </summary>
    public ObservableCollection<string> AvailableScales { get; } = new()
    {
        "Chromatic", "Major", "NaturalMinor", "HarmonicMinor", "MelodicMinor",
        "Dorian", "Phrygian", "Lydian", "Mixolydian", "Locrian",
        "PentatonicMajor", "PentatonicMinor", "Blues"
    };

    /// <summary>
    /// Gets the available root notes.
    /// </summary>
    public ObservableCollection<string> AvailableRootNotes { get; } = new()
    {
        "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"
    };

    #endregion

    #region Tool Properties

    /// <summary>
    /// Gets or sets the currently active editing tool.
    /// </summary>
    [ObservableProperty]
    private PolyphonicPitchTool _currentTool = PolyphonicPitchTool.Select;

    /// <summary>
    /// Gets or sets whether snap to pitch is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _snapToPitch = true;

    /// <summary>
    /// Gets or sets the pitch snap strength (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _pitchSnapStrength = 1.0f;

    /// <summary>
    /// Gets or sets whether snap to time grid is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _snapToTime;

    /// <summary>
    /// Gets or sets the time grid value in seconds.
    /// </summary>
    [ObservableProperty]
    private double _timeGridValue = 0.125; // 1/8 note at 120 BPM

    #endregion

    #region View Properties

    /// <summary>
    /// Gets or sets the horizontal zoom level.
    /// </summary>
    [ObservableProperty]
    private double _zoomX = 1.0;

    /// <summary>
    /// Gets or sets the vertical zoom level.
    /// </summary>
    [ObservableProperty]
    private double _zoomY = 1.0;

    /// <summary>
    /// Gets or sets the horizontal scroll position in seconds.
    /// </summary>
    [ObservableProperty]
    private double _scrollX;

    /// <summary>
    /// Gets or sets the vertical scroll position (MIDI note number).
    /// </summary>
    [ObservableProperty]
    private double _scrollY = 60; // Start at C4

    /// <summary>
    /// Gets or sets the playhead position in seconds.
    /// </summary>
    [ObservableProperty]
    private double _playheadPosition;

    /// <summary>
    /// Gets or sets the total duration of the loaded audio in seconds.
    /// </summary>
    [ObservableProperty]
    private double _totalDuration;

    /// <summary>
    /// Gets or sets the lowest visible MIDI note.
    /// </summary>
    [ObservableProperty]
    private int _lowestVisibleNote = MinVisibleNote;

    /// <summary>
    /// Gets or sets the highest visible MIDI note.
    /// </summary>
    [ObservableProperty]
    private int _highestVisibleNote = MaxVisibleNote;

    #endregion

    #region Formant Properties

    /// <summary>
    /// Gets or sets whether to preserve formants during pitch shifting.
    /// </summary>
    [ObservableProperty]
    private bool _preserveFormants = true;

    /// <summary>
    /// Gets or sets the global formant shift in semitones.
    /// </summary>
    [ObservableProperty]
    private float _globalFormantShift;

    #endregion

    #region Display Properties

    /// <summary>
    /// Gets or sets whether to show pitch drift visualization.
    /// </summary>
    [ObservableProperty]
    private bool _showPitchDrift = true;

    /// <summary>
    /// Gets or sets whether to show the pitch grid.
    /// </summary>
    [ObservableProperty]
    private bool _showPitchGrid = true;

    /// <summary>
    /// Gets or sets whether to show the time grid.
    /// </summary>
    [ObservableProperty]
    private bool _showTimeGrid = true;

    /// <summary>
    /// Gets or sets whether to show voice colors.
    /// </summary>
    [ObservableProperty]
    private bool _showVoiceColors = true;

    /// <summary>
    /// Gets or sets whether to show amplitude as blob size.
    /// </summary>
    [ObservableProperty]
    private bool _showAmplitudeSize = true;

    /// <summary>
    /// Gets or sets whether to highlight modified notes.
    /// </summary>
    [ObservableProperty]
    private bool _highlightModified = true;

    #endregion

    #region Quantization Properties

    /// <summary>
    /// Gets or sets the selected scale for quantization.
    /// </summary>
    [ObservableProperty]
    private string _selectedScale = "Chromatic";

    /// <summary>
    /// Gets or sets the selected root note for quantization.
    /// </summary>
    [ObservableProperty]
    private string _selectedRootNote = "C";

    /// <summary>
    /// Gets or sets the quantization strength (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _quantizationStrength = 1.0f;

    #endregion

    #region Analysis Properties

    /// <summary>
    /// Gets or sets whether analysis is in progress.
    /// </summary>
    [ObservableProperty]
    private bool _isAnalyzing;

    /// <summary>
    /// Gets or sets the analysis progress (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private float _analysisProgress;

    /// <summary>
    /// Gets whether audio has been loaded.
    /// </summary>
    [ObservableProperty]
    private bool _hasAudio;

    /// <summary>
    /// Gets whether analysis has been completed.
    /// </summary>
    [ObservableProperty]
    private bool _hasAnalysis;

    /// <summary>
    /// Gets the total number of detected notes.
    /// </summary>
    [ObservableProperty]
    private int _totalNoteCount;

    /// <summary>
    /// Gets the number of detected voices.
    /// </summary>
    [ObservableProperty]
    private int _voiceCount;

    #endregion

    #region Undo/Redo Properties

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => _editor?.CanUndo ?? false;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => _editor?.CanRedo ?? false;

    /// <summary>
    /// Gets the description of the operation that would be undone.
    /// </summary>
    public string? UndoDescription => _editor?.GetUndoDescription();

    /// <summary>
    /// Gets the description of the operation that would be redone.
    /// </summary>
    public string? RedoDescription => _editor?.GetRedoDescription();

    /// <summary>
    /// Gets whether there are unsaved changes.
    /// </summary>
    public bool HasChanges => _editor?.HasChanges ?? false;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new PolyphonicPitchViewModel instance.
    /// </summary>
    public PolyphonicPitchViewModel()
    {
        _audioEngineService = AudioEngineService.Instance;

        SelectedNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedNotes));
    }

    #endregion

    #region Audio Loading and Analysis

    /// <summary>
    /// Loads audio data for editing.
    /// </summary>
    /// <param name="audioData">Mono audio samples.</param>
    /// <param name="sampleRate">Sample rate of the audio.</param>
    public void LoadAudio(float[] audioData, int sampleRate)
    {
        if (audioData == null || audioData.Length == 0)
        {
            StatusMessage = "Invalid audio data";
            return;
        }

        _editor?.Dispose();
        _editor = new PolyphonicPitchEdit();

        _editor.AnalysisChanged += OnAnalysisChanged;
        _editor.UndoStateChanged += OnUndoStateChanged;
        _editor.AnalysisProgress += OnAnalysisProgress;

        _editor.LoadAudio(audioData, sampleRate);
        _editor.PreserveFormants = PreserveFormants;

        TotalDuration = (double)audioData.Length / sampleRate;
        HasAudio = true;
        HasAnalysis = false;

        ClearNoteBlobs();
        StatusMessage = $"Loaded {TotalDuration:F2}s of audio at {sampleRate}Hz";
    }

    /// <summary>
    /// Analyzes the loaded audio to extract polyphonic notes.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAnalyze))]
    public async Task AnalyzeAsync()
    {
        if (_editor == null || !HasAudio)
        {
            StatusMessage = "No audio loaded";
            return;
        }

        IsAnalyzing = true;
        AnalysisProgress = 0;
        StatusMessage = "Analyzing audio...";

        try
        {
            await Task.Run(() => _editor.Analyze());

            HasAnalysis = true;
            RefreshNoteBlobs();
            StatusMessage = $"Analysis complete: {TotalNoteCount} notes in {VoiceCount} voices";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Analysis failed: {ex.Message}";
            HasAnalysis = false;
        }
        finally
        {
            IsAnalyzing = false;
            AnalysisProgress = 1.0f;
        }
    }

    private bool CanAnalyze() => HasAudio && !IsAnalyzing;

    private void OnAnalysisChanged(object? sender, EventArgs e)
    {
        RefreshNoteBlobs();
        OnPropertyChanged(nameof(HasChanges));
    }

    private void OnUndoStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoDescription));
        OnPropertyChanged(nameof(RedoDescription));
        OnPropertyChanged(nameof(HasChanges));
    }

    private void OnAnalysisProgress(object? sender, float progress)
    {
        AnalysisProgress = progress;
    }

    #endregion

    #region Note Blob Management

    private void ClearNoteBlobs()
    {
        NoteBlobs.Clear();
        SelectedNotes.Clear();
        Voices.Clear();
        TotalNoteCount = 0;
        VoiceCount = 0;
    }

    private void RefreshNoteBlobs()
    {
        ClearNoteBlobs();

        var analysis = _editor?.Analysis;
        if (analysis == null)
            return;

        // Create voice view models
        foreach (var voice in analysis.Voices)
        {
            var voiceVm = new VoiceViewModel(voice);
            Voices.Add(voiceVm);

            // Create note blob view models
            var voiceColor = voiceVm.DisplayColor;
            foreach (var note in voice.Notes)
            {
                var blobVm = new NoteBlobViewModel(note, voiceColor);
                NoteBlobs.Add(blobVm);
            }
        }

        TotalNoteCount = analysis.TotalNoteCount;
        VoiceCount = analysis.Voices.Count;
    }

    #endregion

    #region Selection Commands

    /// <summary>
    /// Selects all notes.
    /// </summary>
    [RelayCommand]
    public void SelectAll()
    {
        SelectedNotes.Clear();
        foreach (var blob in NoteBlobs)
        {
            blob.IsSelected = true;
            SelectedNotes.Add(blob);
        }
        StatusMessage = $"Selected {SelectedNotes.Count} notes";
    }

    /// <summary>
    /// Deselects all notes.
    /// </summary>
    [RelayCommand]
    public void DeselectAll()
    {
        foreach (var blob in SelectedNotes)
        {
            blob.IsSelected = false;
        }
        SelectedNotes.Clear();
    }

    /// <summary>
    /// Inverts the current selection.
    /// </summary>
    [RelayCommand]
    public void InvertSelection()
    {
        var previouslySelected = SelectedNotes.ToList();
        SelectedNotes.Clear();

        foreach (var blob in NoteBlobs)
        {
            bool wasSelected = previouslySelected.Contains(blob);
            blob.IsSelected = !wasSelected;
            if (!wasSelected)
            {
                SelectedNotes.Add(blob);
            }
        }
    }

    /// <summary>
    /// Selects notes within a time range.
    /// </summary>
    public void SelectNotesInRange(double startTime, double endTime, int? lowNote = null, int? highNote = null)
    {
        foreach (var blob in NoteBlobs)
        {
            bool inTimeRange = blob.StartTime < endTime && blob.EndTime > startTime;
            bool inPitchRange = true;

            if (lowNote.HasValue && highNote.HasValue)
            {
                int noteNumber = (int)Math.Round(blob.Pitch);
                inPitchRange = noteNumber >= lowNote.Value && noteNumber <= highNote.Value;
            }

            if (inTimeRange && inPitchRange)
            {
                if (!blob.IsSelected)
                {
                    blob.IsSelected = true;
                    SelectedNotes.Add(blob);
                }
            }
        }
    }

    /// <summary>
    /// Toggles selection for a single note.
    /// </summary>
    public void ToggleNoteSelection(NoteBlobViewModel blob)
    {
        if (blob.IsSelected)
        {
            blob.IsSelected = false;
            SelectedNotes.Remove(blob);
        }
        else
        {
            blob.IsSelected = true;
            SelectedNotes.Add(blob);
        }
    }

    #endregion

    #region Pitch Editing Commands

    /// <summary>
    /// Sets the pitch of selected notes.
    /// </summary>
    /// <param name="deltaSemitones">Pitch change in semitones.</param>
    [RelayCommand]
    public void TransposeSelected(float deltaSemitones)
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            float newPitch = blob.Pitch + deltaSemitones;
            _editor.SetNotePitch(blob.Note, newPitch);
            blob.Pitch = newPitch;
        }

        StatusMessage = $"Transposed {SelectedNotes.Count} notes by {deltaSemitones:+#;-#;0} semitones";
    }

    /// <summary>
    /// Quantizes selected notes to the nearest semitone.
    /// </summary>
    [RelayCommand]
    public void QuantizePitchSelected()
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            _editor.QuantizePitch(blob.Note, QuantizationStrength);
        }

        RefreshSelectedNoteProperties();
        StatusMessage = $"Quantized pitch of {SelectedNotes.Count} notes";
    }

    /// <summary>
    /// Quantizes all notes to the selected scale.
    /// </summary>
    [RelayCommand]
    public void QuantizeToScale()
    {
        if (_editor == null || !HasAnalysis)
            return;

        _editor.QuantizeAllToScale(SelectedScale, SelectedRootNote, QuantizationStrength);
        RefreshNoteBlobs();
        StatusMessage = $"Quantized all notes to {SelectedRootNote} {SelectedScale}";
    }

    /// <summary>
    /// Straightens (flattens) pitch modulation of selected notes.
    /// </summary>
    /// <param name="amount">Amount of straightening (0.0 to 1.0).</param>
    [RelayCommand]
    public void StraightenPitchSelected(float amount)
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            _editor.StraightenPitch(blob.Note, amount);
        }

        StatusMessage = $"Straightened pitch of {SelectedNotes.Count} notes by {amount * 100:F0}%";
    }

    /// <summary>
    /// Resets selected notes to their original pitch.
    /// </summary>
    [RelayCommand]
    public void ResetPitchSelected()
    {
        if (SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            blob.Note.ResetPitch();
        }

        RefreshSelectedNoteProperties();
        StatusMessage = $"Reset pitch of {SelectedNotes.Count} notes";
    }

    #endregion

    #region Time Editing Commands

    /// <summary>
    /// Moves selected notes in time.
    /// </summary>
    /// <param name="deltaSeconds">Time change in seconds.</param>
    [RelayCommand]
    public void MoveTimeSelected(double deltaSeconds)
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            double newStart = Math.Max(0, blob.StartTime + deltaSeconds);
            double newEnd = newStart + blob.Duration;
            _editor.SetNoteTime(blob.Note, newStart, newEnd);
        }

        RefreshSelectedNoteProperties();
        StatusMessage = $"Moved {SelectedNotes.Count} notes by {deltaSeconds * 1000:F0}ms";
    }

    /// <summary>
    /// Stretches/compresses selected notes in time.
    /// </summary>
    /// <param name="factor">Time stretch factor (> 1.0 = longer, < 1.0 = shorter).</param>
    [RelayCommand]
    public void StretchTimeSelected(double factor)
    {
        if (_editor == null || SelectedNotes.Count == 0 || factor <= 0)
            return;

        // Find the anchor point (earliest note start)
        double anchor = SelectedNotes.Min(n => n.StartTime);

        foreach (var blob in SelectedNotes)
        {
            double relativeStart = blob.StartTime - anchor;
            double newStart = anchor + relativeStart * factor;
            double newDuration = blob.Duration * factor;
            double newEnd = newStart + newDuration;

            _editor.SetNoteTime(blob.Note, newStart, newEnd);
        }

        RefreshSelectedNoteProperties();
        StatusMessage = $"Stretched {SelectedNotes.Count} notes by {factor * 100:F0}%";
    }

    #endregion

    #region Formant Commands

    /// <summary>
    /// Sets the formant shift for selected notes.
    /// </summary>
    /// <param name="semitones">Formant shift in semitones.</param>
    [RelayCommand]
    public void SetFormantSelected(float semitones)
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            _editor.SetNoteFormant(blob.Note, semitones);
            blob.Formant = semitones;
        }

        StatusMessage = $"Set formant shift to {semitones:+#.#;-#.#;0} semitones for {SelectedNotes.Count} notes";
    }

    /// <summary>
    /// Resets formant shift for selected notes.
    /// </summary>
    [RelayCommand]
    public void ResetFormantSelected()
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        foreach (var blob in SelectedNotes)
        {
            _editor.SetNoteFormant(blob.Note, 0);
            blob.Formant = 0;
        }

        StatusMessage = $"Reset formant for {SelectedNotes.Count} notes";
    }

    partial void OnPreserveFormantsChanged(bool value)
    {
        if (_editor != null)
        {
            _editor.PreserveFormants = value;
        }
    }

    #endregion

    #region Note Operations

    /// <summary>
    /// Splits a note at the specified time.
    /// </summary>
    public void SplitNote(NoteBlobViewModel blob, double splitTime)
    {
        if (_editor == null)
            return;

        try
        {
            var newNote = _editor.SplitNote(blob.Note, splitTime);
            StatusMessage = $"Split note at {splitTime:F3}s";
        }
        catch (ArgumentException ex)
        {
            StatusMessage = $"Cannot split: {ex.Message}";
        }
    }

    /// <summary>
    /// Merges two adjacent notes.
    /// </summary>
    public void MergeNotes(NoteBlobViewModel note1, NoteBlobViewModel note2)
    {
        if (_editor == null)
            return;

        try
        {
            _editor.MergeNotes(note1.Note, note2.Note);
            StatusMessage = "Merged notes";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Cannot merge: {ex.Message}";
        }
    }

    /// <summary>
    /// Deletes selected notes.
    /// </summary>
    [RelayCommand]
    public void DeleteSelected()
    {
        if (_editor == null || SelectedNotes.Count == 0)
            return;

        int count = SelectedNotes.Count;
        foreach (var blob in SelectedNotes.ToList())
        {
            _editor.DeleteNote(blob.Note);
        }

        SelectedNotes.Clear();
        StatusMessage = $"Deleted {count} notes";
    }

    #endregion

    #region Undo/Redo Commands

    /// <summary>
    /// Undoes the last operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndoExecute))]
    public void Undo()
    {
        _editor?.Undo();
        RefreshNoteBlobs();
        StatusMessage = "Undone";
    }

    private bool CanUndoExecute() => CanUndo;

    /// <summary>
    /// Redoes the last undone operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedoExecute))]
    public void Redo()
    {
        _editor?.Redo();
        RefreshNoteBlobs();
        StatusMessage = "Redone";
    }

    private bool CanRedoExecute() => CanRedo;

    #endregion

    #region View Commands

    /// <summary>
    /// Increases horizontal zoom.
    /// </summary>
    [RelayCommand]
    public void ZoomInX()
    {
        ZoomX = Math.Min(MaxZoom, ZoomX + ZoomStep);
    }

    /// <summary>
    /// Decreases horizontal zoom.
    /// </summary>
    [RelayCommand]
    public void ZoomOutX()
    {
        ZoomX = Math.Max(MinZoom, ZoomX - ZoomStep);
    }

    /// <summary>
    /// Increases vertical zoom.
    /// </summary>
    [RelayCommand]
    public void ZoomInY()
    {
        ZoomY = Math.Min(MaxZoom, ZoomY + ZoomStep);
    }

    /// <summary>
    /// Decreases vertical zoom.
    /// </summary>
    [RelayCommand]
    public void ZoomOutY()
    {
        ZoomY = Math.Max(MinZoom, ZoomY - ZoomStep);
    }

    /// <summary>
    /// Zooms to fit all notes in view.
    /// </summary>
    [RelayCommand]
    public void ZoomToFit()
    {
        if (NoteBlobs.Count == 0 || TotalDuration <= 0)
            return;

        // Calculate bounds
        float minPitch = NoteBlobs.Min(n => n.Pitch);
        float maxPitch = NoteBlobs.Max(n => n.Pitch);
        double minTime = NoteBlobs.Min(n => n.StartTime);
        double maxTime = NoteBlobs.Max(n => n.EndTime);

        // Add some padding
        minPitch = Math.Max(MinVisibleNote, minPitch - 2);
        maxPitch = Math.Min(MaxVisibleNote, maxPitch + 2);

        LowestVisibleNote = (int)minPitch;
        HighestVisibleNote = (int)maxPitch;

        ScrollX = minTime;
        ScrollY = (minPitch + maxPitch) / 2;

        StatusMessage = "Zoomed to fit all notes";
    }

    /// <summary>
    /// Sets the current editing tool.
    /// </summary>
    [RelayCommand]
    public void SetTool(PolyphonicPitchTool tool)
    {
        CurrentTool = tool;
    }

    #endregion

    #region Audio Preview

    /// <summary>
    /// The processed audio result after applying changes.
    /// </summary>
    public float[]? ProcessedAudio { get; private set; }

    /// <summary>
    /// Event raised when audio processing is complete.
    /// </summary>
    public event EventHandler<float[]?>? AudioProcessed;

    /// <summary>
    /// Applies all edits and gets the processed audio.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanApply))]
    private void ApplyChanges()
    {
        if (_editor == null)
        {
            ProcessedAudio = null;
            return;
        }

        StatusMessage = "Applying changes...";
        ProcessedAudio = _editor.Apply();
        StatusMessage = "Changes applied";
        AudioProcessed?.Invoke(this, ProcessedAudio);
    }

    /// <summary>
    /// Gets the processed audio result.
    /// </summary>
    public float[]? GetProcessedAudio()
    {
        if (_editor == null)
            return null;

        return _editor.Apply();
    }

    private bool CanApply() => HasAnalysis && HasChanges;

    /// <summary>
    /// Gets a preview of a time range.
    /// </summary>
    public float[] GetPreview(double startTime, double endTime)
    {
        if (_editor == null)
            return Array.Empty<float>();

        return _editor.GetPreview(startTime, endTime);
    }

    /// <summary>
    /// Plays a preview of selected notes.
    /// </summary>
    [RelayCommand]
    public void PreviewSelected()
    {
        if (SelectedNotes.Count == 0)
            return;

        double startTime = SelectedNotes.Min(n => n.StartTime);
        double endTime = SelectedNotes.Max(n => n.EndTime);

        // Preview through audio engine
        var preview = GetPreview(startTime, endTime);
        if (preview.Length > 0)
        {
            StatusMessage = $"Previewing {endTime - startTime:F2}s";
        }
    }

    #endregion

    #region Helper Methods

    private void RefreshSelectedNoteProperties()
    {
        foreach (var blob in SelectedNotes)
        {
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.Pitch));
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.StartTime));
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.EndTime));
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.Duration));
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.Formant));
            blob.OnPropertyChanged(nameof(NoteBlobViewModel.IsModified));
        }
    }

    partial void OnZoomXChanged(double value)
    {
        ZoomX = Math.Clamp(value, MinZoom, MaxZoom);
    }

    partial void OnZoomYChanged(double value)
    {
        ZoomY = Math.Clamp(value, MinZoom, MaxZoom);
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        if (_editor != null)
        {
            _editor.AnalysisChanged -= OnAnalysisChanged;
            _editor.UndoStateChanged -= OnUndoStateChanged;
            _editor.AnalysisProgress -= OnAnalysisProgress;
            _editor.Dispose();
            _editor = null;
        }

        ClearNoteBlobs();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
