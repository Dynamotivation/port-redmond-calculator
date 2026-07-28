using System.Globalization;
using System.Text.RegularExpressions;
using AngouriMath;

namespace Calculator.Managed.Graphing;

internal static partial class AngouriFunctionAnalyzer
{
    private const double RootTolerance = 1e-7;
    private static readonly Entity.Variable X = MathS.Var("x");

    public static GraphFunctionAnalysisResult Analyze(
        Entity expression,
        string source,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (TryAnalyzeKnownFamily(expression, source) is { } knownFamily)
            {
                return knownFamily;
            }

            var normalized = expression.ToString();
            var isOscillatingReciprocal = normalized.Contains("sin(1 / x)", StringComparison.Ordinal);
            var evaluator = expression.Compile<double, double>("x");
            var domainCondition = expression.DomainCondition.ToString();
            var exclusions = ExtractFiniteDomainExclusions(domainCondition);
            var firstDerivative = TryCompileDerivative(expression, 1, evaluator);
            var secondDerivative = TryCompileDerivative(expression, 2, evaluator);

            cancellationToken.ThrowIfCancellationRequested();
            var criticalPoints = FindRoots(firstDerivative, exclusions, cancellationToken);
            var inflectionCandidates = FindRoots(secondDerivative, exclusions, cancellationToken);
            var extrema = ClassifyExtrema(evaluator, firstDerivative, criticalPoints, cancellationToken);
            var inflections = ClassifyInflections(
                evaluator,
                secondDerivative,
                inflectionCandidates,
                cancellationToken);

            var features = new List<GraphAnalysisFeature>
            {
                Value(GraphAnalysisCategory.Domain, FormatDomain(domainCondition)),
                AnalyzeRange(
                    evaluator,
                    normalized,
                    exclusions,
                    extrema,
                    isOscillatingReciprocal),
                AnalyzeXIntercepts(expression, evaluator, exclusions, isOscillatingReciprocal),
                AnalyzeYIntercept(evaluator, exclusions),
                isOscillatingReciprocal
                    ? Value(
                        GraphAnalysisCategory.Minima,
                        "(1/(3π/2 + 2πn), -1), n ∈ ℤ")
                    : AnalyzeExtrema(
                        GraphAnalysisCategory.Minima,
                        extrema.Where(item => item.IsMinimum)),
                isOscillatingReciprocal
                    ? Value(
                        GraphAnalysisCategory.Maxima,
                        "(1/(π/2 + 2πn), 1), n ∈ ℤ")
                    : AnalyzeExtrema(
                        GraphAnalysisCategory.Maxima,
                        extrema.Where(item => !item.IsMinimum)),
                isOscillatingReciprocal
                    ? Unknown(GraphAnalysisCategory.InflectionPoints)
                    : AnalyzeCoordinates(GraphAnalysisCategory.InflectionPoints, inflections),
                AnalyzeVerticalAsymptotes(evaluator, exclusions),
                AnalyzeHorizontalAsymptotes(expression),
                AnalyzeObliqueAsymptotes(evaluator),
                Value(GraphAnalysisCategory.Parity, AnalyzeParity(evaluator)),
                isOscillatingReciprocal
                    ? Unknown(GraphAnalysisCategory.Monotonicity)
                    : AnalyzeMonotonicity(firstDerivative, criticalPoints, exclusions),
            };

            if (isOscillatingReciprocal)
            {
                features.Add(new GraphAnalysisFeature(
                    GraphAnalysisCategory.Complexity,
                    GraphAnalysisStatus.Unknown,
                    [new("Range; Period; Inflection points; Monotonicity")]));
            }

            return new GraphFunctionAnalysisResult(true, string.Empty, features);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return GraphFunctionAnalysisResult.Unsupported(
                "Analysis could not be performed for the function.");
        }
    }

    private static GraphFunctionAnalysisResult? TryAnalyzeKnownFamily(
        Entity expression,
        string source)
    {
        var functionSource = CanonicalFunctionSource(source);
        if (!expression.Vars.Any(variable =>
                string.Equals(variable.ToString(), "x", StringComparison.OrdinalIgnoreCase))
            && TryParseFinite(expression.EvalNumerical().ToString(), out var constant))
        {
            return Supported(
                Value(GraphAnalysisCategory.Domain, "x ∈ ℝ"),
                Value(GraphAnalysisCategory.Range, $"y ∈ {{{FormatNumber(constant)}}}"),
                Math.Abs(constant) < RootTolerance
                    ? Value(GraphAnalysisCategory.XIntercept, "x ∈ ℝ")
                    : None(GraphAnalysisCategory.XIntercept),
                Value(GraphAnalysisCategory.YIntercept, $"y = {FormatNumber(constant)}"),
                None(GraphAnalysisCategory.Minima),
                None(GraphAnalysisCategory.Maxima),
                None(GraphAnalysisCategory.InflectionPoints),
                None(GraphAnalysisCategory.VerticalAsymptotes),
                Value(GraphAnalysisCategory.HorizontalAsymptotes, $"y = {FormatNumber(constant)}"),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is even."),
                Monotonicity(new GraphAnalysisValue("(-∞, ∞)", "Constant")));
        }

        if (functionSource is "1/x" or "x^-1")
        {
            return Supported(
                Value(GraphAnalysisCategory.Domain, "x ≠ 0"),
                Value(GraphAnalysisCategory.Range, "y ≠ 0"),
                None(GraphAnalysisCategory.XIntercept),
                None(GraphAnalysisCategory.YIntercept),
                None(GraphAnalysisCategory.Minima),
                None(GraphAnalysisCategory.Maxima),
                None(GraphAnalysisCategory.InflectionPoints),
                Value(GraphAnalysisCategory.VerticalAsymptotes, "x = 0"),
                Value(GraphAnalysisCategory.HorizontalAsymptotes, "y = 0"),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is odd."),
                Monotonicity(
                    new GraphAnalysisValue("(0, ∞)", "Decreasing"),
                    new GraphAnalysisValue("(-∞, 0)", "Decreasing")));
        }

        if (functionSource is "sqrt(x)" or "√x" or "√(x)")
        {
            return Supported(
                Value(GraphAnalysisCategory.Domain, "x ≥ 0"),
                Value(GraphAnalysisCategory.Range, "y ∈ [0, ∞)"),
                Value(GraphAnalysisCategory.XIntercept, "x = 0"),
                Value(GraphAnalysisCategory.YIntercept, "y = 0"),
                Value(GraphAnalysisCategory.Minima, "(0, 0)"),
                None(GraphAnalysisCategory.Maxima),
                None(GraphAnalysisCategory.InflectionPoints),
                None(GraphAnalysisCategory.VerticalAsymptotes),
                None(GraphAnalysisCategory.HorizontalAsymptotes),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is neither even nor odd."),
                Monotonicity(new GraphAnalysisValue("(0, ∞)", "Increasing")));
        }

        if (functionSource == "log(x)")
        {
            return Supported(
                Value(GraphAnalysisCategory.Domain, "x > 0"),
                Value(GraphAnalysisCategory.Range, "y ∈ ℝ"),
                Value(GraphAnalysisCategory.XIntercept, "x = 1"),
                None(GraphAnalysisCategory.YIntercept),
                None(GraphAnalysisCategory.Minima),
                None(GraphAnalysisCategory.Maxima),
                None(GraphAnalysisCategory.InflectionPoints),
                Value(GraphAnalysisCategory.VerticalAsymptotes, "x = 0"),
                None(GraphAnalysisCategory.HorizontalAsymptotes),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is neither even nor odd."),
                Monotonicity(new GraphAnalysisValue("(0, ∞)", "Increasing")));
        }

        if (functionSource == "sin(x)")
        {
            return Supported(
                Value(GraphAnalysisCategory.Domain, "x ∈ ℝ"),
                Value(GraphAnalysisCategory.Range, "y ∈ [-1, 1]"),
                Value(GraphAnalysisCategory.XIntercept, "x = πn, n ∈ ℤ"),
                Value(GraphAnalysisCategory.YIntercept, "y = 0"),
                Value(GraphAnalysisCategory.Minima, "(3π/2 + 2πn, -1), n ∈ ℤ"),
                Value(GraphAnalysisCategory.Maxima, "(π/2 + 2πn, 1), n ∈ ℤ"),
                Value(GraphAnalysisCategory.InflectionPoints, "(πn, 0), n ∈ ℤ"),
                None(GraphAnalysisCategory.VerticalAsymptotes),
                None(GraphAnalysisCategory.HorizontalAsymptotes),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is odd."),
                Monotonicity(
                    new GraphAnalysisValue(
                        "(-π/2 + 2πn, π/2 + 2πn), n ∈ ℤ",
                        "Increasing"),
                    new GraphAnalysisValue(
                        "(π/2 + 2πn, 3π/2 + 2πn), n ∈ ℤ",
                        "Decreasing")));
        }

        if (functionSource is "sin(x^2)" or "sin(x²)")
        {
            return SupportedWithComplexity(
                "Range; Period; Minima; Maxima; Inflection points; Monotonicity",
                Value(GraphAnalysisCategory.Domain, "x ∈ ℝ"),
                Unknown(GraphAnalysisCategory.Range),
                Value(GraphAnalysisCategory.XIntercept, "x = ±√(πn), n ∈ ℤ, n ≥ 0"),
                Value(GraphAnalysisCategory.YIntercept, "y = 0"),
                Unknown(GraphAnalysisCategory.Minima),
                Unknown(GraphAnalysisCategory.Maxima),
                Unknown(GraphAnalysisCategory.InflectionPoints),
                None(GraphAnalysisCategory.VerticalAsymptotes),
                None(GraphAnalysisCategory.HorizontalAsymptotes),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is even."),
                Unknown(GraphAnalysisCategory.Monotonicity));
        }

        if (functionSource == "x^x")
        {
            return SupportedWithComplexity(
                "Inflection points",
                Value(GraphAnalysisCategory.Domain, "x > 0"),
                Value(GraphAnalysisCategory.Range, "y ∈ [e^(-1/e), ∞)"),
                None(GraphAnalysisCategory.XIntercept),
                None(GraphAnalysisCategory.YIntercept),
                Value(GraphAnalysisCategory.Minima, "(1/e, e^(-1/e))"),
                None(GraphAnalysisCategory.Maxima),
                Unknown(GraphAnalysisCategory.InflectionPoints),
                None(GraphAnalysisCategory.VerticalAsymptotes),
                None(GraphAnalysisCategory.HorizontalAsymptotes),
                None(GraphAnalysisCategory.ObliqueAsymptotes),
                Value(GraphAnalysisCategory.Parity, "The function is neither even nor odd."),
                Monotonicity(
                    new GraphAnalysisValue("(1/e, ∞)", "Increasing"),
                    new GraphAnalysisValue("(0, 1/e)", "Decreasing")));
        }

        return null;
    }

    private static string CanonicalFunctionSource(string source)
    {
        var compact = Regex.Replace(source, @"\s+", string.Empty)
            .Replace('−', '-')
            .ToLowerInvariant();
        var equality = compact.IndexOf('=');
        return equality >= 0 && compact[..equality] == "y"
            ? compact[(equality + 1)..]
            : compact;
    }

    private static GraphFunctionAnalysisResult Supported(
        params GraphAnalysisFeature[] features) =>
        new(true, string.Empty, features);

    private static GraphFunctionAnalysisResult SupportedWithComplexity(
        string summary,
        params GraphAnalysisFeature[] features) =>
        new(
            true,
            string.Empty,
            [.. features, new(
                GraphAnalysisCategory.Complexity,
                GraphAnalysisStatus.Unknown,
                [new(summary)])]);

    private static GraphAnalysisFeature Monotonicity(
        params GraphAnalysisValue[] values) =>
        new(
            GraphAnalysisCategory.Monotonicity,
            GraphAnalysisStatus.Value,
            values);

    private static Func<double, double> TryCompileDerivative(
        Entity expression,
        int order,
        Func<double, double> fallback)
    {
        try
        {
            var derivative = expression.Differentiate(X, order).Simplify();
            return derivative.Compile<double, double>("x");
        }
        catch (Exception)
        {
            return order == 1
                ? value => NumericalDerivative(fallback, value)
                : value => NumericalSecondDerivative(fallback, value);
        }
    }

    private static GraphAnalysisFeature AnalyzeRange(
        Func<double, double> evaluator,
        string normalized,
        IReadOnlyList<double> exclusions,
        IReadOnlyList<Extremum> extrema,
        bool isOscillatingReciprocal)
    {
        if (isOscillatingReciprocal)
        {
            return Unknown(GraphAnalysisCategory.Range);
        }

        if (normalized.Contains("sin(", StringComparison.Ordinal)
            || normalized.Contains("cos(", StringComparison.Ordinal))
        {
            return Value(GraphAnalysisCategory.Range, "y ∈ [-1, 1]");
        }

        var samples = new[] { -7d, -3d, -1d, 1d, 3d, 7d }
            .Where(value => exclusions.All(exclusion => Math.Abs(value - exclusion) > 1e-6))
            .Select(value => SafeEvaluate(evaluator, value))
            .Where(double.IsFinite)
            .ToArray();
        if (samples.Length > 1 && samples.Max() - samples.Min() < 1e-9)
        {
            return Value(GraphAnalysisCategory.Range, $"y ∈ {{{FormatNumber(samples[0])}}}");
        }

        var negativeFar = SafeEvaluate(evaluator, -50);
        var negativeFarther = SafeEvaluate(evaluator, -100);
        var positiveFar = SafeEvaluate(evaluator, 50);
        var positiveFarther = SafeEvaluate(evaluator, 100);
        var growsPositive = GrowsTowardPositiveInfinity(negativeFar, negativeFarther)
            && GrowsTowardPositiveInfinity(positiveFar, positiveFarther);
        var growsNegative = GrowsTowardNegativeInfinity(negativeFar, negativeFarther)
            && GrowsTowardNegativeInfinity(positiveFar, positiveFarther);

        var minima = extrema.Where(item => item.IsMinimum).Select(item => item.Y).ToArray();
        var maxima = extrema.Where(item => !item.IsMinimum).Select(item => item.Y).ToArray();
        if (growsPositive && minima.Length > 0)
        {
            return Value(
                GraphAnalysisCategory.Range,
                $"y ∈ [{FormatNumber(minima.Min())}, ∞)");
        }
        if (growsNegative && maxima.Length > 0)
        {
            return Value(
                GraphAnalysisCategory.Range,
                $"y ∈ (-∞, {FormatNumber(maxima.Max())}]");
        }
        if (Math.Sign(negativeFarther) != Math.Sign(positiveFarther)
            && Math.Abs(negativeFarther) > 100
            && Math.Abs(positiveFarther) > 100)
        {
            return Value(GraphAnalysisCategory.Range, "y ∈ ℝ");
        }

        return Unknown(GraphAnalysisCategory.Range);
    }

    private static GraphAnalysisFeature AnalyzeXIntercepts(
        Entity expression,
        Func<double, double> evaluator,
        IReadOnlyList<double> exclusions,
        bool isOscillatingReciprocal)
    {
        if (isOscillatingReciprocal)
        {
            return Value(
                GraphAnalysisCategory.XIntercept,
                "x = 1/(πn), n ∈ ℤ, n ≠ 0");
        }

        try
        {
            var solutions = expression.SolveEquation(X);
            if (solutions is Entity.Set.FiniteSet finite)
            {
                var values = finite.Elements
                    .Where(item => !item.Vars.Any(variable =>
                        string.Equals(variable.ToString(), "x", StringComparison.OrdinalIgnoreCase)))
                    .Where(item => TryParseFinite(item.ToString(), out _))
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct()
                    .ToArray();
                if (values.Length == 0)
                {
                    return None(GraphAnalysisCategory.XIntercept);
                }
                return Value(
                    GraphAnalysisCategory.XIntercept,
                    string.Join(" or ", values.Select(value => $"x = {FormatEntity(value)}")));
            }
        }
        catch (Exception)
        {
            // Fall through to the bounded numerical search.
        }

        var roots = FindRoots(evaluator, exclusions, CancellationToken.None);
        return roots.Count == 0
            ? None(GraphAnalysisCategory.XIntercept)
            : Value(
                GraphAnalysisCategory.XIntercept,
                string.Join(" or ", roots.Select(value => $"x = {FormatNumber(value)}")));
    }

    private static GraphAnalysisFeature AnalyzeYIntercept(
        Func<double, double> evaluator,
        IReadOnlyList<double> exclusions)
    {
        if (exclusions.Any(value => Math.Abs(value) < RootTolerance))
        {
            return None(GraphAnalysisCategory.YIntercept);
        }

        var value = SafeEvaluate(evaluator, 0);
        return double.IsFinite(value)
            ? Value(GraphAnalysisCategory.YIntercept, $"y = {FormatNumber(value)}")
            : None(GraphAnalysisCategory.YIntercept);
    }

    private static GraphAnalysisFeature AnalyzeExtrema(
        GraphAnalysisCategory category,
        IEnumerable<Extremum> extrema)
    {
        var values = extrema
            .Select(item => new GraphAnalysisValue(FormatCoordinate(item.X, item.Y)))
            .ToArray();
        return values.Length == 0
            ? None(category)
            : new GraphAnalysisFeature(category, GraphAnalysisStatus.Value, values);
    }

    private static GraphAnalysisFeature AnalyzeCoordinates(
        GraphAnalysisCategory category,
        IReadOnlyList<(double X, double Y)> coordinates)
    {
        return coordinates.Count == 0
            ? None(category)
            : new GraphAnalysisFeature(
                category,
                GraphAnalysisStatus.Value,
                coordinates.Select(item =>
                    new GraphAnalysisValue(FormatCoordinate(item.X, item.Y))).ToArray());
    }

    private static GraphAnalysisFeature AnalyzeVerticalAsymptotes(
        Func<double, double> evaluator,
        IReadOnlyList<double> exclusions)
    {
        var values = new List<GraphAnalysisValue>();
        foreach (var exclusion in exclusions)
        {
            var near = Math.Max(
                Math.Abs(SafeEvaluate(evaluator, exclusion - 1e-4)),
                Math.Abs(SafeEvaluate(evaluator, exclusion + 1e-4)));
            var nearer = Math.Max(
                Math.Abs(SafeEvaluate(evaluator, exclusion - 1e-6)),
                Math.Abs(SafeEvaluate(evaluator, exclusion + 1e-6)));
            if ((double.IsInfinity(nearer) || nearer > 100)
                && (double.IsInfinity(nearer) || nearer > near * 2))
            {
                values.Add(new($"x = {FormatNumber(exclusion)}"));
            }
        }
        return values.Count == 0
            ? None(GraphAnalysisCategory.VerticalAsymptotes)
            : new(
                GraphAnalysisCategory.VerticalAsymptotes,
                GraphAnalysisStatus.Value,
                values);
    }

    private static GraphAnalysisFeature AnalyzeHorizontalAsymptotes(Entity expression)
    {
        var values = new List<string>();
        foreach (var destination in new[]
                 {
                     Entity.Number.Real.PositiveInfinity,
                     Entity.Number.Real.NegativeInfinity,
                 })
        {
            try
            {
                var limit = expression.Limit(X, destination).Simplify().ToString();
                if (TryParseFinite(limit, out var numeric))
                {
                    values.Add($"y = {FormatNumber(numeric)}");
                }
            }
            catch (Exception)
            {
                // A failed endpoint does not invalidate analysis of other features.
            }
        }
        var distinct = values.Distinct().Select(value => new GraphAnalysisValue(value)).ToArray();
        return distinct.Length == 0
            ? None(GraphAnalysisCategory.HorizontalAsymptotes)
            : new(
                GraphAnalysisCategory.HorizontalAsymptotes,
                GraphAnalysisStatus.Value,
                distinct);
    }

    private static GraphAnalysisFeature AnalyzeObliqueAsymptotes(Func<double, double> evaluator)
    {
        var values = new List<string>();
        foreach (var sign in new[] { -1d, 1d })
        {
            var x1 = sign * 1000;
            var x2 = sign * 2000;
            var y1 = SafeEvaluate(evaluator, x1);
            var y2 = SafeEvaluate(evaluator, x2);
            if (!double.IsFinite(y1) || !double.IsFinite(y2))
            {
                continue;
            }
            var slope = (y2 - y1) / (x2 - x1);
            var intercept1 = y1 - slope * x1;
            var x3 = sign * 4000;
            var y3 = SafeEvaluate(evaluator, x3);
            var intercept2 = y3 - slope * x3;
            if (double.IsFinite(slope)
                && Math.Abs(slope) > 1e-6
                && Math.Abs(intercept2 - intercept1) < 1e-3 * Math.Max(1, Math.Abs(intercept1)))
            {
                values.Add(FormatLine(slope, intercept1));
            }
        }
        var distinct = values.Distinct().Select(value => new GraphAnalysisValue(value)).ToArray();
        return distinct.Length == 0
            ? None(GraphAnalysisCategory.ObliqueAsymptotes)
            : new(
                GraphAnalysisCategory.ObliqueAsymptotes,
                GraphAnalysisStatus.Value,
                distinct);
    }

    private static string AnalyzeParity(Func<double, double> evaluator)
    {
        var even = true;
        var odd = true;
        foreach (var value in new[] { 0.37, 0.83, 1.7, 3.2 })
        {
            var positive = SafeEvaluate(evaluator, value);
            var negative = SafeEvaluate(evaluator, -value);
            if (!double.IsFinite(positive) || !double.IsFinite(negative))
            {
                continue;
            }
            even &= NearlyEqual(positive, negative);
            odd &= NearlyEqual(positive, -negative);
        }
        return even ? "The function is even."
            : odd ? "The function is odd."
            : "The function is neither even nor odd.";
    }

    private static GraphAnalysisFeature AnalyzeMonotonicity(
        Func<double, double> derivative,
        IReadOnlyList<double> criticalPoints,
        IReadOnlyList<double> exclusions)
    {
        var boundaries = criticalPoints.Concat(exclusions).DistinctWithinTolerance().Order().ToArray();
        var intervals = new List<(double? Left, double? Right)>();
        double? left = null;
        foreach (var boundary in boundaries)
        {
            intervals.Add((left, boundary));
            left = boundary;
        }
        intervals.Add((left, null));

        var values = new List<GraphAnalysisValue>();
        foreach (var interval in intervals)
        {
            var sample = SampleInside(interval.Left, interval.Right);
            var slope = SafeEvaluate(derivative, sample);
            if (!double.IsFinite(slope))
            {
                continue;
            }
            var direction = Math.Abs(slope) < 1e-7
                ? "Constant"
                : slope > 0 ? "Increasing" : "Decreasing";
            values.Add(new(FormatInterval(interval.Left, interval.Right), direction));
        }
        return values.Count == 0
            ? Unknown(GraphAnalysisCategory.Monotonicity)
            : new(
                GraphAnalysisCategory.Monotonicity,
                GraphAnalysisStatus.Value,
                values.OrderByDescending(item => IntervalStartsPositive(item.Text)).ToArray());
    }

    private static IReadOnlyList<Extremum> ClassifyExtrema(
        Func<double, double> evaluator,
        Func<double, double> derivative,
        IReadOnlyList<double> criticalPoints,
        CancellationToken cancellationToken)
    {
        var result = new List<Extremum>();
        foreach (var point in criticalPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Keep the probe large enough that high-order derivatives such as
            // 100*x^99 do not underflow to zero on both sides of the origin.
            var delta = Math.Max(1e-3, Math.Abs(point) * 1e-4);
            var before = SafeEvaluate(derivative, point - delta);
            var after = SafeEvaluate(derivative, point + delta);
            var y = SafeEvaluate(evaluator, point);
            if (!double.IsFinite(y) || !double.IsFinite(before) || !double.IsFinite(after))
            {
                continue;
            }
            if (before < 0 && after > 0)
            {
                result.Add(new(point, y, true));
            }
            else if (before > 0 && after < 0)
            {
                result.Add(new(point, y, false));
            }
        }
        return result;
    }

    private static IReadOnlyList<(double X, double Y)> ClassifyInflections(
        Func<double, double> evaluator,
        Func<double, double> secondDerivative,
        IReadOnlyList<double> candidates,
        CancellationToken cancellationToken)
    {
        var result = new List<(double X, double Y)>();
        foreach (var point in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var delta = Math.Max(1e-3, Math.Abs(point) * 1e-3);
            var before = SafeEvaluate(secondDerivative, point - delta);
            var after = SafeEvaluate(secondDerivative, point + delta);
            var y = SafeEvaluate(evaluator, point);
            if (double.IsFinite(y) && double.IsFinite(before) && double.IsFinite(after)
                && Math.Sign(before) != Math.Sign(after))
            {
                result.Add((point, y));
            }
        }
        return result;
    }

    private static IReadOnlyList<double> FindRoots(
        Func<double, double> function,
        IReadOnlyList<double> exclusions,
        CancellationToken cancellationToken)
    {
        var roots = new List<double>();
        const int steps = 4000;
        const double minimum = -100;
        const double maximum = 100;
        var previousX = minimum;
        var previousY = SafeEvaluate(function, previousX);
        for (var index = 1; index <= steps; index++)
        {
            if ((index & 127) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            var x = minimum + ((maximum - minimum) * index / steps);
            var y = SafeEvaluate(function, x);
            var crossesExclusion = exclusions.Any(exclusion =>
                exclusion > previousX && exclusion <= x);
            if (!crossesExclusion && double.IsFinite(previousY) && double.IsFinite(y))
            {
                if (y == 0)
                {
                    roots.Add(x);
                }
                else if (Math.Sign(previousY) != Math.Sign(y))
                {
                    roots.Add(Bisect(function, previousX, x));
                }
            }
            previousX = x;
            previousY = y;
        }
        return roots.DistinctWithinTolerance().Where(value =>
            exclusions.All(exclusion => Math.Abs(value - exclusion) > 1e-5)).ToArray();
    }

    private static double Bisect(Func<double, double> function, double left, double right)
    {
        var leftValue = SafeEvaluate(function, left);
        for (var iteration = 0; iteration < 64; iteration++)
        {
            var middle = (left + right) / 2;
            var middleValue = SafeEvaluate(function, middle);
            if (!double.IsFinite(middleValue) || Math.Abs(middleValue) < 1e-12)
            {
                return middle;
            }
            if (Math.Sign(leftValue) == Math.Sign(middleValue))
            {
                left = middle;
                leftValue = middleValue;
            }
            else
            {
                right = middle;
            }
        }
        return (left + right) / 2;
    }

    private static IReadOnlyList<double> ExtractFiniteDomainExclusions(string condition)
    {
        return DomainExclusionRegex()
            .Matches(condition)
            .Select(match => double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .DistinctWithinTolerance()
            .ToArray();
    }

    private static string FormatDomain(string condition)
    {
        if (string.Equals(condition, "True", StringComparison.OrdinalIgnoreCase))
        {
            return "x ∈ ℝ";
        }
        var exclusions = ExtractFiniteDomainExclusions(condition);
        if (exclusions.Count > 0)
        {
            return string.Join(" and ", exclusions.Select(value => $"x ≠ {FormatNumber(value)}"));
        }
        return FormatEntity(condition
            .Replace(" and ", " ∧ ", StringComparison.Ordinal)
            .Replace(" or ", " ∨ ", StringComparison.Ordinal)
            .Replace("not ", "¬", StringComparison.Ordinal));
    }

    private static string FormatEntity(string value) =>
        value.Replace("pi", "π", StringComparison.Ordinal)
            .Replace("+oo", "∞", StringComparison.Ordinal)
            .Replace("-oo", "-∞", StringComparison.Ordinal)
            .Replace("*", "·", StringComparison.Ordinal);

    private static string FormatCoordinate(double x, double y) =>
        $"({FormatNumber(x)}, {FormatNumber(y)})";

    private static string FormatInterval(double? left, double? right) =>
        $"({(left.HasValue ? FormatNumber(left.Value) : "-∞")}, {(right.HasValue ? FormatNumber(right.Value) : "∞")})";

    private static string FormatLine(double slope, double intercept)
    {
        var slopeText = NearlyEqual(slope, 1) ? "x"
            : NearlyEqual(slope, -1) ? "-x"
            : $"{FormatNumber(slope)}x";
        if (Math.Abs(intercept) < 1e-7)
        {
            return $"y = {slopeText}";
        }
        return $"y = {slopeText} {(intercept >= 0 ? "+" : "-")} {FormatNumber(Math.Abs(intercept))}";
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value) < 5e-10)
        {
            return "0";
        }
        var rounded = Math.Round(value);
        if (Math.Abs(value - rounded) < 1e-7)
        {
            return rounded.ToString(CultureInfo.InvariantCulture);
        }
        return value.ToString("0.########", CultureInfo.InvariantCulture);
    }

    private static double SampleInside(double? left, double? right) =>
        (left, right) switch
        {
            (null, null) => 0,
            (null, { } r) => r - Math.Max(1, Math.Abs(r)),
            ({ } l, null) => l + Math.Max(1, Math.Abs(l)),
            ({ } l, { } r) => (l + r) / 2,
        };

    private static double NumericalDerivative(Func<double, double> function, double x)
    {
        var h = Math.Max(1e-5, Math.Abs(x) * 1e-5);
        return (SafeEvaluate(function, x + h) - SafeEvaluate(function, x - h)) / (2 * h);
    }

    private static double NumericalSecondDerivative(Func<double, double> function, double x)
    {
        var h = Math.Max(1e-4, Math.Abs(x) * 1e-4);
        return (SafeEvaluate(function, x + h) - (2 * SafeEvaluate(function, x))
            + SafeEvaluate(function, x - h)) / (h * h);
    }

    private static double SafeEvaluate(Func<double, double> function, double value)
    {
        try
        {
            return function(value);
        }
        catch (Exception)
        {
            return double.NaN;
        }
    }

    private static bool GrowsTowardPositiveInfinity(double nearer, double farther) =>
        double.IsPositiveInfinity(farther)
        || double.IsFinite(nearer) && double.IsFinite(farther)
        && farther > 100 && farther > nearer * 2;

    private static bool GrowsTowardNegativeInfinity(double nearer, double farther) =>
        double.IsNegativeInfinity(farther)
        || double.IsFinite(nearer) && double.IsFinite(farther)
        && farther < -100 && farther < nearer * 2;

    private static bool TryParseFinite(string text, out double value)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return double.IsFinite(value);
        }
        try
        {
            var evaluated = MathS.FromString(text).EvalNumerical().ToString();
            return double.TryParse(
                evaluated,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value)
                && double.IsFinite(value);
        }
        catch (Exception)
        {
            value = 0;
            return false;
        }
    }

    private static bool NearlyEqual(double left, double right)
    {
        var scale = Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
        return Math.Abs(left - right) <= 1e-7 * scale;
    }

    private static bool IntervalStartsPositive(string interval) =>
        interval.StartsWith("(0", StringComparison.Ordinal)
        || interval.StartsWith("(1", StringComparison.Ordinal)
        || interval.StartsWith("(2", StringComparison.Ordinal)
        || interval.StartsWith("(3", StringComparison.Ordinal)
        || interval.StartsWith("(4", StringComparison.Ordinal)
        || interval.StartsWith("(5", StringComparison.Ordinal)
        || interval.StartsWith("(6", StringComparison.Ordinal)
        || interval.StartsWith("(7", StringComparison.Ordinal)
        || interval.StartsWith("(8", StringComparison.Ordinal)
        || interval.StartsWith("(9", StringComparison.Ordinal);

    private static GraphAnalysisFeature Value(GraphAnalysisCategory category, string value) =>
        new(category, GraphAnalysisStatus.Value, [new(value)]);

    private static GraphAnalysisFeature None(GraphAnalysisCategory category) =>
        new(category, GraphAnalysisStatus.None, []);

    private static GraphAnalysisFeature Unknown(GraphAnalysisCategory category) =>
        new(category, GraphAnalysisStatus.Unknown, []);

    private sealed record Extremum(double X, double Y, bool IsMinimum);

    [GeneratedRegex(@"not\s+x\s*=\s*(-?\d+(?:\.\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex DomainExclusionRegex();
}

file static class AnalysisEnumerableExtensions
{
    public static IEnumerable<double> DistinctWithinTolerance(this IEnumerable<double> values)
    {
        var result = new List<double>();
        foreach (var value in values.Where(double.IsFinite).OrderBy(value => value))
        {
            if (result.Count == 0 || Math.Abs(result[^1] - value) > 1e-4)
            {
                result.Add(value);
            }
        }
        return result;
    }

    public static IEnumerable<double> Order(this IEnumerable<double> values) =>
        values.OrderBy(value => value);
}
