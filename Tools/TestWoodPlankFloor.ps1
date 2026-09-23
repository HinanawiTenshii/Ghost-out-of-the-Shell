$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$shader = Get-Content -Raw "$root/Assets/Shaders/WoodPlankFloor.shader"
$mat = Get-Content -Raw "$root/Assets/Materials/WoodPlankFloor.mat"
$checks = 0
function Check($ok, $label) { if (!$ok) { throw $label }; $script:checks++ }
$shaderMeta = Get-Content -Raw "$root/Assets/Shaders/WoodPlankFloor.shader.meta"
$guid = [regex]::Match($shaderMeta, 'guid: (\w+)').Groups[1].Value
Check ($mat.Contains("guid: $guid")) 'Material shader reference'
Check ($shader.Contains('[Enum(Horizontal,0,Vertical,1)]')) 'Inspector orientation'
Check ($shader.Contains('if (direction > 0.5) p = p.yx;')) 'Direction axis swap'
Check ($shader.Contains('_DirectionOverride >= 0.0 ? _DirectionOverride : _Direction')) 'Per-object override with material fallback'
Check ($shader.Contains('_DirectionOverride ("Per Object Direction", Float) = -1')) 'Existing material keeps its default direction'
Check ($shader.Contains('unity_ObjectToWorld')) 'World-space scale'
Check ($shader.Contains('float2(0.01, 0.01)')) 'Safe minimum board dimensions'
Check ($shader.Contains('result.a *= tex2D(_MainTex, input.uv).a;')) 'Sprite alpha preserved'
Check ($shader.Contains('input.color * _Color')) 'Renderer tint preserved'
Check ($shader.Contains('fwidth(grid)') -and $shader.Contains('smoothstep(0.35, 0.9,')) 'Distant pattern filtering'
Check ($shader -notmatch '_Time|sin\(|cos\(') 'Static pattern without animation or grain'
Check ([regex]::Matches($shader, 'tex2D\(').Count -eq 1) 'Only sprite alpha texture sample'
$palette = @()
foreach ($property in @('_WoodColor', '_DarkWood', '_LightWood')) {
    $m = [regex]::Match($mat, "- ${property}: \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: 1\}")
    Check ($m.Success) 'Opaque palette'
    $rgb = @(1..3 | ForEach-Object { [double]$m.Groups[$_].Value })
    Check ($rgb[2] -gt $rgb[1] -and $rgb[1] -gt $rgb[0]) 'Deep blue palette matching existing floors'
    Check ($shader.Contains("($($m.Groups[1].Value), $($m.Groups[2].Value), $($m.Groups[3].Value), 1)")) 'Shader/material palette agreement'
    $palette += ,$rgb
}
$tiles = Get-Content -Raw "$root/Assets/Materials/Level2CorridorTiles.mat"
foreach ($entry in @(@('_DarkTile',1), @('_LightTile',2))) {
    $m = [regex]::Match($tiles, "- $($entry[0]): \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: 1\}")
    Check ($m.Success) 'Existing tile palette found'
    foreach ($channel in 0..2) {
        Check ([Math]::Abs($palette[$entry[1]][$channel] - [double]$m.Groups[$channel+1].Value) -lt .000001) 'Board endpoint matches existing tile color'
    }
}
foreach ($channel in 0..2) {
    Check ([Math]::Abs($palette[0][$channel] - ($palette[1][$channel]+$palette[2][$channel])*.5) -lt .000001) 'Midtone blends tile endpoints evenly'
}
Check ($mat -match '(?m)^\s*- _Direction: [01]\s*$') 'Material default may be either user-selected orientation'
foreach ($entry in @(@('_PlankLength','1.84'), @('_PlankWidth','0.46'))) {
    Check ($mat.Contains("- $($entry[0]): $($entry[1])")) 'Material dimensions and direction'
    Check ($shader -match "$($entry[0]).*= $($entry[1])\b") 'Matching shader defaults'
}
. "$PSScriptRoot/TestSpriteWaterFlowShader.ps1"
$program = [regex]::Match($shader, '(?s)CGPROGRAM(.*?)ENDCG').Groups[1].Value
$program = [regex]::Replace($program, '(?m)^\s*#pragma.*$', '')
$program = $program.Replace('#include "UnityCG.cginc"', 'float4x4 unity_ObjectToWorld; float4 UnityObjectToClipPos(float4 v) { return v; }').Replace('fixed4', 'float4')
[SpriteWaterFlowCompileTest]::Check($program, 'vert', 'vs_4_0')
[SpriteWaterFlowCompileTest]::Check($program, 'frag', 'ps_4_0')
# Numerical reference of shader board indexing, used for layout tests and preview.
function BoardIndex([double]$x, [double]$y, [int]$direction = 0) {
    if ($direction -eq 1) { $x, $y = $y, $x }
    $row = [Math]::Floor($y / .46)
    $offset = $row * .5 - [Math]::Floor($row * .5)
    $column = [Math]::Floor($x / 1.84 + $offset)
    $index = $column + $offset * 2
    return [int]($index - 3 * [Math]::Floor($index / 3))
}
foreach ($x in @(-8.41, -1.27, -.1, .24, 5.31)) {
    foreach ($y in @(-2.31, -.72, .13, .8, 1.49)) {
        $i = BoardIndex $x $y
        Check ($i -ge 0 -and $i -le 2) 'Negative world coordinates valid'
        Check ($i -eq (BoardIndex $y $x 1)) 'Horizontal/vertical equivalence'
        Check ($i -eq (BoardIndex ($x + 5.52) $y)) 'Three-board repeat'
        Check ($i -eq (BoardIndex $x ($y + 2.76))) 'Six-row repeat'
    }
}
Check ((BoardIndex .8 .6) -ne (BoardIndex 1.0 .6)) 'Odd row joint at half length'
Check ((BoardIndex .8 .2) -eq (BoardIndex 1.0 .2)) 'Even row has no half-length joint'
foreach ($row in -5..5) {
    foreach ($x in @(-3.12, -.2, .4, 1.4, 2.0, 3.5)) {
        Check ((BoardIndex $x (($row+.5)*.46)) -ne (BoardIndex $x (($row+1.5)*.46))) 'Adjacent boards remain visually distinct without seams'
    }
}

Add-Type -AssemblyName System.Drawing
$bmp = [Drawing.Bitmap]::new(1000, 620)
$g = [Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25, 32, 43))
$font = [Drawing.Font]::new('Segoe UI', 14)
$colors = @($palette | ForEach-Object { [Drawing.Color]::FromArgb([int]($_[0]*255), [int]($_[1]*255), [int]($_[2]*255)) })
$g.DrawString('Wood plank floor / minimal, staggered boards', $font, [Drawing.Brushes]::White, 26, 16)
# Two orientations, negative coordinates, same scale; no Unity/CRT post-processing.
$g.Flush()
foreach ($direction in 0..1) {
    for ($py = 0; $py -lt 300; $py++) {
        for ($px = 0; $px -lt 460; $px++) {
            $index = BoardIndex (($px+.5)/75 - 2.2) ((300-$py-.5)/75 - .8) $direction
            $bmp.SetPixel(26 + 488*$direction + $px, 58 + $py, $colors[$index])
        }
    }
    $g.DrawString(@('Horizontal', 'Vertical')[$direction], $font, [Drawing.Brushes]::White, 26+488*$direction, 371)
}
# Current checker floor alongside wood, with simplified existing furniture colors.
for ($y = 0; $y -lt 160; $y += 40) {
    for ($x = 0; $x -lt 460; $x += 40) {
        $c = if ((($x+$y)/40)%2 -eq 0) { [Drawing.Color]::FromArgb(27,31,60) } else { [Drawing.Color]::FromArgb(52,56,79) }
        $brush = [Drawing.SolidBrush]::new($c)
        $g.FillRectangle($brush, 26+$x, 420+$y, [Math]::Min(40,460-$x), 40)
        $brush.Dispose()
    }
}
$g.Flush()
for ($py = 0; $py -lt 160; $py++) {
    for ($px = 0; $px -lt 460; $px++) {
        $bmp.SetPixel(514+$px,420+$py,$colors[(BoardIndex (($px+.5)/75) (($py+.5)/75))])
    }
}
foreach ($left in @(130,620)) {
    $frame = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(133,71,36))
    $edge = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(97,51,28))
    $surface = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(56,145,143))
    $g.FillRectangle($edge,$left,455,205,85)
    $g.FillRectangle($frame,$left+5,460,195,75)
    $g.FillRectangle($surface,$left+13,468,179,59)
    $frame.Dispose(); $edge.Dispose(); $surface.Dispose()
}
$bmp.Save("$root/Docs/WoodPlankFloor-preview.png", [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $font.Dispose(); $bmp.Dispose()
Write-Output "PASS: $checks material/layout checks; vertex/fragment Direct3D compilation; preview generated."
