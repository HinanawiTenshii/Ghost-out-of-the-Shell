$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$socket=Get-Content -Raw "$root/Assets/Scripts/Interaction/BatterySocket.cs"
$inventory=Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $begin=$source.IndexOf('{',$start);$end=$begin+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Color {public float r,g,b,a;public static Color white=>new Color{r=1,g=1,b=1,a=1};}
public static class Mathf {public static int Max(int a,int b)=>Math.Max(a,b);public static float Max(float a,float b)=>Math.Max(a,b);public static int Min(int a,int b)=>Math.Min(a,b);}
public class Component{} public class MonoBehaviour:Component{} public class WaterwayGateController:MonoBehaviour{} public class LeverData:MonoBehaviour{}
public class BatteryPickupItem {public const string DefaultItemId="battery",DefaultItemName="魔力电池",DefaultDescription="battery description";}
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
$code+='} public class BatterySocket:MonoBehaviour {'
$code+='private static readonly HashSet<BatterySocket> Sockets=new HashSet<BatterySocket>();private MonoBehaviour linkedMechanism;private bool batteryInstalled,transferring,initialized;public bool startsWithBattery;private PersistentInventory.RuntimeSlotState storedBattery;public bool HasBattery=>initialized?batteryInstalled:startsWithBattery;public BatterySocket(){Sockets.Add(this);} public void Destroy(){Sockets.Remove(this);} '
foreach($signature in @('public struct RuntimeState','public static bool IsPowered(', 'public void BindMechanism(', 'private void EnsureInitialized(', 'private void EnsureBatteryIdentity(', 'public RuntimeState CaptureRuntimeState(', 'public void ApplyRuntimeState(', 'public bool TryInstall(', 'public bool TryUninstall(')){$code+=Extract $socket $signature}
$code+='}'
Add-Type -TypeDefinition $code
$count=0
function Check($ok,$label){if(!$ok){throw $label};$script:count++}
$inv=[PersistentInventory]::new();$s=[BatterySocket]::new();$lever=[LeverData]::new();$controller=[WaterwayGateController]::new()
Check ([BatterySocket]::IsPowered($lever)) 'Unbound mechanisms keep their original behavior'
$s.BindMechanism($lever)
Check (![BatterySocket]::IsPowered($lever)) 'Empty bound socket denies power'
Check ([BatterySocket]::IsPowered($controller)) 'Unrelated controller unaffected'
Check (!$s.TryInstall($inv) -and !$s.TryUninstall($inv)) 'Empty inventory or socket causes no transfer'
Check ($inv.TryAddUniqueItem('battery','battery-A',1,1,'custom name','custom desc',$false,[Color]::white,$false,0,'custom state')) 'Add one inventory battery'
$s.TryInstall($inv)|Out-Null
Check ($s.HasBattery -and $inv.GetQuantity('battery') -eq 0 -and [BatterySocket]::IsPowered($lever)) 'Install consumes exactly one battery and powers lever'
$snapshot=$s.CaptureRuntimeState()
Check ($snapshot.battery.itemInstanceId -eq 'battery-A' -and $snapshot.battery.itemState -eq 'custom state') 'Installed battery identity and metadata preserved'
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
Write-Output "PASS: $count production-method, inventory, power, save and prefab checks (Unity stubs, not Play Mode)."
