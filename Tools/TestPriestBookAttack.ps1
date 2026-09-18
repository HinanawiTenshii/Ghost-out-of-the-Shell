$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
$block=[regex]::Match($source,'(?s)string\[\] BookPixels = \{(.*?)\};').Groups[1].Value
$rows=@([regex]::Matches($block,'"([.A-Z]+)"') | ForEach-Object {$_.Groups[1].Value})
if($rows.Count -ne 12 -or @($rows | Where-Object Length -ne 12).Count){throw 'Invalid book pixel dimensions'}
$palette=@{}
$paletteSource=[regex]::Match($source,'(?s)private static Color BookPixelColor\(char symbol\).*?(?=    private static)').Value
foreach($m in [regex]::Matches($paletteSource,"case '([DCGP])': return new Color\(([\d.]+)f, ([\d.]+)f, ([\d.]+)f, 1f\);")){
    $palette[$m.Groups[1].Value]=@(2..4 | ForEach-Object {[int]([double]::Parse($m.Groups[$_].Value,[cultureinfo]::InvariantCulture)*255)})
}
foreach($symbol in 'D','C','G','P') {if(!$palette.ContainsKey($symbol) -or ($rows -join '') -notmatch $symbol){throw "Missing book detail: $symbol"}}
$enum=[regex]::Match($source,'(?s)enum ZeldaAttackVisualShape\s*\{(.*?)\}').Groups[1].Value
if(($enum -replace '\s','') -ne 'Box,Sword,Hammer,Rock,Shockwave,Book'){throw 'Existing serialized attack enum values changed'}
$character=Get-Content -Raw "$root/Assets/Scripts/Characters/PriestZeldaCharacterData.cs"
if($character -notmatch 'AttackVisualShape => ZeldaAttackVisualShape.Book;'){throw 'Priest must select Book'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Priest-level2.prefab"
foreach($expected in @('attackPower: 1','attackDuration: 0.24','attackSpawnOffset: {x: 0, y: 0.48}','attackSize: {x: 0.46, y: 0.4}','attackVisualTint: {r: 1, g: 1, b: 1, a: 1}')){
    if(!$prefab.Contains($expected)){throw "Unexpected attack configuration: $expected"}
}
# Preview the exact source pixel map, not an independently drawn approximation.
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(240,240)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43))
foreach($row in 0..11){foreach($x in 0..11){
    $symbol=[string]$rows[$row][$x]
    if($symbol -eq '.'){continue}
    if(!$palette.ContainsKey($symbol)){throw "Unknown symbol $symbol"}
    $rgb=$palette[$symbol]
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,$rgb[0],$rgb[1],$rgb[2]))
    $g.FillRectangle($brush,24+$x*16,24+$row*16,16,16)
    $brush.Dispose()
}}
$path=Join-Path $root 'Docs/Priest-book-attack-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose()
Write-Output "PASS: 12x12 bound book, cover/gold/pages, enum compatibility, unchanged attack damage/timing/size. Preview: $path"
