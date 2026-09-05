# 生成 ScreenShare.ico：Fluent 风格应用图标（渐变蓝圆角方块 + 白色显示器符号）
# 多尺寸（16/24/32/48/64/128/256），PNG 压缩打包为 ICO（Vista+ 支持）
Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = "Stop"

function New-RoundedRectPath([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x, $y, $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y, $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d, 0, 90)
    $p.AddArc($x, $y + $h - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $u = [float]$size / 100.0  # 100 逻辑坐标缩放

    # 背景：圆角方形 + 渐变（左上亮蓝 → 右下深蓝）
    $rect = [System.Drawing.RectangleF]::new((3 * $u), (3 * $u), (94 * $u), (94 * $u))
    $bg = New-RoundedRectPath $rect.X $rect.Y $rect.Width $rect.Height (22 * $u)
    $grad = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 91, 210, 255),
        [System.Drawing.Color]::FromArgb(255, 30, 111, 190),
        50.0)
    $g.FillPath($grad, $bg)
    $grad.Dispose(); $bg.Dispose()

    # 白色显示器：屏幕（圆角描边）+ 底座
    $tw = [Math]::Max(1.0, $size / 15.0)   # 线宽随尺寸
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, $tw)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $sx = 22 * $u; $sy = 27 * $u; $sw = 56 * $u; $sh = 38 * $u
    $screen = New-RoundedRectPath $sx $sy $sw $sh (6 * $u)
    $g.DrawPath($pen, $screen)
    $screen.Dispose()

    # 底座：V 形支架 + 底横线
    $g.DrawLine($pen, 36 * $u, 72 * $u, 50 * $u, 81 * $u)
    $g.DrawLine($pen, 64 * $u, 72 * $u, 50 * $u, 81 * $u)
    $g.DrawLine($pen, 40 * $u, 85 * $u, 60 * $u, 85 * $u)

    $pen.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngs = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , ($ms.ToArray())
    $ms.Dispose()
}

$dir = Join-Path $PSScriptRoot "icons"
if (-not (Test-Path $dir)) { New-Item -ItemType Directory $dir | Out-Null }
$out = Join-Path $dir "ScreenShare.ico"

$fs = [System.IO.File]::Create($out)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type: icon
$bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # width
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))  # height
    $bw.Write([byte]0)  # colors
    $bw.Write([byte]0)  # reserved
    $bw.Write([UInt16]1)   # planes
    $bw.Write([UInt16]32)  # bpp
    $bw.Write([UInt32]$len)
    $bw.Write([UInt32]$offset)
    $offset += $len
}
for ($i = 0; $i -lt $sizes.Count; $i++) { $bw.Write($pngs[$i]) }
$bw.Flush(); $bw.Close(); $fs.Close()

"OK: $out ($((Get-Item $out).Length) bytes, $($sizes -join '/'))"
