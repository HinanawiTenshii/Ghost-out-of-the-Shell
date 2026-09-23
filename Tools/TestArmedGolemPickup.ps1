# Execute production pickup, inventory-slot, R/Q and snapshot methods with engine stubs.
# Unity serialization/physics/lifecycle are not Play Mode tested by this harness.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$pickup=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Items/PickupItemBase.cs"
$inventory=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Items/PersistentInventory.cs"
$golem=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/Items/ArmedGolemPickupItem.cs"
function Extract($s,$signature){
    $start=$s.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
    $b=$s.IndexOf('{',$start);$end=$b+1;$depth=1
    while($depth){if($s[$end] -eq '{'){$depth++};if($s[$end] -eq '}'){$depth--};$end++}
    $s.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class SerializeField:Attribute{} public class TextArea:Attribute{public TextArea(int a,int b){}}
 public class Min:Attribute{public Min(float a){}} public class DisallowMultipleComponent:Attribute{}
 public class RequireComponent:Attribute{public RequireComponent(Type a,Type b){}}
 public struct Vector2 {public float x,y;public Vector2(float a,float b){x=a;y=b;}
  public static float Distance(Vector2 a,Vector2 b){return (float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y));}
  public static implicit operator Vector2(Vector3 v){return new Vector2(v.x,v.y);}}
 public struct Vector3 {public float x,y,z;public Vector3(float a,float b,float c=0){x=a;y=b;z=c;}}
 public struct Color {public static Color white=>new Color();}
 public struct Quaternion {public static Quaternion identity=>new Quaternion();}
 public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Clamp(int a,int b,int c)=>Math.Max(b,Math.Min(a,c));}
 public static class Debug {public static void LogWarning(string s,object o){}}
 public class Transform {public Vector3 position,localScale=new Vector3(.85f,.85f,1);public Vector3 lossyScale=>localScale;}
 public class GameObject {
  public bool activeSelf=true,destroyed;public int scene=1;public Transform transform=new Transform();public List<MonoBehaviour> components=new List<MonoBehaviour>();
  public void SetActive(bool value){activeSelf=value;}
  public T Add<T>() where T:MonoBehaviour,new(){var c=new T{gameObject=this};components.Add(c);return c;}
  public T GetComponent<T>() where T:class{foreach(var c in components)if(c is T)return c as T;return null;}}
 public class MonoBehaviour {
  public GameObject gameObject;public bool enabled=true;public bool isActiveAndEnabled=>enabled&&gameObject.activeSelf;public Transform transform=>gameObject.transform;
  public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
  protected static void Destroy(GameObject go){go.destroyed=true;}
  protected static GameObject Instantiate(GameObject go,Vector3 position,Quaternion rotation){return GolemTests.Clone(position);}}
 public static class JsonUtility {
  static Dictionary<string,object> values=new Dictionary<string,object>();
  public static string ToJson(object o){string key=Guid.NewGuid().ToString();values[key]=o;return key;}
  public static T FromJson<T>(string key){return (T)values[key];}}
}
public class ZeldaCharacterData:MonoBehaviour {
 public struct RuntimeState {public int health,currentHealth;}
 public bool IsDead,IsGhostLike;public int FinalAttackPower=3,health=10,currentHealth=6;
 public RuntimeState CaptureRuntimeState()=>new RuntimeState{health=health,currentHealth=currentHealth};
 public void ApplyRuntimeState(RuntimeState s){health=s.health;currentHealth=s.currentHealth;}}
public class AutomatonZeldaCharacterData:ZeldaCharacterData{public void InvalidateVisuals(){}}
public class ZeldaFourWayMover:MonoBehaviour {public FakeBox ActiveCardboardBox;}
public class FakeBox {public bool exited;public void RequestExit(){exited=true;}}
public enum ZeldaAiState{Idle,Hostile}
public class ZeldaCharacterAiBase:MonoBehaviour {public class SaveState{public ZeldaAiState state;public Vector2 facing;public bool automatonHostilityActivated;}}
public class AutomatonCharacterAi:ZeldaCharacterAiBase {
 public bool HostilityActivated,IsPossessionLocked;public Vector2 FacingDirection=new Vector2(0,-1);
 public void DeactivateHostility(){HostilityActivated=false;}
 public SaveState CaptureSaveState()=>new SaveState{automatonHostilityActivated=HostilityActivated,facing=FacingDirection};
 public void ApplySaveState(SaveState s){HostilityActivated=s.automatonHostilityActivated;FacingDirection=s.facing;}}
public static class ZeldaRuntimeRegistry {
 public static ZeldaFourWayMover controlled;public static ZeldaFourWayMover GetControlledMover()=>controlled;
 public static int GetGameplayScene(GameObject go)=>go.scene;}
public static class DocumentReader{public static bool IsInputBlocked;}
public static class ClockworkPuppetRuntime{public static bool BlocksCharacterInput;}
public class ZeldaHealthHeartsUI {
 public static ZeldaHealthHeartsUI Instance=new ZeldaHealthHeartsUI();public string message;
 public void ShowNotificationPopup(string s){message=s;}public void RestorePreviousTaskPointerTarget(){} }
public static class SavedScalarFields {
 public class Value {public int attack;}
 public static List<Value> Capture(object c){return new List<Value>{new Value{attack=c is ZeldaCharacterData d?d.FinalAttackPower:0}};}
 public static void Apply(object c,List<Value> v){if(c is ZeldaCharacterData d&&v!=null)d.FinalAttackPower=v[0].attack;}}
public class PickupItemVisualBase {public Color DisplayColor=>Color.white;public void SetDisplayColor(Color c){}}
public class PickupItemBase:MonoBehaviour {
 public string ItemId="armed_golem",UniqueInstanceId=Guid.NewGuid().ToString(),ItemName="武装魔像",ItemDescription=ArmedGolemPickupItem.DefaultDescription;
 private string itemId=>ItemId;private int pickupQuantity=1,maxStackSize=1;private bool restorePreviousTaskOnPickup=false;
 public bool Locked;protected bool IsPickupLocked=>Locked;protected float PickupDistance=>1.25f;
 public PickupItemVisualBase ItemVisual=new PickupItemVisualBase();public virtual bool HasInventoryCharge=>false;public virtual float InventoryCharge=>0;
 public virtual string InventoryState=>"";public virtual bool UsePlacesInWorld=>false;protected virtual bool UsesTriggerCollider=>true;
 protected virtual bool CanAttemptPickup=>true;public bool PromptEligible=>CanAttemptPickup;public bool IsTrigger=>UsesTriggerCollider;
 public virtual bool CanBeStoredInCardboardBox(ZeldaCharacterData d)=>true;
 protected virtual void Awake(){}public void Initialize(){Awake();}
 protected bool IsPickupBlockedForControlledCharacter()=>false;
 protected void OnPickedUp(PersistentInventory i){}protected void NotifyPickedUp(){}
 public virtual void ApplyInventoryState(string s){} public virtual void ApplyInventoryCharge(bool h,float c){}
 public void SetUniqueInstanceId(string s){UniqueInstanceId=s;}public void SetItemName(string s){ItemName=s;}public void SetItemDescription(string s){ItemDescription=s;}
 public void MarkAsDropped(){Locked=true;OnDropped();} protected virtual void OnDropped(){}
 protected virtual bool ApplyUseEffect(ZeldaCharacterData user)=>true;public bool ExecuteUse(ZeldaCharacterData user)=>ApplyUseEffect(user);
'@
$code+=(Extract $pickup '    public virtual bool TryPickUp()')+"`n}`n"
$code+=($golem -replace '(?m)^using [^;]+;\r?\n','')
$code+=@'
public class CardboardBoxPickupItem {
 public static CardboardBoxPickupItem FindNearestAvailableContainer(Vector3 p)=>null;
 public bool TryStoreItem(PersistentInventory.Slot s,PickupItemBase p,ZeldaCharacterData u)=>false;public void CancelLastStoredItem(){} }
public class PersistentInventory:MonoBehaviour {
 public static PersistentInventory Instance;
 private Slot[] slots=new Slot[]{new Slot()};public int SelectedSlotIndex;
 public event Action<int> SelectedSlotChanged;public Slot GetSlot(int i)=>slots[i];
 public void SelectSlot(int i){SelectedSlotIndex=i;}private void EnsureSlots(){}private void NotifyChanged(){}
 public bool ContainsItemInstance(string id)=>!slots[0].IsEmpty&&slots[0].ItemInstanceId==id;
 public bool TryRemoveItemInstance(string id){if(!ContainsItemInstance(id))return false;slots[0].Clear();return true;}
 public bool TryRemoveItem(string id,int qty){if(slots[0].ItemId!=id)return false;slots[0].Clear();return true;}
 public PickupItemBase template;public PickupItemBase ResolveItemPrefab(string id)=>template;
 private Vector3 GetControlledCharacterPosition()=>ZeldaRuntimeRegistry.controlled.transform.position;
'@
foreach($sig in @('    public struct RuntimeSlotState','    public struct RuntimeState','    public sealed class Slot',
    '    public RuntimeState CaptureRuntimeState()','    public void ApplyRuntimeState(', '    public bool TryAddUniqueItem(',
    '    public bool TryUseSelectedItem()', '    public bool TryDropSelectedItem()')){$code+=Extract $inventory $sig}
$code+="`n}`n"
$code+=@'
public static class GolemTests {
 static int checks;static ArmedGolemPickupItem lastClone;
 static void Check(bool c,string text){checks++;if(!c)throw new Exception(text);}
 static ArmedGolemPickupItem Make(){
  var go=new GameObject();go.Add<AutomatonCharacterAi>();go.Add<AutomatonZeldaCharacterData>();go.Add<ZeldaFourWayMover>().enabled=false;
  var item=go.Add<ArmedGolemPickupItem>();item.Initialize();return item;}
 public static GameObject Clone(Vector3 p){lastClone=Make();lastClone.transform.position=p;return lastClone.gameObject;}
 public static int Run(){
  var inv=new PersistentInventory();PersistentInventory.Instance=inv;inv.template=Make();
  var player=new GameObject();var user=player.Add<ZeldaCharacterData>();ZeldaRuntimeRegistry.controlled=player.Add<ZeldaFourWayMover>();
  var a=Make();var ai=a.GetComponent<AutomatonCharacterAi>();var data=a.GetComponent<AutomatonZeldaCharacterData>();
  Check(a.PromptEligible&&!a.IsTrigger,"Dormant offers pickup and preserves solid collision");
  user.FinalAttackPower=2;Check(!a.TryPickUp()&&!a.gameObject.destroyed&&inv.GetSlot(0).IsEmpty,"Strength 2 must not collect");
  Check(ZeldaHealthHeartsUI.Instance.message==ArmedGolemPickupItem.TooHeavyMessage,"Exact too-heavy message");
  user.FinalAttackPower=3;ai.HostilityActivated=true;
  Check(!a.PromptEligible&&!a.TryPickUp(),"Activation after prompt blocks collection");ai.HostilityActivated=false;
  ai.IsPossessionLocked=true;Check(!a.TryPickUp(),"Possession in progress cannot collect");ai.IsPossessionLocked=false;
  a.GetComponent<ZeldaFourWayMover>().enabled=true;Check(!a.TryPickUp(),"Controlled character cannot be collected");a.GetComponent<ZeldaFourWayMover>().enabled=false;
  user.IsGhostLike=true;Check(!a.TryPickUp(),"Ghost cannot pick up");user.IsGhostLike=false;
  user.IsDead=true;Check(!a.TryPickUp(),"Dead player cannot pick up");user.IsDead=false;
  data.IsDead=true;Check(!a.PromptEligible&&!a.TryPickUp(),"Dead golem cannot pick up");data.IsDead=false;
  a.Locked=true;Check(!a.TryPickUp(),"Drop cooldown respected");a.Locked=false;
  player.transform.position=new Vector3(2,0);Check(!a.TryPickUp(),"Recheck pickup distance");player.transform.position=new Vector3();
  player.scene=2;Check(!a.TryPickUp(),"Recheck scene");player.scene=1;
  inv.TryAddUniqueItem("other","occupied");Check(!a.TryPickUp()&&!a.gameObject.destroyed,"Full inventory leaves golem intact");inv.TryRemoveItemInstance("occupied");
  data.currentHealth=4;data.FinalAttackPower=7;a.transform.localScale=new Vector3(.9f,.9f,1);ai.FacingDirection=new Vector2(1,0);
  Check(a.TryPickUp(),"Strength exactly 3 picks up");
  Check(a.gameObject.destroyed&&!a.gameObject.activeSelf&&!a.TryPickUp(),"No duplicate pickup in same frame");
  var slot=inv.GetSlot(0);string id=slot.ItemInstanceId;
  Check(slot.ItemName=="武装魔像"&&slot.ItemDescription==ArmedGolemPickupItem.DefaultDescription&&slot.Quantity==1,"Exact inventory identity/description and unique slot");
  Check(!string.IsNullOrEmpty(slot.ItemState),"Character state is stored");
  var saved=inv.CaptureRuntimeState();inv.TryRemoveItemInstance(id);inv.ApplyRuntimeState(saved);
  Check(inv.GetSlot(0).ItemState==saved.slots[0].itemState&&inv.GetSlot(0).ItemInstanceId==id,"Snapshot/load preserves payload and unique identity");
  player.transform.position=new Vector3(5,8);user.FinalAttackPower=1;
  ZeldaRuntimeRegistry.controlled.ActiveCardboardBox=new FakeBox();
  Check(inv.TryUseSelectedItem()&&inv.GetSlot(0).IsEmpty,"R places even after switching to weaker character");
  Check(!ZeldaRuntimeRegistry.controlled.ActiveCardboardBox.exited,"R places instead of exiting box");
  var placed=lastClone;
  Check(placed.transform.position.x==5&&placed.transform.position.y==8&&!placed.gameObject.destroyed,"R places live character at feet");
  Check(placed.GetComponent<AutomatonZeldaCharacterData>().currentHealth==4&&placed.GetComponent<AutomatonZeldaCharacterData>().FinalAttackPower==7,"Health and authored stats survive placement");
  Check(placed.UniqueInstanceId==id&&placed.transform.localScale.x==.9f&&!placed.GetComponent<AutomatonCharacterAi>().HostilityActivated&&placed.Locked,"Identity, scale, dormant state and cooldown restored");
  placed.Locked=false;user.FinalAttackPower=4;Check(placed.TryPickUp(),"Placed golem can be reclaimed with strength above 3");
  Check(inv.TryDropSelectedItem()&&inv.GetSlot(0).IsEmpty,"Q uses same placement path");
  Check(lastClone.transform.position.x==5&&lastClone.GetComponent<AutomatonZeldaCharacterData>().currentHealth==4&&!lastClone.GetComponent<AutomatonCharacterAi>().HostilityActivated,"Repeated Q preserves position/health and never activates");
  ZeldaRuntimeRegistry.controlled.ActiveCardboardBox=null;
  Check(!inv.TryDropSelectedItem()&&!inv.TryUseSelectedItem(),"Empty slot cannot duplicate golem");
  Check(!lastClone.CanBeStoredInCardboardBox(user),"R is not intercepted by nearby container");
  inv.TryAddUniqueItem("armed_golem","corrupt",1,1,"golem","",false,default(Color),false,0,"bad payload");
  Check(!inv.TryDropSelectedItem()&&inv.ContainsItemInstance("corrupt")&&lastClone.gameObject.destroyed,"Corrupt state cannot lose item or duplicate character");
  var inactive=new GameObject();inactive.Add<AutomatonCharacterAi>();inactive.Add<AutomatonZeldaCharacterData>();inactive.Add<ZeldaFourWayMover>().enabled=false;
  var unopened=inactive.Add<ArmedGolemPickupItem>();inactive.SetActive(false);
  string inactiveState=unopened.InventoryState;unopened.ApplyInventoryState(inactiveState);
  Check(!string.IsNullOrEmpty(inactiveState),"Inactive scene snapshot works before Awake");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[GolemTests]::Run()+" production pickup/inventory/R-Q/snapshot checks (Unity services stubbed).")
$prefab=Get-Content -Raw -Encoding UTF8 "$root/Assets/Prefabs/Level2NPCS/Automaton-level2.prefab"
$variant=Get-Content -Raw -Encoding UTF8 "$root/Assets/Resources/PickupItems/ArmedGolemPickupItem.prefab"
foreach($s in @('itemId: armed_golem','itemName: 武装魔像','1d5cdeca0bac42c5a8d4beec4ce8240e','0190b5b73e5e4e428ca92d02bd69c2a2','m_IsTrigger: 0')){if(!$prefab.Contains($s)){throw "Prefab missing $s"}}
if(!$variant.Contains('m_SourcePrefab: {fileID: 100100000, guid: a9d8efb48a934a17bba021da0462da1f, type: 3}')){throw 'Resource does not inherit current automaton'}
if(!(Extract $inventory '    public void Load()').Contains('data.slots[i].ItemState')){throw 'PlayerPrefs load drops payload'}
$travel=Get-Content -Raw -Encoding UTF8 "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
if(!$travel.Contains('state.pickupRuntimeState = pickup.InventoryState;') -or !$travel.Contains('pickup.ApplyInventoryState(state.pickupRuntimeState);')){throw 'Scene snapshot drops character item state'}
Write-Output 'PASS: solid character prefab, item resource inheritance, item state save/load wiring.'
