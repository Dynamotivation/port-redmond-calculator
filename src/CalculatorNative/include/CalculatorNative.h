#pragma once

#include <stddef.h>
#include <stdint.h>

#if defined(_WIN32)
#if defined(CALCULATOR_NATIVE_BUILD)
#define CALCULATOR_API __declspec(dllexport)
#else
#define CALCULATOR_API __declspec(dllimport)
#endif
#else
#define CALCULATOR_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C"
{
#endif

    typedef struct calculator_handle calculator_handle;
    typedef struct calculator_unit_converter_handle calculator_unit_converter_handle;

    typedef enum calculator_status
    {
        CALCULATOR_STATUS_OK = 0,
        CALCULATOR_STATUS_INVALID_ARGUMENT = 1,
        CALCULATOR_STATUS_ENGINE_ERROR = 2,
        CALCULATOR_STATUS_INTERNAL_ERROR = 3,
        CALCULATOR_STATUS_BUFFER_TOO_SMALL = 4
    } calculator_status;

    typedef enum calculator_mode
    {
        CALCULATOR_MODE_STANDARD = 0,
        CALCULATOR_MODE_SCIENTIFIC = 1,
        CALCULATOR_MODE_PROGRAMMER = 2
    } calculator_mode;

    typedef struct calculator_resource_entry
    {
        const char* key_utf8;
        const char* value_utf8;
    } calculator_resource_entry;

    typedef void (*calculator_primary_display_callback)(void* context, const char* value_utf8, int32_t is_error);
    typedef void (*calculator_expression_display_callback)(void* context, const char* value_utf8);
    typedef void (*calculator_event_callback)(void* context);

    typedef struct calculator_callbacks
    {
        void* context;
        calculator_primary_display_callback primary_display_changed;
        calculator_expression_display_callback expression_display_changed;
        calculator_event_callback input_changed;
    } calculator_callbacks;

    typedef struct calculator_event_state
    {
        uint64_t no_right_parenthesis_count;
        uint64_t max_digits_reached_count;
        uint64_t binary_operator_received_count;
        uint64_t history_item_added_count;
        uint64_t memory_item_changed_count;
        uint64_t input_changed_count;
        uint32_t parenthesis_count;
        uint32_t last_history_item_index;
        uint32_t last_memory_item_index;
    } calculator_event_state;

    CALCULATOR_API uint32_t calculator_native_abi_version(void);

    CALCULATOR_API calculator_status calculator_create(
        const calculator_resource_entry* resources,
        size_t resource_count,
        const calculator_callbacks* callbacks,
        calculator_handle** result);

    CALCULATOR_API void calculator_destroy(calculator_handle* handle);

    CALCULATOR_API calculator_status calculator_reset(calculator_handle* handle, int32_t clear_memory);
    CALCULATOR_API calculator_status calculator_set_mode(calculator_handle* handle, calculator_mode mode);
    CALCULATOR_API calculator_status calculator_send_command(calculator_handle* handle, int32_t command);

    // UTF-8 getters use a two-call buffer pattern. Pass a null buffer to obtain
    // the required size, including the trailing null byte.
    CALCULATOR_API calculator_status calculator_get_primary_display(
        const calculator_handle* handle,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);

    CALCULATOR_API calculator_status calculator_get_expression_display(
        const calculator_handle* handle,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);

    CALCULATOR_API int32_t calculator_get_is_error(const calculator_handle* handle);
    CALCULATOR_API int32_t calculator_get_is_input_empty(const calculator_handle* handle);

    CALCULATOR_API calculator_status calculator_get_event_state(
        const calculator_handle* handle,
        calculator_event_state* result);

    CALCULATOR_API calculator_status calculator_get_memory_count(const calculator_handle* handle, size_t* count);
    CALCULATOR_API calculator_status calculator_get_memory_value(
        const calculator_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_memory_store(calculator_handle* handle);
    CALCULATOR_API calculator_status calculator_memory_recall(calculator_handle* handle, size_t index);
    CALCULATOR_API calculator_status calculator_memory_add(calculator_handle* handle, size_t index);
    CALCULATOR_API calculator_status calculator_memory_subtract(calculator_handle* handle, size_t index);
    CALCULATOR_API calculator_status calculator_memory_clear(calculator_handle* handle, size_t index);
    CALCULATOR_API calculator_status calculator_memory_clear_all(calculator_handle* handle);

    CALCULATOR_API calculator_status calculator_get_history_count(const calculator_handle* handle, size_t* count);
    CALCULATOR_API calculator_status calculator_get_history_expression(
        const calculator_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_get_history_result(
        const calculator_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_history_remove(calculator_handle* handle, size_t index);
    CALCULATOR_API calculator_status calculator_history_clear(calculator_handle* handle);

    typedef enum calculator_unit_command
    {
        CALCULATOR_UNIT_COMMAND_ZERO = 0,
        CALCULATOR_UNIT_COMMAND_ONE = 1,
        CALCULATOR_UNIT_COMMAND_TWO = 2,
        CALCULATOR_UNIT_COMMAND_THREE = 3,
        CALCULATOR_UNIT_COMMAND_FOUR = 4,
        CALCULATOR_UNIT_COMMAND_FIVE = 5,
        CALCULATOR_UNIT_COMMAND_SIX = 6,
        CALCULATOR_UNIT_COMMAND_SEVEN = 7,
        CALCULATOR_UNIT_COMMAND_EIGHT = 8,
        CALCULATOR_UNIT_COMMAND_NINE = 9,
        CALCULATOR_UNIT_COMMAND_DECIMAL = 10,
        CALCULATOR_UNIT_COMMAND_NEGATE = 11,
        CALCULATOR_UNIT_COMMAND_BACKSPACE = 12,
        CALCULATOR_UNIT_COMMAND_CLEAR = 13,
        CALCULATOR_UNIT_COMMAND_RESET = 14,
        CALCULATOR_UNIT_COMMAND_NONE = 15
    } calculator_unit_command;

    typedef struct calculator_unit_category_info
    {
        int32_t id;
        int32_t supports_negative;
    } calculator_unit_category_info;

    typedef struct calculator_unit_info
    {
        int32_t id;
        int32_t is_conversion_source;
        int32_t is_conversion_target;
        int32_t is_whimsical;
    } calculator_unit_info;

    CALCULATOR_API calculator_status calculator_unit_converter_create(
        const calculator_resource_entry* resources,
        size_t resource_count,
        const char* region_code_utf8,
        calculator_unit_converter_handle** result);
    CALCULATOR_API void calculator_unit_converter_destroy(calculator_unit_converter_handle* handle);
    CALCULATOR_API calculator_status calculator_unit_converter_get_category_count(
        const calculator_unit_converter_handle* handle,
        size_t* count);
    CALCULATOR_API calculator_status calculator_unit_converter_get_category_info(
        const calculator_unit_converter_handle* handle,
        size_t index,
        calculator_unit_category_info* result);
    CALCULATOR_API calculator_status calculator_unit_converter_get_category_name(
        const calculator_unit_converter_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_select_category(
        calculator_unit_converter_handle* handle,
        int32_t category_id);
    CALCULATOR_API calculator_status calculator_unit_converter_get_unit_count(
        const calculator_unit_converter_handle* handle,
        size_t* count);
    CALCULATOR_API calculator_status calculator_unit_converter_get_unit_info(
        const calculator_unit_converter_handle* handle,
        size_t index,
        calculator_unit_info* result);
    CALCULATOR_API calculator_status calculator_unit_converter_get_unit_name(
        const calculator_unit_converter_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_get_unit_abbreviation(
        const calculator_unit_converter_handle* handle,
        size_t index,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_get_selected_units(
        const calculator_unit_converter_handle* handle,
        int32_t* from_unit_id,
        int32_t* to_unit_id);
    CALCULATOR_API calculator_status calculator_unit_converter_set_units(
        calculator_unit_converter_handle* handle,
        int32_t from_unit_id,
        int32_t to_unit_id);
    CALCULATOR_API calculator_status calculator_unit_converter_send_command(
        calculator_unit_converter_handle* handle,
        calculator_unit_command command);
    CALCULATOR_API calculator_status calculator_unit_converter_switch_active(
        calculator_unit_converter_handle* handle,
        const char* current_value_utf8);
    CALCULATOR_API calculator_status calculator_unit_converter_get_from_display(
        const calculator_unit_converter_handle* handle,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_get_to_display(
        const calculator_unit_converter_handle* handle,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_get_suggestion_count(
        const calculator_unit_converter_handle* handle,
        size_t* count);
    CALCULATOR_API calculator_status calculator_unit_converter_get_suggestion(
        const calculator_unit_converter_handle* handle,
        size_t index,
        int32_t* unit_id,
        char* buffer,
        size_t buffer_size,
        size_t* required_size);
    CALCULATOR_API calculator_status calculator_unit_converter_get_max_digits_reached_count(
        const calculator_unit_converter_handle* handle,
        uint64_t* count);

    CALCULATOR_API const char* calculator_get_last_error(void);

#ifdef __cplusplus
}
#endif
