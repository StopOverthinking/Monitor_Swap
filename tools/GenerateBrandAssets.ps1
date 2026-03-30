param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\Assets")
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

function New-RoundedRectanglePath {
    param(
        [float]$X,
        [float]$Y,
        [float]$Width,
        [float]$Height,
        [float]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function New-AppIconBitmap {
    param([int]$Size)

    $bitmap = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $backgroundPath = New-RoundedRectanglePath -X ($Size * 0.08) -Y ($Size * 0.08) -Width ($Size * 0.84) -Height ($Size * 0.84) -Radius ($Size * 0.18)
        $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.RectangleF]::new($Size * 0.08, $Size * 0.08, $Size * 0.84, $Size * 0.84),
            [System.Drawing.Color]::FromArgb(255, 13, 39, 84),
            [System.Drawing.Color]::FromArgb(255, 32, 160, 195),
            45
        )
        $graphics.FillPath($backgroundBrush, $backgroundPath)

        $shadowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90, 255, 255, 255), [Math]::Max(2, $Size * 0.03))
        $graphics.DrawPath($shadowPen, $backgroundPath)

        $screenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(235, 244, 250, 255), [Math]::Max(2, $Size * 0.055))
        $screenPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $screenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(58, 255, 255, 255))
        $screenWidth = $Size * 0.26
        $screenHeight = $Size * 0.19
        $leftX = $Size * 0.16
        $rightX = $Size * 0.58
        $screenY = $Size * 0.2
        $corner = $Size * 0.035

        foreach ($screenX in @($leftX, $rightX)) {
            $screenPath = New-RoundedRectanglePath -X $screenX -Y $screenY -Width $screenWidth -Height $screenHeight -Radius $corner
            $graphics.FillPath($screenBrush, $screenPath)
            $graphics.DrawPath($screenPen, $screenPath)
            $graphics.DrawLine($screenPen, $screenX + $screenWidth * 0.32, $screenY + $screenHeight + $Size * 0.05, $screenX + $screenWidth * 0.68, $screenY + $screenHeight + $Size * 0.05)
            $graphics.DrawLine($screenPen, $screenX + $screenWidth * 0.5, $screenY + $screenHeight, $screenX + $screenWidth * 0.5, $screenY + $screenHeight + $Size * 0.05)
        }

        $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 210, 102), [Math]::Max(3, $Size * 0.075))
        $arrowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

        $leftArrow = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($Size * 0.27, $Size * 0.69),
            [System.Drawing.PointF]::new($Size * 0.64, $Size * 0.69),
            [System.Drawing.PointF]::new($Size * 0.58, $Size * 0.61)
        )
        $rightArrow = [System.Drawing.PointF[]]@(
            [System.Drawing.PointF]::new($Size * 0.73, $Size * 0.49),
            [System.Drawing.PointF]::new($Size * 0.36, $Size * 0.49),
            [System.Drawing.PointF]::new($Size * 0.42, $Size * 0.57)
        )
        $graphics.DrawLines($arrowPen, $leftArrow)
        $graphics.DrawLines($arrowPen, $rightArrow)

        return $bitmap
    }
    finally {
        if ($null -ne $backgroundPath) { $backgroundPath.Dispose() }
        if ($null -ne $backgroundBrush) { $backgroundBrush.Dispose() }
        if ($null -ne $shadowPen) { $shadowPen.Dispose() }
        if ($null -ne $screenPen) { $screenPen.Dispose() }
        if ($null -ne $screenBrush) { $screenBrush.Dispose() }
        if ($null -ne $arrowPen) { $arrowPen.Dispose() }
        $graphics.Dispose()
    }
}

function Convert-BitmapToPngBytes {
    param([System.Drawing.Bitmap]$Bitmap)

    $stream = New-Object System.IO.MemoryStream
    try {
        $Bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        return ,([byte[]]$stream.ToArray())
    }
    finally {
        $stream.Dispose()
    }
}

function Write-IconFile {
    param(
        [string]$Path,
        [int[]]$Sizes
    )

    $images = foreach ($size in $Sizes) {
        $bitmap = New-AppIconBitmap -Size $size
        try {
            [PSCustomObject]@{
                Size = $size
                Bytes = [byte[]](Convert-BitmapToPngBytes -Bitmap $bitmap)
            }
        }
        finally {
            $bitmap.Dispose()
        }
    }

    $stream = [System.IO.File]::Create($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)

    try {
        $writer.Write([UInt16]0)
        $writer.Write([UInt16]1)
        $writer.Write([UInt16]$images.Count)

        $offset = 6 + ($images.Count * 16)
        foreach ($image in $images) {
            $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]32)
            $writer.Write([UInt32]$image.Bytes.Length)
            $writer.Write([UInt32]$offset)
            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write($image.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function New-WizardBitmap {
    param(
        [int]$Width,
        [int]$Height,
        [bool]$Small
    )

    $bitmap = New-Object System.Drawing.Bitmap($Width, $Height, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

        $backgroundBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.Rectangle]::new(0, 0, $Width, $Height),
            [System.Drawing.Color]::FromArgb(11, 26, 52),
            [System.Drawing.Color]::FromArgb(18, 92, 128),
            90
        )
        $graphics.FillRectangle($backgroundBrush, 0, 0, $Width, $Height)

        $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(28, 206, 224))
        $softAccentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(85, 255, 210, 102))
        $graphics.FillEllipse($accentBrush, -30, $Height * 0.08, $Width * 0.9, $Width * 0.9)
        $graphics.FillEllipse($softAccentBrush, $Width * 0.32, $Height * 0.56, $Width * 0.9, $Width * 0.9)

        $logoBitmap = New-AppIconBitmap -Size ([Math]::Min(96, [Math]::Round($Width * 0.55)))
        try {
            $logoSize = if ($Small) { [Math]::Min(38, $Width - 12) } else { [Math]::Min(80, $Width - 36) }
            $logoX = if ($Small) { [Math]::Round(($Width - $logoSize) / 2) } else { 18 }
            $logoY = if ($Small) { 8 } else { 20 }
            $graphics.DrawImage($logoBitmap, $logoX, $logoY, $logoSize, $logoSize)
        }
        finally {
            $logoBitmap.Dispose()
        }

        if (-not $Small) {
            $titleFont = New-Object System.Drawing.Font("Segoe UI Semibold", 16, [System.Drawing.FontStyle]::Bold)
            $bodyFont = New-Object System.Drawing.Font("Segoe UI", 8.75)
            $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(228, 244, 250))
            $mutedBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(185, 223, 236))

            try {
                $graphics.DrawString("Monitor Swap", $titleFont, $bodyBrush, 20, 116)
                $graphics.DrawString("Global hotkeys for rotating windows across your monitors.", $bodyFont, $mutedBrush, [System.Drawing.RectangleF]::new(20, 152, $Width - 34, 54))

                $bulletBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 210, 102))
                $items = @(
                    "Branded tray icon",
                    "Desktop shortcut",
                    "Windows auto-start"
                )
                $y = 218
                foreach ($item in $items) {
                    $graphics.FillEllipse($bulletBrush, 22, $y + 4, 6, 6)
                    $graphics.DrawString($item, $bodyFont, $bodyBrush, [System.Drawing.RectangleF]::new(34, $y, $Width - 48, 32))
                    $y += 24
                }
            }
            finally {
                if ($null -ne $bulletBrush) { $bulletBrush.Dispose() }
                $titleFont.Dispose()
                $bodyFont.Dispose()
                $bodyBrush.Dispose()
                $mutedBrush.Dispose()
            }
        }

        return $bitmap
    }
    finally {
        if ($null -ne $backgroundBrush) { $backgroundBrush.Dispose() }
        if ($null -ne $accentBrush) { $accentBrush.Dispose() }
        if ($null -ne $softAccentBrush) { $softAccentBrush.Dispose() }
        $graphics.Dispose()
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$iconPath = Join-Path $OutputDirectory "MonitorSwap.ico"
$wizardPath = Join-Path $OutputDirectory "InstallerWizard.bmp"
$wizardSmallPath = Join-Path $OutputDirectory "InstallerWizardSmall.bmp"

Write-IconFile -Path $iconPath -Sizes @(16, 24, 32, 48, 64, 128, 256)

$wizardBitmap = New-WizardBitmap -Width 164 -Height 314 -Small $false
try {
    $wizardBitmap.Save($wizardPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
}
finally {
    $wizardBitmap.Dispose()
}

$wizardSmallBitmap = New-WizardBitmap -Width 55 -Height 55 -Small $true
try {
    $wizardSmallBitmap.Save($wizardSmallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
}
finally {
    $wizardSmallBitmap.Dispose()
}

Write-Host "Generated brand assets in $OutputDirectory"
