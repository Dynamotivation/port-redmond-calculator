#pragma once

#include <string>
#include <string_view>

namespace Calculator::PortableCompat
{
    inline std::wstring NormalizeResourceKey(std::wstring_view key)
    {
        // Microsoft's unit catalog contains two historical keys with a trailing
        // space. Windows resource lookup tolerates them; .resw map lookup does
        // not. Preserve the catalog source and normalize at the platform API
        // boundary.
        while (!key.empty() && key.back() == L' ')
        {
            key.remove_suffix(1);
        }
        return std::wstring{ key };
    }
}
