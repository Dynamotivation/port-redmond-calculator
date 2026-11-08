"""Require a new release version, with release notes, before a change that ships can merge.

Every push to main that carries a version nobody has released builds and publishes it, so
every pull request that changes more than workflows and documentation must carry that
version and its notes:

* the version in Directory.Build.props is greater than every published release and not
  lower than the base branch's,
* release-notes/<version>.md was added by the pull request, and
* the notes are bullet points, one change per line, written for people who use the app.

Pull requests that only touch .github/** or *.md files release nothing and need neither.
"""

import os
from pathlib import Path
import re
import subprocess

PROPS = "Directory.Build.props"
NOTES = "release-notes"
RELEASE_TAG = re.compile(r"^v(\d+\.\d+\.\d+)$")


def parse_version(text):
    """The <Version> of a props file as MAJOR.MINOR.PATCH."""
    match = re.search(r"<Version>\s*(\d+\.\d+\.\d+)\s*</Version>", text)
    if not match:
        raise ValueError(f"{PROPS} must set <Version> to MAJOR.MINOR.PATCH.")
    version = match[1]
    if any(str(int(part)) != part for part in version.split(".")):
        raise ValueError("Version components must not contain leading zeroes.")
    return version


def as_tuple(version):
    return tuple(int(part) for part in version.split("."))


def requires_release(path):
    """Workflows and documentation never ship, everything else does."""
    return not (path.startswith(".github/") or path.endswith(".md"))


def require_new_version(version, base_version, released):
    newest = max(map(as_tuple, released), default=(0, 0, 0))
    if as_tuple(version) <= newest:
        raise ValueError(
            f"Version {version} must be greater than the newest release, "
            f"{'.'.join(map(str, newest))}. Raise <Version> in {PROPS}."
        )
    if as_tuple(version) < as_tuple(base_version):
        raise ValueError(f"Version {version} is lower than the base branch's {base_version}.")


def validate_notes(content, path):
    lines = [line for line in content.splitlines() if line.strip()]
    if not lines:
        raise ValueError(f"{path} is empty; list what changed for people who use the app.")
    for number, line in enumerate(content.splitlines(), start=1):
        if line.strip() and not re.match(r"^- \S", line):
            raise ValueError(
                f"{path}:{number} must be a bullet point starting with '- ' "
                f"(one change per line, no headings or paragraphs): {line!r}"
            )


def read_notes(version):
    path = Path(NOTES) / f"{version}.md"
    try:
        content = path.read_text(encoding="utf-8")
    except OSError as error:
        raise ValueError(f"{path} is missing; every release records its notes there.") from error
    validate_notes(content, path)
    return path


def git(*args):
    return subprocess.check_output(["git", *args], text=True)


def released_versions():
    """Published release tags (vMAJOR.MINOR.PATCH) of this repository."""
    output = subprocess.check_output(
        ["gh", "release", "list", "--limit", "200", "--json", "tagName", "--jq", ".[].tagName"],
        text=True,
    )
    return [match[1] for tag in output.split() if (match := RELEASE_TAG.match(tag))]


def main():
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    version = parse_version(Path(PROPS).read_text(encoding="utf-8"))
    released = released_versions()
    required = True

    if event == "pull_request":
        base = os.environ.get("BASE_SHA", "")
        if not re.fullmatch(r"[0-9a-fA-F]{40}", base) or set(base) == {"0"}:
            raise ValueError("A valid base commit is required for the version comparison.")
        changed = git("diff", "--name-only", f"{base}...HEAD").splitlines()
        required = any(requires_release(path) for path in changed)
        if required:
            require_new_version(version, parse_version(git("show", f"{base}:{PROPS}")), released)
            notes = read_notes(version)
            if subprocess.run(["git", "cat-file", "-e", f"{base}:{notes.as_posix()}"],
                              stderr=subprocess.DEVNULL).returncode == 0:
                raise ValueError(
                    f"{notes} already exists on the base branch. Raise <Version> and add notes "
                    "for the new version; a change that ships needs its own."
                )
            print(f"Release version: {version} ({notes})")
        else:
            print("Only workflows or documentation changed; no new version needed.")
    else:
        notes = read_notes(version)
        print(f"Version: {version} ({notes}); "
              f"{'already released' if version in released else 'not released yet'}")

    if output := os.environ.get("GITHUB_OUTPUT"):
        with open(output, "a", encoding="utf-8") as stream:
            stream.write(
                f"version={version}\ntag=v{version}\nrequired={str(required).lower()}\n"
                f"release={str(version not in released).lower()}\n"
            )


if __name__ == "__main__":
    main()
