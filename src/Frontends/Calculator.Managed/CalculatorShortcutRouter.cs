using System.Collections.Generic;

namespace Calculator.Managed;

/// <summary>
/// What the host has to do after a shortcut was routed. Everything the view
/// model can service itself is reported as <see cref="Handled"/>; the clipboard
/// outcomes exist because clipboard access is a per-framework concern that does
/// not belong in the shared view-model project.
/// </summary>
public enum CalculatorShortcutOutcome
{
    NotHandled,
    Handled,
    CopyDisplay,
    PasteExpression,
}

/// <summary>
/// Maps shortcut identifiers from the shortcut catalog onto calculator engine
/// commands and view-model operations.
/// </summary>
/// <remarks>
/// This is deliberately free of any view dependency. A shortcut identifier is
/// resolved to a <see cref="CalculatorCommand"/> without knowing which button
/// happens to represent that command in the current mode, so mode views can
/// own their own pressed-state feedback instead of the window holding a map of
/// every named button.
/// </remarks>
public static class CalculatorShortcutRouter
{
    /// <summary>
    /// Operations the source application refuses to run while the scientific
    /// display is in an error state. Operands and the recovery commands are
    /// intentionally absent so they still clear the error.
    /// </summary>
    private static readonly IReadOnlySet<CalculatorCommand> ScientificErrorDisabledCommands = new HashSet<CalculatorCommand>
    {
        CalculatorCommand.Divide, CalculatorCommand.Multiply, CalculatorCommand.Subtract,
        CalculatorCommand.Add, CalculatorCommand.Sign,
        CalculatorCommand.Square, CalculatorCommand.Cube, CalculatorCommand.SquareRoot,
        CalculatorCommand.CubeRoot, CalculatorCommand.Power, CalculatorCommand.Root,
        CalculatorCommand.TenPowerX, CalculatorCommand.TwoPowerX, CalculatorCommand.EPowerX,
        CalculatorCommand.LogBase10, CalculatorCommand.NaturalLog, CalculatorCommand.LogBaseY,
        CalculatorCommand.Reciprocal, CalculatorCommand.Absolute, CalculatorCommand.Exp,
        CalculatorCommand.Modulo, CalculatorCommand.Factorial, CalculatorCommand.OpenParenthesis,
        CalculatorCommand.CloseParenthesis, CalculatorCommand.Pi, CalculatorCommand.Euler,
        CalculatorCommand.Sin, CalculatorCommand.Cos, CalculatorCommand.Tan,
        CalculatorCommand.Sinh, CalculatorCommand.Cosh, CalculatorCommand.Tanh,
        CalculatorCommand.InverseSin, CalculatorCommand.InverseCos, CalculatorCommand.InverseTan,
        CalculatorCommand.InverseSinh, CalculatorCommand.InverseCosh, CalculatorCommand.InverseTanh,
        CalculatorCommand.Sec, CalculatorCommand.Csc, CalculatorCommand.Cot,
        CalculatorCommand.Sech, CalculatorCommand.Csch, CalculatorCommand.Coth,
        CalculatorCommand.InverseSec, CalculatorCommand.InverseCsc, CalculatorCommand.InverseCot,
        CalculatorCommand.InverseSech, CalculatorCommand.InverseCsch, CalculatorCommand.InverseCoth,
        CalculatorCommand.Floor, CalculatorCommand.Ceiling, CalculatorCommand.Random,
        CalculatorCommand.Dms, CalculatorCommand.Degrees,
    };

    /// <summary>
    /// Resolves a shortcut identifier to the engine command it invokes, for the
    /// shortcuts that map one-to-one onto a keypad button.
    /// </summary>
    public static bool TryGetCommand(string shortcutId, out CalculatorCommand command)
    {
        var resolved = shortcutId switch
        {
            "clearButton" => CalculatorCommand.Clear,
            "clearEntryButton" => CalculatorCommand.ClearEntry,
            "decimalSeparatorButton" => CalculatorCommand.Decimal,
            "divideButton" => CalculatorCommand.Divide,
            "equalButton" => CalculatorCommand.Equals,
            "minusButton" => CalculatorCommand.Subtract,
            "negateButton" => CalculatorCommand.Sign,
            "num0Button" => CalculatorCommand.Zero,
            "num1Button" => CalculatorCommand.One,
            "num2Button" => CalculatorCommand.Two,
            "num3Button" => CalculatorCommand.Three,
            "num4Button" => CalculatorCommand.Four,
            "num5Button" => CalculatorCommand.Five,
            "num6Button" => CalculatorCommand.Six,
            "num7Button" => CalculatorCommand.Seven,
            "num8Button" => CalculatorCommand.Eight,
            "num9Button" => CalculatorCommand.Nine,
            "percentButton" => CalculatorCommand.Percent,
            "plusButton" => CalculatorCommand.Add,
            "squareRootButton" => CalculatorCommand.SquareRoot,
            "backSpaceButton" => CalculatorCommand.Backspace,
            "multiplyButton" => CalculatorCommand.Multiply,
            "modButton" => CalculatorCommand.Modulo,
            "aButton" => CalculatorCommand.A,
            "bButton" => CalculatorCommand.B,
            "cButton" => CalculatorCommand.C,
            "dButton" => CalculatorCommand.D,
            "eButton" => CalculatorCommand.E,
            "fButton" => CalculatorCommand.F,
            "andButton" => CalculatorCommand.And,
            "orButton" => CalculatorCommand.Or,
            "notButton" => CalculatorCommand.Not,
            "nandButton" => CalculatorCommand.Nand,
            "norButton" => CalculatorCommand.Nor,
            "xorButton" => CalculatorCommand.Xor,
            "absButton" => CalculatorCommand.Absolute,
            "ceilButton" => CalculatorCommand.Ceiling,
            "closeParenthesisButton" => CalculatorCommand.CloseParenthesis,
            "cosButton" => CalculatorCommand.Cos,
            "coshButton" => CalculatorCommand.Cosh,
            "cotButton" => CalculatorCommand.Cot,
            "cothButton" => CalculatorCommand.Coth,
            "cscButton" => CalculatorCommand.Csc,
            "cschButton" => CalculatorCommand.Csch,
            "cubeRootButton" => CalculatorCommand.CubeRoot,
            "degreeButton" => CalculatorCommand.Degrees,
            "dmsButton" => CalculatorCommand.Dms,
            "eulerButton" => CalculatorCommand.Euler,
            "expButton" => CalculatorCommand.Exp,
            "factorialButton" => CalculatorCommand.Factorial,
            "floorButton" => CalculatorCommand.Floor,
            "invcosButton" => CalculatorCommand.InverseCos,
            "invcoshButton" => CalculatorCommand.InverseCosh,
            "invcotButton" => CalculatorCommand.InverseCot,
            "invcothButton" => CalculatorCommand.InverseCoth,
            "invcscButton" => CalculatorCommand.InverseCsc,
            "invcschButton" => CalculatorCommand.InverseCsch,
            "invertButton" => CalculatorCommand.Reciprocal,
            "invsecButton" => CalculatorCommand.InverseSec,
            "invsechButton" => CalculatorCommand.InverseSech,
            "invsinButton" => CalculatorCommand.InverseSin,
            "invsinhButton" => CalculatorCommand.InverseSinh,
            "invtanButton" => CalculatorCommand.InverseTan,
            "invtanhButton" => CalculatorCommand.InverseTanh,
            "logBase10Button" => CalculatorCommand.LogBase10,
            "logBaseEButton" => CalculatorCommand.NaturalLog,
            "logBaseY" => CalculatorCommand.LogBaseY,
            "openParenthesisButton" => CalculatorCommand.OpenParenthesis,
            "piButton" => CalculatorCommand.Pi,
            "powerButton" => CalculatorCommand.Power,
            "powerOf10Button" => CalculatorCommand.TenPowerX,
            "powerOfEButton" => CalculatorCommand.EPowerX,
            "randButton" => CalculatorCommand.Random,
            "secButton" => CalculatorCommand.Sec,
            "sechButton" => CalculatorCommand.Sech,
            "sinButton" => CalculatorCommand.Sin,
            "sinhButton" => CalculatorCommand.Sinh,
            "tanButton" => CalculatorCommand.Tan,
            "tanhButton" => CalculatorCommand.Tanh,
            "twoPowerXButton" => CalculatorCommand.TwoPowerX,
            "xpower2Button" => CalculatorCommand.Square,
            "xpower3Button" => CalculatorCommand.Cube,
            "ySquareRootButton" => CalculatorCommand.Root,
            _ => (CalculatorCommand?)null,
        };

        command = resolved ?? default;
        return resolved is not null;
    }

    public static CalculatorShortcutOutcome Dispatch(CalculatorViewModel viewModel, string shortcutId)
    {
        if (TryGetCommand(shortcutId, out var command))
        {
            if (viewModel.IsScientificMode && viewModel.IsError
                && ScientificErrorDisabledCommands.Contains(command))
            {
                return CalculatorShortcutOutcome.Handled;
            }

            viewModel.ExecuteCalculatorCommand(command);
            return CalculatorShortcutOutcome.Handled;
        }

        switch (shortcutId)
        {
            case "lshButton":
            case "lshLogicalButton":
            case "rolButton":
            case "rolCarryButton": viewModel.ExecuteProgrammerLeftShiftCommand.Execute(null); break;
            case "rshButton":
            case "rshLogicalButton":
            case "rorButton":
            case "rorCarryButton": viewModel.ExecuteProgrammerRightShiftCommand.Execute(null); break;
            case "hexButton": viewModel.SelectProgrammerRadixCommand.Execute("Hexadecimal"); break;
            case "decimalButton": viewModel.SelectProgrammerRadixCommand.Execute("Decimal"); break;
            case "octButton": viewModel.SelectProgrammerRadixCommand.Execute("Octal"); break;
            case "binaryButton": viewModel.SelectProgrammerRadixCommand.Execute("Binary"); break;
            case "qwordButton": viewModel.SelectProgrammerWordSizeCommand.Execute("Qword"); break;
            case "dwordButton": viewModel.SelectProgrammerWordSizeCommand.Execute("Dword"); break;
            case "wordButton": viewModel.SelectProgrammerWordSizeCommand.Execute("Word"); break;
            case "byteButton": viewModel.SelectProgrammerWordSizeCommand.Execute("Byte"); break;
            case "HistoryButton": viewModel.History.ToggleCommand.Execute(null); break;
            case "ClearHistory": viewModel.History.ClearCommand.Execute(null); break;
            // The keyboard mirrors what the keypad allows. MC and MR are
            // disabled while memory is empty, and the engine faults if they are
            // invoked anyway, so the shortcut has to check too. M+ and M- stay
            // live because they store into an empty slot. MS was never wired at
            // all, which made Ctrl+M a dead key.
            case "memButton": viewModel.Memory.StoreCommand.Execute(null); break;
            case "ClearMemoryButton":
                if (viewModel.Memory.HasEntries)
                {
                    viewModel.Memory.ClearAllCommand.Execute(null);
                }
                break;
            case "MemRecall":
                if (viewModel.Memory.HasEntries)
                {
                    viewModel.Memory.RecallCommand.Execute(null);
                }
                break;
            case "MemPlus": viewModel.Memory.AddCommand.Execute(null); break;
            case "MemMinus": viewModel.Memory.SubtractCommand.Execute(null); break;
            case "degButton": if (!viewModel.IsError) viewModel.ExecuteCalculatorCommand(CalculatorCommand.Degree); break;
            case "radButton": if (!viewModel.IsError) viewModel.ExecuteCalculatorCommand(CalculatorCommand.Radian); break;
            case "gradButton": if (!viewModel.IsError) viewModel.ExecuteCalculatorCommand(CalculatorCommand.Grads); break;
            case "ftoeButton": if (!viewModel.IsError) viewModel.ToggleScientificNotationCommand.Execute(null); break;
            case "copyButton":
            case "copyButtonAlternate": return CalculatorShortcutOutcome.CopyDisplay;
            case "pasteButton":
            case "pasteButtonAlternate": return CalculatorShortcutOutcome.PasteExpression;
            default: return CalculatorShortcutOutcome.NotHandled;
        }

        return CalculatorShortcutOutcome.Handled;
    }
}
