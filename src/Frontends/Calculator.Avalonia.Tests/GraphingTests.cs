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
        ("graphing equation presentation state is preserved", EquationPresentationStateIsPreserved),
        ("graphing commits drafts and maintains a placeholder", DraftsCommitAtTheFourteenFunctionLimit),
        ("graphing renumbers equations and reuses released colors", DeletionRenumbersAndReusesColors),
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
        Assert(equation.HasError, "malformed expression should expose its error row");
        Assert(viewModel.GetRenderableEquations().Count == 0, "invalid equation should not render");

        equation.Expression = "x";
        Assert(!equation.HasError, "a valid expression should collapse its error row");
    }

    private static void EquationPresentationStateIsPreserved()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var equation = viewModel.Equations.Single();
        equation.Expression = "x";
        equation.Color = "#E81123";
        equation.LineStyle = GraphLineStyle.Dash;
        equation.LineWidth = 4;

        var rendered = viewModel.GetRenderableEquations().Single();
        Assert(rendered.Color == "#E81123", "selected line color should reach the renderer");
        Assert(rendered.LineStyle == GraphLineStyle.Dash, "selected line style should reach the renderer");
        AssertNear(rendered.LineWidth, 4, "selected line width should reach the renderer");
        Assert(equation.FunctionLabel == "f₁", "a populated first equation should be labeled f₁");

        equation.IsEnabled = false;
        Assert(equation.VisibilityAutomationName == "Show equation 1", "hidden equation should expose show action");
        Assert(viewModel.GetRenderableEquations().Count == 0, "hidden equation should not render");
        Assert(viewModel.Equations.Count(item => item.HasExpression) == 1,
            "hidden equation should remain part of the equation count");
        Assert(GraphingViewModel.EquationColors.Length == 14, "line options should expose fourteen colors");
    }

    private static void DraftsCommitAtTheFourteenFunctionLimit()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var first = viewModel.Equations.Single();
        first.DraftExpression = "x";

        Assert(!first.HasExpression && viewModel.GetRenderableEquations().Count == 0,
            "draft input should not parse or render before commit");
        Assert(viewModel.CommitEquation(first), "committing the first placeholder should append another");
        Assert(first.HasExpression && viewModel.GetRenderableEquations().Count == 1,
            "a committed valid draft should render");
        Assert(viewModel.Equations.Count == 2 && !viewModel.Equations[^1].HasExpression,
            "a committed placeholder should be followed by a new placeholder");

        for (var index = 1; index < 14; index++)
        {
            var placeholder = viewModel.Equations[^1];
            placeholder.DraftExpression = $"x + {index}";
            var addedPlaceholder = viewModel.CommitEquation(placeholder);
            Assert(addedPlaceholder == (index < 13),
                "a placeholder should be appended until fourteen functions are committed");
        }

        Assert(viewModel.Equations.Count == 14
            && viewModel.Equations.All(equation => equation.HasExpression)
            && !viewModel.CanAddEquation,
            "fourteen committed functions should be allowed without a fifteenth placeholder");

        viewModel.RemoveEquationCommand.Execute(viewModel.Equations[0]);
        Assert(viewModel.Equations.Count == 14
            && viewModel.Equations.Count(equation => equation.HasExpression) == 13
            && !viewModel.Equations[^1].HasExpression,
            "removing from a full list should restore a trailing placeholder");
    }

    private static void DeletionRenumbersAndReusesColors()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        for (var index = 0; index < 3; index++)
        {
            var placeholder = viewModel.Equations[^1];
            placeholder.DraftExpression = $"x + {index}";
            viewModel.CommitEquation(placeholder);
        }

        var first = viewModel.Equations[0];
        var removed = viewModel.Equations[1];
        var survivingThird = viewModel.Equations[2];
        var survivingColor = survivingThird.Color;
        Assert(removed.Color == GraphingViewModel.EquationColors[1],
            "the second equation should initially use the second palette color");

        viewModel.RemoveEquationCommand.Execute(removed);

        Assert(first.FunctionIndex == 0 && first.FunctionLabel == "f₁",
            "equations before the deletion should retain their number");
        Assert(survivingThird.FunctionIndex == 1 && survivingThird.FunctionLabel == "f₂",
            "equations after the deletion should be renumbered");
        Assert(survivingThird.Color == survivingColor,
            "renumbering should not change a populated equation's color");
        Assert(viewModel.GetRenderableEquations()
                .Select(equation => equation.Color)
                .SequenceEqual([first.Color, survivingThird.Color]),
            "renderable equations should remain in ascending function order");
        Assert(!viewModel.Equations[^1].HasExpression
            && viewModel.Equations[^1].Color == GraphingViewModel.EquationColors[1],
            "the placeholder should reserve the earliest released palette color");

        var reusedColorEquation = viewModel.Equations[^1];
        reusedColorEquation.DraftExpression = "x + 4";
        viewModel.CommitEquation(reusedColorEquation);
        Assert(reusedColorEquation.Color == GraphingViewModel.EquationColors[1],
            "the next committed equation should use the released color");
        Assert(viewModel.Equations[^1].Color == GraphingViewModel.EquationColors[3],
            "the following placeholder should advance to the next unused palette color");
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
