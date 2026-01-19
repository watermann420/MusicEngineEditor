namespace MusicEngineEditor.Models;

/// <summary>
/// Represents an audio asset (sample) in the project
/// </summary>
public class AudioAsset
{
    public string FilePath { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Category { get; set; } = "General";

    public string FileName => System.IO.Path.GetFileName(FilePath);

    /// <summary>
    /// Duration in seconds (loaded when file is analyzed)
    /// </summary>
    public double Duration { get; set; }

    /// <summary>
    /// Sample rate of the audio file
    /// </summary>
    public int SampleRate { get; set; }

    /// <summary>
    /// Number of channels
    /// </summary>
    public int Channels { get; set; }
}
