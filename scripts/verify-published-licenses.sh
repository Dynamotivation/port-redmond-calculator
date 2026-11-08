#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    echo "Usage: scripts/verify-published-licenses.sh <publish-directory>" >&2
    exit 2
fi

publish_directory=$1
license_directory="$publish_directory/Licenses"
inventory="$license_directory/sbom.spdx.json"

if [ ! -s "$inventory" ]; then
    echo "Published SPDX inventory is missing or empty: Licenses/sbom.spdx.json" >&2
    exit 1
fi

# The inventory names every license file of every component; each must have been published.
# The inventory is generated with one property per line, a license-file reference's locator
# directly after its type.
license_files=$(awk '/"referenceType": "license-file"/ { getline; sub(/.*"referenceLocator": "/, ""); sub(/".*/, ""); print }' "$inventory")
if [ -z "$license_files" ]; then
    echo "The published SPDX inventory lists no license files." >&2
    exit 1
fi

# Components that must always be present, so an inventory that silently lost them fails.
for required_component in "Redmond Calculator" "Microsoft Calculator" ".NET runtime" "Inter" "Avalonia"; do
    if ! grep -q "^      \"name\": \"$required_component\"," "$inventory"; then
        echo "The published SPDX inventory does not list $required_component." >&2
        exit 1
    fi
done

count=0
while IFS= read -r relative_path; do
    if [ -z "$relative_path" ]; then
        continue
    fi
    case "$relative_path" in
        /*|../*|*/../*|*/..)
            echo "The SPDX inventory contains an unsafe path: $relative_path" >&2
            exit 1
            ;;
    esac
    if [ ! -s "$license_directory/$relative_path" ]; then
        echo "Published license file is missing or empty: $relative_path" >&2
        exit 1
    fi
    count=$((count + 1))
done <<EOF
$license_files
EOF

echo "Published license bundle is complete ($count license files): $license_directory"
