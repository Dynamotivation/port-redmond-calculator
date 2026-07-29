using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private const double TraceSearchRadius = 24;
    private const double ActiveTraceOffset = 40;
    private Point? _lastPointerPosition;
    private Point? _pointerPressedPosition;
    private Point? _pointerPosition;
    private Point? _activeTraceCursorPosition;
    private Vector _panVelocity;
    private DateTime _lastPointerMoveTime;
    private readonly DispatcherTimer _inertiaTimer;
    private bool _isTracing;
    private bool _isManualAdjustment;
    private Point? _traceScreenPoint;
    private Color _traceColor = Colors.Black;
    private string _traceText = string.Empty;
    private double _xMinimum = DefaultMinimum;
    private double _xMaximum = DefaultMaximum;
    private double _yMinimum = DefaultMinimum;
    private double _yMaximum = DefaultMaximum;

    public GraphCanvas()
    {
        _inertiaTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, OnInertiaTick);
        LostFocus += OnGraphLostFocus;
    }

    public event EventHandler? ViewportChanged;
    public event EventHandler? TraceChanged;
    public event EventHandler? ManualAdjustmentChanged;
    public double XMinimum => _xMinimum;
    public double XMaximum => _xMaximum;
    public double YMinimum => _yMinimum;
    public double YMaximum => _yMaximum;
    public bool IsTracing => _isTracing;
    public bool IsManualAdjustment => _isManualAdjustment;
    public string TraceText => _traceText;
    public Point? ActiveTraceCursorPosition => _activeTraceCursorPosition;

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
        SetManualAdjustment(true);
        ApplyViewport(xMinimum, xMaximum, yMinimum, yMaximum);
    }

    public void SetTracing(bool enabled)
    {
        if (_isTracing == enabled)
        {
            return;
        }

        _isTracing = enabled;
        if (!enabled)
        {
            _activeTraceCursorPosition = null;
            _traceScreenPoint = null;
            _traceText = string.Empty;
            Cursor = null;
        }
        else
        {
            StopInertia();
            _activeTraceCursorPosition = ClampToBounds(new Point(
                Bounds.Width / 2 + ActiveTraceOffset,
                Bounds.Height / 2 - ActiveTraceOffset));
            Cursor = new Cursor(StandardCursorType.None);
            UpdateTrace(_activeTraceCursorPosition.Value);
            Focus();
        }
        TraceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public bool MoveTrace(string direction, bool fine)
    {
        if (!_isTracing)
        {
            return false;
        }

        if (_activeTraceCursorPosition is not { } cursorPosition)
        {
            return false;
        }

        var delta = fine ? 1d : 5d;
        var movement = direction switch
        {
            "LEFT" => new Vector(-delta, 0),
            "RIGHT" => new Vector(delta, 0),
            "UP" => new Vector(0, -delta),
            "DOWN" => new Vector(0, delta),
            _ => default,
        };
        if (movement == default)
        {
            return false;
        }

        _activeTraceCursorPosition = ClampToBounds(cursorPosition + movement);
        UpdateTrace(_activeTraceCursorPosition.Value);
        return true;
    }

    public void SetManualAdjustment(bool enabled)
    {
        if (!enabled)
        {
            RefreshViewAutomatically();
            return;
        }

        SetManualAdjustmentState(true);
    }

    public void RefreshViewAutomatically()
    {
        StopInertia();
        SetManualAdjustmentState(false);
        FitViewToGraph();
    }

    public void ResetView() => RefreshViewAutomatically();

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
            var pen = new Pen(new SolidColorBrush(color), equation.LineWidth, dashStyle)
            {
                LineCap = equation.LineStyle == GraphLineStyle.Dot
                    ? PenLineCap.Round
                    : PenLineCap.Flat,
            };
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

        if (_traceScreenPoint is { } tracePoint)
        {
            DrawTrace(context, tracePoint);
        }
        if (_isTracing && _activeTraceCursorPosition is { } cursorPoint)
        {
            DrawActiveTraceCursor(context, cursorPoint);
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
            if (_isManualAdjustment)
            {
                InvalidateVisual();
            }
            else
            {
                FitViewToGraph();
            }
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
        var current = ClampToBounds(e.GetPosition(this));
        _pointerPosition = current;
        if (_isTracing)
        {
            _activeTraceCursorPosition = current;
            UpdateTrace(current);
        }
        else if (e.Pointer.Captured != this)
        {
            UpdateTrace(current);
        }

        if (_lastPointerPosition is not { } previous
            || e.Pointer.Captured != this)
        {
            return;
        }

        var delta = current - previous;
        var now = DateTime.UtcNow;
        var elapsed = Math.Max(0.001, (now - _lastPointerMoveTime).TotalSeconds);
        _lastPointerMoveTime = now;
        _panVelocity = new Vector(delta.X / elapsed, delta.Y / elapsed);
        _lastPointerPosition = current;
        SetManualAdjustmentState(true);
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

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _pointerPosition = null;
        ClearTrace();
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

    private void OnGraphInvalidated(object? sender, GraphInvalidatedEventArgs e)
    {
        if (!_isManualAdjustment && e.Reason == GraphInvalidationReason.Geometry)
        {
            FitViewToGraph();
        }
        else
        {
            InvalidateVisual();
        }
        RefreshTrace();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!_isTracing)
        {
            return;
        }

        if (e.Key == Key.Escape)
        {
            SetTracing(false);
            e.Handled = true;
            return;
        }

        var delta = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1d : 5d;
        var movement = e.Key switch
        {
            Key.Left => new Vector(-delta, 0),
            Key.Right => new Vector(delta, 0),
            Key.Up => new Vector(0, -delta),
            Key.Down => new Vector(0, delta),
            _ => default,
        };
        if (movement == default || _activeTraceCursorPosition is not { } cursorPosition)
        {
            return;
        }

        _activeTraceCursorPosition = ClampToBounds(cursorPosition + movement);
        UpdateTrace(_activeTraceCursorPosition.Value);
        e.Handled = true;
    }

    private void OnGraphLostFocus(object? sender, RoutedEventArgs e)
    {
        // Defer until a click on the tracing toggle has completed. Stopping
        // synchronously here would reset the toggle before its Click handler
        // can observe that the user intended to turn tracing off.
        Dispatcher.UIThread.Post(
            () =>
            {
                if (_isTracing && !IsFocused)
                {
                    SetTracing(false);
                }
            },
            DispatcherPriority.Background);
    }

    private void ZoomAt(Point center, double factor)
    {
        SetManualAdjustmentState(true);
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
        RefreshTrace();
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
        context.DrawEllipse(new SolidColorBrush(_traceColor), null, point, 3, 3);

        var tooltipOrigin = new Point(
            Math.Clamp(point.X + 10, 4, Math.Max(4, Bounds.Width - 180)),
            Math.Clamp(point.Y - 34, 4, Math.Max(4, Bounds.Height - 32)));
        context.DrawRectangle(
            new SolidColorBrush(Color.Parse("#E6202020")),
            null,
            new RoundedRect(new Rect(tooltipOrigin, new Size(174, 28)), 4));
        DrawLabel(context, _traceText, tooltipOrigin + new Vector(8, 6), Brushes.White, 11);
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
        // This is the Windows Calculator TracePointer vector, normalized from
        // "M0 0 l1371 1371 H538 l-538 538 Z" into its 18px display box.
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(point, true);
        context.LineTo(point + new Vector(12.93, 12.93));
        context.LineTo(point + new Vector(5.08, 12.93));
        context.LineTo(point + new Vector(0, 18));
        context.EndFigure(true);
        return geometry;
    }

    private void UpdateTrace(Point pointerPosition)
    {
        var candidate = FindNearestTracePoint(pointerPosition);
        if (candidate is null)
        {
            ClearTrace();
            return;
        }

        _traceScreenPoint = candidate.Value.ScreenPoint;
        _traceColor = candidate.Value.Color;
        _traceText = $"({candidate.Value.X.ToString("R", CultureInfo.CurrentCulture)}, " +
            $"{candidate.Value.Y.ToString("N15", CultureInfo.CurrentCulture)})";
        TraceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private TraceCandidate? FindNearestTracePoint(Point pointerPosition)
    {
        TraceCandidate? nearest = null;
        foreach (var equation in ViewModel?.GetRenderableEquations() ?? [])
        {
            switch (equation.Evaluator.Kind)
            {
                case GraphEquationKind.Explicit:
                    FindNearestExplicitPoint(equation, pointerPosition, ref nearest);
                    break;
                case GraphEquationKind.Polar:
                    FindNearestPolarPoint(equation, pointerPosition, ref nearest);
                    break;
                case GraphEquationKind.Implicit:
                case GraphEquationKind.Inequality:
                    FindNearestImplicitPoint(equation, pointerPosition, ref nearest);
                    break;
            }
        }

        return nearest is { DistanceSquared: <= TraceSearchRadius * TraceSearchRadius }
            ? nearest
            : null;
    }

    private void FindNearestExplicitPoint(
        GraphEquationRenderModel equation,
        Point pointerPosition,
        ref TraceCandidate? nearest)
    {
        var left = Math.Max(0, pointerPosition.X - TraceSearchRadius);
        var right = Math.Min(Bounds.Width, pointerPosition.X + TraceSearchRadius);
        for (var screenX = left; screenX <= right; screenX++)
        {
            var x = ScreenToGraphX(screenX);
            var y = SafeEvaluate(() => equation.Evaluator.EvaluateExplicit(x));
            if (!double.IsFinite(y))
            {
                continue;
            }
            ConsiderTraceCandidate(
                equation,
                new Point(screenX, GraphToScreenY(y)),
                x,
                y,
                pointerPosition,
                ref nearest);
        }
    }

    private void FindNearestPolarPoint(
        GraphEquationRenderModel equation,
        Point pointerPosition,
        ref TraceCandidate? nearest)
    {
        var sampleCount = Math.Max(720, (int)Math.Ceiling(Bounds.Width * 1.5));
        for (var sample = 0; sample <= sampleCount; sample++)
        {
            var theta = sample / (double)sampleCount * Math.Tau;
            var radius = SafeEvaluate(() => equation.Evaluator.EvaluatePolar(theta));
            if (!double.IsFinite(radius))
            {
                continue;
            }
            var x = radius * Math.Cos(theta);
            var y = radius * Math.Sin(theta);
            ConsiderTraceCandidate(
                equation,
                new Point(GraphToScreenX(x), GraphToScreenY(y)),
                x,
                y,
                pointerPosition,
                ref nearest);
        }
    }

    private void FindNearestImplicitPoint(
        GraphEquationRenderModel equation,
        Point pointerPosition,
        ref TraceCandidate? nearest)
    {
        const double cellSize = 4;
        var left = Math.Max(0, pointerPosition.X - TraceSearchRadius);
        var right = Math.Min(Bounds.Width, pointerPosition.X + TraceSearchRadius);
        var top = Math.Max(0, pointerPosition.Y - TraceSearchRadius);
        var bottom = Math.Min(Bounds.Height, pointerPosition.Y + TraceSearchRadius);
        for (var y = top; y < bottom; y += cellSize)
        {
            for (var x = left; x < right; x += cellSize)
            {
                FindImplicitCellCandidates(
                    equation,
                    pointerPosition,
                    x,
                    y,
                    Math.Min(right, x + cellSize),
                    Math.Min(bottom, y + cellSize),
                    ref nearest);
            }
        }
    }

    private void FindImplicitCellCandidates(
        GraphEquationRenderModel equation,
        Point pointerPosition,
        double left,
        double top,
        double right,
        double bottom,
        ref TraceCandidate? nearest)
    {
        var corners = new[]
        {
            new Point(left, top),
            new Point(right, top),
            new Point(right, bottom),
            new Point(left, bottom),
        };
        var values = corners.Select(point => SafeEvaluate(() =>
            equation.Evaluator.EvaluateImplicit(
                ScreenToGraphX(point.X),
                ScreenToGraphY(point.Y)))).ToArray();
        if (values.Any(value => !double.IsFinite(value)))
        {
            return;
        }

        for (var edge = 0; edge < corners.Length; edge++)
        {
            var next = (edge + 1) % corners.Length;
            var a = values[edge];
            var b = values[next];
            if ((a < 0) == (b < 0) && Math.Abs(a) > 1e-12 && Math.Abs(b) > 1e-12)
            {
                continue;
            }
            var denominator = Math.Abs(a) + Math.Abs(b);
            var ratio = denominator <= 1e-15 ? 0.5 : Math.Abs(a) / denominator;
            var screenPoint = new Point(
                corners[edge].X + (corners[next].X - corners[edge].X) * ratio,
                corners[edge].Y + (corners[next].Y - corners[edge].Y) * ratio);
            ConsiderTraceCandidate(
                equation,
                screenPoint,
                ScreenToGraphX(screenPoint.X),
                ScreenToGraphY(screenPoint.Y),
                pointerPosition,
                ref nearest);
        }
    }

    private static void ConsiderTraceCandidate(
        GraphEquationRenderModel equation,
        Point screenPoint,
        double x,
        double y,
        Point pointerPosition,
        ref TraceCandidate? nearest)
    {
        var difference = screenPoint - pointerPosition;
        var distanceSquared = difference.X * difference.X + difference.Y * difference.Y;
        if (nearest is not null && distanceSquared >= nearest.Value.DistanceSquared)
        {
            return;
        }
        nearest = new TraceCandidate(
            screenPoint,
            x,
            y,
            Color.Parse(equation.Color),
            distanceSquared);
    }

    private void ClearTrace()
    {
        if (_traceScreenPoint is null && string.IsNullOrEmpty(_traceText))
        {
            return;
        }
        _traceScreenPoint = null;
        _traceText = string.Empty;
        TraceChanged?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    private void RefreshTrace()
    {
        var trackingPosition = _isTracing ? _activeTraceCursorPosition : _pointerPosition;
        if (trackingPosition is { } position)
        {
            UpdateTrace(position);
        }
        else
        {
            ClearTrace();
        }
    }

    private Point ClampToBounds(Point point) => new(
        Math.Clamp(point.X, 0, Math.Max(0, Bounds.Width - 5)),
        Math.Clamp(point.Y, 0, Math.Max(0, Bounds.Height - 5)));

    private readonly record struct TraceCandidate(
        Point ScreenPoint,
        double X,
        double Y,
        Color Color,
        double DistanceSquared);

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
        RefreshTrace();
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }

    private void FitViewToGraph()
    {
        var viewport = GraphViewportFitter.Calculate(
            ViewModel?.GetRenderableEquations() ?? []);
        ApplyViewport(
            viewport.XMinimum,
            viewport.XMaximum,
            viewport.YMinimum,
            viewport.YMaximum);
    }

    private void ApplyViewport(
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum)
    {
        _xMinimum = xMinimum;
        _xMaximum = xMaximum;
        _yMinimum = yMinimum;
        _yMaximum = yMaximum;
        NotifyViewportChanged();
    }

    private void SetManualAdjustmentState(bool value)
    {
        if (_isManualAdjustment == value)
        {
            return;
        }

        _isManualAdjustment = value;
        ManualAdjustmentChanged?.Invoke(this, EventArgs.Empty);
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
        const double dotSpacing = 12;
        var color = ParseColor(equation.Color);
        var dotRadius = equation.LineWidth / 2;
        var dotBrush = new SolidColorBrush(Color.FromArgb(112, color.R, color.G, color.B));
        var boundaryPen = new Pen(
            new SolidColorBrush(color),
            equation.LineWidth,
            new DashStyle([2d, 1d], 0))
        {
            LineCap = PenLineCap.Flat,
        };

        for (var y = dotSpacing / 2; y < Bounds.Height; y += dotSpacing)
        {
            for (var x = dotSpacing / 2; x < Bounds.Width; x += dotSpacing)
            {
                var isInside = SafeEvaluateBoolean(() =>
                    equation.Evaluator.EvaluateInequality(
                        ScreenToGraphX(x),
                        ScreenToGraphY(y)));
                if (isInside)
                {
                    context.DrawEllipse(
                        dotBrush,
                        null,
                        new Point(x, y),
                        dotRadius,
                        dotRadius);
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
