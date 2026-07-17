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
dotnet run --project src/PortableResourceTests/Calculator.ResourceLoader.Tests.csproj -- src/Calculator/Resources
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

The frontend packages all original Calculator `.resw` files and selects the
current UI culture at runtime. It is not restricted to the former copied
`en-US/CEngineStrings.resw` file.

## Cross-platform UWP resources

`Calculator.ResourceLoader` implements the `Windows.ApplicationModel.Resources.ResourceLoader`
surface used by Calculator without depending on UWP, WinUI, or Uno. It reads the
repository's `.resw` files directly and supports:

- default and named maps, including `CEngineStrings`
- `GetForCurrentView` and `GetForViewIndependentUse`
- ordinary keys, `Uid/Property`, `/Map/Key`, and `ms-resource:///Map/Key`
- exact-culture, parent/same-language, and configured-default fallback
- effective-map enumeration for provisioning the native engine
- `x:Uid` property projection for frontend adapters

The portable resource test parses both maps in every shipped locale in addition
to checking key normalization and fallback behavior.

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
C++/CX view-model project. Resource lookup is no longer a portability blocker,
but their catalog construction still uses C++/CX types and their region, HTTP,
and storage services use Windows APIs. Their framework-neutral interfaces and
the full conversion engine are portable; those remaining services are the next
data-layer boundary.
