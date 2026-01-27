// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Audio streaming service for low-latency network audio transmission.

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for streaming raw audio data over the network with minimal latency.
/// Instead of sending compressed audio files, this streams raw PCM samples or
/// MusicEngine commands directly, allowing the receiving end to render audio locally.
///
/// Use cases:
/// - Low-latency voice/audio chat (like Discord but with raw samples)
/// - Remote music collaboration
/// - Streaming audio from mic directly to remote MusicEngine instance
/// - Sending sequencer commands instead of rendered audio
/// </summary>
public sealed class AudioStreamingService : IDisposable
{
    #region Singleton

    private static readonly Lazy<AudioStreamingService> _instance = new(
        () => new AudioStreamingService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public static AudioStreamingService Instance => _instance.Value;

    #endregion

    #region Constants

    /// <summary>
    /// Default port for audio streaming.
    /// </summary>
    public const int DefaultPort = 45678;

    /// <summary>
    /// Maximum packet size for UDP streaming.
    /// </summary>
    public const int MaxPacketSize = 1400; // MTU-safe

    /// <summary>
    /// Audio frame size in samples (per channel).
    /// </summary>
    public const int FrameSamples = 480; // 10ms at 48kHz

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private bool _disposed;

    // Server mode
    private UdpClient? _server;
    private TcpListener? _tcpServer;
    private CancellationTokenSource? _serverCts;
    private readonly ConcurrentDictionary<string, RemoteClient> _clients = new();

    // Client mode
    private UdpClient? _clientUdp;
    private TcpClient? _clientTcp;
    private IPEndPoint? _serverEndpoint;
    private CancellationTokenSource? _clientCts;

    // Audio buffer
    private readonly ConcurrentQueue<AudioPacket> _receiveQueue = new();
    private readonly ConcurrentQueue<AudioPacket> _sendQueue = new();
    private readonly ArrayPool<byte> _bufferPool = ArrayPool<byte>.Shared;

    // Statistics
    private long _packetsSent;
    private long _packetsReceived;
    private long _bytesSent;
    private long _bytesReceived;

    #endregion

    #region Properties

    /// <summary>
    /// Whether the service is running as a server.
    /// </summary>
    public bool IsServer { get; private set; }

    /// <summary>
    /// Whether connected to a remote server (client mode).
    /// </summary>
    public bool IsConnected { get; private set; }

    /// <summary>
    /// Current streaming mode.
    /// </summary>
    public StreamingMode Mode { get; set; } = StreamingMode.RawPCM;

    /// <summary>
    /// Sample rate for audio streaming.
    /// </summary>
    public int SampleRate { get; set; } = 48000;

    /// <summary>
    /// Number of audio channels.
    /// </summary>
    public int Channels { get; set; } = 2;

    /// <summary>
    /// Connected client count (server mode).
    /// </summary>
    public int ClientCount => _clients.Count;

    /// <summary>
    /// Statistics about streaming.
    /// </summary>
    public StreamingStatistics Statistics => new()
    {
        PacketsSent = _packetsSent,
        PacketsReceived = _packetsReceived,
        BytesSent = _bytesSent,
        BytesReceived = _bytesReceived,
        ClientCount = _clients.Count,
        IsServer = IsServer,
        IsConnected = IsConnected
    };

    #endregion

    #region Events

    /// <summary>
    /// Raised when audio data is received.
    /// </summary>
    public event EventHandler<AudioReceivedEventArgs>? AudioReceived;

    /// <summary>
    /// Raised when a command is received.
    /// </summary>
    public event EventHandler<CommandReceivedEventArgs>? CommandReceived;

    /// <summary>
    /// Raised when a client connects (server mode).
    /// </summary>
    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;

    /// <summary>
    /// Raised when a client disconnects (server mode).
    /// </summary>
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;

    /// <summary>
    /// Raised when connection state changes.
    /// </summary>
    public event EventHandler<bool>? ConnectionStateChanged;

    /// <summary>
    /// Raised when a text message is received.
    /// </summary>
    public event EventHandler<TextMessageEventArgs>? TextMessageReceived;

    /// <summary>
    /// Raised when a call request is received.
    /// </summary>
    public event EventHandler<CallRequestEventArgs>? CallRequestReceived;

    /// <summary>
    /// Raised when a call is accepted/rejected.
    /// </summary>
    public event EventHandler<CallResponseEventArgs>? CallResponseReceived;

    #endregion

    #region Constructor

    private AudioStreamingService()
    {
    }

    #endregion

    #region Server Mode

    /// <summary>
    /// Starts the streaming server.
    /// </summary>
    /// <param name="port">Port to listen on</param>
    /// <param name="useTcp">Use TCP for reliable delivery (higher latency)</param>
    public async Task StartServerAsync(int port = DefaultPort, bool useTcp = false)
    {
        ThrowIfDisposed();

        if (IsServer) return;

        _serverCts = new CancellationTokenSource();

        try
        {
            if (useTcp)
            {
                _tcpServer = new TcpListener(IPAddress.Any, port);
                _tcpServer.Start();
                _ = AcceptTcpClientsAsync(_serverCts.Token);
            }
            else
            {
                _server = new UdpClient(port);
                _ = ReceiveUdpPacketsServerAsync(_serverCts.Token);
            }

            IsServer = true;
            ConnectionStateChanged?.Invoke(this, true);

            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Server started on port {port}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Server start failed: {ex.Message}");
            throw;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stops the streaming server.
    /// </summary>
    public void StopServer()
    {
        if (!IsServer) return;

        _serverCts?.Cancel();
        _server?.Close();
        _server?.Dispose();
        _server = null;

        _tcpServer?.Stop();
        _tcpServer = null;

        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();

        IsServer = false;
        ConnectionStateChanged?.Invoke(this, false);

        System.Diagnostics.Debug.WriteLine("[AudioStreaming] Server stopped");
    }

    private async Task AcceptTcpClientsAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _tcpServer != null)
        {
            try
            {
                var client = await _tcpServer.AcceptTcpClientAsync(ct);
                var endpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";

                var remoteClient = new RemoteClient
                {
                    Id = Guid.NewGuid().ToString(),
                    TcpClient = client,
                    Endpoint = endpoint,
                    ConnectedAt = DateTime.UtcNow
                };

                _clients[remoteClient.Id] = remoteClient;
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs(remoteClient.Id, endpoint));

                _ = HandleTcpClientAsync(remoteClient, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleTcpClientAsync(RemoteClient client, CancellationToken ct)
    {
        var stream = client.TcpClient!.GetStream();
        var buffer = _bufferPool.Rent(MaxPacketSize);

        try
        {
            while (!ct.IsCancellationRequested && client.TcpClient.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, MaxPacketSize), ct);
                if (bytesRead == 0) break;

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, bytesRead);

                ProcessReceivedPacket(buffer, bytesRead, client.Id);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Client error: {ex.Message}");
        }
        finally
        {
            _bufferPool.Return(buffer);
            _clients.TryRemove(client.Id, out _);
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(client.Id, client.Endpoint));
            client.Dispose();
        }
    }

    private async Task ReceiveUdpPacketsServerAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _server != null)
        {
            try
            {
                var result = await _server.ReceiveAsync(ct);

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, result.Buffer.Length);

                var clientId = result.RemoteEndPoint.ToString();

                // Register client if new
                if (!_clients.ContainsKey(clientId))
                {
                    var remoteClient = new RemoteClient
                    {
                        Id = clientId,
                        UdpEndpoint = result.RemoteEndPoint,
                        Endpoint = clientId,
                        ConnectedAt = DateTime.UtcNow
                    };
                    _clients[clientId] = remoteClient;
                    ClientConnected?.Invoke(this, new ClientConnectedEventArgs(clientId, clientId));
                }

                ProcessReceivedPacket(result.Buffer, result.Buffer.Length, clientId);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioStreaming] UDP receive error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Client Mode

    /// <summary>
    /// Connects to a streaming server.
    /// </summary>
    /// <param name="host">Server hostname or IP</param>
    /// <param name="port">Server port</param>
    /// <param name="useTcp">Use TCP for reliable delivery</param>
    public async Task ConnectAsync(string host, int port = DefaultPort, bool useTcp = false)
    {
        ThrowIfDisposed();

        if (IsConnected) return;

        _clientCts = new CancellationTokenSource();

        try
        {
            _serverEndpoint = new IPEndPoint(
                (await Dns.GetHostAddressesAsync(host))[0],
                port);

            if (useTcp)
            {
                _clientTcp = new TcpClient();
                await _clientTcp.ConnectAsync(_serverEndpoint.Address, port);
                _ = ReceiveTcpPacketsClientAsync(_clientCts.Token);
            }
            else
            {
                _clientUdp = new UdpClient();
                _clientUdp.Connect(_serverEndpoint);
                _ = ReceiveUdpPacketsClientAsync(_clientCts.Token);
            }

            IsConnected = true;
            ConnectionStateChanged?.Invoke(this, true);

            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Connected to {host}:{port}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Connect failed: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Disconnects from the server.
    /// </summary>
    public void Disconnect()
    {
        if (!IsConnected) return;

        _clientCts?.Cancel();
        _clientUdp?.Close();
        _clientUdp?.Dispose();
        _clientUdp = null;

        _clientTcp?.Close();
        _clientTcp?.Dispose();
        _clientTcp = null;

        _serverEndpoint = null;
        IsConnected = false;
        ConnectionStateChanged?.Invoke(this, false);

        System.Diagnostics.Debug.WriteLine("[AudioStreaming] Disconnected");
    }

    private async Task ReceiveTcpPacketsClientAsync(CancellationToken ct)
    {
        if (_clientTcp == null) return;

        var stream = _clientTcp.GetStream();
        var buffer = _bufferPool.Rent(MaxPacketSize);

        try
        {
            while (!ct.IsCancellationRequested && _clientTcp.Connected)
            {
                var bytesRead = await stream.ReadAsync(buffer.AsMemory(0, MaxPacketSize), ct);
                if (bytesRead == 0) break;

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, bytesRead);

                ProcessReceivedPacket(buffer, bytesRead, "server");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] TCP receive error: {ex.Message}");
        }
        finally
        {
            _bufferPool.Return(buffer);
            Disconnect();
        }
    }

    private async Task ReceiveUdpPacketsClientAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _clientUdp != null)
        {
            try
            {
                var result = await _clientUdp.ReceiveAsync(ct);

                Interlocked.Increment(ref _packetsReceived);
                Interlocked.Add(ref _bytesReceived, result.Buffer.Length);

                ProcessReceivedPacket(result.Buffer, result.Buffer.Length, "server");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AudioStreaming] UDP receive error: {ex.Message}");
            }
        }
    }

    #endregion

    #region Audio Streaming

    /// <summary>
    /// Sends raw PCM audio samples to the connected remote(s).
    /// </summary>
    /// <param name="samples">Float audio samples (interleaved if stereo)</param>
    /// <param name="targetClientId">Specific client ID (server mode) or null for all</param>
    public async Task SendAudioAsync(float[] samples, string? targetClientId = null)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.AudioPCM,
            Timestamp = DateTime.UtcNow.Ticks,
            SampleRate = SampleRate,
            Channels = Channels,
            Samples = samples
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);
    }

    /// <summary>
    /// Sends raw PCM audio from a byte buffer (e.g., from microphone).
    /// </summary>
    /// <param name="pcmData">Raw PCM bytes (16-bit samples)</param>
    /// <param name="targetClientId">Specific client ID or null for all</param>
    public async Task SendAudioBytesAsync(byte[] pcmData, string? targetClientId = null)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.AudioRaw,
            Timestamp = DateTime.UtcNow.Ticks,
            SampleRate = SampleRate,
            Channels = Channels,
            RawData = pcmData
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);
    }

    /// <summary>
    /// Sends a MusicEngine command to be executed on the remote end.
    /// This allows sending sequencer commands instead of rendered audio,
    /// enabling the remote to render locally (much lower bandwidth).
    /// </summary>
    /// <param name="command">Command type</param>
    /// <param name="parameters">Command parameters</param>
    /// <param name="targetClientId">Specific client ID or null for all</param>
    public async Task SendCommandAsync(StreamingCommand command, Dictionary<string, object>? parameters = null,
        string? targetClientId = null)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.Command,
            Timestamp = DateTime.UtcNow.Ticks,
            Command = command,
            CommandParameters = parameters
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);
    }

    /// <summary>
    /// Broadcasts audio to all connected clients (server mode).
    /// </summary>
    /// <param name="samples">Audio samples</param>
    public Task BroadcastAudioAsync(float[] samples)
    {
        return SendAudioAsync(samples, null);
    }

    private async Task SendDataAsync(byte[] data, string? targetClientId)
    {
        try
        {
            if (IsServer)
            {
                // Server: send to specific client or broadcast
                var targets = string.IsNullOrEmpty(targetClientId)
                    ? _clients.Values
                    : _clients.TryGetValue(targetClientId, out var client)
                        ? new[] { client }
                        : Array.Empty<RemoteClient>();

                foreach (var target in targets)
                {
                    if (target.TcpClient?.Connected == true)
                    {
                        await target.TcpClient.GetStream().WriteAsync(data);
                    }
                    else if (target.UdpEndpoint != null && _server != null)
                    {
                        await _server.SendAsync(data, data.Length, target.UdpEndpoint);
                    }

                    Interlocked.Increment(ref _packetsSent);
                    Interlocked.Add(ref _bytesSent, data.Length);
                }
            }
            else if (IsConnected)
            {
                // Client: send to server
                if (_clientTcp?.Connected == true)
                {
                    await _clientTcp.GetStream().WriteAsync(data);
                }
                else if (_clientUdp != null)
                {
                    await _clientUdp.SendAsync(data, data.Length);
                }

                Interlocked.Increment(ref _packetsSent);
                Interlocked.Add(ref _bytesSent, data.Length);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Send error: {ex.Message}");
        }
    }

    #endregion

    #region Text Messaging & Calls

    /// <summary>
    /// Sends a text message to connected peer(s).
    /// </summary>
    /// <param name="message">The text message</param>
    /// <param name="targetClientId">Specific client or null for all</param>
    public async Task SendTextMessageAsync(string message, string? targetClientId = null)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.TextMessage,
            Timestamp = DateTime.UtcNow.Ticks,
            TextContent = message,
            SenderId = LocalUserId
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);
    }

    /// <summary>
    /// Requests a call with a peer.
    /// </summary>
    /// <param name="targetClientId">The peer to call</param>
    /// <param name="includeAudio">Include audio in the call</param>
    public async Task RequestCallAsync(string? targetClientId = null, bool includeAudio = true)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.CallRequest,
            Timestamp = DateTime.UtcNow.Ticks,
            SenderId = LocalUserId,
            CommandParameters = new Dictionary<string, object>
            {
                ["includeAudio"] = includeAudio,
                ["userName"] = LocalUserName ?? "User"
            }
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);
    }

    /// <summary>
    /// Responds to a call request.
    /// </summary>
    /// <param name="accepted">Whether to accept the call</param>
    /// <param name="targetClientId">The peer who requested the call</param>
    public async Task RespondToCallAsync(bool accepted, string? targetClientId = null)
    {
        if (!IsConnected && !IsServer) return;

        var packet = new AudioPacket
        {
            Type = PacketType.CallResponse,
            Timestamp = DateTime.UtcNow.Ticks,
            SenderId = LocalUserId,
            CommandParameters = new Dictionary<string, object>
            {
                ["accepted"] = accepted
            }
        };

        var data = SerializePacket(packet);
        await SendDataAsync(data, targetClientId);

        if (accepted)
        {
            IsInCall = true;
        }
    }

    /// <summary>
    /// Ends the current call.
    /// </summary>
    public async Task EndCallAsync()
    {
        IsInCall = false;
        await SendCommandAsync(StreamingCommand.Stop);
    }

    /// <summary>
    /// Local user ID for identification.
    /// </summary>
    public string LocalUserId { get; set; } = Guid.NewGuid().ToString()[..8];

    /// <summary>
    /// Local user display name.
    /// </summary>
    public string? LocalUserName { get; set; }

    /// <summary>
    /// Whether currently in a call.
    /// </summary>
    public bool IsInCall { get; private set; }

    #endregion

    #region Packet Processing

    private void ProcessReceivedPacket(byte[] data, int length, string sourceId)
    {
        try
        {
            var packet = DeserializePacket(data, length);
            if (packet == null) return;

            switch (packet.Type)
            {
                case PacketType.AudioPCM:
                case PacketType.AudioRaw:
                    AudioReceived?.Invoke(this, new AudioReceivedEventArgs(
                        packet.Samples,
                        packet.RawData,
                        packet.SampleRate,
                        packet.Channels,
                        sourceId,
                        packet.Timestamp));
                    break;

                case PacketType.Command:
                    CommandReceived?.Invoke(this, new CommandReceivedEventArgs(
                        packet.Command,
                        packet.CommandParameters,
                        sourceId,
                        packet.Timestamp));

                    // Execute command locally
                    ExecuteCommand(packet.Command, packet.CommandParameters);
                    break;

                case PacketType.TextMessage:
                    TextMessageReceived?.Invoke(this, new TextMessageEventArgs(
                        packet.TextContent ?? "",
                        packet.SenderId ?? sourceId,
                        packet.Timestamp));
                    break;

                case PacketType.CallRequest:
                    CallRequestReceived?.Invoke(this, new CallRequestEventArgs(
                        packet.SenderId ?? sourceId,
                        packet.CommandParameters?.GetValueOrDefault("userName")?.ToString() ?? "User",
                        packet.CommandParameters?.GetValueOrDefault("includeAudio") as bool? ?? true));
                    break;

                case PacketType.CallResponse:
                    var accepted = packet.CommandParameters?.GetValueOrDefault("accepted") as bool? ?? false;
                    CallResponseReceived?.Invoke(this, new CallResponseEventArgs(
                        packet.SenderId ?? sourceId,
                        accepted));
                    if (accepted) IsInCall = true;
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Process error: {ex.Message}");
        }
    }

    private void ExecuteCommand(StreamingCommand command, Dictionary<string, object>? parameters)
    {
        try
        {
            switch (command)
            {
                case StreamingCommand.Play:
                    PlaybackService.Instance.Play();
                    break;

                case StreamingCommand.Stop:
                    PlaybackService.Instance.Stop();
                    break;

                case StreamingCommand.SetBPM:
                    if (parameters?.TryGetValue("bpm", out var bpm) == true)
                        PlaybackService.Instance.BPM = Convert.ToDouble(bpm);
                    break;

                case StreamingCommand.SeekTo:
                    if (parameters?.TryGetValue("position", out var pos) == true)
                        PlaybackService.Instance.SetPosition(Convert.ToDouble(pos));
                    break;

                case StreamingCommand.PlayNote:
                    if (parameters?.TryGetValue("note", out var note) == true)
                        NotePreviewService.Instance.PlayNote(Convert.ToInt32(note), 100);
                    break;

                case StreamingCommand.StopNote:
                    if (parameters?.TryGetValue("note", out var stopNote) == true)
                        NotePreviewService.Instance.StopNote(Convert.ToInt32(stopNote));
                    break;

                case StreamingCommand.SetVariable:
                    if (parameters?.TryGetValue("name", out var name) == true &&
                        parameters.TryGetValue("value", out var value) == true)
                        ExternalControlService.Instance.SetVariable(name.ToString()!, value);
                    break;

                case StreamingCommand.TriggerEvent:
                    if (parameters?.TryGetValue("event", out var evt) == true)
                        ExternalControlService.Instance.TriggerEvent(evt.ToString()!,
                            parameters.GetValueOrDefault("param"));
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AudioStreaming] Command execution error: {ex.Message}");
        }
    }

    #endregion

    #region Serialization

    private byte[] SerializePacket(AudioPacket packet)
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header
        writer.Write((byte)packet.Type);
        writer.Write(packet.Timestamp);
        writer.Write(packet.SampleRate);
        writer.Write((byte)packet.Channels);

        switch (packet.Type)
        {
            case PacketType.AudioPCM when packet.Samples != null:
                writer.Write(packet.Samples.Length);
                foreach (var sample in packet.Samples)
                    writer.Write(sample);
                break;

            case PacketType.AudioRaw when packet.RawData != null:
                writer.Write(packet.RawData.Length);
                writer.Write(packet.RawData);
                break;

            case PacketType.Command:
                writer.Write((byte)packet.Command);
                var paramJson = packet.CommandParameters != null
                    ? System.Text.Json.JsonSerializer.Serialize(packet.CommandParameters)
                    : "";
                writer.Write(paramJson);
                break;

            case PacketType.TextMessage:
                writer.Write(packet.SenderId ?? "");
                writer.Write(packet.TextContent ?? "");
                break;

            case PacketType.CallRequest:
            case PacketType.CallResponse:
                writer.Write(packet.SenderId ?? "");
                var callParamJson = packet.CommandParameters != null
                    ? System.Text.Json.JsonSerializer.Serialize(packet.CommandParameters)
                    : "";
                writer.Write(callParamJson);
                break;
        }

        return ms.ToArray();
    }

    private AudioPacket? DeserializePacket(byte[] data, int length)
    {
        try
        {
            using var ms = new MemoryStream(data, 0, length);
            using var reader = new BinaryReader(ms);

            var packet = new AudioPacket
            {
                Type = (PacketType)reader.ReadByte(),
                Timestamp = reader.ReadInt64(),
                SampleRate = reader.ReadInt32(),
                Channels = reader.ReadByte()
            };

            switch (packet.Type)
            {
                case PacketType.AudioPCM:
                    var sampleCount = reader.ReadInt32();
                    packet.Samples = new float[sampleCount];
                    for (int i = 0; i < sampleCount; i++)
                        packet.Samples[i] = reader.ReadSingle();
                    break;

                case PacketType.AudioRaw:
                    var byteCount = reader.ReadInt32();
                    packet.RawData = reader.ReadBytes(byteCount);
                    break;

                case PacketType.Command:
                    packet.Command = (StreamingCommand)reader.ReadByte();
                    var paramJson = reader.ReadString();
                    if (!string.IsNullOrEmpty(paramJson))
                        packet.CommandParameters = System.Text.Json.JsonSerializer
                            .Deserialize<Dictionary<string, object>>(paramJson);
                    break;

                case PacketType.TextMessage:
                    packet.SenderId = reader.ReadString();
                    packet.TextContent = reader.ReadString();
                    break;

                case PacketType.CallRequest:
                case PacketType.CallResponse:
                    packet.SenderId = reader.ReadString();
                    var callParamJson = reader.ReadString();
                    if (!string.IsNullOrEmpty(callParamJson))
                        packet.CommandParameters = System.Text.Json.JsonSerializer
                            .Deserialize<Dictionary<string, object>>(callParamJson);
                    break;
            }

            return packet;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Helper Methods

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AudioStreamingService));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            StopServer();
            Disconnect();

            _serverCts?.Dispose();
            _clientCts?.Dispose();
        }
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Streaming mode options.
/// </summary>
public enum StreamingMode
{
    /// <summary>
    /// Send raw PCM audio samples.
    /// </summary>
    RawPCM,

    /// <summary>
    /// Send MusicEngine commands (lowest bandwidth).
    /// </summary>
    CommandsOnly,

    /// <summary>
    /// Hybrid: commands for music, PCM for voice.
    /// </summary>
    Hybrid
}

/// <summary>
/// Packet types for streaming protocol.
/// </summary>
public enum PacketType : byte
{
    AudioPCM = 1,
    AudioRaw = 2,
    Command = 3,
    Heartbeat = 4,
    Metadata = 5,
    TextMessage = 6,
    UserInfo = 7,
    CallRequest = 8,
    CallResponse = 9
}

/// <summary>
/// Commands that can be sent over the streaming protocol.
/// </summary>
public enum StreamingCommand : byte
{
    Play = 1,
    Stop = 2,
    Pause = 3,
    SeekTo = 4,
    SetBPM = 5,
    PlayNote = 10,
    StopNote = 11,
    AllNotesOff = 12,
    SetVariable = 20,
    TriggerEvent = 21,
    LoadProject = 30,
    SyncState = 31
}

/// <summary>
/// Internal audio packet structure.
/// </summary>
internal class AudioPacket
{
    public PacketType Type { get; set; }
    public long Timestamp { get; set; }
    public int SampleRate { get; set; }
    public int Channels { get; set; }
    public float[]? Samples { get; set; }
    public byte[]? RawData { get; set; }
    public StreamingCommand Command { get; set; }
    public Dictionary<string, object>? CommandParameters { get; set; }
    public string? TextContent { get; set; }
    public string? SenderId { get; set; }
}

/// <summary>
/// Remote client information.
/// </summary>
internal class RemoteClient : IDisposable
{
    public string Id { get; set; } = string.Empty;
    public string Endpoint { get; set; } = string.Empty;
    public TcpClient? TcpClient { get; set; }
    public IPEndPoint? UdpEndpoint { get; set; }
    public DateTime ConnectedAt { get; set; }

    public void Dispose()
    {
        TcpClient?.Close();
        TcpClient?.Dispose();
    }
}

/// <summary>
/// Streaming statistics.
/// </summary>
public class StreamingStatistics
{
    public long PacketsSent { get; init; }
    public long PacketsReceived { get; init; }
    public long BytesSent { get; init; }
    public long BytesReceived { get; init; }
    public int ClientCount { get; init; }
    public bool IsServer { get; init; }
    public bool IsConnected { get; init; }
}

/// <summary>
/// Event args for received audio.
/// </summary>
public class AudioReceivedEventArgs : EventArgs
{
    public float[]? Samples { get; }
    public byte[]? RawData { get; }
    public int SampleRate { get; }
    public int Channels { get; }
    public string SourceId { get; }
    public long Timestamp { get; }

    public AudioReceivedEventArgs(float[]? samples, byte[]? rawData, int sampleRate,
        int channels, string sourceId, long timestamp)
    {
        Samples = samples;
        RawData = rawData;
        SampleRate = sampleRate;
        Channels = channels;
        SourceId = sourceId;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event args for received commands.
/// </summary>
public class CommandReceivedEventArgs : EventArgs
{
    public StreamingCommand Command { get; }
    public Dictionary<string, object>? Parameters { get; }
    public string SourceId { get; }
    public long Timestamp { get; }

    public CommandReceivedEventArgs(StreamingCommand command, Dictionary<string, object>? parameters,
        string sourceId, long timestamp)
    {
        Command = command;
        Parameters = parameters;
        SourceId = sourceId;
        Timestamp = timestamp;
    }
}

/// <summary>
/// Event args for client connections.
/// </summary>
public class ClientConnectedEventArgs : EventArgs
{
    public string ClientId { get; }
    public string Endpoint { get; }

    public ClientConnectedEventArgs(string clientId, string endpoint)
    {
        ClientId = clientId;
        Endpoint = endpoint;
    }
}

/// <summary>
/// Event args for client disconnections.
/// </summary>
public class ClientDisconnectedEventArgs : EventArgs
{
    public string ClientId { get; }
    public string Endpoint { get; }

    public ClientDisconnectedEventArgs(string clientId, string endpoint)
    {
        ClientId = clientId;
        Endpoint = endpoint;
    }
}

/// <summary>
/// Event args for text messages.
/// </summary>
public class TextMessageEventArgs : EventArgs
{
    public string Message { get; }
    public string SenderId { get; }
    public long Timestamp { get; }
    public DateTime ReceivedAt { get; }

    public TextMessageEventArgs(string message, string senderId, long timestamp)
    {
        Message = message;
        SenderId = senderId;
        Timestamp = timestamp;
        ReceivedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Event args for call requests.
/// </summary>
public class CallRequestEventArgs : EventArgs
{
    public string CallerId { get; }
    public string CallerName { get; }
    public bool IncludesAudio { get; }

    public CallRequestEventArgs(string callerId, string callerName, bool includesAudio)
    {
        CallerId = callerId;
        CallerName = callerName;
        IncludesAudio = includesAudio;
    }
}

/// <summary>
/// Event args for call responses.
/// </summary>
public class CallResponseEventArgs : EventArgs
{
    public string ResponderId { get; }
    public bool Accepted { get; }

    public CallResponseEventArgs(string responderId, bool accepted)
    {
        ResponderId = responderId;
        Accepted = accepted;
    }
}

#endregion
