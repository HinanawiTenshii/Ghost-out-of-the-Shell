$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path="$root/Assets/Prefabs/Decorations/Chair-Normal.prefab"
$text=Get-Content -Raw $path
if(!(Get-Content -Raw ($path+'.meta')).Contains('guid: 115a5b3bc09d59849ad65c8156395d20')){throw 'Prefab identity changed'}
if($text -match 'MonoBehaviour:|Collider2D:|[^\n]--- !u!'){throw 'Unexpected script, collision or invalid YAML boundary'}
$blocks=[regex]::Split($text,'(?m)(?=^--- !u!)')
$ids=@([regex]::Matches($text,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
if(@($ids|Group-Object|Where-Object Count -gt 1).Count){throw 'Duplicate object IDs'}
foreach($ref in [regex]::Matches($text,'\{fileID: (\d+)\}')){if($ref.Groups[1].Value -ne '0' -and $ids -notcontains $ref.Groups[1].Value){throw 'Broken reference'}}
$transforms=@{};$sprites=@()
foreach($b in $blocks){
 if($b -notmatch '^--- !u!(\d+) &(\d+)'){continue};$type=$Matches[1]
 $go=[regex]::Match($b,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
 if($type -eq '4'){
  if($b.Contains('m_Father: {fileID: 0}')){
   if(!$b.Contains('m_LocalPosition: {x: 0, y: 0, z: 0}') -or !$b.Contains('m_LocalScale: {x: 1, y: 1, z: 1}')){throw 'Root must use the centered origin'}
   continue
  }
  $p=[regex]::Match($b,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
  $s=[regex]::Match($b,'m_LocalScale: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)')
  if([double]$p.Groups[3].Value -ne 0 -or $b -notmatch 'm_LocalRotation: \{x: -?0, y: -?0, z: -?0, w: 1\}'){throw 'Depth or orientation changed'}
  $transforms[$go]=@{x=[double]$p.Groups[1].Value;y=[double]$p.Groups[2].Value;w=[double]$s.Groups[1].Value;h=[double]$s.Groups[2].Value}
 }
 if($type -eq '212'){
  if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6')){throw 'Non-Square geometry'}
  $c=[regex]::Match($b,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)')
  if([double]$c.Groups[4].Value -ne 1){throw 'Unexpected transparency'}
  $sprites+=@{go=$go;order=[int][regex]::Match($b,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
 }
}
if($sprites.Count -ne 5){throw 'Expected five rectangles'}
$back=$transforms['7361536954558299784']
if($back.x -lt .15 -or $back.h -ne .4){throw 'Backrest must stay on the original right side'}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(580,560);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(18,28,62))
foreach($r in ($sprites|Sort-Object order)){
 $t=$transforms[$r.go]
 if([Math]::Abs($t.x)+$t.w/2 -gt .2000001 -or [Math]::Abs($t.y)+$t.h/2 -gt .2000001){throw 'Visual exceeds original 0.4 x 0.4 footprint'}
 if($r.order -lt -4 -or $r.order -gt -2){throw 'Original sorting range changed'}
 $c=$r.color;$brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int](255*$c[0]),[int](255*$c[1]),[int](255*$c[2])))
 $g.FillRectangle($brush,[float](290+($t.x-$t.w/2)*850),[float](280-($t.y+$t.h/2)*850),[float]($t.w*850),[float]($t.h*850));$brush.Dispose()
}
$font=[Drawing.Font]::new('Segoe UI',15);$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(189,205,213))
$g.DrawString('Chair-Normal',$font,$label,222,47)
$small=[Drawing.Font]::new('Segoe UI',11);$g.DrawString('Top-down | right-side backrest | static prefab preview',$small,$label,115,503)
$small.Dispose();$font.Dispose();$label.Dispose()
$out="$root/Docs/GeometricChair-preview.png";$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose()
Write-Output 'PASS: 5 Square sprites; centered root, identity, original orientation, 0.4 x 0.4 footprint, sorting and no-collider behavior preserved.'
Write-Output "Preview: $out"
