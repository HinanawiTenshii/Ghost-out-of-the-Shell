# Validate and render the actual prefab's Square sprites. No Unity Play Mode.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab"
$blocks=[regex]::Split($prefab,'(?m)(?=^--- !u!)')
$ids=@([regex]::Matches($prefab,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate IDs'}
foreach($ref in [regex]::Matches($prefab,'\{fileID: (\d+)\}')){
    $id=$ref.Groups[1].Value
    if($id -ne '0' -and $ids -notcontains $id){throw "Broken local reference $id"}
}
$objects=@{};$transforms=@{};$goTransforms=@{};$renderers=@();$colliders=@()
foreach($b in $blocks){
    if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    switch($type){
        '1' {
            $objects[$id]=[regex]::Match($b,'m_Name: (.*)').Groups[1].Value.Trim()
            if(!$b.Contains('m_Layer: 0') -or !$b.Contains('m_IsActive: 1')){throw 'Inactive/vision-blocking decoration'}
        }
        '4' {
            $pos=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            $scale=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
            $rot=[regex]::Match($b,'m_LocalRotation: \{x: 0, y: 0, z: ([^,]+), w: ([^}]+)')
            if(!$rot.Success -or [double]$pos.Groups[3].Value -ne 0){throw 'Not a flat XY decoration'}
            $norm=[Math]::Pow([double]$rot.Groups[1].Value,2)+[Math]::Pow([double]$rot.Groups[2].Value,2)
            if([Math]::Abs($norm-1) -gt .00001){throw 'Non-normalized rotation'}
            $transforms[$id]=@{pos=@([double]$pos.Groups[1].Value,[double]$pos.Groups[2].Value);scale=@([double]$scale.Groups[1].Value,[double]$scale.Groups[2].Value);angle=2*[Math]::Atan2([double]$rot.Groups[1].Value,[double]$rot.Groups[2].Value);parent=[regex]::Match($b,'m_Father: \{fileID: (\d+)').Groups[1].Value}
            $goTransforms[$go]=$id
        }
        '212' {
            if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Non-geometric texture'}
            $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
            $renderers+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([float]$c.Groups[1].Value,[float]$c.Groups[2].Value,[float]$c.Groups[3].Value,[float]$c.Groups[4].Value)}
        }
        '61' {$colliders+=,$b}
        '114' {throw 'Static decoration should not need runtime scripts'}
    }
}
function FlowerbedPoint($id,[double]$x,[double]$y){
    while($transforms.ContainsKey($id)){
        $t=$transforms[$id];$sx=$x*$t.scale[0];$sy=$y*$t.scale[1];$c=[Math]::Cos($t.angle);$s=[Math]::Sin($t.angle)
        $x=$sx*$c-$sy*$s+$t.pos[0];$y=$sx*$s+$sy*$c+$t.pos[1];$id=$t.parent
    }
    return @($x,$y)
}
# Plants are now independent prefabs; do not restore the removed embedded plants.
if($renderers.Count -ne 3){throw 'Unexpected geometry count'}
# Current prefab has the original disabled root box plus a user-added solid frame box.
$rootBox=$colliders|Where-Object{$_ -match '^--- !u!61 &176\b'}
$frameBox=$colliders|Where-Object{$_ -match '^--- !u!61 &7269801477493054471\b'}
if($colliders.Count -ne 2 -or !$rootBox.Contains('m_Enabled: 0') -or !$rootBox.Contains('m_Size: {x: 4.4, y: 2.2}') -or !$frameBox.Contains('m_Enabled: 1') -or !$frameBox.Contains('m_Size: {x: 1, y: 1}') -or !$frameBox.Contains('m_IsTrigger: 0')){throw 'Existing root/frame collider setup changed'}
$bodyNames=@('Stone Border','Inner Stone Edge','Soil')
foreach($r in $renderers){
    $tid=$goTransforms[$r.go];$r.corners=@((FlowerbedPoint $tid -.5 -.5),(FlowerbedPoint $tid .5 -.5),(FlowerbedPoint $tid .5 .5),(FlowerbedPoint $tid -.5 .5))
    if($bodyNames -contains $objects[$r.go]){
        if($transforms[$tid].angle -ne 0){throw 'Bed body must be axis-aligned rectangles'}
    }else{
        foreach($p in $r.corners){if([Math]::Abs($p[0]) -gt 1.93 -or [Math]::Abs($p[1]) -gt .83){throw 'Plant extends beyond soil'}}
        if($r.order -le -3){throw 'Plant hidden beneath soil'}
    }
}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1000,540);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62));$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
foreach($r in ($renderers|Sort-Object order)){
    $pts=[Drawing.PointF[]]@($r.corners|ForEach-Object{[Drawing.PointF]::new([float](500+$_[0]*190),[float](270-$_[1]*190))})
    $c=$r.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$c[3]),[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
    $g.FillPolygon($brush,$pts);$brush.Dispose()
}
$path=Join-Path $root 'Docs/GeometricFlowerbed-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output "Preview: $path"
Write-Output 'PASS: 3 rectangular frame/soil sprites, valid prefab links, existing disabled root box and solid frame box, no runtime generation or vision-blocking layer; plants remain independent.'
