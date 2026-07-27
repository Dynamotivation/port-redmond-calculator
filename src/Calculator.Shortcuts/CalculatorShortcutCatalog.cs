using System.Reflection;
using System.Text.Json;
using Redmond.Shortcuts;

namespace Calculator.Shortcuts;

public enum ShortcutCatalogSource
{
    UwpResource,
    UwpNavigation,
    HardCodedControl,
}

public sealed record ShortcutCatalogBinding(
    string ShortcutId,
    ShortcutGesture Gesture,
    string Scope,
    ShortcutCatalogSource Source,
    string Description,
    ShortcutPlatform? Platform = null);

public sealed class ShortcutCatalogException : Exception
{
    public ShortcutCatalogException(IEnumerable<string> diagnostics)
        : base("The shortcut definition is invalid: " + string.Join("; ", diagnostics))
    {
        Diagnostics = diagnostics.ToArray();
    }

    public IReadOnlyList<string> Diagnostics { get; }
}

public sealed class ShortcutCatalog
{
    internal ShortcutCatalog(
        IReadOnlyList<ShortcutCatalogBinding> bindings,
        IReadOnlyList<ShortcutDefinition> definitions)
    {
        Bindings = bindings;
        Definitions = definitions;
    }

    public IReadOnlyList<ShortcutCatalogBinding> Bindings { get; }

    public IReadOnlyList<ShortcutDefinition> Definitions { get; }

    public IReadOnlyList<IDisposable> RegisterAll(IShortcutService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        return Definitions.Select(definition => service.Register(definition)).ToArray();
    }
}

public static class ShortcutCatalogLoader
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static ShortcutCatalog Load(Stream definition, Stream? remap = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var document = Deserialize(definition, "definition");
        if (remap is not null)
        {
            Merge(document, Deserialize(remap, "remap"));
        }

        return Materialize(document);
    }

    public static ShortcutCatalog LoadBuiltIn(Stream? remap = null)
    {
        using var definition = Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Calculator.Shortcuts.CalculatorShortcuts.json") ??
            throw new InvalidOperationException("The embedded shortcut definition is missing.");
        return Load(definition, remap);
    }

    private static CatalogDocument Deserialize(Stream stream, string label)
    {
        try
        {
            return JsonSerializer.Deserialize<CatalogDocument>(stream, JsonOptions) ??
                throw new ShortcutCatalogException([$"The {label} is empty."]);
        }
        catch (JsonException exception)
        {
            throw new ShortcutCatalogException([$"The {label} contains invalid JSON: {exception.Message}"]);
        }
    }

    private static void Merge(CatalogDocument definition, CatalogDocument remap)
    {
        var diagnostics = new List<string>();
        if (remap.SchemaVersion != CurrentSchemaVersion)
        {
            diagnostics.Add($"The remap schemaVersion must be {CurrentSchemaVersion}.");
        }

        var definitions = (definition.Shortcuts ?? []).ToDictionary(item => item.Id ?? string.Empty, StringComparer.Ordinal);
        foreach (var replacement in remap.Shortcuts ?? [])
        {
            if (string.IsNullOrWhiteSpace(replacement.Id) || !definitions.TryGetValue(replacement.Id, out var target))
            {
                diagnostics.Add($"The remap references unknown shortcut '{replacement.Id ?? "<missing>"}'.");
                continue;
            }

            if (replacement.Bindings is null || replacement.Bindings.Count == 0)
            {
                diagnostics.Add($"The remap for '{replacement.Id}' has no bindings to replace.");
                continue;
            }

            target.Bindings ??= new Dictionary<string, List<BindingDocument>>(StringComparer.OrdinalIgnoreCase);
            foreach (var platform in replacement.Bindings)
            {
                target.Bindings[platform.Key] = platform.Value;
            }
        }

        if (diagnostics.Count > 0)
        {
            throw new ShortcutCatalogException(diagnostics);
        }
    }

    private static ShortcutCatalog Materialize(CatalogDocument document)
    {
        var diagnostics = new List<string>();
        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            diagnostics.Add($"schemaVersion must be {CurrentSchemaVersion}.");
        }

        var entries = document.Shortcuts ?? [];
        var duplicateIds = entries
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        diagnostics.AddRange(duplicateIds.Select(id => $"Shortcut id '{id}' is duplicated."));

        var bindings = new List<ShortcutCatalogBinding>();
        var definitions = new List<ShortcutDefinition>();
        foreach (var entry in entries)
        {
            MaterializeEntry(entry, bindings, definitions, diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            throw new ShortcutCatalogException(diagnostics);
        }

        return new ShortcutCatalog(bindings.AsReadOnly(), definitions.AsReadOnly());
    }

    private static void MaterializeEntry(
        ShortcutDocument entry,
        ICollection<ShortcutCatalogBinding> catalogBindings,
        ICollection<ShortcutDefinition> definitions,
        ICollection<string> diagnostics)
    {
        var id = entry.Id?.Trim();
        if (string.IsNullOrWhiteSpace(id))
        {
            diagnostics.Add("A shortcut is missing its id.");
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.Scope) ||
            !Enum.TryParse<ShortcutCatalogSource>(entry.Source, true, out var source) ||
            entry.Bindings is null || !entry.Bindings.TryGetValue("default", out var defaults) || defaults.Count == 0)
        {
            diagnostics.Add($"Shortcut '{id}' requires scope, source, and at least one default binding.");
            return;
        }

        if (!Enum.TryParse<ShortcutInputHandling>(entry.InputHandling ?? "Consume", true, out var inputHandling))
        {
            diagnostics.Add($"Shortcut '{id}' has invalid inputHandling '{entry.InputHandling}'.");
            return;
        }

        var defaultGestures = ParseGestures(id, "default", defaults, diagnostics);
        if (defaultGestures.Count == 0)
        {
            return;
        }

        var platformPrimary = new Dictionary<ShortcutPlatform, ShortcutGesture>();
        var platformAlternates = new Dictionary<ShortcutPlatform, IReadOnlyList<ShortcutGesture>>();
        var disabledPlatforms = new HashSet<ShortcutPlatform>();
        AddCatalogBindings(defaultGestures, null);

        foreach (var pair in entry.Bindings.Where(pair => !pair.Key.Equals("default", StringComparison.OrdinalIgnoreCase)))
        {
            if (!Enum.TryParse<ShortcutPlatform>(pair.Key, true, out var platform) || platform == ShortcutPlatform.Unknown)
            {
                diagnostics.Add($"Shortcut '{id}' has unknown platform '{pair.Key}'.");
                continue;
            }

            if (pair.Value.Count == 0)
            {
                disabledPlatforms.Add(platform);
                continue;
            }

            var gestures = ParseGestures(id, pair.Key, pair.Value, diagnostics);
            if (gestures.Count == 0)
            {
                continue;
            }

            platformPrimary[platform] = gestures[0];
            platformAlternates[platform] = gestures.Skip(1).ToArray();
            AddCatalogBindings(gestures, platform);
        }

        definitions.Add(new ShortcutDefinition(
            id,
            defaultGestures[0],
            platformPrimary,
            entry.Scope,
            inputHandling: inputHandling,
            alternateGestures: defaultGestures.Skip(1).ToArray(),
            platformAlternateGestures: platformAlternates,
            disabledPlatforms: disabledPlatforms));

        void AddCatalogBindings(IEnumerable<ShortcutGesture> gestures, ShortcutPlatform? platform)
        {
            foreach (var gesture in gestures)
            {
                catalogBindings.Add(new ShortcutCatalogBinding(
                    id,
                    gesture,
                    entry.Scope!,
                    source,
                    entry.Description ?? id,
                    platform));
            }
        }
    }

    private static List<ShortcutGesture> ParseGestures(
        string id,
        string platform,
        IEnumerable<BindingDocument> bindings,
        ICollection<string> diagnostics)
    {
        var gestures = new List<ShortcutGesture>();
        foreach (var binding in bindings)
        {
            if (binding.Key is null || string.IsNullOrWhiteSpace(binding.Key.Value) ||
                !Enum.TryParse<ShortcutKeyKind>(binding.Key.Kind, true, out var kind))
            {
                diagnostics.Add($"Shortcut '{id}' has an invalid {platform} key.");
                continue;
            }

            if (kind == ShortcutKeyKind.Character && binding.Key.Value.Length != 1)
            {
                diagnostics.Add($"Shortcut '{id}' character keys must contain exactly one character.");
                continue;
            }

            var modifiers = ShortcutModifiers.None;
            var invalidModifier = false;
            foreach (var modifierName in binding.Modifiers ?? [])
            {
                if (!Enum.TryParse<ShortcutModifiers>(modifierName, true, out var modifier) || modifier == ShortcutModifiers.None)
                {
                    diagnostics.Add($"Shortcut '{id}' has unknown modifier '{modifierName}'.");
                    invalidModifier = true;
                }
                else
                {
                    modifiers |= modifier;
                }
            }

            if (!invalidModifier)
            {
                var key = kind == ShortcutKeyKind.Character
                    ? ShortcutKey.Character(binding.Key.Value[0])
                    : ShortcutKey.Named(binding.Key.Value);
                gestures.Add(new ShortcutGesture(key, modifiers));
            }
        }

        return gestures.Distinct().ToList();
    }

    private sealed class CatalogDocument
    {
        public int SchemaVersion { get; set; }
        public List<ShortcutDocument>? Shortcuts { get; set; }
    }

    private sealed class ShortcutDocument
    {
        public string? Id { get; set; }
        public string? Scope { get; set; }
        public string? Source { get; set; }
        public string? Description { get; set; }
        public string? InputHandling { get; set; }
        public Dictionary<string, List<BindingDocument>>? Bindings { get; set; }
    }

    private sealed class BindingDocument
    {
        public KeyDocument? Key { get; set; }
        public List<string>? Modifiers { get; set; }
    }

    private sealed class KeyDocument
    {
        public string? Kind { get; set; }
        public string? Value { get; set; }
    }
}

public static class CalculatorShortcutCatalog
{
    public const string CalculatorScope = "calculator";
    public const string ScientificScope = "scientific";
    public const string ProgrammerScope = "programmer";
    public const string ConverterScope = "converter";
    public const string GraphingScope = "graphing";
    public const string NavigationScope = "navigation";

    private static readonly Lazy<ShortcutCatalog> BuiltIn = new(() => ShortcutCatalogLoader.LoadBuiltIn());

    public static IReadOnlyList<ShortcutCatalogBinding> Bindings => BuiltIn.Value.Bindings;

    public static IReadOnlyList<ShortcutDefinition> CreateDefinitions() => BuiltIn.Value.Definitions;

    public static ShortcutCatalog LoadRemap(Stream remap) => ShortcutCatalogLoader.LoadBuiltIn(remap);

    public static IReadOnlyList<IDisposable> RegisterAll(IShortcutService service) => BuiltIn.Value.RegisterAll(service);
}
