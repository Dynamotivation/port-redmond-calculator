#include "CalculatorNative.h"

#include "Utf8.h"
#include "CalcManager/CalculatorManager.h"
#include "CalcManager/CalculatorResource.h"
#include "CalcManager/Command.h"
#include "CalcManager/ExpressionCommandInterface.h"
#include "CalcManager/UnitConverter.h"
#include "CalcViewModel/DataLoaders/UnitConverterDataLoader.h"

#include <algorithm>
#include <cstring>
#include <exception>
#include <memory>
#include <stdexcept>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using CalculationManager::CalculatorManager;
using CalculationManager::Command;
using CalculationManager::IResourceProvider;
namespace UCM = UnitConversionManager;
using CalculatorApp::ViewModel::Common::UnitConverterDataLoader;

static_assert(static_cast<int>(UCM::Command::Zero) == CALCULATOR_UNIT_COMMAND_ZERO);
static_assert(static_cast<int>(UCM::Command::Decimal) == CALCULATOR_UNIT_COMMAND_DECIMAL);
static_assert(static_cast<int>(UCM::Command::None) == CALCULATOR_UNIT_COMMAND_NONE);

namespace
{
    thread_local std::string LastError;

    class ResourceProvider final : public IResourceProvider
    {
    public:
        ResourceProvider(const calculator_resource_entry* resources, std::size_t count)
        {
            for (std::size_t i = 0; i < count; ++i)
            {
                if (resources[i].key_utf8 == nullptr || resources[i].value_utf8 == nullptr)
                {
                    throw std::invalid_argument("resource keys and values must not be null");
                }
                m_resources.emplace(
                    CalculatorNative::Utf8::ToWide(resources[i].key_utf8),
                    CalculatorNative::Utf8::ToWide(resources[i].value_utf8));
            }
        }

        std::wstring GetCEngineString(std::wstring_view id) override
        {
            return GetString(id);
        }

        std::wstring GetString(std::wstring_view id) const
        {
            const auto value = m_resources.find(std::wstring{ id });
            return value == m_resources.end() ? std::wstring{} : value->second;
        }

    private:
        std::unordered_map<std::wstring, std::wstring> m_resources;
    };

    struct UnitSuggestion
    {
        std::string value;
        UCM::Unit unit;
    };

    class UnitConverterDisplayAdapter final : public UCM::IUnitConverterVMCallback
    {
    public:
        void DisplayCallback(const std::wstring& from, const std::wstring& to) override
        {
            m_from = CalculatorNative::Utf8::FromWide(from);
            m_to = CalculatorNative::Utf8::FromWide(to);
        }

        void SuggestedValueCallback(const std::vector<std::tuple<std::wstring, UCM::Unit>>& suggestions) override
        {
            m_suggestions.clear();
            m_suggestions.reserve(suggestions.size());
            for (const auto& [value, unit] : suggestions)
            {
                m_suggestions.push_back({ CalculatorNative::Utf8::FromWide(value), unit });
            }
        }

        void MaxDigitsReached() override
        {
            ++m_maxDigitsReachedCount;
        }

        const std::string& From() const { return m_from; }
        const std::string& To() const { return m_to; }
        const std::vector<UnitSuggestion>& Suggestions() const { return m_suggestions; }
        uint64_t MaxDigitsReachedCount() const { return m_maxDigitsReachedCount; }

    private:
        std::string m_from = "0";
        std::string m_to = "0";
        std::vector<UnitSuggestion> m_suggestions;
        uint64_t m_maxDigitsReachedCount = 0;
    };

    class DisplayAdapter final : public ICalcDisplay
    {
    public:
        explicit DisplayAdapter(calculator_callbacks callbacks)
            : m_callbacks(callbacks)
        {
        }

        void SetPrimaryDisplay(const std::wstring& text, bool isError) override
        {
            m_primaryDisplay = CalculatorNative::Utf8::FromWide(text);
            m_isError = isError;
            if (m_callbacks.primary_display_changed != nullptr)
            {
                m_callbacks.primary_display_changed(m_callbacks.context, m_primaryDisplay.c_str(), isError ? 1 : 0);
            }
        }

        void SetIsInError(bool isError) override
        {
            m_isError = isError;
        }

        void SetExpressionDisplay(
            std::shared_ptr<std::vector<std::pair<std::wstring, int>>> const& tokens,
            std::shared_ptr<std::vector<std::shared_ptr<IExpressionCommand>>> const&) override
        {
            std::wstring expression;
            for (const auto& token : *tokens)
            {
                expression += token.first;
            }
            m_expressionDisplay = CalculatorNative::Utf8::FromWide(expression);
            if (m_callbacks.expression_display_changed != nullptr)
            {
                m_callbacks.expression_display_changed(m_callbacks.context, m_expressionDisplay.c_str());
            }
        }

        void InputChanged() override
        {
            ++m_events.input_changed_count;
            if (m_callbacks.input_changed != nullptr)
            {
                m_callbacks.input_changed(m_callbacks.context);
            }
        }

        void SetParenthesisNumber(unsigned int count) override
        {
            m_events.parenthesis_count = count;
        }

        void OnNoRightParenAdded() override
        {
            ++m_events.no_right_parenthesis_count;
        }

        void MaxDigitsReached() override
        {
            ++m_events.max_digits_reached_count;
        }

        void BinaryOperatorReceived() override
        {
            ++m_events.binary_operator_received_count;
        }

        void OnHistoryItemAdded(unsigned int index) override
        {
            ++m_events.history_item_added_count;
            m_events.last_history_item_index = index;
        }

        void SetMemorizedNumbers(const std::vector<std::wstring>& values) override
        {
            m_memoryValues.clear();
            m_memoryValues.reserve(values.size());
            for (const auto& value : values)
            {
                m_memoryValues.push_back(CalculatorNative::Utf8::FromWide(value));
            }
        }

        void MemoryItemChanged(unsigned int index) override
        {
            ++m_events.memory_item_changed_count;
            m_events.last_memory_item_index = index;
        }

        const std::string& PrimaryDisplay() const { return m_primaryDisplay; }
        const std::string& ExpressionDisplay() const { return m_expressionDisplay; }
        const std::vector<std::string>& MemoryValues() const { return m_memoryValues; }
        const calculator_event_state& Events() const { return m_events; }
        bool IsError() const { return m_isError; }

    private:
        calculator_callbacks m_callbacks{};
        std::string m_primaryDisplay;
        std::string m_expressionDisplay;
        std::vector<std::string> m_memoryValues;
        calculator_event_state m_events{};
        bool m_isError = false;
    };

    calculator_status CopyString(const std::string& value, char* buffer, std::size_t bufferSize, std::size_t* requiredSize)
    {
        if (requiredSize == nullptr)
        {
            return CALCULATOR_STATUS_INVALID_ARGUMENT;
        }

        *requiredSize = value.size() + 1;
        if (buffer == nullptr)
        {
            return CALCULATOR_STATUS_OK;
        }
        if (bufferSize < *requiredSize)
        {
            return CALCULATOR_STATUS_BUFFER_TOO_SMALL;
        }

        std::memcpy(buffer, value.c_str(), *requiredSize);
        return CALCULATOR_STATUS_OK;
    }

    template <typename Action>
    calculator_status Protect(Action&& action)
    {
        try
        {
            LastError.clear();
            std::forward<Action>(action)();
            return CALCULATOR_STATUS_OK;
        }
        catch (uint32_t error)
        {
            LastError = "calculator engine error " + std::to_string(error);
            return CALCULATOR_STATUS_ENGINE_ERROR;
        }
        catch (const std::exception& error)
        {
            LastError = error.what();
            return CALCULATOR_STATUS_INTERNAL_ERROR;
        }
        catch (...)
        {
            LastError = "unknown calculator engine error";
            return CALCULATOR_STATUS_INTERNAL_ERROR;
        }
    }

    std::vector<int> FlattenExpressionCommands(
        const std::vector<std::shared_ptr<IExpressionCommand>>& expressionCommands)
    {
        std::vector<int> commands;
        for (const auto& expressionCommand : expressionCommands)
        {
            switch (expressionCommand->GetCommandType())
            {
            case CalculationManager::CommandType::UnaryCommand:
            {
                const auto command = std::dynamic_pointer_cast<IUnaryCommand>(expressionCommand);
                commands.insert(commands.end(), command->GetCommands()->begin(), command->GetCommands()->end());
                break;
            }
            case CalculationManager::CommandType::BinaryCommand:
                commands.push_back(std::dynamic_pointer_cast<IBinaryCommand>(expressionCommand)->GetCommand());
                break;
            case CalculationManager::CommandType::Parentheses:
                commands.push_back(std::dynamic_pointer_cast<IParenthesisCommand>(expressionCommand)->GetCommand());
                break;
            case CalculationManager::CommandType::OperandCommand:
            {
                const auto command = std::dynamic_pointer_cast<IOpndCommand>(expressionCommand);
                bool needsSign = command->IsNegative();
                for (const auto operandCommand : *command->GetCommands())
                {
                    commands.push_back(operandCommand);
                    if (needsSign && operandCommand != static_cast<int>(Command::Command0))
                    {
                        commands.push_back(static_cast<int>(Command::CommandSIGN));
                        needsSign = false;
                    }
                }
                break;
            }
            }
        }
        return commands;
    }

    class HistoryLoadScope
    {
    public:
        explicit HistoryLoadScope(CalculatorManager& manager)
            : m_manager(manager)
        {
            m_manager.SetInHistoryItemLoadMode(true);
        }

        ~HistoryLoadScope()
        {
            m_manager.SetInHistoryItemLoadMode(false);
        }

    private:
        CalculatorManager& m_manager;
    };
}

struct calculator_handle
{
    std::unique_ptr<ResourceProvider> resources;
    std::unique_ptr<DisplayAdapter> display;
    std::unique_ptr<CalculatorManager> manager;
    calculator_mode mode = CALCULATOR_MODE_STANDARD;
};

struct calculator_unit_converter_handle
{
    std::unique_ptr<ResourceProvider> resources;
    std::shared_ptr<UnitConverterDataLoader> loader;
    std::shared_ptr<UnitConverterDisplayAdapter> display;
    std::unique_ptr<UCM::UnitConverter> converter;
    std::vector<UCM::Category> categories;
    std::vector<UCM::Unit> units;
    UCM::Unit fromUnit = UCM::EMPTY_UNIT;
    UCM::Unit toUnit = UCM::EMPTY_UNIT;
};

namespace
{
    bool SelectUnitCategory(calculator_unit_converter_handle& handle, int32_t categoryId)
    {
        const auto category = std::find_if(handle.categories.begin(), handle.categories.end(), [categoryId](const UCM::Category& value) {
            return value.id == categoryId;
        });
        if (category == handle.categories.end())
        {
            return false;
        }

        auto selection = handle.converter->SetCurrentCategory(*category);
        handle.units = std::get<0>(selection);
        handle.fromUnit = std::get<1>(selection);
        handle.toUnit = std::get<2>(selection);
        handle.converter->SetCurrentUnitTypes(handle.fromUnit, handle.toUnit);
        return true;
    }

    const UCM::Unit* FindUnit(const calculator_unit_converter_handle& handle, int32_t unitId)
    {
        const auto unit = std::find_if(handle.units.begin(), handle.units.end(), [unitId](const UCM::Unit& value) { return value.id == unitId; });
        return unit == handle.units.end() ? nullptr : &*unit;
    }
}

uint32_t calculator_native_abi_version(void)
{
    return 1;
}

calculator_status calculator_create(
    const calculator_resource_entry* resources,
    size_t resourceCount,
    const calculator_callbacks* callbacks,
    calculator_handle** result)
{
    if (result == nullptr || (resourceCount != 0 && resources == nullptr))
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *result = nullptr;

    return Protect([&]() {
        auto handle = std::make_unique<calculator_handle>();
        handle->resources = std::make_unique<ResourceProvider>(resources, resourceCount);
        handle->display = std::make_unique<DisplayAdapter>(callbacks == nullptr ? calculator_callbacks{} : *callbacks);
        handle->manager = std::make_unique<CalculatorManager>(handle->display.get(), handle->resources.get());
        handle->manager->Reset();
        *result = handle.release();
    });
}

void calculator_destroy(calculator_handle* handle)
{
    delete handle;
}

calculator_status calculator_reset(calculator_handle* handle, int32_t clearMemory)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() {
        handle->manager->Reset(clearMemory != 0);
        handle->mode = CALCULATOR_MODE_STANDARD;
    });
}

calculator_status calculator_set_mode(calculator_handle* handle, calculator_mode mode)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() {
        switch (mode)
        {
        case CALCULATOR_MODE_STANDARD:
            handle->manager->SetStandardMode();
            break;
        case CALCULATOR_MODE_SCIENTIFIC:
            handle->manager->SetScientificMode();
            break;
        case CALCULATOR_MODE_PROGRAMMER:
            handle->manager->SetProgrammerMode();
            break;
        default:
            throw std::invalid_argument("unknown calculator mode");
        }
        handle->mode = mode;
    });
}

calculator_status calculator_send_command(calculator_handle* handle, int32_t command)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->SendCommand(static_cast<Command>(command)); });
}

calculator_status calculator_get_primary_display(
    const calculator_handle* handle,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    return handle == nullptr ? CALCULATOR_STATUS_INVALID_ARGUMENT : CopyString(handle->display->PrimaryDisplay(), buffer, bufferSize, requiredSize);
}

calculator_status calculator_get_expression_display(
    const calculator_handle* handle,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    return handle == nullptr ? CALCULATOR_STATUS_INVALID_ARGUMENT : CopyString(handle->display->ExpressionDisplay(), buffer, bufferSize, requiredSize);
}

calculator_status calculator_get_result_for_radix(
    const calculator_handle* handle,
    uint32_t radix,
    int32_t precision,
    int32_t groupDigitsPerRadix,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || requiredSize == nullptr || radix < 2 || radix > 16 || precision <= 0)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }

    calculator_status copyStatus = CALCULATOR_STATUS_OK;
    const auto engineStatus = Protect([&]() {
        const auto value = CalculatorNative::Utf8::FromWide(
            handle->manager->GetResultForRadix(radix, precision, groupDigitsPerRadix != 0));
        copyStatus = CopyString(value, buffer, bufferSize, requiredSize);
    });
    return engineStatus == CALCULATOR_STATUS_OK ? copyStatus : engineStatus;
}

int32_t calculator_get_is_error(const calculator_handle* handle)
{
    return handle != nullptr && handle->display->IsError() ? 1 : 0;
}

int32_t calculator_get_is_input_empty(const calculator_handle* handle)
{
    return handle != nullptr && handle->manager->IsInputEmpty() ? 1 : 0;
}

calculator_status calculator_get_event_state(const calculator_handle* handle, calculator_event_state* result)
{
    if (handle == nullptr || result == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *result = handle->display->Events();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_get_memory_count(const calculator_handle* handle, size_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->display->MemoryValues().size();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_get_memory_value(
    const calculator_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->display->MemoryValues().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return CopyString(handle->display->MemoryValues()[index], buffer, bufferSize, requiredSize);
}

calculator_status calculator_memory_store(calculator_handle* handle)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->MemorizeNumber(); });
}

calculator_status calculator_memory_recall(calculator_handle* handle, size_t index)
{
    if (handle == nullptr || index >= handle->display->MemoryValues().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->MemorizedNumberLoad(static_cast<unsigned int>(index)); });
}

calculator_status calculator_memory_add(calculator_handle* handle, size_t index)
{
    if (handle == nullptr || (!handle->display->MemoryValues().empty() && index >= handle->display->MemoryValues().size())
        || (handle->display->MemoryValues().empty() && index != 0))
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->MemorizedNumberAdd(static_cast<unsigned int>(index)); });
}

calculator_status calculator_memory_subtract(calculator_handle* handle, size_t index)
{
    if (handle == nullptr || (!handle->display->MemoryValues().empty() && index >= handle->display->MemoryValues().size())
        || (handle->display->MemoryValues().empty() && index != 0))
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->MemorizedNumberSubtract(static_cast<unsigned int>(index)); });
}

calculator_status calculator_memory_clear(calculator_handle* handle, size_t index)
{
    if (handle == nullptr || index >= handle->display->MemoryValues().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() {
        handle->manager->MemorizedNumberClear(static_cast<unsigned int>(index));
        handle->manager->SetMemorizedNumbersString();
        handle->manager->MemoryItemChanged(static_cast<unsigned int>(index));
    });
}

calculator_status calculator_memory_clear_all(calculator_handle* handle)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->MemorizedNumberClearAll(); });
}

calculator_status calculator_get_history_count(const calculator_handle* handle, size_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->manager->GetHistoryItems().size();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_get_history_expression(
    const calculator_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->manager->GetHistoryItems().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto value = CalculatorNative::Utf8::FromWide(handle->manager->GetHistoryItems()[index]->historyItemVector.expression);
    return CopyString(value, buffer, bufferSize, requiredSize);
}

calculator_status calculator_get_history_result(
    const calculator_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->manager->GetHistoryItems().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto value = CalculatorNative::Utf8::FromWide(handle->manager->GetHistoryItems()[index]->historyItemVector.result);
    return CopyString(value, buffer, bufferSize, requiredSize);
}

calculator_status calculator_history_recall(
    calculator_handle* handle,
    size_t index,
    int32_t scientificNotationEnabled)
{
    if (handle == nullptr || index >= handle->manager->GetHistoryItems().size()
        || handle->mode == CALCULATOR_MODE_PROGRAMMER)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }

    return Protect([&]() {
        const auto item = handle->manager->GetHistoryItems()[index];
        const auto commands = FlattenExpressionCommands(*item->historyItemVector.spCommands);
        const auto degreeMode = handle->manager->GetCurrentDegreeMode();

        {
            HistoryLoadScope historyLoad(*handle->manager);
            handle->manager->Reset(false);
            if (handle->mode == CALCULATOR_MODE_SCIENTIFIC)
            {
                handle->manager->SetScientificMode();
            }
            else
            {
                handle->manager->SetStandardMode();
            }

            if (scientificNotationEnabled != 0)
            {
                handle->manager->SendCommand(Command::CommandFE);
            }
            handle->manager->SendCommand(degreeMode);
            for (const auto command : commands)
            {
                handle->manager->SendCommand(static_cast<Command>(command));
            }

            // Match StandardCalculatorViewModel::Recalculate(fromHistory: true):
            // the double toggle commits the final operand while preserving F-E.
            handle->manager->SendCommand(Command::CommandFE);
            handle->manager->SendCommand(Command::CommandFE);
        }

        // Recalculation deliberately suppresses intermediate callbacks. UWP's
        // view model then restores the selected item's presentation in one
        // update; the portable adapter owns that boundary here.
        handle->display->SetPrimaryDisplay(item->historyItemVector.result, false);
        handle->display->SetExpressionDisplay(item->historyItemVector.spTokens, item->historyItemVector.spCommands);
    });
}

calculator_status calculator_history_remove(calculator_handle* handle, size_t index)
{
    if (handle == nullptr || index >= handle->manager->GetHistoryItems().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() {
        if (!handle->manager->RemoveHistoryItem(static_cast<unsigned int>(index)))
        {
            throw std::runtime_error("failed to remove calculator history item");
        }
    });
}

calculator_status calculator_history_clear(calculator_handle* handle)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->manager->ClearHistory(); });
}

calculator_status calculator_unit_converter_create(
    const calculator_resource_entry* resources,
    size_t resourceCount,
    const char* regionCodeUtf8,
    calculator_unit_converter_handle** result)
{
    if (result == nullptr || regionCodeUtf8 == nullptr || regionCodeUtf8[0] == '\0' || (resourceCount != 0 && resources == nullptr))
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *result = nullptr;

    return Protect([&]() {
        auto handle = std::make_unique<calculator_unit_converter_handle>();
        handle->resources = std::make_unique<ResourceProvider>(resources, resourceCount);
        auto* resourceProvider = handle->resources.get();
        handle->loader = std::make_shared<UnitConverterDataLoader>(
            CalculatorNative::Utf8::ToWide(regionCodeUtf8),
            [resourceProvider](std::wstring_view key) { return resourceProvider->GetString(key); });
        handle->display = std::make_shared<UnitConverterDisplayAdapter>();
        handle->converter = std::make_unique<UCM::UnitConverter>(handle->loader);
        handle->converter->Initialize();
        handle->converter->SetViewModelCallback(handle->display);

        const auto dataLoader = std::static_pointer_cast<UCM::IConverterDataLoader>(handle->loader);
        for (const auto& category : handle->converter->GetCategories())
        {
            if (dataLoader->SupportsCategory(category))
            {
                handle->categories.push_back(category);
            }
        }
        if (handle->categories.empty() || !SelectUnitCategory(*handle, handle->categories.front().id))
        {
            throw std::runtime_error("unit converter catalog has no supported categories");
        }
        *result = handle.release();
    });
}

void calculator_unit_converter_destroy(calculator_unit_converter_handle* handle)
{
    delete handle;
}

calculator_status calculator_unit_converter_get_category_count(const calculator_unit_converter_handle* handle, size_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->categories.size();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_get_category_info(
    const calculator_unit_converter_handle* handle,
    size_t index,
    calculator_unit_category_info* result)
{
    if (handle == nullptr || result == nullptr || index >= handle->categories.size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto& category = handle->categories[index];
    *result = { category.id, category.supportsNegative ? 1 : 0 };
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_get_category_name(
    const calculator_unit_converter_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->categories.size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return CopyString(CalculatorNative::Utf8::FromWide(handle->categories[index].name), buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_select_category(calculator_unit_converter_handle* handle, int32_t categoryId)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return SelectUnitCategory(*handle, categoryId) ? CALCULATOR_STATUS_OK : CALCULATOR_STATUS_INVALID_ARGUMENT;
}

calculator_status calculator_unit_converter_get_unit_count(const calculator_unit_converter_handle* handle, size_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->units.size();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_get_unit_info(
    const calculator_unit_converter_handle* handle,
    size_t index,
    calculator_unit_info* result)
{
    if (handle == nullptr || result == nullptr || index >= handle->units.size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto& unit = handle->units[index];
    *result = { unit.id, unit.isConversionSource ? 1 : 0, unit.isConversionTarget ? 1 : 0, unit.isWhimsical ? 1 : 0 };
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_get_unit_name(
    const calculator_unit_converter_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->units.size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return CopyString(CalculatorNative::Utf8::FromWide(handle->units[index].name), buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_get_unit_abbreviation(
    const calculator_unit_converter_handle* handle,
    size_t index,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || index >= handle->units.size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return CopyString(CalculatorNative::Utf8::FromWide(handle->units[index].abbreviation), buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_get_selected_units(
    const calculator_unit_converter_handle* handle,
    int32_t* fromUnitId,
    int32_t* toUnitId)
{
    if (handle == nullptr || fromUnitId == nullptr || toUnitId == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *fromUnitId = handle->fromUnit.id;
    *toUnitId = handle->toUnit.id;
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_set_units(
    calculator_unit_converter_handle* handle,
    int32_t fromUnitId,
    int32_t toUnitId)
{
    if (handle == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto* from = FindUnit(*handle, fromUnitId);
    const auto* to = FindUnit(*handle, toUnitId);
    if (from == nullptr || to == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    handle->fromUnit = *from;
    handle->toUnit = *to;
    return Protect([&]() { handle->converter->SetCurrentUnitTypes(handle->fromUnit, handle->toUnit); });
}

calculator_status calculator_unit_converter_send_command(
    calculator_unit_converter_handle* handle,
    calculator_unit_command command)
{
    if (handle == nullptr || command < CALCULATOR_UNIT_COMMAND_ZERO || command > CALCULATOR_UNIT_COMMAND_NONE)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() { handle->converter->SendCommand(static_cast<UCM::Command>(command)); });
}

calculator_status calculator_unit_converter_switch_active(calculator_unit_converter_handle* handle, const char* currentValueUtf8)
{
    if (handle == nullptr || currentValueUtf8 == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    return Protect([&]() {
        handle->converter->SwitchActive(CalculatorNative::Utf8::ToWide(currentValueUtf8));
        std::swap(handle->fromUnit, handle->toUnit);
    });
}

calculator_status calculator_unit_converter_get_from_display(
    const calculator_unit_converter_handle* handle,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    return handle == nullptr ? CALCULATOR_STATUS_INVALID_ARGUMENT : CopyString(handle->display->From(), buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_get_to_display(
    const calculator_unit_converter_handle* handle,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    return handle == nullptr ? CALCULATOR_STATUS_INVALID_ARGUMENT : CopyString(handle->display->To(), buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_get_suggestion_count(const calculator_unit_converter_handle* handle, size_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->display->Suggestions().size();
    return CALCULATOR_STATUS_OK;
}

calculator_status calculator_unit_converter_get_suggestion(
    const calculator_unit_converter_handle* handle,
    size_t index,
    int32_t* unitId,
    char* buffer,
    size_t bufferSize,
    size_t* requiredSize)
{
    if (handle == nullptr || unitId == nullptr || index >= handle->display->Suggestions().size())
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    const auto& suggestion = handle->display->Suggestions()[index];
    *unitId = suggestion.unit.id;
    return CopyString(suggestion.value, buffer, bufferSize, requiredSize);
}

calculator_status calculator_unit_converter_get_max_digits_reached_count(
    const calculator_unit_converter_handle* handle,
    uint64_t* count)
{
    if (handle == nullptr || count == nullptr)
    {
        return CALCULATOR_STATUS_INVALID_ARGUMENT;
    }
    *count = handle->display->MaxDigitsReachedCount();
    return CALCULATOR_STATUS_OK;
}

const char* calculator_get_last_error(void)
{
    return LastError.c_str();
}
