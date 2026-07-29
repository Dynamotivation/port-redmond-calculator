using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        ("graphing selector flyouts insert tokens with square keys", SelectorFlyoutsInsertTokens),
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
}
