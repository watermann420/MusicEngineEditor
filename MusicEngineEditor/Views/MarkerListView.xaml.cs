//MusicEngineEditor - Marker List View
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Views;

/// <summary>
/// Represents the type of marker.
/// </summary>
public enum MarkerListType
{
    /// <summary>Standard cue marker.</summary>
    Cue,
    /// <summary>Loop/cycle marker.</summary>
    Loop,
    /// <summary>Section marker (verse, chorus, etc.).</summary>
    Section
}

/// <summary>
/// Represents a marker in the marker list.
/// </summary>
public partial class MarkerListItem : ObservableObject
{
    private static int _nextId = 1;

    /// <summary>
    /// Gets the unique ID.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Gets or sets the marker name.
    /// </summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>
    /// Gets or sets the position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// Gets or sets the end position for loop markers.
    /// </summary>
    [ObservableProperty]
    private double _endPosition;

    /// <summary>
    /// Gets or sets the marker color (hex).
    /// </summary>
    [ObservableProperty]
    private string _color = "#FFC107";

    /// <summary>
    /// Gets or sets the marker type.
    /// </summary>
    [ObservableProperty]
    private MarkerListType _type = MarkerListType.Cue;

    /// <summary>
    /// Gets or sets the marker notes.
    /// </summary>
    [ObservableProperty]
    private string _notes = "";

    /// <summary>
    /// Gets the position display string (bars:beats).
    /// </summary>
    public string PositionDisplay
    {
        get
        {
            int bar = (int)(Position / 4) + 1;
            int beat = (int)(Position % 4) + 1;
            return $"{bar}:{beat}";
        }
    }

    /// <summary>
    /// Gets the type display string.
    /// </summary>
    public string TypeDisplay => Type.ToString();

    /// <summary>
    /// Creates a new marker list item.
    /// </summary>
    public MarkerListItem()
    {
        Id = _nextId++;
    }

    /// <summary>
    /// Creates a new marker list item with specified values.
    /// </summary>
    public MarkerListItem(string name, double position, MarkerListType type = MarkerListType.Cue) : this()
    {
        Name = name;
        Position = position;
        Type = type;
        Color = type switch
        {
            MarkerListType.Cue => "#FFC107",
            MarkerListType.Loop => "#2196F3",
            MarkerListType.Section => "#4CAF50",
            _ => "#FFC107"
        };
    }

    partial void OnPositionChanged(double value)
    {
        OnPropertyChanged(nameof(PositionDisplay));
    }

    partial void OnTypeChanged(MarkerListType value)
    {
        OnPropertyChanged(nameof(TypeDisplay));
    }
}

/// <summary>
/// View for displaying and managing markers in a DataGrid.
/// </summary>
public partial class MarkerListView : UserControl
{
    private readonly ObservableCollection<MarkerListItem> _markers = [];
    private ICollectionView? _markersView;
    private string _searchFilter = "";
    private int _beatsPerBar = 4;

    /// <summary>
    /// Gets the markers collection.
    /// </summary>
    public ObservableCollection<MarkerListItem> Markers => _markers;

    /// <summary>
    /// Gets or sets whether there is a selection.
    /// </summary>
    public bool HasSelection => MarkerGrid.SelectedItem != null;

    /// <summary>
    /// Gets or sets the beats per bar for position display.
    /// </summary>
    public int BeatsPerBar
    {
        get => _beatsPerBar;
        set
        {
            _beatsPerBar = value;
            RefreshDisplay();
        }
    }

    /// <summary>
    /// Event raised when a marker is selected for navigation.
    /// </summary>
    public event EventHandler<MarkerListItem>? MarkerNavigated;

    /// <summary>
    /// Event raised when markers are modified.
    /// </summary>
    public event EventHandler? MarkersChanged;

    /// <summary>
    /// Creates a new MarkerListView.
    /// </summary>
    public MarkerListView()
    {
        InitializeComponent();
        DataContext = this;

        _markersView = CollectionViewSource.GetDefaultView(_markers);
        _markersView.Filter = FilterMarkers;
        MarkerGrid.ItemsSource = _markersView;

        InitializeSampleMarkers();
    }

    /// <summary>
    /// Initializes sample markers for demonstration.
    /// </summary>
    private void InitializeSampleMarkers()
    {
        _markers.Add(new MarkerListItem("Intro", 0, MarkerListType.Section));
        _markers.Add(new MarkerListItem("Verse 1", 16, MarkerListType.Section) { Color = "#4CAF50" });
        _markers.Add(new MarkerListItem("Chorus", 48, MarkerListType.Section) { Color = "#E91E63" });
        _markers.Add(new MarkerListItem("Build", 32, MarkerListType.Cue));
        _markers.Add(new MarkerListItem("Drop Loop", 64, MarkerListType.Loop) { EndPosition = 80 });
        _markers.Add(new MarkerListItem("Breakdown", 96, MarkerListType.Section) { Color = "#9C27B0" });
        _markers.Add(new MarkerListItem("Outro", 128, MarkerListType.Section) { Color = "#607D8B" });
    }

    /// <summary>
    /// Adds a new marker.
    /// </summary>
    /// <param name="position">The position in beats.</param>
    /// <param name="name">The marker name.</param>
    /// <param name="type">The marker type.</param>
    /// <returns>The created marker.</returns>
    public MarkerListItem AddMarker(double position, string name = "", MarkerListType type = MarkerListType.Cue)
    {
        var marker = new MarkerListItem(name, position, type);
        _markers.Add(marker);
        MarkersChanged?.Invoke(this, EventArgs.Empty);
        return marker;
    }

    /// <summary>
    /// Removes a marker.
    /// </summary>
    /// <param name="marker">The marker to remove.</param>
    public void RemoveMarker(MarkerListItem marker)
    {
        if (_markers.Remove(marker))
        {
            MarkersChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Gets the selected marker.
    /// </summary>
    public MarkerListItem? GetSelectedMarker()
    {
        return MarkerGrid.SelectedItem as MarkerListItem;
    }

    /// <summary>
    /// Refreshes the display.
    /// </summary>
    public void RefreshDisplay()
    {
        _markersView?.Refresh();
    }

    private bool FilterMarkers(object item)
    {
        if (string.IsNullOrWhiteSpace(_searchFilter))
            return true;

        if (item is MarkerListItem marker)
        {
            return marker.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   marker.Notes.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                   marker.TypeDisplay.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchFilter = SearchBox.Text;
        _markersView?.Refresh();
    }

    private void AddMarker_Click(object sender, RoutedEventArgs e)
    {
        var name = ShowInputDialog("Add Marker", "Enter marker name:", "New Marker");
        if (name != null)
        {
            var marker = new MarkerListItem(name, 0, MarkerListType.Cue);
            _markers.Add(marker);
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            MarkerGrid.SelectedItem = marker;
        }
    }

    private void EditMarker_Click(object sender, RoutedEventArgs e)
    {
        if (MarkerGrid.SelectedItem is MarkerListItem marker)
        {
            EditMarker(marker);
        }
    }

    private void EditMarker(MarkerListItem marker)
    {
        var newName = ShowInputDialog("Edit Marker", "Enter marker name:", marker.Name);
        if (newName != null)
        {
            marker.Name = newName;
            MarkersChanged?.Invoke(this, EventArgs.Empty);
            _markersView?.Refresh();
        }
    }

    private string? ShowInputDialog(string title, string message, string defaultValue)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1F22"))
        };

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock
        {
            Text = message,
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BCBEC4")),
            Margin = new Thickness(0, 0, 0, 8)
        };
        Grid.SetRow(label, 0);

        var textBox = new TextBox
        {
            Text = defaultValue,
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B2D30")),
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BCBEC4")),
            BorderBrush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#393B40")),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, 16)
        };
        textBox.SelectAll();
        Grid.SetRow(textBox, 1);

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };
        Grid.SetRow(buttonPanel, 2);

        var okButton = new Button
        {
            Content = "OK",
            IsDefault = true,
            Width = 80,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4B6EAF")),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };
        okButton.Click += (s, args) => { dialog.DialogResult = true; };

        var cancelButton = new Button
        {
            Content = "Cancel",
            IsCancel = true,
            Width = 80,
            Padding = new Thickness(8, 6, 8, 6),
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3C3F41")),
            Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#BCBEC4")),
            BorderThickness = new Thickness(0)
        };

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;
        dialog.Loaded += (s, args) => textBox.Focus();

        if (dialog.ShowDialog() == true)
        {
            return textBox.Text;
        }

        return null;
    }

    private void DeleteMarker_Click(object sender, RoutedEventArgs e)
    {
        if (MarkerGrid.SelectedItem is MarkerListItem marker)
        {
            var result = MessageBox.Show(
                $"Delete marker \"{marker.Name}\"?",
                "Delete Marker",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                RemoveMarker(marker);
            }
        }
    }

    private void GoToMarker_Click(object sender, RoutedEventArgs e)
    {
        if (MarkerGrid.SelectedItem is MarkerListItem marker)
        {
            MarkerNavigated?.Invoke(this, marker);
        }
    }

    private void MarkerGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (MarkerGrid.SelectedItem is MarkerListItem marker)
        {
            MarkerNavigated?.Invoke(this, marker);
        }
    }

    private void MarkerGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Raises PropertyChanged for the specified property.
    /// </summary>
    protected void OnPropertyChanged(string propertyName)
    {
        // Using simple approach - could bind to a ViewModel if needed
    }
}
