namespace Calculator.Managed.Graphing;

public enum GraphEquationKind
{
    Explicit,
    Implicit,
    Polar,
    Inequality,
}

public enum GraphComparison
{
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
}

public enum GraphAnalysisCategory
{
    Domain,
    Range,
    XIntercept,
    YIntercept,
    Minima,
    Maxima,
    InflectionPoints,
    VerticalAsymptotes,
    HorizontalAsymptotes,
    ObliqueAsymptotes,
    Parity,
    Monotonicity,
    Complexity,
}

public enum GraphAnalysisStatus
{
    Value,
    None,
    Unknown,
    Unsupported,
    Error,
}

public sealed record GraphAnalysisValue(string Text, string Annotation = "");

public sealed record GraphAnalysisFeature(
    GraphAnalysisCategory Category,
    GraphAnalysisStatus Status,
    IReadOnlyList<GraphAnalysisValue> Values);

public sealed record GraphFunctionAnalysisResult(
    bool IsSupported,
    string ErrorMessage,
    IReadOnlyList<GraphAnalysisFeature> Features)
{
    public static GraphFunctionAnalysisResult Unsupported(string message) =>
        new(false, message, []);
}

/// <summary>
/// Platform-neutral counterpart of the expression object exposed by
/// GraphingInterfaces/Common.h. Third-party CAS types intentionally do not
/// cross this boundary.
/// </summary>
public interface IExpression
{
    uint ExpressionId { get; }
    string Source { get; }
    string Latex { get; }
    GraphEquationKind Kind { get; }
    GraphComparison Comparison { get; }
    IReadOnlyList<string> Variables { get; }
    bool IsEmptySet { get; }

    bool TryCreateEvaluator(
        IReadOnlyDictionary<string, double> arguments,
        out IGraphExpressionEvaluator evaluator,
        out string error);
}

public interface IGraphExpressionEvaluator
{
    GraphEquationKind Kind { get; }
    GraphComparison Comparison { get; }

    double EvaluateExplicit(double x);
    double EvaluateImplicit(double x, double y);
    double EvaluatePolar(double theta);
    bool EvaluateInequality(double x, double y);
}

/// <summary>
/// Managed equivalent of the parse/serialize portion of IMathSolver. Analysis
/// and rendering stay behind separate interfaces so either implementation can
/// be replaced without changing the ported calculator view models.
/// </summary>
public interface IMathSolver
{
    IExpression ParseInput(string input);
    string Serialize(IExpression expression);
    Task<GraphFunctionAnalysisResult> AnalyzeAsync(
        IExpression expression,
        IReadOnlyDictionary<string, double> arguments,
        CancellationToken cancellationToken);
}

public sealed class GraphingParseException(string message, int errorOffset = -1)
    : FormatException(message)
{
    public int ErrorOffset { get; } = errorOffset;
}
