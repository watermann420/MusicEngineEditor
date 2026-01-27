// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for browsing and selecting track templates.
/// </summary>
public partial class TrackTemplateDialog : Window
{
    private readonly TrackTemplateService _service;
    private TrackTemplateCategory? _selectedCategory;
    private string _searchText = string.Empty;

    /// <summary>
    /// Gets the selected template (after dialog closes with OK).
    /// </summary>
    public TrackTemplate? SelectedTemplate { get; private set; }

    /// <summary>
    /// Event raised when a track should be created from a template.
    /// </summary>
    public event EventHandler<TrackTemplate>? CreateTrackRequested;

    public TrackTemplateDialog()
    {
        InitializeComponent();
        _service = TrackTemplateService.Instance;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _service.InitializeAsync();
        _service.TemplatesChanged += OnTemplatesChanged;
        RefreshTemplateList();
    }

    protected override void OnClosed(EventArgs e)
    {
        _service.TemplatesChanged -= OnTemplatesChanged;
        base.OnClosed(e);
    }

    private void OnTemplatesChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(RefreshTemplateList);
    }

    private void RefreshTemplateList()
    {
        var templates = _service.Templates.AsEnumerable();

        // Filter by category
        if (_selectedCategory.HasValue)
        {
            templates = templates.Where(t => t.Category == _selectedCategory.Value);
        }

        // Filter by search text
        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            templates = _service.SearchTemplates(_searchText);
            if (_selectedCategory.HasValue)
            {
                templates = templates.Where(t => t.Category == _selectedCategory.Value);
            }
        }

        var templateList = templates.OrderBy(t => t.IsBuiltIn ? 0 : 1).ThenBy(t => t.Name).ToList();

        TemplateListBox.ItemsSource = templateList;
        TemplateCountText.Text = $"{templateList.Count} template{(templateList.Count != 1 ? "s" : "")}";
    }

    private void Category_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton rb)
        {
            _selectedCategory = rb.Name switch
            {
                "GeneralCategory" => TrackTemplateCategory.General,
                "DrumsCategory" => TrackTemplateCategory.Drums,
                "BassCategory" => TrackTemplateCategory.Bass,
                "SynthCategory" => TrackTemplateCategory.Synth,
                "GuitarCategory" => TrackTemplateCategory.Guitar,
                "VocalsCategory" => TrackTemplateCategory.Vocals,
                "KeysCategory" => TrackTemplateCategory.Keys,
                "FXCategory" => TrackTemplateCategory.FX,
                "BusCategory" => TrackTemplateCategory.Bus,
                "CustomCategory" => TrackTemplateCategory.Custom,
                _ => null
            };

            RefreshTemplateList();
        }
    }

    private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchText = SearchTextBox.Text;
        RefreshTemplateList();
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is TrackTemplate template)
        {
            ShowTemplateDetails(template);
            CreateTrackButton.IsEnabled = true;
            EditButton.IsEnabled = !template.IsBuiltIn;
            DeleteButton.IsEnabled = !template.IsBuiltIn;
        }
        else
        {
            HideTemplateDetails();
            CreateTrackButton.IsEnabled = false;
            EditButton.IsEnabled = false;
            DeleteButton.IsEnabled = false;
        }
    }

    private void TemplateListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (TemplateListBox.SelectedItem is TrackTemplate template)
        {
            SelectedTemplate = template;
            DialogResult = true;
            Close();
        }
    }

    private void ShowTemplateDetails(TrackTemplate template)
    {
        NoSelectionText.Visibility = Visibility.Collapsed;
        TemplateDetails.Visibility = Visibility.Visible;

        DetailName.Text = template.Name;
        DetailDescription.Text = template.Description;
        DetailTrackType.Text = template.TrackType;
        DetailInstrument.Text = template.InstrumentType ?? "None";
        DetailOutput.Text = template.OutputRouting;
        DetailCategory.Text = template.Category.ToString();

        // Effects
        EffectsList.ItemsSource = template.Effects;
        NoEffectsText.Visibility = template.Effects.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Sends
        SendsList.ItemsSource = template.Sends;
        NoSendsText.Visibility = template.Sends.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Tags
        TagsList.ItemsSource = template.Tags;
    }

    private void HideTemplateDetails()
    {
        NoSelectionText.Visibility = Visibility.Visible;
        TemplateDetails.Visibility = Visibility.Collapsed;
    }

    private void CreateTrack_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is TrackTemplate template)
        {
            SelectedTemplate = template;
            CreateTrackRequested?.Invoke(this, template);
            DialogResult = true;
            Close();
        }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is TrackTemplate template && !template.IsBuiltIn)
        {
            // Open edit dialog
            var editDialog = new TrackTemplateEditDialog(template.Clone())
            {
                Owner = this
            };

            if (editDialog.ShowDialog() == true && editDialog.Template != null)
            {
                // Save changes
                _ = _service.UpdateTemplateAsync(editDialog.Template);
            }
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is TrackTemplate template && !template.IsBuiltIn)
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete the template '{template.Name}'?\n\nThis action cannot be undone.",
                "Delete Template",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _service.DeleteTemplate(template);
            }
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Track Template (*.json)|*.json|All Files (*.*)|*.*",
            Title = "Import Track Template"
        };

        if (dialog.ShowDialog() == true)
        {
            var template = await _service.ImportTemplateAsync(dialog.FileName);
            if (template != null)
            {
                MessageBox.Show($"Template '{template.Name}' imported successfully.",
                    "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("Failed to import template. The file may be invalid.",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is not TrackTemplate template)
        {
            MessageBox.Show("Please select a template to export.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "Track Template (*.json)|*.json",
            FileName = $"{template.Name}.json",
            Title = "Export Track Template"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                await _service.ExportTemplateAsync(template, dialog.FileName);
                MessageBox.Show($"Template exported to:\n{dialog.FileName}",
                    "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting template:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

/// <summary>
/// Simple dialog for editing track template properties.
/// </summary>
public class TrackTemplateEditDialog : Window
{
    private readonly TextBox _nameTextBox;
    private readonly TextBox _descriptionTextBox;
    private readonly System.Windows.Controls.ComboBox _categoryComboBox;

    /// <summary>
    /// Gets the edited template.
    /// </summary>
    public new TrackTemplate? Template { get; private set; }

    public TrackTemplateEditDialog(TrackTemplate template)
    {
        Template = template;

        Title = "Edit Track Template";
        Width = 450;
        Height = 350;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        Foreground = Brushes.White;

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Name
        var nameLabel = new TextBlock { Text = "Name:", Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(nameLabel, 0);
        grid.Children.Add(nameLabel);

        _nameTextBox = new TextBox
        {
            Text = template.Name,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A)),
            Margin = new Thickness(0, 0, 0, 12)
        };
        Grid.SetRow(_nameTextBox, 1);
        grid.Children.Add(_nameTextBox);

        // Category
        var categoryLabel = new TextBlock { Text = "Category:", Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(categoryLabel, 2);
        grid.Children.Add(categoryLabel);

        _categoryComboBox = new System.Windows.Controls.ComboBox
        {
            ItemsSource = Enum.GetValues(typeof(TrackTemplateCategory)),
            SelectedItem = template.Category,
            Margin = new Thickness(0, 0, 0, 12),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A))
        };
        Grid.SetRow(_categoryComboBox, 3);
        grid.Children.Add(_categoryComboBox);

        // Description
        var descLabel = new TextBlock { Text = "Description:", Margin = new Thickness(0, 0, 0, 4) };
        Grid.SetRow(descLabel, 4);
        grid.Children.Add(descLabel);

        _descriptionTextBox = new TextBox
        {
            Text = template.Description,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Padding = new Thickness(8),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A)),
            Margin = new Thickness(0, 24, 0, 16),
            MinHeight = 80
        };
        Grid.SetRow(_descriptionTextBox, 4);
        grid.Children.Add(_descriptionTextBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var saveButton = new Button
        {
            Content = "Save",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(0, 6, 0, 6),
            Background = new SolidColorBrush(Color.FromRgb(0x55, 0xAA, 0xFF)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x55, 0xAA, 0xFF)),
            IsDefault = true
        };
        saveButton.Click += (s, e) =>
        {
            Template!.Name = _nameTextBox.Text;
            Template.Description = _descriptionTextBox.Text;
            Template.Category = (TrackTemplateCategory)_categoryComboBox.SelectedItem;
            Template.ModifiedDate = DateTime.UtcNow;
            DialogResult = true;
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            IsCancel = true
        };
        cancelButton.Click += (s, e) =>
        {
            Template = null;
            DialogResult = false;
        };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 5);
        grid.Children.Add(buttonPanel);

        Content = grid;
    }
}
