// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Session View.

using System;
using System.Windows;
using System.Windows.Controls;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Views;

/// <summary>
/// Interaction logic for SessionView.xaml.
/// Provides an Ableton-style session view with clip launcher grid.
/// </summary>
public partial class SessionView : UserControl, IDisposable
{
    private SessionViewModel? _viewModel;
    private bool _disposed;

    /// <summary>
    /// Dependency property for external ViewModel binding.
    /// </summary>
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(SessionViewModel),
            typeof(SessionView),
            new PropertyMetadata(null, OnViewModelChanged));

    /// <summary>
    /// Gets or sets the SessionViewModel.
    /// </summary>
    public SessionViewModel? ViewModel
    {
        get => (SessionViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <summary>
    /// Event raised when a clip edit is requested.
    /// </summary>
    public event EventHandler<ClipSlotViewModel>? ClipEditRequested;

    /// <summary>
    /// Creates a new SessionView.
    /// </summary>
    public SessionView()
    {
        InitializeComponent();

        // Create default ViewModel if not provided externally
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_viewModel == null && ViewModel == null)
        {
            _viewModel = new SessionViewModel();
            DataContext = _viewModel;

            // Subscribe to clip edit requests
            _viewModel.ClipEditRequested += OnClipEditRequested;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup will be done in Dispose
    }

    private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is SessionView view)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is SessionViewModel oldVm)
            {
                oldVm.ClipEditRequested -= view.OnClipEditRequested;
            }

            // Subscribe to new ViewModel
            if (e.NewValue is SessionViewModel newVm)
            {
                view._viewModel = newVm;
                view.DataContext = newVm;
                newVm.ClipEditRequested += view.OnClipEditRequested;
            }
        }
    }

    private void OnClipEditRequested(object? sender, ClipSlotViewModel slot)
    {
        ClipEditRequested?.Invoke(this, slot);
    }

    /// <summary>
    /// Gets the current SessionViewModel.
    /// </summary>
    /// <returns>The SessionViewModel.</returns>
    public SessionViewModel? GetViewModel()
    {
        return _viewModel ?? ViewModel;
    }

    /// <summary>
    /// Starts the session.
    /// </summary>
    public void Start()
    {
        GetViewModel()?.StartCommand.Execute(null);
    }

    /// <summary>
    /// Stops the session.
    /// </summary>
    public void Stop()
    {
        GetViewModel()?.StopCommand.Execute(null);
    }

    /// <summary>
    /// Stops all clips.
    /// </summary>
    public void StopAll()
    {
        GetViewModel()?.StopAllCommand.Execute(null);
    }

    /// <summary>
    /// Resets the session to beat 0.
    /// </summary>
    public void Reset()
    {
        GetViewModel()?.ResetCommand.Execute(null);
    }

    /// <summary>
    /// Launches a clip at the specified position.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    /// <param name="sceneIndex">Scene index.</param>
    public void LaunchClip(int trackIndex, int sceneIndex)
    {
        var vm = GetViewModel();
        if (vm == null) return;

        var slot = FindSlot(trackIndex, sceneIndex);
        if (slot != null)
        {
            vm.LaunchClipCommand.Execute(slot);
        }
    }

    /// <summary>
    /// Launches a scene.
    /// </summary>
    /// <param name="sceneIndex">Scene index.</param>
    public void LaunchScene(int sceneIndex)
    {
        var vm = GetViewModel();
        if (vm == null || sceneIndex < 0 || sceneIndex >= vm.Scenes.Count) return;

        vm.LaunchSceneCommand.Execute(vm.Scenes[sceneIndex]);
    }

    /// <summary>
    /// Stops a track.
    /// </summary>
    /// <param name="trackIndex">Track index.</param>
    public void StopTrack(int trackIndex)
    {
        var vm = GetViewModel();
        if (vm == null || trackIndex < 0 || trackIndex >= vm.Tracks.Count) return;

        vm.StopTrackCommand.Execute(vm.Tracks[trackIndex]);
    }

    /// <summary>
    /// Finds a clip slot by position.
    /// </summary>
    private ClipSlotViewModel? FindSlot(int trackIndex, int sceneIndex)
    {
        var vm = GetViewModel();
        if (vm == null) return null;

        foreach (var slot in vm.ClipSlots)
        {
            if (slot.TrackIndex == trackIndex && slot.SceneIndex == sceneIndex)
            {
                return slot;
            }
        }
        return null;
    }

    /// <summary>
    /// Processes the clip launcher for the given time delta.
    /// Should be called from the audio engine.
    /// </summary>
    /// <param name="deltaBeats">Time elapsed in beats.</param>
    public void Process(double deltaBeats)
    {
        GetViewModel()?.Process(deltaBeats);
    }

    /// <summary>
    /// Sets the tempo.
    /// </summary>
    /// <param name="bpm">Tempo in BPM.</param>
    public void SetTempo(double bpm)
    {
        var vm = GetViewModel();
        if (vm != null)
        {
            vm.Bpm = bpm;
        }
    }

    /// <summary>
    /// Disposes the SessionView and its resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_viewModel != null)
        {
            _viewModel.ClipEditRequested -= OnClipEditRequested;
            _viewModel.Dispose();
            _viewModel = null;
        }

        GC.SuppressFinalize(this);
    }
}
