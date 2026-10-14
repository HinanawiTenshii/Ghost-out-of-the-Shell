param([string]$BeforeScene='', [string]$PreviewPath='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$path=Join-Path $root 'Assets/Scenes/Level2/Level2-Floor-1.unity'
function ReadBlocks($text){
 $result=@{}
 foreach($p in [regex]::Matches($text,'(?ms)^--- !u!(\d+) &(\d+)[^\r\n]*\r?\n.*?(?=^--- !u!|\z)')){
  $id=$p.Groups[2].Value;if($result.ContainsKey($id)){throw "Duplicate scene fileID $id"};$result[$id]=$p.Value.Replace("`r`n","`n").TrimEnd()
 };return $result
}
$blocks=ReadBlocks (Get-Content -Raw $path)
$count=0
function Check($ok,$message){if(!$ok){throw $message};$script:count++}
function Vec($block,$name){$m=[regex]::Match($block,$name+': \{x: ([^,]+), y: ([^,]+), z: ([^,}]+)');@($m.Groups[1..3]|ForEach-Object{[double]::Parse($_.Value,[cultureinfo]::InvariantCulture)})}
function Bounds($block){
 $p=Vec $block 'm_LocalPosition';$s=Vec $block 'm_LocalScale';$q=Vec $block 'm_LocalRotation'
 if([Math]::Abs($q[2]) -gt .5){$s=@($s[1],$s[0],$s[2])}
 return @{x0=$p[0]-$s[0]/2;x1=$p[0]+$s[0]/2;y0=$p[1]-$s[1]/2;y1=$p[1]+$s[1]/2}
}
Check ($blocks['9327000000'].Contains('m_Name: Wide Water Borders')) 'Named border group exists'
Check ($blocks['9327000001'].Contains('m_Father: {fileID: 1978192927}')) 'Borders share Water transform but not an individual fading water object'
Check ($blocks['1978192927'].Contains('- {fileID: 9327000001}')) 'Water parent references bank group'
$reference=ReadBlocks (Get-Content -Raw "$root/Assets/Scenes/Level2/Level2-Floor1.unity")
$referenceColor=[regex]::Match($reference['578861996'],'m_Color: .*').Value
$referenceSprite=[regex]::Match($reference['578861996'],'m_Sprite: .*').Value
$water=@();$bank=@()
foreach($id in @('16903670','310845370','413915280','427646173','654378125','702626305','947534316','989116382','1011090386','1029985680','1159224588','1226548548','1285032860','1867053255','1912401457','2112728850')){
 $rect=Bounds $blocks[$id];$rect.id=$id;$water+=$rect
}
$wide=@($water|Where-Object {[Math]::Min($_.x1-$_.x0,$_.y1-$_.y0) -gt 1.5})
Check ($wide.Count -eq 12 -and $water.Count-$wide.Count -eq 4) 'Twelve broad surfaces selected; four one-unit branches excluded'
foreach($child in [regex]::Matches($blocks['9327000001'],'- \{fileID: (\d+)\}')){
 $tid=$child.Groups[1].Value;$tr=$blocks[$tid];$goid=[regex]::Match($tr,'m_GameObject: \{fileID: (\d+)').Groups[1].Value;$go=$blocks[$goid]
 $refs=@([regex]::Matches($go,'component: \{fileID: (\d+)')|ForEach-Object{$_.Groups[1].Value})
 Check ($refs.Count -eq 2 -and $refs[0] -eq $tid -and $blocks.ContainsKey($refs[1])) 'Bank is only Transform plus SpriteRenderer, with valid references'
 $r=$blocks[$refs[1]]
 Check ($r.StartsWith('--- !u!212 ') -and $r.Contains($referenceColor) -and $r.Contains($referenceSprite)) 'Bank reuses other-floor square sprite and grey-blue color'
 Check ($go.Contains('m_Layer: 0') -and $r.Contains('m_SortingOrder: -8')) 'World layer, below water/characters and above floor background'
 $b=Bounds $tr;$bank+=$b
 Check ([Math]::Min($b.x1-$b.x0,$b.y1-$b.y0) -le .30002) 'Bank thickness no greater than reference 0.3 world units'
 foreach($w in $water){
  $dx=[Math]::Min($b.x1,$w.x1)-[Math]::Max($b.x0,$w.x0);$dy=[Math]::Min($b.y1,$w.y1)-[Math]::Max($b.y0,$w.y0)
  Check ($dx -le .0001 -or $dy -le .0001) 'No bank enters water or caps a water junction'
 }
 $touching=@($wide|Where-Object {$b.x1 -ge $_.x0-.301 -and $b.x0 -le $_.x1+.301 -and $b.y1 -ge $_.y0-.301 -and $b.y0 -le $_.y1+.301})
 Check ($touching.Count -gt 0) 'Banks only belong to broad water outlines, not thin branch lengths'
}
Check ($bank.Count -eq 53) 'Bounded static geometry: 53 simple bank rectangles, no runtime generator'
foreach($w in $wide){Check (@($bank|Where-Object {$_.x1 -ge $w.x0-.301 -and $_.x0 -le $w.x1+.301 -and $_.y1 -ge $w.y0-.301 -and $_.y0 -le $w.y1+.301}).Count -gt 0) 'Every broad water surface is bordered'}
if($BeforeScene){
 $before=ReadBlocks (Get-Content -Raw $BeforeScene)
 foreach($id in $before.Keys){
  Check ($blocks.ContainsKey($id)) 'No original object removed'
  $actual=$blocks[$id]
  if($id -eq '1978192927'){$actual=$actual.Replace("  - {fileID: 9327000001}`n",'')}
  Check ($actual -eq $before[$id]) "Existing scene block $id remains unchanged (including water, thin branches, gates and bindings)"
 }
}
if($PreviewPath){
 Add-Type -AssemblyName System.Drawing
 $bmp=[Drawing.Bitmap]::new(1500,740);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(23,29,56))
 $rects=@($bank)+@($water)
 $minX=($rects.x0|Measure-Object -Minimum).Minimum;$maxX=($rects.x1|Measure-Object -Maximum).Maximum
 $minY=($rects.y0|Measure-Object -Minimum).Minimum;$maxY=($rects.y1|Measure-Object -Maximum).Maximum
 $scale=[Math]::Min(1420/($maxX-$minX),660/($maxY-$minY));$xOffset=40-$minX*$scale;$yOffset=40+$maxY*$scale
 $bankBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(119,133,178));$waterBrush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(11,100,250))
 foreach($group in @(@($bank,$bankBrush),@($water,$waterBrush))){foreach($r in $group[0]){$g.FillRectangle($group[1],[single]($xOffset+$r.x0*$scale),[single]($yOffset-$r.y1*$scale),[single](($r.x1-$r.x0)*$scale),[single](($r.y1-$r.y0)*$scale))}}
 $bmp.Save($PreviewPath,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bmp.Dispose();$bankBrush.Dispose();$waterBrush.Dispose()
}
Write-Output "PASS: $count scene geometry/reference/preservation checks (not Unity Play Mode)."
