using System.Text.Json;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class LicenseBundleTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("license bundle is packaged next to the app", BundleIsPackaged),
        ("every NuGet package appears with its license", EveryPackageIsListed),
        ("every listed component has displayable license text", EveryComponentHasText),
        ("components are grouped, sorted and Microsoft Calculator is credited", GroupsAreOrganized),
        ("a missing bundle still lists the curated components", MissingBundleDegradesGracefully),
        ("a damaged package inventory does not throw", DamagedInventoryDegradesGracefully),
        ("license paths cannot escape the Licenses directory", PathTraversalIsIgnored),
        ("license text loads only when a row is expanded", TextLoadsLazily),
        ("About shows the assembly version", VersionComesFromAssembly),
    ];

    private static string Bundle() =>
        LicenseBundle.FindDirectory()
        ?? throw new InvalidOperationException("The Licenses directory was not copied next to the test host.");

    private static void BundleIsPackaged()
    {
        var directory = Bundle();
        foreach (var required in new[]
        {
            "Redmond-Calculator-MIT.txt",
            "Microsoft-Calculator-MIT.txt",
            "THIRD_PARTY_NOTICES.md",
            "NUGET-PACKAGES.spdx.json",
        })
        {
            Assert(new FileInfo(Path.Combine(directory, required)) is { Exists: true, Length: > 0 },
                $"{required} should be packaged");
        }
    }

    private static void EveryPackageIsListed()
    {
        var directory = Bundle();
        using var spdx = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "NUGET-PACKAGES.spdx.json")));
        var expected = spdx.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => (
                Name: package.GetProperty("name").GetString()!,
                License: package.GetProperty("licenseConcluded").GetString()!))
            .ToArray();
        Assert(expected.Length > 20, "the inventory should list the resolved packages");

        var listed = LicenseBundle.LoadGroups(directory, "1.2.3")
            .SelectMany(group => group.Components)
            .ToArray();
        foreach (var (name, license) in expected)
        {
            var component = listed.FirstOrDefault(item => item.Name == name);
            Assert(component is not null, $"{name} should be listed");
            Assert(component!.License == license, $"{name} should show its {license} license");
        }
    }

    private static void EveryComponentHasText()
    {
        var directory = Bundle();
        foreach (var component in LicenseBundle.LoadGroups(directory, "1.2.3").SelectMany(group => group.Components))
        {
            Assert(component.LicenseFiles.Count > 0,
                $"{component.Name} ({component.License}) has no license file in the bundle");
            var text = LicenseBundle.ReadLicenseText(directory, component);
            Assert(!text.Contains("is not available in this build", StringComparison.Ordinal),
                $"{component.Name} should have license text, got: {text}");
            Assert(text.Length > 100, $"{component.Name} license text looks empty");
        }
    }

    private static void GroupsAreOrganized()
    {
        var groups = LicenseBundle.LoadGroups(Bundle(), "1.2.3");
        var all = groups.SelectMany(group => group.Components).ToArray();

        var microsoft = all.Single(component => component.Name == "Microsoft Calculator");
        Assert(microsoft.License == "MIT" && microsoft.Copyright!.Contains("Microsoft"),
            "Microsoft Calculator should be credited under MIT");
        Assert(all.Single(component => component.Name == "Redmond Calculator").Version == "1.2.3",
            "the app entry should carry the app version");
        Assert(groups.Single(group => group.Name == "Avalonia and UI").Components.Any(c => c.Name == "Avalonia"),
            "Avalonia belongs to the Avalonia and UI group");
        Assert(groups.Single(group => group.Name == "Graphics and native binaries").Components.Any(c => c.Name == "SkiaSharp"),
            "SkiaSharp belongs to the graphics group");
        Assert(all.Select(component => component.Name).Distinct().Count() == all.Length,
            "no component should be listed twice");
        foreach (var group in groups)
        {
            var names = group.Components.Select(component => component.Name).ToArray();
            Assert(names.SequenceEqual(names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)),
                $"{group.Name} should be sorted by name");
        }
    }

    private static void MissingBundleDegradesGracefully()
    {
        var groups = LicenseBundle.LoadGroups(null, "1.2.3");
        var all = groups.SelectMany(group => group.Components).ToArray();
        Assert(all.Any(component => component.Name == "Microsoft Calculator"),
            "curated components should be listed even without a bundle");
        var text = LicenseBundle.ReadLicenseText(null, all[0]);
        Assert(text.Contains("not available in this build", StringComparison.Ordinal),
            "a missing bundle should say so instead of throwing");
    }

    private static void DamagedInventoryDegradesGracefully()
    {
        var directory = Directory.CreateTempSubdirectory("license-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "NUGET-PACKAGES.spdx.json"), "{ not json");
            var all = LicenseBundle.LoadGroups(directory, "1.2.3").SelectMany(group => group.Components).ToArray();
            Assert(all.Any(component => component.Name == "Microsoft Calculator"),
                "curated components should survive a damaged inventory");
            Assert(all.All(component => component.Source is null || component.Source.StartsWith("https://")),
                "only https sources are ever shown");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void PathTraversalIsIgnored()
    {
        var root = Directory.CreateTempSubdirectory("license-test-").FullName;
        try
        {
            var directory = Path.Combine(root, "Licenses");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(root, "secret.txt"), "TOP SECRET");
            File.WriteAllText(Path.Combine(directory, "ok.txt"), "allowed license text");
            var component = new LicenseComponent(
                "x", string.Empty, "MIT", null, null, ["../secret.txt", "ok.txt"]);
            var text = LicenseBundle.ReadLicenseText(directory, component);
            Assert(!text.Contains("TOP SECRET", StringComparison.Ordinal), "a path outside Licenses must not be read");
            Assert(text.Contains("allowed license text", StringComparison.Ordinal), "files inside Licenses should be read");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void TextLoadsLazily()
    {
        var directory = Bundle();
        var component = LicenseBundle.LoadGroups(directory, "1.2.3")
            .SelectMany(group => group.Components)
            .First(item => item.Name == "Inter");
        var row = new LicenseComponentViewModel(component, directory);
        Assert(row.LicenseText.Length == 0, "text should not load before the row is expanded");
        row.IsExpanded = true;
        Assert(row.LicenseText.Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase),
            "expanding Inter should show the OFL text");
    }

    private static void VersionComesFromAssembly()
    {
        Assert(SettingsStrings.AppVersion == "0.1.0", $"expected 0.1.0 but got {SettingsStrings.AppVersion}");
        Assert(!SettingsStrings.AppVersion.Contains('+'), "build metadata should be stripped");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
