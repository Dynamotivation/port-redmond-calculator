using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;
using CSharpMath.Avalonia;
using CSharpMath.Editor;
using CSharpMath.Rendering.BackEnd;
using CSharpMath.Rendering.FrontEnd;

namespace Calculator.Avalonia.Views.Graphing;

public enum LinearMathEditAction
{
    InsertText,
    Backspace,
    Delete,
    Clear,
}

public sealed class LinearMathEditRequestedEventArgs(
    LinearMathEditAction action,
    string text,
    int suggestedCaretIndex) : EventArgs
{
    public LinearMathEditAction Action { get; } = action;
    public string Text { get; } = text;
    public int SuggestedCaretIndex { get; } = suggestedCaretIndex;
}

public sealed class MathCommitRequestedEventArgs(bool advanceFocus) : EventArgs
{
    public bool AdvanceFocus { get; } = advanceFocus;
}

/// <summary>
/// A committed equation remains professionally typeset while the caret and
/// modifying input move through its math tree.
/// </summary>
public sealed class EditableMathView : MathView
{
    public const int MaximumInputLength = 2048;

    private MathKeyboard? _keyboard;
    private string? _loadedLatex;
    private string? _pendingBackspacePosition;
    private readonly Stack<string> _undoLatex = new();

    public EditableMathView()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        DetachedFromVisualTree += (_, _) => DisposeKeyboard();
    }

    public event EventHandler<LinearMathEditRequestedEventArgs>? LinearEditRequested;
    public event EventHandler<MathCommitRequestedEventArgs>? CommitRequested;

    public static readonly StyledProperty<string> LinearTextProperty =
        AvaloniaProperty.Register<EditableMathView, string>(nameof(LinearText), string.Empty);

    public string LinearText
    {
        get => GetValue(LinearTextProperty);
        set => SetValue(LinearTextProperty, value);
    }

    internal string StructuredInsertionIndex =>
        _keyboard?.InsertionIndex.ToString() ?? string.Empty;

    internal string StructuredLinearText
    {
        get
        {
            EnsureKeyboard();
            return _keyboard is null
                ? LinearText
                : SerializeMathList(_keyboard.MathList);
        }
    }

    internal bool CanUndo => _undoLatex.Count > 0;
    internal bool HasLiteralFunctionCall
    {
        get
        {
            EnsureKeyboard();
            return _keyboard is not null && ContainsLiteralFunctionCall(_keyboard.MathList);
        }
    }

    public override void Render(DrawingContext context)
    {
        EnsureKeyboard();
        base.Render(context);

        if (!IsFocused || _keyboard?.Display is null || Painter.Display is null)
        {
            return;
        }

        _keyboard.Display.Position = Painter.Display.Position;
        if (!_keyboard.ShouldDrawCaret)
        {
            return;
        }

        var canvas = new AvaloniaCanvas(context, Bounds.Size);
        DrawReadableCaret(
            canvas,
            System.Drawing.Color.FromArgb(TextColor.A, TextColor.R, TextColor.G, TextColor.B));
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        EnsureKeyboard();
        _keyboard?.StartBlinking();
        if (_keyboard is not null)
        {
            _keyboard.InsertionPositionHighlighted = true;
        }
        InvalidateVisual();
        base.OnGotFocus(e);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        _keyboard?.StopBlinking();
        if (_keyboard is not null)
        {
            _keyboard.InsertionPositionHighlighted = false;
        }
        InvalidateVisual();
        base.OnLostFocus(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ClearDeletionPreview();
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsLeftButtonPressed)
        {
            Focus();
            EnsureKeyboard();
            if (_keyboard?.Display is not null)
            {
                if (Painter.Display is not null)
                {
                    _keyboard.Display.Position = Painter.Display.Position;
                }
                _keyboard.MoveCaretToPoint(
                    new PointF((float)point.Position.X, (float)point.Position.Y));
                InvalidateVisual();
            }
            e.Handled = true;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_keyboard is null)
        {
            EnsureKeyboard();
        }

        var navigation = e.Key switch
        {
            Key.Left => MathKeyboardInput.Left,
            Key.Right => MathKeyboardInput.Right,
            Key.Up => MathKeyboardInput.Up,
            Key.Down => MathKeyboardInput.Down,
            _ => (MathKeyboardInput?)null,
        };
        if (navigation is { } navigationInput && _keyboard is not null)
        {
            ClearDeletionPreview();
            if ((e.Key is not (Key.Up or Key.Down)) || !MoveVerticallyThroughFraction(e.Key == Key.Down))
            {
                _keyboard.KeyPress(navigationInput);
            }
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Home && _keyboard is not null)
        {
            ClearDeletionPreview();
            _keyboard.InsertionIndex = MathListIndex.Level0Index(0);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.End && _keyboard is not null)
        {
            ClearDeletionPreview();
            _keyboard.InsertionIndex = MathListIndex.Level0Index(_keyboard.MathList.Count);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Back or Key.Delete)
        {
            if (e.Key == Key.Back)
            {
                Backspace();
            }
            else
            {
                Delete();
            }
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            if (e.PhysicalKey != PhysicalKey.NumPadEnter)
            {
                ClearDeletionPreview();
                CommitRequested?.Invoke(this, new MathCommitRequestedEventArgs(advanceFocus: true));
            }
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (!string.IsNullOrEmpty(e.Text))
        {
            InsertLinearText(e.Text);
            e.Handled = true;
            return;
        }
        base.OnTextInput(e);
    }

    public void InsertLinearText(string text)
    {
        EnsureKeyboard();
        if (_keyboard is null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(text))
        {
            PushUndo();
        }
        foreach (var character in text)
        {
            if (StructuredLinearText.Length >= MaximumInputLength)
            {
                break;
            }

            ClearDeletionPreview();
            var input = character switch
            {
                '×' => MathKeyboardInput.Multiply,
                '÷' => MathKeyboardInput.Slash,
                '−' => MathKeyboardInput.Minus,
                _ => (MathKeyboardInput)character,
            };
            if (!Enum.IsDefined(typeof(MathKeyboardInput), input))
            {
                continue;
            }

            var caretIndex = GetSuggestedLinearCaretIndex();
            _keyboard.KeyPress(input);
            SynchronizeTypesetValue();
            LinearEditRequested?.Invoke(
                this,
                new LinearMathEditRequestedEventArgs(
                    LinearMathEditAction.InsertText,
                    character.ToString(),
                    caretIndex));
        }
    }

    public void InsertTemplateText(string token)
    {
        EnsureKeyboard();
        if (_keyboard is null || StructuredLinearText.Length >= MaximumInputLength)
        {
            return;
        }

        ClearDeletionPreview();
        var input = token.TrimEnd('(') switch
        {
            "sin" => MathKeyboardInput.Sine,
            "cos" => MathKeyboardInput.Cosine,
            "tan" => MathKeyboardInput.Tangent,
            "cot" => MathKeyboardInput.Cotangent,
            "sec" => MathKeyboardInput.Secant,
            "csc" => MathKeyboardInput.Cosecant,
            "asin" or "arcsin" => MathKeyboardInput.ArcSine,
            "acos" or "arccos" => MathKeyboardInput.ArcCosine,
            "atan" or "arctan" => MathKeyboardInput.ArcTangent,
            "acot" or "arccot" => MathKeyboardInput.ArcCotangent,
            "asec" or "arcsec" => MathKeyboardInput.ArcSecant,
            "acsc" or "arccsc" => MathKeyboardInput.ArcCosecant,
            "sinh" => MathKeyboardInput.HyperbolicSine,
            "cosh" => MathKeyboardInput.HyperbolicCosine,
            "tanh" => MathKeyboardInput.HyperbolicTangent,
            "coth" => MathKeyboardInput.HyperbolicCotangent,
            "sech" => MathKeyboardInput.HyperbolicSecant,
            "csch" => MathKeyboardInput.HyperbolicCosecant,
            "asinh" or "arcsinh" => MathKeyboardInput.AreaHyperbolicSine,
            "acosh" or "arccosh" => MathKeyboardInput.AreaHyperbolicCosine,
            "atanh" or "arctanh" => MathKeyboardInput.AreaHyperbolicTangent,
            "acoth" or "arccoth" => MathKeyboardInput.AreaHyperbolicCotangent,
            "asech" or "arcsech" => MathKeyboardInput.AreaHyperbolicSecant,
            "acsch" or "arccsch" => MathKeyboardInput.AreaHyperbolicCosecant,
            "log" => MathKeyboardInput.Logarithm,
            "ln" => MathKeyboardInput.NaturalLogarithm,
            "sqrt" => MathKeyboardInput.SquareRoot,
            "cbrt" => MathKeyboardInput.CubeRoot,
            "abs" => MathKeyboardInput.Absolute,
            _ => (MathKeyboardInput?)null,
        };

        if (input is null)
        {
            InsertLinearText(token == "pi" ? "π" : token);
            return;
        }

        var caretIndex = GetSuggestedLinearCaretIndex();
        PushUndo();
        _keyboard.KeyPress(input.Value);
        if (input is not (MathKeyboardInput.SquareRoot
            or MathKeyboardInput.CubeRoot
            or MathKeyboardInput.Absolute))
        {
            _keyboard.KeyPress(MathKeyboardInput.BothRoundBrackets);
        }
        SynchronizeTypesetValue();
        LinearEditRequested?.Invoke(
            this,
            new LinearMathEditRequestedEventArgs(
                LinearMathEditAction.InsertText,
                token,
                caretIndex));
    }

    public void Backspace()
    {
        EnsureKeyboard();
        if (_keyboard is null)
        {
            return;
        }

        var position = _keyboard.InsertionIndex.ToString();
        var previous = _keyboard.InsertionIndex.Previous;
        var previousAtom = _keyboard.MathList.AtomAt(previous);
        if (previous is not null
            && IsComplexDeletionUnit(previousAtom)
            && !string.Equals(_pendingBackspacePosition, position, StringComparison.Ordinal))
        {
            ClearDeletionPreview();
            _pendingBackspacePosition = position;
            _keyboard.Display?.HighlightCharacterAt(
                previous,
                System.Drawing.Color.FromArgb(120, 0, 120, 215));
            InvalidateVisual();
            return;
        }

        var caretIndex = GetSuggestedLinearCaretIndex();
        ClearDeletionPreview();
        PushUndo();
        _keyboard.KeyPress(MathKeyboardInput.Backspace);
        SynchronizeTypesetValue();
        LinearEditRequested?.Invoke(
            this,
            new LinearMathEditRequestedEventArgs(
                LinearMathEditAction.Backspace,
                string.Empty,
                caretIndex));
    }

    public void Delete()
    {
        EnsureKeyboard();
        if (_keyboard is null)
        {
            return;
        }

        ClearDeletionPreview();
        var caretIndex = GetSuggestedLinearCaretIndex();
        if (caretIndex >= LinearText.Length)
        {
            return;
        }
        PushUndo();
        _keyboard.KeyPress(MathKeyboardInput.Right, MathKeyboardInput.Backspace);
        SynchronizeTypesetValue();
        LinearEditRequested?.Invoke(
            this,
            new LinearMathEditRequestedEventArgs(
                LinearMathEditAction.Delete,
                string.Empty,
                caretIndex));
    }

    public void Clear()
    {
        EnsureKeyboard();
        if (_keyboard is null)
        {
            return;
        }

        ClearDeletionPreview();
        PushUndo();
        _keyboard.Clear();
        SynchronizeTypesetValue();
        LinearEditRequested?.Invoke(
            this,
            new LinearMathEditRequestedEventArgs(
                LinearMathEditAction.Clear,
                string.Empty,
                0));
    }

    public void LoadLinearText(string text)
    {
        DisposeKeyboard();
        _undoLatex.Clear();
        _keyboard = CreateKeyboard();
        foreach (var character in text.Take(MaximumInputLength))
        {
            var input = character switch
            {
                '×' => MathKeyboardInput.Multiply,
                '÷' => MathKeyboardInput.Slash,
                '−' => MathKeyboardInput.Minus,
                _ => (MathKeyboardInput)character,
            };
            if (Enum.IsDefined(typeof(MathKeyboardInput), input))
            {
                _keyboard.KeyPress(input);
            }
        }
        SynchronizeTypesetValue();
    }

    public void Undo()
    {
        if (_undoLatex.TryPop(out var latex))
        {
            LoadLatexSnapshot(latex);
            LinearEditRequested?.Invoke(
                this,
                new LinearMathEditRequestedEventArgs(
                    LinearMathEditAction.InsertText,
                    string.Empty,
                    GetSuggestedLinearCaretIndex()));
        }
    }

    private void SynchronizeTypesetValue()
    {
        if (_keyboard is null)
        {
            return;
        }

        var latex = _keyboard.LaTeX;
        _loadedLatex = latex;
        SetCurrentValue(LaTeXProperty, latex);
        InvalidateMeasure();
        InvalidateVisual();
    }

    private static bool IsComplexDeletionUnit(MathAtom? atom) =>
        atom is Fraction or Radical or Inner
        || atom is not null
        && (atom.Superscript.Count > 0 || atom.Subscript.Count > 0);

    private void ClearDeletionPreview()
    {
        if (_pendingBackspacePosition is null)
        {
            return;
        }

        _pendingBackspacePosition = null;
        _keyboard?.RecreateDisplayFromMathList();
        InvalidateVisual();
    }

    private void PushUndo()
    {
        if (_keyboard is not null)
        {
            _undoLatex.Push(_keyboard.LaTeX);
        }
    }

    private void LoadLatexSnapshot(string latex)
    {
        DisposeKeyboard();
        var (mathList, error) = LaTeXParser.MathListFromLaTeX(latex);
        if (error is not null)
        {
            return;
        }
        _keyboard = CreateKeyboard();
        _keyboard.MathList.Append(mathList);
        _keyboard.InsertionIndex = MathListIndex.Level0Index(_keyboard.MathList.Count);
        _keyboard.RecreateDisplayFromMathList();
        SynchronizeTypesetValue();
    }

    private void DrawReadableCaret(AvaloniaCanvas canvas, System.Drawing.Color color)
    {
        if (_keyboard?.Display is null)
        {
            return;
        }

        var position = _keyboard.Display.PointForIndex(
                TypesettingContext.Instance,
                _keyboard.InsertionIndex)
            ?? _keyboard.Display.Position;
        position.Y *= -1;

        // CSharpMath's stock caret is only two thirds of the math point size.
        // Keep it readable after matching Calculator's smaller equation scale.
        var height = Math.Max(15f, FontSize);
        var halfWidth = Math.Max(0.6f, FontSize / 32f);
        using var path = canvas.StartNewPath();
        path.Foreground = color;
        path.MoveTo(position.X - halfWidth, position.Y);
        path.LineTo(position.X - halfWidth, position.Y - height);
        path.LineTo(position.X + halfWidth, position.Y - height);
        path.LineTo(position.X + halfWidth, position.Y);
        path.CloseContour();
    }

    private int GetSuggestedLinearCaretIndex()
    {
        if (_keyboard is null || string.IsNullOrEmpty(LinearText))
        {
            return 0;
        }

        var index = _keyboard.InsertionIndex;
        if (index.SubIndexType == MathListSubIndexType.None)
        {
            if (index.AtomIndex <= 0)
            {
                return 0;
            }
            if (index.AtomIndex >= _keyboard.MathList.Count)
            {
                return LinearText.Length;
            }
        }

        var division = FindTopLevelOperator(LinearText, '/');
        if (division >= 0)
        {
            if (index.HasSubIndexOfType(MathListSubIndexType.Numerator))
            {
                return index.FinalIndex <= 0 ? 0 : division;
            }
            if (index.HasSubIndexOfType(MathListSubIndexType.Denominator))
            {
                return index.FinalIndex <= 0 ? division + 1 : LinearText.Length;
            }
        }

        var power = FindTopLevelOperator(LinearText, '^');
        if (power >= 0 && index.HasSubIndexOfType(MathListSubIndexType.Superscript))
        {
            return Math.Clamp(power + 1 + index.FinalIndex, power + 1, LinearText.Length);
        }

        return index.FinalIndex <= 0 ? 0 : LinearText.Length;
    }

    private bool MoveVerticallyThroughFraction(bool moveDown)
    {
        if (_keyboard is null)
        {
            return false;
        }

        var index = _keyboard.InsertionIndex;
        var sourceType = moveDown
            ? MathListSubIndexType.Numerator
            : MathListSubIndexType.Denominator;
        if (index.FinalSubIndexType != sourceType
            || index.LevelDown() is not { } fractionIndex
            || _keyboard.MathList.AtomAt(fractionIndex) is not Fraction fraction)
        {
            return false;
        }

        var destinationType = moveDown
            ? MathListSubIndexType.Denominator
            : MathListSubIndexType.Numerator;
        var destination = moveDown ? fraction.Denominator : fraction.Numerator;
        _keyboard.InsertionIndex = fractionIndex.LevelUpWithSubIndex(
            destinationType,
            MathListIndex.Level0Index(Math.Min(index.FinalIndex, destination.Count)));
        return true;
    }

    private static int FindTopLevelOperator(string text, char target)
    {
        var depth = 0;
        for (var index = 0; index < text.Length; index++)
        {
            depth += text[index] switch
            {
                '(' or '[' or '{' => 1,
                ')' or ']' or '}' => -1,
                _ => 0,
            };
            if (depth == 0 && text[index] == target)
            {
                return index;
            }
        }
        return -1;
    }

    private static string SerializeMathList(MathList list)
    {
        var builder = new System.Text.StringBuilder();
        foreach (var atom in list)
        {
            if (atom is Space or Comment or Placeholder)
            {
                continue;
            }

            var value = atom switch
            {
                Fraction fraction =>
                    $"({SerializeMathList(fraction.Numerator)})/({SerializeMathList(fraction.Denominator)})",
                Radical radical when radical.Degree.Count == 0 =>
                    $"sqrt({SerializeMathList(radical.Radicand)})",
                Radical radical =>
                    $"({SerializeMathList(radical.Radicand)})^(1/({SerializeMathList(radical.Degree)}))",
                Inner inner =>
                    $"{NormalizeNucleus(inner.LeftBoundary.Nucleus)}"
                    + SerializeMathList(inner.InnerList)
                    + NormalizeNucleus(inner.RightBoundary.Nucleus),
                _ => NormalizeNucleus(atom.Nucleus),
            };
            builder.Append(value);

            if (atom.Subscript.Count > 0)
            {
                builder.Append("_(")
                    .Append(SerializeMathList(atom.Subscript))
                    .Append(')');
            }
            if (atom.Superscript.Count > 0)
            {
                builder.Append("^(")
                    .Append(SerializeMathList(atom.Superscript))
                    .Append(')');
            }
        }
        return builder.ToString();
    }

    private static bool ContainsLiteralFunctionCall(MathList list)
    {
        var atoms = list.ToArray();
        for (var index = 0; index < atoms.Length; index++)
        {
            if (atoms[index] is Open { Nucleus: "(" })
            {
                var name = string.Empty;
                for (var previous = index - 1;
                     previous >= 0 && atoms[previous] is Variable;
                     previous--)
                {
                    name = atoms[previous].Nucleus + name;
                }
                if (IsFunctionName(name))
                {
                    return true;
                }
            }

            if (atoms[index] is Fraction fraction
                && (ContainsLiteralFunctionCall(fraction.Numerator)
                    || ContainsLiteralFunctionCall(fraction.Denominator))
                || atoms[index] is Radical radical
                && (ContainsLiteralFunctionCall(radical.Degree)
                    || ContainsLiteralFunctionCall(radical.Radicand))
                || atoms[index] is Inner inner
                && ContainsLiteralFunctionCall(inner.InnerList)
                || ContainsLiteralFunctionCall(atoms[index].Subscript)
                || ContainsLiteralFunctionCall(atoms[index].Superscript))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFunctionName(string name) =>
        name is "sin" or "cos" or "tan" or "cot" or "sec" or "csc"
            or "asin" or "acos" or "atan" or "acot" or "asec" or "acsc"
            or "arcsin" or "arccos" or "arctan" or "arccot" or "arcsec" or "arccsc"
            or "sinh" or "cosh" or "tanh" or "coth" or "sech" or "csch"
            or "asinh" or "acosh" or "atanh" or "acoth" or "asech" or "acsch"
            or "arcsinh" or "arccosh" or "arctanh" or "arccoth" or "arcsech" or "arccsch"
            or "sqrt" or "cbrt" or "abs" or "log" or "ln";

    private static string NormalizeNucleus(string? nucleus) =>
        nucleus switch
        {
            null => string.Empty,
            "−" => "-",
            "×" or "·" => "*",
            "÷" => "/",
            "π" => "pi",
            "θ" => "theta",
            "≤" => "<=",
            "≥" => ">=",
            "≠" => "!=",
            _ => nucleus,
        };

    private void EnsureKeyboard()
    {
        var latex = LaTeX ?? string.Empty;
        if (_keyboard is not null && string.Equals(_loadedLatex, latex, StringComparison.Ordinal))
        {
            return;
        }

        DisposeKeyboard();
        var (mathList, error) = LaTeXParser.MathListFromLaTeX(latex);
        if (error is not null)
        {
            return;
        }

        _keyboard = CreateKeyboard();
        _keyboard.MathList.Append(mathList);
        _keyboard.InsertionIndex = MathListIndex.Level0Index(_keyboard.MathList.Count);
        _keyboard.RecreateDisplayFromMathList();
        _loadedLatex = latex;
    }

    private MathKeyboard CreateKeyboard()
    {
        var keyboard = new MathKeyboard(FontSize);
        keyboard.RedrawRequested += Keyboard_OnRedrawRequested;
        return keyboard;
    }

    private void Keyboard_OnRedrawRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);

    private void DisposeKeyboard()
    {
        if (_keyboard is null)
        {
            return;
        }
        _keyboard.RedrawRequested -= Keyboard_OnRedrawRequested;
        _keyboard.Dispose();
        _keyboard = null;
        _loadedLatex = null;
        _pendingBackspacePosition = null;
    }
}
