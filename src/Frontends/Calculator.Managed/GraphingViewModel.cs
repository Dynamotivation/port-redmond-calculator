using System.Collections.ObjectModel;
using Calculator.Managed.Graphing;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

public sealed record GraphingStrings(
    string EnterExpressionPlaceholder,
    string AddEquationTooltip,
    string VariablesHeader,
    string ZoomInTooltip,
    string ZoomOutTooltip,
    string ResetViewTooltip,
    string SwitchToEquationMode,
    string SwitchToGraphMode,
    string StartTracingTooltip = "Start tracing",
    string StopTracingTooltip = "Stop tracing",
    string GraphOptionsHeading = "Graph options",
    string WindowHeading = "Window",
    string ResetViewText = "Reset view",
    string UnitsHeading = "Units",
    string RadiansText = "Radians",
    string DegreesText = "Degrees",
    string GradiansText = "Gradians",
    string LineThicknessHeading = "Line thickness",
    string GraphThemeHeading = "Graph theme",
    string AlwaysLightText = "Always light",
    string MatchAppThemeText = "Match app theme",
    string TrigonometryText = "Trigonometry",
    string InequalitiesText = "Inequalities",
    string FunctionText = "Function",
    string AnalyzeFunctionTooltip = "Analyze function",
    string ChangeEquationStyleTooltip = "Change equation style",
    string RemoveEquationTooltip = "Remove equation",
    string LineOptionsHeading = "Line options",
    string ColorHeading = "Color",
    string StyleHeading = "Style",
    string XMinimumHeading = "X-Min",
    string XMaximumHeading = "X-Max",
    string YMinimumHeading = "Y-Min",
    string YMaximumHeading = "Y-Max",
    string VariableMinimumHeading = "Min",
    string VariableStepHeading = "Step",
    string VariableMaximumHeading = "Max",
    string VariableOptionsTooltip = "Toggle variable options",
    string SmallLineWidthName = "Small line width",
    string MediumLineWidthName = "Medium line width",
    string LargeLineWidthName = "Large line width",
    string ExtraLargeLineWidthName = "Extra large line width",
    string AnalysisHeading = "Function analysis",
    string AnalysisBackTooltip = "Back to function list",
    string AnalysisEquationAutomationName = "Function analysis equation box",
    string AnalysisNotSupported = "Analysis is not supported for this function.",
    string AnalysisVariableIsNotX =
        "Analysis is only supported for functions in the f(x) format. Example: y=x",
    string AnalysisCouldNotBePerformed = "Analysis could not be performed for the function.",
    string DomainHeading = "Domain",
    string RangeHeading = "Range",
    string XInterceptHeading = "X-Intercept",
    string YInterceptHeading = "Y-Intercept",
    string MinimaHeading = "Minima",
    string MaximaHeading = "Maxima",
    string InflectionPointsHeading = "Inflection points",
    string VerticalAsymptotesHeading = "Vertical asymptotes",
    string HorizontalAsymptotesHeading = "Horizontal asymptotes",
    string ObliqueAsymptotesHeading = "Oblique asymptotes",
    string ParityHeading = "Parity",
    string MonotonicityHeading = "Monotonicity",
    string RangeUnknown = "Unable to calculate the range for this function.",
    string MinimaNone = "The function does not have any minima points.",
    string MaximaNone = "The function does not have any maxima points.",
    string InflectionPointsNone = "The function does not have any inflection points.",
    string VerticalAsymptotesNone = "The function does not have any vertical asymptotes.",
    string HorizontalAsymptotesNone = "The function does not have any horizontal asymptotes.",
    string ObliqueAsymptotesNone = "The function does not have any oblique asymptotes.",
    string MonotonicityUnknown = "Unable to determine the monotonicity of the function.",
    string TooComplexFeatures = "These features are too complex for Calculator to calculate:",
    string CutEquationText = "Cut",
    string CopyEquationText = "Copy",
    string PasteEquationText = "Paste",
    string UndoEquationText = "Undo",
    string SelectAllEquationText = "Select all",
    string UnexpectedEndOfExpression = "Unexpected end of expression",
    string AutomaticViewTooltip = "Automatic best fit");

public sealed record GraphEquationRenderModel(
    uint ExpressionId,
    string Color,
    GraphLineStyle LineStyle,
    double LineWidth,
    IGraphExpressionEvaluator Evaluator);

public enum GraphLineStyle
{
    Solid,
    Dash,
    Dot,
}

public enum GraphInvalidationReason
{
    Geometry,
    Appearance,
}

public sealed class GraphInvalidatedEventArgs(GraphInvalidationReason reason) : EventArgs
{
    public GraphInvalidationReason Reason { get; } = reason;
}

public sealed record GraphAnalysisDisplayValue(
    string Text,
    string Annotation,
    bool IsMath = false);

public sealed record GraphAnalysisDisplayItem(
    string Title,
    GraphAnalysisStatus Status,
    IReadOnlyList<GraphAnalysisDisplayValue> Values);

public partial class GraphVariableViewModel(
    string name,
    double value = 1,
    double minimum = -10,
    double maximum = 10,
    double step = 1) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial double Value { get; set; } = value;

    [ObservableProperty]
    public partial double Minimum { get; set; } = minimum;

    [ObservableProperty]
    public partial double Maximum { get; set; } = maximum;

    [ObservableProperty]
    public partial double Step { get; set; } = step;

    [ObservableProperty]
    public partial bool AreSettingsVisible { get; set; }

    public event EventHandler? ValueChanged;

    partial void OnValueChanged(double value) => ValueChanged?.Invoke(this, EventArgs.Empty);
}

public partial class GraphEquationViewModel : ObservableObject
{
    private readonly IMathSolver _solver;
    private readonly GraphingStrings _strings;
    private IExpression? _parsedExpression;
    private IGraphExpressionEvaluator? _evaluator;

    public GraphEquationViewModel(
        IMathSolver solver,
        int functionIndex,
        string color,
        GraphingStrings strings)
    {
        _solver = solver;
        _strings = strings;
        FunctionIndex = functionIndex;
        Color = color;
        Placeholder = strings.EnterExpressionPlaceholder;
    }

    public int FunctionIndex { get; private set; }
    public string Placeholder { get; }
    public string FunctionLabel => IsAllocated ? $"f{ToSubscript(FunctionIndex + 1)}" : "f";
    public string FunctionIndexLabel => IsAllocated ? (FunctionIndex + 1).ToString() : string.Empty;
    public string TileColor => !IsAllocated || !IsEnabled ? "#A6A6A6" : Color;
    public string VisibilityAutomationName =>
        $"{(IsEnabled ? "Hide" : "Show")} equation {FunctionIndex + 1}";
    public bool CanToggleVisibility => HasExpression;

    [ObservableProperty]
    public partial string Expression { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DraftExpression { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Color { get; set; } = string.Empty;

    [ObservableProperty]
    public partial GraphLineStyle LineStyle { get; set; } = GraphLineStyle.Solid;

    [ObservableProperty]
    public partial double LineWidth { get; set; } = 2;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public bool ShowErrorIcon => HasError && !IsEditing;

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    [ObservableProperty]
    public partial bool IsAllocated { get; private set; }

    public bool HasExpression => !string.IsNullOrWhiteSpace(Expression);
    public bool CanAnalyze => HasExpression && IsValid;
    public bool UsesAreaRendering =>
        _parsedExpression?.Kind == GraphEquationKind.Inequality;
    public bool CanCustomizeLineStyle => !UsesAreaRendering;
    public GraphLineStyle EffectiveLineStyle =>
        UsesAreaRendering ? GraphLineStyle.Dash : LineStyle;
    public string FormattedExpression => _parsedExpression?.Latex ?? string.Empty;
    public bool ShowFormattedExpression => HasExpression && IsValid;
    public bool ShowPlaceholder => string.IsNullOrEmpty(DraftExpression);
    public bool ShowEquationActions =>
        IsAllocated && (!IsEditing || string.IsNullOrWhiteSpace(DraftExpression));
    public bool ShowStandardEquationActions => ShowEquationActions && !HasError;
    public bool ShowEditingClear =>
        IsEditing && !string.IsNullOrWhiteSpace(DraftExpression);

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    public event EventHandler<GraphInvalidatedEventArgs>? EquationChanged;

    internal void SetFunctionIndex(int functionIndex)
    {
        if (FunctionIndex == functionIndex)
        {
            return;
        }

        FunctionIndex = functionIndex;
        OnPropertyChanged(nameof(FunctionIndex));
        OnPropertyChanged(nameof(FunctionLabel));
        OnPropertyChanged(nameof(FunctionIndexLabel));
        OnPropertyChanged(nameof(VisibilityAutomationName));
    }

    public bool CommitDraft()
    {
        if (!string.Equals(Expression, DraftExpression, StringComparison.Ordinal))
        {
            Expression = DraftExpression;
        }
        return HasExpression;
    }

    partial void OnExpressionChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !IsAllocated)
        {
            IsAllocated = true;
        }
        if (!string.Equals(DraftExpression, value, StringComparison.Ordinal))
        {
            DraftExpression = value;
        }
        OnPropertyChanged(nameof(HasExpression));
        OnPropertyChanged(nameof(FunctionLabel));
        OnPropertyChanged(nameof(FunctionIndexLabel));
        OnPropertyChanged(nameof(CanToggleVisibility));
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(ShowEquationActions));
        OnPropertyChanged(nameof(TileColor));
        Parse();
        if (UsesAreaRendering && LineStyle != GraphLineStyle.Dash)
        {
            LineStyle = GraphLineStyle.Dash;
        }
        OnPropertyChanged(nameof(UsesAreaRendering));
        OnPropertyChanged(nameof(CanCustomizeLineStyle));
        OnPropertyChanged(nameof(EffectiveLineStyle));
        OnPropertyChanged(nameof(FormattedExpression));
        OnPropertyChanged(nameof(ShowFormattedExpression));
        EquationChanged?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Geometry));
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(VisibilityAutomationName));
        OnPropertyChanged(nameof(TileColor));
        EquationChanged?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Geometry));
    }

    partial void OnColorChanged(string value)
    {
        OnPropertyChanged(nameof(TileColor));
        EquationChanged?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Appearance));
    }
    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(ShowErrorIcon));
        OnPropertyChanged(nameof(ShowStandardEquationActions));
    }
    partial void OnIsValidChanged(bool value)
    {
        OnPropertyChanged(nameof(CanAnalyze));
        OnPropertyChanged(nameof(ShowFormattedExpression));
    }
    partial void OnIsAllocatedChanged(bool value)
    {
        OnPropertyChanged(nameof(FunctionLabel));
        OnPropertyChanged(nameof(FunctionIndexLabel));
        OnPropertyChanged(nameof(TileColor));
        OnPropertyChanged(nameof(ShowEquationActions));
        OnPropertyChanged(nameof(ShowStandardEquationActions));
    }
    partial void OnDraftExpressionChanged(string value)
    {
        OnPropertyChanged(nameof(ShowPlaceholder));
        OnPropertyChanged(nameof(ShowEditingClear));
        OnPropertyChanged(nameof(ShowEquationActions));
        OnPropertyChanged(nameof(ShowStandardEquationActions));
    }
    partial void OnIsEditingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowEquationActions));
        OnPropertyChanged(nameof(ShowStandardEquationActions));
        OnPropertyChanged(nameof(ShowEditingClear));
        OnPropertyChanged(nameof(ShowFormattedExpression));
        OnPropertyChanged(nameof(ShowErrorIcon));
    }
    partial void OnLineStyleChanged(GraphLineStyle value)
    {
        OnPropertyChanged(nameof(EffectiveLineStyle));
        EquationChanged?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Appearance));
    }
    partial void OnLineWidthChanged(double value) =>
        EquationChanged?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Appearance));

    internal IEnumerable<string> GetVariables() => _parsedExpression?.Variables ?? [];

    internal void RebuildEvaluator(IReadOnlyDictionary<string, double> arguments)
    {
        _evaluator = null;
        if (_parsedExpression is null)
        {
            return;
        }

        if (_parsedExpression.TryCreateEvaluator(arguments, out var evaluator, out var error))
        {
            _evaluator = evaluator;
            ErrorMessage = string.Empty;
            IsValid = true;
        }
        else
        {
            ErrorMessage = error;
            IsValid = false;
        }
    }

    internal GraphEquationRenderModel? CreateRenderModel()
    {
        if (!IsEnabled || !IsValid || _parsedExpression is null || _evaluator is null)
        {
            return null;
        }

        return new GraphEquationRenderModel(
            _parsedExpression.ExpressionId,
            Color,
            EffectiveLineStyle,
            LineWidth,
            _evaluator);
    }

    internal Task<GraphFunctionAnalysisResult> AnalyzeAsync(
        IReadOnlyDictionary<string, double> arguments,
        CancellationToken cancellationToken)
    {
        return _parsedExpression is null
            ? Task.FromResult(GraphFunctionAnalysisResult.Unsupported(
                "Analysis could not be performed for the function."))
            : _solver.AnalyzeAsync(_parsedExpression, arguments, cancellationToken);
    }

    private static string ToSubscript(int value) =>
        string.Concat(value.ToString().Select(character => character switch
        {
            '0' => '₀',
            '1' => '₁',
            '2' => '₂',
            '3' => '₃',
            '4' => '₄',
            '5' => '₅',
            '6' => '₆',
            '7' => '₇',
            '8' => '₈',
            '9' => '₉',
            _ => character,
        }));

    private void Parse()
    {
        _parsedExpression = null;
        _evaluator = null;
        if (!HasExpression)
        {
            ErrorMessage = string.Empty;
            IsValid = false;
            return;
        }

        try
        {
            _parsedExpression = _solver.ParseInput(Expression);
            ErrorMessage = string.Empty;
            IsValid = true;
        }
        catch (GraphingParseException exception)
        {
            ErrorMessage = exception.ErrorCode switch
            {
                GraphingParseErrorCode.UnexpectedEndOfExpression =>
                    _strings.UnexpectedEndOfExpression,
                _ => exception.Message,
            };
            IsValid = false;
        }
    }
}

public partial class GraphingViewModel : ObservableObject
{
    public static readonly string[] EquationColors =
    [
        "#0063B1",
        "#107C10",
        "#E81123",
        "#FFB900",
        "#00B7C3",
        "#00CC6A",
        "#E3008C",
        "#F7630C",
        "#6600CC",
        "#008055",
        "#B31564",
        "#8E562E",
        "#58595B",
        "#000000",
    ];

    private readonly IMathSolver _solver;

    public GraphingViewModel(GraphingStrings strings, IMathSolver? solver = null)
    {
        Strings = strings;
        _solver = solver ?? new AngouriMathSolver();
        AddEquation();
    }

    public GraphingStrings Strings { get; }
    public ObservableCollection<GraphEquationViewModel> Equations { get; } = [];
    public ObservableCollection<GraphVariableViewModel> Variables { get; } = [];
    public ObservableCollection<GraphAnalysisDisplayItem> AnalysisItems { get; } = [];
    public bool HasVariables => Variables.Count > 0;
    private CancellationTokenSource? _analysisCancellation;

    [ObservableProperty]
    public partial GraphEquationViewModel? SelectedEquation { get; set; }

    [ObservableProperty]
    public partial bool IsAnalysisVisible { get; private set; }

    [ObservableProperty]
    public partial bool IsAnalyzing { get; private set; }

    [ObservableProperty]
    public partial string AnalysisError { get; private set; } = string.Empty;

    public bool HasAnalysisError => !string.IsNullOrEmpty(AnalysisError);
    partial void OnAnalysisErrorChanged(string value) =>
        OnPropertyChanged(nameof(HasAnalysisError));

    public bool CanAddEquation => Equations.Count < EquationColors.Length;
    public event EventHandler<GraphInvalidatedEventArgs>? GraphInvalidated;

    [RelayCommand(CanExecute = nameof(CanAddEquation))]
    private void AddEquation()
    {
        var functionIndex = Equations.Count;
        var equation = new GraphEquationViewModel(
            _solver,
            functionIndex,
            GetNextUnusedColor(),
            Strings);
        equation.EquationChanged += OnEquationChanged;
        Equations.Add(equation);
        SelectedEquation = equation;
        OnPropertyChanged(nameof(CanAddEquation));
        AddEquationCommand.NotifyCanExecuteChanged();
        RefreshGraph();
    }

    [RelayCommand]
    private void RemoveEquation(GraphEquationViewModel? equation)
    {
        if (equation is null || !Equations.Contains(equation))
        {
            return;
        }

        equation.EquationChanged -= OnEquationChanged;
        Equations.Remove(equation);
        RenumberEquations();
        if (Equations.Count == 0)
        {
            AddEquation();
            return;
        }

        if (CanAddEquation && Equations.All(item => item.IsAllocated))
        {
            AddEquation();
            return;
        }

        AssignUnusedColorsToPlaceholders();

        if (ReferenceEquals(SelectedEquation, equation))
        {
            CloseAnalysis();
            SelectedEquation = Equations[^1];
        }

        OnPropertyChanged(nameof(CanAddEquation));
        AddEquationCommand.NotifyCanExecuteChanged();
        RefreshGraph();
    }

    private string GetNextUnusedColor(IEnumerable<GraphEquationViewModel>? equations = null)
    {
        var usedColors = (equations ?? Equations)
            .Select(item => item.Color)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return EquationColors.FirstOrDefault(color => !usedColors.Contains(color))
            ?? EquationColors[0];
    }

    private void RenumberEquations()
    {
        for (var index = 0; index < Equations.Count; index++)
        {
            Equations[index].SetFunctionIndex(index);
        }
    }

    private void AssignUnusedColorsToPlaceholders()
    {
        var assignedEquations = new List<GraphEquationViewModel>(
            Equations.Where(item => item.IsAllocated));
        foreach (var placeholder in Equations.Where(item => !item.IsAllocated))
        {
            placeholder.Color = GetNextUnusedColor(assignedEquations);
            assignedEquations.Add(placeholder);
        }
    }

    public bool CommitEquation(GraphEquationViewModel equation)
    {
        if (!Equations.Contains(equation))
        {
            return false;
        }

        var convertedPlaceholder = !equation.IsAllocated;
        if (!equation.CommitDraft() || !convertedPlaceholder
            || !ReferenceEquals(Equations.LastOrDefault(), equation)
            || !AddEquationCommand.CanExecute(null))
        {
            return false;
        }

        AddEquationCommand.Execute(null);
        return true;
    }

    public IReadOnlyList<GraphEquationRenderModel> GetRenderableEquations() =>
        Equations
            .OrderBy(equation => equation.FunctionIndex)
            .Select(equation => equation.CreateRenderModel())
            .Where(model => model is not null)
            .Cast<GraphEquationRenderModel>()
            .ToArray();

    public async Task AnalyzeEquationAsync(GraphEquationViewModel equation)
    {
        if (!Equations.Contains(equation) || !equation.CanAnalyze)
        {
            return;
        }

        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _analysisCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var cancellationToken = _analysisCancellation.Token;

        SelectedEquation = equation;
        AnalysisItems.Clear();
        AnalysisError = string.Empty;
        IsAnalysisVisible = true;
        IsAnalyzing = true;

        try
        {
            var arguments = Variables.ToDictionary(
                variable => variable.Name,
                variable => variable.Value,
                StringComparer.OrdinalIgnoreCase);
            var result = await equation.AnalyzeAsync(arguments, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.IsSupported)
            {
                AnalysisError = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? Strings.AnalysisNotSupported
                    : LocalizeAnalysisError(result.ErrorMessage);
                return;
            }

            foreach (var feature in result.Features)
            {
                AnalysisItems.Add(ToDisplayItem(feature));
            }
        }
        catch (OperationCanceledException)
        {
            if (IsAnalysisVisible)
            {
                AnalysisError = Strings.AnalysisCouldNotBePerformed;
            }
        }
        catch (Exception)
        {
            AnalysisError = Strings.AnalysisCouldNotBePerformed;
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    public void CloseAnalysis()
    {
        _analysisCancellation?.Cancel();
        _analysisCancellation?.Dispose();
        _analysisCancellation = null;
        IsAnalyzing = false;
        IsAnalysisVisible = false;
        AnalysisError = string.Empty;
        AnalysisItems.Clear();
    }

    private void OnEquationChanged(object? sender, GraphInvalidatedEventArgs e)
    {
        if (IsAnalysisVisible && ReferenceEquals(sender, SelectedEquation))
        {
            CloseAnalysis();
        }
        if (e.Reason == GraphInvalidationReason.Geometry)
        {
            RefreshGraph();
        }
        else
        {
            GraphInvalidated?.Invoke(this, e);
        }
    }

    private void OnVariableValueChanged(object? sender, EventArgs e) => RefreshEvaluators();

    private void RefreshGraph()
    {
        var existingVariables = Variables.ToDictionary(
            variable => variable.Name,
            StringComparer.OrdinalIgnoreCase);
        var variableNames = Equations
            .SelectMany(equation => equation.GetVariables())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var variable in Variables)
        {
            variable.ValueChanged -= OnVariableValueChanged;
        }
        Variables.Clear();
        foreach (var name in variableNames)
        {
            var variable = existingVariables.TryGetValue(name, out var previous)
                ? new GraphVariableViewModel(
                    previous.Name,
                    previous.Value,
                    previous.Minimum,
                    previous.Maximum,
                    previous.Step)
                : new GraphVariableViewModel(name);
            variable.ValueChanged += OnVariableValueChanged;
            Variables.Add(variable);
        }
        OnPropertyChanged(nameof(HasVariables));

        RefreshEvaluators();
    }

    private void RefreshEvaluators()
    {
        var arguments = Variables.ToDictionary(
            variable => variable.Name,
            variable => variable.Value,
            StringComparer.OrdinalIgnoreCase);
        foreach (var equation in Equations)
        {
            equation.RebuildEvaluator(arguments);
        }
        GraphInvalidated?.Invoke(
            this,
            new GraphInvalidatedEventArgs(GraphInvalidationReason.Geometry));
    }

    private GraphAnalysisDisplayItem ToDisplayItem(GraphAnalysisFeature feature)
    {
        var title = GetAnalysisTitle(feature.Category);
        var values = feature.Values
            .Select(value => new GraphAnalysisDisplayValue(
                value.Text,
                value.Annotation,
                feature.Category is not (GraphAnalysisCategory.Parity
                    or GraphAnalysisCategory.Complexity)))
            .ToList();

        if (feature.Status == GraphAnalysisStatus.None)
        {
            values.Add(new GraphAnalysisDisplayValue(GetAnalysisNoneText(feature.Category), string.Empty));
        }
        else if (feature.Status is GraphAnalysisStatus.Unknown
                 or GraphAnalysisStatus.Unsupported
                 or GraphAnalysisStatus.Error)
        {
            values.Add(new GraphAnalysisDisplayValue(GetAnalysisUnknownText(feature.Category), string.Empty));
        }

        if (feature.Category == GraphAnalysisCategory.Complexity)
        {
            values.Insert(0, new GraphAnalysisDisplayValue(Strings.TooComplexFeatures, string.Empty));
        }

        return new GraphAnalysisDisplayItem(title, feature.Status, values);
    }

    private string GetAnalysisTitle(GraphAnalysisCategory category) =>
        category switch
        {
            GraphAnalysisCategory.Domain => Strings.DomainHeading,
            GraphAnalysisCategory.Range => Strings.RangeHeading,
            GraphAnalysisCategory.XIntercept => Strings.XInterceptHeading,
            GraphAnalysisCategory.YIntercept => Strings.YInterceptHeading,
            GraphAnalysisCategory.Minima => Strings.MinimaHeading,
            GraphAnalysisCategory.Maxima => Strings.MaximaHeading,
            GraphAnalysisCategory.InflectionPoints => Strings.InflectionPointsHeading,
            GraphAnalysisCategory.VerticalAsymptotes => Strings.VerticalAsymptotesHeading,
            GraphAnalysisCategory.HorizontalAsymptotes => Strings.HorizontalAsymptotesHeading,
            GraphAnalysisCategory.ObliqueAsymptotes => Strings.ObliqueAsymptotesHeading,
            GraphAnalysisCategory.Parity => Strings.ParityHeading,
            GraphAnalysisCategory.Monotonicity => Strings.MonotonicityHeading,
            _ => string.Empty,
        };

    private string GetAnalysisNoneText(GraphAnalysisCategory category) =>
        category switch
        {
            GraphAnalysisCategory.XIntercept or GraphAnalysisCategory.YIntercept => "∅",
            GraphAnalysisCategory.Minima => Strings.MinimaNone,
            GraphAnalysisCategory.Maxima => Strings.MaximaNone,
            GraphAnalysisCategory.InflectionPoints => Strings.InflectionPointsNone,
            GraphAnalysisCategory.VerticalAsymptotes => Strings.VerticalAsymptotesNone,
            GraphAnalysisCategory.HorizontalAsymptotes => Strings.HorizontalAsymptotesNone,
            GraphAnalysisCategory.ObliqueAsymptotes => Strings.ObliqueAsymptotesNone,
            _ => Strings.AnalysisCouldNotBePerformed,
        };

    private string GetAnalysisUnknownText(GraphAnalysisCategory category) =>
        category switch
        {
            GraphAnalysisCategory.Range => Strings.RangeUnknown,
            GraphAnalysisCategory.Monotonicity => Strings.MonotonicityUnknown,
            // Windows exposes these definite-looking rows while retaining the
            // categories in its too-complex summary. Keep the internal status
            // Unknown even though the compatibility text says "does not have".
            GraphAnalysisCategory.Minima => Strings.MinimaNone,
            GraphAnalysisCategory.Maxima => Strings.MaximaNone,
            GraphAnalysisCategory.InflectionPoints => Strings.InflectionPointsNone,
            GraphAnalysisCategory.Complexity => string.Empty,
            _ => Strings.AnalysisCouldNotBePerformed,
        };

    private string LocalizeAnalysisError(string error) =>
        error.Contains("f(x)", StringComparison.OrdinalIgnoreCase)
            ? Strings.AnalysisVariableIsNotX
            : error.Contains("not supported", StringComparison.OrdinalIgnoreCase)
            ? Strings.AnalysisNotSupported
            : Strings.AnalysisCouldNotBePerformed;
}
