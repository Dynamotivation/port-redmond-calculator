; Per-app setup script: copy this file, rename to <app>.iss, fill the defines, and
; re-render assets with the app's own masters (installer\tools\render_assets.py).
; Compile:
;   iscc /DAppVersion=x.y.z /DSourceDir=<absolute Release or bundle folder> setup\<app>.iss
; The toolkit supplies setup/ (this+common.iss) and assets/ (brand element grouping).

#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z
#endif

; --- Identity ------------------------------------------------------------------
#define AppId "{{<GUID>, one per app, regenerates on merge}}"
#define AppName "<Product name exactly as it should appear>"
#define AppExeName "<exe file name inside the bundle>"
#define OutputBase "<product-slug>"
; Brand elements live one level above setup/ so the toolkit layout works as a subtree too.
#define AssetDir "..\assets"
; Optional publisher override (default Dynamotivation).
;#define Publisher "Dynamotivation"
; Folders outside {app} that hold the user's settings or saves, separated by "|". With
; this defined, the uninstaller offers to keep them (see common.iss); leave it out if the
; app keeps nothing there.
;#define UserDataDirs "{localappdata}\<app data folder>"
; --- Content -------------------------------------------------------------------
; Where the unpacked application bundle lives. CI passes it as /DSourceDir.
#ifndef SourceDir
  #error Pass /DSourceDir=<absolute path to the bundle folder>
#endif

#include "common.iss"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

; Optional DirectX 9.0c prerequisite (e.g. for GameMaker runtimes).
; The installer carries dxwebsetup.exe; CI downloads it from Microsoft's
; official side (URL and SHA pinned in the workflow) and stages it.
;#ifdef DirectXSetup
;[Tasks]
;Name: "directx"; Description: "{cm:InstallDirectX}"; GroupDescription: "{cm:RuntimeComponents}"; Flags: checkedonce
;[Files]
;Source: "directx\dxwebsetup.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall
;[Run]
;Filename: "{tmp}\dxwebsetup.exe"; Parameters: "/Q"; Tasks: "directx"; Flags: nowait; StatusMsg: "Installing DirectX runtime..."
;#endif
