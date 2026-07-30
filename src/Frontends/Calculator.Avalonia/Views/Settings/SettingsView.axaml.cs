using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Calculator.Managed;

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
        await LaunchPackagedDocumentAsync(
            "Redmond-Calculator-MIT.txt",
            "https://github.com/Dynamotivation/port-redmond-calculator/blob/main/LICENSE");

    private async void ThirdPartyNotices_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchPackagedDocumentAsync(
            "THIRD_PARTY_NOTICES.md",
            "https://github.com/Dynamotivation/port-redmond-calculator/blob/main/THIRD_PARTY_NOTICES.md");

    private async void Feedback_OnClick(object? sender, RoutedEventArgs e) =>
        await LaunchAsync("https://github.com/Dynamotivation/port-redmond-calculator/issues");

    private async void ProviderConsent_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CurrencyProviderOption provider })
        {
            return;
        }
        if (provider.IsConsented)
        {
            return;
        }
        if (TopLevel.GetTopLevel(this) is Window owner
            && await new CurrencyConsentDialog(provider).ShowDialog<bool>(owner))
        {
            provider.GrantConsent();
        }
    }

    private async Task LaunchPackagedDocumentAsync(string fileName, string fallbackUrl)
    {
        var licenseDirectories = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Licenses"),
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..",
                "Resources",
                "Licenses")),
        };
        var documentPath = licenseDirectories
            .Select(directory => Path.Combine(directory, fileName))
            .FirstOrDefault(File.Exists);

        await LaunchAsync(documentPath is null
            ? fallbackUrl
            : new Uri(documentPath).AbsoluteUri);
    }

    private async Task LaunchAsync(string url)
    {
        if (TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            await launcher.LaunchUriAsync(new Uri(url));
        }
    }
}
