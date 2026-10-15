; Inno Setup script for Calypsov's Windows installer.
; Built by scripts/package-windows.ps1, which passes the real app version via
; /DAppVersion=X.Y.Z (read live from Calypsov.csproj, same source of truth as
; the macOS packaging script) — never hardcode a version here.
;
; Unsigned: no code-signing certificate is configured for this project (same
; already-accepted tradeoff as the macOS build's ad-hoc signing). Windows
; SmartScreen will warn on first run until/unless that changes.

#define AppName "Calypsov"
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; Fixed once, kept stable across versions so upgrades/uninstalls work correctly.
#define AppId "{A6F2E4C1-2E6B-4C9B-9B7E-7C9E7D2F5A31}"
#define PublishDir "..\Calypsov\bin\Release\net8.0\win-x64\publish"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\Deployed\Windows
OutputBaseFilename=Calypsov-{#AppVersion}-win-x64-setup
SetupIconFile=..\Calypsov\AppIcon\app.ico
UninstallDisplayIcon={app}\Calypsov.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
; The whole publish output, recursively — Program.cs resolves both AppIcon/
; and wwwroot/ relative to the executable's own directory, so they need to
; sit next to it, same approach as the macOS .app bundle's loose copies.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\Calypsov.exe"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Calypsov.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Calypsov.exe"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
