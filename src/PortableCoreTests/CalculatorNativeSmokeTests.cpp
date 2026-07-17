#include "CalculatorNative.h"

#include "CalcManager/Command.h"

#include <algorithm>
#include <fstream>
#include <cctype>
#include <iostream>
#include <regex>
#include <string>
#include <utility>
#include <vector>

namespace
{
    struct CallbackState
    {
        std::string primaryDisplay;
        std::string expressionDisplay;
        int inputChangeCount = 0;
    };

    void OnPrimaryDisplay(void* context, const char* value, int32_t)
    {
        static_cast<CallbackState*>(context)->primaryDisplay = value;
    }

    void OnExpressionDisplay(void* context, const char* value)
    {
        static_cast<CallbackState*>(context)->expressionDisplay = value;
    }

    void OnInputChanged(void* context)
    {
        ++static_cast<CallbackState*>(context)->inputChangeCount;
    }

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

    std::vector<std::pair<std::string, std::string>> LoadResources()
    {
        const std::string path = std::string{ CALCULATOR_SOURCE_DIR } + "/src/Calculator/Resources/en-US/CEngineStrings.resw";
        std::ifstream input(path);
        const std::string xml{ std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
        if (xml.empty())
        {
            throw std::runtime_error("failed to load " + path);
        }

        const std::regex dataPattern{ R"xml(<data\s+name="([^"]+)"[^>]*>\s*<value>([\s\S]*?)</value>)xml" };
        std::vector<std::pair<std::string, std::string>> values;
        for (auto match = std::sregex_iterator(xml.begin(), xml.end(), dataPattern); match != std::sregex_iterator{}; ++match)
        {
            values.emplace_back(DecodeXml((*match)[1].str()), DecodeXml((*match)[2].str()));
        }
        return values;
    }

    std::string WithoutWhitespace(std::string value)
    {
        value.erase(std::remove_if(value.begin(), value.end(), [](unsigned char character) { return std::isspace(character) != 0; }), value.end());
        return value;
    }

    template <typename Getter>
    std::string ReadString(Getter&& getter)
    {
        std::size_t required = 0;
        if (getter(nullptr, 0, &required) != CALCULATOR_STATUS_OK)
        {
            return {};
        }
        std::string value(required, '\0');
        if (getter(value.data(), value.size(), &required) != CALCULATOR_STATUS_OK)
        {
            return {};
        }
        value.resize(required - 1);
        return value;
    }
}

int main()
{
    const auto resourceValues = LoadResources();
    std::vector<calculator_resource_entry> resources;
    resources.reserve(resourceValues.size());
    for (const auto& [key, value] : resourceValues)
    {
        resources.push_back({ key.c_str(), value.c_str() });
    }

    calculator_handle* handle = nullptr;
    CallbackState callbackState;
    const calculator_callbacks callbacks = {
        &callbackState,
        OnPrimaryDisplay,
        OnExpressionDisplay,
        OnInputChanged,
    };
    const auto createStatus = calculator_create(resources.data(), resources.size(), &callbacks, &handle);
    if (createStatus != CALCULATOR_STATUS_OK)
    {
        std::cerr << "calculator_create failed: " << calculator_get_last_error() << '\n';
        return 1;
    }

    using CalculationManager::Command;
    const Command commands[] = { Command::Command2, Command::CommandADD, Command::Command3, Command::CommandEQU };
    for (const auto command : commands)
    {
        if (calculator_send_command(handle, static_cast<int32_t>(command)) != CALCULATOR_STATUS_OK)
        {
            std::cerr << "calculator_send_command failed: " << calculator_get_last_error() << '\n';
            calculator_destroy(handle);
            return 1;
        }
    }

    const auto display = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_primary_display(handle, buffer, size, required);
    });
    if (display != "5")
    {
        std::cerr << "expected display 5, got " << display << '\n';
        calculator_destroy(handle);
        return 1;
    }
    if (callbackState.primaryDisplay != "5" || callbackState.expressionDisplay != "2 + 3=" || callbackState.inputChangeCount == 0)
    {
        std::cerr << "native callbacks did not reflect the completed calculation\n";
        calculator_destroy(handle);
        return 1;
    }

    std::size_t historyCount = 0;
    if (calculator_get_history_count(handle, &historyCount) != CALCULATOR_STATUS_OK || historyCount != 1)
    {
        std::cerr << "native history did not contain the completed calculation\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto historyExpression = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_history_expression(handle, 0, buffer, size, required);
    });
    const auto historyResult = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_history_result(handle, 0, buffer, size, required);
    });
    if (WithoutWhitespace(historyExpression) != "2+3=" || WithoutWhitespace(historyResult) != "5")
    {
        std::cerr << "unexpected native history: " << historyExpression << " = " << historyResult << '\n';
        calculator_destroy(handle);
        return 1;
    }

    if (calculator_memory_store(handle) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "failed to store memory\n";
        calculator_destroy(handle);
        return 1;
    }
    std::size_t memoryCount = 0;
    const auto memoryValue = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_memory_value(handle, 0, buffer, size, required);
    });
    if (calculator_get_memory_count(handle, &memoryCount) != CALCULATOR_STATUS_OK || memoryCount != 1 || memoryValue != "5")
    {
        std::cerr << "native memory did not contain 5\n";
        calculator_destroy(handle);
        return 1;
    }
    if (calculator_memory_add(handle, 0) != CALCULATOR_STATUS_OK || calculator_memory_recall(handle, 0) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "native memory operations failed\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto addedMemory = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_memory_value(handle, 0, buffer, size, required);
    });
    if (addedMemory != "10" || calculator_memory_clear(handle, 0) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "native memory add/clear produced " << addedMemory << '\n';
        calculator_destroy(handle);
        return 1;
    }

    calculator_event_state events{};
    if (calculator_get_event_state(handle, &events) != CALCULATOR_STATUS_OK || events.binary_operator_received_count == 0
        || events.history_item_added_count != 1 || events.memory_item_changed_count == 0 || events.input_changed_count == 0)
    {
        std::cerr << "native event state did not reflect calculator activity\n";
        calculator_destroy(handle);
        return 1;
    }
    if (calculator_history_clear(handle) != CALCULATOR_STATUS_OK
        || calculator_get_history_count(handle, &historyCount) != CALCULATOR_STATUS_OK || historyCount != 0)
    {
        std::cerr << "native history clear failed\n";
        calculator_destroy(handle);
        return 1;
    }

    calculator_destroy(handle);
    return 0;
}
