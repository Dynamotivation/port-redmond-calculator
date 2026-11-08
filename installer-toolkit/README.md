# Dynamotivation installer toolkit

How every Dynamotivation Windows installer is built. A knowledge repo: copy
`setup/`, `tools/` and `assets/` into a new app's repo and you get the same
branded, per-user, per-DPI-crisp setup.

## Decisions (don't reopen these without a reason)

- **Inno Setup 5.6.1** everywhere, for consistency across all apps. Kept even
  even though Inno 5's stock icons are dated. Each app sets its own Windows floor
  with `MinVersion` (Software Hub: Windows 10; the GameMaker games: Windows 7 SP1).
  A single number is the Windows NT version, so `6.1.7601` means Windows 7 SP1.
- **Per-user installs** (`PrivilegesRequired=lowest`), fixed install path
  `...\Programs\Dynamotivation\<App>`, no folder page, no language dialog.
  Setup language follows the Windows display language (en/de).
- **No platform tags, no badges** in the wizard. United brand artwork.
- **One icon everywhere:** the app exe embeds the same `.ico` as the setup.
- **Self-update, no code in app scripts:** the app downloads the setup into its own
  cache folder and checks its SHA-256. Apps that run the setup themselves start it
  after they have quit (a Hub-style app first shows "update ready", installs on
  "restart" or on close, `/VERYSILENT /SUPPRESSMSGBOXES /NORESTART`, and reopens
  itself from the app side); apps that hand over to the wizard start it as it is
  and quit (the generator). Setup's own "close applications" page stays the fallback.
  The uninstaller removes caches and other recreatable files with `[UninstallDelete]`.
- **Settings and saves are the user's choice:** an app lists the folders holding them
  in `UserDataDirs`; after the uninstall confirmation a checked "Keep my settings and
  saved files" box decides. Silent uninstalls keep them unless `/REMOVEUSERDATA=1`.

## Layout

```
setup/common.iss          shared [Setup]/[Tasks]/[Icons]/[Run] directives
setup/<app>.iss           per-app script (see setup/software-hub.iss example)
tools/render_assets.py    renders wizard + ico artwork from SVG masters
tools/capture_window.py   QA helper: screenshot a Setup window to PNG
assets/setup-icon.svg     the installer icon master (badge cut out of the D)
assets/app-icon.svg       optional unbadged mark for the wizard artwork (default: setup-icon.svg); also renders assets/app.ico for the app exe
assets/full-logo.svg      the official roundel (from dynamotivation-assets)
assets/…                  example generated output for the Software Hub
```

## Compile an installer

```
ISCC /DAppVersion=x.y.z [/DSignEnabled] setup\<app>.iss
```

Pass the built Release folder explicitly, because Inno resolves relative
`Source:` paths against the shell's working directory, against which a
double-up relative SourceDir is situational:

```
ISCC /DAppVersion=x.y.z /DSourceDir=C:\path\to\app\build\...\Release setup\<app>.iss
```

Local Inno lives at `D:\tools\InnoSetup5` (5.6.1). `common.iss` expects the app
script to define `AppId` (GUID, doubled brace), `AppName`, `AppExeName`,
`MinVersion` and `SourceDir` (the built Release folder) before including it.
Setups are 64-bit only by default. A 32-bit app defines
`AllowedArchitectures "x86 x64"` and `Is32BitApp` first, otherwise 32-bit Windows
is refused and the app is installed in 64-bit mode.
CI example: see the Software Hub's `.github/workflows/ci.yml`, whose
`windows-installer` job downloads Inno 5.6.1 with a SHA-256 pin, signs and
compiles. Build locally first; CI runs only on request or release tags.

## Artwork pipeline

One command regenerates the multi-size wizard BMPs and the multi-size `setup.ico`:

```
py tools\render_assets.py
```

- Wizard artwork renders from one SVG per shape. The side panel stacks the
  official roundel (`assets/full-logo.svg`) above the installer mark at equal
  size, a burgundy gradient divider, then the wordmark in Rubik.
- Inno scales by DPI; supply the full set or the 100% image is stretched
  instead. Side panel (`WizardImageFile`, aspect 164:314):
  100% 164x314, 125% 202x386, 150% 240x459, 175% 290x556, 200% 315x604,
  225% 366x700, 250% 416x797. Corner glyph (`WizardSmallImageFile`, square):
  58/71/85/103/112/129/147. Comma-list the files; Inno picks the best match.
- The `.ico` uses `mask` to cut the download-badge circle out of the D *flush*
  (hole radius >= badge radius, 0.8 px headroom). A larger hole leaves a ring
  of empty pixels that looks broken on dark surfaces.
- BMPs must be 24-bit; PNG entries are not accepted for wizard images on 5.6.

### Tools it needs

- [resvg](https://github.com/linebender/resvg): `resvg.exe` on `PATH` or pass
  its path as the first argument.
- Rubik TTFs (OFL, see below). Pass `--font-dir`; defaults to `D:\tools\resvg\fonts`
  with `Rubik-Regular.ttf`/`Rubik-Bold.ttf` instanced from Google's variable
  font (`fontTools.varLib.instancer`). The woff2 files in the website repo
  mislabel their family as "Rubik Light" - don't use them directly.
- Pillow + fonttools + brotli (`py -m pip install pillow fonttools brotli`).

## License notes

Rubik is SIL OFL 1.1. The setup only ships rendered pixels, so no attribution
is required. If a shipped artifact ever embeds the font *files* (app fonts,
linux packages), they must be accompanied by `OFL.txt`.

## Gotchas worth their bytes

- `CloseApplications=yes` shows a files-in-use page when the app is running
  while installing (Restart Manager). Normal when closed; expected annoyance
  when the app sits in the tray. Don't "fix" with `CloseApplications=no`.
- After changing only `app_icon.ico`, force the resource recompile: touch the
  `.rc` file and rebuild with the exe **not** running (LNK1104 otherwise).
  Flutter may link the old icon otherwise.
- The Windows taskbar caches the pill icon per exe path, so your very first
  launch's icon can stick even after the exe is fixed. Rename the exe/folder
  copy to verify; only an icon-cache refresh or restart fixes the old path.
- Inno 5.6.1's Language dialog cannot be restyled; turn it off
  (`ShowLanguageDialog=no`) and let setup pick by Windows language.
- Unsigned downloads trigger SmartScreen. CI picks up the PFX secret
  (`SIGNING_CERT_PFX_BASE64` / `SIGNING_CERT_PASSWORD`) and signs exes, DLLs
  and the uninstaller; without the secret the build just skips signing.
