// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: View implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.Controls;
using Shapes = System.Windows.Shapes;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.Views;

/// <summary>
/// Code-behind for the PianoRollView user control.
/// Manages the piano roll editor interface with synchronized scrolling,
/// keyboard shortcuts, and note canvas event wiring.
/// </summary>
public partial class PianoRollView : UserControl
{
    #region Constants

    private const double DefaultBeatWidth = 40.0;
    private const double DefaultNoteHeight = 20.0;
    private const int BeatsPerBar = 4;
    private const double RulerHeight = 24.0;

    #endregion

    #region Private Fields

    private readonly PianoRollViewModel _viewModel;
    private ScrollViewer? _keyboardScrollViewer;
    private ScrollViewer? _canvasScrollViewer;
    private Canvas? _rulerCanvas;
    private bool _isSyncingScroll;

    // Scrubbing support
    private bool _isRulerScrubbing;
    private Point _scrubStartPoint;

    // Scale highlighting
    private ScaleDefinition? _currentScale = ScaleDefinition.Major;
    private int _currentScaleRoot = 0;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new PianoRollView and initializes the PianoRollViewModel.
    /// </summary>
    public PianoRollView()
    {
        InitializeComponent();

        // Initialize ViewModel and set as DataContext
        _viewModel = new PianoRollViewModel();
        DataContext = _viewModel;

        // Wire up events
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the PianoRollViewModel associated with this view.
    /// </summary>
    public PianoRollViewModel ViewModel => _viewModel;

    #endregion

    #region Lifecycle Event Handlers

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Find scroll viewers by traversing the visual tree
        FindScrollViewers();

        // Wire up NoteCanvas events
        WireUpNoteCanvasEvents();

        // Wire up VelocityLane
        WireUpVelocityLane();

        // Wire up CC Lanes
        WireUpCCLanes();

        // Wire up Note Preview
        WireUpNotePreview();

        // Wire up Scale Highlighting
        WireUpScaleHighlighting();

        // Wire up Chord Stamp Panel
        WireUpChordStampPanel();

        // Set up keyboard focus
        Focusable = true;
        Focus();

        // Wire up keyboard events
        PreviewKeyDown += OnPreviewKeyDown;

        // Draw the ruler
        DrawRuler();

        // Wire up ruler scrubbing
        WireUpRulerScrubbing();

        // Load demo notes for testing (optional)
        LoadDemoNotes();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Clean up event handlers
        PreviewKeyDown -= OnPreviewKeyDown;

        UnwireNoteCanvasEvents();
        UnwireScrollSynchronization();
        UnwireNotePreview();
        UnwireRulerScrubbing();
        UnwireCCLanes();
        UnwireChordStampPanel();
    }

    #endregion

    #region Scroll Viewer Management

    /// <summary>
    /// Finds the scroll viewers for keyboard and canvas synchronization.
    /// </summary>
    private void FindScrollViewers()
    {
        // Find the PianoKeyboard's parent ScrollViewer
        var pianoKeyboard = FindChild<PianoKeyboard>(this, "PianoKeyboard");
        if (pianoKeyboard != null)
        {
            _keyboardScrollViewer = FindParent<ScrollViewer>(pianoKeyboard);
        }

        // Find the NoteCanvas's parent ScrollViewer
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas != null)
        {
            _canvasScrollViewer = FindParent<ScrollViewer>(noteCanvas);
        }

        // Find the ruler canvas
        _rulerCanvas = FindChild<Canvas>(this, "RulerCanvas");

        // Wire up scroll synchronization
        WireUpScrollSynchronization();
    }

    /// <summary>
    /// Wires up scroll synchronization between keyboard and canvas.
    /// </summary>
    private void WireUpScrollSynchronization()
    {
        if (_keyboardScrollViewer != null)
        {
            _keyboardScrollViewer.ScrollChanged += OnKeyboardScrollChanged;
        }

        if (_canvasScrollViewer != null)
        {
            _canvasScrollViewer.ScrollChanged += OnCanvasScrollChanged;
        }
    }

    /// <summary>
    /// Removes scroll synchronization event handlers.
    /// </summary>
    private void UnwireScrollSynchronization()
    {
        if (_keyboardScrollViewer != null)
        {
            _keyboardScrollViewer.ScrollChanged -= OnKeyboardScrollChanged;
        }

        if (_canvasScrollViewer != null)
        {
            _canvasScrollViewer.ScrollChanged -= OnCanvasScrollChanged;
        }
    }

    private void OnKeyboardScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;

        _isSyncingScroll = true;
        try
        {
            // Sync vertical scroll from keyboard to canvas
            if (_canvasScrollViewer != null && Math.Abs(e.VerticalChange) > 0.001)
            {
                _canvasScrollViewer.ScrollToVerticalOffset(_keyboardScrollViewer!.VerticalOffset);
            }
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    private void OnCanvasScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isSyncingScroll) return;

        _isSyncingScroll = true;
        try
        {
            // Sync vertical scroll from canvas to keyboard
            if (_keyboardScrollViewer != null && Math.Abs(e.VerticalChange) > 0.001)
            {
                _keyboardScrollViewer.ScrollToVerticalOffset(_canvasScrollViewer!.VerticalOffset);
            }

            // Update ruler position based on horizontal scroll
            UpdateRulerPosition(e.HorizontalOffset);

            // Update ViewModel scroll properties
            _viewModel.ScrollX = e.HorizontalOffset;
            _viewModel.ScrollY = e.VerticalOffset;
        }
        finally
        {
            _isSyncingScroll = false;
        }
    }

    #endregion

    #region NoteCanvas Event Wiring

    /// <summary>
    /// Wires up events from the NoteCanvas to ViewModel methods.
    /// </summary>
    private void WireUpNoteCanvasEvents()
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas == null) return;

        noteCanvas.NoteAdded += OnNoteAdded;
        noteCanvas.NoteDeleted += OnNoteDeleted;
        noteCanvas.NoteSelected += OnNoteSelected;
        noteCanvas.NoteMoved += OnNoteMoved;
        noteCanvas.NoteResized += OnNoteResized;
        noteCanvas.SelectionChanged += OnSelectionChanged;

        // Bind dependency properties
        BindNoteCanvasProperties(noteCanvas);
    }

    /// <summary>
    /// Removes event handlers from the NoteCanvas.
    /// </summary>
    private void UnwireNoteCanvasEvents()
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas == null) return;

        noteCanvas.NoteAdded -= OnNoteAdded;
        noteCanvas.NoteDeleted -= OnNoteDeleted;
        noteCanvas.NoteSelected -= OnNoteSelected;
        noteCanvas.NoteMoved -= OnNoteMoved;
        noteCanvas.NoteResized -= OnNoteResized;
        noteCanvas.SelectionChanged -= OnSelectionChanged;
    }

    /// <summary>
    /// Binds NoteCanvas dependency properties to ViewModel properties.
    /// </summary>
    private void BindNoteCanvasProperties(NoteCanvas noteCanvas)
    {
        // Set up bindings from ViewModel to NoteCanvas
        noteCanvas.Notes = _viewModel.Notes;
        noteCanvas.SelectedNotes = _viewModel.SelectedNotes;
        noteCanvas.LowestNote = _viewModel.LowestNote;
        noteCanvas.HighestNote = _viewModel.HighestNote;
        noteCanvas.TotalBeats = _viewModel.TotalBeats;
        noteCanvas.GridSnapValue = _viewModel.GridSnapValue;
        noteCanvas.ZoomX = _viewModel.ZoomX;
        noteCanvas.ZoomY = _viewModel.ZoomY;
        noteCanvas.PlayheadPosition = _viewModel.PlayheadPosition;

        // Convert tool enum (ViewModel uses ViewModels.PianoRollTool, NoteCanvas uses Controls.PianoRollTool)
        noteCanvas.CurrentTool = ConvertToControlsTool(_viewModel.CurrentTool);

        // Listen for ViewModel property changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(PianoRollViewModel.ZoomX):
                    noteCanvas.ZoomX = _viewModel.ZoomX;
                    DrawRuler();
                    break;
                case nameof(PianoRollViewModel.ZoomY):
                    noteCanvas.ZoomY = _viewModel.ZoomY;
                    break;
                case nameof(PianoRollViewModel.GridSnapValue):
                    noteCanvas.GridSnapValue = _viewModel.GridSnapValue;
                    break;
                case nameof(PianoRollViewModel.TotalBeats):
                    noteCanvas.TotalBeats = _viewModel.TotalBeats;
                    DrawRuler();
                    break;
                case nameof(PianoRollViewModel.PlayheadPosition):
                    noteCanvas.PlayheadPosition = _viewModel.PlayheadPosition;
                    break;
                case nameof(PianoRollViewModel.CurrentTool):
                    noteCanvas.CurrentTool = ConvertToControlsTool(_viewModel.CurrentTool);
                    break;
                case nameof(PianoRollViewModel.LowestNote):
                    noteCanvas.LowestNote = _viewModel.LowestNote;
                    break;
                case nameof(PianoRollViewModel.HighestNote):
                    noteCanvas.HighestNote = _viewModel.HighestNote;
                    break;
            }
        };
    }

    /// <summary>
    /// Converts ViewModels.PianoRollTool to Controls.PianoRollTool.
    /// </summary>
    private static Controls.PianoRollTool ConvertToControlsTool(ViewModels.PianoRollTool tool)
    {
        return tool switch
        {
            ViewModels.PianoRollTool.Select => Controls.PianoRollTool.Select,
            ViewModels.PianoRollTool.Draw => Controls.PianoRollTool.Draw,
            ViewModels.PianoRollTool.Erase => Controls.PianoRollTool.Erase,
            ViewModels.PianoRollTool.Slice => Controls.PianoRollTool.Select, // Map Slice to Select as fallback
            _ => Controls.PianoRollTool.Select
        };
    }

    #endregion

    #region VelocityLane Wiring

    /// <summary>
    /// Wires up the VelocityLane control.
    /// </summary>
    private void WireUpVelocityLane()
    {
        var velocityLane = FindChild<VelocityLane>(this, "VelocityLane");
        if (velocityLane == null) return;

        // Bind properties
        velocityLane.Notes = _viewModel.Notes;
        velocityLane.SelectedNotes = _viewModel.SelectedNotes;
        velocityLane.TotalBeats = _viewModel.TotalBeats;
        velocityLane.ZoomX = _viewModel.ZoomX;
        velocityLane.GridSnapValue = _viewModel.GridSnapValue;

        // Listen for ViewModel property changes
        _viewModel.PropertyChanged += (s, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(PianoRollViewModel.ZoomX):
                    velocityLane.ZoomX = _viewModel.ZoomX;
                    break;
                case nameof(PianoRollViewModel.TotalBeats):
                    velocityLane.TotalBeats = _viewModel.TotalBeats;
                    break;
                case nameof(PianoRollViewModel.GridSnapValue):
                    velocityLane.GridSnapValue = _viewModel.GridSnapValue;
                    break;
                case nameof(PianoRollViewModel.ScrollX):
                    velocityLane.ScrollX = _viewModel.ScrollX;
                    break;
            }
        };

        // Wire up velocity changed event
        velocityLane.VelocityChanged += OnVelocityChanged;
    }

    private void OnVelocityChanged(object? sender, VelocityChangedEventArgs e)
    {
        // The note's velocity is already updated by the VelocityLane
        // Refresh the NoteCanvas to reflect the visual change
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.UpdateNote(e.Note);
    }

    #endregion

    #region CC Lanes Wiring

    /// <summary>
    /// Wires up the CC Lanes section.
    /// </summary>
    private void WireUpCCLanes()
    {
        // Subscribe to collection changes to wire up new lanes
        _viewModel.CCLanes.CollectionChanged += OnCCLanesCollectionChanged;

        // Wire up existing lanes
        foreach (var lane in _viewModel.CCLanes)
        {
            WireUpSingleCCLane(lane);
        }

        // Listen for ViewModel property changes that affect all CC lanes
        _viewModel.PropertyChanged += OnViewModelPropertyChangedForCCLanes;
    }

    /// <summary>
    /// Unwires CC lanes event handlers.
    /// </summary>
    private void UnwireCCLanes()
    {
        _viewModel.CCLanes.CollectionChanged -= OnCCLanesCollectionChanged;
        _viewModel.PropertyChanged -= OnViewModelPropertyChangedForCCLanes;
    }

    private void OnCCLanesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (MidiCCLaneViewModel lane in e.NewItems)
            {
                WireUpSingleCCLane(lane);
            }
        }

        // Update all CC lane controls with current scroll/zoom
        Dispatcher.BeginInvoke(new Action(() =>
        {
            UpdateAllCCLaneControls();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private void OnViewModelPropertyChangedForCCLanes(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PianoRollViewModel.ZoomX):
            case nameof(PianoRollViewModel.TotalBeats):
            case nameof(PianoRollViewModel.GridSnapValue):
            case nameof(PianoRollViewModel.ScrollX):
                UpdateAllCCLaneControls();
                break;
        }
    }

    /// <summary>
    /// Wires up a single CC lane view model.
    /// </summary>
    /// <param name="laneViewModel">The lane view model to wire up.</param>
    private void WireUpSingleCCLane(MidiCCLaneViewModel laneViewModel)
    {
        // The lane will be created by the ItemsControl via DataTemplate
        // We just need to make sure it gets the correct initial values
        // This is handled by UpdateAllCCLaneControls when the control is loaded
    }

    /// <summary>
    /// Updates all CC lane controls with current view model values.
    /// </summary>
    private void UpdateAllCCLaneControls()
    {
        var ccLanesItemsControl = FindChild<ItemsControl>(this, "CCLanesItemsControl");
        if (ccLanesItemsControl == null) return;

        // Find all MidiCCLane controls within the ItemsControl
        var ccLaneControls = FindAllChildrenOfType<MidiCCLane>(ccLanesItemsControl);

        foreach (var ccLane in ccLaneControls)
        {
            ccLane.TotalBeats = _viewModel.TotalBeats;
            ccLane.ZoomX = _viewModel.ZoomX;
            ccLane.GridSnapValue = _viewModel.GridSnapValue;
            ccLane.ScrollX = _viewModel.ScrollX;
        }
    }

    /// <summary>
    /// Finds all children of a specific type in the visual tree.
    /// </summary>
    private static List<T> FindAllChildrenOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        var results = new List<T>();

        if (parent == null) return results;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
            {
                results.Add(typedChild);
            }

            results.AddRange(FindAllChildrenOfType<T>(child));
        }

        return results;
    }

    #endregion

    #region Note Preview Wiring

    /// <summary>
    /// Wires up note preview functionality.
    /// </summary>
    private void WireUpNotePreview()
    {
        // Subscribe to ViewModel note preview events
        _viewModel.NotePreviewRequested += OnNotePreviewRequested;

        // Wire up NoteCanvas for note preview when drawing
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas != null)
        {
            noteCanvas.NoteAdded += OnNoteAddedForPreview;
            noteCanvas.NotePreviewRequested += OnNoteCanvasPreviewRequested;
        }

        // Wire up PianoKeyboard for note preview
        var pianoKeyboard = FindChild<PianoKeyboard>(this, "PianoKeyboard");
        if (pianoKeyboard != null)
        {
            pianoKeyboard.NotePressed += OnPianoKeyPressed;
            pianoKeyboard.NoteReleased += OnPianoKeyReleased;
        }
    }

    /// <summary>
    /// Unwires note preview functionality.
    /// </summary>
    private void UnwireNotePreview()
    {
        _viewModel.NotePreviewRequested -= OnNotePreviewRequested;

        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas != null)
        {
            noteCanvas.NoteAdded -= OnNoteAddedForPreview;
            noteCanvas.NotePreviewRequested -= OnNoteCanvasPreviewRequested;
        }

        var pianoKeyboard = FindChild<PianoKeyboard>(this, "PianoKeyboard");
        if (pianoKeyboard != null)
        {
            pianoKeyboard.NotePressed -= OnPianoKeyPressed;
            pianoKeyboard.NoteReleased -= OnPianoKeyReleased;
        }
    }

    private void OnNoteCanvasPreviewRequested(object? sender, NotePreviewArgs e)
    {
        if (_viewModel.NotePreviewEnabled)
        {
            PlayNotePreview(e.MidiNote, e.Velocity, e.IsNoteOff);
        }
    }

    private void OnNotePreviewRequested(object? sender, NotePreviewEventArgs e)
    {
        // Play or stop the note preview
        // This would typically call into an audio service
        PlayNotePreview(e.MidiNote, e.Velocity, e.IsNoteOff);
    }

    private void OnNoteAddedForPreview(object? sender, NoteEventArgs e)
    {
        if (e.PianoRollNote != null && _viewModel.NotePreviewEnabled)
        {
            _viewModel.RequestNotePreview(e.PianoRollNote.Note, e.PianoRollNote.Velocity);
            // Auto-stop after a short delay (handled by audio service)
        }
    }

    private void OnPianoKeyPressed(object sender, NoteEventArgs e)
    {
        if (_viewModel.NotePreviewEnabled)
        {
            // Use StartNote for piano keys so they sustain while held
            NotePreviewService.Instance.StartNote(e.Note, 100);
        }
    }

    private void OnPianoKeyReleased(object sender, NoteEventArgs e)
    {
        // Always stop the note on release, regardless of preview enabled state
        NotePreviewService.Instance.StopNote(e.Note);
    }

    /// <summary>
    /// Plays a note preview sound using the NotePreviewService.
    /// </summary>
    /// <param name="midiNote">The MIDI note number.</param>
    /// <param name="velocity">The velocity.</param>
    /// <param name="isNoteOff">Whether this is a note-off event.</param>
    private void PlayNotePreview(int midiNote, int velocity, bool isNoteOff)
    {
        try
        {
            var previewService = NotePreviewService.Instance;

            if (isNoteOff)
            {
                // Stop the note
                previewService.StopNote(midiNote);
            }
            else
            {
                // Play the note (StartNote for keys held down, PlayNote for clicks)
                // For piano keyboard, use StartNote so release can trigger StopNote
                // For drawing notes, use PlayNote with auto-off
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    // Sustained note - will be stopped manually
                    previewService.StartNote(midiNote, velocity);
                }
                else
                {
                    // Quick preview with auto-off
                    previewService.PlayNote(midiNote, velocity);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NotePreview] Error: {ex.Message}");
        }
    }

    #endregion

    #region NoteCanvas Event Handlers

    private void OnNoteAdded(object? sender, NoteEventArgs e)
    {
        // Add note to ViewModel
        if (e.PianoRollNote != null)
        {
            _viewModel.AddNote(e.PianoRollNote.Note, e.PianoRollNote.StartBeat, e.PianoRollNote.Duration, e.PianoRollNote.Velocity);
        }
    }

    private void OnNoteDeleted(object? sender, NoteEventArgs e)
    {
        // Remove note from ViewModel
        if (e.PianoRollNote != null)
        {
            _viewModel.RemoveNote(e.PianoRollNote);
        }
    }

    private void OnNoteSelected(object? sender, NoteEventArgs e)
    {
        // Note selection is handled through the SelectedNotes collection binding
    }

    private void OnNoteMoved(object? sender, NoteMovedEventArgs e)
    {
        // Update note position in ViewModel
        e.Note.StartBeat = e.NewBeat;
        e.Note.Note = e.NewNote;
    }

    private void OnNoteResized(object? sender, NoteResizedEventArgs e)
    {
        // Update note duration in ViewModel
        e.Note.Duration = e.NewDuration;
    }

    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        // Selection changes are synchronized through the SelectedNotes collection
    }

    #endregion

    #region Keyboard Shortcut Handling

    /// <summary>
    /// Handles keyboard shortcuts for the piano roll.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        bool ctrlPressed = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        switch (e.Key)
        {
            // Delete: Delete selected notes
            case Key.Delete:
                _viewModel.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+A: Select all notes
            case Key.A when ctrlPressed:
                _viewModel.SelectAllCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+D: Duplicate selected notes
            case Key.D when ctrlPressed:
                _viewModel.DuplicateSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+C: Copy selected notes
            case Key.C when ctrlPressed:
                _viewModel.CopyCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+V: Paste notes
            case Key.V when ctrlPressed:
                _viewModel.PasteCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+X: Cut selected notes
            case Key.X when ctrlPressed:
                _viewModel.CutCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+Z: Undo
            case Key.Z when ctrlPressed:
                Services.EditorUndoService.Instance.Undo();
                e.Handled = true;
                break;

            // Ctrl+Y: Redo
            case Key.Y when ctrlPressed:
                Services.EditorUndoService.Instance.Redo();
                e.Handled = true;
                break;

            // 1: Select tool
            case Key.D1:
            case Key.NumPad1:
                _viewModel.SetToolCommand.Execute(ViewModels.PianoRollTool.Select);
                e.Handled = true;
                break;

            // 2: Draw tool
            case Key.D2:
            case Key.NumPad2:
                _viewModel.SetToolCommand.Execute(ViewModels.PianoRollTool.Draw);
                e.Handled = true;
                break;

            // 3: Erase tool
            case Key.D3:
            case Key.NumPad3:
                _viewModel.SetToolCommand.Execute(ViewModels.PianoRollTool.Erase);
                e.Handled = true;
                break;

            // +/=: Zoom in
            case Key.OemPlus:
            case Key.Add:
                if (ctrlPressed)
                {
                    _viewModel.ZoomInYCommand.Execute(null);
                }
                else
                {
                    _viewModel.ZoomInXCommand.Execute(null);
                }
                e.Handled = true;
                break;

            // -: Zoom out
            case Key.OemMinus:
            case Key.Subtract:
                if (ctrlPressed)
                {
                    _viewModel.ZoomOutYCommand.Execute(null);
                }
                else
                {
                    _viewModel.ZoomOutXCommand.Execute(null);
                }
                e.Handled = true;
                break;

            // Escape: Deselect all
            case Key.Escape:
                _viewModel.DeselectAllCommand.Execute(null);
                e.Handled = true;
                break;

            // Q: Quantize selected notes (quick quantize)
            case Key.Q when !ctrlPressed:
                _viewModel.QuantizeSelectedCommand.Execute(null);
                e.Handled = true;
                break;

            // Ctrl+Q: Open quantize dialog
            case Key.Q when ctrlPressed:
                OpenQuantizeDialog();
                e.Handled = true;
                break;

            // Up arrow: Transpose up
            case Key.Up when ctrlPressed:
                _viewModel.TransposeCommand.Execute(12); // Octave up
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.TransposeCommand.Execute(1); // Semitone up
                e.Handled = true;
                break;

            // Down arrow: Transpose down
            case Key.Down when ctrlPressed:
                _viewModel.TransposeCommand.Execute(-12); // Octave down
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.TransposeCommand.Execute(-1); // Semitone down
                e.Handled = true;
                break;
        }
    }

    #endregion

    #region Quantize Dialog

    /// <summary>
    /// Opens the quantize dialog for advanced quantize options.
    /// </summary>
    private void OpenQuantizeDialog()
    {
        if (_viewModel.SelectedNotes.Count == 0)
        {
            _viewModel.StatusMessage = "No notes selected to quantize";
            return;
        }

        var dialog = new QuantizeDialog(_viewModel.GridSnapValue)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() == true)
        {
            ApplyQuantize(dialog.GridValue, dialog.Mode, dialog.Strength);
        }
    }

    /// <summary>
    /// Applies quantize settings to selected notes.
    /// </summary>
    /// <param name="gridValue">The grid value to quantize to.</param>
    /// <param name="mode">The quantize mode.</param>
    /// <param name="strength">The quantize strength (0-100).</param>
    private void ApplyQuantize(double gridValue, QuantizeMode mode, double strength)
    {
        var strengthFactor = strength / 100.0;

        foreach (var note in _viewModel.SelectedNotes)
        {
            switch (mode)
            {
                case QuantizeMode.StartAndEnd:
                    // Quantize both start and end
                    double targetStart = Math.Round(note.StartBeat / gridValue) * gridValue;
                    double targetEnd = Math.Round(note.GetEndBeat() / gridValue) * gridValue;

                    note.StartBeat = note.StartBeat + (targetStart - note.StartBeat) * strengthFactor;
                    double newEnd = note.GetEndBeat() + (targetEnd - note.GetEndBeat()) * strengthFactor;
                    note.Duration = Math.Max(gridValue, newEnd - note.StartBeat);
                    break;

                case QuantizeMode.StartOnly:
                    // Quantize start only, preserve duration
                    double targetStartOnly = Math.Round(note.StartBeat / gridValue) * gridValue;
                    note.StartBeat = note.StartBeat + (targetStartOnly - note.StartBeat) * strengthFactor;
                    break;

                case QuantizeMode.EndOnly:
                    // Quantize end only, adjust duration
                    double targetEndOnly = Math.Round(note.GetEndBeat() / gridValue) * gridValue;
                    double newEndOnly = note.GetEndBeat() + (targetEndOnly - note.GetEndBeat()) * strengthFactor;
                    note.Duration = Math.Max(gridValue, newEndOnly - note.StartBeat);
                    break;
            }
        }

        // Refresh the NoteCanvas
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.Refresh();

        _viewModel.StatusMessage = $"Quantized {_viewModel.SelectedNotes.Count} note(s) at {strength:F0}% strength";
    }

    #endregion

    #region Ruler Drawing

    /// <summary>
    /// Draws the beat/bar ruler at the top of the piano roll.
    /// </summary>
    private void DrawRuler()
    {
        if (_rulerCanvas == null) return;

        _rulerCanvas.Children.Clear();

        double effectiveBeatWidth = DefaultBeatWidth * _viewModel.ZoomX;
        double totalWidth = _viewModel.TotalBeats * effectiveBeatWidth;
        int totalBeats = (int)Math.Ceiling(_viewModel.TotalBeats);

        // Set canvas size
        _rulerCanvas.Width = totalWidth;
        _rulerCanvas.Height = RulerHeight;

        // Background
        var background = new Shapes.Rectangle
        {
            Width = totalWidth,
            Height = RulerHeight,
            Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30))
        };
        _rulerCanvas.Children.Add(background);

        // Draw beat markers and bar numbers
        for (int beat = 0; beat <= totalBeats; beat++)
        {
            double x = beat * effectiveBeatWidth;
            bool isBarLine = beat % BeatsPerBar == 0;
            int barNumber = beat / BeatsPerBar + 1;

            // Draw tick line
            var line = new Shapes.Line
            {
                X1 = x,
                Y1 = isBarLine ? 0 : RulerHeight * 0.6,
                X2 = x,
                Y2 = RulerHeight,
                Stroke = new SolidColorBrush(isBarLine ? Color.FromRgb(0x6F, 0x73, 0x7A) : Color.FromRgb(0x43, 0x45, 0x4A)),
                StrokeThickness = isBarLine ? 1.5 : 1
            };
            _rulerCanvas.Children.Add(line);

            // Draw bar number at bar lines
            if (isBarLine)
            {
                var text = new TextBlock
                {
                    Text = barNumber.ToString(),
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };

                Canvas.SetLeft(text, x + 4);
                Canvas.SetTop(text, 2);
                _rulerCanvas.Children.Add(text);
            }
        }

        // Draw bottom border
        var bottomBorder = new Shapes.Line
        {
            X1 = 0,
            Y1 = RulerHeight - 1,
            X2 = totalWidth,
            Y2 = RulerHeight - 1,
            Stroke = new SolidColorBrush(Color.FromRgb(0x39, 0x3B, 0x40)),
            StrokeThickness = 1
        };
        _rulerCanvas.Children.Add(bottomBorder);
    }

    /// <summary>
    /// Updates the ruler position based on horizontal scroll offset.
    /// </summary>
    private void UpdateRulerPosition(double horizontalOffset)
    {
        if (_rulerCanvas == null) return;

        // Apply transform to synchronize with canvas scroll
        _rulerCanvas.RenderTransform = new TranslateTransform(-horizontalOffset, 0);
    }

    #endregion

    #region Ruler Scrubbing

    /// <summary>
    /// Wires up ruler scrubbing functionality.
    /// </summary>
    private void WireUpRulerScrubbing()
    {
        if (_rulerCanvas == null) return;

        _rulerCanvas.MouseLeftButtonDown += OnRulerMouseLeftButtonDown;
        _rulerCanvas.MouseMove += OnRulerMouseMove;
        _rulerCanvas.MouseLeftButtonUp += OnRulerMouseLeftButtonUp;
        _rulerCanvas.MouseLeave += OnRulerMouseLeave;
    }

    /// <summary>
    /// Unwires ruler scrubbing functionality.
    /// </summary>
    private void UnwireRulerScrubbing()
    {
        if (_rulerCanvas == null) return;

        _rulerCanvas.MouseLeftButtonDown -= OnRulerMouseLeftButtonDown;
        _rulerCanvas.MouseMove -= OnRulerMouseMove;
        _rulerCanvas.MouseLeftButtonUp -= OnRulerMouseLeftButtonUp;
        _rulerCanvas.MouseLeave -= OnRulerMouseLeave;
    }

    private void OnRulerMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_rulerCanvas == null) return;

        _isRulerScrubbing = true;
        _scrubStartPoint = e.GetPosition(_rulerCanvas);

        // Calculate beat position from x coordinate
        var beat = PixelToBeat(_scrubStartPoint.X);

        // Start scrubbing
        Services.ScrubService.Instance.StartScrub(beat);

        // Capture mouse for drag tracking
        _rulerCanvas.CaptureMouse();
        _rulerCanvas.Cursor = Cursors.IBeam;

        // Update playhead position
        _viewModel.PlayheadPosition = beat;

        e.Handled = true;
    }

    private void OnRulerMouseMove(object sender, MouseEventArgs e)
    {
        if (!_isRulerScrubbing || _rulerCanvas == null) return;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndRulerScrub(false);
            return;
        }

        var position = e.GetPosition(_rulerCanvas);
        var beat = PixelToBeat(position.X);

        // Update scrub position
        Services.ScrubService.Instance.UpdateScrub(beat);

        // Update playhead position
        _viewModel.PlayheadPosition = beat;
    }

    private void OnRulerMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isRulerScrubbing) return;

        // Check if Shift is held to continue playback
        var continuePlayback = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        EndRulerScrub(continuePlayback);
    }

    private void OnRulerMouseLeave(object sender, MouseEventArgs e)
    {
        // Don't end scrub on leave if mouse is still pressed (allow dragging outside)
        if (_isRulerScrubbing && e.LeftButton != MouseButtonState.Pressed)
        {
            EndRulerScrub(false);
        }
    }

    /// <summary>
    /// Ends the ruler scrubbing operation.
    /// </summary>
    /// <param name="continuePlayback">Whether to continue playback from the scrub position.</param>
    private void EndRulerScrub(bool continuePlayback)
    {
        if (!_isRulerScrubbing) return;

        _isRulerScrubbing = false;
        _rulerCanvas?.ReleaseMouseCapture();

        if (_rulerCanvas != null)
        {
            _rulerCanvas.Cursor = Cursors.Arrow;
        }

        Services.ScrubService.Instance.EndScrub(continuePlayback);
    }

    /// <summary>
    /// Converts a pixel x-coordinate to a beat position.
    /// </summary>
    /// <param name="pixelX">The x coordinate in pixels.</param>
    /// <returns>The corresponding beat position.</returns>
    private double PixelToBeat(double pixelX)
    {
        // Account for scroll offset
        var scrollOffset = _canvasScrollViewer?.HorizontalOffset ?? 0;
        var adjustedX = pixelX + scrollOffset;

        // Convert to beats based on zoom
        var effectiveBeatWidth = DefaultBeatWidth * _viewModel.ZoomX;
        return adjustedX / effectiveBeatWidth;
    }

    #endregion

    #region Demo Notes

    /// <summary>
    /// Loads demo notes for testing purposes.
    /// </summary>
    private void LoadDemoNotes()
    {
        // Add a simple C major scale pattern for demonstration
        int[] cMajorScale = { 60, 62, 64, 65, 67, 69, 71, 72 }; // C4 to C5

        for (int i = 0; i < cMajorScale.Length; i++)
        {
            _viewModel.AddNote(
                note: cMajorScale[i],
                startBeat: i * 0.5,
                duration: 0.5,
                velocity: 80 + (i * 5) // Gradually increasing velocity
            );
        }

        // Add a simple chord at beat 4
        _viewModel.AddNote(60, 4.0, 2.0, 100); // C4
        _viewModel.AddNote(64, 4.0, 2.0, 100); // E4
        _viewModel.AddNote(67, 4.0, 2.0, 100); // G4

        // Add another chord at beat 6
        _viewModel.AddNote(65, 6.0, 2.0, 90); // F4
        _viewModel.AddNote(69, 6.0, 2.0, 90); // A4
        _viewModel.AddNote(72, 6.0, 2.0, 90); // C5

        // Add bass notes
        _viewModel.AddNote(48, 0.0, 1.0, 100);  // C3
        _viewModel.AddNote(48, 2.0, 1.0, 100);  // C3
        _viewModel.AddNote(53, 4.0, 2.0, 100);  // F3
        _viewModel.AddNote(55, 6.0, 2.0, 100);  // G3
    }

    #endregion

    #region Visual Tree Helpers

    /// <summary>
    /// Finds a child element of a specific type with the given name.
    /// </summary>
    private static T? FindChild<T>(DependencyObject parent, string childName) where T : FrameworkElement
    {
        if (parent == null) return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild && typedChild.Name == childName)
            {
                return typedChild;
            }

            var foundChild = FindChild<T>(child, childName);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }

    /// <summary>
    /// Finds a parent element of a specific type.
    /// </summary>
    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        if (child == null) return null;

        var parent = VisualTreeHelper.GetParent(child);

        while (parent != null)
        {
            if (parent is T typedParent)
            {
                return typedParent;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return null;
    }

    /// <summary>
    /// Finds a child element of a specific type (first match).
    /// </summary>
    private static T? FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) return null;

        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T typedChild)
            {
                return typedChild;
            }

            var foundChild = FindChildOfType<T>(child);
            if (foundChild != null)
            {
                return foundChild;
            }
        }

        return null;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Refreshes the entire piano roll display.
    /// </summary>
    public void Refresh()
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.Refresh();

        var pianoKeyboard = FindChild<PianoKeyboard>(this, "PianoKeyboard");
        pianoKeyboard?.Refresh();

        DrawRuler();
    }

    /// <summary>
    /// Scrolls to make a specific beat visible.
    /// </summary>
    /// <param name="beat">The beat position to scroll to.</param>
    public void ScrollToBeat(double beat)
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.ScrollToBeat(beat);
    }

    /// <summary>
    /// Scrolls to make a specific note visible.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to scroll to.</param>
    public void ScrollToNote(int midiNote)
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.ScrollToNote(midiNote);
    }

    /// <summary>
    /// Clears all notes from the piano roll.
    /// </summary>
    public void ClearAllNotes()
    {
        _viewModel.Notes.Clear();
        _viewModel.SelectedNotes.Clear();
    }

    /// <summary>
    /// Sets the playhead position.
    /// </summary>
    /// <param name="beat">The beat position for the playhead.</param>
    public void SetPlayheadPosition(double beat)
    {
        _viewModel.PlayheadPosition = Math.Max(0, beat);
    }

    #endregion

    #region Scale Highlighting

    /// <summary>
    /// Wires up scale highlighting functionality.
    /// </summary>
    private void WireUpScaleHighlighting()
    {
        // Initialize scale type lookup
        _scaleTypeLookup = new Dictionary<string, ScaleDefinition>
        {
            ["Major"] = ScaleDefinition.Major,
            ["Minor"] = ScaleDefinition.Minor,
            ["Dorian"] = ScaleDefinition.Dorian,
            ["Phrygian"] = ScaleDefinition.Phrygian,
            ["Lydian"] = ScaleDefinition.Lydian,
            ["Mixolydian"] = ScaleDefinition.Mixolydian,
            ["Locrian"] = ScaleDefinition.Locrian,
            ["Harmonic Minor"] = ScaleDefinition.HarmonicMinor,
            ["Melodic Minor"] = ScaleDefinition.MelodicMinor,
            ["Pentatonic Major"] = ScaleDefinition.PentatonicMajor,
            ["Pentatonic Minor"] = ScaleDefinition.PentatonicMinor,
            ["Blues"] = ScaleDefinition.Blues,
            ["Whole Tone"] = ScaleDefinition.WholeTone,
            ["Chromatic"] = ScaleDefinition.Chromatic
        };

        // Wire up scale highlighting toggle
        ScaleHighlightToggle.Checked += (s, e) => UpdateScaleHighlighting();
        ScaleHighlightToggle.Unchecked += (s, e) => UpdateScaleHighlighting();

        // Initial update
        UpdateScaleHighlighting();
    }

    /// <summary>
    /// Handles root note selection change.
    /// </summary>
    private void OnRootNoteChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RootNoteComboBox.SelectedItem is ComboBoxItem item &&
            item.Tag is string tagStr &&
            int.TryParse(tagStr, out int root))
        {
            _currentScaleRoot = root;
            UpdateScaleHighlighting();
        }
    }

    /// <summary>
    /// Handles scale type selection change.
    /// </summary>
    private void OnScaleTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ScaleTypeComboBox.SelectedItem is ComboBoxItem item &&
            item.Content is string scaleName &&
            _scaleTypeLookup != null &&
            _scaleTypeLookup.TryGetValue(scaleName, out var scale))
        {
            _currentScale = scale;
            UpdateScaleHighlighting();
        }
    }

    /// <summary>
    /// Updates the scale highlighting on the note canvas.
    /// </summary>
    private void UpdateScaleHighlighting()
    {
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        if (noteCanvas == null) return;

        bool isEnabled = ScaleHighlightToggle.IsChecked == true;

        noteCanvas.ScaleHighlightingEnabled = isEnabled;
        noteCanvas.HighlightedScale = _currentScale;
        noteCanvas.HighlightedRoot = _currentScaleRoot;
    }

    private Dictionary<string, ScaleDefinition>? _scaleTypeLookup;

    #endregion

    #region Chord Stamp Panel

    /// <summary>
    /// Wires up the chord stamp panel.
    /// </summary>
    private void WireUpChordStampPanel()
    {
        if (ChordStampPanel == null) return;

        // Set the notes collection
        ChordStampPanel.NotesCollection = _viewModel.Notes;

        // Wire up events
        ChordStampPanel.ChordStamped += OnChordStamped;
        ChordStampPanel.ChordPreviewRequested += OnChordPreviewRequested;

        // Subscribe to playhead position changes
        _viewModel.PropertyChanged += OnViewModelPropertyChangedForChordPanel;
    }

    /// <summary>
    /// Unwires the chord stamp panel.
    /// </summary>
    private void UnwireChordStampPanel()
    {
        if (ChordStampPanel == null) return;

        ChordStampPanel.ChordStamped -= OnChordStamped;
        ChordStampPanel.ChordPreviewRequested -= OnChordPreviewRequested;
        _viewModel.PropertyChanged -= OnViewModelPropertyChangedForChordPanel;
    }

    private void OnViewModelPropertyChangedForChordPanel(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PianoRollViewModel.PlayheadPosition))
        {
            if (ChordStampPanel != null)
            {
                ChordStampPanel.StampPosition = _viewModel.PlayheadPosition;
            }
        }
        else if (e.PropertyName == nameof(PianoRollViewModel.GridSnapValue))
        {
            if (ChordStampPanel != null)
            {
                ChordStampPanel.StampDuration = _viewModel.GridSnapValue;
            }
        }
    }

    private void OnChordStamped(object? sender, ChordStampedEventArgs e)
    {
        // Refresh the note canvas
        var noteCanvas = FindChild<NoteCanvas>(this, "NoteCanvas");
        noteCanvas?.Refresh();

        // Update velocity lane
        var velocityLane = FindChild<VelocityLane>(this, "VelocityLane");
        velocityLane?.Refresh();

        _viewModel.StatusMessage = $"Stamped {e.Chord.GetSymbolWithRoot(e.RootNote)} chord ({e.Notes.Count} notes)";
    }

    private void OnChordPreviewRequested(object? sender, ChordPreviewEventArgs e)
    {
        // Play chord preview through audio service
        foreach (var note in e.Notes)
        {
            if (e.IsNoteOff)
            {
                _viewModel.RequestNotePreviewStop(note);
            }
            else
            {
                _viewModel.RequestNotePreview(note, e.Velocity);
            }
        }
    }

    #endregion
}

