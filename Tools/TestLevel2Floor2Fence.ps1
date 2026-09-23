# Static scene geometry/collision/layer checks. Does not replace a Unity play test.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Reuse the scene transform parser and generate the layout preview.
. "$PSScriptRoot/PreviewLevel2Floor.ps1" -ScenePath "Assets/Scenes/Level2/Level2-Floor2.unity" -OutputPath "Docs/Level2-Floor2-layout-preview.png" -X -60 -Y 45 -Width 210 -Height 50 -NoGrid
$ids=@([regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate scene IDs'}
$fence=$blocks|Where-Object{$_ -match '^--- !u!\d+ &9102000000000\d+'}
$gos=@{};$colliders=@()
function BoundsOf($points){
    return @(
        ($points|ForEach-Object{$_[0]}|Measure-Object -Minimum).Minimum,
        ($points|ForEach-Object{$_[1]}|Measure-Object -Minimum).Minimum,
        ($points|ForEach-Object{$_[0]}|Measure-Object -Maximum).Maximum,
        ($points|ForEach-Object{$_[1]}|Measure-Object -Maximum).Maximum)
}
function Overlap($a,$b){return $a[0] -lt $b[2]-.00001 -and $a[2] -gt $b[0]+.00001 -and $a[1] -lt $b[3]-.00001 -and $a[3] -gt $b[1]+.00001}
foreach($b in $fence){
    if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
    foreach($ref in [regex]::Matches($b,'\{fileID: (\d+)\}')){
        if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken new local reference'}
    }
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    if($type -eq '1'){
        if($b -notmatch 'm_Layer: 0\s' -or $b -notmatch 'm_IsActive: 1'){throw 'Fence must be active on non-occluding Default layer'}
        $gos[$id]=[regex]::Match($b,'m_Name: (.*)').Groups[1].Value.Trim()
    }
    if($type -eq '212' -and !$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Not using the existing engine Square sprite'}
    if($type -eq '61'){
        if($b -notmatch 'm_IsTrigger: 0' -or $b -notmatch 'm_Enabled: 1'){throw 'Non-solid fence collider'}
        $tid=$goTransforms[$go]
        $size=[regex]::Match($b,'m_Size: \{x: ([^,]+), y: ([^}]+)')
        $offset=[regex]::Match($b,'m_Offset: \{x: ([^,]+), y: ([^}]+)')
        $sx=[double]$size.Groups[1].Value/2;$sy=[double]$size.Groups[2].Value/2
        $ox=[double]$offset.Groups[1].Value;$oy=[double]$offset.Groups[2].Value
        $pts=@((WorldPoint $tid ($ox-$sx) ($oy-$sy)),(WorldPoint $tid ($ox+$sx) ($oy+$sy)))
        $colliders+=@{go=$go;bounds=(BoundsOf $pts)}
    }
}
$fenceRenderers=@($renderers|Where-Object{$gos.ContainsKey($_.go)})
if($fenceRenderers.Count -ne 19 -or $colliders.Count -ne 19){throw 'Unexpected fence geometry count'}
if(@($gos.Values|Where-Object{$_ -eq 'Pillar'}).Count -ne 14){throw 'Unexpected pillar count'}
if(@($gos.Values|Where-Object{$_ -like 'Rail *' -or $_ -like 'Bar *'}).Count){throw 'Old double-rail geometry remains'}
$walls=@($fenceRenderers|Where-Object{$gos[$_.go] -eq 'Wall'})
if($walls.Count -ne 5){throw 'Expected one wall per fence run'}
foreach($wall in $walls){
    $collision=@($colliders|Where-Object{$_.go -eq $wall.go})[0].bounds
    $visual=BoundsOf $wall.corners
    foreach($i in 0..3){if([Math]::Abs($visual[$i]-$collision[$i]) -gt .00001){throw 'Single wall and collider bounds mismatch'}}
}
$white=@($renderers|Where-Object{$_.color[0] -eq 1 -and $_.color[1] -eq 1 -and $_.color[2] -eq 1 -and !$gos.ContainsKey($_.go)})
$water=@($renderers|Where-Object{$_.order -eq -7 -and $_.color[2] -gt .9})
foreach($r in $fenceRenderers){
    $scale=$transforms[$r.tid].scale
    if($gos[$r.go] -eq 'Wall' -and [Math]::Abs([Math]::Min($scale[0],$scale[1])-.44) -gt .00001){throw 'Wall thickness differs from Floor1'}
    if($gos[$r.go] -eq 'Pillar' -and ([Math]::Abs($scale[0]-.65) -gt .00001 -or [Math]::Abs($scale[1]-.65) -gt .00001)){throw 'Pillar size differs from Floor1'}
    $bounds=BoundsOf $r.corners
    foreach($wall in $white){if(Overlap $bounds (BoundsOf $wall.corners)){throw "Fence overlaps white wall $($wall.go)"}}
    foreach($river in $water){if(Overlap $bounds (BoundsOf $river.corners)){throw 'Fence intrudes into river'}}
    if([Math]::Abs($r.color[0]-$r.color[2]) -gt .06){throw 'Fence is not gray'}
}
$gates=@(
    @(17.3,59.038573,18.7,62.038573),
    @(17.3,72.283573,18.7,75.283573),
    @(120.943868,54.3,123.943868,55.7))
foreach($gate in $gates){foreach($c in $colliders){if(Overlap $gate $c.bounds){throw 'Collider intrudes into a gate opening'}}}
$runs=@(
    @('y',18,56.36311285,59.038573),
    @('y',18,62.038573,72.283573),
    @('y',18,75.283573,83.95306285),
    @('x',55,110.2080301,120.943868),
    @('x',55,123.943868,137.7973026))
foreach($run in $runs){
    for($p=[double]$run[2]+.01;$p -lt [double]$run[3]-.01;$p+=.1){
        $x=if($run[0] -eq 'y'){$run[1]}else{$p};$y=if($run[0] -eq 'y'){$p}else{$run[1]}
        if(!@($colliders|Where-Object{$x -ge $_.bounds[0] -and $x -le $_.bounds[2] -and $y -ge $_.bounds[1] -and $y -le $_.bounds[3]}).Count){throw 'Hole in continuous fence collision'}
    }
}
$camera=Get-Content -Raw "$root/Assets/Prefabs/Main Camera.prefab"
$mask=[int][regex]::Match($camera,'blockLayers:\s+serializedVersion: 2\s+m_Bits: (\d+)').Groups[1].Value
if(($mask -band 1) -ne 0){throw 'Camera vision unexpectedly blocks Default layer'}
$vision=Get-Content -Raw "$root/Assets/Scripts/Camera/CameraCircularVision.cs"
if(!$vision.Contains('LayerMask.NameToLayer("Hidden Blocks")')){throw 'Recheck runtime block layer configuration'}
$civilian=Get-Content -Raw "$root/Assets/Prefabs/Level2NPCS/Civilian-level2.prefab"
$solid=[uint32][regex]::Match($civilian,'solidCollisionLayers:\s+serializedVersion: 2\s+m_Bits: (\d+)').Groups[1].Value
if(($solid -band 1) -eq 0){throw 'Player movement ignores fence collision layer'}
Write-Output 'PASS: 5 single-layer walls, 14 pillars, 19 Square sprites, 19 solid colliders; matched wall collision bounds; 3 clear gates; no white-wall/river overlap; continuous collision; layer excluded from player vision.'
