$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Compile the actual base and derived pixel-generation code together with stubs.
Add-Type -AssemblyName System.Drawing
$peopleTests=Get-Content -Raw "$PSScriptRoot/TestPeopleVisuals.ps1"
$stubs=[regex]::Match($peopleTests,"(?s)\`$stubs=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$workmenTests=Get-Content -Raw "$PSScriptRoot/TestWorkmenVisuals.ps1"
$checks=[regex]::Match($workmenTests,"(?s)\`$code\+=@'\r?\n(.*?)\r?\n'@").Groups[1].Value
$baseSource=Get-Content -Raw "$root/Assets/Scripts/Characters/BlacksmithZeldaCharacterData.cs"
$baseFields=([regex]::Matches($baseSource,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$baseStart=$baseSource.IndexOf('    private static readonly string[] FrontBody')
$baseEnd=$baseSource.IndexOf('    private static int DirectionIndex')
$baseProduction=$baseSource.Substring($baseStart,$baseEnd-$baseStart)
$utility=(Get-Content -Raw "$root/Assets/Scripts/Characters/PixelWalkFrameUtility.cs").Replace('using UnityEngine;','')
$baseHarness=@"
public class BlacksmithVisualHarness : PeopleVisualBase {
$baseFields
public Color GhostFormEyeColor {get{return new Color(.04f,.1f,.16f,1f);}}
public Color HandColor {get{return PixelColor('G');}}
public Color[] Render(int direction,bool attack){var dirs=new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right};return CreateTexture(dirs[direction],attack).pixels;}
$baseProduction
}
"@
$source=Get-Content -Raw "$root/Assets/Scripts/Characters/DesertBlacksmithZeldaCharacterData.cs"
$fields=([regex]::Matches($source,'private Color \w+\s*=\s*new Color\([^;]+;')|ForEach-Object{$_.Value}) -join "`n"
$start=$source.IndexOf('    private static readonly string[] DesertFront')
if($start -lt 0){throw 'Missing desert smith maps'}
$production=$source.Substring($start) # includes final class brace
Add-Type -TypeDefinition ('using UnityEngine;'+$stubs+$utility+$checks+$baseHarness+'public class DesertBlacksmithVisualHarness : BlacksmithVisualHarness {'+$fields+$production) -CompilerOptions '/nowarn:0414,0649'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Blacksmith-level2.prefab"
$actor=[DesertBlacksmithVisualHarness]::new()
# Populate the inherited palette from the actual new prefab, not the old defaults.
foreach($type in @([BlacksmithVisualHarness],[DesertBlacksmithVisualHarness])){
    foreach($field in $type.GetFields([Reflection.BindingFlags]'NonPublic,Instance,DeclaredOnly')){
        $m=[regex]::Match($prefab,'(?m)^  '+$field.Name+': \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
        if($m.Success){$field.SetValue($actor,[UnityEngine.Color]::new([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value))}
    }
}
foreach($direction in 'Front','Back','Left'){
    $block=[regex]::Match($source,'(?s)string\[\] Desert'+$direction+' = \{(.*?)\};').Groups[1].Value
    $rows=@([regex]::Matches($block,'"([^"]+)"')|ForEach-Object{$_.Groups[1].Value})
    if($rows.Count -ne 16 -or @($rows|Where-Object{$_.Length -ne 16 -or $_ -match '[^.A-Z]'}).Count){throw 'Invalid 16x16 map'}
    foreach($row in 11..12){if($rows[$row] -ne '.....PP..PP.....'){throw 'Short foot positions changed'}}
}
$font=[Drawing.Font]::new('Arial',12);$count=0
foreach($pose in @('Idle','Walk0','Walk1','Attack')){
    $bmp=[Drawing.Bitmap]::new(960,245);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(24,31,43))
    foreach($d in 0..3){
        $pixels=$actor.Render($d,$pose -eq 'Attack')
        if($pose.StartsWith('Walk')){$pixels=[WorkmenChecks]::Walk($pixels,16,[int]::Parse($pose.Substring(4)))}
        [WorkmenChecks]::Connected($pixels,16);$count++
        if($pose -eq 'Attack'){
            $oldX=@(10,4,7,7)[$d]
            foreach($y in 6..7){foreach($x in $oldX..($oldX+1)){if($pixels[$y*16+$x].Equals($actor.HandColor)){throw "Duplicate idle hand in attack $d"}}}
        }
        $metal=@($pixels|Where-Object{[Math]::Abs($_.r-.48) -lt .01 -and [Math]::Abs($_.g-.55) -lt .01 -and $_.a -gt 0}).Count
        if($metal -lt 3){throw "Hammer missing $pose $d"}
        foreach($row in 0..15){foreach($x in 0..15){
            $c=$pixels[(15-$row)*16+$x];if($c.a -le 0){continue}
            $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c.r),[int](255*$c.g),[int](255*$c.b)))
            $g.FillRectangle($brush,24+$d*240+$x*12,10+$row*12,12,12);$brush.Dispose()
        }}
        $g.DrawString((@('Front','Back','Left','Right')[$d]+' / '+$pose),$font,[Drawing.Brushes]::White,50+$d*240,218)
    }
    $path=Join-Path $root "Docs/DesertBlacksmith-$pose-preview.png"
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();Write-Output "Preview: $path"
}
$font.Dispose()
$template=Get-Content -Raw "$root/Assets/Prefabs/Level1NPCS/Blacksmith.prefab"
foreach($field in 'health','skillValue','permissionLevel','possessionEnergy','possessionCost','moveSpeed','cameraOrthographicSize','playerVisionRadius','attackPower','attackPrefab','attackDuration','attackSpawnOffset','attackSize'){
    $pattern="(?m)^  ${field}: (.*)$"
    if([regex]::Match($prefab,$pattern).Groups[1].Value.Trim() -ne [regex]::Match($template,$pattern).Groups[1].Value.Trim()){throw "Gameplay value changed: $field"}
}
$definitions=@([regex]::Matches($prefab,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
foreach($ref in [regex]::Matches($prefab,'\{fileID: (\d+)\}')){
    $id=$ref.Groups[1].Value;if($id -ne '0' -and $definitions -notcontains $id){throw "Broken prefab local reference: $id"}
}
if(!$prefab.Contains('guid: bece9032aed94df08666324b9ba648c2') -or !$source.Contains(': BlacksmithZeldaCharacterData')){throw 'Wrong inherited role/visual script'}
foreach($path in 'Assets/Prefabs/Strongman.prefab','Assets/Prefabs/Level1NPCS/Strongman-Level1.prefab','Assets/Prefabs/Level2NPCS/Strongman-level2.prefab'){
    $s=Get-Content -Raw "$root/$path";$blocks=[regex]::Split($s,'(?m)(?=^--- !u!)')
    $t=$blocks|Where-Object{$_ -match '^--- !u!4 ' -and $_ -match 'm_Father: \{fileID: 0\}'}
    if(!$t.Contains('m_LocalScale: {x: 1.1, y: 1.1, z: 1.1}')){throw "Strongman root not uniformly scaled: $path"}
    $children=$blocks|Where-Object{$_ -match '^--- !u!4 ' -and $_ -notmatch 'm_Father: \{fileID: 0\}'}
    foreach($child in $children){if(!$child.Contains('m_LocalScale: {x: 1, y: 1, z: 1}')){throw 'Visual child scaled twice'}}
}
foreach($name in 'Strongman','DesertStrongman'){
    if((Get-Content -Raw "$root/Assets/Scripts/Characters/${name}ZeldaCharacterData.cs").Contains('BroadenStrongmanBody')){throw 'Superseded body widening must be reverted'}
}
Write-Output "PASS: $count desert smith poses, hammer/hand attachment, inherited values, prefab links; both Strongman styles scale uniformly to 110%, no body redraw."
