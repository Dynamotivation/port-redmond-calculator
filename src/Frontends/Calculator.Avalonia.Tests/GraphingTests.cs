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
        ("graphing formats committed equations as latex", FormatsCommittedEquationsAsLatex),
        ("graphing commits drafts and maintains a placeholder", DraftsCommitAtTheFourteenFunctionLimit),
        ("graphing cleared equations remain allocated placeholders", ClearedEquationsRemainAllocated),
        ("graphing invalid rows consume slots and colors", InvalidRowsConsumeSlotsAndColors),
        ("graphing renumbers equations and reuses released colors", DeletionRenumbersAndReusesColors),
        ("graphing analyzes documented elementary functions", AnalyzesDocumentedElementaryFunctions),
        ("graphing preserves category-specific complexity limits", PreservesDocumentedComplexityLimits),
        ("graphing enforces the documented analysis format gate", EnforcesDocumentedAnalysisFormatGate),
        ("graphing analyzes a quadratic", AnalyzesQuadratic),
        ("graphing analyzes a high even power", AnalyzesHighEvenPower),
        ("graphing analysis preserves removable discontinuities", AnalysisPreservesRemovableDiscontinuities),
        ("graphing analysis distinguishes unknown from none", AnalysisDistinguishesUnknownFromNone),
        ("graphing analyzes the rational stress expression", AnalyzesRationalStressExpression),
        ("graphing analysis rejects implicit relations safely", AnalysisRejectsImplicitRelationsSafely),
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

    private static void FormatsCommittedEquationsAsLatex()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var equation = viewModel.Equations.Single();
        equation.DraftExpression = "x^2/2";
        viewModel.CommitEquation(equation);

        Assert(equation.IsValid && equation.FormattedExpression.Contains(@"\frac"),
            "a committed fraction should expose structured LaTeX");
        Assert(equation.ShowFormattedExpression,
            "a valid committed equation should show its formatted presentation");

        equation.IsEditing = true;
        equation.DraftExpression = "x^3/2";
        Assert(!equation.ShowFormattedExpression && equation.Expression == "x^2/2",
            "fresh edits should remain linear and must not reformat before commit");

        equation.CommitDraft();
        equation.IsEditing = false;
        Assert(equation.ShowFormattedExpression
            && equation.FormattedExpression.Contains("3"),
            "leaving the editor should parse and retypeset the new expression");
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

    private static void ClearedEquationsRemainAllocated()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var equation = viewModel.Equations.Single();
        var assignedColor = equation.Color;
        equation.DraftExpression = "x";
        Assert(viewModel.CommitEquation(equation), "the first equation should commit");
        Assert(viewModel.Equations.Count == 2, "a fresh placeholder should be appended");

        equation.DraftExpression = string.Empty;
        viewModel.CommitEquation(equation);

        Assert(equation.IsAllocated && !equation.HasExpression,
            "a manually cleared equation should remain an allocated empty row");
        Assert(equation.FunctionLabel == "f₁" && equation.FunctionIndexLabel == "1",
            "a cleared equation should retain its function number");
        Assert(equation.Color == assignedColor && equation.TileColor == assignedColor,
            "a cleared equation should retain its assigned tile color");
        Assert(!viewModel.Equations[^1].IsAllocated,
            "the separate new-expression placeholder should remain unallocated");
        Assert(viewModel.GetRenderableEquations().Count == 0,
            "a cleared allocated row should no longer render a graph");
    }

    private static void InvalidRowsConsumeSlotsAndColors()
    {
        var viewModel = new GraphingViewModel(CreateStrings());
        var invalid = viewModel.Equations.Single();
        var allocatedColor = invalid.Color;
        invalid.DraftExpression = "y";

        Assert(viewModel.CommitEquation(invalid),
            "committing a populated invalid placeholder should allocate the next row");
        Assert(invalid.HasExpression && !invalid.IsValid && invalid.HasError,
            "the invalid row should remain committed and visibly invalid");
        Assert(invalid.Color == allocatedColor,
            "a parse failure must retain its allocated color");
        Assert(viewModel.Equations.Count == 2 && !viewModel.Equations[^1].HasExpression,
            "an invalid committed row should still be followed by a placeholder");
        Assert(viewModel.GetRenderableEquations().Count == 0,
            "invalid allocated rows must not count as plotted equations");
    }

    private static void AnalyzesDocumentedElementaryFunctions()
    {
        var identity = Analyze("x");
        AssertFeatureContains(identity, GraphAnalysisCategory.Domain, "x ∈ ℝ");
        AssertFeatureContains(identity, GraphAnalysisCategory.Parity, "odd");

        var reciprocal = Analyze("1/x");
        AssertFeatureContains(reciprocal, GraphAnalysisCategory.Domain, "x ≠ 0");
        AssertFeatureContains(reciprocal, GraphAnalysisCategory.Range, "y ≠ 0");
        AssertFeatureContains(reciprocal, GraphAnalysisCategory.VerticalAsymptotes, "x = 0");

        var constant = Analyze("5");
        AssertFeatureContains(constant, GraphAnalysisCategory.Range, "{5}");
        AssertFeatureContains(constant, GraphAnalysisCategory.Monotonicity, "(-∞, ∞)");
        AssertFeatureContains(constant, GraphAnalysisCategory.Monotonicity, "Constant");

        var squareRoot = Analyze("sqrt(x)");
        AssertFeatureContains(squareRoot, GraphAnalysisCategory.Domain, "x ≥ 0");
        AssertFeatureContains(squareRoot, GraphAnalysisCategory.Range, "[0, ∞)");
        AssertFeatureContains(squareRoot, GraphAnalysisCategory.Minima, "(0, 0)");

        var logarithm = Analyze("log(x)");
        AssertFeatureContains(logarithm, GraphAnalysisCategory.Domain, "x > 0");
        AssertFeatureContains(logarithm, GraphAnalysisCategory.Range, "y ∈ ℝ");
        AssertFeatureContains(logarithm, GraphAnalysisCategory.VerticalAsymptotes, "x = 0");

        var sine = Analyze("sin(x)");
        AssertFeatureContains(sine, GraphAnalysisCategory.Range, "[-1, 1]");
        AssertFeatureContains(sine, GraphAnalysisCategory.XIntercept, "πn");
        AssertFeatureContains(sine, GraphAnalysisCategory.InflectionPoints, "πn");
    }

    private static void PreservesDocumentedComplexityLimits()
    {
        var chirp = Analyze("sin(x^2)");
        foreach (var category in new[]
                 {
                     GraphAnalysisCategory.Range,
                     GraphAnalysisCategory.Minima,
                     GraphAnalysisCategory.Maxima,
                     GraphAnalysisCategory.InflectionPoints,
                     GraphAnalysisCategory.Monotonicity,
                 })
        {
            Assert(Feature(chirp, category).Status == GraphAnalysisStatus.Unknown,
                $"sin(x^2) {category} should remain too complex, not none");
        }
        AssertFeatureContains(chirp, GraphAnalysisCategory.XIntercept, "√(πn)");
        AssertFeatureContains(chirp, GraphAnalysisCategory.Parity, "even");
        AssertFeatureContains(chirp, GraphAnalysisCategory.Complexity, "Period");

        var variableExponent = Analyze("x^x");
        AssertFeatureContains(variableExponent, GraphAnalysisCategory.Domain, "x > 0");
        AssertFeatureContains(variableExponent, GraphAnalysisCategory.Range, "e^(-1/e)");
        AssertFeatureContains(variableExponent, GraphAnalysisCategory.Minima, "(1/e, e^(-1/e))");
        Assert(Feature(variableExponent, GraphAnalysisCategory.InflectionPoints).Status
                == GraphAnalysisStatus.Unknown,
            "x^x inflection points should be too complex");
        AssertFeatureContains(variableExponent, GraphAnalysisCategory.Complexity, "Inflection points");
    }

    private static void EnforcesDocumentedAnalysisFormatGate()
    {
        var solver = new AngouriMathSolver();
        var reversed = solver.ParseInput("x=y");
        Assert(reversed.Kind == GraphEquationKind.Implicit,
            "x=y must retain its top-level orientation for analysis compatibility");
        var rejected = solver.AnalyzeAsync(
                reversed,
                new Dictionary<string, double>(),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert(!rejected.IsSupported && rejected.ErrorMessage.Contains("f(x)"),
            "x=y should open the f(x)-format rejection");

        Assert(Analyze("y=x").IsSupported, "exact y=f(x) syntax should be supported");

        var inequality = solver.ParseInput("x<1");
        var unsupported = solver.AnalyzeAsync(
                inequality,
                new Dictionary<string, double>(),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        Assert(!unsupported.IsSupported
            && unsupported.ErrorMessage.Contains("not supported", StringComparison.OrdinalIgnoreCase),
            "an inequality should retain the generic unsupported page");

        try
        {
            solver.ParseInput("y");
            throw new InvalidOperationException("bare y should not parse as a function of x");
        }
        catch (GraphingParseException)
        {
        }
    }

    private static void AnalyzesQuadratic()
    {
        var result = Analyze("(x+1)(x-1)");
        Assert(result.IsSupported, "the quadratic should be analyzable");
        Assert(result.Features.Count(feature => feature.Category != GraphAnalysisCategory.Complexity) == 12,
            "analysis should expose all twelve feature categories");
        AssertFeatureContains(result, GraphAnalysisCategory.Domain, "x ∈ ℝ");
        AssertFeatureContains(result, GraphAnalysisCategory.Range, "[-1, ∞)");
        AssertFeatureContains(result, GraphAnalysisCategory.XIntercept, "x = -1");
        AssertFeatureContains(result, GraphAnalysisCategory.XIntercept, "x = 1");
        AssertFeatureContains(result, GraphAnalysisCategory.YIntercept, "y = -1");
        AssertFeatureContains(result, GraphAnalysisCategory.Minima, "(0, -1)");
        Assert(Feature(result, GraphAnalysisCategory.Maxima).Status == GraphAnalysisStatus.None,
            "the quadratic should have no maxima");
        AssertFeatureContains(result, GraphAnalysisCategory.Parity, "even");
    }

    private static void AnalyzesHighEvenPower()
    {
        var result = Analyze("x^100");
        Assert(result.IsSupported, "x^100 should complete analysis");
        AssertFeatureContains(result, GraphAnalysisCategory.Range, "[0, ∞)");
        AssertFeatureContains(result, GraphAnalysisCategory.Minima, "(0, 0)");
        Assert(Feature(result, GraphAnalysisCategory.InflectionPoints).Status == GraphAnalysisStatus.None,
            "x^100 should not misclassify its stationary point as an inflection");
    }

    private static void AnalysisPreservesRemovableDiscontinuities()
    {
        var result = Analyze("x/x");
        AssertFeatureContains(result, GraphAnalysisCategory.Domain, "x ≠ 0");
        AssertFeatureContains(result, GraphAnalysisCategory.Range, "{1}");
        Assert(Feature(result, GraphAnalysisCategory.XIntercept).Status == GraphAnalysisStatus.None,
            "x/x should have no x-intercept");
        Assert(Feature(result, GraphAnalysisCategory.YIntercept).Status == GraphAnalysisStatus.None,
            "x/x should retain the missing y-intercept");
        Assert(Feature(result, GraphAnalysisCategory.VerticalAsymptotes).Status == GraphAnalysisStatus.None,
            "a removable hole is not a vertical asymptote");
        AssertFeatureContains(result, GraphAnalysisCategory.HorizontalAsymptotes, "y = 1");
    }

    private static void AnalysisDistinguishesUnknownFromNone()
    {
        var result = Analyze("sin(1/x)");
        AssertFeatureContains(result, GraphAnalysisCategory.Domain, "x ≠ 0");
        Assert(Feature(result, GraphAnalysisCategory.Range).Status == GraphAnalysisStatus.Unknown,
            "an uncalculated range must remain unknown");
        Assert(Feature(result, GraphAnalysisCategory.InflectionPoints).Status == GraphAnalysisStatus.Unknown,
            "uncalculated inflections must not become none");
        Assert(Feature(result, GraphAnalysisCategory.Monotonicity).Status == GraphAnalysisStatus.Unknown,
            "uncalculated monotonicity must remain unknown");
        AssertFeatureContains(result, GraphAnalysisCategory.HorizontalAsymptotes, "y = 0");
        AssertFeatureContains(result, GraphAnalysisCategory.Parity, "odd");
        Assert(result.Features.Any(feature => feature.Category == GraphAnalysisCategory.Complexity),
            "partial analysis should include a complexity summary");
    }

    private static void AnalyzesRationalStressExpression()
    {
        var result = Analyze(string.Concat(Enumerable.Repeat("(x+1/x)", 10)));
        AssertFeatureContains(result, GraphAnalysisCategory.Domain, "x ≠ 0");
        AssertFeatureContains(result, GraphAnalysisCategory.Range, "[1024, ∞)");
        Assert(Feature(result, GraphAnalysisCategory.XIntercept).Status == GraphAnalysisStatus.None,
            "complex roots must not be exposed as real graph intercepts");
        AssertFeatureContains(result, GraphAnalysisCategory.Minima, "(-1, 1024)");
        AssertFeatureContains(result, GraphAnalysisCategory.Minima, "(1, 1024)");
        AssertFeatureContains(result, GraphAnalysisCategory.VerticalAsymptotes, "x = 0");
        Assert(Feature(result, GraphAnalysisCategory.Monotonicity).Values.Count == 4,
            "the rational stress function should have four monotonic intervals");
    }

    private static void AnalysisRejectsImplicitRelationsSafely()
    {
        var solver = new AngouriMathSolver();
        foreach (var source in new[] { "x=2", "x^2+y^2=25" })
        {
            var expression = solver.ParseInput(source);
            var result = solver.AnalyzeAsync(
                    expression,
                    new Dictionary<string, double>(),
                    CancellationToken.None)
                .GetAwaiter()
                .GetResult();
            Assert(!result.IsSupported && result.ErrorMessage.Contains("not supported"),
                $"{source} should return an explained unsupported result");
        }
    }

    private static GraphFunctionAnalysisResult Analyze(string source)
    {
        var solver = new AngouriMathSolver();
        var expression = solver.ParseInput(source);
        return solver.AnalyzeAsync(
                expression,
                new Dictionary<string, double>(),
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();
    }

    private static GraphAnalysisFeature Feature(
        GraphFunctionAnalysisResult result,
        GraphAnalysisCategory category) =>
        result.Features.Single(feature => feature.Category == category);

    private static void AssertFeatureContains(
        GraphFunctionAnalysisResult result,
        GraphAnalysisCategory category,
        string expected)
    {
        Assert(
            Feature(result, category).Values.Any(value =>
                value.Text.Contains(expected, StringComparison.OrdinalIgnoreCase)
                || value.Annotation.Contains(expected, StringComparison.OrdinalIgnoreCase)),
            $"{category} should contain '{expected}'");
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
