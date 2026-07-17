#!/bin/sh
set -eu

project_dir=$(CDPATH= cd -- "$(dirname -- "$0")/../.." && pwd)
publish_dir=${1:-"$project_dir/bin/Release/net10.0/osx-arm64/publish"}
publish_dir=$(CDPATH= cd -- "$publish_dir" && pwd)
bundle="$publish_dir/Calculator Avalonia Slice.app"
contents="$bundle/Contents"

if [ -e "$bundle" ]; then
    echo "Bundle already exists: $bundle" >&2
    exit 1
fi
mkdir -p "$contents/MacOS"
cp "$project_dir/Packaging/macos/Info.plist" "$contents/Info.plist"

find "$publish_dir" -maxdepth 1 -type f -exec cp {} "$contents/MacOS/" \;

echo "$bundle"
