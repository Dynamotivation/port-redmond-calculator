using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Views.Graphing;

public partial class GraphingCalculatorView : UserControl
{
    private const double ColumnsThreshold = 760;
    private bool _showsEquationPanelOnNarrow;
    private bool _showsInverseKeypad;
    private bool _plotEventsAttached;
    private TextBox? _activeTextBox;

    public GraphingCalculatorView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout(Bounds.Width);
        AttachedToVisualTree += (_, _) =>
        {
            if (!_plotEventsAttached)
            {
                Plot.ViewportChanged += Plot_OnViewportChanged;
                Plot.TraceChanged += Plot_OnTraceChanged;
                _plotEventsAttached = true;
            }
            UpdateResponsiveLayout(Bounds.Width);
            UpdateBoundsEditors();
        };
    }

    private GraphingViewModel? Graphing => (DataContext as CalculatorViewModel)?.Graphing;

    public void FocusEquationInput()
    {
        var textBox = EquationPanel.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
        if (textBox is not null)
        {
            _activeTextBox = textBox;
            textBox.Focus();
        }
    }

    public void FocusDefault()
    {
        UpdateResponsiveLayout(Bounds.Width);
        if (EquationPanel.IsVisible)
        {
            FocusEquationInput();
        }
        else
        {
            FocusGraph();
        }
    }

    public void FocusGraph() => Plot.Focus();
    public void ResetView() => Plot.ResetView();

    private void UpdateResponsiveLayout(double width)
    {
        var isNarrow = width < ColumnsThreshold;
        if (!isNarrow)
        {
            var editorWidth = Math.Clamp(width / 3, 300, 420);
            ResponsiveLayout.ColumnDefinitions =
            [
                new ColumnDefinition(1, GridUnitType.Star),
                new ColumnDefinition(editorWidth, GridUnitType.Pixel),
            ];
            Grid.SetColumn(EquationPanel, 1);
            GraphPanel.IsVisible = true;
            EquationPanel.IsVisible = true;
            return;
        }

        ResponsiveLayout.ColumnDefinitions = [new ColumnDefinition(1, GridUnitType.Star)];
        Grid.SetColumn(EquationPanel, 0);
        GraphPanel.IsVisible = !_showsEquationPanelOnNarrow;
        EquationPanel.IsVisible = _showsEquationPanelOnNarrow;

        GraphOptionsPanel.Width = Math.Max(0, width - 16);
        GraphOptionsPanel.MaxHeight = Math.Max(100, Bounds.Height - 16);
    }

    public bool ShowsEquationPanelOnNarrow => _showsEquationPanelOnNarrow;

    public void ShowCompactGraph()
    {
        _showsEquationPanelOnNarrow = false;
        UpdateResponsiveLayout(Bounds.Width);
        Plot.Focus();
    }

    public void ShowCompactEquation()
    {
        _showsEquationPanelOnNarrow = true;
        UpdateResponsiveLayout(Bounds.Width);
        Dispatcher.UIThread.Post(FocusEquationInput, DispatcherPriority.Input);
    }

    private void ZoomIn_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomIn();
    private void ZoomOut_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomOut();

    private void ResetView_OnClick(object? sender, RoutedEventArgs e)
    {
        Plot.ResetView();
        UpdateBoundsEditors();
    }

    private void TraceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Plot.SetTracing(TraceButton.IsChecked == true);
        var label = Plot.IsTracing ? "Stop tracing" : "Start tracing";
        AutomationProperties.SetName(TraceButton, label);
        ToolTip.SetTip(TraceButton, label);
    }

    private void Plot_OnTraceChanged(object? sender, EventArgs e)
    {
        TraceLiveRegion.IsVisible = Plot.IsTracing && !string.IsNullOrEmpty(Plot.TraceText);
        AutomationProperties.SetName(TraceLiveRegion, Plot.TraceText);
    }

    private void GraphOptionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GraphOptionsPanel.IsVisible = !GraphOptionsPanel.IsVisible;
        if (GraphOptionsPanel.IsVisible)
        {
            UpdateBoundsEditors();
        }
    }

    private void CloseGraphOptionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        GraphOptionsPanel.IsVisible = false;
        UpdateResponsiveLayout(Bounds.Width);
    }

    private void Plot_OnViewportChanged(object? sender, EventArgs e) => UpdateBoundsEditors();

    private void UpdateBoundsEditors()
    {
        XMinimumBox.Text = FormatBound(Plot.XMinimum);
        XMaximumBox.Text = FormatBound(Plot.XMaximum);
        YMinimumBox.Text = FormatBound(Plot.YMinimum);
        YMaximumBox.Text = FormatBound(Plot.YMaximum);
    }

    private void BoundsBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (TryParseBound(XMinimumBox.Text, out var xMinimum)
            && TryParseBound(XMaximumBox.Text, out var xMaximum)
            && TryParseBound(YMinimumBox.Text, out var yMinimum)
            && TryParseBound(YMaximumBox.Text, out var yMaximum))
        {
            Plot.SetViewport(xMinimum, xMaximum, yMinimum, yMaximum);
        }
        UpdateBoundsEditors();
    }

    private void GraphTheme_OnChanged(object? sender, RoutedEventArgs e)
    {
        if (AlwaysLightThemeButton is null || Plot is null)
        {
            return;
        }

        var useDark = AlwaysLightThemeButton.IsChecked != true
            && ActualThemeVariant == ThemeVariant.Dark;
        Plot.PlotBackground = new SolidColorBrush(Color.Parse(useDark ? "#202020" : "#FFFFFF"));
        Plot.GridBrush = new SolidColorBrush(Color.Parse(useDark ? "#28FFFFFF" : "#1F000000"));
        Plot.AxisBrush = new SolidColorBrush(Color.Parse(useDark ? "#C8FFFFFF" : "#8A000000"));
        GraphPanel.Background = Plot.PlotBackground;
        Plot.InvalidateVisual();
    }

    private void LineWidthBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Graphing is null || LineWidthBox.SelectedItem is not ComboBoxItem item
            || !double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
        {
            return;
        }

        foreach (var equation in Graphing.Equations)
        {
            equation.LineWidth = width;
        }
    }

    private void EquationTextBox_OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            _activeTextBox = textBox;
            SetEquationCardFocused(textBox, true);
            if (textBox.DataContext is GraphEquationViewModel equation && Graphing is not null)
            {
                Graphing.SelectedEquation = equation;
            }
        }
    }

    private void EquationTextBox_OnLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (textBox.IsFocused)
                {
                    return;
                }
                CommitEquation(textBox);
                SetEquationCardFocused(textBox, false);
            }, DispatcherPriority.Background);
        }
    }

    private static void SetEquationCardFocused(TextBox textBox, bool isFocused)
    {
        var card = textBox.GetVisualAncestors()
            .OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("graphEquationCard"));
        card?.Classes.Set("focused", isFocused);
    }

    private void EquationTextBox_OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }
        SubmitEquation(textBox);
        e.Handled = true;
    }

    private void SubmitEquation(TextBox? textBox = null)
    {
        textBox ??= _activeTextBox;
        if (textBox is null)
        {
            return;
        }

        var placeholderAdded = CommitEquation(textBox);
        if (placeholderAdded)
        {
            Dispatcher.UIThread.Post(FocusLastEquation, DispatcherPriority.Input);
        }
    }

    private bool CommitEquation(TextBox textBox)
    {
        if (textBox.DataContext is not GraphEquationViewModel equation || Graphing is null)
        {
            return false;
        }

        var placeholderAdded = Graphing.CommitEquation(equation);
        if (placeholderAdded && Graphing.Equations.LastOrDefault() is { } newEquation)
        {
            newEquation.LineWidth = GetSelectedLineWidth();
        }
        return placeholderAdded;
    }

    private void FocusLastEquation()
    {
        var textBox = EquationPanel.GetVisualDescendants().OfType<TextBox>().LastOrDefault();
        if (textBox is not null)
        {
            _activeTextBox = textBox;
            textBox.Focus();
        }
    }

    private void InputButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var token = button.Tag?.ToString() ?? string.Empty;
        if (token == "submit-input")
        {
            SubmitEquation();
            return;
        }
        if (_activeTextBox is null)
        {
            FocusEquationInput();
        }
        if (_activeTextBox is null)
        {
            return;
        }
        if (token == "clear-input")
        {
            _activeTextBox.Text = string.Empty;
            _activeTextBox.Focus();
            return;
        }
        if (token == "backspace-input")
        {
            Backspace(_activeTextBox);
            _activeTextBox.Focus();
            return;
        }

        token = ApplyTrigModifiers(token);
        InsertText(_activeTextBox, token);
        _activeTextBox.Focus();
    }

    private string ApplyTrigModifiers(string token)
    {
        var function = token.TrimEnd('(');
        if (function is not ("sin" or "cos" or "tan" or "sec" or "csc" or "cot"))
        {
            return token;
        }

        if (TrigHypButton.IsChecked == true)
        {
            function += "h";
        }
        if (TrigSecondButton.IsChecked == true)
        {
            function = "a" + function;
        }
        return function + "(";
    }

    private void SecondButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _showsInverseKeypad = !_showsInverseKeypad;
        SecondButton.Classes.Set("selected", _showsInverseKeypad);
        PrimaryScientificKeys.IsVisible = !_showsInverseKeypad;
        InverseScientificKeys.IsVisible = _showsInverseKeypad;
    }

    private void TrigModifier_OnClick(object? sender, RoutedEventArgs e)
    {
        UpdateTrigButtonLabels();
    }

    private void UpdateTrigButtonLabels()
    {
        var inverse = TrigSecondButton.IsChecked == true;
        var hyperbolic = TrigHypButton.IsChecked == true;
        var buttons = new[]
        {
            (TrigSinButton, "sin", "sine"),
            (TrigCosButton, "cos", "cosine"),
            (TrigTanButton, "tan", "tangent"),
            (TrigSecButton, "sec", "secant"),
            (TrigCscButton, "csc", "cosecant"),
            (TrigCotButton, "cot", "cotangent"),
        };
        foreach (var (button, shortName, longName) in buttons)
        {
            button.Content = $"{shortName}{(hyperbolic ? "h" : string.Empty)}{(inverse ? "⁻¹" : string.Empty)}";
            var automationName = inverse
                ? hyperbolic ? $"Hyperbolic arc {longName}" : $"Arc {longName}"
                : hyperbolic ? $"Hyperbolic {longName}" : char.ToUpperInvariant(longName[0]) + longName[1..];
            AutomationProperties.SetName(button, automationName);
        }
    }

    private void EquationTextBox_OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.DataContext is not GraphEquationViewModel equation)
        {
            return;
        }

        _activeTextBox = textBox;
        var menu = new ContextMenu();
        menu.Items.Add(CreateMenuItem("Cut", async (_, _) =>
        {
            await CopySelection(textBox);
            DeleteSelection(textBox);
        }, textBox.SelectionEnd > textBox.SelectionStart));
        menu.Items.Add(CreateMenuItem("Copy", async (_, _) => await CopySelection(textBox),
            textBox.SelectionEnd > textBox.SelectionStart));
        menu.Items.Add(CreateMenuItem("Paste", async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            var pasted = clipboard is null ? null : await clipboard.TryGetTextAsync();
            if (!string.IsNullOrEmpty(pasted))
            {
                InsertText(textBox, pasted);
            }
        }));
        menu.Items.Add(CreateMenuItem("Undo", (_, _) => textBox.Undo(), textBox.CanUndo));
        menu.Items.Add(CreateMenuItem("Select all", (_, _) => textBox.SelectAll()));
        menu.Items.Add(new Separator());
        menu.Items.Add(CreateMenuItem("Analyze function", (_, _) => { }, false));
        menu.Items.Add(CreateMenuItem("Change equation style", (_, _) => ShowLineOptions(textBox, equation)));
        menu.Items.Add(CreateMenuItem("Remove equation", (_, _) =>
            Graphing?.RemoveEquationCommand.Execute(equation)));
        textBox.ContextMenu = menu;
        menu.Open(textBox);
        e.Handled = true;
    }

    private void StyleEquation_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GraphEquationViewModel equation)
        {
            ShowLineOptions(button, equation);
        }
    }

    private void RemoveEquation_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GraphEquationViewModel equation }
            && Graphing?.RemoveEquationCommand.CanExecute(equation) == true)
        {
            Graphing.RemoveEquationCommand.Execute(equation);
        }
    }

    private void ShowLineOptions(Control placementTarget, GraphEquationViewModel equation)
    {
        var swatches = new UniformGrid
        {
            Columns = 7,
            Rows = 2,
            Margin = new Thickness(4, 0),
        };
        var colorNames = new[]
        {
            "Blue", "Green", "Red", "Gold", "Cyan", "Mint green", "Magenta",
            "Orange", "Violet", "Dark green", "Plum", "Brown", "Charcoal", "Black",
        };
        var pickerOrder = new[] { 0, 4, 8, 1, 5, 9, 12, 2, 6, 10, 3, 7, 11, 13 };
        foreach (var index in pickerOrder)
        {
            var color = GraphingViewModel.EquationColors[index];
            var swatch = new RadioButton
            {
                GroupName = "EquationColor",
                IsChecked = string.Equals(equation.Color, color, StringComparison.OrdinalIgnoreCase),
                Background = new SolidColorBrush(Color.Parse(color)),
                Content = string.Empty,
            };
            swatch.Classes.Add("graphColorSwatch");
            AutomationProperties.SetName(swatch, colorNames[index]);
            swatch.IsCheckedChanged += (_, _) =>
            {
                if (swatch.IsChecked == true)
                {
                    equation.Color = color;
                }
            };
            swatches.Children.Add(swatch);
        }

        var styleChoices = new UniformGrid { Columns = 3 };
        for (var index = 0; index < 3; index++)
        {
            var lineStyle = (GraphLineStyle)index;
            var styleChoice = new RadioButton
            {
                GroupName = "EquationLineStyle",
                IsChecked = equation.LineStyle == lineStyle,
                Content = CreateLineStylePreview(lineStyle),
            };
            styleChoice.Classes.Add("graphLineStyle");
            AutomationProperties.SetName(
                styleChoice,
                lineStyle switch
                {
                    GraphLineStyle.Dash => "Dashed line",
                    GraphLineStyle.Dot => "Short segmented line",
                    _ => "Solid line",
                });
            styleChoice.IsCheckedChanged += (_, _) =>
            {
                if (styleChoice.IsChecked == true)
                {
                    equation.LineStyle = lineStyle;
                }
            };
            styleChoices.Children.Add(styleChoice);
        }

        var content = new StackPanel
        {
            Width = 316,
            Spacing = 10,
            Children =
            {
                new TextBlock { Text = "Line options", FontSize = 20, FontWeight = FontWeight.SemiBold },
                new TextBlock { Text = "Color", Margin = new Thickness(0, 5, 0, 0) },
                swatches,
                new TextBlock { Text = "Style", Margin = new Thickness(0, 5, 0, 0) },
                styleChoices,
            },
        };
        var flyout = new Flyout { Content = content, Placement = PlacementMode.Left };
        flyout.ShowAt(placementTarget);
    }

    private static Control CreateLineStylePreview(GraphLineStyle lineStyle)
    {
        if (lineStyle == GraphLineStyle.Solid)
        {
            return new Border
            {
                Width = 72,
                Height = 2,
                Background = new SolidColorBrush(Color.Parse("#808080")),
            };
        }

        var segments = new StackPanel
        {
            Width = 72,
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var segmentWidth = lineStyle == GraphLineStyle.Dash ? 10d : 5d;
        var segmentHeight = lineStyle == GraphLineStyle.Dash ? 2d : 1d;
        var segmentCount = lineStyle == GraphLineStyle.Dash ? 6 : 9;
        for (var index = 0; index < segmentCount; index++)
        {
            segments.Children.Add(new Border
            {
                Width = segmentWidth,
                Height = segmentHeight,
                Margin = new Thickness(0, 0, 3, 0),
                Background = new SolidColorBrush(Color.Parse("#808080")),
            });
        }
        return segments;
    }

    private double GetSelectedLineWidth() =>
        LineWidthBox.SelectedItem is ComboBoxItem item
        && double.TryParse(item.Tag?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var width)
            ? width
            : 2;

    private static MenuItem CreateMenuItem(
        string header,
        EventHandler<RoutedEventArgs> handler,
        bool isEnabled = true)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled };
        item.Click += handler;
        return item;
    }

    private async System.Threading.Tasks.Task CopySelection(TextBox textBox)
    {
        if (textBox.SelectionEnd <= textBox.SelectionStart)
        {
            return;
        }
        var selected = (textBox.Text ?? string.Empty)
            .Substring(textBox.SelectionStart, textBox.SelectionEnd - textBox.SelectionStart);
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(selected);
        }
    }

    private static void DeleteSelection(TextBox textBox)
    {
        var text = textBox.Text ?? string.Empty;
        var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
        var length = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);
        if (length > 0)
        {
            textBox.Text = text.Remove(start, length);
            textBox.CaretIndex = start;
        }
    }

    private static void InsertText(TextBox textBox, string value)
    {
        var text = textBox.Text ?? string.Empty;
        var start = Math.Min(textBox.SelectionStart, textBox.SelectionEnd);
        var length = Math.Abs(textBox.SelectionEnd - textBox.SelectionStart);
        textBox.Text = text.Remove(start, length).Insert(start, value);
        textBox.CaretIndex = start + value.Length;
    }

    private static void Backspace(TextBox textBox)
    {
        if (textBox.SelectionEnd != textBox.SelectionStart)
        {
            DeleteSelection(textBox);
            return;
        }
        if (textBox.CaretIndex <= 0)
        {
            return;
        }
        var text = textBox.Text ?? string.Empty;
        var index = textBox.CaretIndex - 1;
        textBox.Text = text.Remove(index, 1);
        textBox.CaretIndex = index;
    }

    private static bool TryParseBound(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);

    private static string FormatBound(double value) =>
        value.ToString("0.###############", CultureInfo.InvariantCulture);
}
