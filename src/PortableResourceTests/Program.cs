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
const string memoryTooltipPath = "MemoryButton/[using:Windows.UI.Xaml.Controls]ToolTipService/ToolTip";
const string memoryTooltipProperty = "[using:Windows.UI.Xaml.Controls]ToolTipService.ToolTip";
Require(resources.GetString(memoryTooltipPath) == "Speicher", "Attached-property resource path normalization failed.");
Require(resources.GetUidProperties("MemoryButton")[memoryTooltipProperty] == "Speicher",
    "Attached-property x:Uid projection failed.");
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
var germanNumberFormat = CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("de-DE"));
using (var calculator = new NativeCalculator(
           ResourceLoader.GetForViewIndependentUse("CEngineStrings"),
           germanNumberFormat))
{
    calculator.SendCommand(CalculatorCommand.One);
    calculator.SendCommand(CalculatorCommand.Two);
    calculator.SendCommand(CalculatorCommand.Decimal);
    calculator.SendCommand(CalculatorCommand.Five);
    Require(calculator.PrimaryDisplay == "12,5", "Managed culture did not reach the native calculator engine.");
}

using (var localizedConverter = new NativeUnitConverter(
           ResourceLoader.GetForViewIndependentUse(),
           "DE",
           germanNumberFormat))
{
    localizedConverter.SendCommand(UnitConverterCommand.One);
    localizedConverter.SendCommand(UnitConverterCommand.Decimal);
    localizedConverter.SendCommand(UnitConverterCommand.Five);
    Require(localizedConverter.FromDisplay == "1,5", "Unit-converter input was not localized at its managed boundary.");
}

using (var converter = new NativeUnitConverter(
           ResourceLoader.GetForViewIndependentUse(),
           "US",
           CalculatorNumberFormat.FromCulture(CultureInfo.InvariantCulture)))
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
    supportsPlatformAppearanceSettings: true,
    numberCulture: CultureInfo.GetCultureInfo("de-DE"),
    availableFontFamilies: ["Aptos", "Inter"],
    initialFontFamily: "Missing Font"))
{
    Require(viewModel.DecimalSeparator == ",", "The keypad did not expose the active culture's decimal separator.");
    Require(viewModel.TitleBarApplicationName == "Calculator",
        "The localized Windows-style title-bar application name was not loaded.");
    Require(viewModel.AlwaysOnTopTooltip == "Keep on top (Alt+Up)",
        "The initial always-on-top tooltip was not localized.");
    Require(viewModel.AlwaysOnTopGlyph == "\uEE49", "The normal always-on-top source glyph was not selected.");
    viewModel.IsAlwaysOnTop = true;
    Require(viewModel.AlwaysOnTopTooltip == "Back to full view (Alt+Down)",
        "The always-on-top tooltip did not follow its state.");
    Require(viewModel.AlwaysOnTopGlyph == "\uEE47", "The exit always-on-top source glyph was not selected.");
    viewModel.IsAlwaysOnTop = false;
    Require(viewModel.CalculatorNavigationItems.Count == 5, "Calculator navigation manifest is incomplete.");
    Require(viewModel.ConverterNavigationItems.Count == 13, "Converter navigation manifest is incomplete.");
    Require(viewModel.IsStandardMode
        && viewModel.CalculatorNavigationItems.Single(item => item.Mode == CalculatorViewMode.Standard).IsSelected,
        "Navigation did not initialize in Standard mode.");

    viewModel.ExecuteCalculatorCommand(CalculatorCommand.One);
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.Add);
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.Two);
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.Equals);
    Require(viewModel.PrimaryDisplay == "3" && viewModel.HasHistory && viewModel.History.Count == 1,
        "Standard calculations did not synchronize typed history from CalculatorManager.");
    viewModel.SetHistoryDocked(false);
    viewModel.ToggleHistoryCommand.Execute(null);
    Require(viewModel.IsNarrowHistoryPaneVisible && !viewModel.IsDockedHistoryPaneVisible,
        "Narrow history did not open on demand.");
    viewModel.CloseHistoryCommand.Execute(null);
    Require(!viewModel.IsNarrowHistoryPaneVisible && !viewModel.IsHistoryOpen,
        "Narrow history did not support the flyout light-dismiss path.");
    viewModel.ToggleHistoryCommand.Execute(null);
    viewModel.SelectHistoryEntryCommand.Execute(viewModel.History[0]);
    Require(viewModel.PrimaryDisplay == "3" && !viewModel.IsHistoryOpen,
        "Selecting narrow history did not restore its display and close the full-page flyout.");
    viewModel.SetHistoryDocked(true);
    Require(viewModel.IsDockedHistoryPaneVisible && !viewModel.IsHistoryOpen && !viewModel.IsHistoryButtonVisible,
        "Wide history did not switch to the source Calculator's docked state.");
    Require(viewModel.TryPasteStandardExpression("-12.5 + 2 =") && viewModel.PrimaryDisplay == "-10,5",
        $"Cross-platform Standard paste did not preserve CalculatorManager semantics or locale formatting (actual: {viewModel.PrimaryDisplay}).");
    viewModel.ClearHistoryCommand.Execute(null);
    Require(!viewModel.HasHistory && viewModel.History.Count == 0,
        "Clear history did not update CalculatorManager and the managed collection together.");

    var scientificItem = viewModel.CalculatorNavigationItems.Single(item => item.Mode == CalculatorViewMode.Scientific);
    Require(scientificItem.IsEnabled, "Scientific navigation was not enabled by the portable frontend.");
    await viewModel.SelectNavigationItemCommand.ExecuteAsync(scientificItem);
    Require(viewModel.IsScientificMode && viewModel.IsCalculatorMode && viewModel.IsDockedHistoryPaneVisible,
        "Scientific navigation did not switch the shared CalculatorManager or retain calculator history behavior.");
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.Clear);
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.Nine);
    viewModel.ExecuteCalculatorCommand(CalculatorCommand.SquareRoot);
    Require(viewModel.PrimaryDisplay == "3", "Scientific commands did not execute through CalculatorManager.");
    viewModel.CycleScientificAngleCommand.Execute(null);
    Require(viewModel.SelectedScientificAngle == CalculatorAngleMode.Radians && viewModel.ScientificAngleLabel == "RAD",
        "Scientific angle mode did not advance from degrees to radians.");

    var standardItem = viewModel.CalculatorNavigationItems.Single(item => item.Mode == CalculatorViewMode.Standard);
    await viewModel.SelectNavigationItemCommand.ExecuteAsync(standardItem);
    Require(viewModel.IsStandardMode, "Scientific mode did not switch back to Standard through the shared mode boundary.");

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
    string? changedFont = null;
    viewModel.FontPreferenceChanged += value => changedFont = value;
    Require(viewModel.SelectedFontFamily == "Inter", "Inter was not retained as the recommended default font.");
    viewModel.SelectedFontFamily = "Aptos";
    Require(changedFont == "Aptos", "An available device font was not selected.");
    PlatformAppearancePreferences? changedAppearance = null;
    viewModel.PlatformAppearancePreferencesChanged += value => changedAppearance = value;
    viewModel.UseMicaEffect = false;
    viewModel.SelectWindowControlStyleCommand.Execute(nameof(WindowControlStyle.MacOS));
    Require(viewModel.SupportsPlatformAppearanceSettings
        && changedAppearance == new PlatformAppearancePreferences(
            false,
            WindowCornerStyle.Windows11,
            WindowControlStyle.MacOS)
        && !viewModel.UsesNativeWindowGeometry
        && viewModel.UsesMacOSWindowControls,
        "macOS controls changed the selected corner geometry or failed to notify the frontend host.");
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.Windows10));
    Require(viewModel.UsesSquareWindowCorners && viewModel.UsesMacOSWindowControls
        && viewModel.WindowCornerRadius == 0,
        "macOS controls could not be combined with the Windows 10 square shape.");
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.MacOS));
    Require(viewModel.IsMacOSCornerStyleSelected && viewModel.UsesMacOSWindowControls,
        "The fully native macOS combination could not be selected.");
    viewModel.SelectWindowControlStyleCommand.Execute(nameof(WindowControlStyle.Windows10));
    Require(viewModel.IsMacOSCornerStyleSelected
        && viewModel.IsWindows10WindowControlStyleSelected
        && viewModel.UsesWindowsWindowControls
        && viewModel.UsesNativeWindowGeometry,
        "macOS corners could not be combined with Windows 10 controls.");
    viewModel.SelectWindowCornerStyleCommand.Execute(nameof(WindowCornerStyle.Windows11));
    Require(viewModel.IsWindows11CornerStyleSelected
        && viewModel.IsWindows10WindowControlStyleSelected
        && viewModel.WindowCornerRadius == 8,
        "Changing corner geometry also changed the Windows title-bar generation.");
    viewModel.SelectWindowControlStyleCommand.Execute(nameof(WindowControlStyle.Windows11));
    Require(viewModel.IsWindows11CornerStyleSelected
        && viewModel.IsWindows11WindowControlStyleSelected
        && viewModel.UsesWindowsWindowControls,
        "Windows 11 title-bar controls could not be selected independently.");
    viewModel.CloseSettingsCommand.Execute(null);
    Require(!viewModel.IsSettingsOpen, "Settings back command did not restore calculator content.");
}

Console.WriteLine("ResourceLoader and managed native-unit-converter compatibility tests passed.");
