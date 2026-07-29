using System.Globalization;
using AngouriMath;

namespace Calculator.Managed.Graphing;

/// <summary>
/// AngouriMath-backed implementation of Microsoft's public graphing seam.
/// AngouriMath is isolated here because its public API and maintenance cadence
/// must not become part of Redmond Calculator's frontend contract.
/// </summary>
public sealed class AngouriMathSolver : IMathSolver
{
    private static int s_nextExpressionId;

    public IExpression ParseInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new GraphingParseException("Enter an expression.");
        }

        var normalized = NormalizeInput(input);
        var comparison = FindTopLevelComparison(normalized);
        try
        {
            if (comparison is null)
            {
                var explicitEntity = MathS.FromString(normalized);
                if (explicitEntity.Vars.Any(variable =>
                    IsReservedCoordinate(variable.ToString())))
                {
                    throw new GraphingParseException(
                        "A function expression must be written in terms of x.");
                }
                return Create(
                    input,
                    GraphEquationKind.Explicit,
                    GraphComparison.Equal,
                    explicitEntity,
                    explicitEntity.Latexise());
            }

            var (index, token, comparisonKind) = comparison.Value;
            var leftText = normalized[..index].Trim();
            var rightText = normalized[(index + token.Length)..].Trim();
            if (leftText.Length == 0 || rightText.Length == 0)
            {
                throw new GraphingParseException("Both sides of the equation are required.", index);
            }

            var left = MathS.FromString(leftText);
            var right = MathS.FromString(rightText);
            if (comparisonKind == GraphComparison.Equal)
            {
                if (IsVariable(leftText, "y") || IsFunctionOfX(leftText))
                {
                    return Create(
                        input,
                        GraphEquationKind.Explicit,
                        comparisonKind,
                        right,
                        FormatComparisonLatex(left, right, comparisonKind));
                }

                if (IsVariable(leftText, "r"))
                {
                    return Create(
                        input,
                        GraphEquationKind.Polar,
                        comparisonKind,
                        right,
                        FormatComparisonLatex(left, right, comparisonKind));
                }

                return Create(
                    input,
                    GraphEquationKind.Implicit,
                    comparisonKind,
                    left - right,
                    FormatComparisonLatex(left, right, comparisonKind));
            }

            return Create(
                input,
                GraphEquationKind.Inequality,
                comparisonKind,
                left - right,
                FormatComparisonLatex(left, right, comparisonKind));
        }
        catch (GraphingParseException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GraphingParseException(ToUserFacingParseError(exception));
        }
    }

    public string Serialize(IExpression expression) => expression.Source;

    public Task<GraphFunctionAnalysisResult> AnalyzeAsync(
        IExpression expression,
        IReadOnlyDictionary<string, double> arguments,
        CancellationToken cancellationToken)
    {
        if (expression is not AngouriExpression angouriExpression)
        {
            return Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                "Analysis is not supported for this function."));
        }

        if (angouriExpression.Kind != GraphEquationKind.Explicit)
        {
            if (IsReverseOrientedYEquality(angouriExpression.Source))
            {
                return Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                    "Analysis is only supported for functions in the f(x) format. Example: y=x"));
            }
            return Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                "Analysis is not supported for this function."));
        }

        if (!IsAcceptedAnalysisForm(angouriExpression.Source))
        {
            return Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                "Analysis is only supported for functions in the f(x) format. Example: y=x"));
        }

        Entity prepared;
        try
        {
            prepared = angouriExpression.Prepare(arguments);
        }
        catch (Exception)
        {
            return Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                "Analysis could not be performed for the function."));
        }

        return Task.Run(
            () => AngouriFunctionAnalyzer.Analyze(
                prepared,
                angouriExpression.Source,
                cancellationToken),
            cancellationToken);
    }

    private static AngouriExpression Create(
        string source,
        GraphEquationKind kind,
        GraphComparison comparison,
        Entity entity,
        string latex)
    {
        var id = checked((uint)Interlocked.Increment(ref s_nextExpressionId));
        return new AngouriExpression(id, source, latex, kind, comparison, entity);
    }

    private static string FormatComparisonLatex(
        Entity left,
        Entity right,
        GraphComparison comparison) =>
        $"{left.Latexise()} {comparison switch
        {
            GraphComparison.Less => "<",
            GraphComparison.LessOrEqual => @"\leq",
            GraphComparison.Greater => ">",
            GraphComparison.GreaterOrEqual => @"\geq",
            _ => "=",
        }} {right.Latexise()}";

    private static string NormalizeInput(string input) =>
        input.Trim()
            .Replace('−', '-')
            .Replace('×', '*')
            .Replace('÷', '/')
            .Replace("π", "pi", StringComparison.Ordinal)
            .Replace("θ", "theta", StringComparison.Ordinal);

    private static bool IsVariable(string value, string expected) =>
        string.Equals(value.Trim(), expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsReservedCoordinate(string value) =>
        IsVariable(value, "y")
        || IsVariable(value, "r")
        || IsVariable(value, "theta");

    private static bool IsAcceptedAnalysisForm(string source)
    {
        var comparison = FindTopLevelComparison(NormalizeInput(source));
        return comparison is null
            || comparison.Value.Comparison == GraphComparison.Equal
            && IsVariable(
                NormalizeInput(source)[..comparison.Value.Index],
                "y");
    }

    private static bool IsReverseOrientedYEquality(string source)
    {
        var normalized = NormalizeInput(source);
        var comparison = FindTopLevelComparison(normalized);
        if (comparison is null || comparison.Value.Comparison != GraphComparison.Equal)
        {
            return false;
        }
        var (index, token, _) = comparison.Value;
        return IsVariable(normalized[(index + token.Length)..], "y")
            && !IsVariable(normalized[..index], "y");
    }

    private static bool IsFunctionOfX(string value)
    {
        var compact = string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        return string.Equals(compact, "f(x)", StringComparison.OrdinalIgnoreCase);
    }

    private static (int Index, string Token, GraphComparison Comparison)? FindTopLevelComparison(string input)
    {
        var depth = 0;
        for (var index = 0; index < input.Length; index++)
        {
            depth += input[index] switch
            {
                '(' or '[' or '{' => 1,
                ')' or ']' or '}' => -1,
                _ => 0,
            };
            if (depth != 0)
            {
                continue;
            }

            if (index + 1 < input.Length)
            {
                var pair = input.Substring(index, 2);
                var pairComparison = pair switch
                {
                    "<=" => GraphComparison.LessOrEqual,
                    ">=" => GraphComparison.GreaterOrEqual,
                    _ => (GraphComparison?)null,
                };
                if (pairComparison is not null)
                {
                    return (index, pair, pairComparison.Value);
                }
            }

            var comparison = input[index] switch
            {
                '=' => GraphComparison.Equal,
                '<' => GraphComparison.Less,
                '>' => GraphComparison.Greater,
                '≤' => GraphComparison.LessOrEqual,
                '≥' => GraphComparison.GreaterOrEqual,
                _ => (GraphComparison?)null,
            };
            if (comparison is not null)
            {
                return (index, input[index].ToString(), comparison.Value);
            }
        }

        return null;
    }

    private static string ToUserFacingParseError(Exception exception)
    {
        var message = exception.Message;
        if (string.IsNullOrWhiteSpace(message))
        {
            return "This expression could not be parsed.";
        }

        var firstLine = message.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine)
            ? "This expression could not be parsed."
            : firstLine;
    }

    private sealed class AngouriExpression(
        uint expressionId,
        string source,
        string latex,
        GraphEquationKind kind,
        GraphComparison comparison,
        Entity entity) : IExpression
    {
        private static readonly StringComparer VariableComparer = StringComparer.OrdinalIgnoreCase;

        public uint ExpressionId { get; } = expressionId;
        public string Source { get; } = source;
        public string Latex { get; } = latex;
        public GraphEquationKind Kind { get; } = kind;
        public GraphComparison Comparison { get; } = comparison;
        public bool IsEmptySet => false;
        public IReadOnlyList<string> Variables { get; } = entity.Vars
            .Select(variable => variable.ToString())
            .Where(variable => !IsCoordinateVariable(variable, kind))
            .Distinct(VariableComparer)
            .OrderBy(variable => variable, VariableComparer)
            .ToArray();

        public bool TryCreateEvaluator(
            IReadOnlyDictionary<string, double> arguments,
            out IGraphExpressionEvaluator evaluator,
            out string error)
        {
            try
            {
                var prepared = Prepare(arguments);

                evaluator = Kind switch
                {
                    GraphEquationKind.Explicit => new ExplicitEvaluator(
                        prepared.Compile<double, double>("x")),
                    GraphEquationKind.Polar => new PolarEvaluator(
                        CompilePolar(prepared)),
                    GraphEquationKind.Implicit => new ImplicitEvaluator(
                        prepared.Compile<double, double, double>("x", "y")),
                    GraphEquationKind.Inequality => new InequalityEvaluator(
                        prepared.Compile<double, double, double>("x", "y"),
                        Comparison),
                    _ => throw new InvalidOperationException($"Unsupported graph equation kind {Kind}."),
                };
                error = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                evaluator = null!;
                error = ToUserFacingParseError(exception);
                return false;
            }
        }

        internal Entity Prepare(IReadOnlyDictionary<string, double> arguments)
        {
            var prepared = entity;
            foreach (var variable in Variables)
            {
                if (!arguments.TryGetValue(variable, out var value))
                {
                    throw new InvalidOperationException($"Variable {variable} does not have a value.");
                }

                var replacement = MathS.FromString(value.ToString("R", CultureInfo.InvariantCulture));
                prepared = prepared.Substitute(variable, replacement);
            }
            return prepared;
        }

        private static Func<double, double> CompilePolar(Entity prepared)
        {
            var thetaVariable = prepared.Vars
                .Select(variable => variable.ToString())
                .FirstOrDefault(variable =>
                    string.Equals(variable, "theta", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(variable, "t", StringComparison.OrdinalIgnoreCase));
            return prepared.Compile<double, double>(thetaVariable ?? "theta");
        }

        private static bool IsCoordinateVariable(string variable, GraphEquationKind kind) =>
            kind switch
            {
                GraphEquationKind.Explicit =>
                    VariableComparer.Equals(variable, "x"),
                GraphEquationKind.Polar =>
                    VariableComparer.Equals(variable, "theta")
                    || VariableComparer.Equals(variable, "t"),
                GraphEquationKind.Implicit or GraphEquationKind.Inequality =>
                    VariableComparer.Equals(variable, "x")
                    || VariableComparer.Equals(variable, "y"),
                _ => false,
            };
    }

    private sealed class ExplicitEvaluator(Func<double, double> function) : IGraphExpressionEvaluator
    {
        public GraphEquationKind Kind => GraphEquationKind.Explicit;
        public GraphComparison Comparison => GraphComparison.Equal;
        public double EvaluateExplicit(double x) => function(x);
        public double EvaluateImplicit(double x, double y) => y - function(x);
        public double EvaluatePolar(double theta) => function(theta);
        public bool EvaluateInequality(double x, double y) => false;
    }

    private sealed class PolarEvaluator(Func<double, double> function) : IGraphExpressionEvaluator
    {
        public GraphEquationKind Kind => GraphEquationKind.Polar;
        public GraphComparison Comparison => GraphComparison.Equal;
        public double EvaluateExplicit(double x) => function(x);
        public double EvaluateImplicit(double x, double y) => double.NaN;
        public double EvaluatePolar(double theta) => function(theta);
        public bool EvaluateInequality(double x, double y) => false;
    }

    private sealed class ImplicitEvaluator(Func<double, double, double> function) : IGraphExpressionEvaluator
    {
        public GraphEquationKind Kind => GraphEquationKind.Implicit;
        public GraphComparison Comparison => GraphComparison.Equal;
        public double EvaluateExplicit(double x) => double.NaN;
        public double EvaluateImplicit(double x, double y) => function(x, y);
        public double EvaluatePolar(double theta) => double.NaN;
        public bool EvaluateInequality(double x, double y) => false;
    }

    private sealed class InequalityEvaluator(
        Func<double, double, double> function,
        GraphComparison comparison) : IGraphExpressionEvaluator
    {
        public GraphEquationKind Kind => GraphEquationKind.Inequality;
        public GraphComparison Comparison { get; } = comparison;
        public double EvaluateExplicit(double x) => double.NaN;
        public double EvaluateImplicit(double x, double y) => function(x, y);
        public double EvaluatePolar(double theta) => double.NaN;
        public bool EvaluateInequality(double x, double y)
        {
            var value = function(x, y);
            return Comparison switch
            {
                GraphComparison.Less => value < 0,
                GraphComparison.LessOrEqual => value <= 0,
                GraphComparison.Greater => value > 0,
                GraphComparison.GreaterOrEqual => value >= 0,
                _ => Math.Abs(value) <= 1e-12,
            };
        }
    }
}
