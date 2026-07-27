using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Calculator.Managed;
using Calculator.Managed.Graphing;

namespace Calculator.Avalonia.Views.Graphing;

public sealed class GraphCanvas : Control
{
    public static readonly StyledProperty<GraphingViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<GraphCanvas, GraphingViewModel?>(nameof(ViewModel));

    public static readonly StyledProperty<IBrush?> PlotBackgroundProperty =
        AvaloniaProperty.Register<GraphCanvas, IBrush?>(nameof(PlotBackground));

    public static readonly StyledProperty<IBrush?> GridBrushProperty =
        AvaloniaProperty.Register<GraphCanvas, IBrush?>(nameof(GridBrush));

    public static readonly StyledProperty<IBrush?> AxisBrushProperty =
        AvaloniaProperty.Register<GraphCanvas, IBrush?>(nameof(AxisBrush));

    private const double DefaultMinimum = -10;
    private const double DefaultMaximum = 10;
    private const double MinimumRange = 1e-5;
    private const double MaximumRange = 1e8;
    private Point? _lastPointerPosition;
    private double _xMinimum = DefaultMinimum;
    private double _xMaximum = DefaultMaximum;
    private double _yMinimum = DefaultMinimum;
    private double _yMaximum = DefaultMaximum;

    public GraphingViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public IBrush? PlotBackground
    {
        get => GetValue(PlotBackgroundProperty);
        set => SetValue(PlotBackgroundProperty, value);
    }

    public IBrush? GridBrush
    {
        get => GetValue(GridBrushProperty);
        set => SetValue(GridBrushProperty, value);
    }

    public IBrush? AxisBrush
    {
        get => GetValue(AxisBrushProperty);
        set => SetValue(AxisBrushProperty, value);
    }

    public void ZoomIn() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 0.8);

    public void ZoomOut() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 1.25);

    public void ResetView()
    {
        _xMinimum = DefaultMinimum;
        _xMaximum = DefaultMaximum;
        _yMinimum = DefaultMinimum;
        _yMaximum = DefaultMaximum;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 1 || Bounds.Height <= 1)
        {
            return;
        }

        context.DrawRectangle(
            PlotBackground ?? Brushes.Transparent,
            null,
            new Rect(Bounds.Size));

        var equations = ViewModel?.GetRenderableEquations() ?? [];
        foreach (var equation in equations.Where(equation =>
                     equation.Evaluator.Kind == GraphEquationKind.Inequality))
        {
            DrawInequality(context, equation);
        }

        DrawGrid(context);

        foreach (var equation in equations.Where(equation =>
                     equation.Evaluator.Kind != GraphEquationKind.Inequality))
        {
            var color = ParseColor(equation.Color);
            var pen = new Pen(new SolidColorBrush(color), 2);
            switch (equation.Evaluator.Kind)
            {
                case GraphEquationKind.Explicit:
                    DrawExplicit(context, equation.Evaluator, pen);
                    break;
                case GraphEquationKind.Polar:
                    DrawPolar(context, equation.Evaluator, pen);
                    break;
                case GraphEquationKind.Implicit:
                    DrawImplicit(context, equation.Evaluator, pen);
                    break;
            }
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is GraphingViewModel oldViewModel)
            {
                oldViewModel.GraphInvalidated -= OnGraphInvalidated;
            }
            if (change.NewValue is GraphingViewModel newViewModel)
            {
                newViewModel.GraphInvalidated += OnGraphInvalidated;
            }
            InvalidateVisual();
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _lastPointerPosition = point.Position;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointerPosition is not { } previous
            || e.Pointer.Captured != this)
        {
            return;
        }

        var current = e.GetPosition(this);
        var delta = current - previous;
        _lastPointerPosition = current;
        var xDelta = -delta.X / Bounds.Width * (_xMaximum - _xMinimum);
        var yDelta = delta.Y / Bounds.Height * (_yMaximum - _yMinimum);
        _xMinimum += xDelta;
        _xMaximum += xDelta;
        _yMinimum += yDelta;
        _yMaximum += yDelta;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
        _lastPointerPosition = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _lastPointerPosition = null;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        ZoomAt(e.GetPosition(this), e.Delta.Y > 0 ? 0.85 : 1 / 0.85);
        e.Handled = true;
    }

    private void OnGraphInvalidated(object? sender, EventArgs e) => InvalidateVisual();

    private void ZoomAt(Point center, double factor)
    {
        var currentXRange = _xMaximum - _xMinimum;
        var currentYRange = _yMaximum - _yMinimum;
        var newXRange = Math.Clamp(currentXRange * factor, MinimumRange, MaximumRange);
        var newYRange = Math.Clamp(currentYRange * factor, MinimumRange, MaximumRange);
        var graphCenterX = ScreenToGraphX(center.X);
        var graphCenterY = ScreenToGraphY(center.Y);
        var xRatio = center.X / Math.Max(1, Bounds.Width);
        var yRatio = center.Y / Math.Max(1, Bounds.Height);

        _xMinimum = graphCenterX - newXRange * xRatio;
        _xMaximum = _xMinimum + newXRange;
        _yMaximum = graphCenterY + newYRange * yRatio;
        _yMinimum = _yMaximum - newYRange;
        InvalidateVisual();
    }

    private void DrawGrid(DrawingContext context)
    {
        var gridPen = new Pen(GridBrush ?? Brushes.Gray, 1);
        var axisPen = new Pen(AxisBrush ?? Brushes.Black, 1.25);
        var xStep = NiceStep((_xMaximum - _xMinimum) / 8);
        var yStep = NiceStep((_yMaximum - _yMinimum) / 8);

        var firstX = Math.Ceiling(_xMinimum / xStep) * xStep;
        for (var x = firstX; x <= _xMaximum; x += xStep)
        {
            var screenX = GraphToScreenX(x);
            context.DrawLine(Math.Abs(x) < xStep * 1e-6 ? axisPen : gridPen,
                new Point(screenX, 0),
                new Point(screenX, Bounds.Height));
        }

        var firstY = Math.Ceiling(_yMinimum / yStep) * yStep;
        for (var y = firstY; y <= _yMaximum; y += yStep)
        {
            var screenY = GraphToScreenY(y);
            context.DrawLine(Math.Abs(y) < yStep * 1e-6 ? axisPen : gridPen,
                new Point(0, screenY),
                new Point(Bounds.Width, screenY));
        }
    }

    private void DrawExplicit(
        DrawingContext context,
        IGraphExpressionEvaluator evaluator,
        Pen pen)
    {
        var geometry = new StreamGeometry();
        using var geometryContext = geometry.Open();
        var hasFigure = false;
        Point previous = default;
        var sampleCount = Math.Max(2, (int)Math.Ceiling(Bounds.Width));
        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var screenX = sample / (double)sampleCount * Bounds.Width;
            var x = ScreenToGraphX(screenX);
            var y = SafeEvaluate(() => evaluator.EvaluateExplicit(x));
            var point = new Point(screenX, GraphToScreenY(y));
            var isFinite = double.IsFinite(y) && IsReasonableScreenPoint(point);
            var discontinuity = hasFigure
                && Math.Abs(point.Y - previous.Y) > Bounds.Height * 1.5;
            if (!isFinite || discontinuity)
            {
                hasFigure = false;
                continue;
            }

            if (!hasFigure)
            {
                geometryContext.BeginFigure(point, false);
                hasFigure = true;
            }
            else
            {
                geometryContext.LineTo(point);
            }
            previous = point;
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private void DrawPolar(
        DrawingContext context,
        IGraphExpressionEvaluator evaluator,
        Pen pen)
    {
        var geometry = new StreamGeometry();
        using var geometryContext = geometry.Open();
        var hasFigure = false;
        Point previous = default;
        var sampleCount = Math.Max(720, (int)Math.Ceiling(Bounds.Width * 1.5));
        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var theta = sample / (double)sampleCount * Math.Tau;
            var radius = SafeEvaluate(() => evaluator.EvaluatePolar(theta));
            var point = new Point(
                GraphToScreenX(radius * Math.Cos(theta)),
                GraphToScreenY(radius * Math.Sin(theta)));
            var isFinite = double.IsFinite(radius) && IsReasonableScreenPoint(point);
            var xDifference = point.X - previous.X;
            var yDifference = point.Y - previous.Y;
            var discontinuity = hasFigure
                && Math.Sqrt(xDifference * xDifference + yDifference * yDifference)
                    > Math.Max(Bounds.Width, Bounds.Height);
            if (!isFinite || discontinuity)
            {
                hasFigure = false;
                continue;
            }

            if (!hasFigure)
            {
                geometryContext.BeginFigure(point, false);
                hasFigure = true;
            }
            else
            {
                geometryContext.LineTo(point);
            }
            previous = point;
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private void DrawImplicit(
        DrawingContext context,
        IGraphExpressionEvaluator evaluator,
        Pen pen)
    {
        const double cellSize = 6;
        for (var top = 0d; top < Bounds.Height; top += cellSize)
        {
            var bottom = Math.Min(Bounds.Height, top + cellSize);
            for (var left = 0d; left < Bounds.Width; left += cellSize)
            {
                var right = Math.Min(Bounds.Width, left + cellSize);
                DrawContourCell(context, evaluator, pen, left, top, right, bottom);
            }
        }
    }

    private void DrawContourCell(
        DrawingContext context,
        IGraphExpressionEvaluator evaluator,
        Pen pen,
        double left,
        double top,
        double right,
        double bottom)
    {
        var values = new[]
        {
            SafeEvaluate(() => evaluator.EvaluateImplicit(ScreenToGraphX(left), ScreenToGraphY(top))),
            SafeEvaluate(() => evaluator.EvaluateImplicit(ScreenToGraphX(right), ScreenToGraphY(top))),
            SafeEvaluate(() => evaluator.EvaluateImplicit(ScreenToGraphX(right), ScreenToGraphY(bottom))),
            SafeEvaluate(() => evaluator.EvaluateImplicit(ScreenToGraphX(left), ScreenToGraphY(bottom))),
        };
        if (values.Any(value => !double.IsFinite(value)))
        {
            return;
        }

        var corners = new[]
        {
            new Point(left, top),
            new Point(right, top),
            new Point(right, bottom),
            new Point(left, bottom),
        };
        Span<Point> intersections = stackalloc Point[4];
        var intersectionCount = 0;
        for (var edge = 0; edge < 4; edge++)
        {
            var next = (edge + 1) % 4;
            var a = values[edge];
            var b = values[next];
            if ((a < 0) == (b < 0) && Math.Abs(a) > 1e-12 && Math.Abs(b) > 1e-12)
            {
                continue;
            }

            var denominator = Math.Abs(a) + Math.Abs(b);
            var ratio = denominator <= 1e-15 ? 0.5 : Math.Abs(a) / denominator;
            intersections[intersectionCount++] = new Point(
                corners[edge].X + (corners[next].X - corners[edge].X) * ratio,
                corners[edge].Y + (corners[next].Y - corners[edge].Y) * ratio);
        }

        if (intersectionCount >= 2)
        {
            context.DrawLine(pen, intersections[0], intersections[1]);
        }
        if (intersectionCount == 4)
        {
            context.DrawLine(pen, intersections[2], intersections[3]);
        }
    }

    private void DrawInequality(DrawingContext context, GraphEquationRenderModel equation)
    {
        const double cellSize = 8;
        var color = ParseColor(equation.Color);
        var fill = new SolidColorBrush(Color.FromArgb(36, color.R, color.G, color.B));
        var boundaryPen = new Pen(new SolidColorBrush(color), 1.5);
        for (var top = 0d; top < Bounds.Height; top += cellSize)
        {
            for (var left = 0d; left < Bounds.Width; left += cellSize)
            {
                var centerX = left + cellSize / 2;
                var centerY = top + cellSize / 2;
                var isInside = SafeEvaluateBoolean(() =>
                    equation.Evaluator.EvaluateInequality(
                        ScreenToGraphX(centerX),
                        ScreenToGraphY(centerY)));
                if (isInside)
                {
                    context.DrawRectangle(
                        fill,
                        null,
                        new Rect(left, top, cellSize + 0.5, cellSize + 0.5));
                }
            }
        }
        DrawImplicit(context, equation.Evaluator, boundaryPen);
    }

    private bool IsReasonableScreenPoint(Point point) =>
        point.X is > -100000 and < 100000
        && point.Y is > -100000 and < 100000;

    private double GraphToScreenX(double x) =>
        (x - _xMinimum) / (_xMaximum - _xMinimum) * Bounds.Width;

    private double GraphToScreenY(double y) =>
        (_yMaximum - y) / (_yMaximum - _yMinimum) * Bounds.Height;

    private double ScreenToGraphX(double x) =>
        _xMinimum + x / Math.Max(1, Bounds.Width) * (_xMaximum - _xMinimum);

    private double ScreenToGraphY(double y) =>
        _yMaximum - y / Math.Max(1, Bounds.Height) * (_yMaximum - _yMinimum);

    private static double SafeEvaluate(Func<double> evaluate)
    {
        try
        {
            return evaluate();
        }
        catch
        {
            return double.NaN;
        }
    }

    private static bool SafeEvaluateBoolean(Func<bool> evaluate)
    {
        try
        {
            return evaluate();
        }
        catch
        {
            return false;
        }
    }

    private static double NiceStep(double rawStep)
    {
        var exponent = Math.Floor(Math.Log10(Math.Max(rawStep, double.Epsilon)));
        var magnitude = Math.Pow(10, exponent);
        var normalized = rawStep / magnitude;
        var nice = normalized switch
        {
            < 1.5 => 1,
            < 3 => 2,
            < 7 => 5,
            _ => 10,
        };
        return nice * magnitude;
    }

    private static Color ParseColor(string value)
    {
        try
        {
            return Color.Parse(value);
        }
        catch
        {
            return Colors.DeepSkyBlue;
        }
    }
}
