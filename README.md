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
