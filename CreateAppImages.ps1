# Create Images directory
$imagesDir = "Platforms\Windows\Images"
New-Item -ItemType Directory -Force -Path $imagesDir | Out-Null

# Create Windows package images that match Resources/AppIcon/appicon*.svg.
function Create-PNG {
    param(
        [string]$Path,
        [int]$Width,
        [int]$Height,
        [string]$BackgroundColor = "#512BD4"
    )
    
    Add-Type -AssemblyName System.Drawing
    
    $bmp = New-Object System.Drawing.Bitmap($Width, $Height)
    $graphics = [System.Drawing.Graphics]::FromImage($bmp)
    
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $size = [math]::Min($Width, $Height)
    $offsetX = ($Width - $size) / 2
    $offsetY = ($Height - $size) / 2
    $scale = $size / 456.0

    $backgroundRect = New-Object System.Drawing.RectangleF(0, 0, $Width, $Height)
    $gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $backgroundRect,
        [System.Drawing.ColorTranslator]::FromHtml("#4F46E5"),
        [System.Drawing.ColorTranslator]::FromHtml("#7C3AED"),
        45
    )
    $graphics.FillRectangle($gradient, $backgroundRect)
    $gradient.Dispose()

    function Fill-RoundedRectangle($Graphics, $Brush, $X, $Y, $W, $H, $Radius) {
        $path = New-Object System.Drawing.Drawing2D.GraphicsPath
        $diameter = $Radius * 2
        $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
        $path.AddArc($X + $W - $diameter, $Y, $diameter, $diameter, 270, 90)
        $path.AddArc($X + $W - $diameter, $Y + $H - $diameter, $diameter, $diameter, 0, 90)
        $path.AddArc($X, $Y + $H - $diameter, $diameter, $diameter, 90, 90)
        $path.CloseFigure()
        $Graphics.FillPath($Brush, $path)
        $path.Dispose()
    }

    function Convert-X($Value) { return [single]($offsetX + ($Value * $scale)) }
    function Convert-Y($Value) { return [single]($offsetY + ($Value * $scale)) }
    function Convert-Size($Value) { return [single]($Value * $scale) }

    $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $screen = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#1e1b4b"))
    Fill-RoundedRectangle $graphics $white (Convert-X 68) (Convert-Y 88) (Convert-Size 320) (Convert-Size 220) (Convert-Size 16)
    Fill-RoundedRectangle $graphics $screen (Convert-X 88) (Convert-Y 110) (Convert-Size 280) (Convert-Size 170) (Convert-Size 6)

    $bars = @(
        @(108,220,30,45,"#4ade80"), @(148,195,30,70,"#22d3ee"),
        @(188,210,30,55,"#a78bfa"), @(228,180,30,85,"#f472b6"),
        @(268,230,30,35,"#fbbf24"), @(308,205,30,60,"#34d399")
    )
    foreach ($bar in $bars) {
        $barBrush = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($bar[4]))
        Fill-RoundedRectangle $graphics $barBrush (Convert-X $bar[0]) (Convert-Y $bar[1]) (Convert-Size $bar[2]) (Convert-Size $bar[3]) (Convert-Size 3)
        $barBrush.Dispose()
    }

    Fill-RoundedRectangle $graphics $white (Convert-X 198) (Convert-Y 308) (Convert-Size 60) (Convert-Size 8) (Convert-Size 4)
    Fill-RoundedRectangle $graphics $white (Convert-X 180) (Convert-Y 316) (Convert-Size 96) (Convert-Size 12) (Convert-Size 6)
    $red = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml("#ef4444"))
    $graphics.FillEllipse($red, (Convert-X 340), (Convert-Y 122), (Convert-Size 16), (Convert-Size 16))
    
    # Save
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    
    # Cleanup
    $white.Dispose()
    $screen.Dispose()
    $red.Dispose()
    $graphics.Dispose()
    $bmp.Dispose()
    
    Write-Host "Created: $Path ($Width x $Height)"
}

# Create all required images
Create-PNG "$imagesDir\Square44x44Logo.png" 44 44
Create-PNG "$imagesDir\Square71x71Logo.png" 71 71
Create-PNG "$imagesDir\Square150x150Logo.png" 150 150
Create-PNG "$imagesDir\Wide310x150Logo.png" 310 150
Create-PNG "$imagesDir\Square310x310Logo.png" 310 310
Create-PNG "$imagesDir\StoreLogo.png" 50 50
Create-PNG "$imagesDir\SplashScreen.png" 620 300

Write-Host "`nAll images created successfully!"
