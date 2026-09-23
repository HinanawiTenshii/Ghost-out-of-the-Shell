$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
function PixelRows($name) {
    $block=[regex]::Match($source,"(?s)string\[\] $name = \{(.*?)\};").Groups[1].Value
    @([regex]::Matches($block,'"([.A-Z]+)"') | ForEach-Object {$_.Groups[1].Value})
}
$rows=PixelRows 'HammerPixels';$sword=PixelRows 'SwordPixels'
if($rows.Count -ne 16 -or @($rows | Where-Object Length -ne 16).Count){throw 'Invalid 16x16 hammer'}
$palette=@{}
$paletteSource=[regex]::Match($source,'(?s)private static Color SwordPixelColor\(char symbol\).*?(?=    private static)').Value
foreach($m in [regex]::Matches($paletteSource,"case '([HMGD])': return new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f, 1f\);")) {
    $palette[$m.Groups[1].Value]=@(2..4 | ForEach-Object {[double]::Parse($m.Groups[$_].Value,[cultureinfo]::InvariantCulture)})
}
if($palette.Count -ne 4){throw 'Sword palette changed'}
$solid=0
foreach($row in 0..15){foreach($x in 0..15){
    $symbol=[string]$rows[$row][$x]
    if($symbol -ne [string]$rows[$row][15-$x]){throw 'Hammer silhouette is unbalanced'}
    if($symbol -ne '.'){
        if(!$palette.ContainsKey($symbol)){throw "Unknown pixel $symbol"}
        $solid++
    }
}}
# Check the head, socket, grip and pommel form a single connected object.
$seen=[Collections.Generic.HashSet[string]]::new();$queue=[Collections.Generic.Queue[string]]::new()
$queue.Enqueue('2,1')
while($queue.Count){
    $key=$queue.Dequeue();if(!$seen.Add($key)){continue}
    $x,$y=@($key.Split(',') | ForEach-Object {[int]$_})
    foreach($d in @(@(-1,0),@(1,0),@(0,-1),@(0,1))){
        $nx=$x+$d[0];$ny=$y+$d[1]
        if($nx -ge 0 -and $nx -lt 16 -and $ny -ge 0 -and $ny -lt 16 -and $rows[$ny][$nx] -ne '.'){$queue.Enqueue("$nx,$ny")}
    }
}
if($seen.Count -ne $solid){throw 'Detached hammer pixels'}
$method=[regex]::Match($source,'(?s)private static Sprite CreateHammerSprite\(\).*?(?=    private static)').Value
foreach($expected in 'Texture2D(16, 16, TextureFormat.RGBA32, false)','FilterMode.Point','SwordPixelColor(HammerPixels[row][x])','new Vector2(0.5f, 0.2f), 16f)'){
    if(!$method.Contains($expected)){throw "Unexpected hammer geometry: $expected"}
}
if(!$source.Contains('hammerSprite = CreateHammerSprite();') -or $source.Contains('hammerTexture')){throw 'Old hammer drawing still active'}
foreach($name in 'StrongmanZeldaCharacterData','BlacksmithZeldaCharacterData'){
    $data=Get-Content -Raw "$root/Assets/Scripts/Characters/$name.cs"
    if($data -notmatch 'AttackVisualShape\s*=>\s*ZeldaAttackVisualShape.Hammer;'){throw "Wrong route: $name"}
}
foreach($type in 'Strongman','Blacksmith'){
    $data=Get-Content -Raw "$root/Assets/Scripts/Characters/Desert${type}ZeldaCharacterData.cs"
    if($data -notmatch ": ${type}ZeldaCharacterData"){throw "Level2 no longer inherits $type"}
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(960,350);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$font=[Drawing.Font]::new('Arial',11)
$tints=@(@(1,0.85,0.2,0.65),@(1,1,1,1),@(1,0.62,0.18,0.55),@(0.72,0.76,0.8,0.82))
$labels=@('SwordGuy reference','Hammer / neutral','Strongman / Level1 + 2','Blacksmith / Level1 + 2')
foreach($i in 0..3){
    $map=if($i -eq 0){$sword}else{$rows};$width=$map[0].Length;$left=$i*240+(240-$width*14)/2
    foreach($y in 0..15){foreach($x in 0..($width-1)){
        $s=[string]$map[$y][$x];if($s -eq '.'){continue};$p=$palette[$s];$t=$tints[$i]
        $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$t[3]),[int](255*$p[0]*$t[0]),[int](255*$p[1]*$t[1]),[int](255*$p[2]*$t[2])))
        $g.FillRectangle($brush,[single]($left+$x*14),[single](28+$y*14),14,14);$brush.Dispose()
    }}
    $g.DrawString($labels[$i],$font,[Drawing.Brushes]::White,$i*240+14,292)
}
$path=Join-Path $root 'Docs/Hammer-attack-preview.png';$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output "PASS: 256 hammer pixels, $solid connected opaque pixels, sword palette, preserved footprint/pivot, and both character families. Preview: $path"
