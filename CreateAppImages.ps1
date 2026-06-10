# Create Images directory
$imagesDir = "Platforms\Windows\Images"
New-Item -ItemType Directory -Force -Path $imagesDir | Out-Null

# Function to create a simple PNG image
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
    
    # Parse color
    $color = [System.Drawing.ColorTranslator]::FromHtml($BackgroundColor)
    
    # Fill background
    $brush = New-Object System.Drawing.SolidBrush($color)
    $graphics.FillRectangle($brush, 0, 0, $Width, $Height)
    $brush.Dispose()
    
    # Add simple text
    $font = New-Object System.Drawing.Font("Arial", [math]::Max(12, $Width / 8), [System.Drawing.FontStyle]::Bold)
    $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $text = "ST"
    $textSize = $graphics.MeasureString($text, $font)
    $x = ($Width - $textSize.Width) / 2
    $y = ($Height - $textSize.Height) / 2
    $graphics.DrawString($text, $font, $textBrush, $x, $y)
    
    # Save
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    
    # Cleanup
    $font.Dispose()
    $textBrush.Dispose()
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
