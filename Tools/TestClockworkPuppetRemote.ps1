$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$inventory = Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
$runtime = Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetRuntime.cs"
$driver = Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetBoxDriver.cs"
$box = Get-Content -Raw "$root/Assets/Scripts/Items/CardboardBoxPickupItem.cs"
$remote = Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetRemotePickupItem.cs"
$visual = Get-Content -Raw "$root/Assets/Scripts/Items/ClockworkPuppetRemotePickupItemVisual.cs"
$travel = Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
function Extract($source, $signature) {
    $start = $source.IndexOf($signature)
    if ($start -lt 0) { throw "Missing method: $signature" }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    return $source.Substring($start, $end - $start)
}
# Execute production inventory transactions, reconnect guards and scalar save logic.
# Only engine/input/graphics/streaming services are stubbed. No PlayerPrefs are touched.
$code = @'
#pragma warning disable 0649, 0414
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public class SerializeField:Attribute {} public class TextAreaAttribute:Attribute {public TextAreaAttribute(int a,int b){}}
 public class MinAttribute:Attribute {public MinAttribute(float v){}}
 public class GameObject {public int scene=1;public bool activeSelf=true,destroyed; public void SetActive(bool v){activeSelf=v;}}
 public class Transform {public Quaternion rotation;}
 public struct Quaternion {public static Quaternion identity=>new Quaternion();}
 public struct Vector2 {public float x,y;public static Vector2 zero=>new Vector2();public static implicit operator Vector3(Vector2 v)=>new Vector3{x=v.x,y=v.y};public static explicit operator Vector2(Vector3 v)=>new Vector2{x=v.x,y=v.y};}
 public struct Vector3 {public float x,y,z;}
 public struct Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color white=>new Color(1,1,1,1);public static Color clear=>new Color();}
 public static class Mathf {public static float Max(float a,float b)=>Math.Max(a,b);public static int Max(int a,int b)=>Math.Max(a,b);public static int Clamp(int v,int a,int b)=>Math.Min(b,Math.Max(a,v));}
 public class MonoBehaviour {public GameObject gameObject=new GameObject(); public Transform transform=new Transform();public bool isActiveAndEnabled=true;public static void Destroy(GameObject go){go.destroyed=true;}}
 public static class Time {public static int frameCount=100;public static float timeScale=1;}
 public class Rigidbody2D {public Vector2 velocity;}
}
public class ZeldaCharacterCommonData:MonoBehaviour {}
public class ZeldaCharacterData:ZeldaCharacterCommonData {public bool IsDead,IsGhostLike;public ZeldaFourWayMover mover;public T GetComponent<T>() where T:class=>mover as T;}
public class ZeldaFourWayMover:MonoBehaviour {public int gestures;public void TryBeginItemAttack(float duration){gestures++;}}
public static class ZeldaRuntimeRegistry {public static ZeldaFourWayMover Controlled;public static int activeScene=1;public static ZeldaFourWayMover GetControlledMover()=>Controlled;public static int GetGameplayScene(GameObject go)=>go.scene==-1?activeScene:go.scene;}
public class SceneTravelStateManager {public static SceneTravelStateManager Instance;public bool IsSceneTravelRestoreInProgress;}
public static class GameSaveSystem {public static bool IsLoading;}
public static class DocumentReader {public static bool IsInputBlocked;}
public class ZeldaHealthHeartsUI {public static ZeldaHealthHeartsUI Instance=new ZeldaHealthHeartsUI();public string last;public void ShowNotificationPopup(string s){last=s;}}
public class PickupItemBase:MonoBehaviour {public string UniqueInstanceId;public int gestures;public void PlayUseGesture(ZeldaCharacterData user){gestures++;}public virtual bool CanBeStoredInCardboardBox(ZeldaCharacterData user)=>true;protected virtual bool CanAttemptPickup=>true;protected virtual bool ApplyUseEffect(ZeldaCharacterData user)=>true;protected virtual void OnDropped(){}public void Drop()=>OnDropped();}
public class PickupItemVisualBase {protected virtual string RuntimeSpriteName=>"";protected virtual bool UsesEmbeddedColors=>false;protected virtual Color GetPixelColor(char c)=>Color.clear;protected virtual string[] GetPixelRows()=>null;}
public class ClockworkPuppetPickupItem {public const string DefaultItemId="clockwork_puppet";}
public class ZeldaReusableGridNavigator {public void InvalidatePath(){}}
public class LeverData {}
public class FortressBallista {}
public class CardboardBoxPickupItem {public bool IsDestroyed;public bool HasStoredItem=true;}
'@
$code += $remote.Replace('using UnityEngine;', '') + $visual.Replace('using UnityEngine;', '')
$savedFields = Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SavedScalarFields.cs"
$code += ($savedFields -replace '(?m)^using .+;\r?\n', '')
$code += "public class PersistentInventory {`n"
foreach ($sig in @('public struct RuntimeSlotState','public struct RuntimeState','public sealed class Slot')) {
    $code += (Extract $inventory $sig) + "`n"
}
$code += @'
 public static PersistentInventory Instance;
 public Slot[] slots={new Slot(),new Slot(),new Slot(),new Slot(),new Slot()};
 public int SelectedSlotIndex;public event Action<int> SelectedSlotChanged;public int changes;
 void NotifyChanged(){changes++;}void EnsureSlots(){}bool IsValidSlotIndex(int i)=>i>=0&&i<slots.Length;
'@
foreach ($sig in @('public RuntimeState CaptureRuntimeState(', 'public void ApplyRuntimeState(', 'public bool TryAddUniqueItem(', 'public bool ContainsItemInstance(', 'public bool TryReplacePuppetWithRemote(', 'public bool RemoveClockworkPuppetRemote(', 'private int FindSlotByInstanceId(', 'public bool SwapSlots(', 'public bool SelectSlot(')) {
    $code += (Extract $inventory $sig) + "`n"
}
$code += "}`npublic class ClockworkPuppetRuntime:MonoBehaviour {`n"
$code += @'
 public static List<ClockworkPuppetRuntime> ActivePuppets=new List<ClockworkPuppetRuntime>();
 static ClockworkPuppetRuntime remoteControlledPuppet;static int characterInputBlockedThroughFrame=-1;
 public string itemInstanceId,itemId="clockwork_puppet",itemName="Custom puppet",itemDescription="Custom description";public float magic=12;public bool broken,attached,manualControlMode,hasVisualColor;public Color visualColor=Color.white;public int maxStackSize=1;
 ZeldaFourWayMover remoteControlOwner;int remoteControlEnteredFrame=-1;Transform initialOwner;
 Vector2 manualMoveDirection;Rigidbody2D physicsBody=new Rigidbody2D();ZeldaReusableGridNavigator navigator;LeverData targetLever;
 public string PuppetInstanceId=>itemInstanceId;public bool IsUnderRemoteControl=>remoteControlledPuppet==this;
 public static ClockworkPuppetRuntime RemoteControlledPuppet=>remoteControlledPuppet;
 public static bool BlocksCharacterInput=>remoteControlledPuppet!=null||ClockworkPuppetBoxDriver.IsRemoteControlActive||Time.frameCount<=characterInputBlockedThroughFrame;
 public void ReclaimForTest()=>Reclaim();public void Spend(float v)=>SpendMagic(v);
 void EnsureControlledCharacterStreamingExemption(){}void ReleaseControlledCharacterStreamingExemption(){}
 void ResetManualDirectionInput(){}void ResetPryProgress(){}void SetReclaimPromptVisible(bool v){}void UpdateMagicVisual(){}
 void AttachToLever(LeverData v){targetLever=v;attached=true;}
 void AssignTargetBallista(FortressBallista v){}bool AttachToBallista(FortressBallista v){attached=true;return true;}
'@
foreach ($sig in @('public bool TryResumeRemoteControl(', 'public static void EndActiveRemoteControl(', 'public void RestoreSavedControl(', 'private void BeginRemoteControl(', 'private void EndRemoteControl(', 'private void Reclaim(', 'private void SpendMagic(', 'private void BreakPuppet(')) {
    $code += (Extract $runtime $sig) + "`n"
}
$code += "}`npublic class ClockworkPuppetBoxDriver:MonoBehaviour {`n"
$code += @'
 public static List<ClockworkPuppetBoxDriver> ActiveDrivers=new List<ClockworkPuppetBoxDriver>();
 static ClockworkPuppetBoxDriver remoteControlledDriver;static int characterInputBlockedThroughFrame=-1;
 public string itemInstanceId;public float magic=12;public bool disembarking;public CardboardBoxPickupItem box=new CardboardBoxPickupItem();
 public string PuppetInstanceId=>itemInstanceId;public bool IsUnderRemoteControl=>remoteControlledDriver==this;
 public static bool IsRemoteControlActive=>remoteControlledDriver!=null;
 ZeldaFourWayMover remoteControlOwner;int remoteControlEnteredFrame=-1;Vector2 manualMoveDirection;
 Rigidbody2D physicsBody=new Rigidbody2D();ZeldaReusableGridNavigator navigator;
 void EnsureOwnerStreamingExemption(){}void ReleaseOwnerStreamingExemption(){}
 void ResetManualDirectionInput(){}void ResetPryProgress(){}void SetPromptVisible(bool v){}
'@
foreach ($sig in @('public bool TryResumeRemoteControl(', 'public static void EndActiveRemoteControl(', 'private void BeginRemoteControl(', 'private void EndRemoteControl(')) {
    $code += (Extract $driver $sig) + "`n"
}
$code += @'
}
public static class ClockworkRemoteTests {
 static int checks;static void Check(bool v,string m){checks++;if(!v)throw new Exception(m);}
 static PersistentInventory Bag(){var bag=new PersistentInventory();PersistentInventory.Instance=bag;return bag;}
 static ClockworkPuppetRuntime Puppet(string id,int scene=1,float energy=12){var p=new ClockworkPuppetRuntime{itemInstanceId=id,magic=energy};p.gameObject.scene=scene;ClockworkPuppetRuntime.ActivePuppets.Add(p);return p;}
 static void Exit(){ClockworkPuppetRuntime.EndActiveRemoteControl();ClockworkPuppetBoxDriver.EndActiveRemoteControl();Time.frameCount++;}
 public static string[] Rows()=>(string[])typeof(ClockworkPuppetRemotePickupItemVisual).GetMethod("GetPixelRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new ClockworkPuppetRemotePickupItemVisual(),null);
 public static Color Pixel(char c)=>(Color)typeof(ClockworkPuppetRemotePickupItemVisual).GetMethod("GetPixelColor",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new ClockworkPuppetRemotePickupItemVisual(),new object[]{c});
 public static int Run(){
  var bag=Bag();bag.slots[2].Set("clockwork_puppet",1,"P1","Custom puppet","Custom description",true,new Color(.7f,.8f,.9f,1),true,8.25f);
  for(int i=0;i<5;i++)if(i!=2)bag.slots[i].Set("filler",1,"F"+i);
  bag.SelectSlot(2);Check(bag.TryReplacePuppetWithRemote("P1"),"Deployment replaces in full bag");
  Check(bag.SelectedSlotIndex==2&&bag.slots[2].ItemId==ClockworkPuppetRemotePickupItem.DefaultItemId&&bag.slots[2].ItemInstanceId=="P1","Exact original slot/identity retained");
  Check(!bag.TryReplacePuppetWithRemote("P1")&&!bag.TryReplacePuppetWithRemote("missing"),"Cannot duplicate remote or convert unrelated items");
  var user=new ZeldaCharacterData{mover=new ZeldaFourWayMover()};ZeldaRuntimeRegistry.Controlled=user.mover;
  var puppet=Puppet("P1",1,8.25f);puppet.hasVisualColor=true;puppet.visualColor=new Color(.7f,.8f,.9f,1);
  puppet.ReclaimForTest();Check(puppet.gameObject.destroyed&&bag.slots[2].ItemId=="clockwork_puppet","Recovery atomically replaces remote in full bag");
  Check(bag.slots[2].StoredCharge==8.25f&&bag.slots[2].ItemName=="Custom puppet"&&bag.slots[2].HasVisualColor,"Energy/custom metadata survive recovery");
  Check(!bag.RemoveClockworkPuppetRemote("P1")&&!bag.TryAddUniqueItem("clockwork_puppet","P1"),"A late unlink never deletes recovered puppet; duplicate pickup rejected");
  Check(bag.TryReplacePuppetWithRemote("P1"),"Redeploy regenerates remote");
  bag.SwapSlots(2,4);Check(bag.TryAddUniqueItem("clockwork_puppet","P1",1,1,"Recovered","",false,default(Color),true,7),"Recovery follows stable identity after drag");
  Check(bag.SelectedSlotIndex==4&&bag.slots[4].StoredCharge==7,"Moved slot selected and latest energy preserved");
  bag.TryReplacePuppetWithRemote("P1");Check(bag.RemoveClockworkPuppetRemote("P1")&&bag.slots[4].IsEmpty,"Dropping removes projected item");
  bag.slots[4].Set("filler",1,"F4");Check(!bag.TryAddUniqueItem("clockwork_puppet","P1"),"After discarding remote, full bag cannot recover puppet");
  bag.slots[1].Clear();Check(bag.TryAddUniqueItem("clockwork_puppet","P1")&&bag.TryReplacePuppetWithRemote("P1"),"Free slot recovery and later use regenerate link");
  bag.slots[0].Set("clockwork_puppet",1,"P2");bag.TryReplacePuppetWithRemote("P2");
  Check(bag.RemoveClockworkPuppetRemote("P1")&&bag.slots[0].ItemInstanceId=="P2","Separate puppet unaffected by first link destruction");
  ClockworkPuppetRuntime.ActivePuppets.Clear();var remote=new ClockworkPuppetRemotePickupItem();puppet=Puppet("P2");
  Check(remote.TryReconnect(user,"P2")&&puppet.IsUnderRemoteControl,"R resumes bound target");Exit();
  Check(remote.TryReconnect(user,"P2"),"R repeat resumes after exit");Exit();
  Check(!remote.TryReconnect(user,"absent")&&bag.slots[0].ItemInstanceId=="P2","Missing target does not consume link");
  user.IsGhostLike=true;Check(!remote.TryReconnect(user,"P2"),"Ghost rejected");user.IsGhostLike=false;
  user.IsDead=true;Check(!remote.TryReconnect(user,"P2"),"Dead user rejected");user.IsDead=false;
  DocumentReader.IsInputBlocked=true;Check(!remote.TryReconnect(user,"P2"),"Modal UI rejected");DocumentReader.IsInputBlocked=false;
  Time.timeScale=0;Check(!remote.TryReconnect(user,"P2"),"Pause rejected");Time.timeScale=1;
  GameSaveSystem.IsLoading=true;Check(!remote.TryReconnect(user,"P2"),"Disk restore rejected until complete");GameSaveSystem.IsLoading=false;
  SceneTravelStateManager.Instance=new SceneTravelStateManager{IsSceneTravelRestoreInProgress=true};Check(!remote.TryReconnect(user,"P2"),"Travel restore rejected until complete");SceneTravelStateManager.Instance=null;
  user.gameObject.scene=2;Check(!remote.TryReconnect(user,"P2"),"Even additively loaded other-scene target cannot seize control");
  user.gameObject.scene=-1;ZeldaRuntimeRegistry.activeScene=2;Check(!remote.TryReconnect(user,"P2"),"Persistent player respects gameplay scene");
  ZeldaRuntimeRegistry.activeScene=1;Check(remote.TryReconnect(user,"P2"),"Returning persistent player reconnects");Exit();
  puppet.magic=5.75f;var fields=SavedScalarFields.Capture(puppet,true);var inventoryState=bag.CaptureRuntimeState();
  ClockworkPuppetRuntime.ActivePuppets.Clear();bag.ApplyRuntimeState(inventoryState);
  Check(!remote.TryReconnect(user,"P2")&&bag.slots[0].ItemInstanceId=="P2","Scene unload is not death and inventory binding survives");
  var restored=Puppet("temporary",1,12);SavedScalarFields.Apply(restored,fields);restored.RestoreSavedControl(null,false,false);
  Check(restored.PuppetInstanceId=="P2"&&restored.magic==5.75f&&!restored.IsUnderRemoteControl,"Restored runtime uses saved ID/energy without auto-control");
  Time.frameCount++;Check(remote.TryReconnect(user,"P2")&&restored.IsUnderRemoteControl,"Recreated target reconnects after return/load");Exit();
  var boxed=new ClockworkPuppetBoxDriver{itemInstanceId="BOX"};ClockworkPuppetBoxDriver.ActiveDrivers.Add(boxed);
  Check(remote.TryReconnect(user,"BOX")&&boxed.IsUnderRemoteControl,"Box-contained puppet reconnects");Exit();
  boxed.gameObject.scene=2;Check(!remote.TryReconnect(user,"BOX"),"Box remote cannot cross scenes");boxed.gameObject.scene=1;
  boxed.box.IsDestroyed=true;Check(!remote.TryReconnect(user,"BOX"),"Broken container rejected");boxed.box.IsDestroyed=false;
  Check(remote.TryReconnect(user,"BOX"),"Box target reusable");
  remote.UniqueInstanceId="P2";remote.Drop();Check(boxed.IsUnderRemoteControl,"Discarding a different link never exits current target");
  remote=new ClockworkPuppetRemotePickupItem{UniqueInstanceId="BOX"};remote.Drop();Check(!boxed.IsUnderRemoteControl&&remote.gameObject.destroyed&&!remote.gameObject.activeSelf,"World drop fallback dissolves immediately and ends matching control");Exit();
  restored.Spend(6);Check(restored.broken&&restored.gameObject.destroyed&&bag.slots[0].IsEmpty,"Energy exhaustion destroys target and matching inventory link");
  Check(!remote.CanBeStoredInCardboardBox(user),"Remote cannot be stored as physical container payload");
  var rows=Rows();Check(rows.Length==16,"Icon height");foreach(string row in rows)Check(row.Length==16,"Icon width");
  Check(Pixel('f').a<.2f&&Pixel('g').a<.4f&&Pixel('W').a==1,"Transparent energy body, fringe and white hot rune");
  return checks;
 }
 public static PersistentInventory.RuntimeState Snapshot(){var b=Bag();b.slots[3].Set("clockwork_puppet",1,"SAVED-PUPPET");b.TryReplacePuppetWithRemote("SAVED-PUPPET");b.SelectSlot(3);return b.CaptureRuntimeState();}
 public static void ValidateRoundTrip(PersistentInventory.RuntimeState state){var b=Bag();b.ApplyRuntimeState(state);Check(b.SelectedSlotIndex==3&&b.slots[3].ItemInstanceId=="SAVED-PUPPET"&&b.slots[3].ItemId=="clockwork_puppet_remote","JSON inventory preserves slot, identity, remote type");}
}
'@
Add-Type -TypeDefinition $code -WarningAction SilentlyContinue
Write-Output "PASS: $([ClockworkRemoteTests]::Run()) inventory lifecycle, reconnect, cross-scene and visual checks (engine services stubbed)."
$state = [ClockworkRemoteTests]::Snapshot() | ConvertTo-Json -Depth 10 | ConvertFrom-Json
[ClockworkRemoteTests]::ValidateRoundTrip([PersistentInventory+RuntimeState]$state)
Write-Output 'PASS: inventory JSON round trip.'

# Verify the engine integration paths that are intentionally not stubbed/executed above.
$travelMethod = Extract $travel 'private void TravelToSceneCovered('
if ($travelMethod.IndexOf('ClockworkPuppetRuntime.EndActiveRemoteControl();') -gt $travelMethod.IndexOf('CaptureScene(currentScene);') -or !$travelMethod.Contains('ClockworkPuppetBoxDriver.EndActiveRemoteControl();')) { throw 'Travel must release control before snapshot' }
foreach($source in @($runtime,$driver)) {
    if ((Extract $source 'private void OnDestroy(').Contains('RemoveClockworkPuppetRemote')) { throw 'Scene unload must not delete remotes' }
    if (!$source.Contains('Time.frameCount == remoteControlEnteredFrame')) { throw 'Same-R-frame protection missing' }
}
$configure = Extract $driver 'public void Configure('
if (!$configure.Contains('if (beginRemoteControl) BeginRemoteControl();') -or !(Extract $box 'public void RestoreSavedContents(').Contains('storedItemVisualColor, wasRemoteControlled)')) { throw 'Idle saved box steals control' }
foreach($method in @('public bool DestroyStoredClockworkPuppet(', 'private void BreakGroundBox(')) {
    if (!(Extract $box $method).Contains('RemoveClockworkPuppetRemote(storedItemInstanceId)')) { throw 'Box death must unlink' }
}
if ((Extract $box 'private void ClearStoredItem(').Contains('RemoveClockworkPuppetRemote')) { throw 'Box disembark must keep binding' }
$drop = Extract $inventory 'public bool TryDropSelectedItem('
if ($drop.IndexOf('RemoveClockworkPuppetRemote') -gt $drop.IndexOf('Instantiate(')) { throw 'Q must remove before creating any world item' }
$use = Extract $inventory 'public bool TryUseSelectedItem('
if (!$use.Contains('magicRemote.TryReconnect(user,slot.ItemInstanceId)') -or !$use.Contains('TryReplacePuppetWithRemote(slot.ItemInstanceId)')) { throw 'R integration missing' }
if (!$travel.Contains('state.deviceFields = SavedScalarFields.Capture(puppet, true)') -or !$travel.Contains('SceneManager.MoveGameObjectToScene(device.gameObject, scene);')) { throw 'Runtime device scene persistence missing' }
$prefab = Get-Content -Raw "$root/Assets/Resources/PickupItems/ClockworkPuppetRemotePickupItem.prefab"
if (!$prefab.Contains('itemId: clockwork_puppet_remote') -or !$prefab.Contains([ClockworkPuppetRemotePickupItem]::DefaultDescription)) { throw 'Prefab identity/description mismatch' }
foreach($script in @('ClockworkPuppetRemotePickupItem','ClockworkPuppetRemotePickupItemVisual')) {
    $guid=[regex]::Match((Get-Content -Raw "$root/Assets/Scripts/Items/$script.cs.meta"),'guid: (\w+)').Groups[1].Value
    if (!$prefab.Contains("guid: $guid")) { throw "Unbound prefab script: $script" }
}
Write-Output 'PASS: R/Q, container death/transfer, scene unload/return, frame guards and prefab references.'

# Preview native code-generated icon at large and HUD sizes on the game's dark background.
Add-Type -AssemblyName System.Drawing
$bmp=[Drawing.Bitmap]::new(560,340);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(20,25,45))
$rows=[ClockworkRemoteTests]::Rows()
for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){
    $c=[ClockworkRemoteTests]::Pixel($rows[$y][$x]);if($c.a -le 0){continue}
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int]($c.a*255),[int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
    $g.FillRectangle($brush,24+$x*16,16+$y*16,16,16)
    $g.FillRectangle($brush,348+$x*5,105+$y*5,5,5);$brush.Dispose()
}}
$font=[Drawing.Font]::new('Segoe UI',12);$g.DrawString('Magic link / translucent red energy', $font,[Drawing.Brushes]::White,24,300)
$bmp.Save("$root/Docs/ClockworkPuppetRemote-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
