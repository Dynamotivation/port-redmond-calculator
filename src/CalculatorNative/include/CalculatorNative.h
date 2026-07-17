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
    CALCULATOR_API const char* calculator_get_last_error(void);

#ifdef __cplusplus
}
#endif
