using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Calculator.Avalonia.Controls;

/// <summary>
/// History in both of its presentations: a docked column beside the calculator,
/// and a light-dismissible bottom sheet over it.
/// </summary>
/// <remarks>
/// The control does not decide which presentation applies. Whether the window
/// is wide enough to dock is a shell concern, so <see cref="IsDocked"/> is set
/// from outside — the same reason <see cref="SheetHeight"/> is passed in rather
/// than measured from a named element in the window.
/// </remarks>
public partial class HistoryPane : UserControl
{
    public static readonly StyledProperty<bool> IsDockedProperty =
        AvaloniaProperty.Register<HistoryPane, bool>(nameof(IsDocked));

    /// <summary>
    /// Height of the overlay sheet. The source flyout matches the keypad, so
    /// the shell supplies the keypad's measured height.
    /// </summary>
    public static readonly StyledProperty<double> SheetHeightProperty =
        AvaloniaProperty.Register<HistoryPane, double>(nameof(SheetHeight));

    public HistoryPane() => InitializeComponent();

    /// <summary>Raised when the user taps the smoke layer to dismiss.</summary>
    public event EventHandler? DismissRequested;

    public bool IsDocked
    {
        get => GetValue(IsDockedProperty);
        set => SetValue(IsDockedProperty, value);
    }

    public double SheetHeight
    {
        get => GetValue(SheetHeightProperty);
        set => SetValue(SheetHeightProperty, value);
    }

    private void HistorySmoke_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        DismissRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }
}
