using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Generates (--write) or verifies (--verify) everything derived from licenses/components.json,
// the resolved NuGet graph and the license files below licenses/:
//   licenses/nuget/<package>/LICENSE.txt   one license text per NuGet package
//   licenses/<component>/...               files synced from their source (see components.json)
//   licenses/sbom.spdx.json                SPDX 2.3 inventory of every component, ours included
//   README.md                              the index between the licenses markers

const string usage = "Usage: LicenseAudit (--write|--verify) --root <repository root> --assets <project.assets.json>";
const string indexStart = "<!-- licenses:start -->";
const string indexEnd = "<!-- licenses:end -->";
var reviewedNuGetLicenses = new[] { "MIT", "BSD-3-Clause", "CC0-1.0" };

var options = ParseArguments(args);
var write = options.ContainsKey("--write");
var verify = options.ContainsKey("--verify");
if (write == verify
    || !options.TryGetValue("--root", out var rootArgument)
    || !options.TryGetValue("--assets", out var assetsPath))
{
    Console.Error.WriteLine(usage);
    return 2;
}

var root = Path.GetFullPath(rootArgument);
var licensesRoot = Path.Combine(root, "licenses");
var templatesRoot = Path.Combine(root, "tools", "LicenseAudit", "templates");
var readmePath = Path.Combine(root, "README.md");
var spdxPath = Path.Combine(licensesRoot, "sbom.spdx.json");

var catalog = JsonNode.Parse(File.ReadAllText(Path.Combine(licensesRoot, "components.json")))!.AsObject();
var packages = ReadPackages(assetsPath);

var unreviewed = packages.Where(package => !reviewedNuGetLicenses.Contains(package.License)).ToArray();
if (unreviewed.Length > 0)
{
    Console.Error.WriteLine(
        "Unreviewed package licenses (review each, then allow it in tools/LicenseAudit/Program.cs):\n" +
        string.Join('\n', unreviewed.Select(package => $"  {package.Name} {package.Version}: {package.License}")));
    return 1;
}

var duplicates = packages.GroupBy(package => package.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToArray();
if (duplicates.Length > 0)
{
    Console.Error.WriteLine("Several versions of one package are resolved, which the license layout cannot represent: "
        + string.Join(", ", duplicates.Select(group => group.Key)));
    return 1;
}

var appVersion = ReadAppVersion(Path.Combine(root, "Directory.Build.props"));
var problems = new List<string>();
var items = BuildItems();
var expectedFiles = new Dictionary<string, byte[]>(StringComparer.Ordinal);   // path under licenses/ -> content
var staticFiles = new HashSet<string>(StringComparer.Ordinal);               // committed as is, only checked
ResolveFiles();
var licenseRefs = catalog["licenseRefs"]!.AsArray().Select(node => node!.AsObject()).ToArray();
var spdxText = BuildSpdx(existingCreated: ReadCreated());
var readmeBlock = BuildIndex();

if (problems.Count > 0)
{
    foreach (var problem in problems)
    {
        Console.Error.WriteLine(problem);
    }
    return 1;
}

if (write)
{
    foreach (var (relative, content) in expectedFiles)
    {
        var path = Path.Combine(licensesRoot, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
    }

    foreach (var stray in FindStrayFiles())
    {
        File.Delete(Path.Combine(licensesRoot, stray.Replace('/', Path.DirectorySeparatorChar)));
    }
    DeleteEmptyDirectories(licensesRoot);

    // Keep the timestamp unless something else changed, so regenerating is a no-op.
    var finalSpdx = File.Exists(spdxPath) && NormalizeNewlines(File.ReadAllText(spdxPath)) == spdxText
        ? File.ReadAllText(spdxPath)
        : BuildSpdx(existingCreated: null);
    File.WriteAllText(spdxPath, finalSpdx, new UTF8Encoding(false));
    File.WriteAllText(readmePath, ReplaceIndex(File.ReadAllText(readmePath), readmeBlock), new UTF8Encoding(false));
    Console.WriteLine($"Wrote {items.Count} components ({packages.Count} NuGet packages), their license files, the SPDX inventory and the README index.");
    return 0;
}

foreach (var (relative, content) in expectedFiles)
{
    var path = Path.Combine(licensesRoot, relative.Replace('/', Path.DirectorySeparatorChar));
    if (!File.Exists(path))
    {
        problems.Add($"licenses/{relative} is missing. Run scripts/update-licensing.sh.");
    }
    else if (!NormalizeLineEndings(File.ReadAllBytes(path)).SequenceEqual(content))
    {
        problems.Add($"licenses/{relative} is stale. Run scripts/update-licensing.sh.");
    }
}

foreach (var stray in FindStrayFiles())
{
    problems.Add($"licenses/{stray} belongs to no component. List it in licenses/components.json or delete it.");
}

if (!File.Exists(spdxPath) || NormalizeNewlines(File.ReadAllText(spdxPath)) != spdxText)
{
    problems.Add("licenses/sbom.spdx.json is stale. Run scripts/update-licensing.sh and commit the result.");
}

if (!File.Exists(readmePath) || !TryReadIndex(File.ReadAllText(readmePath), out var currentBlock) || currentBlock != readmeBlock)
{
    problems.Add("The README license index is missing or stale. Run scripts/update-licensing.sh and commit the result.");
}

if (problems.Count > 0)
{
    foreach (var problem in problems)
    {
        Console.Error.WriteLine(problem);
    }
    return 1;
}

Console.WriteLine($"Verified {items.Count} components ({packages.Count} NuGet packages), their license files, the SPDX inventory and the README index.");
return 0;

// ---------------------------------------------------------------------------------------------

List<Item> BuildItems()
{
    var groups = catalog["groups"]!.AsArray().Select(node => node!.GetValue<string>()).ToList();
    var nugetGroups = catalog["nugetGroups"]!.AsArray().Select(node => node!.AsObject()).ToArray();
    var defaultGroup = catalog["defaultNugetGroup"]!.GetValue<string>();
    var result = new List<Item>();

    foreach (var node in catalog["components"]!.AsArray())
    {
        var component = node!.AsObject();
        var group = component["group"]!.GetValue<string>();
        if (!groups.Contains(group))
        {
            problems.Add($"Component {component["id"]} uses the unknown group '{group}'.");
        }
        var version = component["version"]?.GetValue<string>();
        result.Add(new Item(
            Id: component["id"]!.GetValue<string>(),
            Name: component["name"]!.GetValue<string>(),
            Version: version == "app" ? appVersion : version ?? string.Empty,
            License: component["license"]!.GetValue<string>(),
            Copyright: component["copyright"]?.GetValue<string>(),
            Source: component["source"]?.GetValue<string>(),
            Group: group,
            Purpose: component["purpose"]!.GetValue<string>(),
            Directory: component["id"]!.GetValue<string>(),
            Files: component["files"]!.AsArray().Select(file => file!["name"]!.GetValue<string>()).ToArray(),
            Component: component,
            Package: null));
    }

    foreach (var package in packages)
    {
        var group = nugetGroups
            .FirstOrDefault(entry => entry["prefixes"]!.AsArray().Any(prefix =>
                package.Name.StartsWith(prefix!.GetValue<string>(), StringComparison.OrdinalIgnoreCase)))
            ?["group"]!.GetValue<string>() ?? defaultGroup;
        if (!groups.Contains(group))
        {
            problems.Add($"NuGet group '{group}' is not listed in 'groups'.");
        }
        result.Add(new Item(
            Id: package.Name,
            Name: package.Name,
            Version: package.Version,
            License: package.License,
            Copyright: package.Copyright == "NOASSERTION" ? null : package.Copyright,
            Source: package.Source == "NOASSERTION" ? null : package.Source,
            Group: group,
            Purpose: "LIBRARY",
            Directory: $"nuget/{package.Name}",
            Files: ["LICENSE.txt"],
            Component: null,
            Package: package));
    }

    return result
        .OrderBy(item => groups.IndexOf(item.Group))
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();
}

void ResolveFiles()
{
    var dotnetNotices = new Lazy<DotnetNotices>(FindDotnetNotices);
    var noticeFamilies = packages
        .Select(package => (Package: package, Notice: FindPackageFile(package, "THIRD-PARTY-NOTICES")))
        .Where(entry => entry.Notice is not null)
        .GroupBy(entry => NativeNoticeFamily(entry.Package.Name), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
    var claimedFamilies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var item in items)
    {
        if (item.Package is not null)
        {
            expectedFiles[$"{item.Directory}/LICENSE.txt"] = NuGetLicenseText(item.Package);
            continue;
        }

        foreach (var file in item.Component!["files"]!.AsArray().Select(node => node!.AsObject()))
        {
            var relative = $"{item.Directory}/{file["name"]!.GetValue<string>()}";
            if (file["sync"] is not JsonObject sync)
            {
                staticFiles.Add(relative);
                if (!File.Exists(Path.Combine(licensesRoot, relative.Replace('/', Path.DirectorySeparatorChar))))
                {
                    problems.Add($"licenses/{relative} is listed for {item.Name} but does not exist.");
                }
                continue;
            }

            var source = ResolveSyncSource(item, sync);
            if (source is not null)
            {
                expectedFiles[relative] = NormalizeLineEndings(File.ReadAllBytes(source));
            }
        }
    }

    foreach (var family in noticeFamilies.Keys.Where(family => !claimedFamilies.Contains(family)))
    {
        problems.Add($"The resolved packages of '{family}' carry a THIRD-PARTY-NOTICES file that no component lists. " +
            "Add a component with {\"noticeFamily\": \"" + family + "\"} to licenses/components.json.");
    }

    string? ResolveSyncSource(Item item, JsonObject sync)
    {
        if (sync["path"] is { } path)
        {
            var full = Path.Combine(root, path.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
            return Require(File.Exists(full) ? full : null, $"{path} (source of {item.Name})");
        }

        if (sync["dotnet"] is { } dotnet)
        {
            var notices = dotnetNotices.Value;
            return dotnet.GetValue<string>() == "LICENSE.txt" ? notices.License : notices.ThirdPartyNotices;
        }

        if (sync["package"] is { } packageName)
        {
            var package = packages.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, packageName.GetValue<string>(), StringComparison.OrdinalIgnoreCase));
            var file = sync["file"]!.GetValue<string>();
            var full = package is null ? null : Path.Combine(package.PackageDirectory, file);
            return Require(full is not null && File.Exists(full) ? full : null,
                $"{file} in package {packageName} (source of {item.Name})");
        }

        if (sync["noticeFamily"] is { } familyNode)
        {
            var family = familyNode.GetValue<string>();
            claimedFamilies.Add(family);
            if (!noticeFamilies.TryGetValue(family, out var members))
            {
                problems.Add($"No resolved package of '{family}' carries a THIRD-PARTY-NOTICES file (source of {item.Name}).");
                return null;
            }

            var representative = members[0];
            foreach (var member in members.Skip(1))
            {
                if (!FilesMatch(representative.Notice!, member.Notice!))
                {
                    problems.Add($"{member.Package.Name} {member.Package.Version} has a different native notice set from " +
                        $"{representative.Package.Name} in {family}. Preserve both variants before updating.");
                }
            }
            return representative.Notice;
        }

        problems.Add($"Component {item.Id} has a sync source of an unknown kind.");
        return null;
    }

    string? Require(string? path, string description)
    {
        if (path is null)
        {
            problems.Add($"Cannot sync from {description}: the file was not found.");
        }
        return path;
    }
}

IEnumerable<string> FindStrayFiles()
{
    var known = expectedFiles.Keys.Concat(staticFiles).ToHashSet(StringComparer.Ordinal);
    known.Add("components.json");
    known.Add("sbom.spdx.json");
    return Directory.EnumerateFiles(licensesRoot, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(licensesRoot, path).Replace(Path.DirectorySeparatorChar, '/'))
        .Where(relative => !known.Contains(relative))
        .OrderBy(relative => relative, StringComparer.Ordinal)
        .ToArray();
}

static void DeleteEmptyDirectories(string directory)
{
    foreach (var child in Directory.GetDirectories(directory))
    {
        DeleteEmptyDirectories(child);
        if (!Directory.EnumerateFileSystemEntries(child).Any())
        {
            Directory.Delete(child);
        }
    }
}

byte[] NuGetLicenseText(PackageLicense package)
{
    var provided = FindPackageFile(package, "LICENSE") ?? FindPackageFile(package, "LICENCE") ?? FindPackageFile(package, "COPYING");
    if (provided is not null)
    {
        return NormalizeLineEndings(File.ReadAllBytes(provided));
    }

    var templatePath = Path.Combine(templatesRoot, package.License + ".txt");
    var template = NormalizeNewlines(File.ReadAllText(templatePath)).TrimEnd('\n').Split('\n').ToList();
    // The standard text names a placeholder holder; the package's own declared copyright replaces it.
    var placeholder = template.FindIndex(line => line.Contains("<year>", StringComparison.Ordinal));
    if (placeholder >= 0)
    {
        template.RemoveAt(placeholder);
        if (placeholder < template.Count && template[placeholder].Trim().Length == 0)
        {
            template.RemoveAt(placeholder);
        }
    }

    var builder = new StringBuilder();
    builder.Append(package.Name).Append(' ').Append(package.Version).Append('\n');
    builder.Append(package.Copyright).Append('\n');
    if (package.Source != "NOASSERTION")
    {
        builder.Append("Source: ").Append(package.Source).Append('\n');
    }
    builder.Append("License: ").Append(package.License).Append("\n\n");
    builder.Append(string.Join('\n', template)).Append('\n');
    return new UTF8Encoding(false).GetBytes(builder.ToString());
}

string? FindPackageFile(PackageLicense package, string stem)
{
    var nuspec = Directory.EnumerateFiles(package.PackageDirectory, "*.nuspec", SearchOption.TopDirectoryOnly).Single();
    if (stem == "LICENSE")
    {
        var declared = XDocument.Load(nuspec).Descendants().FirstOrDefault(element =>
            element.Name.LocalName == "license" && element.Attribute("type")?.Value == "file")?.Value.Trim();
        if (!string.IsNullOrEmpty(declared))
        {
            var declaredPath = Path.GetFullPath(Path.Combine(package.PackageDirectory, declared));
            if (declaredPath.StartsWith(package.PackageDirectory, StringComparison.OrdinalIgnoreCase) && File.Exists(declaredPath))
            {
                return declaredPath;
            }
        }
    }

    return Directory.EnumerateFiles(package.PackageDirectory, "*", SearchOption.TopDirectoryOnly)
        .Where(path =>
        {
            var name = Path.GetFileName(path);
            return name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)
                && (name.Length == stem.Length || name[stem.Length] is '.' or '-' or '_')
                && Path.GetExtension(name).ToLowerInvariant() is "" or ".txt" or ".md";
        })
        .OrderBy(path => Path.GetFileNameWithoutExtension(path).Length)
        .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
        .FirstOrDefault();
}

string? ReadCreated()
{
    if (!File.Exists(spdxPath))
    {
        return null;
    }

    try
    {
        return JsonNode.Parse(File.ReadAllText(spdxPath))?["creationInfo"]?["created"]?.GetValue<string>();
    }
    catch (JsonException)
    {
        return null;
    }
}

string BuildSpdx(string? existingCreated)
{
    var identity = string.Join('\n', items.Select(item => $"{item.Name}|{item.Version}|{item.License}"));
    var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    var application = items.Single(item => item.Purpose == "APPLICATION");
    var packageNodes = new JsonArray();
    var relationships = new JsonArray();
    foreach (var item in items)
    {
        var spdxId = SpdxId(item);
        var references = new JsonArray();
        foreach (var file in item.Files)
        {
            references.Add(new JsonObject
            {
                ["referenceCategory"] = "OTHER",
                ["referenceType"] = "license-file",
                ["referenceLocator"] = $"{item.Directory}/{file}",
            });
        }
        references.Add(new JsonObject
        {
            ["referenceCategory"] = "OTHER",
            ["referenceType"] = "display-group",
            ["referenceLocator"] = item.Group,
        });

        var node = new JsonObject
        {
            ["SPDXID"] = spdxId,
            ["name"] = item.Name,
        };
        if (item.Version.Length > 0)
        {
            node["versionInfo"] = item.Version;
        }
        node["downloadLocation"] = item.Source ?? "NOASSERTION";
        node["filesAnalyzed"] = false;
        node["licenseConcluded"] = item.License;
        node["licenseDeclared"] = item.License;
        node["copyrightText"] = item.Copyright ?? "NOASSERTION";
        node["supplier"] = "NOASSERTION";
        node["primaryPackagePurpose"] = item.Purpose;
        node["externalRefs"] = references;
        packageNodes.Add(node);

        relationships.Add(new JsonObject
        {
            ["spdxElementId"] = "SPDXRef-DOCUMENT",
            ["relationshipType"] = "DESCRIBES",
            ["relatedSpdxElement"] = spdxId,
        });
        if (item != application && (item.Package is null || item.Package.IsDirect))
        {
            relationships.Add(new JsonObject
            {
                ["spdxElementId"] = SpdxId(application),
                ["relationshipType"] = "DEPENDS_ON",
                ["relatedSpdxElement"] = spdxId,
            });
        }
    }

    var extracted = new JsonArray();
    foreach (var licenseRef in licenseRefs)
    {
        var file = Path.Combine(licensesRoot, licenseRef["file"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar));
        extracted.Add(new JsonObject
        {
            ["licenseId"] = "LicenseRef-" + licenseRef["id"]!.GetValue<string>(),
            ["name"] = licenseRef["name"]!.GetValue<string>(),
            ["extractedText"] = File.Exists(file) ? NormalizeNewlines(File.ReadAllText(file)).TrimEnd('\n') : string.Empty,
        });
    }

    var document = new JsonObject
    {
        ["spdxVersion"] = "SPDX-2.3",
        ["dataLicense"] = "CC0-1.0",
        ["SPDXID"] = "SPDXRef-DOCUMENT",
        ["name"] = "Redmond Calculator",
        ["documentNamespace"] = $"https://github.com/Dynamotivation/port-redmond-calculator/sbom/{digest}",
        ["creationInfo"] = new JsonObject
        {
            ["created"] = existingCreated ?? DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["creators"] = new JsonArray("Tool: Redmond LicenseAudit"),
        },
        ["hasExtractedLicensingInfos"] = extracted,
        ["packages"] = packageNodes,
        ["relationships"] = relationships,
    };
    return NormalizeNewlines(document.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    })) + "\n";
}

string BuildIndex()
{
    var groups = catalog["groups"]!.AsArray().Select(node => node!.GetValue<string>());
    var builder = new StringBuilder();
    builder.Append("Every component below ships with its own license text in [`licenses/`](licenses). ")
        .Append("The complete inventory, with this project's own entries, is the SPDX 2.3 document ")
        .Append("[`licenses/sbom.spdx.json`](licenses/sbom.spdx.json). The application shows the same list ")
        .Append("under Settings, and every build carries the directory as `Licenses`. Both are generated by ")
        .Append("`scripts/update-licensing.sh`; pull request checks fail when they are out of date.\n");
    foreach (var group in groups)
    {
        var members = items.Where(item => item.Group == group).ToArray();
        if (members.Length == 0)
        {
            continue;
        }

        builder.Append("\n<details>\n<summary><strong>").Append(group).Append("</strong> (").Append(members.Length).Append(")</summary>\n\n");
        builder.Append("| Component | Version | License | License files |\n|---|---|---|---|\n");
        foreach (var item in members)
        {
            builder.Append("| ").Append(EscapeMarkdown(item.Name)).Append(" | ")
                .Append(item.Version.Length > 0 ? EscapeMarkdown(item.Version) : "").Append(" | ")
                .Append(EscapeMarkdown(DisplayLicense(item.License))).Append(" | ")
                .Append(string.Join(", ", item.Files.Select(file =>
                    $"[{EscapeMarkdown(file)}](licenses/{Uri.EscapeDataString(item.Directory).Replace("%2F", "/")}/{Uri.EscapeDataString(file)})")))
                .Append(" |\n");
        }
        builder.Append("\n</details>\n");
    }
    return builder.ToString();
}

string DisplayLicense(string expression)
{
    foreach (var licenseRef in licenseRefs)
    {
        expression = expression.Replace("LicenseRef-" + licenseRef["id"]!.GetValue<string>(), licenseRef["name"]!.GetValue<string>(), StringComparison.Ordinal);
    }
    return expression.Replace(" AND ", " and ", StringComparison.Ordinal);
}

static string SpdxId(Item item) =>
    "SPDXRef-Package-" + Sanitize(item.Id) + (item.Package is null ? string.Empty : "-" + Sanitize(item.Version));

static string Sanitize(string value) =>
    new(value.Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' ? character : '-').ToArray());

static string ReplaceIndex(string readme, string block)
{
    var normalized = NormalizeNewlines(readme);
    var start = normalized.IndexOf(indexStart, StringComparison.Ordinal);
    var end = normalized.IndexOf(indexEnd, StringComparison.Ordinal);
    if (start < 0 || end < start)
    {
        throw new InvalidOperationException($"README.md needs the markers {indexStart} and {indexEnd} around the license index.");
    }

    return normalized[..(start + indexStart.Length)] + "\n" + block + normalized[end..];
}

static bool TryReadIndex(string readme, out string block)
{
    var normalized = NormalizeNewlines(readme);
    var start = normalized.IndexOf(indexStart, StringComparison.Ordinal);
    var end = normalized.IndexOf(indexEnd, StringComparison.Ordinal);
    if (start < 0 || end < start)
    {
        block = string.Empty;
        return false;
    }

    block = normalized[(start + indexStart.Length + 1)..end];
    return true;
}

static string ReadAppVersion(string propsPath)
{
    var match = Regex.Match(File.ReadAllText(propsPath), "<Version>\\s*([^<\\s]+)\\s*</Version>");
    return match.Success
        ? match.Groups[1].Value
        : throw new InvalidOperationException($"{propsPath} does not define <Version>.");
}

static Dictionary<string, string> ParseArguments(string[] arguments)
{
    var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var index = 0; index < arguments.Length; index++)
    {
        var argument = arguments[index];
        if (argument is "--write" or "--verify")
        {
            parsed[argument] = "true";
            continue;
        }

        if (!argument.StartsWith("--", StringComparison.Ordinal) || index + 1 >= arguments.Length)
        {
            continue;
        }
        parsed[argument] = arguments[++index];
    }
    return parsed;
}

static List<PackageLicense> ReadPackages(string assetsPath)
{
    using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
    var root = document.RootElement;
    var packageFolder = root.GetProperty("packageFolders").EnumerateObject().Select(property => property.Name).FirstOrDefault()
        ?? throw new InvalidOperationException("No NuGet package folder was recorded.");

    var directPackages = root.GetProperty("project")
        .GetProperty("frameworks")
        .EnumerateObject()
        .SelectMany(framework => framework.Value.GetProperty("dependencies").EnumerateObject())
        .Select(dependency => dependency.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var packages = new List<PackageLicense>();
    foreach (var library in root.GetProperty("libraries").EnumerateObject())
    {
        if (!library.Value.TryGetProperty("type", out var type) || type.GetString() != "package")
        {
            continue;
        }

        var separator = library.Name.LastIndexOf('/');
        if (separator <= 0)
        {
            continue;
        }
        var name = library.Name[..separator];
        var version = library.Name[(separator + 1)..];
        var packageDirectory = Path.GetFullPath(Path.Combine(packageFolder, name.ToLowerInvariant(), version));
        var nuspecPath = Directory.EnumerateFiles(packageDirectory, "*.nuspec", SearchOption.TopDirectoryOnly).Single();
        var metadata = XDocument.Load(nuspecPath).Descendants().First(element => element.Name.LocalName == "metadata");

        string? Element(string localName) => metadata.Elements().FirstOrDefault(element => element.Name.LocalName == localName)?.Value.Trim();

        var licenseElement = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "license");
        var license = licenseElement?.Attribute("type")?.Value == "expression" ? licenseElement.Value.Trim() : string.Empty;
        license = name switch
        {
            "Avalonia.Angle.Windows.Natives" => "BSD-3-Clause",
            "GenericTensor" => "MIT",
            _ when !string.IsNullOrWhiteSpace(license) => license,
            _ => Element("licenseUrl") ?? "NOASSERTION",
        };

        var repository = metadata.Elements().FirstOrDefault(element => element.Name.LocalName == "repository")?.Attribute("url")?.Value;
        var source = repository ?? Element("projectUrl") ?? "NOASSERTION";
        var authors = Element("authors") ?? "NOASSERTION";
        var copyright = Element("copyright");
        if (string.IsNullOrWhiteSpace(copyright))
        {
            copyright = authors == "NOASSERTION" ? "NOASSERTION" : $"Copyright holders: {authors}";
        }

        packages.Add(new PackageLicense(name, version, directPackages.Contains(name), license, authors, copyright, source, packageDirectory));
    }

    return packages
        .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(package => package.Version, StringComparer.Ordinal)
        .ToList();
}

static string NativeNoticeFamily(string packageName)
{
    const string nativeAssetsMarker = ".NativeAssets.";
    var markerIndex = packageName.IndexOf(nativeAssetsMarker, StringComparison.OrdinalIgnoreCase);
    return markerIndex < 0 ? packageName : packageName[..markerIndex];
}

static DotnetNotices FindDotnetNotices()
{
    // The notices sit at the root of a .NET distribution and are not versioned.
    // CI installs the pinned SDK next to a runner image's older one with
    // --skip-non-versioned-files, so the default lookup would read the image's
    // copy. DOTNET_NOTICES_ROOT points at a distribution that was installed
    // whole, so the notices belong to the pinned version.
    var explicitRoot = Environment.GetEnvironmentVariable("DOTNET_NOTICES_ROOT");
    if (!string.IsNullOrWhiteSpace(explicitRoot))
    {
        var explicitLicense = Path.Combine(explicitRoot, "LICENSE.txt");
        var explicitNotices = Path.Combine(explicitRoot, "ThirdPartyNotices.txt");
        if (File.Exists(explicitLicense) && File.Exists(explicitNotices))
        {
            return new DotnetNotices(explicitLicense, explicitNotices);
        }

        throw new InvalidOperationException(
            $"DOTNET_NOTICES_ROOT is set to '{explicitRoot}', which does not contain LICENSE.txt and ThirdPartyNotices.txt.");
    }

    var candidateRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    AddWithAncestors(Environment.GetEnvironmentVariable("DOTNET_ROOT"));
    AddWithAncestors(Path.GetDirectoryName(Environment.ProcessPath));
    AddWithAncestors(RuntimeEnvironment.GetRuntimeDirectory());

    foreach (var candidateRoot in candidateRoots)
    {
        var candidateDirectories = new[]
        {
            candidateRoot,
            Path.Combine(candidateRoot, "share", "doc", "dotnet"),
            Path.Combine(candidateRoot, "..", "share", "doc", "dotnet"),
        };
        foreach (var directory in candidateDirectories)
        {
            var fullDirectory = Path.GetFullPath(directory);
            var license = Path.Combine(fullDirectory, "LICENSE.txt");
            var thirdPartyNotices = Path.Combine(fullDirectory, "ThirdPartyNotices.txt");
            if (File.Exists(license) && File.Exists(thirdPartyNotices))
            {
                return new DotnetNotices(license, thirdPartyNotices);
            }
        }
    }

    throw new InvalidOperationException(
        "The active .NET distribution's LICENSE.txt and ThirdPartyNotices.txt could not be located. " +
        "Set DOTNET_ROOT to the root of an official .NET installation.");

    void AddWithAncestors(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var directory = new DirectoryInfo(Path.GetFullPath(path));
        if (!directory.Exists && directory.Parent is not null)
        {
            directory = directory.Parent;
        }
        for (var depth = 0; directory is not null && depth < 6; depth++)
        {
            candidateRoots.Add(directory.FullName);
            directory = directory.Parent;
        }
    }
}

static bool FilesMatch(string expectedPath, string actualPath) =>
    File.Exists(actualPath)
    && NormalizeLineEndings(File.ReadAllBytes(expectedPath)).SequenceEqual(NormalizeLineEndings(File.ReadAllBytes(actualPath)));

// Redistributed notices are text. Git normalizes their line endings while upstream sources
// ship CRLF or LF depending on the platform, so content is compared and written as LF.
static byte[] NormalizeLineEndings(byte[] content)
{
    var normalized = new List<byte>(content.Length);
    for (var index = 0; index < content.Length; index++)
    {
        if (content[index] == (byte)'\r' && index + 1 < content.Length && content[index + 1] == (byte)'\n')
        {
            continue;
        }
        normalized.Add(content[index]);
    }
    return normalized.ToArray();
}

static string NormalizeNewlines(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal);

static string EscapeMarkdown(string value) =>
    value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

internal sealed record PackageLicense(
    string Name,
    string Version,
    bool IsDirect,
    string License,
    string Authors,
    string Copyright,
    string Source,
    string PackageDirectory);

internal sealed record Item(
    string Id,
    string Name,
    string Version,
    string License,
    string? Copyright,
    string? Source,
    string Group,
    string Purpose,
    string Directory,
    string[] Files,
    JsonObject? Component,
    PackageLicense? Package);

internal sealed record DotnetNotices(string License, string ThirdPartyNotices);
