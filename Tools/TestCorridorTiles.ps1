$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scene=Get-Content -Raw "$root/Assets/Scenes/Level2/Level2-Floor1.unity"
$mat=Get-Content -Raw "$root/Assets/Materials/Level2CorridorTiles.mat"
$shader=Get-Content -Raw "$root/Assets/Shaders/CheckerTileFloor.shader"
$ids=[regex]::Matches($scene,'(?m)^--- !u!\d+ &(\d+)')|ForEach-Object{$_.Groups[1].Value}
if(($ids|Group-Object|Where-Object Count -gt 1)){throw 'Duplicate scene file IDs'}
$blocks=[regex]::Split($scene,'(?m)(?=^--- !u!)')
$floor=@($blocks|Where-Object{$_ -match '^--- !u!\d+ &210918100[012]\r?\n'})
if($floor.Count -ne 3){throw 'Floor needs exactly GameObject, Transform, SpriteRenderer'}
if([regex]::Matches($scene,'guid: 89b6848b921f4c24a274326a5f098cad').Count -ne 1){throw 'Expected one material instance in scene'}
$parent=$blocks|Where-Object{$_ -match '^--- !u!4 &389795959\r?\n'}
if(!$parent.Contains('- {fileID: 2109181001}')){throw 'Floor not attached to Grounds'}
$renderer=$floor|Where-Object{$_ -match '^--- !u!212'}
if(!$renderer.Contains('m_SortingOrder: -11') -or !$renderer.Contains('m_MaskInteraction: 0')){throw 'Floor sorting/masking regression'}
$transform=$floor|Where-Object{$_ -match '^--- !u!4 '}
if(!$transform.Contains('m_LocalScale: {x: 3.67, y: 18.8506, z: 1}')){throw 'Corridor extent regression'}
if($shader -match '_Grout|smoothstep|sin\(' -or $mat -match '_Grout'){throw 'Only alternating solid squares requested'}
if(!$shader.Contains('floor(grid)') -or !$shader.Contains('unity_ObjectToWorld')){throw 'World-space square grid missing'}
if(!$mat.Contains('guid: 2497685ac5b04ee6936e911a638c8af1')){throw 'Material shader reference incorrect'}
# Reuse the project's D3D syntax harness. Includes are replaced by stubs here;
# this is not an assertion that Unity has imported or rendered the material.
. "$PSScriptRoot/TestSpriteWaterFlowShader.ps1"
$program=[regex]::Match($shader,'(?s)CGPROGRAM(.*?)ENDCG').Groups[1].Value
$program=[regex]::Replace($program,'(?m)^\s*#pragma.*$','')
$program=$program.Replace('#include "UnityCG.cginc"','float4x4 unity_ObjectToWorld; float4 UnityObjectToClipPos(float4 v) { return v; }').Replace('fixed4','float4')
[SpriteWaterFlowCompileTest]::Check($program,'vert','vs_4_0')
[SpriteWaterFlowCompileTest]::Check($program,'frag','ps_4_0')
Write-Output 'PASS: single floor SpriteRenderer, scene references, render order, plain checker grid, Direct3D vertex/fragment syntax.'
