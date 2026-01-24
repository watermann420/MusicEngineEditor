using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MusicEngine.Core.Groove;
using MusicEngineEditor.ViewModels;
using Shapes = System.Windows.Shapes;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for selecting and applying groove templates.
/// </summary>
public partial class GrooveTemplateDialog : Window
{
    private readonly GrooveTemplateViewModel _viewModel;
    private readonly List<GrooveTemplateItem> _allTemplates = [];

    /// <summary>
    /// Gets the selected groove template.
    /// </summary>
    public ExtractedGroove? SelectedGroove => _viewModel.SelectedTemplate?.Groove;

    /// <summary>
    /// Gets the application options.
    /// </summary>
    public GrooveApplyOptions Options { get; private set; } = new();

    /// <summary>
    /// Event raised when preview is requested.
    /// </summary>
    public event EventHandler<ExtractedGroove>? PreviewRequested;

    public GrooveTemplateDialog()
    {
        InitializeComponent();

        _viewModel = new GrooveTemplateViewModel();
        LoadTemplates();
    }

    /// <summary>
    /// Sets the source groove for "Save as Template" functionality.
    /// </summary>
    public ExtractedGroove? SourceGroove { get; set; }

    private void LoadTemplates()
    {
        _allTemplates.Clear();

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
            _allTemplates.Add(item);
        }

        // Load user templates
        var manager = new GrooveTemplateManager();
        var userTemplates = manager.LoadAllUserTemplates();
        foreach (var groove in userTemplates)
        {
            var item = new GrooveTemplateItem
            {
                Name = groove.Name,
                Groove = groove,
                IsBuiltIn = false,
                Category = GetCategoryFromTags(groove.Tags)
            };
            _allTemplates.Add(item);
        }

        UpdateFilteredList();
    }

    private static string GetCategoryFromTags(List<string> tags)
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
        if (tags.Contains("house") || tags.Contains("electronic"))
            return "Electronic";

        return "Other";
    }

    private void UpdateFilteredList()
    {
        var selectedCategory = (CategoryComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

        var filtered = selectedCategory == "All"
            ? _allTemplates
            : _allTemplates.Where(t => t.Category == selectedCategory).ToList();

        TemplateListBox.ItemsSource = filtered;

        if (filtered.Count > 0)
        {
            TemplateListBox.SelectedIndex = 0;
        }
    }

    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (IsLoaded)
        {
            UpdateFilteredList();
        }
    }

    private void TemplateListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TemplateListBox.SelectedItem is GrooveTemplateItem item)
        {
            _viewModel.SelectedTemplate = item;
            UpdatePreview(item.Groove);
        }
    }

    private void UpdatePreview(ExtractedGroove? groove)
    {
        DeviationCanvas.Children.Clear();

        if (groove == null)
        {
            TemplateInfoText.Text = "Select a template";
            return;
        }

        // Update info text
        var info = $"Swing: {groove.SwingAmount:F1}%\n";
        info += $"Cycle: {groove.CycleLengthBeats} beat(s)\n";
        info += $"Points: {groove.TimingDeviations.Count}";
        TemplateInfoText.Text = info;

        // Draw deviation graph
        DrawDeviationGraph(groove);
    }

    private void DrawDeviationGraph(ExtractedGroove groove)
    {
        double canvasWidth = DeviationCanvas.ActualWidth > 0 ? DeviationCanvas.ActualWidth : 170;
        double canvasHeight = DeviationCanvas.ActualHeight > 0 ? DeviationCanvas.ActualHeight : 100;

        if (canvasWidth <= 0 || canvasHeight <= 0 || groove.TimingDeviations.Count == 0)
            return;

        // Draw center line
        var centerLine = new Shapes.Line
        {
            X1 = 0,
            Y1 = canvasHeight / 2,
            X2 = canvasWidth,
            Y2 = canvasHeight / 2,
            Stroke = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 2, 2 }
        };
        DeviationCanvas.Children.Add(centerLine);

        // Find max deviation for scaling
        double maxDev = groove.TimingDeviations.Max(d => Math.Abs(d.DeviationInTicks));
        if (maxDev < 1) maxDev = 1;

        var deviations = groove.TimingDeviations.Take(16).ToList();
        double stepX = canvasWidth / Math.Max(1, deviations.Count - 1);
        double scaleY = (canvasHeight * 0.4) / maxDev;

        // Draw deviation points and lines
        var pointBrush = FindResource("AccentBrush") as Brush ?? Brushes.Blue;
        var lineBrush = new SolidColorBrush(Color.FromArgb(128, 75, 110, 175));

        Point? lastPoint = null;
        for (int i = 0; i < deviations.Count; i++)
        {
            double x = i * stepX;
            double y = canvasHeight / 2 - (deviations[i].DeviationInTicks * scaleY);

            var point = new Shapes.Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = pointBrush
            };
            Canvas.SetLeft(point, x - 3);
            Canvas.SetTop(point, y - 3);
            DeviationCanvas.Children.Add(point);

            // Connect with line
            if (lastPoint.HasValue)
            {
                var line = new Shapes.Line
                {
                    X1 = lastPoint.Value.X,
                    Y1 = lastPoint.Value.Y,
                    X2 = x,
                    Y2 = y,
                    Stroke = lineBrush,
                    StrokeThickness = 2
                };
                DeviationCanvas.Children.Insert(1, line); // Insert behind points
            }

            lastPoint = new Point(x, y);
        }
    }

    private void PreviewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTemplate?.Groove != null)
        {
            PreviewRequested?.Invoke(this, _viewModel.SelectedTemplate.Groove);
        }
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedTemplate?.Groove == null)
        {
            MessageBox.Show("Please select a groove template.", "No Template Selected",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Options = new GrooveApplyOptions
        {
            Amount = AmountSlider.Value / 100.0,
            ApplyTiming = ApplyTimingCheckBox.IsChecked == true,
            ApplyVelocity = ApplyVelocityCheckBox.IsChecked == true,
            QuantizeFirst = QuantizeFirstCheckBox.IsChecked == true,
            QuantizeGrid = 0.25 // 16th notes
        };

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void SaveAsTemplateButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourceGroove == null)
        {
            MessageBox.Show("No groove data available to save.", "Cannot Save",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Show name input dialog
        var nameDialog = new GrooveInputDialog
        {
            Title = "Save Groove Template",
            Prompt = "Enter a name for this template:",
            DefaultValue = SourceGroove.Name ?? "My Groove",
            Owner = this
        };

        if (nameDialog.ShowDialog() == true)
        {
            SourceGroove.Name = nameDialog.InputValue;

            try
            {
                var manager = new GrooveTemplateManager();
                await System.Threading.Tasks.Task.Run(() => manager.SaveTemplate(SourceGroove));

                MessageBox.Show($"Template '{SourceGroove.Name}' saved successfully.",
                    "Template Saved", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadTemplates();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving template: {ex.Message}",
                    "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

/// <summary>
/// Simple input dialog for text entry (used by GrooveTemplateDialog).
/// </summary>
public class GrooveInputDialog : Window
{
    private readonly TextBox _textBox;

    public string Prompt { get; set; } = "Enter value:";
    public string DefaultValue { get; set; } = "";
    public string InputValue { get; private set; } = "";

    public GrooveInputDialog()
    {
        Width = 350;
        Height = 150;
        WindowStyle = WindowStyle.ToolWindow;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30));
        Foreground = Brushes.White;

        var grid = new Grid { Margin = new Thickness(20) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var label = new TextBlock { Text = Prompt, Margin = new Thickness(0, 0, 0, 8) };
        Grid.SetRow(label, 0);
        grid.Children.Add(label);

        _textBox = new TextBox
        {
            Text = DefaultValue,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A)),
            Margin = new Thickness(0, 0, 0, 16)
        };
        Grid.SetRow(_textBox, 1);
        grid.Children.Add(_textBox);

        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            Margin = new Thickness(0, 0, 8, 0),
            Padding = new Thickness(0, 6, 0, 6),
            IsDefault = true
        };
        okButton.Click += (s, e) =>
        {
            InputValue = _textBox.Text;
            DialogResult = true;
        };

        var cancelButton = new Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(0, 6, 0, 6),
            IsCancel = true
        };
        cancelButton.Click += (s, e) => DialogResult = false;

        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        Content = grid;

        Loaded += (s, e) =>
        {
            label.Text = Prompt;
            _textBox.Text = DefaultValue;
            _textBox.SelectAll();
            _textBox.Focus();
        };
    }
}
