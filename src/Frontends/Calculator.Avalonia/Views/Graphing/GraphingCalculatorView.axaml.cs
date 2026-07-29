using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
    private const double ColumnsThreshold = 800;
    private bool _showsEquationPanelOnNarrow;
    private bool _showsInverseKeypad;
    private bool _plotEventsAttached;
    private bool _graphOptionsDismissedDuringPointerClick;
    private TextBox? _activeTextBox;

    public GraphingCalculatorView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout(Bounds.Width);
        GraphOptionsPopup.Closed += (_, _) =>
            _graphOptionsDismissedDuringPointerClick = true;
        AddHandler(
            PointerReleasedEvent,
            (_, _) => _graphOptionsDismissedDuringPointerClick = false,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        AttachedToVisualTree += (_, _) =>
        {
            if (!_plotEventsAttached)
            {
                Plot.ViewportChanged += Plot_OnViewportChanged;
                Plot.TraceChanged += Plot_OnTraceChanged;
                Plot.ManualAdjustmentChanged += Plot_OnManualAdjustmentChanged;
                _plotEventsAttached = true;
            }
            UpdateResponsiveLayout(Bounds.Width);
            UpdateBoundsEditors();
            UpdateGraphViewButton();
            GraphTheme_OnChanged(null, new RoutedEventArgs());
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
            FocusEquationSide();
        }
        else
        {
            FocusGraph();
        }
    }

    public void FocusGraph() => Plot.Focus();

    public void ZoomIn() => Plot.ZoomIn();

    public void ZoomOut() => Plot.ZoomOut();

    public bool MoveTrace(string direction, bool fine) => Plot.MoveTrace(direction, fine);

    public bool StopTracing()
    {
        if (!Plot.IsTracing)
        {
            return false;
        }

        TraceButton.IsChecked = false;
        Plot.SetTracing(false);
        UpdateTraceButton();
        return true;
    }

    public bool SubmitActiveEquation()
    {
        if (_activeTextBox is null)
        {
            return false;
        }

        SubmitEquation(_activeTextBox);
        return true;
    }

    public bool SubmitGraphSetting()
    {
        BoundsBox_OnLostFocus(null, new RoutedEventArgs());
        FocusGraph();
        return true;
    }

    public void ResetView()
    {
        Plot.RefreshViewAutomatically();
        UpdateGraphViewButton();
    }

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

        GraphOptionsPanel.Width = Math.Min(318, Math.Max(0, width - 16));
        GraphOptionsPanel.MaxHeight = Math.Min(468, Math.Max(100, Bounds.Height - 64));
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
        Dispatcher.UIThread.Post(FocusEquationSide, DispatcherPriority.Input);
    }

    private void FocusEquationSide()
    {
        if (Graphing?.IsAnalysisVisible == true)
        {
            AnalysisBackButton.Focus();
            return;
        }
        FocusEquationInput();
    }

    private void ZoomIn_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomIn();
    private void ZoomOut_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomOut();

    private void ResetView_OnClick(object? sender, RoutedEventArgs e)
    {
        Plot.RefreshViewAutomatically();
        UpdateGraphViewButton();
        UpdateBoundsEditors();
    }

    private void GraphViewButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (GraphViewButton.IsChecked == true)
        {
            Plot.SetManualAdjustment(true);
        }
        else
        {
            Plot.RefreshViewAutomatically();
        }
        UpdateGraphViewButton();
    }

    private void TraceButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Plot.SetTracing(TraceButton.IsChecked == true);
        UpdateTraceButton();
    }

    private void Plot_OnTraceChanged(object? sender, EventArgs e)
    {
        UpdateTraceButton();
        TraceLiveRegion.IsVisible = Plot.IsTracing && !string.IsNullOrEmpty(Plot.TraceText);
        AutomationProperties.SetName(TraceLiveRegion, Plot.TraceText);
    }

    private void UpdateTraceButton()
    {
        TraceButton.IsChecked = Plot.IsTracing;
        var label = Plot.IsTracing
            ? Graphing?.Strings.StopTracingTooltip ?? "Stop tracing"
            : Graphing?.Strings.StartTracingTooltip ?? "Start tracing";
        AutomationProperties.SetName(TraceButton, label);
        ToolTip.SetTip(TraceButton, label);
    }

    private void GraphOptionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_graphOptionsDismissedDuringPointerClick)
        {
            _graphOptionsDismissedDuringPointerClick = false;
            return;
        }

        GraphOptionsPopup.IsOpen = !GraphOptionsPopup.IsOpen;
        if (GraphOptionsPopup.IsOpen)
        {
            UpdateBoundsEditors();
        }
    }

    private void Plot_OnViewportChanged(object? sender, EventArgs e) => UpdateBoundsEditors();

    private void Plot_OnManualAdjustmentChanged(object? sender, EventArgs e) =>
        UpdateGraphViewButton();

    private void UpdateGraphViewButton()
    {
        GraphViewButton.IsChecked = Plot.IsManualAdjustment;
        AutomaticViewGlyph.IsVisible = !Plot.IsManualAdjustment;
    }

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
                equation.IsEditing = true;
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
                if (textBox.DataContext is GraphEquationViewModel equation)
                {
                    equation.IsEditing = false;
                }
                SetEquationCardFocused(textBox, false);
            }, DispatcherPriority.Background);
        }
    }

    private void ClearEditingEquation_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GraphEquationViewModel equation })
        {
            var editor = this.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            if (editor is not null)
            {
                editor.Text = string.Empty;
                editor.CaretIndex = 0;
            }
            equation.DraftExpression = string.Empty;
            equation.Expression = string.Empty;
            editor?.Focus();
        }
    }

    private static void SetEquationCardFocused(TextBox textBox, bool isFocused)
    {
        var card = textBox.GetVisualAncestors()
            .OfType<Border>()
            .FirstOrDefault(border => border.Classes.Contains("graphEquationCard"));
        card?.Classes.Set("focused", isFocused);
    }

    private void FormattedEquation_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: GraphEquationViewModel equation } formattedEquation)
        {
            return;
        }
        var properties = e.GetCurrentPoint(formattedEquation).Properties;
        if (!properties.IsRightButtonPressed)
        {
            return;
        }

        ActivateLinearEditor(
            equation,
            equation.DraftExpression.Length,
            editor => editor.ContextMenu?.Open(editor));
        e.Handled = true;
    }

    private void FormattedEquation_OnGotFocus(object? sender, FocusChangedEventArgs e)
    {
        if (sender is not EditableMathView { DataContext: GraphEquationViewModel equation })
        {
            return;
        }

        if (Graphing is not null)
        {
            Graphing.SelectedEquation = equation;
        }
        _activeTextBox = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(textBox =>
                textBox.Name == "EquationExpressionTextBox"
                && ReferenceEquals(textBox.DataContext, equation));
    }

    private void FormattedEquation_OnLinearEditRequested(
        object? sender,
        LinearMathEditRequestedEventArgs e)
    {
        if (sender is not EditableMathView { DataContext: GraphEquationViewModel equation })
        {
            return;
        }

        ActivateLinearEditor(equation, e.SuggestedCaretIndex, editor =>
        {
            switch (e.Action)
            {
                case LinearMathEditAction.InsertText:
                    InsertText(editor, e.Text);
                    break;
                case LinearMathEditAction.Backspace:
                    Backspace(editor);
                    break;
                case LinearMathEditAction.Delete when editor.CaretIndex < (editor.Text?.Length ?? 0):
                    editor.SelectionStart = editor.CaretIndex;
                    editor.SelectionEnd = editor.CaretIndex + 1;
                    DeleteSelection(editor);
                    break;
            }
        });
    }

    private void ActivateLinearEditor(
        GraphEquationViewModel equation,
        int caretIndex,
        Action<TextBox>? activated = null)
    {
        equation.IsEditing = true;
        Dispatcher.UIThread.Post(() =>
        {
            var editor = this.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            if (editor is null)
            {
                return;
            }
            _activeTextBox = editor;
            editor.Focus();
            editor.CaretIndex = Math.Clamp(caretIndex, 0, editor.Text?.Length ?? 0);
            activated?.Invoke(editor);
        }, DispatcherPriority.Input);
    }

    private void SubmitEquation(TextBox? textBox = null)
    {
        textBox ??= _activeTextBox;
        if (textBox is null)
        {
            return;
        }

        var placeholderAdded = CommitEquation(textBox);
        if (textBox.DataContext is GraphEquationViewModel equation)
        {
            equation.IsEditing = false;
            SetEquationCardFocused(textBox, false);
        }
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
        if (_activeTextBox.DataContext is GraphEquationViewModel { ShowFormattedExpression: true } equation)
        {
            equation.IsEditing = true;
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

    private void SelectorButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (_activeTextBox is null)
        {
            FocusEquationInput();
        }
        if (button.Flyout is { IsOpen: false } flyout)
        {
            flyout.ShowAt(button);
        }
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

    private void EquationTextBox_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.ContextMenu is not null)
        {
            return;
        }

        var menu = new ContextMenu();
        var strings = Graphing?.Strings;
        var cutItem = CreateMenuItem(strings?.CutEquationText ?? "Cut", async (_, _) =>
        {
            await CopySelection(textBox);
            DeleteSelection(textBox);
        }, glyph: "\uE8C6");
        var copyItem = CreateMenuItem(
            strings?.CopyEquationText ?? "Copy",
            async (_, _) => await CopySelection(textBox),
            glyph: "\uE8C8");
        var pasteItem = CreateMenuItem(strings?.PasteEquationText ?? "Paste", async (_, _) =>
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            var pasted = clipboard is null ? null : await clipboard.TryGetTextAsync();
            if (!string.IsNullOrEmpty(pasted))
            {
                InsertText(textBox, pasted);
            }
        }, glyph: "\uE77F");
        var undoItem = CreateMenuItem(
            strings?.UndoEquationText ?? "Undo",
            (_, _) => textBox.Undo(),
            glyph: "\uE7A7");
        var selectAllItem = CreateMenuItem(
            strings?.SelectAllEquationText ?? "Select all",
            (_, _) => textBox.SelectAll(),
            glyph: "\uE8B3");
        var analyzeItem = CreateMenuItem(
            strings?.AnalyzeFunctionTooltip ?? "Analyze function",
            (_, _) =>
            {
                if (textBox.DataContext is GraphEquationViewModel equation)
                {
                    _ = OpenAnalysisAsync(equation);
                }
            },
            glyph: "\uE945");
        var styleItem = CreateMenuItem(
            strings?.ChangeEquationStyleTooltip ?? "Change equation style",
            (_, _) =>
            {
                if (textBox.DataContext is GraphEquationViewModel equation)
                {
                    ShowLineOptions(textBox, equation);
                }
            },
            glyph: "\uE790");
        var removeItem = CreateMenuItem(
            strings?.RemoveEquationTooltip ?? "Remove equation",
            (_, _) =>
            {
                if (textBox.DataContext is GraphEquationViewModel equation)
                {
                    Graphing?.RemoveEquationCommand.Execute(equation);
                }
            },
            glyph: "\uECC9");

        menu.Items.Add(cutItem);
        menu.Items.Add(copyItem);
        menu.Items.Add(pasteItem);
        menu.Items.Add(undoItem);
        menu.Items.Add(selectAllItem);
        menu.Items.Add(new Separator());
        menu.Items.Add(analyzeItem);
        menu.Items.Add(styleItem);
        menu.Items.Add(removeItem);
        menu.Opening += (_, _) =>
        {
            _activeTextBox = textBox;
            var hasSelection = textBox.SelectionEnd > textBox.SelectionStart;
            cutItem.IsEnabled = hasSelection;
            copyItem.IsEnabled = hasSelection;
            undoItem.IsEnabled = textBox.CanUndo;
            selectAllItem.IsEnabled = textBox.Text?.Length > 0;

            var equation = textBox.DataContext as GraphEquationViewModel;
            analyzeItem.IsEnabled = equation?.CanAnalyze == true;
            styleItem.IsEnabled = equation is { HasExpression: true, IsValid: true };
            removeItem.IsEnabled = equation is not null
                && Graphing?.RemoveEquationCommand.CanExecute(equation) == true;
        };
        textBox.ContextMenu = menu;
    }

    private void StyleEquation_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is GraphEquationViewModel equation)
        {
            ShowLineOptions(button, equation);
        }
    }

    private void AnalyzeEquation_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: GraphEquationViewModel equation })
        {
            _ = OpenAnalysisAsync(equation);
        }
    }

    private async Task OpenAnalysisAsync(GraphEquationViewModel equation)
    {
        if (Graphing is null)
        {
            return;
        }
        await Graphing.AnalyzeEquationAsync(equation);
        if (Graphing.IsAnalysisVisible && EquationPanel.IsVisible)
        {
            AnalysisBackButton.Focus();
        }
    }

    private void CloseAnalysis_OnClick(object? sender, RoutedEventArgs e)
    {
        Graphing?.CloseAnalysis();
        Dispatcher.UIThread.Post(FocusEquationInput, DispatcherPriority.Input);
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

        var styleBox = new ComboBox
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            IsEnabled = equation.CanCustomizeLineStyle,
        };
        for (var index = 0; index < 3; index++)
        {
            var lineStyle = (GraphLineStyle)index;
            var styleChoice = new ComboBoxItem
            {
                Tag = lineStyle,
                Content = CreateLineStylePreview(lineStyle),
            };
            AutomationProperties.SetName(
                styleChoice,
                lineStyle switch
                {
                    GraphLineStyle.Dash => "Dashed line",
                    GraphLineStyle.Dot => "Short segmented line",
                    _ => "Solid line",
                });
            styleBox.Items.Add(styleChoice);
        }
        styleBox.SelectedIndex = (int)equation.EffectiveLineStyle;
        styleBox.SelectionChanged += (_, _) =>
        {
            if (styleBox.SelectedItem is ComboBoxItem { Tag: GraphLineStyle lineStyle })
            {
                equation.LineStyle = lineStyle;
            }
        };

        var content = new StackPanel
        {
            Width = 316,
            Spacing = 10,
            Children =
            {
                new TextBlock
                {
                    Text = Graphing?.Strings.LineOptionsHeading ?? "Line options",
                    FontSize = 20,
                    FontWeight = FontWeight.Medium,
                },
                new TextBlock
                {
                    Text = Graphing?.Strings.ColorHeading ?? "Color",
                    Margin = new Thickness(0, 5, 0, 0),
                },
                swatches,
                new TextBlock
                {
                    Text = Graphing?.Strings.StyleHeading ?? "Style",
                    Margin = new Thickness(0, 5, 0, 0),
                },
                styleBox,
            },
        };
        var flyout = new Flyout { Content = content, Placement = PlacementMode.Bottom };
        flyout.ShowAt(placementTarget);
    }

    private static Control CreateLineStylePreview(GraphLineStyle lineStyle)
    {
        if (lineStyle == GraphLineStyle.Solid)
        {
            return new Border
            {
                Width = 200,
                Height = 2.5,
                Background = new SolidColorBrush(Color.Parse("#808080")),
            };
        }

        var segments = new StackPanel
        {
            Width = 200,
            Height = 20,
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var segmentWidth = lineStyle == GraphLineStyle.Dash ? 5d : 2.5d;
        var segmentGap = 2.5d;
        var segmentCount = lineStyle == GraphLineStyle.Dash ? 27 : 40;
        for (var index = 0; index < segmentCount; index++)
        {
            segments.Children.Add(new Border
            {
                Width = segmentWidth,
                Height = 2.5,
                CornerRadius = lineStyle == GraphLineStyle.Dot
                    ? new CornerRadius(1.25)
                    : default,
                Margin = new Thickness(0, 0, segmentGap, 0),
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

    private MenuItem CreateMenuItem(
        string header,
        EventHandler<RoutedEventArgs> handler,
        bool isEnabled = true,
        string? glyph = null)
    {
        var item = new MenuItem { Header = header, IsEnabled = isEnabled };
        if (!string.IsNullOrEmpty(glyph)
            && this.TryFindResource("CalculatorIconFont", out var fontResource)
            && fontResource is FontFamily fontFamily)
        {
            item.Icon = new TextBlock
            {
                FontFamily = fontFamily,
                FontSize = 14,
                Text = glyph,
            };
        }
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
