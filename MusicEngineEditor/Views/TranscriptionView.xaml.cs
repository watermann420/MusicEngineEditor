using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using MusicEngine.Core.Analysis;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Views;

/// <summary>
/// Clef type for notation display.
/// </summary>
public enum TranscriptionClefType
{
    Auto,
    Treble,
    Bass,
    GrandStaff
}

/// <summary>
/// View for displaying audio transcription as musical notation.
/// Uses the AudioToMidiConverter from MusicEngine.
/// </summary>
public partial class TranscriptionView : UserControl
{
    private readonly List<DetectedNote> _detectedNotes = new();
    private readonly List<(double time, float frequency, float confidence)> _pitchHistory = new();
    private readonly DispatcherTimer _realtimeTimer;
    private AudioToMidiConverter? _converter;
    private CancellationTokenSource? _analysisCts;
    private TranscriptionClefType _clefType = TranscriptionClefType.Auto;
    private double _bpm = 120;
    private double _quantizeValue = 0;
    private bool _showNoteNames = true;
    private bool _isRealtime;
    private double _currentTime;

    // Staff drawing constants
    private const double StaffLineSpacing = 10;
    private const double StaffTopMargin = 40;
    private const double NoteWidth = 12;
    private const double BeatsPerMeasure = 4;
    private const double PixelsPerBeat = 40;

    // Colors
    private static readonly Brush StaffLineBrush = new SolidColorBrush(Color.FromRgb(80, 80, 80));
    private static readonly Brush NoteBrush = new SolidColorBrush(Color.FromRgb(74, 158, 255));
    private static readonly Brush NoteOutlineBrush = new SolidColorBrush(Color.FromRgb(40, 100, 180));
    private static readonly Brush TextBrush = new SolidColorBrush(Color.FromRgb(188, 190, 196));
    private static readonly Brush BarLineBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100));
    private static readonly Brush PitchLineBrush = new SolidColorBrush(Color.FromRgb(74, 158, 255));

    /// <summary>
    /// Event raised to request audio data for analysis.
    /// </summary>
    public event EventHandler<AudioDataRequestEventArgs>? AudioDataRequested;

    /// <summary>
    /// Event raised when MIDI export is requested.
    /// </summary>
    public event EventHandler<List<DetectedNote>>? MidiExportRequested;

    public TranscriptionView()
    {
        InitializeComponent();

        _realtimeTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(50)
        };
        _realtimeTimer.Tick += OnRealtimeTimerTick;

        Loaded += OnLoaded;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        DrawStaff();
        DrawPitchHistoryBackground();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawStaff();
        DrawPitchHistoryBackground();
    }

    #region Staff Drawing

    private void DrawStaff()
    {
        StaffCanvas.Children.Clear();
        StaffLabelsCanvas.Children.Clear();

        var width = Math.Max(StaffCanvas.ActualWidth, 800);
        var totalBeats = _detectedNotes.Count > 0
            ? _detectedNotes.Max(n => n.StartTime + n.Duration) * _bpm / 60.0
            : 16;
        var totalWidth = totalBeats * PixelsPerBeat + 100;
        StaffCanvas.Width = Math.Max(totalWidth, width);
        StaffCanvas.Height = 300;

        // Determine clef based on note range
        var actualClef = DetermineClef();

        if (actualClef == TranscriptionClefType.GrandStaff)
        {
            DrawGrandStaff(totalWidth);
        }
        else
        {
            DrawSingleStaff(totalWidth, actualClef == TranscriptionClefType.Bass ? 40 : 33);
        }

        // Draw notes
        DrawNotes(actualClef);
    }

    private TranscriptionClefType DetermineClef()
    {
        if (_clefType != TranscriptionClefType.Auto) return _clefType;
        if (_detectedNotes.Count == 0) return TranscriptionClefType.Treble;

        var avgNote = _detectedNotes.Average(n => n.MidiNote);

        // Middle C is MIDI 60
        if (avgNote < 48) return TranscriptionClefType.Bass;
        if (avgNote > 72) return TranscriptionClefType.Treble;
        return TranscriptionClefType.GrandStaff;
    }

    private void DrawSingleStaff(double width, int referenceNote)
    {
        // Draw 5 staff lines
        for (int i = 0; i < 5; i++)
        {
            double y = StaffTopMargin + i * StaffLineSpacing;
            var line = new Shapes.Line
            {
                X1 = 20,
                X2 = width - 20,
                Y1 = y,
                Y2 = y,
                Stroke = StaffLineBrush,
                StrokeThickness = 1
            };
            StaffCanvas.Children.Add(line);
        }

        // Draw clef symbol
        var clefText = new TextBlock
        {
            Text = referenceNote < 50 ? "F" : "G",
            FontSize = 36,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = TextBrush
        };
        Canvas.SetLeft(clefText, 25);
        Canvas.SetTop(clefText, StaffTopMargin - 10);
        StaffCanvas.Children.Add(clefText);

        // Draw bar lines
        DrawBarLines(width);

        // Draw staff labels
        DrawStaffLabels(referenceNote);
    }

    private void DrawGrandStaff(double width)
    {
        // Treble staff
        for (int i = 0; i < 5; i++)
        {
            double y = StaffTopMargin + i * StaffLineSpacing;
            var line = new Shapes.Line
            {
                X1 = 20,
                X2 = width - 20,
                Y1 = y,
                Y2 = y,
                Stroke = StaffLineBrush,
                StrokeThickness = 1
            };
            StaffCanvas.Children.Add(line);
        }

        // Bass staff
        double bassOffset = 100;
        for (int i = 0; i < 5; i++)
        {
            double y = StaffTopMargin + bassOffset + i * StaffLineSpacing;
            var line = new Shapes.Line
            {
                X1 = 20,
                X2 = width - 20,
                Y1 = y,
                Y2 = y,
                Stroke = StaffLineBrush,
                StrokeThickness = 1
            };
            StaffCanvas.Children.Add(line);
        }

        // Bracket connecting staves
        var bracket = new Shapes.Line
        {
            X1 = 20,
            X2 = 20,
            Y1 = StaffTopMargin,
            Y2 = StaffTopMargin + bassOffset + 4 * StaffLineSpacing,
            Stroke = TextBrush,
            StrokeThickness = 2
        };
        StaffCanvas.Children.Add(bracket);

        // Clefs
        var trebleClef = new TextBlock
        {
            Text = "G",
            FontSize = 36,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = TextBrush
        };
        Canvas.SetLeft(trebleClef, 25);
        Canvas.SetTop(trebleClef, StaffTopMargin - 10);
        StaffCanvas.Children.Add(trebleClef);

        var bassClef = new TextBlock
        {
            Text = "F",
            FontSize = 36,
            FontFamily = new FontFamily("Segoe UI"),
            Foreground = TextBrush
        };
        Canvas.SetLeft(bassClef, 25);
        Canvas.SetTop(bassClef, StaffTopMargin + bassOffset - 10);
        StaffCanvas.Children.Add(bassClef);

        DrawBarLines(width, true);
    }

    private void DrawBarLines(double width, bool isGrandStaff = false)
    {
        double staffHeight = isGrandStaff ? 140 : 4 * StaffLineSpacing;
        double startX = 80;
        double measureWidth = PixelsPerBeat * BeatsPerMeasure;

        for (double x = startX; x < width - 20; x += measureWidth)
        {
            var line = new Shapes.Line
            {
                X1 = x,
                X2 = x,
                Y1 = StaffTopMargin,
                Y2 = StaffTopMargin + staffHeight,
                Stroke = BarLineBrush,
                StrokeThickness = 1
            };
            StaffCanvas.Children.Add(line);
        }
    }

    private void DrawStaffLabels(int referenceNote)
    {
        // Reference note names based on clef
        var noteNames = referenceNote < 50
            ? new[] { "A", "F", "D", "B", "G" }  // Bass clef lines
            : new[] { "F", "D", "B", "G", "E" }; // Treble clef lines

        for (int i = 0; i < 5; i++)
        {
            double y = StaffTopMargin + i * StaffLineSpacing - 6;
            var text = new TextBlock
            {
                Text = noteNames[i],
                FontSize = 10,
                Foreground = TextBrush
            };
            Canvas.SetLeft(text, 35);
            Canvas.SetTop(text, y);
            StaffLabelsCanvas.Children.Add(text);
        }
    }

    private void DrawNotes(TranscriptionClefType clef)
    {
        if (_detectedNotes.Count == 0) return;

        foreach (var note in _detectedNotes)
        {
            // Calculate X position based on time
            double x = 80 + (note.StartTime * _bpm / 60.0) * PixelsPerBeat;

            // Calculate Y position based on MIDI note
            double y = GetNoteY(note.MidiNote, clef);

            // Draw note head (filled ellipse)
            var noteHead = new Ellipse
            {
                Width = NoteWidth,
                Height = NoteWidth * 0.75,
                Fill = NoteBrush,
                Stroke = NoteOutlineBrush,
                StrokeThickness = 1
            };
            Canvas.SetLeft(noteHead, x);
            Canvas.SetTop(noteHead, y);
            StaffCanvas.Children.Add(noteHead);

            // Draw note stem
            var stemUp = note.MidiNote < 71; // B4
            var stemLine = new Shapes.Line
            {
                X1 = stemUp ? x + NoteWidth : x,
                X2 = stemUp ? x + NoteWidth : x,
                Y1 = y + NoteWidth * 0.375,
                Y2 = stemUp ? y - 25 : y + 30,
                Stroke = NoteBrush,
                StrokeThickness = 1.5
            };
            StaffCanvas.Children.Add(stemLine);

            // Draw note name if enabled
            if (_showNoteNames)
            {
                var noteName = GetNoteName(note.MidiNote);
                var nameText = new TextBlock
                {
                    Text = noteName,
                    FontSize = 9,
                    Foreground = TextBrush
                };
                Canvas.SetLeft(nameText, x + NoteWidth / 2 - 6);
                Canvas.SetTop(nameText, y + NoteWidth + 2);
                StaffCanvas.Children.Add(nameText);
            }

            // Draw ledger lines if needed
            DrawLedgerLines(x, note.MidiNote, clef);
        }

        NoteCountText.Text = _detectedNotes.Count.ToString();
        if (_detectedNotes.Count > 0)
        {
            var maxTime = _detectedNotes.Max(n => n.StartTime + n.Duration);
            DurationText.Text = TimeSpan.FromSeconds(maxTime).ToString(@"m\:ss");
        }
    }

    private double GetNoteY(int midiNote, TranscriptionClefType clef)
    {
        // Map MIDI note to staff position
        // For treble clef, middle line is B4 (MIDI 71)
        // For bass clef, middle line is D3 (MIDI 50)

        int referenceNote;
        double referenceY;

        if (clef == TranscriptionClefType.Bass)
        {
            referenceNote = 50; // D3
            referenceY = StaffTopMargin + 2 * StaffLineSpacing;
        }
        else if (clef == TranscriptionClefType.GrandStaff)
        {
            // Use middle C (60) as reference between staves
            if (midiNote >= 60)
            {
                referenceNote = 71; // B4
                referenceY = StaffTopMargin + 2 * StaffLineSpacing;
            }
            else
            {
                referenceNote = 50; // D3
                referenceY = StaffTopMargin + 100 + 2 * StaffLineSpacing;
            }
        }
        else // Treble
        {
            referenceNote = 71; // B4
            referenceY = StaffTopMargin + 2 * StaffLineSpacing;
        }

        // Calculate staff position (each step is half a line spacing)
        var noteDiff = GetStaffPosition(midiNote) - GetStaffPosition(referenceNote);
        return referenceY - noteDiff * (StaffLineSpacing / 2) - NoteWidth * 0.375;
    }

    private int GetStaffPosition(int midiNote)
    {
        // Convert MIDI note to staff position (white key position)
        // C4 (60) = 0, D4 = 1, E4 = 2, F4 = 3, G4 = 4, A4 = 5, B4 = 6, C5 = 7, etc.
        int octave = midiNote / 12;
        int noteInOctave = midiNote % 12;

        // Map chromatic to diatonic (0-11 to 0-6)
        int[] chromToDiat = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        int diatonic = chromToDiat[noteInOctave];

        return (octave - 4) * 7 + diatonic;
    }

    private void DrawLedgerLines(double x, int midiNote, TranscriptionClefType clef)
    {
        int staffPosition = GetStaffPosition(midiNote);
        int referencePosition;

        if (clef == TranscriptionClefType.Bass)
        {
            referencePosition = GetStaffPosition(50);
        }
        else if (clef == TranscriptionClefType.GrandStaff && midiNote < 60)
        {
            referencePosition = GetStaffPosition(50);
        }
        else
        {
            referencePosition = GetStaffPosition(71);
        }

        int diff = staffPosition - referencePosition;
        double baseY = clef == TranscriptionClefType.GrandStaff && midiNote < 60
            ? StaffTopMargin + 100
            : StaffTopMargin;

        // Draw ledger lines above staff
        if (diff > 4)
        {
            for (int i = 5; i <= diff; i += 2)
            {
                double y = baseY - (i - 2) * (StaffLineSpacing / 2);
                if (y < baseY)
                {
                    var ledger = new Shapes.Line
                    {
                        X1 = x - 4,
                        X2 = x + NoteWidth + 4,
                        Y1 = y,
                        Y2 = y,
                        Stroke = StaffLineBrush,
                        StrokeThickness = 1
                    };
                    StaffCanvas.Children.Add(ledger);
                }
            }
        }

        // Draw ledger lines below staff
        if (diff < -4)
        {
            for (int i = -5; i >= diff; i -= 2)
            {
                double y = baseY + 4 * StaffLineSpacing - (i + 2) * (StaffLineSpacing / 2);
                if (y > baseY + 4 * StaffLineSpacing)
                {
                    var ledger = new Shapes.Line
                    {
                        X1 = x - 4,
                        X2 = x + NoteWidth + 4,
                        Y1 = y,
                        Y2 = y,
                        Stroke = StaffLineBrush,
                        StrokeThickness = 1
                    };
                    StaffCanvas.Children.Add(ledger);
                }
            }
        }
    }

    private static string GetNoteName(int midiNote)
    {
        string[] names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        int octave = (midiNote / 12) - 1;
        return $"{names[midiNote % 12]}{octave}";
    }

    #endregion

    #region Pitch History Graph

    private void DrawPitchHistoryBackground()
    {
        PitchHistoryCanvas.Children.Clear();

        var width = PitchHistoryCanvas.ActualWidth;
        var height = PitchHistoryCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        // Draw horizontal grid lines (frequency ranges)
        for (int i = 0; i < 5; i++)
        {
            double y = height * i / 4;
            var line = new Shapes.Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = new SolidColorBrush(Color.FromRgb(40, 40, 40)),
                StrokeThickness = 1
            };
            PitchHistoryCanvas.Children.Add(line);
        }

        // Draw pitch history
        DrawPitchHistory();
    }

    private void DrawPitchHistory()
    {
        if (_pitchHistory.Count < 2) return;

        var width = PitchHistoryCanvas.ActualWidth;
        var height = PitchHistoryCanvas.ActualHeight;
        if (width <= 0 || height <= 0) return;

        // Remove old elements except grid lines
        var toRemove = PitchHistoryCanvas.Children.OfType<Polyline>().ToList();
        foreach (var item in toRemove)
        {
            PitchHistoryCanvas.Children.Remove(item);
        }

        // Create polyline for pitch
        var polyline = new Polyline
        {
            Stroke = PitchLineBrush,
            StrokeThickness = 2
        };

        var timeSpan = _pitchHistory.Count > 0
            ? _pitchHistory[^1].time - _pitchHistory[0].time
            : 5.0;
        if (timeSpan < 1) timeSpan = 5.0;

        foreach (var (time, frequency, confidence) in _pitchHistory)
        {
            if (frequency > 0 && confidence > 0.5f)
            {
                // Map frequency to Y (log scale for musical perception)
                // Range: 50 Hz to 2000 Hz
                double freqRatio = Math.Log(frequency / 50) / Math.Log(2000.0 / 50);
                double y = height - (height * freqRatio);

                double x = (time - _pitchHistory[0].time) / timeSpan * width;

                polyline.Points.Add(new Point(x, Math.Clamp(y, 0, height)));
            }
        }

        PitchHistoryCanvas.Children.Add(polyline);
    }

    #endregion

    #region Analysis

    private async void OnAnalyzeClick(object sender, RoutedEventArgs e)
    {
        // Request audio data from parent
        var args = new AudioDataRequestEventArgs();
        AudioDataRequested?.Invoke(this, args);

        if (args.AudioData == null || args.AudioData.Length == 0)
        {
            MessageBox.Show("No audio data available. Please load an audio file first.",
                "Analysis", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _analysisCts = new CancellationTokenSource();
        AnalyzingOverlay.Visibility = Visibility.Visible;
        StatusText.Text = "Analyzing...";

        try
        {
            var audioData = args.AudioData;
            var sampleRate = args.SampleRate;

            await Task.Run(() =>
            {
                _converter = new AudioToMidiConverter(sampleRate);

                // Configure based on UI settings
                if (_quantizeValue > 0)
                {
                    _converter.MinNoteDuration = (float)(60.0 / _bpm * _quantizeValue);
                }

                _converter.ProcessSamples(audioData, 0, audioData.Length, 1);
            }, _analysisCts.Token);

            if (_converter != null)
            {
                _detectedNotes.Clear();
                _detectedNotes.AddRange(_converter.DetectedNotes);

                DrawStaff();
                StatusText.Text = $"Found {_detectedNotes.Count} notes";
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Analysis cancelled";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Analysis failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Analysis failed";
        }
        finally
        {
            AnalyzingOverlay.Visibility = Visibility.Collapsed;
            _analysisCts = null;
        }
    }

    private void OnCancelAnalysisClick(object sender, RoutedEventArgs e)
    {
        _analysisCts?.Cancel();
    }

    #endregion

    #region Real-time Processing

    private void OnRealtimeToggle(object sender, RoutedEventArgs e)
    {
        _isRealtime = RealtimeButton.IsChecked == true;

        if (_isRealtime)
        {
            _converter = new AudioToMidiConverter();
            _converter.NoteDetected += OnNoteDetected;
            _pitchHistory.Clear();
            _realtimeTimer.Start();
            StatusText.Text = "Real-time mode active";
        }
        else
        {
            _realtimeTimer.Stop();
            if (_converter != null)
            {
                _converter.NoteDetected -= OnNoteDetected;
            }
            StatusText.Text = "Ready";
        }
    }

    private void OnNoteDetected(object? sender, NoteDetectedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            // Add to history
            _pitchHistory.Add((e.StartTime, e.Frequency, e.Confidence));

            // Keep only last 5 seconds
            var cutoff = e.StartTime - 5;
            while (_pitchHistory.Count > 0 && _pitchHistory[0].time < cutoff)
            {
                _pitchHistory.RemoveAt(0);
            }

            // Update current note display
            CurrentNoteText.Text = GetNoteName(e.MidiNote);
            CurrentFrequencyText.Text = $"{e.Frequency:F1} Hz";
            CurrentConfidenceText.Text = $"Confidence: {e.Confidence * 100:F0}%";
        });
    }

    private void OnRealtimeTimerTick(object? sender, EventArgs e)
    {
        _currentTime += 0.05;
        DrawPitchHistory();
    }

    /// <summary>
    /// Processes audio samples for real-time transcription.
    /// </summary>
    public void ProcessRealtimeSamples(float[] samples, int sampleRate)
    {
        if (!_isRealtime || _converter == null) return;

        _converter.ProcessSamples(samples, 0, samples.Length, 1);
    }

    #endregion

    #region Export

    private void OnExportMidiClick(object sender, RoutedEventArgs e)
    {
        if (_detectedNotes.Count == 0)
        {
            MessageBox.Show("No notes to export. Please analyze audio first.",
                "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        MidiExportRequested?.Invoke(this, _detectedNotes);

        // Simple file save dialog
        var dialog = new SaveFileDialog
        {
            Filter = "MIDI files (*.mid)|*.mid",
            Title = "Export MIDI"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                // Export using NAudio MIDI
                ExportMidiFile(dialog.FileName);
                MessageBox.Show("MIDI exported successfully.", "Export",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export MIDI: {ex.Message}", "Export Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ExportMidiFile(string filePath)
    {
        using var stream = File.Create(filePath);
        using var writer = new BinaryWriter(stream);

        // MIDI header
        writer.Write(new byte[] { 0x4D, 0x54, 0x68, 0x64 }); // "MThd"
        writer.Write(ToBigEndian(6));     // Header length
        writer.Write(ToBigEndianShort(0)); // Format 0
        writer.Write(ToBigEndianShort(1)); // 1 track
        writer.Write(ToBigEndianShort(480)); // 480 ticks per beat

        // Track header
        writer.Write(new byte[] { 0x4D, 0x54, 0x72, 0x6B }); // "MTrk"
        var trackStart = stream.Position;
        writer.Write(0); // Placeholder for track length

        // Set tempo (120 BPM = 500000 microseconds per beat)
        int tempo = (int)(60_000_000 / _bpm);
        WriteVariableLength(writer, 0);
        writer.Write((byte)0xFF);
        writer.Write((byte)0x51);
        writer.Write((byte)0x03);
        writer.Write((byte)((tempo >> 16) & 0xFF));
        writer.Write((byte)((tempo >> 8) & 0xFF));
        writer.Write((byte)(tempo & 0xFF));

        // Write notes
        var sortedNotes = _detectedNotes.OrderBy(n => n.StartTime).ToList();
        double lastTime = 0;
        double ticksPerSecond = 480 * _bpm / 60;

        foreach (var note in sortedNotes)
        {
            int startTick = (int)(note.StartTime * ticksPerSecond);
            int endTick = (int)(note.EndTime * ticksPerSecond);
            int deltaTicks = startTick - (int)(lastTime * ticksPerSecond);

            // Note On
            WriteVariableLength(writer, Math.Max(0, deltaTicks));
            writer.Write((byte)0x90); // Note On, channel 0
            writer.Write((byte)note.MidiNote);
            writer.Write((byte)note.Velocity);

            // Note Off
            int duration = endTick - startTick;
            WriteVariableLength(writer, Math.Max(1, duration));
            writer.Write((byte)0x80); // Note Off, channel 0
            writer.Write((byte)note.MidiNote);
            writer.Write((byte)0);

            lastTime = note.EndTime;
        }

        // End of track
        WriteVariableLength(writer, 0);
        writer.Write((byte)0xFF);
        writer.Write((byte)0x2F);
        writer.Write((byte)0x00);

        // Update track length
        var trackEnd = stream.Position;
        stream.Position = trackStart;
        writer.Write(ToBigEndian((int)(trackEnd - trackStart - 4)));
    }

    private static byte[] ToBigEndian(int value)
    {
        return new byte[]
        {
            (byte)((value >> 24) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
    }

    private static byte[] ToBigEndianShort(short value)
    {
        return new byte[]
        {
            (byte)((value >> 8) & 0xFF),
            (byte)(value & 0xFF)
        };
    }

    private static void WriteVariableLength(BinaryWriter writer, int value)
    {
        if (value < 0) value = 0;

        var bytes = new List<byte>();
        bytes.Add((byte)(value & 0x7F));
        value >>= 7;

        while (value > 0)
        {
            bytes.Insert(0, (byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        writer.Write(bytes.ToArray());
    }

    #endregion

    #region UI Event Handlers

    private void OnQuantizeChanged(object sender, SelectionChangedEventArgs e)
    {
        _quantizeValue = QuantizeComboBox.SelectedIndex switch
        {
            1 => 1.0,    // 1/4
            2 => 0.5,    // 1/8
            3 => 0.25,   // 1/16
            4 => 0.125,  // 1/32
            _ => 0       // Off
        };
    }

    private void OnClefChanged(object sender, SelectionChangedEventArgs e)
    {
        _clefType = ClefComboBox.SelectedIndex switch
        {
            1 => TranscriptionClefType.Treble,
            2 => TranscriptionClefType.Bass,
            3 => TranscriptionClefType.GrandStaff,
            _ => TranscriptionClefType.Auto
        };
        DrawStaff();
    }

    private void OnBpmChanged(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(BpmTextBox.Text, out var bpm) && bpm > 0)
        {
            _bpm = bpm;
            DrawStaff();
        }
    }

    private void OnShowNoteNamesChanged(object sender, RoutedEventArgs e)
    {
        _showNoteNames = ShowNoteNamesCheckBox.IsChecked == true;
        DrawStaff();
    }

    #endregion

    /// <summary>
    /// Sets detected notes directly.
    /// </summary>
    public void SetDetectedNotes(IEnumerable<DetectedNote> notes)
    {
        _detectedNotes.Clear();
        _detectedNotes.AddRange(notes);
        DrawStaff();
    }

    /// <summary>
    /// Clears all detected notes.
    /// </summary>
    public void Clear()
    {
        _detectedNotes.Clear();
        _pitchHistory.Clear();
        DrawStaff();
        DrawPitchHistoryBackground();
    }
}

/// <summary>
/// Event arguments for requesting audio data.
/// </summary>
public class AudioDataRequestEventArgs : EventArgs
{
    /// <summary>
    /// The audio sample data (mono float).
    /// </summary>
    public float[]? AudioData { get; set; }

    /// <summary>
    /// Sample rate of the audio.
    /// </summary>
    public int SampleRate { get; set; } = 44100;
}
