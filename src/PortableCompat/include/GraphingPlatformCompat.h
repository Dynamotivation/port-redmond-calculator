#pragma once

#include <cstdint>

// Win32 spelling retained by Microsoft's pristine graphing contract headers.
// The replacement engine consumes the same ABI-level result and byte types.
using HRESULT = std::int32_t;
using BYTE = std::uint8_t;

#ifndef GRAPHINGAPI
#define GRAPHINGAPI
#endif
