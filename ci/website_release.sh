#!/usr/bin/env bash
# Records a public release on dynamotivation.com so the Software Hub and the website bring
# the new version to every device, like the other Dynamotivation apps do. Requires the
# release to already exist (publish_release.sh must have run).
#
# Required secret: website_release_data_token, a fine-grained PAT on
# Dynamotivation/dynamotivation.com with Contents and Pull requests read/write. The release
# itself is read with the default GH_TOKEN.
#
# The release notes become the website highlights. On success the live catalog serves the
# new version. Re-running is safe: add_release.py replaces the entry in place and an
# already-merged website PR short-circuits.
set -euo pipefail

: "${RELEASE_VERSION:?Release version is required}"
: "${WEBSITE_RELEASE_DATA_TOKEN:?Secret website_release_data_token is required}"
: "${GITHUB_REPOSITORY:?Repository is required}"
[[ "$GITHUB_EVENT_NAME" == push && "$GITHUB_REF" == refs/heads/main ]] || {
  echo 'Only a main push records a website release.' >&2
  exit 1
}

WEBSITE_REPO="Dynamotivation/dynamotivation.com"
WEBSITE_SLUG="redmond-calculator"
setup="dist/redmond-calculator-setup-$RELEASE_VERSION-windows-x64.exe"

test -s "$setup"
test -s "$setup.sha256"
notes="release-notes/$RELEASE_VERSION.md"
test -s "$notes" || { echo "Missing $notes, the website highlights." >&2; exit 1; }

# SHA-256 file, content digest and data all must be in sync before anything reaches the website.
(cd dist && sha256sum --check "$(basename "$setup.sha256")")
sha256="$(cut -d' ' -f1 "$setup.sha256" | tr 'A-F' 'a-f')"
size="$(stat -c%s "$setup")"
url="https://github.com/$GITHUB_REPOSITORY/releases/download/v$RELEASE_VERSION/$(basename "$setup")"
released_on="$(gh release view "v$RELEASE_VERSION" --json publishedAt --jq '.publishedAt[:10]')"

# The website token both clones and pushes; the job's own token cannot write there.
export GH_TOKEN_RELEASE="$GH_TOKEN"
export GH_TOKEN="$WEBSITE_RELEASE_DATA_TOKEN"
gh auth setup-git
gh repo clone "$WEBSITE_REPO" website -- --depth 1
export GH_TOKEN="$GH_TOKEN_RELEASE"
(
  cd website
  git config user.name 'redmond-calculator-release-bot'
  git config user.email 'redmond-calculator-release-bot@users.noreply.github.com'
  branch="chore/redmond-calculator-release-$RELEASE_VERSION"
  git checkout -b "$branch"
  # The highlights are the bullet text of the notes, one per line.
  sed 's/^- //' "../$notes" > ../highlights.txt
  python3 scripts/add_release.py --slug "$WEBSITE_SLUG" --version "$RELEASE_VERSION" \
    --date "$released_on" --highlights-file ../highlights.txt \
    --download windows x64 "$url" "$sha256" "$size"
  python3 scripts/validate_releases.py
  if git diff --quiet; then
    echo "Website already lists $RELEASE_VERSION."
  else
    git commit -am "Release Redmond Calculator $RELEASE_VERSION"
    # git's credential helper (gh) reads GH_TOKEN at push time, so push with the website token.
    with_website_token() { GH_TOKEN="$WEBSITE_RELEASE_DATA_TOKEN" GH_REPO="$WEBSITE_REPO" "$@"; }
    with_website_token git push --force origin "$branch"
    pr="$(with_website_token gh pr list --head "$branch" --state open --json number --jq '.[0].number // empty')"
    if [[ -z "$pr" ]]; then
      with_website_token gh pr create --base main --head "$branch" \
        --title "Release Redmond Calculator $RELEASE_VERSION" \
        --body "Recorded by $GITHUB_SERVER_URL/$GITHUB_REPOSITORY/actions/runs/$GITHUB_RUN_ID."
      pr="$(with_website_token gh pr list --head "$branch" --state open --json number --jq '.[0].number')"
    fi
    with_website_token gh pr merge "$pr" --merge --delete-branch
  fi
)

# The website deploys on every push to its main; the release is only done once the live
# catalog and the release history really serve the new version.
base=https://dynamotivation.com/v1
latest=''
for _ in $(seq 1 40); do
  latest="$(curl -fsS "$base/catalog.json/" | python3 -c "
import json, sys
d = json.load(sys.stdin)
print(next(s for s in d['software'] if s['url'].rstrip('/').endswith('/$WEBSITE_SLUG'))['releases'][0]['version'])" 2>/dev/null || true)"
  if [[ "$latest" == "$RELEASE_VERSION" ]]; then
    curl -fsS "$base/software/$WEBSITE_SLUG/releases.json/" | python3 -c "
import json, sys
d = json.load(sys.stdin)
assert d['releases'][0]['version'] == '$RELEASE_VERSION'"
    echo "Live: catalog and release history serve $RELEASE_VERSION."
    exit 0
  fi
  sleep 15
done
echo "::error::dynamotivation.com still serves '${latest:-nothing}' after 10 minutes" >&2
exit 1
