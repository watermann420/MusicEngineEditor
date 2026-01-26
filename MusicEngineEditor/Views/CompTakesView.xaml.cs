using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MusicEngineEditor.Views;

/// <summary>
/// Advanced comping UI for selecting the best parts from multiple takes.
/// </summary>
public partial class CompTakesView : UserControl
{
    #region Dependency Properties

    public static readonly DependencyProperty TakesProperty =
        DependencyProperty.Register(nameof(Takes), typeof(ObservableCollection<CompTakeViewModel>), typeof(CompTakesView),
            new PropertyMetadata(null, OnTakesChanged));

    public static readonly DependencyProperty PixelsPerBeatProperty =
        DependencyProperty.Register(nameof(PixelsPerBeat), typeof(double), typeof(CompTakesView),
            new PropertyMetadata(40.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty BpmProperty =
        DependencyProperty.Register(nameof(Bpm), typeof(double), typeof(CompTakesView),
            new PropertyMetadata(120.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty TakeHeightProperty =
        DependencyProperty.Register(nameof(TakeHeight), typeof(double), typeof(CompTakesView),
            new PropertyMetadata(60.0, OnDisplayPropertyChanged));

    public static readonly DependencyProperty DefaultCrossfadeMsProperty =
        DependencyProperty.Register(nameof(DefaultCrossfadeMs), typeof(double), typeof(CompTakesView),
            new PropertyMetadata(10.0));

    #endregion

    #region Properties

    public ObservableCollection<CompTakeViewModel>? Takes
    {
        get => (ObservableCollection<CompTakeViewModel>?)GetValue(TakesProperty);
        set => SetValue(TakesProperty, value);
    }

    public double PixelsPerBeat
    {
        get => (double)GetValue(PixelsPerBeatProperty);
        set => SetValue(PixelsPerBeatProperty, value);
    }

    public double Bpm
    {
        get => (double)GetValue(BpmProperty);
        set => SetValue(BpmProperty, value);
    }

    public double TakeHeight
    {
        get => (double)GetValue(TakeHeightProperty);
        set => SetValue(TakeHeightProperty, value);
    }

    public double DefaultCrossfadeMs
    {
        get => (double)GetValue(DefaultCrossfadeMsProperty);
        set => SetValue(DefaultCrossfadeMsProperty, value);
    }

    #endregion

    #region Events

    public event EventHandler<CompRegionEventArgs>? CompRegionSelected;
    public event EventHandler<CompRegionEventArgs>? CompRegionCleared;
    public event EventHandler? CompFlattened;
    public event EventHandler? AutoCompRequested;

    #endregion

    #region Fields

    private readonly ObservableCollection<CompRegionViewModel> _compRegions = new();
    private readonly Dictionary<string, Path> _waveformPaths = new();
    private readonly List<Rectangle> _compRegionRects = new();

    private bool _isSelecting;
    private Point _selectionStart;
    private double _selectionStartBeat;
    private int _selectionTakeIndex = -1;
    private Rectangle? _selectionPreview;
    private CompTakeViewModel? _selectedTake;

    private bool _isDraggingCrossfade;
    private Ellipse? _draggedCrossfadeHandle;
    private CompRegionViewModel? _draggedRegion;

    private Point _contextMenuPosition;

    #endregion

    public CompTakesView()
    {
        InitializeComponent();

        Takes = new ObservableCollection<CompTakeViewModel>();
        TakeHeadersControl.ItemsSource = Takes;

        SizeChanged += OnSizeChanged;
        CrossfadeText.Text = $"{DefaultCrossfadeMs:F0}ms";
    }

    #region Property Changed Callbacks

    private static void OnTakesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CompTakesView view)
        {
            if (e.OldValue is ObservableCollection<CompTakeViewModel> oldCollection)
            {
                oldCollection.CollectionChanged -= view.OnTakesCollectionChanged;
            }

            if (e.NewValue is ObservableCollection<CompTakeViewModel> newCollection)
            {
                newCollection.CollectionChanged += view.OnTakesCollectionChanged;
                view.TakeHeadersControl.ItemsSource = newCollection;
            }

            view.UpdateDisplay();
            view.UpdateTakeCount();
        }
    }

    private void OnTakesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        UpdateDisplay();
        UpdateTakeCount();
    }

    private static void OnDisplayPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CompTakesView view)
        {
            view.UpdateDisplay();
        }
    }

    #endregion

    #region Event Handlers

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateDisplay();
    }

    private void CompTakesView_KeyDown(object sender, KeyEventArgs e)
    {
        // Number keys 1-9 to select takes
        if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            var takeIndex = e.Key - Key.D1;
            if (Takes != null && takeIndex < Takes.Count)
            {
                SelectTake(Takes[takeIndex]);
                e.Handled = true;
            }
        }
        else if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad9)
        {
            var takeIndex = e.Key - Key.NumPad1;
            if (Takes != null && takeIndex < Takes.Count)
            {
                SelectTake(Takes[takeIndex]);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.None)
        {
            // C to clear current region
            ClearCompButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.F && Keyboard.Modifiers == ModifierKeys.None)
        {
            // F to flatten
            FlattenButton_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.A && Keyboard.Modifiers == ModifierKeys.Control)
        {
            // Ctrl+A to auto-comp
            AutoCompButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private void TakeHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is CompTakeViewModel take)
        {
            SelectTake(take);
        }
    }

    private void StarButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton button && button.Tag is string tagStr)
        {
            var parent = FindParent<Border>(button);
            if (parent?.DataContext is CompTakeViewModel take)
            {
                if (int.TryParse(tagStr, out var rating))
                {
                    take.SetRating(rating);
                }
            }
        }
    }

    private void AutoCompButton_Click(object sender, RoutedEventArgs e)
    {
        PerformAutoComp();
        AutoCompRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearCompButton_Click(object sender, RoutedEventArgs e)
    {
        ClearAllCompRegions();
    }

    private void FlattenButton_Click(object sender, RoutedEventArgs e)
    {
        CompFlattened?.Invoke(this, EventArgs.Empty);
    }

    private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        PixelsPerBeat = 40.0 * e.NewValue;
    }

    private void CompSelectionCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(CompSelectionCanvas);
        _selectionStart = pos;
        _isSelecting = true;

        // Determine which take we're clicking on
        _selectionTakeIndex = (int)(pos.Y / TakeHeight);
        _selectionStartBeat = PixelsToBeat(pos.X);

        // Create selection preview rectangle
        _selectionPreview = new Rectangle
        {
            Fill = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xEB, 0x3B)),
            Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
            StrokeThickness = 1,
            Height = TakeHeight - 4
        };
        Canvas.SetLeft(_selectionPreview, pos.X);
        Canvas.SetTop(_selectionPreview, _selectionTakeIndex * TakeHeight + 2);
        CompSelectionCanvas.Children.Add(_selectionPreview);

        CompSelectionCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void CompSelectionCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting && _selectionPreview != null)
        {
            var pos = e.GetPosition(CompSelectionCanvas);
            var left = Math.Min(_selectionStart.X, pos.X);
            var width = Math.Abs(pos.X - _selectionStart.X);

            Canvas.SetLeft(_selectionPreview, left);
            _selectionPreview.Width = width;
        }
    }

    private void CompSelectionCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting && _selectionPreview != null && Takes != null)
        {
            var pos = e.GetPosition(CompSelectionCanvas);
            var endBeat = PixelsToBeat(pos.X);

            // Only create region if we selected a meaningful area
            if (Math.Abs(endBeat - _selectionStartBeat) > 0.125 && _selectionTakeIndex >= 0 && _selectionTakeIndex < Takes.Count)
            {
                var startBeat = Math.Min(_selectionStartBeat, endBeat);
                var regionEndBeat = Math.Max(_selectionStartBeat, endBeat);

                var take = Takes[_selectionTakeIndex];
                var region = new CompRegionViewModel
                {
                    Take = take,
                    StartBeat = startBeat,
                    EndBeat = regionEndBeat,
                    CrossfadeMs = DefaultCrossfadeMs
                };

                AddCompRegion(region);
            }

            CompSelectionCanvas.Children.Remove(_selectionPreview);
            _selectionPreview = null;
        }

        _isSelecting = false;
        CompSelectionCanvas.ReleaseMouseCapture();
    }

    private void CrossfadeHandlesCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(CrossfadeHandlesCanvas);
        var handle = FindCrossfadeHandleAtPosition(pos);
        if (handle != null)
        {
            _isDraggingCrossfade = true;
            _draggedCrossfadeHandle = handle;
            _draggedRegion = handle.Tag as CompRegionViewModel;
            CrossfadeHandlesCanvas.CaptureMouse();
            e.Handled = true;
        }
    }

    private void CrossfadeHandlesCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingCrossfade && _draggedCrossfadeHandle != null && _draggedRegion != null)
        {
            var pos = e.GetPosition(CrossfadeHandlesCanvas);
            // Adjust crossfade length based on drag distance
            var beat = PixelsToBeat(pos.X);
            var newCrossfade = Math.Max(0, Math.Min(100, Math.Abs(beat - _draggedRegion.StartBeat) * (60000 / Bpm)));
            _draggedRegion.CrossfadeMs = newCrossfade;
            UpdateCrossfadeHandles();
            CrossfadeText.Text = $"{newCrossfade:F0}ms";
        }
    }

    private void CrossfadeHandlesCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingCrossfade = false;
        _draggedCrossfadeHandle = null;
        _draggedRegion = null;
        CrossfadeHandlesCanvas.ReleaseMouseCapture();
    }

    private void UseForRegionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Use selected take for current region
        if (_selectedTake != null)
        {
            var beat = PixelsToBeat(_contextMenuPosition.X);
            var existingRegion = FindRegionAtBeat(beat);
            if (existingRegion != null)
            {
                existingRegion.Take = _selectedTake;
                UpdateCompDisplay();
            }
        }
    }

    private void ClearRegionMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var beat = PixelsToBeat(_contextMenuPosition.X);
        var region = FindRegionAtBeat(beat);
        if (region != null)
        {
            RemoveCompRegion(region);
        }
    }

    private void ClearAllRegionsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ClearAllCompRegions();
    }

    private void SetCrossfadeMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Show crossfade dialog (simplified - in real app would show input dialog)
        var result = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter crossfade duration in milliseconds:",
            "Set Crossfade",
            DefaultCrossfadeMs.ToString());

        if (double.TryParse(result, out var ms))
        {
            DefaultCrossfadeMs = Math.Max(0, Math.Min(1000, ms));
            CrossfadeText.Text = $"{DefaultCrossfadeMs:F0}ms";
        }
    }

    private void AutoCompMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AutoCompButton_Click(sender, e);
    }

    private void FlattenMenuItem_Click(object sender, RoutedEventArgs e)
    {
        FlattenButton_Click(sender, e);
    }

    #endregion

    #region Public Methods

    public void SelectTake(CompTakeViewModel take)
    {
        if (_selectedTake != null)
        {
            _selectedTake.IsSelected = false;
        }

        _selectedTake = take;
        take.IsSelected = true;

        // Highlight selected take
        UpdateTakeHighlight();
    }

    public void AddCompRegion(CompRegionViewModel region)
    {
        // Remove any overlapping regions
        var overlapping = _compRegions
            .Where(r => r.StartBeat < region.EndBeat && r.EndBeat > region.StartBeat)
            .ToList();

        foreach (var r in overlapping)
        {
            _compRegions.Remove(r);
        }

        _compRegions.Add(region);
        UpdateCompDisplay();
        UpdateCompStatus();

        CompRegionSelected?.Invoke(this, new CompRegionEventArgs(region));
    }

    public void RemoveCompRegion(CompRegionViewModel region)
    {
        _compRegions.Remove(region);
        UpdateCompDisplay();
        UpdateCompStatus();

        CompRegionCleared?.Invoke(this, new CompRegionEventArgs(region));
    }

    public void ClearAllCompRegions()
    {
        _compRegions.Clear();
        UpdateCompDisplay();
        UpdateCompStatus();
    }

    public void PerformAutoComp()
    {
        if (Takes == null || Takes.Count == 0) return;

        // Simple auto-comp: select highest-rated take for each beat
        var duration = Takes.Max(t => t.DurationBeats);
        var beatStep = 4.0; // Every measure (4 beats)

        ClearAllCompRegions();

        for (var beat = 0.0; beat < duration; beat += beatStep)
        {
            var endBeat = Math.Min(beat + beatStep, duration);
            var bestTake = Takes.OrderByDescending(t => t.Rating).First();

            var region = new CompRegionViewModel
            {
                Take = bestTake,
                StartBeat = beat,
                EndBeat = endBeat,
                CrossfadeMs = DefaultCrossfadeMs
            };

            _compRegions.Add(region);
        }

        UpdateCompDisplay();
        UpdateCompStatus();
    }

    public IReadOnlyList<CompRegionViewModel> GetCompRegions() => _compRegions.AsReadOnly();

    #endregion

    #region Private Methods

    private void UpdateDisplay()
    {
        UpdateTimeline();
        UpdateTakeWaveforms();
        UpdateCompDisplay();
        UpdateCrossfadeHandles();
        UpdateCompPreview();
    }

    private void UpdateTimeline()
    {
        TimelineRuler.Children.Clear();

        if (Takes == null || Takes.Count == 0) return;

        var duration = Takes.Max(t => t.DurationBeats);

        // Draw beat/measure lines
        for (var beat = 0; beat <= duration + 4; beat++)
        {
            var x = BeatToPixels(beat);
            var isMeasure = beat % 4 == 0;

            var line = new Line
            {
                X1 = x, X2 = x,
                Y1 = isMeasure ? 0 : 14,
                Y2 = 24,
                Stroke = isMeasure ? Brushes.White : new SolidColorBrush(Color.FromRgb(0x60, 0x60, 0x60)),
                StrokeThickness = isMeasure ? 1 : 0.5
            };
            TimelineRuler.Children.Add(line);

            if (isMeasure)
            {
                var label = new TextBlock
                {
                    Text = ((beat / 4) + 1).ToString(),
                    FontSize = 10,
                    Foreground = Brushes.White
                };
                Canvas.SetLeft(label, x + 2);
                Canvas.SetTop(label, 2);
                TimelineRuler.Children.Add(label);
            }
        }
    }

    private void UpdateTakeWaveforms()
    {
        TakeLanesCanvas.Children.Clear();
        _waveformPaths.Clear();

        if (Takes == null) return;

        for (var i = 0; i < Takes.Count; i++)
        {
            var take = Takes[i];
            var y = i * TakeHeight;

            // Background
            var background = new Rectangle
            {
                Width = BeatToPixels(take.DurationBeats),
                Height = TakeHeight - 4,
                Fill = new SolidColorBrush(Color.FromArgb(0x30, take.Color.R, take.Color.G, take.Color.B)),
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(background, 0);
            Canvas.SetTop(background, y + 2);
            TakeLanesCanvas.Children.Add(background);

            // Border
            var border = new Rectangle
            {
                Width = BeatToPixels(take.DurationBeats),
                Height = TakeHeight - 4,
                Stroke = new SolidColorBrush(Color.FromRgb(take.Color.R, take.Color.G, take.Color.B)),
                StrokeThickness = 1,
                Fill = Brushes.Transparent,
                RadiusX = 3,
                RadiusY = 3
            };
            Canvas.SetLeft(border, 0);
            Canvas.SetTop(border, y + 2);
            TakeLanesCanvas.Children.Add(border);

            // Waveform (simplified visualization)
            if (take.WaveformData != null && take.WaveformData.Length > 0)
            {
                var waveform = CreateWaveformPath(take, BeatToPixels(take.DurationBeats), TakeHeight - 8);
                Canvas.SetLeft(waveform, 0);
                Canvas.SetTop(waveform, y + 4);
                TakeLanesCanvas.Children.Add(waveform);
                _waveformPaths[take.Id] = waveform;
            }
        }

        // Update canvas size
        var totalHeight = Takes.Count * TakeHeight;
        var maxWidth = Takes.Count > 0 ? BeatToPixels(Takes.Max(t => t.DurationBeats) + 4) : 100;
        TakeLanesCanvas.Height = Math.Max(totalHeight, TakeLanesScrollViewer.ActualHeight);
        TakeLanesCanvas.Width = Math.Max(maxWidth, TakeLanesScrollViewer.ActualWidth);
        CompSelectionCanvas.Height = TakeLanesCanvas.Height;
        CompSelectionCanvas.Width = TakeLanesCanvas.Width;
        CrossfadeHandlesCanvas.Height = TakeLanesCanvas.Height;
        CrossfadeHandlesCanvas.Width = TakeLanesCanvas.Width;
    }

    private Path CreateWaveformPath(CompTakeViewModel take, double width, double height)
    {
        var data = take.WaveformData;
        if (data == null || data.Length == 0)
        {
            return new Path { Fill = Brushes.Transparent };
        }

        var samplesPerPixel = Math.Max(1, (int)(data.Length / width));
        var centerY = height / 2;
        var halfHeight = height / 2 * 0.9;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var pixelCount = (int)Math.Min(width, data.Length / samplesPerPixel);
            if (pixelCount <= 0) return new Path { Fill = Brushes.Transparent };

            var topPoints = new Point[pixelCount];
            var bottomPoints = new Point[pixelCount];

            for (var x = 0; x < pixelCount; x++)
            {
                var sampleIndex = x * samplesPerPixel;
                float min = 0, max = 0;

                for (var i = 0; i < samplesPerPixel && sampleIndex + i < data.Length; i++)
                {
                    var sample = data[sampleIndex + i];
                    if (sample < min) min = sample;
                    if (sample > max) max = sample;
                }

                topPoints[x] = new Point(x, centerY - max * halfHeight);
                bottomPoints[x] = new Point(x, centerY - min * halfHeight);
            }

            ctx.BeginFigure(topPoints[0], true, true);
            for (var i = 1; i < pixelCount; i++)
            {
                ctx.LineTo(topPoints[i], true, false);
            }
            for (var i = pixelCount - 1; i >= 0; i--)
            {
                ctx.LineTo(bottomPoints[i], true, false);
            }
        }

        geometry.Freeze();

        return new Path
        {
            Data = geometry,
            Fill = new SolidColorBrush(Color.FromRgb(take.Color.R, take.Color.G, take.Color.B)),
            Opacity = 0.6
        };
    }

    private void UpdateCompDisplay()
    {
        // Clear existing comp overlays
        var toRemove = CompSelectionCanvas.Children.OfType<Rectangle>()
            .Where(r => r.Tag is string s && s == "CompRegion").ToList();
        foreach (var r in toRemove)
        {
            CompSelectionCanvas.Children.Remove(r);
        }

        if (Takes == null) return;

        foreach (var region in _compRegions)
        {
            var takeIndex = Takes.IndexOf(region.Take);
            if (takeIndex < 0) continue;

            var y = takeIndex * TakeHeight;
            var x = BeatToPixels(region.StartBeat);
            var width = BeatToPixels(region.EndBeat - region.StartBeat);

            var rect = new Rectangle
            {
                Width = width,
                Height = TakeHeight - 4,
                Fill = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xEB, 0x3B)),
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
                StrokeThickness = 2,
                RadiusX = 3,
                RadiusY = 3,
                Tag = "CompRegion"
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y + 2);
            CompSelectionCanvas.Children.Add(rect);
        }
    }

    private void UpdateCrossfadeHandles()
    {
        CrossfadeHandlesCanvas.Children.Clear();

        if (Takes == null) return;

        foreach (var region in _compRegions)
        {
            var takeIndex = Takes.IndexOf(region.Take);
            if (takeIndex < 0) continue;

            var y = takeIndex * TakeHeight + TakeHeight / 2;

            // Start handle
            var startX = BeatToPixels(region.StartBeat);
            var startHandle = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.SizeWE,
                Tag = region
            };
            Canvas.SetLeft(startHandle, startX - 5);
            Canvas.SetTop(startHandle, y - 5);
            CrossfadeHandlesCanvas.Children.Add(startHandle);

            // End handle
            var endX = BeatToPixels(region.EndBeat);
            var endHandle = new Ellipse
            {
                Width = 10,
                Height = 10,
                Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xBC, 0xD4)),
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Cursor = Cursors.SizeWE,
                Tag = region
            };
            Canvas.SetLeft(endHandle, endX - 5);
            Canvas.SetTop(endHandle, y - 5);
            CrossfadeHandlesCanvas.Children.Add(endHandle);
        }
    }

    private void UpdateCompPreview()
    {
        CompPreviewCanvas.Children.Clear();

        if (_compRegions.Count == 0) return;

        var sorted = _compRegions.OrderBy(r => r.StartBeat).ToList();
        var previewWidth = CompPreviewCanvas.ActualWidth;
        var totalBeats = sorted.Max(r => r.EndBeat);

        if (totalBeats <= 0 || previewWidth <= 0) return;

        var pixelsPerBeat = previewWidth / totalBeats;

        foreach (var region in sorted)
        {
            var x = region.StartBeat * pixelsPerBeat;
            var width = (region.EndBeat - region.StartBeat) * pixelsPerBeat;

            var rect = new Rectangle
            {
                Width = Math.Max(1, width),
                Height = 20,
                Fill = new SolidColorBrush(Color.FromRgb(region.Take.Color.R, region.Take.Color.G, region.Take.Color.B)),
                Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0x3B)),
                StrokeThickness = 1
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, 2);
            CompPreviewCanvas.Children.Add(rect);
        }
    }

    private void UpdateTakeCount()
    {
        var count = Takes?.Count ?? 0;
        TakeCountText.Text = $" ({count} takes)";
    }

    private void UpdateCompStatus()
    {
        if (_compRegions.Count == 0)
        {
            CompStatusText.Text = "No comp regions selected";
        }
        else
        {
            var totalBeats = _compRegions.Sum(r => r.EndBeat - r.StartBeat);
            CompStatusText.Text = $"{_compRegions.Count} region(s), {totalBeats:F1} beats total";
        }
    }

    private void UpdateTakeHighlight()
    {
        // Update visual highlighting of selected take
        foreach (var kvp in _waveformPaths)
        {
            kvp.Value.Opacity = 0.4;
        }

        if (_selectedTake != null && _waveformPaths.TryGetValue(_selectedTake.Id, out var selectedPath))
        {
            selectedPath.Opacity = 1.0;
        }
    }

    private Ellipse? FindCrossfadeHandleAtPosition(Point pos)
    {
        const double hitRadius = 8;
        return CrossfadeHandlesCanvas.Children.OfType<Ellipse>()
            .FirstOrDefault(e =>
            {
                var left = Canvas.GetLeft(e) + 5;
                var top = Canvas.GetTop(e) + 5;
                return Math.Abs(pos.X - left) < hitRadius && Math.Abs(pos.Y - top) < hitRadius;
            });
    }

    private CompRegionViewModel? FindRegionAtBeat(double beat)
    {
        return _compRegions.FirstOrDefault(r => r.StartBeat <= beat && r.EndBeat >= beat);
    }

    private double BeatToPixels(double beats) => beats * PixelsPerBeat;
    private double PixelsToBeat(double pixels) => pixels / PixelsPerBeat;

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = VisualTreeHelper.GetParent(child);
        while (parent != null && parent is not T)
        {
            parent = VisualTreeHelper.GetParent(parent);
        }
        return parent as T;
    }

    #endregion
}

/// <summary>
/// ViewModel for a take in the comp view.
/// </summary>
public class CompTakeViewModel : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Take 1";
    private int _index = 1;
    private int _rating;
    private bool _isSelected;
    private double _durationBeats = 16;
    private float[]? _waveformData;
    private Color _color = Color.FromRgb(0x4C, 0xAF, 0x50);

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public int Index { get => _index; set { _index = value; OnPropertyChanged(); } }
    public int Rating { get => _rating; set { _rating = value; OnPropertyChanged(); UpdateStars(); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public double DurationBeats { get => _durationBeats; set { _durationBeats = value; OnPropertyChanged(); } }
    public float[]? WaveformData { get => _waveformData; set { _waveformData = value; OnPropertyChanged(); } }
    public Color Color { get => _color; set { _color = value; OnPropertyChanged(); OnPropertyChanged(nameof(ColorBrush)); } }

    public SolidColorBrush ColorBrush => new(Color);
    public string DurationText => $"{DurationBeats / 4:F1} bars";

    public bool Star1 => Rating >= 1;
    public bool Star2 => Rating >= 2;
    public bool Star3 => Rating >= 3;
    public bool Star4 => Rating >= 4;
    public bool Star5 => Rating >= 5;

    public void SetRating(int stars)
    {
        Rating = Rating == stars ? stars - 1 : stars;
    }

    private void UpdateStars()
    {
        OnPropertyChanged(nameof(Star1));
        OnPropertyChanged(nameof(Star2));
        OnPropertyChanged(nameof(Star3));
        OnPropertyChanged(nameof(Star4));
        OnPropertyChanged(nameof(Star5));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for a comp region.
/// </summary>
public class CompRegionViewModel : INotifyPropertyChanged
{
    private CompTakeViewModel _take = null!;
    private double _startBeat;
    private double _endBeat;
    private double _crossfadeMs = 10;

    public CompTakeViewModel Take { get => _take; set { _take = value; OnPropertyChanged(); } }
    public double StartBeat { get => _startBeat; set { _startBeat = value; OnPropertyChanged(); } }
    public double EndBeat { get => _endBeat; set { _endBeat = value; OnPropertyChanged(); } }
    public double CrossfadeMs { get => _crossfadeMs; set { _crossfadeMs = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// Event args for comp region events.
/// </summary>
public class CompRegionEventArgs : EventArgs
{
    public CompRegionViewModel Region { get; }

    public CompRegionEventArgs(CompRegionViewModel region)
    {
        Region = region;
    }
}
