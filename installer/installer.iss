; Hear It Loud — one-click installer
; Bundles HearItLoud.exe + bootstraps Equalizer APO + runs --auto.

#define MyAppName        "Hear It Loud"
#define MyAppVersion     "1.10.1"
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
SetupIconFile=hearitloud.ico
PrivilegesRequired=admin
AlwaysRestart=yes
; In-place upgrades: detect a running HearItLoud.exe and close it cleanly,
; then re-launch the GUI after files are replaced. Needed for the in-app
; "Check for Updates → Update Now" flow (Windows locks running .exes).
CloseApplications=yes
RestartApplications=yes
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
; Shortcuts launch the GUI (no args = GUI mode). Power users can still run
; HearItLoud.exe --auto / --diagnose / etc. from a terminal.
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "Launch Hear It Loud"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Step 1: install / verify Equalizer APO (downloads if missing).
; CheckPostInstall halts setup if PS exits non-zero so we don't silently
; proceed to the success popup when EQ APO actually failed to install.
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\install-eqapo.ps1"""; \
  StatusMsg: "Installing Equalizer APO (downloading if needed)..."; \
  Flags: runhidden waituntilterminated; \
  AfterInstall: CheckEqApoInstalled

; Step 2: auto-install the optional VST/HRIR plugins (TDR Nova, LoudMax,
; HeSuVi). Downloaded from publisher URLs at install time, dropped straight
; into EQ APO's VSTPlugins / HeSuVi config dirs. Best-effort: any download
; failure is logged but does not abort install (the app's basic chain works
; without them, and Diagnose & Auto-Fix can retry later).
Filename: "{app}\{#MyAppExeName}"; \
  Parameters: "--install-plugins"; \
  StatusMsg: "Installing optional VST plugins (TDR Nova, LoudMax, HeSuVi)..."; \
  Flags: runhidden waituntilterminated

; Step 3: run --auto so the user has a working config the moment they reboot
Filename: "powershell.exe"; \
  Parameters: "-NoProfile -ExecutionPolicy Bypass -File ""{app}\scripts\run-autotune.ps1"" -ExePath ""{app}\{#MyAppExeName}"""; \
  StatusMsg: "Detecting hardware and installing Hear It Loud config..."; \
  Flags: runhidden waituntilterminated; \
  Tasks: runautotune

[Registry]
; Register the .warzeq preset file extension so double-clicking a preset
; opens Hear It Loud with that preset loaded. Cleaned up on uninstall.
Root: HKCR; Subkey: ".warzeq"; ValueType: string; ValueName: ""; ValueData: "HearItLoud.Preset"; Flags: uninsdeletevalue
Root: HKCR; Subkey: "HearItLoud.Preset"; ValueType: string; ValueName: ""; ValueData: "Hear It Loud preset"; Flags: uninsdeletekey
Root: HKCR; Subkey: "HearItLoud.Preset\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"
Root: HKCR; Subkey: "HearItLoud.Preset\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""

[UninstallRun]
; Remove the Hear It Loud block from EQ APO's master config so we don't leave
; a dangling Include line pointing at a deleted file. EQ APO itself stays —
; it's a system component shared with other apps.
Filename: "{app}\{#MyAppExeName}"; Parameters: "--uninstall-cleanup"; \
  Flags: runhidden waituntilterminated; RunOnceId: "HearItLoudCleanup"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;

procedure CheckEqApoInstalled();
var
  InstallPath: string;
begin
  if not RegQueryStringValue(HKLM, 'SOFTWARE\EqualizerAPO', 'InstallPath', InstallPath) then
  begin
    MsgBox(
      'Equalizer APO could not be installed automatically.' + #13#10 + #13#10 +
      'This usually means your network blocked the download. Please:' + #13#10 +
      '  1. Download Equalizer APO yourself from https://equalizerapo.com' + #13#10 +
      '  2. Install it and reboot' + #13#10 +
      '  3. Run "HearItLoud.exe --auto" from C:\Program Files\Hear It Loud',
      mbCriticalError, MB_OK);
    Abort();
  end;
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
      '  1. Double-click the Hear It Loud icon on your desktop' + #13#10 +
      '       (a small app window will open).' + #13#10 +
      '  2. Click "Auto Setup" (the big green button) and wait.' + #13#10 +
      '  3. Open Warzone' + #13#10 +
      '  4. Settings -> Audio:' + #13#10 +
      '       Audio Mix = Headphones Bass Cut' + #13#10 +
      '       Surround Sound = 7.1' + #13#10 +
      '       Music Volume = 0' + #13#10 +
      '       Enhanced Headphone Mode = OFF' + #13#10 + #13#10 +
      'If anything sounds wrong later, click "Diagnose & Auto-Fix" in the app.' + #13#10 +
      'Footsteps will be louder and more directional. Good luck.',
      mbInformation, MB_OK);
  end;
end;
