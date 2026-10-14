$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$source=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Bench-Normal.prefab"
$chair=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Chair-Normal.prefab"
$blocks=[regex]::Matches($source,'(?ms)^--- !u!(\d+) &(\d+)\r?\n(.*?)(?=^--- !u!|\z)')
$ids=@{};foreach($block in $blocks){if($ids.ContainsKey($block.Groups[2].Value)){throw 'Duplicate file ID'};$ids[$block.Groups[2].Value]=$true}
foreach($block in $blocks){if([int]$block.Groups[1].Value -notin @(1,4,212)){throw 'Bench must use only GameObject, Transform and SpriteRenderer: no physics or scripts'}}
if(@($blocks|Where-Object {$_.Groups[1].Value -eq '212'}).Count -ne 5){throw 'Expected five simple rectangles'}
foreach($match in [regex]::Matches($source,'\{fileID: (\d+)\}')){if($match.Groups[1].Value -ne '0' -and !$ids.ContainsKey($match.Groups[1].Value)){throw "Missing local reference $match"}}
function Vector($text,$key){
 $match=[regex]::Match($text,"${key}: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}")
 if(!$match.Success){throw "Missing $key"};@([double]$match.Groups[1].Value,[double]$match.Groups[2].Value,[double]$match.Groups[3].Value)
}
function Parts($yaml){
 $parts=@()
 foreach($render in [regex]::Matches($yaml,'(?ms)^--- !u!212 &\d+\r?\n(.*?)(?=^--- !u!|\z)')){
  $go=[regex]::Match($render.Value,'m_GameObject: \{fileID: (\d+)\}').Groups[1].Value
  $trans=[regex]::Matches($yaml,'(?ms)^--- !u!4 &\d+\r?\n(.*?)(?=^--- !u!|\z)')|Where-Object {$_.Value.Contains("m_GameObject: {fileID: $go}")}
  $color=[regex]::Match($render.Value,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+), a: ([^}]+)\}')
  $parts+=@{pos=(Vector $trans.Value 'm_LocalPosition');scale=(Vector $trans.Value 'm_LocalScale');color=@([double]$color.Groups[1].Value,[double]$color.Groups[2].Value,[double]$color.Groups[3].Value);order=[int][regex]::Match($render.Value,'m_SortingOrder: (-?\d+)').Groups[1].Value}
  foreach($key in @('m_Materials','m_Sprite','m_MaskInteraction','m_Color','m_SortingOrder')){
   if($key -eq 'm_Materials'){continue}
   $line=[regex]::Match($render.Value,"${key}: [^\r\n]+").Value
   if(!$chair.Contains($line)){throw "Chair style mismatch: $line"}
  }
 }
 return $parts
}
$parts=Parts $source
$rootTransform=($blocks|Where-Object {$_.Groups[1].Value -eq '4' -and $_.Value.Contains('m_Father: {fileID: 0}')}).Value
if((Vector $rootTransform 'm_LocalPosition'|Where-Object {$_ -ne 0}).Count -ne 0){throw 'Root must be at origin'}
if((Vector $rootTransform 'm_LocalScale'|Where-Object {$_ -ne 1}).Count -ne 0){throw 'Root must have unit scale'}
$minX=100.0;$maxX=-100.0;$minY=100.0;$maxY=-100.0
foreach($p in $parts){$minX=[Math]::Min($minX,$p.pos[0]-$p.scale[0]/2);$maxX=[Math]::Max($maxX,$p.pos[0]+$p.scale[0]/2);$minY=[Math]::Min($minY,$p.pos[1]-$p.scale[1]/2);$maxY=[Math]::Max($maxY,$p.pos[1]+$p.scale[1]/2)}
if([Math]::Abs($minX+$maxX) -gt .00001 -or [Math]::Abs($minY+$maxY) -gt .00001){throw 'Root not centered on visual bounds'}
if([Math]::Abs(($maxX-$minX)-1.2) -gt .00001 -or [Math]::Abs(($maxY-$minY)-.4) -gt .00001){throw 'Expected three-chair-wide bench with unchanged depth'}
Add-Type -AssemblyName System.Drawing
$bitmap=[Drawing.Bitmap]::new(740,270);$g=[Drawing.Graphics]::FromImage($bitmap)
$g.Clear([Drawing.Color]::FromArgb(24,31,43));$font=[Drawing.Font]::new('Arial',12)
$entries=@(@{label='Chair / existing';parts=(Parts $chair);x=140},@{label='Bench / new';parts=$parts;x=490})
foreach($entry in $entries){
 foreach($p in ($entry.parts|Sort-Object order)){
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(255,[int]($p.color[0]*255),[int]($p.color[1]*255),[int]($p.color[2]*255)))
  $g.FillRectangle($brush,[single]($entry.x+($p.pos[0]-$p.scale[0]/2)*300),[single](125-($p.pos[1]+$p.scale[1]/2)*300),[single]($p.scale[0]*300),[single]($p.scale[1]*300));$brush.Dispose()
 }
 $g.DrawString($entry.label,$font,[Drawing.Brushes]::White,[single]($entry.x-65),[single]215)
}
$preview=Join-Path $root '.codex-temp/Bench-preview.png'
$bitmap.Save($preview,[Drawing.Imaging.ImageFormat]::Png);$font.Dispose();$g.Dispose();$bitmap.Dispose()
Write-Output "PASS: 5 rectangles, valid local references, no collision/physics/scripts, Chair palette/material/sprite/sorting, centered 1.2 x 0.4 footprint. Geometry preview (not Unity render): $preview"
