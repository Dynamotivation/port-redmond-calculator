#!/bin/sh
set -eu

project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
publish_dir=${1:-"$project_dir/bin/Release/net10.0/osx-arm64/publish"}
publish_dir=$(CDPATH= cd -- "$publish_dir" && pwd)
bundle="$publish_dir/Redmond Calculator.app"
contents="$bundle/Contents"

if [ ! -f "$publish_dir/libhostfxr.dylib" ] ||
   [ ! -f "$publish_dir/libcoreclr.dylib" ]; then
    echo "Refusing to package a framework-dependent build." >&2
    echo "Publish with '-r osx-arm64 --self-contained true' first." >&2
    exit 1
fi

if [ ! -f "$publish_dir/Licenses/Redmond-Calculator-MIT.txt" ] ||
   [ ! -f "$publish_dir/Licenses/THIRD_PARTY_NOTICES.md" ] ||
   [ ! -f "$publish_dir/Licenses/Dotnet-Runtime/ThirdPartyNotices.txt" ]; then
    echo "Refusing to package a build without its license bundle." >&2
    echo "Publish Calculator.Avalonia again so Licenses is populated." >&2
    exit 1
fi

if [ -e "$bundle" ]; then
    echo "Bundle already exists: $bundle" >&2
    exit 1
fi
mkdir -p "$contents/MacOS" "$contents/Resources"
cp "$project_dir/Packaging/macos/Info.plist" "$contents/Info.plist"
cp "$project_dir/Assets/RedmondCalculatorIcon.icns" "$contents/Resources/RedmondCalculatorIcon.icns"

find "$publish_dir" -maxdepth 1 -type f -exec cp {} "$contents/MacOS/" \;
if [ -d "$publish_dir/Resources" ]; then
    cp -R "$publish_dir/Resources" "$contents/MacOS/Resources"
fi
cp -R "$publish_dir/Licenses" "$contents/Resources/Licenses"

echo "$bundle"
