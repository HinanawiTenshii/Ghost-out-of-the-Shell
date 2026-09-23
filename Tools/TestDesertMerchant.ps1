# Run production pixel methods with Unity data stubs; not a Play Mode test.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$people=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($people,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$baseSource=Get-Content -Raw "$root/Assets/Scripts/Characters/CivilianZeldaCharacterData.cs"
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/DesertMerchantZeldaCharacterData.cs"
$baseFields=([regex]::Matches($baseSource,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$start=$baseSource.IndexOf('    private static readonly string[] FrontBody')
$end=$baseSource.IndexOf('    private static int DirectionIndex')
$production=$baseSource.Substring($start,$end-$start)
$derived=$source.Substring($source.IndexOf('    private static readonly string[] MerchantFront'))
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$code='using UnityEngine;'+$stubs+$utility+@"
public class CivilianHarness : PeopleVisualBase {
$baseFields
public Color GhostFormEyeColor {get{return new Color(.05f,.12f,.2f,1f);}}
public Color[] Render(int d,bool attack){return CreateTexture(new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right}[d],attack).pixels;}
$production
}
public class DesertMerchantHarness : CivilianHarness {
$fields
$derived
"@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Merchant-level2.prefab"
$actor=[DesertMerchantHarness]::new()
foreach($type in @([CivilianHarness],[DesertMerchantHarness])){
    foreach($field in $type.GetFields([Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly')){
        $m=[regex]::Match($prefab,'(?m)^  '+$field.Name+': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
        if($m.Success){$field.SetValue($actor,[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value))}
    }
}

foreach($d in 'MerchantFront','MerchantBack','MerchantLeft','MerchantRight'){
    $block=[regex]::Match($source,'(?s)string\[\] '+$d+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 16 -or @($rows|Where-Object{$_.Length -ne 16 -or $_ -match '[^.WJQICDLBFSEKHTPA]'}).Count){throw "Invalid map $d"}
    foreach($r in 11..12){if($rows[$r] -ne '.....KK..KK.....'){throw 'Foot rig mismatch'}}
    foreach($r in 13..15){if($rows[$r].Trim('.')){throw 'Legs too long'}}
    foreach($x in 6..9){if($rows[4][$x] -eq '.' -or $rows[5][$x] -eq '.'){throw 'Neck gap'}}
    if($d -eq 'MerchantFront'){
        if($rows[3] -ne '.....FEFFEF.....' -or $rows[4] -ne '.....SFFFFS.....'){
            throw 'Front eyes must remain two isolated pixels, with skin below rather than dark bars'
        }
    }
}
$clothing=[CivilianHarness].GetField('clothingColor',[Reflection.BindingFlags]'NonPublic,Instance')
$defaultColor=$clothing.GetValue($actor)
$colors=@($defaultColor,[UnityEngine.Color]::new(.68,.23,.30,1),[UnityEngine.Color]::new(.43,.30,.66,1))
$font=[Drawing.Font]::new('Arial',11);$count=0
foreach($pose in 'Idle','Attack','Walk0','Walk1'){
    $bmp=[Drawing.Bitmap]::new(800,630);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($variant in 0..2){foreach($d in 0..3){
        $attack=$pose -eq 'Attack'
        $clothing.SetValue($actor,$defaultColor);$original=$actor.Render($d,$attack)
        $clothing.SetValue($actor,$colors[$variant]);$pixels=$actor.Render($d,$attack)
        if($variant -gt 0){
            $different=@(0..255|Where-Object{!$original[$_].Equals($pixels[$_])}).Count
            if($different -lt 18){throw "Clothing not recolored: $d $pose ($different pixels)"}
            foreach($i in 0..255){if($pixels[$i].a -ne $original[$i].a){throw 'Recolor changed silhouette'}}
            if(!$attack){foreach($i in 176..255){if(!$original[$i].Equals($pixels[$i])){throw 'Recolor changed head'}}}
        }
        if($pose.StartsWith('Walk')){$pixels=[PeopleVisualBase]::Walk($pixels,$false,[int]::Parse($pose.Substring(4)))}
        [PeopleVisualBase]::CheckConnected($pixels);$count++
        if($d -eq 0){
            foreach($x in 6,9){
                $eye=$pixels[12*16+$x];$cheek=$pixels[11*16+$x]
                $eyeBrightness=.2126*$eye.r+.7152*$eye.g+.0722*$eye.b
                $cheekBrightness=.2126*$cheek.r+.7152*$cheek.g+.0722*$cheek.b
                if($eyeBrightness -ge .4*$cheekBrightness){throw 'Eyes need clear contrast against the face'}
                if(!$cheek.Equals($pixels[11*16+7])){throw 'Dark stripe remains below the eyes'}
            }
        }
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
    $path=Join-Path $root "Docs/DesertMerchant-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
$font.Dispose()
# Accessories are independent of the shared civilian clothing tint.
foreach($name in 'headclothColor','trimColor','leatherColor'){
    $field=[DesertMerchantHarness].GetField($name,[Reflection.BindingFlags]'NonPublic,Instance')
    $old=$field.GetValue($actor)
    foreach($d in 0..3){
        $before=$actor.Render($d,$false)
        $field.SetValue($actor,[UnityEngine.Color]::new(.25,.65,.9,1))
        $after=$actor.Render($d,$false)
        if(@(0..255|Where-Object{!$before[$_].Equals($after[$_])}).Count -eq 0){throw "$name has no effect in direction $d"}
        foreach($i in 0..255){if($before[$i].a -ne $after[$i].a){throw 'Accessory tint changed silhouette'}}
        $field.SetValue($actor,$old)
    }
}
# New prefab preserves all civilian physics, AI, possession and camera settings.
$template=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Civilian-level2.prefab"
$normalized=$prefab.Replace('840500000000000','840400000000000').Replace('Merchant-level2','Civilian-level2').Replace('2b58794e13d94d489573aa375068a119','74f92ddf9cfb44dbb9a327f5129c8660')
$normalized=$normalized.Replace('clothingColor: {r: 0.12, g: 0.54, b: 0.48, a: 1}','clothingColor: {r: 0.34, g: 0.51, b: 0.52, a: 1}')
$normalized=$normalized.Replace('headclothColor: {r: 0.87, g: 0.84, b: 0.72, a: 1}','headclothColor: {r: 0.8, g: 0.79, b: 0.73, a: 1}')
$normalized=[regex]::Replace($normalized,'(?m)^  (trimColor|leatherColor):.*\r?\n','')
$template=[regex]::Replace($template,'(?m)^  wearHeadcloth:.*\r?\n','')
if(($normalized -replace '\r','').Trim() -ne ($template -replace '\r','').Trim()){throw 'Unintended gameplay/prefab changes'}
if(!$baseSource.Contains('animator.InvalidateFrames();') -or !$baseSource.Contains('visualsDirty = true;')){throw 'Missing palette cache invalidation'}
Write-Output "PASS: $count production poses, 3 clothing colors, independent accessories, connected art, short feet, centered hands, no neck, unchanged gameplay."
