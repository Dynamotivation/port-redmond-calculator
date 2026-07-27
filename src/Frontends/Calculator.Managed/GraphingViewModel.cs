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
    IGraphExpressionEvaluator Evaluator);

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

    public int FunctionIndex { get; }
    public string Color { get; }
    public string Placeholder { get; }

    [ObservableProperty]
    public partial string Expression { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = true;

    [ObservableProperty]
    public partial string ErrorMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsValid { get; private set; }

    public bool HasExpression => !string.IsNullOrWhiteSpace(Expression);

    public event EventHandler? EquationChanged;

    partial void OnExpressionChanged(string value)
    {
        OnPropertyChanged(nameof(HasExpression));
        Parse();
        EquationChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsEnabledChanged(bool value) => EquationChanged?.Invoke(this, EventArgs.Empty);

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
            _evaluator);
    }

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
    private static readonly string[] EquationColors =
    [
        "#0078D4",
        "#E81123",
        "#107C10",
        "#881798",
        "#FF8C00",
        "#038387",
        "#C239B3",
        "#498205",
        "#4F6BED",
        "#8E562E",
    ];

    private readonly IMathSolver _solver;
    private int _nextFunctionIndex;

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
        var functionIndex = _nextFunctionIndex++;
        var equation = new GraphEquationViewModel(
            _solver,
            functionIndex,
            EquationColors[functionIndex % EquationColors.Length],
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
        if (Equations.Count == 0)
        {
            AddEquation();
            return;
        }

        if (ReferenceEquals(SelectedEquation, equation))
        {
            SelectedEquation = Equations[^1];
        }

        OnPropertyChanged(nameof(CanAddEquation));
        AddEquationCommand.NotifyCanExecuteChanged();
        RefreshGraph();
    }

    public IReadOnlyList<GraphEquationRenderModel> GetRenderableEquations() =>
        Equations
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
