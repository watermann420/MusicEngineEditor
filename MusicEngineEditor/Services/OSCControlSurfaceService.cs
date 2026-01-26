// MusicEngineEditor - OSC Control Surface Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Text.Json;
using MusicEngine.Core.Osc;

namespace MusicEngineEditor.Services;

/// <summary>
/// Represents an OSC address mapping to a parameter.
/// </summary>
public class OscAddressMapping
{
    /// <summary>OSC address pattern (e.g., "/mixer/1/volume").</summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>Target parameter identifier.</summary>
    public string ParameterId { get; set; } = string.Empty;

    /// <summary>Minimum value for scaling.</summary>
    public float MinValue { get; set; }

    /// <summary>Maximum value for scaling.</summary>
    public float MaxValue { get; set; } = 1.0f;

    /// <summary>Whether to send feedback to the controller.</summary>
    public bool SendFeedback { get; set; } = true;

    /// <summary>Whether this mapping is enabled.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Description of this mapping.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Touch index for multi-touch support (0-9).</summary>
    public int TouchIndex { get; set; } = -1;
}

/// <summary>
/// Represents an OSC control surface template (e.g., TouchOSC layout).
/// </summary>
public class OscTemplate
{
    /// <summary>Template name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Template description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Author of the template.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Application compatibility (TouchOSC, Lemur, etc).</summary>
    public string TargetApp { get; set; } = "Generic";

    /// <summary>Version number.</summary>
    public string Version { get; set; } = "1.0";

    /// <summary>Address mappings in this template.</summary>
    public List<OscAddressMapping> Mappings { get; set; } = new();

    /// <summary>Page definitions for multi-page layouts.</summary>
    public List<OscTemplatePage> Pages { get; set; } = new();
}

/// <summary>
/// Represents a page in an OSC template.
/// </summary>
public class OscTemplatePage
{
    /// <summary>Page name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Page index (0-based).</summary>
    public int Index { get; set; }

    /// <summary>Mappings specific to this page.</summary>
    public List<OscAddressMapping> Mappings { get; set; } = new();
}

/// <summary>
/// Event args for OSC parameter changes.
/// </summary>
public class OscParameterEventArgs : EventArgs
{
    public string Address { get; }
    public string ParameterId { get; }
    public float Value { get; }
    public float ScaledValue { get; }
    public int TouchIndex { get; }
    public IPEndPoint? Source { get; }

    public OscParameterEventArgs(string address, string parameterId, float value, float scaledValue, int touchIndex, IPEndPoint? source)
    {
        Address = address;
        ParameterId = parameterId;
        Value = value;
        ScaledValue = scaledValue;
        TouchIndex = touchIndex;
        Source = source;
    }
}

/// <summary>
/// Event args for multi-touch events.
/// </summary>
public class OscMultiTouchEventArgs : EventArgs
{
    public string Address { get; }
    public float[] Values { get; }
    public IPEndPoint? Source { get; }

    public OscMultiTouchEventArgs(string address, float[] values, IPEndPoint? source)
    {
        Address = address;
        Values = values;
        Source = source;
    }
}

/// <summary>
/// Service for OSC control surface integration.
/// Supports TouchOSC, Lemur, and generic OSC controllers with bi-directional feedback.
/// </summary>
public sealed class OSCControlSurfaceService : IDisposable
{
    #region Singleton

    private static readonly Lazy<OSCControlSurfaceService> _instance = new(
        () => new OSCControlSurfaceService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static OSCControlSurfaceService Instance => _instance.Value;

    #endregion

    #region Private Fields

    private OscServer? _oscServer;
    private OscClient? _oscClient;
    private int _listenPort = 8000;
    private string _feedbackHost = "127.0.0.1";
    private int _feedbackPort = 9000;
    private readonly Dictionary<string, OscAddressMapping> _mappings = new();
    private readonly Dictionary<string, float> _parameterValues = new();
    private readonly Dictionary<string, List<IPEndPoint>> _activeClients = new();
    private OscTemplate? _currentTemplate;
    private bool _isRunning;
    private bool _disposed;
    private readonly object _lock = new();

    // Multi-touch tracking
    private readonly Dictionary<int, (float X, float Y)> _touchPoints = new();

    #endregion

    #region Properties

    /// <summary>Gets whether the service is running.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Gets or sets the listen port for incoming OSC messages.</summary>
    public int ListenPort
    {
        get => _listenPort;
        set => _listenPort = Math.Clamp(value, 1024, 65535);
    }

    /// <summary>Gets or sets the host for sending feedback.</summary>
    public string FeedbackHost
    {
        get => _feedbackHost;
        set => _feedbackHost = value ?? "127.0.0.1";
    }

    /// <summary>Gets or sets the port for sending feedback.</summary>
    public int FeedbackPort
    {
        get => _feedbackPort;
        set => _feedbackPort = Math.Clamp(value, 1024, 65535);
    }

    /// <summary>Gets the current template.</summary>
    public OscTemplate? CurrentTemplate => _currentTemplate;

    /// <summary>Gets all registered mappings.</summary>
    public IReadOnlyDictionary<string, OscAddressMapping> Mappings
    {
        get
        {
            lock (_lock)
            {
                return new Dictionary<string, OscAddressMapping>(_mappings);
            }
        }
    }

    #endregion

    #region Events

    /// <summary>Raised when a parameter value changes from OSC input.</summary>
    public event EventHandler<OscParameterEventArgs>? ParameterChanged;

    /// <summary>Raised when a multi-touch event is received.</summary>
    public event EventHandler<OscMultiTouchEventArgs>? MultiTouchReceived;

    /// <summary>Raised when a transport command is received.</summary>
    public event EventHandler<string>? TransportCommand;

    /// <summary>Raised when an unmapped message is received.</summary>
    public event EventHandler<OscMessage>? UnmappedMessageReceived;

    /// <summary>Raised when connection state changes.</summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    #endregion

    #region Constructor

    private OSCControlSurfaceService()
    {
        // Initialize with default mappings for common patterns
        InitializeDefaultMappings();
    }

    #endregion

    #region Server Control

    /// <summary>
    /// Starts the OSC server.
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isRunning) return;

            try
            {
                _oscServer = new OscServer(_listenPort);
                _oscServer.MessageReceived += OnOscMessageReceived;

                // Initialize feedback client
                _oscClient = new OscClient(_feedbackHost, _feedbackPort);

                _oscServer.Start();
                _isRunning = true;

                ConnectionStateChanged?.Invoke(this, true);
            }
            catch (Exception)
            {
                Stop();
                throw;
            }
        }
    }

    /// <summary>
    /// Stops the OSC server.
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            _isRunning = false;

            if (_oscServer != null)
            {
                _oscServer.MessageReceived -= OnOscMessageReceived;
                _oscServer.Stop();
                _oscServer.Dispose();
                _oscServer = null;
            }

            _oscClient?.Dispose();
            _oscClient = null;

            _activeClients.Clear();
            ConnectionStateChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Restarts the server with new settings.
    /// </summary>
    public void Restart()
    {
        Stop();
        Start();
    }

    #endregion

    #region Message Handling

    private void OnOscMessageReceived(object? sender, OscMessageReceivedEventArgs e)
    {
        try
        {
            ProcessOscMessage(e.Message, e.Source);
        }
        catch (Exception)
        {
            // Ignore processing errors
        }
    }

    private void ProcessOscMessage(OscMessage message, IPEndPoint source)
    {
        string address = message.Address;

        // Track active clients for feedback
        TrackClient(address, source);

        // Check for transport commands
        if (ProcessTransportCommand(address, message)) return;

        // Check for multi-touch
        if (ProcessMultiTouch(address, message, source)) return;

        // Process mapped parameters
        lock (_lock)
        {
            if (_mappings.TryGetValue(address, out var mapping) && mapping.IsEnabled)
            {
                ProcessMappedMessage(message, mapping, source);
                return;
            }

            // Try pattern matching
            foreach (var kvp in _mappings)
            {
                if (OscServer.MatchesPattern(kvp.Key, address) && kvp.Value.IsEnabled)
                {
                    ProcessMappedMessage(message, kvp.Value, source);
                    return;
                }
            }
        }

        // Unmapped message
        UnmappedMessageReceived?.Invoke(this, message);
    }

    private void ProcessMappedMessage(OscMessage message, OscAddressMapping mapping, IPEndPoint source)
    {
        float rawValue = 0f;

        // Extract value from message
        if (message.Arguments.Count > 0)
        {
            var arg = message.Arguments[0];
            rawValue = arg.Type switch
            {
                OscType.Float32 => (float)arg.Value!,
                OscType.Int32 => (int)arg.Value!,
                OscType.Float64 => (float)(double)arg.Value!,
                OscType.Int64 => (float)(long)arg.Value!,
                OscType.True => 1f,
                OscType.False => 0f,
                _ => 0f
            };
        }

        // Scale value
        float scaledValue = mapping.MinValue + (rawValue * (mapping.MaxValue - mapping.MinValue));

        // Store value
        lock (_lock)
        {
            _parameterValues[mapping.ParameterId] = scaledValue;
        }

        // Raise event
        ParameterChanged?.Invoke(this, new OscParameterEventArgs(
            message.Address,
            mapping.ParameterId,
            rawValue,
            scaledValue,
            mapping.TouchIndex,
            source
        ));
    }

    private bool ProcessTransportCommand(string address, OscMessage message)
    {
        // Common transport patterns
        var command = address.ToLowerInvariant() switch
        {
            "/transport/play" or "/play" => "play",
            "/transport/stop" or "/stop" => "stop",
            "/transport/record" or "/record" => "record",
            "/transport/rewind" or "/rewind" => "rewind",
            "/transport/forward" or "/forward" => "forward",
            "/transport/loop" or "/loop" => "loop",
            "/transport/click" or "/click" or "/metronome" => "click",
            _ => null
        };

        if (command != null)
        {
            // Only trigger on button press (value > 0)
            bool pressed = true;
            if (message.Arguments.Count > 0)
            {
                var arg = message.Arguments[0];
                pressed = arg.Type switch
                {
                    OscType.Float32 => (float)arg.Value! > 0.5f,
                    OscType.Int32 => (int)arg.Value! > 0,
                    OscType.True => true,
                    OscType.False => false,
                    _ => true
                };
            }

            if (pressed)
            {
                TransportCommand?.Invoke(this, command);
            }
            return true;
        }

        return false;
    }

    private bool ProcessMultiTouch(string address, OscMessage message, IPEndPoint source)
    {
        // TouchOSC multi-touch: /multixy/1/1, /multixy/1/2, etc.
        // Lemur: /MultiBalls/x, /MultiBalls/y with arrays
        if (address.Contains("multi", StringComparison.OrdinalIgnoreCase))
        {
            var values = message.Arguments
                .Where(a => a.Type == OscType.Float32 || a.Type == OscType.Int32)
                .Select(a => a.Type == OscType.Float32 ? (float)a.Value! : (float)(int)a.Value!)
                .ToArray();

            if (values.Length > 0)
            {
                MultiTouchReceived?.Invoke(this, new OscMultiTouchEventArgs(address, values, source));
                return true;
            }
        }

        // Track individual touch points
        if (address.StartsWith("/touch/"))
        {
            var parts = address.Split('/');
            if (parts.Length >= 3 && int.TryParse(parts[2], out int touchId) && touchId >= 0 && touchId < 10)
            {
                if (message.Arguments.Count >= 2)
                {
                    float x = message.Arguments[0].Type == OscType.Float32 ? (float)message.Arguments[0].Value! : 0f;
                    float y = message.Arguments[1].Type == OscType.Float32 ? (float)message.Arguments[1].Value! : 0f;
                    _touchPoints[touchId] = (x, y);
                }
                return true;
            }
        }

        return false;
    }

    private void TrackClient(string address, IPEndPoint source)
    {
        // Extract base address for grouping
        var parts = address.Split('/');
        var baseAddress = parts.Length >= 2 ? $"/{parts[1]}" : address;

        lock (_lock)
        {
            if (!_activeClients.TryGetValue(baseAddress, out var clients))
            {
                clients = new List<IPEndPoint>();
                _activeClients[baseAddress] = clients;
            }

            if (!clients.Any(c => c.Address.Equals(source.Address) && c.Port == source.Port))
            {
                clients.Add(source);
            }
        }
    }

    #endregion

    #region Feedback

    /// <summary>
    /// Sends feedback value to controller.
    /// </summary>
    /// <param name="address">OSC address.</param>
    /// <param name="value">Value to send (0-1).</param>
    public void SendFeedback(string address, float value)
    {
        if (_oscClient == null || !_isRunning) return;

        try
        {
            var message = new OscMessage(address, value);
            _oscClient.Send(message);
        }
        catch { }
    }

    /// <summary>
    /// Sends feedback to all active clients for an address.
    /// </summary>
    public void SendFeedbackToAll(string address, float value)
    {
        if (_oscClient == null || !_isRunning) return;

        try
        {
            var message = new OscMessage(address, value);

            // Send to main feedback address
            _oscClient.Send(message);

            // Send to tracked clients
            lock (_lock)
            {
                var parts = address.Split('/');
                var baseAddress = parts.Length >= 2 ? $"/{parts[1]}" : address;

                if (_activeClients.TryGetValue(baseAddress, out var clients))
                {
                    foreach (var client in clients)
                    {
                        try
                        {
                            using var tempClient = new OscClient(client.Address.ToString(), client.Port);
                            tempClient.Send(message);
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// Sends a string to the controller (for labels/displays).
    /// </summary>
    public void SendString(string address, string text)
    {
        if (_oscClient == null || !_isRunning) return;

        try
        {
            var message = new OscMessage(address, text);
            _oscClient.Send(message);
        }
        catch { }
    }

    /// <summary>
    /// Sends multiple values (for XY pads, etc).
    /// </summary>
    public void SendValues(string address, params float[] values)
    {
        if (_oscClient == null || !_isRunning) return;

        try
        {
            var args = values.Select(v => (object)v).ToArray();
            var message = new OscMessage(address, args);
            _oscClient.Send(message);
        }
        catch { }
    }

    /// <summary>
    /// Updates a parameter value and sends feedback if mapping exists.
    /// </summary>
    public void UpdateParameter(string parameterId, float value)
    {
        lock (_lock)
        {
            _parameterValues[parameterId] = value;

            // Find mappings for this parameter and send feedback
            foreach (var mapping in _mappings.Values.Where(m => m.ParameterId == parameterId && m.SendFeedback))
            {
                // Convert scaled value back to 0-1 range
                float normalizedValue = (value - mapping.MinValue) / (mapping.MaxValue - mapping.MinValue);
                normalizedValue = Math.Clamp(normalizedValue, 0f, 1f);

                SendFeedback(mapping.Address, normalizedValue);
            }
        }
    }

    #endregion

    #region Mapping Management

    /// <summary>
    /// Adds an address mapping.
    /// </summary>
    public void AddMapping(OscAddressMapping mapping)
    {
        if (mapping == null || string.IsNullOrEmpty(mapping.Address)) return;

        lock (_lock)
        {
            _mappings[mapping.Address] = mapping;
        }
    }

    /// <summary>
    /// Adds a simple address mapping.
    /// </summary>
    public void AddMapping(string address, string parameterId, float minValue = 0f, float maxValue = 1f)
    {
        AddMapping(new OscAddressMapping
        {
            Address = address,
            ParameterId = parameterId,
            MinValue = minValue,
            MaxValue = maxValue
        });
    }

    /// <summary>
    /// Removes a mapping by address.
    /// </summary>
    public bool RemoveMapping(string address)
    {
        lock (_lock)
        {
            return _mappings.Remove(address);
        }
    }

    /// <summary>
    /// Clears all mappings.
    /// </summary>
    public void ClearMappings()
    {
        lock (_lock)
        {
            _mappings.Clear();
        }
    }

    private void InitializeDefaultMappings()
    {
        // Default mixer mappings (TouchOSC style)
        for (int i = 1; i <= 8; i++)
        {
            AddMapping($"/mixer/{i}/volume", $"track.{i}.volume", 0f, 1f);
            AddMapping($"/mixer/{i}/pan", $"track.{i}.pan", -1f, 1f);
            AddMapping($"/mixer/{i}/mute", $"track.{i}.mute", 0f, 1f);
            AddMapping($"/mixer/{i}/solo", $"track.{i}.solo", 0f, 1f);
        }

        // Master
        AddMapping("/master/volume", "master.volume", 0f, 1f);

        // Transport
        AddMapping("/transport/bpm", "transport.bpm", 20f, 300f);
        AddMapping("/transport/position", "transport.position", 0f, 1f);
    }

    #endregion

    #region Template Management

    /// <summary>
    /// Loads a template from file.
    /// </summary>
    public OscTemplate? LoadTemplate(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var template = JsonSerializer.Deserialize<OscTemplate>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (template != null)
            {
                ApplyTemplate(template);
            }

            return template;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Saves a template to file.
    /// </summary>
    public void SaveTemplate(OscTemplate template, string filePath)
    {
        var json = JsonSerializer.Serialize(template, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Applies a template, replacing current mappings.
    /// </summary>
    public void ApplyTemplate(OscTemplate template)
    {
        lock (_lock)
        {
            _currentTemplate = template;
            _mappings.Clear();

            foreach (var mapping in template.Mappings)
            {
                _mappings[mapping.Address] = mapping;
            }

            foreach (var page in template.Pages)
            {
                foreach (var mapping in page.Mappings)
                {
                    _mappings[mapping.Address] = mapping;
                }
            }
        }
    }

    /// <summary>
    /// Creates a template from current mappings.
    /// </summary>
    public OscTemplate CreateTemplateFromMappings(string name, string description = "")
    {
        lock (_lock)
        {
            return new OscTemplate
            {
                Name = name,
                Description = description,
                Mappings = _mappings.Values.ToList()
            };
        }
    }

    /// <summary>
    /// Gets built-in templates for common controllers.
    /// </summary>
    public static IEnumerable<OscTemplate> GetBuiltInTemplates()
    {
        yield return CreateTouchOscMixerTemplate();
        yield return CreateLemurMixerTemplate();
        yield return CreateGenericXYTemplate();
    }

    private static OscTemplate CreateTouchOscMixerTemplate()
    {
        var template = new OscTemplate
        {
            Name = "TouchOSC Mixer",
            Description = "8-channel mixer layout for TouchOSC",
            Author = "MusicEngine",
            TargetApp = "TouchOSC"
        };

        for (int i = 1; i <= 8; i++)
        {
            template.Mappings.Add(new OscAddressMapping
            {
                Address = $"/1/fader{i}",
                ParameterId = $"track.{i}.volume",
                Description = $"Channel {i} Volume"
            });
            template.Mappings.Add(new OscAddressMapping
            {
                Address = $"/1/rotary{i}",
                ParameterId = $"track.{i}.pan",
                MinValue = -1f,
                MaxValue = 1f,
                Description = $"Channel {i} Pan"
            });
            template.Mappings.Add(new OscAddressMapping
            {
                Address = $"/1/toggle{i}",
                ParameterId = $"track.{i}.mute",
                Description = $"Channel {i} Mute"
            });
        }

        return template;
    }

    private static OscTemplate CreateLemurMixerTemplate()
    {
        var template = new OscTemplate
        {
            Name = "Lemur Mixer",
            Description = "Multi-touch mixer for Lemur",
            Author = "MusicEngine",
            TargetApp = "Lemur"
        };

        for (int i = 0; i < 8; i++)
        {
            template.Mappings.Add(new OscAddressMapping
            {
                Address = $"/Mixer/Fader/{i}/x",
                ParameterId = $"track.{i + 1}.volume",
                Description = $"Channel {i + 1} Volume"
            });
        }

        // XY Pad for master
        template.Mappings.Add(new OscAddressMapping
        {
            Address = "/XY/x",
            ParameterId = "master.pan",
            MinValue = -1f,
            MaxValue = 1f,
            Description = "Master Pan"
        });
        template.Mappings.Add(new OscAddressMapping
        {
            Address = "/XY/y",
            ParameterId = "master.volume",
            Description = "Master Volume"
        });

        return template;
    }

    private static OscTemplate CreateGenericXYTemplate()
    {
        return new OscTemplate
        {
            Name = "Generic XY Controller",
            Description = "Simple XY pad and faders",
            Author = "MusicEngine",
            TargetApp = "Generic",
            Mappings = new List<OscAddressMapping>
            {
                new() { Address = "/xy/1", ParameterId = "xy.1.x", Description = "XY Pad 1 X" },
                new() { Address = "/xy/2", ParameterId = "xy.1.y", Description = "XY Pad 1 Y" },
                new() { Address = "/fader/1", ParameterId = "fader.1", Description = "Fader 1" },
                new() { Address = "/fader/2", ParameterId = "fader.2", Description = "Fader 2" }
            }
        };
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
    }

    #endregion
}
