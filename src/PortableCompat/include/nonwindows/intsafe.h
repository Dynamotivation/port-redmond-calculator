#pragma once

// Calculator's portable sources include the Microsoft precompiled-header list,
// which names <intsafe.h>. The arithmetic implementation uses its own checked
// helpers in Ratpack/conv.cpp, so no Win32 IntSafe declarations are required by
// the cross-platform target.
