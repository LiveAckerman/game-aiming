<#
.SYNOPSIS
  FPSToolbox 发版脚本 —— 一键编译、打包、上传 GitHub Release

.DESCRIPTION
  支持独立发版三个组件:
    -Target toolbox    发布主框架(在线版 exe + 离线版 exe)
    -Target crosshair  发布屏幕准心工具(zip)
    -Target gamma      发布屏幕调节工具(zip)
    -Target all        同时发布三个

  Tag 命名约定:
    toolbox-v<Version>  /  crosshair-v<Version>  /  gamma-v<Version>

  依赖:
    - dotnet (.NET 8 SDK)
    - Inno Setup 6  (D:\Program Files\Inno Setup 6\iscc.exe)
    - gh CLI        (需要先 `gh auth login`)

.EXAMPLE
  # 发布主框架 v1.0.0 (两个 exe)
  .\scripts\release.ps1 -Target toolbox -Version 1.0.0

.EXAMPLE
  # 单独发一个子工具
  .\scripts\release.ps1 -Target gamma -Version 1.1.0 -Notes "修复预览按钮 bug"

.EXAMPLE
  # 全发
  .\scripts\release.ps1 -Target all -Version 1.0.0
#>

param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('toolbox', 'crosshair', 'gamma', 'all')]
    [string]$Target,

    [Parameter(Mandatory = $true)]
    [string]$Version,

    [string]$Notes = "",

    [string]$InnoSetup = "D:\Program Files\Inno Setup 6\iscc.exe",

    [switch]$DryRun,   # 只打包,不上传
    [switch]$Draft     # 草稿 release
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
Set-Location $root

function Info($msg)  { Write-Host "▶ $msg" -ForegroundColor Cyan }
function Done($msg)  { Write-Host "✓ $msg" -ForegroundColor Green }
function Warn($msg)  { Write-Host "! $msg" -ForegroundColor Yellow }
function Fail($msg)  { Write-Host "✗ $msg" -ForegroundColor Red; exit 1 }

# ─────────────────────────────────────────────────────────────
# 前置检查
# ─────────────────────────────────────────────────────────────
if (-not $DryRun) {
    try { gh --version | Out-Null } catch { Fail "未检测到 gh CLI,请先安装: https://cli.github.com" }
    try { gh auth status 2>&1 | Out-Null } catch { Fail "未登录 gh,请先执行: gh auth login" }
}
if (-not (Test-Path $InnoSetup)) {
    if ($Target -in @('toolbox', 'all')) {
        Fail "Inno Setup 找不到: $InnoSetup  (主框架发版必需)"
    }
}

$distDir   = Join-Path $root 'dist'
$releaseDir = Join-Path $distDir 'release'
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

$runtimesDir = Join-Path $root 'installer\runtimes'
$dotnetVersion = '8.0.11'
$dotnetInstaller = Join-Path $runtimesDir "windowsdesktop-runtime-$dotnetVersion-win-x64.exe"
$dotnetUrl = "https://builds.dotnet.microsoft.com/dotnet/WindowsDesktop/$dotnetVersion/windowsdesktop-runtime-$dotnetVersion-win-x64.exe"

# ─────────────────────────────────────────────────────────────
# 通用步骤
# ─────────────────────────────────────────────────────────────
function Publish-All {
    Info "dotnet publish 三个项目(Release)..."
    & (Join-Path $root 'scripts\publish.ps1')
    if ($LASTEXITCODE -ne 0) { Fail "publish.ps1 失败" }
    Done "产物已生成: dist\payload\"
}

function Ensure-DotNetInstaller {
    if (Test-Path $dotnetInstaller) {
        Info ".NET runtime 安装器已缓存: $dotnetInstaller"
        return
    }
    Info "下载 .NET $dotnetVersion Desktop Runtime (~60 MB)..."
    New-Item -ItemType Directory -Force -Path $runtimesDir | Out-Null
    try {
        Invoke-WebRequest -Uri $dotnetUrl -OutFile $dotnetInstaller -UseBasicParsing
        Done ".NET runtime 下载完成"
    } catch {
        Fail ".NET runtime 下载失败: $_`n请手动到 $dotnetUrl 下载并放到 $dotnetInstaller"
    }
}

function Build-ToolboxInstallers {
    param([string]$V)
    $online = Join-Path $root "installer\output\FPSToolbox_Setup_v$V.exe"
    $offline = Join-Path $root "installer\output\FPSToolbox_Setup_v${V}_offline.exe"
    if (Test-Path $online)  { Remove-Item $online  -Force }
    if (Test-Path $offline) { Remove-Item $offline -Force }

    Info "编译在线版 installer..."
    & $InnoSetup "/DAppVersion=$V" "installer\FPSToolbox.iss" | Out-Null
    if (-not (Test-Path $online)) { Fail "在线版 installer 未生成: $online" }
    Done "在线版: $online  ($([math]::Round((Get-Item $online).Length / 1MB, 2)) MB)"

    Info "编译离线版 installer (内嵌 .NET)..."
    Ensure-DotNetInstaller
    $runtimeRel = ".\runtimes\windowsdesktop-runtime-$dotnetVersion-win-x64.exe"
    & $InnoSetup "/DAppVersion=$V" "/DOFFLINE" "/DDOTNET_RUNTIME_FILE=$runtimeRel" "installer\FPSToolbox.iss" | Out-Null
    if (-not (Test-Path $offline)) { Fail "离线版 installer 未生成: $offline" }
    Done "离线版: $offline  ($([math]::Round((Get-Item $offline).Length / 1MB, 2)) MB)"

    Copy-Item $online  $releaseDir -Force
    Copy-Item $offline $releaseDir -Force
    return @($online, $offline)
}

function Pack-Tool {
    param([string]$ToolName, [string]$V)
    Info "打包 $ToolName v$V ..."
    & (Join-Path $root 'scripts\pack-tool.ps1') -Tool $ToolName -Version $V
    if ($LASTEXITCODE -ne 0) { Fail "pack-tool.ps1 ($ToolName) 失败" }
    $zip = Join-Path $distDir "packages\$ToolName-v$V.zip"
    if (-not (Test-Path $zip)) { Fail "zip 未生成: $zip" }
    Copy-Item $zip $releaseDir -Force
    Done "产出: $zip  ($([math]::Round((Get-Item $zip).Length / 1MB, 2)) MB)"
    return $zip
}

function Publish-GhRelease {
    param(
        [string]$Tag,
        [string]$Title,
        [string[]]$Files,
        [string]$NotesBody
    )
    if ($DryRun) {
        Warn "DryRun:跳过上传。Tag=$Tag  Files=$($Files -join ', ')"
        return
    }
    # 若 tag 已存在,先删(便于重跑。生产建议升小版本号而不是覆盖)
    $prevEAP = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        gh release view $Tag *> $null
        $tagExists = ($LASTEXITCODE -eq 0)
    } finally {
        $ErrorActionPreference = $prevEAP
    }
    if ($tagExists) {
        Warn "Release $Tag 已存在,先删除..."
        gh release delete $Tag --yes --cleanup-tag | Out-Null
    }

    $args = @('release', 'create', $Tag, '--title', $Title)
    if ($Draft) { $args += '--draft' }
    if ($NotesBody) {
        $tmpNotes = New-TemporaryFile
        Set-Content -Path $tmpNotes -Value $NotesBody -Encoding UTF8
        $args += @('--notes-file', "$tmpNotes")
    } else {
        $args += @('--notes', "$Title")
    }
    $args += $Files

    Info "gh $($args -join ' ')"
    & gh @args
    if ($LASTEXITCODE -ne 0) { Fail "gh release create 失败" }
    Done "Release 已发布: $Tag"
}

# ─────────────────────────────────────────────────────────────
# 执行
# ─────────────────────────────────────────────────────────────
Publish-All

switch ($Target) {
    'toolbox' {
        $files = Build-ToolboxInstallers -V $Version
        Publish-GhRelease -Tag "toolbox-v$Version" -Title "FPS 工具箱 v$Version" `
            -Files $files -NotesBody $Notes
    }
    'crosshair' {
        $zip = Pack-Tool -ToolName 'CrosshairTool' -V $Version
        Publish-GhRelease -Tag "crosshair-v$Version" -Title "屏幕准心工具 v$Version" `
            -Files @($zip) -NotesBody $Notes
    }
    'gamma' {
        $zip = Pack-Tool -ToolName 'GammaTool' -V $Version
        Publish-GhRelease -Tag "gamma-v$Version" -Title "屏幕调节工具 v$Version" `
            -Files @($zip) -NotesBody $Notes
    }
    'all' {
        $tbFiles = Build-ToolboxInstallers -V $Version
        $chZip   = Pack-Tool -ToolName 'CrosshairTool' -V $Version
        $gmZip   = Pack-Tool -ToolName 'GammaTool' -V $Version
        Publish-GhRelease -Tag "toolbox-v$Version"   -Title "FPS 工具箱 v$Version"       -Files $tbFiles -NotesBody $Notes
        Publish-GhRelease -Tag "crosshair-v$Version" -Title "屏幕准心工具 v$Version"     -Files @($chZip) -NotesBody $Notes
        Publish-GhRelease -Tag "gamma-v$Version"     -Title "屏幕调节工具 v$Version"     -Files @($gmZip) -NotesBody $Notes
    }
}

Done "全部完成 🎉  产物也复制到: $releaseDir"
