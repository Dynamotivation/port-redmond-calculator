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

    std::vector<std::pair<std::string, std::string>> LoadResources(const std::string& fileName)
    {
        const std::string path = std::string{ CALCULATOR_SOURCE_DIR } + "/src/Calculator/Resources/en-US/" + fileName;
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
    const auto resourceValues = LoadResources("CEngineStrings.resw");
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
    if (calculator_history_recall(handle, 0, 0) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "native history recall failed\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto recalledDisplay = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_primary_display(handle, buffer, size, required);
    });
    if (recalledDisplay != "5"
        || calculator_send_command(handle, static_cast<int32_t>(Command::CommandADD)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::Command5)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::CommandEQU)) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "native history recall did not restore its display\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto recalledResult = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_primary_display(handle, buffer, size, required);
    });
    if (recalledResult != "10")
    {
        std::cerr << "native history recall did not restore an executable value\n";
        calculator_destroy(handle);
        return 1;
    }
    if (calculator_send_command(handle, static_cast<int32_t>(Command::CommandCLEAR)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::Command2)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::CommandADD)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::Command3)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::CommandEQU)) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "failed to restore memory test value after history recall\n";
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
        || events.history_item_added_count < 3 || events.memory_item_changed_count == 0 || events.input_changed_count == 0)
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

    if (calculator_set_mode(handle, CALCULATOR_MODE_PROGRAMMER) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::Command1)) != CALCULATOR_STATUS_OK
        || calculator_send_command(handle, static_cast<int32_t>(Command::Command5)) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "failed to enter a programmer-mode value\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto hexadecimal = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_result_for_radix(handle, 16, 64, 1, buffer, size, required);
    });
    const auto binary = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_result_for_radix(handle, 2, 64, 0, buffer, size, required);
    });
    if (hexadecimal != "F" || binary != "1111")
    {
        std::cerr << "unexpected programmer radix values: " << hexadecimal << " / " << binary << '\n';
        calculator_destroy(handle);
        return 1;
    }
    if (calculator_send_command(handle, static_cast<int32_t>(Command::CommandBINPOS0)) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "programmer bit edit failed\n";
        calculator_destroy(handle);
        return 1;
    }
    const auto flippedHexadecimal = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_result_for_radix(handle, 16, 64, 1, buffer, size, required);
    });
    if (flippedHexadecimal != "E")
    {
        std::cerr << "programmer bit edit produced " << flippedHexadecimal << '\n';
        calculator_destroy(handle);
        return 1;
    }

    const Command programmerCommands[] = {
        Command::CommandCLEAR, static_cast<Command>(313), Command::CommandF, Command::CommandAnd,
        Command::Command3, Command::CommandEQU,
    };
    for (const auto command : programmerCommands)
    {
        if (calculator_send_command(handle, static_cast<int32_t>(command)) != CALCULATOR_STATUS_OK)
        {
            std::cerr << "programmer bitwise operation failed\n";
            calculator_destroy(handle);
            return 1;
        }
    }
    const auto bitwiseHexadecimal = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_result_for_radix(handle, 16, 64, 1, buffer, size, required);
    });
    if (bitwiseHexadecimal != "3")
    {
        std::cerr << "programmer AND produced " << bitwiseHexadecimal << '\n';
        calculator_destroy(handle);
        return 1;
    }

    const Command shiftCommands[] = {
        Command::CommandCLEAR, Command::Command1, Command::CommandLSHF,
        Command::Command1, Command::CommandEQU,
    };
    for (const auto command : shiftCommands)
    {
        if (calculator_send_command(handle, static_cast<int32_t>(command)) != CALCULATOR_STATUS_OK)
        {
            std::cerr << "programmer shift operation failed\n";
            calculator_destroy(handle);
            return 1;
        }
    }
    const auto shiftedHexadecimal = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_get_result_for_radix(handle, 16, 64, 1, buffer, size, required);
    });
    if (shiftedHexadecimal != "2")
    {
        std::cerr << "programmer left shift produced " << shiftedHexadecimal << '\n';
        calculator_destroy(handle);
        return 1;
    }

    calculator_destroy(handle);

    const auto unitResourceValues = LoadResources("Resources.resw");
    std::vector<calculator_resource_entry> unitResources;
    unitResources.reserve(unitResourceValues.size());
    for (const auto& [key, value] : unitResourceValues)
    {
        unitResources.push_back({ key.c_str(), value.c_str() });
    }

    calculator_unit_converter_handle* unitConverter = nullptr;
    if (calculator_unit_converter_create(unitResources.data(), unitResources.size(), "US", &unitConverter) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "calculator_unit_converter_create failed: " << calculator_get_last_error() << '\n';
        return 1;
    }

    std::size_t categoryCount = 0;
    if (calculator_unit_converter_get_category_count(unitConverter, &categoryCount) != CALCULATOR_STATUS_OK || categoryCount != 12)
    {
        std::cerr << "portable unit ABI did not expose the non-currency categories\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }
    if (calculator_unit_converter_select_category(unitConverter, 7) != CALCULATOR_STATUS_OK)
    {
        std::cerr << "portable unit ABI could not select temperature\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }

    std::size_t unitCount = 0;
    int32_t selectedFrom = -1;
    int32_t selectedTo = -1;
    if (calculator_unit_converter_get_unit_count(unitConverter, &unitCount) != CALCULATOR_STATUS_OK || unitCount != 3
        || calculator_unit_converter_get_selected_units(unitConverter, &selectedFrom, &selectedTo) != CALCULATOR_STATUS_OK
        || selectedFrom != 46 || selectedTo != 47)
    {
        std::cerr << "US temperature defaults were not exposed through the native ABI\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }

    const calculator_unit_command unitCommands[] = {
        CALCULATOR_UNIT_COMMAND_ONE, CALCULATOR_UNIT_COMMAND_ZERO, CALCULATOR_UNIT_COMMAND_ZERO
    };
    for (const auto command : unitCommands)
    {
        if (calculator_unit_converter_send_command(unitConverter, command) != CALCULATOR_STATUS_OK)
        {
            std::cerr << "portable unit ABI command failed\n";
            calculator_unit_converter_destroy(unitConverter);
            return 1;
        }
    }
    const auto fromDisplay = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_unit_converter_get_from_display(unitConverter, buffer, size, required);
    });
    const auto toDisplay = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_unit_converter_get_to_display(unitConverter, buffer, size, required);
    });
    if (fromDisplay != "100" || toDisplay != "212")
    {
        std::cerr << "expected 100 Celsius = 212 Fahrenheit, got " << fromDisplay << " = " << toDisplay << '\n';
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }

    std::size_t suggestionCount = 0;
    if (calculator_unit_converter_get_suggestion_count(unitConverter, &suggestionCount) != CALCULATOR_STATUS_OK || suggestionCount == 0)
    {
        std::cerr << "portable unit ABI dropped converter suggestions\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }
    int32_t suggestionUnitId = -1;
    const auto suggestion = ReadString([&](char* buffer, size_t size, size_t* required) {
        return calculator_unit_converter_get_suggestion(unitConverter, 0, &suggestionUnitId, buffer, size, required);
    });
    if (suggestion.empty() || suggestionUnitId < 0)
    {
        std::cerr << "portable unit ABI suggestion payload is empty\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }

    if (calculator_unit_converter_send_command(unitConverter, CALCULATOR_UNIT_COMMAND_CLEAR) != CALCULATOR_STATUS_OK)
    {
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }
    for (int index = 0; index < 20; ++index)
    {
        calculator_unit_converter_send_command(unitConverter, CALCULATOR_UNIT_COMMAND_NINE);
    }
    uint64_t maxDigitsCount = 0;
    if (calculator_unit_converter_get_max_digits_reached_count(unitConverter, &maxDigitsCount) != CALCULATOR_STATUS_OK || maxDigitsCount == 0)
    {
        std::cerr << "portable unit ABI dropped max-digit events\n";
        calculator_unit_converter_destroy(unitConverter);
        return 1;
    }
    calculator_unit_converter_destroy(unitConverter);
    return 0;
}
