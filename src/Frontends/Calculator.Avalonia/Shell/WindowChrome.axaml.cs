using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Calculator.Avalonia.Shell;

/// <summary>
/// The custom title bar: caption buttons, the application identity block and
/// the always-on-top exit affordance.
/// </summary>
/// <remarks>
/// Window operations are surfaced as events instead of being performed here.
/// Minimising, maximising, closing and moving belong to whoever owns the
/// window, and keeping them out of this control lets the chrome be hosted and
/// tested without a real window behind it.
/// </remarks>
public partial class WindowChrome : UserControl
{
    public WindowChrome() => InitializeComponent();

    /// <summary>Raised when the user drags an empty area of the title bar.</summary>
    public event EventHandler<PointerPressedEventArgs>? DragRequested;

    public event EventHandler? MinimizeRequested;

    public event EventHandler? MaximizeRequested;

    public event EventHandler? CloseRequested;

    /// <summary>Raised by the compact always-on-top exit button.</summary>
    public event EventHandler? AlwaysOnTopToggleRequested;

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e) =>
        DragRequested?.Invoke(this, e);

    private void Minimize_OnClick(object? sender, RoutedEventArgs e) =>
        MinimizeRequested?.Invoke(this, EventArgs.Empty);

    private void Maximize_OnClick(object? sender, RoutedEventArgs e) =>
        MaximizeRequested?.Invoke(this, EventArgs.Empty);

    private void Close_OnClick(object? sender, RoutedEventArgs e) =>
        CloseRequested?.Invoke(this, EventArgs.Empty);

    private void AlwaysOnTop_OnClick(object? sender, RoutedEventArgs e) =>
        AlwaysOnTopToggleRequested?.Invoke(this, EventArgs.Empty);
}
