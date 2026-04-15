# CrosshairTool 发布脚本 —— 同时生成两个版本
#
# 版本 A（轻量版）：框架依赖，需用户已安装 .NET 8，EXE ~150KB
# 版本 B（独立版）：自包含，内置 .NET 8，无需用户安装任何依赖，~80MB
#
# 用法：
#   powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
#
# 输出：
#   dist\lite\       → 版本 A（供 installer\installer.iss 打包）
#   dist\standalone\ → 版本 B（供 installer\installer-standalone.iss 打包）

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $root "src\CrosshairTool\CrosshairTool.csproj"

Write-Host "=== CrosshairTool Publish ===" -ForegroundColor Cyan

# ────────────────────────────────────────────
# 1. 生成准心图标
# ────────────────────────────────────────────
Write-Host "[1/4] Generating icon..." -ForegroundColor Yellow

Add-Type -AssemblyName System.Drawing
$sz = 32
$bmp = [System.Drawing.Bitmap]::new($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$g.Clear([System.Drawing.Color]::Transparent)
$c = 16.0; $gap = 5.0; $len = 10.0; $w = 2.5
$green = [System.Drawing.Color]::FromArgb(255, 0, 220, 0)
$black = [System.Drawing.Color]::FromArgb(200, 0, 0, 0)
$op = [System.Drawing.Pen]::new($black, $w + 1.5)
$mp = [System.Drawing.Pen]::new($green, $w)
$op.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $op.EndCap = $op.StartCap
$mp.StartCap = [System.Drawing.Drawing2D.LineCap]::Round; $mp.EndCap = $mp.StartCap
$g.DrawLine($op,$c,$c-$gap-$len,$c,$c-$gap); $g.DrawLine($op,$c,$c+$gap,$c,$c+$gap+$len)
$g.DrawLine($op,$c-$gap-$len,$c,$c-$gap,$c); $g.DrawLine($op,$c+$gap,$c,$c+$gap+$len,$c)
$g.DrawLine($mp,$c,$c-$gap-$len,$c,$c-$gap); $g.DrawLine($mp,$c,$c+$gap,$c,$c+$gap+$len)
$g.DrawLine($mp,$c-$gap-$len,$c,$c-$gap,$c); $g.DrawLine($mp,$c+$gap,$c,$c+$gap+$len,$c)
$g.FillEllipse([System.Drawing.Brushes]::Black,14.5,14.5,3.0,3.0)
$g.FillEllipse([System.Drawing.SolidBrush]::new($green),15.0,15.0,2.0,2.0)
$g.Dispose(); $op.Dispose(); $mp.Dispose()
$pixMs = [System.IO.MemoryStream]::new()
$pw = [System.IO.BinaryWriter]::new($pixMs)
for ($row = $sz-1; $row -ge 0; $row--) {
    for ($col = 0; $col -lt $sz; $col++) {
        $px = $bmp.GetPixel($col,$row)
        $pw.Write([byte]$px.B); $pw.Write([byte]$px.G); $pw.Write([byte]$px.R); $pw.Write([byte]$px.A)
    }
}
$pw.Write([byte[]]::new(128)); $pw.Flush(); $bmp.Dispose()
$bihMs = [System.IO.MemoryStream]::new(); $bw2 = [System.IO.BinaryWriter]::new($bihMs)
$bw2.Write([int32]40);$bw2.Write([int32]$sz);$bw2.Write([int32]($sz*2))
$bw2.Write([int16]1);$bw2.Write([int16]32);$bw2.Write([int32]0)
$bw2.Write([int32]0);$bw2.Write([int32]0);$bw2.Write([int32]0)
$bw2.Write([int32]0);$bw2.Write([int32]0);$bw2.Flush()
$imgSz = [int]($bihMs.Length + $pixMs.Length)
$ico = [System.IO.MemoryStream]::new(); $iw = [System.IO.BinaryWriter]::new($ico)
$iw.Write([uint16]0);$iw.Write([uint16]1);$iw.Write([uint16]1)
$iw.Write([byte]$sz);$iw.Write([byte]$sz);$iw.Write([byte]0);$iw.Write([byte]0)
$iw.Write([uint16]1);$iw.Write([uint16]32);$iw.Write([uint32]$imgSz);$iw.Write([uint32]22)
$iw.Flush(); $bihMs.Position=0;$bihMs.CopyTo($ico); $pixMs.Position=0;$pixMs.CopyTo($ico)
$iconPath = Join-Path $root "src\CrosshairTool\Resources\icon.ico"
[System.IO.File]::WriteAllBytes($iconPath, $ico.ToArray())
Write-Host "    Icon: $iconPath ($($ico.Length) bytes)" -ForegroundColor Gray

# ────────────────────────────────────────────
# 2. 版本 A：框架依赖（轻量版）
# ────────────────────────────────────────────
Write-Host "[2/4] Building LITE (framework-dependent)..." -ForegroundColor Yellow
$liteDir = Join-Path $root "dist\lite"
if (Test-Path $liteDir) { Remove-Item $liteDir -Recurse -Force }

& "C:\Program Files\dotnet\dotnet.exe" publish $proj `
    -c Release --no-self-contained `
    "-p:DebugType=None" "-p:DebugSymbols=false" `
    -o $liteDir

if ($LASTEXITCODE -ne 0) { Write-Host "LITE build FAILED." -ForegroundColor Red; exit 1 }

$liteExeKB = [Math]::Round((Get-Item (Join-Path $liteDir "CrosshairTool.exe")).Length/1KB, 0)
Write-Host "    Lite: $liteDir (EXE=${liteExeKB}KB)" -ForegroundColor Gray

# ────────────────────────────────────────────
# 3. 版本 B：自包含（独立版，内置 .NET 8）
# ────────────────────────────────────────────
Write-Host "[3/4] Building STANDALONE (self-contained, .NET 8 bundled)..." -ForegroundColor Yellow
$standaloneDir = Join-Path $root "dist\standalone"
if (Test-Path $standaloneDir) { Remove-Item $standaloneDir -Recurse -Force }

& "C:\Program Files\dotnet\dotnet.exe" publish $proj `
    -c Release -r win-x64 --self-contained true `
    "-p:DebugType=None" "-p:DebugSymbols=false" `
    "-p:PublishReadyToRun=true" `
    -o $standaloneDir

if ($LASTEXITCODE -ne 0) { Write-Host "STANDALONE build FAILED." -ForegroundColor Red; exit 1 }

$standaloneMB = [Math]::Round((Get-ChildItem $standaloneDir -Recurse | Measure-Object Length -Sum).Sum/1MB, 1)
Write-Host "    Standalone: $standaloneDir (${standaloneMB}MB total)" -ForegroundColor Gray

# ────────────────────────────────────────────
# 4. 汇总
# ────────────────────────────────────────────
Write-Host ""
Write-Host "[4/4] Done!" -ForegroundColor Green
Write-Host "  Lite (needs .NET 8):  dist\lite\"       -ForegroundColor White
Write-Host "  Standalone (no deps): dist\standalone\" -ForegroundColor White
Write-Host ""
Write-Host "Next: open installer\ in Inno Setup and compile:" -ForegroundColor Gray
Write-Host "  installer.iss            → CrosshairTool_Setup_Lite_v1.0.0.exe"      -ForegroundColor Gray
Write-Host "  installer-standalone.iss → CrosshairTool_Setup_Standalone_v1.0.0.exe" -ForegroundColor Gray
