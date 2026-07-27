#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
submodule_path="${1:-${repo_root}/upstream/windows-calculator}"

cd "$repo_root"

duplicated_roots=(
    src/CalcManager
    src/CalcViewModel
    src/Calculator
    src/GraphControl
    src/GraphingImpl
    src/GraphingInterfaces
)

for duplicated_root in "${duplicated_roots[@]}"; do
    if [[ -e "$duplicated_root" ]]; then
        echo "Microsoft-owned source root is duplicated in the superproject: ${duplicated_root}" >&2
        echo "Consume it only through upstream/windows-calculator." >&2
        exit 1
    fi
done

if [[ ! -e "${submodule_path}/.git" ]]; then
    echo "Microsoft Calculator submodule is unavailable at ${submodule_path}." >&2
    echo "Run: git submodule update --init --recursive" >&2
    exit 2
fi

expected_commit="$(git rev-parse ':upstream/windows-calculator')"
actual_commit="$(git -C "$submodule_path" rev-parse HEAD)"
if [[ "$actual_commit" != "$expected_commit" ]]; then
    echo "Microsoft Calculator is checked out at ${actual_commit}, but the superproject pins ${expected_commit}." >&2
    exit 1
fi

if [[ -n "$(git -C "$submodule_path" status --porcelain --untracked-files=all)" ]]; then
    echo "Microsoft Calculator submodule contains local modifications:" >&2
    git -C "$submodule_path" status --short >&2
    echo "All platform work must remain in the Redmond superproject." >&2
    exit 1
fi

echo "Microsoft Calculator submodule is pinned and pristine at ${actual_commit}."
