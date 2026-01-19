using System;
using System.Threading.Tasks;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing MusicEngine projects
/// </summary>
public interface IProjectService
{
    MusicProject? CurrentProject { get; }

    event EventHandler<MusicProject>? ProjectLoaded;
    event EventHandler? ProjectClosed;

    Task<MusicProject> CreateProjectAsync(string name, string path);
    Task<MusicProject> OpenProjectAsync(string projectFilePath);
    Task SaveProjectAsync(MusicProject project);
    Task CloseProjectAsync();

    MusicScript CreateScript(MusicProject project, string name, string? folder = null);
    Task SaveScriptAsync(MusicScript script);
    Task DeleteScriptAsync(MusicScript script);

    Task<AudioAsset> ImportAudioAsync(MusicProject project, string sourcePath, string alias, string category = "General");
    Task DeleteAudioAssetAsync(AudioAsset asset);

    Task AddReferenceAsync(MusicProject project, ProjectReference reference);
    Task RemoveReferenceAsync(MusicProject project, ProjectReference reference);
}
