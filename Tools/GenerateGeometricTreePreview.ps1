# Static geometry check/preview using the source polygons; not a Unity render.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Decorations/GeometricTreeDecoration.cs')
$prefab = Get-Content -Raw (Join-Path $root 'Assets/Prefabs/Decorations/GeometricTree.prefab')
$culture = [Globalization.CultureInfo]::InvariantCulture
function Num([string]$s) { [float]::Parse($s.TrimEnd('f'), $culture) }
function Scalar([string]$name) { Num ([regex]::Match($prefab, ($name + ': ([\d.]+)')).Groups[1].Value) }
function Pair([string]$name) {
    $m = [regex]::Match($prefab, ($name + ': \{x: ([\d.-]+), y: ([\d.-]+)\}'))
    @((Num $m.Groups[1].Value), (Num $m.Groups[2].Value))
}
function Tint([string]$name) {
    $m = [regex]::Match($prefab, ($name + ': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}'))
    [Drawing.Color]::FromArgb(255,[int](255*(Num $m.Groups[1].Value)),[int](255*(Num $m.Groups[2].Value)),[int](255*(Num $m.Groups[3].Value)))
}
$bmp = [Drawing.Bitmap]::new(500,620)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(12,31,48))
$scale = 160
$baseY = 575
$width = Scalar 'trunkWidth'; $height = Scalar 'trunkHeight'
$brush = [Drawing.SolidBrush]::new((Tint 'trunkColor'))
$g.FillRectangle($brush,[float](250-$width*$scale/2),[float]($baseY-$height*$scale),[float]($width*$scale),[float]($height*$scale))
$brush.Dispose()
$center = Pair 'crownCenter'; $size = Pair 'crownSize'
foreach ($spec in @(@('CrownOutline','crownColor',8),@('UpperOutline','upperCrownColor',6),@('LeftLeafFacet','leafAccentColor',4),@('RightLeafFacet','leafAccentColor',4))) {
    $block = [regex]::Match($source, ($spec[0] + '\s*=\s*\{([^}]+)\}')).Groups[1].Value
    $matches = [regex]::Matches($block, 'new Vector2\(([-\d.]+)f, ([-\d.]+)f\)')
    if ($matches.Count -ne $spec[2]) { throw "Unexpected vertex count: $($spec[0])" }
    $localPoints = @($matches | ForEach-Object { ,@((Num $_.Groups[1].Value),(Num $_.Groups[2].Value)) })
    for ($i=0; $i -lt $localPoints.Count; $i++) {
        $a=$localPoints[$i]; $b=$localPoints[($i+1)%$localPoints.Count]; $c=$localPoints[($i+2)%$localPoints.Count]
        $cross=($b[0]-$a[0])*($c[1]-$b[1])-($b[1]-$a[1])*($c[0]-$b[0])
        if ($cross -le 0) { throw "Polygon not convex/CCW: $($spec[0])" }
    }
    $points = [Drawing.PointF[]]@($localPoints | ForEach-Object {
        [Drawing.PointF]::new((250+($center[0]+$_[0]*$size[0])*$scale),($baseY-($center[1]+$_[1]*$size[1])*$scale))
    })
    $brush=[Drawing.SolidBrush]::new((Tint $spec[1])); $g.FillPolygon($brush,$points); $brush.Dispose()
}
$path=Join-Path $root 'Docs/GeometricTree-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "PASS: convex source polygons and five-shape tree preview: $path"
