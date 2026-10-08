# アプリアイコン（app.ico）を生成するスクリプト。
# 使い方: powershell -ExecutionPolicy Bypass -File tools\make-icon.ps1
param([string]$Out = (Join-Path $PSScriptRoot "..\src\TestDataMaker\app.ico"))
Add-Type -AssemblyName System.Drawing

function New-IconPng([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 角丸の背景
    $r = [Math]::Max(2, [int]($size * 0.18))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $w = $size - 1
    $path.AddArc(0, 0, $r * 2, $r * 2, 180, 90)
    $path.AddArc($w - $r * 2, 0, $r * 2, $r * 2, 270, 90)
    $path.AddArc($w - $r * 2, $w - $r * 2, $r * 2, $r * 2, 0, 90)
    $path.AddArc(0, $w - $r * 2, $r * 2, $r * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 30, 94, 200))), $path)

    # 表（ヘッダー行＋データ行）
    $m = [single]($size * 0.18)
    $tw = [single]($size - $m * 2)
    $rowH = [single]($tw / 4)
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::White)
    $header = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 200, 60))
    $g.FillRectangle($header, $m, $m, $tw, $rowH)
    $gap = [single][Math]::Max(1, $size / 32)
    for ($i = 1; $i -lt 4; $i++) {
        $g.FillRectangle($white, $m, $m + $rowH * $i + $gap, $tw, $rowH - $gap)
    }
    # 列の区切り
    if ($size -ge 24) {
        $line = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 30, 94, 200)), ([single][Math]::Max(1, $size / 32))
        $g.DrawLine($line, $m + $tw * 0.35, $m + $rowH, $m + $tw * 0.35, $m + $tw)
    }
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = New-Object System.Collections.Generic.List[byte[]]
foreach ($s in $sizes) { $images.Add([byte[]](New-IconPng $s)) }
"sizes: " + (($images | ForEach-Object { $_.Length }) -join ",")

$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([UInt16]0); $bw.Write([UInt16]1); $bw.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([UInt16]1); $bw.Write([UInt16]32)
    $bw.Write([UInt32]$images[$i].Length); $bw.Write([UInt32]$offset)
    $offset += $images[$i].Length
}
foreach ($img in $images) { $bw.Write($img) }
$bw.Close()
"created $Out"
