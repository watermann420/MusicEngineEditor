using System.IO;
using System.Text.Json;
using FluentAssertions;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using Xunit;

namespace MusicEngineEditor.Tests.Services;

/// <summary>
/// Unit tests for ProjectService
/// </summary>
public class ProjectServiceTests : IDisposable
{
    private readonly ProjectService _projectService;
    private readonly string _testDirectory;

    public ProjectServiceTests()
    {
        _projectService = new ProjectService();
        _testDirectory = Path.Combine(Path.GetTempPath(), "MusicEngineEditorTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Cleanup test directory
        if (Directory.Exists(_testDirectory))
        {
            try
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    #region CreateProjectAsync Tests

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateProjectWithCorrectName()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        project.Name.Should().Be(projectName);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateProjectDirectory()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        var projectDir = Path.Combine(_testDirectory, projectName);
        Directory.Exists(projectDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateScriptsSubdirectory()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        var scriptsDir = Path.Combine(_testDirectory, projectName, projectName, "Scripts");
        Directory.Exists(scriptsDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateAudioSubdirectory()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        var audioDir = Path.Combine(_testDirectory, projectName, projectName, "Audio");
        Directory.Exists(audioDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateBinSubdirectory()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        var binDir = Path.Combine(_testDirectory, projectName, projectName, "bin");
        Directory.Exists(binDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateObjSubdirectory()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        var objDir = Path.Combine(_testDirectory, projectName, projectName, "obj");
        Directory.Exists(objDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateProjectFile()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        File.Exists(project.FilePath).Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldCreateDefaultMainScript()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        project.Scripts.Should().HaveCount(1);
        project.Scripts[0].IsEntryPoint.Should().BeTrue();
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSetDefaultSettings()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        project.Settings.SampleRate.Should().Be(44100);
        project.Settings.BufferSize.Should().Be(512);
        project.Settings.DefaultBpm.Should().Be(120);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSetCurrentProject()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        _projectService.CurrentProject.Should().Be(project);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldRaiseProjectLoadedEvent()
    {
        // Arrange
        var projectName = "TestProject";
        MusicProject? loadedProject = null;
        _projectService.ProjectLoaded += (_, p) => loadedProject = p;

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        loadedProject.Should().Be(project);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldGenerateValidGuid()
    {
        // Arrange
        var projectName = "TestProject";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        project.Guid.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateProjectAsync_ShouldSanitizeNamespace()
    {
        // Arrange
        var projectName = "Test-Project 123";

        // Act
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);

        // Assert
        project.Namespace.Should().NotContain("-");
        project.Namespace.Should().NotContain(" ");
    }

    #endregion

    #region OpenProjectAsync Tests

    [Fact]
    public async Task OpenProjectAsync_ShouldLoadProjectFromFile()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var projectPath = createdProject.FilePath;

        // Create a new service instance to simulate reopening
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(projectPath);

        // Assert
        openedProject.Name.Should().Be(projectName);
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldLoadScripts()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var projectPath = createdProject.FilePath;
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(projectPath);

        // Assert
        openedProject.Scripts.Should().HaveCount(1);
        openedProject.Scripts[0].IsEntryPoint.Should().BeTrue();
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldPreserveProjectGuid()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var originalGuid = createdProject.Guid;
        var projectPath = createdProject.FilePath;
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(projectPath);

        // Assert
        openedProject.Guid.Should().Be(originalGuid);
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldLoadSettings()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        createdProject.Settings.SampleRate = 48000;
        await _projectService.SaveProjectAsync(createdProject);
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(createdProject.FilePath);

        // Assert
        openedProject.Settings.SampleRate.Should().Be(48000);
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldSetCurrentProject()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(createdProject.FilePath);

        // Assert
        newService.CurrentProject.Should().Be(openedProject);
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldRaiseProjectLoadedEvent()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var newService = new ProjectService();
        MusicProject? loadedProject = null;
        newService.ProjectLoaded += (_, p) => loadedProject = p;

        // Act
        var openedProject = await newService.OpenProjectAsync(createdProject.FilePath);

        // Assert
        loadedProject.Should().Be(openedProject);
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldThrowForInvalidFile()
    {
        // Arrange
        var invalidPath = Path.Combine(_testDirectory, "nonexistent.meproj");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(
            () => _projectService.OpenProjectAsync(invalidPath));
    }

    [Fact]
    public async Task OpenProjectAsync_ShouldLoadScriptContent()
    {
        // Arrange
        var projectName = "TestProject";
        var createdProject = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var newService = new ProjectService();

        // Act
        var openedProject = await newService.OpenProjectAsync(createdProject.FilePath);

        // Assert
        openedProject.Scripts[0].Content.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region SaveProjectAsync Tests

    [Fact]
    public async Task SaveProjectAsync_ShouldUpdateModifiedDate()
    {
        // Arrange
        var projectName = "TestProject";
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var originalModified = project.Modified;
        await Task.Delay(10); // Ensure time difference

        // Act
        await _projectService.SaveProjectAsync(project);

        // Assert
        project.Modified.Should().BeAfter(originalModified);
    }

    [Fact]
    public async Task SaveProjectAsync_ShouldClearIsDirtyFlag()
    {
        // Arrange
        var projectName = "TestProject";
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        project.IsDirty = true;

        // Act
        await _projectService.SaveProjectAsync(project);

        // Assert
        project.IsDirty.Should().BeFalse();
    }

    [Fact]
    public async Task SaveProjectAsync_ShouldPersistProjectName()
    {
        // Arrange
        var projectName = "TestProject";
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        project.Name = "RenamedProject";
        await _projectService.SaveProjectAsync(project);

        // Act - Read back the file
        var json = await File.ReadAllTextAsync(project.FilePath);
        var manifest = JsonSerializer.Deserialize<ProjectManifest>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Assert
        manifest!.Name.Should().Be("RenamedProject");
    }

    [Fact]
    public async Task SaveProjectAsync_ShouldPersistSettings()
    {
        // Arrange
        var projectName = "TestProject";
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        project.Settings.SampleRate = 96000;
        project.Settings.BufferSize = 1024;
        await _projectService.SaveProjectAsync(project);
        var newService = new ProjectService();

        // Act
        var reloadedProject = await newService.OpenProjectAsync(project.FilePath);

        // Assert
        reloadedProject.Settings.SampleRate.Should().Be(96000);
        reloadedProject.Settings.BufferSize.Should().Be(1024);
    }

    [Fact]
    public async Task SaveProjectAsync_ShouldPersistScriptsList()
    {
        // Arrange
        var projectName = "TestProject";
        var project = await _projectService.CreateProjectAsync(projectName, _testDirectory);
        var newScript = _projectService.CreateScript(project, "Helper");
        project.Scripts.Add(newScript);
        await _projectService.SaveScriptAsync(newScript);
        await _projectService.SaveProjectAsync(project);
        var newService = new ProjectService();

        // Act
        var reloadedProject = await newService.OpenProjectAsync(project.FilePath);

        // Assert
        reloadedProject.Scripts.Should().HaveCount(2);
    }

    #endregion

    #region CreateScript Tests

    [Fact]
    public async Task CreateScript_ShouldCreateScriptWithCorrectName()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Helper");

        // Assert
        script.FilePath.Should().EndWith("Helper.me");
    }

    [Fact]
    public async Task CreateScript_ShouldSetCorrectNamespace()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Helper");

        // Assert
        script.Namespace.Should().StartWith(project.Namespace);
        script.Namespace.Should().Contain("Scripts");
    }

    [Fact]
    public async Task CreateScript_ShouldCreateScriptInFolder()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Utility", folder: "Utils");

        // Assert
        script.FilePath.Should().Contain("Utils");
        script.Namespace.Should().Contain("Utils");
    }

    [Fact]
    public async Task CreateScript_ShouldSetDefaultContent()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Helper");

        // Assert
        script.Content.Should().NotBeNullOrEmpty();
        script.Content.Should().Contain("namespace");
    }

    [Fact]
    public async Task CreateScript_ShouldSetIsDirtyToTrue()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Helper");

        // Assert
        script.IsDirty.Should().BeTrue();
    }

    [Fact]
    public async Task CreateScript_ShouldNotBeEntryPoint()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var script = _projectService.CreateScript(project, "Helper");

        // Assert
        script.IsEntryPoint.Should().BeFalse();
    }

    #endregion

    #region ImportAudioAsync Tests

    [Fact]
    public async Task ImportAudioAsync_ShouldCopyFileToProjectDirectory()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var sourceFile = Path.Combine(_testDirectory, "test.wav");
        await File.WriteAllTextAsync(sourceFile, "dummy audio content");

        // Act
        var asset = await _projectService.ImportAudioAsync(project, sourceFile, "TestSound");

        // Assert
        File.Exists(asset.FilePath).Should().BeTrue();
        asset.FilePath.Should().Contain(project.ProjectDirectory);
    }

    [Fact]
    public async Task ImportAudioAsync_ShouldSetCorrectAlias()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var sourceFile = Path.Combine(_testDirectory, "test.wav");
        await File.WriteAllTextAsync(sourceFile, "dummy audio content");

        // Act
        var asset = await _projectService.ImportAudioAsync(project, sourceFile, "TestSound");

        // Assert
        asset.Alias.Should().Be("TestSound");
    }

    [Fact]
    public async Task ImportAudioAsync_ShouldAddToProjectAudioAssets()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var sourceFile = Path.Combine(_testDirectory, "test.wav");
        await File.WriteAllTextAsync(sourceFile, "dummy audio content");

        // Act
        await _projectService.ImportAudioAsync(project, sourceFile, "TestSound");

        // Assert
        project.AudioAssets.Should().HaveCount(1);
    }

    [Fact]
    public async Task ImportAudioAsync_ShouldUseSpecifiedCategory()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var sourceFile = Path.Combine(_testDirectory, "test.wav");
        await File.WriteAllTextAsync(sourceFile, "dummy audio content");

        // Act
        var asset = await _projectService.ImportAudioAsync(project, sourceFile, "TestSound", "Drums");

        // Assert
        asset.Category.Should().Be("Drums");
    }

    [Fact]
    public async Task ImportAudioAsync_ShouldCreateCategoryDirectory()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var sourceFile = Path.Combine(_testDirectory, "test.wav");
        await File.WriteAllTextAsync(sourceFile, "dummy audio content");

        // Act
        var asset = await _projectService.ImportAudioAsync(project, sourceFile, "TestSound", "Drums");

        // Assert
        var categoryDir = Path.GetDirectoryName(asset.FilePath);
        Directory.Exists(categoryDir).Should().BeTrue();
        categoryDir.Should().EndWith("Drums");
    }

    #endregion

    #region CloseProjectAsync Tests

    [Fact]
    public async Task CloseProjectAsync_ShouldSetCurrentProjectToNull()
    {
        // Arrange
        await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        await _projectService.CloseProjectAsync();

        // Assert
        _projectService.CurrentProject.Should().BeNull();
    }

    [Fact]
    public async Task CloseProjectAsync_ShouldRaiseProjectClosedEvent()
    {
        // Arrange
        await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var eventRaised = false;
        _projectService.ProjectClosed += (_, _) => eventRaised = true;

        // Act
        await _projectService.CloseProjectAsync();

        // Assert
        eventRaised.Should().BeTrue();
    }

    [Fact]
    public async Task CloseProjectAsync_ShouldSaveIfDirty()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        project.IsDirty = true;
        project.Name = "ModifiedName";

        // Act
        await _projectService.CloseProjectAsync();

        // Verify by reopening
        var newService = new ProjectService();
        var reopened = await newService.OpenProjectAsync(project.FilePath);

        // Assert
        reopened.Name.Should().Be("ModifiedName");
    }

    #endregion

    #region DeleteScriptAsync Tests

    [Fact]
    public async Task DeleteScriptAsync_ShouldRemoveFileFromDisk()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var script = _projectService.CreateScript(project, "ToDelete");
        project.Scripts.Add(script);
        await _projectService.SaveScriptAsync(script);
        var filePath = script.FilePath;

        // Act
        await _projectService.DeleteScriptAsync(script);

        // Assert
        File.Exists(filePath).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteScriptAsync_ShouldRemoveFromProjectScripts()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var script = _projectService.CreateScript(project, "ToDelete");
        project.Scripts.Add(script);
        script.Project = project;
        await _projectService.SaveScriptAsync(script);
        var initialCount = project.Scripts.Count;

        // Act
        await _projectService.DeleteScriptAsync(script);

        // Assert
        project.Scripts.Should().HaveCount(initialCount - 1);
    }

    #endregion

    #region File Serialization Tests

    [Fact]
    public async Task ProjectFile_ShouldBeValidJson()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var json = await File.ReadAllTextAsync(project.FilePath);
        var isValidJson = true;
        try
        {
            JsonDocument.Parse(json);
        }
        catch
        {
            isValidJson = false;
        }

        // Assert
        isValidJson.Should().BeTrue();
    }

    [Fact]
    public async Task ProjectFile_ShouldContainSchemaField()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var json = await File.ReadAllTextAsync(project.FilePath);
        var doc = JsonDocument.Parse(json);

        // Assert
        doc.RootElement.TryGetProperty("$schema", out _).Should().BeTrue();
    }

    [Fact]
    public async Task ProjectFile_ShouldBeHumanReadable()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);

        // Act
        var json = await File.ReadAllTextAsync(project.FilePath);

        // Assert - Check that it's indented (contains newlines and spaces)
        json.Should().Contain("\n");
        json.Should().Contain("  ");
    }

    #endregion

    #region Reference Management Tests

    [Fact]
    public async Task AddReferenceAsync_ShouldAddToProjectReferences()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var reference = new ProjectReference
        {
            Type = "assembly",
            Path = "SomeLibrary.dll",
            Alias = "SomeLib"
        };

        // Act
        await _projectService.AddReferenceAsync(project, reference);

        // Assert
        project.References.Should().Contain(reference);
    }

    [Fact]
    public async Task RemoveReferenceAsync_ShouldRemoveFromProjectReferences()
    {
        // Arrange
        var project = await _projectService.CreateProjectAsync("TestProject", _testDirectory);
        var reference = new ProjectReference
        {
            Type = "assembly",
            Path = "SomeLibrary.dll",
            Alias = "SomeLib"
        };
        await _projectService.AddReferenceAsync(project, reference);

        // Act
        await _projectService.RemoveReferenceAsync(project, reference);

        // Assert
        project.References.Should().NotContain(reference);
    }

    #endregion
}
