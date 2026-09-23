# Checks real prefab data and draws flat geometric previews, without Unity runtime code.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$specs=@(
 @{name='Chest';guid='f9b7927c21844bb408da11427ec9a1f3';count=6;center=@(.020942688,.037400007);size=@(1.1,2.038756);anchor=@(205,260);zoom=140;colliders=1},
 @{name='Table-Normal';guid='26d24882c78b7e349b78b028cf35cf9e';count=4;center=@(0,0);size=@(1.2232358,.45177525);anchor=@(575,260);zoom=240;colliders=1},
 @{name='Shelf';guid='be9eb61e24393094e87eac21c4fd9a39';count=8;center=@(-3.61597,-.5921262);size=@(2.8259516,4.353442);anchor=@(985,260);zoom=74;colliders=1},
 @{name='WorkTable';guid='4151f15e9bcf1714aa52314794c0f3a6';count=8;center=@(.104400635,.0032999516);size=@(3.0639536,1.0955672);anchor=@(350,635);zoom=165;colliders=1},
 @{name='Doll';guid='1b65057ea399c384daceff699709ebba';count=10;center=@(.50290996,-.7000649);size=@(2.17,2.32);anchor=@(945,628);zoom=115;colliders=0}
)
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1220,830);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62));$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$font=[Drawing.Font]::new('Segoe UI',15);$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(189,205,213))
foreach($spec in $specs){
 $path="$root/Assets/Prefabs/Decorations/$($spec.name).prefab";$text=Get-Content -Raw $path
 if(!(Get-Content -Raw ($path+'.meta')).Contains('guid: '+$spec.guid)){throw 'Prefab identity changed'}
 if($text -match '[^\n]--- !u!'){throw 'Malformed YAML document boundary'}
 $blocks=[regex]::Split($text,'(?m)(?=^--- !u!)')
 $ids=@([regex]::Matches($text,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
 if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate object IDs'}
 foreach($ref in [regex]::Matches($text,'\{fileID: (\d+)\}')){if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken local reference'}}
 $transforms=@{};$sprites=@();$colliders=@();$rootTf=$null
 foreach($b in $blocks){
  if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
  $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
  if($type -eq '4'){
   if($b.Contains('m_Father: {fileID: 0}')){$rootTf=$b;continue}
   $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
   $s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
   $r=[regex]::Match($b,'m_LocalRotation: \{x: -?0, y: -?0, z: ([^,]+), w: ([^}]+)')
   if(!$r.Success){throw 'Unexpected 3D rotation'}
   $angle=2*[Math]::Atan2([double]$r.Groups[1].Value,[double]$r.Groups[2].Value)
   $transforms[$go]=@{pos=@([double]$p.Groups[1].Value,[double]$p.Groups[2].Value);size=@([double]$s.Groups[1].Value,[double]$s.Groups[2].Value);angle=$angle}
  }
  if($type -eq '212'){
   if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'All new geometry should use existing Square sprites'}
   $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
   if([double]$c.Groups[4].Value -ne 1){throw 'Unexpected transparent furniture'}
   $sprites+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
  }
  if($type -eq '61'){
   if(!$b.Contains('m_IsTrigger: 0') -or !$b.Contains('m_Enabled: 1') -or !$b.Contains('m_Size: {x: 1, y: 1}') -or !$b.Contains('m_Offset: {x: 0, y: 0}')){throw 'Original solid unit box changed'}
   $colliders+=,$go
  }
 }
 if(!$rootTf -or $sprites.Count -ne $spec.count -or $colliders.Count -ne $spec.colliders){throw 'Structure mismatch'}
 if($spec.name -eq 'Table-Normal' -and !$rootTf.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}')){throw 'Table root must use the centered origin'}
 foreach($go in $colliders){
  $t=$transforms[$go]
  foreach($axis in 0..1){if([Math]::Abs($t.pos[$axis]-$spec.center[$axis]) -gt 1e-7 -or [Math]::Abs($t.size[$axis]-$spec.size[$axis]) -gt 1e-7){throw 'Collision transform changed'}}
 }
 $scripts=@($blocks|Where-Object{$_ -match '^--- !u!114 '})
 if($spec.name -eq 'Doll'){
  if($scripts.Count -ne 1 -or !$scripts[0].Contains('guid: 6875f823f3014169bd940be0424872d5') -or !$scripts[0].Contains('shakeDuration: 0.18') -or !$scripts[0].Contains('shakeAmount: 0.09') -or !$scripts[0].Contains('shakeSpeed: 80')){throw 'Doll hit-shake behavior changed'}
 }elseif($scripts.Count){throw 'Unexpected new runtime script'}
 foreach($sprite in ($sprites|Sort-Object order)){
  $t=$transforms[$sprite.go];$dx=$t.pos[0]-$spec.center[0];$dy=$t.pos[1]-$spec.center[1]
  $cos=[Math]::Cos($t.angle);$sin=[Math]::Sin($t.angle)
  $points=[Drawing.PointF[]]@(@(-.5,-.5),@(.5,-.5),@(.5,.5),@(-.5,.5)|ForEach-Object{
   $x=$_[0]*$t.size[0];$y=$_[1]*$t.size[1];$rx=$dx+$x*$cos-$y*$sin;$ry=$dy+$x*$sin+$y*$cos
   if([Math]::Abs($rx) -gt $spec.size[0]/2+1e-6 -or [Math]::Abs($ry) -gt $spec.size[1]/2+1e-6){throw "Visual outside footprint: $($spec.name)"}
   [Drawing.PointF]::new([float]($spec.anchor[0]+$rx*$spec.zoom),[float]($spec.anchor[1]-$ry*$spec.zoom))
  })
  $c=$sprite.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
  $g.FillPolygon($brush,$points);$brush.Dispose()
 }
 $labelY=if($spec.anchor[1] -lt 400){50}else{468}
 $g.DrawString($spec.name,$font,$label,[float]($spec.anchor[0]-65),[float]$labelY)
 Write-Output "PASS: $($spec.name) - $($sprites.Count) rectangles; original identity, collision and behavior preserved."
}
$small=[Drawing.Font]::new('Segoe UI',11)
$g.DrawString('Geometric furniture | fitted per panel (not equal world scale) | static prefab preview',$small,$label,300,793)
$small.Dispose();$font.Dispose();$label.Dispose()
$out="$root/Docs/GeometricFurniture-preview.png";$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output "Preview: $out"
