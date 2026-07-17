using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Windows.ApplicationModel.Resources;

/// <summary>
/// Cross-platform implementation of the ResourceLoader subset used by
/// Microsoft Calculator. It consumes the repository's .resw files directly.
/// </summary>
public sealed class ResourceLoader
{
    private const string DefaultMapName = "Resources";
    private static readonly object ConfigurationLock = new();
    private static ResourceCatalog? catalog;

    private readonly ResourceCatalog resourceCatalog;
    private readonly string mapName;

    private ResourceLoader(ResourceCatalog resourceCatalog, string mapName)
    {
        this.resourceCatalog = resourceCatalog;
        this.mapName = NormalizeMapName(mapName);
    }

    /// <summary>
    /// Configures the process-wide resource root used by the UWP-compatible
    /// static factory methods. Calling this again atomically replaces it.
    /// </summary>
    public static void Configure(ResourceLoaderConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var replacement = new ResourceCatalog(configuration);
        lock (ConfigurationLock)
        {
            catalog = replacement;
        }
    }

    public static ResourceLoader GetForCurrentView() => GetForViewIndependentUse();

    public static ResourceLoader GetForCurrentView(string resourceMap) => GetForViewIndependentUse(resourceMap);

    public static ResourceLoader GetForViewIndependentUse() => new(GetCatalog(), DefaultMapName);

    public static ResourceLoader GetForViewIndependentUse(string resourceMap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceMap);
        return new ResourceLoader(GetCatalog(), resourceMap);
    }

    public string GetString(string resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var reference = ResourceReference.Parse(mapName, resource);
        return resourceCatalog.TryGetString(reference.MapName, reference.Key, out var value) ? value : string.Empty;
    }

    /// <summary>
    /// Returns the effective map after culture fallback. This portable
    /// extension is used to provision native Calculator resource providers.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAllStrings() => resourceCatalog.GetAllStrings(mapName);

    /// <summary>
    /// Returns UWP x:Uid entries such as Foo.Text as property/value pairs.
    /// Attached-property names are preserved verbatim.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetUidProperties(string uid)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uid);
        var prefix = uid + ".";
        var properties = GetAllStrings()
            .Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .ToDictionary(entry => entry.Key[prefix.Length..], entry => entry.Value, StringComparer.Ordinal);
        return new ReadOnlyDictionary<string, string>(properties);
    }

    private static ResourceCatalog GetCatalog()
    {
        lock (ConfigurationLock)
        {
            return catalog ?? throw new InvalidOperationException(
                "ResourceLoader.Configure must be called before requesting a resource loader.");
        }
    }

    private static string NormalizeMapName(string mapName)
    {
        var normalized = mapName.Replace('\\', '/').Trim('/');
        var separator = normalized.LastIndexOf('/');
        if (separator >= 0)
        {
            normalized = normalized[(separator + 1)..];
        }
        if (normalized.EndsWith(".resw", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[..^5];
        }
        if (normalized.Length == 0 || normalized.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid resource map name.", nameof(mapName));
        }
        return normalized;
    }

    private readonly record struct ResourceReference(string MapName, string Key)
    {
        public static ResourceReference Parse(string currentMap, string resource)
        {
            var normalized = resource.Trim();
            const string uriPrefix = "ms-resource:///";
            if (normalized.StartsWith(uriPrefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = "/" + normalized[uriPrefix.Length..];
            }

            var selectedMap = currentMap;
            if (normalized.StartsWith('/'))
            {
                var mapSeparator = normalized.IndexOf('/', 1);
                if (mapSeparator > 1)
                {
                    selectedMap = NormalizeMapName(normalized[1..mapSeparator]);
                    normalized = normalized[(mapSeparator + 1)..];
                }
                else
                {
                    normalized = normalized.TrimStart('/');
                }
            }

            // UWP permits property lookup as Uid/Property while .resw stores
            // the same entry as Uid.Property.
            var propertySeparator = normalized.LastIndexOf('/');
            if (propertySeparator > 0)
            {
                normalized = normalized[..propertySeparator] + "." + normalized[(propertySeparator + 1)..];
            }
            return new ResourceReference(NormalizeMapName(selectedMap), normalized);
        }
    }

    private sealed class ResourceCatalog
    {
        private readonly string root;
        private readonly string defaultCulture;
        private readonly Func<CultureInfo> cultureProvider;
        private readonly IReadOnlyList<string> availableCultures;
        private readonly ConcurrentDictionary<(string Culture, string Map), MapCacheEntry> maps = new();

        public ResourceCatalog(ResourceLoaderConfiguration configuration)
        {
            if (!Directory.Exists(configuration.ResourceRoot))
            {
                throw new DirectoryNotFoundException($"Resource root does not exist: {configuration.ResourceRoot}");
            }
            ArgumentException.ThrowIfNullOrWhiteSpace(configuration.DefaultCultureName);
            ArgumentNullException.ThrowIfNull(configuration.UICultureProvider);

            root = configuration.ResourceRoot;
            defaultCulture = CultureInfo.GetCultureInfo(configuration.DefaultCultureName).Name;
            cultureProvider = configuration.UICultureProvider;
            availableCultures = Directory.EnumerateDirectories(root)
                .Select(Path.GetFileName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => CultureInfo.GetCultureInfo(name!).Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (!availableCultures.Contains(defaultCulture, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Default resource culture '{defaultCulture}' does not exist under {root}.");
            }
        }

        public bool TryGetString(string map, string key, out string value)
        {
            foreach (var culture in GetFallbackCultures())
            {
                var values = GetMap(culture, map);
                if (values is not null && values.TryGetValue(key, out value!))
                {
                    return true;
                }
            }
            value = string.Empty;
            return false;
        }

        public IReadOnlyDictionary<string, string> GetAllStrings(string map)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var culture in GetFallbackCultures().Reverse())
            {
                var values = GetMap(culture, map);
                if (values is null)
                {
                    continue;
                }
                foreach (var entry in values)
                {
                    result[entry.Key] = entry.Value;
                }
            }
            return new ReadOnlyDictionary<string, string>(result);
        }

        private IReadOnlyList<string> GetFallbackCultures()
        {
            var requested = cultureProvider() ?? throw new InvalidOperationException("UICultureProvider returned null.");
            var result = new List<string>();

            for (var candidate = requested; !candidate.Equals(CultureInfo.InvariantCulture); candidate = candidate.Parent)
            {
                AddIfAvailable(result, candidate.Name);
            }

            if (requested.Name.Length != 0)
            {
                foreach (var sibling in availableCultures.Where(name =>
                             string.Equals(CultureInfo.GetCultureInfo(name).TwoLetterISOLanguageName,
                                 requested.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase)))
                {
                    AddIfAvailable(result, sibling);
                }
            }

            var defaultInfo = CultureInfo.GetCultureInfo(defaultCulture);
            for (var candidate = defaultInfo; !candidate.Equals(CultureInfo.InvariantCulture); candidate = candidate.Parent)
            {
                AddIfAvailable(result, candidate.Name);
            }
            return result;
        }

        private void AddIfAvailable(List<string> result, string culture)
        {
            var actual = availableCultures.FirstOrDefault(value => string.Equals(value, culture, StringComparison.OrdinalIgnoreCase));
            if (actual is not null && !result.Contains(actual, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(actual);
            }
        }

        private IReadOnlyDictionary<string, string>? GetMap(string culture, string map) =>
            maps.GetOrAdd((culture, map), key => new MapCacheEntry(LoadMap(key.Culture, key.Map))).Values;

        private IReadOnlyDictionary<string, string>? LoadMap(string culture, string map)
        {
            var path = Path.Combine(root, culture, map + ".resw");
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            using var reader = XmlReader.Create(stream, new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
            });
            var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var data in document.Root?.Elements("data") ?? [])
            {
                var name = (string?)data.Attribute("name");
                if (string.IsNullOrEmpty(name))
                {
                    throw new InvalidDataException($"A resource in {path} has no name.");
                }
                if (!values.TryAdd(name, (string?)data.Element("value") ?? string.Empty))
                {
                    throw new InvalidDataException($"Duplicate resource key '{name}' in {path}.");
                }
            }
            return new ReadOnlyDictionary<string, string>(values);
        }

        private sealed record MapCacheEntry(IReadOnlyDictionary<string, string>? Values);
    }
}
