# Game Engine Integration - Developer Guide

This guide explains how to integrate MusicEngine into game engines like Unity, Unreal, Godot, or your own custom engine.

## Overview

The `ExternalControlService` provides a bridge between your game and MusicEngine:

- **Variables**: Set values from your game (BPM, intensity, health, etc.)
- **Events**: Trigger music actions (play, stop, transition, play notes)
- **Callbacks**: React to music events in your game

## Architecture

```
┌─────────────────┐         ┌──────────────────────┐         ┌─────────────────┐
│   Game Engine   │ ──────► │ ExternalControlService│ ──────► │  MusicEngine    │
│   (Unity etc.)  │ ◄────── │     (Bridge)          │ ◄────── │  (Audio Core)   │
└─────────────────┘         └──────────────────────┘         └─────────────────┘
        │                            │                               │
        │  SetVariable()             │  Variables Dictionary         │  PlaybackService
        │  TriggerEvent()            │  Events Dictionary            │  AudioEngine
        │  RegisterCallback()        │  Callbacks                    │  Tracks
        │                            │                               │
```

## Quick Start

### 1. Get the Service Instance

```csharp
using MusicEngineEditor.Services;

var control = ExternalControlService.Instance;
```

### 2. Set Variables

```csharp
// Set BPM (affects playback speed)
control.SetVariable("BPM", 140.0f);

// Set master volume
control.SetVariable("MasterVolume", 0.8f);

// Set game-specific variables
control.SetVariable("IntensityLevel", 0.7f);
control.SetVariable("CombatActive", true);
control.SetVariable("PlayerHealth", 85.0f);
```

### 3. Trigger Events

```csharp
// Start/stop playback
control.TriggerEvent("Play");
control.TriggerEvent("Stop");

// Seek to beat
control.TriggerEvent("SeekTo", 16.0f);  // Jump to beat 16

// Set BPM via event
control.TriggerEvent("SetBPM", 120.0f);

// Play a note
control.TriggerEvent("PlayNote", 60);  // MIDI note 60 = C4

// Transition to different music state
control.TriggerEvent("TransitionTo", "Combat");
```

## Built-in Variables

| Variable | Type | Default | Description |
|----------|------|---------|-------------|
| `BPM` | Float | 120.0 | Playback tempo |
| `MasterVolume` | Float | 1.0 | Master volume (0.0 - 1.0) |
| `IntensityLevel` | Float | 0.0 | Music intensity (0.0 - 1.0) |
| `CombatActive` | Bool | false | Combat state flag |
| `PlayerHealth` | Float | 100.0 | Player health value |
| `EnemyCount` | Int | 0 | Number of enemies |
| `TimeOfDay` | Float | 12.0 | Time of day (0.0 - 24.0) |
| `CurrentBiome` | String | "default" | Current world biome |
| `MusicState` | String | "exploration" | Current music state |

## Built-in Events

| Event | Parameters | Description |
|-------|------------|-------------|
| `Play` | none | Start playback |
| `Stop` | none | Stop playback |
| `Pause` | none | Pause playback |
| `Resume` | none | Resume playback |
| `SeekTo` | float (beat) | Seek to beat position |
| `SetBPM` | float | Set tempo |
| `PlayNote` | int (MIDI note) | Play a note |
| `StopNote` | int (MIDI note) | Stop a note |
| `TransitionTo` | string (state) | Transition music state |
| `SetVolume` | float | Set master volume |
| `Mute` | none | Mute audio |
| `Unmute` | none | Unmute audio |

## Custom Variables

### Register a Variable

```csharp
// Register with callback
control.RegisterVariable(
    name: "DangerLevel",
    type: VariableType.Float,
    defaultValue: 0.0f,
    onChange: value => {
        // React to changes
        float danger = (float)value;
        if (danger > 0.8f)
            control.TriggerEvent("TransitionTo", "Combat");
        else if (danger < 0.2f)
            control.TriggerEvent("TransitionTo", "Calm");
    }
);

// Use in game
control.SetVariable("DangerLevel", CalculateDanger());
```

### Variable Types

```csharp
public enum VariableType
{
    Float,    // 32-bit floating point
    Int,      // 32-bit integer
    Bool,     // Boolean
    String,   // Text string
    Vector2,  // 2D vector (x, y)
    Vector3,  // 3D vector (x, y, z)
    Color     // RGBA color
}
```

## Custom Events

### Register an Event

```csharp
control.RegisterEvent(
    name: "BossPhase",
    handler: (parameters) => {
        int phase = parameters.Length > 0 ? (int)parameters[0] : 1;

        switch (phase)
        {
            case 1:
                // Start boss intro music
                control.TriggerEvent("TransitionTo", "BossIntro");
                break;
            case 2:
                // Intensify
                control.SetVariable("IntensityLevel", 0.8f);
                break;
            case 3:
                // Final phase - maximum intensity
                control.SetVariable("IntensityLevel", 1.0f);
                control.SetVariable("BPM", 160.0f);
                break;
        }
    }
);

// Trigger from game
control.TriggerEvent("BossPhase", 2);
```

## Unity Integration

### MusicEngineManager.cs

```csharp
using UnityEngine;
using MusicEngineEditor.Services;

public class MusicEngineManager : MonoBehaviour
{
    public static MusicEngineManager Instance { get; private set; }

    private ExternalControlService _control;

    [Header("Settings")]
    public float initialBPM = 120f;
    public float masterVolume = 1f;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _control = ExternalControlService.Instance;

        // Initialize
        _control.SetVariable("BPM", initialBPM);
        _control.SetVariable("MasterVolume", masterVolume);
    }

    void OnDestroy()
    {
        _control.TriggerEvent("Stop");
    }

    // Public API for other scripts
    public void Play() => _control.TriggerEvent("Play");
    public void Stop() => _control.TriggerEvent("Stop");
    public void SetBPM(float bpm) => _control.SetVariable("BPM", bpm);
    public void SetIntensity(float intensity) => _control.SetVariable("IntensityLevel", intensity);
    public void TransitionTo(string state) => _control.TriggerEvent("TransitionTo", state);
    public void PlayNote(int midiNote) => _control.TriggerEvent("PlayNote", midiNote);
}
```

### CombatMusicController.cs

```csharp
using UnityEngine;

public class CombatMusicController : MonoBehaviour
{
    [SerializeField] private float transitionSpeed = 2f;

    private float _currentIntensity;
    private float _targetIntensity;
    private int _enemyCount;

    void Update()
    {
        // Smooth intensity transitions
        _currentIntensity = Mathf.MoveTowards(
            _currentIntensity,
            _targetIntensity,
            transitionSpeed * Time.deltaTime
        );

        MusicEngineManager.Instance.SetIntensity(_currentIntensity);
    }

    public void OnEnemySpawned()
    {
        _enemyCount++;
        UpdateIntensity();
    }

    public void OnEnemyDefeated()
    {
        _enemyCount = Mathf.Max(0, _enemyCount - 1);
        UpdateIntensity();

        if (_enemyCount == 0)
        {
            MusicEngineManager.Instance.TransitionTo("Exploration");
        }
    }

    public void OnCombatStarted()
    {
        MusicEngineManager.Instance.TransitionTo("Combat");
    }

    private void UpdateIntensity()
    {
        // Calculate intensity based on enemy count
        _targetIntensity = Mathf.Clamp01(_enemyCount / 10f);
    }
}
```

### HealthMusicSync.cs

```csharp
using UnityEngine;
using MusicEngineEditor.Services;

public class HealthMusicSync : MonoBehaviour
{
    [SerializeField] private float lowHealthThreshold = 25f;
    [SerializeField] private float criticalHealthThreshold = 10f;

    private float _lastHealth = 100f;
    private ExternalControlService _control;

    void Start()
    {
        _control = ExternalControlService.Instance;
    }

    public void OnHealthChanged(float currentHealth, float maxHealth)
    {
        float healthPercent = (currentHealth / maxHealth) * 100f;

        _control.SetVariable("PlayerHealth", healthPercent);

        // Trigger music changes at thresholds
        if (healthPercent <= criticalHealthThreshold && _lastHealth > criticalHealthThreshold)
        {
            // Critical health - intense music
            _control.SetVariable("IntensityLevel", 1.0f);
            _control.TriggerEvent("TransitionTo", "Critical");
        }
        else if (healthPercent <= lowHealthThreshold && _lastHealth > lowHealthThreshold)
        {
            // Low health - tense music
            _control.SetVariable("IntensityLevel", 0.7f);
            _control.TriggerEvent("TransitionTo", "LowHealth");
        }
        else if (healthPercent > lowHealthThreshold && _lastHealth <= lowHealthThreshold)
        {
            // Recovered - normal music
            _control.TriggerEvent("TransitionTo", "Normal");
        }

        _lastHealth = healthPercent;
    }
}
```

## Godot Integration

### MusicEngine.gd

```gdscript
extends Node

# Singleton - add to AutoLoad
var _control = null

signal music_state_changed(state)
signal beat_triggered(beat)

func _ready():
    _control = ExternalControlService.Instance

    # Register callbacks
    _control.RegisterCallback("OnBeat", funcref(self, "_on_beat"))
    _control.RegisterCallback("OnStateChange", funcref(self, "_on_state_change"))

func _exit_tree():
    trigger_event("Stop")

# Public API
func play():
    trigger_event("Play")

func stop():
    trigger_event("Stop")

func set_bpm(bpm: float):
    set_variable("BPM", bpm)

func set_intensity(intensity: float):
    set_variable("IntensityLevel", clamp(intensity, 0.0, 1.0))

func transition_to(state: String):
    trigger_event("TransitionTo", state)

func play_note(midi_note: int):
    trigger_event("PlayNote", midi_note)

# Internal
func set_variable(name: String, value):
    _control.SetVariable(name, value)

func get_variable(name: String):
    return _control.GetVariable(name)

func trigger_event(name: String, param = null):
    if param != null:
        _control.TriggerEvent(name, param)
    else:
        _control.TriggerEvent(name)

# Callbacks
func _on_beat(beat: int):
    emit_signal("beat_triggered", beat)

func _on_state_change(state: String):
    emit_signal("music_state_changed", state)
```

### CombatMusic.gd

```gdscript
extends Node

export var transition_speed := 2.0

var current_intensity := 0.0
var target_intensity := 0.0
var enemy_count := 0

func _process(delta):
    current_intensity = move_toward(current_intensity, target_intensity, transition_speed * delta)
    MusicEngine.set_intensity(current_intensity)

func on_enemy_spawned():
    enemy_count += 1
    _update_intensity()

func on_enemy_defeated():
    enemy_count = max(0, enemy_count - 1)
    _update_intensity()

    if enemy_count == 0:
        MusicEngine.transition_to("Exploration")

func on_combat_started():
    MusicEngine.transition_to("Combat")

func _update_intensity():
    target_intensity = clamp(enemy_count / 10.0, 0.0, 1.0)
```

## Unreal Engine Integration (C++)

### MusicEngineSubsystem.h

```cpp
#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "MusicEngineSubsystem.generated.h"

UCLASS()
class MYGAME_API UMusicEngineSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

    UFUNCTION(BlueprintCallable, Category = "Music")
    void Play();

    UFUNCTION(BlueprintCallable, Category = "Music")
    void Stop();

    UFUNCTION(BlueprintCallable, Category = "Music")
    void SetBPM(float BPM);

    UFUNCTION(BlueprintCallable, Category = "Music")
    void SetIntensity(float Intensity);

    UFUNCTION(BlueprintCallable, Category = "Music")
    void TransitionTo(const FString& State);

    UFUNCTION(BlueprintCallable, Category = "Music")
    void PlayNote(int32 MidiNote);

    UFUNCTION(BlueprintCallable, Category = "Music")
    void SetVariable(const FString& Name, float Value);

    UFUNCTION(BlueprintCallable, Category = "Music")
    float GetVariable(const FString& Name);

private:
    // Reference to ExternalControlService
    void* ControlService;
};
```

### MusicEngineSubsystem.cpp

```cpp
#include "MusicEngineSubsystem.h"

// Assuming MusicEngine is compiled as a DLL
extern "C" {
    void* GetExternalControlService();
    void SetVariable(void* service, const char* name, float value);
    float GetVariable(void* service, const char* name);
    void TriggerEvent(void* service, const char* name);
    void TriggerEventWithFloat(void* service, const char* name, float param);
    void TriggerEventWithString(void* service, const char* name, const char* param);
    void TriggerEventWithInt(void* service, const char* name, int param);
}

void UMusicEngineSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    ControlService = GetExternalControlService();
}

void UMusicEngineSubsystem::Deinitialize()
{
    Stop();
    Super::Deinitialize();
}

void UMusicEngineSubsystem::Play()
{
    TriggerEvent(ControlService, "Play");
}

void UMusicEngineSubsystem::Stop()
{
    TriggerEvent(ControlService, "Stop");
}

void UMusicEngineSubsystem::SetBPM(float BPM)
{
    SetVariable(ControlService, "BPM", BPM);
}

void UMusicEngineSubsystem::SetIntensity(float Intensity)
{
    SetVariable(ControlService, "IntensityLevel", FMath::Clamp(Intensity, 0.0f, 1.0f));
}

void UMusicEngineSubsystem::TransitionTo(const FString& State)
{
    TriggerEventWithString(ControlService, "TransitionTo", TCHAR_TO_ANSI(*State));
}

void UMusicEngineSubsystem::PlayNote(int32 MidiNote)
{
    TriggerEventWithInt(ControlService, "PlayNote", MidiNote);
}

void UMusicEngineSubsystem::SetVariable(const FString& Name, float Value)
{
    ::SetVariable(ControlService, TCHAR_TO_ANSI(*Name), Value);
}

float UMusicEngineSubsystem::GetVariable(const FString& Name)
{
    return ::GetVariable(ControlService, TCHAR_TO_ANSI(*Name));
}
```

## Custom Engine Integration

### Basic C# Integration

```csharp
using MusicEngineEditor.Services;

public class GameMusicSystem
{
    private readonly ExternalControlService _control;

    public GameMusicSystem()
    {
        _control = ExternalControlService.Instance;

        // Register game-specific variables
        RegisterGameVariables();

        // Register game-specific events
        RegisterGameEvents();
    }

    private void RegisterGameVariables()
    {
        // Player state
        _control.RegisterVariable("PlayerHealth", VariableType.Float, 100f, OnHealthChanged);
        _control.RegisterVariable("PlayerMana", VariableType.Float, 100f, null);
        _control.RegisterVariable("PlayerPosition", VariableType.Vector3, Vector3.Zero, null);

        // World state
        _control.RegisterVariable("TimeOfDay", VariableType.Float, 12f, OnTimeChanged);
        _control.RegisterVariable("Weather", VariableType.String, "Clear", OnWeatherChanged);
        _control.RegisterVariable("CurrentZone", VariableType.String, "Town", OnZoneChanged);

        // Combat state
        _control.RegisterVariable("InCombat", VariableType.Bool, false, OnCombatStateChanged);
        _control.RegisterVariable("EnemyThreatLevel", VariableType.Float, 0f, null);
    }

    private void RegisterGameEvents()
    {
        _control.RegisterEvent("PlayerDied", OnPlayerDied);
        _control.RegisterEvent("BossEncounter", OnBossEncounter);
        _control.RegisterEvent("QuestComplete", OnQuestComplete);
        _control.RegisterEvent("Discovery", OnDiscovery);
    }

    // Variable callbacks
    private void OnHealthChanged(object value)
    {
        float health = (float)value;
        if (health < 20f)
            _control.SetVariable("IntensityLevel", 0.9f);
    }

    private void OnTimeChanged(object value)
    {
        float time = (float)value;
        if (time >= 20f || time <= 6f)
            _control.TriggerEvent("TransitionTo", "Night");
        else
            _control.TriggerEvent("TransitionTo", "Day");
    }

    private void OnWeatherChanged(object value)
    {
        string weather = (string)value;
        _control.TriggerEvent("WeatherMusic", weather);
    }

    private void OnZoneChanged(object value)
    {
        string zone = (string)value;
        _control.TriggerEvent("TransitionTo", zone);
    }

    private void OnCombatStateChanged(object value)
    {
        bool inCombat = (bool)value;
        _control.TriggerEvent("TransitionTo", inCombat ? "Combat" : "Exploration");
    }

    // Event handlers
    private void OnPlayerDied(object[] parameters)
    {
        _control.TriggerEvent("TransitionTo", "GameOver");
    }

    private void OnBossEncounter(object[] parameters)
    {
        string bossName = parameters.Length > 0 ? (string)parameters[0] : "Generic";
        _control.TriggerEvent("TransitionTo", $"Boss_{bossName}");
        _control.SetVariable("IntensityLevel", 1.0f);
    }

    private void OnQuestComplete(object[] parameters)
    {
        // Play victory stinger
        _control.TriggerEvent("PlayStinger", "QuestComplete");
    }

    private void OnDiscovery(object[] parameters)
    {
        // Play discovery sound
        _control.TriggerEvent("PlayStinger", "Discovery");
    }

    // Public API
    public void Update(float deltaTime)
    {
        // Update any continuous values
    }

    public void SetPlayerHealth(float health) => _control.SetVariable("PlayerHealth", health);
    public void SetTimeOfDay(float time) => _control.SetVariable("TimeOfDay", time);
    public void SetWeather(string weather) => _control.SetVariable("Weather", weather);
    public void EnterZone(string zone) => _control.SetVariable("CurrentZone", zone);
    public void SetCombatState(bool inCombat) => _control.SetVariable("InCombat", inCombat);
    public void TriggerBossEncounter(string bossName) => _control.TriggerEvent("BossEncounter", bossName);
}
```

## Advanced: Music State Machine

```csharp
public class MusicStateMachine
{
    private readonly ExternalControlService _control;
    private readonly Dictionary<string, MusicState> _states = new();
    private MusicState _currentState;

    public MusicStateMachine()
    {
        _control = ExternalControlService.Instance;
        DefineStates();
    }

    private void DefineStates()
    {
        // Define music states
        AddState(new MusicState("Menu")
        {
            BPM = 90,
            Intensity = 0.3f,
            Layers = new[] { "Ambient", "Melody" }
        });

        AddState(new MusicState("Exploration")
        {
            BPM = 100,
            Intensity = 0.4f,
            Layers = new[] { "Ambient", "Melody", "Percussion" }
        });

        AddState(new MusicState("Combat")
        {
            BPM = 140,
            Intensity = 0.8f,
            Layers = new[] { "Ambient", "Melody", "Percussion", "Bass", "Lead" }
        });

        AddState(new MusicState("BossFight")
        {
            BPM = 160,
            Intensity = 1.0f,
            Layers = new[] { "Ambient", "Melody", "Percussion", "Bass", "Lead", "Choir" }
        });

        AddState(new MusicState("Victory")
        {
            BPM = 120,
            Intensity = 0.6f,
            Layers = new[] { "Fanfare", "Melody" }
        });

        AddState(new MusicState("GameOver")
        {
            BPM = 60,
            Intensity = 0.2f,
            Layers = new[] { "Ambient" }
        });
    }

    private void AddState(MusicState state)
    {
        _states[state.Name] = state;
    }

    public void TransitionTo(string stateName, float transitionTime = 2.0f)
    {
        if (!_states.TryGetValue(stateName, out var newState))
            return;

        _currentState = newState;

        // Apply state settings
        _control.SetVariable("BPM", newState.BPM);
        _control.SetVariable("IntensityLevel", newState.Intensity);
        _control.TriggerEvent("TransitionTo", stateName);

        // Enable/disable layers
        foreach (var layer in newState.Layers)
        {
            _control.TriggerEvent("EnableLayer", layer);
        }
    }
}

public class MusicState
{
    public string Name { get; set; }
    public float BPM { get; set; }
    public float Intensity { get; set; }
    public string[] Layers { get; set; }

    public MusicState(string name)
    {
        Name = name;
        Layers = Array.Empty<string>();
    }
}
```

## Performance Tips

1. **Batch Variable Updates**: Don't update variables every frame if the value hasn't changed
2. **Use Events for Discrete Changes**: Use events for one-time triggers, variables for continuous values
3. **Smooth Transitions**: Interpolate values over time rather than instant changes
4. **Pre-register Everything**: Register all variables and events at startup, not during gameplay

## Troubleshooting

| Issue | Solution |
|-------|----------|
| No sound | Check if `Play` event was triggered |
| Delayed response | Reduce buffer size in MusicEngine settings |
| Crackling audio | Increase buffer size |
| Variable not working | Verify variable is registered with correct type |
| Event not triggering | Check event name spelling (case-sensitive) |
