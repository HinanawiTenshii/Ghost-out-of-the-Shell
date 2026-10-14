param([switch]$VerifyScene)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$layout=Get-Content -Raw "$PSScriptRoot/Level2BasementMaze.layout.json" | ConvertFrom-Json
$scene=Get-Content -Raw (Join-Path $root $layout.scene)
$map=@{}
foreach($b in [regex]::Split($scene,'(?m)(?=^--- !u!)')) {
 $m=[regex]::Match($b,'^--- !u!\d+ &(\d+)')
 if($m.Success){$id=$m.Groups[1].Value;if($map.ContainsKey($id)){throw "Duplicate scene id $id"};$map[$id]=$b}
}
$rects=[Collections.Generic.List[object]]::new()
$t=[double]$layout.wallThickness/2
foreach($line in $layout.polylines){for($i=1;$i -lt $line.points.Count;$i++){
 $a=[double[]]$line.points[$i-1];$b=[double[]]$line.points[$i]
 if($a[0] -ne $b[0] -and $a[1] -ne $b[1]){throw 'Non-orthogonal wall'}
 $rects.Add([pscustomobject]@{name="$($line.name) $i";rect=@(([math]::Min($a[0],$b[0])-$t),([math]::Min($a[1],$b[1])-$t),([math]::Max($a[0],$b[0])+$t),([math]::Max($a[1],$b[1])+$t))})
}}
foreach($block in $layout.blocks){$rects.Add($block)}
# Match the actual room boundary walls, including the existing door/exit gaps.
$context=[Collections.Generic.List[object]]::new()
foreach($b in $map.Values){
 if($b -notmatch '^--- !u!4 &' -or !$b.Contains('m_Father: {fileID: 869392058}')){continue}
 $goId=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
 $components=@([regex]::Matches($map[$goId],'component: \{fileID: (\d+)') | ForEach-Object {$map[$_.Groups[1].Value]})
 if(!($components | Where-Object {$_ -match '^--- !u!61 &'})){continue}
 $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+)')
 $s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+)')
 $q=[regex]::Match($b,'m_LocalRotation: \{x: [^,]+, y: [^,]+, z: ([^,]+), w: ([^}]+)')
 $x=[double]$p.Groups[1].Value-3.8829699-$layout.origin[0];$y=[double]$p.Groups[2].Value-.14688715-$layout.origin[1]
 $angle=2*[math]::Atan2([double]$q.Groups[1].Value,[double]$q.Groups[2].Value)
 $sx=[math]::Abs([double]$s.Groups[1].Value);$sy=[math]::Abs([double]$s.Groups[2].Value)
 $w=[math]::Abs([math]::Cos($angle))*$sx+[math]::Abs([math]::Sin($angle))*$sy
 $h=[math]::Abs([math]::Sin($angle))*$sx+[math]::Abs([math]::Cos($angle))*$sy
 if($x+$w/2 -gt -.5 -and $x-$w/2 -lt $layout.size[0]+.5 -and $y+$h/2 -gt -.5 -and $y-$h/2 -lt $layout.size[1]+.5){
  $context.Add([pscustomobject]@{name='Existing boundary';rect=@(($x-$w/2),($y-$h/2),($x+$w/2),($y+$h/2))})
 }
}
Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
public static class MazeConnectivity {
 public static string Check(double[] bounds, double[,] walls, double radius, double[] entrance, double[] exit, double[] reward) {
  const double step=.1; int nx=(int)Math.Ceiling(bounds[0]/step),ny=(int)Math.Ceiling(bounds[1]/step);
  bool[] open=new bool[nx*ny],visited=new bool[nx*ny];int[] dist=new int[nx*ny];int total=0;
  for(int y=0;y<ny;y++)for(int x=0;x<nx;x++){
   double px=(x+.5)*step,py=(y+.5)*step;bool blocked=false;
   for(int w=0;w<walls.GetLength(0);w++)if(px>walls[w,0]-radius&&px<walls[w,2]+radius&&py>walls[w,1]-radius&&py<walls[w,3]+radius){blocked=true;break;}
   if(!blocked){open[y*nx+x]=true;total++;}
  }
  Func<double[],int> cell=p=>Math.Min(ny-1,Math.Max(0,(int)(p[1]/step)))*nx+Math.Min(nx-1,Math.Max(0,(int)(p[0]/step)));
  int start=cell(entrance),finish=cell(exit),prize=cell(reward);
  if(!open[start]||!open[finish]||!open[prize])throw new Exception("Entrance, exit or reward is blocked at radius "+radius);
  var queue=new Queue<int>();queue.Enqueue(start);visited[start]=true;int count=0;
  while(queue.Count>0){int p=queue.Dequeue();count++;int x=p%nx,y=p/nx;
   foreach(int n in new[]{x>0?p-1:-1,x+1<nx?p+1:-1,y>0?p-nx:-1,y+1<ny?p+nx:-1}){
    if(n<0||!open[n]||visited[n])continue;visited[n]=true;dist[n]=dist[p]+1;queue.Enqueue(n);
   }
  }
  if(!visited[finish]||!visited[prize])throw new Exception("Exit/reward unreachable at radius "+radius);
  if(count!=total)throw new Exception("Disconnected traversable area: "+(total-count)+" cells at radius "+radius);
  return "PASS radius="+radius+" reachable="+count+" entrance-to-exit="+(dist[finish]*step).ToString("F1")+" entrance-to-reward="+(dist[prize]*step).ToString("F1");
 }
}
'@
$all=@($rects.ToArray())+@($context.ToArray())
$walls=[double[,]]::new($all.Count,4)
for($i=0;$i -lt $all.Count;$i++){for($j=0;$j -lt 4;$j++){$walls[$i,$j]=$all[$i].rect[$j]}}
foreach($radius in @(.1,.4,.55)){[MazeConnectivity]::Check([double[]]$layout.size,$walls,$radius,[double[]]$layout.entrance,[double[]]$layout.exit,[double[]]$layout.reward)}
if($VerifyScene){
 $group=$map['9337000000'];$transform=$map['9337000001']
 if($group -notmatch 'm_Name: Ossuary Maze' -or $transform -notmatch 'm_Father: \{fileID: 869392058\}'){throw 'Missing maze parent'}
 if([regex]::Matches($transform,'^  - \{fileID:',[Text.RegularExpressions.RegexOptions]::Multiline).Count -ne $rects.Count){throw 'Unexpected wall count'}
 for($i=0;$i -lt $rects.Count;$i++){
  $id=[long]9337000010+$i*4;$go=$map["$id"];$tr=$map["$($id+1)"];$col=$map["$($id+2)"];$sr=$map["$($id+3)"]
  if($go -notmatch 'm_Layer: 7' -or $go -notmatch 'm_IsActive: 1' -or $col -notmatch 'm_IsTrigger: 0' -or $col -notmatch 'm_Enabled: 1'){throw "Bad wall/collider: $id"}
  if($sr -notmatch 'guid: 311925a002f4447b3a28927169b83ea6' -or $sr -notmatch 'm_Color: \{r: 1, g: 1, b: 1, a: 1\}' -or $sr -notmatch 'm_SortingOrder: 0'){throw 'Wall style mismatch'}
  if($tr -notmatch 'm_Father: \{fileID: 9337000001\}' -or $tr -notmatch 'm_LocalRotation: \{x: 0, y: 0, z: 0, w: 1\}'){throw 'Wrong wall parent or rotation'}
  $p=[regex]::Match($tr,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+)');$s=[regex]::Match($tr,'m_LocalScale: \{x: ([^,]+), y: ([^,]+)')
  $r=$rects[$i].rect;$expected=@((($r[0]+$r[2])/2),(($r[1]+$r[3])/2),($r[2]-$r[0]),($r[3]-$r[1]))
  $actual=@([double]$p.Groups[1].Value,[double]$p.Groups[2].Value,[double]$s.Groups[1].Value,[double]$s.Groups[2].Value)
  for($j=0;$j -lt 4;$j++){if([math]::Abs($expected[$j]-$actual[$j]) -gt .0001){throw "Geometry mismatch $id"}}
 }
 Write-Output "PASS: $($rects.Count) editable scene walls match layout, Blocks collision and existing wall appearance."
}
# A scaled top-down preview of the same rectangles used for collision tests.
Add-Type -AssemblyName System.Drawing
$scale=36;$margin=50;$width=[int]($layout.size[0]*$scale+2*$margin);$height=[int]($layout.size[1]*$scale+2*$margin+40)
$bmp=[Drawing.Bitmap]::new($width,$height);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(11,20,54))
$grid=[Drawing.Pen]::new([Drawing.Color]::FromArgb(35,120,170,210),1)
for($x=0;$x -le $layout.size[0];$x++){$px=[single]($margin+$x*$scale);$g.DrawLine($grid,$px,[single]$margin,$px,[single]($height-$margin))}
for($y=0;$y -le $layout.size[1];$y++){$py=[single]($margin+($layout.size[1]-$y)*$scale);$g.DrawLine($grid,[single]$margin,$py,[single]($width-$margin),$py)}
$clip=[Drawing.RectangleF]::new($margin-16,$margin-16,[single]($layout.size[0]*$scale+32),[single]($layout.size[1]*$scale+32));$g.SetClip($clip)
$white=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(238,244,248))
foreach($wall in $all){$r=$wall.rect;$x=[single]($margin+$r[0]*$scale);$y=[single]($margin+($layout.size[1]-$r[3])*$scale);$w=[single](($r[2]-$r[0])*$scale);$h=[single](($r[3]-$r[1])*$scale);$g.FillRectangle($white,$x,$y,$w,$h)}
$g.ResetClip();$font=[Drawing.Font]::new('Microsoft YaHei',11);$cyan=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(80,235,246))
foreach($mark in @(@('入口',$layout.entrance),@('出口',$layout.exit),@('奖励区预留',$layout.reward))){$p=$mark[1];$x=[single]($margin+$p[0]*$scale-25);$y=[single]($margin+($layout.size[1]-$p[1])*$scale-10);$g.DrawString($mark[0],$font,$cyan,$x,$y)}
$g.DrawString('Level2-Floor-1  /  简化迷宫  /  1 格 = 1 单位',$font,$cyan,[single]$margin,[single]($height-30))
$output=Join-Path $root 'Docs/Level2BasementMaze-preview.png';$bmp.Save($output,[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$grid.Dispose();$white.Dispose();$font.Dispose();$cyan.Dispose()
Write-Output "Preview: $output"
