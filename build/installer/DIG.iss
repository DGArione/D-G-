; Inno Setup script for D!G! Infrastructure Browser.
; Build with: ISCC.exe /DMyAppVersion=x.y.z /DSourceDir=<published app folder> DIG.iss
; Produces a per-user EXE installer (no administrator rights required).

#define MyAppName "D!G!"
#define MyAppPublisher "D!G!"
#define MyAppExeName "D!G!.exe"
#define MyAppUrl "https://example.invalid/dig"

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\..\artifacts\app"
#endif

[Setup]
AppId={{7C1D2F9E-4B6A-4C33-9D21-0F5A2B7E8C10}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
; Per-user install so no admin prompt is required. User data in %LOCALAPPDATA%\DIG is untouched by uninstall.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={localappdata}\Programs\DIG
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
OutputDir=Output
OutputBaseFilename=DIG-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\..\assets\branding\dig.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Warn (do not block) if the Edge WebView2 Runtime is not detected.
function WebView2Installed(): Boolean;
var
  v: string;
begin
  Result :=
    RegQueryStringValue(HKLM, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) or
    RegQueryStringValue(HKLM, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v) or
    RegQueryStringValue(HKCU, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', v);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep = ssPostInstall) and (not WebView2Installed()) then
  begin
    MsgBox('D!G! needs the Microsoft Edge WebView2 Runtime, which was not detected on this PC.' + #13#10 +
           'Please install it from https://developer.microsoft.com/microsoft-edge/webview2/ and then start D!G!.',
           mbInformation, MB_OK);
  end;
end;
