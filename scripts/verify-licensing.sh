#!/bin/sh
set -eu

repository_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
project="$repository_root/src/Frontends/Calculator.Avalonia/Calculator.Avalonia.csproj"
audit_project="$repository_root/tools/LicenseAudit/LicenseAudit.csproj"
assets="$repository_root/src/Frontends/Calculator.Avalonia/obj/project.assets.json"
audit_assets="$repository_root/tools/LicenseAudit/obj/project.assets.json"
font="$repository_root/src/Frontends/Calculator.Avalonia/Assets/LatinModernMath-Regular.otf"
expected_font_hash=6075562b771f8b82f0c179e363389684f2dd09de30038269e2628e504bd7be0f

for required_file in \
    "$repository_root/LICENSE" \
    "$repository_root/redmond-commons/LICENSE" \
    "$repository_root/redmond-commons/THIRD_PARTY_NOTICES.md" \
    "$repository_root/redmond-commons/licenses/Microsoft-Calculator-MIT.txt" \
    "$repository_root/upstream/windows-calculator/LICENSE" \
    "$repository_root/upstream/windows-calculator/NOTICE.txt"; do
    if [ ! -s "$required_file" ]; then
        echo "Required licensing file is missing or empty: $required_file" >&2
        exit 1
    fi
done

if command -v shasum >/dev/null 2>&1; then
    actual_font_hash=$(shasum -a 256 "$font" | awk '{print $1}')
elif command -v sha256sum >/dev/null 2>&1; then
    actual_font_hash=$(sha256sum "$font" | awk '{print $1}')
else
    echo "Neither shasum nor sha256sum is available." >&2
    exit 1
fi

if [ "$actual_font_hash" != "$expected_font_hash" ]; then
    echo "Latin Modern Math asset does not match the reviewed upstream font." >&2
    echo "Expected: $expected_font_hash" >&2
    echo "Actual:   $actual_font_hash" >&2
    exit 1
fi

if [ ! -s "$assets" ] || [ ! -s "$audit_assets" ]; then
    echo "Resolved assets are unavailable. Restore Calculator.Avalonia and LicenseAudit first:" >&2
    echo "  dotnet restore $project -p:NuGetAudit=false" >&2
    echo "  dotnet restore $audit_project -p:NuGetAudit=false" >&2
    exit 1
fi

dotnet run --project "$audit_project" --no-restore -- \
    --verify \
    --root "$repository_root" \
    --assets "$assets"

echo "Licensing verification passed."
