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

Microsoft calculation, conversion, graphing-contract, and native domain files
exist only in the pinned `upstream/windows-calculator` submodule. The Redmond
build compiles them without modifying that worktree. Platform dependencies are
supplied through compatibility headers and a generated build-tree overlay; see
the [upstream compatibility contract](UpstreamCompatibility.md).

## Build and test

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
ctest --test-dir build/portable --output-on-failure
dotnet run --project src/PortableResourceTests/Calculator.ResourceLoader.Tests.csproj
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

## Release licensing

Build and publish output includes a `Licenses` directory containing the
Redmond Calculator, Redmond Commons, and Microsoft Calculator license and
notice files; bundled-font licenses; .NET runtime notices; and the exact
resolved NuGet dependency inventory in Markdown and SPDX 2.3 formats.

After changing dependencies or the .NET SDK/runtime, regenerate all derived
compliance material:

```sh
scripts/update-licensing.sh
```

This reads the exact NuGet graph, copies native-package notices from the
resolved package versions, and refreshes the .NET license and third-party
notice snapshot from the active official installation. No package version or
SDK installation path is hard-coded in the application project.

The repository's `global.json` pins the SDK used by CI so the .NET snapshot is
reproducible. When upgrading the SDK, update `global.json`, restore, and run
`scripts/update-licensing.sh` in the same change.

Before releasing, verify source materials and the reviewed Latin Modern Math
font, then check the actual publish directory:

```sh
scripts/verify-licensing.sh
scripts/verify-published-licenses.sh path/to/publish
```

The macOS bundler copies this material to
`Redmond Calculator.app/Contents/Resources/Licenses` and refuses to construct a
bundle if the core application and .NET runtime notices are absent. The About
section opens the packaged project license and third-party notices, with the
repository copies as development fallbacks.

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

Standard, Scientific, Programmer, Date calculation, all 12 static converter
categories, and Currency route to working cross-platform surfaces. Settings
routes to a functional cross-platform page with persisted Light, Dark, and
system theme preferences plus per-provider currency network controls.

Graphing uses a platform-neutral managed backend behind the pristine public
`GraphingInterfaces` contract. Compile-time signature probes and full-header
hash checks make upstream contract changes fail the portable test suite.
`AngouriMathSolver` is the replaceable CAS adapter; AngouriMath types never
enter the view models or Avalonia controls. The current
surface supports explicit, implicit, polar, and inequality plots, multiple
equations, parameter sliders, pan/zoom/reset, and the source 800-logical-pixel
graph/equation-panel breakpoint. Structured math input, equation styling,
tracing, graph settings, key-feature analysis, and the graphing numpad remain
to be ported.

Currency uses a managed cross-platform provider boundary rather than the
Windows-only retail loader. ECB, Federal Reserve H.10, Bank of Canada Valet,
and Frankfurter each have a dedicated parser and disclosure. Provider currency
codes come from the returned table, amounts are converted on device, and
responses are cached only in memory for the app session. No request contains
the entered amount or selected pair.

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
portable compatibility header preserves that exact namespace, type, and
continuation API while implementing its behavior with standard C++ futures.
`UnitConverter.h` and `UnitConverter.cpp` therefore compile unchanged on every
host. This is a real asynchronous implementation; the conversion engine is not
compiled with currency methods removed or stubbed.

## Portable unit catalog

The original `CalcViewModel/DataLoaders/UnitConverterDataLoader.cpp` remains
untouched inside the Microsoft submodule. CMake copies it into the build tree
and applies a deterministic portability transformation there. The generated
standard-C++ constructor takes a two-letter region code and a resource-lookup
function. This preserves one authoritative Microsoft table for unit IDs,
ordering, factors, whimsical units, temperature offsets, and regional source/
target defaults. If the transformation no longer applies after a submodule
update, configuration fails and requires an explicit adapter review.

`UnitConverterDataLoaderPortableTests` loads the real English resources and
verifies US customary, SI, Fahrenheit, and Japanese Pyeong selection along with
localized metadata and explicit temperature conversion data. Required resource
keys are checked rather than silently replaced by blank strings.

## Currency portability boundary

The non-currency `UnitConverterDataLoader` and its complete catalog are now
portable. Microsoft's original `CurrencyDataLoader` remains Windows-only and
is not compiled into the portable frontend because its retail service contract
and licensed feed are not public. `CurrencyConverterViewModel` instead owns a
narrow managed contract implemented by provider-specific rate-table clients.
This leaves the pinned Microsoft submodule pristine while supplying real
cross-platform currency behavior. Frontends must also provide the current
two-letter region code; the portable static catalog itself has no dependency
on `Windows.Globalization.GeographicRegion`.
