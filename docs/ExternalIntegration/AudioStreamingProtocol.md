# Audio Streaming Protocol

This document describes the low-latency audio streaming protocol for sending raw audio or MusicEngine commands over the network.

## Overview

The `AudioStreamingService` enables:
- **Raw PCM Streaming**: Send audio samples directly (like microphone input)
- **Command Streaming**: Send MusicEngine commands instead of rendered audio
- **Hybrid Mode**: Commands for music, PCM for voice
- **Text Messaging**: Send text messages peer-to-peer
- **Direct Calls**: Request and manage P2P audio calls

### Key Benefits

1. **Lower Latency**: Raw samples instead of encoded audio
2. **Lower Bandwidth** (command mode): Send `PlayNote(60)` instead of 48000 samples/second
3. **Local Rendering**: Remote end renders audio locally = better quality
4. **Synchronized Control**: Multiple clients stay in sync

## Quick Start

### Server Mode

```csharp
// Start a streaming server
await AudioStreamingService.Instance.StartServerAsync(port: 45678, useTcp: false);

// Listen for incoming audio
AudioStreamingService.Instance.AudioReceived += (sender, args) =>
{
    // args.Samples contains float[] audio data
    // args.SourceId identifies the sender
    ProcessIncomingAudio(args.Samples, args.SampleRate, args.Channels);
};

// Listen for commands
AudioStreamingService.Instance.CommandReceived += (sender, args) =>
{
    Console.WriteLine($"Command {args.Command} from {args.SourceId}");
    // Commands are auto-executed on the local engine
};

// Broadcast audio to all clients
await AudioStreamingService.Instance.BroadcastAudioAsync(audioSamples);

// Stop server
AudioStreamingService.Instance.StopServer();
```

### Client Mode

```csharp
// Connect to a server
await AudioStreamingService.Instance.ConnectAsync("192.168.1.100", 45678);

// Send audio to server
await AudioStreamingService.Instance.SendAudioAsync(microphoneSamples);

// Or send commands (much lower bandwidth)
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.PlayNote,
    new Dictionary<string, object> { ["note"] = 60 });

// Disconnect
AudioStreamingService.Instance.Disconnect();
```

## Streaming Modes

### 1. Raw PCM Mode

Sends float audio samples directly. Best for:
- Microphone input
- Real-time audio effects
- Voice chat

```csharp
AudioStreamingService.Instance.Mode = StreamingMode.RawPCM;

// Send float samples
float[] samples = GetMicrophoneAudio();
await AudioStreamingService.Instance.SendAudioAsync(samples);

// Or send raw bytes (16-bit PCM)
byte[] pcmData = GetMicrophoneBytes();
await AudioStreamingService.Instance.SendAudioBytesAsync(pcmData);
```

**Bandwidth**: ~192 kbps for mono 48kHz, ~384 kbps for stereo

### 2. Commands Only Mode

Sends MusicEngine commands. The remote end renders locally. Best for:
- Music playback sync
- Collaborative music creation
- Game audio synchronization

```csharp
AudioStreamingService.Instance.Mode = StreamingMode.CommandsOnly;

// Send playback commands
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.Play);
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.SetBPM,
    new() { ["bpm"] = 140.0 });

// Send note commands
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.PlayNote,
    new() { ["note"] = 60, ["velocity"] = 100 });

// Control variables remotely
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.SetVariable,
    new() { ["name"] = "MasterVolume", ["value"] = 0.8f });
```

**Bandwidth**: ~1-10 kbps depending on activity

### 3. Hybrid Mode

Use commands for music, PCM for voice. Best for:
- Music collaboration with voice chat
- Game audio with voice comms

```csharp
AudioStreamingService.Instance.Mode = StreamingMode.Hybrid;

// Music via commands
await AudioStreamingService.Instance.SendCommandAsync(StreamingCommand.Play);

// Voice via raw audio
await AudioStreamingService.Instance.SendAudioAsync(voiceSamples);
```

## Available Commands

| Command | Parameters | Description |
|---------|-----------|-------------|
| `Play` | - | Start playback |
| `Stop` | - | Stop playback |
| `Pause` | - | Pause playback |
| `SeekTo` | position (double) | Seek to beat |
| `SetBPM` | bpm (double) | Set tempo |
| `PlayNote` | note (int), velocity (int) | Play MIDI note |
| `StopNote` | note (int) | Stop MIDI note |
| `AllNotesOff` | - | Stop all notes |
| `SetVariable` | name, value | Set external variable |
| `TriggerEvent` | event, param | Trigger external event |
| `LoadProject` | path (string) | Load project file |
| `SyncState` | - | Request full state sync |

## Protocol Details

### Packet Structure

```
+--------+----------+------------+----------+-------------+
| Type   | Timestamp| SampleRate | Channels | Payload     |
| 1 byte | 8 bytes  | 4 bytes    | 1 byte   | Variable    |
+--------+----------+------------+----------+-------------+
```

### Packet Types

| Type | Value | Description |
|------|-------|-------------|
| AudioPCM | 1 | Float audio samples |
| AudioRaw | 2 | Raw byte audio data |
| Command | 3 | MusicEngine command |
| Heartbeat | 4 | Keep-alive packet |
| Metadata | 5 | Track/project info |

### Transport

- **UDP** (default): Lower latency, no guaranteed delivery
- **TCP**: Reliable delivery, slightly higher latency

```csharp
// UDP (default) - for real-time audio
await AudioStreamingService.Instance.StartServerAsync(45678, useTcp: false);

// TCP - for reliable command delivery
await AudioStreamingService.Instance.StartServerAsync(45678, useTcp: true);
```

## Voice Chat Example

### Sender (Microphone)

```csharp
public class VoiceSender
{
    private WaveInEvent _waveIn;

    public async Task StartAsync(string serverHost)
    {
        await AudioStreamingService.Instance.ConnectAsync(serverHost);

        _waveIn = new WaveInEvent
        {
            WaveFormat = new WaveFormat(48000, 16, 1)
        };

        _waveIn.DataAvailable += async (s, e) =>
        {
            await AudioStreamingService.Instance.SendAudioBytesAsync(e.Buffer);
        };

        _waveIn.StartRecording();
    }

    public void Stop()
    {
        _waveIn?.StopRecording();
        AudioStreamingService.Instance.Disconnect();
    }
}
```

### Receiver (Speaker)

```csharp
public class VoiceReceiver
{
    private BufferedWaveProvider _buffer;
    private WaveOutEvent _waveOut;

    public async Task StartAsync()
    {
        await AudioStreamingService.Instance.StartServerAsync();

        _buffer = new BufferedWaveProvider(new WaveFormat(48000, 16, 1));
        _waveOut = new WaveOutEvent();
        _waveOut.Init(_buffer);
        _waveOut.Play();

        AudioStreamingService.Instance.AudioReceived += (s, e) =>
        {
            if (e.RawData != null)
            {
                _buffer.AddSamples(e.RawData, 0, e.RawData.Length);
            }
        };
    }
}
```

## Music Collaboration Example

### Host

```csharp
public class CollaborationHost
{
    public async Task StartSessionAsync()
    {
        await AudioStreamingService.Instance.StartServerAsync();

        // Broadcast playback events to all clients
        PlaybackService.Instance.PlaybackStarted += async (s, e) =>
        {
            await AudioStreamingService.Instance.SendCommandAsync(
                StreamingCommand.Play);
        };

        PlaybackService.Instance.BpmChanged += async (s, newBpm) =>
        {
            await AudioStreamingService.Instance.SendCommandAsync(
                StreamingCommand.SetBPM, new() { ["bpm"] = newBpm });
        };
    }
}
```

### Participant

```csharp
public class CollaborationClient
{
    public async Task JoinSessionAsync(string hostIp)
    {
        await AudioStreamingService.Instance.ConnectAsync(hostIp);

        // Commands are automatically executed locally
        AudioStreamingService.Instance.CommandReceived += (s, e) =>
        {
            Console.WriteLine($"Received: {e.Command}");
        };
    }

    public async Task SendNoteAsync(int note, int velocity)
    {
        // Send note to host for all participants
        await AudioStreamingService.Instance.SendCommandAsync(
            StreamingCommand.PlayNote,
            new() { ["note"] = note, ["velocity"] = velocity });
    }
}
```

## Statistics

Monitor streaming performance:

```csharp
var stats = AudioStreamingService.Instance.Statistics;

Console.WriteLine($"Packets Sent: {stats.PacketsSent}");
Console.WriteLine($"Packets Received: {stats.PacketsReceived}");
Console.WriteLine($"Bytes Sent: {stats.BytesSent}");
Console.WriteLine($"Bytes Received: {stats.BytesReceived}");
Console.WriteLine($"Connected Clients: {stats.ClientCount}");
```

## Events

| Event | Description |
|-------|-------------|
| `AudioReceived` | Raw audio data received |
| `CommandReceived` | Command packet received |
| `ClientConnected` | Client connected (server) |
| `ClientDisconnected` | Client disconnected (server) |
| `ConnectionStateChanged` | Connection state changed |

## Network Considerations

### Latency Optimization

1. Use UDP for real-time audio
2. Keep packet size under MTU (1400 bytes)
3. Use local network when possible
4. Consider jitter buffer for playback

### Firewall/NAT

- Default port: 45678
- Protocol: UDP or TCP
- May need port forwarding for WAN connections

### Bandwidth Requirements

| Mode | Mono 48kHz | Stereo 48kHz |
|------|------------|--------------|
| Raw PCM (float) | ~384 kbps | ~768 kbps |
| Raw PCM (16-bit) | ~192 kbps | ~384 kbps |
| Commands Only | ~1-10 kbps | ~1-10 kbps |

## Thread Safety

All methods are thread-safe. Network operations run on background threads.

## Text Messaging (P2P Chat)

Send text messages directly without a server:

```csharp
// Set your user info
AudioStreamingService.Instance.LocalUserName = "MyUsername";

// Listen for incoming messages
AudioStreamingService.Instance.TextMessageReceived += (s, e) =>
{
    Console.WriteLine($"[{e.SenderId}]: {e.Message}");
};

// Send a message
await AudioStreamingService.Instance.SendTextMessageAsync("Hello!");
```

## Direct Calls (P2P Voice)

Request and manage voice calls:

```csharp
// Listen for incoming calls
AudioStreamingService.Instance.CallRequestReceived += async (s, e) =>
{
    Console.WriteLine($"Incoming call from {e.CallerName}");

    // Accept or reject
    bool accept = AskUserToAccept();
    await AudioStreamingService.Instance.RespondToCallAsync(accept, e.CallerId);
};

// Listen for call responses
AudioStreamingService.Instance.CallResponseReceived += (s, e) =>
{
    if (e.Accepted)
    {
        Console.WriteLine("Call accepted! Starting audio...");
        StartMicrophoneStreaming();
    }
    else
    {
        Console.WriteLine("Call rejected.");
    }
};

// Request a call
await AudioStreamingService.Instance.RequestCallAsync();

// Check if in call
if (AudioStreamingService.Instance.IsInCall)
{
    // Stream microphone audio
    await AudioStreamingService.Instance.SendAudioBytesAsync(micData);
}

// End the call
await AudioStreamingService.Instance.EndCallAsync();
```

## Complete P2P Chat App Example

```csharp
public class SimpleChatApp
{
    public async Task StartAsync(string peerIp)
    {
        var streaming = AudioStreamingService.Instance;
        streaming.LocalUserName = "User1";

        // Start as server (one side)
        await streaming.StartServerAsync();

        // Or connect as client (other side)
        // await streaming.ConnectAsync(peerIp);

        // Handle messages
        streaming.TextMessageReceived += (s, e) =>
        {
            Console.WriteLine($"{e.SenderId}: {e.Message}");
        };

        // Handle calls
        streaming.CallRequestReceived += async (s, e) =>
        {
            Console.WriteLine($"Call from {e.CallerName}");
            await streaming.RespondToCallAsync(true, e.CallerId);
        };

        streaming.AudioReceived += (s, e) =>
        {
            // Play received audio
            PlayAudio(e.RawData);
        };

        // Chat loop
        while (true)
        {
            var input = Console.ReadLine();
            if (input == "/call")
                await streaming.RequestCallAsync();
            else if (input == "/hangup")
                await streaming.EndCallAsync();
            else
                await streaming.SendTextMessageAsync(input);
        }
    }
}
```

## P2P Architecture

```
┌─────────────┐                         ┌─────────────┐
│   PC 1      │                         │   PC 2      │
│             │                         │             │
│  Server +   │ ◄────── UDP/TCP ──────► │  Client     │
│  Client     │                         │             │
│             │   Text + Audio + Cmds   │             │
└─────────────┘                         └─────────────┘

No central server required!
Both peers can send and receive.
```

## Future Extensions

- Opus compression for lower bandwidth
- STUN/TURN for NAT traversal (for connections over internet)
- WebRTC support for browser clients
- End-to-end encryption
- Group calls (multiple peers)
