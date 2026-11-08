; Shared Inno Setup 5.6.1 settings for every Dynamotivation Windows app.
; An app script defines AppId (as "{{GUID}", the doubled brace is Inno's escape), AppName, AppVersion, AppExeName, SourceDir, OutputBase
; and MinVersion before including this file. Compile with ISCC 5.6.1 (Unicode).
;
; MinVersion is the app's own Windows floor, e.g. "10.0.10240" for a Flutter app or
; "6.1.7601" (Windows 7 SP1) for a GameMaker game. A single number applies to
; Windows NT, which is what every supported Windows is; don't write "0,6.1.7601".
;
; Optional, for apps that are not 64-bit only (the default):
;   #define AllowedArchitectures "x86 x64"   let 32-bit Windows install it too
;   #define Is32BitApp                        install in 32-bit mode (no 64-bit
;                                             mode); {sys} then resolves to SysWOW64
;                                             on 64-bit Windows, where a 32-bit app
;                                             looks for its DLLs

#ifndef Publisher
  #define Publisher "Dynamotivation"
#endif
#define PublisherUrl "https://dynamotivation.com/"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
AppPublisherURL={#PublisherUrl}
AppSupportURL={#PublisherUrl}
AppUpdatesURL={#PublisherUrl}
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#Publisher}
VersionInfoDescription={#AppName} Setup
DefaultDirName={localappdata}\Programs\{#Publisher}\{#AppName}
DefaultGroupName={#Publisher}
DisableProgramGroupPage=yes
DisableDirPage=yes
ShowLanguageDialog=no
#ifndef AssetDir
  #define AssetDir "assets"
#endif
SetupIconFile={#AssetDir}\setup.ico
; Per-DPI wizard artwork, rendered from setup-icon.svg and full-logo.svg by installer\tools\render_assets.py.
; Inno picks the best match at runtime; sizes follow Inno's own recommended table.
WizardImageFile={#AssetDir}\wizard-large-100.bmp,{#AssetDir}\wizard-large-125.bmp,{#AssetDir}\wizard-large-150.bmp,{#AssetDir}\wizard-large-175.bmp,{#AssetDir}\wizard-large-200.bmp,{#AssetDir}\wizard-large-225.bmp,{#AssetDir}\wizard-large-250.bmp
WizardSmallImageFile={#AssetDir}\wizard-small-100.bmp,{#AssetDir}\wizard-small-125.bmp,{#AssetDir}\wizard-small-150.bmp,{#AssetDir}\wizard-small-175.bmp,{#AssetDir}\wizard-small-200.bmp,{#AssetDir}\wizard-small-225.bmp,{#AssetDir}\wizard-small-250.bmp
PrivilegesRequired=lowest
MinVersion={#MinVersion}
#ifndef AllowedArchitectures
  #define AllowedArchitectures "x64"
#endif
ArchitecturesAllowed={#AllowedArchitectures}
#ifndef Is32BitApp
ArchitecturesInstallIn64BitMode=x64
#endif
Compression=lzma2
SolidCompression=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#AppExeName}
UninstallDisplayName={#AppName}
#ifndef OutputDir
  #define OutputDir "..\dist"
#endif
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBase}-setup-{#AppVersion}
#ifdef SignEnabled
SignTool=dyn
SignedUninstaller=yes
#endif

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "de"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{userdesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; --- Settings and saved files ---------------------------------------------------
; An app whose settings or saves live outside its install folder lists those folders
; before including this file, separated by "|" (Inno constants allowed):
;   #define UserDataDirs "{localappdata}\my_app|{userappdata}\My App"
; After the uninstall confirmation, the uninstaller then offers "Keep my settings and
; saved files" (checked). Clearing it deletes the listed folders after the program is
; removed. A silent uninstall keeps them unless it is started with /REMOVEUSERDATA=1.
; Caches and other files the app can recreate do not belong in this list; remove those
; unconditionally with [UninstallDelete]. This block uses the event functions
; InitializeUninstallProgressForm and CurUninstallStepChanged, so an app script that
; includes it must not define them itself.
#ifdef UserDataDirs
[CustomMessages]
en.UserDataCaption=Uninstall {#AppName}
de.UserDataCaption={#AppName} deinstallieren
en.UserDataKeep=&Keep my settings and saved files
de.UserDataKeep=&Meine Einstellungen und gespeicherten Dateien behalten
en.UserDataHint=Keep them to pick up where you left off after reinstalling. Clear the box to remove {#AppName} completely.
de.UserDataHint=Behalte sie, um nach einer Neuinstallation dort weiterzumachen, wo du aufgehört hast. Entferne den Haken, um {#AppName} vollständig zu entfernen.

[Code]
var
  RemoveUserData: Boolean;

{ Shown once the user has confirmed the uninstall, before anything is removed. }
function AskKeepUserData: Boolean;
var
  Form: TSetupForm;
  Hint: TNewStaticText;
  Keep: TNewCheckBox;
  Ok: TNewButton;
begin
  Form := CreateCustomForm();
  try
    Form.ClientWidth := ScaleX(400);
    Form.Caption := CustomMessage('UserDataCaption');
    Keep := TNewCheckBox.Create(Form);
    Keep.Parent := Form;
    Keep.Left := ScaleX(16);
    Keep.Top := ScaleY(16);
    Keep.Width := Form.ClientWidth - ScaleX(32);
    Keep.Height := ScaleY(17);
    Keep.Caption := CustomMessage('UserDataKeep');
    Keep.Checked := True;
    Hint := TNewStaticText.Create(Form);
    Hint.Parent := Form;
    Hint.Left := ScaleX(34);
    Hint.Top := Keep.Top + Keep.Height + ScaleY(6);
    Hint.Width := Form.ClientWidth - ScaleX(50);
    Hint.AutoSize := False;
    Hint.WordWrap := True;
    Hint.Height := ScaleY(42);
    Hint.Caption := CustomMessage('UserDataHint');
    Ok := TNewButton.Create(Form);
    Ok.Parent := Form;
    Ok.Width := ScaleX(75);
    Ok.Height := ScaleY(23);
    Ok.Left := Form.ClientWidth - Ok.Width - ScaleX(16);
    Ok.Top := Hint.Top + Hint.Height + ScaleY(12);
    Ok.Caption := SetupMessage(msgButtonOK);
    Ok.ModalResult := mrOk;
    Ok.Default := True;
    Form.ClientHeight := Ok.Top + Ok.Height + ScaleY(16);
    Form.ActiveControl := Keep;
    Form.Center;
    Form.ShowModal();
    Result := Keep.Checked;
  finally
    Form.Free();
  end;
end;

procedure InitializeUninstallProgressForm;
begin
  if UninstallSilent then
    RemoveUserData := ExpandConstant('{param:REMOVEUSERDATA|0}') = '1'
  else
    RemoveUserData := not AskKeepUserData;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Dirs, Dir: String;
  Split: Integer;
begin
  if (CurUninstallStep <> usPostUninstall) or not RemoveUserData then
    Exit;
  Dirs := '{#UserDataDirs}|';
  while Dirs <> '' do
  begin
    Split := Pos('|', Dirs);
    Dir := Trim(Copy(Dirs, 1, Split - 1));
    Delete(Dirs, 1, Split);
    if Dir <> '' then
      DelTree(ExpandConstant(Dir), True, True, True);
  end;
end;
#endif
