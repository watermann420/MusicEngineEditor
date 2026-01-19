using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using MusicEngineEditor.Editor;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using MusicEngineEditor.Views;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor;

public partial class MainWindow : Window
{
    private readonly EngineService _engineService;
    private readonly IProjectService _projectService;
    private readonly DispatcherTimer _statusTimer;
    private MusicProject? _currentProject;
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private readonly Dictionary<TabItem, MusicScript> _tabScripts = new();
    private bool _hasUnsavedChanges;
    private bool _outputVisible = true;
    private bool _isRunning = false;
    private bool _showingOutput = true;
    private CompletionWindow? _completionWindow;

    // VST Plugin Windows
    private readonly Dictionary<string, VstPluginWindow> _vstWindows = new();

    // Problems/Errors
    public ObservableCollection<ProblemItem> Problems { get; } = new();

    // Active Instruments Display
    public ObservableCollection<ActiveInstrumentInfo> ActiveInstruments { get; } = new();
    private readonly DispatcherTimer _animationTimer;

    // Data for right panel lists
    public ObservableCollection<MidiDeviceInfo> MidiDevices { get; } = new();
    public ObservableCollection<VstPluginInfo> VstPlugins { get; } = new();
    public ObservableCollection<AudioFileInfo> AudioFiles { get; } = new();

    public MainWindow()
    {
        InitializeComponent();

        // Get services from DI
        _engineService = App.Services.GetRequiredService<EngineService>();
        _projectService = App.Services.GetRequiredService<IProjectService>();

        // Load syntax highlighting
        EditorSetup.Configure(CodeEditor);

        // Setup autocomplete
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;

        // Handle Ctrl+Enter for run
        CodeEditor.PreviewKeyDown += CodeEditor_PreviewKeyDown;

        // Start status update timer
        _statusTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _statusTimer.Tick += StatusTimer_Tick;

        // Initialize engine async
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;

        // Track changes
        CodeEditor.TextChanged += (s, e) => _hasUnsavedChanges = true;
        CodeEditor.TextArea.Caret.PositionChanged += Caret_PositionChanged;

        // Bind data to lists
        MidiDevicesList.ItemsSource = MidiDevices;
        VstPluginsList.ItemsSource = VstPlugins;
        AudioFilesList.ItemsSource = AudioFiles;
        ProblemsListView.ItemsSource = Problems;
        ActiveInstrumentsPanel.ItemsSource = ActiveInstruments;

        // Animation timer for pulsing active instruments
        _animationTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _animationTimer.Tick += AnimationTimer_Tick;

        // Set initial content
        CodeEditor.Text = GetDefaultScript();
        _hasUnsavedChanges = false;
    }

    private void CodeEditor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle Ctrl+Enter to run script
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            _ = ExecuteScript();
        }
        // Handle Escape to stop
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _engineService.AllNotesOff();
            _isRunning = false;
            StatusText.Text = "Stopped";
            OutputLine("Stopped (Escape pressed)");
        }
    }

    #region Autocomplete

    private void TextArea_TextEntering(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]))
            {
                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    private void TextArea_TextEntered(object sender, TextCompositionEventArgs e)
    {
        if (e.Text == ".")
        {
            ShowCompletionWindow(GetMemberCompletions());
        }
        else if (char.IsLetter(e.Text[0]))
        {
            // Check if we should show completions
            var offset = CodeEditor.CaretOffset;
            var line = CodeEditor.Document.GetLineByOffset(offset);
            var lineText = CodeEditor.Document.GetText(line.Offset, offset - line.Offset);

            // Get the current word being typed
            var wordStart = lineText.LastIndexOfAny(new[] { ' ', '\t', '(', '{', '[', ',', ';', '=' }) + 1;
            var currentWord = lineText.Substring(wordStart);

            if (currentWord.Length >= 1)
            {
                var completions = GetGlobalCompletions()
                    .Where(c => c.Text.StartsWith(currentWord, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (completions.Any())
                {
                    ShowCompletionWindow(completions, currentWord.Length);
                }
            }
        }
    }

    private void ShowCompletionWindow(IEnumerable<CompletionData> completions, int replaceLength = 0)
    {
        _completionWindow = new CompletionWindow(CodeEditor.TextArea);
        _completionWindow.StartOffset -= replaceLength;

        var data = _completionWindow.CompletionList.CompletionData;
        foreach (var completion in completions)
        {
            data.Add(completion);
        }

        if (data.Count > 0)
        {
            _completionWindow.Show();
            _completionWindow.Closed += (s, e) => _completionWindow = null;
        }
    }

    private List<CompletionData> GetGlobalCompletions()
    {
        return new List<CompletionData>
        {
            // Sequencer
            new("Sequencer", "Global sequencer for timing and patterns\n\nProperties:\n  .Bpm - Get/set tempo\n  .CurrentBeat - Current beat position\n  .IsRunning - Check if running\n\nMethods:\n  .Start() - Start playback\n  .Stop() - Stop playback\n  .Schedule(beat, action) - Schedule action at beat"),

            // Engine
            new("Engine", "Audio engine controller\n\nMethods:\n  .RouteMidiInput(deviceIndex, target) - Route MIDI input\n  .MapRange(device, low, high, target, transpose) - Map MIDI range\n  .GetMidiInputCount() - Get number of MIDI inputs\n  .GetMidiOutputCount() - Get number of MIDI outputs"),

            // Functions
            new("CreateSynth", "CreateSynth() - Create a new synthesizer\n\nReturns: Synth object\n\nExample:\n  var synth = CreateSynth();\n  synth.SetParameter(\"waveform\", 2);"),
            new("CreateSampler", "CreateSampler() - Create a new sampler\n\nReturns: Sampler object\n\nExample:\n  var sampler = CreateSampler();\n  sampler.LoadSample(\"kick.wav\");"),
            new("Print", "Print(message) - Output text to console\n\nParameters:\n  message - Text to display\n\nExample:\n  Print(\"Hello World!\");"),
            new("LoadAudio", "LoadAudio(path) - Load an audio file\n\nParameters:\n  path - Path to audio file\n\nReturns: AudioClip object"),

            // VST
            new("vst", "VST plugin loader\n\nMethods:\n  .load(name) - Load a VST plugin by name\n  .scan() - Scan for available plugins\n\nExample:\n  var vital = vst.load(\"Vital\");"),

            // Pattern
            new("Pattern", "Pattern(name, length) - Create a musical pattern\n\nParameters:\n  name - Pattern identifier\n  length - Length in beats\n\nMethods:\n  .Note(beat, pitch, velocity, duration)\n  .Play() - Start pattern\n  .Stop() - Stop pattern"),

            // Common keywords
            new("var", "var - Declare a variable with inferred type\n\nExample:\n  var synth = CreateSynth();"),
            new("if", "if (condition) { } - Conditional statement"),
            new("for", "for (init; condition; increment) { } - Loop statement"),
            new("while", "while (condition) { } - While loop"),
            new("return", "return value; - Return from function"),
            new("true", "true - Boolean true value"),
            new("false", "false - Boolean false value"),
            new("null", "null - Null reference"),
        };
    }

    private List<CompletionData> GetMemberCompletions()
    {
        // Get the word before the dot
        var offset = CodeEditor.CaretOffset - 1;
        var line = CodeEditor.Document.GetLineByOffset(offset);
        var lineText = CodeEditor.Document.GetText(line.Offset, offset - line.Offset);
        var wordStart = lineText.LastIndexOfAny(new[] { ' ', '\t', '(', '{', '[', ',', ';', '=' }) + 1;
        var objectName = lineText.Substring(wordStart);

        return objectName.ToLower() switch
        {
            "sequencer" => new List<CompletionData>
            {
                new("Bpm", "double Bpm { get; set; } - Tempo in beats per minute\n\nExample:\n  Sequencer.Bpm = 140;"),
                new("CurrentBeat", "double CurrentBeat { get; } - Current playback position in beats"),
                new("IsRunning", "bool IsRunning { get; } - True if sequencer is playing"),
                new("Start", "Start() - Begin playback\n\nExample:\n  Sequencer.Start();"),
                new("Stop", "Stop() - Stop playback"),
                new("Schedule", "Schedule(double beat, Action action) - Schedule action at beat\n\nParameters:\n  beat - Beat number to trigger\n  action - Code to execute"),
            },
            "engine" => new List<CompletionData>
            {
                new("RouteMidiInput", "RouteMidiInput(int device, ISoundSource target)\n\nRoute MIDI from input device to a synth/sampler\n\nExample:\n  Engine.RouteMidiInput(0, synth);"),
                new("MapRange", "MapRange(int device, int low, int high, ISoundSource target, bool transpose)\n\nMap a range of MIDI notes"),
                new("GetMidiInputCount", "int GetMidiInputCount() - Returns number of MIDI input devices"),
                new("GetMidiOutputCount", "int GetMidiOutputCount() - Returns number of MIDI output devices"),
            },
            "synth" or "s" => new List<CompletionData>
            {
                new("NoteOn", "NoteOn(int pitch, int velocity) - Play a note\n\nParameters:\n  pitch - MIDI note (60 = C4)\n  velocity - Volume 0-127\n\nExample:\n  synth.NoteOn(60, 100);"),
                new("NoteOff", "NoteOff(int pitch) - Stop a note\n\nExample:\n  synth.NoteOff(60);"),
                new("SetParameter", "SetParameter(string name, float value)\n\nParameters:\n  waveform: 0=Sine, 1=Square, 2=Saw, 3=Triangle, 4=Noise\n  cutoff: Filter cutoff 0.0-1.0\n  resonance: Filter resonance 0.0-1.0\n  attack, decay, sustain, release: ADSR envelope"),
                new("AllNotesOff", "AllNotesOff() - Stop all playing notes"),
            },
            "vst" => new List<CompletionData>
            {
                new("load", "load(string name) - Load VST plugin by name\n\nReturns: VstPlugin object or null\n\nExample:\n  var vital = vst.load(\"Vital\");"),
                new("scan", "scan() - Scan for available VST plugins"),
                new("list", "list() - List all available plugins"),
            },
            "pattern" or "p" => new List<CompletionData>
            {
                new("Note", "Note(double beat, int pitch, int velocity, double duration)\n\nAdd a note to the pattern\n\nExample:\n  pattern.Note(0, 60, 100, 0.5);"),
                new("Play", "Play() - Start playing the pattern"),
                new("Stop", "Stop() - Stop the pattern"),
                new("Loop", "bool Loop { get; set; } - Enable/disable looping"),
                new("Length", "double Length { get; set; } - Pattern length in beats"),
            },
            _ => new List<CompletionData>()
        };
    }

    #endregion

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Initializing engine...";
        OutputLine("MusicEngine Editor starting...");
        OutputLine("");

        try
        {
            await _engineService.InitializeAsync();
            _statusTimer.Start();

            // Show device enumeration output from engine initialization
            if (!string.IsNullOrEmpty(_engineService.InitializationOutput))
            {
                OutputLine("=== Audio/MIDI Devices ===");
                OutputLine(_engineService.InitializationOutput);
                OutputLine("==========================");
                OutputLine("");
            }

            // Populate MIDI devices list
            RefreshMidiDevices();

            StatusText.Text = "Ready";
            OutputLine("Engine initialized successfully!");
            OutputLine("Press Ctrl+Enter to run the script, Escape to stop.");
            OutputLine("");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Engine initialization failed";
            OutputLine($"ERROR: Failed to initialize engine: {ex.Message}");
            OutputLine("Check if audio devices are available and not in use by another application.");
        }
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MessageBoxResult.Yes)
            {
                SaveAll_Click(this, new RoutedEventArgs());
            }
        }

        _statusTimer.Stop();
        CloseAllVstWindows();
        _engineService.Dispose();
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        // Update Pattern count from engine
        PatternCountDisplay.Text = _engineService.PatternCount.ToString();

        // Sync BPM display with engine (in case script changed it)
        var engineBpm = _engineService.Bpm;
        if (Math.Abs(engineBpm - double.Parse(BpmBox.Text)) > 0.1)
        {
            BpmBox.Text = engineBpm.ToString("F0");
        }

        // Update status indicator based on running state
        if (_isRunning)
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73)); // Green
        }
        else
        {
            StatusIndicator.Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Gray
        }
    }

    private void Caret_PositionChanged(object? sender, EventArgs e)
    {
        CaretPositionDisplay.Text = $"Ln {CodeEditor.TextArea.Caret.Line}, Col {CodeEditor.TextArea.Caret.Column}";
    }

    #region Project Management

    private async void NewProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewProjectDialog { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "Creating project...";
                _currentProject = await _projectService.CreateProjectAsync(dialog.ProjectName, dialog.ProjectLocation);
                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Created project: {_currentProject.Name}";
                OutputLine($"Created new project: {_currentProject.Name}");

                // Open entry point script
                var entryScript = _currentProject.Scripts[0];
                OpenScriptInTab(entryScript);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void OpenProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MusicEngine Projects (*.meproj)|*.meproj|All Files (*.*)|*.*",
            DefaultExt = ".meproj"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                StatusText.Text = "Loading project...";
                _currentProject = await _projectService.OpenProjectAsync(dialog.FileName);
                UpdateProjectExplorer();
                UpdateAudioFilesList();
                ProjectNameDisplay.Text = _currentProject.Name;
                StatusText.Text = $"Loaded: {_currentProject.Name}";
                OutputLine($"Loaded project: {_currentProject.Name}");

                // Open entry point script
                foreach (var script in _currentProject.Scripts)
                {
                    if (script.IsEntryPoint)
                    {
                        OpenScriptInTab(script);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void UpdateProjectExplorer()
    {
        ProjectTree.Items.Clear();

        if (_currentProject == null) return;

        // Project root
        var projectItem = new TreeViewItem
        {
            Header = _currentProject.Name,
            IsExpanded = true
        };

        // Scripts folder
        var scriptsFolder = new TreeViewItem
        {
            Header = "Scripts",
            IsExpanded = true
        };

        foreach (var script in _currentProject.Scripts)
        {
            var scriptItem = new TreeViewItem
            {
                Header = script.IsEntryPoint ? $"{script.FileName} (Entry)" : script.FileName,
                Tag = script
            };
            scriptsFolder.Items.Add(scriptItem);
        }

        projectItem.Items.Add(scriptsFolder);

        // Audio folder
        var audioFolder = new TreeViewItem
        {
            Header = "Audio",
            IsExpanded = true
        };

        foreach (var asset in _currentProject.AudioAssets)
        {
            var assetItem = new TreeViewItem
            {
                Header = $"{asset.Alias} ({asset.FileName})",
                Tag = asset
            };
            audioFolder.Items.Add(assetItem);
        }

        projectItem.Items.Add(audioFolder);

        // References folder (if any)
        if (_currentProject.References.Count > 0)
        {
            var refsFolder = new TreeViewItem
            {
                Header = "References",
                IsExpanded = true
            };

            foreach (var reference in _currentProject.References)
            {
                var refItem = new TreeViewItem
                {
                    Header = reference.Alias,
                    Tag = reference
                };
                refsFolder.Items.Add(refItem);
            }

            projectItem.Items.Add(refsFolder);
        }

        ProjectTree.Items.Add(projectItem);
    }

    private void ProjectTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag is MusicScript script)
        {
            OpenScriptInTab(script);
        }
    }

    #endregion

    #region Tab Management

    private void OpenScriptInTab(MusicScript script)
    {
        // Check if already open
        if (_openTabs.TryGetValue(script.FilePath, out var existingTab))
        {
            EditorTabs.SelectedItem = existingTab;
            CodeEditor.Text = script.Content;
            return;
        }

        // Create new tab
        var tab = new TabItem
        {
            Header = script.FileName,
            Tag = script
        };

        _openTabs[script.FilePath] = tab;
        _tabScripts[tab] = script;
        EditorTabs.Items.Add(tab);
        EditorTabs.SelectedItem = tab;

        CodeEditor.Text = script.Content;
        FileNameDisplay.Text = script.FileName;
    }

    private void EditorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            // Save current content to previous script
            SaveCurrentEditorContent();

            // Load selected script
            CodeEditor.Text = script.Content;
            FileNameDisplay.Text = script.FileName;
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is TabItem tab)
        {
            if (_tabScripts.TryGetValue(tab, out var script))
            {
                if (script.IsDirty)
                {
                    var result = MessageBox.Show(
                        $"Save changes to {script.FileName}?",
                        "Unsaved Changes",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Cancel) return;
                    if (result == MessageBoxResult.Yes)
                    {
                        script.Content = CodeEditor.Text;
                        _ = _projectService.SaveScriptAsync(script);
                    }
                }

                _openTabs.Remove(script.FilePath);
                _tabScripts.Remove(tab);
            }

            EditorTabs.Items.Remove(tab);

            if (EditorTabs.Items.Count == 0)
            {
                CodeEditor.Text = "";
                FileNameDisplay.Text = "";
            }
        }
    }

    private void SaveCurrentEditorContent()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            if (script.Content != CodeEditor.Text)
            {
                script.Content = CodeEditor.Text;
                script.IsDirty = true;
            }
        }
    }

    #endregion

    #region File Operations

    private void NewFile_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Simple input dialog for script name
        var name = InputDialog.Show("Enter script name:", "New Script", "NewScript", this);

        if (!string.IsNullOrWhiteSpace(name))
        {
            var script = _projectService.CreateScript(_currentProject, name);
            _currentProject.Scripts.Add(script);
            UpdateProjectExplorer();
            OpenScriptInTab(script);
        }
    }

    private async void SaveScript_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            await _projectService.SaveScriptAsync(script);
            StatusText.Text = $"Saved: {script.FileName}";
        }
        else if (_currentProject == null)
        {
            // Legacy mode - save as single file
            var dialog = new SaveFileDialog
            {
                Filter = "C# Script Files (*.csx)|*.csx|MusicEngine Scripts (*.me)|*.me|All Files (*.*)|*.*",
                DefaultExt = ".csx"
            };

            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, CodeEditor.Text);
                StatusText.Text = $"Saved: {Path.GetFileName(dialog.FileName)}";
            }
        }

        _hasUnsavedChanges = false;
    }

    private async void SaveAll_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentEditorContent();

        foreach (var kvp in _tabScripts)
        {
            if (kvp.Value.IsDirty)
            {
                await _projectService.SaveScriptAsync(kvp.Value);
            }
        }

        if (_currentProject != null)
        {
            await _projectService.SaveProjectAsync(_currentProject);
        }

        _hasUnsavedChanges = false;
        StatusText.Text = "All files saved";
    }

    #endregion

    #region Script Execution

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteScript();
    }

    private async Task ExecuteScript()
    {
        SaveCurrentEditorContent();
        var code = CodeEditor.Text;

        if (string.IsNullOrWhiteSpace(code))
        {
            OutputLine("No code to execute.");
            return;
        }

        _isRunning = true;
        StatusText.Text = "Executing...";
        RunButton.IsEnabled = false;

        // Clear previous problems
        Problems.Clear();
        UpdateErrorBadge();

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Executing script...");

        var stopwatch = Stopwatch.StartNew();
        var currentFileName = GetCurrentFileName();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Running ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Script executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }

                // Parse code to extract instruments and start animation
                ExtractInstrumentsFromCode(code);
                _animationTimer.Start();

                // Switch to running style with animation
                RunButton.Style = (Style)FindResource("RunningButtonStyle");
                RunButton.Content = "Running";
            }
            else
            {
                _isRunning = false;
                StatusText.Text = "Script error";
                OutputLine($"ERROR: {result.ErrorMessage}");

                foreach (var error in result.Errors)
                {
                    OutputLine($"  Line {error.Line}: {error.Message}");

                    // Add to Problems panel
                    Problems.Add(new ProblemItem
                    {
                        Severity = error.Severity == "Error" ? ProblemSeverity.Error : ProblemSeverity.Warning,
                        Message = error.Message,
                        FileName = currentFileName,
                        FilePath = GetCurrentFilePath(),
                        Line = error.Line,
                        Column = error.Column
                    });
                }

                UpdateErrorBadge();

                // Switch to Problems tab if there are errors
                if (Problems.Count > 0)
                {
                    SwitchOutputTab(false);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _isRunning = false;
            StatusText.Text = "Execution failed";
            OutputLine($"EXCEPTION: {ex.Message}");

            // Add exception to Problems
            Problems.Add(new ProblemItem
            {
                Severity = ProblemSeverity.Error,
                Message = ex.Message,
                FileName = currentFileName,
                FilePath = GetCurrentFilePath(),
                Line = 1,
                Column = 1
            });
            UpdateErrorBadge();
        }

        RunButton.IsEnabled = true;
    }

    private string GetCurrentFileName()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FileName;
        }
        return "Script";
    }

    private string GetCurrentFilePath()
    {
        if (EditorTabs.SelectedItem is TabItem tab && _tabScripts.TryGetValue(tab, out var script))
        {
            return script.FilePath;
        }
        return "";
    }

    private void UpdateErrorBadge()
    {
        var errorCount = Problems.Count(p => p.Severity == ProblemSeverity.Error);
        if (errorCount > 0)
        {
            ErrorCountBadge.Visibility = Visibility.Visible;
            ErrorCountText.Text = errorCount.ToString();
        }
        else
        {
            ErrorCountBadge.Visibility = Visibility.Collapsed;
        }
    }

    private void ExtractInstrumentsFromCode(string code)
    {
        ClearActiveInstruments();

        // Find synth declarations: var name = CreateSynth();
        var synthPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreateSynth\s*\(");
        foreach (System.Text.RegularExpressions.Match match in synthPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "synth");
        }

        // Find VST declarations: var name = vst.load("...")
        var vstPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*vst\.load\s*\(");
        foreach (System.Text.RegularExpressions.Match match in vstPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "vst");
        }

        // Find pattern declarations: var name = CreatePattern(...)
        var patternPattern = new System.Text.RegularExpressions.Regex(@"var\s+(\w+)\s*=\s*CreatePattern\s*\(");
        foreach (System.Text.RegularExpressions.Match match in patternPattern.Matches(code))
        {
            AddActiveInstrument(match.Groups[1].Value, "pattern");
        }
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _engineService.AllNotesOff();
        _isRunning = false;
        _animationTimer.Stop();

        // Switch back to normal style
        RunButton.Style = (Style)FindResource("RunButtonStyle");
        RunButton.Content = "Run";

        ClearActiveInstruments();
        StatusText.Text = "Stopped";
        OutputLine("Stopped");
    }

    private void BpmBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (double.TryParse(BpmBox.Text, out var bpm) && bpm > 0 && bpm < 999)
            {
                _engineService.SetBpm(bpm);
                StatusText.Text = $"BPM set to {bpm}";
            }
        }
    }

    #endregion

    #region Panel Toggle Methods

    private void ToggleProjectExplorer_Click(object sender, RoutedEventArgs e)
    {
        ProjectExplorerPanel.Visibility = ProjectExplorerMenuItem.IsChecked
            ? Visibility.Visible
            : Visibility.Collapsed;
        LeftPanelColumn.Width = ProjectExplorerMenuItem.IsChecked
            ? new GridLength(240)
            : new GridLength(0);
    }

    private void ToggleOutput_Click(object sender, RoutedEventArgs e)
    {
        _outputVisible = !_outputVisible;
        OutputMenuItem.IsChecked = _outputVisible;
        OutputPanel.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
        OutputSplitter.Visibility = _outputVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleMidiPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("midi");
        MidiPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleVstPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("vst");
        VstPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ToggleAudioPanel_Click(object sender, RoutedEventArgs e)
    {
        ShowRightPanel("audio");
        AudioPanelMenuItem.IsChecked = RightPanel.Visibility == Visibility.Visible;
    }

    private void ShowRightPanel(string tab)
    {
        // If panel is hidden, show it
        if (RightPanel.Visibility == Visibility.Collapsed)
        {
            RightPanel.Visibility = Visibility.Visible;
            RightSplitter.Visibility = Visibility.Visible;
            RightPanelColumn.Width = new GridLength(280);
            RightPanelColumn.MinWidth = 200;
        }

        // Switch to the requested tab
        SwitchRightPanelTab(tab);
    }

    private void SwitchRightPanelTab(string tab)
    {
        // Reset all tab headers
        MidiTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        VstTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        AudioTabHeader.Background = System.Windows.Media.Brushes.Transparent;
        ((TextBlock)MidiTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
        ((TextBlock)VstTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
        ((TextBlock)AudioTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");

        // Hide all panels
        MidiDevicesPanel.Visibility = Visibility.Collapsed;
        VstPluginsPanel.Visibility = Visibility.Collapsed;
        AudioFilesPanel.Visibility = Visibility.Collapsed;

        // Show selected tab
        switch (tab)
        {
            case "midi":
                MidiTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)MidiTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)MidiTabHeader.Child).FontWeight = FontWeights.SemiBold;
                MidiDevicesPanel.Visibility = Visibility.Visible;
                break;
            case "vst":
                VstTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)VstTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)VstTabHeader.Child).FontWeight = FontWeights.SemiBold;
                VstPluginsPanel.Visibility = Visibility.Visible;
                break;
            case "audio":
                AudioTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
                ((TextBlock)AudioTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
                ((TextBlock)AudioTabHeader.Child).FontWeight = FontWeights.SemiBold;
                AudioFilesPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void CloseRightPanel_Click(object sender, RoutedEventArgs e)
    {
        RightPanel.Visibility = Visibility.Collapsed;
        RightSplitter.Visibility = Visibility.Collapsed;
        RightPanelColumn.Width = new GridLength(0);
        RightPanelColumn.MinWidth = 0;

        MidiPanelMenuItem.IsChecked = false;
        VstPanelMenuItem.IsChecked = false;
        AudioPanelMenuItem.IsChecked = false;
    }

    private void MidiTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("midi");
    }

    private void VstTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("vst");
    }

    private void AudioTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchRightPanelTab("audio");
    }

    #endregion

    #region Right Panel Data Methods

    private void RefreshMidiDevices_Click(object sender, RoutedEventArgs e)
    {
        RefreshMidiDevices();
    }

    private void RefreshMidiDevices()
    {
        MidiDevices.Clear();

        try
        {
            // Get MIDI devices from engine
            var inputCount = _engineService.GetMidiInputCount();
            for (int i = 0; i < inputCount; i++)
            {
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = _engineService.GetMidiInputName(i),
                    Type = "Input",
                    Channel = "All"
                });
            }

            var outputCount = _engineService.GetMidiOutputCount();
            for (int i = 0; i < outputCount; i++)
            {
                MidiDevices.Add(new MidiDeviceInfo
                {
                    Name = _engineService.GetMidiOutputName(i),
                    Type = "Output",
                    Channel = "-"
                });
            }
        }
        catch
        {
            // If engine methods don't exist yet, add placeholder
            MidiDevices.Add(new MidiDeviceInfo { Name = "No devices found", Type = "-", Channel = "-" });
        }
    }

    private void ScanVstPlugins_Click(object sender, RoutedEventArgs e)
    {
        VstPlugins.Clear();

        // Scan common VST directories
        var vstPaths = new[]
        {
            @"C:\Program Files\Common Files\VST3",
            @"C:\Program Files\VSTPlugins",
            @"C:\Program Files\Steinberg\VSTPlugins",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\Common Files\VST3"
        };

        foreach (var path in vstPaths.Where(Directory.Exists))
        {
            try
            {
                foreach (var file in Directory.GetFiles(path, "*.vst3", SearchOption.AllDirectories))
                {
                    VstPlugins.Add(new VstPluginInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Type = "VST3",
                        Path = Path.GetDirectoryName(file) ?? ""
                    });
                }

                foreach (var file in Directory.GetFiles(path, "*.dll", SearchOption.AllDirectories))
                {
                    VstPlugins.Add(new VstPluginInfo
                    {
                        Name = Path.GetFileNameWithoutExtension(file),
                        Type = "VST2",
                        Path = Path.GetDirectoryName(file) ?? ""
                    });
                }
            }
            catch { /* Skip inaccessible directories */ }
        }

        if (VstPlugins.Count == 0)
        {
            VstPlugins.Add(new VstPluginInfo { Name = "No plugins found", Type = "-", Path = "-" });
        }

        OutputLine($"Found {VstPlugins.Count} VST plugins");
    }

    private void UpdateAudioFilesList()
    {
        AudioFiles.Clear();

        if (_currentProject == null) return;

        foreach (var asset in _currentProject.AudioAssets)
        {
            AudioFiles.Add(new AudioFileInfo
            {
                Alias = asset.Alias,
                Duration = "0:00", // Would need to read from file
                Format = Path.GetExtension(asset.FileName).TrimStart('.')
            });
        }
    }

    private void AudioFile_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AudioFilesList.SelectedItem is AudioFileInfo audio)
        {
            // Insert code to load this audio file
            var code = $"var {audio.Alias} = LoadAudio(\"{audio.Alias}\");";
            CodeEditor.Document.Insert(CodeEditor.CaretOffset, code);
        }
    }

    private void AudioFilesPanel_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            bool hasAudioFile = files.Any(f =>
                audioExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

            e.Effects = hasAudioFile ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void AudioFilesPanel_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var audioExtensions = new[] { ".wav", ".mp3", ".ogg", ".flac", ".aiff" };

            foreach (var file in files)
            {
                if (audioExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                {
                    var alias = Path.GetFileNameWithoutExtension(file);
                    _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                    OutputLine($"Imported audio: {alias}");
                }
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
        }
    }

    #endregion

    #region Menu Handlers

    private void AddScript_Click(object sender, RoutedEventArgs e)
    {
        NewFile_Click(sender, e);
    }

    private void ImportAudio_Click(object sender, RoutedEventArgs e)
    {
        if (_currentProject == null)
        {
            MessageBox.Show("Please create or open a project first.", "No Project",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files (*.wav;*.mp3;*.ogg;*.flac)|*.wav;*.mp3;*.ogg;*.flac|All Files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
            {
                var alias = Path.GetFileNameWithoutExtension(file);
                _ = _projectService.ImportAudioAsync(_currentProject, file, alias);
                OutputLine($"Imported audio: {alias}");
            }

            UpdateProjectExplorer();
            UpdateAudioFilesList();
        }
    }

    private void AddReference_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project references are not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ProjectSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show("Project settings dialog is not yet implemented.", "Coming Soon",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Find_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement find
    }

    private void Replace_Click(object sender, RoutedEventArgs e)
    {
        // TODO: Implement replace
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Documentation_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "https://github.com/watermann420/MusicEngine",
            UseShellExecute = true
        });
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "MusicEngine Editor v1.0\n\n" +
            "A professional IDE for MusicEngine live coding.\n\n" +
            "Shortcuts:\n" +
            "  Ctrl+Enter - Run script\n" +
            "  Escape - Stop / All notes off\n" +
            "  Ctrl+S - Save\n" +
            "  Ctrl+Shift+S - Save All\n" +
            "  Ctrl+Shift+N - New Project\n" +
            "  Ctrl+Shift+O - Open Project",
            "About MusicEngine Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    #endregion

    #region Helpers

    private void OutputLine(string text)
    {
        Dispatcher.Invoke(() =>
        {
            OutputBox.AppendText(text + Environment.NewLine);
            OutputBox.ScrollToEnd();
        });
    }

    private static string GetDefaultScript()
    {
        return """
            // MusicEngine Script
            // Press Ctrl+Enter to execute, Escape to stop

            // Set BPM and start the sequencer
            Sequencer.Bpm = 120;
            Sequencer.Start();

            // Create a simple synth with sawtooth waveform
            var synth = CreateSynth();
            synth.SetParameter("waveform", 2);  // 0=Sine, 1=Square, 2=Sawtooth, 3=Triangle, 4=Noise
            synth.SetParameter("cutoff", 0.6f);

            // === TEST: Play a chord directly (no MIDI keyboard needed) ===
            Print("Playing test chord...");
            synth.NoteOn(60, 100);  // C4 (Middle C)
            synth.NoteOn(64, 100);  // E4
            synth.NoteOn(67, 100);  // G4

            Print("You should hear a C major chord now!");
            Print("Press Escape to stop all notes.");
            Print("");

            // === MIDI Setup (only works if you have a MIDI keyboard connected) ===
            // Check the MIDI panel on the right for available devices
            // Engine.RouteMidiInput(0, synth);
            // Engine.MapRange(0, 21, 108, synth, false);
            // Print("MIDI keyboard routed to synth.");

            // Or load a VST plugin:
            // var vital = vst.load("Vital");
            // vital?.from(0);
            """;
    }

    #endregion

    #region Output/Problems Panel

    private void OutputTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchOutputTab(true);
    }

    private void ProblemsTab_Click(object sender, MouseButtonEventArgs e)
    {
        SwitchOutputTab(false);
    }

    private void SwitchOutputTab(bool showOutput)
    {
        _showingOutput = showOutput;

        if (showOutput)
        {
            OutputTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            ((TextBlock)OutputTabHeader.Child).Foreground = System.Windows.Media.Brushes.White;
            ((TextBlock)OutputTabHeader.Child).FontWeight = FontWeights.SemiBold;

            ProblemsTabHeader.Background = System.Windows.Media.Brushes.Transparent;
            var problemsStack = (StackPanel)ProblemsTabHeader.Child;
            ((TextBlock)problemsStack.Children[0]).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
            ((TextBlock)problemsStack.Children[0]).FontWeight = FontWeights.Normal;

            OutputBox.Visibility = Visibility.Visible;
            ProblemsListView.Visibility = Visibility.Collapsed;
        }
        else
        {
            OutputTabHeader.Background = System.Windows.Media.Brushes.Transparent;
            ((TextBlock)OutputTabHeader.Child).Foreground = (System.Windows.Media.Brush)FindResource("SecondaryForegroundBrush");
            ((TextBlock)OutputTabHeader.Child).FontWeight = FontWeights.Normal;

            ProblemsTabHeader.Background = (System.Windows.Media.Brush)FindResource("AccentBrush");
            var problemsStack = (StackPanel)ProblemsTabHeader.Child;
            ((TextBlock)problemsStack.Children[0]).Foreground = System.Windows.Media.Brushes.White;
            ((TextBlock)problemsStack.Children[0]).FontWeight = FontWeights.SemiBold;

            OutputBox.Visibility = Visibility.Collapsed;
            ProblemsListView.Visibility = Visibility.Visible;
        }
    }

    private void ProblemsListView_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProblemsListView.SelectedItem is ProblemItem problem)
        {
            // Navigate to the error location
            NavigateToError(problem);
        }
    }

    private void NavigateToError(ProblemItem problem)
    {
        // If the file is open in a tab, switch to it
        if (!string.IsNullOrEmpty(problem.FilePath) && _openTabs.TryGetValue(problem.FilePath, out var tab))
        {
            EditorTabs.SelectedItem = tab;
        }

        // Navigate to the line and column
        try
        {
            var line = Math.Max(1, problem.Line);
            var column = Math.Max(1, problem.Column);

            if (line <= CodeEditor.Document.LineCount)
            {
                var offset = CodeEditor.Document.GetOffset(line, column);
                CodeEditor.CaretOffset = offset;
                CodeEditor.ScrollToLine(line);
                CodeEditor.TextArea.Focus();

                // Select the line for visibility
                var lineInfo = CodeEditor.Document.GetLineByNumber(line);
                CodeEditor.Select(lineInfo.Offset, lineInfo.Length);
            }
        }
        catch (Exception ex)
        {
            OutputLine($"Could not navigate to error: {ex.Message}");
        }
    }

    #endregion

    #region VST Plugin Windows

    private void VstPluginsList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (VstPluginsList.SelectedItem is VstPluginInfo plugin)
        {
            OpenVstPluginWindow(plugin.Name, plugin.Name);
        }
    }

    public void OpenVstPluginWindow(string pluginName, string variableName)
    {
        var key = $"{variableName}_{pluginName}";

        if (_vstWindows.TryGetValue(key, out var existingWindow))
        {
            // Window already exists, show it
            existingWindow.ShowWindow();
        }
        else
        {
            // Create new VST window
            var window = new VstPluginWindow(pluginName, variableName, null)
            {
                Owner = this
            };

            _vstWindows[key] = window;

            // Remove from dictionary when force-closed
            window.Closed += (s, e) =>
            {
                if (!window.KeepRunning)
                {
                    _vstWindows.Remove(key);
                }
            };

            window.Show();
            OutputLine($"Opened VST plugin window: {pluginName} (variable: {variableName})");
        }
    }

    public void CloseAllVstWindows()
    {
        foreach (var window in _vstWindows.Values.ToList())
        {
            window.ForceClose();
        }
        _vstWindows.Clear();
    }

    #endregion

    #region Active Instruments Animation

    private void AnimationTimer_Tick(object? sender, EventArgs e)
    {
        foreach (var instrument in ActiveInstruments)
        {
            instrument.UpdateAnimation();
        }
    }

    public void AddActiveInstrument(string name, string type)
    {
        Dispatcher.Invoke(() =>
        {
            // Check if already exists
            var existing = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (existing == null)
            {
                ActiveInstruments.Add(new ActiveInstrumentInfo
                {
                    Name = name,
                    InstrumentType = type,
                    IsActive = true
                });
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void RemoveActiveInstrument(string name)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == name);
            if (instrument != null)
            {
                ActiveInstruments.Remove(instrument);
                UpdateNoInstrumentsVisibility();
            }
        });
    }

    public void TriggerNoteOn(string instrumentName, int note, int velocity)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.TriggerNote(note, velocity);
            }
        });
    }

    public void TriggerNoteOff(string instrumentName, int note)
    {
        Dispatcher.Invoke(() =>
        {
            var instrument = ActiveInstruments.FirstOrDefault(i => i.Name == instrumentName);
            if (instrument != null)
            {
                instrument.ReleaseNote(note);
            }
        });
    }

    public void ClearActiveInstruments()
    {
        Dispatcher.Invoke(() =>
        {
            ActiveInstruments.Clear();
            UpdateNoInstrumentsVisibility();
        });
    }

    private void UpdateNoInstrumentsVisibility()
    {
        NoInstrumentsText.Visibility = ActiveInstruments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion
}

// Data classes for the right panel lists
public class MidiDeviceInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Channel { get; set; } = "";
}

public class VstPluginInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
}

public class AudioFileInfo
{
    public string Alias { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Format { get; set; } = "";
}

// Autocomplete data class
public class CompletionData : ICompletionData
{
    public CompletionData(string text, string description)
    {
        Text = text;
        Description = description;
    }

    public System.Windows.Media.ImageSource? Image => null;
    public string Text { get; }
    public object Content => Text;
    public object Description { get; }
    public double Priority => 0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        textArea.Document.Replace(completionSegment, Text);
    }
}

// Problem/Error item for the Problems panel
public enum ProblemSeverity
{
    Error,
    Warning,
    Info
}

public class ProblemItem
{
    public ProblemSeverity Severity { get; set; } = ProblemSeverity.Error;
    public string Message { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public int Line { get; set; }
    public int Column { get; set; }

    // For display in ListView
    public string Icon => Severity switch
    {
        ProblemSeverity.Error => "\u26A0",   // Warning sign (using this since error icon is not standard)
        ProblemSeverity.Warning => "\u26A0",
        ProblemSeverity.Info => "\u2139",    // Info icon
        _ => "\u26A0"
    };

    public System.Windows.Media.Brush IconColor => Severity switch
    {
        ProblemSeverity.Error => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF7, 0x54, 0x64)),
        ProblemSeverity.Warning => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE8, 0xB3, 0x39)),
        ProblemSeverity.Info => new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x6E, 0xAF)),
        _ => System.Windows.Media.Brushes.White
    };
}

// Active Instrument display item with animation support
public class ActiveInstrumentInfo : System.ComponentModel.INotifyPropertyChanged
{
    private string _name = "";
    private string _instrumentType = "synth";
    private bool _isActive;
    private bool _isPlaying;
    private int _currentNoteValue;
    private int _velocity;
    private double _pulsePhase;
    private DateTime _lastNoteTime;
    private readonly HashSet<int> _activeNotes = new();

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(nameof(Name)); }
    }

    public string InstrumentType
    {
        get => _instrumentType;
        set { _instrumentType = value; OnPropertyChanged(nameof(Icon)); OnPropertyChanged(nameof(IconColor)); }
    }

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            OnPropertyChanged(nameof(IsActive));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(FontWeight));
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            _isPlaying = value;
            OnPropertyChanged(nameof(IsPlaying));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteVisibility));
            OnPropertyChanged(nameof(NoteColor));
        }
    }

    // Icon based on instrument type
    public string Icon => InstrumentType.ToLower() switch
    {
        "synth" => "\u266B",      // Musical note
        "vst" => "\u2699",        // Gear for VST
        "sampler" => "\u25B6",    // Play triangle for sampler
        "pattern" => "\u2630",    // Trigram for pattern
        _ => "\u266A"             // Default music note
    };

    public System.Windows.Media.Brush IconColor
    {
        get
        {
            if (IsPlaying)
            {
                // Pulsing bright color when playing
                var intensity = (byte)(180 + 75 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xFF, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0xAB, 0x73)); // Green
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Gray
        }
    }

    public System.Windows.Media.Brush TextColor
    {
        get
        {
            if (IsPlaying)
            {
                // Bright pulsing white when playing
                var intensity = (byte)(220 + 35 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, intensity, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xDF, 0xE1, 0xE5)); // Bright
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6F, 0x73, 0x7A)); // Dim
        }
    }

    public System.Windows.Media.Brush BackgroundBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Glowing background when playing
                var alpha = (byte)(40 + 30 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x6A, 0xAB, 0x73));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x4B, 0x6E, 0xAF));
            }
            return System.Windows.Media.Brushes.Transparent;
        }
    }

    public System.Windows.Media.Brush BorderBrush
    {
        get
        {
            if (IsPlaying)
            {
                // Bright green border when playing
                var intensity = (byte)(100 + 55 * Math.Sin(_pulsePhase));
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(intensity, 0xAB, intensity));
            }
            if (IsActive)
            {
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x4B, 0x6E, 0xAF));
            }
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x39, 0x3B, 0x40));
        }
    }

    public System.Windows.FontWeight FontWeight => IsPlaying ? System.Windows.FontWeights.Bold : (IsActive ? System.Windows.FontWeights.SemiBold : System.Windows.FontWeights.Normal);

    // Currently playing note display
    public string CurrentNote
    {
        get
        {
            if (!IsPlaying || _activeNotes.Count == 0) return "";
            var note = _activeNotes.First();
            var noteNames = new[] { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            var octave = (note / 12) - 1;
            var noteName = noteNames[note % 12];
            return $"{noteName}{octave}";
        }
    }

    public System.Windows.Media.Brush NoteColor
    {
        get
        {
            if (!IsPlaying) return System.Windows.Media.Brushes.Transparent;
            // Velocity-based color intensity
            var intensity = (byte)(150 + (_velocity * 105 / 127));
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, intensity, 0x50));
        }
    }

    public System.Windows.Visibility NoteVisibility => IsPlaying && _activeNotes.Count > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public void TriggerNote(int note, int velocity)
    {
        _activeNotes.Add(note);
        _currentNoteValue = note;
        _velocity = velocity;
        _lastNoteTime = DateTime.Now;
        IsPlaying = true;
        OnPropertyChanged(nameof(CurrentNote));
    }

    public void ReleaseNote(int note)
    {
        _activeNotes.Remove(note);
        if (_activeNotes.Count == 0)
        {
            IsPlaying = false;
        }
        OnPropertyChanged(nameof(CurrentNote));
        OnPropertyChanged(nameof(NoteVisibility));
    }

    public void UpdateAnimation()
    {
        if (IsPlaying)
        {
            _pulsePhase += 0.3; // Speed of pulse
            if (_pulsePhase > Math.PI * 2) _pulsePhase -= Math.PI * 2;

            // Notify all visual properties to update
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
            OnPropertyChanged(nameof(NoteColor));
        }
        else if (_pulsePhase > 0)
        {
            // Fade out animation
            _pulsePhase = 0;
            OnPropertyChanged(nameof(IconColor));
            OnPropertyChanged(nameof(TextColor));
            OnPropertyChanged(nameof(BackgroundBrush));
            OnPropertyChanged(nameof(BorderBrush));
        }
    }

    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
