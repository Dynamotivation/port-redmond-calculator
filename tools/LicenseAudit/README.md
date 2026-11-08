# LicenseAudit

Generates and verifies the license inventory (`scripts/update-licensing.sh`, `scripts/verify-licensing.sh`):

- `licenses/nuget/<package>/LICENSE.txt`: one license text per resolved NuGet package. The package's own license file is used when it ships one; otherwise the standard text from `templates/` is used with the copyright the package declares.
- `licenses/<component>/`: the files listed for each component in `licenses/components.json`. A file with a `sync` source is copied from it (a repository file, a file of a resolved package, a native notice set, or the .NET distribution) and compared on verify.
- `licenses/sbom.spdx.json`: the SPDX 2.3 inventory of every component, including this project's own. It records the application version, so a version bump needs a regenerated file.
- the index between the `licenses` markers in `README.md`.

A NuGet license that is not MIT, BSD-3-Clause or CC0-1.0 fails until it has been reviewed and allowed in `Program.cs`.

The .NET notices are read from the root of the active .NET distribution. Set `DOTNET_NOTICES_ROOT` to a distribution that was installed whole (CI installs the pinned SDK into its own directory) when the active one may not carry the pinned version's notices, for example a runner image whose SDK was updated with `--skip-non-versioned-files`.
