// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: UI control implementation.

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;

namespace MusicEngineEditor.Controls;

/// <summary>
/// A panel control for managing project notes with rich text formatting.
/// </summary>
public partial class ProjectNotesPanel : UserControl
{
    #region Dependency Properties

    /// <summary>
    /// Identifies the NotesText dependency property.
    /// </summary>
    public static readonly DependencyProperty NotesTextProperty =
        DependencyProperty.Register(
            nameof(NotesText),
            typeof(string),
            typeof(ProjectNotesPanel),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnNotesTextChanged));

    /// <summary>
    /// Identifies the IsReadOnly dependency property.
    /// </summary>
    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(ProjectNotesPanel),
            new PropertyMetadata(false, OnIsReadOnlyChanged));

    /// <summary>
    /// Gets or sets the notes text as XAML-serialized FlowDocument content.
    /// </summary>
    public string NotesText
    {
        get => (string)GetValue(NotesTextProperty);
        set => SetValue(NotesTextProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the notes panel is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the notes content has changed.
    /// </summary>
    public event EventHandler? NotesContentChanged;

    #endregion

    #region Fields

    private bool _isUpdatingFromProperty;
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private bool _isDirty;
#pragma warning restore CS0414

    #endregion

    #region Constructor

    public ProjectNotesPanel()
    {
        InitializeComponent();
        UpdateCharCount();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Clears all notes content.
    /// </summary>
    public void ClearNotes()
    {
        NotesDocument.Blocks.Clear();
        NotesDocument.Blocks.Add(new Paragraph(new Run(string.Empty)));
        _isDirty = true;
        UpdateCharCount();
        SyncToProperty();
    }

    /// <summary>
    /// Gets the plain text content of the notes.
    /// </summary>
    public string GetPlainText()
    {
        var textRange = new TextRange(
            NotesDocument.ContentStart,
            NotesDocument.ContentEnd);
        return textRange.Text.TrimEnd();
    }

    /// <summary>
    /// Sets the notes content from plain text.
    /// </summary>
    public void SetPlainText(string text)
    {
        NotesDocument.Blocks.Clear();

        if (string.IsNullOrEmpty(text))
        {
            NotesDocument.Blocks.Add(new Paragraph(new Run(string.Empty)));
        }
        else
        {
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                NotesDocument.Blocks.Add(new Paragraph(new Run(line)));
            }
        }

        UpdateCharCount();
    }

    #endregion

    #region Event Handlers

    private void BoldButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSelectionProperty(TextElement.FontWeightProperty, FontWeights.Bold, FontWeights.Normal);
        UpdateFormattingButtons();
    }

    private void ItalicButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleSelectionProperty(TextElement.FontStyleProperty, FontStyles.Italic, FontStyles.Normal);
        UpdateFormattingButtons();
    }

    private void UnderlineButton_Click(object sender, RoutedEventArgs e)
    {
        var currentValue = NotesRichTextBox.Selection.GetPropertyValue(Inline.TextDecorationsProperty);
        var underlined = currentValue != DependencyProperty.UnsetValue &&
                         currentValue is TextDecorationCollection decorations &&
                         decorations.Contains(TextDecorations.Underline[0]);

        NotesRichTextBox.Selection.ApplyPropertyValue(
            Inline.TextDecorationsProperty,
            underlined ? null : TextDecorations.Underline);
        UpdateFormattingButtons();
    }

    private void BulletListButton_Click(object sender, RoutedEventArgs e)
    {
        // Insert a bullet point at current position
        var position = NotesRichTextBox.CaretPosition;
        var paragraph = position.Paragraph;

        if (paragraph != null)
        {
            var text = new TextRange(paragraph.ContentStart, paragraph.ContentEnd).Text;
            if (!text.StartsWith("- "))
            {
                paragraph.Inlines.InsertBefore(paragraph.Inlines.FirstInline, new Run("- "));
            }
        }
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            "Are you sure you want to clear all notes?",
            "Clear Notes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            ClearNotes();
        }
    }

    private void NotesRichTextBox_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateFormattingButtons();
    }

    private void NotesRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingFromProperty)
            return;

        _isDirty = true;
        UpdateCharCount();
        SyncToProperty();
        NotesContentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NotesRichTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Handle keyboard shortcuts
        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.B:
                    BoldButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.I:
                    ItalicButton_Click(sender, e);
                    e.Handled = true;
                    break;
                case Key.U:
                    UnderlineButton_Click(sender, e);
                    e.Handled = true;
                    break;
            }
        }
    }

    #endregion

    #region Private Methods

    private static void OnNotesTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectNotesPanel panel)
        {
            panel.LoadFromProperty();
        }
    }

    private static void OnIsReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ProjectNotesPanel panel)
        {
            panel.NotesRichTextBox.IsReadOnly = (bool)e.NewValue;
            panel.UpdateStatus();
        }
    }

    private void LoadFromProperty()
    {
        if (_isUpdatingFromProperty)
            return;

        _isUpdatingFromProperty = true;
        try
        {
            var text = NotesText;
            if (string.IsNullOrEmpty(text))
            {
                NotesDocument.Blocks.Clear();
                NotesDocument.Blocks.Add(new Paragraph(new Run(string.Empty)));
            }
            else if (text.StartsWith("<FlowDocument"))
            {
                // Try to load as XAML FlowDocument
                try
                {
                    using var reader = new StringReader(text);
                    var doc = XamlReader.Load(
                        System.Xml.XmlReader.Create(reader)) as FlowDocument;

                    if (doc != null)
                    {
                        NotesDocument.Blocks.Clear();
                        while (doc.Blocks.Count > 0)
                        {
                            var block = doc.Blocks.FirstBlock;
                            doc.Blocks.Remove(block);
                            NotesDocument.Blocks.Add(block);
                        }
                    }
                }
                catch
                {
                    // Fall back to plain text if XAML parsing fails
                    SetPlainText(text);
                }
            }
            else
            {
                // Load as plain text
                SetPlainText(text);
            }

            _isDirty = false;
            UpdateCharCount();
        }
        finally
        {
            _isUpdatingFromProperty = false;
        }
    }

    private void SyncToProperty()
    {
        if (_isUpdatingFromProperty)
            return;

        _isUpdatingFromProperty = true;
        try
        {
            // Serialize FlowDocument to XAML string
            using var writer = new StringWriter();
            using var xmlWriter = System.Xml.XmlWriter.Create(writer, new System.Xml.XmlWriterSettings
            {
                Indent = false,
                OmitXmlDeclaration = true
            });

            XamlWriter.Save(NotesDocument, xmlWriter);
            NotesText = writer.ToString();
        }
        catch
        {
            // Fall back to plain text if serialization fails
            NotesText = GetPlainText();
        }
        finally
        {
            _isUpdatingFromProperty = false;
        }
    }

    private void ToggleSelectionProperty(DependencyProperty property, object onValue, object offValue)
    {
        var currentValue = NotesRichTextBox.Selection.GetPropertyValue(property);
        var newValue = currentValue != DependencyProperty.UnsetValue &&
                       currentValue.Equals(onValue) ? offValue : onValue;
        NotesRichTextBox.Selection.ApplyPropertyValue(property, newValue);
    }

    private void UpdateFormattingButtons()
    {
        var selection = NotesRichTextBox.Selection;

        // Update Bold button
        var fontWeight = selection.GetPropertyValue(TextElement.FontWeightProperty);
        BoldButton.IsChecked = fontWeight != DependencyProperty.UnsetValue &&
                               fontWeight.Equals(FontWeights.Bold);

        // Update Italic button
        var fontStyle = selection.GetPropertyValue(TextElement.FontStyleProperty);
        ItalicButton.IsChecked = fontStyle != DependencyProperty.UnsetValue &&
                                 fontStyle.Equals(FontStyles.Italic);

        // Update Underline button
        var textDecorations = selection.GetPropertyValue(Inline.TextDecorationsProperty);
        UnderlineButton.IsChecked = textDecorations != DependencyProperty.UnsetValue &&
                                    textDecorations is TextDecorationCollection decorations &&
                                    decorations.Contains(TextDecorations.Underline[0]);
    }

    private void UpdateCharCount()
    {
        var text = GetPlainText();
        var charCount = text.Length;
        var wordCount = string.IsNullOrWhiteSpace(text) ? 0 :
            text.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

        CharCountText.Text = $"{charCount} characters, {wordCount} words";
    }

    private void UpdateStatus()
    {
        StatusText.Text = IsReadOnly ? "Read-only" : "Ready";
    }

    #endregion
}
