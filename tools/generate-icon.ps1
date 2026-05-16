# Generates installer/hearitloud.ico — the app's launcher icon.
#
# Design brief (from user): "like a nuke sign but instead of the sign put a
# pierced ear". Interpretation: radiation-trefoil layout (yellow disc, three
# black 60° wedges at the canonical 30°/150°/270° angles, white center disc),
# but the center disc holds a stylized ear silhouette with a piercing stud
# instead of the usual small black circle.
#
# Renders at 256, 64, 48, 32, 16 px and packages all five into a multi-
# resolution .ico file. Windows picks the right size for taskbar / start menu
# / window-title automatically.
#
# Run from the repo root: pwsh tools/generate-icon.ps1

[CmdletBinding()]
param(
    [string] $OutputPath = "installer/hearitloud.ico"
)

Add-Type -AssemblyName System.Drawing

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode  = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # Palette: warm radiation yellow, deep almost-black for wedges, near-white
    # center, deep black ear stroke, gold piercing stud.
    $yellow = [System.Drawing.Color]::FromArgb(255, 250, 196, 36)
    $wedge  = [System.Drawing.Color]::FromArgb(255, 18, 18, 22)
    $center = [System.Drawing.Color]::FromArgb(255, 246, 246, 248)
    $stroke = [System.Drawing.Color]::FromArgb(255, 18, 18, 22)
    $stud   = [System.Drawing.Color]::FromArgb(255, 232, 168, 32)

    # Yellow disc with a thin dark rim so the icon reads at 16px on dark taskbars.
    $rim = [Math]::Max(1, [int]($Size * 0.025))
    $discRect = New-Object System.Drawing.Rectangle($rim, $rim, ($Size - 2*$rim), ($Size - 2*$rim))
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($yellow)), $discRect)
    $rimPen = New-Object System.Drawing.Pen($wedge, [float]($rim * 1.0))
    $g.DrawEllipse($rimPen, $discRect)

    # Three radiation wedges. System.Drawing angles are 0°=east, +cw. Center
    # the wedges on the canonical 90°/210°/330° (top, bottom-left, bottom-right).
    $wedgeBrush = New-Object System.Drawing.SolidBrush($wedge)
    $wedgePadding = [int]($Size * 0.13)
    $wedgeRect = New-Object System.Drawing.Rectangle($wedgePadding, $wedgePadding, ($Size - 2*$wedgePadding), ($Size - 2*$wedgePadding))
    foreach ($centerDeg in @(-90, 30, 150)) {
        # 60° wedge centered on each
        $g.FillPie($wedgeBrush, $wedgeRect, ($centerDeg - 30), 60)
    }

    # Center disc (where the radiation trefoil's inner circle would go) — this
    # is the spot the ear sits in.
    $cd = [int]($Size * 0.38)
    $cx = [int](($Size - $cd) / 2)
    $centerRect = New-Object System.Drawing.Rectangle($cx, $cx, $cd, $cd)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($center)), $centerRect)

    # Stylized ear: a C-shaped path with a small inner curl. We draw it
    # relative to the center disc so it scales cleanly with $Size.
    $earPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $earLeft  = $cx + [int]($cd * 0.18)
    $earTop   = $cx + [int]($cd * 0.10)
    $earW     = [int]($cd * 0.58)
    $earH     = [int]($cd * 0.80)

    # Outer helix arc (top of ear, curving right then down).
    $earPath.AddBezier(
        ($earLeft + $earW * 0.20), ($earTop + $earH * 0.05),     # start: upper-left
        ($earLeft + $earW * 0.95), ($earTop + $earH * 0.00),     # ctrl 1: upper-right reach
        ($earLeft + $earW * 1.05), ($earTop + $earH * 0.55),     # ctrl 2: middle-right reach
        ($earLeft + $earW * 0.70), ($earTop + $earH * 0.85))     # end: lower middle
    # Earlobe curve.
    $earPath.AddBezier(
        ($earLeft + $earW * 0.70), ($earTop + $earH * 0.85),
        ($earLeft + $earW * 0.55), ($earTop + $earH * 1.05),
        ($earLeft + $earW * 0.35), ($earTop + $earH * 0.95),
        ($earLeft + $earW * 0.30), ($earTop + $earH * 0.75))
    # Inner curl going back up.
    $earPath.AddBezier(
        ($earLeft + $earW * 0.30), ($earTop + $earH * 0.75),
        ($earLeft + $earW * 0.55), ($earTop + $earH * 0.65),
        ($earLeft + $earW * 0.62), ($earTop + $earH * 0.45),
        ($earLeft + $earW * 0.50), ($earTop + $earH * 0.30))

    $strokeW = [Math]::Max(1.5, [float]($Size * 0.018))
    $earPen = New-Object System.Drawing.Pen($stroke, $strokeW)
    $earPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $earPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $earPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($earPen, $earPath)

    # Piercing stud — a small gold dot on the earlobe.
    $studD = [Math]::Max(2, [int]($cd * 0.10))
    $studX = $earLeft + [int]($earW * 0.45) - [int]($studD / 2)
    $studY = $earTop  + [int]($earH * 0.92) - [int]($studD / 2)
    $g.FillEllipse((New-Object System.Drawing.SolidBrush($stud)),
        (New-Object System.Drawing.Rectangle($studX, $studY, $studD, $studD)))
    $g.DrawEllipse((New-Object System.Drawing.Pen($stroke, [Math]::Max(1, [float]($Size * 0.008)))),
        (New-Object System.Drawing.Rectangle($studX, $studY, $studD, $studD)))

    $g.Dispose()
    return $bmp
}

function Write-Ico {
    param(
        [string]$Path,
        [System.Drawing.Bitmap[]]$Bitmaps
    )

    # ICO container: ICONDIR (6 bytes) + N * ICONDIRENTRY (16 bytes each) +
    # concatenated image data. Each image is stored as a PNG (modern Windows
    # ICOs support this; smaller than raw BMP DIBs).
    $entries = @()
    foreach ($bmp in $Bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $entries += [PSCustomObject]@{
            Size = $bmp.Width
            Data = $ms.ToArray()
        }
    }

    $fs = [System.IO.File]::Create($Path)
    $bw = New-Object System.IO.BinaryWriter($fs)
    try {
        $bw.Write([UInt16] 0)              # Reserved
        $bw.Write([UInt16] 1)              # Type 1 = icon
        $bw.Write([UInt16] $entries.Count) # Number of images

        $offset = 6 + 16 * $entries.Count
        foreach ($e in $entries) {
            $w = if ($e.Size -ge 256) { 0 } else { [byte]$e.Size }
            $bw.Write([byte]$w)                 # Width  (0 means 256)
            $bw.Write([byte]$w)                 # Height (0 means 256)
            $bw.Write([byte]0)                  # Palette colors (0 = none)
            $bw.Write([byte]0)                  # Reserved
            $bw.Write([UInt16] 1)               # Color planes
            $bw.Write([UInt16] 32)              # Bits per pixel
            $bw.Write([UInt32] $e.Data.Length)  # Image data size
            $bw.Write([UInt32] $offset)         # Offset to image data
            $offset += $e.Data.Length
        }
        foreach ($e in $entries) { $bw.Write($e.Data) }
    }
    finally {
        $bw.Close()
        $fs.Close()
    }
}

# Render at 5 standard Windows icon resolutions.
$sizes = @(256, 64, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { New-IconBitmap -Size $_ }

# Ensure output directory exists.
$dir = Split-Path -Parent $OutputPath
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

Write-Ico -Path $OutputPath -Bitmaps $bitmaps
Write-Host "Wrote $OutputPath ($((Get-Item $OutputPath).Length) bytes, $($sizes.Count) resolutions: $($sizes -join ', '))"

# Cleanup bitmaps
$bitmaps | ForEach-Object { $_.Dispose() }
