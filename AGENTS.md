# Redmond Calculator agent guide

## Mission

Build a cross-platform Avalonia port of Windows Calculator while reusing as
much Microsoft calculation, conversion, resource, and public contract code as
possible. Replace UI and platform services additively; do not maintain a
modified copy of Microsoft source.

## Architecture

- `upstream/windows-calculator`: immutable, pinned Microsoft Calculator
  submodule.
- `src/CalculatorCore`: builds the original CalcManager, CalcEngine, RatPack,
  and UnitConverter sources directly from the submodule.
- `src/PortableCompat`: API-compatible platform shims and build-tree-only
  source transformations.
- `src/CalculatorNative`: stable C ABI over the native engine.
- `src/Frontends/Calculator.Managed`: framework-neutral managed application
  state and native bindings.
- `src/Calculator.ResourceLoader` and `src/Calculator.Shortcuts`: portable UWP
  resource and keyboard compatibility.
- `src/Frontends/Calculator.Avalonia`: cross-platform shell and views.
- `redmond-commons`: separately versioned reusable Avalonia controls and
  windowing.

## Principles

1. Never modify or add files inside `upstream/windows-calculator`.
2. Preserve upstream APIs and signatures. Put shims, adapters, injection, and
   generated overlays in this repository.
3. Use real cross-platform implementations, not behaviorless stubs. UI views
   may be rewritten, but calculation and conversion behavior should remain
   source-faithful.
4. Transform unavoidable C++/CX sources only in the build directory. An
   upstream mismatch must fail loudly.
5. Keep platform-specific code behind narrow interfaces. Do not leak Avalonia,
   AppKit, WinUI, or a replacement graphing library into shared domain code.
6. Do not commit build outputs. Update compatibility and migration
   documentation when a new porting difference is discovered.

## Build and verify

Initialize dependencies:

```sh
git submodule update --init --recursive
```

Build and test the native engine:

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
ctest --test-dir build/portable --output-on-failure
```

Build and test the managed/Avalonia layers:

```sh
dotnet build src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj
dotnet build src/Frontends/Calculator.Avalonia.Tests/Calculator.Avalonia.Tests.csproj
dotnet run --project src/PortableResourceTests/Calculator.ResourceLoader.Tests.csproj
dotnet run --project src/PortableShortcutTests/Calculator.Shortcuts.Tests.csproj
dotnet run --project src/Frontends/Calculator.Avalonia.Tests/Calculator.Avalonia.Tests.csproj
```

Run the application:

```sh
dotnet run --project src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj
```

Before committing, also run `scripts/verify-upstream-pristine.sh` and confirm
both submodules have no unintended changes.
