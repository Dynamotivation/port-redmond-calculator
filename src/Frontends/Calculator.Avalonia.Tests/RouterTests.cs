using Calculator.Managed;
using Calculator.Shortcuts;

namespace Calculator.Avalonia.Tests;

/// <summary>
/// Non-visual guards for keyboard routing. Snapshots cannot see any of this,
/// and the shortcut path is the part of the decomposition most likely to break
/// silently, so it gets its own assertions.
/// </summary>
internal static class RouterTests
{
    private static readonly string[] CalculatorScopes = ["calculator", "scientific", "programmer"];

    /// <summary>
    /// Catalog identifiers the router deliberately does not handle. Empty: every
    /// calculator-scope shortcut routes.
    /// </summary>
    private static readonly Dictionary<string, string> KnownUnroutable = new(StringComparer.Ordinal);

    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("every calculator shortcut routes", EveryCalculatorShortcutRoutes),
        ("command mapping is stable", CommandMappingIsStable),
        ("digits reach the engine", DigitsReachTheEngine),
        ("scientific error gating", ScientificErrorGating),
        ("programmer radix and word size", ProgrammerRadixAndWordSize),
        ("clipboard shortcuts defer to the host", ClipboardShortcutsDeferToHost),
        ("memory shortcuts are safe when empty", MemoryShortcutsAreSafeWhenEmpty),
        ("memory store shortcut stores", MemoryStoreShortcutStores),
    ];

    private static CalculatorViewModel CreateViewModel() => new();

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Guards against a shortcut identifier being dropped from the router. Every
    /// id the catalog declares for a calculator scope has to resolve to
    /// something; an unroutable id is a dead key on the keyboard.
    /// </summary>
    private static void EveryCalculatorShortcutRoutes()
    {
        var unroutable = new List<string>();
        foreach (var definition in ShortcutCatalogLoader.LoadBuiltIn().Definitions)
        {
            if (definition.Scope is null || !CalculatorScopes.Contains(definition.Scope))
            {
                continue;
            }

            // A fresh view model per identifier: dispatching mutates engine
            // state, and a stale error state would change later outcomes. No
            // seeding — every shortcut has to be safe against empty memory.
            var viewModel = CreateViewModel();
            var handled = CalculatorShortcutRouter.Dispatch(viewModel, definition.Id)
                != CalculatorShortcutOutcome.NotHandled;
            var expectedUnroutable = KnownUnroutable.ContainsKey(definition.Id);

            if (!handled && !expectedUnroutable)
            {
                unroutable.Add($"{definition.Id} (scope {definition.Scope})");
            }
            else if (handled && expectedUnroutable)
            {
                unroutable.Add($"{definition.Id} now routes — remove it from KnownUnroutable");
            }
        }

        Assert(unroutable.Count == 0, $"shortcut routing changed: {string.Join(", ", unroutable)}");
    }

    private static void CommandMappingIsStable()
    {
        (string ShortcutId, CalculatorCommand Command)[] expected =
        [
            ("num0Button", CalculatorCommand.Zero),
            ("num9Button", CalculatorCommand.Nine),
            ("plusButton", CalculatorCommand.Add),
            ("minusButton", CalculatorCommand.Subtract),
            ("multiplyButton", CalculatorCommand.Multiply),
            ("divideButton", CalculatorCommand.Divide),
            ("equalButton", CalculatorCommand.Equals),
            ("backSpaceButton", CalculatorCommand.Backspace),
            ("clearButton", CalculatorCommand.Clear),
            ("clearEntryButton", CalculatorCommand.ClearEntry),
            ("decimalSeparatorButton", CalculatorCommand.Decimal),
            ("negateButton", CalculatorCommand.Sign),
            ("percentButton", CalculatorCommand.Percent),
            ("squareRootButton", CalculatorCommand.SquareRoot),
            ("modButton", CalculatorCommand.Modulo),
            ("fButton", CalculatorCommand.F),
            ("xorButton", CalculatorCommand.Xor),
            ("invsinButton", CalculatorCommand.InverseSin),
            ("logBaseY", CalculatorCommand.LogBaseY),
            ("xpower2Button", CalculatorCommand.Square),
            ("ySquareRootButton", CalculatorCommand.Root),
        ];

        foreach (var (shortcutId, command) in expected)
        {
            Assert(
                CalculatorShortcutRouter.TryGetCommand(shortcutId, out var actual) && actual == command,
                $"{shortcutId} should map to {command}");
        }

        // Identifiers that intentionally do not map to a single engine command.
        foreach (var shortcutId in new[] { "hexButton", "HistoryButton", "copyButton", "lshButton", "degButton" })
        {
            Assert(
                !CalculatorShortcutRouter.TryGetCommand(shortcutId, out _),
                $"{shortcutId} should not map to a single engine command");
        }
    }

    private static void DigitsReachTheEngine()
    {
        var viewModel = CreateViewModel();
        foreach (var shortcutId in new[] { "num1Button", "num2Button", "num3Button" })
        {
            CalculatorShortcutRouter.Dispatch(viewModel, shortcutId);
        }

        Assert(
            viewModel.PrimaryDisplay.Contains("123", StringComparison.Ordinal),
            $"expected the display to contain 123, was '{viewModel.PrimaryDisplay}'");
    }

    /// <summary>
    /// In scientific error state the source application swallows operators but
    /// lets operands recover the display. Losing this makes the keypad feel
    /// stuck after a divide by zero.
    /// </summary>
    private static void ScientificErrorGating()
    {
        var viewModel = CreateViewModel();
        SwitchToScientific(viewModel);

        foreach (var shortcutId in new[] { "num1Button", "divideButton", "num0Button", "equalButton" })
        {
            CalculatorShortcutRouter.Dispatch(viewModel, shortcutId);
        }

        Assert(viewModel.IsError, "dividing by zero should leave the view model in error");

        var errorDisplay = viewModel.PrimaryDisplay;
        CalculatorShortcutRouter.Dispatch(viewModel, "plusButton");
        Assert(viewModel.IsError, "an operator should be swallowed while in error");
        Assert(
            viewModel.PrimaryDisplay == errorDisplay,
            "an operator should not change the display while in error");

        CalculatorShortcutRouter.Dispatch(viewModel, "num7Button");
        Assert(!viewModel.IsError, "an operand should clear the error state");
    }

    private static void ProgrammerRadixAndWordSize()
    {
        var viewModel = CreateViewModel();
        SwitchTo(viewModel, CalculatorViewMode.Programmer);

        CalculatorShortcutRouter.Dispatch(viewModel, "num9Button");
        CalculatorShortcutRouter.Dispatch(viewModel, "hexButton");
        Assert(viewModel.Programmer.IsHexadecimal, "hexButton should select the hexadecimal radix");

        CalculatorShortcutRouter.Dispatch(viewModel, "binaryButton");
        Assert(viewModel.Programmer.IsBinary, "binaryButton should select the binary radix");

        CalculatorShortcutRouter.Dispatch(viewModel, "byteButton");
        Assert(viewModel.Programmer.SelectedWordSize == CalculatorProgrammerWordSize.Byte,
            "byteButton should select the byte word size");
    }

    private static void ClipboardShortcutsDeferToHost()
    {
        var viewModel = CreateViewModel();
        Assert(
            CalculatorShortcutRouter.Dispatch(viewModel, "copyButton") == CalculatorShortcutOutcome.CopyDisplay,
            "copyButton should ask the host to copy");
        Assert(
            CalculatorShortcutRouter.Dispatch(viewModel, "pasteButton") == CalculatorShortcutOutcome.PasteExpression,
            "pasteButton should ask the host to paste");
    }


    /// <summary>
    /// MC and MR are disabled on the keypad while memory is empty, and the
    /// engine faults if they reach it anyway. Every memory shortcut has to be a
    /// no-op rather than a crash in that state.
    /// </summary>
    private static void MemoryShortcutsAreSafeWhenEmpty()
    {
        foreach (var shortcutId in new[] { "MemRecall", "ClearMemoryButton", "MemPlus", "MemMinus" })
        {
            var viewModel = CreateViewModel();
            Assert(!viewModel.Memory.HasEntries, "a fresh view model should have empty memory");
            Assert(
                CalculatorShortcutRouter.Dispatch(viewModel, shortcutId) == CalculatorShortcutOutcome.Handled,
                $"{shortcutId} should be handled even with empty memory");
        }
    }

    private static void MemoryStoreShortcutStores()
    {
        var viewModel = CreateViewModel();
        CalculatorShortcutRouter.Dispatch(viewModel, "num8Button");
        CalculatorShortcutRouter.Dispatch(viewModel, "memButton");

        Assert(viewModel.Memory.HasEntries, "memButton should store the display into memory");

        // And the recall that used to throw now round-trips.
        CalculatorShortcutRouter.Dispatch(viewModel, "clearButton");
        CalculatorShortcutRouter.Dispatch(viewModel, "MemRecall");
        Assert(
            viewModel.PrimaryDisplay.Contains('8'),
            $"MemRecall should restore the stored value, display was '{viewModel.PrimaryDisplay}'");
    }

    private static void SwitchToScientific(CalculatorViewModel viewModel) =>
        SwitchTo(viewModel, CalculatorViewMode.Scientific);

    private static void SwitchTo(CalculatorViewModel viewModel, CalculatorViewMode mode)
    {
        var item = viewModel.CalculatorNavigationItems.First(candidate => candidate.Mode == mode);
        viewModel.SelectNavigationItemCommand.Execute(item);
    }
}
