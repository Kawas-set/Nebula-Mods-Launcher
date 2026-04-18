#define MyAppName "Nebula Mods Launcher"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "by Kawasaki"
#define MyAppExeName "NebulaModsLauncher.exe"
#define MyAppId "{{1D53C0CC-4D56-4B74-BE9B-D0E84AB34B64}"
#ifndef MyPublishDir
#define MyPublishDir "..\artifacts\publish-release-current"
#endif
#ifndef MyOutputDir
#define MyOutputDir "..\artifacts\installer-release"
#endif
#define MyIconFile "..\Assets\nebula-mods-launcher.ico"
#define MyLicenseFile "LICENSE-SETUP.txt"
#define MyWizardImageFile "branding\wizard-image.bmp"
#define MyWizardSmallImageFile "branding\wizard-small.bmp"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=no
OutputDir={#MyOutputDir}
OutputBaseFilename=NebulaModsLauncher-Setup-v{#MyAppVersion}-win64
SetupIconFile={#MyIconFile}
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile={#MyLicenseFile}
WizardImageFile={#MyWizardImageFile}
WizardSmallImageFile={#MyWizardSmallImageFile}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=commandline
CloseApplications=yes
RestartApplications=no
UsePreviousAppDir=yes
DisableReadyPage=no
DisableWelcomePage=no
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
AppCopyright=Copyright (C) 2026 {#MyAppPublisher}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
