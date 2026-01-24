using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core.Freeze;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for freezing/unfreezing a track with progress indication.
/// </summary>
public partial class FreezeTrackControl : UserControl
{
    private FreezeTrackViewModel? _viewModel;
    private FreezeState _currentState = FreezeState.Live;

    /// <summary>
    /// Event raised when freeze is requested.
    /// </summary>
    public event EventHandler? FreezeRequested;

    /// <summary>
    /// Event raised when unfreeze is requested.
    /// </summary>
    public event EventHandler? UnfreezeRequested;

    /// <summary>
    /// Event raised when cancel is requested.
    /// </summary>
    public event EventHandler? CancelRequested;

    /// <summary>
    /// Gets or sets the freeze state.
    /// </summary>
    public FreezeState State
    {
        get => _currentState;
        set
        {
            _currentState = value;
            UpdateStateVisuals();
        }
    }

    /// <summary>
    /// Gets or sets the freeze progress (0-100).
    /// </summary>
    public double Progress
    {
        get => FreezeProgressBar.Value;
        set
        {
            FreezeProgressBar.Value = value;
            ProgressText.Text = $"{value:F0}%";
        }
    }

    /// <summary>
    /// Gets or sets the CPU savings percentage.
    /// </summary>
    public double CpuSavings
    {
        get => double.TryParse(CpuSavingsText.Text.TrimEnd('%'), out var v) ? v : 0;
        set
        {
            if (value > 0)
            {
                CpuSavingsText.Text = $"CPU: -{value:F0}%";
                CpuSavingsText.Visibility = Visibility.Visible;
            }
            else
            {
                CpuSavingsText.Visibility = Visibility.Collapsed;
            }
        }
    }

    /// <summary>
    /// Gets or sets the frozen duration display.
    /// </summary>
    public string FrozenDuration
    {
        get => FrozenDurationText.Text;
        set => FrozenDurationText.Text = value;
    }

    public FreezeTrackControl()
    {
        InitializeComponent();
        UpdateStateVisuals();
    }

    /// <summary>
    /// Binds to a FreezeTrackViewModel.
    /// </summary>
    public void BindViewModel(FreezeTrackViewModel viewModel)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = viewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            State = _viewModel.FreezeState;
            Progress = _viewModel.FreezeProgress;
            CpuSavings = _viewModel.CpuSavings;
            FrozenDuration = _viewModel.FrozenDuration;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel == null) return;

        Dispatcher.Invoke(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(FreezeTrackViewModel.FreezeState):
                    State = _viewModel.FreezeState;
                    break;
                case nameof(FreezeTrackViewModel.FreezeProgress):
                    Progress = _viewModel.FreezeProgress;
                    break;
                case nameof(FreezeTrackViewModel.CpuSavings):
                    CpuSavings = _viewModel.CpuSavings;
                    break;
                case nameof(FreezeTrackViewModel.FrozenDuration):
                    FrozenDuration = _viewModel.FrozenDuration;
                    break;
            }
        });
    }

    private void UpdateStateVisuals()
    {
        switch (_currentState)
        {
            case FreezeState.Live:
                StateIcon.Text = "\u25B6"; // Play symbol
                StateIcon.Foreground = FindResource("LiveStateBrush") as Brush ?? Brushes.Green;
                StateText.Text = "Live";

                FreezeButton.Visibility = Visibility.Visible;
                UnfreezeButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Collapsed;
                ProgressSection.Visibility = Visibility.Collapsed;
                FrozenDurationText.Visibility = Visibility.Collapsed;
                CpuSavingsText.Visibility = Visibility.Collapsed;
                break;

            case FreezeState.Freezing:
                StateIcon.Text = "\u23F3"; // Hourglass
                StateIcon.Foreground = FindResource("FreezingStateBrush") as Brush ?? Brushes.Orange;
                StateText.Text = "Freezing...";

                FreezeButton.Visibility = Visibility.Collapsed;
                UnfreezeButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Visible;
                ProgressSection.Visibility = Visibility.Visible;
                FrozenDurationText.Visibility = Visibility.Collapsed;
                break;

            case FreezeState.Frozen:
                StateIcon.Text = "\u2744"; // Snowflake
                StateIcon.Foreground = FindResource("FrozenStateBrush") as Brush ?? Brushes.LightBlue;
                StateText.Text = "Frozen";

                FreezeButton.Visibility = Visibility.Collapsed;
                UnfreezeButton.Visibility = Visibility.Visible;
                CancelButton.Visibility = Visibility.Collapsed;
                ProgressSection.Visibility = Visibility.Collapsed;
                FrozenDurationText.Visibility = !string.IsNullOrEmpty(FrozenDuration)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
                break;

            case FreezeState.Unfreezing:
                StateIcon.Text = "\u23F3"; // Hourglass
                StateIcon.Foreground = FindResource("FreezingStateBrush") as Brush ?? Brushes.Orange;
                StateText.Text = "Unfreezing...";

                FreezeButton.Visibility = Visibility.Collapsed;
                UnfreezeButton.Visibility = Visibility.Collapsed;
                CancelButton.Visibility = Visibility.Visible;
                ProgressSection.Visibility = Visibility.Visible;
                FrozenDurationText.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private async void FreezeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.FreezeCommand.CanExecute(null))
            {
                await _viewModel.FreezeCommand.ExecuteAsync(null);
            }
        }
        else
        {
            FreezeRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void UnfreezeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.UnfreezeCommand.CanExecute(null))
            {
                _viewModel.UnfreezeCommand.Execute(null);
            }
        }
        else
        {
            UnfreezeRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            if (_viewModel.CancelFreezeCommand.CanExecute(null))
            {
                _viewModel.CancelFreezeCommand.Execute(null);
            }
        }
        else
        {
            CancelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Sets the progress with an optional message.
    /// </summary>
    public void SetProgress(double percent, string? message = null)
    {
        Progress = percent;
        if (message != null)
        {
            ProgressText.Text = message;
        }
    }

    /// <summary>
    /// Resets the control to live state.
    /// </summary>
    public void Reset()
    {
        State = FreezeState.Live;
        Progress = 0;
        CpuSavings = 0;
        FrozenDuration = "";
    }
}
