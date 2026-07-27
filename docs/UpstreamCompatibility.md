# Upstream source compatibility

The cross-platform frontend may replace UWP views, windowing, and presentation
code. Microsoft-owned calculation, conversion, graphing-contract, and native
domain sources remain byte-for-byte identical to `upstream/main`.

Run the invariant check directly with:

```sh
scripts/verify-upstream-pristine.sh upstream/main
```

Test configuration fails when the selected upstream ref is unavailable; the
guard is never silently skipped. CI and shallow checkouts must fetch the
reviewed upstream commit or set `CALCULATOR_UPSTREAM_REF` to an available,
reviewed commit.

The check rejects modified, deleted, or renamed files under:

- `src/CalcManager`
- `src/CalcViewModel/DataLoaders`
- `src/GraphingInterfaces`
- `src/GraphingImpl`
- `src/GraphControl`

New adjacent build or adapter files are allowed. Platform behavior must not be
implemented by editing an existing protected file.

## Calculator and converter engine

The portable CMake target compiles Microsoft's original CalculatorManager,
CalcEngine, RatPack, UnitConverter, and number-formatting translation units
directly.

Windows SDK dependencies named by those files are supplied on non-Windows hosts
through the `src/PortableCompat/include/nonwindows` include directory. In
particular, the portable `ppltasks.h` preserves the `concurrency::task<T>`,
`task_from_result`, `get`, and `then` API used by UnitConverter while
implementing it with standard C++. Windows continues to resolve the real SDK
headers. The Microsoft `UnitConverter.h` and `UnitConverter.cpp` signatures are
not changed.

## C++/CX unit catalog

`UnitConverterDataLoader` contains C++/CX syntax that a non-Windows compiler
cannot parse. Its authoritative Microsoft files remain pristine. During CMake
configuration they are copied to
`build/portable/generated/portable-upstream`, where the deterministic
`UnitConverterDataLoader.portable.patch` replaces only platform-bound resource,
region, and navigation access.

The generated copy is the one compiled into the portable library. If upstream
changes invalidate the transformation, configuration fails instead of silently
building a stale fork. The conversion table, ordering, factors, offsets,
regional choices, and whimsical units continue to originate from Microsoft's
file.

## Graphing replacement

The proprietary Microsoft graphing implementation is unavailable, but its
public headers remain the authoritative compatibility contract.

`GraphingInterfaceContractTests` compiles the pristine headers through the
portable `HRESULT`, `BYTE`, and `GRAPHINGAPI` compatibility definitions and
checks the principal method signatures. `GraphingInterfaceHash` checks every
interface header. Any upstream header change therefore fails the portable test
suite until the replacement backend's coverage has been reviewed.

Refreshing a graphing-contract hash without reviewing every changed signature
and feature is prohibited.

## Upstream update procedure

1. Fetch and integrate `upstream/main`.
2. Run `scripts/verify-upstream-pristine.sh upstream/main`.
3. Configure the portable build. A failed generated-overlay patch is an
   upstream compatibility event, not a reason to edit Microsoft source.
4. Build and run all CTest targets, including graphing contract checks.
5. Run resource, managed behavior, and visual snapshot tests.
6. Update adapters or generated transformations only after classifying every
   upstream change.
