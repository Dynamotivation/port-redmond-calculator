using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Calculator.Managed;

public sealed partial class CurrencyConverterViewModel : ObservableObject, IDisposable
{
    private sealed record CachedRateTable(CurrencyRateTable Table, DateTimeOffset RefreshedAt);

    private readonly CalculatorNumberFormat _numberFormat;
    private readonly Dictionary<string, ICurrencyRateProvider> _providers;
    private readonly Dictionary<string, CachedRateTable> _sessionCache = new(StringComparer.Ordinal);
    private CancellationTokenSource? _loadCancellation;
    private string _canonicalInput = "0";
    private bool _synchronizing;
    private bool _isActive;

    public CurrencyConverterViewModel(
        CalculatorNumberFormat numberFormat,
        IReadOnlyList<ICurrencyRateProvider> providers)
    {
        _numberFormat = numberFormat;
        _providers = providers.ToDictionary(provider => provider.Option.Id, StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            Providers.Add(provider.Option);
            provider.Option.PropertyChanged += OnProviderOptionChanged;
        }
        RebuildSelectableProviders();
        SelectedProvider = SelectableProviders.FirstOrDefault(provider => provider.IsConsented);
    }

    public event Action<CurrencyProviderOption>? ProviderPreferenceChanged;

    public ObservableCollection<CurrencyProviderOption> Providers { get; } = [];
    public ObservableCollection<CurrencyProviderOption> SelectableProviders { get; } = [];
    public ObservableCollection<CurrencyUnit> Currencies { get; } = [];

    [ObservableProperty]
    public partial CurrencyProviderOption? SelectedProvider { get; set; }

    [ObservableProperty]
    public partial CurrencyUnit? SelectedFromCurrency { get; set; }

    [ObservableProperty]
    public partial CurrencyUnit? SelectedToCurrency { get; set; }

    [ObservableProperty]
    public partial string FromDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial string ToDisplay { get; private set; } = "0";

    [ObservableProperty]
    public partial string RateSummary { get; private set; } = "No source selected.";

    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; private set; }

    [ObservableProperty]
    public partial string LastRefreshedText { get; private set; } = "Never refreshed on this device.";

    [ObservableProperty]
    public partial bool NeedsConsent { get; private set; }

    public bool HasConsentedProviders => Providers.Any(provider => provider.IsConsented);
    public string ProviderInformationText => SelectedProvider is null
        ? "Missing a currency? Try another consented source. Each source supplies its own currency list and publication date."
        : $"{SelectedProvider.DisclosureText}\n\nMissing a currency? Try another consented source. Each source supplies its own currency list and publication date.";

    public Task ActivateAsync()
    {
        _isActive = true;
        return LoadSelectedProviderAsync(forceRefresh: false);
    }

    public void Deactivate()
    {
        _isActive = false;
        _loadCancellation?.Cancel();
        IsLoading = false;
    }

    public bool TryDispatchShortcut(string shortcutId)
    {
        var command = shortcutId switch
        {
            "num0Button" => UnitConverterCommand.Zero,
            "num1Button" => UnitConverterCommand.One,
            "num2Button" => UnitConverterCommand.Two,
            "num3Button" => UnitConverterCommand.Three,
            "num4Button" => UnitConverterCommand.Four,
            "num5Button" => UnitConverterCommand.Five,
            "num6Button" => UnitConverterCommand.Six,
            "num7Button" => UnitConverterCommand.Seven,
            "num8Button" => UnitConverterCommand.Eight,
            "num9Button" => UnitConverterCommand.Nine,
            "decimalSeparatorButton" => UnitConverterCommand.Decimal,
            "converterNegateButton" => UnitConverterCommand.Negate,
            "backSpaceButton" => UnitConverterCommand.Backspace,
            "clearButton" or "clearEntryButton" => UnitConverterCommand.Clear,
            _ => (UnitConverterCommand?)null,
        };
        if (command is null)
        {
            return false;
        }
        Send(command.Value);
        return true;
    }

    public bool TryPaste(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }
        var normalized = _numberFormat.DelocalizeNumber(text.Trim()).Replace('\u2212', '-');
        if (!decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }
        _canonicalInput = normalized.TrimStart('+');
        NormalizeInput();
        Recalculate();
        return true;
    }

    [RelayCommand]
    public void SendCommand(string commandName) =>
        Send(Enum.Parse<UnitConverterCommand>(commandName, ignoreCase: false));

    [RelayCommand]
    public void Swap()
    {
        (SelectedFromCurrency, SelectedToCurrency) = (SelectedToCurrency, SelectedFromCurrency);
        Recalculate();
    }

    [RelayCommand]
    private Task Refresh() => LoadSelectedProviderAsync(forceRefresh: true);

    [RelayCommand]
    private void ConsentSelectedProvider()
    {
        SelectedProvider?.GrantConsent();
    }

    partial void OnSelectedProviderChanged(CurrencyProviderOption? value)
    {
        OnPropertyChanged(nameof(ProviderInformationText));
        if (value is null)
        {
            _loadCancellation?.Cancel();
            PresentNoSelection();
            return;
        }
        if (!value.IsConsented)
        {
            _loadCancellation?.Cancel();
            PresentConsentRequired(value);
            return;
        }
        if (_isActive)
        {
            _ = LoadSelectedProviderAsync(forceRefresh: false);
        }
    }

    partial void OnSelectedFromCurrencyChanged(CurrencyUnit? value) => Recalculate();
    partial void OnSelectedToCurrencyChanged(CurrencyUnit? value) => Recalculate();

    private async Task LoadSelectedProviderAsync(bool forceRefresh)
    {
        if (!_isActive)
        {
            return;
        }
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        _loadCancellation = new CancellationTokenSource();
        var cancellationToken = _loadCancellation.Token;
        var selected = SelectedProvider;
        if (selected is null)
        {
            PresentNoSelection();
            return;
        }
        if (!selected.IsConsented)
        {
            PresentConsentRequired(selected);
            return;
        }
        if (!_providers.TryGetValue(selected.Id, out var provider))
        {
            return;
        }

        NeedsConsent = false;
        IsLoading = true;
        StatusMessage = $"Updating rates from {selected.Name}…";
        try
        {
            if (forceRefresh || !_sessionCache.TryGetValue(selected.Id, out var cached))
            {
                var fetchedTable = await provider.FetchLatestAsync(cancellationToken);
                cached = new CachedRateTable(fetchedTable, DateTimeOffset.Now);
                _sessionCache[selected.Id] = cached;
            }
            if (cancellationToken.IsCancellationRequested || SelectedProvider?.Id != selected.Id)
            {
                return;
            }

            var table = cached.Table;
            var priorFrom = SelectedFromCurrency?.Code;
            var priorTo = SelectedToCurrency?.Code;
            var currencies = CurrencyUnitCatalog.Create(table.RatesPerBase.Keys);
            _synchronizing = true;
            try
            {
                Currencies.Clear();
                foreach (var currency in currencies)
                {
                    Currencies.Add(currency);
                }
                SelectedFromCurrency = currencies.FirstOrDefault(currency => currency.Code == priorFrom)
                    ?? currencies.FirstOrDefault(currency => currency.Code == table.BaseCurrency)
                    ?? currencies.FirstOrDefault();
                SelectedToCurrency = currencies.FirstOrDefault(currency => currency.Code == priorTo)
                    ?? currencies.FirstOrDefault(currency => currency.Code == "USD" && currency.Code != SelectedFromCurrency?.Code)
                    ?? currencies.FirstOrDefault(currency => currency.Code != SelectedFromCurrency?.Code);
            }
            finally
            {
                _synchronizing = false;
            }
            StatusMessage = $"{currencies.Count} currencies · published {table.PublishedDate:yyyy-MM-dd}";
            LastRefreshedText = $"Last refreshed on this device: {cached.RefreshedAt.ToLocalTime():g}";
            Recalculate();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or FormatException or JsonException)
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                Currencies.Clear();
                StatusMessage = $"{selected.Name} could not provide rates: {exception.Message}";
                RateSummary = "Try another consented source.";
                Recalculate();
            }
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsLoading = false;
            }
        }
    }

    private void Send(UnitConverterCommand command)
    {
        switch (command)
        {
        case >= UnitConverterCommand.Zero and <= UnitConverterCommand.Nine:
            var digit = (char)('0' + (int)command);
            _canonicalInput = _canonicalInput == "0" ? digit.ToString() : _canonicalInput + digit;
            break;
        case UnitConverterCommand.Decimal:
            if (!_canonicalInput.Contains('.', StringComparison.Ordinal))
            {
                _canonicalInput += ".";
            }
            break;
        case UnitConverterCommand.Negate:
            _canonicalInput = _canonicalInput.StartsWith("-", StringComparison.Ordinal)
                ? _canonicalInput[1..]
                : "-" + _canonicalInput;
            break;
        case UnitConverterCommand.Backspace:
            _canonicalInput = _canonicalInput.Length <= 1
                || (_canonicalInput.Length == 2 && _canonicalInput[0] == '-')
                ? "0"
                : _canonicalInput[..^1];
            break;
        case UnitConverterCommand.Clear:
        case UnitConverterCommand.Reset:
            _canonicalInput = "0";
            break;
        }
        if (_canonicalInput.Length > 32)
        {
            _canonicalInput = _canonicalInput[..32];
        }
        NormalizeInput();
        Recalculate();
    }

    private void NormalizeInput()
    {
        if (_canonicalInput is "-" or "-0")
        {
            _canonicalInput = "0";
        }
    }

    private void Recalculate()
    {
        FromDisplay = _numberFormat.LocalizeCanonicalNumber(_canonicalInput);
        if (_synchronizing
            || SelectedProvider is null
            || !_sessionCache.TryGetValue(SelectedProvider.Id, out var cached)
            || SelectedFromCurrency is null
            || SelectedToCurrency is null
            || !decimal.TryParse(_canonicalInput.TrimEnd('.'), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var input))
        {
            ToDisplay = "—";
            return;
        }

        var table = cached.Table;
        if (!table.RatesPerBase.TryGetValue(SelectedFromCurrency.Code, out var fromRate)
            || !table.RatesPerBase.TryGetValue(SelectedToCurrency.Code, out var toRate))
        {
            ToDisplay = "—";
            return;
        }
        var output = input / fromRate * toRate;
        ToDisplay = _numberFormat.LocalizeCanonicalNumber(output.ToString("0.############", CultureInfo.InvariantCulture));
        var factor = toRate / fromRate;
        RateSummary = $"1 {SelectedFromCurrency.Code} = {factor.ToString("0.########", CultureInfo.CurrentCulture)} {SelectedToCurrency.Code}";
    }

    private void OnProviderOptionChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (sender is not CurrencyProviderOption option
            || args.PropertyName is not (nameof(CurrencyProviderOption.IsConsented) or nameof(CurrencyProviderOption.IsVisibleInSelector)))
        {
            return;
        }
        ProviderPreferenceChanged?.Invoke(option);
        OnPropertyChanged(nameof(HasConsentedProviders));
        if (args.PropertyName == nameof(CurrencyProviderOption.IsVisibleInSelector))
        {
            RebuildSelectableProviders();
            if (!option.IsVisibleInSelector && SelectedProvider == option)
            {
                SelectedProvider = SelectableProviders.FirstOrDefault(provider => provider.IsConsented);
            }
        }
        if (args.PropertyName == nameof(CurrencyProviderOption.IsConsented) && SelectedProvider == option)
        {
            if (_isActive)
            {
                _ = LoadSelectedProviderAsync(forceRefresh: false);
            }
        }
    }

    private void RebuildSelectableProviders()
    {
        SelectableProviders.Clear();
        foreach (var provider in Providers.Where(provider => provider.IsVisibleInSelector))
        {
            SelectableProviders.Add(provider);
        }
    }

    private void PresentNoSelection()
    {
        IsLoading = false;
        NeedsConsent = false;
        Currencies.Clear();
        RateSummary = "No source selected.";
        StatusMessage = HasConsentedProviders
            ? "Choose a consented source. No provider has been contacted."
            : "No source has consent. Choose a source to review and consent; no provider has been contacted.";
        LastRefreshedText = "Never refreshed on this device.";
        Recalculate();
    }

    private void PresentConsentRequired(CurrencyProviderOption provider)
    {
        IsLoading = false;
        NeedsConsent = true;
        Currencies.Clear();
        RateSummary = "Consent required";
        StatusMessage = $"No data sent. Review what {provider.Name} receives, or switch source.";
        LastRefreshedText = "Never refreshed on this device.";
        Recalculate();
    }

    public void Dispose()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        foreach (var provider in Providers)
        {
            provider.PropertyChanged -= OnProviderOptionChanged;
        }
    }
}
