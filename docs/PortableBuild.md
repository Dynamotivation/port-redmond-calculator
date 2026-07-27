# Portable native build

The portable build compiles the original Calculator arithmetic and state engine
without the UWP user interface. It currently includes:

- RatPack infinite-precision arithmetic
- CalcEngine parsing and command processing
- CalculatorManager, history, expression commands, and number formatting
- UnitConverter input, conversion, suggestion, and preference behavior
- the original UnitConverter category, unit, factor, temperature, and regional-default catalog
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

## Avalonia calculator frontend on macOS

The source-faithful Standard, Scientific, and Unit Converter frontend calls the
portable engine through the managed C ABI wrapper. The Unit Converter surface
uses the original `UnitConverter`, catalog, regional defaults, input behavior,
and suggested-value generation; it is not a UI-only approximation. Build the
native target first so MSBuild can copy the dynamic library into the application
output:

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
dotnet run --project src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj
```

The macOS frontend uses an `NSVisualEffectView` behind Avalonia content for a
native translucent material. Its borderless window retains drag, resize,
minimize, maximize, and close behavior through explicit custom chrome. The
Settings page can switch the material off and exposes two independent radio
choices. Window shape can be Windows 10 square, Windows 11 rounded, or native
macOS rounded; title-bar controls can be Windows caption buttons or genuine
AppKit traffic lights. The selections remain independent. macOS controls with a
Windows corner style use standalone system-rendered buttons in the custom title
region. Selecting both macOS controls and macOS corners upgrades the window to
AppKit's complete titled-window implementation, including coordinated hover and
window-management behavior. macOS corners with Windows controls use Avalonia's
`BorderOnly` extended-client mode. These host preferences are persisted
alongside the app theme and are hidden on unsupported platforms.

The observed WinUI/UWP versus Avalonia Fluent defaults, Calculator-specific
overrides, reusable porting patterns, and visual QA rules are maintained in the
[Fluent migration guide](AvaloniaFluentMigration.md).

The frontend packages all original Calculator `.resw` files and selects the
current UI culture at runtime. It is not restricted to the former copied
`en-US/CEngineStrings.resw` file.

The hamburger button opens an Avalonia replacement for the original
`NavigationView` in `LeftMinimal` mode. Its manifest preserves the source order,
serialization IDs, localized labels, category groups, and glyphs from
`CalcViewModel/Common/NavCategory.cpp`; the glyphs come from the repository's
`CalculatorIcons.ttf` rather than approximate platform symbols. The pane uses
overlay/light-dismiss behavior and retains the selected mode.

The Standard and Scientific pages consume the portable shortcut catalog and send every
calculator operation through the managed `CalculatorManager` bridge. Printable
keys remain keyboard-layout aware, keypad and named-key fallbacks are supported,
and keyboard activation drives the same pressed visuals as pointer input.
Copy/paste uses Avalonia's platform clipboard while expression parsing remains
framework-neutral and feeds CalculatorManager commands.

Scientific mode uses the original five-column operator topology, normal and
inverse operator banks, DEG/RAD/GRAD cycling, F-E state, trigonometry and
function popups, culture-sensitive decimal input, and the same native history
and memory collections as Standard mode. Its compact, medium, and large operator
states are selected from the operator panel's own arranged width and height,
using the original two-axis UWP thresholds and corresponding caption, numeric,
operator-row, and popup dimensions. The composite operator buttons retain the
source Calculator-font glyph, localized label, and chevron. Scientific paste
supports native power, modulo, parentheses, unary signs, and exponent notation;
unsupported named-function syntax is rejected rather than partially executed.
The native boundary exposes mode selection and input-empty state directly so
the frontend does not infer engine state from localized display strings. Error
recovery likewise follows the source command boundary: digits and decimal clear
and replace an error, while non-recoverable commands only clear it.

History is sourced directly from CalculatorManager rather than duplicated UI
state. The Avalonia host reproduces all three source layout states rather than
approximating them from width alone: below 560 logical pixels history is a full
placement flyout; from 560 it is docked at `320*:240*`; at 1024 by 768 or 768 by
1366 it becomes a fixed 320-logical-pixel column. The source minimum window size
of 320 by 500 is enforced. The result area likewise keeps the source mode-aware
height states: Scientific uses the original 544 and 800 thresholds, while the
shared large state uses the source 108 minimum and 72 maximum font size.

The narrow history flyout is deliberately two materials. Its full-window layer
is the source `BackgroundSmokeFillColorBrush`: black at 30% opacity in light
theme and 42% in dark theme, so it dims the calculator controls still rendered
beneath it. Only the numpad-height lower row is painted with the opaque
`SolidBackgroundFillColorBase` surface. It is therefore neither a fully opaque
replacement page nor host-backdrop transparency showing the desktop. In docked
layout the opaque lower shade is removed, matching `HistoryList.xaml`'s
`DockedLayout` state. The solid surface values are `#F3F3F3` in light theme and
`#202020` in dark theme; this is intentionally a dedicated history resource so
unrelated custom window-surface colors cannot leak into the flyout. History
selection, clear, keyboard toggling, and empty state are wired on both
responsive surfaces.

Standard, Scientific, and all 12 static converter categories route to working native-backed
surfaces. Settings routes to a functional cross-platform page with persisted
Light, Dark, and system theme preferences. Graphing, Programmer, Date, and
Currency remain present but disabled until their corresponding
frontend or platform layer is ported. Currency remains unavailable until its
Windows HTTP/cache implementation is replaced with a real cross-platform loader.

## Cross-platform UWP resources

`Calculator.ResourceLoader` implements the `Windows.ApplicationModel.Resources.ResourceLoader`
surface used by Calculator without depending on a Windows UI framework. It reads the
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

The ABI is intentionally independent of a UI framework. A desktop, mobile, or
command-line frontend can call the same native library.
In addition to arithmetic, history, and memory, it exposes an opaque Unit
Converter handle with category/unit enumeration, regional selection, input
commands, active-unit switching, display values, suggestions, and max-digit
events. `Calculator.Managed.NativeUnitConverter` is the .NET binding used by the
Avalonia frontend.

## Asynchronous compatibility boundary

The original currency interfaces use Microsoft's PPL `concurrency::task`. The
portable source keeps that exact type on Windows, preserving compatibility with
the existing UWP currency loaders. On non-Windows platforms the same interface
is represented by the standard C++ `std::future`, and currency refresh
continuations run with `std::async`. This is a real asynchronous implementation;
the conversion engine is not compiled with currency methods removed or stubbed.

## Portable unit catalog

The original `CalcViewModel/DataLoaders/UnitConverterDataLoader.cpp` now builds
inside `CalculatorCore` both with C++/CX on UWP and as standard C++20 elsewhere.
The portable constructor takes a two-letter region code and a resource-lookup
function. This preserves one authoritative Microsoft table for unit IDs,
ordering, factors, whimsical units, temperature offsets, and regional source/
target defaults.

`UnitConverterDataLoaderPortableTests` loads the real English resources and
verifies US customary, SI, Fahrenheit, and Japanese Pyeong selection along with
localized metadata and explicit temperature conversion data. Required resource
keys are checked rather than silently replaced by blank strings.

## Remaining portability boundary

The non-currency `UnitConverterDataLoader` and its complete catalog are now
portable. The remaining Windows-only conversion component is
`CurrencyDataLoader`, whose HTTP, cache storage, JSON, and network-policy code
still uses Windows APIs. Frontends must also provide the current two-letter
region code; the portable catalog itself has no dependency on
`Windows.Globalization.GeographicRegion`.
