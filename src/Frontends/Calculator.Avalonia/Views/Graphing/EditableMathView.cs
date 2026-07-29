using System;
using System.Drawing;
using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using CSharpMath.Atom;
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

/// <summary>
/// A committed equation remains professionally typeset while the caret and
/// modifying input move through its math tree.
/// </summary>
public sealed class EditableMathView : MathView
{
    private MathKeyboard? _keyboard;
    private string? _loadedLatex;

    public EditableMathView()
    {
        Focusable = true;
        IsTabStop = true;
        Cursor = new Cursor(StandardCursorType.Ibeam);
        DetachedFromVisualTree += (_, _) => DisposeKeyboard();
    }

    public event EventHandler<LinearMathEditRequestedEventArgs>? LinearEditRequested;
    public event EventHandler? CommitRequested;

    public static readonly StyledProperty<string> LinearTextProperty =
        AvaloniaProperty.Register<EditableMathView, string>(nameof(LinearText), string.Empty);

    public string LinearText
    {
        get => GetValue(LinearTextProperty);
        set => SetValue(LinearTextProperty, value);
    }

    internal string StructuredInsertionIndex =>
        _keyboard?.InsertionIndex.ToString() ?? string.Empty;

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
            _keyboard.KeyPress(navigationInput);
            InvalidateVisual();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Home && _keyboard is not null)
        {
            _keyboard.InsertionIndex = MathListIndex.Level0Index(0);
            InvalidateVisual();
            e.Handled = true;
            return;
        }
        if (e.Key == Key.End && _keyboard is not null)
        {
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
        if (e.Key == Key.Enter)
        {
            CommitRequested?.Invoke(this, EventArgs.Empty);
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

        foreach (var character in text)
        {
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

    public void Backspace()
    {
        EnsureKeyboard();
        if (_keyboard is null)
        {
            return;
        }

        var caretIndex = GetSuggestedLinearCaretIndex();
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

        var caretIndex = GetSuggestedLinearCaretIndex();
        if (caretIndex >= LinearText.Length)
        {
            return;
        }
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

        _keyboard.Clear();
        SynchronizeTypesetValue();
        LinearEditRequested?.Invoke(
            this,
            new LinearMathEditRequestedEventArgs(
                LinearMathEditAction.Clear,
                string.Empty,
                0));
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

        _keyboard = new MathKeyboard(FontSize);
        _keyboard.MathList.Append(mathList);
        _keyboard.InsertionIndex = MathListIndex.Level0Index(_keyboard.MathList.Count);
        _keyboard.RecreateDisplayFromMathList();
        _keyboard.RedrawRequested += Keyboard_OnRedrawRequested;
        _loadedLatex = latex;
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
    }
}
