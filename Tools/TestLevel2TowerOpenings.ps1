# Static geometry/collision checks; does not replace Unity Play Mode.
$ErrorActionPreference='Stop'
. "$PSScriptRoot/PreviewLevel2Floor.ps1" -ScenePath 'Assets/Scenes/Level2/Level2-Floor2.unity' -OutputPath 'Docs/Level2-Floor2-towers-preview.png' -X -115 -Y -50 -Width 185 -Height 100 -NoGrid
$openings=@(
    @{go='275411079';returnGo='9123000000000000';axis='x';x=-107.56296990000001;y=42.67111285;length=6.9985924},
    @{go='2074082760';returnGo='9123000000000004';axis='x';x=-107.56296990000001;y=36.15911285;length=6.9985924},
    @{go='1535926980';returnGo='9123000000000008';axis='x';x=-107.56296990000001;y=-38.67588715;length=6.9985924},
    @{go='675766037';returnGo='9123000000000012';axis='y';x=-104.31296990000001;y=-41.92688715;length=7.004615},
    @{go='1533906414';returnGo='9123000000000016';axis='x';x=-62.6029699;y=-38.67388715;length=6.9985924},
    @{go='1668279191';returnGo='9123000000000020';axis='y';x=-65.8529699;y=-41.92688715;length=7.004615},
    @{go='1345354026';returnGo='9123000000000024';axis='x';x=-62.6009699;y=-10.35688715;length=6.9985924},
    @{go='1776046138';returnGo='9123000000000028';axis='y';x=-59.3529699;y=-7.10088715;length=7.004615},
    @{go='715467621';returnGo='9123000000000032';axis='y';x=-27.942969899999998;y=-7.10088715;length=7.004615},
    @{go='663368278';returnGo='9123000000000036';axis='y';x=-21.442969899999998;y=-7.10088715;length=7.004615},
    @{go='2060670193';returnGo='9123000000000040';axis='x';x=-24.692409899999998;y=-3.84688715;length=6.9985924})
$ids=@([regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate scene IDs'}
$byId=@{}
foreach($b in $blocks){if($b -match '^--- !u!\d+ &(\d+)'){$byId[$Matches[1]]=$b}}
function Bounds($points){
    return @(($points|ForEach-Object{$_[0]}|Measure-Object -Minimum).Minimum,
        ($points|ForEach-Object{$_[1]}|Measure-Object -Minimum).Minimum,
        ($points|ForEach-Object{$_[0]}|Measure-Object -Maximum).Maximum,
        ($points|ForEach-Object{$_[1]}|Measure-Object -Maximum).Maximum)
}
function Close($a,$b){if([Math]::Abs($a-$b) -gt .0001){throw "Geometry mismatch: $a vs $b"}}
$colliders=@{}
foreach($b in $blocks){
    if($b -notmatch '^--- !u!61 ' -or $b -notmatch 'm_Enabled: 1' -or $b -notmatch 'm_IsTrigger: 0'){continue}
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    if($byId[$go] -match 'm_IsActive: 0'){continue}
    $tid=$goTransforms[$go];if(!$tid){continue}
    $sz=[regex]::Match($b,'m_Size: \{x: ([^,]+), y: ([^}]+)')
    $off=[regex]::Match($b,'m_Offset: \{x: ([^,]+), y: ([^}]+)')
    $sx=[double]$sz.Groups[1].Value/2;$sy=[double]$sz.Groups[2].Value/2
    $ox=[double]$off.Groups[1].Value;$oy=[double]$off.Groups[2].Value
    $colliders[$go]=Bounds @((WorldPoint $tid ($ox-$sx) ($oy-$sy)),(WorldPoint $tid ($ox+$sx) ($oy+$sy)))
}
foreach($o in $openings){
    $parts=@()
    foreach($go in @($o.go,$o.returnGo)){
        $r=@($renderers|Where-Object{$_.go -eq $go})
        if($r.Count -ne 1 -or !$colliders.ContainsKey($go)){throw "Missing sprite/collider on $go"}
        $bounds=Bounds $r[0].corners
        foreach($i in 0..3){Close $bounds[$i] $colliders[$go][$i]}
        if($r[0].color[0] -ne 1 -or $r[0].color[1] -ne 1 -or $r[0].color[2] -ne 1){throw 'Wall color changed'}
        $parts+=,@($bounds)
        $tid=$goTransforms[$go]
        if(!$byId['138136558'].Contains("- {fileID: $tid}")){throw 'Wall missing from parent hierarchy'}
    }
    $axis=if($o.axis -eq 'x'){0}else{1}
    $center=if($axis -eq 0){$o.x}else{$o.y}
    Close $parts[0][$axis] ($center-$o.length/2)
    Close $parts[0][$axis+2] ($center-1.3)
    Close $parts[1][$axis] ($center+1.3)
    Close $parts[1][$axis+2] ($center+$o.length/2)
    # Verify a passage through the whole wall, not just its centerline.
    foreach($side in @(-.65,0,.65)){
        foreach($across in @(-1.2,0,1.2)){
            $px=if($axis -eq 0){$o.x+$across}else{$o.x+$side}
            $py=if($axis -eq 0){$o.y+$side}else{$o.y+$across}
            foreach($go in $colliders.Keys){
                $b=$colliders[$go]
                if($px -gt $b[0]+.0001 -and $px -lt $b[2]-.0001 -and $py -gt $b[1]+.0001 -and $py -lt $b[3]-.0001){throw "Opening $($o.go) blocked by $go"}
            }
        }
    }
}
foreach($id in $byId.Keys|Where-Object{$_ -like '9123000000000*'}){
    foreach($ref in [regex]::Matches($byId[$id],'\{fileID: (\d+)\}')){
        if($ref.Groups[1].Value -ne '0' -and !$byId.ContainsKey($ref.Groups[1].Value)){throw "Broken new reference $id"}
    }
}
Write-Output 'PASS: 11 openings across 5 towers; 2.6-unit clear width; original outer endpoints preserved; matching visual/collider bounds; passage samples clear of scene wall colliders; unique IDs and valid new references.'
