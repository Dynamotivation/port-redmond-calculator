using System.Globalization;
using Calculator.Managed;
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

ResourceLoader.Configure(new ResourceLoaderConfiguration(resourceRoot)
{
    UICultureProvider = static () => CultureInfo.GetCultureInfo("en-US"),
});
using (var converter = new NativeUnitConverter(ResourceLoader.GetForViewIndependentUse(), "US"))
{
    Require(converter.Categories.Count == 12, "Managed unit converter did not expose all non-currency categories.");
    var temperature = converter.Categories.Single(category => category.Id == 7);
    Require(temperature.Name == "Temperature" && temperature.SupportsNegative, "Managed category metadata is incorrect.");

    converter.SelectCategory(temperature.Id);
    var units = converter.Units;
    var selected = converter.SelectedUnits;
    Require(units.Count == 3 && selected.FromUnitId == 46 && selected.ToUnitId == 47,
        "Managed US temperature defaults are incorrect.");
    Require(units.Single(unit => unit.Id == 46).Abbreviation == "°C", "Managed UTF-8 unit metadata is incorrect.");

    converter.SendCommand(UnitConverterCommand.One);
    converter.SendCommand(UnitConverterCommand.Zero);
    converter.SendCommand(UnitConverterCommand.Zero);
    Require(converter.FromDisplay == "100" && converter.ToDisplay == "212", "Managed conversion did not produce 100 °C = 212 °F.");
    Require(converter.Suggestions.Count != 0, "Managed conversion suggestions were dropped.");
}

using (var viewModel = new CalculatorViewModel(
    initialPlatformAppearance: new PlatformAppearancePreferences(),
    supportsPlatformAppearanceSettings: true))
{
    Require(viewModel.CalculatorNavigationItems.Count == 5, "Calculator navigation manifest is incomplete.");
    Require(viewModel.ConverterNavigationItems.Count == 13, "Converter navigation manifest is incomplete.");
    Require(viewModel.IsStandardMode
        && viewModel.CalculatorNavigationItems.Single(item => item.Mode == CalculatorViewMode.Standard).IsSelected,
        "Navigation did not initialize in Standard mode.");

    await viewModel.ToggleNavigationPaneCommand.ExecuteAsync(null);
    Require(viewModel.IsNavigationPaneOpen, "Hamburger command did not open the navigation pane.");
    var temperatureItem = viewModel.ConverterNavigationItems.Single(item => item.Mode == CalculatorViewMode.Temperature);
    await viewModel.SelectNavigationItemCommand.ExecuteAsync(temperatureItem);
    Require(viewModel.IsUnitConverterMode && viewModel.CurrentViewMode == CalculatorViewMode.Temperature
        && viewModel.SelectedUnitCategory?.Id == (int)CalculatorViewMode.Temperature
        && !viewModel.IsNavigationPaneOpen && temperatureItem.IsSelected,
        "Navigation did not route to the selected converter category.");

    await viewModel.ToggleNavigationPaneCommand.ExecuteAsync(null);
    await viewModel.OpenSettingsCommand.ExecuteAsync(null);
    Require(viewModel.IsSettingsOpen && !viewModel.IsNavigationPaneOpen,
        "Settings navigation did not close the pane and open the Settings surface.");
    AppThemePreference? changedTheme = null;
    viewModel.ThemePreferenceChanged += value => changedTheme = value;
    viewModel.SelectThemeCommand.Execute(nameof(AppThemePreference.Light));
    Require(viewModel.IsLightThemeSelected && changedTheme == AppThemePreference.Light,
        "Settings theme selection did not update state or notify the frontend host.");
    PlatformAppearancePreferences? changedAppearance = null;
    viewModel.PlatformAppearancePreferencesChanged += value => changedAppearance = value;
    viewModel.UseMicaEffect = false;
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.MacOS));
    viewModel.SelectWindowControlStyleCommand.Execute(nameof(WindowControlStyle.MacOS));
    Require(viewModel.SupportsPlatformAppearanceSettings
        && changedAppearance == new PlatformAppearancePreferences(
            false,
            WindowCornerStyle.MacOS,
            WindowControlStyle.MacOS)
        && viewModel.UsesNativeWindowGeometry
        && viewModel.UsesMacOSWindowControls,
        "Platform appearance settings did not update state or notify the frontend host.");
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.Windows10));
    Require(viewModel.UsesSquareWindowCorners && viewModel.UsesMacOSWindowControls
        && viewModel.WindowCornerRadius == 0,
        "macOS controls could not be combined with the Windows 10 square shape.");
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.Windows11));
    viewModel.SelectWindowControlStyleCommand.Execute(nameof(WindowControlStyle.Windows));
    Require(viewModel.IsWindows11CornerStyleSelected && viewModel.UsesWindowsWindowControls
        && viewModel.WindowCornerRadius == 8,
        "Windows 11 shape and Windows controls did not restore independently.");
    viewModel.CloseSettingsCommand.Execute(null);
    Require(!viewModel.IsSettingsOpen, "Settings back command did not restore calculator content.");
}

Console.WriteLine("ResourceLoader and managed native-unit-converter compatibility tests passed.");
