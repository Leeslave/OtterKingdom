param([string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$art = Join-Path $ProjectRoot 'ArtSource/Plaza/ThinLine'
$out = Join-Path $ProjectRoot 'Assets/Art/Plaza/ThinLineMap'
New-Item -ItemType Directory -Force -Path $out | Out-Null
$source = [System.Drawing.Bitmap]::new((Join-Path $art 'approved-map.png'))
function Save-Resized([int]$width, [int]$height, [string]$path) {
    $bitmap = [System.Drawing.Bitmap]::new($width, $height)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
    $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
    $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(0,0,$width,$height), 0,0,$source.Width,$source.Height,[System.Drawing.GraphicsUnit]::Pixel,$attributes)
    $bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $attributes.Dispose()
    $graphics.Dispose()
    return $bitmap
}
# Requested delivery size. This is resampling, NOT native 4K generation.
$delivery = Save-Resized 4097 3984 (Join-Path $art 'Plaza_ThinLine_4097x3984.png')
$delivery.Dispose()
# Keep the existing map's world dimensions and sprite PPU unchanged.
$base = Save-Resized 4072 3984 (Join-Path $out 'Plaza_ThinLine_4072.png')
$base.Dispose()
$full = Save-Resized 8144 7968 (Join-Path $art 'Plaza_ThinLine_Runtime_8144.png')
foreach ($tile in @(@('TL',0,0),@('TR',4072,0),@('BL',0,3984),@('BR',4072,3984))) {
    $part = $full.Clone([System.Drawing.Rectangle]::new([int]$tile[1],[int]$tile[2],4072,3984),[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $part.Save((Join-Path $out "$($tile[0])_Placement.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $part.Dispose()
}
$full.Dispose()
$source.Dispose()
