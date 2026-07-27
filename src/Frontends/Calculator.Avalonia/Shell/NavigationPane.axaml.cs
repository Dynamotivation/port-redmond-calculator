using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Calculator.Managed;

namespace Calculator.Avalonia.Shell;

/// <summary>
/// The navigation pane: the hamburger toggle, the scrim, and the calculator and
/// converter mode lists.
/// </summary>
/// <remarks>
/// Replaces the source NavigationView in its LeftMinimal mode. The pane
/// overlays page content and leaves the title bar unobstructed, so it is placed
/// over the whole page area rather than taking part in the row layout.
/// </remarks>
public partial class NavigationPane : UserControl
{
    public NavigationPane() => InitializeComponent();

    public void FocusSelectedItem()
    {
        var selected = this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button =>
                button.IsEffectivelyVisible
                && button.IsEnabled
                && button.DataContext is CalculatorNavigationItem { IsSelected: true });
        (selected ?? this.GetVisualDescendants()
            .OfType<Button>()
            .FirstOrDefault(button => button.IsEffectivelyVisible && button.IsEnabled))
            ?.Focus();
    }

    public void FocusToggle() => NavigationToggleButton.Focus();
}
