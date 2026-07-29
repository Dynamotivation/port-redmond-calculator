using System;
using System.Collections.Generic;
using System.Linq;
using Calculator.Managed;
using Calculator.Managed.Graphing;

namespace Calculator.Avalonia.Views.Graphing;

internal readonly record struct GraphViewport(
    double XMinimum,
    double XMaximum,
    double YMinimum,
    double YMaximum);

/// <summary>
/// Computes a useful initial display range from the same numerical evaluators
/// used by the renderer. X and Y are deliberately fitted independently, which
/// matches the Windows graph control's non-proportional axes.
/// </summary>
internal static class GraphViewportFitter
{
    private const double DefaultMinimum = -10;
    private const double DefaultMaximum = 10;
    private const int FunctionSamples = 801;
    private const int ContourDivisions = 80;
    private const double PaddingRatio = 0.1;
    private const double MinimumRange = 1e-5;
    private const double MaximumRange = 1e8;

    public static GraphViewport Calculate(
        IReadOnlyList<GraphEquationRenderModel> equations)
    {
        if (equations.Count == 0)
        {
            return DefaultViewport();
        }

        var xBounds = new BoundsAccumulator();
        var yBounds = new BoundsAccumulator();
        foreach (var equation in equations)
        {
            switch (equation.Evaluator.Kind)
            {
                case GraphEquationKind.Explicit:
                    IncludeExplicit(equation.Evaluator, xBounds, yBounds);
                    break;
                case GraphEquationKind.Polar:
                    IncludePolar(equation.Evaluator, xBounds, yBounds);
                    break;
                case GraphEquationKind.Implicit:
                case GraphEquationKind.Inequality:
                    IncludeImplicit(equation.Evaluator, xBounds, yBounds);
                    break;
            }
        }

        var xRange = CreateRange(xBounds, DefaultMinimum, DefaultMaximum);
        var yRange = CreateRange(yBounds, DefaultMinimum, DefaultMaximum);
        return new GraphViewport(xRange.Minimum, xRange.Maximum, yRange.Minimum, yRange.Maximum);
    }

    private static void IncludeExplicit(
        IGraphExpressionEvaluator evaluator,
        BoundsAccumulator xBounds,
        BoundsAccumulator yBounds)
    {
        var center = FindExplicitFeatureCenter(evaluator);
        var xMinimum = center + DefaultMinimum;
        var xMaximum = center + DefaultMaximum;
        xBounds.Include(xMinimum, xMaximum);

        var values = new List<double>(FunctionSamples);
        for (var index = 0; index < FunctionSamples; index++)
        {
            var x = xMinimum
                + (xMaximum - xMinimum) * index / (FunctionSamples - 1d);
            var y = SafeEvaluate(() => evaluator.EvaluateExplicit(x));
            if (double.IsFinite(y) && Math.Abs(y) <= MaximumRange * 10)
            {
                values.Add(y);
            }
        }

        IncludeRobust(values, yBounds);
    }

    private static double FindExplicitFeatureCenter(
        IGraphExpressionEvaluator evaluator)
    {
        const int divisions = 800;
        var candidates = new List<double>();
        foreach (var halfRange in new[] { 10d, 100d, 1000d, 10000d })
        {
            var step = halfRange * 2 / divisions;
            var previousX = -halfRange;
            var previous = SafeEvaluate(() => evaluator.EvaluateExplicit(previousX));
            var currentX = previousX + step;
            var current = SafeEvaluate(() => evaluator.EvaluateExplicit(currentX));

            for (var index = 1; index < divisions; index++)
            {
                var nextX = -halfRange + (index + 1) * step;
                var next = SafeEvaluate(() => evaluator.EvaluateExplicit(nextX));

                if (double.IsFinite(previous) && double.IsFinite(current)
                    && previous != 0
                    && current != 0
                    && Math.Sign(previous) != Math.Sign(current))
                {
                    candidates.Add(InterpolateRoot(previousX, previous, currentX, current));
                }

                if (double.IsNaN(previous) != double.IsNaN(current))
                {
                    candidates.Add((previousX + currentX) / 2);
                }

                if (double.IsInfinity(current)
                    && double.IsFinite(previous)
                    && double.IsFinite(next)
                    && Math.Sign(previous) != Math.Sign(next))
                {
                    candidates.Add(currentX);
                }

                if (double.IsFinite(previous)
                    && double.IsFinite(current)
                    && double.IsFinite(next))
                {
                    if (current == 0
                        && previous != 0
                        && next != 0
                        && Math.Sign(previous) != Math.Sign(next))
                    {
                        candidates.Add(currentX);
                    }

                    var incoming = current - previous;
                    var outgoing = next - current;
                    var scale = Math.Max(
                        1,
                        Math.Max(Math.Abs(previous), Math.Max(Math.Abs(current), Math.Abs(next))));
                    if (Math.Abs(incoming) > scale * 1e-10
                        && Math.Abs(outgoing) > scale * 1e-10
                        && Math.Sign(incoming) != Math.Sign(outgoing))
                    {
                        candidates.Add(currentX);
                    }
                }

                previousX = currentX;
                previous = current;
                currentX = nextX;
                current = next;
            }
        }

        if (candidates.Count == 0)
        {
            return 0;
        }

        var closest = candidates
            .Where(double.IsFinite)
            .OrderBy(Math.Abs)
            .FirstOrDefault();
        // Features already inside the standard window should not cause small,
        // distracting shifts of the axes.
        return Math.Abs(closest) <= 6 ? 0 : closest;
    }

    private static double InterpolateRoot(
        double firstX,
        double firstValue,
        double secondX,
        double secondValue)
    {
        var denominator = Math.Abs(firstValue) + Math.Abs(secondValue);
        if (denominator <= 1e-15)
        {
            return (firstX + secondX) / 2;
        }

        return firstX
            + (secondX - firstX) * Math.Abs(firstValue) / denominator;
    }

    private static void IncludePolar(
        IGraphExpressionEvaluator evaluator,
        BoundsAccumulator xBounds,
        BoundsAccumulator yBounds)
    {
        const int samples = 1441;
        var xValues = new List<double>(samples);
        var yValues = new List<double>(samples);
        for (var index = 0; index < samples; index++)
        {
            var theta = Math.Tau * index / (samples - 1d);
            var radius = SafeEvaluate(() => evaluator.EvaluatePolar(theta));
            if (!double.IsFinite(radius) || Math.Abs(radius) > MaximumRange * 10)
            {
                continue;
            }

            xValues.Add(radius * Math.Cos(theta));
            yValues.Add(radius * Math.Sin(theta));
        }

        IncludeRobust(xValues, xBounds);
        IncludeRobust(yValues, yBounds);
    }

    private static void IncludeImplicit(
        IGraphExpressionEvaluator evaluator,
        BoundsAccumulator xBounds,
        BoundsAccumulator yBounds)
    {
        var contour = FindContour(
            evaluator,
            DefaultMinimum,
            DefaultMaximum,
            DefaultMinimum,
            DefaultMaximum,
            ContourDivisions);

        if (contour.X.Count == 0)
        {
            foreach (var halfRange in new[] { 40d, 160d, 640d, 2560d, 10240d })
            {
                contour = FindContour(
                    evaluator,
                    -halfRange,
                    halfRange,
                    -halfRange,
                    halfRange,
                    ContourDivisions * 2);
                if (contour.X.Count > 0)
                {
                    break;
                }
            }
        }

        if (contour.X.Count == 0)
        {
            xBounds.Include(DefaultMinimum, DefaultMaximum);
            yBounds.Include(DefaultMinimum, DefaultMaximum);
            return;
        }

        if (contour.XMinimum < DefaultMinimum
            || contour.XMaximum > DefaultMaximum
            || contour.YMinimum < DefaultMinimum
            || contour.YMaximum > DefaultMaximum)
        {
            var centerX = (contour.X.Min() + contour.X.Max()) / 2;
            var centerY = (contour.Y.Min() + contour.Y.Max()) / 2;
            var refined = FindContour(
                evaluator,
                centerX + DefaultMinimum,
                centerX + DefaultMaximum,
                centerY + DefaultMinimum,
                centerY + DefaultMaximum,
                ContourDivisions);
            if (refined.X.Count > 0)
            {
                contour = refined;
            }
        }

        IncludeContourAxis(
            contour.X,
            contour.XMinimum,
            contour.XMaximum,
            contour.Divisions,
            xBounds);
        IncludeContourAxis(
            contour.Y,
            contour.YMinimum,
            contour.YMaximum,
            contour.Divisions,
            yBounds);
    }

    private static ContourSamples FindContour(
        IGraphExpressionEvaluator evaluator,
        double xMinimum,
        double xMaximum,
        double yMinimum,
        double yMaximum,
        int divisions)
    {
        var xStep = (xMaximum - xMinimum) / divisions;
        var yStep = (yMaximum - yMinimum) / divisions;
        var values = new double[divisions + 1, divisions + 1];
        for (var row = 0; row <= divisions; row++)
        {
            var y = yMinimum + row * yStep;
            for (var column = 0; column <= divisions; column++)
            {
                var x = xMinimum + column * xStep;
                values[row, column] = SafeEvaluate(() => evaluator.EvaluateImplicit(x, y));
            }
        }

        var contourX = new List<double>();
        var contourY = new List<double>();
        for (var row = 0; row <= divisions; row++)
        {
            var y = yMinimum + row * yStep;
            for (var column = 0; column < divisions; column++)
            {
                var x = xMinimum + column * xStep;
                if (TryInterpolateZero(values[row, column], values[row, column + 1], out var ratio))
                {
                    contourX.Add(x + ratio * xStep);
                    contourY.Add(y);
                }
            }
        }
        for (var column = 0; column <= divisions; column++)
        {
            var x = xMinimum + column * xStep;
            for (var row = 0; row < divisions; row++)
            {
                var y = yMinimum + row * yStep;
                if (TryInterpolateZero(values[row, column], values[row + 1, column], out var ratio))
                {
                    contourX.Add(x);
                    contourY.Add(y + ratio * yStep);
                }
            }
        }

        return new ContourSamples(
            contourX,
            contourY,
            xMinimum,
            xMaximum,
            yMinimum,
            yMaximum,
            divisions);
    }

    private static void IncludeContourAxis(
        List<double> values,
        double probeMinimum,
        double probeMaximum,
        int divisions,
        BoundsAccumulator bounds)
    {
        var minimum = values.Min();
        var maximum = values.Max();
        var boundaryTolerance = (probeMaximum - probeMinimum) / divisions * 1.5;
        if (minimum <= probeMinimum + boundaryTolerance
            || maximum >= probeMaximum - boundaryTolerance)
        {
            // The relation continues outside the probe window on this axis.
            // Preserve that axis rather than treating the clipped edge as a bound.
            bounds.Include(DefaultMinimum, DefaultMaximum);
            return;
        }

        bounds.Include(minimum, maximum);
    }

    private static bool TryInterpolateZero(double first, double second, out double ratio)
    {
        ratio = 0;
        if (!double.IsFinite(first) || !double.IsFinite(second))
        {
            return false;
        }
        if (Math.Abs(first) <= 1e-12)
        {
            return true;
        }
        if (Math.Abs(second) <= 1e-12)
        {
            ratio = 1;
            return true;
        }
        if (Math.Sign(first) == Math.Sign(second))
        {
            return false;
        }

        ratio = Math.Abs(first) / (Math.Abs(first) + Math.Abs(second));
        return true;
    }

    private static void IncludeRobust(
        List<double> values,
        BoundsAccumulator bounds)
    {
        if (values.Count == 0)
        {
            return;
        }

        values.Sort();
        if (values.Count < 20)
        {
            bounds.Include(values[0], values[^1]);
            return;
        }

        // Discontinuities can produce enormous isolated samples. The central
        // 96% retains the visible branches while preventing a pole from making
        // the rest of the graph effectively flat.
        var lower = values[(int)Math.Floor((values.Count - 1) * 0.02)];
        var upper = values[(int)Math.Ceiling((values.Count - 1) * 0.98)];
        bounds.Include(lower, upper);
    }

    private static (double Minimum, double Maximum) CreateRange(
        BoundsAccumulator bounds,
        double fallbackMinimum,
        double fallbackMaximum)
    {
        if (!bounds.HasValue)
        {
            return (fallbackMinimum, fallbackMaximum);
        }

        var minimum = bounds.Minimum;
        var maximum = bounds.Maximum;
        var span = maximum - minimum;
        if (!double.IsFinite(span) || span > MaximumRange)
        {
            return (fallbackMinimum, fallbackMaximum);
        }

        if (span < MinimumRange)
        {
            var center = (minimum + maximum) / 2;
            var halfRange = Math.Max(1, Math.Abs(center) * 0.05);
            return ClampRange(center - halfRange, center + halfRange);
        }

        var padding = Math.Max(MinimumRange, span * PaddingRatio);
        return ClampRange(minimum - padding, maximum + padding);
    }

    private static (double Minimum, double Maximum) ClampRange(
        double minimum,
        double maximum)
    {
        var center = (minimum + maximum) / 2;
        var span = Math.Clamp(maximum - minimum, MinimumRange, MaximumRange);
        return (center - span / 2, center + span / 2);
    }

    private static GraphViewport DefaultViewport() =>
        new(DefaultMinimum, DefaultMaximum, DefaultMinimum, DefaultMaximum);

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

    private sealed class BoundsAccumulator
    {
        public bool HasValue { get; private set; }
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }

        public void Include(double minimum, double maximum)
        {
            if (!double.IsFinite(minimum) || !double.IsFinite(maximum))
            {
                return;
            }
            if (minimum > maximum)
            {
                (minimum, maximum) = (maximum, minimum);
            }

            if (!HasValue)
            {
                Minimum = minimum;
                Maximum = maximum;
                HasValue = true;
                return;
            }

            Minimum = Math.Min(Minimum, minimum);
            Maximum = Math.Max(Maximum, maximum);
        }
    }

    private sealed record ContourSamples(
        List<double> X,
        List<double> Y,
        double XMinimum,
        double XMaximum,
        double YMinimum,
        double YMaximum,
        int Divisions);
}
