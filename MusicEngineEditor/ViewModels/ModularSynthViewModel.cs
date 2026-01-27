// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Modular Synth Patch Editor.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;
using MusicEngine.Core.Synthesizers.Modular;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Defines the available module types that can be added to the modular synth.
/// </summary>
public enum ModuleType
{
    VCO,
    VCF,
    VCA,
    LFO,
    ADSR,
    Mixer,
    Output,
    Noise,
    Delay,
    Quantizer,
    SampleAndHold,
    SlewLimiter,
    Sequencer,
    Clock,
    Logic,
    Multiply,
    Utility
}

/// <summary>
/// Represents a module in the visual patch editor.
/// </summary>
public partial class ModuleViewModel : ObservableObject
{
    private ModuleBase _module;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private ModuleType _moduleType;

    [ObservableProperty]
    private double _x;

    [ObservableProperty]
    private double _y;

    [ObservableProperty]
    private double _width = 120;

    [ObservableProperty]
    private double _height = 180;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the underlying module.
    /// </summary>
    public ModuleBase Module => _module;

    /// <summary>
    /// Gets the module's unique identifier.
    /// </summary>
    public Guid Id => _module.Id;

    /// <summary>
    /// Gets the input ports.
    /// </summary>
    public ObservableCollection<PortViewModel> Inputs { get; } = new();

    /// <summary>
    /// Gets the output ports.
    /// </summary>
    public ObservableCollection<PortViewModel> Outputs { get; } = new();

    /// <summary>
    /// Gets the parameters.
    /// </summary>
    public ObservableCollection<ParameterViewModel> Parameters { get; } = new();

    public ModuleViewModel(ModuleBase module, ModuleType type, double x = 0, double y = 0)
    {
        _module = module ?? throw new ArgumentNullException(nameof(module));
        _name = module.Name;
        _moduleType = type;
        _x = x;
        _y = y;

        // Initialize ports
        foreach (var input in module.Inputs)
        {
            Inputs.Add(new PortViewModel(input, this));
        }

        foreach (var output in module.Outputs)
        {
            Outputs.Add(new PortViewModel(output, this));
        }

        // Initialize parameters
        foreach (var paramInfo in module.ParameterInfos)
        {
            Parameters.Add(new ParameterViewModel(paramInfo.Key, paramInfo.Value, module));
        }

        // Set size based on module type
        UpdateSize();
    }

    private void UpdateSize()
    {
        int portCount = Math.Max(Inputs.Count, Outputs.Count);
        int paramCount = Parameters.Count;

        // Header + ports + parameters + padding
        Height = 40 + (portCount * 24) + (paramCount * 28) + 20;
        Width = _moduleType switch
        {
            ModuleType.Mixer => 160,
            ModuleType.Sequencer => 180,
            _ => 120
        };
    }
}

/// <summary>
/// Represents a port (input or output) on a module.
/// </summary>
public partial class PortViewModel : ObservableObject
{
    private readonly ModulePort _port;
    private readonly ModuleViewModel _owner;

    [ObservableProperty]
    private bool _isConnected;

    /// <summary>
    /// Gets the port name.
    /// </summary>
    public string Name => _port.Name;

    /// <summary>
    /// Gets the port type.
    /// </summary>
    public PortType Type => _port.Type;

    /// <summary>
    /// Gets the port direction.
    /// </summary>
    public PortDirection Direction => _port.Direction;

    /// <summary>
    /// Gets whether this is an input port.
    /// </summary>
    public bool IsInput => Direction == PortDirection.Input;

    /// <summary>
    /// Gets the underlying port.
    /// </summary>
    public ModulePort Port => _port;

    /// <summary>
    /// Gets the owning module view model.
    /// </summary>
    public ModuleViewModel Owner => _owner;

    /// <summary>
    /// Gets the port color based on signal type.
    /// </summary>
    public Brush PortColor => Type switch
    {
        PortType.Audio => new SolidColorBrush(Color.FromRgb(0x00, 0xD9, 0xFF)),    // Cyan for audio
        PortType.Control => new SolidColorBrush(Color.FromRgb(0xFF, 0xA5, 0x00)),   // Orange for CV
        PortType.Gate => new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88)),      // Green for gate
        PortType.Trigger => new SolidColorBrush(Color.FromRgb(0xFF, 0x6B, 0x6B)),   // Red for trigger
        _ => new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80))
    };

    public PortViewModel(ModulePort port, ModuleViewModel owner)
    {
        _port = port ?? throw new ArgumentNullException(nameof(port));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _isConnected = port.IsConnected;
    }

    /// <summary>
    /// Updates the connection state.
    /// </summary>
    public void UpdateConnectionState()
    {
        IsConnected = _port.IsConnected;
    }
}

/// <summary>
/// Represents a parameter on a module.
/// </summary>
public partial class ParameterViewModel : ObservableObject
{
    private readonly string _name;
    private readonly ParameterInfo _info;
    private readonly ModuleBase _module;

    [ObservableProperty]
    private float _value;

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the minimum value.
    /// </summary>
    public float MinValue => _info.MinValue;

    /// <summary>
    /// Gets the maximum value.
    /// </summary>
    public float MaxValue => _info.MaxValue;

    /// <summary>
    /// Gets the default value.
    /// </summary>
    public float DefaultValue => _info.DefaultValue;

    public ParameterViewModel(string name, ParameterInfo info, ModuleBase module)
    {
        _name = name;
        _info = info;
        _module = module;
        _value = module.GetParameter(name);
    }

    partial void OnValueChanged(float value)
    {
        _module.SetParameter(_name, value);
    }
}

/// <summary>
/// Represents a cable connection between two ports.
/// </summary>
public partial class CableViewModel : ObservableObject
{
    private readonly Cable _cable;
    private PortViewModel _sourcePort;
    private PortViewModel _destinationPort;

    [ObservableProperty]
    private Point _startPoint;

    [ObservableProperty]
    private Point _endPoint;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the underlying cable.
    /// </summary>
    public Cable Cable => _cable;

    /// <summary>
    /// Gets the cable's unique identifier.
    /// </summary>
    public Guid Id => _cable.Id;

    /// <summary>
    /// Gets the source port view model.
    /// </summary>
    public PortViewModel SourcePort => _sourcePort;

    /// <summary>
    /// Gets the destination port view model.
    /// </summary>
    public PortViewModel DestinationPort => _destinationPort;

    /// <summary>
    /// Gets the cable color as a Color value.
    /// </summary>
    public Color CableColorValue => _cable.Source.Type switch
    {
        PortType.Audio => Color.FromRgb(0x00, 0xD9, 0xFF),    // Cyan for audio
        PortType.Control => Color.FromRgb(0xFF, 0xA5, 0x00),   // Orange for CV
        PortType.Gate => Color.FromRgb(0x00, 0xFF, 0x88),      // Green for gate
        PortType.Trigger => Color.FromRgb(0xFF, 0x6B, 0x6B),   // Red for trigger
        _ => Color.FromRgb(0x80, 0x80, 0x80)
    };

    /// <summary>
    /// Gets the cable color based on signal type.
    /// </summary>
    public Brush CableColor => new SolidColorBrush(CableColorValue);

    public CableViewModel(Cable cable, PortViewModel sourcePort, PortViewModel destinationPort)
    {
        _cable = cable ?? throw new ArgumentNullException(nameof(cable));
        _sourcePort = sourcePort ?? throw new ArgumentNullException(nameof(sourcePort));
        _destinationPort = destinationPort ?? throw new ArgumentNullException(nameof(destinationPort));
    }

    /// <summary>
    /// Updates the cable endpoints based on module positions.
    /// </summary>
    public void UpdateEndpoints(Point start, Point end)
    {
        StartPoint = start;
        EndPoint = end;
    }
}

/// <summary>
/// ViewModel for the Modular Synth Patch Editor.
/// </summary>
public partial class ModularSynthViewModel : ViewModelBase, IDisposable
{
    private ModularSynth? _synth;
    private bool _disposed;
    private PortViewModel? _pendingConnectionSource;

    [ObservableProperty]
    private double _canvasWidth = 2000;

    [ObservableProperty]
    private double _canvasHeight = 1500;

    [ObservableProperty]
    private double _zoomLevel = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private ModuleViewModel? _selectedModule;

    [ObservableProperty]
    private CableViewModel? _selectedCable;

    [ObservableProperty]
    private bool _isConnecting;

    [ObservableProperty]
    private Point _connectionPreviewEnd;

    /// <summary>
    /// Gets the collection of modules.
    /// </summary>
    public ObservableCollection<ModuleViewModel> Modules { get; } = new();

    /// <summary>
    /// Gets the collection of cables.
    /// </summary>
    public ObservableCollection<CableViewModel> Cables { get; } = new();

    /// <summary>
    /// Gets the available module types.
    /// </summary>
    public IReadOnlyList<ModuleType> AvailableModuleTypes { get; } = Enum.GetValues<ModuleType>().ToList();

    /// <summary>
    /// Event raised when a module is added.
    /// </summary>
    public event EventHandler<ModuleViewModel>? ModuleAdded;

    /// <summary>
    /// Event raised when a module is removed.
    /// </summary>
    public event EventHandler<ModuleViewModel>? ModuleRemoved;

    /// <summary>
    /// Event raised when a cable is added.
    /// </summary>
    public event EventHandler<CableViewModel>? CableAdded;

    /// <summary>
    /// Event raised when a cable is removed.
    /// </summary>
    public event EventHandler<CableViewModel>? CableRemoved;

    /// <summary>
    /// Gets the pending connection source port.
    /// </summary>
    public PortViewModel? PendingConnectionSource => _pendingConnectionSource;

    public ModularSynthViewModel()
    {
        // Design-time constructor
    }

    public ModularSynthViewModel(ModularSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes with a new ModularSynth instance.
    /// </summary>
    public void Initialize(int sampleRate = 44100, int bufferSize = 1024)
    {
        _synth?.Dispose();
        _synth = new ModularSynth(sampleRate, bufferSize);
        LoadFromSynth();
    }

    /// <summary>
    /// Loads the current state from the synth.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        Modules.Clear();
        Cables.Clear();

        // Create view models for existing modules
        double x = 50;
        double y = 50;
        var moduleViewModels = new Dictionary<Guid, ModuleViewModel>();

        foreach (var module in _synth.Modules)
        {
            var type = GetModuleType(module);
            var vm = new ModuleViewModel(module, type, x, y);
            Modules.Add(vm);
            moduleViewModels[module.Id] = vm;

            // Arrange modules in a grid
            x += 150;
            if (x > 600)
            {
                x = 50;
                y += 220;
            }
        }

        // Create view models for existing cables
        foreach (var cable in _synth.Cables)
        {
            var sourceModule = moduleViewModels.Values.FirstOrDefault(m => m.Module == cable.Source.Owner);
            var destModule = moduleViewModels.Values.FirstOrDefault(m => m.Module == cable.Destination.Owner);

            if (sourceModule != null && destModule != null)
            {
                var sourcePort = sourceModule.Outputs.FirstOrDefault(p => p.Port == cable.Source);
                var destPort = destModule.Inputs.FirstOrDefault(p => p.Port == cable.Destination);

                if (sourcePort != null && destPort != null)
                {
                    var cableVm = new CableViewModel(cable, sourcePort, destPort);
                    Cables.Add(cableVm);
                    sourcePort.UpdateConnectionState();
                    destPort.UpdateConnectionState();
                }
            }
        }
    }

    private static ModuleType GetModuleType(ModuleBase module)
    {
        return module switch
        {
            VCOModule => ModuleType.VCO,
            VCFModule => ModuleType.VCF,
            VCAModule => ModuleType.VCA,
            LFOModule => ModuleType.LFO,
            ADSRModule => ModuleType.ADSR,
            MixerModule => ModuleType.Mixer,
            OutputModule => ModuleType.Output,
            NoiseModule => ModuleType.Noise,
            DelayModule => ModuleType.Delay,
            QuantizerModule => ModuleType.Quantizer,
            SampleAndHoldModule => ModuleType.SampleAndHold,
            SlewLimiterModule => ModuleType.SlewLimiter,
            SequencerModule => ModuleType.Sequencer,
            ClockModule => ModuleType.Clock,
            LogicModule => ModuleType.Logic,
            MultiplyModule => ModuleType.Multiply,
            UtilityModule => ModuleType.Utility,
            _ => ModuleType.VCO
        };
    }

    [RelayCommand]
    private void AddModule(ModuleType type)
    {
        AddModuleAt(type, 100 + Modules.Count * 20, 100 + Modules.Count * 20);
    }

    /// <summary>
    /// Adds a module at the specified position.
    /// </summary>
    public void AddModuleAt(ModuleType type, double x, double y)
    {
        if (_synth == null)
        {
            Initialize();
        }

        ModuleBase module = type switch
        {
            ModuleType.VCO => _synth!.AddModule<VCOModule>(),
            ModuleType.VCF => _synth!.AddModule<VCFModule>(),
            ModuleType.VCA => _synth!.AddModule<VCAModule>(),
            ModuleType.LFO => _synth!.AddModule<LFOModule>(),
            ModuleType.ADSR => _synth!.AddModule<ADSRModule>(),
            ModuleType.Mixer => _synth!.AddModule<MixerModule>(),
            ModuleType.Output => _synth!.AddModule<OutputModule>(),
            ModuleType.Noise => _synth!.AddModule<NoiseModule>(),
            ModuleType.Delay => _synth!.AddModule<DelayModule>(),
            ModuleType.Quantizer => _synth!.AddModule<QuantizerModule>(),
            ModuleType.SampleAndHold => _synth!.AddModule<SampleAndHoldModule>(),
            ModuleType.SlewLimiter => _synth!.AddModule<SlewLimiterModule>(),
            ModuleType.Sequencer => _synth!.AddModule<SequencerModule>(),
            ModuleType.Clock => _synth!.AddModule<ClockModule>(),
            ModuleType.Logic => _synth!.AddModule<LogicModule>(),
            ModuleType.Multiply => _synth!.AddModule<MultiplyModule>(),
            ModuleType.Utility => _synth!.AddModule<UtilityModule>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var vm = new ModuleViewModel(module, type, x, y);
        Modules.Add(vm);
        SelectedModule = vm;
        StatusMessage = $"Added {type} module";
        ModuleAdded?.Invoke(this, vm);
    }

    [RelayCommand]
    private void DeleteSelectedModule()
    {
        if (SelectedModule == null || _synth == null)
            return;

        DeleteModule(SelectedModule);
    }

    /// <summary>
    /// Deletes a module and its connections.
    /// </summary>
    public void DeleteModule(ModuleViewModel moduleVm)
    {
        if (_synth == null) return;

        // Remove all cables connected to this module
        var cablesToRemove = Cables.Where(c =>
            c.SourcePort.Owner == moduleVm ||
            c.DestinationPort.Owner == moduleVm).ToList();

        foreach (var cable in cablesToRemove)
        {
            _synth.Disconnect(cable.Cable);
            Cables.Remove(cable);
            cable.SourcePort.UpdateConnectionState();
            cable.DestinationPort.UpdateConnectionState();
            CableRemoved?.Invoke(this, cable);
        }

        // Remove the module
        _synth.RemoveModule(moduleVm.Module);
        Modules.Remove(moduleVm);

        if (SelectedModule == moduleVm)
        {
            SelectedModule = null;
        }

        StatusMessage = $"Deleted {moduleVm.Name} module";
        ModuleRemoved?.Invoke(this, moduleVm);
    }

    [RelayCommand]
    private void DeleteSelectedCable()
    {
        if (SelectedCable == null || _synth == null)
            return;

        DeleteCable(SelectedCable);
    }

    /// <summary>
    /// Deletes a cable.
    /// </summary>
    public void DeleteCable(CableViewModel cableVm)
    {
        if (_synth == null) return;

        _synth.Disconnect(cableVm.Cable);
        Cables.Remove(cableVm);
        cableVm.SourcePort.UpdateConnectionState();
        cableVm.DestinationPort.UpdateConnectionState();

        if (SelectedCable == cableVm)
        {
            SelectedCable = null;
        }

        StatusMessage = "Deleted cable";
        CableRemoved?.Invoke(this, cableVm);
    }

    /// <summary>
    /// Starts a new connection from a port.
    /// </summary>
    public void StartConnection(PortViewModel port)
    {
        _pendingConnectionSource = port;
        IsConnecting = true;
        StatusMessage = $"Connecting from {port.Owner.Name}.{port.Name}...";
    }

    /// <summary>
    /// Cancels the pending connection.
    /// </summary>
    [RelayCommand]
    private void CancelConnection()
    {
        _pendingConnectionSource = null;
        IsConnecting = false;
        StatusMessage = "";
    }

    /// <summary>
    /// Completes a connection to the target port.
    /// </summary>
    public bool CompleteConnection(PortViewModel targetPort)
    {
        if (_pendingConnectionSource == null || _synth == null || targetPort == null)
        {
            CancelConnection();
            return false;
        }

        // Validate connection
        if (_pendingConnectionSource.Owner == targetPort.Owner)
        {
            StatusMessage = "Cannot connect ports on the same module";
            CancelConnection();
            return false;
        }

        if (_pendingConnectionSource.Direction == targetPort.Direction)
        {
            StatusMessage = "Cannot connect two inputs or two outputs";
            CancelConnection();
            return false;
        }

        // Determine source and destination
        PortViewModel sourcePort, destPort;
        if (_pendingConnectionSource.Direction == PortDirection.Output)
        {
            sourcePort = _pendingConnectionSource;
            destPort = targetPort;
        }
        else
        {
            sourcePort = targetPort;
            destPort = _pendingConnectionSource;
        }

        try
        {
            // Create the connection
            var cable = _synth.Connect(sourcePort.Port, destPort.Port);
            var cableVm = new CableViewModel(cable, sourcePort, destPort);
            Cables.Add(cableVm);

            sourcePort.UpdateConnectionState();
            destPort.UpdateConnectionState();

            StatusMessage = $"Connected {sourcePort.Owner.Name}.{sourcePort.Name} to {destPort.Owner.Name}.{destPort.Name}";
            CableAdded?.Invoke(this, cableVm);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            CancelConnection();
            return false;
        }

        CancelConnection();
        return true;
    }

    /// <summary>
    /// Updates the connection preview endpoint.
    /// </summary>
    public void UpdateConnectionPreview(Point endPoint)
    {
        if (IsConnecting)
        {
            ConnectionPreviewEnd = endPoint;
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (_synth == null) return;

        _synth.Clear();
        Modules.Clear();
        Cables.Clear();
        SelectedModule = null;
        SelectedCable = null;
        StatusMessage = "Cleared all modules and cables";
    }

    [RelayCommand]
    private void ResetAllModules()
    {
        _synth?.Reset();
        StatusMessage = "Reset all modules";
    }

    [RelayCommand]
    private void ZoomIn()
    {
        ZoomLevel = Math.Min(3.0, ZoomLevel + 0.1);
    }

    [RelayCommand]
    private void ZoomOut()
    {
        ZoomLevel = Math.Max(0.25, ZoomLevel - 0.1);
    }

    [RelayCommand]
    private void ResetZoom()
    {
        ZoomLevel = 1.0;
        PanX = 0;
        PanY = 0;
    }

    /// <summary>
    /// Moves a module to a new position.
    /// </summary>
    public void MoveModule(ModuleViewModel module, double newX, double newY)
    {
        module.X = Math.Max(0, Math.Min(CanvasWidth - module.Width, newX));
        module.Y = Math.Max(0, Math.Min(CanvasHeight - module.Height, newY));
    }

    /// <summary>
    /// Selects a module.
    /// </summary>
    public void SelectModule(ModuleViewModel? module)
    {
        if (SelectedModule != null)
        {
            SelectedModule.IsSelected = false;
        }

        SelectedModule = module;

        if (module != null)
        {
            module.IsSelected = true;
            SelectedCable = null;
        }
    }

    /// <summary>
    /// Selects a cable.
    /// </summary>
    public void SelectCable(CableViewModel? cable)
    {
        if (SelectedCable != null)
        {
            SelectedCable.IsSelected = false;
        }

        SelectedCable = cable;

        if (cable != null)
        {
            cable.IsSelected = true;
            SelectedModule = null;
            if (SelectedModule != null)
            {
                SelectedModule.IsSelected = false;
            }
        }
    }

    /// <summary>
    /// Gets the underlying ModularSynth instance.
    /// </summary>
    public ModularSynth? GetSynth() => _synth;

    /// <summary>
    /// Triggers a note on the synth.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
    }

    /// <summary>
    /// Triggers a note off on the synth.
    /// </summary>
    public void NoteOff(int note)
    {
        _synth?.NoteOff(note);
    }

    /// <summary>
    /// Stops all notes.
    /// </summary>
    public void AllNotesOff()
    {
        _synth?.AllNotesOff();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _synth?.Dispose();
        _synth = null;
    }
}
