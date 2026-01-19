using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Rendering;

namespace MusicEngineEditor.Editor;

/// <summary>
/// Provides inline slider functionality for numeric literals in the code editor.
/// Similar to Strudel.cc's number manipulation feature.
/// </summary>
public class InlineSliderService : IDisposable
{
    private readonly TextEditor _editor;
    private readonly DispatcherTimer _hoverTimer;
    private SliderPopup? _activePopup;
    private DetectedNumber? _hoveredNumber;
    private Point _lastMousePosition;
    private bool _isDisposed;

    /// <summary>
    /// Event fired when the code is modified by the slider
    /// </summary>
    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;

    /// <summary>
    /// Event fired when the slider is released and the change is complete
    /// </summary>
    public event EventHandler<SliderValueChangedEventArgs>? ValueChangeCompleted;

    public InlineSliderService(TextEditor editor)
    {
        _editor = editor;

        // Setup hover detection timer
        _hoverTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(300)
        };
        _hoverTimer.Tick += HoverTimer_Tick;

        // Subscribe to mouse events
        _editor.TextArea.TextView.MouseMove += TextView_MouseMove;
        _editor.TextArea.TextView.MouseLeave += TextView_MouseLeave;
        _editor.TextArea.TextView.MouseLeftButtonDown += TextView_MouseLeftButtonDown;
        _editor.PreviewKeyDown += Editor_PreviewKeyDown;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        _hoverTimer.Stop();
        _hoverTimer.Tick -= HoverTimer_Tick;

        _editor.TextArea.TextView.MouseMove -= TextView_MouseMove;
        _editor.TextArea.TextView.MouseLeave -= TextView_MouseLeave;
        _editor.TextArea.TextView.MouseLeftButtonDown -= TextView_MouseLeftButtonDown;
        _editor.PreviewKeyDown -= Editor_PreviewKeyDown;

        ClosePopup();
    }

    private void TextView_MouseMove(object sender, MouseEventArgs e)
    {
        _lastMousePosition = e.GetPosition(_editor.TextArea.TextView);

        // If popup is open and mouse is over it, don't do anything
        if (_activePopup != null && _activePopup.IsMouseOver)
        {
            return;
        }

        // Reset hover timer
        _hoverTimer.Stop();
        _hoveredNumber = null;

        // Get position from mouse
        var pos = _editor.GetPositionFromPoint(_lastMousePosition);
        if (pos == null) return;

        var offset = _editor.Document.GetOffset(pos.Value.Location);
        var detectedNumber = NumberDetector.GetNumberAtOffset(_editor.Document, offset);

        if (detectedNumber != null)
        {
            _hoveredNumber = detectedNumber;
            _hoverTimer.Start();
        }
    }

    private void TextView_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverTimer.Stop();
        _hoveredNumber = null;

        // Close popup if mouse leaves editor area and isn't over popup
        if (_activePopup != null && !_activePopup.IsMouseOver)
        {
            // Delay closing to allow mouse to move to popup
            var closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            closeTimer.Tick += (s, args) =>
            {
                closeTimer.Stop();
                if (_activePopup != null && !_activePopup.IsMouseOver && !_editor.TextArea.TextView.IsMouseOver)
                {
                    ClosePopup();
                }
            };
            closeTimer.Start();
        }
    }

    private void TextView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Check if clicking on a number
        var pos = _editor.GetPositionFromPoint(e.GetPosition(_editor.TextArea.TextView));
        if (pos == null) return;

        var offset = _editor.Document.GetOffset(pos.Value.Location);
        var detectedNumber = NumberDetector.GetNumberAtOffset(_editor.Document, offset);

        if (detectedNumber != null && Keyboard.Modifiers == ModifierKeys.Alt)
        {
            // Alt+Click to show slider immediately
            ShowSlider(detectedNumber);
            e.Handled = true;
        }
    }

    private void Editor_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Escape closes popup
        if (e.Key == Key.Escape && _activePopup != null)
        {
            ClosePopup();
        }
    }

    private void HoverTimer_Tick(object? sender, EventArgs e)
    {
        _hoverTimer.Stop();

        if (_hoveredNumber != null && _activePopup == null)
        {
            ShowSlider(_hoveredNumber);
        }
    }

    private void ShowSlider(DetectedNumber number)
    {
        ClosePopup();

        // Calculate position for the popup
        var visualPosition = GetVisualPosition(number);
        if (visualPosition == null) return;

        // Create and show the popup
        _activePopup = new SliderPopup(number, _editor);
        _activePopup.ValueChanged += Popup_ValueChanged;
        _activePopup.ValueChangeCompleted += Popup_ValueChangeCompleted;
        _activePopup.CloseRequested += Popup_CloseRequested;

        // Position the popup below the number
        var screenPos = _editor.TextArea.TextView.PointToScreen(visualPosition.Value);
        _activePopup.Show(screenPos);
    }

    private Point? GetVisualPosition(DetectedNumber number)
    {
        var line = _editor.Document.GetLineByNumber(number.Line);
        var visualLine = _editor.TextArea.TextView.GetVisualLine(number.Line);

        if (visualLine == null) return null;

        var textLine = visualLine.GetTextLine(number.Column - 1);
        if (textLine == null) return null;

        var x = visualLine.GetVisualColumn(number.StartOffset - line.Offset);
        var visualColumn = visualLine.GetVisualPosition(x, VisualYPosition.LineBottom);

        return new Point(visualColumn.X, visualColumn.Y);
    }

    private void Popup_ValueChanged(object? sender, SliderValueChangedEventArgs e)
    {
        // Update the document
        UpdateDocumentValue(e.Number, e.NewValue);

        // Fire event for hot-reload
        ValueChanged?.Invoke(this, e);
    }

    private void Popup_ValueChangeCompleted(object? sender, SliderValueChangedEventArgs e)
    {
        ValueChangeCompleted?.Invoke(this, e);
    }

    private void Popup_CloseRequested(object? sender, EventArgs e)
    {
        ClosePopup();
    }

    private void UpdateDocumentValue(DetectedNumber number, double newValue)
    {
        var formattedValue = NumberDetector.FormatNumber(newValue, number);

        // Replace the text in the document
        _editor.Document.Replace(number.StartOffset, number.Length, formattedValue);

        // Update the number's offsets if the length changed
        var lengthDiff = formattedValue.Length - number.Length;
        if (lengthDiff != 0 && _activePopup != null)
        {
            _activePopup.UpdateNumber(new DetectedNumber
            {
                StartOffset = number.StartOffset,
                EndOffset = number.StartOffset + formattedValue.Length,
                OriginalText = formattedValue,
                Value = newValue,
                IsFloat = number.IsFloat,
                HasFloatSuffix = number.HasFloatSuffix,
                HasDoubleSuffix = number.HasDoubleSuffix,
                Line = number.Line,
                Column = number.Column,
                SliderConfig = number.SliderConfig,
                Context = number.Context
            });
        }
    }

    private void ClosePopup()
    {
        if (_activePopup != null)
        {
            _activePopup.ValueChanged -= Popup_ValueChanged;
            _activePopup.ValueChangeCompleted -= Popup_ValueChangeCompleted;
            _activePopup.CloseRequested -= Popup_CloseRequested;
            _activePopup.Close();
            _activePopup = null;
        }
    }
}

/// <summary>
/// Event arguments for slider value changes
/// </summary>
public class SliderValueChangedEventArgs : EventArgs
{
    public DetectedNumber Number { get; init; } = null!;
    public double OldValue { get; init; }
    public double NewValue { get; init; }
}

/// <summary>
/// The popup window containing the slider control
/// </summary>
public class SliderPopup : Window
{
    private DetectedNumber _number;
    private readonly TextEditor _editor;
    private readonly Slider _slider;
    private readonly TextBox _valueTextBox;
    private readonly TextBlock _labelTextBlock;
    private readonly TextBlock _rangeTextBlock;
    private double _originalValue;
    private bool _isDragging;
    private bool _suppressTextChangedEvent;

    public event EventHandler<SliderValueChangedEventArgs>? ValueChanged;
    public event EventHandler<SliderValueChangedEventArgs>? ValueChangeCompleted;
    public event EventHandler? CloseRequested;

    public SliderPopup(DetectedNumber number, TextEditor editor)
    {
        _number = number;
        _editor = editor;
        _originalValue = number.Value;

        // Window style
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight;

        // Create the visual content
        var border = new Border
        {
            Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x30)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(12, 8, 12, 8),
            Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 12,
                ShadowDepth = 4,
                Opacity = 0.5
            }
        };

        var mainStack = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        // Label row (if available)
        var config = number.SliderConfig;
        if (!string.IsNullOrEmpty(config?.Label))
        {
            _labelTextBlock = new TextBlock
            {
                Text = config.Label,
                Foreground = new SolidColorBrush(Color.FromRgb(0x4E, 0xC9, 0xB0)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4)
            };
            mainStack.Children.Add(_labelTextBlock);
        }
        else
        {
            _labelTextBlock = new TextBlock();
        }

        // Value and slider row
        var sliderRow = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 4)
        };

        // Value text box
        _valueTextBox = new TextBox
        {
            Text = number.Value.ToString(number.IsFloat ? "F2" : "F0", CultureInfo.InvariantCulture),
            Width = 60,
            Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0xE0, 0xE0)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x43, 0x45, 0x4A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 2, 4, 2),
            FontFamily = new FontFamily("JetBrains Mono, Consolas"),
            FontSize = 12,
            TextAlignment = TextAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center
        };
        _valueTextBox.TextChanged += ValueTextBox_TextChanged;
        _valueTextBox.PreviewKeyDown += ValueTextBox_PreviewKeyDown;
        DockPanel.SetDock(_valueTextBox, Dock.Right);
        sliderRow.Children.Add(_valueTextBox);

        // Slider
        var minVal = config?.MinValue ?? 0;
        var maxVal = config?.MaxValue ?? 100;

        _slider = new Slider
        {
            Minimum = minVal,
            Maximum = maxVal,
            Value = Math.Clamp(number.Value, minVal, maxVal),
            Width = 150,
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = CreateSliderStyle()
        };

        if (config?.Step > 0)
        {
            _slider.TickFrequency = config.Step;
            _slider.IsSnapToTickEnabled = true;
        }

        _slider.ValueChanged += Slider_ValueChanged;
        _slider.PreviewMouseLeftButtonDown += Slider_PreviewMouseLeftButtonDown;
        _slider.PreviewMouseLeftButtonUp += Slider_PreviewMouseLeftButtonUp;

        sliderRow.Children.Add(_slider);
        mainStack.Children.Add(sliderRow);

        // Range display
        _rangeTextBlock = new TextBlock
        {
            Text = $"{minVal} - {maxVal}",
            Foreground = new SolidColorBrush(Color.FromRgb(0x6F, 0x73, 0x7A)),
            FontSize = 10,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        mainStack.Children.Add(_rangeTextBlock);

        // Quick buttons row for common adjustments
        var buttonRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 6, 0, 0)
        };

        var step = config?.Step ?? 1;
        AddQuickButton(buttonRow, "-10", -10 * step);
        AddQuickButton(buttonRow, "-1", -step);
        AddQuickButton(buttonRow, "+1", step);
        AddQuickButton(buttonRow, "+10", 10 * step);

        mainStack.Children.Add(buttonRow);

        border.Child = mainStack;
        Content = border;

        // Close when clicking outside
        Deactivated += (s, e) =>
        {
            if (!_isDragging)
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        };

        // Support mouse wheel
        MouseWheel += SliderPopup_MouseWheel;
    }

    private void AddQuickButton(StackPanel panel, string text, double delta)
    {
        var button = new Button
        {
            Content = text,
            Width = 32,
            Height = 22,
            Margin = new Thickness(2, 0, 2, 0),
            Background = new SolidColorBrush(Color.FromRgb(0x3C, 0x3F, 0x41)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xBC, 0xBE, 0xC4)),
            BorderThickness = new Thickness(0),
            FontSize = 10,
            Cursor = Cursors.Hand
        };

        button.Click += (s, e) =>
        {
            var newValue = Math.Clamp(_slider.Value + delta, _slider.Minimum, _slider.Maximum);
            _slider.Value = newValue;
        };

        panel.Children.Add(button);
    }

    private void SliderPopup_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var step = _number.SliderConfig?.Step ?? 1;
        var delta = e.Delta > 0 ? step : -step;
        var newValue = Math.Clamp(_slider.Value + delta, _slider.Minimum, _slider.Maximum);
        _slider.Value = newValue;
        e.Handled = true;
    }

    private Style CreateSliderStyle()
    {
        var style = new Style(typeof(Slider));

        // Use system default but override colors
        style.Setters.Add(new Setter(Slider.ForegroundProperty,
            new SolidColorBrush(Color.FromRgb(0x4B, 0x6E, 0xAF))));

        return style;
    }

    public void Show(Point screenPosition)
    {
        Left = screenPosition.X - 20;
        Top = screenPosition.Y + 5;
        Show();
        _slider.Focus();
    }

    public void UpdateNumber(DetectedNumber newNumber)
    {
        _number = newNumber;
    }

    private void Slider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDragging = true;
        _originalValue = _number.Value;
    }

    private void Slider_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;

        // Fire completion event
        if (Math.Abs(_slider.Value - _originalValue) > 0.0001)
        {
            ValueChangeCompleted?.Invoke(this, new SliderValueChangedEventArgs
            {
                Number = _number,
                OldValue = _originalValue,
                NewValue = _slider.Value
            });
        }
    }

    private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        _suppressTextChangedEvent = true;
        _valueTextBox.Text = e.NewValue.ToString(_number.IsFloat ? "F2" : "F0", CultureInfo.InvariantCulture);
        _suppressTextChangedEvent = false;

        // Fire value changed event for live update
        ValueChanged?.Invoke(this, new SliderValueChangedEventArgs
        {
            Number = _number,
            OldValue = e.OldValue,
            NewValue = e.NewValue
        });
    }

    private void ValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChangedEvent) return;

        if (double.TryParse(_valueTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            var clampedValue = Math.Clamp(value, _slider.Minimum, _slider.Maximum);
            if (Math.Abs(_slider.Value - clampedValue) > 0.0001)
            {
                _slider.Value = clampedValue;
            }
        }
    }

    private void ValueTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (double.TryParse(_valueTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                _slider.Value = Math.Clamp(value, _slider.Minimum, _slider.Maximum);

                ValueChangeCompleted?.Invoke(this, new SliderValueChangedEventArgs
                {
                    Number = _number,
                    OldValue = _originalValue,
                    NewValue = _slider.Value
                });

                _originalValue = _slider.Value;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }
}

/// <summary>
/// Visual element renderer that highlights numbers with slider support
/// </summary>
public class NumberHighlightRenderer : IBackgroundRenderer
{
    private readonly TextEditor _editor;
    private static readonly SolidColorBrush HighlightBrush = new(Color.FromArgb(30, 75, 110, 175));
    private static readonly Pen HighlightPen = new(new SolidColorBrush(Color.FromArgb(80, 75, 110, 175)), 1);

    public NumberHighlightRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!textView.VisualLinesValid) return;

        var numbers = NumberDetector.DetectNumbers(_editor.Document);

        foreach (var number in numbers)
        {
            var segment = new TextSegment
            {
                StartOffset = number.StartOffset,
                Length = number.Length
            };

            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                drawingContext.DrawRoundedRectangle(
                    HighlightBrush,
                    HighlightPen,
                    new Rect(rect.Location, rect.Size),
                    2, 2);
            }
        }
    }
}
