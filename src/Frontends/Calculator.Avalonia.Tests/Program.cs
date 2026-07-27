using Avalonia;
using Avalonia.Headless;
using Calculator.Avalonia.Tests;

// Visual-parity harness for the MainWindow decomposition. Snapshots are raw
// RGBA frames so comparisons do not depend on PNG encoder determinism; a PNG
// is written alongside each one for human inspection.
//
//   dotnet run --project Calculator.Avalonia.Tests            -- compare
//   dotnet run --project Calculator.Avalonia.Tests -- --update -- rewrite
var update = args.Contains("--update", StringComparer.Ordinal);

AppBuilder.Configure<Calculator.Avalonia.App>()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia()
    .SetupWithoutStarting();

// SetupWithoutStarting leaves ApplicationLifetime null, so the app never builds
// its own MainWindow. Resource loading still has to be configured by hand.
SnapshotRunner.ConfigureResources();

// Behavioural guards first: they are fast, and a broken shortcut table makes
// the snapshot diff much harder to read.
foreach (var (name, run) in RouterTests.All
             .Concat(GraphingTests.All)
             .Concat(DateCalculatorTests.All)
             .Concat(PressedStateTests.All)
             .Concat(ShellInteractionTests.All))
{
    run();
    Console.WriteLine($"PASS: {name}");
}

Console.WriteLine();

// Every frame is captured before anything is written. Interleaving file I/O
// between captures perturbs the render clock enough to shift in-flight
// transitions by a pixel or two, which made --update runs disagree with
// comparison runs. Keeping the capture loop pure makes the two identical.
var captured = Scenarios.All
    .Select(scenario => (scenario.Name, Frame: SnapshotRunner.Capture(scenario)))
    .ToList();

var failures = 0;

foreach (var (name, frame) in captured)
{
    if (update)
    {
        SnapshotRunner.Write(name, frame);
        SnapshotRunner.WritePreview(name, frame);
        Console.WriteLine($"WROTE: {name} ({frame.Width}x{frame.Height})");
        continue;
    }

    var result = SnapshotRunner.Compare(name, frame);
    if (result.IsMatch)
    {
        Console.WriteLine(result.Message == "identical"
            ? $"PASS: {name}"
            : $"PASS: {name} — {result.Message}");
    }
    else
    {
        Console.WriteLine($"FAIL: {name} — {result.Message}");
        SnapshotRunner.WriteActual(name, frame);
        SnapshotRunner.WritePreview($"{name}.actual", frame);
        failures++;
    }
}

if (update)
{
    Console.WriteLine($"\n{captured.Count} baseline snapshot(s) written to {SnapshotRunner.SnapshotDirectory}");
    return 0;
}

if (failures > 0)
{
    Console.WriteLine($"\n{failures} snapshot(s) regressed. Actual frames written next to the baselines.");
    return 1;
}

Console.WriteLine($"\nAll {captured.Count} snapshots match.");
return 0;
