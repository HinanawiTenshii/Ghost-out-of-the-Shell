$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$battery=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItem.cs"
$stubs=@'
namespace UnityEngine {
 public class SerializeField:System.Attribute{} public class MinAttribute:System.Attribute{public MinAttribute(int value){}} public class InspectorNameAttribute:System.Attribute{public InspectorNameAttribute(string value){}}
 public static class Mathf {public static int Max(int a,int b)=>System.Math.Max(a,b);public static int Clamp(int v,int a,int b)=>System.Math.Max(a,System.Math.Min(v,b));public static int RoundToInt(float v)=>(int)System.Math.Round(v);public static int CeilToInt(float v)=>(int)System.Math.Ceiling(v);}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color clear=>new Color();}
 public struct Rect {public Rect(float x,float y,float w,float h){}} public struct Vector2 {public Vector2(float x,float y){}}
 public enum TextureFormat{RGBA32}public enum FilterMode{Point}public enum TextureWrapMode{Clamp}public enum HideFlags{HideAndDontSave}
 public class Texture2D {public string name;public FilterMode filterMode;public TextureWrapMode wrapMode;public HideFlags hideFlags;public Color[,] pixels;public Texture2D(int w,int h,TextureFormat format,bool mip){pixels=new Color[w,h];}public void SetPixel(int x,int y,Color color){pixels[x,y]=color;}public void Apply(bool mip,bool release){}}
 public class Sprite {public string name;public HideFlags hideFlags;public Texture2D texture;public static Sprite Create(Texture2D t,Rect r,Vector2 pivot,float ppu)=>new Sprite{texture=t};}
 public struct Vector3 {public float x,y,z;}
 public class Transform {public Vector3 position;}
 public class GameObject {public bool active=true,destroyed;public SceneManagement.Scene scene;public void SetActive(bool value){active=value;}}
 public class Component {public GameObject gameObject=new GameObject();public Transform transform=new Transform();public bool isActiveAndEnabled=>gameObject.active;}
 public static class Resources {public static object template;public static string lastPath;public static T Load<T>(string path) where T:class {lastPath=path;return template as T;}}
 public static class Debug {public static void LogError(string text,object context){}}
 namespace SceneManagement {
  public struct Scene {public int handle;public bool isLoaded;public bool IsValid()=>handle!=0;}
  public static class SceneManager {public static void MoveGameObjectToScene(GameObject go,Scene scene){go.scene=scene;}}
 }
}
public class ZeldaCharacterData {}
public class PickupItemBase:UnityEngine.Component {
 public virtual bool HasInventoryCharge=>false;public virtual float InventoryCharge=>0;public virtual string InventoryState=>"";public virtual void ApplyInventoryCharge(bool hasCharge,float charge){} public virtual void ApplyInventoryState(string state){}
 protected virtual bool CanAttemptPickup=>true;public virtual bool CanUseFromInventory=>true;
 protected virtual bool ApplyUseEffect(ZeldaCharacterData user)=>true;public virtual bool TryPickUp()=>true;
 protected void Destroy(UnityEngine.GameObject go){go.destroyed=true;}
}
public class PlacedBomb:UnityEngine.Component {public int detonations;public System.Action onDetonate;public void DetonateImmediately(){detonations++;onDetonate?.Invoke();}}
public class BombPickupItem {
 public int calls,power;public UnityEngine.Vector3 position;public ZeldaCharacterData owner;public PlacedBomb last;
 public PlacedBomb CreateConfiguredPlacedBomb(UnityEngine.Vector3 p,ZeldaCharacterData user,int? attackPowerOverride=null){calls++;position=p;owner=user;power=attackPowerOverride??3;return last=new PlacedBomb();}
}
'@
$visualSource=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItemVisual.cs"
function ExtractVisual($signature){
 $start=$visualSource.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$visualSource.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($visualSource[$end] -eq '{'){$depth++};if($visualSource[$end] -eq '}'){$depth--};$end++}
 $visualSource.Substring($start,$end-$start)
}
$visualHarness='public static class BatteryVisualHarness {private static readonly Sprite[] chargeSprites=new Sprite[8];'
$visualHarness+=(ExtractVisual 'private static Sprite GetChargeSprite(').Replace('private static','public static')
$visualHarness+=ExtractVisual 'public static Color PixelColor('
$visualHarness+=(ExtractVisual 'private static readonly string[] FullRows')+';}'
$consumableExplosive=Get-Content -Raw "$root/Assets/Scripts/Items/IConsumableExplosive.cs"
Add-Type -TypeDefinition ($battery+"`n"+$consumableExplosive+"`n"+$stubs+"`n"+$visualHarness)
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$template=[BombPickupItem]::new();[UnityEngine.Resources]::template=$template
$batteryItem=[BatteryPickupItem]::new();$attacker=[ZeldaCharacterData]::new()
$scene=[UnityEngine.SceneManagement.Scene]::new();$scene.handle=17;$scene.isLoaded=$true;$batteryItem.gameObject.scene=$scene
$position=[UnityEngine.Vector3]::new();$position.x=12;$position.y=-8;$batteryItem.transform.position=$position
$batteryItem.ReceiveAttack(0,$attacker);$batteryItem.ReceiveAttack(-5,$attacker)
Check ($template.calls -eq 0 -and !$batteryItem.gameObject.destroyed) 'Non-damage does not destroy battery'
$batteryItem.ReceiveAttack(1,$attacker)
Check ($template.calls -eq 1 -and $template.power -eq 4) 'Minimum positive damage creates exactly one attack-power-4 explosion'
Check ([UnityEngine.Resources]::lastPath -eq 'PickupItems/Bombs/BombPickupItem') 'Uses ordinary bomb resource, not super bomb'
Check ($template.owner -eq $attacker -and $template.position.x -eq 12 -and $template.position.y -eq -8) 'Preserves instigator and world position'
Check ($template.last.gameObject.scene.handle -eq 17 -and $template.last.detonations -eq 1) 'Immediate explosion in battery scene'
Check ($batteryItem.gameObject.destroyed -and !$batteryItem.gameObject.active -and !$batteryItem.TryPickUp()) 'Exploded battery is disabled, destroyed and cannot be picked up'
$batteryItem.ReceiveAttack(4,$attacker);$batteryItem.ReceiveAttack(100,$null)
Check ($template.calls -eq 1) 'Repeated and chain-reaction callbacks cannot duplicate explosion'
Check (!$batteryItem.CanUseFromInventory) 'Battery still cannot be used with R'
[UnityEngine.Resources]::template=$null;$retry=[BatteryPickupItem]::new();$retry.ReceiveAttack(1,$null)
Check (!$retry.gameObject.destroyed -and $retry.gameObject.active) 'Missing prefab fails safely without consuming battery'
[UnityEngine.Resources]::template=$template;$retry.ReceiveAttack(99,$null)
Check ($template.calls -eq 2 -and $template.owner -eq $null -and $retry.gameObject.destroyed) 'Environment damage works and failed configuration can recover'
$bombSource=Get-Content -Raw "$root/Assets/Scripts/Items/BombPickupItem.cs"
Check ($bombSource.Contains('int? attackPowerOverride = null') -and $bombSource.Contains('attackPowerOverride ?? explosionAttackPower,')) 'Shared bomb factory changes only power when explicitly overridden'
$hitbox=Get-Content -Raw "$root/Assets/Scripts/Characters/ZeldaAttackHitbox.cs"
Check ($hitbox.Contains('battery.ReceiveAttack(attackPower, owner)') -and $hitbox.Contains('batterySocket.ReceiveAttack(attackPower, owner)')) 'Common attack hitbox routes loose and installed battery damage'
$placed=Get-Content -Raw "$root/Assets/Scripts/Items/PlacedBomb.cs"
foreach($call in @('CreateAttackHitbox(explosionPosition);','CreateExplosionVisual(explosionPosition);','PlayExplosionSound(explosionPosition);','NotifyNearbyAi(explosionPosition);','CreateInvestigationPulse(explosionPosition);')){
 Check ($placed.Contains($call)) 'Shared detonation retains damage, visuals, audio and attraction'
}
Write-Output "PASS: $count battery explosion production-method and integration checks (Unity stubs, not Play Mode)."

$fresh=[BatteryPickupItem]::new()
Check ($fresh.RemainingCharge -eq 5 -and $fresh.MaximumCharge -eq 5 -and $fresh.HasInventoryCharge) 'New battery defaults to integer 5/5'
for($charge=0;$charge -le 5;$charge++){
 $saved=[BatteryPickupItem]::EncodeCharge($charge,5)
 $fresh.ApplyInventoryCharge($true,$charge);$fresh.ApplyInventoryState($saved)
 Check ($fresh.RemainingCharge -eq $charge -and $fresh.InventoryState -eq $saved) 'Every charge including zero survives inventory restore'
 $dropped=[BatteryPickupItem]::new();$dropped.ApplyInventoryCharge($fresh.HasInventoryCharge,$fresh.InventoryCharge);$dropped.ApplyInventoryState($fresh.InventoryState)
 Check ($dropped.RemainingCharge -eq $charge) 'Drop/pickup metadata keeps exact charge'
 $sceneRestored=[BatteryPickupItem]::new();$sceneRestored.ApplyInventoryState($saved)
 Check ($sceneRestored.RemainingCharge -eq $charge) 'Scene runtime string alone restores charge'
}
$fresh.ApplyInventoryCharge($false,0);$fresh.ApplyInventoryState('')
Check ($fresh.RemainingCharge -eq 5) 'Old inventory without charge metadata defaults to full'
$fresh.ApplyInventoryCharge($true,0);$fresh.ApplyInventoryState('')
Check ($fresh.RemainingCharge -eq 0) 'Explicit zero in old shared charge field does not refill'
$fresh.ApplyInventoryState([BatteryPickupItem]::EncodeCharge(7,9))
Check ($fresh.RemainingCharge -eq 7 -and $fresh.MaximumCharge -eq 9) 'Custom capacity survives scene and inventory serialization'
$fresh.ApplyInventoryState('battery-charge-v1:5:99')
Check ($fresh.RemainingCharge -eq 5) 'Malformed excessive charge clamps to capacity'
$fresh.ApplyInventoryCharge($true,2.6)
Check ($fresh.RemainingCharge -eq 3) 'Legacy fractional charge normalizes to integer'
$save=Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
$inventory=Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
Check ($save.Contains('state.pickupRuntimeState = pickup.InventoryState;') -and $save.Contains('pickup.ApplyInventoryState(state.pickupRuntimeState);')) 'Scene snapshot stores and reapplies integer state'
Check ($inventory.Contains('droppedItem.ApplyInventoryState(droppedItemState)') -and $inventory.Contains('itemState = slots[i].ItemState')) 'Inventory save/drop retains serialized charge state'
$visual=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItemVisual.cs"
$hud=Get-Content -Raw "$root/Assets/Scripts/UI/ZeldaHealthHeartsUI.cs"
$window=Get-Content -Raw "$root/Assets/Scripts/Items/ItemDescriptionWindow.cs"
Check ($visual.Contains('ReadCharge(slot.ItemState, slot.HasStoredCharge, slot.StoredCharge)') -and $hud.Contains('itemVisual.GetInventoryIcon(slot)') -and $window.Contains('visual.GetInventoryIcon(slot)')) 'Inventory and description icons use per-item data without mutating prefab'
Write-Output "PASS: $count battery explosion/charge/save/visual integration checks in total (Unity stubs, not Play Mode)."
$previousRed=-1
for($charge=0;$charge -le 5;$charge++){
 $frame=[BatteryVisualHarness]::GetChargeSprite($charge,5);$red=0
 foreach($pixel in $frame.texture.pixels){if($pixel.r -gt .3 -and $pixel.r -gt $pixel.g*2){$red++}}
 Check ($red -gt $previousRed) 'Each charge step has strictly more visible red pixels than the previous step'
 if($charge -eq 0){Check ($red -eq 0) 'Zero-charge battery has no red core pixels'}
 $previousRed=$red
 Check ([object]::ReferenceEquals($frame,[BatteryVisualHarness]::GetChargeSprite($charge,5))) 'Charge frame is cached instead of allocating every UI/world frame'
}
Write-Output "PASS: $count total checks including actual pixel generation and frame cache."
$socketVisual=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketVisual.cs"
Check ($socketVisual.Contains("Fill(7, 8, 6, 8, 'S')") -and $socketVisual.Contains("Fill(7, 8, 6, height, 'R')")) 'Installed battery core and empty region both span the six-pixel end-cap width'
Write-Output "PASS: $count checks including installed battery core alignment."
$crossPattern=@('8,6','8,7','7,8','8,8','9,8','8,9') | Sort-Object
for($charge=0;$charge -le 5;$charge++){
 $frame=[BatteryVisualHarness]::GetChargeSprite($charge,5);$actualCross=@()
 for($x=5;$x -le 10;$x++){for($y=4;$y -le 10;$y++){
  $pixel=$frame.texture.pixels[$x,$y]
  if([Math]::Abs($pixel.r-.78) -lt .001 -and [Math]::Abs($pixel.g-.83) -lt .001){$actualCross+="$x,$y"}
 }}
 Check ((($actualCross | Sort-Object) -join ';') -eq ($crossPattern -join ';')) 'All item charge frames vertically flip the cross within its original four-pixel bounds'
}
Check ($socketVisual.Contains("Fill(10, 11, 1, 4, 'H'); Fill(9, 12, 3, 1, 'H');")) 'Reference socket cross remains unchanged'
Write-Output "PASS: $count checks including vertically flipped item cross at all charge levels."
