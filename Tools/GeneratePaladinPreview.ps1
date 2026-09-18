# Preview the exact runtime pixel maps, without Unity lighting/CRT.
param([string]$PrefabPath, [ValidateRange(0,3)][int]$Pose = 0)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$source = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PaladinZeldaCharacterData.cs')
$base = Get-Content -Raw (Join-Path $PSScriptRoot 'TestPeopleVisuals.ps1')
$stubs = [regex]::Match($base,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$stubs = $stubs.Replace('void Apply(bool mip, bool discard)', 'void Apply(bool mip = true, bool discard = false)')
$fields = ([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;') | ForEach-Object {$_.Value}) -join [Environment]::NewLine
$mapsStart = $source.IndexOf('    private static readonly string[] Front')
$mapsEnd = $source.IndexOf('    private readonly Sprite[,] frames')
$methodsStart = $source.IndexOf('    private Sprite CreateSprite')
$methodsEnd = $source.IndexOf('    protected override void OnValidate')
$production = $source.Substring($mapsStart,$mapsEnd-$mapsStart) + $source.Substring($methodsStart,$methodsEnd-$methodsStart)
$utility = Get-Content -Raw (Join-Path $root 'Assets/Scripts/Characters/PixelWalkFrameUtility.cs')
$code = 'using UnityEngine;' + $stubs + @"
public class PaladinVisualHarness : PeopleVisualBase {
$fields
$production
public Color[] Render(int direction,int pose) { return CreateSprite(direction,pose).texture.pixels; }
}
"@ + $utility.Replace('using UnityEngine;','')
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$character = New-Object PaladinVisualHarness
$prefab = if ($PrefabPath) { Get-Content -Raw (Join-Path $root $PrefabPath) } else { $null }
$palette = @{}
foreach ($entry in @(@('A','armorColor'),@('S','armorShadow'),@('H','armorHighlight'),@('C','capeColor'),@('D','capeShadow'),@('V','visorColor'))) {
    if ($prefab) {
        $match = [regex]::Match($prefab, $entry[1]+': \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')
        if (-not $match.Success) { throw "Missing prefab color: $($entry[1])" }
        $rgb = @(1..4 | ForEach-Object { [double]::Parse($match.Groups[$_].Value,[Globalization.CultureInfo]::InvariantCulture) })
    } else {
        $match = [regex]::Match($source, $entry[1]+' = new Color\(([^)]+)\)')
        $rgb = @($match.Groups[1].Value.Split(',') | ForEach-Object { [double]::Parse($_.Trim().TrimEnd('f'),[Globalization.CultureInfo]::InvariantCulture) })
    }
    $palette[$entry[0]] = [Drawing.Color]::FromArgb(255,[int]($rgb[0]*255),[int]($rgb[1]*255),[int]($rgb[2]*255))
    $character.SetColor($entry[1],$rgb[0],$rgb[1],$rgb[2])
}
$bmp = [Drawing.Bitmap]::new(800,300)
$graphics = [Drawing.Graphics]::FromImage($bmp)
$graphics.Clear([Drawing.Color]::FromArgb(25,32,43))
$font = [Drawing.Font]::new('Arial',12)
$directions = @('Front','Back','Left','Right')
for ($i=0; $i -lt 4; $i++) {
    $match = [regex]::Match($source, '(?s)string\[\] '+$directions[$i]+' = \{(.*?)\};')
    $rows = @([regex]::Matches($match.Groups[1].Value,'"([.ASHCDOV]+)"') | ForEach-Object { $_.Groups[1].Value })
    if ($rows.Count -ne 16) { throw "Invalid row count: $($directions[$i])" }
    if ($i -eq 2 -or $i -eq 3) {
        foreach ($row in 1..5) {
            if ($i -eq 2 -and ($rows[$row].Substring(0,4) -ne '....' -or $rows[$row][4] -eq '.')) { throw 'Left helmet must have a flush front edge.' }
            if ($i -eq 3 -and ($rows[$row].Substring(12,4) -ne '....' -or $rows[$row][11] -eq '.')) { throw 'Right helmet must have a flush front edge.' }
        }
    }
    if ($i -eq 0) {
        if ($rows[3].Substring(6,4) -ne 'VVVV' -or $rows[4].Substring(7,2) -ne 'VV' -or $rows[5].Substring(7,2) -ne 'VV') { throw 'Front visor must form a T.' }
        if ($rows[6].Substring(6,5) -ne 'SSSSS') { throw 'Missing neck/armor separation.' }
    }
    # Exercise all four runtime poses, not a separate redraw of the idle maps.
    foreach ($checkPose in 0..3) {
        $checkPixels = $character.Render($i,$checkPose)
        [PeopleVisualBase]::CheckConnected($checkPixels)
        foreach ($row in 0..15) {foreach ($x in 0..15) {
            if ($rows[$row][$x] -notin @('C','D')) {continue}
            $c = $checkPixels[(15-$row)*16+$x]
            $expected = $palette[[string]$rows[$row][$x]]
            if ([Math]::Abs($c.r*255-$expected.R) -gt 1 -or [Math]::Abs($c.g*255-$expected.G) -gt 1 -or [Math]::Abs($c.b*255-$expected.B) -gt 1) {throw "Hand over cape: $i / $checkPose / $x,$row"}
        }}
    }
    # The old resting hand must revert to the body/air underneath during attack.
    $probeX = @(11,4,6,8)[$i]; $probeRow = @(9,9,11,9)[$i]
    $attackPixels = $character.Render($i,3)
    $c = $attackPixels[(15-$probeRow)*16+$probeX]
    $under = [string]$rows[$probeRow][$probeX]
    if ($under -eq '.') {
        if ($c.a -ne 0) {throw 'Resting hand remains during attack'}
    } else {
        $expected = $palette[$under]
        if ([Math]::Abs($c.r*255-$expected.R) -gt 1 -or [Math]::Abs($c.g*255-$expected.G) -gt 1 -or [Math]::Abs($c.b*255-$expected.B) -gt 1) {throw 'Resting hand remains during attack'}
    }
    $pixels = $character.Render($i,$Pose)
    for ($y=0; $y -lt 16; $y++) {
        if ($rows[$y].Length -ne 16) { throw "Invalid width: $($directions[$i]) row $y = $($rows[$y].Length)" }
        for ($x=0; $x -lt 16; $x++) {
            $c = $pixels[(15-$y)*16+$x]
            if ($c.a -eq 0) { continue }
            $brush = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $graphics.FillRectangle($brush,($i*200+20+$x*10),(35+$y*10),10,10)
            $brush.Dispose()
        }
    }
    $graphics.DrawString($directions[$i],$font,[Drawing.Brushes]::White,($i*200+70),245)
}
$previewName = if ($PrefabPath) { [IO.Path]::GetFileNameWithoutExtension($PrefabPath) } else { 'Paladin' }
if ($Pose -ne 0) { $previewName += '-Pose'+$Pose }
$path = Join-Path $root "Docs/$previewName-preview.png"
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $bmp.Dispose(); $font.Dispose()
Write-Output "PASS: 16 production poses, connected silhouettes and cape occlusion. Preview: $path"
