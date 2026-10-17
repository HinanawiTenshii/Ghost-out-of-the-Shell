$ErrorActionPreference='Stop'
. "$PSScriptRoot/TestFortressInteriors.ps1"
$root=Split-Path $PSScriptRoot -Parent
$checks=0
$kind=[InteriorProbe+FurnitureShape]::Trapdoor
Check ([int]$kind -eq 5) 'New enum appended without renumbering older furniture'
$parts=[InteriorProbe]::BuildParts($kind,[Color]::new(.67,.39,.20,1),[Color]::new(.67,.71,.74,1),[Color]::new(.22,.57,.56,1))
$size=[InteriorProbe]::ReferenceSize($kind)
Check ($size.x -gt 1.79 -and $size.x -lt 1.81 -and $size.y -eq $size.x) 'Square 1.8m centered footprint'
$vertices=0
foreach($part in $parts){
 $vertices+=$part.Points.Length
 foreach($point in $part.Points){Check ([Math]::Abs($point.x) -le .90001 -and [Math]::Abs($point.y) -le .90001) 'Part stays inside centered lid footprint'}
 for($i=1;$i -lt $part.Points.Length-1;$i++){
  $a=$part.Points[0];$b=$part.Points[$i];$c=$part.Points[$i+1]
  Check ((($b.x-$a.x)*($c.y-$a.y)-($b.y-$a.y)*($c.x-$a.x)) -gt .000001) 'Convex nondegenerate geometry'
 }
 Check ($part.Color.a -eq 1) 'Opaque top-down surface'
}
Check ($vertices -le 160 -and $parts.Count -le 32) 'One compact combined mesh'
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Trapdoor.prefab"
foreach($token in @('m_Name: Trapdoor','shape: 5','size: {x: 1.8, y: 1.8}','solid: 0','sortingOrder: -5','m_Layer: 0','m_LocalPosition: {x: 0, y: 0, z: 0}')){
 Check ($prefab.Contains($token)) "Prefab setting: $token"
}
$colliderBlock=[regex]::Match($prefab,'(?s)BoxCollider2D:(.*?)---').Groups[1].Value
Check ($colliderBlock.Contains('m_Enabled: 0')) 'No movement/vision obstruction on a flat ground decoration'
Check (([regex]::Matches($prefab,'m_Script:')).Count -eq 2) 'Shared visual plus trapdoor interaction, no extra renderers'
Check ($prefab.Contains('guid: 9205b5fa791343889ce934da4ab56290')) 'Password/transport interaction attached'
Check ((Get-Content -Raw "$root/Assets/Prefabs/Decorations/Trapdoor.prefab.meta").Contains('guid: 7218725a5fb34a15a2f633795b5abf68')) 'Unique prefab asset metadata'
Write-Output "PASS: $checks trapdoor geometry/prefab checks, $vertices vertices in one mesh."
