using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Calculator.Managed;

namespace Calculator.AvaloniaBench.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void ThemeToggle_OnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                ThemeToggle.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        (DataContext as CalculatorViewModel)?.Dispose();
        base.OnClosed(e);
    }
}
