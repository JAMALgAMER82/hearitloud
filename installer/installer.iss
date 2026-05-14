; Hear It Loud — one-click installer
; Bundles HearItLoud.exe + bootstraps Equalizer APO + runs --auto.

#define MyAppName        "Hear It Loud"
#define MyAppVersion     "1.0.0"
#define MyAppPublisher   "MasterMind George"
#define MyAppURL         "https://github.com/yourname/hearitloud"
#define MyAppExeName     "HearItLoud.exe"

[Setup]
AppId={{A82D7C18-7D40-4A6D-A2D8-9F1B2D8E4F3C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\publish\installer
OutputBaseFilename=HearItLoud-Setup
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=
PrivilegesRequired=admin
AlwaysRestart=yes
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"
Name: "runautotune"; Description: "Run auto-detect + install after Equalizer APO is set up"; GroupDescription: "Post-install:"; Flags: checkedonce

[Files]
; Our self-contained HearItLoud.exe
Source: "..\publish\win-x64\HearItLoud.exe"; DestDir: "{app}"; Flags: ignoreversion

; Helper scripts
Source: "scripts\install-eqapo.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion
Source: "scripts\run-autotune.ps1"; DestDir: "{app}\scripts"; Flags: ignoreversion

; Documentation
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\superpowers\specs\2026-05-14-warzone-eq-design.md"; DestDir: "{app}\docs"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--auto"; Comment: "Run auto-detect + install"
Name: "{group}\{#MyAppName} (detect only)"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--detect"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Parameters: "--auto"; Tasks: desktopicon

[Run]
; Step 1: install / verify Equalizer APO (downloads if missing)
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-eqapo.ps1"""; \
  StatusMsg: "Installing Equalizer APO (downloading if needed)..."; \
  Flags: runhidden waituntilterminated

; Step 2: run --auto so the user has a working config the moment they reboot
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\run-autotune.ps1"" -ExePath ""{app}\{#MyAppExeName}"""; \
  StatusMsg: "Detecting hardware and installing Hear It Loud config..."; \
  Flags: runhidden waituntilterminated; \
  Tasks: runautotune

[UninstallRun]
; No special uninstall steps for v1 — EQ APO stays installed (it's a system component).

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox(
      'Hear It Loud is installed.' + #13#10 +
      'by MasterMind George' + #13#10 + #13#10 +
      'Windows will now reboot to activate Equalizer APO.' + #13#10 + #13#10 +
      'After reboot:' + #13#10 +
      '  1. Open Warzone' + #13#10 +
      '  2. Settings -> Audio:' + #13#10 +
      '       Audio Mix = Headphones Bass Cut' + #13#10 +
      '       Surround Sound = 7.1' + #13#10 +
      '       Music Volume = 0' + #13#10 +
      '       Enhanced Headphone Mode = OFF' + #13#10 + #13#10 +
      'Footsteps will be louder and more directional. Good luck.',
      mbInformation, MB_OK);
  end;
end;
