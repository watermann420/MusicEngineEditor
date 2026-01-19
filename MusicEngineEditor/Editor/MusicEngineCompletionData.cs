using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Represents the type of completion item for icon display
/// </summary>
public enum CompletionItemType
{
    Keyword,
    Class,
    Method,
    Property,
    Variable,
    Field,
    Event,
    Snippet,
    Namespace,
    Interface,
    Enum,
    Constant
}

/// <summary>
/// Enhanced completion data for MusicEngine API with rich tooltips and icons
/// </summary>
public class MusicEngineCompletionData : ICompletionData
{
    private readonly string _signature;
    private readonly string _documentation;
    private readonly string? _insertionText;
    private readonly int _cursorOffset;

    /// <summary>
    /// Creates a new completion item
    /// </summary>
    /// <param name="text">The text to display and insert</param>
    /// <param name="itemType">The type of item (for icon)</param>
    /// <param name="signature">The method/property signature</param>
    /// <param name="documentation">Detailed documentation</param>
    /// <param name="insertionText">Optional custom text to insert (if different from display)</param>
    /// <param name="cursorOffset">Offset to move cursor after insertion (negative = move back)</param>
    public MusicEngineCompletionData(
        string text,
        CompletionItemType itemType,
        string signature = "",
        string documentation = "",
        string? insertionText = null,
        int cursorOffset = 0)
    {
        Text = text;
        ItemType = itemType;
        _signature = signature;
        _documentation = documentation;
        _insertionText = insertionText;
        _cursorOffset = cursorOffset;
        Priority = GetPriority(itemType);
    }

    public string Text { get; }
    public CompletionItemType ItemType { get; }
    public double Priority { get; set; }

    /// <summary>
    /// The icon displayed in the completion list
    /// </summary>
    public ImageSource? Image => null; // We use text-based icons in Content instead

    /// <summary>
    /// The content displayed in the completion list (includes icon)
    /// </summary>
    public object Content
    {
        get
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            // Icon based on type
            var icon = new TextBlock
            {
                Text = GetIcon(),
                Foreground = GetIconBrush(),
                FontFamily = new FontFamily("Segoe UI Symbol, Segoe UI"),
                FontSize = 14,
                Width = 20,
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(icon);

            // Text
            var text = new TextBlock
            {
                Text = Text,
                Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(text);

            // Type hint (for methods, show return type or param hint)
            if (!string.IsNullOrEmpty(_signature) && ItemType == CompletionItemType.Method)
            {
                var hint = ExtractReturnTypeHint(_signature);
                if (!string.IsNullOrEmpty(hint))
                {
                    var hintBlock = new TextBlock
                    {
                        Text = $"  {hint}",
                        Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)),
                        FontStyle = FontStyles.Italic,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    panel.Children.Add(hintBlock);
                }
            }

            return panel;
        }
    }

    /// <summary>
    /// The tooltip/description shown when the item is selected
    /// </summary>
    public object Description
    {
        get
        {
            var panel = new StackPanel { MaxWidth = 450 };

            // Signature header
            if (!string.IsNullOrEmpty(_signature))
            {
                var signatureBlock = new TextBlock
                {
                    FontFamily = new FontFamily("JetBrains Mono, Consolas, Courier New"),
                    FontSize = 12,
                    Margin = new Thickness(0, 0, 0, 8),
                    TextWrapping = TextWrapping.Wrap
                };

                // Color-code the signature
                FormatSignature(signatureBlock, _signature);
                panel.Children.Add(signatureBlock);

                // Separator
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
                    Margin = new Thickness(0, 0, 0, 8)
                });
            }

            // Documentation
            if (!string.IsNullOrEmpty(_documentation))
            {
                var docBlock = new TextBlock
                {
                    Text = _documentation,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4))
                };
                panel.Children.Add(docBlock);
            }

            return panel;
        }
    }

    /// <summary>
    /// Completes the text in the editor
    /// </summary>
    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        var textToInsert = _insertionText ?? Text;
        textArea.Document.Replace(completionSegment, textToInsert);

        // Adjust cursor position if needed
        if (_cursorOffset != 0)
        {
            textArea.Caret.Offset += _cursorOffset;
        }
    }

    private string GetIcon()
    {
        return ItemType switch
        {
            CompletionItemType.Keyword => "\u2666",      // Diamond for keywords
            CompletionItemType.Class => "C",             // C for class
            CompletionItemType.Method => "M",            // M for method
            CompletionItemType.Property => "P",          // P for property
            CompletionItemType.Variable => "V",          // V for variable
            CompletionItemType.Field => "F",             // F for field
            CompletionItemType.Event => "E",             // E for event
            CompletionItemType.Snippet => "\u2630",      // Trigram for snippet
            CompletionItemType.Namespace => "N",         // N for namespace
            CompletionItemType.Interface => "I",         // I for interface
            CompletionItemType.Enum => "\u2261",         // Triple bar for enum
            CompletionItemType.Constant => "\u03C0",     // Pi for constant
            _ => "\u2022"                                // Bullet default
        };
    }

    private Brush GetIconBrush()
    {
        return ItemType switch
        {
            CompletionItemType.Keyword => new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)),  // Blue
            CompletionItemType.Class => new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),    // Teal
            CompletionItemType.Method => new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA)),   // Yellow
            CompletionItemType.Property => new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)), // Light blue
            CompletionItemType.Variable => new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)), // Light blue
            CompletionItemType.Field => new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)),    // Light blue
            CompletionItemType.Event => new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xAA)),    // Yellow
            CompletionItemType.Snippet => new SolidColorBrush(Color.FromRgb(0xC5, 0x86, 0xC0)),  // Purple
            CompletionItemType.Namespace => new SolidColorBrush(Color.FromRgb(0xD7, 0xBA, 0x7D)),// Orange
            CompletionItemType.Interface => new SolidColorBrush(Color.FromRgb(0xB8, 0xD7, 0xA3)),// Green
            CompletionItemType.Enum => new SolidColorBrush(Color.FromRgb(0xB8, 0xD7, 0xA3)),     // Green
            CompletionItemType.Constant => new SolidColorBrush(Color.FromRgb(0xB5, 0xCE, 0xA8)), // Light green
            _ => new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4))
        };
    }

    private static double GetPriority(CompletionItemType itemType)
    {
        // Lower priority = shown first
        return itemType switch
        {
            CompletionItemType.Snippet => 0,
            CompletionItemType.Method => 1,
            CompletionItemType.Property => 2,
            CompletionItemType.Class => 3,
            CompletionItemType.Variable => 4,
            CompletionItemType.Keyword => 5,
            _ => 10
        };
    }

    private static string ExtractReturnTypeHint(string signature)
    {
        // Try to extract return type from signature like "void Method()" or "int Property"
        var trimmed = signature.Trim();
        var spaceIndex = trimmed.IndexOf(' ');
        if (spaceIndex > 0)
        {
            var firstWord = trimmed.Substring(0, spaceIndex);
            if (firstWord != "void" && !firstWord.Contains("("))
            {
                return $": {firstWord}";
            }
        }
        return string.Empty;
    }

    private static void FormatSignature(TextBlock block, string signature)
    {
        // Simple syntax highlighting for signatures
        var keywords = new[] { "void", "int", "double", "float", "string", "bool", "var", "object", "dynamic", "Action", "Task", "async" };
        var parts = signature.Split(' ', '(', ')', ',', '<', '>', '{', '}');

        var currentText = signature;
        var lastIndex = 0;

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;

            var index = currentText.IndexOf(part, lastIndex);
            if (index < 0) continue;

            // Add any text before this part
            if (index > lastIndex)
            {
                block.Inlines.Add(new Run(currentText.Substring(lastIndex, index - lastIndex))
                {
                    Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4))
                });
            }

            // Determine color for this part
            Brush color;
            if (Array.Exists(keywords, k => k == part))
            {
                color = new SolidColorBrush(Color.FromRgb(0x56, 0x9C, 0xD6)); // Blue for keywords
            }
            else if (char.IsUpper(part[0]))
            {
                color = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)); // Teal for types
            }
            else
            {
                color = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE)); // Light blue for params
            }

            block.Inlines.Add(new Run(part) { Foreground = color });
            lastIndex = index + part.Length;
        }

        // Add remaining text
        if (lastIndex < currentText.Length)
        {
            block.Inlines.Add(new Run(currentText.Substring(lastIndex))
            {
                Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4))
            });
        }
    }
}

/// <summary>
/// Factory for creating completion items with consistent documentation
/// </summary>
public static class CompletionItems
{
    #region MusicEngine API - Classes and Global Objects

    public static MusicEngineCompletionData Sequencer => new(
        "Sequencer",
        CompletionItemType.Class,
        "class Sequencer",
        "Global sequencer for timing and patterns.\n\nUse Sequencer to control tempo, playback, and schedule events at specific beats.",
        "Sequencer");

    public static MusicEngineCompletionData Engine => new(
        "Engine",
        CompletionItemType.Class,
        "class Engine",
        "Audio engine controller for MIDI routing and device management.\n\nUse Engine to connect MIDI devices to synths and manage audio routing.");

    public static MusicEngineCompletionData Pattern => new(
        "Pattern",
        CompletionItemType.Class,
        "class Pattern",
        "A musical pattern for sequencing notes.\n\nCreate patterns to build loops and sequences that can be triggered and looped.");

    public static MusicEngineCompletionData AudioEngine => new(
        "AudioEngine",
        CompletionItemType.Class,
        "class AudioEngine",
        "Low-level audio engine access.\n\nProvides direct control over the audio subsystem.");

    public static MusicEngineCompletionData SimpleSynth => new(
        "SimpleSynth",
        CompletionItemType.Class,
        "class SimpleSynth",
        "A simple built-in synthesizer with basic waveforms.\n\nSupports sine, square, saw, triangle, and noise waveforms with filter and ADSR.");

    public static MusicEngineCompletionData Vst => new(
        "vst",
        CompletionItemType.Variable,
        "VstLoader vst",
        "VST plugin loader.\n\nUse vst.load(\"PluginName\") to load VST instruments and effects.");

    public static MusicEngineCompletionData Midi => new(
        "midi",
        CompletionItemType.Variable,
        "MidiManager midi",
        "MIDI device manager.\n\nAccess and configure MIDI input/output devices.");

    #endregion

    #region Functions

    public static MusicEngineCompletionData CreateSynth => new(
        "CreateSynth",
        CompletionItemType.Method,
        "ISynth CreateSynth()",
        "Create a new synthesizer instance.\n\nReturns a synthesizer with default settings that can be customized.\n\nExample:\n  var synth = CreateSynth();\n  synth.SetParameter(\"waveform\", 2);",
        "CreateSynth()",
        -1);

    public static MusicEngineCompletionData CreateSampler => new(
        "CreateSampler",
        CompletionItemType.Method,
        "ISampler CreateSampler()",
        "Create a new sampler instance.\n\nSamplers can load and play audio files.\n\nExample:\n  var sampler = CreateSampler();\n  sampler.LoadSample(\"kick.wav\");",
        "CreateSampler()",
        -1);

    public static MusicEngineCompletionData CreatePattern => new(
        "CreatePattern",
        CompletionItemType.Method,
        "Pattern CreatePattern(ISynth target)",
        "Create a new pattern for sequencing notes.\n\nParameters:\n  target - The synth to play notes on\n\nExample:\n  var pattern = CreatePattern(synth);\n  pattern.Note(60, 0, 0.5, 100);",
        "CreatePattern()",
        -1);

    public static MusicEngineCompletionData Print => new(
        "Print",
        CompletionItemType.Method,
        "void Print(string message)",
        "Output text to the console.\n\nParameters:\n  message - The text to display\n\nExample:\n  Print(\"Hello World!\");",
        "Print(\"\")",
        -2);

    public static MusicEngineCompletionData LoadAudio => new(
        "LoadAudio",
        CompletionItemType.Method,
        "AudioClip LoadAudio(string path)",
        "Load an audio file from disk.\n\nParameters:\n  path - Path to the audio file (WAV, MP3, OGG, FLAC)\n\nReturns: AudioClip object for playback",
        "LoadAudio(\"\")",
        -2);

    public static MusicEngineCompletionData Sleep => new(
        "Sleep",
        CompletionItemType.Method,
        "void Sleep(int milliseconds)",
        "Pause execution for a specified time.\n\nParameters:\n  milliseconds - Time to pause in ms\n\nExample:\n  Sleep(500);  // Wait half a second",
        "Sleep()",
        -1);

    #endregion

    #region Sequencer Members

    public static MusicEngineCompletionData SequencerBpm => new(
        "Bpm",
        CompletionItemType.Property,
        "double Bpm { get; set; }",
        "Get or set the tempo in beats per minute.\n\nRange: 20-999 BPM\n\nExample:\n  Sequencer.Bpm = 140;");

    public static MusicEngineCompletionData SequencerStart => new(
        "Start",
        CompletionItemType.Method,
        "void Start()",
        "Start sequencer playback.\n\nBegins playing all active patterns and scheduled events.\n\nExample:\n  Sequencer.Start();",
        "Start()",
        -1);

    public static MusicEngineCompletionData SequencerStop => new(
        "Stop",
        CompletionItemType.Method,
        "void Stop()",
        "Stop sequencer playback.\n\nStops all patterns and clears scheduled events.\n\nExample:\n  Sequencer.Stop();",
        "Stop()",
        -1);

    public static MusicEngineCompletionData SequencerSchedule => new(
        "Schedule",
        CompletionItemType.Method,
        "void Schedule(double beat, Action action)",
        "Schedule an action to run at a specific beat.\n\nParameters:\n  beat - The beat number to trigger at\n  action - Code to execute\n\nExample:\n  Sequencer.Schedule(0, () => synth.NoteOn(60, 100));\n  Sequencer.Schedule(1, () => synth.NoteOff(60));",
        "Schedule(, () => )",
        -2);

    public static MusicEngineCompletionData SequencerCurrentBeat => new(
        "CurrentBeat",
        CompletionItemType.Property,
        "double CurrentBeat { get; }",
        "Get the current playback position in beats.\n\nUseful for synchronizing events with the sequencer.");

    public static MusicEngineCompletionData SequencerIsRunning => new(
        "IsRunning",
        CompletionItemType.Property,
        "bool IsRunning { get; }",
        "Check if the sequencer is currently playing.\n\nReturns true while playback is active.");

    #endregion

    #region Synth Members

    public static MusicEngineCompletionData NoteOn => new(
        "NoteOn",
        CompletionItemType.Method,
        "void NoteOn(int note, int velocity)",
        "Play a MIDI note.\n\nParameters:\n  note - MIDI note number (0-127, 60 = Middle C)\n  velocity - Volume/intensity (0-127)\n\nExample:\n  synth.NoteOn(60, 100);  // Play middle C at velocity 100",
        "NoteOn(, )",
        -3);

    public static MusicEngineCompletionData NoteOff => new(
        "NoteOff",
        CompletionItemType.Method,
        "void NoteOff(int note)",
        "Stop a playing note.\n\nParameters:\n  note - MIDI note number to stop\n\nExample:\n  synth.NoteOff(60);  // Stop middle C",
        "NoteOff()",
        -1);

    public static MusicEngineCompletionData AllNotesOff => new(
        "AllNotesOff",
        CompletionItemType.Method,
        "void AllNotesOff()",
        "Stop all currently playing notes.\n\nUseful for panic stop or before changing sounds.\n\nExample:\n  synth.AllNotesOff();",
        "AllNotesOff()",
        -1);

    public static MusicEngineCompletionData SetParameter => new(
        "SetParameter",
        CompletionItemType.Method,
        "void SetParameter(string name, float value)",
        "Set a synthesizer parameter.\n\nParameters:\n  name - Parameter name\n  value - Parameter value\n\nCommon parameters:\n  \"waveform\" - 0=Sine, 1=Square, 2=Saw, 3=Triangle, 4=Noise\n  \"cutoff\" - Filter cutoff frequency (0.0-1.0)\n  \"resonance\" - Filter resonance (0.0-1.0)\n  \"attack\" - Attack time in seconds\n  \"decay\" - Decay time in seconds\n  \"sustain\" - Sustain level (0.0-1.0)\n  \"release\" - Release time in seconds\n\nExample:\n  synth.SetParameter(\"waveform\", 2);  // Sawtooth wave\n  synth.SetParameter(\"cutoff\", 0.5f);",
        "SetParameter(\"\", )",
        -4);

    public static MusicEngineCompletionData GetParameter => new(
        "GetParameter",
        CompletionItemType.Method,
        "float GetParameter(string name)",
        "Get the current value of a synthesizer parameter.\n\nParameters:\n  name - Parameter name\n\nReturns: Current parameter value",
        "GetParameter(\"\")",
        -2);

    #endregion

    #region Pattern Members

    public static MusicEngineCompletionData PatternNote => new(
        "Note",
        CompletionItemType.Method,
        "void Note(int pitch, double beat, double duration, int velocity)",
        "Add a note to the pattern.\n\nParameters:\n  pitch - MIDI note number (60 = Middle C)\n  beat - Beat position in the pattern\n  duration - Note length in beats\n  velocity - Volume (0-127)\n\nExample:\n  pattern.Note(60, 0, 0.5, 100);    // C at beat 0\n  pattern.Note(64, 0.5, 0.5, 100);  // E at beat 0.5\n  pattern.Note(67, 1, 0.5, 100);    // G at beat 1",
        "Note(, , , )",
        -7);

    public static MusicEngineCompletionData PatternPlay => new(
        "Play",
        CompletionItemType.Method,
        "void Play()",
        "Start playing the pattern.\n\nThe pattern will play through once, or loop if Loop is enabled.\n\nExample:\n  pattern.Play();",
        "Play()",
        -1);

    public static MusicEngineCompletionData PatternStop => new(
        "Stop",
        CompletionItemType.Method,
        "void Stop()",
        "Stop the pattern playback.\n\nExample:\n  pattern.Stop();",
        "Stop()",
        -1);

    public static MusicEngineCompletionData PatternLoop => new(
        "Loop",
        CompletionItemType.Property,
        "bool Loop { get; set; }",
        "Enable or disable pattern looping.\n\nWhen true, the pattern will repeat indefinitely.\n\nExample:\n  pattern.Loop = true;");

    public static MusicEngineCompletionData PatternLength => new(
        "Length",
        CompletionItemType.Property,
        "double Length { get; set; }",
        "Get or set the pattern length in beats.\n\nExample:\n  pattern.Length = 4;  // 4-beat pattern");

    public static MusicEngineCompletionData PatternClear => new(
        "Clear",
        CompletionItemType.Method,
        "void Clear()",
        "Remove all notes from the pattern.\n\nExample:\n  pattern.Clear();",
        "Clear()",
        -1);

    #endregion

    #region Engine Members

    public static MusicEngineCompletionData RouteMidiInput => new(
        "RouteMidiInput",
        CompletionItemType.Method,
        "void RouteMidiInput(int device, ISoundSource target)",
        "Route MIDI from an input device to a synth or VST.\n\nParameters:\n  device - MIDI device index (0-based)\n  target - Target synth, sampler, or VST plugin\n\nExample:\n  Engine.RouteMidiInput(0, synth);",
        "RouteMidiInput(, )",
        -3);

    public static MusicEngineCompletionData MapRange => new(
        "MapRange",
        CompletionItemType.Method,
        "void MapRange(int device, int lowNote, int highNote, ISoundSource target, int transpose)",
        "Map a range of MIDI notes to a target.\n\nParameters:\n  device - MIDI device index\n  lowNote - Lowest note to map (0-127)\n  highNote - Highest note to map (0-127)\n  target - Target sound source\n  transpose - Transpose amount in semitones\n\nExample:\n  Engine.MapRange(0, 21, 59, bass, 0);     // Lower half to bass\n  Engine.MapRange(0, 60, 108, lead, 12);   // Upper half to lead, +1 octave",
        "MapRange(, , , , )",
        -9);

    public static MusicEngineCompletionData GetMidiInputCount => new(
        "GetMidiInputCount",
        CompletionItemType.Method,
        "int GetMidiInputCount()",
        "Get the number of available MIDI input devices.\n\nReturns: Number of MIDI inputs\n\nExample:\n  Print($\"Found {Engine.GetMidiInputCount()} MIDI inputs\");",
        "GetMidiInputCount()",
        -1);

    public static MusicEngineCompletionData GetMidiOutputCount => new(
        "GetMidiOutputCount",
        CompletionItemType.Method,
        "int GetMidiOutputCount()",
        "Get the number of available MIDI output devices.\n\nReturns: Number of MIDI outputs",
        "GetMidiOutputCount()",
        -1);

    public static MusicEngineCompletionData GetMidiInputName => new(
        "GetMidiInputName",
        CompletionItemType.Method,
        "string GetMidiInputName(int index)",
        "Get the name of a MIDI input device.\n\nParameters:\n  index - Device index\n\nReturns: Device name string",
        "GetMidiInputName()",
        -1);

    #endregion

    #region VST Members

    public static MusicEngineCompletionData VstLoad => new(
        "load",
        CompletionItemType.Method,
        "VstPlugin? vst.load(string name)",
        "Load a VST plugin by name.\n\nParameters:\n  name - Plugin name (e.g., \"Vital\", \"Serum\", \"Diva\")\n\nReturns: VstPlugin object or null if not found\n\nExample:\n  var vital = vst.load(\"Vital\");\n  if (vital != null) {\n    vital.ShowEditor();\n  }",
        "load(\"\")",
        -2);

    public static MusicEngineCompletionData VstScan => new(
        "scan",
        CompletionItemType.Method,
        "void vst.scan()",
        "Scan for available VST plugins.\n\nSearches standard VST directories for plugins.\n\nExample:\n  vst.scan();",
        "scan()",
        -1);

    public static MusicEngineCompletionData VstList => new(
        "list",
        CompletionItemType.Method,
        "string[] vst.list()",
        "Get a list of all available VST plugins.\n\nReturns: Array of plugin names",
        "list()",
        -1);

    public static MusicEngineCompletionData ShowEditor => new(
        "ShowEditor",
        CompletionItemType.Method,
        "void ShowEditor()",
        "Open the VST plugin's editor window.\n\nDisplays the plugin's native GUI.\n\nExample:\n  vital.ShowEditor();",
        "ShowEditor()",
        -1);

    public static MusicEngineCompletionData VstFrom => new(
        "from",
        CompletionItemType.Method,
        "void from(int deviceIndex)",
        "Route MIDI input from a device to this VST plugin.\n\nParameters:\n  deviceIndex - MIDI device index (0-based)\n\nExample:\n  vital.from(0);  // Route first MIDI device to plugin",
        "from()",
        -1);

    #endregion

    #region C# Keywords

    public static MusicEngineCompletionData Var => new(
        "var",
        CompletionItemType.Keyword,
        "var",
        "Declare a variable with inferred type.\n\nThe compiler determines the type from the assigned value.\n\nExample:\n  var synth = CreateSynth();");

    public static MusicEngineCompletionData If => new(
        "if",
        CompletionItemType.Keyword,
        "if (condition) { }",
        "Conditional statement.\n\nExecutes code block if condition is true.\n\nExample:\n  if (velocity > 0) {\n    synth.NoteOn(note, velocity);\n  }",
        "if () {\n\t\n}",
        -5);

    public static MusicEngineCompletionData Else => new(
        "else",
        CompletionItemType.Keyword,
        "else { }",
        "Alternative branch for if statement.\n\nExample:\n  if (active) {\n    Play();\n  } else {\n    Stop();\n  }",
        "else {\n\t\n}",
        -3);

    public static MusicEngineCompletionData For => new(
        "for",
        CompletionItemType.Keyword,
        "for (init; condition; increment) { }",
        "Loop with counter.\n\nExample:\n  for (int i = 0; i < 8; i++) {\n    pattern.Note(60 + i, i * 0.5, 0.25, 100);\n  }",
        "for (int i = 0; i < ; i++) {\n\t\n}",
        -18);

    public static MusicEngineCompletionData Foreach => new(
        "foreach",
        CompletionItemType.Keyword,
        "foreach (var item in collection) { }",
        "Iterate over a collection.\n\nExample:\n  foreach (var note in chord) {\n    synth.NoteOn(note, 100);\n  }",
        "foreach (var item in ) {\n\t\n}",
        -8);

    public static MusicEngineCompletionData While => new(
        "while",
        CompletionItemType.Keyword,
        "while (condition) { }",
        "Loop while condition is true.\n\nExample:\n  while (Sequencer.IsRunning) {\n    Sleep(100);\n  }",
        "while () {\n\t\n}",
        -6);

    public static MusicEngineCompletionData Return => new(
        "return",
        CompletionItemType.Keyword,
        "return value;",
        "Exit from the current function and optionally return a value.\n\nExample:\n  return synth;");

    public static MusicEngineCompletionData True => new(
        "true",
        CompletionItemType.Constant,
        "bool true",
        "Boolean true value.");

    public static MusicEngineCompletionData False => new(
        "false",
        CompletionItemType.Constant,
        "bool false",
        "Boolean false value.");

    public static MusicEngineCompletionData Null => new(
        "null",
        CompletionItemType.Constant,
        "null",
        "Null reference - indicates no value or object.");

    public static MusicEngineCompletionData Async => new(
        "async",
        CompletionItemType.Keyword,
        "async",
        "Mark a method as asynchronous.\n\nAllows use of await inside the method.\n\nExample:\n  async void PlaySequence() {\n    await Task.Delay(500);\n  }");

    public static MusicEngineCompletionData Await => new(
        "await",
        CompletionItemType.Keyword,
        "await",
        "Wait for an async operation to complete.\n\nExample:\n  await Task.Delay(500);");

    public static MusicEngineCompletionData New => new(
        "new",
        CompletionItemType.Keyword,
        "new Type()",
        "Create a new instance of a type.\n\nExample:\n  var list = new List<int>();");

    public static MusicEngineCompletionData This => new(
        "this",
        CompletionItemType.Keyword,
        "this",
        "Reference to the current instance.");

    public static MusicEngineCompletionData Try => new(
        "try",
        CompletionItemType.Keyword,
        "try { } catch { }",
        "Exception handling block.\n\nExample:\n  try {\n    var plugin = vst.load(\"Unknown\");\n  } catch {\n    Print(\"Plugin not found\");\n  }",
        "try {\n\t\n} catch {\n\t\n}",
        -20);

    public static MusicEngineCompletionData Catch => new(
        "catch",
        CompletionItemType.Keyword,
        "catch (Exception ex) { }",
        "Handle exceptions from try block.\n\nExample:\n  catch (Exception ex) {\n    Print(ex.Message);\n  }",
        "catch {\n\t\n}",
        -3);

    #endregion

    #region Snippets

    public static MusicEngineCompletionData SnippetBasicSetup => new(
        "setup-basic",
        CompletionItemType.Snippet,
        "Basic Setup Snippet",
        "Creates a basic MusicEngine script setup with synth and sequencer.",
        @"// Basic MusicEngine Setup
Sequencer.Bpm = 120;

var synth = CreateSynth();
synth.SetParameter(""waveform"", 2);  // Sawtooth
synth.SetParameter(""cutoff"", 0.6f);

// Route MIDI input (if available)
if (Engine.GetMidiInputCount() > 0) {
    Engine.RouteMidiInput(0, synth);
    Print(""MIDI routed to synth"");
}

Sequencer.Start();
",
        0);

    public static MusicEngineCompletionData SnippetPattern => new(
        "pattern-basic",
        CompletionItemType.Snippet,
        "Basic Pattern Snippet",
        "Creates a simple 4-beat pattern with a C major arpeggio.",
        @"// Create a simple pattern
var synth = CreateSynth();
var pattern = CreatePattern(synth);

// C major arpeggio
pattern.Note(60, 0, 0.25, 100);    // C
pattern.Note(64, 0.5, 0.25, 100);  // E
pattern.Note(67, 1, 0.25, 100);    // G
pattern.Note(72, 1.5, 0.25, 100);  // C (octave up)

pattern.Length = 2;
pattern.Loop = true;

Sequencer.Bpm = 120;
Sequencer.Start();
pattern.Play();
",
        0);

    public static MusicEngineCompletionData SnippetVst => new(
        "vst-load",
        CompletionItemType.Snippet,
        "VST Plugin Setup Snippet",
        "Load a VST plugin and route MIDI to it.",
        @"// Load VST plugin
var plugin = vst.load(""Vital"");
if (plugin != null) {
    plugin.ShowEditor();
    plugin.from(0);  // Route MIDI device 0
    Print(""VST loaded successfully"");
} else {
    Print(""VST not found"");
}
",
        0);

    public static MusicEngineCompletionData SnippetSchedule => new(
        "schedule-notes",
        CompletionItemType.Snippet,
        "Scheduled Notes Snippet",
        "Schedule notes at specific beats using the sequencer.",
        @"// Schedule notes at specific beats
var synth = CreateSynth();

Sequencer.Bpm = 120;

// Schedule a chord progression
Sequencer.Schedule(0, () => {
    synth.NoteOn(60, 100);  // C
    synth.NoteOn(64, 100);  // E
    synth.NoteOn(67, 100);  // G
});

Sequencer.Schedule(2, () => {
    synth.AllNotesOff();
    synth.NoteOn(65, 100);  // F
    synth.NoteOn(69, 100);  // A
    synth.NoteOn(72, 100);  // C
});

Sequencer.Schedule(4, () => synth.AllNotesOff());

Sequencer.Start();
",
        0);

    #endregion
}
