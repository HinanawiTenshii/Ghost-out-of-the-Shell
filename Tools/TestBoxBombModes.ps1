$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$box = Get-Content -Raw "$root/Assets/Scripts/Items/CardboardBoxPickupItem.cs"
$placed = Get-Content -Raw "$root/Assets/Scripts/Items/PlacedBomb.cs"
$inventory = Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
function Extract($source, $signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $brace = $source.IndexOf('{', $start)
    $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
$arm = Extract $box '    public void ArmStoredBomb('
$tick = Extract $box '    private void TickStoredBomb('
$resume = Extract $placed '    public void RestoreRemainingFuse('
$release = Extract $box '    private void ReleaseStoredItem('
# Exercise the actual armed-release branch, before the legacy generic pickup branch.
$armedRelease = $release.Substring(0, $release.IndexOf('        if (storedPuppetDriver != null)')) + "`n}"
$code = @'
using System;
public static class Mathf {
 public static float Max(float a,float b)=>Math.Max(a,b);
 public static float Clamp(float a,float b,float c)=>Math.Min(c,Math.Max(a,b));
}
public class GameObject { public int scene=2; }
public class Transform { public float position=7; }
namespace UnityEngine.SceneManagement {
 public static class SceneManager {public static void MoveGameObjectToScene(GameObject o,int scene){o.scene=scene;}}
}
public class ZeldaCharacterData { public string IndividualAccessId="owner"; }
public class ZeldaCharacterAiBase {}
public class PickupItemBase {}
public class BombPickupItem:PickupItemBase {
 public bool IsDecoy;
 public bool IsProximity;
 public float ExplosionSize=3.6f;
 public float FuseDuration=2;
 public PlacedBomb last;
 public PlacedBomb CreateConfiguredPlacedBomb(float position,ZeldaCharacterData user){
  last=new PlacedBomb{fuseDuration=FuseDuration,owner=user,position=position}; return last;
 }
}
public class PlacedBomb {
 public Transform ExplosionSource;
 public float fuseDuration,elapsed,position;
 public int detonations;
 public ZeldaCharacterData owner;
 public GameObject gameObject=new GameObject();
 public void RestoreSavedOwner(ZeldaCharacterData user){owner=user;}
 public void RestoreProximityOwnerId(string id){}
 public void DetonateImmediately(){detonations++;}
 RESUME
}
public static class BombProximitySensor {
 public const float CheckInterval=.05f;
 public static bool targetPresent;
 public static bool HasTarget(float pos,float size,int scene,ZeldaCharacterData owner,string id)=>targetPresent;
}
public class BoxHarness {
 public bool hasStoredItem=true,isDestroyed,storedBombArmed;
 public float storedBombRemainingFuse;
 public float storedBombProximityCheckRemaining;
 public string storedBombOwnerId;
 public ZeldaCharacterData storedBombOwner;
 public PickupItemBase storedItemPrefab;
 public int retained,breaks,clears;
 public GameObject gameObject=new GameObject();
 public Transform transform=new Transform();
 void RefreshStoredBombRetention(){retained++;}
 ZeldaCharacterData ResolveStoredBombOwner(){return storedBombOwner;}
 void ClearStoredItem(){clears++;hasStoredItem=false;storedBombArmed=false;storedBombRemainingFuse=0;storedItemPrefab=null;}
 void BreakGroundBox(ZeldaCharacterAiBase ai){isDestroyed=true;breaks++;ReleaseStoredItem(ai,true);}
 public void Advance(float dt){TickStoredBomb(dt);}
 public void Recover(){ReleaseStoredItem(null,false);}
 public void DestroyBox(){BreakGroundBox(null);}
 ARM
 TICK
 RELEASE
}
public static class BoxBombTests {
 static int count;
 static void Check(bool b,string message){count++;if(!b)throw new Exception(message);}
 public static int Run(){
  var owner=new ZeldaCharacterData();
  foreach(float duration in new[]{2f,5f}){
   var prefab=new BombPickupItem{FuseDuration=duration};
   var box=new BoxHarness{storedItemPrefab=prefab};
   box.Advance(100);Check(box.breaks==0&&!box.storedBombArmed,"Q storage has no timed explosion");
   box.ArmStoredBomb(owner);Check(box.storedBombArmed&&box.storedBombRemainingFuse==duration,"R uses this bomb variant's fuse");
   Check(box.retained==1&&box.storedBombOwnerId==owner.IndividualAccessId,"Retain simulation and save owner identity");
   box.Advance(.5f);box.ArmStoredBomb(owner);Check(box.storedBombRemainingFuse==duration-.5f&&box.retained==1,"Rearming cannot reset fuse");
   box.Advance(0);Check(box.storedBombRemainingFuse==duration-.5f,"Pause freezes countdown");
   box.Advance(duration);Check(box.breaks==1&&prefab.last.detonations==1,"Expiry breaks box and detonates once");
   box.Advance(100);Check(box.breaks==1,"No repeat explosion on later frame");
   Check(!box.hasStoredItem&&box.clears==1,"Contents cleared before chain reactions");
   Check(prefab.last.owner==owner&&prefab.last.gameObject.scene==box.gameObject.scene,"Preserve owner and container scene");
   var recover=new BoxHarness{storedItemPrefab=prefab};recover.ArmStoredBomb(owner);recover.Advance(.75f);recover.Recover();
   Check(prefab.last.elapsed==.75f&&prefab.last.detonations==0,"Picking up lit box releases timed bomb with same remaining fuse");
   Check(!recover.hasStoredItem,"Recovery cannot duplicate bomb");
   var hit=new BoxHarness{storedItemPrefab=prefab};hit.ArmStoredBomb(owner);hit.DestroyBox();
   Check(prefab.last.detonations==1,"Destruction detonates an armed stored bomb immediately");
   var restored=new BoxHarness{storedItemPrefab=prefab,storedBombArmed=true,storedBombRemainingFuse=.25f};
   restored.Advance(.3f);Check(prefab.last.detonations==1,"Restored scalar countdown expires without restarting");
  }
  var decoy=new BoxHarness{storedItemPrefab=new BombPickupItem{IsDecoy=true}};
  decoy.ArmStoredBomb(owner);decoy.Advance(1000);
  Check(decoy.breaks==0&&decoy.storedBombArmed,"Concealed activated decoy never expires");
  var proximity=new BoxHarness{storedItemPrefab=new BombPickupItem{IsProximity=true}};
  proximity.ArmStoredBomb(owner);proximity.Advance(1000);
  Check(proximity.breaks==0&&proximity.storedBombArmed,"Concealed proximity bomb never expires");
  BombProximitySensor.targetPresent=true;proximity.Advance(0);
  Check(proximity.breaks==0,"Pause freezes proximity checks");
  proximity.Advance(.1f);Check(proximity.breaks==1,"Concealed proximity target breaks box once");
  BombProximitySensor.targetPresent=false;
  var other=new BoxHarness{storedItemPrefab=new PickupItemBase()};other.ArmStoredBomb(owner);
  Check(!other.storedBombArmed&&other.retained==0,"R storage of other items unchanged");
  var missing=new BoxHarness{hasStoredItem=false};missing.ArmStoredBomb(null);missing.Advance(100);
  Check(missing.breaks==0,"Empty container never arms");
  var p=new PlacedBomb{fuseDuration=2};p.RestoreRemainingFuse(-1);Check(p.elapsed==2,"Expired fuse clamped");
  p.RestoreRemainingFuse(10);Check(p.elapsed==0,"Remaining fuse cannot exceed configured duration");
  return count;
 }
}
'@
$code=$code.Replace(' RESUME',$resume).Replace(' ARM',$arm).Replace(' TICK',$tick).Replace(' RELEASE',$armedRelease)
Add-Type -TypeDefinition $code
$count=[BoxBombTests]::Run()
$use=Extract $inventory '    public bool TryUseSelectedItem()'
$drop=Extract $inventory '    public bool TryDropSelectedItem()'
if (!$use.Contains('nearbyBox.ArmStoredBomb(user);') -or $use.IndexOf('nearbyBox.ArmStoredBomb(user);') -lt $use.IndexOf('nearbyBox.CancelLastStoredItem();')) {throw 'R must arm only after successful inventory removal'}
if (!$drop.Contains('itemPrefab is BombPickupItem') -or !$drop.Contains('box.TryStoreItem(slot, itemPrefab, user)') -or $drop.Contains('ArmStoredBomb')) {throw 'Q must safely store bombs only'}
if (!$drop.Contains('box.CancelLastStoredItem();') -or !$drop.Contains('if (mover.IsAttacking) return false;')) {throw 'Q transaction/busy guard missing'}
if (!(Extract $box '    private void LateUpdate()').Contains('if (!GameSaveSystem.IsLoading) TickStoredBomb(Time.deltaTime);')) {throw 'Load/pause countdown guard missing'}
if (!(Extract $box '    public void RestoreSavedContents(').Contains('if (storedBombArmed) RefreshStoredBombRetention();')) {throw 'Restore retention missing'}
if (!$release.Contains('released.OnReleasedFromCardboardBox(observingAi);') -or !$release.Contains('if (invokeReleaseEffect)')) {throw 'Safe stored bomb destruction behavior must remain'}
if (!(Extract $box '    private void ClearStoredItem()').Contains('storedBombRetention.enabled = false;')) {throw 'Retention cleanup missing'}
Write-Output "PASS: $count production fuse/armed-release checks and 7 integration guards. Unity objects are test substitutes, not Play Mode."
