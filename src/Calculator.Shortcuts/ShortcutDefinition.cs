using System.Collections.ObjectModel;

namespace Calculator.Shortcuts;

public sealed class ShortcutDefinition
{
    public ShortcutDefinition(
        string id,
        ShortcutGesture defaultGesture,
        IReadOnlyDictionary<ShortcutPlatform, ShortcutGesture>? platformGestures = null,
        string? scope = null,
        int priority = 0,
        bool allowRepeat = false,
        Func<bool>? isEnabled = null,
        ShortcutInputHandling inputHandling = ShortcutInputHandling.Consume,
        IReadOnlyList<ShortcutGesture>? alternateGestures = null,
        IReadOnlyDictionary<ShortcutPlatform, IReadOnlyList<ShortcutGesture>>? platformAlternateGestures = null,
        IReadOnlySet<ShortcutPlatform>? disabledPlatforms = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        Id = id;
        DefaultGesture = defaultGesture;
        Scope = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim();
        Priority = priority;
        AllowRepeat = allowRepeat;
        IsEnabled = isEnabled;
        InputHandling = inputHandling;
        AlternateGestures = Array.AsReadOnly(alternateGestures?.ToArray() ?? []);

        var overrides = platformGestures is null
            ? new Dictionary<ShortcutPlatform, ShortcutGesture>()
            : new Dictionary<ShortcutPlatform, ShortcutGesture>(platformGestures);
        PlatformGestures = new ReadOnlyDictionary<ShortcutPlatform, ShortcutGesture>(overrides);

        var alternateOverrides = new Dictionary<ShortcutPlatform, IReadOnlyList<ShortcutGesture>>();
        if (platformAlternateGestures is not null)
        {
            foreach (var pair in platformAlternateGestures)
            {
                alternateOverrides[pair.Key] = Array.AsReadOnly(pair.Value.ToArray());
            }
        }

        PlatformAlternateGestures =
            new ReadOnlyDictionary<ShortcutPlatform, IReadOnlyList<ShortcutGesture>>(alternateOverrides);
        DisabledPlatforms = new HashSet<ShortcutPlatform>(disabledPlatforms ?? new HashSet<ShortcutPlatform>());
    }

    public string Id { get; }

    public ShortcutGesture DefaultGesture { get; }

    public IReadOnlyDictionary<ShortcutPlatform, ShortcutGesture> PlatformGestures { get; }

    public string? Scope { get; }

    public int Priority { get; }

    public bool AllowRepeat { get; }

    public Func<bool>? IsEnabled { get; }

    public ShortcutInputHandling InputHandling { get; }

    public IReadOnlyList<ShortcutGesture> AlternateGestures { get; }

    public IReadOnlyDictionary<ShortcutPlatform, IReadOnlyList<ShortcutGesture>> PlatformAlternateGestures { get; }

    public IReadOnlySet<ShortcutPlatform> DisabledPlatforms { get; }

    public ShortcutGesture ResolveGesture(ShortcutPlatform platform)
    {
        if (DisabledPlatforms.Contains(platform))
        {
            throw new InvalidOperationException($"The shortcut '{Id}' is disabled on {platform}.");
        }

        return PlatformGestures.TryGetValue(platform, out var gesture) ? gesture : DefaultGesture;
    }

    public IReadOnlyList<ShortcutGesture> ResolveGestures(ShortcutPlatform platform)
    {
        if (DisabledPlatforms.Contains(platform))
        {
            return [];
        }

        var primary = ResolveGesture(platform);
        var alternates = PlatformAlternateGestures.TryGetValue(platform, out var platformAlternates)
            ? platformAlternates
            : AlternateGestures;
        if (alternates.Count == 0)
        {
            return [primary];
        }

        var gestures = new List<ShortcutGesture>(alternates.Count + 1) { primary };
        gestures.AddRange(alternates.Where(item => item != primary));
        return gestures.AsReadOnly();
    }

    internal bool IsCurrentlyEnabled() => IsEnabled?.Invoke() ?? true;
}
