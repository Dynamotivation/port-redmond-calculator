#include "CalcManager/UnitConverter.h"
#include "CalcViewModel/DataLoaders/UnitConverterDataConstants.h"
#include "CalcViewModel/DataLoaders/UnitConverterDataLoader.h"
#include "CalculatorNative/Utf8.h"
#include "ResourceKeyCompat.h"

#include <algorithm>
#include <cmath>
#include <fstream>
#include <iostream>
#include <regex>
#include <string>
#include <unordered_map>
#include <utility>

namespace
{
    using CalculatorApp::ViewModel::Common::UnitConverterDataLoader;
    using CalculatorApp::ViewModel::Common::UnitConverterUnits;
    using UnitConversionManager::Category;
    using UnitConversionManager::IConverterDataLoader;
    using UnitConversionManager::Unit;

    std::string DecodeXml(std::string value)
    {
        const std::pair<std::string_view, std::string_view> entities[] = {
            { "&lt;", "<" }, { "&gt;", ">" }, { "&quot;", "\"" }, { "&apos;", "'" }, { "&amp;", "&" }
        };
        for (const auto& [encoded, decoded] : entities)
        {
            std::size_t offset = 0;
            while ((offset = value.find(encoded, offset)) != std::string::npos)
            {
                value.replace(offset, encoded.size(), decoded);
                offset += decoded.size();
            }
        }
        return value;
    }

    std::unordered_map<std::wstring, std::wstring> LoadResources()
    {
        const std::string path = std::string{ CALCULATOR_SOURCE_DIR } + "/src/Calculator/Resources/en-US/Resources.resw";
        std::ifstream input(path);
        const std::string xml{ std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
        if (xml.empty())
        {
            throw std::runtime_error("failed to load " + path);
        }

        const std::regex dataPattern{ R"xml(<data\s+name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)</value>)xml" };
        std::unordered_map<std::wstring, std::wstring> values;
        for (auto match = std::sregex_iterator(xml.begin(), xml.end(), dataPattern); match != std::sregex_iterator{}; ++match)
        {
            values.emplace(
                CalculatorNative::Utf8::ToWide(DecodeXml((*match)[1].str())),
                CalculatorNative::Utf8::ToWide(DecodeXml((*match)[2].str())));
        }
        return values;
    }

    std::shared_ptr<IConverterDataLoader> CreateLoader(
        const std::wstring& region,
        const std::unordered_map<std::wstring, std::wstring>& resources)
    {
        return std::make_shared<UnitConverterDataLoader>(region, [&resources](std::wstring_view key) {
            const auto found = resources.find(Calculator::PortableCompat::NormalizeResourceKey(key));
            return found == resources.end() ? std::wstring{} : found->second;
        });
    }

    const Category& FindCategory(const std::vector<Category>& categories, int id)
    {
        return *std::find_if(categories.begin(), categories.end(), [id](const Category& category) { return category.id == id; });
    }

    const Unit& FindUnit(const std::vector<Unit>& units, int id)
    {
        return *std::find_if(units.begin(), units.end(), [id](const Unit& unit) { return unit.id == id; });
    }

    bool HasUnit(const std::vector<Unit>& units, int id)
    {
        return std::any_of(units.begin(), units.end(), [id](const Unit& unit) { return unit.id == id; });
    }
}

int main()
{
    const auto resources = LoadResources();
    auto usLoader = CreateLoader(L"US", resources);
    usLoader->LoadData();

    const auto categories = usLoader->GetOrderedCategories();
    if (categories.size() != 13 || categories.front().id != 16 || categories.front().name != L"Currency")
    {
        std::cerr << "portable unit categories do not match the Microsoft catalog\n";
        return 1;
    }
    if (usLoader->SupportsCategory(categories.front()) || !usLoader->SupportsCategory(FindCategory(categories, 9)))
    {
        std::cerr << "portable category support incorrectly includes currency\n";
        return 1;
    }

    const auto areaUnits = usLoader->GetOrderedUnits(FindCategory(categories, 9));
    const auto& squareFoot = FindUnit(areaUnits, UnitConverterUnits::Area_SquareFoot);
    const auto& squareMeter = FindUnit(areaUnits, UnitConverterUnits::Area_SquareMeter);
    if (squareFoot.name != L"Square feet" || squareMeter.abbreviation != L"m²" || squareFoot.isConversionSource
        || !squareFoot.isConversionTarget || !squareMeter.isConversionSource || squareMeter.isConversionTarget
        || HasUnit(areaUnits, UnitConverterUnits::Area_Pyeong))
    {
        std::cerr << "US regional defaults or localized unit metadata are incorrect\n";
        return 1;
    }

    const auto temperatureUnits = usLoader->GetOrderedUnits(FindCategory(categories, 7));
    const auto& celsius = FindUnit(temperatureUnits, UnitConverterUnits::Temperature_DegreesCelsius);
    const auto& fahrenheit = FindUnit(temperatureUnits, UnitConverterUnits::Temperature_DegreesFahrenheit);
    const auto temperatureRatios = usLoader->LoadOrderedRatios(celsius);
    const auto celsiusToFahrenheit = temperatureRatios.at(fahrenheit);
    if (std::abs(celsiusToFahrenheit.ratio - 1.8) > 1e-12 || std::abs(celsiusToFahrenheit.offset - 32.0) > 1e-12
        || celsiusToFahrenheit.offsetFirst || celsius.isConversionTarget || !fahrenheit.isConversionTarget)
    {
        std::cerr << "explicit temperature conversion data is incorrect\n";
        return 1;
    }

    auto japanLoader = CreateLoader(L"JP", resources);
    japanLoader->LoadData();
    const auto japanCategories = japanLoader->GetOrderedCategories();
    if (!HasUnit(japanLoader->GetOrderedUnits(FindCategory(japanCategories, 9)), UnitConverterUnits::Area_Pyeong))
    {
        std::cerr << "Japanese regional Pyeong unit was not included\n";
        return 1;
    }

    auto germanyLoader = CreateLoader(L"DE", resources);
    germanyLoader->LoadData();
    const auto germanyCategories = germanyLoader->GetOrderedCategories();
    const auto germanyArea = germanyLoader->GetOrderedUnits(FindCategory(germanyCategories, 9));
    if (!FindUnit(germanyArea, UnitConverterUnits::Area_SquareMeter).isConversionTarget
        || FindUnit(germanyArea, UnitConverterUnits::Area_SquareFoot).isConversionTarget)
    {
        std::cerr << "SI regional defaults are incorrect\n";
        return 1;
    }

    UnitConversionManager::UnitConverter converter(usLoader);
    converter.Initialize();
    if (converter.GetCategories().size() != categories.size())
    {
        std::cerr << "portable data loader did not initialize the real conversion engine\n";
        return 1;
    }

    return 0;
}
