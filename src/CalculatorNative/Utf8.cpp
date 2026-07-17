#include "Utf8.h"

#include <climits>
#include <cstdint>
#include <stdexcept>

namespace
{
    constexpr char32_t ReplacementCharacter = 0xfffd;

    void AppendUtf8(std::string& result, char32_t codePoint)
    {
        if (codePoint <= 0x7f)
        {
            result.push_back(static_cast<char>(codePoint));
        }
        else if (codePoint <= 0x7ff)
        {
            result.push_back(static_cast<char>(0xc0 | (codePoint >> 6)));
            result.push_back(static_cast<char>(0x80 | (codePoint & 0x3f)));
        }
        else if (codePoint <= 0xffff)
        {
            result.push_back(static_cast<char>(0xe0 | (codePoint >> 12)));
            result.push_back(static_cast<char>(0x80 | ((codePoint >> 6) & 0x3f)));
            result.push_back(static_cast<char>(0x80 | (codePoint & 0x3f)));
        }
        else
        {
            result.push_back(static_cast<char>(0xf0 | (codePoint >> 18)));
            result.push_back(static_cast<char>(0x80 | ((codePoint >> 12) & 0x3f)));
            result.push_back(static_cast<char>(0x80 | ((codePoint >> 6) & 0x3f)));
            result.push_back(static_cast<char>(0x80 | (codePoint & 0x3f)));
        }
    }

    char32_t DecodeUtf8(std::string_view value, std::size_t& offset)
    {
        const auto first = static_cast<unsigned char>(value[offset++]);
        if (first <= 0x7f)
        {
            return first;
        }

        int continuationCount = 0;
        char32_t codePoint = 0;
        if ((first & 0xe0) == 0xc0)
        {
            continuationCount = 1;
            codePoint = first & 0x1f;
        }
        else if ((first & 0xf0) == 0xe0)
        {
            continuationCount = 2;
            codePoint = first & 0x0f;
        }
        else if ((first & 0xf8) == 0xf0)
        {
            continuationCount = 3;
            codePoint = first & 0x07;
        }
        else
        {
            return ReplacementCharacter;
        }

        if (offset + continuationCount > value.size())
        {
            offset = value.size();
            return ReplacementCharacter;
        }

        for (int i = 0; i < continuationCount; ++i)
        {
            const auto next = static_cast<unsigned char>(value[offset]);
            if ((next & 0xc0) != 0x80)
            {
                return ReplacementCharacter;
            }
            ++offset;
            codePoint = (codePoint << 6) | (next & 0x3f);
        }

        if (codePoint > 0x10ffff || (codePoint >= 0xd800 && codePoint <= 0xdfff))
        {
            return ReplacementCharacter;
        }
        return codePoint;
    }
}

namespace CalculatorNative::Utf8
{
    std::wstring ToWide(std::string_view value)
    {
        std::wstring result;
        result.reserve(value.size());

        std::size_t offset = 0;
        while (offset < value.size())
        {
            const char32_t codePoint = DecodeUtf8(value, offset);
#if WCHAR_MAX <= 0xffff
            if (codePoint > 0xffff)
            {
                const char32_t adjusted = codePoint - 0x10000;
                result.push_back(static_cast<wchar_t>(0xd800 + (adjusted >> 10)));
                result.push_back(static_cast<wchar_t>(0xdc00 + (adjusted & 0x3ff)));
            }
            else
#endif
            {
                result.push_back(static_cast<wchar_t>(codePoint));
            }
        }
        return result;
    }

    std::string FromWide(std::wstring_view value)
    {
        std::string result;
        result.reserve(value.size());

        for (std::size_t i = 0; i < value.size(); ++i)
        {
            char32_t codePoint = static_cast<char32_t>(value[i]);
#if WCHAR_MAX <= 0xffff
            if (codePoint >= 0xd800 && codePoint <= 0xdbff && i + 1 < value.size())
            {
                const char32_t low = static_cast<char32_t>(value[i + 1]);
                if (low >= 0xdc00 && low <= 0xdfff)
                {
                    codePoint = 0x10000 + ((codePoint - 0xd800) << 10) + (low - 0xdc00);
                    ++i;
                }
            }
#endif
            if (codePoint >= 0xd800 && codePoint <= 0xdfff)
            {
                codePoint = ReplacementCharacter;
            }
            AppendUtf8(result, codePoint);
        }
        return result;
    }
}
