// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Spectral Editor view.

using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Analysis;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Specifies the selection tool type for spectral editing.
/// </summary>
public enum SpectralSelectionTool
{
    Rectangle,
    Lasso,
    MagicWand,
    Paintbrush
}

/// <summary>
/// Specifies the operation to perform on a spectral selection.
/// </summary>
public enum SpectralEditOperation
{
    Cut,
    Copy,
    Paste,
    Attenuate,
    Boost,
    Erase,
    NoiseReduce,
    HarmonicEnhance
}

/// <summary>
/// Represents a point in the spectrogram for lasso selection.
/// </summary>
public class SpectralPoint
{
    public double Time { get; set; }
    public float Frequency { get; set; }
}

/// <summary>
/// Represents clipboard data for copy/paste operations.
/// </summary>
public class SpectralClipboardData
{
    public List<SpectralFrame> Frames { get; set; } = new();
    public double StartTime { get; set; }
    public double EndTime { get; set; }
    public float MinFrequency { get; set; }
    public float MaxFrequency { get; set; }
}

/// <summary>
/// ViewModel for the Spectral Editor, providing FFT-based audio editing capabilities.
/// </summary>
public partial class SpectralEditorViewModel : ViewModelBase
{
    #region Private Fields

    private readonly SpectralEditor _spectralEditor;
    private SpectralClipboardData? _clipboard;
    private float[]? _originalAudio;
    private int _sampleRate = 44100;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAudio))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isAudioLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isProcessing;

    [ObservableProperty]
    private string _audioFileName = "No audio loaded";

    [ObservableProperty]
    private double _duration;

    [ObservableProperty]
    private int _frameCount;

    [ObservableProperty]
    private int _fftSize = 4096;

    [ObservableProperty]
    private int _hopSize = 1024;

    [ObservableProperty]
    private SpectralSelectionTool _selectedTool = SpectralSelectionTool.Rectangle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _hasValidSelection;

    [ObservableProperty]
    private double _selectionStartTime;

    [ObservableProperty]
    private double _selectionEndTime;

    [ObservableProperty]
    private float _selectionMinFrequency;

    [ObservableProperty]
    private float _selectionMaxFrequency;

    [ObservableProperty]
    private double _zoomLevelX = 1.0;

    [ObservableProperty]
    private double _zoomLevelY = 1.0;

    [ObservableProperty]
    private double _scrollOffsetX;

    [ObservableProperty]
    private double _scrollOffsetY;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private float _boostAmount = 6.0f;

    [ObservableProperty]
    private float _attenuateAmount = -6.0f;

    [ObservableProperty]
    private float _paintbrushIntensity = 1.0f;

    [ObservableProperty]
    private float _paintbrushSize = 100f;

    [ObservableProperty]
    private float _magicWandThreshold = 0.5f;

    [ObservableProperty]
    private int _colorMapIndex;

    [ObservableProperty]
    private float _spectrogramMinDb = -80f;

    [ObservableProperty]
    private float _spectrogramMaxDb = 0f;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUndo))]
    private int _undoCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRedo))]
    private int _redoCount;

    [ObservableProperty]
    private ObservableCollection<string> _undoHistory = new();

    [ObservableProperty]
    private ObservableCollection<SpectralPoint> _lassoPoints = new();

    [ObservableProperty]
    private float _maxFrequency = 22050f;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets whether audio is loaded and ready for editing.
    /// </summary>
    public bool HasAudio => IsAudioLoaded;

    /// <summary>
    /// Gets whether editing operations can be performed.
    /// </summary>
    public bool CanEdit => IsAudioLoaded && !IsProcessing;

    /// <summary>
    /// Gets whether there is a valid selection.
    /// </summary>
    public bool HasSelection => HasValidSelection;

    /// <summary>
    /// Gets whether undo is available.
    /// </summary>
    public bool CanUndo => UndoCount > 0;

    /// <summary>
    /// Gets whether redo is available.
    /// </summary>
    public bool CanRedo => RedoCount > 0;

    /// <summary>
    /// Gets whether there is data on the clipboard.
    /// </summary>
    public bool HasClipboard => _clipboard != null;

    /// <summary>
    /// Gets the list of available FFT sizes.
    /// </summary>
    public int[] AvailableFftSizes => new[] { 512, 1024, 2048, 4096, 8192, 16384 };

    /// <summary>
    /// Gets the list of available color maps.
    /// </summary>
    public string[] AvailableColorMaps => new[] { "Heat", "Grayscale", "Plasma", "Viridis", "Magma", "Inferno", "Turbo" };

    /// <summary>
    /// Gets the available selection tools.
    /// </summary>
    public SpectralSelectionTool[] AvailableTools => Enum.GetValues<SpectralSelectionTool>();

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the spectrogram data needs to be redrawn.
    /// </summary>
    public event EventHandler? SpectrogramUpdated;

    /// <summary>
    /// Event raised when the selection changes.
    /// </summary>
    public event EventHandler? SelectionChanged;

    /// <summary>
    /// Event raised when audio is synthesized and ready for playback.
    /// </summary>
    public event EventHandler<float[]>? AudioSynthesized;

    #endregion

    #region Constructor

    /// <summary>
    /// Initializes a new instance of the SpectralEditorViewModel.
    /// </summary>
    public SpectralEditorViewModel()
    {
        _spectralEditor = new SpectralEditor(FftSize, HopSize);
        _spectralEditor.AnalysisComplete += OnAnalysisComplete;
        _spectralEditor.OperationApplied += OnOperationApplied;
    }

    #endregion

    #region Commands - File Operations

    /// <summary>
    /// Command to load audio data for spectral editing.
    /// </summary>
    [RelayCommand]
    private async Task LoadAudioAsync(float[]? audioData)
    {
        if (audioData == null || audioData.Length == 0)
        {
            StatusMessage = "No audio data provided.";
            return;
        }

        IsProcessing = true;
        StatusMessage = "Analyzing audio...";

        try
        {
            await Task.Run(() =>
            {
                _originalAudio = new float[audioData.Length];
                Array.Copy(audioData, _originalAudio, audioData.Length);

                _spectralEditor.FftSize = FftSize;
                _spectralEditor.HopSize = HopSize;
                _spectralEditor.Analyze(audioData, _sampleRate);
            });

            IsAudioLoaded = true;
            FrameCount = _spectralEditor.FrameCount;
            Duration = _spectralEditor.Duration;
            MaxFrequency = _sampleRate / 2f;

            UpdateUndoRedoState();
            SpectrogramUpdated?.Invoke(this, EventArgs.Empty);

            StatusMessage = $"Loaded {Duration:F2}s of audio ({FrameCount} frames)";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error analyzing audio: {ex.Message}";
            IsAudioLoaded = false;
        }
        finally
        {
            IsProcessing = false;
        }
    }

    /// <summary>
    /// Command to synthesize audio from the spectral data.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task SynthesizeAudioAsync()
    {
        IsProcessing = true;
        StatusMessage = "Synthesizing audio...";

        try
        {
            float[] synthesized = Array.Empty<float>();
            await Task.Run(() =>
            {
                synthesized = _spectralEditor.Synthesize();
            });

            AudioSynthesized?.Invoke(this, synthesized);
            StatusMessage = "Audio synthesized successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error synthesizing audio: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    #endregion

    #region Commands - Selection Tools

    /// <summary>
    /// Command to select the rectangle selection tool.
    /// </summary>
    [RelayCommand]
    private void SelectRectangleTool()
    {
        SelectedTool = SpectralSelectionTool.Rectangle;
        StatusMessage = "Rectangle selection tool active";
    }

    /// <summary>
    /// Command to select the lasso selection tool.
    /// </summary>
    [RelayCommand]
    private void SelectLassoTool()
    {
        SelectedTool = SpectralSelectionTool.Lasso;
        LassoPoints.Clear();
        StatusMessage = "Lasso selection tool active";
    }

    /// <summary>
    /// Command to select the magic wand tool.
    /// </summary>
    [RelayCommand]
    private void SelectMagicWandTool()
    {
        SelectedTool = SpectralSelectionTool.MagicWand;
        StatusMessage = "Magic wand tool active - click to select similar frequencies";
    }

    /// <summary>
    /// Command to select the paintbrush tool.
    /// </summary>
    [RelayCommand]
    private void SelectPaintbrushTool()
    {
        SelectedTool = SpectralSelectionTool.Paintbrush;
        StatusMessage = "Paintbrush tool active - paint to modify frequencies";
    }

    /// <summary>
    /// Command to clear the current selection.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SelectionStartTime = 0;
        SelectionEndTime = 0;
        SelectionMinFrequency = 0;
        SelectionMaxFrequency = 0;
        HasValidSelection = false;
        LassoPoints.Clear();
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Selection cleared";
    }

    /// <summary>
    /// Command to select all content.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void SelectAll()
    {
        SelectionStartTime = 0;
        SelectionEndTime = Duration;
        SelectionMinFrequency = 0;
        SelectionMaxFrequency = MaxFrequency;
        HasValidSelection = true;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Selected all";
    }

    #endregion

    #region Commands - Edit Operations

    /// <summary>
    /// Command to cut the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        Copy();
        Erase();
        StatusMessage = "Cut to clipboard";
    }

    /// <summary>
    /// Command to copy the selection to clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        _clipboard = new SpectralClipboardData
        {
            StartTime = selection.StartTime,
            EndTime = selection.EndTime,
            MinFrequency = selection.MinFrequency,
            MaxFrequency = selection.MaxFrequency,
            Frames = new List<SpectralFrame>()
        };

        int startFrame = _spectralEditor.GetFrameIndexAtTime(selection.StartTime);
        int endFrame = _spectralEditor.GetFrameIndexAtTime(selection.EndTime);

        for (int i = startFrame; i <= endFrame && i < _spectralEditor.FrameCount; i++)
        {
            _clipboard.Frames.Add(_spectralEditor.GetFrame(i).Clone());
        }

        OnPropertyChanged(nameof(HasClipboard));
        StatusMessage = $"Copied {_clipboard.Frames.Count} frames to clipboard";
    }

    /// <summary>
    /// Command to paste from clipboard.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanPaste))]
    private void Paste()
    {
        if (_clipboard == null || !HasValidSelection) return;

        var targetSelection = CreateSelection();
        var operation = SpectralOperation.CreateClone(
            targetSelection,
            _clipboard.StartTime,
            _clipboard.EndTime);

        ApplyOperation(operation);
        StatusMessage = "Pasted from clipboard";
    }

    private bool CanPaste() => CanEdit && HasClipboard && HasValidSelection;

    /// <summary>
    /// Command to attenuate (reduce volume) the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Attenuate()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateAmplify(selection, AttenuateAmount);
        ApplyOperation(operation);
        StatusMessage = $"Attenuated by {AttenuateAmount:F1} dB";
    }

    /// <summary>
    /// Command to boost (increase volume) the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Boost()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateAmplify(selection, BoostAmount);
        ApplyOperation(operation);
        StatusMessage = $"Boosted by {BoostAmount:F1} dB";
    }

    /// <summary>
    /// Command to erase the selection (set to zero).
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Erase()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateErase(selection);
        ApplyOperation(operation);
        StatusMessage = "Erased selection";
    }

    /// <summary>
    /// Command to apply noise reduction to the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void NoiseReduce()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateNoiseReduce(selection, 0.5f);
        ApplyOperation(operation);
        StatusMessage = "Applied noise reduction";
    }

    /// <summary>
    /// Command to enhance harmonics in the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void HarmonicEnhance()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateHarmonicEnhance(selection, 0.5f);
        ApplyOperation(operation);
        StatusMessage = "Enhanced harmonics";
    }

    /// <summary>
    /// Command to fade in the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FadeIn()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateFade(selection, true);
        ApplyOperation(operation);
        StatusMessage = "Applied fade in";
    }

    /// <summary>
    /// Command to fade out the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void FadeOut()
    {
        if (!HasValidSelection) return;

        var selection = CreateSelection();
        var operation = SpectralOperation.CreateFade(selection, false);
        ApplyOperation(operation);
        StatusMessage = "Applied fade out";
    }

    #endregion

    #region Commands - Undo/Redo

    /// <summary>
    /// Command to undo the last operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        _spectralEditor.Undo();
        UpdateUndoRedoState();
        SpectrogramUpdated?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Undone";
    }

    /// <summary>
    /// Command to redo the last undone operation.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        _spectralEditor.Redo();
        UpdateUndoRedoState();
        SpectrogramUpdated?.Invoke(this, EventArgs.Empty);
        StatusMessage = "Redone";
    }

    /// <summary>
    /// Command to clear all undo/redo history.
    /// </summary>
    [RelayCommand]
    private void ClearHistory()
    {
        _spectralEditor.ClearHistory();
        UpdateUndoRedoState();
        StatusMessage = "History cleared";
    }

    #endregion

    #region Commands - View Controls

    /// <summary>
    /// Command to zoom in horizontally.
    /// </summary>
    [RelayCommand]
    private void ZoomInX()
    {
        ZoomLevelX = Math.Min(ZoomLevelX * 1.5, 32.0);
    }

    /// <summary>
    /// Command to zoom out horizontally.
    /// </summary>
    [RelayCommand]
    private void ZoomOutX()
    {
        ZoomLevelX = Math.Max(ZoomLevelX / 1.5, 0.1);
    }

    /// <summary>
    /// Command to zoom in vertically (frequency axis).
    /// </summary>
    [RelayCommand]
    private void ZoomInY()
    {
        ZoomLevelY = Math.Min(ZoomLevelY * 1.5, 16.0);
    }

    /// <summary>
    /// Command to zoom out vertically (frequency axis).
    /// </summary>
    [RelayCommand]
    private void ZoomOutY()
    {
        ZoomLevelY = Math.Max(ZoomLevelY / 1.5, 0.25);
    }

    /// <summary>
    /// Command to reset zoom to default.
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevelX = 1.0;
        ZoomLevelY = 1.0;
        ScrollOffsetX = 0;
        ScrollOffsetY = 0;
    }

    /// <summary>
    /// Command to zoom to fit all content.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ZoomToFit()
    {
        ZoomLevelX = 1.0;
        ZoomLevelY = 1.0;
        ScrollOffsetX = 0;
        ScrollOffsetY = 0;
        StatusMessage = "Zoomed to fit";
    }

    /// <summary>
    /// Command to zoom to the selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void ZoomToSelection()
    {
        if (!HasValidSelection) return;

        double selectionDuration = SelectionEndTime - SelectionStartTime;
        if (selectionDuration > 0)
        {
            ZoomLevelX = Duration / selectionDuration;
        }

        float freqRange = SelectionMaxFrequency - SelectionMinFrequency;
        if (freqRange > 0)
        {
            ZoomLevelY = MaxFrequency / freqRange;
        }

        StatusMessage = "Zoomed to selection";
    }

    #endregion

    #region Commands - Analysis Settings

    /// <summary>
    /// Command to re-analyze with new FFT settings.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanReanalyze))]
    private async Task ReanalyzeAsync()
    {
        if (_originalAudio == null) return;

        await LoadAudioAsync(_originalAudio);
    }

    private bool CanReanalyze() => HasAudio && !IsProcessing && _originalAudio != null;

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the selection from external coordinates.
    /// </summary>
    /// <param name="startTime">Start time in seconds.</param>
    /// <param name="endTime">End time in seconds.</param>
    /// <param name="minFreq">Minimum frequency in Hz.</param>
    /// <param name="maxFreq">Maximum frequency in Hz.</param>
    public void SetSelection(double startTime, double endTime, float minFreq, float maxFreq)
    {
        SelectionStartTime = Math.Min(startTime, endTime);
        SelectionEndTime = Math.Max(startTime, endTime);
        SelectionMinFrequency = Math.Min(minFreq, maxFreq);
        SelectionMaxFrequency = Math.Max(minFreq, maxFreq);
        HasValidSelection = SelectionEndTime > SelectionStartTime && SelectionMaxFrequency > SelectionMinFrequency;
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Adds a point to the lasso selection.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    public void AddLassoPoint(double time, float frequency)
    {
        LassoPoints.Add(new SpectralPoint { Time = time, Frequency = frequency });
    }

    /// <summary>
    /// Completes the lasso selection and converts to bounding box.
    /// </summary>
    public void CompleteLassoSelection()
    {
        if (LassoPoints.Count < 3)
        {
            LassoPoints.Clear();
            return;
        }

        double minTime = LassoPoints.Min(p => p.Time);
        double maxTime = LassoPoints.Max(p => p.Time);
        float minFreq = LassoPoints.Min(p => p.Frequency);
        float maxFreq = LassoPoints.Max(p => p.Frequency);

        SetSelection(minTime, maxTime, minFreq, maxFreq);
    }

    /// <summary>
    /// Performs magic wand selection at a point.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    public void MagicWandSelect(double time, float frequency)
    {
        if (!IsAudioLoaded) return;

        var frame = _spectralEditor.GetFrameAtTime(time);
        int bin = frame.GetBinForFrequency(frequency);
        float targetMagnitude = frame.Magnitudes[bin];

        int minBin = bin;
        int maxBin = bin;

        float threshold = targetMagnitude * MagicWandThreshold;

        while (minBin > 0 && frame.Magnitudes[minBin - 1] >= threshold)
            minBin--;

        while (maxBin < frame.BinCount - 1 && frame.Magnitudes[maxBin + 1] >= threshold)
            maxBin++;

        float minFreq = frame.GetFrequencyForBin(minBin);
        float maxFreq = frame.GetFrequencyForBin(maxBin);

        double timeWindow = 0.1;
        SetSelection(time - timeWindow, time + timeWindow, minFreq, maxFreq);
    }

    /// <summary>
    /// Applies paintbrush effect at a point.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    public void PaintAt(double time, float frequency)
    {
        if (!IsAudioLoaded) return;

        float halfSize = PaintbrushSize / 2f;
        var selection = new SpectralSelection(
            time - 0.01,
            time + 0.01,
            Math.Max(0, frequency - halfSize),
            Math.Min(MaxFrequency, frequency + halfSize));

        float gainDb = PaintbrushIntensity > 0 ? BoostAmount * PaintbrushIntensity : AttenuateAmount * Math.Abs(PaintbrushIntensity);
        var operation = SpectralOperation.CreateAmplify(selection, gainDb);
        ApplyOperation(operation);
    }

    /// <summary>
    /// Gets the magnitude at a specific time and frequency for display.
    /// </summary>
    /// <param name="time">Time in seconds.</param>
    /// <param name="frequency">Frequency in Hz.</param>
    /// <returns>Magnitude value.</returns>
    public float GetMagnitudeAt(double time, float frequency)
    {
        if (!IsAudioLoaded) return 0f;

        var frame = _spectralEditor.GetFrameAtTime(time);
        return frame.GetMagnitudeAtFrequency(frequency);
    }

    /// <summary>
    /// Gets the spectrogram data for rendering.
    /// </summary>
    /// <returns>2D array of magnitude values [frame, bin].</returns>
    public float[,]? GetSpectrogramData()
    {
        if (!IsAudioLoaded || FrameCount == 0) return null;

        int binCount = _spectralEditor.GetFrame(0).BinCount;
        var data = new float[FrameCount, binCount];

        for (int f = 0; f < FrameCount; f++)
        {
            var frame = _spectralEditor.GetFrame(f);
            for (int b = 0; b < binCount; b++)
            {
                data[f, b] = frame.Magnitudes[b];
            }
        }

        return data;
    }

    /// <summary>
    /// Gets a color for a magnitude value using the current color map.
    /// </summary>
    /// <param name="magnitude">Magnitude value.</param>
    /// <returns>Color for the magnitude.</returns>
    public Color GetColorForMagnitude(float magnitude)
    {
        float db = magnitude > 0 ? 20f * MathF.Log10(magnitude) : SpectrogramMinDb;
        float normalized = Math.Clamp((db - SpectrogramMinDb) / (SpectrogramMaxDb - SpectrogramMinDb), 0f, 1f);

        return ColorMapIndex switch
        {
            0 => GetHeatColor(normalized),
            1 => GetGrayscaleColor(normalized),
            2 => GetPlasmaColor(normalized),
            3 => GetViridisColor(normalized),
            4 => GetMagmaColor(normalized),
            5 => GetInfernoColor(normalized),
            6 => GetTurboColor(normalized),
            _ => GetHeatColor(normalized)
        };
    }

    /// <summary>
    /// Sets the sample rate for analysis.
    /// </summary>
    /// <param name="sampleRate">Sample rate in Hz.</param>
    public void SetSampleRate(int sampleRate)
    {
        _sampleRate = sampleRate;
        MaxFrequency = sampleRate / 2f;
    }

    #endregion

    #region Private Methods

    private SpectralSelection CreateSelection()
    {
        return new SpectralSelection(
            SelectionStartTime,
            SelectionEndTime,
            SelectionMinFrequency,
            SelectionMaxFrequency);
    }

    private void ApplyOperation(SpectralOperation operation)
    {
        try
        {
            _spectralEditor.ApplyOperation(operation);
            UpdateUndoRedoState();
            SpectrogramUpdated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Operation failed: {ex.Message}";
        }
    }

    private void UpdateUndoRedoState()
    {
        UndoCount = _spectralEditor.UndoCount;
        RedoCount = _spectralEditor.RedoCount;

        UndoHistory.Clear();
        foreach (var desc in _spectralEditor.GetUndoHistory())
        {
            UndoHistory.Add(desc);
        }

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnAnalysisComplete(object? sender, EventArgs e)
    {
        FrameCount = _spectralEditor.FrameCount;
        Duration = _spectralEditor.Duration;
    }

    private void OnOperationApplied(object? sender, SpectralOperation e)
    {
        UpdateUndoRedoState();
    }

    #endregion

    #region Color Map Implementations

    private static Color GetHeatColor(float t)
    {
        byte r = (byte)(Math.Clamp(t * 3f, 0f, 1f) * 255);
        byte g = (byte)(Math.Clamp((t - 0.33f) * 3f, 0f, 1f) * 255);
        byte b = (byte)(Math.Clamp((t - 0.67f) * 3f, 0f, 1f) * 255);
        return Color.FromRgb(r, g, b);
    }

    private static Color GetGrayscaleColor(float t)
    {
        byte v = (byte)(t * 255);
        return Color.FromRgb(v, v, v);
    }

    private static Color GetPlasmaColor(float t)
    {
        float r = 0.05f + 0.95f * t;
        float g = 0.05f + 0.5f * MathF.Sin(MathF.PI * t);
        float b = 0.53f + 0.47f * MathF.Cos(MathF.PI * t);
        return Color.FromRgb((byte)(r * 255), (byte)(g * 255), (byte)(b * 255));
    }

    private static Color GetViridisColor(float t)
    {
        float r = 0.267f + 0.733f * t * t;
        float g = 0.004f + 0.873f * t - 0.294f * t * t;
        float b = 0.329f + 0.483f * t - 0.812f * t * t + 0.5f * t * t * t;
        return Color.FromRgb(
            (byte)(Math.Clamp(r, 0f, 1f) * 255),
            (byte)(Math.Clamp(g, 0f, 1f) * 255),
            (byte)(Math.Clamp(b, 0f, 1f) * 255));
    }

    private static Color GetMagmaColor(float t)
    {
        float r = t;
        float g = t * t * 0.8f;
        float b = 0.2f + 0.3f * t + 0.5f * t * t;
        return Color.FromRgb(
            (byte)(Math.Clamp(r, 0f, 1f) * 255),
            (byte)(Math.Clamp(g, 0f, 1f) * 255),
            (byte)(Math.Clamp(b, 0f, 1f) * 255));
    }

    private static Color GetInfernoColor(float t)
    {
        float r = t * 1.1f;
        float g = t * t * 0.7f;
        float b = 0.1f + 0.2f * MathF.Sin(MathF.PI * t);
        return Color.FromRgb(
            (byte)(Math.Clamp(r, 0f, 1f) * 255),
            (byte)(Math.Clamp(g, 0f, 1f) * 255),
            (byte)(Math.Clamp(b, 0f, 1f) * 255));
    }

    private static Color GetTurboColor(float t)
    {
        float r = MathF.Sin(MathF.PI * (t - 0.25f));
        float g = MathF.Sin(MathF.PI * t);
        float b = MathF.Sin(MathF.PI * (t + 0.25f));
        return Color.FromRgb(
            (byte)(Math.Clamp(r, 0f, 1f) * 255),
            (byte)(Math.Clamp(g, 0f, 1f) * 255),
            (byte)(Math.Clamp(b, 0f, 1f) * 255));
    }

    #endregion
}
