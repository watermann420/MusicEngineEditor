using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for the Project Explorer panel
/// </summary>
public partial class ProjectExplorerViewModel : ViewModelBase
{
    [ObservableProperty]
    private MusicProject? _project;

    [ObservableProperty]
    private FileTreeNode? _selectedNode;

    public ObservableCollection<FileTreeNode> RootNodes { get; } = new();

    public event System.EventHandler<MusicScript>? ScriptDoubleClicked;

    public void LoadProject(MusicProject project)
    {
        Project = project;
        RootNodes.Clear();

        if (project == null) return;

        // Create root node for project
        var projectNode = new FileTreeNode
        {
            Name = project.Name,
            FullPath = project.FilePath,
            NodeType = FileTreeNodeType.Project,
            IsExpanded = true
        };

        // Add Scripts folder
        var scriptsNode = new FileTreeNode
        {
            Name = "Scripts",
            FullPath = Path.Combine(project.ProjectDirectory, "Scripts"),
            NodeType = FileTreeNodeType.Folder,
            IsExpanded = true
        };

        foreach (var script in project.Scripts)
        {
            scriptsNode.Children.Add(new FileTreeNode
            {
                Name = script.FileName,
                FullPath = script.FilePath,
                NodeType = FileTreeNodeType.Script,
                Script = script,
                IsEntryPoint = script.IsEntryPoint
            });
        }

        projectNode.Children.Add(scriptsNode);

        // Add Audio folder
        var audioNode = new FileTreeNode
        {
            Name = "Audio",
            FullPath = Path.Combine(project.ProjectDirectory, "Audio"),
            NodeType = FileTreeNodeType.Folder,
            IsExpanded = true
        };

        // Group by category
        var categories = new Dictionary<string, FileTreeNode>();
        foreach (var asset in project.AudioAssets)
        {
            if (!categories.TryGetValue(asset.Category, out var categoryNode))
            {
                categoryNode = new FileTreeNode
                {
                    Name = asset.Category,
                    FullPath = Path.Combine(project.ProjectDirectory, "Audio", asset.Category),
                    NodeType = FileTreeNodeType.Folder
                };
                categories[asset.Category] = categoryNode;
                audioNode.Children.Add(categoryNode);
            }

            categoryNode.Children.Add(new FileTreeNode
            {
                Name = $"{asset.Alias} ({asset.FileName})",
                FullPath = asset.FilePath,
                NodeType = FileTreeNodeType.Audio,
                AudioAsset = asset
            });
        }

        projectNode.Children.Add(audioNode);

        // Add References folder
        if (project.References.Count > 0)
        {
            var referencesNode = new FileTreeNode
            {
                Name = "References",
                FullPath = string.Empty,
                NodeType = FileTreeNodeType.Folder
            };

            foreach (var reference in project.References)
            {
                referencesNode.Children.Add(new FileTreeNode
                {
                    Name = reference.Alias,
                    FullPath = reference.Path,
                    NodeType = FileTreeNodeType.Reference,
                    Reference = reference
                });
            }

            projectNode.Children.Add(referencesNode);
        }

        RootNodes.Add(projectNode);
    }

    [RelayCommand]
    private void NodeDoubleClick(FileTreeNode? node)
    {
        if (node?.Script != null)
        {
            ScriptDoubleClicked?.Invoke(this, node.Script);
        }
    }

    [RelayCommand]
    private void AddNewScript()
    {
        // TODO: Implement
    }

    [RelayCommand]
    private void AddNewFolder()
    {
        // TODO: Implement
    }

    [RelayCommand]
    private void DeleteNode()
    {
        // TODO: Implement
    }

    [RelayCommand]
    private void RenameNode()
    {
        // TODO: Implement
    }
}

/// <summary>
/// Represents a node in the file tree
/// </summary>
public partial class FileTreeNode : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private FileTreeNodeType _nodeType;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isEntryPoint;

    public MusicScript? Script { get; set; }
    public AudioAsset? AudioAsset { get; set; }
    public ProjectReference? Reference { get; set; }

    public ObservableCollection<FileTreeNode> Children { get; } = new();
}

public enum FileTreeNodeType
{
    Project,
    Folder,
    Script,
    Audio,
    Reference
}
