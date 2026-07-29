# Third-party notices

Redmond Calculator's original code is licensed under the repository's MIT
License. This notice identifies separately owned code, assets, and runtime
components used by the project. Their licenses apply independently.

Release builds carry the complete redistribution bundle in `Licenses`.
The source materials for that bundle are in
`src/Frontends/Calculator.Avalonia/Packaging/licenses`.

## Microsoft Calculator

The calculator engine, upstream resources, Calculator icon font, and other
Microsoft-originated material come from the pinned Microsoft Calculator
submodule:

- Source: <https://github.com/microsoft/calculator>
- License: `upstream/windows-calculator/LICENSE`
- Notices: `upstream/windows-calculator/NOTICE.txt`

Microsoft Calculator is licensed under MIT and retains its own copyright and
third-party notices. Redmond Calculator does not modify that submodule.

## Redmond Commons

The shared controls, windowing components, and shortcut engine in the
`redmond-commons` submodule are separately licensed under MIT. Its complete
license is in `redmond-commons/LICENSE`. Redmond Commons also embeds a
byte-identical copy of Microsoft Calculator's icon font; its provenance and
license are recorded in `redmond-commons/THIRD_PARTY_NOTICES.md` and are
covered by the Microsoft license shipped in this application's bundle.

## Managed package dependencies

The exact direct and transitive NuGet dependency graph is generated from the
resolved `project.assets.json`:

- `Licenses/NUGET-PACKAGES.md` records package version, relationship, declared
  license, copyright or authors, and source.
- `Licenses/NUGET-PACKAGES.spdx.json` provides the same inventory as SPDX 2.3.
- `Licenses/NuGet` contains complete MIT, BSD 3-Clause, and CC0 1.0 texts used
  by the current resolved graph.
- `Licenses/Native-Dependencies` contains the exact ANGLE license and the
  native SkiaSharp/HarfBuzz third-party notice set copied from the resolved
  packages by `scripts/update-licensing.sh`.

Run `scripts/update-licensing.sh` whenever package references or resolved
versions change. The verifier rejects native platform packages with divergent
notice sets until each variant is preserved explicitly.

## Bundled fonts

### Inter

`Avalonia.Fonts.Inter` supplies the Inter font used by the application.

Copyright (c) 2016 The Inter Project Authors.

Inter is licensed under the SIL Open Font License, Version 1.1. Its copyright
notice and complete license are in `Licenses/Inter`.

### Latin Modern Math

The graph equation editor bundles the canonical Latin Modern Math font used by
the CSharpMath renderer. The checked asset is byte-identical to version 1.959
from the upstream CTAN distribution.

Copyright 2012–2014 B. Jackowski, P. Strzelczyk, and P. Pianowski on behalf of
TeX users groups.

Latin Modern Math is distributed under the GUST Font License. The complete
license, manifest, and upstream README are in
`Licenses/Latin-Modern-Math`.

### Fonts embedded by CSharpMath

The CSharpMath rendering assemblies also contain Latin Modern Math under the
GUST Font License and Cyrillic Modern and AMS Capital Blackboard Bold under the
SIL Open Font License, Version 1.1. Sources and complete-license locations are
recorded in `Licenses/CSharpMath-Embedded-Fonts.txt`.

## Self-contained .NET runtime

Self-contained releases include the Microsoft .NET runtime. Its MIT license and
Microsoft-maintained third-party notices are included in
`Licenses/Dotnet-Runtime`. Framework-dependent builds do not redistribute the
runtime, but retaining these files in the common bundle is harmless and keeps
release artifacts consistent. The update and verification scripts locate the
active official .NET distribution, so an SDK/runtime upgrade refreshes or
invalidates this snapshot automatically.

## No endorsement

Third-party names are used for attribution and compatibility description.
Nothing in these notices implies sponsorship or endorsement. See
`TRADEMARKS.md`.
