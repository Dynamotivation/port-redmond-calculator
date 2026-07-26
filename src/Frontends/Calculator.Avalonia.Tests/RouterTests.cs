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
    /// Catalog identifiers that the router deliberately does not handle today.
    /// These are pre-existing gaps carried across the decomposition unchanged so
    /// that the refactor stays a pure parity change; the entry is here to keep
    /// the coverage test honest rather than to bless the behaviour.
    /// </summary>
    private static readonly Dictionary<string, string> KnownUnroutable = new(StringComparer.Ordinal)
    {
        // Ctrl+M. The MS keypad button works; the keyboard shortcut has never
        // been wired to MemoryStoreCommand, so the key does nothing.
        ["memButton"] = "memory store shortcut is not wired to the view model",
    };

    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("every calculator shortcut routes", EveryCalculatorShortcutRoutes),
        ("command mapping is stable", CommandMappingIsStable),
        ("digits reach the engine", DigitsReachTheEngine),
        ("scientific error gating", ScientificErrorGating),
        ("programmer radix and word size", ProgrammerRadixAndWordSize),
        ("clipboard shortcuts defer to the host", ClipboardShortcutsDeferToHost),
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
            // state, and a stale error state would change later outcomes. The
            // seed matters because the memory shortcuts reach the engine with
            // no HasMemory guard — see SeedMemory.
            var viewModel = CreateViewModel();
            SeedMemory(viewModel);
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
        Assert(viewModel.IsProgrammerHexadecimal, "hexButton should select the hexadecimal radix");

        CalculatorShortcutRouter.Dispatch(viewModel, "binaryButton");
        Assert(viewModel.IsProgrammerBinary, "binaryButton should select the binary radix");

        CalculatorShortcutRouter.Dispatch(viewModel, "byteButton");
        Assert(viewModel.SelectedProgrammerWordSize == CalculatorProgrammerWordSize.Byte,
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
    /// Stores a value in memory. The MemRecall/MemPlus/MemMinus shortcuts call
    /// straight into the engine without checking HasMemory, and the engine
    /// throws on an empty memory slot — the keypad buttons are disabled in that
    /// state but the keyboard path is not. That is pre-existing behaviour and
    /// this refactor preserves it, so the coverage test has to seed memory
    /// rather than assert against the crash.
    /// </summary>
    private static void SeedMemory(CalculatorViewModel viewModel)
    {
        viewModel.ExecuteCalculatorCommand(CalculatorCommand.Five);
        viewModel.Memory.StoreCommand.Execute(null);
    }

    private static void SwitchToScientific(CalculatorViewModel viewModel) =>
        SwitchTo(viewModel, CalculatorViewMode.Scientific);

    private static void SwitchTo(CalculatorViewModel viewModel, CalculatorViewMode mode)
    {
        var item = viewModel.CalculatorNavigationItems.First(candidate => candidate.Mode == mode);
        viewModel.SelectNavigationItemCommand.Execute(item);
    }
}
