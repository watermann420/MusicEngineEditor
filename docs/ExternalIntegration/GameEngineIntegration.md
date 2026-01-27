# Game Engine Integration

This document describes how to integrate MusicEngine with game engines (Unity, Unreal, Godot, custom engines) or other external applications.

## Overview

The `ExternalControlService` provides a simple API for:
- **Variable Binding**: Get/set engine parameters (BPM, volume, effects, custom values)
- **Event Triggers**: Play, stop, trigger patterns, fire custom events
- **State Queries**: Current playback position, active patterns, engine state

## Quick Start

### Enable External Control

```csharp
// Enable the external control service
ExternalControlService.Instance.IsEnabled = true;
```

### Set Variables from Game Engine

```csharp
// Set BPM based on game intensity
ExternalControlService.Instance.SetVariable("BPM", 140.0);

// Set master volume
ExternalControlService.Instance.SetVariable("MasterVolume", 0.8f);

// Set game-specific variables
ExternalControlService.Instance.SetVariable("IntensityLevel", 0.7f);
ExternalControlService.Instance.SetVariable("CombatActive", true);
ExternalControlService.Instance.SetVariable("EnvironmentType", "dungeon");
```

### Get Variable Values

```csharp
// Get current BPM
double bpm = ExternalControlService.Instance.GetVariable<double>("BPM", 120.0);

// Get playback state
bool isPlaying = PlaybackService.Instance.IsPlaying;
double currentBeat = PlaybackService.Instance.CurrentBeat;
```

### Trigger Events

```csharp
// Start/stop playback
ExternalControlService.Instance.TriggerEvent("Play");
ExternalControlService.Instance.TriggerEvent("Stop");
ExternalControlService.Instance.TriggerEvent("TogglePlayback");

// Seek to position
ExternalControlService.Instance.TriggerEvent("SeekTo", 16.0); // beat 16

// Set BPM via event
ExternalControlService.Instance.TriggerEvent("SetBPM", 140.0);

// Play a note (for sound effects)
ExternalControlService.Instance.TriggerEvent("PlayNote", 60); // Middle C
ExternalControlService.Instance.TriggerEvent("StopNote", 60);

// Adaptive music
ExternalControlService.Instance.TriggerEvent("SetIntensity", 0.8f);
ExternalControlService.Instance.TriggerEvent("TransitionTo", "combat");
```

## Default Variables

| Variable Name | Type | Range | Description |
|--------------|------|-------|-------------|
| `BPM` | double | 20-999 | Beats per minute |
| `MasterVolume` | float | 0-1 | Master output volume |
| `PlaybackPosition` | double | 0-... | Current beat position |
| `LoopStart` | double | 0-... | Loop start beat |
| `LoopEnd` | double | 0-... | Loop end beat |
| `LoopEnabled` | bool | - | Loop on/off |
| `Track1Volume` | float | 0-2 | Track 1 volume |
| `Track1Pan` | float | -1 to 1 | Track 1 pan |
| `Track1Mute` | bool | - | Track 1 mute |
| `Track1Solo` | bool | - | Track 1 solo |
| `ReverbMix` | float | 0-1 | Reverb wet/dry |
| `DelayTime` | float | 0.01-2 | Delay time (seconds) |
| `FilterCutoff` | float | 20-20000 | Filter cutoff Hz |
| `FilterResonance` | float | 0-1 | Filter resonance |
| `IntensityLevel` | float | 0-1 | Game intensity for adaptive music |
| `EnvironmentType` | string | - | Current game environment |
| `CombatActive` | bool | - | Combat state |
| `PlayerHealth` | float | 0-1 | Player health percentage |

## Default Events

| Event Name | Parameter | Description |
|-----------|-----------|-------------|
| `Play` | - | Start playback |
| `Stop` | - | Stop playback |
| `Pause` | - | Pause playback |
| `TogglePlayback` | - | Toggle play/pause |
| `SeekTo` | double (beat) | Seek to beat position |
| `SeekToStart` | - | Seek to beginning |
| `SetBPM` | double | Set tempo |
| `TriggerPattern` | string (name) | Trigger pattern by name |
| `PlayNote` | int (MIDI note) | Play a note |
| `StopNote` | int (MIDI note) | Stop a note |
| `AllNotesOff` | - | Panic - stop all notes |
| `SetIntensity` | float (0-1) | Set music intensity |
| `TransitionTo` | string (section) | Transition to section |

## Custom Variables and Events

### Register Custom Variable

```csharp
// Register a custom variable
ExternalControlService.Instance.RegisterVariable(
    name: "EnemyCount",
    defaultValue: 0,
    minValue: 0,
    maxValue: 100,
    description: "Number of active enemies"
);

// Listen for changes
ExternalControlService.Instance.OnVariableChanged("EnemyCount", variable =>
{
    int count = (int)variable.Value;
    // Adjust music intensity based on enemy count
    float intensity = Math.Min(count / 10.0f, 1.0f);
    ExternalControlService.Instance.SetVariable("IntensityLevel", intensity);
});
```

### Register Custom Event

```csharp
// Register a custom event
ExternalControlService.Instance.RegisterEvent("BossSpawn", param =>
{
    // Trigger boss music
    ExternalControlService.Instance.TriggerEvent("TransitionTo", "boss_fight");
    ExternalControlService.Instance.SetVariable("IntensityLevel", 1.0f);
});

// Later, trigger from game:
ExternalControlService.Instance.TriggerEvent("BossSpawn");
```

## Batch Updates

For performance, update multiple variables at once:

```csharp
// Set multiple variables in one call
ExternalControlService.Instance.SetVariables(new Dictionary<string, object?>
{
    ["IntensityLevel"] = 0.8f,
    ["CombatActive"] = true,
    ["EnvironmentType"] = "boss_arena",
    ["BPM"] = 160.0
});

// Get all variables at once
var state = ExternalControlService.Instance.GetAllVariables();
```

## Event Subscriptions

Listen for changes from the engine:

```csharp
// Listen for variable changes
ExternalControlService.Instance.VariableChanged += (sender, args) =>
{
    Console.WriteLine($"Variable {args.VariableName} changed from {args.OldValue} to {args.NewValue}");
};

// Listen for external events
ExternalControlService.Instance.ExternalEventTriggered += (sender, args) =>
{
    Console.WriteLine($"Event {args.EventName} triggered with param: {args.Parameter}");
};
```

## Unity Integration Example

```csharp
// Unity MonoBehaviour example
public class MusicEngineController : MonoBehaviour
{
    private void Start()
    {
        // Enable external control
        ExternalControlService.Instance.IsEnabled = true;
    }

    private void Update()
    {
        // Sync player health to music engine
        float health = PlayerManager.Instance.HealthPercentage;
        ExternalControlService.Instance.SetVariable("PlayerHealth", health);

        // Combat music when enemies nearby
        bool inCombat = EnemyManager.Instance.EnemiesNearby > 0;
        ExternalControlService.Instance.SetVariable("CombatActive", inCombat);
    }

    public void OnBossSpawn()
    {
        ExternalControlService.Instance.TriggerEvent("BossSpawn");
    }

    public void OnPlayerDeath()
    {
        ExternalControlService.Instance.TriggerEvent("Stop");
    }
}
```

## Godot Integration Example (C#)

```csharp
// Godot Node example
public partial class MusicController : Node
{
    public override void _Ready()
    {
        ExternalControlService.Instance.IsEnabled = true;
    }

    public override void _Process(double delta)
    {
        // Update music based on game state
        var player = GetNode<Player>("/root/Player");
        ExternalControlService.Instance.SetVariable("PlayerHealth", player.HealthPercent);
    }

    public void OnAreaEntered(string areaType)
    {
        ExternalControlService.Instance.SetVariable("EnvironmentType", areaType);
        ExternalControlService.Instance.TriggerEvent("TransitionTo", areaType);
    }
}
```

## Thread Safety

The `ExternalControlService` is thread-safe. You can call methods from any thread (game thread, audio thread, network thread).

## Performance Tips

1. **Batch updates** when setting multiple variables
2. **Use events** for one-time triggers, **variables** for continuous values
3. **Register callbacks** instead of polling for changes
4. **Disable** the service when not needed: `IsEnabled = false`
