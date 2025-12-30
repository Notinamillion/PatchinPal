# Convert PNG logo to ICO format for system tray
Add-Type -AssemblyName System.Drawing

$pngPath = "C:\Users\s.bateman\Programs\PatchinPal2\logo.png"
$icoPath = "C:\Users\s.bateman\Programs\PatchinPal2\PatchinPal.Client\logo.ico"

# Load the PNG image
$img = [System.Drawing.Image]::FromFile($pngPath)

# Create multiple sizes for the icon (Windows needs 16x16, 32x32, 48x48, 256x256)
$sizes = @(16, 32, 48, 256)
$icon = New-Object System.Drawing.Icon($img.GetThumbnailImage(256, 256, $null, [IntPtr]::Zero).GetHbitmap())

# For proper ICO creation, we'll use a simpler approach - just resize to 32x32 for tray
$bitmap = New-Object System.Drawing.Bitmap(32, 32)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.DrawImage($img, 0, 0, 32, 32)
$graphics.Dispose()

# Save as icon
$ms = New-Object System.IO.MemoryStream
$bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$ms.Position = 0

# Create ICO file manually
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICO header
$bw.Write([uint16]0)        # Reserved
$bw.Write([uint16]1)        # Type: 1 = ICO
$bw.Write([uint16]1)        # Number of images

# Image directory entry
$bw.Write([byte]32)         # Width
$bw.Write([byte]32)         # Height
$bw.Write([byte]0)          # Color palette
$bw.Write([byte]0)          # Reserved
$bw.Write([uint16]1)        # Color planes
$bw.Write([uint16]32)       # Bits per pixel
$bw.Write([uint32]$ms.Length) # Image size
$bw.Write([uint32]22)       # Image offset

# Write PNG data
$ms.Position = 0
$ms.CopyTo($fs)

$bw.Close()
$fs.Close()
$ms.Dispose()
$bitmap.Dispose()
$img.Dispose()

Write-Host "Logo converted successfully to: $icoPath"
