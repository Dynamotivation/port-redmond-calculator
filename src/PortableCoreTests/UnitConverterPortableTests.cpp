#include "CalcManager/Command.h"
#include "CalcManager/UnitConverter.h"

#include <iostream>
#include <memory>
#include <string>
#include <tuple>
#include <unordered_map>
#include <utility>
#include <vector>

using namespace UnitConversionManager;

namespace
{
    constexpr int LengthCategoryId = 1;
    constexpr int TemperatureCategoryId = 2;
    constexpr int CurrencyCategoryId = 3;

    const Category Length{ LengthCategoryId, L"Length", true };
    const Category Temperature{ TemperatureCategoryId, L"Temperature", true };
    const Category Currency{ CurrencyCategoryId, L"Currency", false };

    const Unit Inches{ 1, L"Inches", L"in", true, true, false };
    const Unit Feet{ 2, L"Feet", L"ft", false, false, false };
    const Unit Celsius{ 3, L"Celsius", L"°C", true, true, false };
    const Unit Fahrenheit{ 4, L"Fahrenheit", L"°F", false, false, false };
    const Unit UsDollar{ 5, L"US dollar", L"USD", true, true, false };
    const Unit Euro{ 6, L"Euro", L"EUR", false, false, false };

    bool Expect(bool condition, std::string_view description)
    {
        if (!condition)
        {
            std::cerr << "failed: " << description << '\n';
        }
        return condition;
    }

    bool ExpectEqual(std::wstring_view actual, std::wstring_view expected, std::wstring_view description)
    {
        if (actual == expected)
        {
            return true;
        }

        std::wcerr << description << L": expected '" << expected << L"', got '" << actual << L"'\n";
        return false;
    }

    concurrency::task<bool> ReadyAsync(bool value)
    {
        return concurrency::task_from_result(value);
    }

    class StaticDataLoader final : public IConverterDataLoader
    {
    public:
        StaticDataLoader()
        {
            m_units[LengthCategoryId] = { Inches, Feet };
            m_units[TemperatureCategoryId] = { Celsius, Fahrenheit };

            m_ratios[Inches] = {
                { Inches, ConversionData{ 1.0, 0.0, false } },
                { Feet, ConversionData{ 1.0 / 12.0, 0.0, false } },
            };
            m_ratios[Feet] = {
                { Inches, ConversionData{ 12.0, 0.0, false } },
                { Feet, ConversionData{ 1.0, 0.0, false } },
            };
            m_ratios[Celsius] = {
                { Celsius, ConversionData{ 1.0, 0.0, false } },
                { Fahrenheit, ConversionData{ 1.8, 32.0, false } },
            };
            m_ratios[Fahrenheit] = {
                { Celsius, ConversionData{ 5.0 / 9.0, -32.0, true } },
                { Fahrenheit, ConversionData{ 1.0, 0.0, false } },
            };
        }

        void LoadData() override { ++LoadCount; }
        std::vector<Category> GetOrderedCategories() override { return { Length, Temperature, Currency }; }
        std::vector<Unit> GetOrderedUnits(const Category& category) override { return m_units[category.id]; }
        std::unordered_map<Unit, ConversionData, UnitHash> LoadOrderedRatios(const Unit& unit) override { return m_ratios[unit]; }
        bool SupportsCategory(const Category& category) override { return category.id != CurrencyCategoryId; }

        int LoadCount = 0;

    private:
        CategoryToUnitVectorMap m_units;
        UnitToUnitToConversionDataMap m_ratios;
    };

    class CurrencyDataLoader final : public IConverterDataLoader, public ICurrencyConverterDataLoader
    {
    public:
        CurrencyDataLoader()
        {
            m_ratios[UsDollar] = {
                { UsDollar, ConversionData{ 1.0, 0.0, false } },
                { Euro, ConversionData{ 0.9, 0.0, false } },
            };
            m_ratios[Euro] = {
                { UsDollar, ConversionData{ 1.0 / 0.9, 0.0, false } },
                { Euro, ConversionData{ 1.0, 0.0, false } },
            };
        }

        void LoadData() override {}
        std::vector<Category> GetOrderedCategories() override { return { Currency }; }
        std::vector<Unit> GetOrderedUnits(const Category&) override { return { UsDollar, Euro }; }
        std::unordered_map<Unit, ConversionData, UnitHash> LoadOrderedRatios(const Unit& unit) override { return m_ratios[unit]; }
        bool SupportsCategory(const Category& category) override { return category.id == CurrencyCategoryId; }

        void SetViewModelCallback(const std::shared_ptr<IViewModelCurrencyCallback>& callback) override { m_callback = callback; }
        std::pair<std::wstring, std::wstring> GetCurrencySymbols(const Unit&, const Unit&) override { return { L"$", L"€" }; }
        std::pair<std::wstring, std::wstring> GetCurrencyRatioEquality(const Unit&, const Unit&) override
        {
            return { L"1 USD = 0.9 EUR", L"1 US dollar equals 0.9 euro" };
        }
        std::wstring GetCurrencyTimestamp() override { return L"2026-07-17T00:00:00Z"; }
        concurrency::task<bool> TryLoadDataFromCacheAsync() override { return ReadyAsync(true); }
        concurrency::task<bool> TryLoadDataFromWebAsync() override { return ReadyAsync(true); }
        concurrency::task<bool> TryLoadDataFromWebOverrideAsync() override { return ReadyAsync(true); }

    private:
        UnitToUnitToConversionDataMap m_ratios;
        std::shared_ptr<IViewModelCurrencyCallback> m_callback;
    };

    class TestViewModelCallback final : public IUnitConverterVMCallback
    {
    public:
        void DisplayCallback(const std::wstring& from, const std::wstring& to) override
        {
            From = from;
            To = to;
        }

        void SuggestedValueCallback(const std::vector<std::tuple<std::wstring, Unit>>& values) override { Suggested = values; }
        void MaxDigitsReached() override { ++MaxDigitsReachedCount; }

        std::wstring From;
        std::wstring To;
        std::vector<std::tuple<std::wstring, Unit>> Suggested;
        int MaxDigitsReachedCount = 0;
    };
}

int main()
{
    auto staticLoader = std::make_shared<StaticDataLoader>();
    auto currencyLoader = std::make_shared<CurrencyDataLoader>();
    auto callback = std::make_shared<TestViewModelCallback>();
    auto converter = std::make_shared<UnitConverter>(staticLoader, currencyLoader);
    converter->SetViewModelCallback(callback);

    converter->Initialize();
    if (!Expect(staticLoader->LoadCount == 1, "data loader initialization"))
    {
        return 1;
    }

    converter->SetCurrentCategory(Length);
    converter->SetCurrentUnitTypes(Inches, Feet);
    converter->SendCommand(Command::Three);
    converter->SendCommand(Command::Zero);
    if (!ExpectEqual(callback->From, L"30", L"length input") || !ExpectEqual(callback->To, L"2.5", L"inch-to-foot conversion"))
    {
        return 1;
    }

    converter->SendCommand(Command::Clear);
    converter->SetCurrentCategory(Temperature);
    converter->SetCurrentUnitTypes(Celsius, Fahrenheit);
    converter->SendCommand(Command::One);
    converter->SendCommand(Command::Zero);
    converter->SendCommand(Command::Zero);
    if (!ExpectEqual(callback->To, L"212", L"offset-first/ratio temperature conversion"))
    {
        return 1;
    }

    constexpr std::wstring_view quotedInput = L"{p}Weig;[ht|";
    if (!ExpectEqual(UnitConverter::Unquote(UnitConverter::Quote(quotedInput)), quotedInput, L"preference escaping round trip"))
    {
        return 1;
    }

    converter->SendCommand(Command::Clear);
    for (int digit = 0; digit < 16; ++digit)
    {
        converter->SendCommand(Command::One);
    }
    if (!Expect(callback->MaxDigitsReachedCount == 1, "maximum digit callback"))
    {
        return 1;
    }

    const auto [didRefresh, timestamp] = converter->RefreshCurrencyRatios().get();
    if (!Expect(didRefresh, "portable asynchronous currency refresh")
        || !ExpectEqual(timestamp, L"2026-07-17T00:00:00Z", L"currency timestamp propagation"))
    {
        return 1;
    }

    return 0;
}
