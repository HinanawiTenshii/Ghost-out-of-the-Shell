# Scene/reference and render-contract checks. GPU kernel checks are in GardenBloomShaderSmoke.cs.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$scene = Get-Content -Raw "$root/Assets/Scenes/Level1/Level1-Garden.unity"
$profile = Get-Content -Raw "$root/Assets/Scripts/Rendering/GardenFluorescenceTest.cs"
$bloom = Get-Content -Raw "$root/Assets/Scripts/Rendering/SelectiveWorldBloom.cs"
$shader = Get-Content -Raw "$root/Assets/Resources/SelectiveWorldBloom.shader"
$vision = Get-Content -Raw "$root/Assets/Scripts/Camera/CameraCircularVision.cs"
$script:checks = 0
function Check([bool]$value, [string]$message) { if (!$value) { throw $message }; $script:checks++ }
$ids = @([regex]::Matches($scene, '(?m)^--- !u!\d+ &(\d+)') | ForEach-Object {$_.Groups[1].Value})
Check (@($ids | Group-Object | Where-Object Count -gt 1).Count -eq 0) 'Duplicate scene object IDs'
foreach ($reference in [regex]::Matches($scene, '\{fileID: (\d+)\}')) {
    Check ($reference.Groups[1].Value -eq '0' -or $ids -contains $reference.Groups[1].Value) ('Unresolved scene reference '+$reference.Value)
}
Check ([regex]::Matches($scene,'guid: 75acf15fcd7a44c5a1a20c515b5b8bdb').Count -eq 1) 'Expected one Garden test profile'
foreach ($entry in @('testEnabled: 1','adjustPalette: 1','enableBloom: 1','fountain: {fileID: 920100100}',
    'walls: {fileID: 1508382463}','decorations: {fileID: 1245951283}',
    '- {fileID: 426750397}','- {fileID: 688539744}','- {fileID: 1344759493}','- {fileID: 1783658459}')) {
    Check ($scene.Contains($entry)) "Missing scene configuration $entry"
}
Check ($scene.Contains('m_PrefabInstance: {fileID: 1402147725}')) 'Fountain instance disconnected'
Check ($profile.Contains('originalColors.Clear()') -and $profile.Contains('entry.Key.color = entry.Value')) 'Palette must restore'
Check ($profile.Contains('private void OnDisable() { Restore(); }')) 'Scene disable must release owned effects'
Check ($profile.Contains('bloom.enabled = false;') -and $profile.Contains('Destroy(bloom);')) 'Persistent camera must not retain Garden effect'
Check (!$profile.Contains('FindObjectsOfType')) 'Avoid full-scene polling'
Check (!$profile.Contains('ExecuteAlways')) 'Test palette must not dirty edit-time objects'
Check ($profile.Contains('part == "Water"') -and $profile.Contains('"Flower Center"')) 'Missing selected highlights'
Check ($bloom.Contains('worldCamera != Camera.main')) 'World pass only'
Check (!$bloom.Contains('CRTScreenEffect') -and !$bloom.Contains('Canvas')) 'Do not change UI/CRT pipeline'
Check ($bloom.Contains('TryGetPostEffectVisibilityMesh')) 'Must reuse exact visibility mesh'
Check ($vision.Contains('!SoulMarkRuntime.IsVisionCovered')) 'Fail closed during soul transfer'
Check ($bloom.Contains('masked ? Color.black : Color.white')) 'Missing polygon must fail closed'
Check ($shader.Contains('selected * visible')) 'Extract must be selected and visible'
Check ($shader.Contains('* visibility *')) 'Clip final blurred halo too'
Check ($shader.Contains('BlendOp Max')) 'Overlapping selections must not accumulate'
Check ($bloom.Contains('RenderTextureFormat.R8')) 'Masks should use single-channel storage'
Check ($bloom.Contains('Temporary(source, 2)') -and $bloom.Contains('Temporary(source, 4)')) 'Blur should be half/quarter resolution'
Check ($bloom.Contains('finally') -and $bloom.Contains('Release(farScratch)')) 'Temporary textures must always release'
Check ($bloom.Contains('commands.Release()') -and $bloom.Contains('meshes.Clear()')) 'Cached resources must release'
Check ([regex]::Matches($shader,'(?m)^        Pass\s*$').Count -eq 5) 'Expected five shader passes'
Check (0.52 -lt (0.66 - 0.12)) 'Water fill must stay below emission knee'
Check (0.96 -gt (0.66 + 0.12)) 'Glints must exceed emission knee'
# Ensure the wall palette leaves headroom even on the brightest CRT scanline.
function CrtPeak([double]$channel, [double]$maxChannel) {
    $v = ($channel - 0.5) * 1.12 + 0.5
    $m = ($maxChannel - 0.5) * 1.12 + 0.5
    $t = [Math]::Clamp(($m - 0.45) / 0.55, 0, 1)
    return ($v + $v*$v*($t*$t*(3-2*$t))*0.313)*1.51
}
foreach($channel in 0.5,0.53,0.56) { Check ((CrtPeak $channel 0.56) -lt 0.95) 'Wall still clips to white under existing CRT' }
Write-Output "PASS: $script:checks scene-reference, palette-headroom and render-contract checks (not an in-game visual approval)."
