# Redmond Calculator

Redmond Calculator is a cross-platform Avalonia frontend that reuses the
Microsoft Calculator engine without modifying its source tree.

Microsoft Calculator is pinned as the `upstream/windows-calculator` Git
submodule. Redmond-owned compatibility headers, generated overlays, native
bindings, managed view models, and Avalonia views live exclusively in this
superproject.

## Clone

```sh
git clone --recurse-submodules https://github.com/Dynamotivation/port-redmond-calculator.git
cd port-redmond-calculator
```

For an existing checkout:

```sh
git submodule update --init --recursive
```

## Build

```sh
cmake -S . -B build/portable -DCMAKE_BUILD_TYPE=Release
cmake --build build/portable --parallel
ctest --test-dir build/portable --output-on-failure
dotnet build src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj
```

See [PortableBuild.md](docs/PortableBuild.md) for frontend and packaging
details, and [UpstreamCompatibility.md](docs/UpstreamCompatibility.md) for the
strict submodule compatibility contract.

## Currency data

Currency conversion is opt-in by provider at use time and always calculates
entered amounts locally. The app offers four independently selectable sources:

- European Central Bank Data Portal
- Federal Reserve H.10
- Bank of Canada Valet
- Frankfurter

Each source has its own response parser, currency coverage, publication cadence,
disclosure, explicit consent state, source-list switch, and informational-use
disclaimer. No source is contacted until it has consent and the user selects it.
A provider receives only a fixed latest-rate-table request plus ordinary HTTPS
connection metadata; selected currency pairs and entered values are never
included. Consent and source-list choices are persisted locally, while downloaded
rates are held only for the app session. Cross-pairs are reconstructed locally
from the selected provider's base table.

## Windows build with MSVC

The native Windows build uses the Visual Studio 2022 generator and MSVC, not
the compiler selected by the MSYS2 environment. The checked-in project remains
CMake-based for the native layer, but CMake is explicitly configured to emit a
Visual Studio solution for x64.

On the configured development machine, the local convenience helpers are:

```powershell
.\scripts\build-windows-msvc.ps1
.\scripts\run-windows-msvc.ps1 -NoBuild
```

These helpers are intentionally machine-local and are excluded through
`.git/info/exclude`. They use the .NET SDK version pinned by `global.json`. For
a clean checkout, run the same steps from a Visual Studio Developer PowerShell
or use the pinned Visual Studio environment script:

```text
cmake -S . -B build/windows-msvc -G "Visual Studio 17 2022" -A x64
cmake --build build/windows-msvc --config Release --target CalculatorNative --parallel
dotnet build src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj --configuration Release
dotnet run --project src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj --configuration Release --no-build
```

## Upstream ownership boundary

- Never modify files below `upstream/windows-calculator`.
- Build Microsoft sources directly from the pinned submodule.
- Put platform compatibility in this repository.
- Apply unavoidable syntax transformations only to generated build-tree
  copies.
- Advance the Microsoft submodule only in a dedicated, fully verified update.

Microsoft Calculator retains its own MIT license and third-party notices inside
the submodule. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## License

Redmond Calculator's original code is available under the
[MIT License](LICENSE). The Microsoft Calculator and Redmond Commons
submodules, bundled fonts, native runtime components, and package dependencies
retain their respective licenses. See
[THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for the ownership boundaries
and redistribution notices. See [TRADEMARKS.md](TRADEMARKS.md) for the
independent-project and trademark disclaimer.

Packaged builds contain a complete `Licenses` directory, including a generated
NuGet inventory and SPDX 2.3 document. Run `scripts/verify-licensing.sh` before
releasing.
