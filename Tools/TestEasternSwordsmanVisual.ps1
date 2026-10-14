$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Add-Type -AssemblyName System.Drawing
$people=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($people,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$stubs=$stubs.Replace('void Apply(bool mip, bool discard)', 'void Apply(bool mip = true, bool discard = false)')
$baseSource=Get-Content -Raw "$root/Assets/Scripts/Characters/PaladinZeldaCharacterData.cs"
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/EasternSwordsmanZeldaCharacterData.cs"
$baseFields=([regex]::Matches($baseSource,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$start=$baseSource.IndexOf('    private static readonly string[] Front')
$end=$baseSource.IndexOf('    private readonly Sprite[,] frames')
$methods=$baseSource.IndexOf('    protected virtual Sprite CreateSprite')
$methodsEnd=$baseSource.IndexOf('    protected override void OnValidate')
$production=$baseSource.Substring($start,$end-$start)+$baseSource.Substring($methods,$methodsEnd-$methods)
$derived=$source.Substring($source.IndexOf('    private static readonly string[] Front'))
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$code='using UnityEngine;'+$stubs+$utility+@"
public class PaladinHarness : PeopleVisualBase {
$baseFields
public Color GhostFormEyeColor {get{return visorColor;}}
public Color[] Render(int d,int pose){return CreateSprite(d,pose).texture.pixels;}
$production
}
public class EasternSwordsmanHarness : PaladinHarness {
$fields
$derived
"@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:0414,0649'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Idle/EasternSwordsman.prefab"
$actor=[EasternSwordsmanHarness]::new()
foreach($type in @([PaladinHarness],[EasternSwordsmanHarness])){
    foreach($field in $type.GetFields([Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly')){
        $m=[regex]::Match($prefab,'(?m)^  '+$field.Name+': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
        if($m.Success){$field.SetValue($actor,[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value))}
    }
}
foreach($direction in 'Front','Back','Left','Right'){
    $block=[regex]::Match($source,'(?s)string\[\] '+$direction+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 16 -or @($rows|Where-Object{$_.Length -ne 16 -or $_ -match '[^.A-Z]'}).Count){throw "Invalid map $direction"}
    $widths=@($rows[0..3]|ForEach-Object{$_.Trim('.').Length})
    if(($widths -join ',') -ne '2,6,10,12'){throw "Conical hat tiers: $direction"}
    foreach($row in 12..13){if($rows[$row] -ne '.....KK..KK.....'){throw 'Foot rig mismatch'}}
}
$font=[Drawing.Font]::new('Arial',12)
foreach($pose in 0..3){
    $bmp=[Drawing.Bitmap]::new(960,245);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($d in 0..3){
        $pixels=$actor.Render($d,$pose)
        [PeopleVisualBase]::CheckConnected($pixels)
        if($pose -in 1,2){
            $idle=$actor.Render($d,0)
            foreach($i in 80..255){if(!$pixels[$i].Equals($idle[$i])){throw 'Walk moves hat/upper body'}}
        }
        if($pose -eq 3){
            $oldX=@(4,10,7,7)[$d]
            $skin=$actor.Render(0,0)[10*16+7]
            foreach($y in 6..7){foreach($x in $oldX..($oldX+1)){if($pixels[$y*16+$x].Equals($skin)){throw 'Duplicate resting hand'}}}
        }
        foreach($row in 0..15){foreach($x in 0..15){
            $c=$pixels[(15-$row)*16+$x];if($c.a -le 0){continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
            $g.FillRectangle($brush,24+$d*240+$x*12,10+$row*12,12,12);$brush.Dispose()
        }}
        $g.DrawString((@('Front','Back','Left','Right')[$d]+' / '+@('Idle','Walk0','Walk1','Attack')[$pose]),$font,[Drawing.Brushes]::White,50+$d*240,218)
    }
    $path=Join-Path $root ('Docs/EasternSwordsman-'+@('Idle','Walk0','Walk1','Attack')[$pose]+'-preview.png')
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
$font.Dispose()
Write-Output 'PASS: 16 poses; conical hat, short boots, connected sheath and single attacking hand.'
