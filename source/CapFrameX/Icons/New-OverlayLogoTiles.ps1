param(
    # The OSD editor embeds both PNGs; the default is the sibling OSD checkout's Controls.
    [string] $OutputRoot = (Join-Path $PSScriptRoot '..\..\..\..\CapFrameX.OSD\CapFrameX.OSD.Controls\Resources\Logos')
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

# The palette's CapFrameX banner and logo tiles draw these images with fit "contain" on a tile
# in the brand blue (#0271F9), so the images carry only the white and lime mark and wordmark.
# Banner: 5:1 (OsdLogoPresets.BannerAspectRatio), mark and wordmark side by side as on the
# installer banner, with the same margin on all four sides.
# Logo: square, the mark where the app icon places it (New-TileIcon in Generate-CXAssets.ps1).
$bannerWidth = 1280
$bannerHeight = 256
$logoSize = 512

$productionRoot = Join-Path $PSScriptRoot 'CX_Concepts\Production'
$markPath = Join-Path $productionRoot 'CX_A1_Mark_Master.png'
$wordmarkPath = Join-Path $productionRoot 'CX_A1_Wordmark_Master.png'

function New-HighQualityGraphics {
    param([System.Drawing.Image] $Image)

    $graphics = [System.Drawing.Graphics]::FromImage($Image)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.Clear([System.Drawing.Color]::Transparent)
    return $graphics
}

function Draw-ContainedImage {
    param(
        [System.Drawing.Graphics] $Graphics,
        [System.Drawing.Image] $Image,
        [System.Drawing.RectangleF] $Bounds
    )

    $scale = [Math]::Min($Bounds.Width / $Image.Width, $Bounds.Height / $Image.Height)
    $width = [single] ($Image.Width * $scale)
    $height = [single] ($Image.Height * $scale)
    $x = [single] ($Bounds.X + (($Bounds.Width - $width) / 2.0))
    $y = [single] ($Bounds.Y + (($Bounds.Height - $height) / 2.0))
    # Clamp the edge pixels so the bicubic filter does not fade the mark's outer rows.
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    try {
        $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
        [System.Drawing.PointF[]] $destination = @(
            [System.Drawing.PointF]::new($x, $y),
            [System.Drawing.PointF]::new($x + $width, $y),
            [System.Drawing.PointF]::new($x, $y + $height))
        $Graphics.DrawImage(
            $Image,
            $destination,
            [System.Drawing.RectangleF]::new(0, 0, $Image.Width, $Image.Height),
            [System.Drawing.GraphicsUnit]::Pixel,
            $attributes)
    }
    finally {
        $attributes.Dispose()
    }
}

function Save-PngAtomic {
    param(
        [System.Drawing.Image] $Image,
        [string] $Path
    )

    $temporaryPath = "$Path.new"
    $Image.Save($temporaryPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

foreach ($path in @($markPath, $wordmarkPath)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing master asset: $path (run Generate-CXAssets.ps1 first)"
    }
}

New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
$OutputRoot = (Resolve-Path -LiteralPath $OutputRoot).Path

$mark = [System.Drawing.Image]::FromFile($markPath)
$wordmark = [System.Drawing.Image]::FromFile($wordmarkPath)
try {
    $markAspect = $mark.Width / $mark.Height
    $wordmarkAspect = $wordmark.Width / $wordmark.Height

    # Proportions of CX_Installer_Banner.png: the wordmark is half as tall as the mark, and the
    # gap between them is 0.32 mark heights. Content and margins fill the banner exactly:
    # width = 2m + content(Hm), height = 2m + Hm.
    $wordmarkShare = 0.5
    $gapShare = 0.32
    $contentPerMarkHeight = $markAspect + $gapShare + ($wordmarkAspect * $wordmarkShare)
    $markHeight = ($bannerWidth - $bannerHeight) / ($contentPerMarkHeight - 1.0)
    $margin = ($bannerHeight - $markHeight) / 2.0
    $wordmarkHeight = $markHeight * $wordmarkShare
    $markWidth = $markHeight * $markAspect
    $wordmarkWidth = $wordmarkHeight * $wordmarkAspect

    $banner = [System.Drawing.Bitmap]::new(
        $bannerWidth,
        $bannerHeight,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = New-HighQualityGraphics -Image $banner
    try {
        Draw-ContainedImage -Graphics $graphics -Image $mark -Bounds ([System.Drawing.RectangleF]::new(
            [single] $margin, [single] $margin, [single] $markWidth, [single] $markHeight))
        $wordmarkX = $margin + $markWidth + ($markHeight * $gapShare)
        Draw-ContainedImage -Graphics $graphics -Image $wordmark -Bounds ([System.Drawing.RectangleF]::new(
            [single] $wordmarkX, [single] (($bannerHeight - $wordmarkHeight) / 2.0),
            [single] $wordmarkWidth, [single] $wordmarkHeight))
    }
    finally {
        $graphics.Dispose()
    }

    try {
        Save-PngAtomic -Image $banner -Path (Join-Path $OutputRoot 'CapFrameX-Banner.png')
    }
    finally {
        $banner.Dispose()
    }

    $logo = [System.Drawing.Bitmap]::new(
        $logoSize,
        $logoSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = New-HighQualityGraphics -Image $logo
    try {
        Draw-ContainedImage -Graphics $graphics -Image $mark -Bounds ([System.Drawing.RectangleF]::new(
            [single] ($logoSize * 0.095), [single] ($logoSize * 0.245),
            [single] ($logoSize * 0.81), [single] ($logoSize * 0.51)))
    }
    finally {
        $graphics.Dispose()
    }

    try {
        Save-PngAtomic -Image $logo -Path (Join-Path $OutputRoot 'CapFrameX-Logo.png')
    }
    finally {
        $logo.Dispose()
    }
}
finally {
    $wordmark.Dispose()
    $mark.Dispose()
}

Get-Item -LiteralPath @(
    (Join-Path $OutputRoot 'CapFrameX-Banner.png'),
    (Join-Path $OutputRoot 'CapFrameX-Logo.png')) |
    Select-Object FullName, Length
