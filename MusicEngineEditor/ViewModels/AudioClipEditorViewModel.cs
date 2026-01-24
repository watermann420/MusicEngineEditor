using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Audio Clip Editor, providing editing operations for AudioClip instances.
/// </summary>
public partial class AudioClipEditorViewModel : ViewModelBase
{
    #region Observable Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasClip))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    [NotifyPropertyChangedFor(nameof(ClipName))]
    [NotifyPropertyChangedFor(nameof(ClipLength))]
    [NotifyPropertyChangedFor(nameof(GainDb))]
    [NotifyPropertyChangedFor(nameof(TimeStretchFactor))]
    [NotifyPropertyChangedFor(nameof(IsReversed))]
    [NotifyPropertyChangedFor(nameof(FadeInDuration))]
    [NotifyPropertyChangedFor(nameof(FadeOutDuration))]
    [NotifyPropertyChangedFor(nameof(FadeInType))]
    [NotifyPropertyChangedFor(nameof(FadeOutType))]
    private AudioClip? _selectedClip;

    [ObservableProperty]
    private double _selectionStart;

    [ObservableProperty]
    private double _selectionEnd;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _hasValidSelection;

    [ObservableProperty]
    private double _zoomLevelX = 1.0;

    [ObservableProperty]
    private double _zoomLevelY = 1.0;

    [ObservableProperty]
    private double _playheadPosition;

    [ObservableProperty]
    private double _scrollOffset;

    [ObservableProperty]
    private float _normalizeTargetDb = 0f;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets whether a clip is currently loaded.
    /// </summary>
    public bool HasClip => SelectedClip != null;

    /// <summary>
    /// Gets whether the clip can be edited (not locked).
    /// </summary>
    public bool CanEdit => SelectedClip != null && !SelectedClip.IsLocked;

    /// <summary>
    /// Gets whether there is a valid selection.
    /// </summary>
    public bool HasSelection => HasValidSelection && SelectionEnd > SelectionStart;

    /// <summary>
    /// Gets the clip name.
    /// </summary>
    public string ClipName => SelectedClip?.Name ?? "No Clip Selected";

    /// <summary>
    /// Gets the clip length in beats.
    /// </summary>
    public double ClipLength => SelectedClip?.Length ?? 0;

    /// <summary>
    /// Gets or sets the gain in dB.
    /// </summary>
    public float GainDb
    {
        get => SelectedClip?.GainDb ?? 0f;
        set
        {
            if (SelectedClip != null && Math.Abs(SelectedClip.GainDb - value) > 0.001f)
            {
                AudioClipEditor.SetGain(SelectedClip, value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the time stretch factor.
    /// </summary>
    public double TimeStretchFactor
    {
        get => SelectedClip?.TimeStretchFactor ?? 1.0;
        set
        {
            if (SelectedClip != null && Math.Abs(SelectedClip.TimeStretchFactor - value) > 0.001)
            {
                AudioClipEditor.TimeStretch(SelectedClip, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClipLength));
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the clip is reversed.
    /// </summary>
    public bool IsReversed
    {
        get => SelectedClip?.IsReversed ?? false;
        set
        {
            if (SelectedClip != null && SelectedClip.IsReversed != value)
            {
                AudioClipEditor.SetReversed(SelectedClip, value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fade-in duration.
    /// </summary>
    public double FadeInDuration
    {
        get => SelectedClip?.FadeInDuration ?? 0;
        set
        {
            if (SelectedClip != null && Math.Abs(SelectedClip.FadeInDuration - value) > 0.001)
            {
                AudioClipEditor.FadeIn(SelectedClip, value, SelectedClip.FadeInType);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fade-out duration.
    /// </summary>
    public double FadeOutDuration
    {
        get => SelectedClip?.FadeOutDuration ?? 0;
        set
        {
            if (SelectedClip != null && Math.Abs(SelectedClip.FadeOutDuration - value) > 0.001)
            {
                AudioClipEditor.FadeOut(SelectedClip, value, SelectedClip.FadeOutType);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fade-in type.
    /// </summary>
    public FadeType FadeInType
    {
        get => SelectedClip?.FadeInType ?? FadeType.Linear;
        set
        {
            if (SelectedClip != null && SelectedClip.FadeInType != value)
            {
                AudioClipEditor.FadeIn(SelectedClip, SelectedClip.FadeInDuration, value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the fade-out type.
    /// </summary>
    public FadeType FadeOutType
    {
        get => SelectedClip?.FadeOutType ?? FadeType.Linear;
        set
        {
            if (SelectedClip != null && SelectedClip.FadeOutType != value)
            {
                AudioClipEditor.FadeOut(SelectedClip, SelectedClip.FadeOutDuration, value);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the available fade types for binding.
    /// </summary>
    public IEnumerable<FadeType> FadeTypes => Enum.GetValues<FadeType>();

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a clip split operation creates a new clip.
    /// </summary>
    public event EventHandler<AudioClip>? ClipSplit;

    /// <summary>
    /// Event raised when the clip has been modified.
    /// </summary>
    public event EventHandler? ClipModified;

    #endregion

    #region Commands

    /// <summary>
    /// Command to normalize the clip.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Normalize()
    {
        if (SelectedClip == null) return;

        // For demo purposes, assume current peak is -3dB (in real implementation, analyze the audio)
        var currentPeakDb = -3f;
        AudioClipEditor.Normalize(SelectedClip, currentPeakDb, NormalizeTargetDb);
        OnPropertyChanged(nameof(GainDb));
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to toggle reverse state.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Reverse()
    {
        if (SelectedClip == null) return;

        AudioClipEditor.Reverse(SelectedClip);
        OnPropertyChanged(nameof(IsReversed));
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to reset gain to 0 dB.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetGain()
    {
        if (SelectedClip == null) return;

        AudioClipEditor.ResetGain(SelectedClip);
        OnPropertyChanged(nameof(GainDb));
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to split the clip at the playhead position.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSplitAtPlayhead))]
    private void SplitAtPlayhead()
    {
        if (SelectedClip == null) return;

        var splitPosition = SelectedClip.StartPosition + PlayheadPosition;
        if (!SelectedClip.ContainsPosition(splitPosition)) return;

        try
        {
            var (_, rightClip) = AudioClipEditor.Split(SelectedClip, splitPosition);
            ClipSplit?.Invoke(this, rightClip);
            ClipModified?.Invoke(this, EventArgs.Empty);
            OnPropertyChanged(nameof(ClipLength));
        }
        catch (Exception ex)
        {
            StatusMessage = $"Split failed: {ex.Message}";
        }
    }

    private bool CanSplitAtPlayhead()
    {
        if (SelectedClip == null || SelectedClip.IsLocked) return false;
        var absPosition = SelectedClip.StartPosition + PlayheadPosition;
        return SelectedClip.ContainsPosition(absPosition) &&
               PlayheadPosition > 0 &&
               PlayheadPosition < SelectedClip.Length;
    }

    /// <summary>
    /// Command to trim clip to selection.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanTrimToSelection))]
    private void TrimToSelection()
    {
        if (SelectedClip == null || !HasSelection) return;

        try
        {
            var startOffset = SelectionStart;
            var newLength = SelectionEnd - SelectionStart;
            AudioClipEditor.TrimToRegion(SelectedClip, startOffset, newLength);
            ClearSelection();
            OnPropertyChanged(nameof(ClipLength));
            ClipModified?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Trim failed: {ex.Message}";
        }
    }

    private bool CanTrimToSelection() => CanEdit && HasSelection;

    /// <summary>
    /// Command to remove all fades.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void RemoveFades()
    {
        if (SelectedClip == null) return;

        AudioClipEditor.RemoveFades(SelectedClip);
        OnPropertyChanged(nameof(FadeInDuration));
        OnPropertyChanged(nameof(FadeOutDuration));
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Command to zoom in horizontally.
    /// </summary>
    [RelayCommand]
    private void ZoomInX()
    {
        ZoomLevelX = Math.Min(ZoomLevelX * 1.5, 16.0);
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
    /// Command to zoom in vertically.
    /// </summary>
    [RelayCommand]
    private void ZoomInY()
    {
        ZoomLevelY = Math.Min(ZoomLevelY * 1.25, 4.0);
    }

    /// <summary>
    /// Command to zoom out vertically.
    /// </summary>
    [RelayCommand]
    private void ZoomOutY()
    {
        ZoomLevelY = Math.Max(ZoomLevelY / 1.25, 0.25);
    }

    /// <summary>
    /// Command to reset zoom to default.
    /// </summary>
    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevelX = 1.0;
        ZoomLevelY = 1.0;
    }

    /// <summary>
    /// Command to reset time stretch to original speed.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ResetTimeStretch()
    {
        if (SelectedClip == null) return;

        AudioClipEditor.ResetTimeStretch(SelectedClip);
        OnPropertyChanged(nameof(TimeStretchFactor));
        OnPropertyChanged(nameof(ClipLength));
        ClipModified?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Methods

    /// <summary>
    /// Loads a clip for editing.
    /// </summary>
    /// <param name="clip">The audio clip to edit.</param>
    public void LoadClip(AudioClip clip)
    {
        SelectedClip = clip;
        ClearSelection();
        PlayheadPosition = 0;
        ScrollOffset = 0;
        ResetZoom();
    }

    /// <summary>
    /// Sets the selection range.
    /// </summary>
    /// <param name="start">Start position within clip (in beats).</param>
    /// <param name="end">End position within clip (in beats).</param>
    public void SetSelection(double start, double end)
    {
        SelectionStart = Math.Min(start, end);
        SelectionEnd = Math.Max(start, end);
        HasValidSelection = SelectionEnd > SelectionStart;
    }

    /// <summary>
    /// Clears the current selection.
    /// </summary>
    public void ClearSelection()
    {
        SelectionStart = 0;
        SelectionEnd = 0;
        HasValidSelection = false;
    }

    /// <summary>
    /// Duplicates the current clip.
    /// </summary>
    /// <returns>The duplicated clip, or null if no clip is selected.</returns>
    public AudioClip? DuplicateClip()
    {
        if (SelectedClip == null) return null;
        return SelectedClip.Duplicate();
    }

    #endregion
}
