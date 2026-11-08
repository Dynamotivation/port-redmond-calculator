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
/// <param name="License">The license as shown to people, for example "MIT" or "OFL-1.1 and GUST Font License".</param>
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
/// <c>Licenses</c> directory, which is the repository's <c>licenses</c> directory
/// (see the README's License section).
/// </summary>
/// <remarks>
/// The list of components is the SPDX inventory <c>sbom.spdx.json</c>, so nothing
/// is hard-coded here: a component appears when the inventory lists it. Everything
/// shown comes from files we ship. The license text itself is displayed as plain
/// text and never interpreted, so a malformed or hostile notice cannot load remote
/// content or run anything.
/// </remarks>
public static class LicenseBundle
{
    // A notice larger than this is truncated rather than loaded whole.
    private const int MaxTextBytes = 1024 * 1024;

    private const string InventoryFile = "sbom.spdx.json";

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

    /// <summary>
    /// Components grouped for display, in the order the inventory lists them.
    /// Empty when the inventory is missing or damaged; never throws for a damaged bundle.
    /// </summary>
    public static IReadOnlyList<LicenseComponentGroup> LoadGroups(string? licenseDirectory)
    {
        var groups = new List<(string Name, List<LicenseComponent> Items)>();
        foreach (var (group, component) in ReadInventory(licenseDirectory))
        {
            var entry = groups.FindIndex(candidate => candidate.Name == group);
            if (entry < 0)
            {
                groups.Add((group, []));
                entry = groups.Count - 1;
            }

            groups[entry].Items.Add(component);
        }

        return groups
            .Select(group => new LicenseComponentGroup(group.Name, group.Items.ToArray()))
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
            return "The license files are not available in this build. See the License section of the project README on GitHub.";
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

    private static IEnumerable<(string Group, LicenseComponent Component)> ReadInventory(string? licenseDirectory)
    {
        if (licenseDirectory is null)
        {
            yield break;
        }

        var path = Path.Combine(licenseDirectory, InventoryFile);
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
            // A damaged inventory only hides the list; the packaged files stay available.
        }

        if (document is null)
        {
            yield break;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("packages", out var packages)
                || packages.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            // Licenses that have no SPDX identifier appear as LicenseRef-<id> with a readable name.
            var licenseNames = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("hasExtractedLicensingInfos", out var infos) && infos.ValueKind == JsonValueKind.Array)
            {
                foreach (var info in infos.EnumerateArray())
                {
                    var id = Text(info, "licenseId");
                    var name = Text(info, "name");
                    if (id.Length > 0 && name.Length > 0)
                    {
                        licenseNames[id] = name;
                    }
                }
            }

            foreach (var package in packages.EnumerateArray())
            {
                var name = Text(package, "name");
                if (name.Length == 0)
                {
                    continue;
                }

                var license = Text(package, "licenseDeclared");
                foreach (var (id, readable) in licenseNames)
                {
                    license = license.Replace(id, readable, StringComparison.Ordinal);
                }

                license = license.Replace(" AND ", " and ", StringComparison.Ordinal);

                var source = Text(package, "downloadLocation");
                var copyright = Text(package, "copyrightText");
                var files = new List<string>();
                var group = "Other";
                if (package.ValueKind == JsonValueKind.Object
                    && package.TryGetProperty("externalRefs", out var references)
                    && references.ValueKind == JsonValueKind.Array)
                {
                    foreach (var reference in references.EnumerateArray())
                    {
                        var locator = Text(reference, "referenceLocator");
                        switch (Text(reference, "referenceType"))
                        {
                            case "license-file" when locator.Length > 0:
                                files.Add(locator);
                                break;
                            case "display-group" when locator.Length > 0:
                                group = locator;
                                break;
                        }
                    }
                }

                yield return (group, new LicenseComponent(
                    name,
                    Text(package, "versionInfo"),
                    license.Length > 0 ? license : "NOASSERTION",
                    copyright.Length > 0 && copyright != "NOASSERTION" ? copyright : null,
                    source.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ? source : null,
                    files));
            }
        }

        static string Text(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
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
