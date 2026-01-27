// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Modulation Matrix control.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Modulation;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Represents a modulation source in the matrix UI.
/// </summary>
public partial class ModulationSourceViewModel : ObservableObject
{
    /// <summary>
    /// The underlying modulation source.
    /// </summary>
    public ModulationSource Source { get; }

    /// <summary>
    /// Display name for the source.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    /// Type of the source for icon/display purposes.
    /// </summary>
    [ObservableProperty]
    private ModulationSourceType _sourceType;

    /// <summary>
    /// Current value of the source (0-1 or -1 to 1).
    /// </summary>
    [ObservableProperty]
    private float _currentValue;

    /// <summary>
    /// Whether the source is bipolar.
    /// </summary>
    [ObservableProperty]
    private bool _isBipolar;

    public ModulationSourceViewModel(ModulationSource source)
    {
        Source = source;
        _name = source.Name;
        _sourceType = source.Type;
        _isBipolar = source.IsBipolar;
        _currentValue = source.Value;
    }

    /// <summary>
    /// Updates the current value from the source.
    /// </summary>
    public void UpdateValue()
    {
        CurrentValue = Source.Value;
    }
}

/// <summary>
/// Represents a modulation destination (parameter) in the matrix UI.
/// </summary>
public partial class ModulationDestinationViewModel : ObservableObject
{
    /// <summary>
    /// The parameter path/name.
    /// </summary>
    [ObservableProperty]
    private string _parameterPath;

    /// <summary>
    /// Display name for the destination.
    /// </summary>
    [ObservableProperty]
    private string _displayName;

    /// <summary>
    /// Category/group of the parameter.
    /// </summary>
    [ObservableProperty]
    private string _category;

    public ModulationDestinationViewModel(string parameterPath, string displayName, string category = "")
    {
        _parameterPath = parameterPath;
        _displayName = displayName;
        _category = category;
    }
}

/// <summary>
/// Represents a single cell in the modulation matrix.
/// </summary>
public partial class ModulationCellViewModel : ObservableObject
{
    /// <summary>
    /// The source for this cell.
    /// </summary>
    public ModulationSourceViewModel Source { get; }

    /// <summary>
    /// The destination for this cell.
    /// </summary>
    public ModulationDestinationViewModel Destination { get; }

    /// <summary>
    /// The underlying modulation slot, or null if no routing exists.
    /// </summary>
    [ObservableProperty]
    private ModulationSlot? _slot;

    /// <summary>
    /// Modulation amount (-1 to 1, displayed as -100% to +100%).
    /// </summary>
    [ObservableProperty]
    private float _amount;

    /// <summary>
    /// Whether this cell has an active routing.
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Whether this cell is currently selected for editing.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether this cell is being hovered.
    /// </summary>
    [ObservableProperty]
    private bool _isHovered;

    /// <summary>
    /// Row index in the matrix.
    /// </summary>
    public int RowIndex { get; }

    /// <summary>
    /// Column index in the matrix.
    /// </summary>
    public int ColumnIndex { get; }

    public ModulationCellViewModel(
        ModulationSourceViewModel source,
        ModulationDestinationViewModel destination,
        int rowIndex,
        int columnIndex)
    {
        Source = source;
        Destination = destination;
        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
    }

    /// <summary>
    /// Sets the modulation amount and updates the slot.
    /// </summary>
    public void SetAmount(float amount, ModulationMatrix matrix)
    {
        Amount = Math.Clamp(amount, -1f, 1f);

        if (Math.Abs(Amount) < 0.001f)
        {
            // Remove routing
            if (Slot != null)
            {
                matrix.RemoveModulation(Slot);
                Slot = null;
                IsActive = false;
            }
        }
        else
        {
            // Add or update routing
            if (Slot == null)
            {
                Slot = matrix.AddModulation(Source.Source, Destination.ParameterPath, Amount);
                IsActive = true;
            }
            else
            {
                Slot.Amount = Amount;
            }
        }
    }

    /// <summary>
    /// Clears the routing for this cell.
    /// </summary>
    public void Clear(ModulationMatrix matrix)
    {
        if (Slot != null)
        {
            matrix.RemoveModulation(Slot);
            Slot = null;
        }
        Amount = 0;
        IsActive = false;
    }

    partial void OnAmountChanged(float value)
    {
        IsActive = Math.Abs(value) > 0.001f;
    }
}

/// <summary>
/// Data for clipboard copy/paste of modulation routings.
/// </summary>
public class ModulationClipboardData
{
    public string SourceName { get; set; } = string.Empty;
    public ModulationSourceType SourceType { get; set; }
    public string DestinationPath { get; set; } = string.Empty;
    public float Amount { get; set; }
}

/// <summary>
/// ViewModel for the Modulation Matrix control.
/// Manages the grid of modulation routings between sources and destinations.
/// </summary>
public partial class ModulationMatrixViewModel : ViewModelBase
{
    #region Private Fields

    private ModulationMatrix? _modulationMatrix;
    private ModulationClipboardData? _clipboardData;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collection of modulation sources (rows).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModulationSourceViewModel> _sources = new();

    /// <summary>
    /// Collection of modulation destinations (columns).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModulationDestinationViewModel> _destinations = new();

    /// <summary>
    /// 2D grid of modulation cells.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<ModulationCellViewModel> _cells = new();

    /// <summary>
    /// Currently selected cell.
    /// </summary>
    [ObservableProperty]
    private ModulationCellViewModel? _selectedCell;

    /// <summary>
    /// Whether the matrix is in edit mode.
    /// </summary>
    [ObservableProperty]
    private bool _isEditMode;

    /// <summary>
    /// Current amount being edited (for display).
    /// </summary>
    [ObservableProperty]
    private float _editAmount;

    /// <summary>
    /// Total number of active routings.
    /// </summary>
    [ObservableProperty]
    private int _activeRoutingCount;

    /// <summary>
    /// Whether clipboard has data for paste.
    /// </summary>
    [ObservableProperty]
    private bool _hasClipboardData;

    /// <summary>
    /// Number of columns (destinations).
    /// </summary>
    [ObservableProperty]
    private int _columnCount;

    /// <summary>
    /// Number of rows (sources).
    /// </summary>
    [ObservableProperty]
    private int _rowCount;

    #endregion

    #region Constructor

    public ModulationMatrixViewModel()
    {
        InitializeDefaultSourcesAndDestinations();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Sets the modulation matrix instance to work with.
    /// </summary>
    public void SetModulationMatrix(ModulationMatrix matrix)
    {
        _modulationMatrix = matrix;
        LoadFromMatrix();
    }

    private void InitializeDefaultSourcesAndDestinations()
    {
        // Add default sources
        AddDefaultSources();

        // Add common destinations
        AddDefaultDestinations();

        // Build the cell grid
        RebuildCellGrid();
    }

    private void AddDefaultSources()
    {
        // Create placeholder sources for UI display
        // These will be replaced when a real ModulationMatrix is set

        var lfo1 = new ModulationSource(ModulationSourceType.LFO) { Name = "LFO 1" };
        var lfo2 = new ModulationSource(ModulationSourceType.LFO) { Name = "LFO 2" };
        var env1 = new ModulationSource(ModulationSourceType.Envelope) { Name = "Env 1" };
        var env2 = new ModulationSource(ModulationSourceType.Envelope) { Name = "Env 2" };
        var velocity = new ModulationSource(ModulationSourceType.Velocity) { Name = "Velocity" };
        var aftertouch = new ModulationSource(ModulationSourceType.Aftertouch) { Name = "Aftertouch" };
        var modWheel = new ModulationSource(ModulationSourceType.ModWheel) { Name = "Mod Wheel" };
        var pitchBend = new ModulationSource(ModulationSourceType.PitchBend) { Name = "Pitch Bend" };

        Sources.Add(new ModulationSourceViewModel(lfo1));
        Sources.Add(new ModulationSourceViewModel(lfo2));
        Sources.Add(new ModulationSourceViewModel(env1));
        Sources.Add(new ModulationSourceViewModel(env2));
        Sources.Add(new ModulationSourceViewModel(velocity));
        Sources.Add(new ModulationSourceViewModel(aftertouch));
        Sources.Add(new ModulationSourceViewModel(modWheel));
        Sources.Add(new ModulationSourceViewModel(pitchBend));

        RowCount = Sources.Count;
    }

    private void AddDefaultDestinations()
    {
        // Common synthesizer parameters
        Destinations.Add(new ModulationDestinationViewModel("Filter.Cutoff", "Cutoff", "Filter"));
        Destinations.Add(new ModulationDestinationViewModel("Filter.Resonance", "Resonance", "Filter"));
        Destinations.Add(new ModulationDestinationViewModel("Oscillator1.Pitch", "Osc1 Pitch", "Oscillator"));
        Destinations.Add(new ModulationDestinationViewModel("Oscillator2.Pitch", "Osc2 Pitch", "Oscillator"));
        Destinations.Add(new ModulationDestinationViewModel("Oscillator1.PulseWidth", "Osc1 PW", "Oscillator"));
        Destinations.Add(new ModulationDestinationViewModel("Oscillator2.PulseWidth", "Osc2 PW", "Oscillator"));
        Destinations.Add(new ModulationDestinationViewModel("Amplifier.Level", "Amp Level", "Amplifier"));
        Destinations.Add(new ModulationDestinationViewModel("Amplifier.Pan", "Pan", "Amplifier"));
        Destinations.Add(new ModulationDestinationViewModel("LFO1.Rate", "LFO1 Rate", "LFO"));
        Destinations.Add(new ModulationDestinationViewModel("LFO2.Rate", "LFO2 Rate", "LFO"));

        ColumnCount = Destinations.Count;
    }

    private void RebuildCellGrid()
    {
        Cells.Clear();

        for (int row = 0; row < Sources.Count; row++)
        {
            for (int col = 0; col < Destinations.Count; col++)
            {
                var cell = new ModulationCellViewModel(
                    Sources[row],
                    Destinations[col],
                    row,
                    col);

                Cells.Add(cell);
            }
        }

        UpdateActiveRoutingCount();
    }

    private void LoadFromMatrix()
    {
        if (_modulationMatrix == null) return;

        // Clear existing sources and load from matrix
        Sources.Clear();

        foreach (var source in _modulationMatrix.Sources)
        {
            Sources.Add(new ModulationSourceViewModel(source));
        }

        RowCount = Sources.Count;

        // Rebuild grid with actual sources
        RebuildCellGrid();

        // Load existing slots into cells
        foreach (var slot in _modulationMatrix.Slots)
        {
            var cell = FindCell(slot.Source, slot.TargetParameter);
            if (cell != null)
            {
                cell.Slot = slot;
                cell.Amount = slot.Amount;
            }
        }

        UpdateActiveRoutingCount();
    }

    private ModulationCellViewModel? FindCell(ModulationSource source, string targetParameter)
    {
        var sourceVm = Sources.FirstOrDefault(s => s.Source == source);
        var destVm = Destinations.FirstOrDefault(d => d.ParameterPath == targetParameter);

        if (sourceVm == null || destVm == null) return null;

        return Cells.FirstOrDefault(c =>
            c.Source == sourceVm && c.Destination == destVm);
    }

    private ModulationCellViewModel? GetCell(int row, int col)
    {
        if (row < 0 || row >= RowCount || col < 0 || col >= ColumnCount)
            return null;

        int index = row * ColumnCount + col;
        if (index >= 0 && index < Cells.Count)
            return Cells[index];

        return null;
    }

    #endregion

    #region Commands

    /// <summary>
    /// Selects a cell for editing.
    /// </summary>
    [RelayCommand]
    private void SelectCell(ModulationCellViewModel? cell)
    {
        // Deselect previous
        if (SelectedCell != null)
        {
            SelectedCell.IsSelected = false;
        }

        SelectedCell = cell;

        if (cell != null)
        {
            cell.IsSelected = true;
            EditAmount = cell.Amount;
            IsEditMode = true;
        }
        else
        {
            IsEditMode = false;
        }
    }

    /// <summary>
    /// Sets the amount for the currently selected cell.
    /// </summary>
    [RelayCommand]
    private void SetCellAmount(float amount)
    {
        if (SelectedCell == null || _modulationMatrix == null) return;

        SelectedCell.SetAmount(amount, _modulationMatrix);
        EditAmount = SelectedCell.Amount;
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Increments the amount for the selected cell.
    /// </summary>
    [RelayCommand]
    private void IncrementAmount()
    {
        if (SelectedCell == null || _modulationMatrix == null) return;

        float newAmount = Math.Min(SelectedCell.Amount + 0.05f, 1f);
        SelectedCell.SetAmount(newAmount, _modulationMatrix);
        EditAmount = SelectedCell.Amount;
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Decrements the amount for the selected cell.
    /// </summary>
    [RelayCommand]
    private void DecrementAmount()
    {
        if (SelectedCell == null || _modulationMatrix == null) return;

        float newAmount = Math.Max(SelectedCell.Amount - 0.05f, -1f);
        SelectedCell.SetAmount(newAmount, _modulationMatrix);
        EditAmount = SelectedCell.Amount;
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Clears the routing for the selected cell.
    /// </summary>
    [RelayCommand]
    private void ClearSelectedRouting()
    {
        if (SelectedCell == null || _modulationMatrix == null) return;

        SelectedCell.Clear(_modulationMatrix);
        EditAmount = 0;
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Clears all routings in the matrix.
    /// </summary>
    [RelayCommand]
    private void ClearAllRoutings()
    {
        if (_modulationMatrix == null) return;

        foreach (var cell in Cells)
        {
            cell.Clear(_modulationMatrix);
        }

        EditAmount = 0;
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Copies the selected cell's routing to clipboard.
    /// </summary>
    [RelayCommand]
    private void CopyRouting()
    {
        if (SelectedCell == null || !SelectedCell.IsActive) return;

        _clipboardData = new ModulationClipboardData
        {
            SourceName = SelectedCell.Source.Name,
            SourceType = SelectedCell.Source.SourceType,
            DestinationPath = SelectedCell.Destination.ParameterPath,
            Amount = SelectedCell.Amount
        };

        HasClipboardData = true;

        // Also copy to system clipboard as JSON
        try
        {
            var json = JsonSerializer.Serialize(_clipboardData);
            Clipboard.SetText(json);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    /// <summary>
    /// Pastes the clipboard routing to the selected cell.
    /// </summary>
    [RelayCommand]
    private void PasteRouting()
    {
        if (SelectedCell == null || _modulationMatrix == null) return;

        // Try to get from internal clipboard first
        if (_clipboardData != null)
        {
            SelectedCell.SetAmount(_clipboardData.Amount, _modulationMatrix);
            EditAmount = SelectedCell.Amount;
            UpdateActiveRoutingCount();
            return;
        }

        // Try to parse from system clipboard
        try
        {
            var json = Clipboard.GetText();
            if (!string.IsNullOrEmpty(json))
            {
                var data = JsonSerializer.Deserialize<ModulationClipboardData>(json);
                if (data != null)
                {
                    SelectedCell.SetAmount(data.Amount, _modulationMatrix);
                    EditAmount = SelectedCell.Amount;
                    UpdateActiveRoutingCount();
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }
    }

    /// <summary>
    /// Copies an entire row (source) of routings.
    /// </summary>
    [RelayCommand]
    private void CopyRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount) return;

        // Store all routings for this row
        var rowData = new List<ModulationClipboardData>();

        for (int col = 0; col < ColumnCount; col++)
        {
            var cell = GetCell(rowIndex, col);
            if (cell != null && cell.IsActive)
            {
                rowData.Add(new ModulationClipboardData
                {
                    SourceName = cell.Source.Name,
                    SourceType = cell.Source.SourceType,
                    DestinationPath = cell.Destination.ParameterPath,
                    Amount = cell.Amount
                });
            }
        }

        if (rowData.Count > 0)
        {
            try
            {
                var json = JsonSerializer.Serialize(rowData);
                Clipboard.SetText(json);
                HasClipboardData = true;
            }
            catch
            {
                // Ignore clipboard errors
            }
        }
    }

    /// <summary>
    /// Pastes routings to an entire row.
    /// </summary>
    [RelayCommand]
    private void PasteRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount || _modulationMatrix == null) return;

        try
        {
            var json = Clipboard.GetText();
            if (string.IsNullOrEmpty(json)) return;

            var rowData = JsonSerializer.Deserialize<List<ModulationClipboardData>>(json);
            if (rowData == null) return;

            foreach (var data in rowData)
            {
                // Find destination column
                var destVm = Destinations.FirstOrDefault(d => d.ParameterPath == data.DestinationPath);
                if (destVm == null) continue;

                int colIndex = Destinations.IndexOf(destVm);
                var cell = GetCell(rowIndex, colIndex);
                if (cell != null)
                {
                    cell.SetAmount(data.Amount, _modulationMatrix);
                }
            }

            UpdateActiveRoutingCount();
        }
        catch
        {
            // Ignore parse errors
        }
    }

    /// <summary>
    /// Clears an entire row of routings.
    /// </summary>
    [RelayCommand]
    private void ClearRow(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= RowCount || _modulationMatrix == null) return;

        for (int col = 0; col < ColumnCount; col++)
        {
            var cell = GetCell(rowIndex, col);
            cell?.Clear(_modulationMatrix);
        }

        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Clears an entire column of routings.
    /// </summary>
    [RelayCommand]
    private void ClearColumn(int colIndex)
    {
        if (colIndex < 0 || colIndex >= ColumnCount || _modulationMatrix == null) return;

        for (int row = 0; row < RowCount; row++)
        {
            var cell = GetCell(row, colIndex);
            cell?.Clear(_modulationMatrix);
        }

        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Adds a custom destination parameter.
    /// </summary>
    [RelayCommand]
    private void AddDestination(string parameterPath)
    {
        if (string.IsNullOrWhiteSpace(parameterPath)) return;
        if (Destinations.Any(d => d.ParameterPath == parameterPath)) return;

        // Parse display name from path
        var parts = parameterPath.Split('.');
        var displayName = parts.Length > 0 ? parts[^1] : parameterPath;
        var category = parts.Length > 1 ? parts[0] : "";

        Destinations.Add(new ModulationDestinationViewModel(parameterPath, displayName, category));
        ColumnCount = Destinations.Count;
        RebuildCellGrid();
    }

    /// <summary>
    /// Removes a destination from the matrix.
    /// </summary>
    [RelayCommand]
    private void RemoveDestination(ModulationDestinationViewModel destination)
    {
        if (destination == null) return;

        // Clear all routings to this destination first
        if (_modulationMatrix != null)
        {
            var colIndex = Destinations.IndexOf(destination);
            if (colIndex >= 0)
            {
                ClearColumn(colIndex);
            }
        }

        Destinations.Remove(destination);
        ColumnCount = Destinations.Count;
        RebuildCellGrid();
    }

    /// <summary>
    /// Updates source values for real-time display.
    /// </summary>
    [RelayCommand]
    private void UpdateSourceValues()
    {
        foreach (var source in Sources)
        {
            source.UpdateValue();
        }
    }

    /// <summary>
    /// Exits edit mode.
    /// </summary>
    [RelayCommand]
    private void ExitEditMode()
    {
        if (SelectedCell != null)
        {
            SelectedCell.IsSelected = false;
        }
        SelectedCell = null;
        IsEditMode = false;
    }

    #endregion

    #region Helper Methods

    private void UpdateActiveRoutingCount()
    {
        ActiveRoutingCount = Cells.Count(c => c.IsActive);
    }

    /// <summary>
    /// Gets the amount for a cell at the specified position.
    /// </summary>
    public float GetCellAmount(int row, int col)
    {
        var cell = GetCell(row, col);
        return cell?.Amount ?? 0f;
    }

    /// <summary>
    /// Sets the amount for a cell at the specified position.
    /// </summary>
    public void SetCellAmount(int row, int col, float amount)
    {
        if (_modulationMatrix == null) return;

        var cell = GetCell(row, col);
        cell?.SetAmount(amount, _modulationMatrix);
        UpdateActiveRoutingCount();
    }

    /// <summary>
    /// Formats the amount as a percentage string.
    /// </summary>
    public static string FormatAmount(float amount)
    {
        int percent = (int)Math.Round(amount * 100);
        if (percent >= 0)
        {
            return $"+{percent}%";
        }
        return $"{percent}%";
    }

    #endregion
}
