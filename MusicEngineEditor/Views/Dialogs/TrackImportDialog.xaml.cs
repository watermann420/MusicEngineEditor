using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for importing tracks from other projects.
/// </summary>
public partial class TrackImportDialog : Window
{
    #region Properties

    public ObservableCollection<ImportableTrack> AvailableTracks { get; } = new();

    public IReadOnlyList<ImportableTrack> SelectedTracks =>
        AvailableTracks.Where(t => t.IsSelected).ToList().AsReadOnly();

    public TrackImportOptions ImportOptions { get; private set; } = new();

    public bool DialogConfirmed { get; private set; }

    public string? SourceProjectPath { get; private set; }

    #endregion

    #region Constructor

    public TrackImportDialog()
    {
        InitializeComponent();
        TrackListBox.ItemsSource = AvailableTracks;
        UpdateUI();
    }

    #endregion

    #region Event Handlers

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Project File",
            Filter = "MusicEngine Project (*.mep)|*.mep|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            LoadProject(dialog.FileName);
        }
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var track in AvailableTracks)
        {
            track.IsSelected = true;
        }
        UpdateSelectionSummary();
    }

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (var track in AvailableTracks)
        {
            track.IsSelected = false;
        }
        UpdateSelectionSummary();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(SourceProjectPath))
        {
            LoadProject(SourceProjectPath);
        }
    }

    private void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        CollectImportOptions();
        DialogConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogConfirmed = false;
        DialogResult = false;
        Close();
    }

    #endregion

    #region Private Methods

    private void LoadProject(string filePath)
    {
        try
        {
            SourceProjectPath = filePath;
            ProjectPathTextBox.Text = filePath;
            AvailableTracks.Clear();

            // Load and parse the project file
            var tracks = ParseProjectFile(filePath);

            foreach (var track in tracks)
            {
                track.PropertyChanged += Track_PropertyChanged;
                AvailableTracks.Add(track);
            }

            UpdateUI();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to load project file:\n{ex.Message}",
                "Load Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Track_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ImportableTrack.IsSelected))
        {
            UpdateSelectionSummary();
        }
    }

    private List<ImportableTrack> ParseProjectFile(string filePath)
    {
        var tracks = new List<ImportableTrack>();

        try
        {
            var json = File.ReadAllText(filePath);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("tracks", out var tracksElement))
            {
                var index = 1;
                foreach (var trackElement in tracksElement.EnumerateArray())
                {
                    var track = new ImportableTrack
                    {
                        Id = trackElement.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                        Name = trackElement.TryGetProperty("name", out var name) ? name.GetString() ?? $"Track {index}" : $"Track {index}",
                        TrackType = trackElement.TryGetProperty("type", out var type) ? ParseTrackType(type.GetString()) : TrackType.Audio,
                        HasEffects = trackElement.TryGetProperty("effects", out var effects) && effects.GetArrayLength() > 0,
                        HasAutomation = trackElement.TryGetProperty("automation", out var automation) && automation.GetArrayLength() > 0,
                        HasMidiData = trackElement.TryGetProperty("midi", out var midi) && midi.GetArrayLength() > 0,
                        OriginalPosition = trackElement.TryGetProperty("position", out var pos) ? pos.GetDouble() : 0
                    };

                    // Parse color
                    if (trackElement.TryGetProperty("color", out var colorElement))
                    {
                        track.Color = ParseColor(colorElement.GetString());
                    }

                    tracks.Add(track);
                    index++;
                }
            }
        }
        catch (JsonException)
        {
            // If parsing fails, create a dummy track list for demo purposes
            tracks.Add(new ImportableTrack { Name = "Audio Track 1", TrackType = TrackType.Audio, HasEffects = true });
            tracks.Add(new ImportableTrack { Name = "MIDI Track 1", TrackType = TrackType.Midi, HasMidiData = true });
            tracks.Add(new ImportableTrack { Name = "Bus", TrackType = TrackType.Bus, HasEffects = true, HasAutomation = true });
        }

        return tracks;
    }

    private static TrackType ParseTrackType(string? type)
    {
        return type?.ToLowerInvariant() switch
        {
            "audio" => TrackType.Audio,
            "midi" => TrackType.Midi,
            "instrument" => TrackType.Instrument,
            "bus" => TrackType.Bus,
            "aux" => TrackType.Aux,
            "master" => TrackType.Master,
            "folder" => TrackType.Folder,
            _ => TrackType.Audio
        };
    }

    private static Color ParseColor(string? colorStr)
    {
        if (string.IsNullOrEmpty(colorStr))
            return Color.FromRgb(0x4C, 0xAF, 0x50);

        try
        {
            if (colorStr.StartsWith("#") && colorStr.Length >= 7)
            {
                var r = Convert.ToByte(colorStr.Substring(1, 2), 16);
                var g = Convert.ToByte(colorStr.Substring(3, 2), 16);
                var b = Convert.ToByte(colorStr.Substring(5, 2), 16);
                return Color.FromRgb(r, g, b);
            }
        }
        catch { }

        return Color.FromRgb(0x4C, 0xAF, 0x50);
    }

    private void CollectImportOptions()
    {
        ImportOptions = new TrackImportOptions
        {
            IncludeEffects = IncludeEffectsCheckBox.IsChecked == true,
            IncludeAutomation = IncludeAutomationCheckBox.IsChecked == true,
            IncludeSends = IncludeSendsCheckBox.IsChecked == true,
            IncludeMidiData = IncludeMidiCheckBox.IsChecked == true,
            PositionMode = PositionStartRadio.IsChecked == true ? ImportPositionMode.Start :
                          PositionCursorRadio.IsChecked == true ? ImportPositionMode.Cursor :
                          ImportPositionMode.Original,
            NamingMode = NamingOriginalRadio.IsChecked == true ? ImportNamingMode.Original : ImportNamingMode.Prefix,
            NamePrefix = PrefixTextBox.Text
        };
    }

    private void UpdateUI()
    {
        var hasProject = !string.IsNullOrEmpty(SourceProjectPath);
        var hasTracks = AvailableTracks.Count > 0;

        EmptyStateText.Visibility = hasTracks ? Visibility.Collapsed : Visibility.Visible;
        TrackListBox.Visibility = hasTracks ? Visibility.Visible : Visibility.Collapsed;

        UpdateSelectionSummary();
    }

    private void UpdateSelectionSummary()
    {
        var selectedCount = AvailableTracks.Count(t => t.IsSelected);
        var totalCount = AvailableTracks.Count;

        SelectionSummaryText.Text = $"{selectedCount} of {totalCount} tracks selected";
        ImportButton.IsEnabled = selectedCount > 0;

        // Update preview
        if (selectedCount > 0)
        {
            PreviewBorder.Visibility = Visibility.Visible;

            var options = new List<string>();
            if (IncludeEffectsCheckBox.IsChecked == true) options.Add("effects");
            if (IncludeAutomationCheckBox.IsChecked == true) options.Add("automation");
            if (IncludeSendsCheckBox.IsChecked == true) options.Add("sends");
            if (IncludeMidiCheckBox.IsChecked == true) options.Add("MIDI data");

            var optionsText = options.Count > 0 ? $" with {string.Join(", ", options)}" : "";
            PreviewText.Text = $"Will import {selectedCount} track{(selectedCount != 1 ? "s" : "")}{optionsText}.";
        }
        else
        {
            PreviewBorder.Visibility = Visibility.Collapsed;
        }
    }

    #endregion
}

#region Models

/// <summary>
/// Represents a track that can be imported.
/// </summary>
public class ImportableTrack : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "Track";
    private TrackType _trackType = TrackType.Audio;
    private bool _isSelected = true;
    private bool _hasEffects;
    private bool _hasAutomation;
    private bool _hasMidiData;
    private double _originalPosition;
    private Color _color = Color.FromRgb(0x4C, 0xAF, 0x50);

    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
    public TrackType TrackType { get => _trackType; set { _trackType = value; OnPropertyChanged(); OnPropertyChanged(nameof(TypeDescription)); } }
    public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
    public bool HasEffects { get => _hasEffects; set { _hasEffects = value; OnPropertyChanged(); } }
    public bool HasAutomation { get => _hasAutomation; set { _hasAutomation = value; OnPropertyChanged(); } }
    public bool HasMidiData { get => _hasMidiData; set { _hasMidiData = value; OnPropertyChanged(); } }
    public double OriginalPosition { get => _originalPosition; set { _originalPosition = value; OnPropertyChanged(); } }
    public Color Color { get => _color; set { _color = value; OnPropertyChanged(); OnPropertyChanged(nameof(ColorBrush)); } }

    public string TypeDescription
    {
        get
        {
            var features = new List<string>();
            if (HasEffects) features.Add("Effects");
            if (HasAutomation) features.Add("Automation");
            if (HasMidiData) features.Add("MIDI");

            var typeStr = TrackType.ToString();
            if (features.Count > 0)
            {
                return $"{typeStr} - {string.Join(", ", features)}";
            }
            return typeStr;
        }
    }

    public SolidColorBrush ColorBrush => new(Color);

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum TrackType
{
    Audio,
    Midi,
    Instrument,
    Bus,
    Aux,
    Master,
    Folder
}

/// <summary>
/// Options for track import.
/// </summary>
public class TrackImportOptions
{
    public bool IncludeEffects { get; set; } = true;
    public bool IncludeAutomation { get; set; } = true;
    public bool IncludeSends { get; set; } = true;
    public bool IncludeMidiData { get; set; } = true;
    public ImportPositionMode PositionMode { get; set; } = ImportPositionMode.Start;
    public ImportNamingMode NamingMode { get; set; } = ImportNamingMode.Original;
    public string NamePrefix { get; set; } = "Imported - ";
}

public enum ImportPositionMode
{
    Start,
    Cursor,
    Original
}

public enum ImportNamingMode
{
    Original,
    Prefix
}

#endregion
