# Run production pixel methods with Unity data stubs; not a Play Mode test.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$people=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($people,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$baseSource=Get-Content -Raw "$root/Assets/Scripts/Characters/CivilianZeldaCharacterData.cs"
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/DesertCivilianZeldaCharacterData.cs"
$baseFields=([regex]::Matches($baseSource,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$fields+="`n"+[regex]::Match($source,'private bool wearHeadcloth = true;').Value
$start=$baseSource.IndexOf('    private static readonly string[] FrontBody')
$end=$baseSource.IndexOf('    private static int DirectionIndex')
$production=$baseSource.Substring($start,$end-$start)
$derived=$source.Substring($source.IndexOf('    private static readonly string[] DesertFront'))
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$code='using UnityEngine;'+$stubs+$utility+@"
public class CivilianHarness : PeopleVisualBase {
$baseFields
public Color GhostFormEyeColor {get{return new Color(.05f,.12f,.2f,1f);}}
public Color[] Render(int d,bool attack){return CreateTexture(new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right}[d],attack).pixels;}
$production
}
public class DesertCivilianHarness : CivilianHarness {
$fields
$derived
"@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Civilian-level2.prefab"
$actor=[DesertCivilianHarness]::new()
foreach($type in @([CivilianHarness],[DesertCivilianHarness])){
    foreach($field in $type.GetFields([Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly')){
        $m=[regex]::Match($prefab,'(?m)^  '+$field.Name+': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
        if($m.Success){$field.SetValue($actor,[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value))}
    }
}
foreach($d in 'DesertFront','DesertBack','DesertLeft','DesertRight','BareFront','BareBack','BareLeft'){
    $block=[regex]::Match($source,'(?s)string\[\] '+$d+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 16 -or @($rows|Where-Object{$_.Length -ne 16 -or $_ -match '[^.WJQICDLBFSEKH]'}).Count){throw "Invalid map $d"}
    if($d.StartsWith('Bare') -and ($rows -join '') -match '[WJQ]'){throw 'Headcloth remains on bareheaded body'}
    foreach($r in 11..12){if($rows[$r] -ne '.....KK..KK.....'){throw 'Foot rig mismatch'}}
    foreach($r in 13..15){if($rows[$r].Trim('.')){throw 'Legs too long'}}
    foreach($x in 6..9){if($rows[4][$x] -eq '.' -or $rows[5][$x] -eq '.'){throw 'Neck gap'}}
}
$headcloth=[DesertCivilianHarness].GetField('wearHeadcloth',[Reflection.BindingFlags]'NonPublic,Instance')
if(!$headcloth -or !$prefab.Contains('wearHeadcloth: 1')){throw 'Missing/default-disabled headcloth toggle'}
$headcloth.SetValue($actor,$false)
$left=$actor.Render(2,$false);$right=$actor.Render(3,$false)
foreach($y in 0..15){foreach($x in 0..15){if(!$left[$y*16+$x].Equals($right[$y*16+15-$x])){throw 'Profile mirror mismatch'}}}
$clothing=[CivilianHarness].GetField('clothingColor',[Reflection.BindingFlags]'NonPublic,Instance')
$defaultColor=$clothing.GetValue($actor)
$colors=@($defaultColor,[UnityEngine.Color]::new(.62,.32,.25,1),[UnityEngine.Color]::new(.55,.49,.33,1))
$font=[Drawing.Font]::new('Arial',11);$count=0
foreach($wear in @($true,$false)){
$headcloth.SetValue($actor,$wear)
foreach($pose in 'Idle','Attack','Walk0','Walk1'){
    $bmp=[Drawing.Bitmap]::new(800,630);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($variant in 0..2){foreach($d in 0..3){
        $attack=$pose -eq 'Attack'
        $clothing.SetValue($actor,$defaultColor);$original=$actor.Render($d,$attack)
        $clothing.SetValue($actor,$colors[$variant]);$pixels=$actor.Render($d,$attack)
        if($variant -gt 0){
            $different=@(0..255|Where-Object{!$original[$_].Equals($pixels[$_])}).Count
            if($different -lt 18){throw "Clothing not recolored: $d $pose"}
            foreach($i in 0..255){if($pixels[$i].a -ne $original[$i].a){throw 'Recolor changed silhouette'}}
            if(!$attack){foreach($i in 176..255){if(!$original[$i].Equals($pixels[$i])){throw 'Recolor changed head'}}}
        }
        if($pose.StartsWith('Walk')){$pixels=[PeopleVisualBase]::Walk($pixels,$false,[int]::Parse($pose.Substring(4)))}
        [PeopleVisualBase]::CheckConnected($pixels);$count++
        if($attack){
            $skin=$actor.Render(0,$false)[11*16+7];$oldX=@(4,10,7,7)[$d]
            foreach($y in 6..7){foreach($x in $oldX..($oldX+1)){if($pixels[$y*16+$x].Equals($skin)){throw 'Duplicate attack hand'}}}
        }
        foreach($row in 0..15){foreach($x in 0..15){
            $c=$pixels[(15-$row)*16+$x];if($c.a -le 0){continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,20+$d*200+$x*10,10+$variant*210+$row*10,10,10);$brush.Dispose()
        }}
        $g.DrawString((@('Front','Back','Left','Right')[$d]+' / '+$pose),$font,[Drawing.Brushes]::White,40+$d*200,183+$variant*210)
    }}
    $suffix=if($wear){''}else{'-Bareheaded'}
    $path=Join-Path $root "Docs/DesertCivilian$suffix-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
}
# Toggling affects the head/drape in all directions, never the feet; hair color
# only changes the exposed head and never the wrapped head or clothing.
$hair=[CivilianHarness].GetField('hairColor',[Reflection.BindingFlags]'NonPublic,Instance')
$originalHair=$hair.GetValue($actor)
foreach($d in 0..3){foreach($attack in @($false,$true)){
    $headcloth.SetValue($actor,$true);$wrapped=$actor.Render($d,$attack)
    $headcloth.SetValue($actor,$false);$bare=$actor.Render($d,$attack)
    if(@(0..255|Where-Object{!$wrapped[$_].Equals($bare[$_])}).Count -lt 10){throw 'Toggle did not redraw the head'}
    foreach($i in 0..79){if(!$wrapped[$i].Equals($bare[$i])){throw 'Toggle changed the feet'}}
    $hair.SetValue($actor,[UnityEngine.Color]::new(.42,.28,.14,1));$recolored=$actor.Render($d,$attack)
    if(@(0..255|Where-Object{!$bare[$_].Equals($recolored[$_])}).Count -lt 5){throw 'Bare hair not editable'}
    foreach($i in 0..175){if(!$bare[$i].Equals($recolored[$i])){throw 'Hair recolor changed clothing'}}
    $headcloth.SetValue($actor,$true);$wrappedRecolored=$actor.Render($d,$attack)
    foreach($i in 0..255){if(!$wrapped[$i].Equals($wrappedRecolored[$i])){throw 'Hair color changed wrapped art'}}
    $hair.SetValue($actor,$originalHair)
}}
$font.Dispose()
$template=Get-Content -Raw "$root/Assets/Prefabs/Level1NPCS/Civilian-Base 1.prefab"
# Only identity and appearance should differ; all combat, AI and collider data stay inherited.
$normalized=$prefab.Replace('840400000000000','710100000000000').Replace('Civilian-level2','Civilian-Base 1').Replace('74f92ddf9cfb44dbb9a327f5129c8660','4d086e0b3cdc4c6c92f77e44ea669790')
$normalized=$normalized.Replace('clothingColor: {r: 0.34, g: 0.51, b: 0.52, a: 1}','clothingColor: {r: 0.3, g: 0.62, b: 0.78, a: 1}').Replace('skinColor: {r: 0.72, g: 0.49, b: 0.32, a: 1}','skinColor: {r: 0.84, g: 0.62, b: 0.43, a: 1}')
$normalized=[regex]::Replace($normalized,'(?m)^  (headclothColor|wearHeadcloth):.*\r?\n','')
if($normalized.Replace("`r",'').Trim() -ne $template.Replace("`r",'').Trim()){throw 'Unintended gameplay/prefab changes'}
if(!$baseSource.Contains('animator.InvalidateFrames();') -or !$baseSource.Contains('visualsDirty = true;')){throw 'Missing palette cache invalidation'}
Write-Output "PASS: $count production poses across 3 clothing colors and both headwear states; connected art, short feet, centered hands, no neck, hair/clothing palette isolation, unchanged gameplay/prefab data."
