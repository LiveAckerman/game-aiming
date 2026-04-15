; CrosshairTool Inno Setup 安装脚本
; 使用前提：
;   1. 已安装 Inno Setup 6：https://jrsoftware.org/isdl.php
;   2. 已运行 scripts\publish.ps1 生成 dist\CrosshairTool.exe
;
; 编译方式：
;   - 用 Inno Setup Compiler 打开此文件，点击 Build → Compile
;   - 或命令行：iscc.exe installer\installer.iss
; 输出：installer\output\CrosshairTool_Setup_v1.0.0.exe

#define AppName      "CrosshairTool"
#define AppVersion   "1.0.0"
#define AppPublisher "CrosshairTool"
#define AppURL       "https://github.com/your-repo/CrosshairTool"
#define AppExeName   "CrosshairTool.exe"
#define SourceDir    "..\dist\lite"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
CreateUninstallRegKey=yes
UsePreviousGroup=yes
AllowNoIcons=yes
; 安装包输出目录
OutputDir=output
OutputBaseFilename=CrosshairTool_Setup_Lite_v{#AppVersion}
; 使用现代风格向导
WizardStyle=modern
; 压缩
Compression=lzma2/ultra64
SolidCompression=yes
; 需要 64 位系统
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
; 图标
SetupIconFile=..\src\CrosshairTool\Resources\icon.ico
; 不需要管理员权限（安装到用户目录）
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
; 版本信息
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} - FPS Crosshair Overlay Tool
VersionInfoCopyright=Copyright (C) 2026 {#AppPublisher}
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english";           MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon";   Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked
Name: "startupentry";  Description: "开机自动启动";     GroupDescription: "启动选项:"; Flags: unchecked

[Files]
; 框架依赖版，需目标机已安装 .NET 8 WindowsDesktop Runtime
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\{#AppName}";           Filename: "{app}\{#AppExeName}"
Name: "{userprograms}\卸载 {#AppName}";      Filename: "{uninstallexe}"
Name: "{userdesktop}\{#AppName}";            Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
; 开机自启动（仅当用户勾选时）
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "{#AppName}"; \
  ValueData: """{app}\{#AppExeName}"""; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
; 安装完成后立即运行
Filename: "{app}\{#AppExeName}"; \
  Description: "立即运行 {#AppName}"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; 卸载前先关闭正在运行的程序
Filename: "taskkill.exe"; Parameters: "/f /im {#AppExeName}"; Flags: runhidden; RunOnceId: "KillApp"

[Code]
// 检查是否已安装 .NET 8 WindowsDesktop Runtime
function IsDotNet8Installed(): Boolean;
var
  Key: String;
begin
  Key := 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App';
  Result := RegKeyExists(HKLM, Key) or RegKeyExists(HKCU, Key);
  // 更保守的检测：检查已知路径
  if not Result then
    Result := FileExists(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.0\wpfgfx_x64.dll'))
              or DirExists(ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.0.26'));
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;
  if not IsDotNet8Installed() then
  begin
    if MsgBox('CrosshairTool 需要 .NET 8 WindowsDesktop 运行时。' + #13#10 +
              '是否打开下载页面？（下载后请重新运行安装程序）',
              mbConfirmation, MB_YESNO) = IDYES then
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOW, ewNoWait, ErrorCode);
    Result := False;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
    Exec('taskkill.exe', '/f /im {#AppExeName}', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
end;
