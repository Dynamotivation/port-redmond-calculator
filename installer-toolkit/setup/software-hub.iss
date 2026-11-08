; Dynamotivation Software Hub. Flutter requires Windows 10 or newer, so the floor is 10.0.
; Compile: iscc /DAppVersion=0.1.0 /Sdyn=<sign-command> installer\software-hub.iss
#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z
#endif
#define AppId "{{6F0B3E52-2C58-4C0B-9F5E-1D7A4B8E9C31}"
#define AppName "Dynamotivation Software Hub"
#define AppExeName "dynamotivation_software_hub.exe"
#define OutputBase "dynamotivation-software-hub"
#define MinVersion "10.0.10240"
; The app's per-user data folder (its catalog cache and staged self-update setups).
#define AppDataName "Dynamotivation Software Hub"
; Toast identity the app registers (hubAppUserModelId in the app).
#define NotificationId "Dynamotivation.SoftwareHub"
#ifndef SourceDir
  #define SourceDir "..\apps\windows\build\windows\x64\runner\Release"
#endif
; Assets live one level above setup/ so the toolkit layout works as a subtree too.
#define AssetDir "..\assets"
; Settings and saved files the uninstaller offers to keep (see common.iss): the setup
; logs the Hub writes and the settings folders of its 2026 prototypes.
#define UserDataDirs "{localappdata}\" + AppDataName + "\Logs|{userappdata}\Dynamotivation\SoftwareHub|{userappdata}\Dynamotivation\Dynamotivation Software Hub"

#include "common.iss"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Registry]
; The app registers its toast identity (name, icon, click handler) under these keys the
; first time it shows a notification. Setup never wrote them, so remove them on uninstall.
Root: HKCU; Subkey: "Software\Classes\AppUserModelId\{#NotificationId}"; Flags: uninsdeletekey dontcreatekey
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\PushNotifications\Backup\{#NotificationId}"; Flags: uninsdeletekey dontcreatekey

[InstallDelete]
; Older versions showed toasts through a Start menu shortcut named after the app.
Type: files; Name: "{userprograms}\{#AppName}.lnk"

[UninstallDelete]
; The app writes its cache and the setup of the next version here; Setup never installed
; it, so without this the uninstaller leaves the folder behind. The cache is always
; removed; the logs next to it only when the user does not keep their data (UserDataDirs).
Type: filesandordirs; Name: "{localappdata}\{#AppDataName}\Cache"
Type: dirifempty; Name: "{localappdata}\{#AppDataName}"
Type: dirifempty; Name: "{userappdata}\Dynamotivation"
Type: dirifempty; Name: "{app}"
