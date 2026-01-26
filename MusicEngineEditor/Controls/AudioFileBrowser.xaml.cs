// MusicEngineEditor - Audio File Browser Control
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Services;
using MusicEngineEditor.ViewModels;
using NAudio.Wave;
using Path = System.IO.Path;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a file or folder item in the browser.
/// </summary>
public partial class BrowserItem : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private bool _isFile;

    [ObservableProperty]
    private bool _isFolder;

    [ObservableProperty]
    private string _icon = "&#x1F4C1;";

    [ObservableProperty]
    private string _format = string.Empty;

    [ObservableProperty]
    private string _metadata = string.Empty;

    [ObservableProperty]
    private bool _hasMetadata;

    [ObservableProperty]
    private double? _bpm;

    [ObservableProperty]
    private string? _key;

    [ObservableProperty]
    private TimeSpan _duration;

    [ObservableProperty]
    private bool _hasBpm;

    [ObservableProperty]
    private bool _hasKey;

    public string BpmDisplay => Bpm.HasValue ? $"{Bpm:F0} BPM" : "";
    public string KeyDisplay => Key ?? "";
    public string DurationDisplay => Duration.TotalSeconds > 0 ? $"{Duration:mm\\:ss}" : "";
}

/// <summary>
/// Represents a favorite folder.
/// </summary>
public class FavoriteFolder
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IsFavorite { get; set; } = true;
}

/// <summary>
/// ViewModel for the Audio File Browser.
/// </summary>
public partial class AudioFileBrowserViewModel : ViewModelBase
{
    private readonly WaveformService _waveformService;
    private WaveOutEvent? _previewPlayer;
    private AudioFileReader? _previewReader;
    private CancellationTokenSource? _waveformCts;

    // Navigation
    [ObservableProperty]
    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _selectedFormatFilter = "All Audio";

    // Collections
    public ObservableCollection<BrowserItem> Items { get; } = new();
    public ObservableCollection<BrowserItem> FilteredItems { get; } = new();
    public ObservableCollection<FavoriteFolder> FavoriteFolders { get; } = new();
    public ObservableCollection<string> FormatFilters { get; } = new()
    {
        "All Audio",
        "WAV",
        "MP3",
        "FLAC",
        "OGG",
        "AIFF"
    };

    [ObservableProperty]
    private BrowserItem? _selectedItem;

    // Preview state
    [ObservableProperty]
    private bool _showWaveformPreview;

    [ObservableProperty]
    private bool _isLoadingWaveform;

    [ObservableProperty]
    private bool _isPreviewPlaying;

    [ObservableProperty]
    private string _previewFileName = string.Empty;

    [ObservableProperty]
    private float[]? _waveformData;

    // Status
    [ObservableProperty]
    private bool _hasFavorites;

    [ObservableProperty]
    private bool _canImport;

    public event EventHandler<string>? FileImportRequested;
    public event EventHandler<float[]>? WaveformDataLoaded;

    public AudioFileBrowserViewModel()
    {
        _waveformService = new WaveformService();

        InitializeFavorites();
        LoadDirectory(CurrentPath);
    }

    private void InitializeFavorites()
    {
        // Add default favorite locations
        FavoriteFolders.Add(new FavoriteFolder
        {
            Name = "Music",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        });

        FavoriteFolders.Add(new FavoriteFolder
        {
            Name = "Desktop",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
        });

        FavoriteFolders.Add(new FavoriteFolder
        {
            Name = "Documents",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        });

        HasFavorites = FavoriteFolders.Count > 0;
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFormatFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedItemChanged(BrowserItem? value)
    {
        CanImport = value?.IsFile == true;

        if (value?.IsFile == true)
        {
            ShowWaveformPreview = true;
            PreviewFileName = value.Name;
            _ = LoadWaveformPreviewAsync(value.FullPath);
        }
        else
        {
            ShowWaveformPreview = false;
        }
    }

    private void LoadDirectory(string path)
    {
        if (!Directory.Exists(path)) return;

        Items.Clear();
        CurrentPath = path;

        try
        {
            // Add folders first
            foreach (var dir in Directory.GetDirectories(path).OrderBy(d => Path.GetFileName(d)))
            {
                Items.Add(new BrowserItem
                {
                    Name = Path.GetFileName(dir),
                    FullPath = dir,
                    IsFolder = true,
                    Icon = "\U0001F4C1" // Folder icon
                });
            }

            // Add audio files
            var audioExtensions = GetAudioExtensions();
            foreach (var file in Directory.GetFiles(path)
                .Where(f => audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => Path.GetFileName(f)))
            {
                var item = CreateFileItem(file);
                Items.Add(item);
            }

            ApplyFilter();
            StatusMessage = $"{Items.Count(i => i.IsFile)} audio files";
        }
        catch (UnauthorizedAccessException)
        {
            StatusMessage = "Access denied";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    private BrowserItem CreateFileItem(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        var item = new BrowserItem
        {
            Name = Path.GetFileName(path),
            FullPath = path,
            IsFile = true,
            Format = ext.TrimStart('.').ToUpperInvariant(),
            Icon = "\U0001F3B5" // Music note icon
        };

        // Try to read metadata asynchronously
        _ = Task.Run(() => LoadFileMetadata(item));

        return item;
    }

    private void LoadFileMetadata(BrowserItem item)
    {
        try
        {
            using var reader = new AudioFileReader(item.FullPath);
            item.Duration = reader.TotalTime;

            // Build metadata string
            var metadata = new List<string>
            {
                $"{reader.WaveFormat.SampleRate / 1000.0:F1}kHz",
                $"{reader.WaveFormat.Channels}ch"
            };

            if (reader.WaveFormat.BitsPerSample > 0)
            {
                metadata.Add($"{reader.WaveFormat.BitsPerSample}bit");
            }

            item.Metadata = string.Join(", ", metadata);
            item.HasMetadata = true;

            // Auto-detect BPM from filename (common pattern: "120_bpm_sample.wav")
            var name = item.Name.ToLowerInvariant();
            var bpmMatch = System.Text.RegularExpressions.Regex.Match(name, @"(\d{2,3})\s*bpm");
            if (bpmMatch.Success && double.TryParse(bpmMatch.Groups[1].Value, out double bpm))
            {
                item.Bpm = bpm;
                item.HasBpm = true;
            }

            // Auto-detect key from filename (common patterns: "Am", "Cmaj", "F#m")
            var keyMatch = System.Text.RegularExpressions.Regex.Match(name, @"([A-Ga-g][#b]?)\s*(maj|min|m)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (keyMatch.Success)
            {
                var key = keyMatch.Groups[1].Value.ToUpperInvariant();
                var mode = keyMatch.Groups[2].Value.ToLowerInvariant();
                if (mode == "m" || mode == "min")
                {
                    key += "m";
                }
                item.Key = key;
                item.HasKey = true;
            }
        }
        catch
        {
            // Ignore metadata loading errors
        }
    }

    private void ApplyFilter()
    {
        FilteredItems.Clear();

        var filtered = Items.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(i => i.Name.ToLowerInvariant().Contains(search));
        }

        // Apply format filter
        if (SelectedFormatFilter != "All Audio")
        {
            var ext = "." + SelectedFormatFilter.ToLowerInvariant();
            filtered = filtered.Where(i => i.IsFolder || i.FullPath.ToLowerInvariant().EndsWith(ext));
        }

        foreach (var item in filtered)
        {
            FilteredItems.Add(item);
        }

        StatusMessage = $"{FilteredItems.Count(i => i.IsFile)} files shown";
    }

    private static HashSet<string> GetAudioExtensions()
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav", ".mp3", ".flac", ".ogg", ".aiff", ".aif", ".m4a", ".wma"
        };
    }

    private async Task LoadWaveformPreviewAsync(string path)
    {
        _waveformCts?.Cancel();
        _waveformCts = new CancellationTokenSource();

        IsLoadingWaveform = true;

        try
        {
            var data = await _waveformService.LoadFromFileAsync(path, _waveformCts.Token);
            WaveformData = data.Samples;
            WaveformDataLoaded?.Invoke(this, data.Samples);
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch
        {
            WaveformData = null;
        }
        finally
        {
            IsLoadingWaveform = false;
        }
    }

    [RelayCommand]
    private void GoUp()
    {
        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
        {
            LoadDirectory(parent.FullName);
        }
    }

    [RelayCommand]
    private void GoHome()
    {
        LoadDirectory(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
    }

    [RelayCommand]
    private void AddFavorite()
    {
        if (string.IsNullOrEmpty(CurrentPath) || FavoriteFolders.Any(f => f.Path == CurrentPath))
            return;

        FavoriteFolders.Add(new FavoriteFolder
        {
            Name = Path.GetFileName(CurrentPath),
            Path = CurrentPath
        });
        HasFavorites = true;
    }

    [RelayCommand]
    private void NavigateToFavorite(FavoriteFolder? folder)
    {
        if (folder != null && Directory.Exists(folder.Path))
        {
            LoadDirectory(folder.Path);
        }
    }

    public void NavigateToItem(BrowserItem? item)
    {
        if (item == null) return;

        if (item.IsFolder)
        {
            LoadDirectory(item.FullPath);
        }
    }

    [RelayCommand]
    private void TogglePreview()
    {
        if (IsPreviewPlaying)
        {
            StopPreview();
        }
        else if (SelectedItem?.IsFile == true)
        {
            StartPreview(SelectedItem.FullPath);
        }
    }

    public void StartPreview(string path)
    {
        StopPreview();

        try
        {
            _previewReader = new AudioFileReader(path);
            _previewPlayer = new WaveOutEvent();
            _previewPlayer.Init(_previewReader);
            _previewPlayer.PlaybackStopped += (s, e) =>
            {
                Application.Current?.Dispatcher.Invoke(() =>
                {
                    IsPreviewPlaying = false;
                });
            };
            _previewPlayer.Play();
            IsPreviewPlaying = true;
        }
        catch
        {
            IsPreviewPlaying = false;
        }
    }

    public void StopPreview()
    {
        _previewPlayer?.Stop();
        _previewPlayer?.Dispose();
        _previewReader?.Dispose();
        _previewPlayer = null;
        _previewReader = null;
        IsPreviewPlaying = false;
    }

    [RelayCommand]
    private void Import()
    {
        if (SelectedItem?.IsFile == true)
        {
            FileImportRequested?.Invoke(this, SelectedItem.FullPath);
        }
    }

    public void Cleanup()
    {
        StopPreview();
        _waveformCts?.Cancel();
        _waveformService.Dispose();
    }
}

/// <summary>
/// Audio file browser control with tree navigation, waveform preview, and drag-to-timeline support.
/// </summary>
public partial class AudioFileBrowser : UserControl
{
    private AudioFileBrowserViewModel? _viewModel;

    /// <summary>
    /// Event raised when a file should be imported to the timeline.
    /// </summary>
    public event EventHandler<string>? FileImportRequested;

    public AudioFileBrowser()
    {
        InitializeComponent();
        Loaded += AudioFileBrowser_Loaded;
        Unloaded += AudioFileBrowser_Unloaded;
    }

    private void AudioFileBrowser_Loaded(object sender, RoutedEventArgs e)
    {
        _viewModel = new AudioFileBrowserViewModel();
        _viewModel.FileImportRequested += (s, path) => FileImportRequested?.Invoke(this, path);
        _viewModel.WaveformDataLoaded += OnWaveformDataLoaded;
        DataContext = _viewModel;
    }

    private void AudioFileBrowser_Unloaded(object sender, RoutedEventArgs e)
    {
        _viewModel?.Cleanup();
    }

    private void OnWaveformDataLoaded(object? sender, float[] data)
    {
        Dispatcher.Invoke(() => DrawWaveform(data));
    }

    private void DrawWaveform(float[] samples)
    {
        WaveformCanvas.Children.Clear();

        if (samples == null || samples.Length == 0) return;

        double width = WaveformCanvas.ActualWidth;
        double height = WaveformCanvas.ActualHeight;

        if (width <= 0 || height <= 0) return;

        double centerY = height / 2;
        int samplesPerPixel = Math.Max(1, samples.Length / (int)width);

        var points = new PointCollection();
        var pointsNeg = new PointCollection();

        for (int x = 0; x < (int)width; x++)
        {
            int start = x * samplesPerPixel;
            int end = Math.Min(start + samplesPerPixel, samples.Length);

            float max = 0, min = 0;
            for (int i = start; i < end; i++)
            {
                if (samples[i] > max) max = samples[i];
                if (samples[i] < min) min = samples[i];
            }

            points.Add(new Point(x, centerY - max * centerY * 0.9));
            pointsNeg.Add(new Point(x, centerY - min * centerY * 0.9));
        }

        // Create waveform polygon
        var allPoints = new PointCollection();
        foreach (var p in points) allPoints.Add(p);
        for (int i = pointsNeg.Count - 1; i >= 0; i--) allPoints.Add(pointsNeg[i]);

        var polygon = new Polygon
        {
            Points = allPoints,
            Fill = new SolidColorBrush(Color.FromArgb(180, 75, 110, 175)),
            Stroke = new SolidColorBrush(Color.FromArgb(255, 75, 110, 175)),
            StrokeThickness = 0.5
        };

        WaveformCanvas.Children.Add(polygon);

        // Center line
        var centerLine = new Line
        {
            X1 = 0,
            Y1 = centerY,
            X2 = width,
            Y2 = centerY,
            Stroke = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)),
            StrokeThickness = 1
        };
        WaveformCanvas.Children.Add(centerLine);
    }

    private void FileListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel?.SelectedItem != null)
        {
            if (_viewModel.SelectedItem.IsFolder)
            {
                _viewModel.NavigateToItem(_viewModel.SelectedItem);
            }
            else
            {
                // Double-click on file imports it
                FileImportRequested?.Invoke(this, _viewModel.SelectedItem.FullPath);
            }
        }
    }

    private void FileListView_KeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel == null) return;

        switch (e.Key)
        {
            case Key.Enter:
                if (_viewModel.SelectedItem?.IsFolder == true)
                {
                    _viewModel.NavigateToItem(_viewModel.SelectedItem);
                }
                else if (_viewModel.SelectedItem?.IsFile == true)
                {
                    FileImportRequested?.Invoke(this, _viewModel.SelectedItem.FullPath);
                }
                e.Handled = true;
                break;

            case Key.Space:
                _viewModel.TogglePreviewCommand.Execute(null);
                e.Handled = true;
                break;

            case Key.Back:
                _viewModel.GoUpCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Navigates to a specific path.
    /// </summary>
    public void NavigateTo(string path)
    {
        if (_viewModel != null && Directory.Exists(path))
        {
            _viewModel.NavigateToItem(new BrowserItem { FullPath = path, IsFolder = true });
        }
    }
}
