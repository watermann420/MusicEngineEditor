using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A Learn/Wiki panel that displays categorized code examples with run, copy, and insert functionality.
/// </summary>
public partial class LearnPanel : UserControl
{
    #region Events

    /// <summary>
    /// Occurs when the user clicks the Run button on an example.
    /// </summary>
    public event EventHandler<CodeExampleEventArgs>? OnRunExample;

    /// <summary>
    /// Occurs when the user clicks the Copy button on an example.
    /// </summary>
    public event EventHandler<CodeExampleEventArgs>? OnCopyExample;

    /// <summary>
    /// Occurs when the user clicks the Insert button on an example.
    /// </summary>
    public event EventHandler<CodeExampleEventArgs>? OnInsertExample;

    #endregion

    #region Private Fields

    private readonly List<CodeExample> _allExamples = new();
    private readonly Dictionary<string, List<CodeExample>> _categorizedExamples = new();

    #endregion

    #region Constructor

    public LearnPanel()
    {
        InitializeComponent();
        InitializeExamples();
        PopulateCategories();
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeExamples()
    {
        // Getting Started Examples
        _categorizedExamples["GettingStarted"] = new List<CodeExample>
        {
            new()
            {
                Title = "Create Your First Synth",
                Description = "Create a simple synthesizer and play middle C",
                Category = "GettingStarted",
                Code = @"// Create your first synth
var synth = CreateSynth();
synth.NoteOn(60, 100); // Play middle C"
            },
            new()
            {
                Title = "Play a Simple Melody",
                Description = "Play a sequence of notes with timing",
                Category = "GettingStarted",
                Code = @"// Play a simple melody
var synth = CreateSynth();
synth.NoteOn(60, 100); // C
await Task.Delay(500);
synth.NoteOff(60);
synth.NoteOn(64, 100); // E"
            }
        };

        // MIDI Examples
        _categorizedExamples["MIDI"] = new List<CodeExample>
        {
            new()
            {
                Title = "Route MIDI Keyboard",
                Description = "Connect a MIDI device to a synthesizer",
                Category = "MIDI",
                Code = @"// Route MIDI keyboard to synth
var synth = CreateSynth();
midi.device(0).route(synth);
Print(""MIDI routed!"");"
            },
            new()
            {
                Title = "Map CC to Cutoff",
                Description = "Map a MIDI control change to filter cutoff",
                Category = "MIDI",
                Code = @"// Map CC to cutoff
var synth = CreateSynth();
midi.device(0).cc(1).to(synth, ""cutoff"");"
            }
        };

        // VST Plugins Examples
        _categorizedExamples["VST"] = new List<CodeExample>
        {
            new()
            {
                Title = "Load a VST Plugin",
                Description = "Load and route MIDI to a VST instrument",
                Category = "VST",
                Code = @"// Load a VST plugin
var vital = vst.load(""Vital"");
vital?.from(0); // Route MIDI"
            }
        };

        // Patterns & Sequencing Examples
        _categorizedExamples["Patterns"] = new List<CodeExample>
        {
            new()
            {
                Title = "Create a Drum Pattern",
                Description = "Build a basic kick and snare pattern",
                Category = "Patterns",
                Code = @"// Create a drum pattern
var synth = CreateSynth();
var pattern = CreatePattern(synth);
pattern.Note(36, 0, 0.25, 100);   // Kick
pattern.Note(38, 1, 0.25, 100);   // Snare
pattern.Note(36, 2, 0.25, 100);   // Kick
pattern.Note(38, 3, 0.25, 100);   // Snare"
            }
        };

        // Audio Examples (placeholder for future content)
        _categorizedExamples["Audio"] = new List<CodeExample>();

        // Advanced Examples (placeholder for future content)
        _categorizedExamples["Advanced"] = new List<CodeExample>();

        // Build flat list of all examples
        foreach (var category in _categorizedExamples.Values)
        {
            _allExamples.AddRange(category);
        }
    }

    private void PopulateCategories()
    {
        GettingStartedItems.ItemsSource = _categorizedExamples["GettingStarted"];
        GettingStartedExpander.Tag = _categorizedExamples["GettingStarted"].Count;

        MidiItems.ItemsSource = _categorizedExamples["MIDI"];
        MidiExpander.Tag = _categorizedExamples["MIDI"].Count;

        VstItems.ItemsSource = _categorizedExamples["VST"];
        VstExpander.Tag = _categorizedExamples["VST"].Count;

        PatternsItems.ItemsSource = _categorizedExamples["Patterns"];
        PatternsExpander.Tag = _categorizedExamples["Patterns"].Count;

        AudioItems.ItemsSource = _categorizedExamples["Audio"];
        AudioExpander.Tag = _categorizedExamples["Audio"].Count;

        AdvancedItems.ItemsSource = _categorizedExamples["Advanced"];
        AdvancedExpander.Tag = _categorizedExamples["Advanced"].Count;

        UpdateCategoryVisibility();
    }

    private void UpdateCategoryVisibility()
    {
        GettingStartedExpander.Visibility = GetVisibility(_categorizedExamples["GettingStarted"]);
        MidiExpander.Visibility = GetVisibility(_categorizedExamples["MIDI"]);
        VstExpander.Visibility = GetVisibility(_categorizedExamples["VST"]);
        PatternsExpander.Visibility = GetVisibility(_categorizedExamples["Patterns"]);
        AudioExpander.Visibility = GetVisibility(_categorizedExamples["Audio"]);
        AdvancedExpander.Visibility = GetVisibility(_categorizedExamples["Advanced"]);
    }

    private static Visibility GetVisibility(ICollection<CodeExample> examples)
    {
        return examples.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion

    #region Event Handlers

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchTextBox.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        if (string.IsNullOrEmpty(searchText))
        {
            // Reset to show all examples
            PopulateCategories();
            return;
        }

        // Filter examples based on search text
        var filteredGettingStarted = FilterExamples(_categorizedExamples["GettingStarted"], searchText);
        var filteredMidi = FilterExamples(_categorizedExamples["MIDI"], searchText);
        var filteredVst = FilterExamples(_categorizedExamples["VST"], searchText);
        var filteredPatterns = FilterExamples(_categorizedExamples["Patterns"], searchText);
        var filteredAudio = FilterExamples(_categorizedExamples["Audio"], searchText);
        var filteredAdvanced = FilterExamples(_categorizedExamples["Advanced"], searchText);

        GettingStartedItems.ItemsSource = filteredGettingStarted;
        GettingStartedExpander.Tag = filteredGettingStarted.Count;
        GettingStartedExpander.Visibility = GetVisibility(filteredGettingStarted);

        MidiItems.ItemsSource = filteredMidi;
        MidiExpander.Tag = filteredMidi.Count;
        MidiExpander.Visibility = GetVisibility(filteredMidi);

        VstItems.ItemsSource = filteredVst;
        VstExpander.Tag = filteredVst.Count;
        VstExpander.Visibility = GetVisibility(filteredVst);

        PatternsItems.ItemsSource = filteredPatterns;
        PatternsExpander.Tag = filteredPatterns.Count;
        PatternsExpander.Visibility = GetVisibility(filteredPatterns);

        AudioItems.ItemsSource = filteredAudio;
        AudioExpander.Tag = filteredAudio.Count;
        AudioExpander.Visibility = GetVisibility(filteredAudio);

        AdvancedItems.ItemsSource = filteredAdvanced;
        AdvancedExpander.Tag = filteredAdvanced.Count;
        AdvancedExpander.Visibility = GetVisibility(filteredAdvanced);
    }

    private static List<CodeExample> FilterExamples(List<CodeExample> examples, string searchText)
    {
        return examples.Where(ex =>
            ex.Title.ToLowerInvariant().Contains(searchText) ||
            ex.Description.ToLowerInvariant().Contains(searchText) ||
            ex.Code.ToLowerInvariant().Contains(searchText) ||
            ex.Category.ToLowerInvariant().Contains(searchText)
        ).ToList();
    }

    private void OnRunClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CodeExample example })
        {
            OnRunExample?.Invoke(this, new CodeExampleEventArgs(example.Code, example));
        }
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CodeExample example })
        {
            try
            {
                System.Windows.Clipboard.SetText(example.Code);
                OnCopyExample?.Invoke(this, new CodeExampleEventArgs(example.Code, example));
            }
            catch (Exception)
            {
                // Clipboard access can fail in some scenarios
            }
        }
    }

    private void OnInsertClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CodeExample example })
        {
            OnInsertExample?.Invoke(this, new CodeExampleEventArgs(example.Code, example));
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a new code example to the panel.
    /// </summary>
    /// <param name="example">The example to add.</param>
    public void AddExample(CodeExample example)
    {
        if (!_categorizedExamples.ContainsKey(example.Category))
        {
            _categorizedExamples[example.Category] = new List<CodeExample>();
        }

        _categorizedExamples[example.Category].Add(example);
        _allExamples.Add(example);
        PopulateCategories();
    }

    /// <summary>
    /// Removes a code example from the panel.
    /// </summary>
    /// <param name="example">The example to remove.</param>
    /// <returns>True if the example was removed, false otherwise.</returns>
    public bool RemoveExample(CodeExample example)
    {
        var removed = false;

        if (_categorizedExamples.ContainsKey(example.Category))
        {
            removed = _categorizedExamples[example.Category].Remove(example);
        }

        _allExamples.Remove(example);

        if (removed)
        {
            PopulateCategories();
        }

        return removed;
    }

    /// <summary>
    /// Clears all examples from the panel.
    /// </summary>
    public void ClearExamples()
    {
        foreach (var category in _categorizedExamples.Values)
        {
            category.Clear();
        }

        _allExamples.Clear();
        PopulateCategories();
    }

    /// <summary>
    /// Gets all examples in the panel.
    /// </summary>
    /// <returns>A read-only list of all examples.</returns>
    public IReadOnlyList<CodeExample> GetAllExamples() => _allExamples.AsReadOnly();

    /// <summary>
    /// Gets examples for a specific category.
    /// </summary>
    /// <param name="category">The category name.</param>
    /// <returns>A list of examples in the category, or an empty list if the category doesn't exist.</returns>
    public IReadOnlyList<CodeExample> GetExamplesByCategory(string category)
    {
        return _categorizedExamples.TryGetValue(category, out var examples)
            ? examples.AsReadOnly()
            : new List<CodeExample>().AsReadOnly();
    }

    /// <summary>
    /// Expands all categories.
    /// </summary>
    public void ExpandAll()
    {
        GettingStartedExpander.IsExpanded = true;
        MidiExpander.IsExpanded = true;
        VstExpander.IsExpanded = true;
        PatternsExpander.IsExpanded = true;
        AudioExpander.IsExpanded = true;
        AdvancedExpander.IsExpanded = true;
    }

    /// <summary>
    /// Collapses all categories.
    /// </summary>
    public void CollapseAll()
    {
        GettingStartedExpander.IsExpanded = false;
        MidiExpander.IsExpanded = false;
        VstExpander.IsExpanded = false;
        PatternsExpander.IsExpanded = false;
        AudioExpander.IsExpanded = false;
        AdvancedExpander.IsExpanded = false;
    }

    /// <summary>
    /// Clears the search filter.
    /// </summary>
    public void ClearSearch()
    {
        SearchTextBox.Text = string.Empty;
    }

    #endregion
}

#region Data Models

/// <summary>
/// Represents a code example in the Learn panel.
/// </summary>
public class CodeExample
{
    /// <summary>
    /// Gets or sets the title of the example.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description of the example.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the code snippet.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the category of the example.
    /// Valid categories: GettingStarted, MIDI, VST, Patterns, Audio, Advanced
    /// </summary>
    public string Category { get; set; } = "GettingStarted";

    /// <summary>
    /// Gets or sets optional tags for searching.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Gets or sets the original code for reset functionality.
    /// </summary>
    public string OriginalCode { get; set; } = string.Empty;

    /// <summary>
    /// Resets the code to its original state.
    /// </summary>
    public void Reset()
    {
        if (!string.IsNullOrEmpty(OriginalCode))
        {
            Code = OriginalCode;
        }
    }
}

/// <summary>
/// Event arguments for code example events.
/// </summary>
public class CodeExampleEventArgs : EventArgs
{
    /// <summary>
    /// Gets the code from the example.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the full example object.
    /// </summary>
    public CodeExample Example { get; }

    /// <summary>
    /// Creates a new instance of CodeExampleEventArgs.
    /// </summary>
    /// <param name="code">The code snippet.</param>
    /// <param name="example">The full example object.</param>
    public CodeExampleEventArgs(string code, CodeExample example)
    {
        Code = code;
        Example = example;
    }
}

#endregion
