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
    string SwitchToGraphMode);

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

    public event EventHandler? ValueChanged;

    partial void OnValueChanged(double value) => ValueChanged?.Invoke(this, EventArgs.Empty);
}

public partial class GraphEquationViewModel : ObservableObject
{
    private readonly IMathSolver _solver;
    private IExpression? _parsedExpression;
    private IGraphExpressionEvaluator? _evaluator;

    public GraphEquationViewModel(
        IMathSolver solver,
        int functionIndex,
        string color,
        string placeholder)
    {
        _solver = solver;
        FunctionIndex = functionIndex;
        Color = color;
        Placeholder = placeholder;
    }

    public int FunctionIndex { get; private set; }
    public string Placeholder { get; }
    public string FunctionLabel => HasExpression ? $"f{ToSubscript(FunctionIndex + 1)}" : "f";
    public string FunctionIndexLabel => HasExpression ? (FunctionIndex + 1).ToString() : string.Empty;
    public string TileColor => !HasExpression || !IsEnabled ? "#A6A6A6" : Color;
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

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public bool HasExpression => !string.IsNullOrWhiteSpace(Expression);

    public event EventHandler? EquationChanged;

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
        return HasExpression && IsValid;
    }

    partial void OnExpressionChanged(string value)
    {
        if (!string.Equals(DraftExpression, value, StringComparison.Ordinal))
        {
            DraftExpression = value;
        }
        OnPropertyChanged(nameof(HasExpression));
        OnPropertyChanged(nameof(FunctionLabel));
        OnPropertyChanged(nameof(FunctionIndexLabel));
        OnPropertyChanged(nameof(CanToggleVisibility));
        OnPropertyChanged(nameof(TileColor));
        Parse();
        EquationChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(VisibilityAutomationName));
        OnPropertyChanged(nameof(TileColor));
        EquationChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnColorChanged(string value)
    {
        OnPropertyChanged(nameof(TileColor));
        EquationChanged?.Invoke(this, EventArgs.Empty);
    }
    partial void OnErrorMessageChanged(string value) => OnPropertyChanged(nameof(HasError));
    partial void OnLineStyleChanged(GraphLineStyle value) => EquationChanged?.Invoke(this, EventArgs.Empty);
    partial void OnLineWidthChanged(double value) => EquationChanged?.Invoke(this, EventArgs.Empty);

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
            LineStyle,
            LineWidth,
            _evaluator);
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
            ErrorMessage = exception.Message;
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
    public bool HasVariables => Variables.Count > 0;

    [ObservableProperty]
    public partial GraphEquationViewModel? SelectedEquation { get; set; }

    public bool CanAddEquation => Equations.Count < EquationColors.Length;
    public event EventHandler? GraphInvalidated;

    [RelayCommand(CanExecute = nameof(CanAddEquation))]
    private void AddEquation()
    {
        var functionIndex = Equations.Count;
        var equation = new GraphEquationViewModel(
            _solver,
            functionIndex,
            GetNextUnusedColor(),
            Strings.EnterExpressionPlaceholder);
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

        if (CanAddEquation && Equations.All(item => item.HasExpression))
        {
            AddEquation();
            return;
        }

        AssignUnusedColorsToPlaceholders();

        if (ReferenceEquals(SelectedEquation, equation))
        {
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
            Equations.Where(item => item.HasExpression));
        foreach (var placeholder in Equations.Where(item => !item.HasExpression))
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

        var convertedPlaceholder = !equation.HasExpression;
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

    private void OnEquationChanged(object? sender, EventArgs e) => RefreshGraph();

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
        GraphInvalidated?.Invoke(this, EventArgs.Empty);
    }
}
