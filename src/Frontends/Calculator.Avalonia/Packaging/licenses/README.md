# Redmond Calculator license bundle

This directory is copied to `Licenses` in build and publish output. A packaged
macOS application places the same directory at
`Redmond Calculator.app/Contents/Resources/Licenses`.

- The repository-level license and notices are added by the project file.
- `NUGET-PACKAGES.md` and `NUGET-PACKAGES.spdx.json` inventory the exact
  resolved managed dependency graph.
- `NuGet` contains the complete license texts referenced by that inventory.
- `Inter` and `Latin-Modern-Math` cover bundled font software.
- `Dotnet-Runtime` covers the Microsoft .NET runtime included by self-contained
  distributions.
- `Native-Dependencies` is refreshed from the resolved ANGLE, SkiaSharp, and
  HarfBuzzSharp packages so their exact upstream notices accompany native
  binaries.

`scripts/update-licensing.sh` refreshes the package inventory, SPDX document,
native-package notices, and .NET notices from the active toolchain.
`scripts/verify-licensing.sh` compares every generated copy with those resolved
sources and fails if any source has changed.
`GENERATED-REDISTRIBUTION-FILES.txt` lets the publish verifier follow additions
or removals without maintaining a second hard-coded package list.
