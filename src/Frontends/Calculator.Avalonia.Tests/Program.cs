using Avalonia;
using Avalonia.Headless;
using Calculator.Avalonia.Tests;
using Windows.ApplicationModel.Resources;

AppBuilder.Configure<Calculator.Avalonia.App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

// SetupWithoutStarting leaves ApplicationLifetime null, so the app never builds
// its own MainWindow. Resource loading still has to be configured by hand.
ResourceLoader.Configure(new ResourceLoaderConfiguration(
    Path.Combine(AppContext.BaseDirectory, "Resources")));

var tests = RouterTests.All
    .Concat(CurrencyTests.All)
    .Concat(GraphingTests.All)
    .Concat(GraphingInteractionTests.All)
    .Concat(DateCalculatorTests.All)
    .Concat(PressedStateTests.All)
    .Concat(ShellInteractionTests.All)
    .ToArray();

foreach (var (name, run) in tests)
{
    run();
    Console.WriteLine($"PASS: {name}");
}

Console.WriteLine($"\nAll {tests.Length} behavioral tests passed.");
return 0;
