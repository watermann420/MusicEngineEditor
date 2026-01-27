// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MusicEngineEditor.ViewModels;
using Line = System.Windows.Shapes.Line;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Reusable clip control for displaying audio and MIDI clips in the arrangement view.
/// </summary>
public partial class ClipControl : UserControl
{
    #region Dependency Properties

    public static new readonly DependencyProperty ClipProperty =
        DependencyProperty.Register(nameof(Clip), typeof(ClipViewModel), typeof(ClipControl),
            new PropertyMetadata(null, OnClipChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(ClipControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty ClipColorProperty =
        DependencyProperty.Register(nameof(ClipColor), typeof(Color), typeof(ClipControl),
            new PropertyMetadata(Color.FromRgb(0x4C, 0xAF, 0x50), OnClipColorChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(ClipControl),
            new PropertyMetadata(0.0));

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
    /// Gets or sets the current playhead position (for split operations).
    /// </summary>
    public double PlayheadPosition
    {
        get => (double)GetValue(PlayheadPositionProperty);
        set => SetValue(PlayheadPositionProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when the clip is selected.
    /// </summary>
    public event EventHandler<ClipViewModel>? ClipSelected;

    /// <summary>
    /// Event raised when the clip is moved.
    /// </summary>
    public event EventHandler<ClipMovedEventArgs>? ClipMoved;

    /// <summary>
    /// Event raised when the clip is resized.
    /// </summary>
    public event EventHandler<ClipResizedEventArgs>? ClipResized;

    /// <summary>
    /// Event raised when split is requested.
    /// </summary>
    public event EventHandler<ClipSplitRequestedEventArgs>? SplitRequested;

    /// <summary>
    /// Event raised when fade is changed.
    /// </summary>
#pragma warning disable CS0067 // Event is never used - available for external fade change handling
    public event EventHandler<ClipFadeChangedEventArgs>? FadeChanged;
#pragma warning restore CS0067

    #endregion

    #region Fields

    private bool _isDragging;
    private bool _isResizingLeft;
    private bool _isResizingRight;
    private bool _isAdjustingFadeIn;
    private bool _isAdjustingFadeOut;
    private Point _dragStartPoint;
    private double _originalStartPosition;
    private double _originalLength;
    private double _originalFadeIn;
    private double _originalFadeOut;

    #endregion

    public ClipControl()
    {
        InitializeComponent();

        // Event handlers for mouse interaction
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
    }

    #region Property Changed Callbacks

    private static void OnClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipControl control)
        {
            control.DataContext = e.NewValue;
            control.UpdateVisualState();

            if (e.OldValue is ClipViewModel oldClip)
            {
                oldClip.PropertyChanged -= control.OnClipPropertyChanged;
            }

            if (e.NewValue is ClipViewModel newClip)
            {
                newClip.PropertyChanged += control.OnClipPropertyChanged;

                // Parse color from clip
                control.ClipColor = ParseColor(newClip.Color);
            }
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipControl control)
        {
            control.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnClipColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ClipControl control)
        {
            control.UpdateColors();
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
                UpdateLockedState();
                break;
            case nameof(ClipViewModel.Color):
                if (Clip != null)
                {
                    ClipColor = ParseColor(Clip.Color);
                }
                break;
            case nameof(ClipViewModel.FadeInDuration):
            case nameof(ClipViewModel.FadeOutDuration):
                UpdateFadeVisuals();
                break;
        }
    }

    #endregion

    #region Visual Updates

    private void UpdateVisualState()
    {
        if (Clip == null) return;

        UpdateColors();
        UpdateMutedState();
        UpdateLockedState();
        UpdateFadeVisuals();

        // Update context menu items
        MuteMenuItem.Header = Clip.IsMuted ? "Unmute" : "Mute";
        LockMenuItem.Header = Clip.IsLocked ? "Unlock" : "Lock";
        BounceMenuItem.Visibility = Clip.ClipType == ClipType.Midi ? Visibility.Visible : Visibility.Collapsed;

        // Show/hide fade handles based on clip type
        FadeInHandle.Visibility = Clip.ClipType == ClipType.Audio ? Visibility.Collapsed : Visibility.Collapsed;
        FadeOutHandle.Visibility = Clip.ClipType == ClipType.Audio ? Visibility.Collapsed : Visibility.Collapsed;
    }

    private void UpdateColors()
    {
        var color = ClipColor;
        var fillBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B));
        var headerBrush = new SolidColorBrush(color);
        var borderBrush = new SolidColorBrush(color);

        ClipBorder.Background = fillBrush;
        ClipBorder.BorderBrush = borderBrush;
        HeaderBar.Background = headerBrush;
    }

    private void UpdateMutedState()
    {
        var isMuted = Clip?.IsMuted ?? false;
        MutedOverlay.Visibility = isMuted ? Visibility.Visible : Visibility.Collapsed;
        MuteMenuItem.Header = isMuted ? "Unmute" : "Mute";
    }

    private void UpdateLockedState()
    {
        var isLocked = Clip?.IsLocked ?? false;
        LockedOverlay.Visibility = isLocked ? Visibility.Visible : Visibility.Collapsed;
        LockMenuItem.Header = isLocked ? "Unlock" : "Lock";
    }

    private void UpdateFadeVisuals()
    {
        // Fade visuals would be drawn in ContentCanvas for audio clips
        // This is a placeholder for the fade curve rendering
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Color.FromRgb(0x4C, 0xAF, 0x50);
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

        // Select the clip
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
        if (_isDragging || _isResizingLeft || _isResizingRight || _isAdjustingFadeIn || _isAdjustingFadeOut)
        {
            // Notify about changes
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
        }

        _isDragging = false;
        _isResizingLeft = false;
        _isResizingRight = false;
        _isAdjustingFadeIn = false;
        _isAdjustingFadeOut = false;

        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;

        if (!_isDragging && !_isResizingLeft && !_isResizingRight &&
            !_isAdjustingFadeIn && !_isAdjustingFadeOut)
            return;

        var currentPoint = e.GetPosition(Parent as IInputElement);
        var deltaX = currentPoint.X - _dragStartPoint.X;

        // Note: The actual position calculation would need pixelsPerBeat from the parent
        // This is a simplified version - real implementation would use events
        if (_isDragging && !_isResizingLeft && !_isResizingRight)
        {
            // Moving the clip would be handled by the parent ArrangementView
        }
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        // Show header buttons
        foreach (var child in HeaderButtons.Children)
        {
            if (child is Button button)
            {
                button.Visibility = Visibility.Visible;
            }
        }

        // Show fade handles for audio clips
        if (Clip?.ClipType == ClipType.Audio)
        {
            FadeInHandle.Visibility = Visibility.Visible;
            FadeOutHandle.Visibility = Visibility.Visible;
        }
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_isDragging || _isResizingLeft || _isResizingRight ||
            _isAdjustingFadeIn || _isAdjustingFadeOut)
            return;

        // Hide header buttons
        foreach (var child in HeaderButtons.Children)
        {
            if (child is Button button)
            {
                button.Visibility = Visibility.Collapsed;
            }
        }

        // Hide fade handles
        FadeInHandle.Visibility = Visibility.Collapsed;
        FadeOutHandle.Visibility = Visibility.Collapsed;
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

    private void FadeInHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null || Clip.IsLocked || Clip.ClipType != ClipType.Audio) return;

        _isAdjustingFadeIn = true;
        _dragStartPoint = e.GetPosition(this);
        _originalFadeIn = Clip.FadeInDuration;

        CaptureMouse();
        e.Handled = true;
    }

    private void FadeOutHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null || Clip.IsLocked || Clip.ClipType != ClipType.Audio) return;

        _isAdjustingFadeOut = true;
        _dragStartPoint = e.GetPosition(this);
        _originalFadeOut = Clip.FadeOutDuration;

        CaptureMouse();
        e.Handled = true;
    }

    #endregion

    #region Context Menu and Button Handlers

    private void EditMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.EditCommand.Execute(null);
    }

    private void SplitMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip == null) return;

        // Split at playhead position if within clip bounds
        if (PlayheadPosition > Clip.StartPosition && PlayheadPosition < Clip.EndPosition)
        {
            SplitRequested?.Invoke(this, new ClipSplitRequestedEventArgs(Clip, PlayheadPosition));
        }
    }

    private void DuplicateMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.DuplicateCommand.Execute(null);
    }

    private void MuteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
        {
            Clip.IsMuted = !Clip.IsMuted;
        }
    }

    private void LockMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
        {
            Clip.IsLocked = !Clip.IsLocked;
        }
    }

    private void BounceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.BounceCommand.Execute(null);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Clip?.DeleteCommand.Execute(null);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
        {
            Clip.IsMuted = !Clip.IsMuted;
        }
        e.Handled = true;
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        if (Clip != null)
        {
            Clip.IsLocked = !Clip.IsLocked;
        }
        e.Handled = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Draws waveform data to the content canvas.
    /// </summary>
    public void DrawWaveform(float[]? waveformData)
    {
        ContentCanvas.Children.Clear();

        if (waveformData == null || waveformData.Length == 0)
            return;

        var width = ContentCanvas.ActualWidth;
        var height = ContentCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        // Draw waveform as lines
        var samplesPerPixel = Math.Max(1, waveformData.Length / (int)width);

        for (int x = 0; x < (int)width; x++)
        {
            var sampleIndex = x * samplesPerPixel;
            if (sampleIndex >= waveformData.Length) break;

            // Find min/max in this range
            var min = 0f;
            var max = 0f;
            for (int i = 0; i < samplesPerPixel && sampleIndex + i < waveformData.Length; i++)
            {
                var sample = waveformData[sampleIndex + i];
                if (sample < min) min = sample;
                if (sample > max) max = sample;
            }

            var line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = centerY - max * halfHeight,
                Y2 = centerY - min * halfHeight,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Opacity = 0.7
            };
            ContentCanvas.Children.Add(line);
        }
    }

    /// <summary>
    /// Draws mini piano roll notes to the content canvas.
    /// </summary>
    public void DrawMiniNotes(MiniNoteData[]? noteData, double clipLength)
    {
        ContentCanvas.Children.Clear();

        if (noteData == null || noteData.Length == 0)
            return;

        var width = ContentCanvas.ActualWidth;
        var height = ContentCanvas.ActualHeight;

        if (width <= 0 || height <= 0 || clipLength <= 0)
            return;

        // Find note range
        var minNote = 127;
        var maxNote = 0;
        foreach (var note in noteData)
        {
            if (note.Note < minNote) minNote = note.Note;
            if (note.Note > maxNote) maxNote = note.Note;
        }

        if (minNote > maxNote) return;

        var noteRange = Math.Max(12, maxNote - minNote + 1);
        var noteHeight = height / noteRange;
        var pixelsPerBeat = width / clipLength;

        foreach (var note in noteData)
        {
            var noteY = height - (note.Note - minNote + 1) * noteHeight;
            var noteX = note.Start * pixelsPerBeat;
            var noteWidth = Math.Max(2, note.Duration * pixelsPerBeat);

            var rect = new Rectangle
            {
                Width = noteWidth,
                Height = Math.Max(2, noteHeight - 1),
                Fill = Brushes.White,
                Opacity = note.Velocity / 127.0 * 0.8 + 0.2,
                RadiusX = 1,
                RadiusY = 1
            };

            Canvas.SetLeft(rect, noteX);
            Canvas.SetTop(rect, noteY);
            ContentCanvas.Children.Add(rect);
        }
    }

    #endregion
}

/// <summary>
/// Event arguments for clip moved events.
/// </summary>
public class ClipMovedEventArgs : EventArgs
{
    public ClipViewModel Clip { get; }
    public double OriginalPosition { get; }

    public ClipMovedEventArgs(ClipViewModel clip, double originalPosition)
    {
        Clip = clip;
        OriginalPosition = originalPosition;
    }
}

/// <summary>
/// Event arguments for clip resized events.
/// </summary>
public class ClipResizedEventArgs : EventArgs
{
    public ClipViewModel Clip { get; }
    public double OriginalStartPosition { get; }
    public double OriginalLength { get; }

    public ClipResizedEventArgs(ClipViewModel clip, double originalStartPosition, double originalLength)
    {
        Clip = clip;
        OriginalStartPosition = originalStartPosition;
        OriginalLength = originalLength;
    }
}

/// <summary>
/// Event arguments for clip split requests.
/// </summary>
public class ClipSplitRequestedEventArgs : EventArgs
{
    public ClipViewModel Clip { get; }
    public double SplitPosition { get; }

    public ClipSplitRequestedEventArgs(ClipViewModel clip, double splitPosition)
    {
        Clip = clip;
        SplitPosition = splitPosition;
    }
}

/// <summary>
/// Event arguments for clip fade changes.
/// </summary>
public class ClipFadeChangedEventArgs : EventArgs
{
    public ClipViewModel Clip { get; }
    public double OriginalFadeIn { get; }
    public double OriginalFadeOut { get; }

    public ClipFadeChangedEventArgs(ClipViewModel clip, double originalFadeIn, double originalFadeOut)
    {
        Clip = clip;
        OriginalFadeIn = originalFadeIn;
        OriginalFadeOut = originalFadeOut;
    }
}
