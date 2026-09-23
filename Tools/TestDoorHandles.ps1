$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
foreach($name in 'DoorHinge','DoubleDoorHinge'){
 $text=Get-Content -Raw "$root/Assets/Prefabs/Decorations/$name.prefab"
 $blocks=[regex]::Split($text,'(?m)(?=^--- !u!)')
 $count=if($name -eq 'DoorHinge'){2}else{4}
 $handles=@($blocks|Where-Object{$_ -match '^--- !u!4 &866100'})
 if($handles.Count -ne $count){throw 'Must have exactly two handle rectangles per leaf'}
 $ids=@([regex]::Matches($text,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value})
 if(@($ids|Select-Object -Unique).Count -ne $ids.Count){throw 'Duplicate ID'}
 foreach($r in [regex]::Matches($text,'\{fileID: (\d+)\}')){
  if($r.Groups[1].Value -ne '0' -and $ids -notcontains $r.Groups[1].Value){throw 'Dangling local reference'}
 }
 foreach($group in ($handles|Group-Object{[regex]::Match($_,'m_Father: \{fileID: (\d+)').Groups[1].Value})){
  if($group.Count -ne 2){throw 'Handles must share their own leaf parent'}
  $xs=@();$ys=@()
  foreach($h in $group.Group){
   $m=[regex]::Match($h,'m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: 0\}')
   if(!$m.Success){throw 'Invalid handle position'}
   $xs+=[double]$m.Groups[1].Value;$ys+=[double]$m.Groups[2].Value
   if(!$h.Contains('m_LocalScale: {x: 0.055, y: 0.5, z: 1}')){throw 'Handle too large'}
   $id=[regex]::Match($h,'^--- !u!4 &(\d+)').Groups[1].Value
   if([regex]::Matches($text,('  - \{fileID: '+$id+'\}')).Count -ne 2){throw 'Missing leaf/shake attachment'}
  }
  if($xs[0] -ne $xs[1] -or [Math]::Abs($xs[0]) -ne .39 -or [Math]::Abs($ys[0]) -ne .65 -or $ys[0]+$ys[1] -ne 0){throw 'Handles must mirror across long sides at free end'}
 }
 if(@($blocks|Where-Object{$_ -match '^--- !u!61 '}).Count -ne $count/2){throw 'Handle must not add collision'}
 foreach($b in $blocks|Where-Object{$_ -match '^--- !u!212 &866100'}){
  if(!$b.Contains('guid: 311925a002f4447b3a28927169b83ea6') -or !$b.Contains('guid: 0000000000000000f000000000000000')){throw 'Handles must use built-in Square/default sprite material'}
 }
}
$hinge=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorHingeInteraction.cs"
$data=Get-Content -Raw "$root/Assets/Scripts/Interaction/DoorData.cs"
if(!$hinge.Contains('!leaf.IsAttachedVisual(candidates[i].transform)')){throw 'Hardware must not enter interaction bounds'}
if(!$data.Contains('OffsetAttachedVisuals(shakeVisual.transform.localPosition);') -or !$data.Contains('OffsetAttachedVisuals(Vector3.zero);')){throw 'Hardware must follow/reset hit shake'}
$shader=Get-Content -Raw "$root/Assets/Shaders/MinimalDoorSurface.shader"
if($shader -match '_HardwareColor|float latch'){throw 'Old painted handle still present'}
Write-Output 'PASS: two small built-in rectangles per leaf; mirrored placement; no added physics; excluded from proximity bounds; follows hit shake; painted latch removed.'
