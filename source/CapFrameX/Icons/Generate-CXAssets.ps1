param(
    [string] $OutputRoot = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing

$blue = [System.Drawing.Color]::FromArgb(255, 2, 113, 249)
$green = [System.Drawing.Color]::FromArgb(255, 151, 239, 4)
$white = [System.Drawing.Color]::FromArgb(255, 253, 253, 253)

$markSourcePath = Join-Path $PSScriptRoot 'CX_Concepts\A_Refinements\CX_A1_SoftSquare.png'
$wordmarkSourcePath = Join-Path $PSScriptRoot 'CX_Concepts\Production\wordmark_chroma.png'
$productionRoot = Join-Path $PSScriptRoot 'CX_Concepts\Production'

New-Item -ItemType Directory -Path $productionRoot -Force | Out-Null

function New-ArgbBitmap {
    param(
        [int] $Width,
        [int] $Height
    )

    return [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
}

function New-HighQualityGraphics {
    param([System.Drawing.Image] $Image)

    $graphics = [System.Drawing.Graphics]::FromImage($Image)
    $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    return $graphics
}

function Get-ResizedCrop {
    param(
        [string] $Path,
        [System.Drawing.Rectangle] $Crop,
        [int] $Width,
        [int] $Height
    )

    $source = [System.Drawing.Image]::FromFile($Path)
    try {
        $result = New-ArgbBitmap -Width $Width -Height $Height
        $graphics = New-HighQualityGraphics -Image $result
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $destination = [System.Drawing.Rectangle]::new(0, 0, $Width, $Height)
            $graphics.DrawImage(
                $source,
                $destination,
                $Crop,
                [System.Drawing.GraphicsUnit]::Pixel)
        }
        finally {
            $graphics.Dispose()
        }

        return $result
    }
    finally {
        $source.Dispose()
    }
}

function Convert-BlueKeyToMark {
    param([System.Drawing.Bitmap] $Source)

    $width = $Source.Width
    $height = $Source.Height
    $rect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $sourceData = $Source.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        $pixelFormat)

    try {
        $stride = [Math]::Abs($sourceData.Stride)
        $sourceBytes = [byte[]]::new($stride * $height)
        [System.Runtime.InteropServices.Marshal]::Copy(
            $sourceData.Scan0,
            $sourceBytes,
            0,
            $sourceBytes.Length)
    }
    finally {
        $Source.UnlockBits($sourceData)
    }

    $result = New-ArgbBitmap -Width $width -Height $height
    $resultData = $result.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
        $pixelFormat)

    try {
        $resultStride = [Math]::Abs($resultData.Stride)
        $resultBytes = [byte[]]::new($resultStride * $height)

        # Foreground coverage is reconstructed from the two color ramps in the
        # generated concept: blue -> white and blue -> lime.
        $whiteDenominator = 82617.0
        $greenDenominator = 98102.0

        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $sourceIndex = ($y * $stride) + ($x * 4)
                $resultIndex = ($y * $resultStride) + ($x * 4)

                $b = [double] $sourceBytes[$sourceIndex]
                $g = [double] $sourceBytes[$sourceIndex + 1]
                $r = [double] $sourceBytes[$sourceIndex + 2]

                $dr = $r - 2.0
                $dg = $g - 113.0
                $db = $b - 249.0

                $whiteAlpha = (($dr * 251.0) + ($dg * 140.0) + ($db * 4.0)) / $whiteDenominator
                $whiteAlpha = [Math]::Max(0.0, [Math]::Min(1.0, $whiteAlpha))
                $whiteR = 2.0 + ($whiteAlpha * 251.0)
                $whiteG = 113.0 + ($whiteAlpha * 140.0)
                $whiteB = 249.0 + ($whiteAlpha * 4.0)
                $whiteError = (($r - $whiteR) * ($r - $whiteR)) +
                    (($g - $whiteG) * ($g - $whiteG)) +
                    (($b - $whiteB) * ($b - $whiteB))

                $greenAlpha = (($dr * 149.0) + ($dg * 126.0) - ($db * 245.0)) / $greenDenominator
                $greenAlpha = [Math]::Max(0.0, [Math]::Min(1.0, $greenAlpha))
                $greenR = 2.0 + ($greenAlpha * 149.0)
                $greenG = 113.0 + ($greenAlpha * 126.0)
                $greenB = 249.0 - ($greenAlpha * 245.0)
                $greenError = (($r - $greenR) * ($r - $greenR)) +
                    (($g - $greenG) * ($g - $greenG)) +
                    (($b - $greenB) * ($b - $greenB))

                if ($greenError -lt $whiteError) {
                    $alpha = $greenAlpha
                    $foreground = $green
                    $error = $greenError
                }
                else {
                    $alpha = $whiteAlpha
                    $foreground = $white
                    $error = $whiteError
                }

                if (($alpha -le 0.02) -or ($error -gt 2500.0)) {
                    continue
                }

                $alpha = ($alpha - 0.02) / 0.98
                $resultBytes[$resultIndex] = $foreground.B
                $resultBytes[$resultIndex + 1] = $foreground.G
                $resultBytes[$resultIndex + 2] = $foreground.R
                $resultBytes[$resultIndex + 3] = [byte] [Math]::Round(255.0 * $alpha)
            }
        }

        [System.Runtime.InteropServices.Marshal]::Copy(
            $resultBytes,
            0,
            $resultData.Scan0,
            $resultBytes.Length)
    }
    finally {
        $result.UnlockBits($resultData)
    }

    return $result
}

function Convert-MagentaKeyToWordmark {
    param([System.Drawing.Bitmap] $Source)

    $width = $Source.Width
    $height = $Source.Height
    $rect = [System.Drawing.Rectangle]::new(0, 0, $width, $height)
    $pixelFormat = [System.Drawing.Imaging.PixelFormat]::Format32bppArgb
    $sourceData = $Source.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        $pixelFormat)

    try {
        $stride = [Math]::Abs($sourceData.Stride)
        $sourceBytes = [byte[]]::new($stride * $height)
        [System.Runtime.InteropServices.Marshal]::Copy(
            $sourceData.Scan0,
            $sourceBytes,
            0,
            $sourceBytes.Length)
    }
    finally {
        $Source.UnlockBits($sourceData)
    }

    $result = New-ArgbBitmap -Width $width -Height $height
    $resultData = $result.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::WriteOnly,
        $pixelFormat)

    try {
        $resultStride = [Math]::Abs($resultData.Stride)
        $resultBytes = [byte[]]::new($resultStride * $height)
        $greenStart = [int] [Math]::Round($width * 0.875)

        for ($y = 0; $y -lt $height; $y++) {
            for ($x = 0; $x -lt $width; $x++) {
                $sourceIndex = ($y * $stride) + ($x * 4)
                $resultIndex = ($y * $resultStride) + ($x * 4)
                $g = [double] $sourceBytes[$sourceIndex + 1]

                # The chroma background has a green channel below 32. Both
                # foreground colors are above 235, so this preserves a soft
                # antialiased edge without retaining the magenta spill.
                $alpha = ($g - 32.0) / 203.0
                $alpha = [Math]::Max(0.0, [Math]::Min(1.0, $alpha))
                if ($alpha -le 0.02) {
                    continue
                }

                $alpha = ($alpha - 0.02) / 0.98
                $foreground = if ($x -ge $greenStart) { $green } else { $white }
                $resultBytes[$resultIndex] = $foreground.B
                $resultBytes[$resultIndex + 1] = $foreground.G
                $resultBytes[$resultIndex + 2] = $foreground.R
                $resultBytes[$resultIndex + 3] = [byte] [Math]::Round(255.0 * $alpha)
            }
        }

        [System.Runtime.InteropServices.Marshal]::Copy(
            $resultBytes,
            0,
            $resultData.Scan0,
            $resultBytes.Length)
    }
    finally {
        $result.UnlockBits($resultData)
    }

    return $result
}

function Get-AlphaCrop {
    param(
        [System.Drawing.Bitmap] $Source,
        [int] $Threshold = 4
    )

    $left = $Source.Width
    $top = $Source.Height
    $right = -1
    $bottom = -1

    for ($y = 0; $y -lt $Source.Height; $y++) {
        for ($x = 0; $x -lt $Source.Width; $x++) {
            if ($Source.GetPixel($x, $y).A -le $Threshold) {
                continue
            }

            $left = [Math]::Min($left, $x)
            $top = [Math]::Min($top, $y)
            $right = [Math]::Max($right, $x)
            $bottom = [Math]::Max($bottom, $y)
        }
    }

    if ($right -lt $left) {
        throw 'No foreground pixels were found while cropping an alpha image.'
    }

    $crop = [System.Drawing.Rectangle]::new(
        $left,
        $top,
        ($right - $left) + 1,
        ($bottom - $top) + 1)
    return $Source.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
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
    $destination = [System.Drawing.RectangleF]::new($x, $y, $width, $height)
    $Graphics.DrawImage($Image, $destination)
    return $destination
}

function Resize-Contained {
    param(
        [System.Drawing.Image] $Source,
        [int] $Width,
        [int] $Height,
        [int] $Padding = 0
    )

    $result = New-ArgbBitmap -Width $Width -Height $Height
    $graphics = New-HighQualityGraphics -Image $result
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $bounds = [System.Drawing.RectangleF]::new(
            [single] $Padding,
            [single] $Padding,
            [single] ($Width - (2 * $Padding)),
            [single] ($Height - (2 * $Padding)))
        $null = Draw-ContainedImage -Graphics $graphics -Image $Source -Bounds $bounds
    }
    finally {
        $graphics.Dispose()
    }

    return $result
}

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF] $Rectangle,
        [single] $Radius
    )

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $Radius * 2.0
    $path.AddArc($Rectangle.X, $Rectangle.Y, $diameter, $diameter, 180.0, 90.0)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Y, $diameter, $diameter, 270.0, 90.0)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Bottom - $diameter, $diameter, $diameter, 0.0, 90.0)
    $path.AddArc($Rectangle.X, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90.0, 90.0)
    $path.CloseFigure()
    return $path
}

function Resize-Bitmap {
    param(
        [System.Drawing.Image] $Source,
        [int] $Width,
        [int] $Height
    )

    $result = New-ArgbBitmap -Width $Width -Height $Height
    $graphics = New-HighQualityGraphics -Image $result
    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($Source, [System.Drawing.Rectangle]::new(0, 0, $Width, $Height))
    }
    finally {
        $graphics.Dispose()
    }

    return $result
}

function New-TileIcon {
    param(
        [System.Drawing.Image] $Mark,
        [int] $Size
    )

    $supersample = 4
    $workingSize = $Size * $supersample
    $working = New-ArgbBitmap -Width $workingSize -Height $workingSize
    $graphics = New-HighQualityGraphics -Image $working

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $tileBounds = [System.Drawing.RectangleF]::new(0.0, 0.0, [single] $workingSize, [single] $workingSize)
        $tilePath = New-RoundedRectanglePath -Rectangle $tileBounds -Radius ([single] ($workingSize * 0.145))
        $tileBrush = [System.Drawing.SolidBrush]::new($blue)
        try {
            $graphics.FillPath($tileBrush, $tilePath)
        }
        finally {
            $tileBrush.Dispose()
            $tilePath.Dispose()
        }

        $markBounds = [System.Drawing.RectangleF]::new(
            [single] ($workingSize * 0.095),
            [single] ($workingSize * 0.245),
            [single] ($workingSize * 0.81),
            [single] ($workingSize * 0.51))
        $null = Draw-ContainedImage -Graphics $graphics -Image $Mark -Bounds $markBounds
    }
    finally {
        $graphics.Dispose()
    }

    try {
        return Resize-Bitmap -Source $working -Width $Size -Height $Size
    }
    finally {
        $working.Dispose()
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

function Save-JpegAtomic {
    param(
        [System.Drawing.Image] $Image,
        [string] $Path,
        [long] $Quality = 96
    )

    $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
        Where-Object MimeType -eq 'image/jpeg' |
        Select-Object -First 1
    $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
    $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new(
        [System.Drawing.Imaging.Encoder]::Quality,
        $Quality)

    try {
        $temporaryPath = "$Path.new"
        $Image.Save($temporaryPath, $encoder, $parameters)
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        $parameters.Dispose()
    }
}

function ConvertTo-IcoDibFrame {
    param([System.Drawing.Bitmap] $Frame)

    if ($Frame.Width -ne $Frame.Height) {
        throw 'ICO frames must be square.'
    }

    $width = $Frame.Width
    $height = $Frame.Height
    $xorByteCount = $width * $height * 4
    $maskStride = [int] ([Math]::Ceiling($width / 32.0) * 4)

    $stream = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($stream)
    try {
        # BITMAPINFOHEADER. ICO DIB heights include both the color image and AND mask.
        $writer.Write([uint32] 40)
        $writer.Write([int32] $width)
        $writer.Write([int32] ($height * 2))
        $writer.Write([uint16] 1)
        $writer.Write([uint16] 32)
        $writer.Write([uint32] 0)
        $writer.Write([uint32] $xorByteCount)
        $writer.Write([int32] 0)
        $writer.Write([int32] 0)
        $writer.Write([uint32] 0)
        $writer.Write([uint32] 0)

        # Windows icon DIBs use bottom-up BGRA rows.
        for ($y = $height - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $width; $x++) {
                $color = $Frame.GetPixel($x, $y)
                $writer.Write([byte] $color.B)
                $writer.Write([byte] $color.G)
                $writer.Write([byte] $color.R)
                $writer.Write([byte] $color.A)
            }
        }

        # Keep a 1-bpp transparency mask for decoders that do not honor alpha.
        for ($y = $height - 1; $y -ge 0; $y--) {
            $maskRow = [byte[]]::new($maskStride)
            for ($x = 0; $x -lt $width; $x++) {
                if ($Frame.GetPixel($x, $y).A -eq 0) {
                    $maskIndex = [int] [Math]::Floor($x / 8.0)
                    $maskRow[$maskIndex] = [byte] ($maskRow[$maskIndex] -bor (0x80 -shr ($x % 8)))
                }
            }

            $writer.Write($maskRow)
        }

        $writer.Flush()
        return $stream.ToArray()
    }
    finally {
        $writer.Dispose()
        $stream.Dispose()
    }
}

function Save-IcoAtomic {
    param(
        [hashtable] $Frames,
        [string] $Path
    )

    $sizes = @($Frames.Keys | ForEach-Object { [int] $_ } | Sort-Object)
    $frameBytes = @{}

    foreach ($size in $sizes) {
        if ($size -eq 256) {
            $stream = [System.IO.MemoryStream]::new()
            try {
                $Frames[$size].Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
                $frameBytes[$size] = $stream.ToArray()
            }
            finally {
                $stream.Dispose()
            }
        }
        else {
            # Small PNG-compressed ICO frames are not decoded reliably by all Windows
            # shell consumers (including Task Manager). Store them as classic DIBs.
            $frameBytes[$size] = ConvertTo-IcoDibFrame -Frame $Frames[$size]
        }
    }

    $temporaryPath = "$Path.new"
    $fileStream = [System.IO.FileStream]::new(
        $temporaryPath,
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write)
    $writer = [System.IO.BinaryWriter]::new($fileStream)

    try {
        $writer.Write([uint16] 0)
        $writer.Write([uint16] 1)
        $writer.Write([uint16] $sizes.Count)

        $offset = 6 + (16 * $sizes.Count)
        foreach ($size in $sizes) {
            $dimension = if ($size -eq 256) { [byte] 0 } else { [byte] $size }
            $bytes = $frameBytes[$size]
            $writer.Write($dimension)
            $writer.Write($dimension)
            $writer.Write([byte] 0)
            $writer.Write([byte] 0)
            $writer.Write([uint16] 1)
            $writer.Write([uint16] 32)
            $writer.Write([uint32] $bytes.Length)
            $writer.Write([uint32] $offset)
            $offset += $bytes.Length
        }

        foreach ($size in $sizes) {
            $writer.Write([byte[]] $frameBytes[$size])
        }
    }
    finally {
        $writer.Dispose()
        $fileStream.Dispose()
    }

    Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
}

if (-not (Test-Path -LiteralPath $markSourcePath)) {
    throw "Missing approved mark source: $markSourcePath"
}

if (-not (Test-Path -LiteralPath $wordmarkSourcePath)) {
    throw "Missing generated wordmark source: $wordmarkSourcePath"
}

$markCrop = Get-ResizedCrop `
    -Path $markSourcePath `
    -Crop ([System.Drawing.Rectangle]::new(130, 285, 1040, 655)) `
    -Width 1040 `
    -Height 655
$markKeyed = Convert-BlueKeyToMark -Source $markCrop
$markCrop.Dispose()
$mark = Get-AlphaCrop -Source $markKeyed
$markKeyed.Dispose()

$wordmarkCrop = Get-ResizedCrop `
    -Path $wordmarkSourcePath `
    -Crop ([System.Drawing.Rectangle]::new(60, 220, 2050, 270)) `
    -Width 1367 `
    -Height 180
$wordmarkKeyed = Convert-MagentaKeyToWordmark -Source $wordmarkCrop
$wordmarkCrop.Dispose()
$wordmark = Get-AlphaCrop -Source $wordmarkKeyed
$wordmarkKeyed.Dispose()

try {
    Save-PngAtomic -Image $mark -Path (Join-Path $productionRoot 'CX_A1_Mark_Master.png')
    Save-PngAtomic -Image $wordmark -Path (Join-Path $productionRoot 'CX_A1_Wordmark_Master.png')

    $logoOnly = Resize-Contained -Source $mark -Width 110 -Height 66 -Padding 1
    try {
        Save-PngAtomic -Image $logoOnly -Path (Join-Path $OutputRoot 'CapFrameXLogoOnly.png')
    }
    finally {
        $logoOnly.Dispose()
    }

    $screenWordmark = Resize-Contained -Source $wordmark -Width 302 -Height 30 -Padding 1
    try {
        Save-PngAtomic -Image $screenWordmark -Path (Join-Path $OutputRoot 'CX_Screen_Logo_Name.png')
    }
    finally {
        $screenWordmark.Dispose()
    }

    $windowIcon = New-TileIcon -Mark $mark -Size 70
    try {
        Save-PngAtomic -Image $windowIcon -Path (Join-Path $OutputRoot 'CX_Icon.png')
    }
    finally {
        $windowIcon.Dispose()
    }

    $iconFrames = @{}
    try {
        foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
            $iconFrames[$size] = New-TileIcon -Mark $mark -Size $size
        }

        Save-IcoAtomic -Frames $iconFrames -Path (Join-Path $OutputRoot 'cx_icon_BUC.ico')
        Save-PngAtomic -Image $iconFrames[256] -Path (Join-Path $productionRoot 'cx_icon_BUC_256_preview.png')
    }
    finally {
        foreach ($frame in $iconFrames.Values) {
            $frame.Dispose()
        }
    }

    $banner = [System.Drawing.Bitmap]::new(
        1500,
        500,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $bannerGraphics = New-HighQualityGraphics -Image $banner
    try {
        $bannerGraphics.Clear($blue)
        $markBounds = [System.Drawing.RectangleF]::new(540.0, 52.0, 420.0, 252.0)
        $null = Draw-ContainedImage -Graphics $bannerGraphics -Image $mark -Bounds $markBounds

        $wordmarkBounds = [System.Drawing.RectangleF]::new(395.0, 307.0, 710.0, 62.0)
        $null = Draw-ContainedImage -Graphics $bannerGraphics -Image $wordmark -Bounds $wordmarkBounds

        $tagline = 'FRAMETIMES CAPTURE AND ANALYSIS TOOL'
        $taglineFont = [System.Drawing.Font]::new(
            'Bahnschrift SemiBold',
            [single] 23,
            [System.Drawing.FontStyle]::Regular,
            [System.Drawing.GraphicsUnit]::Pixel)
        $taglineBrush = [System.Drawing.SolidBrush]::new($white)
        $taglineFormat = [System.Drawing.StringFormat]::new()
        $taglineFormat.Alignment = [System.Drawing.StringAlignment]::Center
        $taglineFormat.LineAlignment = [System.Drawing.StringAlignment]::Center

        try {
            $taglineBounds = [System.Drawing.RectangleF]::new(260.0, 382.0, 980.0, 42.0)
            $bannerGraphics.DrawString(
                $tagline,
                $taglineFont,
                $taglineBrush,
                $taglineBounds,
                $taglineFormat)
        }
        finally {
            $taglineFormat.Dispose()
            $taglineBrush.Dispose()
            $taglineFont.Dispose()
        }
    }
    finally {
        $bannerGraphics.Dispose()
    }

    try {
        Save-JpegAtomic -Image $banner -Path (Join-Path $OutputRoot 'X_Banner.jpg') -Quality 96
    }
    finally {
        $banner.Dispose()
    }

    $preview = [System.Drawing.Bitmap]::new(
        1100,
        650,
        [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $previewGraphics = New-HighQualityGraphics -Image $preview
    $previewTitleFont = [System.Drawing.Font]::new(
        'Segoe UI Semibold',
        [single] 18,
        [System.Drawing.FontStyle]::Regular,
        [System.Drawing.GraphicsUnit]::Pixel)
    $previewLabelFont = [System.Drawing.Font]::new(
        'Segoe UI',
        [single] 13,
        [System.Drawing.FontStyle]::Regular,
        [System.Drawing.GraphicsUnit]::Pixel)
    $previewTextBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(255, 35, 40, 48))
    $previewDarkBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(255, 30, 32, 36))
    $previewLightBrush = [System.Drawing.SolidBrush]::new(
        [System.Drawing.Color]::FromArgb(255, 250, 250, 250))

    try {
        $previewGraphics.Clear([System.Drawing.Color]::FromArgb(255, 235, 237, 241))
        $previewGraphics.DrawString(
            'CX A1 production assets - 16 / 24 / 32 / 48 / 70 px',
            $previewTitleFont,
            $previewTextBrush,
            [single] 24,
            [single] 14)
        $previewGraphics.FillRectangle($previewDarkBrush, 20, 48, 1060, 105)
        $previewGraphics.FillRectangle($previewLightBrush, 20, 173, 1060, 105)

        foreach ($rowY in @(66, 191)) {
            $x = 48
            foreach ($size in @(16, 24, 32, 48, 70)) {
                $smallIcon = New-TileIcon -Mark $mark -Size $size
                try {
                    $y = $rowY + [int] ((70 - $size) / 2)
                    $previewGraphics.DrawImage($smallIcon, $x, $y, $size, $size)
                }
                finally {
                    $smallIcon.Dispose()
                }

                $x += 92
            }

            $logoBounds = [System.Drawing.RectangleF]::new(520.0, [single] ($rowY + 2), 165.0, 66.0)
            $null = Draw-ContainedImage -Graphics $previewGraphics -Image $mark -Bounds $logoBounds
            $wordmarkBounds = [System.Drawing.RectangleF]::new(730.0, [single] ($rowY + 20), 320.0, 30.0)
            $null = Draw-ContainedImage -Graphics $previewGraphics -Image $wordmark -Bounds $wordmarkBounds
        }

        $previewGraphics.DrawString(
            'Icon sizes',
            $previewLabelFont,
            $previewTextBrush,
            [single] 48,
            [single] 286)
        $previewGraphics.DrawString(
            'Transparent signet and wordmark',
            $previewLabelFont,
            $previewTextBrush,
            [single] 520,
            [single] 286)

        $bannerPreview = [System.Drawing.Image]::FromFile((Join-Path $OutputRoot 'X_Banner.jpg'))
        try {
            $previewGraphics.DrawImage(
                $bannerPreview,
                [System.Drawing.Rectangle]::new(100, 326, 900, 300))
        }
        finally {
            $bannerPreview.Dispose()
        }
    }
    finally {
        $previewLightBrush.Dispose()
        $previewDarkBrush.Dispose()
        $previewTextBrush.Dispose()
        $previewLabelFont.Dispose()
        $previewTitleFont.Dispose()
        $previewGraphics.Dispose()
    }

    try {
        Save-PngAtomic `
            -Image $preview `
            -Path (Join-Path $productionRoot 'CX_A1_Production_Preview.png')
    }
    finally {
        $preview.Dispose()
    }
}
finally {
    $wordmark.Dispose()
    $mark.Dispose()
}

Get-Item -LiteralPath @(
    (Join-Path $OutputRoot 'CX_Icon.png'),
    (Join-Path $OutputRoot 'CapFrameXLogoOnly.png'),
    (Join-Path $OutputRoot 'cx_icon_BUC.ico'),
    (Join-Path $OutputRoot 'CX_Screen_Logo_Name.png'),
    (Join-Path $OutputRoot 'X_Banner.jpg')) |
    Select-Object Name, Length
