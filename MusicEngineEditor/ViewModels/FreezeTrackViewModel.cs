using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Freeze;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for track freeze/unfreeze operations.
/// </summary>
public partial class FreezeTrackViewModel : ViewModelBase
{
    private readonly FreezeManager? _freezeManager;
    private CancellationTokenSource? _freezeCts;

    [ObservableProperty]
    private int _trackIndex;

    [ObservableProperty]
    private string _trackName = "Track";

    [ObservableProperty]
    private FreezeState _freezeState = FreezeState.Live;

    [ObservableProperty]
    private double _freezeProgress;

    [ObservableProperty]
    private string _progressMessage = "";

    [ObservableProperty]
    private bool _isFreezing;

    [ObservableProperty]
    private bool _isUnfreezing;

    [ObservableProperty]
    private bool _canFreeze = true;

    [ObservableProperty]
    private bool _canUnfreeze;

    [ObservableProperty]
    private double _cpuSavings;

    [ObservableProperty]
    private string _frozenDuration = "";

    [ObservableProperty]
    private bool _includeEffects = true;

    [ObservableProperty]
    private double _tailLengthSeconds = 2.0;

    /// <summary>
    /// Event raised when freeze state changes.
    /// </summary>
    public event EventHandler<FreezeStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Event raised when freeze operation completes.
    /// </summary>
    public event EventHandler<FreezeCompletedEventArgs>? FreezeCompleted;

    /// <summary>
    /// Event raised when unfreeze operation completes.
    /// </summary>
    public event EventHandler<UnfreezeCompletedEventArgs>? UnfreezeCompleted;

    public FreezeTrackViewModel()
    {
        // Design-time constructor
    }

    public FreezeTrackViewModel(FreezeManager freezeManager, int trackIndex, string trackName)
    {
        _freezeManager = freezeManager ?? throw new ArgumentNullException(nameof(freezeManager));
        _trackIndex = trackIndex;
        _trackName = trackName;

        // Subscribe to manager events
        _freezeManager.FreezeStateChanged += OnFreezeStateChanged;
        _freezeManager.FreezeCompleted += OnFreezeCompleted;
        _freezeManager.UnfreezeCompleted += OnUnfreezeCompleted;

        // Get initial state
        FreezeState = _freezeManager.GetTrackState(trackIndex);
        UpdateStateProperties();
    }

    private void OnFreezeStateChanged(object? sender, FreezeStateChangedEventArgs e)
    {
        if (e.TrackIndex != TrackIndex)
            return;

        FreezeState = e.NewState;
        UpdateStateProperties();
        StateChanged?.Invoke(this, e);
    }

    private void OnFreezeCompleted(object? sender, FreezeCompletedEventArgs e)
    {
        if (e.TrackIndex != TrackIndex)
            return;

        IsFreezing = false;
        FreezeProgress = 100;

        if (e.Success)
        {
            StatusMessage = $"Track frozen successfully ({e.Duration.TotalSeconds:F1}s)";
            FrozenDuration = $"{e.FrozenLengthSeconds:F1}s";

            // Estimate CPU savings (simplified - real implementation would measure)
            CpuSavings = 75; // Placeholder
        }
        else
        {
            StatusMessage = $"Freeze failed: {e.ErrorMessage}";
        }

        ProgressMessage = "";
        FreezeCompleted?.Invoke(this, e);
    }

    private void OnUnfreezeCompleted(object? sender, UnfreezeCompletedEventArgs e)
    {
        if (e.TrackIndex != TrackIndex)
            return;

        IsUnfreezing = false;
        FreezeProgress = 0;
        CpuSavings = 0;
        FrozenDuration = "";

        if (e.Success)
        {
            StatusMessage = "Track unfrozen successfully";
        }
        else
        {
            StatusMessage = $"Unfreeze failed: {e.ErrorMessage}";
        }

        ProgressMessage = "";
        UnfreezeCompleted?.Invoke(this, e);
    }

    private void UpdateStateProperties()
    {
        switch (FreezeState)
        {
            case FreezeState.Live:
                CanFreeze = true;
                CanUnfreeze = false;
                IsFreezing = false;
                IsUnfreezing = false;
                break;

            case FreezeState.Freezing:
                CanFreeze = false;
                CanUnfreeze = false;
                IsFreezing = true;
                IsUnfreezing = false;
                break;

            case FreezeState.Frozen:
                CanFreeze = false;
                CanUnfreeze = true;
                IsFreezing = false;
                IsUnfreezing = false;
                break;

            case FreezeState.Unfreezing:
                CanFreeze = false;
                CanUnfreeze = false;
                IsFreezing = false;
                IsUnfreezing = true;
                break;
        }
    }

    [RelayCommand(CanExecute = nameof(CanFreeze))]
    private async Task FreezeAsync()
    {
        if (_freezeManager == null)
            return;

        _freezeCts?.Cancel();
        _freezeCts = new CancellationTokenSource();

        IsBusy = true;
        IsFreezing = true;
        FreezeProgress = 0;
        StatusMessage = "Freezing track...";

        try
        {
            // Configure freeze manager
            _freezeManager.FreezeWithEffects = IncludeEffects;
            _freezeManager.TailLengthSeconds = TailLengthSeconds;

            var progress = new Progress<RenderProgress>(OnProgressUpdate);

            await _freezeManager.FreezeTrackAsync(
                TrackIndex,
                progress: progress,
                cancellationToken: _freezeCts.Token);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Freeze cancelled";
            IsFreezing = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Freeze error: {ex.Message}";
            IsFreezing = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanUnfreeze))]
    private void Unfreeze()
    {
        if (_freezeManager == null)
            return;

        IsBusy = true;
        IsUnfreezing = true;
        StatusMessage = "Unfreezing track...";

        try
        {
            _freezeManager.UnfreezeTrack(TrackIndex, deleteAudioFile: false);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Unfreeze error: {ex.Message}";
            IsUnfreezing = false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void CancelFreeze()
    {
        _freezeCts?.Cancel();
    }

    private void OnProgressUpdate(RenderProgress progress)
    {
        FreezeProgress = progress.PercentComplete;
        ProgressMessage = $"{progress.Stage}: {progress.PercentComplete:F0}%";

        if (progress.EstimatedTimeRemaining.HasValue)
        {
            ProgressMessage += $" (ETA: {progress.EstimatedTimeRemaining.Value:mm\\:ss})";
        }
    }

    /// <summary>
    /// Gets the state display text.
    /// </summary>
    public string StateText => FreezeState switch
    {
        FreezeState.Live => "Live",
        FreezeState.Freezing => "Freezing...",
        FreezeState.Frozen => "Frozen",
        FreezeState.Unfreezing => "Unfreezing...",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the state icon character.
    /// </summary>
    public string StateIcon => FreezeState switch
    {
        FreezeState.Live => "\u25B6", // Play symbol
        FreezeState.Freezing => "\u23F3", // Hourglass
        FreezeState.Frozen => "\u2744", // Snowflake
        FreezeState.Unfreezing => "\u23F3", // Hourglass
        _ => "\u003F" // Question mark
    };

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        _freezeCts?.Cancel();
        _freezeCts?.Dispose();

        if (_freezeManager != null)
        {
            _freezeManager.FreezeStateChanged -= OnFreezeStateChanged;
            _freezeManager.FreezeCompleted -= OnFreezeCompleted;
            _freezeManager.UnfreezeCompleted -= OnUnfreezeCompleted;
        }
    }
}
