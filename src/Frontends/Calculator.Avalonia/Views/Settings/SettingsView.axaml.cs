using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Calculator.Avalonia.Views;

/// <summary>
/// The settings page: theme, font, backdrop, corner and title-control options,
/// plus the About group.
/// </summary>
/// <remarks>
/// This view owns settings presentation only. Every preference it edits is
/// applied by the view model and, for the ones that need native window APIs, by
/// the window itself — nothing here talks to AppKit or resizes a window.
/// </remarks>
public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    public void FocusFirstInteractiveControl() =>
        this.GetVisualDescendants()
            .OfType<InputElement>()
            .FirstOrDefault(element =>
                element.Focusable
                && element.IsEffectivelyVisible
                && element.IsEffectivelyEnabled)
            ?.Focus();

    private async void License_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchAsync("https://github.com/microsoft/calculator/blob/main/LICENSE");

    private async void ServicesAgreement_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchAsync("https://go.microsoft.com/fwlink/?LinkID=822631");

    private async void PrivacyStatement_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchAsync("https://go.microsoft.com/fwlink/?LinkID=521839");

    private async void Feedback_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchAsync("https://github.com/Dynamotivation/RedmondCalculator/issues");

    private async System.Threading.Tasks.Task LaunchAsync(string url)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
