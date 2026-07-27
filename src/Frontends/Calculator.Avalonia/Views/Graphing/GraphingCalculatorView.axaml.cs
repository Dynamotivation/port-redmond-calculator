using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Views.Graphing;

public partial class GraphingCalculatorView : UserControl
{
    private const double ColumnsThreshold = 800;
    private bool _showsEquationPanelOnNarrow;

    public GraphingCalculatorView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => UpdateResponsiveLayout(Bounds.Width);
        AttachedToVisualTree += (_, _) => UpdateResponsiveLayout(Bounds.Width);
    }

    public void FocusEquationInput()
    {
        var textBox = this.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault();
        textBox?.Focus();
    }

    public void FocusDefault()
    {
        UpdateResponsiveLayout(Bounds.Width);
        if (EquationPanel.IsVisible)
        {
            FocusEquationInput();
        }
        else
        {
            FocusGraph();
        }
    }

    public void FocusGraph() => Plot.Focus();

    public void ResetView() => Plot.ResetView();

    private void UpdateResponsiveLayout(double width)
    {
        var isNarrow = width < ColumnsThreshold;
        NarrowModeToggle.IsVisible = isNarrow;
        if (!isNarrow)
        {
            ResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("2*,360");
            Grid.SetColumn(EquationPanel, 1);
            Plot.IsVisible = true;
            EquationPanel.IsVisible = true;
            return;
        }

        ResponsiveLayout.ColumnDefinitions = new ColumnDefinitions("*");
        Grid.SetColumn(EquationPanel, 0);
        Plot.IsVisible = !_showsEquationPanelOnNarrow;
        EquationPanel.IsVisible = _showsEquationPanelOnNarrow;
        NarrowModeGlyph.Text = _showsEquationPanelOnNarrow ? "\uF770" : "\uF893";
        NarrowModeSubscript.IsVisible = !_showsEquationPanelOnNarrow;
        ToolTip.SetTip(NarrowModeToggle, DataContext is CalculatorViewModel viewModel
            ? _showsEquationPanelOnNarrow
                ? viewModel.Graphing.Strings.SwitchToGraphMode
                : viewModel.Graphing.Strings.SwitchToEquationMode
            : null);
    }

    private void NarrowModeToggle_OnClick(object? sender, RoutedEventArgs e)
    {
        _showsEquationPanelOnNarrow = !_showsEquationPanelOnNarrow;
        UpdateResponsiveLayout(Bounds.Width);
        if (_showsEquationPanelOnNarrow)
        {
            FocusEquationInput();
        }
        else
        {
            Plot.Focus();
        }
    }

    private void ZoomIn_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomIn();

    private void ZoomOut_OnClick(object? sender, RoutedEventArgs e) => Plot.ZoomOut();

    private void ResetView_OnClick(object? sender, RoutedEventArgs e) => Plot.ResetView();
}
