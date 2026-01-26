
![BannerEditor](https://github.com/user-attachments/assets/d0751482-093f-4d9f-b980-4da5137bf8bf)

# MusicEngineEditor

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)

**MusicEngineEditor** is a professional code editor for the **MusicEngine** audio scripting system. Create music through code with real-time visualization, inline parameter controls, and VCV Rack-style modulation.

> **Note:** The core MusicEngine was written manually. The Editor and many features are AI-enhanced and may still have rough edges. Contributions welcome!

Discord: discord.gg/tWkqHMsB6a
---

## Features

### Code Editor
- Syntax highlighting optimized for MusicEngine scripts
- Intelligent autocomplete for classes, methods, and parameters
- Strudel-style inline sliders (drag numbers to change values)
- Live code visualization (active patterns glow)
- Dark/Light themes

### Audio Engine
- Real-time audio playback and preview
- Multiple synthesizer types (Simple, Advanced, Modular)
- Built-in effects (Reverb, Delay, Filter, etc.)
- MIDI input/output support
- VCV Rack-style modular parameter system

### Workflow
- Project management
- Pattern and arrangement editor
- Waveform visualization
- Performance monitoring
- VST plugin support

---

## Quick Start

**No programming knowledge required:**

```bash
git clone https://github.com/watermann420/MusicEngineEditor.git
```

Then **double-click `StartEditor.bat`** - done!

---

## Code Example

```csharp
// Create a sequencer
var seq = new Sequencer { Bpm = 120 };

// Create instruments
var synth = new AdvancedSynth();
synth.FilterType = SynthFilterType.MoogLadder;
synth.FilterCutoff = 0.6f;

// Create a pattern
var melody = seq.CreatePattern("melody", synth);

// Add notes (pitch, beat, duration, velocity)
melody.Note(60, 0, 0.5, 100);    // C4
melody.Note(64, 0.5, 0.5, 100);  // E4
melody.Note(67, 1, 0.5, 100);    // G4

// Play!
seq.Play();
```

---


## VCV Rack-style Modulation

Every parameter can be modulated by any source:

```csharp
// Create modulation sources
var lfo = new ModularLFO("lfo1", "Filter LFO", sampleRate);
lfo.Rate.Value = 2.0;      // 2 Hz
lfo.Depth.Value = 0.5;     // 50% depth

// Connect to filter cutoff
synth.Connect(lfo, synth.GetParameter("cutoff"), 0.5);

// Create envelope modulation
var env = new ModularEnvelope("env1", "Amp Env", sampleRate);
synth.Connect(env, synth.GetParameter("volume"), 1.0);
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MusicEngine](https://github.com/watermann420/MusicEngine)

---

## Installation

### Option 1: StartEditor.bat (Recommended)

1. Clone the repository
2. Double-click `StartEditor.bat`

### Option 2: Manual

```bash
git clone https://github.com/watermann420/MusicEngineEditor.git
cd MusicEngineEditor
dotnet build
dotnet run --project MusicEngineEditor
```

---

## Project Structure

```
MusicEngineEditor/
├── Controls/        # UI controls (meters, visualizations)
├── Editor/          # Code editor components
├── Models/          # Data models
├── Services/        # Business logic
├── ViewModels/      # MVVM ViewModels
├── Views/           # XAML Views
└── Themes/          # Dark/Light themes

MusicEngine/         # Core audio engine (separate repo)
└── Core/
    ├── Sequencer.cs
    ├── Pattern.cs
    ├── Effects/
    ├── Analysis/
    └── Modulation/  # VCV Rack-style system
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Project structure overview
- Syntax guide with examples
- Code style guidelines
- Pull request process

---

## Documentation

- [Modulation System](docs/MODULATION_SYSTEM.md) - VCV Rack-style parameter modulation
- [CONTRIBUTING Guide](CONTRIBUTING.md) - How to contribute

---

## License

[MusicEngine License (MEL)](LICENSE) - Honor-Based Commercial Support

---

## Links

- [MusicEngine Core](https://github.com/watermann420/MusicEngine)
- [Report Issues](https://github.com/watermann420/MusicEngineEditor/issues)
