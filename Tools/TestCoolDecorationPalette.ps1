$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$manifest=Get-Content -Raw "$PSScriptRoot/CoolDecorationPalette.json"|ConvertFrom-Json
$reverse=@{}
foreach($entry in $manifest.colors.PSObject.Properties){$key=$entry.Value -join ',';if(!$reverse.ContainsKey($key)){$reverse[$key]=@($entry.Name.Split(',')|ForEach-Object{[double]$_})}}
$previewReverse=@{}
foreach($entry in $manifest.previousColors.PSObject.Properties){$key=$entry.Value -join ',';if(!$previewReverse.ContainsKey($key)){$previewReverse[$key]=@($entry.Name.Split(',')|ForEach-Object{[double]$_})}}
$count=0
foreach($path in $manifest.files){
 $allowed=@{};foreach($key in $reverse.Keys){$allowed[$key]=$true}
 $custom=$manifest.perFileColors.PSObject.Properties[$path]
 if($custom){foreach($entry in $custom.Value.PSObject.Properties){$allowed[$entry.Value -join ',']=$true}}
 $text=Get-Content -Raw (Join-Path $root $path)
 foreach($m in [regex]::Matches($text,'(?:m_Color|\w*[Cc]olor): \{r: ([\d.]+), g: ([\d.]+), b: ([\d.]+), a: ([\d.]+)\}')){
  $rgb=@([double]$m.Groups[1].Value,[double]$m.Groups[2].Value,[double]$m.Groups[3].Value)
  if(!$allowed.ContainsKey(($rgb -join ','))){throw "Color outside curated material palette: $path"}
  $count++
 }
}
# Vegetation, water and decorative colors stay saturated; stone/steel/linen should not.
foreach($key in @('0.28,0.38,0.22','0.27,0.4,0.43','0.4,0.32,0.47')){
 $rgb=$manifest.colors.$key
 $max=($rgb|Measure-Object -Maximum).Maximum
 $min=($rgb|Measure-Object -Minimum).Minimum
 if(($max-$min)/$max -lt .6 -or $max -lt .7){throw "Primary surface too muted or dim: $key"}
}
$fountain=Get-Content -Raw "$root/Assets/Prefabs/Decorations/喷泉.prefab"
$bed=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab"
foreach($color in @('m_Color: {r: 0.66, g: 0.68, b: 0.7, a: 1}','m_Color: {r: 0.43, g: 0.46, b: 0.49, a: 1}')){if(!$fountain.Contains($color) -or !$bed.Contains($color)){throw 'Stone palettes are inconsistent'}}
foreach($key in @('0.54,0.53,0.48','0.36,0.35,0.3','0.4,0.44,0.43')){
 $rgb=$manifest.colors.$key
 if((($rgb|Measure-Object -Maximum).Maximum-($rgb|Measure-Object -Minimum).Minimum) -gt .1){throw 'Stone/steel is not neutral enough'}
}
foreach($key in @('0.3,0.22,0.14','0.54,0.38,0.2','0.44,0.37,0.27','0.38,0.31,0.22')){
 $rgb=$manifest.colors.$key
 if($rgb[0] -lt $rgb[1]*1.35 -or $rgb[1] -lt $rgb[2]*1.35){throw 'Wood is not recognizably brown'}
}
$tree=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Plants/GeometricTree.prefab"
$sign=Get-Content -Raw "$root/Assets/Prefabs/Decorations/GeometricSignpost.prefab"
if(!$tree.Contains('trunkColor: {r: 0.55, g: 0.29, b: 0.14, a: 1}') -or !$sign.Contains('poleColor: {r: 0.55, g: 0.29, b: 0.14, a: 1}')){throw 'Tree/sign wooden supports mismatch'}
foreach($channel in @(@('r','0.32'),@('g','0.84'),@('b','0.98'),@('a','0.65'))){if($fountain -notmatch ('propertyPath: particleColor\.'+$channel[0]+'\s+value: '+[regex]::Escape($channel[1])+'\s')){throw 'Water particle tint/alpha mismatch'}}
Add-Type -AssemblyName System.Drawing
$cache=@{}
function Shapes($path){
 if($cache.ContainsKey($path)){return ,$cache[$path]}
 $blocks=[regex]::Split((Get-Content -Raw (Join-Path $root $path)),'(?m)(?=^--- !u!)')
 $tf=@{};$goTf=@{};$sprites=@()
 foreach($b in $blocks){
  if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1];$id=$Matches[2]
  $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
  if($type -eq '4' -and $go){
   $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
   $s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
   $r=[regex]::Match($b,'m_LocalRotation: \{x: -?0, y: -?0, z: ([^,]+), w: ([^}]+)')
   $tf[$id]=@{pos=@([double]$p.Groups[1].Value,[double]$p.Groups[2].Value);scale=@([double]$s.Groups[1].Value,[double]$s.Groups[2].Value);angle=2*[Math]::Atan2([double]$r.Groups[1].Value,[double]$r.Groups[2].Value);parent=[regex]::Match($b,'m_Father: \{fileID: (\d+)').Groups[1].Value}
   $goTf[$go]=$id
  }
  if($type -eq '212'){
   $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
   $sprites+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
  }
 }
 foreach($sprite in $sprites){
  $sprite.points=@(@(-.5,-.5),@(.5,-.5),@(.5,.5),@(-.5,.5)|ForEach-Object{
   $x=$_[0];$y=$_[1];$id=$goTf[$sprite.go]
   while($tf.ContainsKey($id) -and $tf[$id].parent -ne '0'){
    $t=$tf[$id];$sx=$x*$t.scale[0];$sy=$y*$t.scale[1];$c=[Math]::Cos($t.angle);$s=[Math]::Sin($t.angle)
    $x=$sx*$c-$sy*$s+$t.pos[0];$y=$sx*$s+$sy*$c+$t.pos[1];$id=$t.parent
   }
   ,@($x,$y)
  })
 }
 $cache[$path]=@($sprites|Sort-Object order);return ,$cache[$path]
}
$bmp=[Drawing.Bitmap]::new(1240,690);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62));$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
function DrawPrefab($path,$cx,$cy,$scale,$before){
 foreach($r in (Shapes $path)){
  $rgb=$r.color
  if($before){
   $key=$rgb -join ',';$custom=$manifest.perFileColors.PSObject.Properties[$path]
   $override=if($custom){@($custom.Value.PSObject.Properties|Where-Object{($_.Value -join ',') -eq $key})}else{@()}
   if($override.Count){$rgb=@($override[0].Name.Split(',')|ForEach-Object{[double]$_})}else{$rgb=$previewReverse[$key]}
  }
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$rgb[0]),[int](255*$rgb[1]),[int](255*$rgb[2])))
  $points=[Drawing.PointF[]]@($r.points|ForEach-Object{[Drawing.PointF]::new([float]($cx+$_[0]*$scale),[float]($cy-$_[1]*$scale))})
  $g.FillPolygon($brush,$points);$brush.Dispose()
 }
}
$font=[Drawing.Font]::new('Segoe UI',18);$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(195,211,229))
foreach($panel in 0..1){
 $cx=310+$panel*620;$before=$panel -eq 0
 $g.DrawString($(if($before){'Before: blue-dominant palette'}else{'After: material-based palette'}),$font,$label,[float]($cx-240),30)
 DrawPrefab 'Assets/Prefabs/Decorations/喷泉.prefab' $cx 225 270 $before
 DrawPrefab 'Assets/Prefabs/Decorations/Plants/GeometricFlowerbed.prefab' $cx 515 110 $before
 DrawPrefab 'Assets/Prefabs/Decorations/Plants/GeometricLeafCluster.prefab' ($cx-155) 515 108 $before
 DrawPrefab 'Assets/Prefabs/Decorations/Plants/GeometricGoldFlower.prefab' $cx 515 100 $before
 DrawPrefab 'Assets/Prefabs/Decorations/Plants/GeometricSucculent.prefab' ($cx+155) 515 115 $before
}
$small=[Drawing.Font]::new('Segoe UI',11)
$g.DrawString('Actual prefab geometry/colors on the same navy background | illustrative layout, no CRT or particle simulation',$small,$label,220,655)
$small.Dispose();$font.Dispose();$label.Dispose()
$out="$root/Docs/CoolDecorations-comparison.png";$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output "PASS: $($manifest.files.Count) decoration prefabs, $count curated RGB fields, matching stone palette, blue water particles with alpha retained."
Write-Output "Comparison: $out"
