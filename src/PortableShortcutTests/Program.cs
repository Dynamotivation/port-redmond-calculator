using Calculator.Shortcuts;
using Redmond.Shortcuts;
using System.Text;

var tests = new (string Name, Action Run)[]
{
    ("platform overrides", PlatformOverrides),
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
    ("JSON platform remap and inheritance", JsonPlatformRemapAndInheritance),
    ("JSON platform disable", JsonPlatformDisable),
    ("JSON remap validation", JsonRemapValidation),
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS: {test.Name}");
}

return;

static void PlatformOverrides()
{
    var service = ShortcutServices.Create(ShortcutPlatform.MacOS);
    service.Register(new ShortcutDefinition(
        "history.open",
        Gesture('H', ShortcutModifiers.Control),
        new Dictionary<ShortcutPlatform, ShortcutGesture>
        {
            [ShortcutPlatform.MacOS] = Gesture('H', ShortcutModifiers.Command),
        }));

    Require(service.IsMatch("history.open", new ShortcutInput(Gesture('H', ShortcutModifiers.Command))),
        "The macOS override was not selected.");
    Require(!service.IsMatch("history.open", new ShortcutInput(Gesture('H', ShortcutModifiers.Control))),
        "The default Windows-style gesture unexpectedly matched macOS.");
}

static void HandlerAndEventNotification()
{
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
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
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
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
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("once", Gesture('A')));
    service.Register(new ShortcutDefinition("repeat", Gesture('B'), allowRepeat: true));

    Require(service.Process(new ShortcutInput(Gesture('A'), IsRepeat: true)).Count == 0,
        "A non-repeatable shortcut accepted a repeated input.");
    Require(service.Process(new ShortcutInput(Gesture('B'), IsRepeat: true)).Count == 1,
        "A repeatable shortcut rejected a repeated input.");
}

static void RegistrationDisposal()
{
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
    var registration = service.Register(new ShortcutDefinition("temporary", Gesture('T')));
    registration.Dispose();
    Require(service.Process(new ShortcutInput(Gesture('T'))).Count == 0,
        "Disposing a registration did not remove it.");
}

static void ConflictDetection()
{
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("first", Gesture('X'), scope: "standard"));
    service.Register(new ShortcutDefinition("second", Gesture('X'), scope: "standard"));
    service.Register(new ShortcutDefinition("separate", Gesture('X'), scope: "programmer"));

    var conflicts = service.GetConflicts();
    Require(conflicts.Count == 1 && conflicts[0].FirstShortcutId == "first" && conflicts[0].SecondShortcutId == "second",
        "Conflict detection did not respect shortcut scopes.");
}

static void MacDisplaySymbols()
{
    var service = ShortcutServices.Create(ShortcutPlatform.MacOS);
    service.Register(new ShortcutDefinition(
        "find",
        Gesture('F', ShortcutModifiers.Control),
        new Dictionary<ShortcutPlatform, ShortcutGesture>
        {
            [ShortcutPlatform.MacOS] = Gesture('F', ShortcutModifiers.Command | ShortcutModifiers.Shift),
        }));

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
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition("copy", Gesture('C', ShortcutModifiers.Control)));
    Require(service.FormatText("copy", "Copy ({shortcut})") == "Copy (Ctrl+C)",
        "The shortcut placeholder was not replaced.");
}

static void LegacySuffixRewriting()
{
    var service = ShortcutServices.Create(ShortcutPlatform.MacOS);
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
            item.Source == ShortcutCatalogSource.UwpResource && item.Platform is null) == 129,
        "The catalog does not contain all 129 resource-defined bindings from the matrix.");
    Require(
        CalculatorShortcutCatalog.Bindings.Count(item =>
            item.Source == ShortcutCatalogSource.UwpNavigation && item.Platform is null) == 13,
        "The catalog does not contain all 13 navigation accelerators from the matrix.");
    Require(
        CalculatorShortcutCatalog.Bindings.Count(item =>
            item.Source == ShortcutCatalogSource.HardCodedControl && item.Platform is null) == 20,
        "The hard-coded matrix behaviors did not expand to all 20 concrete gestures.");

    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
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
    var mac = ShortcutServices.Create(ShortcutPlatform.MacOS);
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
            "The graph reset display text did not come from its macOS binding.");
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
            mac.GetGestureDisplayText("HistoryButton") == "⌃H",
            "A calculator-specific physical Control binding was not displayed accurately on macOS.");
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

    var windows = ShortcutServices.Create(ShortcutPlatform.Windows);
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
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
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
    var observe = ShortcutServices.Create(ShortcutPlatform.Windows);
    observe.Register(new ShortcutDefinition(
        "observer",
        Gesture('O'),
        inputHandling: ShortcutInputHandling.ObserveOnly));
    var observed = observe.Process(new ShortcutInput(Gesture('O')));
    Require(observed.WasMatched && !observed.Handled && !observed.ShouldConsume,
        "Observe-only input reported incorrect handling state.");

    var handle = ShortcutServices.Create(ShortcutPlatform.Windows);
    handle.Register(new ShortcutDefinition(
        "handler",
        Gesture('H'),
        inputHandling: ShortcutInputHandling.Handle));
    var handled = handle.Process(new ShortcutInput(Gesture('H')));
    Require(handled.WasMatched && handled.Handled && !handled.ShouldConsume,
        "Handled but non-consuming input reported incorrect state.");

    var consume = ShortcutServices.Create(ShortcutPlatform.Windows);
    consume.Register(new ShortcutDefinition("consumer", Gesture('C')));
    var consumed = consume.Process(new ShortcutInput(Gesture('C')));
    Require(consumed.WasMatched && consumed.Handled && consumed.ShouldConsume,
        "Consuming input reported incorrect state.");
}

static void PlatformConventionValidation()
{
    var service = ShortcutServices.Create(ShortcutPlatform.MacOS);
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
    var service = ShortcutServices.Create(ShortcutPlatform.Windows);
    service.Register(new ShortcutDefinition(
        "equals",
        Gesture('='),
        alternateGestures: [new ShortcutGesture(ShortcutKey.Named("Enter"))]));

    Require(service.GetGestureDisplayTexts("equals").SequenceEqual(["=", "Enter"]),
        "The display API did not return all alternate gestures.");
    Require(service.FormatText("equals", "Equals ({shortcuts})") == "Equals (= / Enter)",
        "The plural shortcut placeholder did not format all gestures.");
}

static void JsonPlatformRemapAndInheritance()
{
    using var remap = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            {
              "id": "HistoryButton",
              "bindings": {
                "macOS": [
                  {
                    "key": { "kind": "named", "value": "H" },
                    "modifiers": ["command"]
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
            new ShortcutGesture(ShortcutKey.Named("H"), ShortcutModifiers.Command),
        "The macOS remap was not applied.");
    Require(history.ResolveGesture(ShortcutPlatform.Windows) ==
            new ShortcutGesture(ShortcutKey.Named("H"), ShortcutModifiers.Control),
        "A platform remap unexpectedly replaced the inherited Windows default.");
}

static void JsonPlatformDisable()
{
    using var remap = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            { "id": "copyButtonAlternate", "bindings": { "macOS": [] } }
          ]
        }
        """);
    var catalog = CalculatorShortcutCatalog.LoadRemap(remap);
    var copyAlternate = catalog.Definitions.Single(item => item.Id == "copyButtonAlternate");
    Require(copyAlternate.ResolveGestures(ShortcutPlatform.MacOS).Count == 0,
        "An empty platform binding list did not disable the shortcut.");
    Require(copyAlternate.ResolveGestures(ShortcutPlatform.Windows).Count == 1,
        "Disabling macOS unexpectedly disabled the inherited Windows binding.");
}

static void JsonRemapValidation()
{
    using var unknown = JsonStream("""
        {
          "schemaVersion": 1,
          "shortcuts": [
            { "id": "does.not.exist", "bindings": { "macOS": [] } }
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
                "macOS": [
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
