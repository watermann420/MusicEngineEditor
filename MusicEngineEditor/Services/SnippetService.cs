using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing code snippets with JSON persistence
/// </summary>
public class SnippetService : ISnippetService
{
    private static readonly string SnippetsFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MusicEngineEditor");

    private static readonly string SnippetsFilePath = Path.Combine(SnippetsFolder, "snippets.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private SnippetCollection _snippetCollection = new();

    /// <summary>
    /// Gets all loaded snippets
    /// </summary>
    public IReadOnlyList<CodeSnippet> Snippets => _snippetCollection.Snippets.AsReadOnly();

    /// <summary>
    /// Loads snippets from the JSON file, creating defaults if none exist
    /// </summary>
    public async Task<IReadOnlyList<CodeSnippet>> LoadSnippetsAsync()
    {
        try
        {
            if (File.Exists(SnippetsFilePath))
            {
                var json = await File.ReadAllTextAsync(SnippetsFilePath);
                _snippetCollection = JsonSerializer.Deserialize<SnippetCollection>(json, JsonOptions)
                    ?? new SnippetCollection();

                // Ensure built-in snippets are present
                EnsureBuiltInSnippets();
            }
            else
            {
                // Initialize with default snippets
                _snippetCollection = new SnippetCollection
                {
                    Snippets = GetDefaultSnippets()
                };
                await SaveSnippetsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load snippets: {ex.Message}");
            _snippetCollection = new SnippetCollection
            {
                Snippets = GetDefaultSnippets()
            };
        }

        return _snippetCollection.Snippets.AsReadOnly();
    }

    /// <summary>
    /// Saves all snippets to the JSON file
    /// </summary>
    public async Task SaveSnippetsAsync()
    {
        try
        {
            Directory.CreateDirectory(SnippetsFolder);
            var json = JsonSerializer.Serialize(_snippetCollection, JsonOptions);
            await File.WriteAllTextAsync(SnippetsFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save snippets: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Gets all snippets in a specific category
    /// </summary>
    public IReadOnlyList<CodeSnippet> GetSnippetsByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return _snippetCollection.Snippets.AsReadOnly();

        return _snippetCollection.Snippets
            .Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets a snippet by its shortcut trigger
    /// </summary>
    public CodeSnippet? GetSnippetByShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return null;

        return _snippetCollection.Snippets
            .FirstOrDefault(s => s.Shortcut.Equals(shortcut, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Gets a snippet by its name
    /// </summary>
    public CodeSnippet? GetSnippetByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return _snippetCollection.Snippets
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Adds a new snippet
    /// </summary>
    public async Task AddSnippetAsync(CodeSnippet snippet)
    {
        if (snippet == null)
            throw new ArgumentNullException(nameof(snippet));

        if (string.IsNullOrWhiteSpace(snippet.Name))
            throw new ArgumentException("Snippet name cannot be empty", nameof(snippet));

        // Check for duplicate name
        if (_snippetCollection.Snippets.Any(s => s.Name.Equals(snippet.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A snippet with name '{snippet.Name}' already exists");

        // Check for duplicate shortcut if provided
        if (!string.IsNullOrWhiteSpace(snippet.Shortcut) &&
            _snippetCollection.Snippets.Any(s => s.Shortcut.Equals(snippet.Shortcut, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A snippet with shortcut '{snippet.Shortcut}' already exists");

        snippet.CreatedDate = DateTime.UtcNow;
        snippet.IsBuiltIn = false;
        _snippetCollection.Snippets.Add(snippet);
        await SaveSnippetsAsync();
    }

    /// <summary>
    /// Updates an existing snippet
    /// </summary>
    public async Task UpdateSnippetAsync(CodeSnippet snippet)
    {
        if (snippet == null)
            throw new ArgumentNullException(nameof(snippet));

        var existing = _snippetCollection.Snippets
            .FirstOrDefault(s => s.Name.Equals(snippet.Name, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            throw new InvalidOperationException($"Snippet '{snippet.Name}' not found");

        if (existing.IsBuiltIn)
            throw new InvalidOperationException("Cannot modify built-in snippets");

        // Check for duplicate shortcut with other snippets
        if (!string.IsNullOrWhiteSpace(snippet.Shortcut) &&
            _snippetCollection.Snippets.Any(s =>
                !s.Name.Equals(snippet.Name, StringComparison.OrdinalIgnoreCase) &&
                s.Shortcut.Equals(snippet.Shortcut, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A snippet with shortcut '{snippet.Shortcut}' already exists");

        var index = _snippetCollection.Snippets.IndexOf(existing);
        _snippetCollection.Snippets[index] = snippet;
        await SaveSnippetsAsync();
    }

    /// <summary>
    /// Deletes a snippet by name
    /// </summary>
    public async Task<bool> DeleteSnippetAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var snippet = _snippetCollection.Snippets
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (snippet == null)
            return false;

        if (snippet.IsBuiltIn)
            throw new InvalidOperationException("Cannot delete built-in snippets");

        _snippetCollection.Snippets.Remove(snippet);
        await SaveSnippetsAsync();
        return true;
    }

    /// <summary>
    /// Searches snippets by name, description, or tags
    /// </summary>
    public IReadOnlyList<CodeSnippet> SearchSnippets(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return _snippetCollection.Snippets.AsReadOnly();

        var term = searchTerm.ToLowerInvariant();
        return _snippetCollection.Snippets
            .Where(s =>
                s.Name.ToLowerInvariant().Contains(term) ||
                s.Description.ToLowerInvariant().Contains(term) ||
                s.Shortcut.ToLowerInvariant().Contains(term) ||
                s.Tags.Any(t => t.ToLowerInvariant().Contains(term)))
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Gets all unique categories
    /// </summary>
    public IReadOnlyList<string> GetCategories()
    {
        return _snippetCollection.Snippets
            .Select(s => s.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Processes snippet code by replacing placeholders
    /// Returns the processed code and the cursor position
    /// </summary>
    public (string ProcessedCode, int CursorPosition, List<TabStop> TabStops) ProcessSnippetCode(string code)
    {
        var tabStops = new List<TabStop>();
        var processedCode = code;
        var cursorPosition = -1;

        // Find and process tab stops ($1$, $2$, etc.)
        for (int i = 1; i <= 9; i++)
        {
            var placeholder = $"${i}$";
            var index = processedCode.IndexOf(placeholder, StringComparison.Ordinal);
            if (index >= 0)
            {
                tabStops.Add(new TabStop { Index = i, Position = index });
                processedCode = processedCode.Replace(placeholder, "");
            }
        }

        // Find cursor position ($CURSOR$)
        const string cursorPlaceholder = "$CURSOR$";
        cursorPosition = processedCode.IndexOf(cursorPlaceholder, StringComparison.Ordinal);
        if (cursorPosition >= 0)
        {
            processedCode = processedCode.Replace(cursorPlaceholder, "");
        }
        else if (tabStops.Count > 0)
        {
            // If no cursor placeholder, use first tab stop position
            cursorPosition = tabStops[0].Position;
        }
        else
        {
            // Default to end of code
            cursorPosition = processedCode.Length;
        }

        // Recalculate tab stop positions after removing placeholders
        tabStops = RecalculateTabStopPositions(code, processedCode);

        return (processedCode, cursorPosition, tabStops);
    }

    private List<TabStop> RecalculateTabStopPositions(string originalCode, string processedCode)
    {
        var tabStops = new List<TabStop>();

        // Process in order
        for (int i = 1; i <= 9; i++)
        {
            var placeholder = $"${i}$";
            var originalIndex = originalCode.IndexOf(placeholder, StringComparison.Ordinal);
            if (originalIndex >= 0)
            {
                // Calculate actual position accounting for removed placeholders
                var actualPosition = originalIndex;
                for (int j = 1; j < i; j++)
                {
                    var prevPlaceholder = $"${j}$";
                    var prevIndex = originalCode.IndexOf(prevPlaceholder, StringComparison.Ordinal);
                    if (prevIndex >= 0 && prevIndex < originalIndex)
                    {
                        actualPosition -= prevPlaceholder.Length;
                    }
                }

                // Account for $CURSOR$ if it appears before this position
                const string cursorPlaceholder = "$CURSOR$";
                var cursorIndex = originalCode.IndexOf(cursorPlaceholder, StringComparison.Ordinal);
                if (cursorIndex >= 0 && cursorIndex < originalIndex)
                {
                    actualPosition -= cursorPlaceholder.Length;
                }

                tabStops.Add(new TabStop { Index = i, Position = actualPosition });
            }
        }

        return tabStops.OrderBy(t => t.Index).ToList();
    }

    /// <summary>
    /// Ensures all built-in snippets are present
    /// </summary>
    private void EnsureBuiltInSnippets()
    {
        var defaults = GetDefaultSnippets();
        foreach (var defaultSnippet in defaults)
        {
            if (!_snippetCollection.Snippets.Any(s =>
                s.Shortcut.Equals(defaultSnippet.Shortcut, StringComparison.OrdinalIgnoreCase) && s.IsBuiltIn))
            {
                _snippetCollection.Snippets.Add(defaultSnippet);
            }
        }
    }

    /// <summary>
    /// Creates the default built-in snippets
    /// </summary>
    private static List<CodeSnippet> GetDefaultSnippets()
    {
        return new List<CodeSnippet>
        {
            // Basic SimpleSynth setup
            new CodeSnippet
            {
                Name = "Basic SimpleSynth",
                Description = "Creates a basic SimpleSynth with configurable waveform and frequency",
                Shortcut = "syn",
                Category = "Synths",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "synth", "simple", "basic", "oscillator" },
                Code = @"// Create a basic synth
var $1$synth = new SimpleSynth
{
    Waveform = Waveform.$2$Sine,
    Frequency = $3$440,
    Amplitude = $4$0.5
};

// Connect to output
engine.Connect($1$synth, engine.Output);
$CURSOR$"
            },

            // PolySynth with envelope
            new CodeSnippet
            {
                Name = "PolySynth with Envelope",
                Description = "Creates a polyphonic synthesizer with ADSR envelope",
                Shortcut = "poly",
                Category = "Synths",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "synth", "polyphonic", "envelope", "adsr" },
                Code = @"// Create a polyphonic synth with envelope
var $1$polySynth = new PolySynth
{
    Voices = $2$8,
    Waveform = Waveform.$3$Sawtooth,

    // ADSR Envelope
    Attack = $4$0.01,
    Decay = $5$0.1,
    Sustain = $6$0.7,
    Release = $7$0.3
};

// Connect to output
engine.Connect($1$polySynth, engine.Output);
$CURSOR$"
            },

            // Pattern with notes
            new CodeSnippet
            {
                Name = "Pattern with Notes",
                Description = "Creates a pattern with a sequence of notes",
                Shortcut = "pat",
                Category = "Patterns",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "pattern", "sequence", "notes", "melody" },
                Code = @"// Create a note pattern
var $1$pattern = new Pattern
{
    Name = ""$2$MyPattern"",
    Length = $3$4, // bars
    TimeSignature = new TimeSignature($4$4, $5$4)
};

// Add notes to the pattern
$1$pattern.AddNote(Note.C4, 0, 0.5);
$1$pattern.AddNote(Note.E4, 0.5, 0.5);
$1$pattern.AddNote(Note.G4, 1, 0.5);
$1$pattern.AddNote(Note.C5, 1.5, 0.5);
$CURSOR$

// Connect pattern to synth
engine.Connect($1$pattern, synth);"
            },

            // Arpeggiator setup
            new CodeSnippet
            {
                Name = "Arpeggiator Setup",
                Description = "Creates an arpeggiator with configurable parameters",
                Shortcut = "arp",
                Category = "Patterns",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "arpeggiator", "arp", "sequence", "pattern" },
                Code = @"// Create an arpeggiator
var $1$arp = new Arpeggiator
{
    Mode = ArpMode.$2$Up,
    Rate = $3$8, // notes per beat
    Octaves = $4$2,
    Gate = $5$0.8
};

// Define chord to arpeggiate
$1$arp.SetChord(new[] { Note.C4, Note.E4, Note.G4, Note.B4 });

// Connect to synth
engine.Connect($1$arp, synth);
$CURSOR$"
            },

            // Effect chain template
            new CodeSnippet
            {
                Name = "Effect Chain",
                Description = "Creates a chain of audio effects",
                Shortcut = "fx",
                Category = "Effects",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "effects", "fx", "chain", "reverb", "delay" },
                Code = @"// Create an effect chain
var $1$reverb = new Reverb
{
    RoomSize = $2$0.7,
    Damping = $3$0.5,
    WetMix = $4$0.3
};

var $5$delay = new Delay
{
    Time = $6$0.25, // seconds
    Feedback = $7$0.4,
    WetMix = $8$0.3
};

// Chain effects: source -> delay -> reverb -> output
engine.Connect(source, $5$delay);
engine.Connect($5$delay, $1$reverb);
engine.Connect($1$reverb, engine.Output);
$CURSOR$"
            },

            // MIDI routing setup
            new CodeSnippet
            {
                Name = "MIDI Routing",
                Description = "Sets up MIDI input routing to a synthesizer",
                Shortcut = "midi",
                Category = "MIDI",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "midi", "input", "controller", "routing" },
                Code = @"// Set up MIDI input
var $1$midiIn = new MidiInput
{
    DeviceName = ""$2$Default"",
    Channel = $3$1 // 1-16, or 0 for omni
};

// Map MIDI CC to parameters
$1$midiIn.MapCC($4$1, synth, ""Cutoff""); // Mod wheel -> Cutoff
$1$midiIn.MapCC($5$74, synth, ""Resonance""); // CC74 -> Resonance

// Connect MIDI to synth for note input
engine.Connect($1$midiIn, synth);
$CURSOR$

// Enable MIDI input
$1$midiIn.Enable();"
            },

            // DrumKit pattern
            new CodeSnippet
            {
                Name = "DrumKit Pattern",
                Description = "Creates a drum kit with a basic beat pattern",
                Shortcut = "drum",
                Category = "Patterns",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "drums", "kit", "beat", "percussion", "rhythm" },
                Code = @"// Create a drum kit
var $1$drums = new DrumKit
{
    Kit = ""$2$Acoustic""
};

// Create a drum pattern (16 steps)
var $3$drumPattern = new DrumPattern
{
    Name = ""$4$BasicBeat"",
    Steps = 16,
    BPM = $5$120
};

// Kick on 1 and 9 (1-indexed)
$3$drumPattern.SetHit(DrumSound.Kick, 1);
$3$drumPattern.SetHit(DrumSound.Kick, 9);

// Snare on 5 and 13
$3$drumPattern.SetHit(DrumSound.Snare, 5);
$3$drumPattern.SetHit(DrumSound.Snare, 13);

// Hi-hat on every other step
for (int i = 1; i <= 16; i += 2)
{
    $3$drumPattern.SetHit(DrumSound.HiHat, i);
}
$CURSOR$

// Connect pattern to kit
engine.Connect($3$drumPattern, $1$drums);
engine.Connect($1$drums, engine.Output);"
            },

            // Basic loop structure
            new CodeSnippet
            {
                Name = "Basic Loop",
                Description = "Creates a basic loop structure with start/stop control",
                Shortcut = "loop",
                Category = "Patterns",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "loop", "transport", "playback", "control" },
                Code = @"// Create a loop region
var $1$loop = new Loop
{
    Name = ""$2$MainLoop"",
    StartBar = $3$1,
    EndBar = $4$5,
    BPM = $5$120,
    Enabled = true
};

// Set up transport
engine.Transport.Loop = $1$loop;
engine.Transport.BPM = $5$120;

// Start playback
engine.Transport.Play();
$CURSOR$

// To stop: engine.Transport.Stop();
// To pause: engine.Transport.Pause();"
            },

            // Filter setup
            new CodeSnippet
            {
                Name = "Filter Setup",
                Description = "Creates a configurable audio filter",
                Shortcut = "filt",
                Category = "Effects",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "filter", "lowpass", "highpass", "bandpass" },
                Code = @"// Create a filter
var $1$filter = new Filter
{
    Type = FilterType.$2$LowPass,
    Cutoff = $3$1000, // Hz
    Resonance = $4$0.5,
    Slope = $5$24 // dB/octave (12 or 24)
};

// Connect: source -> filter -> output
engine.Connect(source, $1$filter);
engine.Connect($1$filter, engine.Output);
$CURSOR$"
            },

            // LFO modulation
            new CodeSnippet
            {
                Name = "LFO Modulation",
                Description = "Creates an LFO for parameter modulation",
                Shortcut = "lfo",
                Category = "Synths",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "lfo", "modulation", "wobble", "vibrato" },
                Code = @"// Create an LFO
var $1$lfo = new LFO
{
    Waveform = Waveform.$2$Sine,
    Rate = $3$4, // Hz
    Depth = $4$0.5,
    Phase = $5$0
};

// Modulate synth parameter
$1$lfo.Connect(synth, ""$6$Frequency"", ModulationType.$7$Multiply);
$CURSOR$

// Start LFO
$1$lfo.Start();"
            },

            // Audio sample player
            new CodeSnippet
            {
                Name = "Sample Player",
                Description = "Creates a sample player for audio files",
                Shortcut = "samp",
                Category = "Synths",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "sample", "player", "audio", "wav" },
                Code = @"// Create a sample player
var $1$sampler = new SamplePlayer
{
    FilePath = ""$2$samples/sample.wav"",
    Loop = $3$false,
    LoopStart = $4$0,
    LoopEnd = $5$-1, // -1 for end of sample
    Pitch = $6$1.0,
    Volume = $7$0.8
};

// Connect to output
engine.Connect($1$sampler, engine.Output);
$CURSOR$

// Trigger playback
$1$sampler.Play();"
            },

            // Mixer channel
            new CodeSnippet
            {
                Name = "Mixer Channel",
                Description = "Creates a mixer channel with volume and pan",
                Shortcut = "mix",
                Category = "Effects",
                Author = "MusicEngine",
                IsBuiltIn = true,
                Tags = new List<string> { "mixer", "channel", "volume", "pan" },
                Code = @"// Create a mixer
var $1$mixer = new Mixer
{
    Channels = $2$8
};

// Configure a channel
$1$mixer.SetChannelVolume($3$0, $4$0.8);
$1$mixer.SetChannelPan($3$0, $5$0); // -1 (left) to 1 (right)
$1$mixer.SetChannelMute($3$0, false);
$1$mixer.SetChannelSolo($3$0, false);

// Connect source to mixer channel
engine.Connect(source, $1$mixer.Channel($3$0));

// Connect mixer to output
engine.Connect($1$mixer, engine.Output);
$CURSOR$"
            }
        };
    }
}

/// <summary>
/// Represents a tab stop position in a snippet
/// </summary>
public class TabStop
{
    /// <summary>
    /// The tab stop index (1-9)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The position in the processed code
    /// </summary>
    public int Position { get; set; }
}
