# Run production pixel methods with Unity data stubs; not a Play Mode test.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$people=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($people,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$baseSource=Get-Content -Raw "$root/Assets/Scripts/Characters/CivilianZeldaCharacterData.cs"
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/DancerZeldaCharacterData.cs"
$baseFields=([regex]::Matches($baseSource,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$start=$baseSource.IndexOf('    private static readonly string[] FrontBody')
$end=$baseSource.IndexOf('    private static int DirectionIndex')
$production=$baseSource.Substring($start,$end-$start)
$derived=$source.Substring($source.IndexOf('    private static readonly string[] DancerFront'))
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$code='using UnityEngine;'+$stubs+$utility+@"
public class CivilianHarness : PeopleVisualBase {
$baseFields
public Color GhostFormEyeColor {get{return new Color(.05f,.12f,.2f,1f);}}
public Color[] Render(int d,bool attack){return CreateTexture(new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right}[d],attack).pixels;}
$production
}
public class DancerHarness : CivilianHarness {
$fields
$derived
"@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Patrol/Dancer-level2.prefab"
$actor=[DancerHarness]::new()
foreach($type in @([CivilianHarness],[DancerHarness])){
    foreach($field in $type.GetFields([Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly')){
        $m=[regex]::Match($prefab,'(?m)^  '+$field.Name+': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
        if($m.Success){$field.SetValue($actor,[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value))}
    }
}

foreach($d in 'DancerFront','DancerBack','DancerLeft','DancerRight'){
    $block=[regex]::Match($source,'(?s)string\[\] '+$d+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 16 -or @($rows|Where-Object{$_.Length -ne 16 -or $_ -match '[^.ICDLBFSEKHTAVJ]'}).Count){throw "Invalid map $d"}
    foreach($r in 11..12){if($rows[$r] -ne '.....KK..KK.....'){throw 'Foot rig mismatch'}}
    foreach($r in 13..15){if($rows[$r].Trim('.')){throw 'Legs too long'}}
    foreach($x in 6..9){if($rows[4][$x] -eq '.' -or $rows[5][$x] -eq '.'){throw 'Neck gap'}}
    if($d -eq 'DancerFront'){
        if($rows[3] -ne '....HFEFFEFH....' -or $rows[4] -ne '....TSFFFFST....'){
            throw 'Front eyes must remain two isolated pixels, with skin below rather than dark bars'
        }
    }
}
$clothing=[CivilianHarness].GetField('clothingColor',[Reflection.BindingFlags]'NonPublic,Instance')
$defaultColor=$clothing.GetValue($actor)
$colors=@($defaultColor,[UnityEngine.Color]::new(.12,.68,.68,1),[UnityEngine.Color]::new(.43,.30,.66,1))
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
    $path=Join-Path $root "Docs/Dancer-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
$font.Dispose()
# Accessories are independent of the shared civilian clothing tint.
foreach($name in 'sashColor','jewelryColor'){
    $field=[DancerHarness].GetField($name,[Reflection.BindingFlags]'NonPublic,Instance')
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
Write-Output "PASS: $count production poses, 3 clothing colors, editable accessories, connected art, short feet, and attack arm checks."
