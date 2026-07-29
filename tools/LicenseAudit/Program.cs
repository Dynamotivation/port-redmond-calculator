using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;

const string usage =
    "Usage: LicenseAudit (--write|--verify) --assets <project.assets.json> " +
    "--notices <NUGET-PACKAGES.md> --spdx <NUGET-PACKAGES.spdx.json> " +
    "--license-root <license bundle directory>";

var options = ParseArguments(args);
var write = options.ContainsKey("--write");
var verify = options.ContainsKey("--verify");
if (write == verify
    || !options.TryGetValue("--assets", out var assetsPath)
    || !options.TryGetValue("--notices", out var noticesPath)
    || !options.TryGetValue("--spdx", out var spdxPath)
    || !options.TryGetValue("--license-root", out var licenseRoot))
{
    Console.Error.WriteLine(usage);
    return 2;
}

var packages = ReadPackages(assetsPath);
var unknownLicenses = packages
    .Where(package => package.License is not ("MIT" or "BSD-3-Clause" or "CC0-1.0"))
    .ToArray();
if (unknownLicenses.Length > 0)
{
    Console.Error.WriteLine(
        "Unreviewed package licenses:\n" +
        string.Join('\n', unknownLicenses.Select(package =>
            $"  {package.Name} {package.Version}: {package.License}")));
    return 1;
}

var notices = CreateNotices(packages);
var spdx = CreateSpdx(packages);
var redistributedFiles = ResolveRedistributedFiles(packages, licenseRoot);
var redistributedManifestPath =
    Path.Combine(licenseRoot, "GENERATED-REDISTRIBUTION-FILES.txt");
var redistributedManifest = string.Join(
    '\n',
    redistributedFiles
        .Select(file => Path.GetRelativePath(licenseRoot, file.Destination)
            .Replace(Path.DirectorySeparatorChar, '/'))
        .OrderBy(path => path, StringComparer.Ordinal)) + "\n";
if (write)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(noticesPath))!);
    File.WriteAllText(noticesPath, notices, new UTF8Encoding(false));
    File.WriteAllText(
        spdxPath,
        spdx.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n",
        new UTF8Encoding(false));
    var nativeDirectory = Path.Combine(licenseRoot, "Native-Dependencies");
    if (Directory.Exists(nativeDirectory))
    {
        var expectedNativeFiles = redistributedFiles
            .Select(file => Path.GetFullPath(file.Destination))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var existingFile in Directory.EnumerateFiles(nativeDirectory))
        {
            if (!expectedNativeFiles.Contains(Path.GetFullPath(existingFile)))
            {
                File.Delete(existingFile);
            }
        }
    }
    foreach (var redistributedFile in redistributedFiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(redistributedFile.Destination)!);
        File.Copy(
            redistributedFile.Source,
            redistributedFile.Destination,
            overwrite: true);
    }
    File.WriteAllText(
        redistributedManifestPath,
        redistributedManifest,
        new UTF8Encoding(false));
    Console.WriteLine($"Wrote notices for {packages.Count} resolved NuGet packages.");
    return 0;
}

var failed = false;
if (!File.Exists(noticesPath)
    || !string.Equals(
        NormalizeNewlines(File.ReadAllText(noticesPath)),
        NormalizeNewlines(notices),
        StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"{noticesPath} is stale. Run scripts/update-licensing.sh.");
    failed = true;
}

if (!File.Exists(spdxPath) || !SpdxMatches(spdxPath, packages))
{
    Console.Error.WriteLine(
        $"{spdxPath} is stale. Run scripts/update-licensing.sh.");
    failed = true;
}

foreach (var redistributedFile in redistributedFiles)
{
    if (!FilesMatch(redistributedFile.Source, redistributedFile.Destination))
    {
        Console.Error.WriteLine(
            $"{redistributedFile.Destination} is missing or stale. " +
            "Run scripts/update-licensing.sh.");
        failed = true;
    }
}

if (!File.Exists(redistributedManifestPath)
    || !string.Equals(
        NormalizeNewlines(File.ReadAllText(redistributedManifestPath)),
        redistributedManifest,
        StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"{redistributedManifestPath} is missing or stale. " +
        "Run scripts/update-licensing.sh.");
    failed = true;
}

if (failed)
{
    return 1;
}

Console.WriteLine(
    $"Verified notices and SPDX inventory for {packages.Count} resolved NuGet packages.");
return 0;

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

        if (!argument.StartsWith("--", StringComparison.Ordinal)
            || index + 1 >= arguments.Length)
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
    var packageFolder = root.GetProperty("packageFolders")
        .EnumerateObject()
        .Select(property => property.Name)
        .FirstOrDefault()
        ?? throw new InvalidOperationException("No NuGet package folder was recorded.");

    var directPackages = root.GetProperty("project")
        .GetProperty("frameworks")
        .EnumerateObject()
        .SelectMany(framework =>
            framework.Value.GetProperty("dependencies").EnumerateObject())
        .Select(dependency => dependency.Name)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    var packages = new List<PackageLicense>();
    foreach (var library in root.GetProperty("libraries").EnumerateObject())
    {
        if (!library.Value.TryGetProperty("type", out var type)
            || type.GetString() != "package")
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
        var packageDirectory = Path.Combine(
            packageFolder,
            name.ToLowerInvariant(),
            version);
        var nuspecPath = Directory
            .EnumerateFiles(packageDirectory, "*.nuspec", SearchOption.TopDirectoryOnly)
            .Single();
        var metadata = XDocument.Load(nuspecPath)
            .Descendants()
            .First(element => element.Name.LocalName == "metadata");

        string? Element(string localName) => metadata.Elements()
            .FirstOrDefault(element => element.Name.LocalName == localName)
            ?.Value.Trim();

        var licenseElement = metadata.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "license");
        var license = licenseElement?.Attribute("type")?.Value == "expression"
            ? licenseElement.Value.Trim()
            : string.Empty;
        license = name switch
        {
            "Avalonia.Angle.Windows.Natives" => "BSD-3-Clause",
            "GenericTensor" => "MIT",
            _ when !string.IsNullOrWhiteSpace(license) => license,
            _ => Element("licenseUrl") ?? "NOASSERTION",
        };

        var repository = metadata.Elements()
            .FirstOrDefault(element => element.Name.LocalName == "repository")
            ?.Attribute("url")?.Value;
        var source = repository ?? Element("projectUrl") ?? "NOASSERTION";
        var authors = Element("authors") ?? "NOASSERTION";
        var copyright = Element("copyright");
        if (string.IsNullOrWhiteSpace(copyright))
        {
            copyright = authors == "NOASSERTION"
                ? "NOASSERTION"
                : $"Copyright holders: {authors}";
        }

        packages.Add(new PackageLicense(
            name,
            version,
            directPackages.Contains(name),
            license,
            authors,
            copyright,
            source,
            packageDirectory));
    }

    return packages
        .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
        .ThenBy(package => package.Version, StringComparer.Ordinal)
        .ToList();
}

static string CreateNotices(IReadOnlyList<PackageLicense> packages)
{
    var builder = new StringBuilder();
    builder.AppendLine("# Resolved NuGet package notices");
    builder.AppendLine();
    builder.AppendLine(
        "This inventory is generated from `Calculator.Avalonia`'s resolved " +
        "`project.assets.json`. Package copyrights remain with their respective holders.");
    builder.AppendLine();
    builder.AppendLine("| Package | Version | Relationship | License | Copyright / authors | Source |");
    builder.AppendLine("|---|---:|---|---|---|---|");
    foreach (var package in packages)
    {
        builder.Append("| ")
            .Append(EscapeMarkdown(package.Name)).Append(" | ")
            .Append(EscapeMarkdown(package.Version)).Append(" | ")
            .Append(package.IsDirect ? "Direct" : "Transitive").Append(" | ")
            .Append(EscapeMarkdown(package.License)).Append(" | ")
            .Append(EscapeMarkdown(package.Copyright)).Append(" | ")
            .Append(package.Source == "NOASSERTION"
                ? "Not declared"
                : $"<{package.Source}>")
            .AppendLine(" |");
    }

    builder.AppendLine();
    builder.AppendLine("The corresponding complete license texts are distributed in:");
    builder.AppendLine();
    builder.AppendLine("- `NuGet/MIT.txt`");
    builder.AppendLine("- `NuGet/BSD-3-Clause.txt`");
    builder.AppendLine("- `NuGet/CC0-1.0.txt`");
    builder.AppendLine();
    builder.AppendLine(
        "Package-level expressions do not replace licenses for embedded assets. " +
        "Inter and CSharpMath's embedded fonts are documented separately in this bundle.");
    return builder.ToString();
}

static JsonObject CreateSpdx(IReadOnlyList<PackageLicense> packages)
{
    var identity = string.Join(
        '\n',
        packages.Select(package =>
            $"{package.Name}|{package.Version}|{package.License}"));
    var digest = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    var packageNodes = new JsonArray();
    var relationships = new JsonArray();
    foreach (var package in packages)
    {
        var spdxId = $"SPDXRef-Package-{SanitizeSpdxId(package.Name)}-{SanitizeSpdxId(package.Version)}";
        packageNodes.Add(new JsonObject
        {
            ["SPDXID"] = spdxId,
            ["name"] = package.Name,
            ["versionInfo"] = package.Version,
            ["downloadLocation"] = package.Source,
            ["filesAnalyzed"] = false,
            ["licenseConcluded"] = package.License,
            ["licenseDeclared"] = package.License,
            ["copyrightText"] = package.Copyright,
            ["supplier"] = "NOASSERTION",
            ["primaryPackagePurpose"] = "LIBRARY",
        });
        relationships.Add(new JsonObject
        {
            ["spdxElementId"] = "SPDXRef-DOCUMENT",
            ["relationshipType"] = "DESCRIBES",
            ["relatedSpdxElement"] = spdxId,
        });
    }

    return new JsonObject
    {
        ["spdxVersion"] = "SPDX-2.3",
        ["dataLicense"] = "CC0-1.0",
        ["SPDXID"] = "SPDXRef-DOCUMENT",
        ["name"] = "Redmond Calculator resolved NuGet packages",
        ["documentNamespace"] =
            $"https://github.com/Dynamotivation/port-redmond-calculator/sbom/nuget-{digest}",
        ["creationInfo"] = new JsonObject
        {
            ["created"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            ["creators"] = new JsonArray("Tool: Redmond LicenseAudit"),
        },
        ["packages"] = packageNodes,
        ["relationships"] = relationships,
    };
}

static List<RedistributedFile> ResolveRedistributedFiles(
    IReadOnlyList<PackageLicense> packages,
    string licenseRoot)
{
    var dotnetNotices = FindDotnetNotices();
    List<RedistributedFile> files =
    [
        new RedistributedFile(
            dotnetNotices.License,
            Path.Combine(licenseRoot, "Dotnet-Runtime", "LICENSE.txt")),
        new RedistributedFile(
            dotnetNotices.ThirdPartyNotices,
            Path.Combine(
                licenseRoot,
                "Dotnet-Runtime",
                "ThirdPartyNotices.txt"))
    ];

    var angle = packages.FirstOrDefault(package => string.Equals(
        package.Name,
        "Avalonia.Angle.Windows.Natives",
        StringComparison.OrdinalIgnoreCase));
    if (angle is not null)
    {
        files.Add(FromPackage(
            angle,
            "LICENSE",
            "Native-Dependencies/ANGLE-LICENSE.txt"));
    }

    var packageNotices = packages
        .Select(package => new
        {
            Package = package,
            Notice = Directory
                .EnumerateFiles(
                    package.PackageDirectory,
                    "*",
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault(path =>
                    Path.GetFileName(path).StartsWith(
                        "THIRD-PARTY-NOTICES",
                        StringComparison.OrdinalIgnoreCase)),
        })
        .Where(item => item.Notice is not null)
        .GroupBy(
            item => NativeNoticeFamily(item.Package.Name),
            StringComparer.OrdinalIgnoreCase);
    foreach (var family in packageNotices)
    {
        var representative = family.First();
        foreach (var item in family.Skip(1))
        {
            if (!FilesMatch(representative.Notice!, item.Notice!))
            {
                throw new InvalidOperationException(
                    $"{item.Package.Name} {item.Package.Version} has a " +
                    "different native notice set from another package in " +
                    $"{family.Key}. Preserve both variants before updating.");
            }
        }
        files.Add(new RedistributedFile(
            representative.Notice!,
            Path.Combine(
                licenseRoot,
                "Native-Dependencies",
                $"{SanitizeSpdxId(family.Key)}-THIRD-PARTY-NOTICES.txt")));
    }
    return files;

    RedistributedFile FromPackage(
        PackageLicense package,
        string sourceFile,
        string destinationFile)
    {
        var source = Path.Combine(package.PackageDirectory, sourceFile);
        if (!File.Exists(source))
        {
            throw new InvalidOperationException(
                $"{package.Name} {package.Version} does not contain {sourceFile}.");
        }
        return new RedistributedFile(
            source,
            Path.Combine(
                licenseRoot,
                destinationFile.Replace('/', Path.DirectorySeparatorChar)));
    }

    static string NativeNoticeFamily(string packageName)
    {
        const string nativeAssetsMarker = ".NativeAssets.";
        var markerIndex = packageName.IndexOf(
            nativeAssetsMarker,
            StringComparison.OrdinalIgnoreCase);
        return markerIndex < 0
            ? packageName
            : packageName[..markerIndex];
    }
}

static DotnetNotices FindDotnetNotices()
{
    var candidateRoots = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase);

    AddWithAncestors(Environment.GetEnvironmentVariable("DOTNET_ROOT"));
    AddWithAncestors(Path.GetDirectoryName(Environment.ProcessPath));
    AddWithAncestors(RuntimeEnvironment.GetRuntimeDirectory());

    foreach (var root in candidateRoots)
    {
        var candidateDirectories = new[]
        {
            root,
            Path.Combine(root, "share", "doc", "dotnet"),
            Path.Combine(root, "..", "share", "doc", "dotnet"),
        };
        foreach (var directory in candidateDirectories)
        {
            var fullDirectory = Path.GetFullPath(directory);
            var license = Path.Combine(fullDirectory, "LICENSE.txt");
            var thirdPartyNotices =
                Path.Combine(fullDirectory, "ThirdPartyNotices.txt");
            if (File.Exists(license) && File.Exists(thirdPartyNotices))
            {
                return new DotnetNotices(license, thirdPartyNotices);
            }
        }
    }

    throw new InvalidOperationException(
        "The active .NET distribution's LICENSE.txt and " +
        "ThirdPartyNotices.txt could not be located. Set DOTNET_ROOT to the " +
        "root of an official .NET installation.");

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

static bool FilesMatch(string expectedPath, string actualPath)
{
    if (!File.Exists(actualPath))
    {
        return false;
    }

    using var expected = File.OpenRead(expectedPath);
    using var actual = File.OpenRead(actualPath);
    if (expected.Length != actual.Length)
    {
        return false;
    }

    return SHA256.HashData(expected).SequenceEqual(SHA256.HashData(actual));
}

static bool SpdxMatches(string spdxPath, IReadOnlyList<PackageLicense> packages)
{
    using var document = JsonDocument.Parse(File.ReadAllText(spdxPath));
    if (!document.RootElement.TryGetProperty("packages", out var packageNodes))
    {
        return false;
    }
    var actual = packageNodes.EnumerateArray()
        .Select(package => string.Join(
            '|',
            package.GetProperty("name").GetString(),
            package.GetProperty("versionInfo").GetString(),
            package.GetProperty("licenseDeclared").GetString()))
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    var expected = packages
        .Select(package =>
            $"{package.Name}|{package.Version}|{package.License}")
        .OrderBy(value => value, StringComparer.Ordinal)
        .ToArray();
    return actual.SequenceEqual(expected, StringComparer.Ordinal);
}

static string EscapeMarkdown(string value) =>
    value.Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

static string SanitizeSpdxId(string value) =>
    new(value.Select(character =>
        char.IsAsciiLetterOrDigit(character) || character is '.' or '-'
            ? character
            : '-').ToArray());

static string NormalizeNewlines(string value) =>
    value.Replace("\r\n", "\n", StringComparison.Ordinal);

internal sealed record PackageLicense(
    string Name,
    string Version,
    bool IsDirect,
    string License,
    string Authors,
    string Copyright,
    string Source,
    string PackageDirectory);

internal sealed record RedistributedFile(string Source, string Destination);

internal sealed record DotnetNotices(
    string License,
    string ThirdPartyNotices);
