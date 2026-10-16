param(
    [string]$Source = 'C:/Users/이상현/Downloads/해달왕국_해안맵_원본색감_4097x3984.png',
    [string]$ProjectRoot = (Resolve-Path "$PSScriptRoot/../..").Path
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$archive = Join-Path $ProjectRoot 'ArtSource/Plaza/Pastel_01'
$output = Join-Path $ProjectRoot 'Assets/Art/Plaza/Pastel_01'
New-Item -ItemType Directory -Force -Path $archive,$output | Out-Null
$original = Join-Path $archive 'Plaza_Original_00_4097x3984.png'
if (!(Test-Path -LiteralPath $original)) { Copy-Item -LiteralPath $Source -Destination $original }
$inputImage = [System.Drawing.Bitmap]::new($original)
if ($inputImage.Width -ne 4097 -or $inputImage.Height -ne 3984) { throw 'Expected original 4097x3984 image.' }
# Pixel-local color transform only: 90% saturation, 8% warm ivory lift.
# No blur, sharpening, generation or resampling of the archived master.
$saturation = 0.90
$retain = 0.92
$luma = @(0.2126,0.7152,0.0722)
$matrix = [System.Drawing.Imaging.ColorMatrix]::new()
for ($i=0; $i -lt 3; $i++) {
    for ($j=0; $j -lt 3; $j++) {
        $value = (1-$saturation)*$luma[$i]
        if ($i -eq $j) { $value += $saturation }
        $matrix."Matrix$i$j" = [single]($retain*$value)
    }
}
$matrix.Matrix40 = 0.08
$matrix.Matrix41 = 0.0784
$matrix.Matrix42 = 0.0752
$attributes = [System.Drawing.Imaging.ImageAttributes]::new()
$attributes.SetColorMatrix($matrix)
$master = [System.Drawing.Bitmap]::new(4097,3984,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($master)
$g.DrawImage($inputImage,[System.Drawing.Rectangle]::new(0,0,4097,3984),0,0,4097,3984,[System.Drawing.GraphicsUnit]::Pixel,$attributes)
$g.Dispose()
$attributes.Dispose()
$inputImage.Dispose()
$master.Save((Join-Path $archive 'Plaza_Pastel_01_4097x3984.png'),[System.Drawing.Imaging.ImageFormat]::Png)
# Existing game coordinates use a 4072x3984 reference. Only runtime width
# is reduced by 25px; no enlargement. Four tiles retain native PPU 40.
$runtime = [System.Drawing.Bitmap]::new(4072,3984,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($runtime)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$edges = [System.Drawing.Imaging.ImageAttributes]::new()
$edges.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
$g.DrawImage($master,[System.Drawing.Rectangle]::new(0,0,4072,3984),0,0,4097,3984,[System.Drawing.GraphicsUnit]::Pixel,$edges)
$g.Dispose()
$edges.Dispose()
$master.Dispose()
$runtime.Save((Join-Path $output 'Plaza_Pastel_01_4072.png'),[System.Drawing.Imaging.ImageFormat]::Png)
foreach ($tile in @(@('TL',0,0),@('TR',2036,0),@('BL',0,1992),@('BR',2036,1992))) {
    $part = $runtime.Clone([System.Drawing.Rectangle]::new([int]$tile[1],[int]$tile[2],2036,1992),[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $part.Save((Join-Path $output "$($tile[0])_Placement.png"),[System.Drawing.Imaging.ImageFormat]::Png)
    $part.Dispose()
}
$preview = $runtime.Clone([System.Drawing.Rectangle]::new(3270,2470,800,1000),[System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$preview.Save((Join-Path $archive 'Start_Preview_01.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$preview.Dispose()
$runtime.Dispose()
