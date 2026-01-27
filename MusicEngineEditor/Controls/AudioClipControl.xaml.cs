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
/// Control for displaying audio clips with waveform visualization and fade curves.
/// </summary>
public partial class AudioClipControl : UserControl
{
    #region Dependency Properties

    public static new readonly DependencyProperty ClipProperty =
        DependencyProperty.Register(nameof(Clip), typeof(ClipViewModel), typeof(AudioClipControl),
            new PropertyMetadata(null, OnClipChanged));

    public static readonly DependencyProperty IsSelectedProperty =
        DependencyProperty.Register(nameof(IsSelected), typeof(bool), typeof(AudioClipControl),
            new PropertyMetadata(false, OnIsSelectedChanged));

    public static readonly DependencyProperty ClipColorProperty =
        DependencyProperty.Register(nameof(ClipColor), typeof(Color), typeof(AudioClipControl),
            new PropertyMetadata(Color.FromRgb(0x4C, 0xAF, 0x50), OnClipColorChanged));

    public static readonly DependencyProperty PlayheadPositionProperty =
        DependencyProperty.Register(nameof(PlayheadPosition), typeof(double), typeof(AudioClipControl),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty WaveformDataProperty =
        DependencyProperty.Register(nameof(WaveformData), typeof(float[]), typeof(AudioClipControl),
            new PropertyMetadata(null, OnWaveformDataChanged));

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
    /// Gets or sets the waveform data.
    /// </summary>
    public float[]? WaveformData
    {
        get => (float[]?)GetValue(WaveformDataProperty);
        set => SetValue(WaveformDataProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<ClipViewModel>? ClipSelected;
    public event EventHandler<ClipMovedEventArgs>? ClipMoved;
    public event EventHandler<ClipResizedEventArgs>? ClipResized;
    public event EventHandler<ClipSplitRequestedEventArgs>? SplitRequested;
    public event EventHandler<ClipFadeChangedEventArgs>? FadeChanged;

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

    public AudioClipControl()
    {
        InitializeComponent();

        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseMove += OnMouseMove;
        MouseEnter += OnMouseEnter;
        MouseLeave += OnMouseLeave;
        SizeChanged += OnSizeChanged;
    }

    #region Property Changed Callbacks

    private static void OnClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
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
                control.UpdateGainIndicator();
                control.UpdateMutedState();
                control.UpdateFadeCurves();

                if (newClip.WaveformData != null)
                {
                    control.WaveformData = newClip.WaveformData;
                }
            }
        }
    }

    private static void OnIsSelectedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.SelectionBorder.Visibility = (bool)e.NewValue ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private static void OnClipColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.UpdateColors();
        }
    }

    private static void OnWaveformDataChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AudioClipControl control)
        {
            control.RenderWaveform();
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
            case nameof(ClipViewModel.FadeInDuration):
            case nameof(ClipViewModel.FadeOutDuration):
                UpdateFadeCurves();
                break;
            case nameof(ClipViewModel.GainDb):
                UpdateGainIndicator();
                break;
            case nameof(ClipViewModel.Name):
                UpdateClipName();
                break;
            case nameof(ClipViewModel.WaveformData):
                if (Clip?.WaveformData != null)
                    WaveformData = Clip.WaveformData;
                break;
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RenderWaveform();
        UpdateFadeCurves();
        UpdateCenterLine();
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
        ClipNameText.Text = Clip?.Name ?? "Audio Clip";
    }

    private void UpdateGainIndicator()
    {
        if (Clip == null) return;

        if (Math.Abs(Clip.GainDb) > 0.1)
        {
            GainIndicator.Visibility = Visibility.Visible;
            GainText.Text = Clip.GainDb > 0 ? $"+{Clip.GainDb:F1}dB" : $"{Clip.GainDb:F1}dB";
        }
        else
        {
            GainIndicator.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateMutedState()
    {
        var isMuted = Clip?.IsMuted ?? false;
        MutedOverlay.Visibility = isMuted ? Visibility.Visible : Visibility.Collapsed;
        MuteMenuItem.Header = isMuted ? "Unmute" : "Mute";
    }

    private void UpdateCenterLine()
    {
        var height = WaveformCanvas.ActualHeight;
        if (height <= 0) return;

        CenterLine.X1 = 0;
        CenterLine.X2 = WaveformCanvas.ActualWidth;
        CenterLine.Y1 = height / 2;
        CenterLine.Y2 = height / 2;
    }

    private void UpdateFadeCurves()
    {
        if (Clip == null) return;

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        // Fade In
        if (Clip.FadeInDuration > 0 && Clip.Length > 0)
        {
            var fadeWidth = (Clip.FadeInDuration / Clip.Length) * width;
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(0, 0),
                IsClosed = true
            };
            figure.Segments.Add(new LineSegment(new Point(fadeWidth, 0), true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(fadeWidth * 0.7, height * 0.3),
                new Point(0, height), true));
            geometry.Figures.Add(figure);

            FadeInPath.Data = geometry;
            FadeInPath.Visibility = Visibility.Visible;
        }
        else
        {
            FadeInPath.Visibility = Visibility.Collapsed;
        }

        // Fade Out
        if (Clip.FadeOutDuration > 0 && Clip.Length > 0)
        {
            var fadeWidth = (Clip.FadeOutDuration / Clip.Length) * width;
            var startX = width - fadeWidth;
            var geometry = new PathGeometry();
            var figure = new PathFigure
            {
                StartPoint = new Point(width, 0),
                IsClosed = true
            };
            figure.Segments.Add(new LineSegment(new Point(startX, 0), true));
            figure.Segments.Add(new QuadraticBezierSegment(
                new Point(startX + fadeWidth * 0.3, height * 0.3),
                new Point(width, height), true));
            geometry.Figures.Add(figure);

            FadeOutPath.Data = geometry;
            FadeOutPath.Visibility = Visibility.Visible;
        }
        else
        {
            FadeOutPath.Visibility = Visibility.Collapsed;
        }
    }

    private void RenderWaveform()
    {
        var data = WaveformData;
        if (data == null || data.Length == 0)
        {
            WaveformPath.Data = null;
            return;
        }

        var width = WaveformCanvas.ActualWidth;
        var height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
            return;

        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            var samplesPerPixel = Math.Max(1, data.Length / (int)width);
            var pixelCount = Math.Min((int)width, data.Length);

            var topPoints = new Point[pixelCount];
            var bottomPoints = new Point[pixelCount];

            for (int x = 0; x < pixelCount; x++)
            {
                var sampleIndex = x * samplesPerPixel;
                if (sampleIndex >= data.Length) break;

                var min = 0f;
                var max = 0f;
                for (int i = 0; i < samplesPerPixel && sampleIndex + i < data.Length; i++)
                {
                    var sample = data[sampleIndex + i];
                    if (sample < min) min = sample;
                    if (sample > max) max = sample;
                }

                topPoints[x] = new Point(x, centerY - max * halfHeight);
                bottomPoints[x] = new Point(x, centerY - min * halfHeight);
            }

            // Draw polygon
            context.BeginFigure(topPoints[0], true, true);

            for (int i = 1; i < pixelCount; i++)
            {
                context.LineTo(topPoints[i], true, false);
            }

            for (int i = pixelCount - 1; i >= 0; i--)
            {
                context.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();
        WaveformPath.Data = geometry;
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
        else if (_isAdjustingFadeIn || _isAdjustingFadeOut)
        {
            FadeChanged?.Invoke(this, new ClipFadeChangedEventArgs(Clip!, _originalFadeIn, _originalFadeOut));
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
        _isAdjustingFadeIn = false;
        _isAdjustingFadeOut = false;

        ReleaseMouseCapture();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;
        // Movement handled by parent
    }

    private void OnMouseEnter(object sender, MouseEventArgs e)
    {
        FadeInHandle.Visibility = Visibility.Visible;
        FadeOutHandle.Visibility = Visibility.Visible;
    }

    private void OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isAdjustingFadeIn && !_isAdjustingFadeOut)
        {
            FadeInHandle.Visibility = Visibility.Collapsed;
            FadeOutHandle.Visibility = Visibility.Collapsed;
        }
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
        if (Clip == null || Clip.IsLocked) return;

        _isAdjustingFadeIn = true;
        _dragStartPoint = e.GetPosition(this);
        _originalFadeIn = Clip.FadeInDuration;

        CaptureMouse();
        e.Handled = true;
    }

    private void FadeOutHandle_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Clip == null || Clip.IsLocked) return;

        _isAdjustingFadeOut = true;
        _dragStartPoint = e.GetPosition(this);
        _originalFadeOut = Clip.FadeOutDuration;

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

    private void SetFadeIn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var duration))
            {
                Clip?.SetFadeInCommand.Execute(duration);
            }
        }
    }

    private void SetFadeOut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is string tagStr)
        {
            if (double.TryParse(tagStr, out var duration))
            {
                Clip?.SetFadeOutCommand.Execute(duration);
            }
        }
    }

    private void NormalizeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Normalize the clip's gain to 0 dB
        if (Clip != null)
        {
            // Assuming a default current peak of -3 dB (actual implementation would analyze the audio)
            var currentPeak = -3.0;
            var targetPeak = 0.0;
            var gainAdjustment = targetPeak - currentPeak;
            Clip.GainDb = Math.Clamp(Clip.GainDb + gainAdjustment, -96.0, 12.0);
        }
    }

    private void ReverseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Toggle reverse state - handled by raising an event to parent
        // The actual audio reversal would be handled by the audio engine
        Clip?.EditCommand.Execute(null);
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
