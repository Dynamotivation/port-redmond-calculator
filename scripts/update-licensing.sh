#!/bin/sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
project="$repository_root/src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj"
audit_project="$repository_root/tools/LicenseAudit/LicenseAudit.csproj"
assets="$repository_root/src/Frontends/Calculator.Avalonia/obj/project.assets.json"
audit_assets="$repository_root/tools/LicenseAudit/obj/project.assets.json"
license_root="$repository_root/src/Frontends/Calculator.Avalonia/Packaging/licenses"

if [ ! -s "$assets" ] || [ ! -s "$audit_assets" ]; then
    echo "Resolved assets are unavailable. Restore these projects first:" >&2
    echo "  dotnet restore $project -p:NuGetAudit=false" >&2
    echo "  dotnet restore $audit_project -p:NuGetAudit=false" >&2
    exit 1
fi

dotnet run --project "$audit_project" --no-restore -- \
    --write \
    --assets "$assets" \
    --notices "$license_root/NUGET-PACKAGES.md" \
    --spdx "$license_root/NUGET-PACKAGES.spdx.json" \
    --license-root "$license_root"

echo "Updated dependency, native-binary, and .NET redistribution notices."
