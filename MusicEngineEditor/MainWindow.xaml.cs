using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Highlighting.Xshd;
using MusicEngineEditor.Services;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MusicEngineEditor;

public partial class MainWindow : Window
{
    private readonly EngineService _engineService;
    private readonly DispatcherTimer _statusTimer;
    private string? _currentFilePath;
    private bool _hasUnsavedChanges;

    public MainWindow()
    {
        InitializeComponent();

        // Initialize engine service
        _engineService = new EngineService();

        // Load syntax highlighting
        LoadSyntaxHighlighting();

        // Configure editor
        ConfigureEditor();

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

        // Set initial content
        CodeEditor.Text = GetDefaultScript();
        _hasUnsavedChanges = false;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "Initializing engine...";
        OutputLine("MusicEngine Editor starting...");

        try
        {
            await _engineService.InitializeAsync();
            _statusTimer.Start();
            StatusText.Text = "Ready";
            OutputLine("Engine initialized successfully!");
            OutputLine("Press F5 or click 'Run' to execute your script.");
            OutputLine("");
        }
        catch (Exception ex)
        {
            StatusText.Text = "Engine initialization failed";
            OutputLine($"ERROR: Failed to initialize engine: {ex.Message}");
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
                SaveScript_Click(this, new RoutedEventArgs());
            }
        }

        _statusTimer.Stop();
        _engineService.Dispose();
    }

    private void LoadSyntaxHighlighting()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("MusicEngineEditor.Editor.CSharpScript.xshd");

            if (stream != null)
            {
                using var reader = new XmlTextReader(stream);
                CodeEditor.SyntaxHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
            }
            else
            {
                // Fallback to built-in C# highlighting
                CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
            }
        }
        catch
        {
            // Use built-in C# highlighting as fallback
            CodeEditor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        }
    }

    private void ConfigureEditor()
    {
        CodeEditor.Options.EnableHyperlinks = false;
        CodeEditor.Options.EnableEmailHyperlinks = false;
        CodeEditor.Options.ConvertTabsToSpaces = true;
        CodeEditor.Options.IndentationSize = 4;
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.Options.ShowEndOfLine = false;
        CodeEditor.Options.ShowSpaces = false;
        CodeEditor.Options.ShowTabs = false;

        // Set current line highlight color
        CodeEditor.TextArea.TextView.CurrentLineBackground = new System.Windows.Media.SolidColorBrush(
            System.Windows.Media.Color.FromArgb(30, 255, 255, 255));
        CodeEditor.TextArea.TextView.CurrentLineBorder = new System.Windows.Media.Pen(
            new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 255, 255, 255)), 1);
    }

    private void StatusTimer_Tick(object? sender, EventArgs e)
    {
        BpmDisplay.Text = _engineService.Bpm.ToString("F0");
        BeatDisplay.Text = _engineService.CurrentBeat.ToString("F2");
        PatternCountDisplay.Text = _engineService.PatternCount.ToString();
    }

    private async void RunScript_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteScript();
    }

    private async Task ExecuteScript()
    {
        var code = CodeEditor.Text;

        if (string.IsNullOrWhiteSpace(code))
        {
            OutputLine("No code to execute.");
            return;
        }

        StatusText.Text = "Executing...";
        RunButton.IsEnabled = false;

        OutputLine("----------------------------------------");
        OutputLine($"[{DateTime.Now:HH:mm:ss}] Executing script...");

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var result = await _engineService.ExecuteScriptAsync(code);
            stopwatch.Stop();

            if (result.Success)
            {
                StatusText.Text = $"Executed ({stopwatch.ElapsedMilliseconds}ms)";
                OutputLine($"Script executed successfully ({stopwatch.ElapsedMilliseconds}ms)");

                if (!string.IsNullOrEmpty(result.Output))
                {
                    OutputLine(result.Output);
                }
            }
            else
            {
                StatusText.Text = "Script error";
                OutputLine($"ERROR: {result.ErrorMessage}");

                foreach (var error in result.Errors)
                {
                    OutputLine($"  Line {error.Line}: {error.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            StatusText.Text = "Execution failed";
            OutputLine($"EXCEPTION: {ex.Message}");
        }

        RunButton.IsEnabled = true;
    }

    private void Panic_Click(object sender, RoutedEventArgs e)
    {
        _engineService.AllNotesOff();
        StatusText.Text = "All notes stopped";
        OutputLine("All notes off (panic)");
    }

    private void NewScript_Click(object sender, RoutedEventArgs e)
    {
        if (_hasUnsavedChanges)
        {
            var result = MessageBox.Show(
                "You have unsaved changes. Do you want to save before creating a new script?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Cancel) return;
            if (result == MessageBoxResult.Yes) SaveScript_Click(sender, e);
        }

        CodeEditor.Text = GetDefaultScript();
        _currentFilePath = null;
        _hasUnsavedChanges = false;
        FileNameDisplay.Text = "Untitled";
        StatusText.Text = "New script created";
    }

    private void OpenScript_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "C# Script Files (*.csx)|*.csx|All Files (*.*)|*.*",
            DefaultExt = ".csx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                CodeEditor.Text = File.ReadAllText(dialog.FileName);
                _currentFilePath = dialog.FileName;
                _hasUnsavedChanges = false;
                FileNameDisplay.Text = Path.GetFileName(dialog.FileName);
                StatusText.Text = $"Opened: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveScript_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            SaveScriptAs_Click(sender, e);
            return;
        }

        try
        {
            File.WriteAllText(_currentFilePath, CodeEditor.Text);
            _hasUnsavedChanges = false;
            StatusText.Text = $"Saved: {Path.GetFileName(_currentFilePath)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveScriptAs_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "C# Script Files (*.csx)|*.csx|All Files (*.*)|*.*",
            DefaultExt = ".csx"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                File.WriteAllText(dialog.FileName, CodeEditor.Text);
                _currentFilePath = dialog.FileName;
                _hasUnsavedChanges = false;
                FileNameDisplay.Text = Path.GetFileName(dialog.FileName);
                StatusText.Text = $"Saved: {Path.GetFileName(dialog.FileName)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ClearOutput_Click(object sender, RoutedEventArgs e)
    {
        OutputBox.Clear();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "MusicEngine Editor v1.0\n\n" +
            "A live code editor for MusicEngine scripts.\n\n" +
            "Shortcuts:\n" +
            "  F5 - Run script\n" +
            "  Ctrl+S - Save\n" +
            "  Ctrl+O - Open\n" +
            "  Ctrl+N - New\n" +
            "  Esc - All notes off (panic)",
            "About MusicEngine Editor",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

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
            // Press F5 to execute

            // Set BPM
            SetBpm(120);
            Start();

            // Create a simple synth
            var synth = CreateSynth();
            synth.Waveform = WaveType.Sawtooth;
            synth.SetParameter("cutoff", 0.6f);

            // Route MIDI input (device 0) to the synth
            midi.device(0).route(synth);

            // Map full keyboard range
            midi.playablekeys.range(21, 108).low.to.high.map(synth);

            // Map modulation wheel to filter cutoff
            midi.device(0).cc(1).to(synth, "cutoff");

            Print("Synth ready! Play your MIDI keyboard.");

            // Or load a VST plugin:
            // var vital = vst.load("Vital");
            // vital?.from(0);
            """;
    }
}
