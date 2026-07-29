using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Calculator.Avalonia.Views.Graphing;

public sealed class GraphTraceOverlay : Control
{
    private Point? _tracePoint;
    private Point? _cursorPoint;
    private string _traceText = string.Empty;
    private Color _traceColor = Colors.Black;
    private bool _isTracing;

    public GraphTraceOverlay()
    {
        IsHitTestVisible = false;
    }

    public void UpdateFrom(GraphCanvas plot)
    {
        _tracePoint = plot.TraceScreenPoint;
        _cursorPoint = plot.ActiveTraceCursorPosition;
        _traceText = plot.TraceText;
        _traceColor = plot.TraceColor;
        _isTracing = plot.IsTracing;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_tracePoint is { } tracePoint)
        {
            DrawTrace(context, tracePoint);
        }
        if (_isTracing && _cursorPoint is { } cursorPoint)
        {
            DrawActiveTraceCursor(context, cursorPoint);
        }
    }

    private void DrawTrace(DrawingContext context, Point point)
    {
        context.DrawEllipse(new SolidColorBrush(_traceColor), null, point, 3, 3);

        var tooltipOrigin = new Point(
            Math.Clamp(point.X + 10, 4, Math.Max(4, Bounds.Width - 180)),
            Math.Clamp(point.Y - 34, 4, Math.Max(4, Bounds.Height - 32)));
        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#E6202020")),
            null,
            new RoundedRect(new Rect(tooltipOrigin, new Size(174, 28)), 4));
        DrawLabel(context, _traceText, tooltipOrigin + new Vector(8, 6));
    }

    private static void DrawLabel(DrawingContext context, string text, Point origin)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default),
            11,
            Brushes.White);
        context.DrawText(formatted, origin);
    }

    private static void DrawActiveTraceCursor(DrawingContext context, Point point)
    {
        var shadow = CreateTraceCursorGeometry(point + new Vector(2, 2));
        context.DrawGeometry(new SolidColorBrush(Color.FromArgb(84, 0, 0, 0)), null, shadow);

        var cursor = CreateTraceCursorGeometry(point);
        context.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1), cursor);
    }

    private static StreamGeometry CreateTraceCursorGeometry(Point point)
    {
        // Windows Calculator's TracePointer vector:
        // "M0 0 l1371 1371 H538 l-538 538 Z", normalized into its 18px box.
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(point, true);
        context.LineTo(point + new Vector(12.93, 12.93));
        context.LineTo(point + new Vector(5.08, 12.93));
        context.LineTo(point + new Vector(0, 18));
        context.EndFigure(true);
        return geometry;
    }
}
