using Calculator.Managed;
using Calculator.Managed.Graphing;

namespace Calculator.Avalonia.Tests;

internal static class GraphingTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("graphing evaluates explicit expressions", EvaluatesExplicitExpressions),
        ("graphing evaluates implicit equations", EvaluatesImplicitEquations),
        ("graphing evaluates polar expressions", EvaluatesPolarExpressions),
        ("graphing evaluates inequalities", EvaluatesInequalities),
        ("graphing view model tracks parameters", ViewModelTracksParameters),
    ];

    private static void EvaluatesExplicitExpressions()
    {
        var expression = new AngouriMathSolver().ParseInput("y = sin(x) + a*x");
        Assert(expression.Kind == GraphEquationKind.Explicit, "y=f(x) should be explicit");
        Assert(expression.Variables.SequenceEqual(["a"]), "a should be the only parameter");
        var evaluator = CreateEvaluator(expression, new Dictionary<string, double> { ["a"] = 2 });
        AssertNear(
            evaluator.EvaluateExplicit(0.5),
            Math.Sin(0.5) + 1,
            "explicit expression result");
    }

    private static void EvaluatesImplicitEquations()
    {
        var expression = new AngouriMathSolver().ParseInput("x^2 + y^2 = 4");
        Assert(expression.Kind == GraphEquationKind.Implicit, "circle should be implicit");
        var evaluator = CreateEvaluator(expression);
        AssertNear(evaluator.EvaluateImplicit(2, 0), 0, "circle boundary");
        Assert(evaluator.EvaluateImplicit(0, 0) < 0, "circle center should be inside");
    }

    private static void EvaluatesPolarExpressions()
    {
        var expression = new AngouriMathSolver().ParseInput("r = 2*cos(theta)");
        Assert(expression.Kind == GraphEquationKind.Polar, "r=f(theta) should be polar");
        var evaluator = CreateEvaluator(expression);
        AssertNear(evaluator.EvaluatePolar(0), 2, "polar radius at zero");
        AssertNear(evaluator.EvaluatePolar(Math.PI / 2), 0, "polar radius at pi/2");
    }

    private static void EvaluatesInequalities()
    {
        var expression = new AngouriMathSolver().ParseInput("y <= x + 1");
        Assert(expression.Kind == GraphEquationKind.Inequality, "comparison should be inequality");
        var evaluator = CreateEvaluator(expression);
        Assert(evaluator.EvaluateInequality(0, 0), "origin should satisfy y <= x + 1");
        Assert(!evaluator.EvaluateInequality(0, 2), "(0,2) should not satisfy y <= x + 1");
    }

    private static void ViewModelTracksParameters()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var equation = viewModel.Equations.Single();
        equation.Expression = "y = a*x + b";

        Assert(
            viewModel.Variables.Select(variable => variable.Name).SequenceEqual(["a", "b"]),
            "the variable controls should follow expression parameters");
        Assert(viewModel.GetRenderableEquations().Count == 1, "valid equation should render");

        viewModel.Variables.Single(variable => variable.Name == "a").Value = 3;
        var evaluator = viewModel.GetRenderableEquations().Single().Evaluator;
        AssertNear(evaluator.EvaluateExplicit(2), 7, "variable changes should rebuild evaluator");

        equation.Expression = "y = (";
        Assert(!equation.IsValid, "malformed expression should be rejected");
        Assert(viewModel.GetRenderableEquations().Count == 0, "invalid equation should not render");
    }

    private static IGraphExpressionEvaluator CreateEvaluator(
        IExpression expression,
        IReadOnlyDictionary<string, double>? arguments = null)
    {
        var created = expression.TryCreateEvaluator(
            arguments ?? new Dictionary<string, double>(),
            out var evaluator,
            out var error);
        Assert(created, $"evaluator creation failed: {error}");
        return evaluator;
    }

    private static GraphingStrings CreateStrings() => new(
        "Enter an expression",
        "Add equation",
        "Variables",
        "Zoom in",
        "Zoom out",
        "Reset view",
        "Show equations",
        "Show graph");

    private static void AssertNear(double actual, double expected, string message)
    {
        if (Math.Abs(actual - expected) > 1e-9)
        {
            throw new InvalidOperationException(
                $"{message}: expected {expected:R}, got {actual:R}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
