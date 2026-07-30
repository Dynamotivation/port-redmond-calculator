using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

public sealed record CurrencyRateTable(
    string ProviderId,
    string BaseCurrency,
    DateOnly PublishedDate,
    IReadOnlyDictionary<string, decimal> RatesPerBase);

public sealed record CurrencyUnit(string Code, string Name, string Symbol)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Symbol) || Symbol == Code
        ? $"{Name} ({Code})"
        : $"{Name} ({Code}, {Symbol})";
}

public sealed record CurrencyProviderPreference(
    bool IsConsented = false,
    bool IsVisibleInSelector = true);

public sealed partial class CurrencyProviderOption : ObservableObject
{
    public CurrencyProviderOption(
        string id,
        string name,
        string organization,
        string sourceType,
        string requestSummary,
        string collectionSummary,
        string dataShared,
        string dataStored,
        string disclaimer,
        string termsUrl,
        string privacyUrl,
        string refreshFrequency,
        string refreshCadence,
        CurrencyProviderPreference preference)
    {
        Id = id;
        Name = name;
        Organization = organization;
        SourceType = sourceType;
        RequestSummary = requestSummary;
        CollectionSummary = collectionSummary;
        DataShared = dataShared;
        DataStored = dataStored;
        Disclaimer = disclaimer;
        TermsUrl = termsUrl;
        PrivacyUrl = privacyUrl;
        RefreshFrequency = refreshFrequency;
        RefreshCadence = refreshCadence;
        _isConsented = preference.IsConsented;
        IsVisibleInSelector = preference.IsVisibleInSelector;
    }

    public string Id { get; }
    public string Name { get; }
    public string Organization { get; }
    public string SourceType { get; }
    public string RequestSummary { get; }
    public string CollectionSummary { get; }
    public string DataShared { get; }
    public string DataStored { get; }
    public string Disclaimer { get; }
    public string TermsUrl { get; }
    public string PrivacyUrl { get; }
    public string RefreshFrequency { get; }
    public string RefreshCadence { get; }

    private bool _isConsented;
    public bool IsConsented => _isConsented;
    public bool CanConsent => !_isConsented;

    [ObservableProperty]
    public partial bool IsVisibleInSelector { get; set; }

    public string ConsentStatus => IsConsented ? "Consented" : "Not consented";
    public string SelectorLabel => $"{Name} ({RefreshFrequency})";
    public string DisclosureText =>
        $"{Organization}\n\nPublication\n{RefreshCadence}\n\nRequest\n{RequestSummary}\n\nShared with the provider\n{DataShared}\n\nStored by Redmond Calculator\n{DataStored}\n\n{Disclaimer}\n\nTerms: {TermsUrl}\nPrivacy: {PrivacyUrl}";

    public bool GrantConsent()
    {
        if (_isConsented)
        {
            return false;
        }
        _isConsented = true;
        OnPropertyChanged(nameof(IsConsented));
        OnPropertyChanged(nameof(CanConsent));
        OnPropertyChanged(nameof(ConsentStatus));
        return true;
    }
}

public interface ICurrencyRateProvider
{
    CurrencyProviderOption Option { get; }
    Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken);
}

public static class CurrencyProviderCatalog
{
    public static IReadOnlyList<ICurrencyRateProvider> Create(
        IReadOnlyDictionary<string, CurrencyProviderPreference>? preferences = null,
        HttpClient? httpClient = null)
    {
        var client = httpClient ?? CreateHttpClient();
        CurrencyProviderPreference Preference(string id) =>
            preferences is not null && preferences.TryGetValue(id, out var preference)
                ? preference
                : new CurrencyProviderPreference();

        return
        [
            new EcbCurrencyProvider(client, Preference(EcbCurrencyProvider.Id)),
            new FederalReserveCurrencyProvider(client, Preference(FederalReserveCurrencyProvider.Id)),
            new BankOfCanadaCurrencyProvider(client, Preference(BankOfCanadaCurrencyProvider.Id)),
            new FrankfurterCurrencyProvider(client, Preference(FrankfurterCurrencyProvider.Id)),
        ];
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RedmondCalculator", "0.1"));
        return client;
    }
}

public abstract class CurrencyRateProvider(
    HttpClient client,
    CurrencyProviderOption option) : ICurrencyRateProvider
{
    protected HttpClient Client { get; } = client;
    public CurrencyProviderOption Option { get; } = option;

    public abstract Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken);

    protected async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await Client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}

public sealed class EcbCurrencyProvider(HttpClient client, CurrencyProviderPreference preference)
    : CurrencyRateProvider(client, CreateOption(preference))
{
    public const string Id = "ecb";
    private static readonly Uri Endpoint = new(
        "https://data-api.ecb.europa.eu/service/data/EXR/D..EUR.SP00.A?lastNObservations=1&detail=dataonly&format=csvdata");

    public override async Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken) =>
        EcbCurrencyParser.Parse(await GetStringAsync(Endpoint, cancellationToken));

    private static CurrencyProviderOption CreateOption(CurrencyProviderPreference preference) => new(
        Id,
        "European Central Bank",
        "European Central Bank (EU)",
        "Official daily EUR reference rates",
        "One fixed HTTPS request for the latest complete rate table. Currency selections and entered amounts stay on this device.",
        "IP, request time, TLS metadata, app user-agent",
        "IP address, request time, ordinary TLS/network metadata, app user-agent, and the fixed latest-rates URL.",
        "The consent and source-list choices are stored in local app settings. The downloaded table and timestamp are retained in memory for this app session.",
        "Third-party service. Use at your own risk. No warranties are provided by Redmond Calculator or the ECB. Reference rates are informational and are not meant for transaction settlement.",
        "https://www.ecb.europa.eu/services/using-our-site/disclaimer/html/index.en.html",
        "https://www.ecb.europa.eu/services/data-protection/privacy-statements/html/index.en.html",
        "refreshes daily",
        "Every working day around 16:00 CET, except TARGET closing days.",
        preference);
}

public sealed class FederalReserveCurrencyProvider(HttpClient client, CurrencyProviderPreference preference)
    : CurrencyRateProvider(client, CreateOption(preference))
{
    public const string Id = "federal-reserve";
    private static readonly Uri Endpoint = new(
        "https://www.federalreserve.gov/datadownload/Output.aspx?rel=H10&series=60f32914ab61dfab590e0e470153e3ae&lastobs=10&from=&to=&filetype=csv&label=include&layout=seriescolumn&type=package");

    public override async Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken) =>
        FederalReserveCurrencyParser.Parse(await GetStringAsync(Endpoint, cancellationToken));

    private static CurrencyProviderOption CreateOption(CurrencyProviderPreference preference) => new(
        Id,
        "Federal Reserve H.10",
        "Board of Governors of the Federal Reserve System (US)",
        "Official weekly release of daily USD rates",
        "One fixed HTTPS request for the H.10 daily-rates package. Currency selections and entered amounts stay on this device.",
        "IP, request time, TLS metadata, app user-agent",
        "IP address, request time, ordinary TLS/network metadata, app user-agent, and the fixed H.10 package URL.",
        "The consent and source-list choices are stored in local app settings. The downloaded table and timestamp are retained in memory for this app session.",
        "Third-party US government service. Use at your own risk. No warranties are provided by Redmond Calculator or the Federal Reserve. H.10 observations are informational and are not meant for transaction settlement.",
        "https://www.federalreserve.gov/aboutthefed/website-linking-policies.htm",
        "https://www.federalreserve.gov/privacy.htm",
        "refreshes weekly",
        "Weekly on Monday at 16:15 ET for the previous business week; next business day after a federal holiday.",
        preference);
}

public sealed class BankOfCanadaCurrencyProvider(HttpClient client, CurrencyProviderPreference preference)
    : CurrencyRateProvider(client, CreateOption(preference))
{
    public const string Id = "bank-of-canada";
    private static readonly Uri Endpoint = new(
        "https://www.bankofcanada.ca/valet/observations/group/FX_RATES_DAILY/json?recent=1");

    public override async Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken) =>
        BankOfCanadaCurrencyParser.Parse(await GetStringAsync(Endpoint, cancellationToken));

    private static CurrencyProviderOption CreateOption(CurrencyProviderPreference preference) => new(
        Id,
        "Bank of Canada Valet",
        "Bank of Canada",
        "Official daily CAD indicative rates",
        "One fixed HTTPS request for the latest daily exchange-rate group. Currency selections and entered amounts stay on this device.",
        "IP, request time, TLS metadata, app user-agent",
        "IP address, request time, ordinary TLS/network metadata, app user-agent, and the fixed Valet group URL.",
        "The consent and source-list choices are stored in local app settings. The downloaded table and timestamp are retained in memory for this app session.",
        "Third-party Canadian central-bank service. Use at your own risk. No warranties are provided by Redmond Calculator or the Bank of Canada. Indicative rates are for statistical and analytical use and are not meant for transaction settlement.",
        "https://www.bankofcanada.ca/terms/",
        "https://www.bankofcanada.ca/privacy/",
        "refreshes each business day",
        "Once each business day by 16:30 ET.",
        preference);
}

public sealed class FrankfurterCurrencyProvider(HttpClient client, CurrencyProviderPreference preference)
    : CurrencyRateProvider(client, CreateOption(preference))
{
    public const string Id = "frankfurter";
    private static readonly Uri Endpoint = new("https://api.frankfurter.dev/v2/rates");

    public override async Task<CurrencyRateTable> FetchLatestAsync(CancellationToken cancellationToken) =>
        FrankfurterCurrencyParser.Parse(await GetStringAsync(Endpoint, cancellationToken));

    private static CurrencyProviderOption CreateOption(CurrencyProviderPreference preference) => new(
        Id,
        "Frankfurter",
        "Line of Flight / Frankfurter",
        "Open-source blend of institutional reference rates",
        "One fixed HTTPS request for its latest blended rate table. Currency selections and entered amounts stay on this device.",
        "IP, request time, TLS metadata, app user-agent; Cloudflare analytics",
        "IP address, request time, ordinary TLS/network metadata, app user-agent, and the fixed rates URL. Its public service is behind Cloudflare.",
        "The consent and source-list choices are stored in local app settings. The downloaded table, provider attribution, and timestamp are retained in memory for this app session.",
        "Third-party aggregate service using multiple underlying providers. Use at your own risk. No warranties are provided by Redmond Calculator or Frankfurter. Rates are informational and are not meant for transaction settlement.",
        "https://frankfurter.dev/",
        "https://frankfurter.dev/#faq",
        "refreshes daily",
        "Daily source observations; timing varies across its contributing central banks.",
        preference);
}

public static class CurrencyUnitCatalog
{
    public static IReadOnlyList<CurrencyUnit> Create(IEnumerable<string> codes)
    {
        var metadata = new Dictionary<string, CurrencyUnit>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                metadata.TryAdd(region.ISOCurrencySymbol, new(
                    region.ISOCurrencySymbol,
                    region.CurrencyEnglishName,
                    region.CurrencySymbol));
            }
            catch (ArgumentException)
            {
                // Some runtime-specific cultures have no corresponding region.
            }
        }

        return codes
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => metadata.TryGetValue(code, out var unit)
                ? unit
                : new CurrencyUnit(code, code, code))
            .OrderBy(unit => unit.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(unit => unit.Code, StringComparer.Ordinal)
            .ToArray();
    }
}

internal static class CurrencyCsv
{
    public static IReadOnlyList<string[]> Parse(string text)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == ',' && !quoted)
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }
                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => value.Length > 0))
                {
                    rows.Add(row.ToArray());
                }
                row.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row.ToArray());
        }
        return rows;
    }
}

public static class EcbCurrencyParser
{
    public static CurrencyRateTable Parse(string payload)
    {
        var rows = CurrencyCsv.Parse(payload);
        if (rows.Count < 2)
        {
            throw new FormatException("ECB response contained no observations.");
        }

        var header = rows[0];
        var currencyIndex = Array.IndexOf(header, "CURRENCY");
        var dateIndex = Array.IndexOf(header, "TIME_PERIOD");
        var valueIndex = Array.IndexOf(header, "OBS_VALUE");
        if (currencyIndex < 0 || dateIndex < 0 || valueIndex < 0)
        {
            throw new FormatException("ECB response did not contain the expected columns.");
        }

        var observations = rows.Skip(1)
            .Where(row => row.Length > Math.Max(currencyIndex, Math.Max(dateIndex, valueIndex)))
            .Select(row => new
            {
                Currency = row[currencyIndex],
                Date = DateOnly.Parse(row[dateIndex], CultureInfo.InvariantCulture),
                Value = decimal.Parse(row[valueIndex], NumberStyles.Float, CultureInfo.InvariantCulture),
            })
            .ToArray();
        var latest = observations.Max(item => item.Date);
        var rates = observations
            .Where(item => item.Date == latest && item.Value > 0)
            .ToDictionary(item => item.Currency, item => item.Value, StringComparer.OrdinalIgnoreCase);
        rates["EUR"] = 1m;
        return new(EcbCurrencyProvider.Id, "EUR", latest, rates);
    }
}

public static class FederalReserveCurrencyParser
{
    public static CurrencyRateTable Parse(string payload)
    {
        var rows = CurrencyCsv.Parse(payload);
        var currencyRow = rows.FirstOrDefault(row => row.FirstOrDefault() == "Currency:")
            ?? throw new FormatException("Federal Reserve response contained no currency row.");
        var identifierRow = rows.FirstOrDefault(row => row.FirstOrDefault() == "Unique Identifier:")
            ?? throw new FormatException("Federal Reserve response contained no identifier row.");
        var dataRow = rows.LastOrDefault(row =>
            row.Length > 1 && DateOnly.TryParse(row[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            ?? throw new FormatException("Federal Reserve response contained no observations.");
        var date = DateOnly.Parse(dataRow[0], CultureInfo.InvariantCulture);
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["USD"] = 1m,
        };

        for (var index = 1; index < Math.Min(currencyRow.Length, Math.Min(identifierRow.Length, dataRow.Length)); index++)
        {
            if (string.IsNullOrWhiteSpace(currencyRow[index])
                || !decimal.TryParse(dataRow[index], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                || value <= 0)
            {
                continue;
            }

            // H.10 mixes quote conventions. "$US" series are USD per foreign
            // currency and must be inverted; the other series are foreign
            // currency units per USD.
            rates[currencyRow[index]] = identifierRow[index].Contains("RXI$US_N", StringComparison.Ordinal)
                ? 1m / value
                : value;
        }
        return new(FederalReserveCurrencyProvider.Id, "USD", date, rates);
    }
}

public static class BankOfCanadaCurrencyParser
{
    public static CurrencyRateTable Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var newestByCurrency = new Dictionary<string, (DateOnly Date, decimal CadPerUnit)>(StringComparer.OrdinalIgnoreCase);
        foreach (var observation in document.RootElement.GetProperty("observations").EnumerateArray())
        {
            var date = DateOnly.Parse(observation.GetProperty("d").GetString()!, CultureInfo.InvariantCulture);
            foreach (var property in observation.EnumerateObject())
            {
                if (!property.Name.StartsWith("FX", StringComparison.Ordinal)
                    || !property.Name.EndsWith("CAD", StringComparison.Ordinal)
                    || property.Name.Length != 8
                    || !property.Value.TryGetProperty("v", out var valueElement)
                    || !decimal.TryParse(valueElement.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    || value <= 0)
                {
                    continue;
                }

                var currency = property.Name[2..5];
                if (!newestByCurrency.TryGetValue(currency, out var existing) || date > existing.Date)
                {
                    newestByCurrency[currency] = (date, value);
                }
            }
        }

        if (newestByCurrency.Count == 0)
        {
            throw new FormatException("Bank of Canada response contained no exchange-rate observations.");
        }

        var latest = newestByCurrency.Values.Max(item => item.Date);
        var cutoff = latest.AddDays(-14);
        var rates = newestByCurrency
            .Where(item => item.Value.Date >= cutoff)
            .ToDictionary(item => item.Key, item => 1m / item.Value.CadPerUnit, StringComparer.OrdinalIgnoreCase);
        rates["CAD"] = 1m;
        return new(BankOfCanadaCurrencyProvider.Id, "CAD", latest, rates);
    }
}

public static class FrankfurterCurrencyParser
{
    public static CurrencyRateTable Parse(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        string? baseCurrency = null;
        DateOnly? latest = null;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var itemBase = item.GetProperty("base").GetString();
            var quote = item.GetProperty("quote").GetString();
            var date = DateOnly.Parse(item.GetProperty("date").GetString()!, CultureInfo.InvariantCulture);
            var rate = item.GetProperty("rate").GetDecimal();
            if (string.IsNullOrWhiteSpace(itemBase) || string.IsNullOrWhiteSpace(quote) || rate <= 0)
            {
                continue;
            }
            baseCurrency ??= itemBase;
            latest = latest is null || date > latest ? date : latest;
            rates[quote] = rate;
        }

        if (baseCurrency is null || latest is null || rates.Count == 0)
        {
            throw new FormatException("Frankfurter response contained no rates.");
        }
        rates[baseCurrency] = 1m;
        return new(FrankfurterCurrencyProvider.Id, baseCurrency, latest.Value, rates);
    }
}
