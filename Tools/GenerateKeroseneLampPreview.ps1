# Geometry preview from the actual polygon calls, not a Unity/CRT render.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw -LiteralPath (Join-Path $root 'Assets/Scripts/Rendering/KeroseneStreetLamp.cs')
$prefab = Get-Content -Raw -LiteralPath (Join-Path $root 'Assets/Prefabs/Decorations/KeroseneStreetLamp.prefab')
$culture = [Globalization.CultureInfo]::InvariantCulture
function Number([string]$s) { [float]::Parse($s.Trim().TrimEnd('f'), $culture) }
function Tint([string]$name) {
    $m = [regex]::Match($prefab, ($name + ': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}'))
    if (!$m.Success) { throw "Missing tint: $name" }
    [Drawing.Color]::FromArgb([int](255*(Number $m.Groups[4].Value)), [int](255*(Number $m.Groups[1].Value)), [int](255*(Number $m.Groups[2].Value)), [int](255*(Number $m.Groups[3].Value)))
}
$bmp = [Drawing.Bitmap]::new(520,650)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(12,31,48))
$scale = 155
$baseY = 608
foreach ($light in @(@(0.9,'outerLightColor'), @(0.48,'innerLightColor'))) {
    $radius = [float]$light[0] * $scale
    $brush = [Drawing.SolidBrush]::new((Tint $light[1]))
    $g.FillEllipse($brush, [float](260-$radius), [float]($baseY-2.77*$scale-$radius), [float](2*$radius), [float](2*$radius))
    $brush.Dispose()
}
$pattern = 'AddShape\(vertices, colors, triangles, ([^;]+)\);'
$shapes = [regex]::Matches($source, $pattern)
if ($shapes.Count -ne 5) { throw "Expected 5 minimal body shapes, got $($shapes.Count)" }
foreach ($match in $shapes) {
    $values = $match.Groups[1].Value -split ', ', 6
    $bottom=Number $values[0]; $top=Number $values[1]
    $bw=Number $values[2]; $tw=Number $values[3]; $x=Number $values[4]
    if ($top -le $bottom -or $bw -le 0 -or $tw -lt 0) { throw 'Invalid shape dimensions' }
    $points = [Drawing.PointF[]]@(
        [Drawing.PointF]::new((260+($x-$bw/2)*$scale),($baseY-$bottom*$scale)),
        [Drawing.PointF]::new((260+($x+$bw/2)*$scale),($baseY-$bottom*$scale)),
        [Drawing.PointF]::new((260+($x+$tw/2)*$scale),($baseY-$top*$scale)),
        [Drawing.PointF]::new((260+($x-$tw/2)*$scale),($baseY-$top*$scale))
    )
    $color = if ($values[5].StartsWith('new Color')) { [Drawing.Color]::FromArgb(255,242,183) } else { Tint $values[5] }
    $brush = [Drawing.SolidBrush]::new($color)
    $g.FillPolygon($brush,$points)
    $brush.Dispose()
}
$path = Join-Path $root 'Docs/KeroseneStreetLamp-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "PASS: 5 minimal geometric shapes; preview saved to $path"
