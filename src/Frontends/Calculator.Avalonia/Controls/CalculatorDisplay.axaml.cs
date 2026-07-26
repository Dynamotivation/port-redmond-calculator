using Avalonia;
using Avalonia.Controls;

namespace Calculator.Avalonia.Controls;

/// <summary>
/// The height band the display renders at. Calculator.xaml has three states for
/// the result row; the shell decides which one applies because the thresholds
/// are measured against the window, not against this control.
/// </summary>
public enum CalculatorDisplaySize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// The shared expression and primary result presentation.
/// </summary>
/// <remarks>
/// This control owns how large it draws, but not when. It is told a size band
/// and whether it is in compact overlay, and applies the source application's
/// minimums and font sizes itself — the window no longer reaches in by name to
/// set MinHeight and FontSize.
/// </remarks>
public partial class CalculatorDisplay : UserControl
{
    public static readonly StyledProperty<CalculatorDisplaySize> SizeProperty =
        AvaloniaProperty.Register<CalculatorDisplay, CalculatorDisplaySize>(
            nameof(Size),
            CalculatorDisplaySize.Medium);

    /// <summary>
    /// Compact always-on-top drops the expression row entirely and uses its own
    /// pair of result metrics.
    /// </summary>
    public static readonly StyledProperty<bool> IsCompactOverlayProperty =
        AvaloniaProperty.Register<CalculatorDisplay, bool>(nameof(IsCompactOverlay));

    static CalculatorDisplay()
    {
        SizeProperty.Changed.AddClassHandler<CalculatorDisplay>((display, _) => display.ApplyMetrics());
        IsCompactOverlayProperty.Changed.AddClassHandler<CalculatorDisplay>((display, _) => display.ApplyMetrics());
    }

    public CalculatorDisplay()
    {
        InitializeComponent();
        ApplyMetrics();
    }

    public CalculatorDisplaySize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsCompactOverlay
    {
        get => GetValue(IsCompactOverlayProperty);
        set => SetValue(IsCompactOverlayProperty, value);
    }

    private void ApplyMetrics()
    {
        if (IsCompactOverlay)
        {
            // The expression row collapses; the remaining space is all result.
            DisplayRows.RowDefinitions[0].Height = new GridLength(0);
            DisplayRows.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            ExpressionText.IsVisible = false;

            var isRoomy = Size != CalculatorDisplaySize.Small;
            ResultHost.MinHeight = isRoomy ? 54 : 20;
            PrimaryResultText.FontSize = isRoomy ? 46 : 18;
            return;
        }

        DisplayRows.RowDefinitions[0].Height = new GridLength(22, GridUnitType.Star);
        DisplayRows.RowDefinitions[1].Height = new GridLength(72, GridUnitType.Star);
        ExpressionText.IsVisible = true;

        // Keep the original thresholds, minimums and maximum font sizes.
        (ResultHost.MinHeight, PrimaryResultText.FontSize) = Size switch
        {
            CalculatorDisplaySize.Large => (108d, 72d),
            CalculatorDisplaySize.Medium => (72d, 46d),
            _ => (42d, 26d),
        };
    }
}
