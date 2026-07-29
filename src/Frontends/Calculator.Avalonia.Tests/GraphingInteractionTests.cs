using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Managed;
using Calculator.Avalonia.Views.Graphing;

namespace Calculator.Avalonia.Tests;

internal static class GraphingInteractionTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("equation context menu is attached before right click", EquationContextMenuIsAttached),
        ("committed equation switches between typeset and linear editing", CommittedEquationSwitchesPresentation),
        ("editing clear button clears the committed equation", EditingClearButtonClearsEquation),
        ("invalid equation uses the native error presentation", InvalidEquationUsesNativeErrorPresentation),
        ("graphing selector flyouts insert tokens with square keys", SelectorFlyoutsInsertTokens),
        ("graph options popup is anchored and dismissible", GraphOptionsPopupIsAnchoredAndDismissible),
        ("graph shortcuts use shared contextual scopes", GraphShortcutsUseSharedContextualScopes),
        ("automatic graph view is a stable on off toggle", AutomaticGraphViewIsStableOnOffToggle),
        ("graph tracing matches Windows active cursor behavior", GraphTracingMatchesWindowsBehavior),
    ];

    private static void EquationContextMenuIsAttached()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();
            var viewModel = (CalculatorViewModel)window.DataContext!;

            var editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(textBox => textBox.Name == "EquationExpressionTextBox")
                ?? throw new InvalidOperationException("The equation editor is missing.");
            var menu = editor.ContextMenu
                ?? throw new InvalidOperationException("The equation editor has no context menu.");

            var headers = menu.Items
                .OfType<MenuItem>()
                .Select(item => item.Header?.ToString())
                .ToArray();
            Assert(
                headers.SequenceEqual(
                    [viewModel.Graphing.Strings.CutEquationText,
                     viewModel.Graphing.Strings.CopyEquationText,
                     viewModel.Graphing.Strings.PasteEquationText,
                     viewModel.Graphing.Strings.UndoEquationText,
                     viewModel.Graphing.Strings.SelectAllEquationText,
                     viewModel.Graphing.Strings.AnalyzeFunctionTooltip,
                     viewModel.Graphing.Strings.ChangeEquationStyleTooltip,
                     viewModel.Graphing.Strings.RemoveEquationTooltip]),
                $"Unexpected equation context menu: {string.Join(", ", headers)}");

            menu.Open(editor);
            Pump();
            Assert(menu.IsOpen, "The equation context menu should open for its editor.");
            menu.Close();
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void CommittedEquationSwitchesPresentation()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            equation.DraftExpression = "x^2/2";
            viewModel.Graphing.CommitEquation(equation);
            equation.IsEditing = false;
            Pump();

            var editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            var typeset = window.GetVisualDescendants()
                .OfType<EditableMathView>()
                .First(mathView => ReferenceEquals(mathView.DataContext, equation));
            Assert(
                editor.TryFindResource("GraphEquationFont", out var fontResource)
                && fontResource is FontFamily equationFont
                && editor.FontFamily.Equals(equationFont)
                && equationFont.Name.Contains("Latin Modern Math", StringComparison.Ordinal),
                "The linear equation editor should use the same Latin Modern Math face as the typeset renderer.");
            Assert(
                typeset.IsEffectivelyVisible && !editor.IsEffectivelyVisible,
                "A committed equation should show its typeset presentation, not the linear editor.");

            var center = typeset.TranslatePoint(
                    new Point(typeset.Bounds.Width / 2, typeset.Bounds.Height / 2),
                    window)
                ?? throw new InvalidOperationException("Could not locate the typeset equation.");
            window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            Pump();

            Assert(
                !equation.IsEditing && typeset.IsEffectivelyVisible && typeset.IsFocused
                && !editor.IsEffectivelyVisible,
                "Clicking a committed equation should keep its focused structured presentation.");

            var insertionBeforeNavigation = typeset.StructuredInsertionIndex;
            window.KeyPress(
                Key.Left,
                RawInputModifiers.None,
                PhysicalKey.ArrowLeft,
                null);
            Pump();
            Assert(
                typeset.StructuredInsertionIndex != insertionBeforeNavigation
                && typeset.IsEffectivelyVisible,
                "Arrow keys should move the caret through the structured equation.");

            window.KeyTextInput("z");
            Pump();
            Assert(
                equation.IsEditing && editor.IsEffectivelyVisible && editor.IsFocused
                && !typeset.IsEffectivelyVisible && equation.DraftExpression.Contains('z'),
                "The first modifying input should return to the focused linear editor.");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void EditingClearButtonClearsEquation()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            var assignedColor = equation.Color;
            equation.DraftExpression = "x";
            viewModel.Graphing.CommitEquation(equation);
            Pump();

            var editors = window.GetVisualDescendants()
                .OfType<TextBox>()
                .Where(textBox => textBox.Name == "EquationExpressionTextBox")
                .ToArray();
            var editor = editors.First(textBox =>
                ReferenceEquals(textBox.DataContext, equation));
            var newExpressionEditor = editors.First(textBox =>
                !ReferenceEquals(textBox.DataContext, equation));
            newExpressionEditor.Focus();
            Pump();

            var typeset = window.GetVisualDescendants()
                .OfType<EditableMathView>()
                .First(mathView => ReferenceEquals(mathView.DataContext, equation));
            var typesetCenter = typeset.TranslatePoint(
                    new Point(typeset.Bounds.Width / 2, typeset.Bounds.Height / 2),
                    window)
                ?? throw new InvalidOperationException("Could not locate the typeset equation.");
            window.MouseDown(typesetCenter, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(typesetCenter, MouseButton.Left, RawInputModifiers.None);
            Pump();
            window.KeyTextInput("z");
            Pump();

            var clear = window.GetVisualDescendants()
                .OfType<Button>()
                .First(button =>
                    button.Classes.Contains("graphEquationClear")
                    && ReferenceEquals(button.DataContext, equation));
            Assert(!clear.Focusable, "The editing clear button must not steal editor focus.");

            var clearCenter = clear.TranslatePoint(
                    new Point(clear.Bounds.Width / 2, clear.Bounds.Height / 2),
                    window)
                ?? throw new InvalidOperationException("Could not locate the editing clear button.");
            window.MouseDown(clearCenter, MouseButton.Left, RawInputModifiers.None);
            Pump();
            Assert(
                clear.IsVisible && editor.IsFocused,
                "Mouse-down on clear must keep the editor active until the click completes.");
            window.MouseUp(clearCenter, MouseButton.Left, RawInputModifiers.None);
            Pump();

            Assert(
                string.IsNullOrEmpty(equation.DraftExpression)
                && string.IsNullOrEmpty(equation.Expression)
                && string.IsNullOrEmpty(editor.Text),
                "The editing clear button should empty the editor, draft, and committed text.");
            Assert(
                equation.IsAllocated
                && equation.FunctionIndexLabel == "1"
                && equation.TileColor == assignedColor,
                "The cleared row should retain its function number and assigned color.");
            Assert(
                viewModel.Graphing.Equations.Count == 2
                && !viewModel.Graphing.Equations[1].IsAllocated,
                "Clearing should not consume or replace the separate new-expression placeholder.");
            var clearedRowActions = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button =>
                    button.Classes.Contains("graphEquationAction")
                    && ReferenceEquals(button.DataContext, equation))
                .ToArray();
            var ghostRowActions = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button =>
                    button.Classes.Contains("graphEquationAction")
                    && ReferenceEquals(button.DataContext, viewModel.Graphing.Equations[1]))
                .ToArray();
            Assert(
                equation.ShowEquationActions
                && clearedRowActions.Length == 3
                && clearedRowActions.All(button => button.IsEffectivelyVisible),
                "A cleared numbered row should retain Analyze, Style, and Remove actions.");
            Assert(
                !viewModel.Graphing.Equations[1].ShowEquationActions
                && ghostRowActions.Length == 3
                && ghostRowActions.All(button => !button.IsEffectivelyVisible),
                "Only the unallocated grey add-row placeholder should hide equation actions.");
            Assert(
                viewModel.Graphing.GetRenderableEquations().Count == 0,
                "Clearing the equation should remove its graph immediately.");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void InvalidEquationUsesNativeErrorPresentation()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Light,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            equation.Expression = "≤";
            equation.IsEditing = false;
            Pump();

            var errorIcon = window.GetVisualDescendants()
                .OfType<TextBlock>()
                .First(control =>
                    control.Name == "EquationErrorIcon"
                    && ReferenceEquals(control.DataContext, equation));
            var actions = window.GetVisualDescendants()
                .OfType<Button>()
                .Where(button =>
                    ReferenceEquals(button.DataContext, equation)
                    && (button.Classes.Contains("graphEquationAction")
                        || button.Classes.Contains("graphEquationErrorAction"))
                    && button.IsEffectivelyVisible)
                .ToArray();

            Assert(equation.ErrorMessage == viewModel.Graphing.Strings.UnexpectedEndOfExpression,
                "The parser should use the localized Windows Calculator error resource.");
            Assert(errorIcon.IsEffectivelyVisible
                && Equals(ToolTip.GetTip(errorIcon), equation.ErrorMessage),
                "The native error glyph should expose the localized error as its tooltip.");
            Assert(actions.Length == 1,
                "An invalid row should expose only its remove action, not analyze or line style.");

            equation.IsEditing = true;
            Pump();
            Assert(!errorIcon.IsEffectivelyVisible,
                "The native error glyph should be hidden while the invalid row is focused.");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void SelectorFlyoutsInsertTokens()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            var editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            editor.Focus();
            Pump();

            OpenAndPress("TrigButton", "TrigSinButton");
            OpenAndPress("InequalitiesButton", "LessThanOrEqualButton");
            OpenAndPress("InequalitiesButton", "GreaterThanOrEqualButton");
            OpenAndPress("FunctionsButton", "AbsoluteValueButton");

            Assert(
                equation.DraftExpression == "sin(≤≥abs(",
                $"Selector keys inserted '{equation.DraftExpression}' instead of the expected tokens.");

            void OpenAndPress(string selectorName, string keyName)
            {
                var selector = window.GetVisualDescendants()
                    .OfType<Button>()
                    .First(button => button.Name == selectorName);
                selector.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();

                var flyout = selector.Flyout as Flyout
                    ?? throw new InvalidOperationException($"{selectorName} has no flyout.");
                Assert(flyout.IsOpen, $"{selectorName} should open its flyout.");

                var keys = ((Control)flyout.Content!).GetVisualDescendants()
                    .OfType<Button>()
                    .ToArray();
                Assert(keys.Length > 0, $"{selectorName} should expose flyout keys.");
                Assert(
                    keys.All(key =>
                        Math.Abs(key.Bounds.Width - key.Bounds.Height) < 0.01
                        && Math.Abs(key.Bounds.Width - 48) < 0.01),
                    $"{selectorName} flyout keys should render as 48 by 48 squares.");

                keys.First(key => key.Name == keyName)
                    .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Pump();
                flyout.Hide();
                Pump();
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void GraphOptionsPopupIsAnchoredAndDismissible()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Light,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var graphOptionsButton = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "GraphOptionsButton");
            var graphView = window.GetVisualDescendants()
                .OfType<GraphingCalculatorView>()
                .Single();
            var graphOptionsPopup = graphView.FindControl<Popup>("GraphOptionsPopup")
                ?? throw new InvalidOperationException("The graph options popup is missing.");
            var graphOptionsPanel = graphView.FindControl<Border>("GraphOptionsPanel")
                ?? throw new InvalidOperationException("The graph options panel is missing.");
            var plot = window.GetVisualDescendants().OfType<GraphCanvas>().Single();
            var zoomInButton = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button =>
                    AutomationProperties.GetName(button) == "Zoom in");

            Click(graphOptionsButton);
            Pump();
            Assert(graphOptionsPopup.IsOpen, "The graph options popup did not open.");
            var buttonBottom = graphOptionsButton.PointToScreen(
                new Point(0, graphOptionsButton.Bounds.Height));
            var panelTop = graphOptionsPanel.PointToScreen(default);
            Assert(
                panelTop.Y >= buttonBottom.Y,
                "The graph options popup should open below its invoking button.");

            var xMinimum = graphOptionsPanel.FindControl<TextBox>("XMinimumBox")
                ?? throw new InvalidOperationException("The X minimum graph setting is missing.");
            xMinimum.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Pump();
            Assert(!graphOptionsPopup.IsOpen, "Escape did not dismiss the graph options popup.");

            Click(graphOptionsButton);
            Pump();
            Click(graphOptionsButton);
            Pump();
            Assert(
                !graphOptionsPopup.IsOpen,
                "Clicking the graph options button a second time did not dismiss its popup.");

            var widthBeforeZoom = plot.XMaximum - plot.XMinimum;
            Click(graphOptionsButton);
            Pump();
            Click(zoomInButton);
            Pump();
            Assert(!graphOptionsPopup.IsOpen, "Clicking outside did not dismiss the graph options popup.");
            Assert(
                plot.XMaximum - plot.XMinimum < widthBeforeZoom,
                "The click that dismissed graph options did not reach the underlying zoom button.");

            void Click(Control control)
            {
                var center = control.TranslatePoint(
                        new Point(control.Bounds.Width / 2, control.Bounds.Height / 2),
                        window)
                    ?? throw new InvalidOperationException($"Could not locate {control.Name}.");
                window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void GraphShortcutsUseSharedContextualScopes()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Dark,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            var editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            editor.Text = "x^2";
            editor.Focus();
            Pump();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Pump();
            Assert(
                equation.Expression == "x^2" && viewModel.Graphing.Equations.Count == 2,
                "Enter in the equation editor did not use the equation-input shortcut scope");

            var plot = window.GetVisualDescendants().OfType<GraphCanvas>().Single();
            plot.SetTracing(true);
            plot.Focus();
            Pump();
            var traceCursorBefore = plot.ActiveTraceCursorPosition
                ?? throw new InvalidOperationException("The active trace cursor is missing.");
            window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Pump();
            Assert(
                plot.ActiveTraceCursorPosition is { } traceCursorAfter
                && traceCursorAfter.X > traceCursorBefore.X,
                "Right arrow did not move the trace cursor through the graph shortcut scope");
            window.KeyRelease(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null);
            Pump();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
            Pump();
            Assert(!plot.IsTracing, "Escape did not stop tracing through the graph shortcut scope");

            var graphView = window.GetVisualDescendants()
                .OfType<GraphingCalculatorView>()
                .Single();
            var graphOptionsButton = graphView.FindControl<Button>("GraphOptionsButton")
                ?? throw new InvalidOperationException("The graph options button is missing.");
            var graphOptionsPopup = graphView.FindControl<Popup>("GraphOptionsPopup")
                ?? throw new InvalidOperationException("The graph options popup is missing.");
            graphOptionsPopup.IsOpen = true;
            Pump();
            var xMinimum = graphView.FindControl<TextBox>("XMinimumBox")
                ?? throw new InvalidOperationException("The X minimum graph setting is missing.");
            xMinimum.Text = "-4";
            xMinimum.Focus();
            Pump();
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Pump();
            Assert(
                Math.Abs(plot.XMinimum - (-4)) < 0.001,
                "Enter in a graph setting did not use the graph-settings shortcut scope");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void GraphTracingMatchesWindowsBehavior()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Light,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var equation = viewModel.Graphing.Equations[0];
            equation.DraftExpression = "x";
            viewModel.Graphing.CommitEquation(equation);
            Pump();

            var plot = window.GetVisualDescendants()
                .OfType<GraphCanvas>()
                .Single();
            var traceButton = window.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(button => button.Name == "TraceButton");
            var hoverPosition = new Point(
                plot.Bounds.Width / 2 + 40,
                plot.Bounds.Height / 2 - 40);
            var hoverInWindow = plot.TranslatePoint(hoverPosition, window)
                ?? throw new InvalidOperationException("Could not locate the graph canvas.");

            window.MouseMove(hoverInWindow, RawInputModifiers.None);
            Pump();
            Assert(
                !plot.IsTracing && !string.IsNullOrEmpty(plot.TraceText),
                "Ordinary hover should expose the nearest graph coordinates without active tracing.");

            ClickTraceButton();
            Pump();
            Assert(
                plot.IsTracing && plot.IsFocused && plot.ActiveTraceCursorPosition is not null,
                "Starting tracing should focus the graph and show the Windows active trace cursor.");

            var physicalPointer = new Point(
                plot.Bounds.Width / 2 + 80,
                plot.Bounds.Height / 2 + 80);
            var physicalPointerInWindow = plot.TranslatePoint(physicalPointer, window)
                ?? throw new InvalidOperationException("Could not locate the active pointer position.");
            window.MouseMove(physicalPointerInWindow, RawInputModifiers.None);
            Pump();
            Assert(
                plot.ActiveTraceCursorPosition is { } mouseCursor
                && Math.Abs(mouseCursor.X - physicalPointer.X) < 0.01
                && Math.Abs(mouseCursor.Y - physicalPointer.Y) < 0.01
                && string.IsNullOrEmpty(plot.TraceText),
                "The custom cursor should follow the mouse and repaint even when no curve is nearby.");

            var initialCursor = plot.ActiveTraceCursorPosition!.Value;
            window.KeyPress(
                Key.Right,
                RawInputModifiers.None,
                PhysicalKey.ArrowRight,
                null);
            window.KeyRelease(
                Key.Right,
                RawInputModifiers.None,
                PhysicalKey.ArrowRight,
                null);
            Pump();
            var normalCursor = plot.ActiveTraceCursorPosition!.Value;
            Assert(
                Math.Abs(normalCursor.X - initialCursor.X - 5) < 0.01,
                "An arrow key should move the active cursor by five pixels.");

            window.KeyPress(
                Key.Right,
                RawInputModifiers.Shift,
                PhysicalKey.ArrowRight,
                null);
            window.KeyRelease(
                Key.Right,
                RawInputModifiers.None,
                PhysicalKey.ArrowRight,
                null);
            Pump();
            var fineCursor = plot.ActiveTraceCursorPosition!.Value;
            Assert(
                Math.Abs(fineCursor.X - normalCursor.X - 1) < 0.01,
                "Shift plus an arrow key should move the active cursor by one pixel.");

            var holdStart = plot.ActiveTraceCursorPosition!.Value;
            window.KeyPress(
                Key.Down,
                RawInputModifiers.None,
                PhysicalKey.ArrowDown,
                null);
            Pump();
            var afterInitialKeyDown = plot.ActiveTraceCursorPosition!.Value;
            for (var frame = 0; frame < 3; frame++)
            {
                plot.AdvanceTraceMovementFrame();
            }
            var afterHeldKey = plot.ActiveTraceCursorPosition!.Value;
            Assert(
                Math.Abs(afterInitialKeyDown.Y - holdStart.Y - 5) < 0.01
                && plot.IsTraceMovementActive
                && Math.Abs(afterHeldKey.Y - afterInitialKeyDown.Y - 7.5) < 0.01,
                "Holding an arrow key should advance the cursor continuously on render ticks.");
            window.KeyRelease(
                Key.Down,
                RawInputModifiers.None,
                PhysicalKey.ArrowDown,
                null);
            Pump();
            var releasedCursor = plot.ActiveTraceCursorPosition!.Value;
            plot.AdvanceTraceMovementFrame();
            Assert(
                !plot.IsTraceMovementActive
                && plot.ActiveTraceCursorPosition == releasedCursor,
                "Releasing the arrow key should stop continuous cursor movement immediately.");

            var movedPhysicalPointer = physicalPointer + new Vector(10, 0);
            var movedPhysicalPointerInWindow = plot.TranslatePoint(movedPhysicalPointer, window)
                ?? throw new InvalidOperationException("Could not locate the moved pointer position.");
            window.MouseMove(movedPhysicalPointerInWindow, RawInputModifiers.None);
            Pump();
            var continuedCursor = plot.ActiveTraceCursorPosition!.Value;
            Assert(
                Math.Abs(continuedCursor.X - releasedCursor.X - 10) < 0.01
                && Math.Abs(continuedCursor.Y - releasedCursor.Y) < 0.01,
                "Mouse movement should track one-to-one from the keyboard-moved virtual cursor.");

            window.MouseDown(movedPhysicalPointerInWindow, MouseButton.Left, RawInputModifiers.None);
            window.MouseUp(movedPhysicalPointerInWindow, MouseButton.Left, RawInputModifiers.None);
            Pump();
            Assert(
                !plot.IsTracing && traceButton.IsChecked != true,
                "Clicking the graph should stop tracing and restore the normal pointer.");

            ClickTraceButton();
            Pump();
            window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                null);
            Pump();
            Assert(
                !plot.IsTracing && traceButton.IsChecked != true,
                "Escape should stop tracing and release the toggle button.");

            ClickTraceButton();
            Pump();
            var graphOptionsButton = window.GetVisualDescendants()
                .OfType<Button>()
                .Single(button => button.Name == "GraphOptionsButton");
            graphOptionsButton.Focus();
            Pump();
            Assert(
                graphOptionsButton.IsFocused
                && !plot.IsTracing
                && traceButton.IsChecked != true,
                "Moving focus away from the graph should stop tracing and release the toggle.");

            void ClickTraceButton()
            {
                var center = traceButton.TranslatePoint(
                        new Point(traceButton.Bounds.Width / 2, traceButton.Bounds.Height / 2),
                        window)
                    ?? throw new InvalidOperationException("Could not locate the tracing button.");
                window.MouseDown(center, MouseButton.Left, RawInputModifiers.None);
                window.MouseUp(center, MouseButton.Left, RawInputModifiers.None);
            }
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void AutomaticGraphViewIsStableOnOffToggle()
    {
        Application.Current!.RequestedThemeVariant = ThemeVariant.Light;
        var window = new MainWindow(new AppSettings(
            AppThemePreference.Light,
            "Inter",
            UseMicaEffect: false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.Windows11))
        {
            Width = 1204,
            Height = 720,
        };

        try
        {
            window.Show();
            Pump();
            window.KeyPressQwerty(PhysicalKey.Digit3, RawInputModifiers.Alt);
            Pump();

            var viewModel = (CalculatorViewModel)window.DataContext!;
            var graphView = window.GetVisualDescendants()
                .OfType<GraphingCalculatorView>()
                .Single();
            var plot = window.GetVisualDescendants()
                .OfType<GraphCanvas>()
                .Single();
            var button = window.GetVisualDescendants()
                .OfType<ToggleButton>()
                .Single(control => control.Name == "GraphViewButton");
            var zoomInButton = graphView.FindControl<Button>("ZoomInButton")
                ?? throw new InvalidOperationException("The zoom-in button is missing.");
            var zoomOutButton = graphView.FindControl<Button>("ZoomOutButton")
                ?? throw new InvalidOperationException("The zoom-out button is missing.");
            var glyphs = button.GetVisualDescendants()
                .OfType<TextBlock>()
                .ToArray();
            var outlineGlyph = glyphs.Single(glyph => glyph.Name == "GraphViewGlyph");
            var fillGlyph = glyphs.Single(glyph => glyph.Name == "GraphViewFillGlyph");

            Assert(
                glyphs.Length == 2
                && outlineGlyph.Text == "\uE45E"
                && fillGlyph.Text == "\uE45D"
                && fillGlyph.Opacity == 1
                && button.IsChecked == true,
                "Automatic best fit should fill the stable graph-view glyph.");
            Assert(
                IsTransparent(button.Background),
                "The active graph-view mode should not retain a pressed-button backdrop.");
            Assert(
                new ContentControl[] { zoomInButton, zoomOutButton, button }.All(control =>
                    control.Bounds.Width == 32
                    && control.Bounds.Height == 32
                    && control.HorizontalContentAlignment == HorizontalAlignment.Center
                    && control.VerticalContentAlignment == VerticalAlignment.Center),
                "Graph command icons should use consistently centered 32-pixel buttons.");
            Assert(
                Equals(ToolTip.GetTip(button), viewModel.Graphing.Strings.AutomaticViewTooltip),
                "The toggle should be labelled as automatic best fit.");

            plot.SetManualAdjustment(true);
            Pump();
            Assert(
                outlineGlyph.Text == "\uE45E"
                && fillGlyph.Opacity == 0
                && button.IsChecked != true,
                "Manual view should remove only the graph-view glyph's active fill.");
            Assert(
                Equals(ToolTip.GetTip(button), viewModel.Graphing.Strings.AutomaticViewTooltip),
                "The toggle label should remain stable between states.");

            plot.RefreshViewAutomatically();
            Pump();
            Assert(
                outlineGlyph.Text == "\uE45E"
                && fillGlyph.Opacity == 1
                && button.IsChecked == true,
                "Returning to automatic best fit should restore the glyph fill.");
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Pump()
    {
        for (var tick = 0; tick < 20; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool IsTransparent(IBrush? brush) =>
        brush is null
        || brush.Opacity == 0
        || brush is ISolidColorBrush solid && solid.Color.A == 0;
}
