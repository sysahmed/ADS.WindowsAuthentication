; ADS Windows Authentication Client - Inno Setup Script
; Senior .NET approach: clean installer with shortcuts, uninstall

#define MyAppName "ADS Windows Auth Client"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Nursan Bulgaria"
#define MyAppExeName "ADS.WindowsAuth.Client.exe"
#define MyAppAssocName "ADS Windows Authentication"

[Setup]
AppId={{A7E8F2C1-4B9D-4E3A-8F1C-2D5E6A9B0C3D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\ADS.WindowsAuth.Client
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\InstallerOutput
OutputBaseFilename=ADS-Windows-Auth-Client-Setup
; ; SetupIconFile=..\favicon.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
WizardImageFile=compiler:WizModernImage.bmp
WizardSmallImageFile=compiler:WizModernSmallImage.bmp
PrivilegesRequired=admin
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "bulgarian"; MessagesFile: "compiler:Languages\Bulgarian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Създай икона на работния плот"; GroupDescription: "Допълнителни икони:"; Flags: unchecked
Name: "quicklaunchicon"; Description: "Създай бърз старт икона"; GroupDescription: "Допълнителни икони:"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application - run "dotnet publish -c Release" or use build output
Source: "..\bin\Release\net8.0-windows8.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Премахни {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Стартирай {#MyAppName}"; Flags: nowait postinstall skipifsilent runascurrentuser

[UninstallDelete]
Type: dirifempty; Name: "{app}"
Type: filesandordirs; Name: "{app}\LOGS"

[Code]
function InitializeSetup(): Boolean;
begin
  Result := True;
end;
