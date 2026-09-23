# Static scene geometry/collision/layer checks. Does not replace a Unity play test.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
# Reuse the scene transform parser and generate the layout preview.
. "$PSScriptRoot/PreviewLevel2Floor.ps1" -X -95 -Y 10 -Width 125 -Height 70 -NoGrid
$ids=@([regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate scene IDs'}
$fence=$blocks|Where-Object{$_ -match '^--- !u!\d+ &9101000000000\d+'}
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
if($fenceRenderers.Count -ne 37 -or $colliders.Count -ne 37){throw 'Unexpected fence geometry count'}
if(@($gos.Values|Where-Object{$_ -eq 'Pillar'}).Count -ne 31){throw 'Unexpected pillar count'}
if(@($gos.Values|Where-Object{$_ -like 'Rail *' -or $_ -like 'Bar *'}).Count){throw 'Old double-rail geometry remains'}
$walls=@($fenceRenderers|Where-Object{$gos[$_.go] -eq 'Wall'})
if($walls.Count -ne 6){throw 'Expected one wall per fence run'}
foreach($wall in $walls){
    $parent=$transforms[$wall.tid].parent
    $parentGo=@($goTransforms.Keys|Where-Object{$goTransforms[$_] -eq $parent})[0]
    $collision=@($colliders|Where-Object{$_.go -eq $parentGo})[0].bounds
    $visual=BoundsOf $wall.corners
    foreach($i in 0..3){if([Math]::Abs($visual[$i]-$collision[$i]) -gt .00001){throw 'Single wall and collider bounds mismatch'}}
}
$white=@($renderers|Where-Object{$_.color[0] -eq 1 -and $_.color[1] -eq 1 -and $_.color[2] -eq 1 -and !$gos.ContainsKey($_.go)})
$water=@($renderers|Where-Object{$_.order -eq -7 -and $_.color[2] -gt .9})
foreach($r in $fenceRenderers){
    $bounds=BoundsOf $r.corners
    foreach($wall in $white){if(Overlap $bounds (BoundsOf $wall.corners)){throw "Fence overlaps white wall $($wall.go)"}}
    foreach($river in $water){if(Overlap $bounds (BoundsOf $river.corners)){throw 'Fence intrudes into river'}}
    if([Math]::Abs($r.color[0]-$r.color[2]) -gt .06){throw 'Fence is not gray'}
}
$gates=@(
    @(-84.5,37.496738,-83.2,40.496738),
    @(-26.2536,17.9,-23.2536,19.2),
    @(18.35,38.296738,19.65,41.296738))
foreach($gate in $gates){foreach($c in $colliders){if(Overlap $gate $c.bounds){throw 'Collider intrudes into a gate opening'}}}
$runs=@(
    @('y',-83.85,18.55,37.496738),@('y',-83.85,40.496738,70.35000285),
    @('x',18.55,-83.85,-26.2536),@('x',18.55,-23.2536,19),
    @('y',19,18.55,38.296738),@('y',19,41.296738,55.34986885))
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
Write-Output 'PASS: 6 single-layer walls, 31 pillars, 37 Square sprites, 37 solid colliders; matched wall collision bounds; 3 clear gates; no white-wall/river overlap; continuous collision; layer excluded from player vision.'
