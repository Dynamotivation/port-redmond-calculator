using System.Globalization;
using System.Text.Json;
using Calculator.Avalonia;
using Calculator.Managed;

namespace Calculator.Avalonia.Tests;

internal static class CurrencyTests
{
    public static IReadOnlyList<(string Name, Action Run)> All =>
    [
        ("ECB parser keeps only the latest table", EcbParserKeepsLatestTable),
        ("Federal Reserve parser normalizes mixed quote conventions", FederalReserveParserNormalizesQuotes),
        ("Bank of Canada parser normalizes CAD quotes", BankOfCanadaParserNormalizesQuotes),
        ("Frankfurter parser reads provider-native JSON", FrankfurterParserReadsJson),
        ("currency never fetches before explicit consent", NoFetchBeforeExplicitConsent),
        ("currency fetches only the selected source", OnlySelectedSourceIsFetched),
        ("force refresh bypasses the session cache", ForceRefreshBypassesSessionCache),
        ("currency conversion sends no user values", ConversionSendsNoUserValues),
        ("currency reconstructs non-base pairs locally", ReconstructsNonBasePairsLocally),
        ("every provider has an independent disclosure", EveryProviderHasDisclosure),
        ("provider preferences persist under one settings key", ProviderPreferencesPersistUnderOneSettingsKey),
        ("currency navigation is enabled", CurrencyNavigationIsEnabled),
    ];

    private static void EcbParserKeepsLatestTable()
    {
        const string payload = """
            KEY,FREQ,CURRENCY,CURRENCY_DENOM,EXR_TYPE,EXR_SUFFIX,TIME_PERIOD,OBS_VALUE
            EXR.D.OLD.EUR.SP00.A,D,OLD,EUR,SP00,A,2020-01-01,2
            EXR.D.USD.EUR.SP00.A,D,USD,EUR,SP00,A,2026-07-30,1.25
            EXR.D.GBP.EUR.SP00.A,D,GBP,EUR,SP00,A,2026-07-30,0.80
            """;
        var table = EcbCurrencyParser.Parse(payload);
        Assert(table.BaseCurrency == "EUR", "ECB base should be EUR");
        Assert(table.RatesPerBase["EUR"] == 1m, "ECB base rate should be one");
        Assert(table.RatesPerBase["USD"] == 1.25m, "ECB USD rate was not parsed");
        Assert(!table.RatesPerBase.ContainsKey("OLD"), "stale/discontinued ECB observations should be excluded");
    }

    private static void FederalReserveParserNormalizesQuotes()
    {
        const string payload = """
            "Series Description","Euro-Area Euro","Canadian Dollar"
            "Currency:","EUR","CAD"
            "Unique Identifier:","H10/H10/RXI$US_N.B.EU","H10/H10/RXI_N.B.CA"
            "Time Period","RXI$US_N.B.EU","RXI_N.B.CA"
            2026-07-29,1.2500,1.5000
            """;
        var table = FederalReserveCurrencyParser.Parse(payload);
        Assert(table.BaseCurrency == "USD", "Federal Reserve base should be USD");
        Assert(table.RatesPerBase["EUR"] == 0.8m, "USD-per-EUR series should be inverted");
        Assert(table.RatesPerBase["CAD"] == 1.5m, "CAD-per-USD series should remain direct");
    }

    private static void BankOfCanadaParserNormalizesQuotes()
    {
        const string payload = """
            {
              "observations": [
                {
                  "d": "2026-07-29",
                  "FXUSDCAD": { "v": "1.25" },
                  "FXEURCAD": { "v": "1.50" }
                }
              ]
            }
            """;
        var table = BankOfCanadaCurrencyParser.Parse(payload);
        Assert(table.BaseCurrency == "CAD", "Bank of Canada base should be CAD");
        Assert(table.RatesPerBase["USD"] == 0.8m, "CAD per USD should be inverted");
        Assert(table.RatesPerBase["EUR"] == 2m / 3m, "CAD per EUR should be inverted");
    }

    private static void FrankfurterParserReadsJson()
    {
        const string payload = """
            [
              { "date": "2026-07-30", "base": "EUR", "quote": "USD", "rate": 1.2 },
              { "date": "2026-07-30", "base": "EUR", "quote": "GBP", "rate": 0.8 }
            ]
            """;
        var table = FrankfurterCurrencyParser.Parse(payload);
        Assert(table.RatesPerBase["EUR"] == 1m, "Frankfurter base rate should be one");
        Assert(table.RatesPerBase["USD"] == 1.2m, "Frankfurter USD rate was not parsed");
    }

    private static void ConversionSendsNoUserValues()
    {
        var provider = new RecordingProvider();
        using var viewModel = new CurrencyConverterViewModel(
            CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("en-US")),
            [provider]);
        viewModel.ActivateAsync().GetAwaiter().GetResult();
        viewModel.SelectedFromCurrency = viewModel.Currencies.First(currency => currency.Code == "EUR");
        viewModel.SelectedToCurrency = viewModel.Currencies.First(currency => currency.Code == "USD");
        viewModel.SendCommand("One");
        viewModel.SendCommand("Two");

        Assert(provider.CallCount == 1, "one table request should serve multiple local input changes");
        Assert(viewModel.FromDisplay == "12", "currency input should be edited locally");
        Assert(viewModel.ToDisplay == "24", "currency conversion should be calculated locally");
    }

    private static void NoFetchBeforeExplicitConsent()
    {
        var provider = new RecordingProvider(isConsented: false);
        using var viewModel = new CurrencyConverterViewModel(
            CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("en-US")),
            [provider]);

        viewModel.ActivateAsync().GetAwaiter().GetResult();
        Assert(provider.CallCount == 0, "activation must not contact an unconsented provider");
        Assert(viewModel.SelectedProvider is null, "the selector should start empty when no source has consent");

        viewModel.SelectedProvider = provider.Option;
        Assert(provider.CallCount == 0, "selecting an unconsented source must not contact it");
        Assert(viewModel.NeedsConsent, "the converter should offer consent for the selected source");
        Assert(viewModel.RateSummary == "Consent required", "the compact source status should not include disclosure copy");

        viewModel.ConsentSelectedProviderCommand.Execute(null);
        Assert(provider.CallCount == 1, "an explicitly consented selected source should be fetched once");
        Assert(provider.Option.IsConsented, "consent should remain recorded after it is granted");
        provider.Option.IsVisibleInSelector = false;
        Assert(provider.Option.IsConsented, "hiding a source must not erase its consent history");
    }

    private static void OnlySelectedSourceIsFetched()
    {
        var selected = new RecordingProvider("selected");
        var other = new RecordingProvider("other");
        using var viewModel = new CurrencyConverterViewModel(
            CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("en-US")),
            [selected, other]);

        viewModel.ActivateAsync().GetAwaiter().GetResult();
        Assert(selected.CallCount == 1, "the selected source should provide its own currency list and rates");
        Assert(other.CallCount == 0, "unselected sources must not be fetched to aggregate currency coverage");
    }

    private static void ForceRefreshBypassesSessionCache()
    {
        var provider = new RecordingProvider();
        using var viewModel = new CurrencyConverterViewModel(
            CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("en-US")),
            [provider]);
        viewModel.ActivateAsync().GetAwaiter().GetResult();

        viewModel.RefreshCommand.Execute(null);
        Assert(provider.CallCount == 2, "force refresh should issue a new request for only the selected source");
        Assert(viewModel.LastRefreshedText.StartsWith("Last refreshed on this device:", StringComparison.Ordinal), "refresh time should be shown as fine print");
    }

    private static void ReconstructsNonBasePairsLocally()
    {
        var provider = new RecordingProvider(
            rates: new Dictionary<string, decimal>
            {
                ["USD"] = 1m,
                ["EUR"] = 0.8m,
                ["GBP"] = 0.5m,
            });
        using var viewModel = new CurrencyConverterViewModel(
            CalculatorNumberFormat.FromCulture(CultureInfo.GetCultureInfo("en-US")),
            [provider]);
        viewModel.ActivateAsync().GetAwaiter().GetResult();
        viewModel.SelectedFromCurrency = viewModel.Currencies.Single(currency => currency.Code == "EUR");
        viewModel.SelectedToCurrency = viewModel.Currencies.Single(currency => currency.Code == "GBP");
        viewModel.SendCommand("One");

        Assert(viewModel.ToDisplay == "0.625", "EUR to GBP should be reconstructed through the provider's USD base table");
        Assert(provider.CallCount == 1, "cross-rate reconstruction must happen without another provider request");
    }

    private static void EveryProviderHasDisclosure()
    {
        var providers = CurrencyProviderCatalog.Create();
        Assert(providers.Count >= 4, "the app should offer multiple independent sources");
        foreach (var provider in providers)
        {
            var option = provider.Option;
            Assert(option.DataShared.Contains("IP address", StringComparison.Ordinal), $"{option.Name} must disclose network metadata");
            Assert(option.DataStored.Contains("local app settings", StringComparison.Ordinal), $"{option.Name} must disclose local storage");
            Assert(option.Disclaimer.Contains("Use at your own risk", StringComparison.Ordinal), $"{option.Name} must carry an own-risk disclaimer");
            Assert(option.Disclaimer.Contains("not meant for transaction settlement", StringComparison.Ordinal), $"{option.Name} must reject settlement use");
            Assert(!string.IsNullOrWhiteSpace(option.RefreshCadence), $"{option.Name} must disclose its publication cadence");
        }
    }

    private static void CurrencyNavigationIsEnabled()
    {
        using var viewModel = new CalculatorViewModel();
        var item = viewModel.ConverterNavigationItems.Single(item => item.Mode == CalculatorViewMode.Currency);
        Assert(item.IsEnabled, "currency navigation should be enabled");
    }

    private static void ProviderPreferencesPersistUnderOneSettingsKey()
    {
        var preferences = new Dictionary<string, CurrencyProviderPreference>(StringComparer.Ordinal)
        {
            [EcbCurrencyProvider.Id] = new(IsConsented: true, IsVisibleInSelector: false),
        };
        var settings = new AppSettings() with { CurrencyProviderPreferences = preferences };
        var json = JsonSerializer.Serialize(settings);

        Assert(settings.CurrencyProviderPreferences?[EcbCurrencyProvider.Id].IsConsented == true, "the consent choice should remain attached to settings");
        Assert(settings.CurrencyProviderPreferences?[EcbCurrencyProvider.Id].IsVisibleInSelector == false, "the source-list choice should remain attached to settings");
        Assert(json.Contains("\"CurrencyProviderPreferences\"", StringComparison.Ordinal), "provider choices need one stable persisted key");
    }

    private sealed class RecordingProvider : ICurrencyRateProvider
    {
        private readonly IReadOnlyDictionary<string, decimal> _rates;

        public RecordingProvider(
            string id = "recording",
            bool isConsented = true,
            bool isVisible = true,
            IReadOnlyDictionary<string, decimal>? rates = null)
        {
            _rates = rates ?? new Dictionary<string, decimal>
            {
                ["EUR"] = 1m,
                ["USD"] = 2m,
            };
            Option = new CurrencyProviderOption(
                id,
                $"Recording provider {id}",
                "Tests",
                "Test table",
                "Fixed table request",
                "Test metadata",
                "Nothing",
                "Nothing",
                "Use at your own risk; not meant for transaction settlement.",
                "https://example.invalid/terms",
                "https://example.invalid/privacy",
                "refreshes on demand",
                "Test cadence",
                new CurrencyProviderPreference(isConsented, isVisible));
        }

        public int CallCount { get; private set; }
        public CurrencyProviderOption Option { get; }

        public Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new CurrencyRateTable(
                Option.Id,
                "EUR",
                new DateOnly(2026, 7, 30),
                new Dictionary<string, decimal>
                (_rates, StringComparer.OrdinalIgnoreCase)));
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
