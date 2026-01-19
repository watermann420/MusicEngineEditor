using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Represents a detected numeric literal in the code
/// </summary>
public class DetectedNumber
{
    /// <summary>
    /// The start offset in the document
    /// </summary>
    public int StartOffset { get; init; }

    /// <summary>
    /// The end offset in the document
    /// </summary>
    public int EndOffset { get; init; }

    /// <summary>
    /// The original text of the number
    /// </summary>
    public string OriginalText { get; init; } = string.Empty;

    /// <summary>
    /// The parsed value
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Whether this is a floating-point number
    /// </summary>
    public bool IsFloat { get; init; }

    /// <summary>
    /// Whether this number has an 'f' suffix
    /// </summary>
    public bool HasFloatSuffix { get; init; }

    /// <summary>
    /// Whether this number has a 'd' suffix
    /// </summary>
    public bool HasDoubleSuffix { get; init; }

    /// <summary>
    /// The line number (1-based)
    /// </summary>
    public int Line { get; init; }

    /// <summary>
    /// The column (1-based)
    /// </summary>
    public int Column { get; init; }

    /// <summary>
    /// Slider configuration if detected from comments
    /// </summary>
    public SliderConfig? SliderConfig { get; set; }

    /// <summary>
    /// The context/parameter name this number is associated with (e.g., "bpm", "velocity")
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// The length of the text
    /// </summary>
    public int Length => EndOffset - StartOffset;
}

/// <summary>
/// Configuration for a slider, detected from comments or inferred from context
/// </summary>
public class SliderConfig
{
    public double MinValue { get; init; }
    public double MaxValue { get; init; }
    public double Step { get; init; } = 1;
    public string? Label { get; init; }

    /// <summary>
    /// Create a default slider config for a given context
    /// </summary>
    public static SliderConfig FromContext(string? context, double currentValue, bool isFloat)
    {
        var contextLower = context?.ToLower() ?? "";

        // BPM: 20-999
        if (contextLower.Contains("bpm") || contextLower.Contains("tempo"))
        {
            return new SliderConfig { MinValue = 20, MaxValue = 300, Step = 1, Label = "BPM" };
        }

        // Velocity: 0-127
        if (contextLower.Contains("velocity") || contextLower.Contains("vel"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 127, Step = 1, Label = "Velocity" };
        }

        // MIDI note: 0-127
        if (contextLower.Contains("note") && !contextLower.Contains("noteon") && !contextLower.Contains("noteoff"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 127, Step = 1, Label = "Note" };
        }

        // Frequency: 20-20000
        if (contextLower.Contains("freq") || contextLower.Contains("hz"))
        {
            return new SliderConfig { MinValue = 20, MaxValue = 20000, Step = 1, Label = "Frequency" };
        }

        // Volume/gain: 0-1 or 0-100
        if (contextLower.Contains("volume") || contextLower.Contains("gain") || contextLower.Contains("level"))
        {
            return isFloat || currentValue <= 1.0
                ? new SliderConfig { MinValue = 0, MaxValue = 1, Step = 0.01, Label = "Volume" }
                : new SliderConfig { MinValue = 0, MaxValue = 100, Step = 1, Label = "Volume" };
        }

        // Cutoff/resonance: 0-1
        if (contextLower.Contains("cutoff") || contextLower.Contains("resonance") || contextLower.Contains("filter"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 1, Step = 0.01, Label = contextLower.Contains("cutoff") ? "Cutoff" : "Resonance" };
        }

        // Attack/Decay/Release time: 0-10 seconds
        if (contextLower.Contains("attack") || contextLower.Contains("decay") || contextLower.Contains("release") || contextLower.Contains("sustain"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 10, Step = 0.01, Label = "Time" };
        }

        // Beat/duration: 0-16
        if (contextLower.Contains("beat") || contextLower.Contains("duration") || contextLower.Contains("length"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 16, Step = 0.25, Label = "Beat" };
        }

        // Pan: -1 to 1
        if (contextLower.Contains("pan"))
        {
            return new SliderConfig { MinValue = -1, MaxValue = 1, Step = 0.01, Label = "Pan" };
        }

        // Waveform: 0-4
        if (contextLower.Contains("waveform") || contextLower.Contains("wave"))
        {
            return new SliderConfig { MinValue = 0, MaxValue = 4, Step = 1, Label = "Waveform" };
        }

        // Octave: -4 to 4
        if (contextLower.Contains("octave"))
        {
            return new SliderConfig { MinValue = -4, MaxValue = 4, Step = 1, Label = "Octave" };
        }

        // Semitones: -24 to 24
        if (contextLower.Contains("semitone") || contextLower.Contains("transpose"))
        {
            return new SliderConfig { MinValue = -24, MaxValue = 24, Step = 1, Label = "Semitones" };
        }

        // Default ranges based on value and type
        if (isFloat)
        {
            // Float: typically 0-1 range
            return new SliderConfig { MinValue = 0, MaxValue = 1, Step = 0.01, Label = null };
        }

        // Integer: use adaptive range based on current value
        var magnitude = Math.Max(1, Math.Abs(currentValue));
        var maxVal = Math.Pow(10, Math.Ceiling(Math.Log10(magnitude + 1)));
        var minVal = currentValue >= 0 ? 0 : -maxVal;

        return new SliderConfig { MinValue = minVal, MaxValue = maxVal, Step = 1, Label = null };
    }
}

/// <summary>
/// Detects numeric literals in code and their positions
/// </summary>
public class NumberDetector
{
    // Regex pattern for numeric literals:
    // - Integers: 123, -123
    // - Floats: 1.5, 1.5f, 1.5d, .5, .5f, 1.5e10
    // - Hex: 0x1A (excluded from slider support)
    private static readonly Regex NumberPattern = new(
        @"(?<![a-zA-Z_0-9])(-?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?[fFdD]?)(?![a-zA-Z_0-9])",
        RegexOptions.Compiled);

    // Pattern for @slider comment annotation
    // Supports: // @slider(min, max) or // @slider(min, max, step) or // @slider(min, max, step, "label")
    private static readonly Regex SliderAnnotationPattern = new(
        @"//\s*@slider\s*\(\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*(?:,\s*(-?\d+(?:\.\d+)?))?\s*(?:,\s*""([^""]+)"")?\s*\)",
        RegexOptions.Compiled);

    // Pattern to detect context from method calls like NoteOn(note, velocity)
    private static readonly Regex MethodCallPattern = new(
        @"(\w+)\s*\(\s*(?:[^,)]*,\s*)*([^,)]+)\s*(?:,|\))",
        RegexOptions.Compiled);

    /// <summary>
    /// Detects all numeric literals in the given text
    /// </summary>
    public static List<DetectedNumber> DetectNumbers(TextDocument document)
    {
        var results = new List<DetectedNumber>();
        var text = document.Text;

        // First pass: collect slider annotations per line
        var sliderAnnotations = new Dictionary<int, SliderConfig>();
        foreach (Match match in SliderAnnotationPattern.Matches(text))
        {
            var lineNumber = document.GetLineByOffset(match.Index).LineNumber;
            var min = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var max = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var step = match.Groups[3].Success
                ? double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture)
                : (max - min) / 100.0;
            var label = match.Groups[4].Success ? match.Groups[4].Value : null;

            sliderAnnotations[lineNumber] = new SliderConfig
            {
                MinValue = min,
                MaxValue = max,
                Step = step,
                Label = label
            };
        }

        // Second pass: find all numbers
        foreach (Match match in NumberPattern.Matches(text))
        {
            var numText = match.Value;
            var startOffset = match.Index;
            var endOffset = match.Index + match.Length;

            // Skip hex numbers
            if (numText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                continue;

            // Skip if inside a string literal
            if (IsInsideString(text, startOffset))
                continue;

            // Skip if inside a comment
            if (IsInsideComment(text, startOffset))
                continue;

            // Parse the number
            var hasFloatSuffix = numText.EndsWith("f", StringComparison.OrdinalIgnoreCase);
            var hasDoubleSuffix = numText.EndsWith("d", StringComparison.OrdinalIgnoreCase);
            var numberPart = hasFloatSuffix || hasDoubleSuffix
                ? numText.Substring(0, numText.Length - 1)
                : numText;

            if (!double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                continue;

            var isFloat = numText.Contains('.') || hasFloatSuffix || hasDoubleSuffix || numText.Contains('e') || numText.Contains('E');

            var location = document.GetLocation(startOffset);

            var detected = new DetectedNumber
            {
                StartOffset = startOffset,
                EndOffset = endOffset,
                OriginalText = numText,
                Value = value,
                IsFloat = isFloat,
                HasFloatSuffix = hasFloatSuffix,
                HasDoubleSuffix = hasDoubleSuffix,
                Line = location.Line,
                Column = location.Column
            };

            // Check for slider annotation on this line
            if (sliderAnnotations.TryGetValue(detected.Line, out var sliderConfig))
            {
                detected.SliderConfig = sliderConfig;
            }
            else
            {
                // Try to infer context from surrounding code
                detected.Context = GetContext(text, startOffset);
                detected.SliderConfig = SliderConfig.FromContext(detected.Context, value, isFloat);
            }

            results.Add(detected);
        }

        return results;
    }

    /// <summary>
    /// Find the number at a specific offset
    /// </summary>
    public static DetectedNumber? GetNumberAtOffset(TextDocument document, int offset)
    {
        var numbers = DetectNumbers(document);
        foreach (var num in numbers)
        {
            if (offset >= num.StartOffset && offset <= num.EndOffset)
            {
                return num;
            }
        }
        return null;
    }

    /// <summary>
    /// Find all numbers on a specific line
    /// </summary>
    public static List<DetectedNumber> GetNumbersOnLine(TextDocument document, int lineNumber)
    {
        var numbers = DetectNumbers(document);
        return numbers.FindAll(n => n.Line == lineNumber);
    }

    /// <summary>
    /// Check if the position is inside a string literal
    /// </summary>
    private static bool IsInsideString(string text, int offset)
    {
        bool inString = false;
        bool inVerbatim = false;
        char stringChar = '\0';

        for (int i = 0; i < offset && i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (c == '\\' && !inVerbatim && i + 1 < text.Length)
                {
                    i++; // Skip escaped character
                    continue;
                }

                if (c == stringChar)
                {
                    if (inVerbatim && i + 1 < text.Length && text[i + 1] == '"')
                    {
                        i++; // Skip escaped quote in verbatim string
                        continue;
                    }
                    inString = false;
                }
            }
            else
            {
                if (c == '"' || c == '\'')
                {
                    inString = true;
                    stringChar = c;
                    inVerbatim = c == '"' && i > 0 && text[i - 1] == '@';
                }
            }
        }

        return inString;
    }

    /// <summary>
    /// Check if the position is inside a comment
    /// </summary>
    private static bool IsInsideComment(string text, int offset)
    {
        // Check for // comment
        var lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
        var beforeOffset = text.Substring(lineStart, offset - lineStart);
        if (beforeOffset.Contains("//"))
        {
            return true;
        }

        // Check for /* */ comment
        var lastOpen = text.LastIndexOf("/*", offset, StringComparison.Ordinal);
        if (lastOpen >= 0)
        {
            var lastClose = text.LastIndexOf("*/", offset, StringComparison.Ordinal);
            if (lastClose < lastOpen)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to determine the context of a number from surrounding code
    /// </summary>
    private static string? GetContext(string text, int offset)
    {
        // Look back to find method name and parameter position
        var lineStart = text.LastIndexOf('\n', Math.Max(0, offset - 1)) + 1;
        var lineEnd = text.IndexOf('\n', offset);
        if (lineEnd < 0) lineEnd = text.Length;

        var line = text.Substring(lineStart, lineEnd - lineStart);
        var posInLine = offset - lineStart;

        // Common patterns and their parameter names
        var patterns = new Dictionary<string, string[]>
        {
            { @"\.Bpm\s*=", new[] { "bpm" } },
            { @"NoteOn\s*\(", new[] { "note", "velocity" } },
            { @"NoteOff\s*\(", new[] { "note" } },
            { @"SetParameter\s*\(\s*""([^""]+)""\s*,", new[] { "$1" } },  // Extract parameter name
            { @"Schedule\s*\(", new[] { "beat" } },
            { @"\.Note\s*\(", new[] { "pitch", "beat", "duration", "velocity" } },
            { @"Delay\s*\(", new[] { "milliseconds" } },
            { @"CreatePattern\s*\(", new[] { "target" } },
            { @"RouteMidiInput\s*\(", new[] { "device", "target" } },
            { @"\.Volume\s*=", new[] { "volume" } },
            { @"\.cutoff", new[] { "cutoff" } },
            { @"\.resonance", new[] { "resonance" } },
            { @"\.attack", new[] { "attack" } },
            { @"\.decay", new[] { "decay" } },
            { @"\.sustain", new[] { "sustain" } },
            { @"\.release", new[] { "release" } },
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(line, pattern.Key, RegexOptions.IgnoreCase);
            if (match.Success && match.Index < posInLine)
            {
                // Count commas to determine parameter index
                var afterMatch = line.Substring(match.Index + match.Length);
                var upToPos = afterMatch.Substring(0, Math.Min(posInLine - match.Index - match.Length, afterMatch.Length));

                // Handle captured group (like SetParameter parameter name)
                if (match.Groups.Count > 1 && match.Groups[1].Success)
                {
                    return match.Groups[1].Value;
                }

                var commaCount = upToPos.Count(c => c == ',');
                if (commaCount < pattern.Value.Length)
                {
                    return pattern.Value[commaCount];
                }
            }
        }

        // Check for assignment context
        var assignMatch = Regex.Match(line.Substring(0, Math.Min(posInLine, line.Length)), @"(\w+)\s*=\s*$");
        if (assignMatch.Success)
        {
            return assignMatch.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Format a number value according to the original format
    /// </summary>
    public static string FormatNumber(double value, DetectedNumber original)
    {
        string formatted;

        if (original.IsFloat)
        {
            // Preserve decimal places from original
            var decimalPlaces = 0;
            if (original.OriginalText.Contains('.'))
            {
                var afterDecimal = original.OriginalText.Split('.')[1];
                // Remove suffix if present
                afterDecimal = afterDecimal.TrimEnd('f', 'F', 'd', 'D');
                decimalPlaces = afterDecimal.Length;
            }
            else
            {
                decimalPlaces = 1; // At least one decimal for floats
            }

            formatted = value.ToString($"F{decimalPlaces}", CultureInfo.InvariantCulture);
        }
        else
        {
            // Integer
            formatted = ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        }

        // Add back suffix
        if (original.HasFloatSuffix)
        {
            formatted += "f";
        }
        else if (original.HasDoubleSuffix)
        {
            formatted += "d";
        }

        return formatted;
    }
}
