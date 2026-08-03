using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Calculator.Avalonia;

/// <summary>
/// Centers the visible glyph ink instead of the font's line box.
/// </summary>
/// <remarks>
/// Font ascent, descent, and line-gap metrics vary by typeface and platform.
/// A vertically centered line box can therefore leave its visible glyphs high
/// or low. Avalonia exposes the measured black-pixel extent and bottom
/// overhang, which lets this control compensate for the active text and font
/// without typeface-specific margins.
/// </remarks>
public sealed class InkCenteredTextBlock : TextBlock
{
    private readonly TranslateTransform _inkOffset = new();

    public InkCenteredTextBlock()
    {
        RenderTransform = _inkOffset;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        var layout = TextLayout;
        _inkOffset.Y = CalculateVerticalInkOffset(
            layout.Height,
            layout.Extent,
            layout.OverhangAfter);
        return arranged;
    }

    internal static double CalculateVerticalInkOffset(
        double lineBoxHeight,
        double inkExtent,
        double bottomOverhang)
    {
        if (!double.IsFinite(lineBoxHeight)
            || !double.IsFinite(inkExtent)
            || !double.IsFinite(bottomOverhang)
            || inkExtent <= 0)
        {
            return 0;
        }

        return (inkExtent - lineBoxHeight) / 2 - bottomOverhang;
    }
}
