// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Control for displaying MIDI clips with mini piano roll preview.
/// </summary>
public partial class MidiClipControl : UserControl
{
    #region Dependency Properties

    public static new readonly DependencyProperty ClipProperty =
        DependencyProperty.Register(nameof(Clip), typeof(ClipViewModel), typeof(MidiClipControl),
            new PropertyMetadata(null, OnClipChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(MidiClipControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty ClipColorProperty =
        DependencyProperty.Register(nameof(ClipColor), typeof(Color), typeof(MidiClipControl),
            new PropertyMetadata(Color.FromRgb(0x21, 0x96, 0xF3), OnClipColorChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(MidiClipControl),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty NoteDataProperty =
        DependencyProperty.Register(nameof(NoteData), typeof(MiniNoteData[]), typeof(MidiClipControl),
            new PropertyMetadata(null, OnNoteDataChanged));

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the clip view model.
    /// </summary>
    public new ClipViewModel? Clip
    {
        get => (ClipViewModel?)GetValue(ClipProperty);
        set => SetValue(ClipProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the clip is selected.
    /// </summary>
    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>
    /// Gets or sets the clip color.
    /// </summary>
    public Color ClipColor
    {
        get => (Color)GetValue(ClipColorProperty);
        set => SetValue(ClipColorProperty, value);
    }

    /// <summary>
    /// Gets or sets the current playhead position.
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    /// <summary>
    /// Gets or sets the note data for mini piano roll display.
    /// </summary>
    public MiniNoteData[]? NoteData
    {
        get => (MiniNoteData[]?)GetValue(NoteDataProperty);
        set => SetValue(NoteDataProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<ClipViewModel>? ClipSelected;
    public event EventHandler<ClipMovedEventArgs>? ClipMoved;
    public event EventHandler<ClipResizedEventArgs>? ClipResized;
    public event EventHandler<ClipSplitRequestedEventArgs>? SplitRequested;

    #endregion

    #region Fields

    private bool _isDragging;
    private bool _isResizingLeft;
    private bool _isResizingRight;
    private Point _dragStartPoint;
    private double _originalStartPosition;
    private double _originalLength;

    private const int MinNoteHeight = 2;
    private const int MaxNoteHeight = 6;

    #endregion

    public MidiClipControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.DataContext = e.NewValue;

            if (e.OldValue is ClipViewModel oldClip)
            {
                oldClip.PropertyChanged -= control.OnClipPropertyChanged;
            }

            if (e.NewValue is ClipViewModel newClip)
            {
                newClip.PropertyChanged += control.OnClipPropertyChanged;
                control.ClipColor = ParseColor(newClip.Color);
                control.UpdateClipName();
                control.UpdateMutedState();
                control.UpdateLoopIndicator();

                if (newClip.NoteData != null)
                {
                    control.NoteData = newClip.NoteData;
                }
            }
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnClipColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.UpdateColors();
        }
    }

    private static void OnNoteDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MidiClipControl control)
        {
            control.RenderNotes();
        }
    }

    private void OnClipPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(ClipViewModel.IsSelected):
                IsSelected = Clip?.IsSelected ?? false;
                break;
            case nameof(ClipViewModel.IsMuted):
                UpdateMutedState();
                break;
            case nameof(ClipViewModel.IsLocked):
                LockMenuItem.Header = Clip?.IsLocked == true ? "Unlock" : "Lock";
                break;
            case nameof(ClipViewModel.Color):
                if (Clip != null)
                    ClipColor = ParseColor(Clip.Color);
                break;
            case nameof(ClipViewModel.IsLooping):
            case nameof(ClipViewModel.LoopLength):
                UpdateLoopIndicator();
                RenderNotes();
                break;
            case nameof(ClipViewModel.Name):
                UpdateClipName();
                break;
            case nameof(ClipViewModel.NoteData):
                if (Clip?.NoteData != null)
                    NoteData = Clip.NoteData;
                break;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderNotes();
    }

    #endregion

    #region Visual Updates

    private void UpdateColors()
    {
        var color = ClipColor;
        var fillBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var borderBrush = new SolidColorBrush(color);

        ClipBorder.Background = fillBrush;
        ClipBorder.BorderBrush = borderBrush;
        HeaderBar.Background = borderBrush;
    }

    private void UpdateClipName()
    {
        ClipNameText.Text = Clip?.Name ?? "MIDI Clip";
    }

    private void UpdateMutedState()
    {
        var isMuted = Clip?.IsMuted ?? false;
        MutedOverlay.Visibility = isMuted ? Visibility.Visible : Visibility.Collapsed;
        MuteMenuItem.Header = isMuted ? "Unmute" : "Mute";
    }

    private void UpdateLoopIndicator()
    {
        var isLooping = Clip?.IsLooping ?? false;
        LoopIndicator.Visibility = isLooping ? Visibility.Visible : Visibility.Collapsed;
        EnableLoopMenuItem.Header = isLooping ? "Disable Loop" : "Enable Loop";
    }

    private void RenderNotes()
    {
        NoteCanvas.Children.Clear();
        LoopLinesCanvas.Children.Clear();

        var data = NoteData;
        if (data == null || data.Length == 0)
        {
            if (Clip != null)
            {
                NoteCountText.Text = "Empty";
                NoteCountText.Visibility = Visibility.Visible;
            }
            return;
        }

        var width = NoteCanvas.ActualWidth;
        var height = NoteCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        // If clip is too small, just show note count
        if (width < 40)
        {
            NoteCountText.Text = $"{data.Length} notes";
            NoteCountText.Visibility = Visibility.Visible;
            return;
        }

        NoteCountText.Visibility = Visibility.Collapsed;

        var clipLength = Clip?.Length ?? 4.0;
        if (clipLength <= 0) return;

        // Find note range
        var minNote = 127;
        var maxNote = 0;
        foreach (var note in data)
        {
            if (note.Note < minNote) minNote = note.Note;
            if (note.Note > maxNote) maxNote = note.Note;
        }

        if (minNote > maxNote) return;

        // Ensure reasonable range
        var range = maxNote - minNote + 1;
        if (range < 12)
        {
            var expand = (12 - range) / 2;
            minNote = Math.Max(0, minNote - expand);
            maxNote = Math.Min(127, maxNote + expand);
            range = maxNote - minNote + 1;
        }

        var noteHeight = Math.Max(MinNoteHeight, Math.Min(MaxNoteHeight, height / range));
        var pixelsPerBeat = width / clipLength;

        // Render notes
        foreach (var note in data)
        {
            var noteY = height - ((note.Note - minNote + 1) * (height / range));
            var noteX = note.Start * pixelsPerBeat;
            var noteWidth = Math.Max(2, note.Duration * pixelsPerBeat - 1);

            // Clip note to visible area
            if (noteX + noteWidth < 0 || noteX > width) continue;

            var rect = new Rectangle
            {
                Width = noteWidth,
                Height = Math.Max(2, noteHeight - 1),
                Fill = Brushes.White,
                Opacity = note.Velocity / 127.0 * 0.7 + 0.3,
                RadiusX = 1,
                RadiusY = 1
            };

            Canvas.SetLeft(rect, noteX);
            Canvas.SetTop(rect, noteY);
            NoteCanvas.Children.Add(rect);
        }

        // Render loop boundary lines if looping
        if (Clip?.IsLooping == true && Clip.LoopLength > 0)
        {
            LoopLinesCanvas.Visibility = Visibility.Visible;
            var loopWidth = Clip.LoopLength * pixelsPerBeat;

            // Draw vertical lines at loop boundaries
            var x = loopWidth;
            while (x < width)
            {
                var line = new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = height,
                    Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                    StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                LoopLinesCanvas.Children.Add(line);
                x += loopWidth;
            }
        }
        else
        {
            LoopLinesCanvas.Visibility = Visibility.Collapsed;
        }
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x21, 0x96, 0xF3);
        }
    }

    #endregion

    #region Mouse Interaction

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null) return;

        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartPosition = Clip.StartPosition;
        _originalLength = Clip.Length;
        _isDragging = true;

        IsSelected = true;
        if (Clip != null)
        {
            Clip.IsSelected = true;
            ClipSelected?.Invoke(this, Clip);
        }

        CaptureMouse();
        e.Handled = true;
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingLeft || _isResizingRight)
        {
            ClipResized?.Invoke(this, new ClipResizedEventArgs(Clip!, _originalStartPosition, _originalLength));
        }
        else if (_isDragging && Clip != null)
        {
            var currentPos = e.GetPosition(Parent as IInputElement);
            if (Math.Abs(currentPos.X - _dragStartPoint.X) > 5)
            {
                ClipMoved?.Invoke(this, new ClipMovedEventArgs(Clip, _originalStartPosition));
            }
        }

        _isDragging = false;
        _isResizingLeft = false;
        _isResizingRight = false;

        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;
        // Movement handled by parent
    }

    private void LeftResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;

        _isResizingLeft = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartPosition = Clip.StartPosition;
        _originalLength = Clip.Length;

        CaptureMouse();
        e.Handled = true;
    }

    private void RightResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;

        _isResizingRight = true;
        _dragStartPoint = e.GetPosition(Parent as IInputElement);
        _originalStartPosition = Clip.StartPosition;
        _originalLength = Clip.Length;

        CaptureMouse();
        e.Handled = true;
    }

    #endregion

    #region Context Menu Handlers

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.EditCommand.Execute(null);
    }

    private void SplitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip == null) return;

        if (PlayheadPosition > Clip.StartPosition && PlayheadPosition < Clip.EndPosition)
        {
            SplitRequested?.Invoke(this, new ClipSplitRequestedEventArgs(Clip, PlayheadPosition));
        }
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.DuplicateCommand.Execute(null);
    }

    private void EnableLoop_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
        {
            Clip.IsLooping = !Clip.IsLooping;
            if (Clip.IsLooping && Clip.LoopLength <= 0)
            {
                Clip.LoopLength = Clip.Length;
            }
        }
    }

    private void SetLoopLength_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr && Clip != null)
        {
            if (double.TryParse(tagStr, out var length))
            {
                Clip.LoopLength = length;
                Clip.IsLooping = true;
            }
        }
    }

    private void Transpose_Click(object sender, RoutedEventArgs e)
    {
        // Transpose would be handled by the parent view model
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr)
        {
            if (int.TryParse(tagStr, out var semitones) && NoteData != null)
            {
                // This would normally modify the underlying MIDI data
                // For now, just update the visual
            }
        }
    }

    private void Quantize_Click(object sender, RoutedEventArgs e)
    {
        // Quantize would be handled by the parent view model
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var grid))
            {
                // This would normally modify the underlying MIDI data
            }
        }
    }

    private void BounceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.BounceCommand.Execute(null);
    }

    private void MuteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
            Clip.IsMuted = !Clip.IsMuted;
    }

    private void LockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
            Clip.IsLocked = !Clip.IsLocked;
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.DeleteCommand.Execute(null);
    }

    #endregion
}
