#include "CalculatorNative.h"

#include "Utf8.h"
#include "CalcManager/CalculatorManager.h"
#include "CalcManager/CalculatorResource.h"
#include "CalcManager/Command.h"

#include <algorithm>
#include <cstring>
#include <exception>
#include <memory>
#include <string>
#include <unordered_map>
#include <utility>
#include <vector>

using CalculationManager::CalculatorManager;
using CalculationManager::Command;
using CalculationManager::IResourceProvider;

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
            const auto value = m_resources.find(std::wstring{ id });
            return value == m_resources.end() ? std::wstring{} : value->second;
        }

    private:
        std::unordered_map<std::wstring, std::wstring> m_resources;
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
            if (m_callbacks.input_changed != nullptr)
            {
                m_callbacks.input_changed(m_callbacks.context);
            }
        }

        void SetParenthesisNumber(unsigned int) override {}
        void OnNoRightParenAdded() override {}
        void MaxDigitsReached() override {}
        void BinaryOperatorReceived() override {}
        void OnHistoryItemAdded(unsigned int) override {}
        void SetMemorizedNumbers(const std::vector<std::wstring>&) override {}
        void MemoryItemChanged(unsigned int) override {}

        const std::string& PrimaryDisplay() const { return m_primaryDisplay; }
        const std::string& ExpressionDisplay() const { return m_expressionDisplay; }
        bool IsError() const { return m_isError; }

    private:
        calculator_callbacks m_callbacks{};
        std::string m_primaryDisplay;
        std::string m_expressionDisplay;
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
}

struct calculator_handle
{
    std::unique_ptr<ResourceProvider> resources;
    std::unique_ptr<DisplayAdapter> display;
    std::unique_ptr<CalculatorManager> manager;
};

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
    return Protect([&]() { handle->manager->Reset(clearMemory != 0); });
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

int32_t calculator_get_is_error(const calculator_handle* handle)
{
    return handle != nullptr && handle->display->IsError() ? 1 : 0;
}

const char* calculator_get_last_error(void)
{
    return LastError.c_str();
}
