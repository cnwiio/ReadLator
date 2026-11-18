; สคริปต์ทางเลือก Inno Setup สำหรับสร้างตัวติดตั้ง Windows (.exe)
; 1. สั่ง publish ก่อน: dotnet publish src\NovelTranslator.App\NovelTranslator.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
; 2. เปิดไฟล์นี้ด้วยโปรแกรม Inno Setup Compiler แล้วกด Compile (หรือรัน iscc installer\setup-inno.iss)

#define MyAppName "NovelTranslator"
#ifndef MyAppVersion
  #define MyAppVersion "7.0.0"
#endif
#define MyAppExeName "NovelTranslator.App.exe"

[Setup]
AppId={{8E9E4A21-3C7D-4B18-A2D4-7B9F0C1E2345}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DisableDirPage=no
DefaultGroupName={#MyAppName}
OutputDir=Releases\inno
OutputBaseFilename=NovelTranslator-Setup-{#MyAppVersion}-win-x64
SetupIconFile=..\src\NovelTranslator.App\Assets\avalonia-logo.ico
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent