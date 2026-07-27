# Microsoft Calculator submodule contract

Microsoft Calculator is an immutable input to the Redmond build. Its complete
source tree lives only in the `upstream/windows-calculator` Git submodule.
Redmond source, adapters, patches, and generated files live in the
superproject.

Run the invariant check directly with:

```sh
scripts/verify-upstream-pristine.sh
```

The check fails when:

- the submodule is missing;
- its checked-out commit differs from the superproject gitlink;
- any tracked or untracked file exists as a local submodule change.

The CMake test suite always runs this check. It is never silently skipped.

## Calculator and converter engine

`src/CalculatorCore/CMakeLists.txt` compiles Microsoft's original
CalculatorManager, CalcEngine, RatPack, UnitConverter, and number-formatting
translation units directly from the submodule.

Windows SDK dependencies are supplied on non-Windows hosts through
`src/PortableCompat/include/nonwindows`. The portable `ppltasks.h` preserves
the `concurrency::task<T>`, `task_from_result`, `get`, and `then` API used by
UnitConverter while implementing it with standard C++. Windows continues to
resolve its real SDK headers.

No Microsoft header or implementation file is copied into the Redmond source
tree.

## Generated C++/CX unit catalog overlay

Microsoft's `UnitConverterDataLoader` contains C++/CX syntax that a
non-Windows compiler cannot parse. CMake copies its three authoritative source
files from the submodule into `build/portable/generated/portable-upstream` and
applies `UnitConverterDataLoader.portable.patch` there.

Only the generated build-tree copy is compiled. A patch failure is an upstream
compatibility event and stops configuration. The source catalog, conversion
factors, offsets, ordering, regional defaults, and unit identifiers remain
owned by Microsoft.

## Graphing replacement

Microsoft's proprietary graphing implementation is unavailable, but the public
headers in `src/GraphingInterfaces` remain authoritative.

`GraphingInterfaceContractTests` compiles those headers directly from the
submodule and checks principal method signatures. `GraphingInterfaceHash`
checks every interface header. Any contract change therefore stops the build
until the managed replacement and adapters have been reviewed.

## Resources and assets

`Directory.Build.props` exposes one `MicrosoftCalculatorRoot` MSBuild property.
Avalonia projects consume localized `.resw` resources and
`CalculatorIcons.ttf` through that path. They do not duplicate those Microsoft
files in this repository.

## Upstream update procedure

1. Update only the `upstream/windows-calculator` gitlink.
2. Leave the submodule worktree untouched.
3. Configure and build the native targets. A failed generated patch requires an
   adapter update in this repository.
4. Run all CTest targets, resource tests, managed behavior tests, and visual
   snapshots.
5. Review every graph-contract hash or signature change before refreshing its
   expected value.
6. Commit the submodule pointer and required Redmond-side adapter changes as an
   isolated upstream-update batch.
