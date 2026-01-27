// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Performance Mode / Scene Manager View.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngine.Core.Performance;

namespace MusicEngineEditor.Views;

/// <summary>
/// Interaction logic for PerformanceModeView.xaml.
/// Provides a scene manager for live performance with crossfade control and MIDI triggers.
/// </summary>
public partial class PerformanceModeView : UserControl, IDisposable, INotifyPropertyChanged
{
    private PerformanceMode? _performanceMode;
    private bool _disposed;
    private SceneViewModel? _selectedScene;
    private bool _isEditMode;
    private bool _isMidiLearnMode;
    private string _statusMessage = "Ready";
    private double _crossfadePosition;

    /// <summary>
    /// Creates a new PerformanceModeView.
    /// </summary>
    public PerformanceModeView()
    {
        InitializeComponent();
        DataContext = this;

        // Initialize commands
        AddSceneCommand = new RelayCommand(AddScene);
        DuplicateSceneCommand = new RelayCommand(DuplicateScene, () => SelectedScene != null);
        DeleteSceneCommand = new RelayCommand(DeleteScene, () => SelectedScene != null);
        ToggleEditModeCommand = new RelayCommand(ToggleEditMode);
        ToggleMidiLearnCommand = new RelayCommand(ToggleMidiLearn);
        SetSceneColorCommand = new RelayCommand<string>(SetSceneColor);
        AddTriggerCommand = new RelayCommand(AddTrigger, () => SelectedScene != null && IsEditMode);
        RemoveTriggerCommand = new RelayCommand<SceneTrigger>(RemoveTrigger);
        StartMidiLearnForSceneCommand = new RelayCommand(StartMidiLearnForScene, () => SelectedScene != null);
        TriggerSelectedSceneCommand = new RelayCommand(TriggerSelectedScene, () => SelectedScene != null);
        CrossfadeToSelectedSceneCommand = new RelayCommand(CrossfadeToSelectedScene, () => SelectedScene != null);
        PreviousSceneCommand = new RelayCommand(PreviousScene);
        NextSceneCommand = new RelayCommand(NextScene);

        // Initialize collections
        Scenes = new ObservableCollection<SceneViewModel>();
        TransitionModes = new ObservableCollection<SceneTransitionMode>(
            (SceneTransitionMode[])Enum.GetValues(typeof(SceneTransitionMode)));

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #region Dependency Properties

    /// <summary>
    /// Dependency property for external PerformanceMode binding.
    /// </summary>
    public static readonly DependencyProperty PerformanceModeProperty =
        DependencyProperty.Register(
            nameof(PerformanceMode),
            typeof(PerformanceMode),
            typeof(PerformanceModeView),
            new PropertyMetadata(null, OnPerformanceModeChanged));

    /// <summary>
    /// Gets or sets the PerformanceMode engine instance.
    /// </summary>
    public PerformanceMode? PerformanceMode
    {
        get => (PerformanceMode?)GetValue(PerformanceModeProperty);
        set => SetValue(PerformanceModeProperty, value);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the collection of scenes.
    /// </summary>
    public ObservableCollection<SceneViewModel> Scenes { get; }

    /// <summary>
    /// Gets the available transition modes.
    /// </summary>
    public ObservableCollection<SceneTransitionMode> TransitionModes { get; }

    /// <summary>
    /// Gets or sets the currently selected scene.
    /// </summary>
    public SceneViewModel? SelectedScene
    {
        get => _selectedScene;
        set
        {
            if (_selectedScene != value)
            {
                _selectedScene = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSelectedScene));
                UpdateCrossfadeLabels();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Gets whether a scene is selected.
    /// </summary>
    public bool HasSelectedScene => SelectedScene != null;

    /// <summary>
    /// Gets whether there are multiple scenes for crossfading.
    /// </summary>
    public bool HasMultipleScenes => Scenes.Count > 1;

    /// <summary>
    /// Gets the scene count.
    /// </summary>
    public int SceneCount => Scenes.Count;

    /// <summary>
    /// Gets or sets whether edit mode is enabled.
    /// </summary>
    public bool IsEditMode
    {
        get => _isEditMode;
        set
        {
            if (_isEditMode != value)
            {
                _isEditMode = value;
                OnPropertyChanged();
                StatusMessage = value ? "Edit mode enabled" : "Edit mode disabled";
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether MIDI learn mode is enabled.
    /// </summary>
    public bool IsMidiLearnMode
    {
        get => _isMidiLearnMode;
        set
        {
            if (_isMidiLearnMode != value)
            {
                _isMidiLearnMode = value;
                OnPropertyChanged();
                StatusMessage = value ? "MIDI Learn: Waiting for input..." : "MIDI Learn cancelled";
            }
        }
    }

    /// <summary>
    /// Gets whether a crossfade is in progress.
    /// </summary>
    public bool IsCrossfading => _performanceMode?.IsCrossfading ?? false;

    /// <summary>
    /// Gets the crossfade progress (0-100).
    /// </summary>
    public float CrossfadeProgress => (_performanceMode?.CrossfadeProgress ?? 0f) * 100f;

    /// <summary>
    /// Gets or sets the crossfade position (0-100) for manual crossfading.
    /// </summary>
    public double CrossfadePosition
    {
        get => _crossfadePosition;
        set
        {
            if (Math.Abs(_crossfadePosition - value) > 0.001)
            {
                _crossfadePosition = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the name of the source scene for crossfade display.
    /// </summary>
    public string CrossfadeFromSceneName
    {
        get
        {
            if (Scenes.Count == 0) return "Scene A";
            var current = _performanceMode?.CurrentScene;
            return current?.Name ?? (Scenes.Count > 0 ? Scenes[0].Name : "Scene A");
        }
    }

    /// <summary>
    /// Gets the name of the target scene for crossfade display.
    /// </summary>
    public string CrossfadeToSceneName
    {
        get
        {
            if (SelectedScene != null) return SelectedScene.Name;
            if (Scenes.Count > 1) return Scenes[1].Name;
            return "Scene B";
        }
    }

    /// <summary>
    /// Gets or sets the status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage != value)
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }
    }

    #endregion

    #region Commands

    public ICommand AddSceneCommand { get; }
    public ICommand DuplicateSceneCommand { get; }
    public ICommand DeleteSceneCommand { get; }
    public ICommand ToggleEditModeCommand { get; }
    public ICommand ToggleMidiLearnCommand { get; }
    public ICommand SetSceneColorCommand { get; }
    public ICommand AddTriggerCommand { get; }
    public ICommand RemoveTriggerCommand { get; }
    public ICommand StartMidiLearnForSceneCommand { get; }
    public ICommand TriggerSelectedSceneCommand { get; }
    public ICommand CrossfadeToSelectedSceneCommand { get; }
    public ICommand PreviousSceneCommand { get; }
    public ICommand NextSceneCommand { get; }

    #endregion

    #region Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_performanceMode == null && PerformanceMode == null)
        {
            // Create default PerformanceMode if not provided externally
            _performanceMode = new PerformanceMode();
            SubscribeToEvents();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup will be done in Dispose
    }

    private static void OnPerformanceModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PerformanceModeView view)
        {
            // Unsubscribe from old instance
            if (e.OldValue is PerformanceMode oldPm)
            {
                view.UnsubscribeFromEvents(oldPm);
            }

            // Subscribe to new instance
            if (e.NewValue is PerformanceMode newPm)
            {
                view._performanceMode = newPm;
                view.SubscribeToEvents();
                view.SyncScenesFromEngine();
            }
        }
    }

    private void SubscribeToEvents()
    {
        if (_performanceMode == null) return;

        _performanceMode.SceneChanged += OnSceneChanged;
        _performanceMode.CrossfadeProgressChanged += OnCrossfadeProgressChanged;
        _performanceMode.CrossfadeComplete += OnCrossfadeComplete;
        _performanceMode.MidiLearned += OnMidiLearned;
    }

    private void UnsubscribeFromEvents(PerformanceMode pm)
    {
        pm.SceneChanged -= OnSceneChanged;
        pm.CrossfadeProgressChanged -= OnCrossfadeProgressChanged;
        pm.CrossfadeComplete -= OnCrossfadeComplete;
        pm.MidiLearned -= OnMidiLearned;
    }

    private void OnSceneChanged(object? sender, SceneChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateActiveScenes();
            OnPropertyChanged(nameof(IsCrossfading));
            StatusMessage = e.IsCrossfading
                ? $"Crossfading to '{e.NewScene.Name}'..."
                : $"Scene '{e.NewScene.Name}' activated";
        });
    }

    private void OnCrossfadeProgressChanged(object? sender, CrossfadeProgressEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            OnPropertyChanged(nameof(CrossfadeProgress));
            OnPropertyChanged(nameof(IsCrossfading));
        });
    }

    private void OnCrossfadeComplete(object? sender, PerformanceScene scene)
    {
        Dispatcher.Invoke(() =>
        {
            UpdateActiveScenes();
            OnPropertyChanged(nameof(IsCrossfading));
            OnPropertyChanged(nameof(CrossfadeProgress));
            StatusMessage = $"Crossfade to '{scene.Name}' complete";
        });
    }

    private void OnMidiLearned(object? sender, PerformanceMidiLearnEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            IsMidiLearnMode = false;

            if (e.Scene != null)
            {
                StatusMessage = $"MIDI trigger learned: Note {e.Note} (Ch {e.Channel})";
                SyncScenesFromEngine();
            }
            else if (e.Mapping != null)
            {
                StatusMessage = $"MIDI mapping learned: CC {e.CC} (Ch {e.Channel})";
            }
        });
    }

    private void Scene_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is FrameworkElement element && element.DataContext is SceneViewModel scene)
        {
            // Double-click triggers the scene
            TriggerScene(scene);
            e.Handled = true;
        }
    }

    private void Scene_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Single click is handled by ListBox selection
    }

    #endregion

    #region Command Implementations

    private void AddScene()
    {
        var scene = _performanceMode?.CreateScene($"Scene {Scenes.Count + 1}");
        if (scene != null)
        {
            var vm = new SceneViewModel(scene);
            Scenes.Add(vm);
            SelectedScene = vm;
            OnPropertyChanged(nameof(SceneCount));
            OnPropertyChanged(nameof(HasMultipleScenes));
            StatusMessage = $"Added scene '{scene.Name}'";
        }
    }

    private void DuplicateScene()
    {
        if (SelectedScene == null || _performanceMode == null) return;

        var cloned = SelectedScene.Scene.Clone();
        cloned.Name = $"{SelectedScene.Name} (Copy)";
        _performanceMode.AddScene(cloned);

        var vm = new SceneViewModel(cloned);
        Scenes.Add(vm);
        SelectedScene = vm;
        OnPropertyChanged(nameof(SceneCount));
        OnPropertyChanged(nameof(HasMultipleScenes));
        StatusMessage = $"Duplicated scene as '{cloned.Name}'";
    }

    private void DeleteScene()
    {
        if (SelectedScene == null || _performanceMode == null) return;

        var name = SelectedScene.Name;
        _performanceMode.RemoveScene(SelectedScene.Scene);
        Scenes.Remove(SelectedScene);
        SelectedScene = Scenes.Count > 0 ? Scenes[0] : null;
        OnPropertyChanged(nameof(SceneCount));
        OnPropertyChanged(nameof(HasMultipleScenes));
        StatusMessage = $"Deleted scene '{name}'";
    }

    private void ToggleEditMode()
    {
        IsEditMode = !IsEditMode;
    }

    private void ToggleMidiLearn()
    {
        if (IsMidiLearnMode)
        {
            _performanceMode?.StopMidiLearn();
            IsMidiLearnMode = false;
        }
        else
        {
            IsMidiLearnMode = true;
        }
    }

    private void SetSceneColor(string? color)
    {
        if (SelectedScene == null || string.IsNullOrEmpty(color)) return;

        SelectedScene.Color = color;
        StatusMessage = $"Set scene color to {color}";
    }

    private void AddTrigger()
    {
        if (SelectedScene == null) return;

        var trigger = new SceneTrigger
        {
            Type = SceneTriggerType.Manual,
            Enabled = true
        };

        SelectedScene.Scene.Triggers.Add(trigger);
        OnPropertyChanged(nameof(SelectedScene));
        StatusMessage = "Added new trigger";
    }

    private void RemoveTrigger(SceneTrigger? trigger)
    {
        if (SelectedScene == null || trigger == null) return;

        SelectedScene.Scene.Triggers.Remove(trigger);
        OnPropertyChanged(nameof(SelectedScene));
        StatusMessage = "Removed trigger";
    }

    private void StartMidiLearnForScene()
    {
        if (SelectedScene == null || _performanceMode == null) return;

        _performanceMode.StartMidiLearnForScene(SelectedScene.Scene);
        IsMidiLearnMode = true;
        StatusMessage = "MIDI Learn: Press a note or send CC...";
    }

    private void TriggerSelectedScene()
    {
        if (SelectedScene == null) return;
        TriggerScene(SelectedScene);
    }

    private void TriggerScene(SceneViewModel scene)
    {
        _performanceMode?.TriggerScene(scene.Scene);
        UpdateActiveScenes();
        StatusMessage = $"Triggered scene '{scene.Name}'";
    }

    private void CrossfadeToSelectedScene()
    {
        if (SelectedScene == null || _performanceMode == null) return;

        _performanceMode.CrossfadeTo(SelectedScene.Scene);
        StatusMessage = $"Crossfading to '{SelectedScene.Name}'...";
    }

    private void PreviousScene()
    {
        if (Scenes.Count == 0) return;

        var currentIndex = SelectedScene != null ? Scenes.IndexOf(SelectedScene) : 0;
        var prevIndex = (currentIndex - 1 + Scenes.Count) % Scenes.Count;
        SelectedScene = Scenes[prevIndex];
    }

    private void NextScene()
    {
        if (Scenes.Count == 0) return;

        var currentIndex = SelectedScene != null ? Scenes.IndexOf(SelectedScene) : -1;
        var nextIndex = (currentIndex + 1) % Scenes.Count;
        SelectedScene = Scenes[nextIndex];
    }

    #endregion

    #region Helper Methods

    private void SyncScenesFromEngine()
    {
        if (_performanceMode == null) return;

        Scenes.Clear();
        foreach (var scene in _performanceMode.Scenes)
        {
            Scenes.Add(new SceneViewModel(scene));
        }

        UpdateActiveScenes();
        OnPropertyChanged(nameof(SceneCount));
        OnPropertyChanged(nameof(HasMultipleScenes));
    }

    private void UpdateActiveScenes()
    {
        var currentScene = _performanceMode?.CurrentScene;
        foreach (var scene in Scenes)
        {
            scene.IsActive = currentScene != null && scene.Scene.Id == currentScene.Id;
        }
    }

    private void UpdateCrossfadeLabels()
    {
        OnPropertyChanged(nameof(CrossfadeFromSceneName));
        OnPropertyChanged(nameof(CrossfadeToSceneName));
    }

    /// <summary>
    /// Processes a MIDI message for trigger detection.
    /// </summary>
    /// <param name="message">Raw MIDI bytes.</param>
    /// <returns>True if the message was handled.</returns>
    public bool ProcessMidiMessage(byte[] message)
    {
        return _performanceMode?.ProcessMidiMessage(message) ?? false;
    }

    /// <summary>
    /// Gets the underlying PerformanceMode engine.
    /// </summary>
    public PerformanceMode? GetPerformanceMode()
    {
        return _performanceMode ?? PerformanceMode;
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_performanceMode != null)
        {
            UnsubscribeFromEvents(_performanceMode);

            // Only dispose if we created it internally
            if (PerformanceMode == null)
            {
                _performanceMode.Dispose();
            }

            _performanceMode = null;
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

#region SceneViewModel

/// <summary>
/// ViewModel wrapper for PerformanceScene.
/// </summary>
public class SceneViewModel : INotifyPropertyChanged
{
    private bool _isActive;

    public SceneViewModel(PerformanceScene scene)
    {
        Scene = scene ?? throw new ArgumentNullException(nameof(scene));
    }

    /// <summary>
    /// Gets the underlying scene.
    /// </summary>
    public PerformanceScene Scene { get; }

    /// <summary>
    /// Gets or sets the scene name.
    /// </summary>
    public string Name
    {
        get => Scene.Name;
        set
        {
            if (Scene.Name != value)
            {
                Scene.Name = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the scene color.
    /// </summary>
    public string Color
    {
        get => Scene.Color;
        set
        {
            if (Scene.Color != value)
            {
                Scene.Color = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the scene triggers.
    /// </summary>
    public System.Collections.Generic.List<SceneTrigger> Triggers => Scene.Triggers;

    /// <summary>
    /// Gets or sets the transition time in milliseconds.
    /// </summary>
    public int TransitionTimeMs
    {
        get => Scene.TransitionTimeMs;
        set
        {
            if (Scene.TransitionTimeMs != value)
            {
                Scene.TransitionTimeMs = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the transition mode.
    /// </summary>
    public SceneTransitionMode TransitionMode
    {
        get => Scene.TransitionMode;
        set
        {
            if (Scene.TransitionMode != value)
            {
                Scene.TransitionMode = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether the scene is enabled.
    /// </summary>
    public bool Enabled
    {
        get => Scene.Enabled;
        set
        {
            if (Scene.Enabled != value)
            {
                Scene.Enabled = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether this scene is currently active.
    /// </summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive != value)
            {
                _isActive = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

#endregion

#region RelayCommand

/// <summary>
/// Simple relay command implementation.
/// </summary>
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => _execute();
}

/// <summary>
/// Generic relay command implementation.
/// </summary>
public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<T?, bool>? _canExecute;

    public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter)
    {
        if (_canExecute == null) return true;
        return parameter is T t ? _canExecute(t) : _canExecute(default);
    }

    public void Execute(object? parameter)
    {
        if (parameter is T t)
            _execute(t);
        else
            _execute(default);
    }
}

#endregion

#region Converters

/// <summary>
/// Converts a hex color string to a SolidColorBrush.
/// </summary>
public class PerformanceModeColorToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string colorString && !string.IsNullOrEmpty(colorString))
        {
            try
            {
                var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(colorString);
                return new SolidColorBrush(color);
            }
            catch
            {
                // Return default color on error
            }
        }
        return new SolidColorBrush(Colors.Gray);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts SceneTriggerType to display string.
/// </summary>
public class PerformanceModeTriggerTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SceneTriggerType type)
        {
            return type switch
            {
                SceneTriggerType.MidiNote => "NOTE",
                SceneTriggerType.MidiCC => "CC",
                SceneTriggerType.MidiProgramChange => "PC",
                SceneTriggerType.Keyboard => "KEY",
                SceneTriggerType.OSC => "OSC",
                SceneTriggerType.Manual => "MAN",
                _ => type.ToString().ToUpper()
            };
        }
        return "???";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts SceneTransitionMode to display string.
/// </summary>
public class PerformanceModeTransitionModeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is SceneTransitionMode mode)
        {
            return mode switch
            {
                SceneTransitionMode.Instant => "Instant",
                SceneTransitionMode.Linear => "Linear",
                SceneTransitionMode.EaseInOut => "Ease In/Out",
                SceneTransitionMode.Exponential => "Exponential",
                SceneTransitionMode.Logarithmic => "Logarithmic",
                _ => mode.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to Visibility with optional inverse parameter.
/// </summary>
public class PerformanceModeBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            // Check for inverse parameter
            if (parameter is string param && param.Equals("inverse", StringComparison.OrdinalIgnoreCase))
            {
                boolValue = !boolValue;
            }

            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }

        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public class PerformanceModeInverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }
        return value;
    }
}

/// <summary>
/// Converts MIDI note number to note name (e.g., 60 -> C4).
/// </summary>
public class PerformanceModeMidiNoteNameConverter : IValueConverter
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int noteNumber && noteNumber >= 0 && noteNumber <= 127)
        {
            var octave = (noteNumber / 12) - 1;
            var noteName = NoteNames[noteNumber % 12];
            return $"{noteName}{octave}";
        }
        return value?.ToString() ?? "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
