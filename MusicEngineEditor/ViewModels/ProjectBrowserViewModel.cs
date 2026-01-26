// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for project browser.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MusicEngineEditor.Models;
using MusicEngineEditor.Views.Dialogs;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Project Browser.
/// </summary>
public partial class ProjectBrowserViewModel : ViewModelBase
{
    private static readonly string[] ProjectExtensions = { ".mep", ".json" };
    private string _currentDirectory;

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _projects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _filteredProjects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _recentProjects = new();

    [ObservableProperty]
    private ObservableCollection<ProjectInfo> _favoriteProjects = new();

    [ObservableProperty]
    private ProjectInfo? _selectedProject;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ProjectSortOption _sortOption = ProjectSortOption.DateModified;

    [ObservableProperty]
    private ProjectViewMode _viewMode = ProjectViewMode.List;

    [ObservableProperty]
    private bool _showFavoritesOnly;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _currentPath = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _recentDirectories = new();

    /// <summary>
    /// Event raised when a project should be opened.
    /// </summary>
    public event EventHandler<ProjectInfo>? ProjectOpened;

    /// <summary>
    /// Event raised when a new project should be created.
    /// </summary>
    public event EventHandler? NewProjectRequested;

    /// <summary>
    /// Gets the default projects directory.
    /// </summary>
    public static string DefaultProjectsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MusicEngine", "Projects");

    public ProjectBrowserViewModel()
    {
        _currentDirectory = DefaultProjectsDirectory;
        CurrentPath = _currentDirectory;

        // Ensure default directory exists
        if (!Directory.Exists(_currentDirectory))
        {
            Directory.CreateDirectory(_currentDirectory);
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSortOptionChanged(ProjectSortOption value)
    {
        ApplyFilter();
    }

    partial void OnShowFavoritesOnlyChanged(bool value)
    {
        ApplyFilter();
    }

    /// <summary>
    /// Loads projects from the current directory.
    /// </summary>
    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        IsLoading = true;
        IsBusy = true;
        StatusMessage = "Loading projects...";

        try
        {
            Projects.Clear();

            await Task.Run(() =>
            {
                if (!Directory.Exists(_currentDirectory))
                    return;

                var files = Directory.GetFiles(_currentDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => ProjectExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .ToList();

                foreach (var file in files)
                {
                    var info = ProjectInfo.FromFile(file);
                    Application.Current.Dispatcher.Invoke(() => Projects.Add(info));
                }
            });

            ApplyFilter();
            StatusMessage = $"Found {Projects.Count} projects";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading projects: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            IsBusy = false;
        }
    }

    /// <summary>
    /// Navigates to a directory.
    /// </summary>
    [RelayCommand]
    private async Task NavigateToAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            StatusMessage = "Directory does not exist";
            return;
        }

        _currentDirectory = path;
        CurrentPath = path;

        // Add to recent directories
        if (!RecentDirectories.Contains(path))
        {
            RecentDirectories.Insert(0, path);
            if (RecentDirectories.Count > 10)
            {
                RecentDirectories.RemoveAt(RecentDirectories.Count - 1);
            }
        }

        await LoadProjectsAsync();
    }

    /// <summary>
    /// Opens a folder browser dialog.
    /// </summary>
    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select Projects Folder",
            SelectedPath = _currentDirectory
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            await NavigateToAsync(dialog.SelectedPath);
        }
    }

    /// <summary>
    /// Opens the selected project.
    /// </summary>
    [RelayCommand]
    private void OpenProject()
    {
        if (SelectedProject != null)
        {
            ProjectOpened?.Invoke(this, SelectedProject);
        }
    }

    /// <summary>
    /// Opens a project by double-click.
    /// </summary>
    public void OpenProject(ProjectInfo project)
    {
        ProjectOpened?.Invoke(this, project);
    }

    /// <summary>
    /// Creates a new project.
    /// </summary>
    [RelayCommand]
    private void NewProject()
    {
        NewProjectRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Toggles favorite status for the selected project.
    /// </summary>
    [RelayCommand]
    private void ToggleFavorite()
    {
        if (SelectedProject != null)
        {
            SelectedProject.IsFavorite = !SelectedProject.IsFavorite;
            UpdateFavorites();
            ApplyFilter();
        }
    }

    /// <summary>
    /// Deletes the selected project.
    /// </summary>
    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        if (SelectedProject == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{SelectedProject.Name}'?\n\nThis action cannot be undone.",
            "Delete Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                if (File.Exists(SelectedProject.FilePath))
                {
                    File.Delete(SelectedProject.FilePath);
                }

                Projects.Remove(SelectedProject);
                FilteredProjects.Remove(SelectedProject);
                SelectedProject = null;
                StatusMessage = "Project deleted";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete project: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    /// <summary>
    /// Opens the project folder in Explorer.
    /// </summary>
    [RelayCommand]
    private void OpenInExplorer()
    {
        if (SelectedProject != null && Directory.Exists(SelectedProject.Directory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{SelectedProject.FilePath}\"",
                UseShellExecute = true
            });
        }
        else if (Directory.Exists(_currentDirectory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _currentDirectory,
                UseShellExecute = true
            });
        }
    }

    /// <summary>
    /// Duplicates the selected project.
    /// </summary>
    [RelayCommand]
    private async Task DuplicateProjectAsync()
    {
        if (SelectedProject == null || !File.Exists(SelectedProject.FilePath))
            return;

        try
        {
            var dir = Path.GetDirectoryName(SelectedProject.FilePath) ?? _currentDirectory;
            var name = Path.GetFileNameWithoutExtension(SelectedProject.FilePath);
            var ext = Path.GetExtension(SelectedProject.FilePath);

            // Find a unique name
            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(dir, $"{name} ({counter}){ext}");
                counter++;
            } while (File.Exists(newPath));

            File.Copy(SelectedProject.FilePath, newPath);

            // Reload projects
            await LoadProjectsAsync();
            StatusMessage = "Project duplicated";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to duplicate project: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Refreshes the project list.
    /// </summary>
    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadProjectsAsync();
    }

    /// <summary>
    /// Exports the selected project to a specified location.
    /// </summary>
    [RelayCommand]
    private async Task ExportProjectAsync()
    {
        if (SelectedProject == null || !File.Exists(SelectedProject.FilePath))
            return;

        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Project",
                FileName = Path.GetFileName(SelectedProject.FilePath),
                Filter = "MusicEngine Project (*.mep)|*.mep|JSON Project (*.json)|*.json|ZIP Archive (*.zip)|*.zip|All Files (*.*)|*.*",
                DefaultExt = Path.GetExtension(SelectedProject.FilePath),
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };

            if (saveDialog.ShowDialog() == true)
            {
                IsLoading = true;
                StatusMessage = "Exporting project...";

                await Task.Run(() =>
                {
                    if (saveDialog.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        // Export as ZIP archive
                        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                        Directory.CreateDirectory(tempDir);

                        try
                        {
                            // Copy the project file
                            var destFile = Path.Combine(tempDir, Path.GetFileName(SelectedProject.FilePath));
                            File.Copy(SelectedProject.FilePath, destFile);

                            // Copy associated files (same directory, same base name)
                            var sourceDir = Path.GetDirectoryName(SelectedProject.FilePath);
                            var baseName = Path.GetFileNameWithoutExtension(SelectedProject.FilePath);
                            if (sourceDir != null)
                            {
                                var associatedFiles = Directory.GetFiles(sourceDir)
                                    .Where(f => Path.GetFileNameWithoutExtension(f).StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                                             && f != SelectedProject.FilePath);

                                foreach (var file in associatedFiles)
                                {
                                    File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
                                }

                                // Copy subdirectory if exists (e.g., audio files)
                                var subDir = Path.Combine(sourceDir, baseName);
                                if (Directory.Exists(subDir))
                                {
                                    CopyDirectory(subDir, Path.Combine(tempDir, baseName));
                                }
                            }

                            // Create ZIP
                            if (File.Exists(saveDialog.FileName))
                                File.Delete(saveDialog.FileName);

                            ZipFile.CreateFromDirectory(tempDir, saveDialog.FileName);
                        }
                        finally
                        {
                            // Cleanup temp directory
                            if (Directory.Exists(tempDir))
                                Directory.Delete(tempDir, true);
                        }
                    }
                    else
                    {
                        // Export as copy
                        File.Copy(SelectedProject.FilePath, saveDialog.FileName, overwrite: true);
                    }
                });

                StatusMessage = "Project exported successfully";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to export project: {ex.Message}", "Export Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Export failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Renames the selected project.
    /// </summary>
    [RelayCommand]
    private async Task RenameProjectAsync()
    {
        if (SelectedProject == null || !File.Exists(SelectedProject.FilePath))
            return;

        // Show input dialog for new name
        var newName = InputDialog.Show(
            "Enter new project name:",
            "Rename Project",
            SelectedProject.Name,
            Application.Current.MainWindow);

        if (!string.IsNullOrWhiteSpace(newName))
        {
            newName = newName.Trim();

            // Validate new name
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                MessageBox.Show("The name contains invalid characters.", "Invalid Name",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var dir = Path.GetDirectoryName(SelectedProject.FilePath) ?? _currentDirectory;
                var ext = Path.GetExtension(SelectedProject.FilePath);
                var newPath = Path.Combine(dir, newName + ext);

                if (File.Exists(newPath))
                {
                    MessageBox.Show($"A project with the name '{newName}' already exists.", "Name Conflict",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                IsLoading = true;
                StatusMessage = "Renaming project...";

                await Task.Run(() =>
                {
                    // Rename the file
                    File.Move(SelectedProject.FilePath, newPath);

                    // Update the Name property in the JSON file if possible
                    try
                    {
                        var json = File.ReadAllText(newPath);
                        if (json.Contains("\"Name\""))
                        {
                            // Simple replacement - works for most JSON formats
                            json = System.Text.RegularExpressions.Regex.Replace(
                                json,
                                @"""Name""\s*:\s*""[^""]*""",
                                $"\"Name\": \"{newName}\"");
                            File.WriteAllText(newPath, json);
                        }
                    }
                    catch
                    {
                        // Ignore errors updating the JSON content
                    }
                });

                // Update the model
                SelectedProject.Name = newName;
                SelectedProject.FilePath = newPath;

                // Refresh the list
                await LoadProjectsAsync();
                StatusMessage = "Project renamed successfully";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename project: {ex.Message}", "Rename Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusMessage = "Rename failed";
            }
            finally
            {
                IsLoading = false;
            }
        }
    }

    /// <summary>
    /// Creates a ZIP archive of the selected project.
    /// </summary>
    [RelayCommand]
    private async Task ArchiveProjectAsync()
    {
        if (SelectedProject == null || !File.Exists(SelectedProject.FilePath))
            return;

        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Archive Project",
                FileName = $"{Path.GetFileNameWithoutExtension(SelectedProject.FilePath)}_archive.zip",
                Filter = "ZIP Archive (*.zip)|*.zip",
                DefaultExt = ".zip",
                InitialDirectory = Path.GetDirectoryName(SelectedProject.FilePath) ?? _currentDirectory
            };

            if (saveDialog.ShowDialog() == true)
            {
                IsLoading = true;
                StatusMessage = "Creating archive...";

                await Task.Run(() =>
                {
                    var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                    Directory.CreateDirectory(tempDir);

                    try
                    {
                        var sourceDir = Path.GetDirectoryName(SelectedProject.FilePath);
                        var baseName = Path.GetFileNameWithoutExtension(SelectedProject.FilePath);

                        // Copy the main project file
                        File.Copy(SelectedProject.FilePath, Path.Combine(tempDir, Path.GetFileName(SelectedProject.FilePath)));

                        // Copy all associated files with the same base name
                        if (sourceDir != null)
                        {
                            var associatedFiles = Directory.GetFiles(sourceDir)
                                .Where(f => Path.GetFileNameWithoutExtension(f)
                                    .StartsWith(baseName, StringComparison.OrdinalIgnoreCase)
                                    && f != SelectedProject.FilePath);

                            foreach (var file in associatedFiles)
                            {
                                File.Copy(file, Path.Combine(tempDir, Path.GetFileName(file)));
                            }

                            // Copy project subdirectory if exists (audio samples, etc.)
                            var projectSubDir = Path.Combine(sourceDir, baseName);
                            if (Directory.Exists(projectSubDir))
                            {
                                CopyDirectory(projectSubDir, Path.Combine(tempDir, baseName));
                            }

                            // Also check for common subdirectories
                            string[] commonSubDirs = { "Audio", "Samples", "Presets", "Renders" };
                            foreach (var subDir in commonSubDirs)
                            {
                                var fullSubDir = Path.Combine(sourceDir, subDir);
                                if (Directory.Exists(fullSubDir))
                                {
                                    CopyDirectory(fullSubDir, Path.Combine(tempDir, subDir));
                                }
                            }
                        }

                        // Create the archive
                        if (File.Exists(saveDialog.FileName))
                            File.Delete(saveDialog.FileName);

                        ZipFile.CreateFromDirectory(tempDir, saveDialog.FileName, CompressionLevel.Optimal, false);
                    }
                    finally
                    {
                        // Cleanup
                        if (Directory.Exists(tempDir))
                            Directory.Delete(tempDir, true);
                    }
                });

                StatusMessage = "Archive created successfully";

                // Ask if user wants to open the archive location
                var result = MessageBox.Show(
                    $"Archive created successfully.\n\nDo you want to open the folder containing the archive?",
                    "Archive Complete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{saveDialog.FileName}\"",
                        UseShellExecute = true
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create archive: {ex.Message}", "Archive Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            StatusMessage = "Archive failed";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// Recursively copies a directory.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
    }

    /// <summary>
    /// Applies filtering and sorting to the projects list.
    /// </summary>
    private void ApplyFilter()
    {
        var filtered = Projects.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p =>
                p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Author.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.Tags.Any(t => t.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        // Apply favorites filter
        if (ShowFavoritesOnly)
        {
            filtered = filtered.Where(p => p.IsFavorite);
        }

        // Apply sorting
        filtered = SortOption switch
        {
            ProjectSortOption.Name => filtered.OrderBy(p => p.Name),
            ProjectSortOption.NameDescending => filtered.OrderByDescending(p => p.Name),
            ProjectSortOption.DateModified => filtered.OrderByDescending(p => p.ModifiedDate),
            ProjectSortOption.DateModifiedAscending => filtered.OrderBy(p => p.ModifiedDate),
            ProjectSortOption.DateCreated => filtered.OrderByDescending(p => p.CreatedDate),
            ProjectSortOption.FileSize => filtered.OrderByDescending(p => p.FileSize),
            ProjectSortOption.Bpm => filtered.OrderBy(p => p.Bpm),
            _ => filtered
        };

        FilteredProjects.Clear();
        foreach (var project in filtered)
        {
            FilteredProjects.Add(project);
        }
    }

    /// <summary>
    /// Updates the favorites collection.
    /// </summary>
    private void UpdateFavorites()
    {
        FavoriteProjects.Clear();
        foreach (var project in Projects.Where(p => p.IsFavorite))
        {
            FavoriteProjects.Add(project);
        }
    }

    /// <summary>
    /// Adds a project to the recent list.
    /// </summary>
    public void AddToRecent(ProjectInfo project)
    {
        // Remove if already in list
        var existing = RecentProjects.FirstOrDefault(p => p.FilePath == project.FilePath);
        if (existing != null)
        {
            RecentProjects.Remove(existing);
        }

        // Add to front
        RecentProjects.Insert(0, project);

        // Limit to 10 recent projects
        while (RecentProjects.Count > 10)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }
    }
}
