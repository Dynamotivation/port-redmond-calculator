#!/usr/bin/env bash
# Tags the exact pushed main commit and publishes the signed setup as a GitHub Release.
# The release stays a draft until both assets are uploaded, so nobody sees it half done.
# Re-running is safe: an existing tag must already point at this commit, and a release
# that is already published is left alone.
set -euo pipefail

: "${RELEASE_VERSION:?Release version is required}"
: "${GITHUB_SHA:?Exact main commit is required}"
: "${GITHUB_REPOSITORY:?Repository is required}"
[[ "$GITHUB_EVENT_NAME" == push && "$GITHUB_REF" == refs/heads/main ]] || {
  echo 'Only a main push may publish a release.' >&2
  exit 1
}
tag="v$RELEASE_VERSION"
setup="dist/redmond-calculator-setup-$RELEASE_VERSION-windows-x64.exe"
notes="release-notes/$RELEASE_VERSION.md"
test -s "$setup"
test -s "$setup.sha256"
test -s "$notes"
(cd dist && sha256sum --check "$(basename "$setup.sha256")")

if git show-ref --verify --quiet "refs/tags/$tag"; then
  [[ "$(git rev-parse "$tag^{commit}")" == "$GITHUB_SHA" ]] || {
    echo "Tag $tag already points at another commit; refusing to move it." >&2
    exit 1
  }
else
  git tag "$tag" "$GITHUB_SHA"
  git push origin "refs/tags/$tag"
fi

if gh release view "$tag" --json isDraft --jq '.isDraft' > release-state.txt; then
  if [[ "$(cat release-state.txt)" == false ]]; then
    echo "Release $tag is already published for this commit."
    exit 0
  fi
else
  gh release create "$tag" --verify-tag --target "$GITHUB_SHA" --draft --title "$tag" --notes-file "$notes"
fi
gh release upload "$tag" "$setup" "$setup.sha256" --clobber
release_id=$(gh release view "$tag" --json databaseId --jq '.databaseId')
# GitHub chooses the latest release by version, including when two builds finish out of
# order. Publish only after both assets have been uploaded.
gh api --method PATCH "repos/$GITHUB_REPOSITORY/releases/$release_id" \
  -F draft=false -f make_latest=legacy --silent
