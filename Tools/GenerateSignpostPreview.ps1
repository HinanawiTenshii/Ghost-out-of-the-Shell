# Static geometry preview, not a Unity/CRT render.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$prefab = Get-Content -Raw (Join-Path $root 'Assets/Prefabs/Decorations/GeometricSignpost.prefab')
function Scalar([string]$name) {
    [float]::Parse([regex]::Match($prefab, ($name + ': ([\d.]+)')).Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture)
}
function Tint([string]$name) {
    $m = [regex]::Match($prefab, ($name + ': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}'))
    $c = @(1..3 | ForEach-Object { [float]::Parse($m.Groups[$_].Value, [Globalization.CultureInfo]::InvariantCulture) })
    [Drawing.Color]::FromArgb(255, [int]($c[0]*255), [int]($c[1]*255), [int]($c[2]*255))
}
$poleW = Scalar 'poleWidth'; $poleH = Scalar 'poleHeight'
$boardW = Scalar 'boardWidth'; $boardH = Scalar 'boardHeight'; $gap = Scalar 'boardGap'
$tip = $boardW * (Scalar 'arrowTipRatio')
$exposed = $poleH - 3*$boardH - 2*$gap
if ($exposed -lt $poleH/2) { throw 'Pole must dominate the silhouette.' }
$bmp = [Drawing.Bitmap]::new(400,620)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.Color]::FromArgb(12,31,48))
$scale = 160; $baseY = 575
$brush = [Drawing.SolidBrush]::new((Tint 'poleColor'))
$g.FillRectangle($brush,[float](200-$poleW*$scale/2),[float]($baseY-$poleH*$scale),[float]($poleW*$scale),[float]($poleH*$scale))
$brush.Dispose()
$brush = [Drawing.SolidBrush]::new((Tint 'boardColor'))
for ($i=0; $i -lt 3; $i++) {
    $top=$poleH-$i*($boardH+$gap); $bottom=$top-$boardH
    $left=-$boardW/2; $right=$boardW/2
    $pointsLeft = (($i%2 -eq 0) -eq ((Scalar 'topPointsLeft') -eq 1))
    if ($pointsLeft) {
        $points = @(@(($left+$tip),$bottom),@($right,$bottom),@($right,$top),@(($left+$tip),$top),@($left,(($top+$bottom)/2)))
    } else {
        $points = @(@($left,$bottom),@(($right-$tip),$bottom),@($right,(($top+$bottom)/2)),@(($right-$tip),$top),@($left,$top))
    }
    for ($j=0; $j -lt 5; $j++) {
        $a=$points[$j]; $b=$points[($j+1)%5]; $c=$points[($j+2)%5]
        if ((($b[0]-$a[0])*($c[1]-$b[1])-($b[1]-$a[1])*($c[0]-$b[0])) -le 0) { throw 'Non-convex arrow.' }
    }
    $screen = [Drawing.PointF[]]@($points | ForEach-Object { [Drawing.PointF]::new((200+$_[0]*$scale),($baseY-$_[1]*$scale)) })
    $g.FillPolygon($brush,$screen)
}
$brush.Dispose()
$path = Join-Path $root 'Docs/GeometricSignpost-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
Write-Output "PASS: convex arrow boards; exposed pole $exposed / $poleH. Preview: $path"
