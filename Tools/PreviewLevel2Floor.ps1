param([float]$X=0,[float]$Y=0,[float]$Width=0,[float]$Height=0,[switch]$Labels,[switch]$NoGrid)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scene=Get-Content -Raw "$root/Assets/Scenes/Level2/Level2-Floor1.unity"
$blocks=[regex]::Split($scene,'(?m)(?=^--- !u!)')
$transforms=@{};$objects=@{};$renderers=@();$goTransforms=@{}
function FieldVector($b,$key) {
    $m=[regex]::Match($b,"${key}: \{x: ([^,]+), y: ([^,]+), z: ([^,}]+)")
    if(!$m.Success){return @(0,0,0)}
    return @([double]$m.Groups[1].Value,[double]$m.Groups[2].Value,[double]$m.Groups[3].Value)
}
foreach($b in $blocks){
    if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
    $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
    switch($type){
        '1' {$objects[$id]=[regex]::Match($b,'m_Name: (.*)').Groups[1].Value.Trim()}
        '4' {
            $rot=[regex]::Match($b,'m_LocalRotation: \{x: [^,]+, y: [^,]+, z: ([^,]+), w: ([^}]+)')
            if(!$rot.Success){continue}
            $transforms[$id]=@{pos=(FieldVector $b 'm_LocalPosition');scale=(FieldVector $b 'm_LocalScale');angle=(2*[Math]::Atan2([double]$rot.Groups[1].Value,[double]$rot.Groups[2].Value));parent=[regex]::Match($b,'m_Father: \{fileID: (\d+)').Groups[1].Value}
            $goTransforms[$go]=$id
        }
        '212' {
            $m=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
            if(!$m.Success){continue}
            $renderers+=@{id=$id;go=$go;color=@([float]$m.Groups[1].Value,[float]$m.Groups[2].Value,[float]$m.Groups[3].Value,[float]$m.Groups[4].Value);order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;tiles=$b.Contains('89b6848b921f4c24a274326a5f098cad')}
        }
    }
}
function WorldPoint($id,[double]$px,[double]$py,[double]$pz=0){
    while($transforms.ContainsKey($id)){
        $t=$transforms[$id];$sx=$px*$t.scale[0];$sy=$py*$t.scale[1];$c=[Math]::Cos($t.angle);$s=[Math]::Sin($t.angle)
        $px=$sx*$c-$sy*$s+$t.pos[0];$py=$sx*$s+$sy*$c+$t.pos[1];$pz=$pz*$t.scale[2]+$t.pos[2];$id=$t.parent
    }
    return @($px,$py,$pz)
}
foreach($r in $renderers){
    $tid=$goTransforms[$r.go];$r.tid=$tid
    $r.corners=@((WorldPoint $tid -.5 -.5),(WorldPoint $tid .5 -.5),(WorldPoint $tid .5 .5),(WorldPoint $tid -.5 .5))
    $r.center=WorldPoint $tid 0 0;$r.depth=$r.center[2]
}
if($Width -le 0){$X=-140;$Y=-100;$Width=330;$Height=240}
Add-Type -AssemblyName System.Drawing
$scale=[Math]::Min(1600/$Width,1200/$Height)
$bmp=[Drawing.Bitmap]::new([int]($Width*$scale),[int]($Height*$scale));$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(68,68,68));$font=[Drawing.Font]::new('Arial',8)
$tileMat=Get-Content -Raw "$root/Assets/Materials/Level2CorridorTiles.mat"
$tileSize=[float][regex]::Match($tileMat,'_TileSize: ([\d.]+)').Groups[1].Value
$grid=[regex]::Match($tileMat,'_GridOrigin: \{r: ([^,]+), g: ([^,]+)')
$originX=[float]$grid.Groups[1].Value;$originY=[float]$grid.Groups[2].Value
$tileColors=@('DarkTile','LightTile')|ForEach-Object{
    $match=[regex]::Match($tileMat,"_${_}: \{r: ([^,]+), g: ([^,]+), b: ([^,]+)")
    [Drawing.Color]::FromArgb([int](255*[float]$match.Groups[1].Value),[int](255*[float]$match.Groups[2].Value),[int](255*[float]$match.Groups[3].Value))
}
foreach($r in ($renderers | Sort-Object order,@{Expression='depth';Descending=$true})){
    $pts=[Drawing.PointF[]]@($r.corners|ForEach-Object{[Drawing.PointF]::new([float](($_[0]-$X)*$scale),[float](($Y+$Height-$_[1])*$scale))})
    $col=$r.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$col[3]),[int](255*$col[0]),[int](255*$col[1]),[int](255*$col[2])))
    $g.FillPolygon($brush,$pts);$brush.Dispose()
    if($r.tiles){
        $state=$g.Save();$clip=[Drawing.Drawing2D.GraphicsPath]::new();$clip.AddPolygon($pts);$g.SetClip($clip)
        $minX=($r.corners|ForEach-Object{$_[0]}|Measure-Object -Minimum).Minimum
        $maxX=($r.corners|ForEach-Object{$_[0]}|Measure-Object -Maximum).Maximum
        $minY=($r.corners|ForEach-Object{$_[1]}|Measure-Object -Minimum).Minimum
        $maxY=($r.corners|ForEach-Object{$_[1]}|Measure-Object -Maximum).Maximum
        for($ix=[int][Math]::Floor(($minX-$originX)/$tileSize);$originX+$ix*$tileSize -lt $maxX;$ix++){
            for($iy=[int][Math]::Floor(($minY-$originY)/$tileSize);$originY+$iy*$tileSize -lt $maxY;$iy++){
                $parity=(($ix+$iy)%2+2)%2;$tileBrush=[Drawing.SolidBrush]::new($tileColors[$parity])
                $g.FillRectangle($tileBrush,[float](($originX+$ix*$tileSize-$X)*$scale),[float](($Y+$Height-$originY-($iy+1)*$tileSize)*$scale),[float]($tileSize*$scale+.5),[float]($tileSize*$scale+.5));$tileBrush.Dispose()
            }
        }
        $g.Restore($state);$clip.Dispose()
    }
    if($Labels -and $r.center[0] -ge $X -and $r.center[0] -le ($X+$Width) -and $r.center[1] -ge $Y -and $r.center[1] -le ($Y+$Height)){
        $g.DrawString($r.go,$font,[Drawing.Brushes]::Lime,[float](($r.center[0]-$X)*$scale),[float](($Y+$Height-$r.center[1])*$scale))
        Write-Output "$($r.go) $($objects[$r.go]) transform=$($r.tid) center=$($r.center) localScale=$($transforms[$r.tid].scale) color=$col order=$($r.order)"
    }
}
$pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(55,190,190,190))
if(!$NoGrid){
    for($a=[Math]::Ceiling($X/10)*10;$a -lt $X+$Width;$a+=10){$px=[float](($a-$X)*$scale);$g.DrawLine($pen,$px,0,$px,$bmp.Height);$g.DrawString("$a",$font,[Drawing.Brushes]::Orange,$px,0)}
    for($a=[Math]::Ceiling($Y/10)*10;$a -lt $Y+$Height;$a+=10){$py=[float](($Y+$Height-$a)*$scale);$g.DrawLine($pen,0,$py,$bmp.Width,$py);$g.DrawString("$a",$font,[Drawing.Brushes]::Orange,0,$py)}
}
$path=Join-Path $root 'Docs/Level2-Floor1-layout-preview.png'
$bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$font.Dispose();$pen.Dispose()
Write-Output $path
