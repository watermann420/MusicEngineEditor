// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Expression Map Editor UI control for managing articulations.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using MusicEngine.Core.Midi;

namespace MusicEngineEditor.Controls.MIDI;

/// <summary>
/// Expression Map Editor control for managing articulations and keyswitches.
/// Integrates with MusicEngine.Core.Midi.ExpressionMap for engine-side functionality.
/// </summary>
public partial class ExpressionMapControl : UserControl
{
    #region Fields

    private readonly ObservableCollection<ExpressionMapArticulationViewModel> _articulations = new();
    private ExpressionMapArticulationViewModel? _selectedArticulation;
    private bool _isModified;
    private bool _isLearning;
    private bool _suppressEvents;
    private string _currentFilePath = string.Empty;

    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    private static readonly SolidColorBrush[] ColorPaletteBrushes =
    [
        new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)), // Cyan (Accent)
        new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)), // Purple
        new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99)), // Pink
        new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), // Orange
        new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)), // Green
        new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), // Red
        new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)), // Indigo
        new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6)), // Teal
        new SolidColorBrush(Color.FromRgb(0x84, 0xCC, 0x16)), // Lime
        new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)), // Violet
    ];

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the name of the expression map.
    /// </summary>
    public string MapName { get; set; } = "Untitled";

    /// <summary>
    /// Gets or sets the instrument name.
    /// </summary>
    public string InstrumentName { get; set; } = string.Empty;

    /// <summary>
    /// Gets the list of articulations.
    /// </summary>
    public IReadOnlyList<ExpressionMapArticulationViewModel> Articulations => _articulations;

    /// <summary>
    /// Gets the currently selected articulation.
    /// </summary>
    public ExpressionMapArticulationViewModel? SelectedArticulation => _selectedArticulation;

    /// <summary>
    /// Gets whether the map has been modified.
    /// </summary>
    public bool IsModified => _isModified;

    /// <summary>
    /// Gets whether MIDI learn mode is active.
    /// </summary>
    public bool IsLearning => _isLearning;

    #endregion

    #region Events

    /// <summary>
    /// Occurs when an articulation is triggered.
    /// </summary>
    public event EventHandler<ExpressionMapArticulationEventArgs>? ArticulationTriggered;

    /// <summary>
    /// Occurs when the map is modified.
    /// </summary>
    public event EventHandler? MapModified;

    /// <summary>
    /// Occurs when a map is loaded.
    /// </summary>
    public event EventHandler? MapLoaded;

    /// <summary>
    /// Occurs when a map is saved.
    /// </summary>
    public event EventHandler? MapSaved;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ExpressionMapControl.
    /// </summary>
    public ExpressionMapControl()
    {
        InitializeComponent();
        InitializeControls();

        ArticulationList.ItemsSource = _articulations;
        UpdateStatusBar();
    }

    #endregion

    #region Initialization

    private void InitializeControls()
    {
        // Initialize keyswitch note combo
        InitializeNoteComboBox(KeyswitchNoteCombo);
        InitializeNoteComboBox(OutputNoteCombo);

        // Initialize CC number combo
        for (int cc = 0; cc <= 127; cc++)
        {
            var ccName = GetCCName(cc);
            CCNumberCombo.Items.Add($"{cc} - {ccName}");
        }
        CCNumberCombo.SelectedIndex = 32;

        // Initialize channel combo
        for (int i = 1; i <= 16; i++)
        {
            ChannelComboBox.Items.Add($"Channel {i}");
        }
        ChannelComboBox.SelectedIndex = 0;

        // Initialize articulation type combo
        foreach (ArticulationType type in Enum.GetValues<ArticulationType>())
        {
            ArticulationTypeCombo.Items.Add(type.ToString());
        }
        ArticulationTypeCombo.SelectedIndex = 0;

        // Initialize color palette
        ColorPalette.ItemsSource = ColorPaletteBrushes;

        // Initialize slider values
        CCValueSlider.Value = 127;
        CCValueTextBox.Text = "127";
        ProgramChangeSlider.Value = 0;
        ProgramChangeTextBox.Text = "0";
    }

    private void InitializeNoteComboBox(System.Windows.Controls.ComboBox comboBox)
    {
        for (int note = 0; note <= 127; note++)
        {
            var noteName = NoteNames[note % 12];
            var octave = (note / 12) - 2;
            comboBox.Items.Add($"{noteName}{octave} ({note})");
        }
        comboBox.SelectedIndex = 24; // C0
    }

    private static string GetCCName(int cc)
    {
        return cc switch
        {
            0 => "Bank Select MSB",
            1 => "Modulation",
            2 => "Breath Controller",
            7 => "Volume",
            10 => "Pan",
            11 => "Expression",
            32 => "Bank Select LSB",
            64 => "Sustain Pedal",
            65 => "Portamento",
            66 => "Sostenuto",
            67 => "Soft Pedal",
            91 => "Reverb",
            93 => "Chorus",
            _ => "Controller"
        };
    }

    #endregion

    #region Map Management

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (_isModified)
        {
            var result = MessageBox.Show(
                "Create a new expression map? Unsaved changes will be lost.",
                "New Expression Map",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _articulations.Clear();
        MapName = "Untitled";
        InstrumentName = string.Empty;
        _currentFilePath = string.Empty;
        _isModified = false;

        MapNameTextBox.Text = MapName;
        InstrumentNameTextBox.Text = InstrumentName;
        MapNameText.Text = " - Untitled";
        ModifiedIndicator.Visibility = Visibility.Collapsed;

        _selectedArticulation = null;
        UpdateEditor();
        UpdateStatusBar();
        SetStatus("New expression map created");
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Expression Map (*.expressionmap)|*.expressionmap|JSON (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Expression Map"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                LoadFromFile(dialog.FileName);
                SetStatus($"Imported: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing expression map: {ex.Message}",
                    "Import Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("Import failed");
            }
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Expression Map (*.expressionmap)|*.expressionmap|JSON (*.json)|*.json",
            Title = "Export Expression Map",
            FileName = MapName
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                SaveToFile(dialog.FileName);
                SetStatus($"Exported: {Path.GetFileName(dialog.FileName)}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting expression map: {ex.Message}",
                    "Export Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                SetStatus("Export failed");
            }
        }
    }

    private void AddPreset_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();

        // Orchestra Strings preset
        var stringsItem = new MenuItem { Header = "Orchestra Strings" };
        stringsItem.Click += (s, args) => AddPresetArticulations(GetStringsPreset());
        menu.Items.Add(stringsItem);

        // Orchestra Brass preset
        var brassItem = new MenuItem { Header = "Orchestra Brass" };
        brassItem.Click += (s, args) => AddPresetArticulations(GetBrassPreset());
        menu.Items.Add(brassItem);

        // Orchestra Woodwinds preset
        var woodwindsItem = new MenuItem { Header = "Orchestra Woodwinds" };
        woodwindsItem.Click += (s, args) => AddPresetArticulations(GetWoodwindsPreset());
        menu.Items.Add(woodwindsItem);

        // Piano preset
        var pianoItem = new MenuItem { Header = "Piano" };
        pianoItem.Click += (s, args) => AddPresetArticulations(GetPianoPreset());
        menu.Items.Add(pianoItem);

        // Guitar preset
        var guitarItem = new MenuItem { Header = "Guitar" };
        guitarItem.Click += (s, args) => AddPresetArticulations(GetGuitarPreset());
        menu.Items.Add(guitarItem);

        menu.Items.Add(new Separator());

        // Basic preset
        var basicItem = new MenuItem { Header = "Basic (Sustain, Staccato, Legato)" };
        basicItem.Click += (s, args) => AddPresetArticulations(GetBasicPreset());
        menu.Items.Add(basicItem);

        menu.IsOpen = true;
    }

    private void AddPresetArticulations(List<ExpressionMapArticulationViewModel> preset)
    {
        int startNote = _articulations.Count > 0
            ? _articulations.Max(a => a.KeyswitchNote) + 1
            : 24;

        foreach (var art in preset)
        {
            art.KeyswitchNote = Math.Min(startNote++, 127);
            _articulations.Add(art);
        }

        MarkModified();
        UpdateStatusBar();
        SetStatus($"Added {preset.Count} articulations from preset");
    }

    #endregion

    #region Preset Definitions

    private static List<ExpressionMapArticulationViewModel> GetStringsPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
            new() { Name = "Pizzicato", Type = ArticulationType.Pizzicato, DisplayColor = Color.FromRgb(0xEC, 0x48, 0x99) },
            new() { Name = "Tremolo", Type = ArticulationType.Tremolo, DisplayColor = Color.FromRgb(0x8B, 0x5C, 0xF6) },
            new() { Name = "Trill", Type = ArticulationType.Trill, DisplayColor = Color.FromRgb(0x63, 0x66, 0xF1) },
            new() { Name = "Spiccato", Type = ArticulationType.Spiccato, DisplayColor = Color.FromRgb(0xEF, 0x44, 0x44) },
            new() { Name = "Col Legno", Type = ArticulationType.Col_Legno, DisplayColor = Color.FromRgb(0x14, 0xB8, 0xA6) },
        ];
    }

    private static List<ExpressionMapArticulationViewModel> GetBrassPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Marcato", Type = ArticulationType.Marcato, DisplayColor = Color.FromRgb(0xEF, 0x44, 0x44) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
            new() { Name = "Tenuto", Type = ArticulationType.Tenuto, DisplayColor = Color.FromRgb(0x8B, 0x5C, 0xF6) },
        ];
    }

    private static List<ExpressionMapArticulationViewModel> GetWoodwindsPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
            new() { Name = "Trill", Type = ArticulationType.Trill, DisplayColor = Color.FromRgb(0x63, 0x66, 0xF1) },
            new() { Name = "Tremolo", Type = ArticulationType.Tremolo, DisplayColor = Color.FromRgb(0x8B, 0x5C, 0xF6) },
        ];
    }

    private static List<ExpressionMapArticulationViewModel> GetPianoPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
        ];
    }

    private static List<ExpressionMapArticulationViewModel> GetGuitarPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
            new() { Name = "Tremolo", Type = ArticulationType.Tremolo, DisplayColor = Color.FromRgb(0x8B, 0x5C, 0xF6) },
        ];
    }

    private static List<ExpressionMapArticulationViewModel> GetBasicPreset()
    {
        return
        [
            new() { Name = "Sustain", Type = ArticulationType.Sustain, DisplayColor = Color.FromRgb(0x00, 0xD9, 0xFF) },
            new() { Name = "Staccato", Type = ArticulationType.Staccato, DisplayColor = Color.FromRgb(0xF5, 0x9E, 0x0B) },
            new() { Name = "Legato", Type = ArticulationType.Legato, DisplayColor = Color.FromRgb(0x10, 0xB9, 0x81) },
        ];
    }

    #endregion

    #region Articulation List Management

    private void Search_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text.ToLowerInvariant();

        if (string.IsNullOrEmpty(searchText))
        {
            ArticulationList.ItemsSource = _articulations;
        }
        else
        {
            var filtered = _articulations
                .Where(a => a.Name.ToLowerInvariant().Contains(searchText) ||
                           a.Type.ToString().ToLowerInvariant().Contains(searchText))
                .ToList();
            ArticulationList.ItemsSource = filtered;
        }
    }

    private void ArticulationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedArticulation = ArticulationList.SelectedItem as ExpressionMapArticulationViewModel;
        UpdateEditor();
    }

    private void AddArticulation_Click(object sender, RoutedEventArgs e)
    {
        var articulation = new ExpressionMapArticulationViewModel
        {
            Name = $"Articulation {_articulations.Count + 1}",
            Type = ArticulationType.Sustain,
            KeyswitchNote = 24 + _articulations.Count,
            DisplayColor = ColorPaletteBrushes[_articulations.Count % ColorPaletteBrushes.Length].Color
        };

        _articulations.Add(articulation);
        ArticulationList.SelectedItem = articulation;
        MarkModified();
        UpdateStatusBar();
    }

    private void RemoveArticulation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticulation != null)
        {
            var index = _articulations.IndexOf(_selectedArticulation);
            _articulations.Remove(_selectedArticulation);

            if (_articulations.Count > 0)
            {
                ArticulationList.SelectedIndex = Math.Min(index, _articulations.Count - 1);
            }
            else
            {
                _selectedArticulation = null;
                UpdateEditor();
            }

            MarkModified();
            UpdateStatusBar();
        }
    }

    private void DuplicateArticulation_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticulation != null)
        {
            var duplicate = _selectedArticulation.Clone();
            duplicate.Name += " (Copy)";
            duplicate.KeyswitchNote = Math.Min(127, _selectedArticulation.KeyswitchNote + 1);

            _articulations.Add(duplicate);
            ArticulationList.SelectedItem = duplicate;
            MarkModified();
            UpdateStatusBar();
        }
    }

    private void MoveArticulation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button)
        {
            button.ContextMenu.IsOpen = true;
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticulation != null)
        {
            var index = _articulations.IndexOf(_selectedArticulation);
            if (index > 0)
            {
                _articulations.Move(index, index - 1);
                MarkModified();
            }
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedArticulation != null)
        {
            var index = _articulations.IndexOf(_selectedArticulation);
            if (index < _articulations.Count - 1)
            {
                _articulations.Move(index, index + 1);
                MarkModified();
            }
        }
    }

    #endregion

    #region Editor Updates

    private void UpdateEditor()
    {
        if (_selectedArticulation == null)
        {
            EditorPanel.Visibility = Visibility.Collapsed;
            EmptyStatePanel.Visibility = Visibility.Visible;
            return;
        }

        EditorPanel.Visibility = Visibility.Visible;
        EmptyStatePanel.Visibility = Visibility.Collapsed;

        _suppressEvents = true;
        try
        {
            // Update fields
            ArticulationNameTextBox.Text = _selectedArticulation.Name;
            ArticulationTypeCombo.SelectedIndex = (int)_selectedArticulation.Type;

            // Trigger type
            UpdateTriggerTypeSelection();

            // Keyswitch
            KeyswitchNoteCombo.SelectedIndex = Math.Max(0, _selectedArticulation.KeyswitchNote);

            // CC
            CCNumberCombo.SelectedIndex = Math.Max(0, _selectedArticulation.ControlChange);
            CCValueSlider.Value = _selectedArticulation.ControlValue;
            CCValueTextBox.Text = _selectedArticulation.ControlValue.ToString();

            // Program Change
            ProgramChangeSlider.Value = Math.Max(0, _selectedArticulation.ProgramChange);
            ProgramChangeTextBox.Text = Math.Max(0, _selectedArticulation.ProgramChange).ToString();

            // Velocity
            VelocityMinTextBox.Text = _selectedArticulation.VelocityMin.ToString();
            VelocityMaxTextBox.Text = _selectedArticulation.VelocityMax.ToString();

            // Channel
            ChannelComboBox.SelectedIndex = _selectedArticulation.Channel;

            // Output settings
            OutputKeyswitchCheck.IsChecked = _selectedArticulation.OutputKeyswitch;
            OutputCCCheck.IsChecked = _selectedArticulation.OutputCC;
            OutputProgramChangeCheck.IsChecked = _selectedArticulation.OutputProgramChange;
            OutputNoteCombo.SelectedIndex = Math.Max(0, _selectedArticulation.OutputNote);
            OutputVelocityTextBox.Text = _selectedArticulation.OutputVelocity.ToString();
            SustainCheck.IsChecked = _selectedArticulation.Sustain;

            // Display settings
            ShowInLaneCheck.IsChecked = _selectedArticulation.ShowInLane;
            ColorPreview.Background = new SolidColorBrush(_selectedArticulation.DisplayColor);

            // Update panel visibility
            UpdateTriggerPanels();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void UpdateTriggerTypeSelection()
    {
        if (_selectedArticulation == null) return;

        if (_selectedArticulation.UsesKeyswitch)
        {
            TriggerKeyswitch.IsChecked = true;
        }
        else if (_selectedArticulation.UsesControlChange)
        {
            TriggerCC.IsChecked = true;
        }
        else if (_selectedArticulation.UsesProgramChange)
        {
            TriggerProgramChange.IsChecked = true;
        }
        else if (_selectedArticulation.UsesVelocity)
        {
            TriggerVelocity.IsChecked = true;
        }
        else
        {
            TriggerKeyswitch.IsChecked = true;
        }
    }

    private void UpdateTriggerPanels()
    {
        KeyswitchPanel.Visibility = TriggerKeyswitch.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        CCPanel.Visibility = TriggerCC.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ProgramChangePanel.Visibility = TriggerProgramChange.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        VelocityPanel.Visibility = TriggerVelocity.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateStatusBar()
    {
        ArticulationCountText.Text = $"{_articulations.Count} articulation(s)";
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void MarkModified()
    {
        _isModified = true;
        ModifiedIndicator.Visibility = Visibility.Visible;
        MapModified?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Editor Event Handlers

    private void MapName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;

        MapName = MapNameTextBox.Text;
        MapNameText.Text = $" - {MapName}";
        MarkModified();
    }

    private void InstrumentName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents) return;

        InstrumentName = InstrumentNameTextBox.Text;
        MarkModified();
    }

    private void ArticulationName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.Name = ArticulationNameTextBox.Text;
        ArticulationList.Items.Refresh();
        MarkModified();
    }

    private void ArticulationType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (ArticulationTypeCombo.SelectedIndex >= 0)
        {
            _selectedArticulation.Type = (ArticulationType)ArticulationTypeCombo.SelectedIndex;
            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    private void TriggerType_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        // Update articulation based on selected trigger type
        if (TriggerKeyswitch.IsChecked == true)
        {
            _selectedArticulation.KeyswitchNote = KeyswitchNoteCombo.SelectedIndex;
            _selectedArticulation.ControlChange = -1;
            _selectedArticulation.ProgramChange = -1;
            _selectedArticulation.VelocityMin = 1;
            _selectedArticulation.VelocityMax = 127;
        }
        else if (TriggerCC.IsChecked == true)
        {
            _selectedArticulation.KeyswitchNote = -1;
            _selectedArticulation.ControlChange = CCNumberCombo.SelectedIndex;
            _selectedArticulation.ControlValue = (int)CCValueSlider.Value;
            _selectedArticulation.ProgramChange = -1;
        }
        else if (TriggerProgramChange.IsChecked == true)
        {
            _selectedArticulation.KeyswitchNote = -1;
            _selectedArticulation.ControlChange = -1;
            _selectedArticulation.ProgramChange = (int)ProgramChangeSlider.Value;
        }
        else if (TriggerVelocity.IsChecked == true)
        {
            _selectedArticulation.KeyswitchNote = -1;
            _selectedArticulation.ControlChange = -1;
            _selectedArticulation.ProgramChange = -1;
        }

        UpdateTriggerPanels();
        ArticulationList.Items.Refresh();
        MarkModified();
    }

    private void KeyswitchNote_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (KeyswitchNoteCombo.SelectedIndex >= 0)
        {
            _selectedArticulation.KeyswitchNote = KeyswitchNoteCombo.SelectedIndex;
            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    private void Learn_Click(object sender, RoutedEventArgs e)
    {
        _isLearning = !_isLearning;
        LearnButton.Content = _isLearning ? "Cancel" : "Learn";
        LearnIndicator.Visibility = _isLearning ? Visibility.Visible : Visibility.Collapsed;
        LearnModeIndicator.Visibility = _isLearning ? Visibility.Visible : Visibility.Collapsed;
        SetStatus(_isLearning ? "Press a MIDI key to assign keyswitch..." : "Ready");
    }

    /// <summary>
    /// Processes a MIDI note for learn mode.
    /// </summary>
    /// <param name="noteNumber">The MIDI note number.</param>
    /// <returns>True if the note was processed, false otherwise.</returns>
    public bool ProcessLearnNote(int noteNumber)
    {
        if (_isLearning && _selectedArticulation != null)
        {
            _selectedArticulation.KeyswitchNote = noteNumber;

            _suppressEvents = true;
            KeyswitchNoteCombo.SelectedIndex = noteNumber;
            _suppressEvents = false;

            _isLearning = false;
            LearnButton.Content = "Learn";
            LearnIndicator.Visibility = Visibility.Collapsed;
            LearnModeIndicator.Visibility = Visibility.Collapsed;

            ArticulationList.Items.Refresh();
            MarkModified();
            SetStatus($"Assigned keyswitch: {GetNoteName(noteNumber)}");
            return true;
        }
        return false;
    }

    private void CCNumber_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (CCNumberCombo.SelectedIndex >= 0)
        {
            _selectedArticulation.ControlChange = CCNumberCombo.SelectedIndex;
            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    private void CCValue_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.ControlValue = (int)CCValueSlider.Value;

        _suppressEvents = true;
        CCValueTextBox.Text = _selectedArticulation.ControlValue.ToString();
        _suppressEvents = false;

        ArticulationList.Items.Refresh();
        MarkModified();
    }

    private void CCValueText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (int.TryParse(CCValueTextBox.Text, out var value))
        {
            value = Math.Clamp(value, 0, 127);
            _selectedArticulation.ControlValue = value;

            _suppressEvents = true;
            CCValueSlider.Value = value;
            _suppressEvents = false;

            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    private void ProgramChange_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.ProgramChange = (int)ProgramChangeSlider.Value;

        _suppressEvents = true;
        ProgramChangeTextBox.Text = _selectedArticulation.ProgramChange.ToString();
        _suppressEvents = false;

        ArticulationList.Items.Refresh();
        MarkModified();
    }

    private void ProgramChangeText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (int.TryParse(ProgramChangeTextBox.Text, out var value))
        {
            value = Math.Clamp(value, 0, 127);
            _selectedArticulation.ProgramChange = value;

            _suppressEvents = true;
            ProgramChangeSlider.Value = value;
            _suppressEvents = false;

            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    private void VelocityMin_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (int.TryParse(VelocityMinTextBox.Text, out var value))
        {
            _selectedArticulation.VelocityMin = Math.Clamp(value, 1, 127);
            MarkModified();
        }
    }

    private void VelocityMax_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (int.TryParse(VelocityMaxTextBox.Text, out var value))
        {
            _selectedArticulation.VelocityMax = Math.Clamp(value, 1, 127);
            MarkModified();
        }
    }

    private void Channel_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (ChannelComboBox.SelectedIndex >= 0)
        {
            _selectedArticulation.Channel = ChannelComboBox.SelectedIndex;
            MarkModified();
        }
    }

    private void OutputKeyswitch_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.OutputKeyswitch = OutputKeyswitchCheck.IsChecked == true;
        OutputKeyswitchPanel.Visibility = _selectedArticulation.OutputKeyswitch
            ? Visibility.Visible
            : Visibility.Collapsed;
        MarkModified();
    }

    private void OutputCC_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.OutputCC = OutputCCCheck.IsChecked == true;
        MarkModified();
    }

    private void OutputProgramChange_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.OutputProgramChange = OutputProgramChangeCheck.IsChecked == true;
        MarkModified();
    }

    private void OutputNote_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (OutputNoteCombo.SelectedIndex >= 0)
        {
            _selectedArticulation.OutputNote = OutputNoteCombo.SelectedIndex;
            MarkModified();
        }
    }

    private void OutputVelocity_Changed(object sender, TextChangedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        if (int.TryParse(OutputVelocityTextBox.Text, out var value))
        {
            _selectedArticulation.OutputVelocity = Math.Clamp(value, 1, 127);
            MarkModified();
        }
    }

    private void Sustain_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.Sustain = SustainCheck.IsChecked == true;
        MarkModified();
    }

    private void ShowInLane_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || _selectedArticulation == null) return;

        _selectedArticulation.ShowInLane = ShowInLaneCheck.IsChecked == true;
        MarkModified();
    }

    private void ColorPalette_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedArticulation == null) return;

        if (sender is Border border && border.Background is SolidColorBrush brush)
        {
            _selectedArticulation.DisplayColor = brush.Color;
            ColorPreview.Background = brush;
            ArticulationList.Items.Refresh();
            MarkModified();
        }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Loads an expression map from the engine.
    /// </summary>
    /// <param name="map">The engine expression map to load.</param>
    public void LoadFromEngine(ExpressionMap map)
    {
        _articulations.Clear();

        MapName = map.Name;
        InstrumentName = map.InstrumentName;

        MapNameTextBox.Text = MapName;
        InstrumentNameTextBox.Text = InstrumentName;
        MapNameText.Text = $" - {MapName}";

        foreach (var engineArt in map.Articulations)
        {
            var viewModel = new ExpressionMapArticulationViewModel
            {
                Id = engineArt.Id,
                Name = engineArt.Name,
                Type = engineArt.Type,
                KeyswitchNote = engineArt.KeyswitchNote,
                ProgramChange = engineArt.ProgramChange,
                ControlChange = engineArt.ControlChange,
                ControlValue = engineArt.ControlValue,
                DisplayColor = Color.FromArgb(
                    engineArt.DisplayColor.A,
                    engineArt.DisplayColor.R,
                    engineArt.DisplayColor.G,
                    engineArt.DisplayColor.B)
            };
            _articulations.Add(viewModel);
        }

        _isModified = false;
        ModifiedIndicator.Visibility = Visibility.Collapsed;
        UpdateStatusBar();
        MapLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Converts the current state to an engine ExpressionMap.
    /// </summary>
    /// <returns>The engine expression map.</returns>
    public ExpressionMap ToEngine()
    {
        var map = new ExpressionMap(MapName, InstrumentName);

        foreach (var viewModel in _articulations)
        {
            var articulation = new MusicEngine.Core.Midi.Articulation(viewModel.Id)
            {
                Name = viewModel.Name,
                Type = viewModel.Type,
                KeyswitchNote = viewModel.KeyswitchNote,
                ProgramChange = viewModel.ProgramChange,
                ControlChange = viewModel.ControlChange,
                ControlValue = viewModel.ControlValue,
                DisplayColor = System.Drawing.Color.FromArgb(
                    viewModel.DisplayColor.A,
                    viewModel.DisplayColor.R,
                    viewModel.DisplayColor.G,
                    viewModel.DisplayColor.B)
            };
            map.AddArticulation(articulation);
        }

        return map;
    }

    /// <summary>
    /// Loads an expression map from a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public void LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dto = JsonSerializer.Deserialize<ExpressionMapDto>(json);

        if (dto == null)
            throw new InvalidOperationException("Failed to deserialize expression map.");

        _articulations.Clear();

        MapName = dto.Name ?? "Untitled";
        InstrumentName = dto.InstrumentName ?? string.Empty;

        MapNameTextBox.Text = MapName;
        InstrumentNameTextBox.Text = InstrumentName;
        MapNameText.Text = $" - {MapName}";

        if (dto.Articulations != null)
        {
            foreach (var artDto in dto.Articulations)
            {
                var viewModel = new ExpressionMapArticulationViewModel
                {
                    Id = artDto.Id,
                    Name = artDto.Name ?? "Articulation",
                    Type = Enum.TryParse<ArticulationType>(artDto.Type, out var type) ? type : ArticulationType.Sustain,
                    KeyswitchNote = artDto.KeyswitchNote,
                    ProgramChange = artDto.ProgramChange,
                    ControlChange = artDto.ControlChange,
                    ControlValue = artDto.ControlValue,
                    VelocityMin = artDto.VelocityMin,
                    VelocityMax = artDto.VelocityMax,
                    Channel = artDto.Channel,
                    OutputKeyswitch = artDto.OutputKeyswitch,
                    OutputCC = artDto.OutputCC,
                    OutputProgramChange = artDto.OutputProgramChange,
                    OutputNote = artDto.OutputNote,
                    OutputVelocity = artDto.OutputVelocity,
                    Sustain = artDto.Sustain,
                    ShowInLane = artDto.ShowInLane,
                    DisplayColor = Color.FromArgb(
                        (byte)((artDto.DisplayColorArgb >> 24) & 0xFF),
                        (byte)((artDto.DisplayColorArgb >> 16) & 0xFF),
                        (byte)((artDto.DisplayColorArgb >> 8) & 0xFF),
                        (byte)(artDto.DisplayColorArgb & 0xFF))
                };
                _articulations.Add(viewModel);
            }
        }

        _currentFilePath = filePath;
        _isModified = false;
        ModifiedIndicator.Visibility = Visibility.Collapsed;
        UpdateStatusBar();
        MapLoaded?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Saves the expression map to a file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    public void SaveToFile(string filePath)
    {
        var dto = new ExpressionMapDto
        {
            Id = Guid.NewGuid(),
            Name = MapName,
            InstrumentName = InstrumentName,
            Articulations = _articulations.Select(a => new ExpressionMapArticulationDto
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type.ToString(),
                KeyswitchNote = a.KeyswitchNote,
                ProgramChange = a.ProgramChange,
                ControlChange = a.ControlChange,
                ControlValue = a.ControlValue,
                VelocityMin = a.VelocityMin,
                VelocityMax = a.VelocityMax,
                Channel = a.Channel,
                OutputKeyswitch = a.OutputKeyswitch,
                OutputCC = a.OutputCC,
                OutputProgramChange = a.OutputProgramChange,
                OutputNote = a.OutputNote,
                OutputVelocity = a.OutputVelocity,
                Sustain = a.Sustain,
                ShowInLane = a.ShowInLane,
                DisplayColorArgb = (a.DisplayColor.A << 24) | (a.DisplayColor.R << 16) | (a.DisplayColor.G << 8) | a.DisplayColor.B
            }).ToList()
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(dto, options);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(filePath, json);

        _currentFilePath = filePath;
        _isModified = false;
        ModifiedIndicator.Visibility = Visibility.Collapsed;
        MapSaved?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Gets the articulation for a given keyswitch note.
    /// </summary>
    /// <param name="noteNumber">The MIDI note number.</param>
    /// <returns>The matching articulation or null.</returns>
    public ExpressionMapArticulationViewModel? GetArticulationForNote(int noteNumber)
    {
        return _articulations.FirstOrDefault(a => a.UsesKeyswitch && a.KeyswitchNote == noteNumber);
    }

    /// <summary>
    /// Triggers an articulation.
    /// </summary>
    /// <param name="articulation">The articulation to trigger.</param>
    public void TriggerArticulation(ExpressionMapArticulationViewModel articulation)
    {
        ArticulationTriggered?.Invoke(this, new ExpressionMapArticulationEventArgs(articulation));
    }

    #endregion

    #region Helper Methods

    private static string GetNoteName(int note)
    {
        if (note < 0 || note > 127) return "N/A";
        var noteName = NoteNames[note % 12];
        var octave = (note / 12) - 2;
        return $"{noteName}{octave}";
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// View model for an articulation in the expression map editor.
/// </summary>
public class ExpressionMapArticulationViewModel : INotifyPropertyChanged
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Articulation";
    private ArticulationType _type = ArticulationType.Sustain;
    private int _keyswitchNote = -1;
    private int _programChange = -1;
    private int _controlChange = -1;
    private int _controlValue;
    private int _velocityMin = 1;
    private int _velocityMax = 127;
    private int _channel;
    private bool _outputKeyswitch = true;
    private bool _outputCC;
    private bool _outputProgramChange;
    private int _outputNote;
    private int _outputVelocity = 100;
    private bool _sustain;
    private bool _showInLane = true;
    private Color _displayColor = Color.FromRgb(0x00, 0xD9, 0xFF);

    public Guid Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetField(ref _name, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
            }
        }
    }

    public ArticulationType Type
    {
        get => _type;
        set
        {
            if (SetField(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeShortName));
            }
        }
    }

    public int KeyswitchNote
    {
        get => _keyswitchNote;
        set
        {
            if (SetField(ref _keyswitchNote, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
                OnPropertyChanged(nameof(UsesKeyswitch));
            }
        }
    }

    public int ProgramChange
    {
        get => _programChange;
        set
        {
            if (SetField(ref _programChange, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
                OnPropertyChanged(nameof(UsesProgramChange));
            }
        }
    }

    public int ControlChange
    {
        get => _controlChange;
        set
        {
            if (SetField(ref _controlChange, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
                OnPropertyChanged(nameof(UsesControlChange));
            }
        }
    }

    public int ControlValue
    {
        get => _controlValue;
        set
        {
            if (SetField(ref _controlValue, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
            }
        }
    }

    public int VelocityMin
    {
        get => _velocityMin;
        set
        {
            if (SetField(ref _velocityMin, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
                OnPropertyChanged(nameof(UsesVelocity));
            }
        }
    }

    public int VelocityMax
    {
        get => _velocityMax;
        set
        {
            if (SetField(ref _velocityMax, value))
            {
                OnPropertyChanged(nameof(TriggerDisplayText));
                OnPropertyChanged(nameof(UsesVelocity));
            }
        }
    }

    public int Channel
    {
        get => _channel;
        set => SetField(ref _channel, value);
    }

    public bool OutputKeyswitch
    {
        get => _outputKeyswitch;
        set => SetField(ref _outputKeyswitch, value);
    }

    public bool OutputCC
    {
        get => _outputCC;
        set => SetField(ref _outputCC, value);
    }

    public bool OutputProgramChange
    {
        get => _outputProgramChange;
        set => SetField(ref _outputProgramChange, value);
    }

    public int OutputNote
    {
        get => _outputNote;
        set => SetField(ref _outputNote, value);
    }

    public int OutputVelocity
    {
        get => _outputVelocity;
        set => SetField(ref _outputVelocity, value);
    }

    public bool Sustain
    {
        get => _sustain;
        set => SetField(ref _sustain, value);
    }

    public bool ShowInLane
    {
        get => _showInLane;
        set => SetField(ref _showInLane, value);
    }

    public Color DisplayColor
    {
        get => _displayColor;
        set
        {
            if (SetField(ref _displayColor, value))
            {
                OnPropertyChanged(nameof(DisplayColorBrush));
            }
        }
    }

    // Computed properties
    public bool UsesKeyswitch => KeyswitchNote >= 0 && KeyswitchNote <= 127;
    public bool UsesProgramChange => ProgramChange >= 0 && ProgramChange <= 127;
    public bool UsesControlChange => ControlChange >= 0 && ControlChange <= 127;
    public bool UsesVelocity => VelocityMin > 1 || VelocityMax < 127;

    public Brush DisplayColorBrush => new SolidColorBrush(DisplayColor);

    public string TypeShortName => Type switch
    {
        ArticulationType.Sustain => "SUS",
        ArticulationType.Staccato => "STAC",
        ArticulationType.Legato => "LEG",
        ArticulationType.Pizzicato => "PIZZ",
        ArticulationType.Tremolo => "TREM",
        ArticulationType.Trill => "TRILL",
        ArticulationType.Marcato => "MARC",
        ArticulationType.Tenuto => "TEN",
        ArticulationType.Spiccato => "SPIC",
        ArticulationType.Col_Legno => "COL",
        _ => "?"
    };

    public string TriggerDisplayText
    {
        get
        {
            var parts = new List<string>();

            if (UsesKeyswitch)
            {
                parts.Add($"KS: {GetNoteName(KeyswitchNote)}");
            }

            if (UsesControlChange)
            {
                parts.Add($"CC{ControlChange}={ControlValue}");
            }

            if (UsesProgramChange)
            {
                parts.Add($"PC: {ProgramChange}");
            }

            if (UsesVelocity)
            {
                parts.Add($"Vel: {VelocityMin}-{VelocityMax}");
            }

            return parts.Count > 0 ? string.Join(" | ", parts) : "No trigger";
        }
    }

    private static string GetNoteName(int note)
    {
        if (note < 0 || note > 127) return "N/A";
        string[] noteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
        var noteName = noteNames[note % 12];
        var octave = (note / 12) - 2;
        return $"{noteName}{octave}";
    }

    public ExpressionMapArticulationViewModel Clone()
    {
        return new ExpressionMapArticulationViewModel
        {
            Name = Name,
            Type = Type,
            KeyswitchNote = KeyswitchNote,
            ProgramChange = ProgramChange,
            ControlChange = ControlChange,
            ControlValue = ControlValue,
            VelocityMin = VelocityMin,
            VelocityMax = VelocityMax,
            Channel = Channel,
            OutputKeyswitch = OutputKeyswitch,
            OutputCC = OutputCC,
            OutputProgramChange = OutputProgramChange,
            OutputNote = OutputNote,
            OutputVelocity = OutputVelocity,
            Sustain = Sustain,
            ShowInLane = ShowInLane,
            DisplayColor = DisplayColor
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Event arguments for articulation events.
/// </summary>
public sealed class ExpressionMapArticulationEventArgs : EventArgs
{
    /// <summary>
    /// The articulation that was triggered.
    /// </summary>
    public ExpressionMapArticulationViewModel Articulation { get; }

    public ExpressionMapArticulationEventArgs(ExpressionMapArticulationViewModel articulation)
    {
        Articulation = articulation;
    }
}

#endregion

#region DTO Classes

internal class ExpressionMapDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? InstrumentName { get; set; }
    public List<ExpressionMapArticulationDto>? Articulations { get; set; }
}

internal class ExpressionMapArticulationDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public int KeyswitchNote { get; set; }
    public int ProgramChange { get; set; }
    public int ControlChange { get; set; }
    public int ControlValue { get; set; }
    public int VelocityMin { get; set; } = 1;
    public int VelocityMax { get; set; } = 127;
    public int Channel { get; set; }
    public bool OutputKeyswitch { get; set; } = true;
    public bool OutputCC { get; set; }
    public bool OutputProgramChange { get; set; }
    public int OutputNote { get; set; }
    public int OutputVelocity { get; set; } = 100;
    public bool Sustain { get; set; }
    public bool ShowInLane { get; set; } = true;
    public int DisplayColorArgb { get; set; }
}

#endregion

#region Converters

/// <summary>
/// Converts output action to string.
/// </summary>
public class ExpressionMapOutputActionToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue && parameter is string paramStr)
        {
            return boolValue ? paramStr : string.Empty;
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts trigger type to visibility.
/// </summary>
public class ExpressionMapTriggerTypeToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive)
        {
            return isActive ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts note number to string.
/// </summary>
public class ExpressionMapNoteToStringConverter : IValueConverter
{
    private static readonly string[] NoteNames = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int note && note >= 0 && note <= 127)
        {
            var noteName = NoteNames[note % 12];
            var octave = (note / 12) - 2;
            return $"{noteName}{octave}";
        }
        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts articulation type to brush color.
/// </summary>
public class ExpressionMapArticulationTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ArticulationType type)
        {
            return type switch
            {
                ArticulationType.Sustain => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),
                ArticulationType.Staccato => new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
                ArticulationType.Legato => new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81)),
                ArticulationType.Pizzicato => new SolidColorBrush(Color.FromRgb(0xEC, 0x48, 0x99)),
                ArticulationType.Tremolo => new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)),
                ArticulationType.Trill => new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1)),
                ArticulationType.Marcato => new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)),
                ArticulationType.Tenuto => new SolidColorBrush(Color.FromRgb(0x14, 0xB8, 0xA6)),
                ArticulationType.Spiccato => new SolidColorBrush(Color.FromRgb(0x84, 0xCC, 0x16)),
                ArticulationType.Col_Legno => new SolidColorBrush(Color.FromRgb(0xA8, 0x55, 0xF7)),
                _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
            };
        }
        return new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

#endregion
