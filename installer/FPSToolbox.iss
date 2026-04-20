; FPSToolbox Inno Setup 安装脚本
;
; 编译命令(在线版,小):
;   iscc.exe installer\FPSToolbox.iss
;
; 编译命令(离线版,内嵌 .NET 8 运行时,~60 MB):
;   iscc.exe /DOFFLINE /DDOTNET_RUNTIME_FILE=".\runtimes\windowsdesktop-runtime-8.0.11-win-x64.exe" installer\FPSToolbox.iss
;
; 指定版本号:
;   iscc.exe /DAppVersion=1.2.3 installer\FPSToolbox.iss
;
; 在线版会在安装时从 Microsoft 官方 CDN 自动下载 .NET 8 运行时。
; 离线版会直接静默安装随包的 runtime 安装器。

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#define AppName      "FPS 工具箱"
#define AppId        "FPSToolbox"
#define AppPublisher "FPSToolbox"
#define AppURL       "https://github.com/LiveAckerman/game-aiming"
#define AppExeName   "FPSToolbox.exe"
#define SourceDir    "..\dist\payload"

; .NET 8 Desktop Runtime 版本(离线版需要对应下载)
#define DotNetVersion "8.0.11"
#define DotNetUrl     "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/8.0.11/windowsdesktop-runtime-8.0.11-win-x64.exe"

#ifdef OFFLINE
  #define OutputSuffix "_offline"
#else
  #define OutputSuffix ""
#endif

[Setup]
AppId={{C3D4E5F6-A7B8-9012-CDEF-234567890123}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
DefaultDirName={autopf}\FPSToolbox
DefaultGroupName=FPS 工具箱
CreateUninstallRegKey=yes
UsePreviousGroup=yes
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=FPSToolbox_Setup_v{#AppVersion}{#OutputSuffix}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
SetupIconFile=..\src\FPSToolbox\Resources\icon.ico
UninstallDisplayIcon={app}\{#AppExeName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline dialog
DisableDirPage=no
DisableProgramGroupPage=no
VersionInfoVersion={#AppVersion}
VersionInfoDescription={#AppName} Installer
VersionInfoCopyright=Copyright (C) 2026 {#AppPublisher}

[Languages]
Name: "chinesesimplified"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english";           MessagesFile: "compiler:Default.isl"

[Types]
Name: "full";    Description: "完整安装（主程序 + 两个工具）"
Name: "compact"; Description: "最小安装（仅主程序）"
Name: "custom";  Description: "自定义安装";                       Flags: iscustom

[Components]
Name: "main";      Description: "FPS 工具箱主程序（必需）"; Types: full compact custom; Flags: fixed
Name: "crosshair"; Description: "屏幕准心工具";            Types: full
Name: "gamma";     Description: "屏幕调节工具";            Types: full

[Tasks]
Name: "desktopicon";  Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"; Flags: unchecked
Name: "startupentry"; Description: "开机自动启动 FPS 工具箱（最小化到托盘）"; GroupDescription: "启动选项:"; Flags: unchecked

[Files]
; 主程序
Source: "{#SourceDir}\FPSToolbox.exe";               DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.dll";               DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.deps.json";         DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\FPSToolbox.Shared.dll";        DestDir: "{app}"; Flags: ignoreversion; Components: main
Source: "{#SourceDir}\*.dll";                        DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist; Excludes: "FPSToolbox.Shared.dll,FPSToolbox.dll"; Components: main
Source: "{#SourceDir}\Resources\*";                  DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs skipifsourcedoesntexist; Components: main

; 屏幕准心工具(uninsneveruninstall:Inno 默认不删;卸载时由 [Code] 段根据用户勾选决定)
Source: "{#SourceDir}\tools\CrosshairTool\*"; DestDir: "{app}\tools\CrosshairTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: crosshair

; 屏幕调节工具(同上)
Source: "{#SourceDir}\tools\GammaTool\*"; DestDir: "{app}\tools\GammaTool"; \
    Flags: ignoreversion recursesubdirs createallsubdirs uninsneveruninstall; Components: gamma

#ifdef OFFLINE
; 离线版:内嵌 .NET Desktop Runtime 安装器
Source: "{#DOTNET_RUNTIME_FILE}"; DestDir: "{tmp}"; Flags: deleteafterinstall; DestName: "dotnet-runtime.exe"; Check: NeedsDotNet
#endif

[Icons]
Name: "{group}\FPS 工具箱";      Filename: "{app}\{#AppExeName}"
Name: "{group}\卸载 FPS 工具箱"; Filename: "{uninstallexe}"
Name: "{userdesktop}\FPS 工具箱"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; \
  ValueType: string; ValueName: "FPSToolbox"; \
  ValueData: """{app}\{#AppExeName}"" --minimized"; \
  Flags: uninsdeletevalue; Tasks: startupentry

[Run]
#ifdef OFFLINE
; 离线版:安装前先装 .NET(如果缺)
Filename: "{tmp}\dotnet-runtime.exe"; Parameters: "/install /quiet /norestart"; \
    StatusMsg: "正在安装 .NET 8 运行时..."; Check: NeedsDotNet
#endif
; 交互安装:在向导末页显示"立即运行"勾选框(用户可勾可不勾)
Filename: "{app}\{#AppExeName}"; Description: "立即运行 FPS 工具箱"; \
    Flags: nowait postinstall skipifsilent
; 静默安装(主框架"立即更新"走 /SILENT):装完自动启动新版,不显示任何提示
Filename: "{app}\{#AppExeName}"; Flags: nowait runasoriginaluser; Check: WizardSilent

[UninstallRun]
; 卸载前先关掉所有相关进程
Filename: "taskkill.exe"; Parameters: "/f /im FPSToolbox.exe";   Flags: runhidden; RunOnceId: "KillMain"
Filename: "taskkill.exe"; Parameters: "/f /im CrosshairTool.exe"; Flags: runhidden; RunOnceId: "KillCross"
Filename: "taskkill.exe"; Parameters: "/f /im GammaTool.exe";     Flags: runhidden; RunOnceId: "KillGamma"

[Code]
#ifndef OFFLINE
var
  DownloadPage: TDownloadWizardPage;
#endif

// ──────────────────────────────────────────────────────────────
// .NET 8 检测(只要装了 8.x 的任一小版本就满足)
// ──────────────────────────────────────────────────────────────
function ExistsDotNet8Dir(): Boolean;
var
  FindRec: TFindRec;
  Pattern: String;
begin
  Result := False;
  Pattern := ExpandConstant('{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\8.*');
  if FindFirst(Pattern, FindRec) then
  try
    repeat
      if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0)
         and (FindRec.Name <> '.') and (FindRec.Name <> '..') then
      begin
        Result := True;
        Exit;
      end;
    until not FindNext(FindRec);
  finally
    FindClose(FindRec);
  end;
end;

function IsDotNet8Installed(): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  // 最准:注册表里列出的 WindowsDesktop 子版本
  if RegGetSubkeyNames(HKLM64, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App', Names) then
  begin
    for I := 0 to GetArrayLength(Names) - 1 do
      if (Length(Names[I]) >= 2) and (Copy(Names[I], 1, 2) = '8.') then
      begin
        Result := True;
        Exit;
      end;
  end;
  // 兜底:查 Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\8.*
  if not Result then
    Result := ExistsDotNet8Dir();
end;

// 是否需要安装 .NET(供 [Files]/[Run] 的 Check 使用)
function NeedsDotNet(): Boolean;
begin
  Result := not IsDotNet8Installed();
end;

// ──────────────────────────────────────────────────────────────
// 在线版:创建下载页,在进入 ssInstall 之前拉 .NET runtime
// ──────────────────────────────────────────────────────────────
#ifndef OFFLINE
procedure InitializeWizard;
begin
  DownloadPage := CreateDownloadPage(
    SetupMessage(msgWizardPreparing),
    '正在下载 .NET 8 运行时(仅首次需要)...',
    nil);
end;

// PrepareToInstall 在所有页过完、文件落盘之前执行。对 /SILENT 和交互两种模式都通用。
// 返回非空字符串会中止安装并把字符串当作错误显示。
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not NeedsDotNet() then Exit;

  DownloadPage.Clear;
  DownloadPage.Add('{#DotNetUrl}', 'dotnet-runtime.exe', '');
  DownloadPage.Show;
  try
    try
      DownloadPage.Download;
    except
      Result := '下载 .NET 8 运行时失败:' + #13#10 + GetExceptionMessage() + #13#10 + #13#10 +
                '请检查网络后重试,或改用离线版安装包(含 runtime)。' + #13#10 +
                '官方下载: https://dotnet.microsoft.com/download/dotnet/8.0';
    end;
  finally
    DownloadPage.Hide;
  end;
end;

// 安装阶段执行下载好的 runtime
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/f /im FPSToolbox.exe',   '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im CrosshairTool.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im GammaTool.exe',     '', SW_HIDE, ewWaitUntilTerminated, ResultCode);

    if NeedsDotNet() then
    begin
      WizardForm.StatusLabel.Caption := '正在安装 .NET 8 运行时...';
      if not Exec(ExpandConstant('{tmp}\dotnet-runtime.exe'),
                  '/install /quiet /norestart',
                  '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
        MsgBox('.NET 8 安装失败(错误码 ' + IntToStr(ResultCode) + ')。' + #13#10 +
               '主程序可能无法启动,请手动安装 .NET 8 Desktop Runtime。',
               mbError, MB_OK);
    end;
  end;
end;
#else
// 离线版:只在 ssInstall 时 kill 进程, runtime 由 [Run] 段用 Check=NeedsDotNet 安装
procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
begin
  if CurStep = ssInstall then
  begin
    Exec('taskkill.exe', '/f /im FPSToolbox.exe',   '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im CrosshairTool.exe', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
    Exec('taskkill.exe', '/f /im GammaTool.exe',     '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  end;
end;
#endif

// ──────────────────────────────────────────────────────────────
// 进入组件选择页时,扫 {app}\tools\ 把已存在的子工具自动勾选
// (升级 / 重装 / 之前卸载时保留了子工具 三种场景都能识别)
// ──────────────────────────────────────────────────────────────
procedure AutoCheckExistingTools;
var
  I: Integer;
  AppDir, Desc: String;
  HasCrosshair, HasGamma: Boolean;
begin
  AppDir := WizardForm.DirEdit.Text;
  HasCrosshair := FileExists(AppDir + '\tools\CrosshairTool\CrosshairTool.exe');
  HasGamma     := FileExists(AppDir + '\tools\GammaTool\GammaTool.exe');
  if (not HasCrosshair) and (not HasGamma) then Exit;

  for I := 0 to WizardForm.ComponentsList.Items.Count - 1 do
  begin
    Desc := WizardForm.ComponentsList.ItemCaption[I];
    if HasCrosshair and (Pos('准心', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
    if HasGamma and (Pos('调节', Desc) > 0) then
      WizardForm.ComponentsList.Checked[I] := True;
  end;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpSelectComponents then
    AutoCheckExistingTools();
end;

// ──────────────────────────────────────────────────────────────
// 卸载前:询问用户勾选哪些子工具一起卸载 + 是否清用户数据
// ──────────────────────────────────────────────────────────────
var
  UninstCrosshair: Boolean;
  UninstGamma: Boolean;

function InitializeUninstall(): Boolean;
var
  ToolsRoot: String;
  HasCrosshair, HasGamma: Boolean;
begin
  Result := True;
  UninstCrosshair := False;
  UninstGamma := False;

  ToolsRoot := ExpandConstant('{app}\tools');
  HasCrosshair := DirExists(ToolsRoot + '\CrosshairTool');
  HasGamma     := DirExists(ToolsRoot + '\GammaTool');

  if HasCrosshair then
    UninstCrosshair := (MsgBox(
      '是否同时卸载「屏幕准心工具」？' + #13#10 + #13#10 +
      '安装位置：' + ToolsRoot + '\CrosshairTool' + #13#10 + #13#10 +
      '选择"是"：删除该工具的所有文件。' + #13#10 +
      '选择"否"：保留该工具的文件（下次安装 FPS 工具箱后可直接使用）。',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);

  if HasGamma then
    UninstGamma := (MsgBox(
      '是否同时卸载「屏幕调节工具」？' + #13#10 + #13#10 +
      '安装位置：' + ToolsRoot + '\GammaTool' + #13#10 + #13#10 +
      '选择"是"：删除该工具的所有文件。' + #13#10 +
      '选择"否"：保留该工具的文件（下次安装 FPS 工具箱后可直接使用）。',
      mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppPath, DataPath: String;
  ClearData: Boolean;
begin
  if CurUninstallStep = usUninstall then
  begin
    AppPath := ExpandConstant('{app}');
    if UninstCrosshair then DelTree(AppPath + '\tools\CrosshairTool', True, True, True);
    if UninstGamma     then DelTree(AppPath + '\tools\GammaTool',     True, True, True);
    // 若两个子工具都删了,顺手把空的 tools\ 目录也清掉
    if UninstCrosshair and UninstGamma then
      RemoveDir(AppPath + '\tools');
  end;
  if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{userappdata}\FPSToolbox');
    if DirExists(DataPath) then
    begin
      ClearData := (MsgBox('是否同时清除 FPS 工具箱的用户数据？' + #13#10 + #13#10 +
                           '用户数据包括：所有工具的配置、保存的方案 / 预设。' + #13#10 +
                           '路径：' + DataPath + #13#10 + #13#10 +
                           '选择"是"：彻底清除，再次安装时需要重新配置。' + #13#10 +
                           '选择"否"：保留数据，再次安装后可继续使用原来的方案。',
                           mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES);
      if ClearData then
        DelTree(DataPath, True, True, True);
    end;
  end;
end;
