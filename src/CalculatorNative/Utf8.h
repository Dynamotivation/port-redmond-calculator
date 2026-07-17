#pragma once

#include <string>
#include <string_view>

namespace CalculatorNative::Utf8
{
    std::wstring ToWide(std::string_view value);
    std::string FromWide(std::wstring_view value);
}
