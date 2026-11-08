using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Calculator.Managed;

/// <summary>One third-party (or first-party) component and its license.</summary>
/// <param name="Version">Empty when the component has no meaningful version.</param>
/// <param name="LicenseFiles">Paths relative to the Licenses directory.</param>
public sealed record LicenseComponent(
    string Name,
    string Version,
    string License,
    string? Copyright,
    string? Source,
    IReadOnlyList<string> LicenseFiles);

public sealed record LicenseComponentGroup(string Name, IReadOnlyList<LicenseComponent> Components);

/// <summary>
/// Reads the license bundle shipped next to the application: the packaged
/// <c>Licenses</c> directory (see <c>Packaging/licenses/README.md</c>).
/// </summary>
/// <remarks>
/// Everything shown comes from files we ship. The license text itself is
/// displayed as plain text and never interpreted, so a malformed or hostile
/// notice cannot load remote content or run anything.
/// </remarks>
public static class LicenseBundle
{
    // A notice larger than this is truncated rather than loaded whole.
    private const int MaxTextBytes = 1024 * 1024;

    // SPDX identifiers that have a shared license text in the bundle.
    private static readonly IReadOnlyDictionary<string, string> SharedLicenseFiles =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MIT"] = "NuGet/MIT.txt",
            ["BSD-3-Clause"] = "NuGet/BSD-3-Clause.txt",
            ["CC0-1.0"] = "NuGet/CC0-1.0.txt",
        };

    /// <summary>
    /// Finds the packaged Licenses directory: next to the executable, or inside
    /// a macOS application bundle's Resources folder.
    /// </summary>
    public static string? FindDirectory(string? baseDirectory = null)
    {
        baseDirectory ??= AppContext.BaseDirectory;
        return new[]
        {
            Path.Combine(baseDirectory, "Licenses"),
            Path.GetFullPath(Path.Combine(baseDirectory, "..", "Resources", "Licenses")),
        }.FirstOrDefault(Directory.Exists);
    }

    /// <summary>Components grouped for display. Never throws for a damaged bundle.</summary>
    public static IReadOnlyList<LicenseComponentGroup> LoadGroups(string? licenseDirectory, string appVersion)
    {
        var groups = new List<(string Name, List<LicenseComponent> Items)>
        {
            ("Microsoft Calculator", []),
            ("Redmond", []),
            ("Avalonia and UI", []),
            ("Math and text", []),
            ("Graphics and native binaries", []),
            ("Runtime", []),
            ("Fonts", []),
            ("Other packages", []),
        };
        List<LicenseComponent> Group(string name) => groups.First(group => group.Name == name).Items;

        Group("Microsoft Calculator").Add(new LicenseComponent(
            "Microsoft Calculator",
            string.Empty,
            "MIT",
            "Copyright (c) Microsoft Corporation. All rights reserved.",
            "https://github.com/microsoft/calculator",
            ["Microsoft-Calculator-MIT.txt", "Microsoft-Calculator-NOTICE.txt"]));
        Group("Redmond").Add(new LicenseComponent(
            "Redmond Calculator",
            appVersion,
            "MIT",
            "Copyright (c) 2026 Redmond Calculator contributors",
            "https://github.com/Dynamotivation/port-redmond-calculator",
            ["Redmond-Calculator-MIT.txt"]));
        Group("Redmond").Add(new LicenseComponent(
            "Redmond Commons",
            string.Empty,
            "MIT",
            "Copyright (c) 2026 Redmond Commons contributors",
            "https://github.com/Dynamotivation/port-redmond-commons",
            ["Redmond-Commons-MIT.txt", "Redmond-Commons-THIRD-PARTY-NOTICES.md"]));
        Group("Runtime").Add(new LicenseComponent(
            ".NET runtime",
            string.Empty,
            "MIT",
            "Copyright (c) .NET Foundation and Contributors",
            "https://github.com/dotnet/runtime",
            ["Dotnet-Runtime/LICENSE.txt", "Dotnet-Runtime/ThirdPartyNotices.txt"]));
        Group("Graphics and native binaries").Add(new LicenseComponent(
            "ANGLE",
            string.Empty,
            "BSD-3-Clause",
            null,
            "https://github.com/AvaloniaUI/angle",
            ["Native-Dependencies/ANGLE-LICENSE.txt"]));
        Group("Graphics and native binaries").Add(new LicenseComponent(
            "SkiaSharp native notices",
            string.Empty,
            "MIT",
            null,
            "https://github.com/mono/SkiaSharp",
            ["Native-Dependencies/SkiaSharp-THIRD-PARTY-NOTICES.txt"]));
        Group("Graphics and native binaries").Add(new LicenseComponent(
            "HarfBuzzSharp native notices",
            string.Empty,
            "MIT",
            null,
            "https://github.com/mono/SkiaSharp",
            ["Native-Dependencies/HarfBuzzSharp-THIRD-PARTY-NOTICES.txt"]));
        Group("Fonts").Add(new LicenseComponent(
            "Inter",
            string.Empty,
            "OFL-1.1",
            "Copyright (c) 2016 The Inter Project Authors",
            "https://github.com/rsms/inter",
            ["Inter/NOTICE.txt", "Inter/OFL-1.1.txt"]));
        Group("Fonts").Add(new LicenseComponent(
            "Latin Modern Math",
            string.Empty,
            "GUST Font License",
            null,
            "https://www.gust.org.pl/projects/e-foundry/lm-math",
            ["Latin-Modern-Math/GUST-FONT-LICENSE.txt"]));
        Group("Fonts").Add(new LicenseComponent(
            "CSharpMath embedded fonts",
            string.Empty,
            "OFL-1.1 and GUST Font License",
            null,
            "https://github.com/verybadcat/CSharpMath",
            ["CSharpMath-Embedded-Fonts.txt"]));

        foreach (var package in ReadNuGetPackages(licenseDirectory))
        {
            Group(NuGetGroupName(package.Name)).Add(package);
        }

        return groups
            .Where(group => group.Items.Count > 0)
            .Select(group => new LicenseComponentGroup(
                group.Name,
                group.Items
                    .OrderBy(component => component.Name, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .ToArray();
    }

    /// <summary>
    /// Reads and joins a component's license files as plain text. Paths that
    /// escape the Licenses directory are ignored.
    /// </summary>
    public static string ReadLicenseText(string? licenseDirectory, LicenseComponent component)
    {
        if (licenseDirectory is null)
        {
            return "The license files are not available in this build. See the project's THIRD_PARTY_NOTICES.md on GitHub.";
        }

        var root = Path.GetFullPath(licenseDirectory) + Path.DirectorySeparatorChar;
        var builder = new StringBuilder();
        foreach (var relativePath in component.LicenseFiles)
        {
            var path = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append("\n\n----------------------------------------\n\n");
            }

            using var stream = File.OpenRead(path);
            var buffer = new byte[Math.Min(stream.Length, MaxTextBytes)];
            stream.ReadExactly(buffer);
            builder.Append(Encoding.UTF8.GetString(buffer));
        }

        return builder.Length > 0
            ? builder.ToString()
            : $"The license text for {component.Name} ({component.License}) is not available in this build.";
    }

    private static IEnumerable<LicenseComponent> ReadNuGetPackages(string? licenseDirectory)
    {
        if (licenseDirectory is null)
        {
            yield break;
        }

        var path = Path.Combine(licenseDirectory, "NUGET-PACKAGES.spdx.json");
        JsonDocument? document = null;
        try
        {
            if (File.Exists(path))
            {
                document = JsonDocument.Parse(File.ReadAllText(path));
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A damaged inventory only hides the package list; the curated
            // components and their notices stay available.
        }

        if (document is null)
        {
            yield break;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var package in packages.EnumerateArray())
            {
                var name = Text(package, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                var license = Text(package, "licenseConcluded");
                var source = Text(package, "downloadLocation");
                var copyright = Text(package, "copyrightText");
                yield return new LicenseComponent(
                    name,
                    Text(package, "versionInfo"),
                    license.Length > 0 ? license : "NOASSERTION",
                    copyright.Length > 0 && copyright != "NOASSERTION" ? copyright : null,
                    source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? source : null,
                    SharedLicenseFiles.TryGetValue(license, out var file) ? [file] : []);
            }
        }

        static string Text(JsonElement element, string property) =>
            element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    private static string NuGetGroupName(string packageName)
    {
        static bool Starts(string name, params string[] prefixes) =>
            prefixes.Any(prefix => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return packageName switch
        {
            _ when Starts(packageName, "SkiaSharp", "HarfBuzzSharp", "Avalonia.Skia", "Avalonia.HarfBuzz",
                "Avalonia.Angle", "Avalonia.Native") => "Graphics and native binaries",
            _ when Starts(packageName, "Avalonia", "FluentAvalonia", "MicroCom", "Tmds.DBus") => "Avalonia and UI",
            _ when Starts(packageName, "AngouriMath", "Antlr4", "CSharpMath", "Sylinko.CSharpMath",
                "GenericTensor", "HonkSharp", "PeterO") => "Math and text",
            _ => "Other packages",
        };
    }
}

/// <summary>A component row whose license text loads when it is first expanded.</summary>
public sealed partial class LicenseComponentViewModel : ObservableObject
{
    private readonly string? _licenseDirectory;
    private string? _licenseText;

    public LicenseComponentViewModel(LicenseComponent component, string? licenseDirectory)
    {
        Component = component;
        _licenseDirectory = licenseDirectory;
    }

    public LicenseComponent Component { get; }

    public string Name => Component.Name;

    public string Header => Component.Version.Length > 0 ? $"{Component.Name} {Component.Version}" : Component.Name;

    public string License => Component.License;

    public string? Copyright => Component.Copyright;

    public string? Source => Component.Source;

    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>The license text; empty until the row is first expanded.</summary>
    public string LicenseText => _licenseText ?? string.Empty;

    partial void OnIsExpandedChanged(bool value)
    {
        if (value && _licenseText is null)
        {
            _licenseText = LicenseBundle.ReadLicenseText(_licenseDirectory, Component);
            OnPropertyChanged(nameof(LicenseText));
        }
    }
}

public sealed class LicenseGroupViewModel
{
    public LicenseGroupViewModel(LicenseComponentGroup group, string? licenseDirectory)
    {
        Name = group.Name;
        Components = group.Components
            .Select(component => new LicenseComponentViewModel(component, licenseDirectory))
            .ToArray();
    }

    public string Name { get; }

    public IReadOnlyList<LicenseComponentViewModel> Components { get; }

    public string CountText => Components.Count == 1 ? "1 component" : $"{Components.Count} components";
}
