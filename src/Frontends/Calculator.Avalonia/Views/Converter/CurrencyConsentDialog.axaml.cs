using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Calculator.Managed;

namespace Calculator.Avalonia.Views;

public partial class CurrencyConsentDialog : Window
{
    public CurrencyConsentDialog()
    {
        InitializeComponent();
    }

    public CurrencyConsentDialog(CurrencyProviderOption provider)
        : this()
    {
        DataContext = provider;
    }

    private void Cancel_OnClick(object? sender, RoutedEventArgs e) => Close(false);

    private void Allow_OnClick(object? sender, RoutedEventArgs e) => Close(true);

    private async void Terms_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CurrencyProviderOption provider)
        {
            await LaunchAsync(provider.TermsUrl);
        }
    }

    private async void Privacy_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CurrencyProviderOption provider)
        {
            await LaunchAsync(provider.PrivacyUrl);
        }
    }

    private async Task LaunchAsync(string url)
    {
        if (Launcher is { } launcher && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            await launcher.LaunchUriAsync(uri);
        }
    }
}
