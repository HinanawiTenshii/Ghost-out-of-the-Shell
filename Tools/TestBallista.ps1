$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$weapon=Get-Content -Raw "$root/Assets/Scripts/Interaction/FortressBallista.cs"
$bolt=Get-Content -Raw "$root/Assets/Scripts/Items/FortressBallistaBolt.cs"
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/Fortress/Fortress-Ballista.prefab"
$checks=0
function Check($ok,$label){if(!$ok){throw $label};$script:checks++}
foreach($pair in @(@('Interaction/FortressBallista','fdbc6d573ea14b43802364689b052dce'),@('Items/FortressBallistaBolt','df2319b6b58c4ca1aa23b8c551edf68e'))){
 Check ((Get-Content -Raw "$root/Assets/Scripts/$($pair[0]).cs.meta").Contains("guid: $($pair[1])")) "Valid metadata: $($pair[0])"
}
Check ($prefab.Contains('guid: fdbc6d573ea14b43802364689b052dce')) 'Interaction attached to existing prefab'
Check ($prefab.Contains('guid: 076bd33f4dd04d1188f3cf494d301c92')) 'Streaming exemption attached'
Check ($prefab.Contains('guid: 292af55397c443f6a5d7774297dac481')) 'Existing pixel font'
Check ($prefab.Contains('m_Size: {x: 2.2, y: 2}') -and $prefab.Contains('m_IsTrigger: 0')) 'Solid collision bounds preserved'
foreach($token in @('Cooldown = 3f','ZeldaInteractionArbiter.OfferInteraction','ZeldaInteractionArbiter.Submit','GetOperator()','IsGhostLike','InverseTransformPoint','transform.TransformVector(Vector3.up)','Time.time + Cooldown','TryBeginItemAttack(.24f)','geometry.GeneratedRoot','recoilRoot.localPosition','ResetRecoil()')){
 Check ($weapon.Contains($token)) "Interaction integration: $token"
}
foreach($token in @('AttackPower = 8','Lifetime = 4f','ExplosionWidth = 1.6f','Physics2D.CircleCast','Physics2D.OverlapCircle','Physics2D.SyncTransforms()','collider.transform.IsChildOf(launcher)','CameraVisionStreamingRegion','ZeldaAttackHitbox','ZeldaAttackVisualShape.Box,true','BombExplosionVisual','new Color(.80f,.81f,.82f,.95f)','finished=true;','Destroy(gameObject)')){
 Check ($bolt.Contains($token)) "Projectile integration: $token"
}
foreach($token in @('Physics2D.OverlapBox(start,Vector2.one*ExplosionWidth,0f','Physics2D.BoxCast(start,Vector2.one*ExplosionWidth,0f','CanTriggerProximity','!character.IsDead && character.CanTakeAttackDamage','Mathf.Min(nearestContact,nearestProximity)','nearestProximity+ProximityContactInset')){
 Check ($bolt.Contains($token)) "Character-only swept proximity fuse: $token"
}
$bomb=Get-Content -Raw "$root/Assets/Resources/PickupItems/Bombs/BombPickupItem.prefab"
$size=[regex]::Match($bomb,'explosionSize: \{x: ([\d.]+), y: ([\d.]+)\}')
Check ($size.Success -and [double]::Parse($size.Groups[1].Value,[cultureinfo]::InvariantCulture) -gt 1.6 -and [double]::Parse($size.Groups[2].Value,[cultureinfo]::InvariantCulture) -gt 1.6) 'Explosion smaller than actual bomb prefab'
Write-Output "PASS: $checks ballista prefab/integration checks. Physics/play-mode checks: Tools/BallistaSmoke.cs."
