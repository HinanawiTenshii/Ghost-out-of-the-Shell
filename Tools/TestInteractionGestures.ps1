$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$pickup=Get-Content -Raw "$root/Assets/Scripts/Items/PickupItemBase.cs"
$lever=Get-Content -Raw "$root/Assets/Scripts/Interaction/LeverData.cs"
function Extract($s,$sig){
 $start=$s.IndexOf($sig);if($start -lt 0){throw "Missing $sig"};$end=$s.IndexOf('{',$start)+1;$depth=1
 while($depth){if($s[$end] -eq '{'){$depth++};if($s[$end] -eq '}'){$depth--};$end++};$s.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 one=>new Vector2(1,1);public static Vector2 zero=>new Vector2();
 public static Vector2 left=>new Vector2(-1,0);public static Vector2 right=>new Vector2(1,0);public static Vector2 up=>new Vector2(0,1);public static Vector2 down=>new Vector2(0,-1);
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);public static Vector2 operator *(Vector2 a,float b)=>new Vector2(a.x*b,a.y*b);
 public static implicit operator Vector2(Vector3 v)=>new Vector2(v.x,v.y);public static implicit operator Vector3(Vector2 v)=>new Vector3(v.x,v.y);
}
public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z=0){this.x=x;this.y=y;this.z=z;}public static Vector3 operator +(Vector3 a,Vector3 b)=>new Vector3(a.x+b.x,a.y+b.y,a.z+b.z);}
public static class Mathf {public static float Abs(float x)=>Math.Abs(x);public static float Max(float a,float b)=>Math.Max(a,b);}
public class Transform {public Vector3 position;public Transform parent;public bool IsChildOf(Transform p){return this==p||(parent!=null&&parent.IsChildOf(p));}}
public class GameObject {public bool activeInHierarchy=true,destroyed;public int scene=1,layer;public Transform transform=new Transform();public ZeldaFourWayMover mover;}
public class Component {public GameObject gameObject=new GameObject();public Transform transform=>gameObject.transform;public T GetComponent<T>() where T:class=>gameObject.mover as T;protected static void Destroy(GameObject g){g.destroyed=true;}}
public class ZeldaCharacterData:Component {public int FinalSkillValue=3;}
public class ZeldaFourWayMover {public bool IsAttacking;public Vector2 FacingDirection=Vector2.down;public int gestures;public float duration;public bool TryBeginItemAttack(float d){if(IsAttacking)return false;IsAttacking=true;gestures++;duration=d;return true;}}
public class Collider2D:Component {public bool enabled=true,isTrigger;}
public struct RaycastHit2D {public Collider2D collider;}
public struct ContactFilter2D {public bool useTriggers;}
public static class LayerMask {public static int NameToLayer(string n)=>n=="Blocks"?8:n=="Hidden Blocks"?9:-1;}
public static class ZeldaRuntimeRegistry {public static ZeldaFourWayMover controlled;public static int GetGameplayScene(GameObject g)=>1;}
public class ItemDescriptionWindow {
 public static ItemDescriptionWindow Instance=new ItemDescriptionWindow();public static ItemDescriptionWindow GetOrCreate()=>Instance;
 public int notifications;public bool sawGesture;public void NotifyPickedUp(PickupItemBase item){notifications++;sawGesture=ZeldaRuntimeRegistry.controlled!=null&&ZeldaRuntimeRegistry.controlled.IsAttacking;}
}
public static class Physics2D {
 public static List<Collider2D> swept=new List<Collider2D>(),overlaps=new List<Collider2D>();public static Vector2 start,size,end;public static bool triggers;public static float distance;
 public static int BoxCast(Vector2 p,Vector2 s,float a,Vector2 d,ContactFilter2D f,List<RaycastHit2D> results,float length){start=p;size=s;distance=length;triggers=f.useTriggers;foreach(var c in swept)results.Add(new RaycastHit2D{collider=c});return results.Count;}
 public static int OverlapBox(Vector2 p,Vector2 s,float a,ContactFilter2D f,List<Collider2D> results){end=p;results.AddRange(overlaps);return results.Count;}
 public static void Clear(){swept.Clear();overlaps.Clear();}
}
public class PickupItemBase:Component {
 public Action<PickupItemBase> PickedUp;private ZeldaFourWayMover FindControlledMover()=>ZeldaRuntimeRegistry.controlled;
 public void NotifySuccessfulPickup()=>NotifyPickedUp();
 public bool UsesSimpleUseGesture=true,consumes=true,reusable;public int effects;
 protected bool ApplyUseEffect(ZeldaCharacterData user){effects++;if(reusable)PlayUseGesture(user);return consumes;}
'@
foreach($sig in @('    public bool ExecuteUse(', '    public void PlayUseGesture(', '    protected void NotifyPickedUp()', '    public static Vector3 ResolvePlacementPosition(', '    private static bool BlocksPlacement(')){$code+=Extract $pickup $sig}
$code+=@'
}
public class LeverHarness {
 public bool HasBatteryPower=true,isOn;public int complexity=2,changes,visuals;public Action<LeverHarness,bool> SignalEmitted;
 void ApplyVisual(){visuals++;}void ApplyLinkedObjectChange(){changes++;}public bool Interact(ZeldaCharacterData d)=>TryInteract(d);
'@
$code+=Extract $lever '    private bool TryInteract('
$code+=@'
}
public static class Tests {
 static int count;static void Check(bool b,string m){count++;if(!b)throw new Exception(m);}
 static ZeldaCharacterData User(){var d=new ZeldaCharacterData();d.gameObject.mover=new ZeldaFourWayMover();d.transform.position=new Vector3(3,4,2);return d;}
 static bool At(Vector3 p,float x,float y)=>Math.Abs(p.x-x)<.0001&&Math.Abs(p.y-y)<.0001&&p.z==2;
 static Vector3 Place(ZeldaCharacterData u,GameObject ignore=null)=>PickupItemBase.ResolvePlacementPosition(u,new Vector3(),new Vector2(.5f,.7f),new Vector2(0,.1f),ignore);
 public static int Run(){
  var user=User();var m=user.gameObject.mover;
  foreach(var facing in new[]{Vector2.down,Vector2.up,Vector2.left,Vector2.right}){
   Physics2D.Clear();m.FacingDirection=facing;var p=Place(user);Check(At(p,3+facing.x*.65f,4+facing.y*.65f),"Forward offset follows facing and preserves Z");
   Check(Math.Abs(Physics2D.size.x-.54f)<.0001&&Math.Abs(Physics2D.size.y-.74f)<.0001&&Math.Abs(Physics2D.start.y-4.1f)<.0001,"Sweep uses item footprint, padding and collider-center offset");
   Check(Physics2D.triggers&&Physics2D.distance==.65f,"Sweep includes trigger walls over whole reach");
  }
  m.FacingDirection=Vector2.right;
  foreach(bool endpoint in new[]{false,true}){
   Physics2D.Clear();var wall=new Collider2D();if(endpoint)Physics2D.overlaps.Add(wall);else Physics2D.swept.Add(wall);
   Check(At(Place(user),3,4),"Blocked endpoint OR path returns exact character origin, not partial movement");
  }
  foreach(int layer in new[]{8,9}){Physics2D.Clear();Physics2D.swept.Add(new Collider2D{isTrigger=true,gameObject=new GameObject{layer=layer}});Check(At(Place(user),3,4),"Trigger Blocks/Hidden Blocks still blocks placement");}
  foreach(int ignoredCase in new[]{0,1,2,3,4,5}){
   Physics2D.Clear();var c=new Collider2D();GameObject item=new GameObject();
   if(ignoredCase==0)c.transform.parent=user.transform;
   if(ignoredCase==1)c.transform.parent=item.transform;
   if(ignoredCase==2)c.gameObject.scene=2;
   if(ignoredCase==3)c.isTrigger=true;
   if(ignoredCase==4)c.enabled=false;
   if(ignoredCase==5)c.gameObject.activeInHierarchy=false;
   Physics2D.swept.Add(c);Physics2D.overlaps.Add(c);Check(At(Place(user,item),3.65f,4),"Self, temporary item, other scene, area trigger and disabled objects ignored");
  }
  Physics2D.Clear();m.FacingDirection=new Vector2(.2f,.9f);Check(At(Place(user),3,4.65f),"Diagonal facing snaps to authored cardinal pose");
  Check(At(PickupItemBase.ResolvePlacementPosition(null,new Vector3(3,4,2),Vector2.one,Vector2.zero,null),3,4),"Missing user preserves fallback");
  var itemUse=new PickupItemBase();Check(itemUse.ExecuteUse(user)&&itemUse.gameObject.destroyed&&m.gestures==1&&m.duration==.24f,"Consumed item gestures exactly once");
  var busy=new PickupItemBase();Check(!busy.ExecuteUse(user)&&busy.effects==0&&!busy.gameObject.destroyed,"Busy hands do not consume or execute another generic item");
  m.IsAttacking=false;var noop=new PickupItemBase{consumes=false};Check(!noop.ExecuteUse(user)&&m.gestures==1,"No-op materials do not animate");
  var remote=new PickupItemBase{consumes=false,reusable=true};Check(!remote.ExecuteUse(user)&&m.gestures==2&&!remote.gameObject.destroyed,"Reusable remote gestures without consuming inventory item");
  m.IsAttacking=false;var excluded=new PickupItemBase{UsesSimpleUseGesture=false};Check(excluded.ExecuteUse(user)&&m.gestures==2,"Excluded items do not add a gesture");
  var lever=new LeverHarness();Check(lever.Interact(user)&&lever.isOn&&lever.changes==1&&m.gestures==3,"Player lever interaction gestures once");
  Check(!lever.Interact(user)&&lever.changes==1,"No second lever toggle during gesture");
  m.IsAttacking=false;lever.HasBatteryPower=false;Check(!lever.Interact(user)&&m.gestures==3,"Unpowered lever does not gesture");
  lever.HasBatteryPower=true;user.FinalSkillValue=1;Check(!lever.Interact(user)&&m.gestures==3,"Insufficient skill does not gesture");
  ZeldaRuntimeRegistry.controlled=m;
  foreach(bool usesGesture in new[]{true,false}){
   m.IsAttacking=false;int before=m.gestures,events=0;var picked=new PickupItemBase{UsesSimpleUseGesture=usesGesture};picked.PickedUp+=i=>events++;
   picked.NotifySuccessfulPickup();Check(m.gestures==before+1&&m.duration==.24f,"All successful pickups gesture, including items excluded from use gestures");
   Check(ItemDescriptionWindow.Instance.sawGesture&&events==1,"Pose starts before first-pickup modal and preserves event notification");
  }
  int busyBefore=m.gestures;new PickupItemBase().NotifySuccessfulPickup();Check(m.gestures==busyBefore,"Pickup never restarts or cancels an existing attack/gesture");
  ZeldaRuntimeRegistry.controlled=null;int notices=ItemDescriptionWindow.Instance.notifications;new PickupItemBase().NotifySuccessfulPickup();Check(ItemDescriptionWindow.Instance.notifications==notices+1,"No active character is safe and does not suppress notification");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
$count=[Tests]::Run()
foreach($file in @('CardboardBoxPickupItem','StabilityCrystalPickupItem','DisposableHandgunPickupItem')){
 if(!(Get-Content -Raw "$root/Assets/Scripts/Items/$file.cs").Contains('override bool UsesSimpleUseGesture => false')){throw "Missing gesture exception: $file"}
}
foreach($file in @('BombPickupItem','ClockworkPuppetPickupItem')){
 if(!(Get-Content -Raw "$root/Assets/Scripts/Items/$file.cs").Contains('ResolvePlacementPosition(user,')){throw "Missing forward deployment: $file"}
}
if((Extract $lever '    public void ActivateFromClockworkPuppet()').Contains('TryBeginItemAttack')){throw 'Autonomous lever activation must not animate the player'}
foreach($file in @('PickupItemBase','BombPickupItem','CardboardBoxPickupItem','DisposableHandgunPickupItem','StabilityCrystalPickupItem')){
 $itemSource=Get-Content -Raw "$root/Assets/Scripts/Items/$file.cs"
 $signature=if($file -eq 'PickupItemBase'){'    public virtual bool TryPickUp()'}else{'    public override bool TryPickUp()'}
 $method=Extract $itemSource $signature
 if(!$method.Contains('NotifyPickedUp();') -or $method.IndexOf('NotifyPickedUp();') -lt $method.IndexOf('OnPickedUp(inventory);')){throw "Missing success-only shared pickup feedback: $file"}
}
$puppetSource=Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetRuntime.cs"
if(!(Extract $puppetSource '    private void Reclaim()').Contains('GetControlledMover()?.TryBeginItemAttack(.24f)')){throw 'Deployed puppet reclaim needs the same gesture'}
Write-Output "PASS: $count production gesture/placement/filter checks; item exclusions and deployment wiring verified. Physics hit results are substitutes, not Unity Play Mode."
