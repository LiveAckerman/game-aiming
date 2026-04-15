Add-Type -AssemblyName System.Drawing

function Draw-Crosshair([int]$Size) {
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $c = [double]$Size / 2.0
    $gap = [Math]::Max(2.0, $c / 3.2)
    $len = [Math]::Max(4.0, $c / 1.6)
    $w   = [Math]::Max(1.5, [double]$Size / 13.0)
    $ow  = $w + 1.5

    $green = [System.Drawing.Color]::FromArgb(255, 0, 220, 0)
    $black = [System.Drawing.Color]::FromArgb(200, 0, 0, 0)

    $op = New-Object System.Drawing.Pen($black, [float]$ow)
    $op.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $op.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $mp = New-Object System.Drawing.Pen($green, [float]$w)
    $mp.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $mp.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $coords = @(
        [float]($c), [float]($c - $gap - $len), [float]($c), [float]($c - $gap),
        [float]($c), [float]($c + $gap),         [float]($c), [float]($c + $gap + $len),
        [float]($c - $gap - $len), [float]$c,    [float]($c - $gap), [float]$c,
        [float]($c + $gap),        [float]$c,    [float]($c + $gap + $len), [float]$c
    )
    for ($i = 0; $i -lt $coords.Count; $i += 4) {
        $g.DrawLine($op, $coords[$i], $coords[$i+1], $coords[$i+2], $coords[$i+3])
    }
    for ($i = 0; $i -lt $coords.Count; $i += 4) {
        $g.DrawLine($mp, $coords[$i], $coords[$i+1], $coords[$i+2], $coords[$i+3])
    }

    $dr = [Math]::Max(1.2, [double]$Size / 18.0)
    $br1 = New-Object System.Drawing.SolidBrush($black)
    $br2 = New-Object System.Drawing.SolidBrush($green)
    $g.FillEllipse($br1, [float]($c-$dr-0.8), [float]($c-$dr-0.8), [float](($dr+0.8)*2), [float](($dr+0.8)*2))
    $g.FillEllipse($br2, [float]($c-$dr),     [float]($c-$dr),     [float]($dr*2),        [float]($dr*2))

    $br1.Dispose(); $br2.Dispose(); $op.Dispose(); $mp.Dispose(); $g.Dispose()
    return $bmp
}

# 将 Bitmap 序列化为 ICO BMP 格式（32bpp，行序 bottom-up）
function Bitmap-ToIcoBmpStream([System.Drawing.Bitmap]$bmp, [int]$sz) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    $bw.Write([int32]40)          # BITMAPINFOHEADER.biSize
    $bw.Write([int32]$sz)         # biWidth
    $bw.Write([int32]($sz * 2))   # biHeight (XOR+AND stacked)
    $bw.Write([int16]1)           # biPlanes
    $bw.Write([int16]32)          # biBitCount
    $bw.Write([int32]0)           # biCompression
    $bw.Write([int32]0)           # biSizeImage
    $bw.Write([int32]0)           # biXPelsPerMeter
    $bw.Write([int32]0)           # biYPelsPerMeter
    $bw.Write([int32]0)           # biClrUsed
    $bw.Write([int32]0)           # biClrImportant

    # XOR mask：BGRA，bottom-up
    for ($row = $sz - 1; $row -ge 0; $row--) {
        for ($col = 0; $col -lt $sz; $col++) {
            $px = $bmp.GetPixel($col, $row)
            $bw.Write([byte]$px.B)
            $bw.Write([byte]$px.G)
            $bw.Write([byte]$px.R)
            $bw.Write([byte]$px.A)
        }
    }

    # AND mask：行宽对齐到 4 字节，全 0（透明由 Alpha 控制）
    $andRowBytes = [int]([Math]::Ceiling([double]$sz / 32.0)) * 4
    $andTotal    = $andRowBytes * $sz
    $bw.Write((New-Object byte[] $andTotal))
    $bw.Flush()
    return $ms
}

$sizes = @(16, 32, 48, 256)

# 先生成所有图像流，收集长度用于计算偏移
$streams = New-Object System.Collections.Generic.List[System.IO.MemoryStream]
foreach ($sz in $sizes) {
    $bmp = Draw-Crosshair -Size $sz
    $streams.Add((Bitmap-ToIcoBmpStream -bmp $bmp -sz $sz))
    $bmp.Dispose()
}

$count      = $sizes.Count
$dataOffset = 6 + 16 * $count

$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

# ICONDIR
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)

# ICONDIRENTRY
$off = [int]$dataOffset
for ($i = 0; $i -lt $count; $i++) {
    $s  = $streams[$i]
    $sz = if ($sizes[$i] -eq 256) { [byte]0 } else { [byte]$sizes[$i] }
    $bw.Write($sz)
    $bw.Write($sz)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$s.Length)
    $bw.Write([uint32]$off)
    $off += [int]$s.Length
}

# 图像数据
foreach ($s in $streams) {
    $s.Position = 0
    $s.CopyTo($out)
    $s.Dispose()
}

$bw.Flush()

$outPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\src\CrosshairTool\Resources\icon.ico"))
[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())
Write-Host "Icon: $outPath ($($out.Length) bytes, 16/32/48/256px)" -ForegroundColor Green
