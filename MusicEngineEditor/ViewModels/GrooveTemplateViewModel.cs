using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;
using MusicEngine.Core.Groove;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// ViewModel for groove template selection and application.
/// </summary>
public partial class GrooveTemplateViewModel : ViewModelBase
{
    private readonly GrooveTemplateManager _templateManager;

    [ObservableProperty]
    private ObservableCollection<GrooveTemplateItem> _templates = [];

    [ObservableProperty]
    private GrooveTemplateItem? _selectedTemplate;

    [ObservableProperty]
    private double _amount = 1.0;

    [ObservableProperty]
    private bool _applyTiming = true;

    [ObservableProperty]
    private bool _applyVelocity = true;

    [ObservableProperty]
    private bool _quantizeFirst;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    [ObservableProperty]
    private string _previewText = "";

    /// <summary>
    /// Event raised when a groove should be applied.
    /// </summary>
    public event EventHandler<GrooveApplyEventArgs>? ApplyRequested;

    /// <summary>
    /// Event raised when preview is requested.
    /// </summary>
    public event EventHandler<ExtractedGroove>? PreviewRequested;

    public GrooveTemplateViewModel()
    {
        _templateManager = new GrooveTemplateManager();
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        Categories.Clear();
        Categories.Add("All");

        // Load built-in templates
        var builtIn = GrooveTemplateManager.GetBuiltInTemplates();
        foreach (var kvp in builtIn)
        {
            var item = new GrooveTemplateItem
            {
                Name = kvp.Key,
                Groove = kvp.Value,
                IsBuiltIn = true,
                Category = GetCategoryFromTags(kvp.Value.Tags)
            };
            Templates.Add(item);

            if (!Categories.Contains(item.Category))
            {
                Categories.Add(item.Category);
            }
        }

        // Load user templates
        var userTemplates = _templateManager.LoadAllUserTemplates();
        foreach (var groove in userTemplates)
        {
            var item = new GrooveTemplateItem
            {
                Name = groove.Name,
                Groove = groove,
                IsBuiltIn = false,
                Category = GetCategoryFromTags(groove.Tags)
            };
            Templates.Add(item);

            if (!Categories.Contains(item.Category))
            {
                Categories.Add(item.Category);
            }
        }

        // Select first template if available
        if (Templates.Count > 0)
        {
            SelectedTemplate = Templates[0];
        }
    }

    private static string GetCategoryFromTags(System.Collections.Generic.List<string> tags)
    {
        if (tags.Contains("mpc") || tags.Contains("swing"))
            return "MPC Swing";
        if (tags.Contains("shuffle"))
            return "Shuffle";
        if (tags.Contains("hip-hop") || tags.Contains("lazy"))
            return "Hip-Hop";
        if (tags.Contains("funk"))
            return "Funk";
        if (tags.Contains("jazz"))
            return "Jazz";
        if (tags.Contains("reggae") || tags.Contains("dub"))
            return "Reggae";
        if (tags.Contains("house") || tags.Contains("electronic"))
            return "Electronic";
        if (tags.Contains("dnb") || tags.Contains("jungle"))
            return "Drum & Bass";

        return "Other";
    }

    partial void OnSelectedTemplateChanged(GrooveTemplateItem? value)
    {
        if (value?.Groove != null)
        {
            UpdatePreviewText(value.Groove);
        }
        else
        {
            PreviewText = "";
        }
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        // Could filter templates here if needed
    }

    private void UpdatePreviewText(ExtractedGroove groove)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"Name: {groove.Name}");
        lines.AppendLine($"Swing: {groove.SwingAmount:F1}%");
        lines.AppendLine($"Cycle: {groove.CycleLengthBeats} beat(s)");
        lines.AppendLine($"Resolution: {groove.Resolution} PPQN");

        if (!string.IsNullOrEmpty(groove.Description))
        {
            lines.AppendLine();
            lines.AppendLine(groove.Description);
        }

        if (groove.TimingDeviations.Count > 0)
        {
            lines.AppendLine();
            lines.AppendLine("Timing Deviations:");
            foreach (var dev in groove.TimingDeviations.Take(8))
            {
                lines.AppendLine($"  {dev.BeatPosition:F2}: {dev.DeviationInTicks:+0;-0;0} ticks");
            }
            if (groove.TimingDeviations.Count > 8)
            {
                lines.AppendLine($"  ... ({groove.TimingDeviations.Count - 8} more)");
            }
        }

        PreviewText = lines.ToString();
    }

    [RelayCommand]
    private void Apply()
    {
        if (SelectedTemplate?.Groove == null)
            return;

        var options = new GrooveApplyOptions
        {
            Amount = Amount,
            ApplyTiming = ApplyTiming,
            ApplyVelocity = ApplyVelocity,
            QuantizeFirst = QuantizeFirst,
            QuantizeGrid = 0.25 // 16th notes
        };

        ApplyRequested?.Invoke(this, new GrooveApplyEventArgs(SelectedTemplate.Groove, options));
    }

    [RelayCommand]
    private void Preview()
    {
        if (SelectedTemplate?.Groove != null)
        {
            PreviewRequested?.Invoke(this, SelectedTemplate.Groove);
        }
    }

    [RelayCommand]
    private async Task SaveAsTemplateAsync(ExtractedGroove? sourceGroove)
    {
        if (sourceGroove == null)
            return;

        IsBusy = true;
        StatusMessage = "Saving groove template...";

        try
        {
            await Task.Run(() => _templateManager.SaveTemplate(sourceGroove));
            LoadTemplates(); // Refresh list
            StatusMessage = $"Template '{sourceGroove.Name}' saved successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving template: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void DeleteTemplate()
    {
        if (SelectedTemplate == null || SelectedTemplate.IsBuiltIn)
            return;

        // Find the file path and delete
        var files = _templateManager.ListUserTemplates();
        foreach (var file in files)
        {
            var loaded = _templateManager.LoadTemplate(file);
            if (loaded?.Name == SelectedTemplate.Name)
            {
                _templateManager.DeleteTemplate(file);
                break;
            }
        }

        LoadTemplates();
    }

    [RelayCommand]
    private void RefreshTemplates()
    {
        LoadTemplates();
    }

    /// <summary>
    /// Gets filtered templates based on selected category.
    /// </summary>
    public ObservableCollection<GrooveTemplateItem> FilteredTemplates
    {
        get
        {
            if (SelectedCategory == "All")
                return Templates;

            var filtered = new ObservableCollection<GrooveTemplateItem>();
            foreach (var item in Templates)
            {
                if (item.Category == SelectedCategory)
                    filtered.Add(item);
            }
            return filtered;
        }
    }
}

/// <summary>
/// Represents a groove template item in the list.
/// </summary>
public partial class GrooveTemplateItem : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private ExtractedGroove? _groove;

    [ObservableProperty]
    private bool _isBuiltIn;

    [ObservableProperty]
    private string _category = "Other";

    public string DisplayName => IsBuiltIn ? Name : $"{Name} (User)";
}

/// <summary>
/// Event args for groove apply requests.
/// </summary>
public class GrooveApplyEventArgs : EventArgs
{
    public ExtractedGroove Groove { get; }
    public GrooveApplyOptions Options { get; }

    public GrooveApplyEventArgs(ExtractedGroove groove, GrooveApplyOptions options)
    {
        Groove = groove;
        Options = options;
    }
}
