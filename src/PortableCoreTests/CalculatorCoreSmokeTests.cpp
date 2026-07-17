#include "CalcManager/Header Files/Rational.h"
#include "CalcManager/NumberFormattingUtils.h"
#include "CalcManager/Ratpack/ratpak.h"

#include <iostream>
#include <string>

namespace
{
    bool ExpectEqual(std::wstring_view actual, std::wstring_view expected, std::wstring_view description)
    {
        if (actual == expected)
        {
            return true;
        }

        std::wcerr << description << L": expected '" << expected << L"', got '" << actual << L"'\n";
        return false;
    }
}

int main()
{
    constexpr uint32_t radix = 10;
    constexpr int32_t precision = 128;

    ChangeConstants(radix, precision);

    const auto result = CalcEngine::Rational{ 2 } + CalcEngine::Rational{ 3 };
    if (!ExpectEqual(result.ToString(radix, NumberFormat::Float, precision), L"5", L"RatPack addition"))
    {
        return 1;
    }

    std::wstring formatted = L"12.3400";
    UnitConversionManager::NumberFormattingUtils::TrimTrailingZeros(formatted);
    if (!ExpectEqual(formatted, L"12.34", L"number formatting"))
    {
        return 1;
    }

    return 0;
}
