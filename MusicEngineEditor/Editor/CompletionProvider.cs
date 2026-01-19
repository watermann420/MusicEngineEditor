using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Provides intelligent code completion for the MusicEngine scripting API
/// </summary>
public class CompletionProvider
{
    private readonly TextEditor _editor;
    private CompletionWindow? _completionWindow;
    private OverloadInsightWindow? _insightWindow;

    // Track variable declarations for context-aware completions
    private readonly Dictionary<string, string> _declaredVariables = new(StringComparer.OrdinalIgnoreCase);

    public CompletionProvider(TextEditor editor)
    {
        _editor = editor;

        // Subscribe to text events
        _editor.TextArea.TextEntering += TextArea_TextEntering;
        _editor.TextArea.TextEntered += TextArea_TextEntered;
        _editor.PreviewKeyDown += Editor_PreviewKeyDown;
        _editor.TextChanged += Editor_TextChanged;
    }

    /// <summary>
    /// Detach event handlers when disposing
    /// </summary>
    public void Detach()
    {
        _editor.TextArea.TextEntering -= TextArea_TextEntering;
        _editor.TextArea.TextEntered -= TextArea_TextEntered;
        _editor.PreviewKeyDown -= Editor_PreviewKeyDown;
        _editor.TextChanged -= Editor_TextChanged;
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Ctrl+Space triggers completion manually
        if (e.Key == Key.Space && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            ShowCompletionWindow(GetContextualCompletions(), 0);
        }
    }

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            // Insert on non-identifier characters
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        // Trigger completion on dot (.)
        if (e.Text == ".")
        {
            var memberCompletions = GetMemberCompletions();
            if (memberCompletions.Any())
            {
                ShowCompletionWindow(memberCompletions, 0);
            }
        }
        // Trigger on opening parenthesis for parameter hints
        else if (e.Text == "(")
        {
            ShowParameterInfo();
        }
        // Auto-show completions while typing identifiers
        else if (char.IsLetter(e.Text[0]))
        {
            TryShowIdentifierCompletions();
        }
    }

    private void Editor_TextChanged(object? sender, EventArgs e)
    {
        // Update variable declarations cache
        UpdateVariableDeclarations();
    }

    /// <summary>
    /// Updates the cache of declared variables for context-aware completions
    /// </summary>
    private void UpdateVariableDeclarations()
    {
        _declaredVariables.Clear();

        var text = _editor.Text;
        var lines = text.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Match var declarations: var name = CreateSynth();
            if (trimmed.StartsWith("var "))
            {
                var match = System.Text.RegularExpressions.Regex.Match(trimmed,
                    @"var\s+(\w+)\s*=\s*(\w+)");
                if (match.Success)
                {
                    var varName = match.Groups[1].Value;
                    var assignment = match.Groups[2].Value;

                    // Infer type from assignment
                    var inferredType = assignment.ToLower() switch
                    {
                        "createsynth" => "synth",
                        "createsampler" => "sampler",
                        "createpattern" => "pattern",
                        "loadaudio" => "audio",
                        _ when trimmed.Contains("vst.load") => "vst",
                        _ => "unknown"
                    };

                    _declaredVariables[varName] = inferredType;
                }
            }
        }
    }

    /// <summary>
    /// Try to show completions for partially typed identifiers
    /// </summary>
    private void TryShowIdentifierCompletions()
    {
        var offset = _editor.CaretOffset;
        var line = _editor.Document.GetLineByOffset(offset);
        var lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

        // Find the start of the current word
        var wordStart = FindWordStart(lineText);
        var currentWord = lineText.Substring(wordStart);

        // Only show if we have at least 1 character
        if (currentWord.Length >= 1 && _completionWindow == null)
        {
            var completions = GetGlobalCompletions()
                .Where(c => c.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Also add matching snippets
            completions.AddRange(GetSnippetCompletions()
                .Where(c => c.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase)));

            if (completions.Any())
            {
                ShowCompletionWindow(completions, currentWord.Length);
            }
        }
    }

    /// <summary>
    /// Get completions based on current context
    /// </summary>
    private List<MusicEngineCompletionData> GetContextualCompletions()
    {
        var offset = _editor.CaretOffset;
        var line = _editor.Document.GetLineByOffset(offset);
        var lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

        // Check if we're after a dot
        if (lineText.TrimEnd().EndsWith("."))
        {
            return GetMemberCompletions();
        }

        // Check if we're typing an identifier
        var wordStart = FindWordStart(lineText);
        var currentWord = lineText.Substring(wordStart);

        var completions = new List<MusicEngineCompletionData>();

        // Add all global completions
        completions.AddRange(GetGlobalCompletions());

        // Add declared variables
        foreach (var varName in _declaredVariables.Keys)
        {
            completions.Add(new MusicEngineCompletionData(
                varName,
                CompletionItemType.Variable,
                $"var {varName}",
                $"Local variable of type {_declaredVariables[varName]}"));
        }

        // Add snippets
        completions.AddRange(GetSnippetCompletions());

        // Filter by current word if any
        if (!string.IsNullOrEmpty(currentWord))
        {
            completions = completions
                .Where(c => c.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return completions;
    }

    /// <summary>
    /// Get completions for member access (after a dot)
    /// </summary>
    private List<MusicEngineCompletionData> GetMemberCompletions()
    {
        var offset = _editor.CaretOffset - 1; // Position before the dot
        var line = _editor.Document.GetLineByOffset(offset);
        var lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

        // Find the object name before the dot
        var wordStart = FindWordStart(lineText);
        var objectName = lineText.Substring(wordStart);

        // First check if it's a known global object
        var objectLower = objectName.ToLower();

        // Check declared variables first
        if (_declaredVariables.TryGetValue(objectName, out var varType))
        {
            return GetMembersForType(varType);
        }

        // Then check global objects
        return objectLower switch
        {
            "sequencer" => GetSequencerMembers(),
            "engine" => GetEngineMembers(),
            "vst" => GetVstLoaderMembers(),
            "midi" => GetMidiMembers(),
            "task" => GetTaskMembers(),
            "math" => GetMathMembers(),
            "console" => GetConsoleMembers(),
            _ => GetMembersForType(InferTypeFromContext(objectName))
        };
    }

    /// <summary>
    /// Infer the type of a variable from context
    /// </summary>
    private string InferTypeFromContext(string varName)
    {
        var text = _editor.Text;

        // Look for assignment patterns
        var patterns = new Dictionary<string, string>
        {
            { $@"var\s+{varName}\s*=\s*CreateSynth", "synth" },
            { $@"var\s+{varName}\s*=\s*CreateSampler", "sampler" },
            { $@"var\s+{varName}\s*=\s*CreatePattern", "pattern" },
            { $@"var\s+{varName}\s*=\s*vst\.load", "vst" },
            { $@"var\s+{varName}\s*=\s*LoadAudio", "audio" },
            { $@"var\s+{varName}\s*=\s*midi\.device", "mididevice" },
        };

        foreach (var pattern in patterns)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(text, pattern.Key))
            {
                return pattern.Value;
            }
        }

        return "unknown";
    }

    /// <summary>
    /// Get members for a specific type
    /// </summary>
    private List<MusicEngineCompletionData> GetMembersForType(string typeName)
    {
        return typeName.ToLower() switch
        {
            "synth" => GetSynthMembers(),
            "sampler" => GetSamplerMembers(),
            "pattern" => GetPatternMembers(),
            "vst" or "vstplugin" => GetVstPluginMembers(),
            "audio" => GetAudioMembers(),
            "mididevice" => GetMidiDeviceMembers(),
            _ => new List<MusicEngineCompletionData>()
        };
    }

    #region Global Completions

    private static List<MusicEngineCompletionData> GetGlobalCompletions()
    {
        return new List<MusicEngineCompletionData>
        {
            // MusicEngine API - Classes
            CompletionItems.Sequencer,
            CompletionItems.Engine,
            CompletionItems.Pattern,
            CompletionItems.AudioEngine,
            CompletionItems.SimpleSynth,
            CompletionItems.Vst,
            CompletionItems.Midi,

            // Functions
            CompletionItems.CreateSynth,
            CompletionItems.CreateSampler,
            CompletionItems.CreatePattern,
            CompletionItems.Print,
            CompletionItems.LoadAudio,
            CompletionItems.Sleep,

            // C# Keywords
            CompletionItems.Var,
            CompletionItems.If,
            CompletionItems.Else,
            CompletionItems.For,
            CompletionItems.Foreach,
            CompletionItems.While,
            CompletionItems.Return,
            CompletionItems.True,
            CompletionItems.False,
            CompletionItems.Null,
            CompletionItems.Async,
            CompletionItems.Await,
            CompletionItems.New,
            CompletionItems.This,
            CompletionItems.Try,
            CompletionItems.Catch,
        };
    }

    private static List<MusicEngineCompletionData> GetSnippetCompletions()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.SnippetBasicSetup,
            CompletionItems.SnippetPattern,
            CompletionItems.SnippetVst,
            CompletionItems.SnippetSchedule,
        };
    }

    #endregion

    #region Member Completions

    private static List<MusicEngineCompletionData> GetSequencerMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.SequencerBpm,
            CompletionItems.SequencerStart,
            CompletionItems.SequencerStop,
            CompletionItems.SequencerSchedule,
            CompletionItems.SequencerCurrentBeat,
            CompletionItems.SequencerIsRunning,
        };
    }

    private static List<MusicEngineCompletionData> GetEngineMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.RouteMidiInput,
            CompletionItems.MapRange,
            CompletionItems.GetMidiInputCount,
            CompletionItems.GetMidiOutputCount,
            CompletionItems.GetMidiInputName,
        };
    }

    private static List<MusicEngineCompletionData> GetSynthMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.NoteOn,
            CompletionItems.NoteOff,
            CompletionItems.AllNotesOff,
            CompletionItems.SetParameter,
            CompletionItems.GetParameter,
        };
    }

    private static List<MusicEngineCompletionData> GetSamplerMembers()
    {
        var members = new List<MusicEngineCompletionData>
        {
            CompletionItems.NoteOn,
            CompletionItems.NoteOff,
            CompletionItems.AllNotesOff,
            new MusicEngineCompletionData(
                "LoadSample",
                CompletionItemType.Method,
                "void LoadSample(string path)",
                "Load a sample file into the sampler.\n\nParameters:\n  path - Path to the audio file\n\nExample:\n  sampler.LoadSample(\"kick.wav\");",
                "LoadSample(\"\")",
                -2),
            new MusicEngineCompletionData(
                "SetRootNote",
                CompletionItemType.Method,
                "void SetRootNote(int note)",
                "Set the root note for the loaded sample.\n\nParameters:\n  note - MIDI note number\n\nExample:\n  sampler.SetRootNote(60);  // C4",
                "SetRootNote()",
                -1),
        };
        return members;
    }

    private static List<MusicEngineCompletionData> GetPatternMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.PatternNote,
            CompletionItems.PatternPlay,
            CompletionItems.PatternStop,
            CompletionItems.PatternLoop,
            CompletionItems.PatternLength,
            CompletionItems.PatternClear,
        };
    }

    private static List<MusicEngineCompletionData> GetVstLoaderMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.VstLoad,
            CompletionItems.VstScan,
            CompletionItems.VstList,
        };
    }

    private static List<MusicEngineCompletionData> GetVstPluginMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            CompletionItems.NoteOn,
            CompletionItems.NoteOff,
            CompletionItems.AllNotesOff,
            CompletionItems.ShowEditor,
            CompletionItems.VstFrom,
            new MusicEngineCompletionData(
                "CloseEditor",
                CompletionItemType.Method,
                "void CloseEditor()",
                "Close the VST plugin's editor window.\n\nExample:\n  plugin.CloseEditor();",
                "CloseEditor()",
                -1),
            new MusicEngineCompletionData(
                "SetParameter",
                CompletionItemType.Method,
                "void SetParameter(int index, float value)",
                "Set a VST parameter by index.\n\nParameters:\n  index - Parameter index\n  value - Parameter value (0.0-1.0)\n\nExample:\n  plugin.SetParameter(0, 0.5f);",
                "SetParameter(, )",
                -3),
            new MusicEngineCompletionData(
                "GetParameterCount",
                CompletionItemType.Method,
                "int GetParameterCount()",
                "Get the number of parameters in the plugin.\n\nReturns: Number of parameters",
                "GetParameterCount()",
                -1),
        };
    }

    private static List<MusicEngineCompletionData> GetMidiMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData(
                "device",
                CompletionItemType.Method,
                "MidiDevice device(int index)",
                "Get a MIDI device by index.\n\nParameters:\n  index - Device index (0-based)\n\nReturns: MidiDevice object\n\nExample:\n  midi.device(0).route(synth);",
                "device()",
                -1),
            new MusicEngineCompletionData(
                "inputs",
                CompletionItemType.Property,
                "int inputs { get; }",
                "Get the number of MIDI input devices."),
            new MusicEngineCompletionData(
                "outputs",
                CompletionItemType.Property,
                "int outputs { get; }",
                "Get the number of MIDI output devices."),
        };
    }

    private static List<MusicEngineCompletionData> GetMidiDeviceMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData(
                "route",
                CompletionItemType.Method,
                "void route(ISoundSource target)",
                "Route this MIDI device to a target synth or VST.\n\nParameters:\n  target - Target sound source\n\nExample:\n  midi.device(0).route(synth);",
                "route()",
                -1),
            new MusicEngineCompletionData(
                "cc",
                CompletionItemType.Method,
                "MidiCC cc(int number)",
                "Get a MIDI CC controller for mapping.\n\nParameters:\n  number - CC number (0-127)\n\nReturns: MidiCC object\n\nExample:\n  midi.device(0).cc(1).to(synth, \"cutoff\");",
                "cc()",
                -1),
            new MusicEngineCompletionData(
                "name",
                CompletionItemType.Property,
                "string name { get; }",
                "Get the name of this MIDI device."),
        };
    }

    private static List<MusicEngineCompletionData> GetAudioMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData(
                "Play",
                CompletionItemType.Method,
                "void Play()",
                "Start playing the audio clip.\n\nExample:\n  audio.Play();",
                "Play()",
                -1),
            new MusicEngineCompletionData(
                "Stop",
                CompletionItemType.Method,
                "void Stop()",
                "Stop playing the audio clip.\n\nExample:\n  audio.Stop();",
                "Stop()",
                -1),
            new MusicEngineCompletionData(
                "Volume",
                CompletionItemType.Property,
                "float Volume { get; set; }",
                "Get or set the playback volume (0.0-1.0).\n\nExample:\n  audio.Volume = 0.8f;"),
            new MusicEngineCompletionData(
                "Loop",
                CompletionItemType.Property,
                "bool Loop { get; set; }",
                "Enable or disable looping.\n\nExample:\n  audio.Loop = true;"),
            new MusicEngineCompletionData(
                "Duration",
                CompletionItemType.Property,
                "double Duration { get; }",
                "Get the duration of the audio clip in seconds."),
        };
    }

    private static List<MusicEngineCompletionData> GetTaskMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData(
                "Delay",
                CompletionItemType.Method,
                "Task Delay(int milliseconds)",
                "Create a task that completes after a delay.\n\nParameters:\n  milliseconds - Time to wait in ms\n\nExample:\n  await Task.Delay(500);  // Wait 500ms",
                "Delay()",
                -1),
            new MusicEngineCompletionData(
                "Run",
                CompletionItemType.Method,
                "Task Run(Action action)",
                "Run an action on a background thread.\n\nParameters:\n  action - Code to execute\n\nExample:\n  await Task.Run(() => HeavyComputation());",
                "Run(() => )",
                -2),
        };
    }

    private static List<MusicEngineCompletionData> GetMathMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData("Sin", CompletionItemType.Method,
                "double Sin(double a)", "Returns the sine of the specified angle.", "Sin()", -1),
            new MusicEngineCompletionData("Cos", CompletionItemType.Method,
                "double Cos(double a)", "Returns the cosine of the specified angle.", "Cos()", -1),
            new MusicEngineCompletionData("Abs", CompletionItemType.Method,
                "double Abs(double value)", "Returns the absolute value.", "Abs()", -1),
            new MusicEngineCompletionData("Min", CompletionItemType.Method,
                "double Min(double a, double b)", "Returns the smaller of two values.", "Min(, )", -3),
            new MusicEngineCompletionData("Max", CompletionItemType.Method,
                "double Max(double a, double b)", "Returns the larger of two values.", "Max(, )", -3),
            new MusicEngineCompletionData("Floor", CompletionItemType.Method,
                "double Floor(double d)", "Returns the largest integer less than or equal to d.", "Floor()", -1),
            new MusicEngineCompletionData("Ceiling", CompletionItemType.Method,
                "double Ceiling(double d)", "Returns the smallest integer greater than or equal to d.", "Ceiling()", -1),
            new MusicEngineCompletionData("Round", CompletionItemType.Method,
                "double Round(double d)", "Rounds to the nearest integer.", "Round()", -1),
            new MusicEngineCompletionData("PI", CompletionItemType.Constant,
                "const double PI", "The ratio of a circle's circumference to its diameter (3.14159...)"),
        };
    }

    private static List<MusicEngineCompletionData> GetConsoleMembers()
    {
        return new List<MusicEngineCompletionData>
        {
            new MusicEngineCompletionData("WriteLine", CompletionItemType.Method,
                "void WriteLine(string value)", "Write a line to the console.", "WriteLine(\"\")", -2),
            new MusicEngineCompletionData("Write", CompletionItemType.Method,
                "void Write(string value)", "Write text to the console without newline.", "Write(\"\")", -2),
        };
    }

    #endregion

    #region Parameter Info

    private void ShowParameterInfo()
    {
        var offset = _editor.CaretOffset - 1;
        var line = _editor.Document.GetLineByOffset(offset);
        var lineText = _editor.Document.GetText(line.Offset, offset - line.Offset);

        // Find the method name before the parenthesis
        var wordStart = FindWordStart(lineText);
        var methodName = lineText.Substring(wordStart);

        var paramInfo = GetParameterInfo(methodName);
        if (!string.IsNullOrEmpty(paramInfo))
        {
            ShowInsightWindow(paramInfo);
        }
    }

    private static string GetParameterInfo(string methodName)
    {
        return methodName.ToLower() switch
        {
            "noteon" => "NoteOn(int note, int velocity)\n  note: MIDI note number (0-127, 60 = Middle C)\n  velocity: Volume (0-127)",
            "noteoff" => "NoteOff(int note)\n  note: MIDI note number to stop",
            "setparameter" => "SetParameter(string name, float value)\n  name: Parameter name (\"waveform\", \"cutoff\", etc.)\n  value: Parameter value",
            "schedule" => "Schedule(double beat, Action action)\n  beat: Beat number to trigger at\n  action: Code to execute (lambda)",
            "note" => "Note(int pitch, double beat, double duration, int velocity)\n  pitch: MIDI note (60 = C4)\n  beat: Position in pattern\n  duration: Note length in beats\n  velocity: Volume (0-127)",
            "routemidiinput" => "RouteMidiInput(int device, ISoundSource target)\n  device: MIDI device index (0-based)\n  target: Synth, sampler, or VST",
            "maprange" => "MapRange(int device, int low, int high, ISoundSource target, int transpose)\n  device: MIDI device index\n  low/high: Note range (0-127)\n  target: Sound source\n  transpose: Semitones to shift",
            "print" => "Print(string message)\n  message: Text to display in output",
            "load" => "load(string name)\n  name: Plugin name (e.g., \"Vital\", \"Serum\")",
            "delay" => "Delay(int milliseconds)\n  milliseconds: Time to wait",
            "createpattern" => "CreatePattern(ISynth target)\n  target: Synth to play notes on",
            _ => string.Empty
        };
    }

    private void ShowInsightWindow(string content)
    {
        _insightWindow?.Close();

        _insightWindow = new OverloadInsightWindow(_editor.TextArea);

        // Create styled content
        var textBlock = new System.Windows.Controls.TextBlock
        {
            Text = content,
            FontFamily = new FontFamily("JetBrains Mono, Consolas"),
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
            Padding = new Thickness(8, 4, 8, 4)
        };

        _insightWindow.Content = textBlock;
        _insightWindow.Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        _insightWindow.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));
        _insightWindow.BorderThickness = new Thickness(1);

        _insightWindow.Show();

        // Auto-close after typing or moving cursor
        void CloseHandler(object? s, EventArgs e)
        {
            _insightWindow?.Close();
            _editor.TextArea.Caret.PositionChanged -= CloseHandler;
        }
        _editor.TextArea.Caret.PositionChanged += CloseHandler;
    }

    #endregion

    #region Completion Window

    private void ShowCompletionWindow(IEnumerable<MusicEngineCompletionData> completions, int replaceLength)
    {
        var completionsList = completions.ToList();
        if (!completionsList.Any()) return;

        _completionWindow = new CompletionWindow(_editor.TextArea)
        {
            Width = 400,
            MinWidth = 300
        };

        // Adjust start offset for replacement
        _completionWindow.StartOffset -= replaceLength;

        // Style the completion window with dark theme
        StyleCompletionWindow(_completionWindow);

        // Add items
        foreach (var completion in completionsList.OrderBy(c => c.Priority).ThenBy(c => c.Text))
        {
            _completionWindow.CompletionList.CompletionData.Add(completion);
        }

        _completionWindow.Show();
        _completionWindow.Closed += (s, e) => _completionWindow = null;
    }

    private static void StyleCompletionWindow(CompletionWindow window)
    {
        window.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        window.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
        window.BorderBrush = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41));
        window.BorderThickness = new Thickness(1);

        // Style the completion list
        var completionList = window.CompletionList;
        completionList.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
        completionList.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));

        // Style the ListBox
        if (completionList.ListBox != null)
        {
            completionList.ListBox.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x1E));
            completionList.ListBox.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0));
            completionList.ListBox.BorderThickness = new Thickness(0);
        }
    }

    #endregion

    #region Helper Methods

    private static int FindWordStart(string text)
    {
        var i = text.Length - 1;
        while (i >= 0 && (char.IsLetterOrDigit(text[i]) || text[i] == '_'))
        {
            i--;
        }
        return i + 1;
    }

    #endregion
}
