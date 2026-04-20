# FPSToolbox 子工具打包脚本
# 把 CrosshairTool 或 GammaTool 单独打成 .zip（含 manifest.json + exe + dll）
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File scripts\pack-tool.ps1 -Tool CrosshairTool
#   powershell -ExecutionPolicy Bypass -File scripts\pack-tool.ps1 -Tool GammaTool
#
# 输出位置：
#   dist\packages\CrosshairTool-v1.0.0.zip
#   dist\packages\GammaTool-v1.0.0.zip
#
# 把 zip 上传到你的网盘(推荐 GitHub Release, 天然支持直链), 拿到直链后,
# 填到 src\FPSToolbox\Core\ToolRegistry.cs 里对应 ToolDescriptor 的 DownloadUrl 字段,
# 然后重新 publish + 打 FPSToolbox 安装包即可。

param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("CrosshairTool", "GammaTool")]
    [string]$Tool,

    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

$csproj = Join-Path $root "src\$Tool\$Tool.csproj"
$outDir = Join-Path $root "dist\packages\$Tool"
$zipPath = Join-Path $root "dist\packages\$Tool-v$Version.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

Write-Host "Publishing $Tool ..." -ForegroundColor Cyan
& dotnet publish $csproj `
    -c Release --no-self-contained `
    "-p:DebugType=None" "-p:DebugSymbols=false" `
    -o $outDir 2>&1 | Out-Null

if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# 计算 sha256
$exePath = Join-Path $outDir "$Tool.exe"
$sha = (Get-FileHash $exePath -Algorithm SHA256).Hash

$manifest = @{
    name = $Tool
    version = $Version
    exeName = "$Tool.exe"
    sha256 = $sha
} | ConvertTo-Json

Set-Content (Join-Path $outDir "manifest.json") -Value $manifest -Encoding UTF8

if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$outDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

$zipKB = [Math]::Round((Get-Item $zipPath).Length / 1KB, 0)
Write-Host ""
Write-Host "Done: $zipPath (${zipKB} KB)" -ForegroundColor Green
Write-Host "Upload this zip, share the link. Users import via FPSToolbox > '从本地安装'." -ForegroundColor Gray
