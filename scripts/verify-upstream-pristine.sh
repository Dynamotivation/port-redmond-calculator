#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
upstream_ref="${1:-upstream/main}"

cd "$repo_root"

if ! git rev-parse --verify --quiet "${upstream_ref}^{commit}" >/dev/null; then
    echo "Unable to verify protected Microsoft sources: ${upstream_ref} is unavailable." >&2
    echo "Fetch the upstream remote or pass an explicit upstream commit." >&2
    exit 2
fi

protected_roots=(
    src/CalcManager
    src/CalcViewModel/DataLoaders
    src/GraphingInterfaces
    src/GraphingImpl
    src/GraphControl
)

violations="$(
    git diff --name-status --diff-filter=MDRT "${upstream_ref}" -- "${protected_roots[@]}"
)"

if [[ -n "$violations" ]]; then
    echo "Protected Microsoft domain sources differ from ${upstream_ref}:" >&2
    echo "$violations" >&2
    echo "Move platform work into shims, adapters, or generated build overlays." >&2
    exit 1
fi

echo "Protected Microsoft calculation, conversion, graphing, and native sources are pristine."
