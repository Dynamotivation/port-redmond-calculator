using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Calculator.Avalonia;
using Calculator.Managed;
using Windows.ApplicationModel.Resources;

namespace Calculator.Avalonia.Tests;

internal sealed record Frame(int Width, int Height, byte[] Pixels);

internal sealed record ComparisonResult(bool IsMatch, string Message);

internal static class SnapshotRunner
{
    private const int SettleTickSleepMilliseconds = 8;
    private const int NavigationTransitionTimeoutMilliseconds = 2000;

    /// <summary>
    /// Largest per-channel difference treated as compositing noise. Measured
    /// against the real thing: extracting the title bar into a UserControl
    /// moved ~70 antialiased edge pixels of one glyph by 1/255 in dark and
    /// 2/255 in light, uniformly across all channels — the rounding of one
    /// extra alpha composite, not a change to colour, position or size.
    /// </summary>
    private const int MaxToleratedChannelDelta = 2;

    /// <summary>Largest share of pixels allowed to carry that difference.</summary>
    private const double MaxToleratedPixelFraction = 0.005;

    public static string SnapshotDirectory { get; } = ResolveSnapshotDirectory();

    public static void ConfigureResources() =>
        ResourceLoader.Configure(new ResourceLoaderConfiguration(
            Path.Combine(AppContext.BaseDirectory, "Resources")));

    public static Frame Capture(Scenario scenario)
    {
        var application = Application.Current
            ?? throw new InvalidOperationException("Avalonia application was not initialised.");
        application.RequestedThemeVariant = scenario.Theme;

        var window = new MainWindow(scenario.Settings)
        {
            Width = scenario.Width,
            Height = scenario.Height,
        };

        // The headless backing buffer is never cleared, so the transparent
        // window background composites over undefined memory and the frame
        // differs run to run. Painting a known colour underneath makes the
        // capture deterministic while leaving every in-window brush — including
        // the mica/noBackdrop split — exactly as the app draws it. Magenta so
        // that unintended see-through is obvious in the preview PNGs.
        window.Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF));

        try
        {
            window.Show();
            Settle(ticks: 20);

            var viewModel = window.DataContext as CalculatorViewModel
                ?? throw new InvalidOperationException("MainWindow has no CalculatorViewModel.");
            scenario.Arrange(viewModel);

            // Arranging can resize the window (compact always-on-top does), so
            // restore the scenario geometry before the frame is measured.
            window.MinWidth = scenario.MinWidth ?? window.MinWidth;
            window.MinHeight = scenario.MinHeight ?? window.MinHeight;
            window.Width = scenario.Width;
            window.Height = scenario.Height;

            // IsNavigationPaneTransitioning is cleared by a wall-clock delay in
            // the view model, and it drives the hamburger's enabled state. Wait
            // it out explicitly — a tick budget alone leaves the capture racing
            // that timer and the toggle renders enabled or disabled at random.
            WaitForNavigationTransition(viewModel);
            Settle(ticks: 80);

            using var bitmap = window.CaptureRenderedFrame()
                ?? throw new InvalidOperationException($"No frame rendered for '{scenario.Name}'.");
            using var framebuffer = bitmap.Lock();

            var width = framebuffer.Size.Width;
            var height = framebuffer.Size.Height;
            var pixels = new byte[width * height * 4];
            for (var row = 0; row < height; row++)
            {
                unsafe
                {
                    var source = new ReadOnlySpan<byte>(
                        (byte*)framebuffer.Address + (row * framebuffer.RowBytes),
                        width * 4);
                    source.CopyTo(pixels.AsSpan(row * width * 4));
                }
            }

            NormalizeTransparentPixels(pixels);
            return new Frame(width, height, pixels);
        }
        finally
        {
            window.Close();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// A window with a transparency hint leaves fully transparent pixels with
    /// whatever colour happened to be in the backing buffer, which differs run
    /// to run. Those pixels have no defined colour, so flatten them before the
    /// frame is stored or compared.
    /// </summary>
    private static void NormalizeTransparentPixels(byte[] pixels)
    {
        for (var index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0)
            {
                pixels[index] = 0;
                pixels[index + 1] = 0;
                pixels[index + 2] = 0;
            }
        }
    }

    /// <summary>
    /// Pumps the dispatcher and render clock until animations have run out.
    /// The count of render ticks is fixed rather than derived from elapsed wall
    /// time: the headless animation clock advances per tick, so a time-bounded
    /// loop lands transitions at a different position on a loaded machine and
    /// the captured frame stops being reproducible. The sleep is what gives the
    /// view model's 220ms navigation delay room to complete.
    /// </summary>
    private static void WaitForNavigationTransition(CalculatorViewModel viewModel)
    {
        var deadline = Environment.TickCount64 + NavigationTransitionTimeoutMilliseconds;
        while (viewModel.IsNavigationPaneTransitioning && Environment.TickCount64 < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(SettleTickSleepMilliseconds);
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static void Settle(int ticks)
    {
        for (var tick = 0; tick < ticks; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Thread.Sleep(SettleTickSleepMilliseconds);
        }

        // A final pair of ticks so any layout invalidated by the last job is
        // measured, arranged and drawn before the frame is captured.
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public static void Write(string name, Frame frame)
    {
        Directory.CreateDirectory(SnapshotDirectory);
        File.WriteAllBytes(BaselinePath(name), Encode(frame));
    }

    /// <summary>
    /// Renders a frame to PNG for human inspection. Comparisons never read
    /// these, so encoder differences cannot make the suite flaky.
    /// </summary>
    public static void WritePreview(string name, Frame frame)
    {
        Directory.CreateDirectory(SnapshotDirectory);
        using var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using (var framebuffer = bitmap.Lock())
        {
            for (var row = 0; row < frame.Height; row++)
            {
                unsafe
                {
                    var destination = new Span<byte>(
                        (byte*)framebuffer.Address + (row * framebuffer.RowBytes),
                        frame.Width * 4);
                    frame.Pixels.AsSpan(row * frame.Width * 4, frame.Width * 4).CopyTo(destination);
                }
            }
        }

        bitmap.Save(Path.Combine(SnapshotDirectory, $"{name}.png"));
    }

    public static void WriteActual(string name, Frame frame)
    {
        Directory.CreateDirectory(SnapshotDirectory);
        File.WriteAllBytes(Path.Combine(SnapshotDirectory, $"{name}.actual.frame"), Encode(frame));
    }

    public static ComparisonResult Compare(string name, Frame actual)
    {
        var path = BaselinePath(name);
        if (!File.Exists(path))
        {
            return new ComparisonResult(false, "no baseline; run with --update");
        }

        var expected = Decode(File.ReadAllBytes(path));
        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new ComparisonResult(
                false,
                $"size changed {expected.Width}x{expected.Height} -> {actual.Width}x{actual.Height}");
        }

        var differing = 0;
        var maxChannelDelta = 0;
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

        for (var index = 0; index < expected.Pixels.Length; index += 4)
        {
            var delta = 0;
            for (var channel = 0; channel < 4; channel++)
            {
                delta = Math.Max(delta, Math.Abs(expected.Pixels[index + channel] - actual.Pixels[index + channel]));
            }

            if (delta == 0)
            {
                continue;
            }

            differing++;
            maxChannelDelta = Math.Max(maxChannelDelta, delta);

            var pixel = index / 4;
            var x = pixel % expected.Width;
            var y = pixel / expected.Width;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
        }

        if (differing == 0)
        {
            return new ComparisonResult(true, "identical");
        }

        var total = expected.Width * expected.Height;
        var fraction = (double)differing / total;

        // Wrapping markup in a UserControl adds a compositing layer, and each
        // extra layer costs one round of 8-bit quantisation. That shows up as a
        // handful of pixels off by one in a single channel — invisible, and
        // unavoidable if the window is to be decomposed at all. Anything larger
        // than that (a moved control, a wrong brush, a changed font size) blows
        // straight past both limits, so they stay tight.
        var isQuantisationNoise = maxChannelDelta <= MaxToleratedChannelDelta
            && fraction <= MaxToleratedPixelFraction;

        var detail = $"{differing} of {total} pixels differ ({fraction:P2}), "
            + $"max channel delta {maxChannelDelta}, in x {minX}-{maxX}, y {minY}-{maxY}";

        return isQuantisationNoise
            ? new ComparisonResult(true, $"within tolerance ({detail})")
            : new ComparisonResult(false, detail);
    }

    private static string BaselinePath(string name) =>
        Path.Combine(SnapshotDirectory, $"{name}.frame");

    private static byte[] Encode(Frame frame)
    {
        var buffer = new byte[8 + frame.Pixels.Length];
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(0), frame.Width);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(4), frame.Height);
        frame.Pixels.CopyTo(buffer.AsSpan(8));
        return buffer;
    }

    private static Frame Decode(byte[] buffer) => new(
        BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(0)),
        BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(4)),
        buffer[8..]);

    private static string ResolveSnapshotDirectory()
    {
        // bin/<config>/<tfm>/ -> project root
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
            && !File.Exists(Path.Combine(directory.FullName, "Calculator.Avalonia.Tests.csproj")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? AppContext.BaseDirectory,
            "Snapshots");
    }
}
