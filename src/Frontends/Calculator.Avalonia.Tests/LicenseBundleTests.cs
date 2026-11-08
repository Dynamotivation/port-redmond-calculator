using System.Text.Json;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class LicenseBundleTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("license bundle is packaged next to the app", BundleIsPackaged),
        ("every inventory entry is listed with its license", EveryEntryIsListed),
        ("every listed component has displayable license text", EveryComponentHasText),
        ("components are grouped, sorted and Microsoft Calculator is credited", GroupsAreOrganized),
        ("licenses without an SPDX identifier read as names", NamedLicensesAreReadable),
        ("a missing bundle lists nothing and says so", MissingBundleDegradesGracefully),
        ("a damaged inventory does not throw", DamagedInventoryDegradesGracefully),
        ("license paths cannot escape the Licenses directory", PathTraversalIsIgnored),
        ("license text loads only when a row is expanded", TextLoadsLazily),
        ("About shows the assembly version, which the inventory records", VersionComesFromAssembly),
    ];

    private static string Bundle() =>
        LicenseBundle.FindDirectory()
        ?? throw new InvalidOperationException("The Licenses directory was not copied next to the test host.");

    private static IReadOnlyList<LicenseComponent> Components() =>
        LicenseBundle.LoadGroups(Bundle()).SelectMany(group => group.Components).ToArray();

    private static void BundleIsPackaged()
    {
        var directory = Bundle();
        foreach (var required in new[] { "sbom.spdx.json", "redmond-calculator/LICENSE.txt", "microsoft-calculator/LICENSE.txt" })
        {
            Assert(new FileInfo(Path.Combine(directory, required)) is { Exists: true, Length: > 0 },
                $"{required} should be packaged");
        }
    }

    private static void EveryEntryIsListed()
    {
        var directory = Bundle();
        using var spdx = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "sbom.spdx.json")));
        var expected = spdx.RootElement.GetProperty("packages").EnumerateArray()
            .Select(package => (
                Name: package.GetProperty("name").GetString()!,
                License: package.GetProperty("licenseDeclared").GetString()!))
            .ToArray();
        Assert(expected.Length > 40, "the inventory should list the components and the resolved packages");

        var listed = Components();
        Assert(listed.Count == expected.Length, $"expected {expected.Length} components but listed {listed.Count}");
        foreach (var (name, license) in expected)
        {
            var component = listed.FirstOrDefault(item => item.Name == name);
            Assert(component is not null, $"{name} should be listed");
            if (!license.Contains("LicenseRef-", StringComparison.Ordinal))
            {
                Assert(component!.License == license.Replace(" AND ", " and ", StringComparison.Ordinal),
                    $"{name} should show its {license} license");
            }
        }
    }

    private static void EveryComponentHasText()
    {
        var directory = Bundle();
        foreach (var component in Components())
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
        var groups = LicenseBundle.LoadGroups(Bundle());
        var all = groups.SelectMany(group => group.Components).ToArray();

        var microsoft = all.Single(component => component.Name == "Microsoft Calculator");
        Assert(microsoft.License == "MIT" && microsoft.Copyright!.Contains("Microsoft"),
            "Microsoft Calculator should be credited under MIT");
        Assert(groups[0].Name == "Microsoft Calculator", "Microsoft Calculator should come first");
        Assert(all.Any(component => component.Name == "Redmond Calculator"), "the app lists itself");
        Assert(all.Any(component => component.Name == "Redmond Commons"), "Redmond Commons is credited");
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

    private static void NamedLicensesAreReadable()
    {
        var all = Components();
        Assert(all.Single(component => component.Name == "Latin Modern Math").License == "GUST Font License",
            "Latin Modern Math should show the readable license name");
        Assert(all.Single(component => component.Name == "CSharpMath embedded fonts").License == "OFL-1.1 and GUST Font License",
            "a combined license should read naturally");
        Assert(all.All(component => !component.License.Contains("LicenseRef-", StringComparison.Ordinal)),
            "no raw license reference should reach the screen");
    }

    private static void MissingBundleDegradesGracefully()
    {
        Assert(LicenseBundle.LoadGroups(null).Count == 0, "no bundle means nothing to list");
        var text = LicenseBundle.ReadLicenseText(null, new LicenseComponent("x", string.Empty, "MIT", null, null, ["x/LICENSE.txt"]));
        Assert(text.Contains("not available in this build", StringComparison.Ordinal),
            "a missing bundle should say so instead of throwing");
    }

    private static void DamagedInventoryDegradesGracefully()
    {
        var directory = Directory.CreateTempSubdirectory("license-test-").FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "sbom.spdx.json"), "{ not json");
            Assert(LicenseBundle.LoadGroups(directory).Count == 0, "a damaged inventory lists nothing");

            File.WriteAllText(Path.Combine(directory, "sbom.spdx.json"),
                "{\"packages\":[{\"name\":\"x\",\"downloadLocation\":\"http://insecure.example\"},42,{\"name\":\"y\"}]}");
            var all = LicenseBundle.LoadGroups(directory).SelectMany(group => group.Components).ToArray();
            Assert(all.Length == 2, "entries that cannot be read are skipped");
            Assert(all.All(component => component.Source is null), "only https sources are ever shown");
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
        var component = Components().First(item => item.Name == "Inter");
        var row = new LicenseComponentViewModel(component, directory);
        Assert(row.LicenseText.Length == 0, "text should not load before the row is expanded");
        row.IsExpanded = true;
        Assert(row.LicenseText.Contains("SIL OPEN FONT LICENSE", StringComparison.OrdinalIgnoreCase),
            "expanding Inter should show the OFL text");
    }

    private static void VersionComesFromAssembly()
    {
        Assert(!SettingsStrings.AppVersion.Contains('+'), "build metadata should be stripped");
        var recorded = Components().Single(component => component.Name == "Redmond Calculator").Version;
        Assert(recorded == SettingsStrings.AppVersion,
            $"the inventory records {recorded} but the assembly is {SettingsStrings.AppVersion}; run scripts/update-licensing.sh");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
