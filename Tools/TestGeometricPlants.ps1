# Validate the actual standalone prefab geometry and render a contact sheet.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root 'Assets/Prefabs/Decorations/Plants'
$kinds=@(
    @('GeometricLeafCluster','Leaf Cluster',5),
    @('GeometricCoralFlower','Coral Flower',7),
    @('GeometricGoldFlower','Gold Flower',7),
    @('GeometricBroadleafShrub','Broadleaf Shrub',6),
    @('GeometricGrassTuft','Grass Tuft',7),
    @('GeometricFern','Fern',12),
    @('GeometricSucculent','Succulent',13))
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1280,680);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62));$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$font=[Drawing.Font]::new('Arial',15);$smallFont=[Drawing.Font]::new('Arial',11)
$guids=@();$total=0
foreach($index in 0..($kinds.Count-1)){
    $kind=$kinds[$index];$path=Join-Path $directory ($kind[0]+'.prefab')
    $prefab=Get-Content -Raw $path;$blocks=[regex]::Split($prefab,'(?m)(?=^--- !u!)')
    $ids=@([regex]::Matches($prefab,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
    if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate local IDs'}
    foreach($ref in [regex]::Matches($prefab,'\{fileID: (\d+)\}')){
        if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken local reference'}
    }
    $guid=[regex]::Match((Get-Content -Raw ($path+'.meta')),'guid: ([a-f0-9]{32})').Groups[1].Value
    if(!$guid -or $guids -contains $guid){throw 'Missing/duplicate asset GUID'};$guids+=,$guid
    $transforms=@{};$renderers=@();$rootCount=0
    foreach($b in $blocks){
        if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
        $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
        switch($type){
            '1' {if($b -notmatch 'm_Layer: 0\s' -or !$b.Contains('m_IsActive: 1')){throw 'Inactive/vision-blocking plant'}}
            '4' {
                $pos=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
                $scale=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
                $rot=[regex]::Match($b,'m_LocalRotation: \{x: 0, y: 0, z: ([^,]+), w: ([^}]+)')
                $parent=[regex]::Match($b,'m_Father: \{fileID: (\d+)').Groups[1].Value
                if(!$rot.Success -or [double]$pos.Groups[3].Value -ne 0){throw 'Not flat top-down geometry'}
                $norm=[Math]::Pow([double]$rot.Groups[1].Value,2)+[Math]::Pow([double]$rot.Groups[2].Value,2)
                if([Math]::Abs($norm-1) -gt .00001){throw 'Invalid rotation quaternion'}
                if($parent -eq '0'){
                    $rootCount++
                    if(!$b.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}') -or !$b.Contains('m_LocalScale: {x: 1, y: 1, z: 1}') -or !$b.Contains('m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}')){throw 'Root should have neutral transforms'}
                }elseif($parent -ne '101'){throw 'Unexpected hierarchy'}
                $transforms[$go]=@{x=[double]$pos.Groups[1].Value;y=[double]$pos.Groups[2].Value;w=[double]$scale.Groups[1].Value;h=[double]$scale.Groups[2].Value;a=2*[Math]::Atan2([double]$rot.Groups[1].Value,[double]$rot.Groups[2].Value)}
            }
            '212' {
                if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Expected engine Square sprite'}
                $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
                $renderers+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([float]$c.Groups[1].Value,[float]$c.Groups[2].Value,[float]$c.Groups[3].Value,[float]$c.Groups[4].Value)}
            }
            default {throw "Unexpected collider/script/physics component $type"}
        }
    }
    if($rootCount -ne 1 -or $renderers.Count -ne $kind[2]){throw 'Unexpected root/renderer count'}
    $cx=160+($index%4)*320;$cy=145+[Math]::Floor($index/4)*340
    foreach($r in ($renderers|Sort-Object order)){
        $t=$transforms[$r.go];$c=[Math]::Cos($t.a);$s=[Math]::Sin($t.a);$points=@()
        foreach($v in @(@(-.5,-.5),@(.5,-.5),@(.5,.5),@(-.5,.5))){
            $x=$v[0]*$t.w;$y=$v[1]*$t.h
            $wx=$x*$c-$y*$s+$t.x;$wy=$x*$s+$y*$c+$t.y
            if([Math]::Abs($wx) -gt .72 -or [Math]::Abs($wy) -gt .72){throw 'Plant exceeds intended compact size'}
            $points+=[Drawing.PointF]::new([float]($cx+$wx*210),[float]($cy-$wy*210))
        }
        $col=$r.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$col[3]),[int](255*$col[0]),[int](255*$col[1]),[int](255*$col[2])))
        $g.FillPolygon($brush,[Drawing.PointF[]]$points);$brush.Dispose()
    }
    $g.DrawString($kind[1],$font,[Drawing.Brushes]::White,[float]($cx-95),[float]($cy+135))
    $g.DrawString(($renderers.Count.ToString()+' shapes'),$smallFont,[Drawing.Brushes]::LightGray,[float]($cx-95),[float]($cy+162))
    $total+=$renderers.Count
}
$path=Join-Path $root 'Docs/GeometricPlants-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$font.Dispose();$smallFont.Dispose()
Write-Output "Preview: $path"
Write-Output "PASS: 7 standalone prefabs, $total geometric sprites, unique GUIDs, valid references, neutral roots, normalized rotations, compact top-down bounds; no colliders or runtime scripts."
