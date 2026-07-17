# Portable native build

The portable build compiles the original Calculator arithmetic and state engine
without the UWP user interface. It currently includes:

- RatPack infinite-precision arithmetic
- CalcEngine parsing and command processing
- CalculatorManager, history, expression commands, and number formatting
- A versioned C ABI suitable for .NET P/Invoke or other foreign-function interfaces

The native library does not reimplement or stub Calculator behavior. It links the
same C++ sources used by the Windows application.

## Build and test

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
ctest --test-dir build/portable --output-on-failure
```

The shared-library output is named `calculator_engine.dll` on Windows,
`libcalculator_engine.dylib` on macOS, and `libcalculator_engine.so` on Linux.

## Avalonia vertical slice on macOS

The source-faithful Standard Calculator frontend calls the portable engine
through the managed C ABI wrapper. Build the native target first so MSBuild can
copy the dynamic library into the application output:

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
dotnet run --project src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj
```

The macOS frontend uses an `NSVisualEffectView` behind Avalonia content for a
native translucent material. Its borderless window retains drag, resize,
minimize, maximize, and close behavior through explicit custom chrome.

## Native boundary

[`CalculatorNative.h`](../src/CalculatorNative/include/CalculatorNative.h) is a
C-compatible API. It uses opaque handles, fixed-width integers, caller-owned
UTF-8 buffers, and status codes. C++ exceptions do not cross the ABI boundary.
Engine resources are supplied as UTF-8 key/value entries and copied when a
calculator instance is created.

The ABI is intentionally independent of a UI framework. A Uno Platform,
Avalonia, Flutter, Qt, or command-line frontend can call the same native library.

## Remaining portability boundary

`UnitConverter` is not yet part of the portable target. Its conversion math is
standard C++, but its public currency-refresh interfaces use Microsoft's
`concurrency::task` from `ppltasks.h`. The old cross-platform PPL implementation
from C++ REST SDK is no longer maintained, so the portable build does not take a
dependency on it. Currency networking and asynchronous orchestration need a
maintained platform service boundary before this source is added.
