using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class GraphingInteractionTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("equation context menu is attached before right click", EquationContextMenuIsAttached),
        ("editing clear button clears the committed equation", EditingClearButtonClearsEquation),
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
            equation.Expression = "x";
            Pump();

            var editor = window.GetVisualDescendants()
                .OfType<TextBox>()
                .First(textBox =>
                    textBox.Name == "EquationExpressionTextBox"
                    && ReferenceEquals(textBox.DataContext, equation));
            editor.Focus();
            Pump();

            var clear = window.GetVisualDescendants()
                .OfType<Button>()
                .First(button =>
                    button.Classes.Contains("graphEquationClear")
                    && ReferenceEquals(button.DataContext, equation));
            clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Pump();

            Assert(
                string.IsNullOrEmpty(equation.DraftExpression)
                && string.IsNullOrEmpty(equation.Expression),
                "The editing clear button should clear both draft and committed text.");
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
