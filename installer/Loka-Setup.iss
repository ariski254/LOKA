; Inno Setup Script for LOKA - Modern Windows Local Development Environment
; Fulfills Sections 5, 6, 14, and 15 of Fitur.md

#define MyAppName "Loka"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Loka Dev Team"
#define MyAppURL "https://github.com/loka/loka"
#define MyAppExeName "Loka.App.exe"

[Setup]
AppId={{5D460DFD-4B0A-4759-A57A-7DF44878D784}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DefaultGroupName={#MyAppName}
OutputDir=..\bin\Installer
OutputBaseFilename=Loka-Setup
SetupIconFile=..\logo.png
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} Local Development Environment

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\bin\Release\Loka.App\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\logo.png"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\logo.png"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Resources\logo.png"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Section 15: Uninstaller with Selective Data Removal
var
  UninstallTypePage: TInputOptionWizardPage;

procedure InitializeUninstallProgressForm();
begin
  // Create Custom Option page for selective uninstall (Section 15)
  UninstallTypePage := CreateInputOptionPage(wpInstalling,
    'Uninstall Options', 'What would you like to remove?',
    'Loka environment data is stored separately from the application. Please select your preferred removal option:',
    True, False);

  UninstallTypePage.Add('Application only (Recommended - keep projects, databases, and runtimes)');
  UninstallTypePage.Add('Application + configuration (%AppData%\Loka)');
  UninstallTypePage.Add('Application + entire environment (Warning: Deletes C:\Loka, projects, and databases!)');
  
  UninstallTypePage.SelectedValueIndex := 0; // Default: Application only
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  WarningResult: Integer;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if UninstallTypePage.SelectedValueIndex = 1 then
    begin
      // Remove configuration
      DelTree(ExpandConstant('{userappdata}\Loka'), True, True, True);
    end
    else if UninstallTypePage.SelectedValueIndex = 2 then
    begin
      // Require explicit confirmation before deleting environment
      WarningResult := MsgBox('WARNING: This will permanently delete your local projects, MySQL databases, runtimes, and SSL certificates located in C:\Loka!' + #13#10 + #13#10 + 'Are you absolutely sure you want to proceed?', mbConfirmation, MB_YESNO);
      if WarningResult = IDYES then
      begin
        DelTree(ExpandConstant('C:\Loka'), True, True, True);
        DelTree(ExpandConstant('{userappdata}\Loka'), True, True, True);
      end;
    end;
  end;
end;
