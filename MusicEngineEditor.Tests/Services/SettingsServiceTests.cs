using System.IO;
using FluentAssertions;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;
using Xunit;

namespace MusicEngineEditor.Tests.Services;

/// <summary>
/// Unit tests for SettingsService
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private readonly SettingsService _settingsService;
    private readonly string _originalSettingsPath;
    private readonly string _testSettingsFolder;

    public SettingsServiceTests()
    {
        _settingsService = new SettingsService();

        // Store original settings location for cleanup
        _testSettingsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MusicEngineEditor");
        _originalSettingsPath = Path.Combine(_testSettingsFolder, "settings.json");

        // Backup existing settings if present
        if (File.Exists(_originalSettingsPath))
        {
            var backupPath = _originalSettingsPath + ".backup";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            File.Move(_originalSettingsPath, backupPath);
        }
    }

    public void Dispose()
    {
        // Restore original settings if backup exists
        var backupPath = _originalSettingsPath + ".backup";
        if (File.Exists(backupPath))
        {
            if (File.Exists(_originalSettingsPath))
                File.Delete(_originalSettingsPath);
            File.Move(backupPath, _originalSettingsPath);
        }
    }

    #region LoadSettingsAsync Tests

    [Fact]
    public async Task LoadSettingsAsync_ShouldReturnSettings()
    {
        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldReturnDefaultsWhenNoFileExists()
    {
        // Arrange - Ensure no settings file exists
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Audio.Should().NotBeNull();
        settings.Midi.Should().NotBeNull();
        settings.Editor.Should().NotBeNull();
        settings.Paths.Should().NotBeNull();
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldCreateSettingsFileIfNotExists()
    {
        // Arrange - Ensure no settings file exists
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        await _settingsService.LoadSettingsAsync();

        // Assert
        File.Exists(_originalSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldSetSettingsProperty()
    {
        // Act
        var loaded = await _settingsService.LoadSettingsAsync();

        // Assert
        _settingsService.Settings.Should().Be(loaded);
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldLoadExistingSettings()
    {
        // Arrange
        var customSettings = new AppSettings
        {
            Audio = new AudioSettings { SampleRate = 96000 },
            Editor = new EditorSettings { FontSize = 18 }
        };
        await _settingsService.SaveSettingsAsync(customSettings);

        // Create new service to simulate fresh load
        var newService = new SettingsService();

        // Act
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Audio.SampleRate.Should().Be(96000);
        loaded.Editor.FontSize.Should().Be(18);
    }

    [Fact]
    public async Task LoadSettingsAsync_ShouldHandleCorruptedFile()
    {
        // Arrange - Write invalid JSON
        Directory.CreateDirectory(_testSettingsFolder);
        await File.WriteAllTextAsync(_originalSettingsPath, "{ invalid json }}}");

        var newService = new SettingsService();

        // Act
        var settings = await newService.LoadSettingsAsync();

        // Assert - Should return defaults rather than crash
        settings.Should().NotBeNull();
    }

    #endregion

    #region SaveSettingsAsync Tests

    [Fact]
    public async Task SaveSettingsAsync_ShouldCreateSettingsFile()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        var settings = new AppSettings();

        // Act
        await _settingsService.SaveSettingsAsync(settings);

        // Assert
        File.Exists(_originalSettingsPath).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistAudioSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            Audio = new AudioSettings
            {
                OutputDevice = "TestDevice",
                SampleRate = 48000,
                BufferSize = 1024
            }
        };

        // Act
        await _settingsService.SaveSettingsAsync(settings);
        var newService = new SettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Audio.OutputDevice.Should().Be("TestDevice");
        loaded.Audio.SampleRate.Should().Be(48000);
        loaded.Audio.BufferSize.Should().Be(1024);
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistMidiSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            Midi = new MidiSettings
            {
                InputDevice = "TestMidiIn",
                OutputDevice = "TestMidiOut",
                EnableClockSync = true,
                EnableMidiThrough = true
            }
        };

        // Act
        await _settingsService.SaveSettingsAsync(settings);
        var newService = new SettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Midi.InputDevice.Should().Be("TestMidiIn");
        loaded.Midi.OutputDevice.Should().Be("TestMidiOut");
        loaded.Midi.EnableClockSync.Should().BeTrue();
        loaded.Midi.EnableMidiThrough.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistEditorSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            Editor = new EditorSettings
            {
                Theme = "Light",
                FontSize = 16,
                AutoSaveInterval = 10,
                ShowLineNumbers = false,
                HighlightCurrentLine = false,
                WordWrap = true
            }
        };

        // Act
        await _settingsService.SaveSettingsAsync(settings);
        var newService = new SettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Editor.Theme.Should().Be("Light");
        loaded.Editor.FontSize.Should().Be(16);
        loaded.Editor.AutoSaveInterval.Should().Be(10);
        loaded.Editor.ShowLineNumbers.Should().BeFalse();
        loaded.Editor.HighlightCurrentLine.Should().BeFalse();
        loaded.Editor.WordWrap.Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldPersistPathSettings()
    {
        // Arrange
        var settings = new AppSettings
        {
            Paths = new PathSettings
            {
                VstPluginPaths = new List<string> { @"C:\VST", @"D:\Plugins" },
                SampleDirectories = new List<string> { @"C:\Samples" },
                DefaultProjectLocation = @"C:\Projects"
            }
        };

        // Act
        await _settingsService.SaveSettingsAsync(settings);
        var newService = new SettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Paths.VstPluginPaths.Should().Contain(@"C:\VST");
        loaded.Paths.VstPluginPaths.Should().Contain(@"D:\Plugins");
        loaded.Paths.SampleDirectories.Should().Contain(@"C:\Samples");
        loaded.Paths.DefaultProjectLocation.Should().Be(@"C:\Projects");
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldUpdateSettingsProperty()
    {
        // Arrange
        var settings = new AppSettings
        {
            Editor = new EditorSettings { FontSize = 20 }
        };

        // Act
        await _settingsService.SaveSettingsAsync(settings);

        // Assert
        _settingsService.Settings.Editor.FontSize.Should().Be(20);
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldCreateDirectoryIfNotExists()
    {
        // Arrange
        if (Directory.Exists(_testSettingsFolder))
            Directory.Delete(_testSettingsFolder, recursive: true);

        var settings = new AppSettings();

        // Act
        await _settingsService.SaveSettingsAsync(settings);

        // Assert
        Directory.Exists(_testSettingsFolder).Should().BeTrue();
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldOverwriteExistingFile()
    {
        // Arrange
        var initialSettings = new AppSettings
        {
            Editor = new EditorSettings { FontSize = 12 }
        };
        await _settingsService.SaveSettingsAsync(initialSettings);

        var updatedSettings = new AppSettings
        {
            Editor = new EditorSettings { FontSize = 24 }
        };

        // Act
        await _settingsService.SaveSettingsAsync(updatedSettings);
        var newService = new SettingsService();
        var loaded = await newService.LoadSettingsAsync();

        // Assert
        loaded.Editor.FontSize.Should().Be(24);
    }

    #endregion

    #region ResetToDefaults Tests

    [Fact]
    public void ResetToDefaults_ShouldReturnDefaultSettings()
    {
        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Should().NotBeNull();
    }

    [Fact]
    public void ResetToDefaults_ShouldSetDefaultAudioSettings()
    {
        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Audio.OutputDevice.Should().Be("Default");
        defaults.Audio.SampleRate.Should().Be(44100);
        defaults.Audio.BufferSize.Should().Be(512);
    }

    [Fact]
    public void ResetToDefaults_ShouldSetDefaultMidiSettings()
    {
        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Midi.InputDevice.Should().Be("None");
        defaults.Midi.OutputDevice.Should().Be("None");
        defaults.Midi.EnableClockSync.Should().BeFalse();
        defaults.Midi.EnableMidiThrough.Should().BeFalse();
    }

    [Fact]
    public void ResetToDefaults_ShouldSetDefaultEditorSettings()
    {
        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Editor.Theme.Should().Be("Dark");
        defaults.Editor.FontSize.Should().Be(14);
        defaults.Editor.AutoSaveInterval.Should().Be(5);
        defaults.Editor.ShowLineNumbers.Should().BeTrue();
        defaults.Editor.HighlightCurrentLine.Should().BeTrue();
        defaults.Editor.WordWrap.Should().BeFalse();
    }

    [Fact]
    public void ResetToDefaults_ShouldSetDefaultPathSettings()
    {
        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Paths.VstPluginPaths.Should().NotBeEmpty();
        defaults.Paths.DefaultProjectLocation.Should().Contain("MusicEngineProjects");
    }

    [Fact]
    public void ResetToDefaults_ShouldUpdateSettingsProperty()
    {
        // Arrange - First set custom settings
        _settingsService.Settings.Editor.FontSize = 24;

        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        _settingsService.Settings.Should().Be(defaults);
        _settingsService.Settings.Editor.FontSize.Should().Be(14);
    }

    [Fact]
    public async Task ResetToDefaults_AfterLoadingCustomSettings_ShouldReturnDefaults()
    {
        // Arrange
        var customSettings = new AppSettings
        {
            Editor = new EditorSettings { FontSize = 30 },
            Audio = new AudioSettings { SampleRate = 192000 }
        };
        await _settingsService.SaveSettingsAsync(customSettings);
        await _settingsService.LoadSettingsAsync();

        // Act
        var defaults = _settingsService.ResetToDefaults();

        // Assert
        defaults.Editor.FontSize.Should().Be(14);
        defaults.Audio.SampleRate.Should().Be(44100);
    }

    #endregion

    #region Default Values Tests

    [Fact]
    public async Task DefaultSettings_AudioSampleRate_ShouldBe44100()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Audio.SampleRate.Should().Be(44100);
    }

    [Fact]
    public async Task DefaultSettings_AudioBufferSize_ShouldBe512()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Audio.BufferSize.Should().Be(512);
    }

    [Fact]
    public async Task DefaultSettings_EditorTheme_ShouldBeDark()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.Theme.Should().Be("Dark");
    }

    [Fact]
    public async Task DefaultSettings_EditorFontSize_ShouldBe14()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.FontSize.Should().Be(14);
    }

    [Fact]
    public async Task DefaultSettings_ShowLineNumbers_ShouldBeTrue()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.ShowLineNumbers.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultSettings_HighlightCurrentLine_ShouldBeTrue()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.HighlightCurrentLine.Should().BeTrue();
    }

    [Fact]
    public async Task DefaultSettings_WordWrap_ShouldBeFalse()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.WordWrap.Should().BeFalse();
    }

    [Fact]
    public async Task DefaultSettings_AutoSaveInterval_ShouldBe5()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Editor.AutoSaveInterval.Should().Be(5);
    }

    [Fact]
    public async Task DefaultSettings_VstPluginPaths_ShouldContainStandardPaths()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Paths.VstPluginPaths.Should().Contain(@"C:\Program Files\Common Files\VST3");
    }

    [Fact]
    public async Task DefaultSettings_DefaultProjectLocation_ShouldBeInDocuments()
    {
        // Arrange
        if (File.Exists(_originalSettingsPath))
            File.Delete(_originalSettingsPath);

        // Act
        var settings = await _settingsService.LoadSettingsAsync();

        // Assert
        settings.Paths.DefaultProjectLocation.Should().Contain(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
    }

    #endregion

    #region GetDevices Tests

    [Fact]
    public void GetAudioOutputDevices_ShouldReturnAtLeastDefault()
    {
        // Act
        var devices = _settingsService.GetAudioOutputDevices();

        // Assert
        devices.Should().NotBeEmpty();
        devices.Should().Contain("Default");
    }

    [Fact]
    public void GetMidiInputDevices_ShouldReturnAtLeastNone()
    {
        // Act
        var devices = _settingsService.GetMidiInputDevices();

        // Assert
        devices.Should().NotBeEmpty();
        devices.Should().Contain("None");
    }

    [Fact]
    public void GetMidiOutputDevices_ShouldReturnAtLeastNone()
    {
        // Act
        var devices = _settingsService.GetMidiOutputDevices();

        // Assert
        devices.Should().NotBeEmpty();
        devices.Should().Contain("None");
    }

    #endregion

    #region Static Values Tests

    [Fact]
    public void AudioSettings_AvailableSampleRates_ShouldContainCommonRates()
    {
        // Assert
        AudioSettings.AvailableSampleRates.Should().Contain(44100);
        AudioSettings.AvailableSampleRates.Should().Contain(48000);
        AudioSettings.AvailableSampleRates.Should().Contain(96000);
    }

    [Fact]
    public void AudioSettings_AvailableBufferSizes_ShouldContainCommonSizes()
    {
        // Assert
        AudioSettings.AvailableBufferSizes.Should().Contain(128);
        AudioSettings.AvailableBufferSizes.Should().Contain(256);
        AudioSettings.AvailableBufferSizes.Should().Contain(512);
        AudioSettings.AvailableBufferSizes.Should().Contain(1024);
    }

    [Fact]
    public void EditorSettings_AvailableThemes_ShouldContainDarkAndLight()
    {
        // Assert
        EditorSettings.AvailableThemes.Should().Contain("Dark");
        EditorSettings.AvailableThemes.Should().Contain("Light");
    }

    [Fact]
    public void EditorSettings_AvailableFontSizes_ShouldContainReasonableRange()
    {
        // Assert
        EditorSettings.AvailableFontSizes.Should().Contain(12);
        EditorSettings.AvailableFontSizes.Should().Contain(14);
        EditorSettings.AvailableFontSizes.Should().Contain(16);
    }

    #endregion
}
