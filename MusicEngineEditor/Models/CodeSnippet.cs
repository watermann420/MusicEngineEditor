using System;
using System.Collections.Generic;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a code snippet template for quick insertion into the editor
/// </summary>
public class CodeSnippet
{
    /// <summary>
    /// Unique name identifier for the snippet
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of what the snippet does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The actual code template with placeholders
    /// Supports $CURSOR$ for cursor position and $1$, $2$, etc. for tab stops
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Category for organizing snippets (e.g., "Synths", "Patterns", "Effects", "MIDI")
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Short trigger text to quickly insert the snippet (e.g., "syn" for synth template)
    /// </summary>
    public string Shortcut { get; set; } = string.Empty;

    /// <summary>
    /// Author or creator of the snippet
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// List of searchable tags for finding snippets
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Date when the snippet was created or last modified
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Indicates whether this is a built-in default snippet
    /// </summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Creates a deep copy of this snippet
    /// </summary>
    public CodeSnippet Clone()
    {
        return new CodeSnippet
        {
            Name = Name,
            Description = Description,
            Code = Code,
            Category = Category,
            Shortcut = Shortcut,
            Author = Author,
            Tags = new List<string>(Tags),
            CreatedDate = CreatedDate,
            IsBuiltIn = IsBuiltIn
        };
    }

    public override string ToString() => $"{Name} ({Shortcut})";
}

/// <summary>
/// Container for storing snippets in JSON format
/// </summary>
public class SnippetCollection
{
    /// <summary>
    /// Version of the snippet file format
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Collection of code snippets
    /// </summary>
    public List<CodeSnippet> Snippets { get; set; } = new();
}
