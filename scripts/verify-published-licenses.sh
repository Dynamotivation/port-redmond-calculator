#!/bin/sh
set -eu

if [ "$#" -ne 1 ]; then
    echo "Usage: scripts/verify-published-licenses.sh <publish-directory>" >&2
    exit 2
fi

publish_directory=$1
license_directory="$publish_directory/Licenses"

for relative_path in \
    Redmond-Calculator-MIT.txt \
    Redmond-Commons-MIT.txt \
    Redmond-Commons-THIRD-PARTY-NOTICES.md \
    Microsoft-Calculator-MIT.txt \
    Microsoft-Calculator-NOTICE.txt \
    THIRD_PARTY_NOTICES.md \
    GENERATED-REDISTRIBUTION-FILES.txt \
    NUGET-PACKAGES.md \
    NUGET-PACKAGES.spdx.json \
    Inter/OFL-1.1.txt \
    Latin-Modern-Math/GUST-FONT-LICENSE.txt \
    NuGet/MIT.txt \
    NuGet/BSD-3-Clause.txt \
    NuGet/CC0-1.0.txt; do
    if [ ! -s "$license_directory/$relative_path" ]; then
        echo "Published license file is missing or empty: $relative_path" >&2
        exit 1
    fi
done

while IFS= read -r relative_path; do
    if [ -z "$relative_path" ]; then
        continue
    fi
    case "$relative_path" in
        /*|../*|*/../*|*/..)
            echo "Generated license manifest contains an unsafe path: $relative_path" >&2
            exit 1
            ;;
    esac
    if [ ! -s "$license_directory/$relative_path" ]; then
        echo "Generated published license file is missing or empty: $relative_path" >&2
        exit 1
    fi
done < "$license_directory/GENERATED-REDISTRIBUTION-FILES.txt"

echo "Published license bundle is complete: $license_directory"
