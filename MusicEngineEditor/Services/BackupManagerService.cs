using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for managing project backups.
/// Supports automatic backups, versioned backups, and restoration.
/// </summary>
public sealed class BackupManagerService : IDisposable, INotifyPropertyChanged
{
    private static readonly Lazy<BackupManagerService> _instance = new(
        () => new BackupManagerService(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static BackupManagerService Instance => _instance.Value;

    private readonly System.Timers.Timer _autoBackupTimer;
    private readonly object _lock = new();
    private IProjectService? _projectService;
    private string _backupDirectory;
    private bool _isEnabled = true;
    private int _intervalMinutes = 5;
    private int _maxBackupsPerProject = 10;
    private int _maxBackupAgeDays = 30;
    private bool _backupBeforeMajorOperations = true;
    private bool _isDisposed;
    private bool _isBackingUp;
    private DateTime? _lastBackup;
    private readonly BackupSettings _settings;
    private readonly string _settingsPath;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Occurs when a backup completes successfully.
    /// </summary>
    public event EventHandler<BackupEventArgs>? BackupCompleted;

    /// <summary>
    /// Occurs when a backup fails.
    /// </summary>
    public event EventHandler<Exception>? BackupFailed;

    /// <summary>
    /// Occurs when a restore completes successfully.
    /// </summary>
    public event EventHandler<BackupInfo>? RestoreCompleted;

    /// <summary>
    /// Gets or sets whether automatic backups are enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                NotifyPropertyChanged();
                UpdateTimer();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the backup interval in minutes.
    /// Valid values: 1, 5, 10, 30.
    /// </summary>
    public int IntervalMinutes
    {
        get => _intervalMinutes;
        set
        {
            var validIntervals = new[] { 1, 5, 10, 30 };
            var closest = validIntervals.OrderBy(x => Math.Abs(x - value)).First();

            if (_intervalMinutes != closest)
            {
                _intervalMinutes = closest;
                NotifyPropertyChanged();
                UpdateTimer();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum number of backups to keep per project.
    /// </summary>
    public int MaxBackupsPerProject
    {
        get => _maxBackupsPerProject;
        set
        {
            var clamped = Math.Clamp(value, 1, 100);
            if (_maxBackupsPerProject != clamped)
            {
                _maxBackupsPerProject = clamped;
                NotifyPropertyChanged();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the maximum age of backups in days.
    /// </summary>
    public int MaxBackupAgeDays
    {
        get => _maxBackupAgeDays;
        set
        {
            var clamped = Math.Clamp(value, 1, 365);
            if (_maxBackupAgeDays != clamped)
            {
                _maxBackupAgeDays = clamped;
                NotifyPropertyChanged();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to create a backup before major operations.
    /// </summary>
    public bool BackupBeforeMajorOperations
    {
        get => _backupBeforeMajorOperations;
        set
        {
            if (_backupBeforeMajorOperations != value)
            {
                _backupBeforeMajorOperations = value;
                NotifyPropertyChanged();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets or sets the backup directory.
    /// </summary>
    public string BackupDirectory
    {
        get => _backupDirectory;
        set
        {
            if (_backupDirectory != value && !string.IsNullOrWhiteSpace(value))
            {
                _backupDirectory = value;
                Directory.CreateDirectory(_backupDirectory);
                NotifyPropertyChanged();
                SaveSettings();
            }
        }
    }

    /// <summary>
    /// Gets the timestamp of the last successful backup.
    /// </summary>
    public DateTime? LastBackup
    {
        get => _lastBackup;
        private set
        {
            _lastBackup = value;
            NotifyPropertyChanged();
            NotifyPropertyChanged(nameof(LastBackupDisplay));
        }
    }

    /// <summary>
    /// Gets a display string for the last backup time.
    /// </summary>
    public string LastBackupDisplay => _lastBackup.HasValue
        ? $"Last backup: {_lastBackup.Value:g}"
        : "No backups yet";

    /// <summary>
    /// Gets whether a backup is currently in progress.
    /// </summary>
    public bool IsBackingUp => _isBackingUp;

    private static JsonSerializerOptions JsonOptions => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private BackupManagerService()
    {
        _settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngineEditor", "backup_settings.json");

        _backupDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MusicEngineEditor", "Backups");

        _settings = LoadSettings();
        ApplySettings();

        Directory.CreateDirectory(_backupDirectory);

        _autoBackupTimer = new System.Timers.Timer(_intervalMinutes * 60 * 1000);
        _autoBackupTimer.Elapsed += OnAutoBackupTimerElapsed;
        _autoBackupTimer.AutoReset = true;
    }

    /// <summary>
    /// Initializes the backup service with a project service.
    /// </summary>
    /// <param name="projectService">The project service.</param>
    public void Initialize(IProjectService projectService)
    {
        _projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));
        Start();
    }

    /// <summary>
    /// Starts the automatic backup timer.
    /// </summary>
    public void Start()
    {
        if (_isEnabled && !_isDisposed)
        {
            _autoBackupTimer.Start();
        }
    }

    /// <summary>
    /// Stops the automatic backup timer.
    /// </summary>
    public void Stop()
    {
        _autoBackupTimer.Stop();
    }

    /// <summary>
    /// Creates a backup of the current project immediately.
    /// </summary>
    /// <param name="label">Optional label for the backup.</param>
    /// <returns>The backup info, or null if backup failed.</returns>
    public async Task<BackupInfo?> BackupNowAsync(string? label = null)
    {
        if (_isDisposed || _isBackingUp)
            return null;

        var project = _projectService?.CurrentProject;
        if (project == null)
            return null;

        return await PerformBackupAsync(project, label);
    }

    /// <summary>
    /// Creates a backup before a major operation.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <returns>The backup info, or null if backup is disabled or failed.</returns>
    public async Task<BackupInfo?> BackupBeforeOperationAsync(string operationName)
    {
        if (!_backupBeforeMajorOperations)
            return null;

        return await BackupNowAsync($"Before {operationName}");
    }

    /// <summary>
    /// Gets all backup files for a project.
    /// </summary>
    /// <param name="projectGuid">The project GUID.</param>
    /// <returns>List of backup information.</returns>
    public List<BackupInfo> GetBackupsForProject(Guid projectGuid)
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(_backupDirectory))
            return backups;

        var pattern = $"{projectGuid}_*.backup";
        var files = Directory.GetFiles(_backupDirectory, pattern)
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (var file in files)
        {
            try
            {
                var info = ReadBackupInfo(file);
                if (info != null)
                {
                    backups.Add(info);
                }
            }
            catch
            {
                // Skip corrupted backup files
            }
        }

        return backups;
    }

    /// <summary>
    /// Gets all available backups.
    /// </summary>
    /// <returns>List of all backup information.</returns>
    public List<BackupInfo> GetAllBackups()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(_backupDirectory))
            return backups;

        var files = Directory.GetFiles(_backupDirectory, "*.backup")
            .OrderByDescending(File.GetLastWriteTimeUtc);

        foreach (var file in files)
        {
            try
            {
                var info = ReadBackupInfo(file);
                if (info != null)
                {
                    backups.Add(info);
                }
            }
            catch
            {
                // Skip corrupted backup files
            }
        }

        return backups;
    }

    /// <summary>
    /// Restores a project from a backup.
    /// </summary>
    /// <param name="backupInfo">The backup to restore.</param>
    /// <returns>The restored project, or null if restoration failed.</returns>
    public async Task<MusicProject?> RestoreFromBackupAsync(BackupInfo backupInfo)
    {
        if (!File.Exists(backupInfo.FilePath))
            return null;

        try
        {
            var json = await File.ReadAllTextAsync(backupInfo.FilePath);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);

            if (manifest == null)
                return null;

            var project = new MusicProject
            {
                Name = manifest.ProjectName,
                Guid = Guid.TryParse(manifest.ProjectGuid, out var guid) ? guid : Guid.NewGuid(),
                Namespace = manifest.Namespace ?? SanitizeNamespace(manifest.ProjectName),
                FilePath = manifest.OriginalPath,
                Created = DateTime.TryParse(manifest.Created, out var created) ? created : DateTime.UtcNow,
                Modified = DateTime.UtcNow,
                MusicEngineVersion = manifest.MusicEngineVersion ?? "1.0.0",
                Settings = manifest.Settings ?? new ProjectSettings(),
                IsDirty = true
            };

            // Restore scripts
            if (manifest.Scripts != null)
            {
                foreach (var scriptData in manifest.Scripts)
                {
                    var script = new MusicScript
                    {
                        FilePath = scriptData.FilePath,
                        Namespace = scriptData.Namespace,
                        IsEntryPoint = scriptData.IsEntryPoint,
                        Content = scriptData.Content,
                        Project = project
                    };
                    project.Scripts.Add(script);
                }
            }

            // Restore audio assets
            if (manifest.AudioAssets != null)
            {
                foreach (var assetData in manifest.AudioAssets)
                {
                    project.AudioAssets.Add(new AudioAsset
                    {
                        FilePath = assetData.FilePath,
                        Alias = assetData.Alias,
                        Category = assetData.Category
                    });
                }
            }

            // Restore references
            if (manifest.References != null)
            {
                foreach (var refData in manifest.References)
                {
                    project.References.Add(new ProjectReference
                    {
                        Type = refData.Type,
                        Path = refData.Path,
                        Alias = refData.Alias,
                        Version = refData.Version
                    });
                }
            }

            RestoreCompleted?.Invoke(this, backupInfo);
            return project;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Deletes a specific backup.
    /// </summary>
    /// <param name="backupInfo">The backup to delete.</param>
    public void DeleteBackup(BackupInfo backupInfo)
    {
        if (File.Exists(backupInfo.FilePath))
        {
            try
            {
                File.Delete(backupInfo.FilePath);
            }
            catch
            {
                // Ignore deletion errors
            }
        }
    }

    /// <summary>
    /// Cleans up old backups for a project.
    /// </summary>
    /// <param name="projectGuid">The project GUID.</param>
    public void CleanupBackups(Guid projectGuid)
    {
        var backups = GetBackupsForProject(projectGuid);

        // Delete backups exceeding the maximum count
        foreach (var backup in backups.Skip(_maxBackupsPerProject))
        {
            DeleteBackup(backup);
        }

        // Delete old backups
        var cutoffDate = DateTime.UtcNow.AddDays(-_maxBackupAgeDays);
        foreach (var backup in backups.Where(b => b.BackupTime < cutoffDate))
        {
            DeleteBackup(backup);
        }
    }

    /// <summary>
    /// Cleans up all old backups.
    /// </summary>
    public void CleanupAllOldBackups()
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-_maxBackupAgeDays);
        var backups = GetAllBackups();

        foreach (var backup in backups.Where(b => b.BackupTime < cutoffDate))
        {
            DeleteBackup(backup);
        }
    }

    /// <summary>
    /// Gets the total size of all backups.
    /// </summary>
    /// <returns>Total size in bytes.</returns>
    public long GetTotalBackupSize()
    {
        if (!Directory.Exists(_backupDirectory))
            return 0;

        return Directory.GetFiles(_backupDirectory, "*.backup")
            .Sum(f => new FileInfo(f).Length);
    }

    /// <summary>
    /// Gets a display string for the total backup size.
    /// </summary>
    public string TotalBackupSizeDisplay
    {
        get
        {
            var bytes = GetTotalBackupSize();
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{bytes / 1024.0:F1} KB";
            if (bytes < 1024 * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} GB";
        }
    }

    private void UpdateTimer()
    {
        lock (_lock)
        {
            _autoBackupTimer.Stop();
            _autoBackupTimer.Interval = _intervalMinutes * 60 * 1000;

            if (_isEnabled && !_isDisposed)
            {
                _autoBackupTimer.Start();
            }
        }
    }

    private async void OnAutoBackupTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        if (_isDisposed || _isBackingUp)
            return;

        var project = _projectService?.CurrentProject;
        if (project == null || !project.IsDirty)
            return;

        await PerformBackupAsync(project, "Auto-backup");
    }

    private async Task<BackupInfo?> PerformBackupAsync(MusicProject project, string? label = null)
    {
        if (_isBackingUp)
            return null;

        lock (_lock)
        {
            if (_isBackingUp)
                return null;
            _isBackingUp = true;
        }

        NotifyPropertyChanged(nameof(IsBackingUp));

        try
        {
            var timestamp = DateTime.UtcNow;
            var version = GetNextVersionNumber(project.Guid);
            var fileName = $"{project.Guid}_{timestamp:yyyyMMdd_HHmmss}_v{version}.backup";
            var filePath = Path.Combine(_backupDirectory, fileName);

            var manifest = new BackupManifest
            {
                Schema = "https://musicengine.dev/schema/backup-1.0.json",
                ProjectName = project.Name,
                ProjectGuid = project.Guid.ToString(),
                Namespace = project.Namespace,
                OriginalPath = project.FilePath,
                Created = project.Created.ToString("o"),
                BackupTime = timestamp.ToString("o"),
                Version = version,
                Label = label,
                MusicEngineVersion = project.MusicEngineVersion,
                Settings = project.Settings,
                Scripts = project.Scripts.Select(s => new BackupScript
                {
                    FilePath = s.FilePath,
                    Namespace = s.Namespace,
                    IsEntryPoint = s.IsEntryPoint,
                    Content = s.Content
                }).ToList(),
                AudioAssets = project.AudioAssets.Select(a => new BackupAudioAsset
                {
                    FilePath = a.FilePath,
                    Alias = a.Alias,
                    Category = a.Category
                }).ToList(),
                References = project.References.Select(r => new BackupReference
                {
                    Type = r.Type,
                    Path = r.Path,
                    Alias = r.Alias,
                    Version = r.Version
                }).ToList()
            };

            var json = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            LastBackup = timestamp;

            // Cleanup old backups
            CleanupBackups(project.Guid);

            var backupInfo = new BackupInfo
            {
                FilePath = filePath,
                ProjectName = project.Name,
                ProjectGuid = project.Guid,
                BackupTime = timestamp,
                Version = version,
                Label = label,
                FileSize = new FileInfo(filePath).Length
            };

            BackupCompleted?.Invoke(this, new BackupEventArgs
            {
                BackupInfo = backupInfo,
                ProjectName = project.Name
            });

            return backupInfo;
        }
        catch (Exception ex)
        {
            BackupFailed?.Invoke(this, ex);
            return null;
        }
        finally
        {
            lock (_lock)
            {
                _isBackingUp = false;
            }
            NotifyPropertyChanged(nameof(IsBackingUp));
        }
    }

    private int GetNextVersionNumber(Guid projectGuid)
    {
        var backups = GetBackupsForProject(projectGuid);
        if (backups.Count == 0)
            return 1;

        return backups.Max(b => b.Version) + 1;
    }

    private BackupInfo? ReadBackupInfo(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(json, JsonOptions);

            if (manifest == null)
                return null;

            return new BackupInfo
            {
                FilePath = filePath,
                ProjectName = manifest.ProjectName,
                ProjectGuid = Guid.TryParse(manifest.ProjectGuid, out var guid) ? guid : Guid.Empty,
                BackupTime = DateTime.TryParse(manifest.BackupTime, out var time) ? time : File.GetLastWriteTime(filePath),
                Version = manifest.Version,
                Label = manifest.Label,
                FileSize = new FileInfo(filePath).Length
            };
        }
        catch
        {
            return null;
        }
    }

    private void ApplySettings()
    {
        _isEnabled = _settings.IsEnabled;
        _intervalMinutes = _settings.IntervalMinutes;
        _maxBackupsPerProject = _settings.MaxBackupsPerProject;
        _maxBackupAgeDays = _settings.MaxBackupAgeDays;
        _backupBeforeMajorOperations = _settings.BackupBeforeMajorOperations;

        if (!string.IsNullOrWhiteSpace(_settings.BackupDirectory))
        {
            _backupDirectory = _settings.BackupDirectory;
        }
    }

    private BackupSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<BackupSettings>(json, JsonOptions) ?? new BackupSettings();
            }
        }
        catch
        {
            // Ignore errors, return default settings
        }

        return new BackupSettings();
    }

    private void SaveSettings()
    {
        try
        {
            _settings.IsEnabled = _isEnabled;
            _settings.IntervalMinutes = _intervalMinutes;
            _settings.MaxBackupsPerProject = _maxBackupsPerProject;
            _settings.MaxBackupAgeDays = _maxBackupAgeDays;
            _settings.BackupBeforeMajorOperations = _backupBeforeMajorOperations;
            _settings.BackupDirectory = _backupDirectory;

            var directory = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }

    private static string SanitizeNamespace(string name)
    {
        var result = new System.Text.StringBuilder();
        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
            {
                result.Append(c);
            }
        }
        return result.Length > 0 ? result.ToString() : "MusicProject";
    }

    private void NotifyPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Disposes the backup service.
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _autoBackupTimer.Stop();
        _autoBackupTimer.Elapsed -= OnAutoBackupTimerElapsed;
        _autoBackupTimer.Dispose();
        SaveSettings();
    }
}

/// <summary>
/// Event arguments for backup completion.
/// </summary>
public class BackupEventArgs : EventArgs
{
    /// <summary>The backup information.</summary>
    public BackupInfo BackupInfo { get; init; } = new();

    /// <summary>The project name.</summary>
    public string ProjectName { get; init; } = "";
}

/// <summary>
/// Information about a backup.
/// </summary>
public class BackupInfo
{
    /// <summary>Path to the backup file.</summary>
    public string FilePath { get; init; } = "";

    /// <summary>Project name.</summary>
    public string ProjectName { get; init; } = "";

    /// <summary>Project GUID.</summary>
    public Guid ProjectGuid { get; init; }

    /// <summary>Backup timestamp.</summary>
    public DateTime BackupTime { get; init; }

    /// <summary>Backup version number.</summary>
    public int Version { get; init; }

    /// <summary>Optional label.</summary>
    public string? Label { get; init; }

    /// <summary>File size in bytes.</summary>
    public long FileSize { get; init; }

    /// <summary>Display string for file size.</summary>
    public string FileSizeDisplay
    {
        get
        {
            if (FileSize < 1024)
                return $"{FileSize} B";
            if (FileSize < 1024 * 1024)
                return $"{FileSize / 1024.0:F1} KB";
            return $"{FileSize / (1024.0 * 1024.0):F1} MB";
        }
    }

    /// <summary>Display string for the backup.</summary>
    public string DisplayText
    {
        get
        {
            var labelPart = !string.IsNullOrEmpty(Label) ? $" - {Label}" : "";
            return $"v{Version} - {BackupTime:g}{labelPart}";
        }
    }
}

/// <summary>
/// Settings for the backup manager.
/// </summary>
internal class BackupSettings
{
    public bool IsEnabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
    public int MaxBackupsPerProject { get; set; } = 10;
    public int MaxBackupAgeDays { get; set; } = 30;
    public bool BackupBeforeMajorOperations { get; set; } = true;
    public string? BackupDirectory { get; set; }
}

/// <summary>
/// Internal manifest structure for backup files.
/// </summary>
internal class BackupManifest
{
    public string Schema { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ProjectGuid { get; set; } = "";
    public string? Namespace { get; set; }
    public string OriginalPath { get; set; } = "";
    public string Created { get; set; } = "";
    public string BackupTime { get; set; } = "";
    public int Version { get; set; }
    public string? Label { get; set; }
    public string? MusicEngineVersion { get; set; }
    public ProjectSettings? Settings { get; set; }
    public List<BackupScript>? Scripts { get; set; }
    public List<BackupAudioAsset>? AudioAssets { get; set; }
    public List<BackupReference>? References { get; set; }
}

internal class BackupScript
{
    public string FilePath { get; set; } = "";
    public string Namespace { get; set; } = "";
    public bool IsEntryPoint { get; set; }
    public string Content { get; set; } = "";
}

internal class BackupAudioAsset
{
    public string FilePath { get; set; } = "";
    public string Alias { get; set; } = "";
    public string Category { get; set; } = "";
}

internal class BackupReference
{
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string Alias { get; set; } = "";
    public string? Version { get; set; }
}
