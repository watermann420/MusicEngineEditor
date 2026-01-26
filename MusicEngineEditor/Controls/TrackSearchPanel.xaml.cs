//MusicEngineEditor - Track Search Panel
// copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Controls;

/// <summary>
/// Represents a track type for display.
/// </summary>
public enum SearchTrackType
{
    /// <summary>Audio track.</summary>
    Audio,
    /// <summary>MIDI track.</summary>
    Midi,
    /// <summary>Instrument track.</summary>
    Instrument,
    /// <summary>Bus/Group track.</summary>
    Bus,
    /// <summary>Aux/Return track.</summary>
    Aux,
    /// <summary>Master track.</summary>
    Master
}

/// <summary>
/// Represents a searchable track item.
/// </summary>
public partial class SearchableTrack : ObservableObject
{
    /// <summary>
    /// Gets the track index.
    /// </summary>
    public int Index { get; init; }

    /// <summary>
    /// Gets or sets the track name.
    /// </summary>
    [ObservableProperty]
    private string _name = "";

    /// <summary>
    /// Gets or sets the track color (hex).
    /// </summary>
    [ObservableProperty]
    private string _color = "#4A9EFF";

    /// <summary>
    /// Gets or sets the track type.
    /// </summary>
    [ObservableProperty]
    private SearchTrackType _type = SearchTrackType.Audio;

    /// <summary>
    /// Gets or sets whether the track is selected in the results.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the type display string.
    /// </summary>
    public string TypeDisplay => Type.ToString();

    /// <summary>
    /// Creates a new searchable track.
    /// </summary>
    public SearchableTrack() { }

    /// <summary>
    /// Creates a new searchable track with specified values.
    /// </summary>
    public SearchableTrack(int index, string name, string color, SearchTrackType type)
    {
        Index = index;
        Name = name;
        Color = color;
        Type = type;
    }
}

/// <summary>
/// Search panel for finding and navigating to tracks.
/// </summary>
public partial class TrackSearchPanel : UserControl
{
    private readonly ObservableCollection<SearchableTrack> _allTracks = [];
    private readonly ObservableCollection<SearchableTrack> _filteredTracks = [];
    private int _selectedIndex = -1;

    /// <summary>
    /// Gets the collection of all tracks.
    /// </summary>
    public ObservableCollection<SearchableTrack> AllTracks => _allTracks;

    /// <summary>
    /// Event raised when a track is selected.
    /// </summary>
    public event EventHandler<SearchableTrack>? TrackSelected;

    /// <summary>
    /// Event raised when the user requests to navigate to a track.
    /// </summary>
    public event EventHandler<SearchableTrack>? TrackNavigateRequested;

    /// <summary>
    /// Creates a new TrackSearchPanel.
    /// </summary>
    public TrackSearchPanel()
    {
        InitializeComponent();
        ResultsList.ItemsSource = _filteredTracks;

        InitializeSampleTracks();
    }

    /// <summary>
    /// Initializes sample tracks for demonstration.
    /// </summary>
    private void InitializeSampleTracks()
    {
        _allTracks.Add(new SearchableTrack(0, "Kick", "#FF5555", SearchTrackType.Audio));
        _allTracks.Add(new SearchableTrack(1, "Snare", "#55FF55", SearchTrackType.Audio));
        _allTracks.Add(new SearchableTrack(2, "Hi-Hat", "#5555FF", SearchTrackType.Audio));
        _allTracks.Add(new SearchableTrack(3, "Bass", "#FF9500", SearchTrackType.Midi));
        _allTracks.Add(new SearchableTrack(4, "Lead Synth", "#FF55FF", SearchTrackType.Instrument));
        _allTracks.Add(new SearchableTrack(5, "Pad", "#55FFFF", SearchTrackType.Instrument));
        _allTracks.Add(new SearchableTrack(6, "FX", "#FFFF55", SearchTrackType.Audio));
        _allTracks.Add(new SearchableTrack(7, "Vocals", "#AA55FF", SearchTrackType.Audio));
        _allTracks.Add(new SearchableTrack(8, "Drums Bus", "#FF5555", SearchTrackType.Bus));
        _allTracks.Add(new SearchableTrack(9, "Synth Bus", "#55FFFF", SearchTrackType.Bus));
        _allTracks.Add(new SearchableTrack(10, "Reverb", "#3B82F6", SearchTrackType.Aux));
        _allTracks.Add(new SearchableTrack(11, "Delay", "#8B5CF6", SearchTrackType.Aux));
    }

    /// <summary>
    /// Sets the tracks to search.
    /// </summary>
    /// <param name="tracks">The tracks collection.</param>
    public void SetTracks(ObservableCollection<SearchableTrack> tracks)
    {
        _allTracks.Clear();
        foreach (var track in tracks)
        {
            _allTracks.Add(track);
        }
    }

    /// <summary>
    /// Adds a track to the search list.
    /// </summary>
    /// <param name="track">The track to add.</param>
    public void AddTrack(SearchableTrack track)
    {
        _allTracks.Add(track);
    }

    /// <summary>
    /// Clears all tracks.
    /// </summary>
    public void ClearTracks()
    {
        _allTracks.Clear();
        _filteredTracks.Clear();
    }

    /// <summary>
    /// Focuses the search input.
    /// </summary>
    public void FocusSearch()
    {
        SearchInput.Focus();
    }

    private void FilterTracks(string query)
    {
        _filteredTracks.Clear();
        _selectedIndex = -1;

        if (string.IsNullOrWhiteSpace(query))
        {
            ResultsPopup.IsOpen = false;
            return;
        }

        var matches = _allTracks.Where(t =>
            t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            t.TypeDisplay.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            t.Color.Contains(query, StringComparison.OrdinalIgnoreCase)
        ).ToList();

        foreach (var track in matches)
        {
            track.IsSelected = false;
            _filteredTracks.Add(track);
        }

        ResultsHeader.Text = $"{_filteredTracks.Count} track{(_filteredTracks.Count != 1 ? "s" : "")} found";
        EmptyState.Visibility = _filteredTracks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ResultsList.Visibility = _filteredTracks.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        ResultsPopup.IsOpen = true;

        // Select first item if available
        if (_filteredTracks.Count > 0)
        {
            _selectedIndex = 0;
            _filteredTracks[0].IsSelected = true;
        }
    }

    private void SelectTrack(SearchableTrack track)
    {
        TrackSelected?.Invoke(this, track);
    }

    private void NavigateToTrack(SearchableTrack track)
    {
        TrackNavigateRequested?.Invoke(this, track);
        ResultsPopup.IsOpen = false;
        SearchInput.Text = "";
    }

    private void MoveSelection(int direction)
    {
        if (_filteredTracks.Count == 0)
            return;

        // Clear current selection
        if (_selectedIndex >= 0 && _selectedIndex < _filteredTracks.Count)
        {
            _filteredTracks[_selectedIndex].IsSelected = false;
        }

        // Update index
        _selectedIndex += direction;
        if (_selectedIndex < 0)
            _selectedIndex = _filteredTracks.Count - 1;
        else if (_selectedIndex >= _filteredTracks.Count)
            _selectedIndex = 0;

        // Set new selection
        _filteredTracks[_selectedIndex].IsSelected = true;
        ResultsList.ScrollIntoView(_filteredTracks[_selectedIndex]);
    }

    private void ConfirmSelection()
    {
        if (_selectedIndex >= 0 && _selectedIndex < _filteredTracks.Count)
        {
            NavigateToTrack(_filteredTracks[_selectedIndex]);
        }
    }

    #region Event Handlers

    private void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var hasText = !string.IsNullOrEmpty(SearchInput.Text);
        PlaceholderText.Visibility = hasText ? Visibility.Collapsed : Visibility.Visible;
        ClearButton.Visibility = hasText ? Visibility.Visible : Visibility.Collapsed;

        FilterTracks(SearchInput.Text);
    }

    private void SearchInput_GotFocus(object sender, RoutedEventArgs e)
    {
        SearchBorder.BorderBrush = (System.Windows.Media.SolidColorBrush)FindResource("SearchFocusBorderBrush");

        if (!string.IsNullOrEmpty(SearchInput.Text))
        {
            FilterTracks(SearchInput.Text);
        }
    }

    private void SearchInput_LostFocus(object sender, RoutedEventArgs e)
    {
        SearchBorder.BorderBrush = (System.Windows.Media.SolidColorBrush)FindResource("SearchBorderBrush");

        // Delay closing popup to allow click on results
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            if (!ResultsList.IsMouseOver && !SearchInput.IsFocused)
            {
                ResultsPopup.IsOpen = false;
            }
        });
    }

    private void SearchInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                MoveSelection(1);
                e.Handled = true;
                break;

            case Key.Up:
                MoveSelection(-1);
                e.Handled = true;
                break;

            case Key.Enter:
                ConfirmSelection();
                e.Handled = true;
                break;

            case Key.Escape:
                ResultsPopup.IsOpen = false;
                SearchInput.Text = "";
                e.Handled = true;
                break;
        }
    }

    private void UserControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ResultsPopup.IsOpen = false;
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        SearchInput.Text = "";
        SearchInput.Focus();
    }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchableTrack track)
        {
            SelectTrack(track);
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ResultsList.SelectedItem is SearchableTrack track)
        {
            NavigateToTrack(track);
        }
    }

    #endregion
}
