using Calculator.Shortcuts;
using Redmond.Shortcuts;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("Primary modifier resolution", PrimaryModifierResolution),
    ("handler and event notification", HandlerAndEventNotification),
    ("scope and priority dispatch", ScopeAndPriorityDispatch),
    ("repeat filtering", RepeatFiltering),
    ("registration disposal", RegistrationDisposal),
    ("conflict detection", ConflictDetection),
    ("macOS display symbols", MacDisplaySymbols),
    ("localized modifier vocabulary", LocalizedModifierVocabulary),
    ("template formatting", TemplateFormatting),
    ("legacy suffix rewriting", LegacySuffixRewriting),
    ("catalog completeness", CatalogCompleteness),
    ("built-in macOS bindings and text", BuiltInMacBindingsAndText),
    ("alternate gesture dispatch", AlternateGestureDispatch),
    ("input handling result", InputHandlingResult),
    ("platform convention validation", PlatformConventionValidation),
    ("alternate gesture display text", AlternateGestureDisplayText),
    ("JSON remap applies to every platform", JsonRemapAppliesToEveryPlatform),
    ("JSON platform-specific bindings are rejected", JsonPlatformBindingsRejected),
    ("JSON remap validation", JsonRemapValidation),
    ("macOS Control modifier preference", MacControlModifierPreference),
    ("macOS modifier preference leaves other platforms unchanged", MacModifierPreferenceLeavesOtherPlatforms),
    ("macOS modifier preference validation", MacModifierPreferenceValidation),
    ("no platform-specific gestures", NoPlatformSpecificGestures),
    ("no macOS collisions", NoMacOSCollisions),
    ("localized labels", LocalizedLabels),
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

return;

static IShortcutService Service(
    ShortcutPlatform platform,
    MacPrimaryModifier macModifier = MacPrimaryModifier.Command,
    string culture = "en-US") =>
    ShortcutServices.Create(platform, new ShortcutServiceOptions
    {
        MacPrimaryModifier = macModifier,
        LabelCulture = System.Globalization.CultureInfo.GetCultureInfo(culture),
    });

static void PrimaryModifierResolution()
{
    var definition = new ShortcutDefinition("history.open", ShortcutGesture.Parse("Primary+H"));
    var mac = Service(ShortcutPlatform.MacOS);
    mac.Register(definition);
    Require(mac.IsMatch("history.open", new ShortcutInput(Named("H", ShortcutModifiers.Command))),
        "Primary did not resolve to Command on macOS.");
    Require(!mac.IsMatch("history.open", new ShortcutInput(Named("H", ShortcutModifiers.Control))),
        "Control unexpectedly matched Primary on macOS.");

    var windows = Service(ShortcutPlatform.Windows);
    windows.Register(definition);
    Require(windows.IsMatch("history.open", new ShortcutInput(Named("H", ShortcutModifiers.Control))),
        "Primary did not resolve to Control on Windows.");
}

static void HandlerAndEventNotification()
{
    var service = Service(ShortcutPlatform.Windows);
    var handlerCalls = 0;
    var eventCalls = 0;
    service.ShortcutPressed += (_, args) =>
    {
        Require(args.Match.ShortcutId == "copy", "The event reported the wrong shortcut.");
        eventCalls++;
    };
    service.Register(
        new ShortcutDefinition("copy", Gesture('C', ShortcutModifiers.Control)),
        _ => handlerCalls++);

    var matches = service.Process(new ShortcutInput(Gesture('C', ShortcutModifiers.Control)));
    Require(matches.Count == 1 && handlerCalls == 1 && eventCalls == 1,
        "Shortcut processing did not notify all consumers once.");
}

static void ScopeAndPriorityDispatch()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("global", Gesture('K'), priority: 0));
    service.Register(new ShortcutDefinition("scientific", Gesture('K'), scope: "scientific", priority: 10));

    var matches = service.Process(
        new ShortcutInput(Gesture('K')),
        new HashSet<string>(StringComparer.Ordinal) { "scientific" });
    Require(matches.Count == 1 && matches[0].ShortcutId == "scientific",
        "The highest-priority active scope did not win FirstMatch dispatch.");
}

static void RepeatFiltering()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("once", Gesture('A')));
    service.Register(new ShortcutDefinition("repeat", Gesture('B'), allowRepeat: true));

    Require(service.Process(new ShortcutInput(Gesture('A'), IsRepeat: true)).Count == 0,
        "A non-repeatable shortcut accepted a repeated input.");
    Require(service.Process(new ShortcutInput(Gesture('B'), IsRepeat: true)).Count == 1,
        "A repeatable shortcut rejected a repeated input.");
}

static void RegistrationDisposal()
{
    var service = Service(ShortcutPlatform.Windows);
    var registration = service.Register(new ShortcutDefinition("temporary", Gesture('T')));
    registration.Dispose();
    Require(service.Process(new ShortcutInput(Gesture('T'))).Count == 0,
        "Disposing a registration did not remove it.");
}

static void ConflictDetection()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("first", Gesture('X'), scope: "standard"));
    service.Register(new ShortcutDefinition("second", Gesture('X'), scope: "standard"));
    service.Register(new ShortcutDefinition("separate", Gesture('X'), scope: "programmer"));

    var conflicts = service.GetConflicts();
    Require(conflicts.Count == 1 && conflicts[0].FirstShortcutId == "first" && conflicts[0].SecondShortcutId == "second",
        "Conflict detection did not respect shortcut scopes.");
}

static void MacDisplaySymbols()
{
    var service = Service(ShortcutPlatform.MacOS);
    service.Register(new ShortcutDefinition("find", ShortcutGesture.Parse("Primary+Shift+F")));

    Require(service.GetGestureDisplayText("find") == "⇧⌘F", "The macOS symbolic display text was incorrect.");
}

static void LocalizedModifierVocabulary()
{
    var displayOptions = new ShortcutDisplayOptions(
        ShortcutDisplayStyle.Text,
        modifierNames: new Dictionary<ShortcutModifiers, string>
        {
            [ShortcutModifiers.Control] = "Strg",
            [ShortcutModifiers.Shift] = "Umschalt",
        });
    var service = ShortcutServices.Create(
        ShortcutPlatform.Windows,
        new ShortcutServiceOptions { DisplayOptions = displayOptions });
    service.Register(new ShortcutDefinition(
        "localized",
        Gesture('L', ShortcutModifiers.Control | ShortcutModifiers.Shift)));

    Require(service.GetGestureDisplayText("localized") == "Strg+Umschalt+L",
        "The localized modifier vocabulary was not used.");
}

static void TemplateFormatting()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("copy", Gesture('C', ShortcutModifiers.Control)));
    Require(service.FormatText("copy", "Copy ({shortcut})") == "Copy (Ctrl+C)",
        "The shortcut placeholder was not replaced.");
}

static void LegacySuffixRewriting()
{
    var service = Service(ShortcutPlatform.MacOS);
    service.Register(new ShortcutDefinition("copy", Gesture('C', ShortcutModifiers.Command)));

    Require(
        service.RewriteText("copy", "Copy (Ctrl+C)", ShortcutTextRewriteMode.ReplaceTrailingParenthetical) == "Copy (⌘C)",
        "An ASCII legacy shortcut suffix was not replaced.");
    Require(
        service.RewriteText("copy", "コピー（Ctrl+C）", ShortcutTextRewriteMode.ReplaceTrailingParenthetical) == "コピー（⌘C）",
        "A fullwidth legacy shortcut suffix was not replaced.");
    Require(
        service.RewriteText(
            "copy",
            "Zoom in (Ctrl + plus (+) key)",
            ShortcutTextRewriteMode.ReplaceTrailingParenthetical) == "Zoom in (⌘C)",
        "A nested legacy shortcut suffix was not replaced.");
    Require(
        service.RewriteText("copy", "Copy", ShortcutTextRewriteMode.ReplaceOrAppend) == "Copy (⌘C)",
        "A missing shortcut suffix was not appended when requested.");
    Require(
        service.RewriteText("copy", "Copy", ShortcutTextRewriteMode.TemplateOnly) == "Copy",
        "Template-only mode unexpectedly changed unmarked localized text.");
}

static void CatalogCompleteness()
{
    Require(
        CalculatorShortcutCatalog.Bindings.Count(item =>
            item.Source == ShortcutCatalogSource.UwpResource) == 129,
        "The catalog does not contain all 129 resource-defined bindings from the matrix.");
    Require(
        CalculatorShortcutCatalog.Bindings.Count(item =>
            item.Source == ShortcutCatalogSource.UwpNavigation) == 13,
        "The catalog does not contain all 13 navigation accelerators from the matrix.");
    Require(
        CalculatorShortcutCatalog.Bindings.Count(item =>
            item.Source == ShortcutCatalogSource.HardCodedControl) == 20,
        "The hard-coded matrix behaviors did not expand to all 20 concrete gestures.");

    var service = Service(ShortcutPlatform.Windows);
    var registrations = CalculatorShortcutCatalog.RegisterAll(service);
    Require(registrations.Count == CalculatorShortcutCatalog.CreateDefinitions().Count,
        "The complete catalog could not be registered through the central service.");
    foreach (var registration in registrations)
    {
        registration.Dispose();
    }
}

static void BuiltInMacBindingsAndText()
{
    var mac = Service(ShortcutPlatform.MacOS);
    var registrations = CalculatorShortcutCatalog.RegisterAll(mac);
    try
    {
        var calculatorScope = new HashSet<string>(StringComparer.Ordinal) { "calculator" };
        Require(
            mac.IsMatch(
                "copyButton",
                new ShortcutInput(new ShortcutGesture(
                    ShortcutKey.Named("C"),
                    ShortcutModifiers.Command)),
                calculatorScope),
            "The built-in copy shortcut did not use Command on macOS.");
        Require(
            !mac.IsMatch(
                "copyButton",
                new ShortcutInput(new ShortcutGesture(
                    ShortcutKey.Named("C"),
                    ShortcutModifiers.Control)),
                calculatorScope),
            "The Windows copy gesture unexpectedly remained active on macOS.");
        Require(
            mac.GetGestureDisplayText("graph.view.reset") == "⌘0",
            "The graph reset display text did not resolve Primary to Command.");
        Require(
            mac.RewriteText(
                "graph.view.reset",
                "Refresh view automatically (Ctrl + 0)",
                ShortcutTextRewriteMode.ReplaceOrAppend) ==
            "Refresh view automatically (⌘0)",
            "The graph reset tooltip did not use the shared text rewriter.");
        Require(
            mac.GetGestureDisplayText("graph.zoom.in") == "⌘+",
            "The graph zoom-in display text did not use Command on macOS.");
        Require(
            mac.GetGestureDisplayText("HistoryButton") == "⇧⌘H",
            "History did not use Primary+Shift+H on macOS.");
        Require(
            mac.TryGetDefinition("copyButtonAlternate", out var copyAlternate)
            && copyAlternate!.ResolveGestures(ShortcutPlatform.MacOS).Count == 0,
            "The unavailable Insert-based copy binding remained active on macOS.");
    }
    finally
    {
        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }

    var windows = Service(ShortcutPlatform.Windows);
    var windowsRegistrations = CalculatorShortcutCatalog.RegisterAll(windows);
    try
    {
        Require(
            windows.GetGestureDisplayText("graph.view.reset") == "Ctrl+0",
            "The graph reset binding did not preserve the Windows shortcut.");
    }
    finally
    {
        foreach (var registration in windowsRegistrations)
        {
            registration.Dispose();
        }
    }
}

static void AlternateGestureDispatch()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition(
        "equals",
        Gesture('='),
        alternateGestures: [new ShortcutGesture(ShortcutKey.Named("Enter"))]));

    var enter = new ShortcutGesture(ShortcutKey.Named("Enter"));
    var result = service.Process(new ShortcutInput(enter));
    Require(result.Count == 1 && result[0].Gesture == enter,
        "An alternate gesture did not match or was not reported as the pressed gesture.");
}

static void InputHandlingResult()
{
    var observe = Service(ShortcutPlatform.Windows);
    observe.Register(new ShortcutDefinition(
        "observer",
        Gesture('O'),
        inputHandling: ShortcutInputHandling.ObserveOnly));
    var observed = observe.Process(new ShortcutInput(Gesture('O')));
    Require(observed.WasMatched && !observed.Handled && !observed.ShouldConsume,
        "Observe-only input reported incorrect handling state.");

    var handle = Service(ShortcutPlatform.Windows);
    handle.Register(new ShortcutDefinition(
        "handler",
        Gesture('H'),
        inputHandling: ShortcutInputHandling.Handle));
    var handled = handle.Process(new ShortcutInput(Gesture('H')));
    Require(handled.WasMatched && handled.Handled && !handled.ShouldConsume,
        "Handled but non-consuming input reported incorrect state.");

    var consume = Service(ShortcutPlatform.Windows);
    consume.Register(new ShortcutDefinition("consumer", Gesture('C')));
    var consumed = consume.Process(new ShortcutInput(Gesture('C')));
    Require(consumed.WasMatched && consumed.Handled && consumed.ShouldConsume,
        "Consuming input reported incorrect state.");
}

static void PlatformConventionValidation()
{
    var service = Service(ShortcutPlatform.MacOS);
    service.Register(new ShortcutDefinition("legacy.copy", Gesture('C', ShortcutModifiers.Control)));
    service.Register(new ShortcutDefinition(
        "alwaysOnTop",
        new ShortcutGesture(ShortcutKey.Named("Up"), ShortcutModifiers.Alt)));
    service.Register(new ShortcutDefinition(
        "copyAlternate",
        new ShortcutGesture(ShortcutKey.Named("Insert"), ShortcutModifiers.Control)));
    service.Register(new ShortcutDefinition(
        "mode.standard",
        new ShortcutGesture(ShortcutKey.Named("1"), ShortcutModifiers.Alt)));

    var issues = service.ValidateConventions();
    Require(issues.Any(item => item.Kind == ShortcutConventionIssueKind.NonIdiomaticModifier),
        "macOS validation did not flag a Control-based application shortcut.");
    Require(issues.Any(item => item.Kind == ShortcutConventionIssueKind.CommonApplicationCollision),
        "macOS validation did not flag Option+Up text-navigation behavior.");
    Require(issues.Any(item => item.Kind == ShortcutConventionIssueKind.UnavailableKey),
        "macOS validation did not flag the unavailable Insert key.");
    Require(issues.Any(item =>
            item.ShortcutId == "mode.standard" && item.Kind == ShortcutConventionIssueKind.LayoutDependentInput),
        "macOS validation did not flag Option+number as layout-dependent.");
}

static void AlternateGestureDisplayText()
{
    var service = Service(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition(
        "equals",
        Gesture('='),
        alternateGestures: [new ShortcutGesture(ShortcutKey.Named("Enter"))]));

    Require(service.GetGestureDisplayTexts("equals").SequenceEqual(["=", "Enter"]),
        "The display API did not return all alternate gestures.");
    Require(service.FormatText("equals", "Equals ({shortcuts})") == "Equals (= / Enter)",
        "The plural shortcut placeholder did not format all gestures.");
}

static void JsonRemapAppliesToEveryPlatform()
{
    using var remap = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            {
              "id": "HistoryButton",
              "bindings": {
                "default": [
                  {
                    "key": { "kind": "named", "value": "Y" },
                    "modifiers": ["primary", "shift"]
                  }
                ]
              }
            }
          ]
        }
        """);
    var catalog = CalculatorShortcutCatalog.LoadRemap(remap);
    var history = catalog.Definitions.Single(item => item.Id == "HistoryButton");

    Require(history.ResolveGesture(ShortcutPlatform.MacOS) ==
            Named("Y", ShortcutModifiers.Command | ShortcutModifiers.Shift),
        "The remap did not resolve Primary to Command on macOS.");
    Require(history.ResolveGesture(ShortcutPlatform.Windows) ==
            Named("Y", ShortcutModifiers.Control | ShortcutModifiers.Shift),
        "The remap did not resolve Primary to Control on Windows.");
}

static void JsonPlatformBindingsRejected()
{
    using var disable = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            { "id": "copyButtonAlternate", "bindings": { "macOS": [] } }
          ]
        }
        """);
    RequireThrows<ShortcutCatalogException>(
        () => CalculatorShortcutCatalog.LoadRemap(disable),
        "A platform-specific remap was accepted.");

    using var definition = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            {
              "id": "copy",
              "scope": "calculator",
              "source": "UwpResource",
              "bindings": {
                "default": [ { "key": { "kind": "named", "value": "C" }, "modifiers": ["primary"] } ],
                "windows": [ { "key": { "kind": "named", "value": "C" }, "modifiers": ["control"] } ]
              }
            }
          ]
        }
        """);
    RequireThrows<ShortcutCatalogException>(
        () => ShortcutCatalogLoader.Load(definition),
        "A platform-specific definition was accepted.");
}

static void JsonRemapValidation()
{
    using var unknown = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            { "id": "does.not.exist", "bindings": { "default": [] } }
          ]
        }
        """);
    RequireThrows<ShortcutCatalogException>(
        () => CalculatorShortcutCatalog.LoadRemap(unknown),
        "An unknown shortcut remap was accepted.");

    using var invalidModifier = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            {
              "id": "HistoryButton",
              "bindings": {
                "default": [
                  { "key": { "kind": "named", "value": "H" }, "modifiers": ["Hyper"] }
                ]
              }
            }
          ]
        }
        """);
    RequireThrows<ShortcutCatalogException>(
        () => CalculatorShortcutCatalog.LoadRemap(invalidModifier),
        "An unknown modifier was accepted.");
}

static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));

static void MacControlModifierPreference()
{
    var mac = Service(ShortcutPlatform.MacOS, MacPrimaryModifier.Control);
    var registrations = CalculatorShortcutCatalog.RegisterAll(mac);
    try
    {
        var calculatorScope = new HashSet<string>(StringComparer.Ordinal) { "calculator" };
        var navigationScope = new HashSet<string>(StringComparer.Ordinal) { "navigation" };
        var graphingScope = new HashSet<string>(StringComparer.Ordinal) { "graphing" };
        Require(
            mac.IsMatch("copyButton", new ShortcutInput(Named("C", ShortcutModifiers.Control)), calculatorScope)
            && mac.IsMatch("pasteButton", new ShortcutInput(Named("V", ShortcutModifiers.Control)), calculatorScope)
            && mac.IsMatch("navigation.copy", new ShortcutInput(Named("C", ShortcutModifiers.Control)), navigationScope)
            && mac.IsMatch("navigation.paste", new ShortcutInput(Named("V", ShortcutModifiers.Control)), navigationScope),
            "Copy and paste did not switch to Control on macOS.");
        Require(
            !mac.IsMatch("copyButton", new ShortcutInput(Named("C", ShortcutModifiers.Command)), calculatorScope),
            "Command+C still matched after selecting the Control modifier.");
        Require(
            mac.IsMatch("graph.zoom.in", new ShortcutInput(Named("+", ShortcutModifiers.Control)), graphingScope)
            && mac.IsMatch("graph.zoom.out", new ShortcutInput(Named("-", ShortcutModifiers.Control)), graphingScope)
            && mac.IsMatch("graph.view.reset", new ShortcutInput(Named("0", ShortcutModifiers.Control)), graphingScope)
            && mac.IsMatch("graphViewButton", new ShortcutInput(Named("HOME", ShortcutModifiers.Control)), graphingScope),
            "The graph shortcuts did not switch to Control on macOS.");
        Require(
            mac.GetGestureDisplayText("graph.view.reset") == "⌃0",
            "The graph reset display text did not follow the Control modifier.");
        Require(
            mac.TryGetDefinition("copyButtonAlternate", out var copyAlternate)
            && copyAlternate!.ResolveGestures(ShortcutPlatform.MacOS, MacPrimaryModifier.Control).Count == 0,
            "The Insert-based alternates must stay unavailable on macOS.");
        Require(
            mac.GetConflicts().Count == CountMacConflicts(MacPrimaryModifier.Command),
            "Switching to Control introduced a macOS shortcut conflict.");
        // Control is a deliberate user choice on macOS, so the only new
        // findings may be the validator's non-idiomatic-modifier warnings.
        var commandIssues = ValidateMacConventions(MacPrimaryModifier.Command);
        var newIssues = mac.ValidateConventions().Except(commandIssues).ToArray();
        Require(
            newIssues.Length > 0
            && newIssues.All(issue =>
                issue.Kind == ShortcutConventionIssueKind.NonIdiomaticModifier
                && issue.Severity == ShortcutConventionSeverity.Warning),
            "Switching to Control introduced a macOS convention issue other than the expected modifier warning.");
    }
    finally
    {
        foreach (var registration in registrations)
        {
            registration.Dispose();
        }
    }

    static int CountMacConflicts(MacPrimaryModifier modifier)
    {
        var service = Service(ShortcutPlatform.MacOS, modifier);
        CalculatorShortcutCatalog.RegisterAll(service);
        return service.GetConflicts().Count;
    }

    static IReadOnlyList<ShortcutConventionIssue> ValidateMacConventions(MacPrimaryModifier modifier)
    {
        var service = Service(ShortcutPlatform.MacOS, modifier);
        CalculatorShortcutCatalog.RegisterAll(service);
        return service.ValidateConventions();
    }
}

static void MacModifierPreferenceLeavesOtherPlatforms()
{
    var definitions = CalculatorShortcutCatalog.CreateDefinitions();
    foreach (var definition in definitions)
    {
        foreach (var platform in new[] { ShortcutPlatform.Windows, ShortcutPlatform.Linux, ShortcutPlatform.Unknown })
        {
            Require(
                definition.ResolveGestures(platform, MacPrimaryModifier.Command)
                    .SequenceEqual(definition.ResolveGestures(platform, MacPrimaryModifier.Control)),
                $"The modifier preference changed '{definition.Id}' on {platform}.");
        }
    }

    var remapped = definitions
        .Where(definition =>
            !definition.ResolveGestures(ShortcutPlatform.MacOS, MacPrimaryModifier.Command)
                .SequenceEqual(definition.ResolveGestures(ShortcutPlatform.MacOS, MacPrimaryModifier.Control)))
        .Select(definition => definition.Id)
        .Order(StringComparer.Ordinal)
        .ToArray();
    var primary = definitions
        .Where(definition => new[] { definition.Gesture }.Concat(definition.AlternateGestures).Any(gesture =>
            gesture.Modifiers.HasFlag(ShortcutModifiers.Primary)
            && ShortcutPrimaryModifier.IsKeyAvailable(gesture.Key, ShortcutPlatform.MacOS)))
        .Select(definition => definition.Id)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(primary.Length > 0 && remapped.SequenceEqual(primary, StringComparer.Ordinal),
        "The Control preference must change exactly the Primary shortcuts on macOS: " + string.Join(", ", remapped));
}

static void MacModifierPreferenceValidation()
{
    var service = Service(ShortcutPlatform.MacOS);
    RequireThrows<ArgumentOutOfRangeException>(
        () => service.MacPrimaryModifier = (MacPrimaryModifier)7,
        "A value other than Command or Control was accepted as the macOS primary modifier.");
    Require(service.MacPrimaryModifier == MacPrimaryModifier.Command,
        "A rejected value changed the macOS primary modifier.");
}

static void NoPlatformSpecificGestures()
{
    Require(
        CalculatorShortcutCatalog.Bindings.All(binding =>
            (binding.Gesture.Modifiers & (ShortcutModifiers.Command | ShortcutModifiers.Control)) == 0),
        "The catalog names Ctrl or Command; use Primary so each platform resolves it.");

    // With Control selected, macOS uses exactly the Windows gestures, less the
    // keys a Mac keyboard does not have.
    foreach (var definition in CalculatorShortcutCatalog.CreateDefinitions())
    {
        var windows = definition.ResolveGestures(ShortcutPlatform.Windows)
            .Where(gesture => ShortcutPrimaryModifier.IsKeyAvailable(gesture.Key, ShortcutPlatform.MacOS));
        Require(
            definition.ResolveGestures(ShortcutPlatform.MacOS, MacPrimaryModifier.Control).SequenceEqual(windows),
            $"'{definition.Id}' has a macOS gesture that Windows does not.");
    }
}

static void NoMacOSCollisions()
{
    foreach (var modifier in new[] { MacPrimaryModifier.Command, MacPrimaryModifier.Control })
    {
        var mac = Service(ShortcutPlatform.MacOS, modifier);
        CalculatorShortcutCatalog.RegisterAll(mac);
        var collisions = mac.ValidateConventions().Where(issue => issue.IsCollision).ToArray();
        Require(collisions.Length == 0,
            $"Shortcuts collide with macOS ({modifier}): " +
            string.Join("; ", collisions.Select(issue => $"{issue.ShortcutId}: {issue.Message}")));
    }

    var windows = Service(ShortcutPlatform.Windows);
    CalculatorShortcutCatalog.RegisterAll(windows);
    var changed = new Dictionary<string, string>
    {
        ["HistoryButton"] = "Ctrl+Shift+H",
        ["memButton"] = "Ctrl+Shift+M",
        ["MemMinus"] = "Ctrl+Shift+P",
        ["powerOfEButton"] = "Ctrl+Shift+N",
        ["window.alwaysOnTop.enter"] = "Ctrl+Alt+Up",
        ["window.alwaysOnTop.exit"] = "Ctrl+Alt+Down",
        ["MemPlus"] = "Ctrl+P",
        ["ClearMemoryButton"] = "Ctrl+L",
    };
    foreach (var (id, expected) in changed)
    {
        var actual = windows.GetGestureDisplayText(id);
        Require(actual == expected, $"'{id}' must be {expected} on every platform, not {actual}.");
    }
}

static void LocalizedLabels()
{
    var german = Service(ShortcutPlatform.Windows, culture: "de-DE");
    CalculatorShortcutCatalog.RegisterAll(german);
    Require(german.GetGestureDisplayText("ClearHistory") == "Strg+Umschalt+D",
        "German Windows labels did not use Strg and Umschalt.");
    Require(
        german.RewriteText(
            "graph.view.reset",
            "Ansicht automatisch aktualisieren (Strg + 0)",
            ShortcutTextRewriteMode.ReplaceOrAppend) == "Ansicht automatisch aktualisieren (Strg+0)",
        "Tooltips did not use the shared German labels.");
}

static ShortcutGesture Named(string key, ShortcutModifiers modifiers) =>
    new(ShortcutKey.Named(key), modifiers);

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}

static ShortcutGesture Gesture(char key, ShortcutModifiers modifiers = ShortcutModifiers.None) =>
    new(ShortcutKey.Character(key), modifiers);

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
