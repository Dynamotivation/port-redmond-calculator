; Redmond Calculator. A self-contained .NET 10 app (64-bit only), so the floor is .NET 10's own:
; Windows 10 version 1607 (10.0.14393) or newer. Windows 7, 8 and 8.1 are not supported by .NET 10
; (https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), and the single
; SHA-256 signature is readable on every system that can run the app.
; Compile: iscc /DAppVersion=x.y.z /DSourceDir=<absolute publish folder> installer\redmond-calculator.iss
#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z
#endif
#define AppId "{{6B8E3755-3E3D-42E9-9541-360C1EF37D0B}"
#define AppName "Redmond Calculator"
#define AppExeName "Redmond Calculator.exe"
#define OutputBase "redmond-calculator"
#define MinVersion "10.0.14393"
; The app's per-user settings folder (%APPDATA%\RedmondCalculator).
#define AppDataName "RedmondCalculator"
; The toolkit lives one level above installer/ as a git subtree; AssetDir paths
; resolve relative to THIS script's folder.
#define AssetDir "assets"
#ifndef SourceDir
  #error Pass /DSourceDir=<absolute path to the publish folder>
#endif

#include "..\installer-toolkit\setup\common.iss"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[UninstallDelete]
; The app writes its settings here; Setup never installed it, so without this the
; uninstaller leaves the folder behind.
Type: filesandordirs; Name: "{userappdata}\{#AppDataName}"
Type: dirifempty; Name: "{app}"
