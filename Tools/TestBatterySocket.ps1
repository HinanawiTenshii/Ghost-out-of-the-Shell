$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$socket=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocket.cs"
$inventory=Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
$battery=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItem.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Color {public float r,g,b,a;public static Color white=>new Color{r=1,g=1,b=1,a=1};}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);public static int Clamp(int v,int a,int b)=>Math.Max(a,Math.Min(v,b));public static int RoundToInt(float v)=>(int)Math.Round(v);}
public class Component{
 public bool isActiveAndEnabled=true;public List<MonoBehaviour> siblings=new List<MonoBehaviour>();
 public T[] GetComponents<T>() where T:Component {var found=new List<T>();if(this is T self)found.Add(self);foreach(var sibling in siblings)if(sibling is T value)found.Add(value);return found.ToArray();}
}
public class MonoBehaviour:Component{} public class WaterwayGateController:MonoBehaviour{public void RefreshBatteryPowerState(){}} public class LeverData:MonoBehaviour{}
public static class Debug {public static int warnings;public static void LogWarning(string text,object context){warnings++;}}
public class ZeldaCharacterData{}
public class ElectricGatePillar:MonoBehaviour {public bool HasPanel=true;}
public class AlarmDevice:MonoBehaviour {}
public class PlacedBomb {public static int detonations;public static Action onDetonate;public void DetonateImmediately(){detonations++;onDetonate?.Invoke();}}
public partial class BatteryPickupItem {public const string DefaultItemId="battery",DefaultItemName="魔力电池",DefaultDescription="battery description"; public static bool missingTemplate;public static PlacedBomb CreateDetonation(Component source,ZeldaCharacterData attacker)=>missingTemplate?null:new PlacedBomb();}
public class PersistentInventory {
 private Slot[] slots={new Slot(),new Slot(),new Slot(),new Slot(),new Slot()};public IReadOnlyList<Slot> Slots=>slots;
 public int SelectedSlotIndex;public int changed;public Action onChanged;
 public void NotifyChanged(){changed++;onChanged?.Invoke();}public void SelectSlot(int i){SelectedSlotIndex=i;}
 public Slot GetSlot(int i)=>i>=0&&i<slots.Length?slots[i]:null;
 public int GetQuantity(string id){int n=0;foreach(var s in slots)if(!s.IsEmpty&&s.ItemId==id)n+=s.Quantity;return n;}
 public bool Contains(string id,int n)=>GetQuantity(id)>=n;
'@
foreach($signature in @('public struct RuntimeSlotState','public sealed class Slot','public bool TryAddItem(', 'public bool TryAddUniqueItem(', 'public bool ContainsItemInstance(', 'public bool TryRemoveItemInstance(', 'public bool TryRemoveItem(', 'private int FindSlotByInstanceId(')){
 $code+=(Extract $inventory $signature) -replace '\[SerializeField[^\]]*\]\s*',''
}
$code+='} public partial class BatteryPickupItem { public const int DefaultCapacity=5;private const string ChargePrefix="battery-charge-v1:";'
foreach($signature in @('public struct ChargeState','public static string EncodeCharge(', 'public static ChargeState ReadCharge(')){$code+=Extract $battery $signature}
$code+='} public class BatterySocket:MonoBehaviour {'
$code+='private static readonly HashSet<BatterySocket> Sockets=new HashSet<BatterySocket>();private MonoBehaviour linkedMechanism;private bool batteryInstalled,transferring,initialized;public bool startsWithBattery;private PersistentInventory.RuntimeSlotState storedBattery;public bool HasBattery=>initialized?batteryInstalled:startsWithBattery;public BatterySocket(){Sockets.Add(this);} public void Destroy(){Sockets.Remove(this);} '
$code+='private BatteryPickupItem.ChargeState charge;public int RemainingCharge=>HasBattery?(initialized?charge.remaining:BatteryPickupItem.DefaultCapacity):0;public int MaximumCharge=>initialized&&HasBattery?charge.capacity:BatteryPickupItem.DefaultCapacity;public bool HasPower=>HasBattery&&RemainingCharge>0;'
$code+='public float interactionDistance=1.25f;public MonoBehaviour LinkedMechanism=>linkedMechanism;public void SimulateInspectorDrop(MonoBehaviour component){linkedMechanism=component;OnValidate();}'
foreach($signature in @('public struct RuntimeState','public static bool IsPowered(', 'public void BindMechanism(', 'private static bool TryResolveMechanism(', 'private static bool SupportsMechanism(', 'public static bool TryConsumeForMechanisms(', 'private void OnValidate(', 'private void EnsureInitialized(', 'private void EnsureBatteryIdentity(', 'private void StoreCharge(', 'public bool TryConsumeCharge(', 'public void ConsumeChargeFrom(', 'public RuntimeState CaptureRuntimeState(', 'public void ApplyRuntimeState(', 'public bool TryInstall(', 'public bool TryUninstall(', 'public void ReceiveAttack(')){$code+=Extract $socket $signature}
$code+=Extract $socket 'private void NotifyMechanismPowerState('
$code+='}'
$code=$code.Replace('public static bool IsPowered(', (Extract $socket 'public static bool HasConnectedSocket(')+'public static bool IsPowered(')
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$inv=[PersistentInventory]::new();$s=[BatterySocket]::new();$lever=[LeverData]::new();$controller=[WaterwayGateController]::new()
Check ([BatterySocket]::IsPowered($lever)) 'Unbound mechanisms keep their original behavior'
Check (![BatterySocket]::HasConnectedSocket($lever)) 'Unbound detection is distinct from legacy powered default'
$alarm=[AlarmDevice]::new();$alarmSupply=[BatterySocket]::new();$alarmSupply.BindMechanism($alarm)
Check ([BatterySocket]::HasConnectedSocket($alarm) -and ![BatterySocket]::IsPowered($alarm)) 'Alarm accepts binding but empty battery blocks use'
$alarmSupply.startsWithBattery=$true
Check ([BatterySocket]::HasConnectedSocket($alarm) -and [BatterySocket]::IsPowered($alarm)) 'Charged battery permits alarm'
$alarmSupply.Destroy()
Check (![BatterySocket]::HasConnectedSocket($alarm)) 'Removed supply cannot leave a powered alarm'
$s.BindMechanism($lever)
Check (![BatterySocket]::IsPowered($lever)) 'Empty bound socket denies power'
Check ([BatterySocket]::IsPowered($controller)) 'Unrelated controller unaffected'
Check (!$s.TryInstall($inv) -and !$s.TryUninstall($inv)) 'Empty inventory or socket causes no transfer'
Check ($inv.TryAddUniqueItem('battery','battery-A',1,1,'custom name','custom desc',$false,[Color]::white,$false,0,'battery-charge-v1:5:3')) 'Add one inventory battery'
$s.TryInstall($inv)|Out-Null
Check ($s.HasBattery -and $inv.GetQuantity('battery') -eq 0 -and [BatterySocket]::IsPowered($lever)) 'Install consumes exactly one battery and powers lever'
$snapshot=$s.CaptureRuntimeState()
Check ($snapshot.battery.itemInstanceId -eq 'battery-A' -and $snapshot.battery.itemState -eq 'battery-charge-v1:5:3') 'Installed battery identity and charge preserved'
Check (!$s.TryInstall($inv)) 'Cannot install into filled socket'
1..5|ForEach-Object {$inv.TryAddUniqueItem('filler',"filler-$_")|Out-Null}
Check (!$s.TryUninstall($inv) -and $s.HasBattery -and [BatterySocket]::IsPowered($lever)) 'Full inventory leaves battery and power unchanged'
$inv.TryRemoveItemInstance('filler-3')|Out-Null
Check ($s.TryUninstall($inv) -and !$s.HasBattery -and ![BatterySocket]::IsPowered($lever)) 'Free slot accepts battery and cuts power'
Check ($inv.ContainsItemInstance('battery-A') -and $inv.GetSlot(2).ItemName -eq 'custom name') 'Uninstall preserves ID and item name'
Check (!$s.TryUninstall($inv)) 'Cannot duplicate by repeated removal'
$inv.SelectSlot(0)
Check ($s.TryInstall($inv)) 'Finds battery anywhere in inventory when selected slot holds other item'
$s.ApplyRuntimeState($snapshot)
Check ($s.HasBattery -and $s.CaptureRuntimeState().battery.itemInstanceId -eq 'battery-A') 'Save restore keeps installed battery without generating duplicate ID'
$s.BindMechanism($controller)
Check ([BatterySocket]::IsPowered($lever) -and [BatterySocket]::IsPowered($controller)) 'Rebinding affects only newly linked mechanism'
$s.TryUninstall($inv)|Out-Null
Check (![BatterySocket]::IsPowered($controller)) 'Empty re-bound controller loses power'
$s.BindMechanism($null)
Check ([BatterySocket]::IsPowered($controller)) 'Removing binding restores normal behavior'
$rejected=$false;try{$s.BindMechanism([MonoBehaviour]::new())}catch{$rejected=$true}
Check $rejected 'Unsupported binding is rejected'
$s.BindMechanism($lever);$s.Destroy()
Check ([BatterySocket]::IsPowered($lever)) 'Destroyed socket leaves no stale lock'
$stack=[PersistentInventory]::new()
Check ($stack.TryAddItem('battery',5)) 'Five batteries can occupy five separate slots'
foreach($slot in $stack.Slots){Check ($slot.Quantity -eq 1) 'Battery never stacks above one even with default inventory maximum'}
Check (!$stack.TryAddItem('battery',1)) 'Sixth battery cannot enter full inventory'
$s2=[BatterySocket]::new()
Check ($s2.TryInstall($stack) -and $stack.GetQuantity('battery') -eq 4) 'Non-unique legacy battery installs exactly one'
Check (![string]::IsNullOrEmpty($s2.CaptureRuntimeState().battery.itemInstanceId)) 'Non-unique battery receives stable identity'
Check ($s2.TryUninstall($stack) -and $stack.GetQuantity('battery') -eq 5) 'Non-unique battery roundtrip preserves count'
$s3=[BatterySocket]::new();$s3.BindMechanism($controller)
$initial=[BatterySocket+RuntimeState]::new();$initial.installed=$true;$s3.ApplyRuntimeState($initial)
$initialId=$s3.CaptureRuntimeState().battery.itemInstanceId
Check (![string]::IsNullOrEmpty($initialId) -and $initialId -eq $s3.CaptureRuntimeState().battery.itemInstanceId) 'Initially installed battery ID is generated only once'
$s2.BindMechanism($controller)
Check (![BatterySocket]::IsPowered($controller)) 'Multiple bound sockets require each one to be populated'
$s2.Destroy()
Check ([BatterySocket]::IsPowered($controller)) 'Remaining filled socket powers controller'
$s3.Destroy()
$configured=[BatterySocket]::new();$configured.startsWithBattery=$true;$configured.BindMechanism($controller)
Check ($configured.HasBattery -and [BatterySocket]::IsPowered($controller)) 'Default battery option previews and supplies power before Awake'
$configuredInventory=[PersistentInventory]::new()
Check ($configured.TryUninstall($configuredInventory) -and $configuredInventory.GetQuantity('battery') -eq 1) 'Default battery is a real removable inventory item'
Check (!$configured.HasBattery -and !$configured.CaptureRuntimeState().installed) 'Default option does not respawn removed battery'
$emptyState=$configured.CaptureRuntimeState();$configured.Destroy()
$restoredDefault=[BatterySocket]::new();$restoredDefault.startsWithBattery=$true;$restoredDefault.ApplyRuntimeState($emptyState)
Check (!$restoredDefault.HasBattery -and !$restoredDefault.TryUninstall($configuredInventory)) 'Saved empty state overrides default-installed option'
$configuredInventory.TryRemoveItem('battery')|Out-Null
$restoredDefault.ApplyRuntimeState($snapshot)
Check ($restoredDefault.TryUninstall($configuredInventory) -and $configuredInventory.ContainsItemInstance('battery-A')) 'Saved installed identity takes precedence over generated initial battery'
$restoredDefault.Destroy()
$volatile=[BatterySocket]::new();$volatile.startsWithBattery=$true;$volatile.BindMechanism($controller)
$volatile.ReceiveAttack(0,$null);$volatile.ReceiveAttack(-1,$null)
Check ($volatile.HasBattery -and [PlacedBomb]::detonations -eq 0) 'Zero and negative damage do not explode installed battery'
[PlacedBomb]::onDetonate={
 Check (!$volatile.HasBattery -and ![BatterySocket]::IsPowered($controller)) 'Battery disappears and power is cut before explosion'
 $volatile.ReceiveAttack(4,$null)
}
$volatile.ReceiveAttack(1,$null);$volatile.ReceiveAttack(100,$null)
Check (!$volatile.HasBattery -and [PlacedBomb]::detonations -eq 1) 'Any positive damage detonates once despite reentrant and repeat hits'
Check (!$volatile.CaptureRuntimeState().installed -and [string]::IsNullOrEmpty($volatile.CaptureRuntimeState().battery.itemInstanceId)) 'Exploded battery and its identity are absent from save state'
Check (!$volatile.TryUninstall([PersistentInventory]::new())) 'Destroyed battery cannot be recovered from empty socket'
$replacement=[PersistentInventory]::new();$replacement.TryAddItem('battery',1)|Out-Null
Check ($volatile.TryInstall($replacement) -and $volatile.HasBattery) 'Socket remains reusable after explosion'
[BatteryPickupItem]::missingTemplate=$true;$volatile.ReceiveAttack(2,$null)
Check ($volatile.HasBattery) 'Configuration failure does not consume installed battery'
[BatteryPickupItem]::missingTemplate=$false;$volatile.ReceiveAttack(2,$null)
Check (!$volatile.HasBattery -and [PlacedBomb]::detonations -eq 2) 'Replacement battery can explode normally'
[PlacedBomb]::onDetonate=$null;$volatile.Destroy()
$powered=[BatterySocket]::new();$powered.startsWithBattery=$true;$powered.BindMechanism($controller)
Check ($powered.RemainingCharge -eq 5 -and $powered.HasPower) 'Default installed battery previews full before Awake'
$powered.ConsumeChargeFrom($lever)
Check ($powered.RemainingCharge -eq 5) 'Events from unrelated mechanisms cannot consume charge'
$powered.ConsumeChargeFrom($controller)
Check ($powered.RemainingCharge -eq 4) 'Explicit linked event consumes exactly one'
$chargeSnapshot=$powered.CaptureRuntimeState();$powered.Destroy()
$reloaded=[BatterySocket]::new();$reloaded.startsWithBattery=$true;$reloaded.BindMechanism($controller);$reloaded.ApplyRuntimeState($chargeSnapshot)
Check ($reloaded.RemainingCharge -eq 4) 'Installed charge survives scene/save restore without default refill'
$transport=[PersistentInventory]::new()
Check ($reloaded.TryUninstall($transport) -and $transport.GetSlot(0).StoredCharge -eq 4) 'Uninstall carries charge into inventory'
$transportState=$transport.GetSlot(0).ItemState
Check ($transportState -eq 'battery-charge-v1:5:4') 'Versioned integer charge accompanies inventory slot'
Check ($reloaded.TryInstall($transport) -and $reloaded.RemainingCharge -eq 4) 'Reinstall cannot recharge battery'
for($i=3;$i -ge 0;$i--){Check ($reloaded.TryConsumeCharge() -and $reloaded.RemainingCharge -eq $i) 'Repeated events consume one integer unit each'}
Check (!$reloaded.TryConsumeCharge() -and $reloaded.HasBattery -and ![BatterySocket]::IsPowered($controller)) 'Exhausted battery remains installed but cannot power mechanism or underflow'
$emptySnapshot=$reloaded.CaptureRuntimeState();$reloaded.ApplyRuntimeState($emptySnapshot)
Check ($reloaded.RemainingCharge -eq 0 -and $reloaded.HasBattery) 'Save/load preserves installed empty battery distinctly from empty socket'
Check ($reloaded.TryUninstall($transport) -and $transport.GetSlot(0).StoredCharge -eq 0) 'Empty battery can be removed and remains empty'
Check ($reloaded.TryInstall($transport) -and $reloaded.RemainingCharge -eq 0) 'Empty battery remains empty when reinstalled'
$reloaded.ReceiveAttack(1,$null)
Check (!$reloaded.HasBattery) 'Exhausted battery retains requested damage/explosion behavior'
$reloaded.Destroy()
$battery=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItem.cs"
Check ($battery.Contains('CanUseFromInventory => false') -and $battery.Contains('ApplyUseEffect(ZeldaCharacterData user) => false')) 'Battery rejects R and direct use without consumption'
Check ($inventory.IndexOf('CanUseFromInventory == false') -lt $inventory.IndexOf('controlledMover.ActiveCardboardBox.RequestExit()')) 'R rejection runs before world effects and box shortcuts'
Check ($socket.Contains('GetComponentsInChildren<BatterySocket>(true)')) 'Initially inactive scene sockets also register power restriction'
Check (!$socket.Contains('OnDisable() => Sockets.Remove')) 'Temporarily disabled socket cannot bypass requirement'
$leverSource=Get-Content -Raw "$root/Assets/Scripts/Interaction/LeverData.cs"
Check ($leverSource -match 'ActivateFromClockworkPuppet\(\)\s*\{\s*if \(!HasBatteryPower\) return;') 'Puppet cannot bypass battery requirement'
Check ($leverSource.Contains('characterData == null || !HasBatteryPower || characterData.FinalSkillValue < complexity')) 'Lever player path checks power before emitting signal'
$save=Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
Check ($save.Contains('batterySocket.CaptureRuntimeState()') -and $save.Contains('batterySocket.ApplyRuntimeState(state.batterySocketState)')) 'Socket participates in existing scene/save snapshot path'
$prefab=Get-Content -Raw "$root/Assets/Resources/PickupItems/BatteryPickupItem.prefab"
Check ($prefab.Contains('maxStackSize: 1') -and $prefab.Contains('pickupQuantity: 1') -and $prefab.Contains('itemId: battery')) 'Resource prefab configured for inventory and Q recreation'
$displayName=[regex]::Match($battery,'DefaultItemName = "([^"]+)"').Groups[1].Value
$description=[regex]::Match($battery,'DefaultDescription = "([^"]+)"').Groups[1].Value
Check ($displayName -eq '魔力电池' -and $prefab.Contains('itemName: "'+$displayName+'"')) 'Magic battery prefab and generated-item name match'
Check ($description.Length -gt 0 -and $prefab.Contains('itemDescription: "'+$description+'"')) 'Magic battery prefab and default description match'
$socketPrefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/BatterySocket.prefab"
Check ($socketPrefab.Contains('m_Layer: 0') -and $socketPrefab.Contains('linkedMechanism: {fileID: 0}')) 'Socket is masked world geometry with safe default unbound state'
Check ($prefab.Contains('m_Layer: 0')) 'Dropped battery uses the masked world layer'
$socketVisual=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocketVisual.cs"
$batteryVisual=Get-Content -Raw "$root/Assets/Scripts/Items/BatteryPickupItemVisual.cs"
foreach($visual in @($socketVisual,$batteryVisual)){
 Check ($visual.Contains('if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;')) 'Legacy device layer is migrated without changing unrelated custom layers'
}
Check ($batteryVisual.Contains('base.OnEnable();') -and $batteryVisual.Contains('breathingVisual.gameObject.layer = gameObject.layer;')) 'Battery breathing renderer follows migrated layer on enable'
foreach($file in @('Assets/Scripts/Items/BatteryPickupItem.cs','Assets/Scripts/Items/BatteryPickupItemVisual.cs','Assets/Scripts/Interaction/BatterySocket.cs','Assets/Scripts/Interaction/BatterySocketVisual.cs')){
 $guid=[regex]::Match((Get-Content -Raw "$root/$file.meta"),'guid: ([0-9a-f]{32})').Groups[1].Value
 Check ($guid.Length -eq 32 -and ($prefab.Contains($guid) -or $socketPrefab.Contains($guid))) 'Every prefab script has a resolvable matching meta GUID'
}
$gateA=[ElectricGatePillar]::new();$gateB=[ElectricGatePillar]::new();$bare=[ElectricGatePillar]::new();$bare.HasPanel=$false
$electricA=[BatterySocket]::new();$electricA.startsWithBattery=$true;$electricA.BindMechanism($gateA)
$electricB=[BatterySocket]::new();$electricB.BindMechanism($gateB)
$rejected=$false;try{$electricB.BindMechanism($bare)}catch{$rejected=$true}
Check $rejected 'Bare pillars cannot accept battery bindings'
Check (![BatterySocket]::TryConsumeForMechanisms($gateA,$gateB) -and $electricA.RemainingCharge -eq 5) 'Preflight is atomic when the other battery is absent'
$electricB.startsWithBattery=$true
Check ([BatterySocket]::TryConsumeForMechanisms($gateA,$gateB) -and $electricA.RemainingCharge -eq 4 -and $electricB.RemainingCharge -eq 4) 'Both connected batteries consume once per field trigger'
Check ([BatterySocket]::TryConsumeForMechanisms($gateA,$gateA) -and $electricA.RemainingCharge -eq 3) 'Duplicate endpoint cannot double-charge one socket'
1..3|ForEach-Object {Check ([BatterySocket]::TryConsumeForMechanisms($gateA,$null)) 'Successful single-panel trigger'}
Check ($electricA.RemainingCharge -eq 0 -and ![BatterySocket]::IsPowered($gateA)) 'Last trigger consumes final unit and cuts power'
Check (![BatterySocket]::TryConsumeForMechanisms($gateA,$gateB) -and $electricB.RemainingCharge -eq 4) 'Depleted endpoint cannot consume other battery or go negative'
Check ([BatterySocket]::TryConsumeForMechanisms($bare,$null)) 'Unbound standalone power policy remains supported'
$electricA.Destroy();$electricB.Destroy()
$dragSocket=[BatterySocket]::new()
foreach($device in @([ElectricGatePillar]::new(),[WaterwayGateController]::new(),[LeverData]::new())){
 $visual=[MonoBehaviour]::new();$visual.siblings.Add($device)
 $beforeWarnings=[Debug]::warnings
 $dragSocket.SimulateInspectorDrop($visual)
 Check ([object]::ReferenceEquals($dragSocket.LinkedMechanism,$device)) 'Whole-object drag resolves sibling functional component in actual OnValidate'
 Check ([Debug]::warnings -eq $beforeWarnings) 'Valid visual-first drag does not emit unsupported-binding warning'
 Check (![BatterySocket]::IsPowered($device)) 'Resolved reference actually participates in power gating'
 $dragSocket.BindMechanism($visual)
 Check ([object]::ReferenceEquals($dragSocket.LinkedMechanism,$device)) 'Runtime binding uses the same resolver as Inspector'
 $dragSocket.SimulateInspectorDrop($device)
 Check ([object]::ReferenceEquals($dragSocket.LinkedMechanism,$device)) 'Explicit valid component reference remains unchanged'
}
$bareVisual=[MonoBehaviour]::new();$bareVisual.siblings.Add($bare)
$dragSocket.SimulateInspectorDrop($bareVisual)
Check ($null -eq $dragSocket.LinkedMechanism) 'Visual-first drag cannot bypass disabled-panel restriction'
$dragSocket.SimulateInspectorDrop([MonoBehaviour]::new())
Check ($null -eq $dragSocket.LinkedMechanism) 'Unrelated object cannot resolve to another hierarchy device'
$dragSocket.BindMechanism($gateA)
$beforeBinding=$dragSocket.LinkedMechanism
$rejected=$false;try{$dragSocket.BindMechanism($bareVisual)}catch{$rejected=$true}
Check ($rejected -and [object]::ReferenceEquals($dragSocket.LinkedMechanism,$beforeBinding)) 'Invalid runtime binding preserves the previously valid link'
$dragSocket.SimulateInspectorDrop($null)
Check ($null -eq $dragSocket.LinkedMechanism) 'Inspector clearing remains supported'
$dragSocket.BindMechanism($gateA);$dragSocket.BindMechanism($null)
Check ($null -eq $dragSocket.LinkedMechanism) 'Runtime unbinding remains supported'
$dragSocket.Destroy()
Write-Output "PASS: $count production-method, inventory, power, save and prefab checks (Unity stubs, not Play Mode)."
