# Read-only scene validation and a palette preview; does not save scenes or prefabs.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$guid='26d24882c78b7e349b78b028cf35cf9e'
$surfaceId='2972888045026333280'
$panelId='865400000000000002'
$records=@{}
function ReadTint($block,$id){
 $rgb=@()
 foreach($channel in 'r','g','b'){
  $matches=[regex]::Matches($block,('target: \{fileID: '+$id+', guid: '+$guid+', type: 3\}\s+propertyPath: m_Color\.'+$channel+'\s+value: ([^\r\n]+)'))
  if($matches.Count -ne 1){throw "Expected one $id/$channel color override"}
  $rgb+= [double]::Parse($matches[0].Groups[1].Value,[Globalization.CultureInfo]::InvariantCulture)
 }
 return ,$rgb
}
foreach($file in Get-ChildItem "$root/Assets/Scenes/Level1/*.unity"){
 foreach($block in [regex]::Split((Get-Content -Raw $file.FullName),'(?m)(?=^--- !u!)')){
  if($block -notmatch '^--- !u!1001 &(\d+)'){continue};$instance=$Matches[1]
  if($block -notmatch ('m_SourcePrefab: \{fileID: 100100000, guid: '+$guid)){continue}
  if($block -notmatch ('target: \{fileID: '+$surfaceId+', guid: '+$guid+', type: 3\}\s+propertyPath: m_Color\.[rgb]')){continue}
  $surface=ReadTint $block $surfaceId
  $panel=ReadTint $block $panelId
  foreach($i in 0..2){
   if([Math]::Abs($panel[$i]-($surface[$i]*.94+.06)) -gt .00000002){throw "Panel tint does not follow the existing tabletop: $($file.Name) / $instance"}
  }
  $records["$($file.Name)/$instance"]=@{surface=$surface;panel=$panel}
 }
}
if($records.Count -ne 13){throw "Expected 13 customized tabletops, found $($records.Count)"}

# Draw actual prefab rectangles with each scene's color overrides at a common scale.
# Scene placement/scale are intentionally not reproduced in this isolated palette preview.
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Table-Normal.prefab"
$transforms=@{};$sprites=@()
foreach($block in [regex]::Split($prefab,'(?m)(?=^--- !u!)')){
 if($block -notmatch '^--- !u!(\d+) &(\d+)'){continue}
 $type=$Matches[1];$id=$Matches[2]
 $go=[regex]::Match($block,'m_GameObject: \{fileID: (\d+)').Groups[1].Value
 if($type -eq '4'){
  $p=[regex]::Match($block,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+)')
  $s=[regex]::Match($block,'m_LocalScale: \{x: ([^,]+), y: ([^,]+)')
  $transforms[$go]=@{x=[double]$p.Groups[1].Value;y=[double]$p.Groups[2].Value;w=[double]$s.Groups[1].Value;h=[double]$s.Groups[2].Value}
 }
 if($type -eq '212'){
  $c=[regex]::Match($block,'m_Color: \{r: ([^,]+), g: ([^,]+), b: ([^,]+)')
  $sprites+=@{id=$id;go=$go;order=[int][regex]::Match($block,'m_SortingOrder: (-?\d+)').Groups[1].Value;color=@([double]$c.Groups[1].Value,[double]$c.Groups[2].Value,[double]$c.Groups[3].Value)}
 }
}
if(@($sprites|Where-Object id -eq $panelId).Count -ne 1){throw 'Panel override target missing from prefab'}
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(1300,480);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(18,28,62))
$font=[Drawing.Font]::new('Segoe UI',14)
$small=[Drawing.Font]::new('Segoe UI',11)
$label=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(209,220,233))
$samples=@(
 @('Floor1 / Purple','Level1-Floor1.unity/476065350'),
 @('Floor2 / Green','Level1-Floor2.unity/213343145'),
 @('Floor3 / Orange','Level1-Floor3.unity/1336527541'),
 @('Floor3 / Gold','Level1-Floor3.unity/1980699390')
)
$g.DrawString('Before: inherited teal panel',$font,$label,22,20)
$g.DrawString('After: panel follows the existing tabletop color (+6% lightness blend)',$font,$label,22,240)
foreach($column in 0..3){
 $record=$records[$samples[$column][1]]
 if(!$record){throw 'Preview sample missing'}
 $cx=170+$column*320
 foreach($row in 0..1){
  $cy=110+$row*220
  foreach($sprite in ($sprites|Sort-Object order)){
   $rgb=$sprite.color
   if($sprite.id -eq $surfaceId){$rgb=$record.surface}
   if($sprite.id -eq $panelId -and $row -eq 1){$rgb=$record.panel}
   $t=$transforms[$sprite.go]
   $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int](255*$rgb[0]),[int](255*$rgb[1]),[int](255*$rgb[2])))
   $g.FillRectangle($brush,[float]($cx+($t.x-.2582-$t.w/2)*210),[float]($cy-($t.y+.4947+$t.h/2)*210),[float]($t.w*210),[float]($t.h*210))
   $brush.Dispose()
  }
  $g.DrawString($samples[$column][0],$small,$label,[float]($cx-75),[float]($cy+64))
 }
}
$g.DrawString('Prefab geometry + saved scene colors; illustrative layout only, no CRT. Chair colors and wood frames are unchanged.',$small,$label,180,440)
$out="$root/Docs/Level1TableColors-preview.png"
$bmp.Save($out,[Drawing.Imaging.ImageFormat]::Png)
$label.Dispose();$small.Dispose();$font.Dispose();$g.Dispose();$bmp.Dispose()
Write-Output 'PASS: 13 customized Level1 tables have valid, unique RGB panel overrides matching their existing surface hues.'
Write-Output "Preview: $out"
