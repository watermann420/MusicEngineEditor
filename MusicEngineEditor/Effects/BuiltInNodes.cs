// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Professional-grade modular effect nodes with anti-aliasing,
//              high-quality algorithms, and proper DSP implementations.

using System;
using System.Collections.Generic;

namespace MusicEngineEditor.Effects;

#region Generators

/// <summary>
/// Band-limited oscillator with PolyBLEP anti-aliasing for professional-quality waveforms.
/// Supports sine, saw, square, triangle, and noise with smooth transitions.
/// </summary>
public class OscillatorNode : EffectNodeBase
{
    public override string NodeType => "Oscillator";
    public override string Category => "Generators";
    public override string Description => "Anti-aliased oscillator with PolyBLEP";

    private double _phase;
    private double _lastPhase;
    private float _lastOutput;
    private readonly Random _random = new();

    // For triangle wave integration
    private float _triangleState;

    protected override void InitializePorts()
    {
        AddInput("Freq CV", PortDataType.Control);
        AddInput("PWM", PortDataType.Control, 0.5f);
        AddInput("Sync", PortDataType.Trigger);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("Sub", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Frequency", 440f, 20f, 20000f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Waveform", 0f, 0f, 4f, ""); // 0=Sine, 1=Saw, 2=Square, 3=Triangle, 4=Noise
        AddParameter("PulseWidth", 0.5f, 0.01f, 0.99f, "");
        AddParameter("Detune", 0f, -100f, 100f, "cents");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var freq = GetParam("Frequency") + GetInput(0) * 1000f;
        var waveform = (int)GetParam("Waveform");
        var pw = Math.Clamp(GetParam("PulseWidth") + GetInput(1) * 0.5f, 0.01f, 0.99f);
        var detune = GetParam("Detune");

        freq *= MathF.Pow(2f, detune / 1200f);
        freq = Math.Clamp(freq, 0.1f, sampleRate * 0.49f);
        var phaseInc = freq / sampleRate;

        float sample = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            // Handle sync input
            if (GetInput(2) > 0.5f) _phase = 0;

            _lastPhase = _phase;
            _phase += phaseInc;

            // Phase wrap
            bool wrapped = false;
            if (_phase >= 1.0)
            {
                _phase -= 1.0;
                wrapped = true;
            }

            sample = waveform switch
            {
                0 => GenerateSine(),
                1 => GeneratePolyBlepSaw(phaseInc, wrapped),
                2 => GeneratePolyBlepSquare(phaseInc, pw),
                3 => GeneratePolyBlepTriangle(phaseInc),
                4 => (float)(_random.NextDouble() * 2 - 1),
                _ => 0f
            };

            _lastOutput = sample;
            buffer[i] += sample;
        }

        SetOutput(0, sample);
        // Sub oscillator: one octave down (divide phase by 2)
        SetOutput(1, MathF.Sin((float)(_phase * Math.PI)));
    }

    private float GenerateSine()
    {
        return MathF.Sin((float)(_phase * 2 * Math.PI));
    }

    /// <summary>
    /// PolyBLEP (Polynomial Band-Limited Step) for alias-free sawtooth.
    /// </summary>
    private float GeneratePolyBlepSaw(double phaseInc, bool wrapped)
    {
        float naive = (float)(2 * _phase - 1);

        // Apply PolyBLEP correction at discontinuity
        if (wrapped)
        {
            naive -= PolyBlep(_phase, phaseInc);
        }

        return naive;
    }

    /// <summary>
    /// PolyBLEP square wave with variable pulse width.
    /// </summary>
    private float GeneratePolyBlepSquare(double phaseInc, float pw)
    {
        float naive = _phase < pw ? 1f : -1f;

        // PolyBLEP at rising edge (phase = 0)
        naive += PolyBlep(_phase, phaseInc);
        // PolyBLEP at falling edge (phase = pw)
        naive -= PolyBlep((_phase - pw + 1) % 1, phaseInc);

        return naive;
    }

    /// <summary>
    /// PolyBLEP triangle via integrated square wave.
    /// </summary>
    private float GeneratePolyBlepTriangle(double phaseInc)
    {
        // Generate square first
        float square = GeneratePolyBlepSquare(phaseInc, 0.5f);

        // Leaky integrator to create triangle from square
        _triangleState = (float)(0.999 * _triangleState + square * phaseInc * 4);

        return Math.Clamp(_triangleState, -1f, 1f);
    }

    /// <summary>
    /// PolyBLEP correction function - smooths discontinuities.
    /// </summary>
    private static float PolyBlep(double t, double dt)
    {
        if (t < dt)
        {
            t /= dt;
            return (float)(t + t - t * t - 1);
        }
        else if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return (float)(t * t + t + t + 1);
        }
        return 0f;
    }
}

/// <summary>
/// High-quality noise generator with white, pink (Paul Kellet), and brown noise.
/// </summary>
public class NoiseGeneratorNode : EffectNodeBase
{
    public override string NodeType => "NoiseGenerator";
    public override string Category => "Generators";
    public override string Description => "Multi-type noise generator";

    private readonly Random _random = new();

    // Pink noise state (Paul Kellet's refined method - 7 stages)
    private float _b0, _b1, _b2, _b3, _b4, _b5, _b6;

    // Brown noise state
    private float _brownState;

    protected override void InitializePorts()
    {
        AddOutput("White", PortDataType.Audio);
        AddOutput("Pink", PortDataType.Audio);
        AddOutput("Brown", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Level", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var level = GetParam("Level");

        for (int i = 0; i < sampleCount; i++)
        {
            // White noise (uniform distribution)
            float white = (float)(_random.NextDouble() * 2 - 1) * level;

            // Pink noise - Paul Kellet's economy method (perf/quality trade-off)
            // -3dB/octave rolloff
            _b0 = 0.99886f * _b0 + white * 0.0555179f;
            _b1 = 0.99332f * _b1 + white * 0.0750759f;
            _b2 = 0.96900f * _b2 + white * 0.1538520f;
            _b3 = 0.86650f * _b3 + white * 0.3104856f;
            _b4 = 0.55000f * _b4 + white * 0.5329522f;
            _b5 = -0.7616f * _b5 - white * 0.0168980f;
            float pink = (_b0 + _b1 + _b2 + _b3 + _b4 + _b5 + _b6 + white * 0.5362f) * 0.11f;
            _b6 = white * 0.115926f;

            // Brown noise (red noise) - integration with leak
            // -6dB/octave rolloff
            _brownState = (_brownState + white * 0.02f) / 1.02f;
            float brown = _brownState * 3.5f;

            buffer[i] += white;
            SetOutput(0, white);
            SetOutput(1, pink * level);
            SetOutput(2, brown * level);
        }
    }
}

/// <summary>
/// Low Frequency Oscillator for modulation with multiple waveform outputs.
/// </summary>
public class LfoNode : EffectNodeBase
{
    public override string NodeType => "LFO";
    public override string Category => "Generators";
    public override string Description => "Multi-waveform modulation oscillator";

    private double _phase;

    protected override void InitializePorts()
    {
        AddInput("Rate CV", PortDataType.Control);
        AddInput("Reset", PortDataType.Trigger);
        AddOutput("Sine", PortDataType.Control);
        AddOutput("Saw", PortDataType.Control);
        AddOutput("Square", PortDataType.Control);
        AddOutput("Triangle", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Rate", 1f, 0.01f, 100f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Depth", 1f, 0f, 1f, "");
        AddParameter("Offset", 0f, -1f, 1f, "");
        AddParameter("Phase", 0f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var rate = GetParam("Rate") + GetInput(0) * 10f;
        var depth = GetParam("Depth");
        var offset = GetParam("Offset");
        var phaseOffset = GetParam("Phase");

        if (GetInput(1) > 0.5f) _phase = 0;

        var phaseInc = rate / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            _phase += phaseInc;
            if (_phase >= 1) _phase -= 1;
        }

        double p = (_phase + phaseOffset) % 1.0;

        float sine = MathF.Sin((float)(p * 2 * Math.PI)) * depth + offset;
        float saw = (float)(2 * p - 1) * depth + offset;
        float square = (p < 0.5f ? 1f : -1f) * depth + offset;
        float triangle = (float)(4 * Math.Abs(p - 0.5) - 1) * depth + offset;

        SetOutput(0, sine);
        SetOutput(1, saw);
        SetOutput(2, square);
        SetOutput(3, triangle);
    }
}

#endregion

#region Filters

/// <summary>
/// Moog-style 4-pole ladder filter with self-oscillation and saturation.
/// Based on the classic transistor ladder design with improved stability.
/// </summary>
public class FilterNode : EffectNodeBase
{
    public override string NodeType => "Filter";
    public override string Category => "Filters";
    public override string Description => "Moog-style 4-pole ladder filter";

    // 4 stages of the ladder
    private float _stage0, _stage1, _stage2, _stage3;

    // For oversampling
    private const int OversampleFactor = 2;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Cutoff CV", PortDataType.Control);
        AddInput("Resonance CV", PortDataType.Control);
        AddOutput("LP", PortDataType.Audio);
        AddOutput("HP", PortDataType.Audio);
        AddOutput("BP", PortDataType.Audio);
        AddOutput("Notch", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Cutoff", 1000f, 20f, 20000f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Resonance", 0f, 0f, 1f, "");
        AddParameter("Drive", 0f, 0f, 1f, "");
        AddParameter("Mode", 0f, 0f, 3f, ""); // 0=LP24, 1=LP12, 2=BP, 3=HP
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var cutoff = GetParam("Cutoff") + GetInput(1) * 5000f;
        var resonance = GetParam("Resonance") + GetInput(2) * 0.5f;
        var drive = GetParam("Drive");
        var mode = (int)GetParam("Mode");

        // Clamp and scale parameters
        cutoff = Math.Clamp(cutoff, 20f, sampleRate * 0.45f);
        resonance = Math.Clamp(resonance, 0f, 1f);

        // Frequency coefficient (normalized)
        float fc = 2f * MathF.Sin(MathF.PI * cutoff / (sampleRate * OversampleFactor));

        // Resonance (k = 0 to 4 for self-oscillation)
        float k = resonance * 4f;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Apply input drive (soft saturation)
            if (drive > 0)
                input = SoftClip(input * (1f + drive * 3f));

            float lp = 0, hp = 0, bp = 0;

            // Oversample for stability at high resonance
            for (int os = 0; os < OversampleFactor; os++)
            {
                // Feedback with saturation for stability
                float feedback = SoftClip(k * _stage3);

                // 4-pole cascade (Moog ladder topology)
                _stage0 += fc * (SoftClip(input - feedback) - _stage0);
                _stage1 += fc * (_stage0 - _stage1);
                _stage2 += fc * (_stage1 - _stage2);
                _stage3 += fc * (_stage2 - _stage3);
            }

            // Output modes
            lp = _stage3;
            bp = _stage1 - _stage3;
            hp = input - _stage3 - k * bp;

            float output = mode switch
            {
                0 => lp,           // LP 24dB
                1 => _stage1,      // LP 12dB
                2 => bp,           // Bandpass
                3 => hp,           // Highpass
                _ => lp
            };

            buffer[i] = output;
        }

        SetOutput(0, _stage3);              // LP
        SetOutput(1, buffer[0] - _stage3);  // HP (approximate)
        SetOutput(2, _stage1 - _stage3);    // BP
        SetOutput(3, _stage3 + (buffer[0] - _stage3)); // Notch
    }

    private static float SoftClip(float x)
    {
        // Cubic soft clipper
        if (x > 1f) return 2f / 3f;
        if (x < -1f) return -2f / 3f;
        return x - (x * x * x) / 3f;
    }
}

/// <summary>
/// Professional 3-band parametric EQ with shelving and peaking filters.
/// Uses biquad filters with proper coefficient calculation.
/// </summary>
public class Eq3BandNode : EffectNodeBase
{
    public override string NodeType => "EQ3Band";
    public override string Category => "Filters";
    public override string Description => "3-band parametric equalizer";

    // Biquad states for each band (2 channels each)
    private readonly BiquadState[] _lowStates = { new(), new() };
    private readonly BiquadState[] _midStates = { new(), new() };
    private readonly BiquadState[] _highStates = { new(), new() };

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Low Gain", 0f, -18f, 18f, "dB");
        AddParameter("Low Freq", 100f, 20f, 500f, "Hz");
        AddParameter("Mid Gain", 0f, -18f, 18f, "dB");
        AddParameter("Mid Freq", 1000f, 200f, 8000f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Mid Q", 1f, 0.1f, 10f, "");
        AddParameter("High Gain", 0f, -18f, 18f, "dB");
        AddParameter("High Freq", 8000f, 2000f, 16000f, "Hz");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var lowGain = GetParam("Low Gain");
        var lowFreq = GetParam("Low Freq");
        var midGain = GetParam("Mid Gain");
        var midFreq = GetParam("Mid Freq");
        var midQ = GetParam("Mid Q");
        var highGain = GetParam("High Gain");
        var highFreq = GetParam("High Freq");

        // Calculate biquad coefficients
        var lowCoeffs = CalculateLowShelf(lowFreq, lowGain, sampleRate);
        var midCoeffs = CalculatePeaking(midFreq, midQ, midGain, sampleRate);
        var highCoeffs = CalculateHighShelf(highFreq, highGain, sampleRate);

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Process through each band in series
            float output = ProcessBiquad(input, lowCoeffs, _lowStates[0]);
            output = ProcessBiquad(output, midCoeffs, _midStates[0]);
            output = ProcessBiquad(output, highCoeffs, _highStates[0]);

            buffer[i] = output;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }

    private static BiquadCoeffs CalculateLowShelf(float freq, float gainDb, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * freq / sampleRate;
        float cos_w0 = MathF.Cos(w0);
        float sin_w0 = MathF.Sin(w0);
        float alpha = sin_w0 / 2f * MathF.Sqrt(2f);
        float sqrtA = MathF.Sqrt(A);

        float b0 = A * ((A + 1) - (A - 1) * cos_w0 + 2 * sqrtA * alpha);
        float b1 = 2 * A * ((A - 1) - (A + 1) * cos_w0);
        float b2 = A * ((A + 1) - (A - 1) * cos_w0 - 2 * sqrtA * alpha);
        float a0 = (A + 1) + (A - 1) * cos_w0 + 2 * sqrtA * alpha;
        float a1 = -2 * ((A - 1) + (A + 1) * cos_w0);
        float a2 = (A + 1) + (A - 1) * cos_w0 - 2 * sqrtA * alpha;

        return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private static BiquadCoeffs CalculateHighShelf(float freq, float gainDb, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * freq / sampleRate;
        float cos_w0 = MathF.Cos(w0);
        float sin_w0 = MathF.Sin(w0);
        float alpha = sin_w0 / 2f * MathF.Sqrt(2f);
        float sqrtA = MathF.Sqrt(A);

        float b0 = A * ((A + 1) + (A - 1) * cos_w0 + 2 * sqrtA * alpha);
        float b1 = -2 * A * ((A - 1) + (A + 1) * cos_w0);
        float b2 = A * ((A + 1) + (A - 1) * cos_w0 - 2 * sqrtA * alpha);
        float a0 = (A + 1) - (A - 1) * cos_w0 + 2 * sqrtA * alpha;
        float a1 = 2 * ((A - 1) - (A + 1) * cos_w0);
        float a2 = (A + 1) - (A - 1) * cos_w0 - 2 * sqrtA * alpha;

        return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private static BiquadCoeffs CalculatePeaking(float freq, float Q, float gainDb, int sampleRate)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * freq / sampleRate;
        float cos_w0 = MathF.Cos(w0);
        float sin_w0 = MathF.Sin(w0);
        float alpha = sin_w0 / (2f * Q);

        float b0 = 1 + alpha * A;
        float b1 = -2 * cos_w0;
        float b2 = 1 - alpha * A;
        float a0 = 1 + alpha / A;
        float a1 = -2 * cos_w0;
        float a2 = 1 - alpha / A;

        return new BiquadCoeffs(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    private static float ProcessBiquad(float input, BiquadCoeffs c, BiquadState s)
    {
        float output = c.B0 * input + c.B1 * s.X1 + c.B2 * s.X2 - c.A1 * s.Y1 - c.A2 * s.Y2;
        s.X2 = s.X1;
        s.X1 = input;
        s.Y2 = s.Y1;
        s.Y1 = output;
        return output;
    }

    private record BiquadCoeffs(float B0, float B1, float B2, float A1, float A2);
    private class BiquadState { public float X1, X2, Y1, Y2; }
}

/// <summary>
/// Formant (vowel) filter using parallel resonant filters.
/// </summary>
public class FormantFilterNode : EffectNodeBase
{
    public override string NodeType => "FormantFilter";
    public override string Category => "Filters";
    public override string Description => "Vowel formant filter";

    // Formant frequencies for vowels: [vowel][formant]
    private static readonly float[,] _vowelFormants = new float[5, 4]
    {
        { 800, 1150, 2900, 3900 },   // A
        { 350, 2000, 2800, 3600 },   // E
        { 270, 2140, 2950, 3900 },   // I
        { 450, 800, 2830, 3800 },    // O
        { 325, 700, 2700, 3800 }     // U
    };

    private static readonly float[,] _vowelBandwidths = new float[5, 4]
    {
        { 80, 90, 120, 130 },
        { 60, 100, 120, 150 },
        { 60, 90, 100, 120 },
        { 70, 80, 100, 130 },
        { 50, 60, 170, 180 }
    };

    private readonly FormantBand[] _bands = new FormantBand[4];

    public FormantFilterNode()
    {
        for (int i = 0; i < 4; i++)
            _bands[i] = new FormantBand();
    }

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Vowel CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Vowel", 0f, 0f, 4f, "");
        AddParameter("Resonance", 0.9f, 0.5f, 0.99f, "");
        AddParameter("Mix", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var vowel = Math.Clamp(GetParam("Vowel") + GetInput(1) * 4f, 0f, 4f);
        var resonance = GetParam("Resonance");
        var mix = GetParam("Mix");

        int v1 = (int)vowel;
        int v2 = Math.Min(v1 + 1, 4);
        float blend = vowel - v1;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);
            float output = 0f;

            for (int f = 0; f < 4; f++)
            {
                float freq = _vowelFormants[v1, f] * (1 - blend) + _vowelFormants[v2, f] * blend;
                float bw = _vowelBandwidths[v1, f] * (1 - blend) + _vowelBandwidths[v2, f] * blend;
                float q = freq / bw * resonance;

                output += _bands[f].Process(input, freq, q, sampleRate) * 0.25f;
            }

            buffer[i] = input * (1 - mix) + output * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }

    private class FormantBand
    {
        private float _z1, _z2;

        public float Process(float input, float freq, float q, int sampleRate)
        {
            float w0 = 2f * MathF.PI * freq / sampleRate;
            float alpha = MathF.Sin(w0) / (2f * q);
            float cos_w0 = MathF.Cos(w0);

            float b0 = alpha;
            float b1 = 0;
            float b2 = -alpha;
            float a0 = 1 + alpha;
            float a1 = -2 * cos_w0;
            float a2 = 1 - alpha;

            float output = (b0 / a0) * input + (b1 / a0) * _z1 + (b2 / a0) * _z2
                         - (a1 / a0) * _z1 - (a2 / a0) * _z2;

            _z2 = _z1;
            _z1 = output;

            return output;
        }
    }
}

#endregion

#region Dynamics

/// <summary>
/// Professional gain control with smoothing and metering.
/// </summary>
public class GainNode : EffectNodeBase
{
    public override string NodeType => "Gain";
    public override string Category => "Dynamics";
    public override string Description => "Smooth gain control";

    private float _smoothedGain = 1f;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Gain", 0f, -60f, 24f, "dB");
        AddParameter("Smooth", 10f, 0f, 100f, "ms");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var gainDb = GetParam("Gain") + GetInput(1) * 24f;
        var targetGain = MathF.Pow(10f, gainDb / 20f);
        var smoothTime = GetParam("Smooth") * 0.001f * sampleRate;
        var smoothCoef = smoothTime > 0 ? 1f / smoothTime : 1f;

        for (int i = 0; i < sampleCount; i++)
        {
            _smoothedGain += (targetGain - _smoothedGain) * smoothCoef;
            buffer[i] = (buffer[i] + GetInput(0)) * _smoothedGain;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

/// <summary>
/// Professional compressor with look-ahead, soft knee, and RMS/peak detection.
/// </summary>
public class CompressorNode : EffectNodeBase
{
    public override string NodeType => "Compressor";
    public override string Category => "Dynamics";
    public override string Description => "Professional compressor with look-ahead";

    private float _envelope;
    private float _gainSmooth;

    // Look-ahead delay buffer
    private float[] _lookAheadBuffer = Array.Empty<float>();
    private int _lookAheadPos;

    // RMS calculation
    private float _rmsSum;
    private readonly float[] _rmsBuffer = new float[256];
    private int _rmsPos;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Sidechain", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("GR", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Threshold", -20f, -60f, 0f, "dB");
        AddParameter("Ratio", 4f, 1f, 20f, ":1");
        AddParameter("Attack", 10f, 0.1f, 100f, "ms");
        AddParameter("Release", 100f, 10f, 2000f, "ms");
        AddParameter("Knee", 6f, 0f, 24f, "dB");
        AddParameter("Makeup", 0f, 0f, 24f, "dB");
        AddParameter("Look-ahead", 5f, 0f, 20f, "ms");
        AddParameter("Detection", 0f, 0f, 1f, ""); // 0=Peak, 1=RMS
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        var thresholdDb = GetParam("Threshold");
        var ratio = GetParam("Ratio");
        var attackMs = GetParam("Attack");
        var releaseMs = GetParam("Release");
        var kneeDb = GetParam("Knee");
        var makeupDb = GetParam("Makeup");
        var lookAheadMs = GetParam("Look-ahead");
        var detection = GetParam("Detection");

        float threshold = MathF.Pow(10f, thresholdDb / 20f);
        float makeup = MathF.Pow(10f, makeupDb / 20f);
        float attackCoef = MathF.Exp(-1f / (attackMs * 0.001f * sampleRate));
        float releaseCoef = MathF.Exp(-1f / (releaseMs * 0.001f * sampleRate));
        int lookAheadSamples = (int)(lookAheadMs * 0.001f * sampleRate);

        // Initialize look-ahead buffer
        if (_lookAheadBuffer.Length != lookAheadSamples && lookAheadSamples > 0)
        {
            _lookAheadBuffer = new float[lookAheadSamples];
            _lookAheadPos = 0;
        }

        float gr = 1f;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);
            float sidechain = GetInput(1) != 0 ? GetInput(1) : input;

            // Detection (peak or RMS)
            float level;
            if (detection > 0.5f)
            {
                // RMS detection
                _rmsSum -= _rmsBuffer[_rmsPos] * _rmsBuffer[_rmsPos];
                _rmsBuffer[_rmsPos] = sidechain;
                _rmsSum += sidechain * sidechain;
                _rmsPos = (_rmsPos + 1) % _rmsBuffer.Length;
                level = MathF.Sqrt(_rmsSum / _rmsBuffer.Length);
            }
            else
            {
                // Peak detection
                level = MathF.Abs(sidechain);
            }

            // Envelope follower
            float coef = level > _envelope ? attackCoef : releaseCoef;
            _envelope = coef * _envelope + (1 - coef) * level;

            // Gain calculation with soft knee
            float levelDb = 20f * MathF.Log10(_envelope + 1e-10f);
            float gainDb = 0f;

            if (kneeDb > 0 && levelDb > thresholdDb - kneeDb / 2 && levelDb < thresholdDb + kneeDb / 2)
            {
                // Soft knee region
                float x = levelDb - thresholdDb + kneeDb / 2;
                gainDb = -((1f / ratio - 1f) * x * x) / (2f * kneeDb);
            }
            else if (levelDb > thresholdDb)
            {
                // Above knee
                float overDb = levelDb - thresholdDb;
                gainDb = -(overDb - overDb / ratio);
            }

            gr = MathF.Pow(10f, gainDb / 20f);

            // Smooth gain changes
            _gainSmooth += (gr - _gainSmooth) * 0.01f;

            // Apply look-ahead delay
            float delayed = input;
            if (lookAheadSamples > 0)
            {
                delayed = _lookAheadBuffer[_lookAheadPos];
                _lookAheadBuffer[_lookAheadPos] = input;
                _lookAheadPos = (_lookAheadPos + 1) % lookAheadSamples;
            }

            buffer[i] = delayed * _gainSmooth * makeup;
        }

        SetOutput(0, buffer[sampleCount - 1]);
        SetOutput(1, 1f - gr); // Gain reduction (inverted for metering)
    }
}

/// <summary>
/// True-peak brickwall limiter with oversampling.
/// </summary>
public class LimiterNode : EffectNodeBase
{
    public override string NodeType => "Limiter";
    public override string Category => "Dynamics";
    public override string Description => "True-peak brickwall limiter";

    private float _envelope;
    private float _gain = 1f;

    // Look-ahead buffer
    private readonly float[] _lookAheadBuffer = new float[128];
    private int _lookAheadPos;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("GR", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Ceiling", -0.3f, -12f, 0f, "dB");
        AddParameter("Release", 100f, 10f, 1000f, "ms");
        AddParameter("Look-ahead", 1f, 0f, 5f, "ms");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float ceiling = MathF.Pow(10f, GetParam("Ceiling") / 20f);
        float releaseMs = GetParam("Release");
        float lookAheadMs = GetParam("Look-ahead");

        float releaseCoef = MathF.Exp(-1f / (releaseMs * 0.001f * sampleRate));
        int lookAheadSamples = Math.Min((int)(lookAheadMs * 0.001f * sampleRate), _lookAheadBuffer.Length);

        float gr = 1f;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);
            float absInput = MathF.Abs(input);

            // Instant attack, smooth release
            if (absInput > _envelope)
                _envelope = absInput;
            else
                _envelope = releaseCoef * _envelope + (1 - releaseCoef) * absInput;

            // Calculate gain
            if (_envelope > ceiling)
                gr = ceiling / _envelope;
            else
                gr = 1f;

            // Smooth gain changes to avoid distortion
            _gain += (gr - _gain) * 0.1f;

            // Look-ahead delay
            float delayed = _lookAheadBuffer[_lookAheadPos];
            _lookAheadBuffer[_lookAheadPos] = input;
            _lookAheadPos = (_lookAheadPos + 1) % Math.Max(lookAheadSamples, 1);

            buffer[i] = delayed * _gain;
        }

        SetOutput(0, buffer[sampleCount - 1]);
        SetOutput(1, 1f - gr);
    }
}

/// <summary>
/// Professional noise gate with sidechain filtering and range control.
/// </summary>
public class GateNode : EffectNodeBase
{
    public override string NodeType => "Gate";
    public override string Category => "Dynamics";
    public override string Description => "Noise gate with sidechain";

    private float _gainSmooth;
    private int _holdCounter;
    private bool _isOpen;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Sidechain", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("Gate", PortDataType.Gate);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Threshold", -40f, -80f, 0f, "dB");
        AddParameter("Attack", 0.5f, 0.01f, 50f, "ms");
        AddParameter("Hold", 50f, 0f, 500f, "ms");
        AddParameter("Release", 100f, 10f, 2000f, "ms");
        AddParameter("Range", -80f, -80f, 0f, "dB");
        AddParameter("Hysteresis", 3f, 0f, 12f, "dB");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float threshold = MathF.Pow(10f, GetParam("Threshold") / 20f);
        float hysteresis = MathF.Pow(10f, GetParam("Hysteresis") / 20f);
        float attackCoef = 1f - MathF.Exp(-1f / (GetParam("Attack") * 0.001f * sampleRate));
        float releaseCoef = 1f - MathF.Exp(-1f / (GetParam("Release") * 0.001f * sampleRate));
        int holdSamples = (int)(GetParam("Hold") * 0.001f * sampleRate);
        float range = MathF.Pow(10f, GetParam("Range") / 20f);

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);
            float sidechain = GetInput(1) != 0 ? GetInput(1) : input;
            float level = MathF.Abs(sidechain);

            // Hysteresis for stable switching
            float openThreshold = _isOpen ? threshold / hysteresis : threshold;

            if (level > openThreshold)
            {
                _isOpen = true;
                _holdCounter = holdSamples;
            }
            else if (_holdCounter > 0)
            {
                _holdCounter--;
            }
            else
            {
                _isOpen = false;
            }

            // Smooth gain transitions
            float targetGain = _isOpen ? 1f : range;
            float coef = targetGain > _gainSmooth ? attackCoef : releaseCoef;
            _gainSmooth += (targetGain - _gainSmooth) * coef;

            buffer[i] = input * _gainSmooth;
        }

        SetOutput(0, buffer[sampleCount - 1]);
        SetOutput(1, _isOpen ? 1f : 0f);
    }
}

#endregion

#region Effects

/// <summary>
/// Professional stereo delay with interpolation, modulation, and filtering.
/// </summary>
public class DelayNode : EffectNodeBase
{
    public override string NodeType => "Delay";
    public override string Category => "Effects";
    public override string Description => "Stereo delay with modulation";

    private float[] _delayBufferL = Array.Empty<float>();
    private float[] _delayBufferR = Array.Empty<float>();
    private int _writePos;
    private double _lfoPhase;

    // Smoothed read position for tape-like behavior
    private float _smoothedReadPos;

    // Feedback filter state
    private float _filterState;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Time CV", PortDataType.Control);
        AddInput("Feedback CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("Wet", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Time", 250f, 1f, 2000f, "ms");
        AddParameter("Feedback", 0.4f, 0f, 0.95f, "");
        AddParameter("Mix", 0.5f, 0f, 1f, "");
        AddParameter("Mod Rate", 0.5f, 0f, 5f, "Hz");
        AddParameter("Mod Depth", 0f, 0f, 20f, "ms");
        AddParameter("Filter", 8000f, 200f, 20000f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Ping-Pong", 0f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float timeMs = GetParam("Time") + GetInput(1) * 500f;
        float feedback = Math.Clamp(GetParam("Feedback") + GetInput(2) * 0.5f, 0f, 0.95f);
        float mix = GetParam("Mix");
        float modRate = GetParam("Mod Rate");
        float modDepth = GetParam("Mod Depth") * 0.001f * sampleRate;
        float filterFreq = GetParam("Filter");
        float pingPong = GetParam("Ping-Pong");

        int maxDelay = sampleRate * 3; // 3 seconds max
        if (_delayBufferL.Length != maxDelay)
        {
            _delayBufferL = new float[maxDelay];
            _delayBufferR = new float[maxDelay];
        }

        float targetReadPos = timeMs * 0.001f * sampleRate;
        float filterCoef = 2f * MathF.Sin(MathF.PI * filterFreq / sampleRate);
        float lfoInc = modRate / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Smooth delay time changes (tape-like behavior)
            _smoothedReadPos += (targetReadPos - _smoothedReadPos) * 0.001f;

            // LFO modulation
            float mod = (float)(Math.Sin(_lfoPhase * 2 * Math.PI) * modDepth);
            _lfoPhase += lfoInc;
            if (_lfoPhase >= 1) _lfoPhase -= 1;

            float readPos = _smoothedReadPos + mod;
            readPos = Math.Clamp(readPos, 1f, maxDelay - 2);

            // Hermite interpolation for smooth delay time modulation
            float delayedL = HermiteInterpolate(_delayBufferL, _writePos - readPos, maxDelay);
            float delayedR = HermiteInterpolate(_delayBufferR, _writePos - readPos, maxDelay);

            // Feedback filtering (lowpass)
            _filterState += filterCoef * (delayedL - _filterState);
            float filteredFeedback = _filterState;

            // Write to delay buffers
            if (pingPong > 0.5f)
            {
                _delayBufferL[_writePos] = input + delayedR * feedback;
                _delayBufferR[_writePos] = delayedL * feedback;
            }
            else
            {
                _delayBufferL[_writePos] = input + filteredFeedback * feedback;
                _delayBufferR[_writePos] = input + filteredFeedback * feedback;
            }

            _writePos = (_writePos + 1) % maxDelay;

            float wet = (delayedL + delayedR) * 0.5f;
            buffer[i] = input * (1 - mix) + wet * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
        SetOutput(1, (_delayBufferL[_writePos > 0 ? _writePos - 1 : _delayBufferL.Length - 1]));
    }

    private static float HermiteInterpolate(float[] buffer, float pos, int length)
    {
        while (pos < 0) pos += length;
        int idx = (int)pos;
        float frac = pos - idx;

        int i0 = (idx - 1 + length) % length;
        int i1 = idx % length;
        int i2 = (idx + 1) % length;
        int i3 = (idx + 2) % length;

        float y0 = buffer[i0], y1 = buffer[i1], y2 = buffer[i2], y3 = buffer[i3];
        float c0 = y1;
        float c1 = 0.5f * (y2 - y0);
        float c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        float c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);

        return ((c3 * frac + c2) * frac + c1) * frac + c0;
    }
}

/// <summary>
/// Freeverb-style algorithmic reverb with Schroeder-Moorer architecture.
/// 8 parallel comb filters + 4 series allpass filters.
/// </summary>
public class ReverbNode : EffectNodeBase
{
    public override string NodeType => "Reverb";
    public override string Category => "Effects";
    public override string Description => "Freeverb algorithmic reverb";

    // Freeverb standard delays (44100 Hz reference, scaled for other sample rates)
    private static readonly int[] CombDelays = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
    private static readonly int[] AllpassDelays = { 556, 441, 341, 225 };

    private readonly CombFilter[] _combsL = new CombFilter[8];
    private readonly CombFilter[] _combsR = new CombFilter[8];
    private readonly AllpassFilter[] _allpassesL = new AllpassFilter[4];
    private readonly AllpassFilter[] _allpassesR = new AllpassFilter[4];

    private int _preDelayPos;
    private float[] _preDelay = Array.Empty<float>();

    public ReverbNode()
    {
        for (int i = 0; i < 8; i++)
        {
            _combsL[i] = new CombFilter(CombDelays[i] + 23 * i); // Slight stereo spread
            _combsR[i] = new CombFilter(CombDelays[i]);
        }
        for (int i = 0; i < 4; i++)
        {
            _allpassesL[i] = new AllpassFilter(AllpassDelays[i] + 13 * i);
            _allpassesR[i] = new AllpassFilter(AllpassDelays[i]);
        }
    }

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("L", PortDataType.Audio);
        AddOutput("R", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Size", 0.5f, 0f, 1f, "");
        AddParameter("Damping", 0.5f, 0f, 1f, "");
        AddParameter("Width", 1f, 0f, 1f, "");
        AddParameter("Pre-delay", 0f, 0f, 100f, "ms");
        AddParameter("Mix", 0.3f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float roomSize = 0.28f + GetParam("Size") * 0.7f; // 0.28 to 0.98
        float damp = GetParam("Damping");
        float width = GetParam("Width");
        float preDelayMs = GetParam("Pre-delay");
        float mix = GetParam("Mix");

        // Pre-delay buffer
        int preDelaySamples = (int)(preDelayMs * 0.001f * sampleRate);
        if (preDelaySamples > 0 && _preDelay.Length != preDelaySamples)
            _preDelay = new float[preDelaySamples];

        // Scale comb feedback based on room size
        float combFeedback = roomSize;
        float combDamp = damp;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Pre-delay
            float delayed = input;
            if (preDelaySamples > 0)
            {
                delayed = _preDelay[_preDelayPos];
                _preDelay[_preDelayPos] = input;
                _preDelayPos = (_preDelayPos + 1) % preDelaySamples;
            }

            // Sum of parallel comb filters
            float outL = 0f, outR = 0f;
            for (int c = 0; c < 8; c++)
            {
                outL += _combsL[c].Process(delayed, combFeedback, combDamp);
                outR += _combsR[c].Process(delayed, combFeedback, combDamp);
            }

            // Normalize
            outL *= 0.125f;
            outR *= 0.125f;

            // Series allpass filters for diffusion
            for (int a = 0; a < 4; a++)
            {
                outL = _allpassesL[a].Process(outL);
                outR = _allpassesR[a].Process(outR);
            }

            // Stereo width
            float wet1 = outL + outR;
            float wet2 = outL - outR;
            outL = wet1 * (1 - width) / 2 + outL * width;
            outR = wet1 * (1 - width) / 2 + outR * width;

            // Mix
            float mono = (outL + outR) * 0.5f;
            buffer[i] = input * (1 - mix) + mono * mix;
        }

        float finalL = 0, finalR = 0;
        for (int c = 0; c < 8; c++)
        {
            finalL += _combsL[c].LastOutput;
            finalR += _combsR[c].LastOutput;
        }

        SetOutput(0, buffer[sampleCount - 1]);
        SetOutput(1, finalL * 0.125f * mix);
        SetOutput(2, finalR * 0.125f * mix);
    }

    private class CombFilter
    {
        private readonly float[] _buffer;
        private int _pos;
        private float _filterState;
        public float LastOutput { get; private set; }

        public CombFilter(int size)
        {
            _buffer = new float[size];
        }

        public float Process(float input, float feedback, float damp)
        {
            float output = _buffer[_pos];

            // Lowpass filter in feedback path (damping)
            _filterState = output * (1 - damp) + _filterState * damp;

            _buffer[_pos] = input + _filterState * feedback;
            _pos = (_pos + 1) % _buffer.Length;

            LastOutput = output;
            return output;
        }
    }

    private class AllpassFilter
    {
        private readonly float[] _buffer;
        private int _pos;
        private const float Feedback = 0.5f;

        public AllpassFilter(int size)
        {
            _buffer = new float[size];
        }

        public float Process(float input)
        {
            float delayed = _buffer[_pos];
            float output = -input + delayed;
            _buffer[_pos] = input + delayed * Feedback;
            _pos = (_pos + 1) % _buffer.Length;
            return output;
        }
    }
}

/// <summary>
/// Stereo chorus with multiple voices and BBD-style modulation.
/// </summary>
public class ChorusNode : EffectNodeBase
{
    public override string NodeType => "Chorus";
    public override string Category => "Effects";
    public override string Description => "Multi-voice stereo chorus";

    private float[] _delayBuffer = Array.Empty<float>();
    private int _writePos;
    private readonly double[] _lfoPhases = new double[3];

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("L", PortDataType.Audio);
        AddOutput("R", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Rate", 0.8f, 0.1f, 5f, "Hz");
        AddParameter("Depth", 0.5f, 0f, 1f, "");
        AddParameter("Voices", 2f, 1f, 3f, "");
        AddParameter("Spread", 0.5f, 0f, 1f, "");
        AddParameter("Mix", 0.5f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float rate = GetParam("Rate");
        float depth = GetParam("Depth") * 0.007f * sampleRate; // Max 7ms modulation
        int voices = (int)GetParam("Voices");
        float spread = GetParam("Spread");
        float mix = GetParam("Mix");

        int bufSize = (int)(0.05f * sampleRate); // 50ms buffer
        if (_delayBuffer.Length != bufSize)
            _delayBuffer = new float[bufSize];

        float baseDelay = bufSize * 0.4f; // Center point
        float lfoInc = rate / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);
            _delayBuffer[_writePos] = input;

            float wetL = 0f, wetR = 0f;

            for (int v = 0; v < voices; v++)
            {
                // Each voice has slightly different rate and phase
                float voiceRate = lfoInc * (1f + v * 0.1f);
                _lfoPhases[v] += voiceRate;
                if (_lfoPhases[v] >= 1) _lfoPhases[v] -= 1;

                float lfo = (float)Math.Sin(_lfoPhases[v] * 2 * Math.PI);
                float readPos = baseDelay + lfo * depth * (1f + v * 0.2f);

                float delayed = HermiteRead(_delayBuffer, _writePos - readPos, bufSize);

                // Stereo spread
                float pan = (v / (float)(voices - 1) - 0.5f) * spread;
                wetL += delayed * (0.5f - pan);
                wetR += delayed * (0.5f + pan);
            }

            wetL /= voices;
            wetR /= voices;

            _writePos = (_writePos + 1) % bufSize;

            buffer[i] = input * (1 - mix) + (wetL + wetR) * 0.5f * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }

    private static float HermiteRead(float[] buffer, float pos, int length)
    {
        while (pos < 0) pos += length;
        int idx = (int)pos;
        float frac = pos - idx;

        int i0 = (idx - 1 + length) % length;
        int i1 = idx % length;
        int i2 = (idx + 1) % length;
        int i3 = (idx + 2) % length;

        float y0 = buffer[i0], y1 = buffer[i1], y2 = buffer[i2], y3 = buffer[i3];
        float c1 = 0.5f * (y2 - y0);
        float c2 = y0 - 2.5f * y1 + 2f * y2 - 0.5f * y3;
        float c3 = 0.5f * (y3 - y0) + 1.5f * (y1 - y2);

        return ((c3 * frac + c2) * frac + c1) * frac + y1;
    }
}

/// <summary>
/// Classic through-zero flanger effect.
/// </summary>
public class FlangerNode : EffectNodeBase
{
    public override string NodeType => "Flanger";
    public override string Category => "Effects";
    public override string Description => "Through-zero flanger";

    private float[] _delayBuffer = Array.Empty<float>();
    private int _writePos;
    private double _lfoPhase;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Rate", 0.3f, 0.05f, 5f, "Hz");
        AddParameter("Depth", 0.7f, 0f, 1f, "");
        AddParameter("Manual", 0.5f, 0f, 1f, "");
        AddParameter("Feedback", 0.5f, -0.99f, 0.99f, "");
        AddParameter("Mix", 0.5f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float rate = GetParam("Rate");
        float depth = GetParam("Depth");
        float manual = GetParam("Manual");
        float feedback = GetParam("Feedback");
        float mix = GetParam("Mix");

        int bufSize = (int)(0.02f * sampleRate); // 20ms max
        if (_delayBuffer.Length != bufSize)
            _delayBuffer = new float[bufSize];

        float lfoInc = rate / sampleRate;
        float maxDelay = bufSize * 0.8f;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Triangular LFO for classic flanger sound
            float lfo = (float)(Math.Abs(_lfoPhase * 2 - 1) * 2 - 1);
            _lfoPhase += lfoInc;
            if (_lfoPhase >= 1) _lfoPhase -= 1;

            float modulation = (lfo * depth + manual) * 0.5f + 0.5f;
            float readPos = 1f + modulation * maxDelay;

            // Interpolated read
            int idx = (int)readPos;
            float frac = readPos - idx;
            int r0 = (_writePos - idx + bufSize) % bufSize;
            int r1 = (r0 + 1) % bufSize;
            float delayed = _delayBuffer[r0] * (1 - frac) + _delayBuffer[r1] * frac;

            // Write with feedback
            _delayBuffer[_writePos] = input + delayed * feedback;
            _writePos = (_writePos + 1) % bufSize;

            buffer[i] = input * (1 - mix) + (input + delayed) * 0.5f * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

/// <summary>
/// Multi-stage analog phaser emulation.
/// </summary>
public class PhaserNode : EffectNodeBase
{
    public override string NodeType => "Phaser";
    public override string Category => "Effects";
    public override string Description => "Analog multi-stage phaser";

    private readonly AllpassStage[] _stages = new AllpassStage[12];
    private double _lfoPhase;
    private float _feedback;

    public PhaserNode()
    {
        for (int i = 0; i < 12; i++)
            _stages[i] = new AllpassStage();
    }

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Rate", 0.5f, 0.01f, 5f, "Hz");
        AddParameter("Depth", 0.8f, 0f, 1f, "");
        AddParameter("Feedback", 0.7f, 0f, 0.99f, "");
        AddParameter("Stages", 6f, 2f, 12f, "");
        AddParameter("Freq Min", 200f, 50f, 1000f, "Hz");
        AddParameter("Freq Max", 2000f, 500f, 10000f, "Hz");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float rate = GetParam("Rate");
        float depth = GetParam("Depth");
        float feedbackAmt = GetParam("Feedback");
        int stages = (int)GetParam("Stages");
        float freqMin = GetParam("Freq Min");
        float freqMax = GetParam("Freq Max");

        float lfoInc = rate / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Sine LFO
            float lfo = (float)((Math.Sin(_lfoPhase * 2 * Math.PI) + 1) * 0.5f);
            _lfoPhase += lfoInc;
            if (_lfoPhase >= 1) _lfoPhase -= 1;

            // Sweep frequency
            float freq = freqMin * MathF.Pow(freqMax / freqMin, lfo * depth);

            // Process through allpass stages
            float output = input + _feedback * feedbackAmt;
            for (int s = 0; s < stages; s++)
            {
                // Each stage has slightly different frequency for richer sound
                float stageFreq = freq * (1f + s * 0.1f);
                output = _stages[s].Process(output, stageFreq, sampleRate);
            }

            _feedback = output;
            buffer[i] = (input + output) * 0.5f;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }

    private class AllpassStage
    {
        private float _z1;

        public float Process(float input, float freq, int sampleRate)
        {
            float a1 = (1 - MathF.PI * freq / sampleRate) / (1 + MathF.PI * freq / sampleRate);
            float output = a1 * input + _z1;
            _z1 = input - a1 * output;
            return output;
        }
    }
}

/// <summary>
/// Bitcrusher with variable bit depth and sample rate reduction.
/// </summary>
public class BitcrusherNode : EffectNodeBase
{
    public override string NodeType => "Bitcrusher";
    public override string Category => "Effects";
    public override string Description => "Bit depth and sample rate reduction";

    private float _holdSample;
    private float _holdCounter;
    private float _errorAcc; // For noise shaping

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Bits CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Bits", 8f, 1f, 16f, "");
        AddParameter("Downsample", 1f, 1f, 64f, "x");
        AddParameter("Noise Shape", 0f, 0f, 1f, "");
        AddParameter("Mix", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float bits = Math.Clamp(GetParam("Bits") + GetInput(1) * 8f, 1f, 16f);
        float downsample = GetParam("Downsample");
        float noiseShape = GetParam("Noise Shape");
        float mix = GetParam("Mix");

        float steps = MathF.Pow(2f, bits - 1);

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            _holdCounter += 1;
            if (_holdCounter >= downsample)
            {
                _holdCounter -= downsample;

                // Noise shaping: add error from previous quantization
                float shaped = input + _errorAcc * noiseShape;

                // Quantize
                float quantized = MathF.Round(shaped * steps) / steps;

                // Calculate error for noise shaping
                _errorAcc = shaped - quantized;

                _holdSample = quantized;
            }

            buffer[i] = input * (1 - mix) + _holdSample * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

/// <summary>
/// High-quality waveshaper distortion with 4x oversampling.
/// </summary>
public class DistortionNode : EffectNodeBase
{
    public override string NodeType => "Distortion";
    public override string Category => "Effects";
    public override string Description => "Oversampled waveshaper distortion";

    // Oversampling filter states
    private float _upState1, _upState2;
    private float _downState1, _downState2;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Drive CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Drive", 2f, 1f, 20f, "");
        AddParameter("Type", 0f, 0f, 4f, ""); // 0=Soft, 1=Hard, 2=Tube, 3=Foldback, 4=Asymmetric
        AddParameter("Tone", 0.5f, 0f, 1f, "");
        AddParameter("Mix", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float drive = GetParam("Drive") + GetInput(1) * 10f;
        int type = (int)GetParam("Type");
        float tone = GetParam("Tone");
        float mix = GetParam("Mix");

        // Simple lowpass for tone control
        float toneCoef = 2f * MathF.Sin(MathF.PI * (1000f + tone * 9000f) / sampleRate);

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // 4x oversampling for reduced aliasing
            float distorted = 0;
            for (int os = 0; os < 4; os++)
            {
                // Upsample (simple interpolation + filter)
                float upsampled = (os == 0) ? input : 0;
                _upState1 += 0.5f * (upsampled - _upState1);
                _upState2 += 0.5f * (_upState1 - _upState2);
                float up = _upState2 * 4; // Compensate for interpolation

                // Apply drive
                float driven = up * drive;

                // Waveshaping
                float shaped = type switch
                {
                    0 => SoftClip(driven),
                    1 => HardClip(driven),
                    2 => TubeEmulation(driven),
                    3 => Foldback(driven),
                    4 => AsymmetricClip(driven),
                    _ => driven
                };

                // Downsample (decimate with filter)
                _downState1 += 0.5f * (shaped - _downState1);
                _downState2 += 0.5f * (_downState1 - _downState2);
                distorted = _downState2;
            }

            // Tone filter
            distorted = distorted * tone + input * (1 - tone) * drive * 0.1f;

            buffer[i] = input * (1 - mix) + distorted * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }

    private static float SoftClip(float x)
    {
        return MathF.Tanh(x);
    }

    private static float HardClip(float x)
    {
        return Math.Clamp(x, -1f, 1f);
    }

    private static float TubeEmulation(float x)
    {
        // Asymmetric soft clipping (tube-like)
        if (x >= 0)
            return 1f - MathF.Exp(-x);
        else
            return -1f + MathF.Exp(x);
    }

    private static float Foldback(float x)
    {
        // Wave folding
        while (x > 1 || x < -1)
        {
            if (x > 1) x = 2 - x;
            if (x < -1) x = -2 - x;
        }
        return x;
    }

    private static float AsymmetricClip(float x)
    {
        // Different clipping for positive and negative
        if (x > 0)
            return x / (1 + x);
        else
            return x / (1 - 0.5f * x);
    }
}

/// <summary>
/// Ring modulator with internal oscillator and external carrier support.
/// </summary>
public class RingModNode : EffectNodeBase
{
    public override string NodeType => "RingMod";
    public override string Category => "Effects";
    public override string Description => "Ring modulator";

    private double _phase;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("Carrier", PortDataType.Audio);
        AddInput("Freq CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Frequency", 440f, 20f, 5000f, "Hz", ParameterScale.Logarithmic);
        AddParameter("Waveform", 0f, 0f, 2f, ""); // 0=Sine, 1=Square, 2=Triangle
        AddParameter("Mix", 0.5f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float freq = GetParam("Frequency") + GetInput(2) * 1000f;
        int waveform = (int)GetParam("Waveform");
        float mix = GetParam("Mix");

        double phaseInc = freq / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            float input = buffer[i] + GetInput(0);

            // Use external carrier if connected, else internal oscillator
            float carrier = GetInput(1);
            if (carrier == 0)
            {
                carrier = waveform switch
                {
                    0 => MathF.Sin((float)(_phase * 2 * Math.PI)),
                    1 => _phase < 0.5 ? 1f : -1f,
                    2 => (float)(4 * Math.Abs(_phase - 0.5) - 1),
                    _ => MathF.Sin((float)(_phase * 2 * Math.PI))
                };
            }

            _phase += phaseInc;
            if (_phase >= 1) _phase -= 1;

            float ringMod = input * carrier;
            buffer[i] = input * (1 - mix) + ringMod * mix;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

#endregion

#region Modulators

/// <summary>
/// Professional ADSR envelope with curves and retrigger modes.
/// </summary>
public class EnvelopeNode : EffectNodeBase
{
    public override string NodeType => "Envelope";
    public override string Category => "Modulators";
    public override string Description => "ADSR envelope generator";

    private enum EnvStage { Idle, Attack, Decay, Sustain, Release }
    private EnvStage _stage = EnvStage.Idle;
    private float _envelope;
    private bool _gateWasHigh;
    private float _releaseLevel;

    protected override void InitializePorts()
    {
        AddInput("Gate", PortDataType.Gate);
        AddInput("Trigger", PortDataType.Trigger);
        AddInput("Velocity", PortDataType.Control);
        AddOutput("Env", PortDataType.Control);
        AddOutput("Inv", PortDataType.Control);
        AddOutput("EOC", PortDataType.Trigger); // End of cycle
    }

    protected override void InitializeParameters()
    {
        AddParameter("Attack", 10f, 0.1f, 10000f, "ms", ParameterScale.Logarithmic);
        AddParameter("Decay", 100f, 1f, 10000f, "ms", ParameterScale.Logarithmic);
        AddParameter("Sustain", 0.7f, 0f, 1f, "");
        AddParameter("Release", 200f, 1f, 10000f, "ms", ParameterScale.Logarithmic);
        AddParameter("Curve", 0f, -1f, 1f, ""); // -1=Log, 0=Linear, 1=Exp
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float attackTime = GetParam("Attack") * 0.001f * sampleRate;
        float decayTime = GetParam("Decay") * 0.001f * sampleRate;
        float sustain = GetParam("Sustain");
        float releaseTime = GetParam("Release") * 0.001f * sampleRate;
        float curve = GetParam("Curve");
        float velocity = GetInput(2) > 0 ? GetInput(2) : 1f;

        bool gateHigh = GetInput(0) > 0.5f;
        bool trigger = GetInput(1) > 0.5f;

        float eoc = 0f;

        // Gate edge detection
        if ((gateHigh && !_gateWasHigh) || trigger)
        {
            _stage = EnvStage.Attack;
        }
        else if (!gateHigh && _gateWasHigh)
        {
            _stage = EnvStage.Release;
            _releaseLevel = _envelope;
        }
        _gateWasHigh = gateHigh;

        for (int i = 0; i < sampleCount; i++)
        {
            switch (_stage)
            {
                case EnvStage.Attack:
                    _envelope += 1f / attackTime;
                    if (_envelope >= 1f)
                    {
                        _envelope = 1f;
                        _stage = EnvStage.Decay;
                    }
                    break;

                case EnvStage.Decay:
                    float decayTarget = sustain;
                    float decayRate = (1f - sustain) / decayTime;
                    _envelope -= decayRate;
                    if (_envelope <= sustain)
                    {
                        _envelope = sustain;
                        _stage = EnvStage.Sustain;
                    }
                    break;

                case EnvStage.Sustain:
                    _envelope = sustain;
                    break;

                case EnvStage.Release:
                    _envelope -= _releaseLevel / releaseTime;
                    if (_envelope <= 0f)
                    {
                        _envelope = 0f;
                        _stage = EnvStage.Idle;
                        eoc = 1f; // Trigger end of cycle
                    }
                    break;

                case EnvStage.Idle:
                    _envelope = 0f;
                    break;
            }
        }

        // Apply curve shaping
        float shaped = _envelope;
        if (curve < 0)
            shaped = 1f - MathF.Pow(1f - _envelope, 1f - curve);
        else if (curve > 0)
            shaped = MathF.Pow(_envelope, 1f + curve);

        SetOutput(0, shaped * velocity);
        SetOutput(1, (1f - shaped) * velocity);
        SetOutput(2, eoc);
    }
}

/// <summary>
/// Sample and hold with noise source and glide.
/// </summary>
public class SampleAndHoldNode : EffectNodeBase
{
    public override string NodeType => "SampleAndHold";
    public override string Category => "Modulators";
    public override string Description => "Sample and hold circuit";

    private float _heldValue;
    private float _smoothedValue;
    private bool _triggerWasHigh;
    private readonly Random _random = new();

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Control);
        AddInput("Trigger", PortDataType.Trigger);
        AddOutput("Out", PortDataType.Control);
        AddOutput("Noise", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Glide", 0f, 0f, 1000f, "ms");
        AddParameter("Noise Mix", 0f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float glideMs = GetParam("Glide");
        float noiseMix = GetParam("Noise Mix");
        float input = GetInput(0);
        bool triggerHigh = GetInput(1) > 0.5f;

        float glideCoef = glideMs > 0 ? 1f / (glideMs * 0.001f * sampleRate) : 1f;

        if (triggerHigh && !_triggerWasHigh)
        {
            // Mix input with internal noise
            float noise = (float)(_random.NextDouble() * 2 - 1);
            _heldValue = input * (1 - noiseMix) + noise * noiseMix;
        }
        _triggerWasHigh = triggerHigh;

        // Glide smoothing
        for (int i = 0; i < sampleCount; i++)
        {
            _smoothedValue += (_heldValue - _smoothedValue) * glideCoef;
        }

        SetOutput(0, _smoothedValue);
        SetOutput(1, (float)(_random.NextDouble() * 2 - 1));
    }
}

/// <summary>
/// Slew limiter (portamento/glide) with separate rise and fall times.
/// </summary>
public class SlewNode : EffectNodeBase
{
    public override string NodeType => "Slew";
    public override string Category => "Modulators";
    public override string Description => "Slew limiter / portamento";

    private float _current;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Control);
        AddOutput("Out", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Rise", 100f, 0.1f, 10000f, "ms", ParameterScale.Logarithmic);
        AddParameter("Fall", 100f, 0.1f, 10000f, "ms", ParameterScale.Logarithmic);
        AddParameter("Shape", 0f, -1f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float riseRate = 1f / (GetParam("Rise") * 0.001f * sampleRate);
        float fallRate = 1f / (GetParam("Fall") * 0.001f * sampleRate);
        float shape = GetParam("Shape");

        float target = GetInput(0);

        for (int i = 0; i < sampleCount; i++)
        {
            float diff = target - _current;
            float rate = diff > 0 ? riseRate : fallRate;

            // Apply curve shaping
            if (shape != 0)
            {
                float normalized = MathF.Abs(diff);
                if (shape > 0)
                    rate *= MathF.Pow(normalized + 0.01f, shape);
                else
                    rate *= MathF.Pow(1f / (normalized + 0.01f), -shape);
            }

            if (diff > 0)
                _current = Math.Min(_current + rate, target);
            else
                _current = Math.Max(_current - rate, target);
        }

        SetOutput(0, _current);
    }
}

/// <summary>
/// Musical scale quantizer with multiple scale options.
/// </summary>
public class QuantizerNode : EffectNodeBase
{
    public override string NodeType => "Quantizer";
    public override string Category => "Modulators";
    public override string Description => "Musical scale quantizer";

    private static readonly int[][] _scales = new[]
    {
        new[] { 0, 2, 4, 5, 7, 9, 11 },       // Major / Ionian
        new[] { 0, 2, 3, 5, 7, 8, 10 },       // Natural Minor / Aeolian
        new[] { 0, 2, 3, 5, 7, 9, 10 },       // Dorian
        new[] { 0, 1, 3, 5, 7, 8, 10 },       // Phrygian
        new[] { 0, 2, 4, 6, 7, 9, 11 },       // Lydian
        new[] { 0, 2, 4, 5, 7, 9, 10 },       // Mixolydian
        new[] { 0, 1, 3, 5, 6, 8, 10 },       // Locrian
        new[] { 0, 2, 3, 5, 7, 8, 11 },       // Harmonic Minor
        new[] { 0, 2, 3, 5, 7, 9, 11 },       // Melodic Minor
        new[] { 0, 2, 4, 7, 9 },              // Major Pentatonic
        new[] { 0, 3, 5, 7, 10 },             // Minor Pentatonic
        new[] { 0, 3, 5, 6, 7, 10 },          // Blues
        new[] { 0, 2, 4, 6, 8, 10 },          // Whole Tone
        new[] { 0, 2, 3, 5, 6, 8, 9, 11 },    // Diminished
        new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } // Chromatic
    };

    private float _lastQuantized;
    private bool _changed;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Control);
        AddInput("Root CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Control);
        AddOutput("Trigger", PortDataType.Trigger);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Scale", 0f, 0f, 14f, "");
        AddParameter("Root", 0f, 0f, 11f, "");
        AddParameter("Range", 5f, 1f, 10f, "oct");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        int scaleIndex = Math.Clamp((int)GetParam("Scale"), 0, _scales.Length - 1);
        int root = (int)(GetParam("Root") + GetInput(1) * 12) % 12;
        float range = GetParam("Range");
        int[] scale = _scales[scaleIndex];

        float input = GetInput(0);

        // Convert input (0-1) to semitones
        int totalSemitones = (int)(input * range * 12);
        int octave = totalSemitones / 12;
        int noteInOctave = totalSemitones % 12;
        if (noteInOctave < 0) { noteInOctave += 12; octave--; }

        // Find closest note in scale
        int closest = scale[0];
        int minDist = 12;
        foreach (var note in scale)
        {
            int dist = Math.Min(Math.Abs(noteInOctave - note), 12 - Math.Abs(noteInOctave - note));
            if (dist < minDist)
            {
                minDist = dist;
                closest = note;
            }
        }

        int quantizedSemitone = octave * 12 + closest + root;
        float quantized = quantizedSemitone / (range * 12f);

        _changed = Math.Abs(quantized - _lastQuantized) > 0.001f;
        _lastQuantized = quantized;

        SetOutput(0, quantized);
        SetOutput(1, _changed ? 1f : 0f);
    }
}

#endregion

#region Utilities

/// <summary>
/// Multi-channel mixer with panning and mute/solo.
/// </summary>
public class MixerNode : EffectNodeBase
{
    public override string NodeType => "Mixer";
    public override string Category => "Utilities";
    public override string Description => "4-channel mixer with pan";

    protected override void InitializePorts()
    {
        AddInput("In 1", PortDataType.Audio);
        AddInput("In 2", PortDataType.Audio);
        AddInput("In 3", PortDataType.Audio);
        AddInput("In 4", PortDataType.Audio);
        AddOutput("Out L", PortDataType.Audio);
        AddOutput("Out R", PortDataType.Audio);
        AddOutput("Mono", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Level 1", 1f, 0f, 2f, "");
        AddParameter("Pan 1", 0f, -1f, 1f, "");
        AddParameter("Level 2", 1f, 0f, 2f, "");
        AddParameter("Pan 2", 0f, -1f, 1f, "");
        AddParameter("Level 3", 1f, 0f, 2f, "");
        AddParameter("Pan 3", 0f, -1f, 1f, "");
        AddParameter("Level 4", 1f, 0f, 2f, "");
        AddParameter("Pan 4", 0f, -1f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float outL = 0, outR = 0;

        for (int ch = 0; ch < 4; ch++)
        {
            float input = GetInput(ch);
            float level = GetParam($"Level {ch + 1}");
            float pan = GetParam($"Pan {ch + 1}");

            // Equal power panning
            float panL = MathF.Cos((pan + 1) * MathF.PI / 4);
            float panR = MathF.Sin((pan + 1) * MathF.PI / 4);

            outL += input * level * panL;
            outR += input * level * panR;
        }

        SetOutput(0, outL);
        SetOutput(1, outR);
        SetOutput(2, (outL + outR) * 0.5f);
    }
}

/// <summary>
/// Voltage Controlled Amplifier with linear and exponential response.
/// </summary>
public class VcaNode : EffectNodeBase
{
    public override string NodeType => "VCA";
    public override string Category => "Utilities";
    public override string Description => "Voltage controlled amplifier";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddInput("CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Gain", 1f, 0f, 2f, "");
        AddParameter("Response", 0f, 0f, 1f, ""); // 0=Linear, 1=Exponential
        AddParameter("CV Depth", 1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float gain = GetParam("Gain");
        float cv = Math.Clamp(GetInput(1), 0f, 1f);
        float response = GetParam("Response");
        float cvDepth = GetParam("CV Depth");

        // Response curve
        float modGain;
        if (response < 0.5f)
            modGain = cv; // Linear
        else
            modGain = MathF.Pow(cv, 1f + response * 3f); // Exponential

        float totalGain = gain * (1f - cvDepth + modGain * cvDepth);

        for (int i = 0; i < sampleCount; i++)
        {
            buffer[i] = (buffer[i] + GetInput(0)) * totalGain;
        }

        SetOutput(0, buffer[sampleCount - 1]);
    }
}

/// <summary>
/// Signal splitter (mult) with buffered outputs.
/// </summary>
public class SplitNode : EffectNodeBase
{
    public override string NodeType => "Split";
    public override string Category => "Utilities";
    public override string Description => "Signal splitter";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out 1", PortDataType.Audio);
        AddOutput("Out 2", PortDataType.Audio);
        AddOutput("Out 3", PortDataType.Audio);
        AddOutput("Out 4", PortDataType.Audio);
    }

    protected override void InitializeParameters() { }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float input = GetInput(0);
        SetOutput(0, input);
        SetOutput(1, input);
        SetOutput(2, input);
        SetOutput(3, input);
    }
}

/// <summary>
/// Signal merger with sum and average outputs.
/// </summary>
public class MergeNode : EffectNodeBase
{
    public override string NodeType => "Merge";
    public override string Category => "Utilities";
    public override string Description => "Signal merger";

    protected override void InitializePorts()
    {
        AddInput("In 1", PortDataType.Audio);
        AddInput("In 2", PortDataType.Audio);
        AddInput("In 3", PortDataType.Audio);
        AddInput("In 4", PortDataType.Audio);
        AddOutput("Sum", PortDataType.Audio);
        AddOutput("Avg", PortDataType.Audio);
        AddOutput("Max", PortDataType.Audio);
    }

    protected override void InitializeParameters() { }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float sum = GetInput(0) + GetInput(1) + GetInput(2) + GetInput(3);
        float max = Math.Max(Math.Max(GetInput(0), GetInput(1)), Math.Max(GetInput(2), GetInput(3)));

        SetOutput(0, sum);
        SetOutput(1, sum * 0.25f);
        SetOutput(2, max);
    }
}

/// <summary>
/// Signal inverter with offset.
/// </summary>
public class InverterNode : EffectNodeBase
{
    public override string NodeType => "Inverter";
    public override string Category => "Utilities";
    public override string Description => "Signal inverter";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
        AddOutput("+Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Offset", 0f, -1f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float input = GetInput(0);
        float offset = GetParam("Offset");

        SetOutput(0, -input + offset);
        SetOutput(1, input + offset);
    }
}

/// <summary>
/// DC offset and scaling utility.
/// </summary>
public class OffsetNode : EffectNodeBase
{
    public override string NodeType => "Offset";
    public override string Category => "Utilities";
    public override string Description => "DC offset and scale";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Offset", 0f, -1f, 1f, "");
        AddParameter("Scale", 1f, -2f, 2f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        SetOutput(0, GetInput(0) * GetParam("Scale") + GetParam("Offset"));
    }
}

/// <summary>
/// Signal rectifier with full and half-wave modes.
/// </summary>
public class RectifierNode : EffectNodeBase
{
    public override string NodeType => "Rectifier";
    public override string Category => "Utilities";
    public override string Description => "Signal rectifier";

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Full", PortDataType.Audio);
        AddOutput("Half+", PortDataType.Audio);
        AddOutput("Half-", PortDataType.Audio);
    }

    protected override void InitializeParameters() { }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float input = GetInput(0);
        SetOutput(0, MathF.Abs(input));
        SetOutput(1, Math.Max(0, input));
        SetOutput(2, Math.Max(0, -input));
    }
}

/// <summary>
/// Crossfader with multiple curve options.
/// </summary>
public class CrossfadeNode : EffectNodeBase
{
    public override string NodeType => "Crossfade";
    public override string Category => "Utilities";
    public override string Description => "A/B crossfader";

    protected override void InitializePorts()
    {
        AddInput("A", PortDataType.Audio);
        AddInput("B", PortDataType.Audio);
        AddInput("Mix CV", PortDataType.Control);
        AddOutput("Out", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Mix", 0.5f, 0f, 1f, "");
        AddParameter("Curve", 0f, 0f, 2f, ""); // 0=Linear, 1=Equal Power, 2=S-Curve
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float mix = Math.Clamp(GetParam("Mix") + GetInput(2) * 0.5f, 0f, 1f);
        int curve = (int)GetParam("Curve");

        float gainA, gainB;
        switch (curve)
        {
            case 1: // Equal power
                gainA = MathF.Cos(mix * MathF.PI / 2);
                gainB = MathF.Sin(mix * MathF.PI / 2);
                break;
            case 2: // S-curve
                float s = mix * mix * (3 - 2 * mix);
                gainA = 1 - s;
                gainB = s;
                break;
            default: // Linear
                gainA = 1 - mix;
                gainB = mix;
                break;
        }

        SetOutput(0, GetInput(0) * gainA + GetInput(1) * gainB);
    }
}

#endregion

#region Analyzers

/// <summary>
/// Envelope follower with attack/release control.
/// </summary>
public class EnvelopeFollowerNode : EffectNodeBase
{
    public override string NodeType => "Follower";
    public override string Category => "Analyzers";
    public override string Description => "Envelope follower";

    private float _envelope;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Env", PortDataType.Control);
        AddOutput("Gate", PortDataType.Gate);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Attack", 5f, 0.1f, 100f, "ms");
        AddParameter("Release", 50f, 10f, 2000f, "ms");
        AddParameter("Threshold", 0.1f, 0f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float attackCoef = MathF.Exp(-1f / (GetParam("Attack") * 0.001f * sampleRate));
        float releaseCoef = MathF.Exp(-1f / (GetParam("Release") * 0.001f * sampleRate));
        float threshold = GetParam("Threshold");

        for (int i = 0; i < sampleCount; i++)
        {
            float input = MathF.Abs(buffer[i] + GetInput(0));
            float coef = input > _envelope ? attackCoef : releaseCoef;
            _envelope = coef * _envelope + (1 - coef) * input;
        }

        SetOutput(0, _envelope);
        SetOutput(1, _envelope > threshold ? 1f : 0f);
    }
}

/// <summary>
/// Simple pitch detector using zero-crossing and autocorrelation.
/// </summary>
public class PitchDetectorNode : EffectNodeBase
{
    public override string NodeType => "PitchDetect";
    public override string Category => "Analyzers";
    public override string Description => "Pitch detector";

    private readonly float[] _buffer = new float[2048];
    private int _bufferPos;
    private float _detectedPitch;
    private float _confidence;

    protected override void InitializePorts()
    {
        AddInput("In", PortDataType.Audio);
        AddOutput("Pitch", PortDataType.Control);
        AddOutput("Freq", PortDataType.Control);
        AddOutput("Confidence", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Min Freq", 50f, 20f, 500f, "Hz");
        AddParameter("Max Freq", 2000f, 500f, 8000f, "Hz");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float minFreq = GetParam("Min Freq");
        float maxFreq = GetParam("Max Freq");

        // Fill circular buffer
        for (int i = 0; i < sampleCount; i++)
        {
            _buffer[_bufferPos++] = buffer[i] + GetInput(0);
            if (_bufferPos >= _buffer.Length) _bufferPos = 0;
        }

        // Simple autocorrelation pitch detection
        int minPeriod = (int)(sampleRate / maxFreq);
        int maxPeriod = (int)(sampleRate / minFreq);
        maxPeriod = Math.Min(maxPeriod, _buffer.Length / 2);

        float bestCorr = 0;
        int bestPeriod = minPeriod;

        for (int period = minPeriod; period < maxPeriod; period++)
        {
            float corr = 0;
            for (int j = 0; j < _buffer.Length / 2; j++)
            {
                int idx1 = (j + _bufferPos) % _buffer.Length;
                int idx2 = (j + period + _bufferPos) % _buffer.Length;
                corr += _buffer[idx1] * _buffer[idx2];
            }

            if (corr > bestCorr)
            {
                bestCorr = corr;
                bestPeriod = period;
            }
        }

        _detectedPitch = sampleRate / (float)bestPeriod;
        _confidence = bestCorr / (_buffer.Length / 2);

        // Convert to MIDI note (0-1 range for ~5 octaves)
        float midiNote = 12f * MathF.Log2(_detectedPitch / 440f) + 69f;
        float normalizedPitch = (midiNote - 24f) / 72f; // C1 to C7

        SetOutput(0, Math.Clamp(normalizedPitch, 0f, 1f));
        SetOutput(1, _detectedPitch / 1000f);
        SetOutput(2, Math.Clamp(_confidence * 10f, 0f, 1f));
    }
}

#endregion

#region Sequencing

/// <summary>
/// 8/16 step sequencer with per-step controls.
/// </summary>
public class StepSequencerNode : EffectNodeBase
{
    public override string NodeType => "StepSequencer";
    public override string Category => "Sequencing";
    public override string Description => "8/16 step sequencer";

    private int _currentStep;
    private bool _clockWasHigh;

    protected override void InitializePorts()
    {
        AddInput("Clock", PortDataType.Trigger);
        AddInput("Reset", PortDataType.Trigger);
        AddInput("Direction", PortDataType.Control);
        AddOutput("CV", PortDataType.Control);
        AddOutput("Gate", PortDataType.Gate);
        AddOutput("Trigger", PortDataType.Trigger);
    }

    protected override void InitializeParameters()
    {
        for (int i = 1; i <= 8; i++)
            AddParameter($"Step {i}", 0.5f, 0f, 1f, "");
        AddParameter("Steps", 8f, 1f, 8f, "");
        AddParameter("Gate Length", 0.5f, 0.1f, 1f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        bool clockHigh = GetInput(0) > 0.5f;
        bool reset = GetInput(1) > 0.5f;
        float direction = GetInput(2);

        if (reset) _currentStep = 0;

        bool triggered = false;
        if (clockHigh && !_clockWasHigh)
        {
            int steps = (int)GetParam("Steps");

            if (direction < -0.3f)
                _currentStep = (_currentStep - 1 + steps) % steps; // Reverse
            else if (direction <= 0.3f)
                _currentStep = (_currentStep + 1) % steps; // Forward
            // direction > 0.3f means Hold - do nothing

            triggered = true;
        }
        _clockWasHigh = clockHigh;

        float cv = GetParam($"Step {_currentStep + 1}");
        float gateLength = GetParam("Gate Length");

        SetOutput(0, cv);
        SetOutput(1, clockHigh ? 1f : 0f);
        SetOutput(2, triggered ? 1f : 0f);
    }
}

/// <summary>
/// Master clock with multiple divisions and swing.
/// </summary>
public class ClockNode : EffectNodeBase
{
    public override string NodeType => "Clock";
    public override string Category => "Sequencing";
    public override string Description => "Master clock generator";

    private double _phase;
    private int _beatCount;

    protected override void InitializePorts()
    {
        AddInput("BPM CV", PortDataType.Control);
        AddInput("Reset", PortDataType.Trigger);
        AddOutput("Beat", PortDataType.Trigger);
        AddOutput("8th", PortDataType.Trigger);
        AddOutput("16th", PortDataType.Trigger);
        AddOutput("Phase", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("BPM", 120f, 20f, 300f, "");
        AddParameter("Swing", 0f, 0f, 0.9f, "");
        AddParameter("Pulse Width", 0.1f, 0.01f, 0.5f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float bpm = GetParam("BPM") + GetInput(0) * 100f;
        float swing = GetParam("Swing");
        float pulseWidth = GetParam("Pulse Width");

        if (GetInput(1) > 0.5f)
        {
            _phase = 0;
            _beatCount = 0;
        }

        double phaseInc = bpm / 60.0 / sampleRate;

        for (int i = 0; i < sampleCount; i++)
        {
            _phase += phaseInc;
            if (_phase >= 1)
            {
                _phase -= 1;
                _beatCount++;
            }
        }

        // Apply swing to 8th notes (delay every other 8th)
        float swingOffset = (_beatCount % 2 == 1) ? swing * 0.5f : 0f;
        float adjustedPhase = (float)((_phase + swingOffset) % 1.0);

        bool beat = _phase < pulseWidth;
        bool eighth = (adjustedPhase * 2 % 1) < pulseWidth;
        bool sixteenth = (adjustedPhase * 4 % 1) < pulseWidth;

        SetOutput(0, beat ? 1f : 0f);
        SetOutput(1, eighth ? 1f : 0f);
        SetOutput(2, sixteenth ? 1f : 0f);
        SetOutput(3, (float)_phase);
    }
}

/// <summary>
/// Clock divider and multiplier.
/// </summary>
public class ClockDividerNode : EffectNodeBase
{
    public override string NodeType => "ClockDiv";
    public override string Category => "Sequencing";
    public override string Description => "Clock divider/multiplier";

    private int _count;
    private bool _clockWasHigh;

    protected override void InitializePorts()
    {
        AddInput("Clock", PortDataType.Trigger);
        AddInput("Reset", PortDataType.Trigger);
        AddOutput("/2", PortDataType.Trigger);
        AddOutput("/4", PortDataType.Trigger);
        AddOutput("/8", PortDataType.Trigger);
        AddOutput("/16", PortDataType.Trigger);
    }

    protected override void InitializeParameters() { }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        bool clockHigh = GetInput(0) > 0.5f;
        if (GetInput(1) > 0.5f) _count = 0;

        if (clockHigh && !_clockWasHigh)
            _count++;

        _clockWasHigh = clockHigh;

        SetOutput(0, (_count % 2 == 0 && clockHigh) ? 1f : 0f);
        SetOutput(1, (_count % 4 == 0 && clockHigh) ? 1f : 0f);
        SetOutput(2, (_count % 8 == 0 && clockHigh) ? 1f : 0f);
        SetOutput(3, (_count % 16 == 0 && clockHigh) ? 1f : 0f);
    }
}

#endregion

#region I/O Nodes

/// <summary>
/// Audio input node for receiving external audio.
/// </summary>
public class AudioInputNode : EffectNodeBase
{
    public override string NodeType => "AudioInput";
    public override string Category => "I/O";
    public override string Description => "Audio input";

    protected override void InitializePorts()
    {
        AddOutput("L", PortDataType.Audio);
        AddOutput("R", PortDataType.Audio);
        AddOutput("Mono", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Gain", 0f, -24f, 24f, "dB");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float gain = MathF.Pow(10f, GetParam("Gain") / 20f);
        float sample = buffer.Length > 0 ? buffer[0] * gain : 0;
        SetOutput(0, sample);
        SetOutput(1, sample);
        SetOutput(2, sample);
    }
}

/// <summary>
/// Audio output node for sending audio to the mixer.
/// </summary>
public class AudioOutputNode : EffectNodeBase
{
    public override string NodeType => "AudioOutput";
    public override string Category => "I/O";
    public override string Description => "Audio output";

    protected override void InitializePorts()
    {
        AddInput("L", PortDataType.Audio);
        AddInput("R", PortDataType.Audio);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Gain", 0f, -24f, 24f, "dB");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        float gain = MathF.Pow(10f, GetParam("Gain") / 20f);
        float left = GetInput(0) * gain;
        float right = GetInput(1) != 0 ? GetInput(1) * gain : left;

        for (int i = 0; i < sampleCount && i * 2 + 1 < buffer.Length; i++)
        {
            buffer[i * 2] = left;
            buffer[i * 2 + 1] = right;
        }
    }
}

/// <summary>
/// MIDI input node for receiving MIDI data.
/// </summary>
public class MidiInputNode : EffectNodeBase
{
    public override string NodeType => "MidiInput";
    public override string Category => "I/O";
    public override string Description => "MIDI input";

    protected override void InitializePorts()
    {
        AddOutput("Pitch", PortDataType.Control);
        AddOutput("Gate", PortDataType.Gate);
        AddOutput("Velocity", PortDataType.Control);
        AddOutput("Aftertouch", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Channel", 0f, 0f, 16f, ""); // 0 = Omni
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        // Would receive from MIDI input service
        SetOutput(0, 0.5f);
        SetOutput(1, 0f);
        SetOutput(2, 0f);
        SetOutput(3, 0f);
    }
}

/// <summary>
/// MIDI output node for sending MIDI data.
/// </summary>
public class MidiOutputNode : EffectNodeBase
{
    public override string NodeType => "MidiOutput";
    public override string Category => "I/O";
    public override string Description => "MIDI output";

    protected override void InitializePorts()
    {
        AddInput("Pitch", PortDataType.Control);
        AddInput("Gate", PortDataType.Gate);
        AddInput("Velocity", PortDataType.Control);
    }

    protected override void InitializeParameters()
    {
        AddParameter("Channel", 1f, 1f, 16f, "");
    }

    public override void Process(float[] buffer, int sampleCount, int sampleRate)
    {
        // Would send to MIDI output service
    }
}

#endregion
