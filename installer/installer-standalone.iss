; CrosshairTool 独立版安装脚本（内置 .NET 8，无需用户额外安装）
; 使用前提：
;   1. 已安装 Inno Setup 6：https://jrsoftware.org/isdl.php
;   2. 已运行 scripts\publish.ps1 生成 dist\standalone\
;
; 编译方式：
;   - 用 Inno Setup Compiler 打开此文件，点击 Build → Compile
;   - 或命令行：iscc.exe installer\installer-standalone.iss
; 输出：installer\output\CrosshairTool_Setup_Standalone_v1.0.0.exe

#define AppName      "CrosshairTool"
#define AppVersion   "1.0.0"
#define AppPublisher "CrosshairTool"
#define AppURL       "https://github.com/your-repo/CrosshairTool"
#define AppExeName   "CrosshairTool.exe"
#define SourceDir    "..\dist\standalone"

[Setup]
; 独立版使用不同的 AppId，可与轻量版并存
AppId={{B2C3D4E5-F6A7-8901-BCDE-F12345678901}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion} (独立版)
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
CreateUninstallRegKey=yes
UsePreviousGroup=yes
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=CrosshairTool_Setup_Standalone_v{#AppVersion}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupIconFile=..\src\CrosshairTool\Resources\icon.ico
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
DisableDirPage=no
DisableProgramGroupPage=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} - FPS Crosshair Overlay Tool (Standalone)
VersionInfoCopyright=Copyright (C) 2026 {#AppPublisher}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english";           MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked
Name: "startupentry";  Description: "开机自动启动";     GroupDescription: "启动选项:"; Flags: unchecked

[Files]
; 独立版：内置 .NET 8 运行时，无需用户单独安装
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{userprograms}\卸载 {#AppName}";      Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}";            Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
Filename: "{app}\{#AppExeName}"; \
  Description: "立即运行 {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/f /im {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
