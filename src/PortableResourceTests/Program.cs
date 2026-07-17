using System.Globalization;
using Windows.ApplicationModel.Resources;

var resourceRoot = args.Length == 1
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Calculator/Resources"));

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

ResourceLoader.Configure(new ResourceLoaderConfiguration(resourceRoot)
{
    UICultureProvider = static () => CultureInfo.GetCultureInfo("de-DE"),
});

var resources = ResourceLoader.GetForViewIndependentUse();
var engineResources = ResourceLoader.GetForViewIndependentUse("CEngineStrings");

Require(resources.GetString("AppName") == "Rechner", "Exact-culture resource lookup failed.");
Require(resources.GetString("MemoryLabel/Text") == resources.GetString("MemoryLabel.Text"), "Uid/property key normalization failed.");
Require(resources.GetUidProperties("MemoryLabel")["Text"] == resources.GetString("MemoryLabel.Text"), "Uid property projection failed.");
Require(engineResources.GetAllStrings().Count > 100, "Named CEngineStrings map was not loaded.");
Require(resources.GetAllStrings().Count > 500, "Default Resources map was not loaded.");
Require(resources.GetString("ThisKeyDoesNotExist") == string.Empty, "Missing-resource behavior is not UWP-compatible.");
Require(ResourceLoader.GetForViewIndependentUse("MissingMap").GetString("MissingKey") == string.Empty,
    "Missing-map behavior is not UWP-compatible.");

var firstEngineEntry = engineResources.GetAllStrings().First();
Require(resources.GetString($"/CEngineStrings/{firstEngineEntry.Key}") == firstEngineEntry.Value, "Absolute named-map lookup failed.");
Require(resources.GetString($"ms-resource:///CEngineStrings/{firstEngineEntry.Key}") == firstEngineEntry.Value, "ms-resource URI lookup failed.");

ResourceLoader.Configure(new ResourceLoaderConfiguration(resourceRoot)
{
    UICultureProvider = static () => CultureInfo.GetCultureInfo("de-AT"),
});
Require(ResourceLoader.GetForCurrentView().GetString("AppName") == "Rechner", "Same-language fallback failed.");

ResourceLoader.Configure(new ResourceLoaderConfiguration(resourceRoot)
{
    UICultureProvider = static () => CultureInfo.GetCultureInfo("zu-ZA"),
});
Require(ResourceLoader.GetForCurrentView().GetString("AppName") == "Calculator", "Default-culture fallback failed.");

foreach (var cultureDirectory in Directory.EnumerateDirectories(resourceRoot))
{
    var cultureName = Path.GetFileName(cultureDirectory);
    ResourceLoader.Configure(new ResourceLoaderConfiguration(resourceRoot)
    {
        UICultureProvider = () => CultureInfo.GetCultureInfo(cultureName),
    });
    Require(ResourceLoader.GetForViewIndependentUse().GetAllStrings().Count > 500,
        $"Resources.resw failed to load for {cultureName}.");
    Require(ResourceLoader.GetForViewIndependentUse("CEngineStrings").GetAllStrings().Count > 100,
        $"CEngineStrings.resw failed to load for {cultureName}.");
}

Console.WriteLine("ResourceLoader portable compatibility tests passed.");
