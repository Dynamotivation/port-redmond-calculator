# Portable native build

The portable build compiles the original Calculator arithmetic and state engine
without the UWP user interface. It currently includes:

- RatPack infinite-precision arithmetic
- CalcEngine parsing and command processing
- CalculatorManager, history, expression commands, and number formatting
- UnitConverter input, conversion, suggestion, and preference behavior
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

## Asynchronous compatibility boundary

The original currency interfaces use Microsoft's PPL `concurrency::task`. The
portable source keeps that exact type on Windows, preserving compatibility with
the existing UWP currency loaders. On non-Windows platforms the same interface
is represented by the standard C++ `std::future`, and currency refresh
continuations run with `std::async`. This is a real asynchronous implementation;
the conversion engine is not compiled with currency methods removed or stubbed.

## Remaining portability boundary

The Windows `UnitConverterDataLoader` and `CurrencyDataLoader` remain in the
C++/CX view-model project because they read UWP resources and use Windows HTTP,
globalization, and storage APIs. Their framework-neutral interfaces and the full
conversion engine are portable; each frontend still needs platform-native data
loading and currency-network service implementations.
