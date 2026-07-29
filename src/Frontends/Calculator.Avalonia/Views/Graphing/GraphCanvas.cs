using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
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
    private Point? _pointerPressedPosition;
    private Vector _panVelocity;
    private DateTime _lastPointerMoveTime;
    private readonly DispatcherTimer _inertiaTimer;
    private bool _isTracing;
    private Point? _traceScreenPoint;
    private string _traceText = string.Empty;
    private double _xMinimum = DefaultMinimum;
    private double _xMaximum = DefaultMaximum;
    private double _yMinimum = DefaultMinimum;
    private double _yMaximum = DefaultMaximum;

    public GraphCanvas()
    {
        _inertiaTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnInertiaTick);
    }

    public event EventHandler? ViewportChanged;
    public event EventHandler? TraceChanged;
    public double XMinimum => _xMinimum;
    public double XMaximum => _xMaximum;
    public double YMinimum => _yMinimum;
    public double YMaximum => _yMaximum;
    public bool IsTracing => _isTracing;
    public string TraceText => _traceText;

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

    public void ZoomIn() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 16d / 17d);

    public void ZoomOut() => ZoomAt(new Point(Bounds.Width / 2, Bounds.Height / 2), 17d / 16d);

    public void SetViewport(double xMinimum, double xMaximum, double yMinimum, double yMaximum)
    {
        if (!double.IsFinite(xMinimum) || !double.IsFinite(xMaximum)
            || !double.IsFinite(yMinimum) || !double.IsFinite(yMaximum)
            || xMinimum >= xMaximum || yMinimum >= yMaximum)
        {
            return;
        }

        StopInertia();
        _xMinimum = xMinimum;
        _xMaximum = xMaximum;
        _yMinimum = yMinimum;
        _yMaximum = yMaximum;
        NotifyViewportChanged();
    }

    public void SetTracing(bool enabled)
    {
        _isTracing = enabled;
        if (!enabled)
        {
            _traceScreenPoint = null;
            _traceText = string.Empty;
        }
        else
        {
            UpdateTrace(new Point(GraphToScreenX(0), GraphToScreenY(0)));
        }
        TraceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void ResetView()
    {
        StopInertia();
        _xMinimum = DefaultMinimum;
        _xMaximum = DefaultMaximum;
        _yMinimum = DefaultMinimum;
        _yMaximum = DefaultMaximum;
        NotifyViewportChanged();
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
            var dashStyle = equation.LineStyle switch
            {
                GraphLineStyle.Dash => new DashStyle([2d, 1d], 0),
                GraphLineStyle.Dot => new DashStyle([1d], 0),
                _ => null,
            };
            var pen = new Pen(new SolidColorBrush(color), equation.LineWidth, dashStyle);
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

        if (_traceScreenPoint is { } tracePoint && _isTracing)
        {
            DrawTrace(context, tracePoint);
        }

        UpdateAutomationDescription();
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
        _pointerPressedPosition = point.Position;
        _panVelocity = default;
        _lastPointerMoveTime = DateTime.UtcNow;
        StopInertia();
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
        var now = DateTime.UtcNow;
        var elapsed = Math.Max(0.001, (now - _lastPointerMoveTime).TotalSeconds);
        _lastPointerMoveTime = now;
        _panVelocity = new Vector(delta.X / elapsed, delta.Y / elapsed);
        _lastPointerPosition = current;
        var xDelta = -delta.X / Bounds.Width * (_xMaximum - _xMinimum);
        var yDelta = delta.Y / Bounds.Height * (_yMaximum - _yMinimum);
        _xMinimum += xDelta;
        _xMaximum += xDelta;
        _yMinimum += yDelta;
        _yMaximum += yDelta;
        NotifyViewportChanged();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.Pointer.Captured == this)
        {
            e.Pointer.Capture(null);
        }
        var releasePosition = e.GetPosition(this);
        var wasClick = _pointerPressedPosition is { } pressed
            && Math.Sqrt(
                Math.Pow(releasePosition.X - pressed.X, 2)
                + Math.Pow(releasePosition.Y - pressed.Y, 2)) < 5;
        if (_isTracing && wasClick)
        {
            UpdateTrace(releasePosition);
        }
        else if (!wasClick && _panVelocity.Length > 80)
        {
            _inertiaTimer.Start();
        }
        _lastPointerPosition = null;
        _pointerPressedPosition = null;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _lastPointerPosition = null;
        _pointerPressedPosition = null;
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
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DrawGrid(DrawingContext context)
    {
        var gridPen = new Pen(GridBrush ?? Brushes.Gray, 1);
        var numberedGridPen = new Pen(GridBrush ?? Brushes.Gray, 1.5);
        var axisPen = new Pen(AxisBrush ?? Brushes.Black, 1.25);
        var xDivisions = Math.Clamp(Bounds.Width / 40, 4, 20);
        var yDivisions = Math.Clamp(Bounds.Height / 40, 4, 20);
        var xStep = NiceStep((_xMaximum - _xMinimum) / xDivisions);
        var yStep = NiceStep((_yMaximum - _yMinimum) / yDivisions);
        var numberedXStep = NiceStep((_xMaximum - _xMinimum) / 4);
        var numberedYStep = NiceStep((_yMaximum - _yMinimum) / 4);

        var firstX = Math.Ceiling(_xMinimum / xStep) * xStep;
        for (var x = firstX; x <= _xMaximum; x += xStep)
        {
            var screenX = GraphToScreenX(x);
            var pen = Math.Abs(x) < xStep * 1e-6
                ? axisPen
                : IsStepMultiple(x, numberedXStep) ? numberedGridPen : gridPen;
            context.DrawLine(pen,
                new Point(screenX, 0),
                new Point(screenX, Bounds.Height));
        }

        var firstY = Math.Ceiling(_yMinimum / yStep) * yStep;
        for (var y = firstY; y <= _yMaximum; y += yStep)
        {
            var screenY = GraphToScreenY(y);
            var pen = Math.Abs(y) < yStep * 1e-6
                ? axisPen
                : IsStepMultiple(y, numberedYStep) ? numberedGridPen : gridPen;
            context.DrawLine(pen,
                new Point(0, screenY),
                new Point(Bounds.Width, screenY));
        }

        DrawAxisDecorations(context);
    }

    private static bool IsStepMultiple(double value, double step)
    {
        var multiple = value / step;
        return Math.Abs(multiple - Math.Round(multiple)) < 1e-6;
    }

    private void DrawAxisDecorations(DrawingContext context)
    {
        var axisBrush = AxisBrush ?? Brushes.Black;
        var axisPen = new Pen(axisBrush, 1.25);
        var xAxisY = Math.Clamp(GraphToScreenY(0), 10, Math.Max(10, Bounds.Height - 10));
        var yAxisX = Math.Clamp(GraphToScreenX(0), 10, Math.Max(10, Bounds.Width - 10));

        if (_xMaximum > 0)
        {
            var tip = new Point(Bounds.Width - 5, xAxisY);
            context.DrawLine(axisPen, tip, new Point(tip.X - 7, tip.Y - 4));
            context.DrawLine(axisPen, tip, new Point(tip.X - 7, tip.Y + 4));
            DrawLabel(context, "x", new Point(tip.X - 15, tip.Y + 5), axisBrush, 13, FontStyle.Italic);
        }
        if (_yMaximum > 0)
        {
            var tip = new Point(yAxisX, 5);
            context.DrawLine(axisPen, tip, new Point(tip.X - 4, tip.Y + 7));
            context.DrawLine(axisPen, tip, new Point(tip.X + 4, tip.Y + 7));
            DrawLabel(context, "y", new Point(tip.X + 7, tip.Y + 2), axisBrush, 13, FontStyle.Italic);
        }

        var labelEveryX = NiceStep((_xMaximum - _xMinimum) / 4);
        var labelEveryY = NiceStep((_yMaximum - _yMinimum) / 4);
        for (var x = Math.Ceiling(_xMinimum / labelEveryX) * labelEveryX;
             x <= _xMaximum; x += labelEveryX)
        {
            if (Math.Abs(x) < labelEveryX * 1e-6)
            {
                continue;
            }
            var labelX = GraphToScreenX(x);
            if (labelX is > 18 && labelX < Bounds.Width - 18)
            {
                DrawLabel(context, FormatCoordinate(x),
                    new Point(labelX - 8, xAxisY + 4), axisBrush, 11);
            }
        }
        for (var y = Math.Ceiling(_yMinimum / labelEveryY) * labelEveryY;
             y <= _yMaximum; y += labelEveryY)
        {
            if (Math.Abs(y) < labelEveryY * 1e-6)
            {
                continue;
            }
            var labelY = GraphToScreenY(y);
            if (labelY is > 12 && labelY < Bounds.Height - 12)
            {
                DrawLabel(context, FormatCoordinate(y),
                    new Point(yAxisX + 4, labelY - 8), axisBrush, 11);
            }
        }
        if (_xMinimum <= 0 && _xMaximum >= 0 && _yMinimum <= 0 && _yMaximum >= 0)
        {
            DrawLabel(context, "0", new Point(yAxisX + 4, xAxisY + 4), axisBrush, 11);
        }
    }

    private static void DrawLabel(
        DrawingContext context,
        string text,
        Point origin,
        IBrush brush,
        double size,
        FontStyle style = FontStyle.Normal)
    {
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily.Default, style),
            size,
            brush);
        context.DrawText(formatted, origin);
    }

    private void DrawTrace(DrawingContext context, Point point)
    {
        var accent = new SolidColorBrush(Color.Parse("#0078D4"));
        var marker = new StreamGeometry();
        using (var geometry = marker.Open())
        {
            geometry.BeginFigure(new Point(point.X, point.Y - 7), true);
            geometry.LineTo(new Point(point.X - 6, point.Y + 5));
            geometry.LineTo(new Point(point.X + 6, point.Y + 5));
            geometry.EndFigure(true);
        }
        context.DrawGeometry(Brushes.White, new Pen(accent, 2), marker);

        var tooltipOrigin = new Point(
            Math.Clamp(point.X + 10, 4, Math.Max(4, Bounds.Width - 180)),
            Math.Clamp(point.Y - 34, 4, Math.Max(4, Bounds.Height - 32)));
        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#E6202020")),
            null,
            new RoundedRect(new Rect(tooltipOrigin, new Size(174, 28)), 4));
        DrawLabel(context, _traceText, tooltipOrigin + new Vector(8, 6), Brushes.White, 11);
    }

    private void UpdateTrace(Point pointerPosition)
    {
        var equation = ViewModel?.GetRenderableEquations()
            .FirstOrDefault(model => model.Evaluator.Kind == GraphEquationKind.Explicit);
        if (equation is null)
        {
            _traceScreenPoint = null;
            _traceText = string.Empty;
            TraceChanged?.Invoke(this, EventArgs.Empty);
            InvalidateVisual();
            return;
        }

        var x = ScreenToGraphX(pointerPosition.X);
        var y = SafeEvaluate(() => equation.Evaluator.EvaluateExplicit(x));
        if (!double.IsFinite(y))
        {
            return;
        }
        _traceScreenPoint = new Point(GraphToScreenX(x), GraphToScreenY(y));
        _traceText = $"({x:0.###############}, {y:0.000000000000000})";
        TraceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void OnInertiaTick(object? sender, EventArgs e)
    {
        const double seconds = 0.016;
        var delta = _panVelocity * seconds;
        var xDelta = -delta.X / Math.Max(1, Bounds.Width) * (_xMaximum - _xMinimum);
        var yDelta = delta.Y / Math.Max(1, Bounds.Height) * (_yMaximum - _yMinimum);
        _xMinimum += xDelta;
        _xMaximum += xDelta;
        _yMinimum += yDelta;
        _yMaximum += yDelta;
        _panVelocity *= 0.88;
        if (_panVelocity.Length < 10)
        {
            StopInertia();
        }
        NotifyViewportChanged();
    }

    private void StopInertia()
    {
        _inertiaTimer.Stop();
        _panVelocity = default;
    }

    private void NotifyViewportChanged()
    {
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateAutomationDescription()
    {
        var count = ViewModel?.Equations.Count(equation => equation.HasExpression) ?? 0;
        AutomationProperties.SetName(this,
            $"Graph viewing window, x-axis bounded by {FormatCoordinate(_xMinimum)} and {FormatCoordinate(_xMaximum)}, " +
            $"y-axis bounded by {FormatCoordinate(_yMinimum)} and {FormatCoordinate(_yMaximum)}, displaying {count} equations");
    }

    private static string FormatCoordinate(double value) =>
        Math.Abs(value) < 1e-12
            ? "0"
            : value.ToString("0.###############", CultureInfo.InvariantCulture);

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
