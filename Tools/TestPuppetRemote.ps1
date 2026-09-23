$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$item = Get-Content -Raw "$root/Assets/Scripts/Items/PuppetRemotePickupItem.cs"
$visual = Get-Content -Raw "$root/Assets/Scripts/Items/PuppetRemotePickupItemVisual.cs"
$code = @'
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
namespace UnityEngine {
 public struct Scene {
  public int id;public bool isLoaded;public bool IsValid()=>id>0;
  public static bool operator ==(Scene a,Scene b)=>a.id==b.id;
  public static bool operator !=(Scene a,Scene b)=>a.id!=b.id;
  public override bool Equals(object o)=>o is Scene && ((Scene)o).id==id;public override int GetHashCode()=>id;
 }
 public class GameObject{public int scene=1;public bool loaded=true,activeInHierarchy=true;public ZeldaCharacterData data;}
 public class Color {public float r,g,b,a;public Color(float r,float g,float b,float a){this.r=r;this.g=g;this.b=b;this.a=a;}public static Color clear=>new Color(0,0,0,0);}
}
public class ZeldaCharacterData {public GameObject gameObject=new GameObject();public bool IsDead,IsGhostLike;}
public class AutomatonCharacterAi {
 public static List<AutomatonCharacterAi> Instances=new List<AutomatonCharacterAi>();
 public GameObject gameObject=new GameObject();public bool enabled=true;public bool HostilityActivated;public int calls;
 public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
 public T GetComponent<T>() where T:class=>gameObject.data as T;
 public void SetHostilityActive(bool b){HostilityActivated=b;calls++;}
}
public static class ZeldaRuntimeRegistry {
 public static int activeScene=1;
 public static Scene GetGameplayScene(GameObject g)=>new Scene{id=g.scene==-1?activeScene:g.scene,isLoaded=g.loaded};
}
public class ZeldaHealthHeartsUI {
 public static ZeldaHealthHeartsUI Instance=new ZeldaHealthHeartsUI();public string last;
 public void ShowNotificationPopup(string s){last=s;}
}
public class PickupItemBase {
 public virtual bool CanBeStoredInCardboardBox(ZeldaCharacterData d)=>true;
 protected virtual bool ApplyUseEffect(ZeldaCharacterData d)=>true;
 public bool ExecuteUse(ZeldaCharacterData d)=>ApplyUseEffect(d);
}
public class PickupItemVisualBase {
 protected virtual string RuntimeSpriteName=>"";protected virtual bool UsesEmbeddedColors=>false;
 protected virtual Color GetPixelColor(char c)=>Color.clear;protected virtual string[] GetPixelRows()=>null;
}
'@
$code += $item.Replace('using UnityEngine;', '')
$code += $visual.Replace('using UnityEngine;', '')
$code += @'
public static class PuppetRemoteTests {
 static int checks;static void Check(bool v,string msg){if(!v)throw new Exception(msg);checks++;}
 static AutomatonCharacterAi Make(bool hostile=false,int scene=1){var a=new AutomatonCharacterAi{HostilityActivated=hostile};a.gameObject.scene=scene;a.gameObject.data=new ZeldaCharacterData{gameObject=a.gameObject};AutomatonCharacterAi.Instances.Add(a);return a;}
 public static string[] Rows()=> (string[])typeof(PuppetRemotePickupItemVisual).GetMethod("GetPixelRows",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new PuppetRemotePickupItemVisual(),null);
 public static Color Pixel(char c)=> (Color)typeof(PuppetRemotePickupItemVisual).GetMethod("GetPixelColor",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(new PuppetRemotePickupItemVisual(),new object[]{c});
 public static int Run(){
  AutomatonCharacterAi.Instances.Clear();var remote=new PuppetRemotePickupItem();var user=new ZeldaCharacterData();
  var idle=Make();var hostile=Make(true);var otherScene=Make(false,2);var disabled=Make();disabled.enabled=false;
  var inactive=Make();inactive.gameObject.activeInHierarchy=false;var dead=Make();dead.gameObject.data.IsDead=true;
  var ghost=Make();ghost.gameObject.data.IsGhostLike=true;var noData=Make();noData.gameObject.data=null;AutomatonCharacterAi.Instances.Add(null);
  Check(remote.ToggleSceneAutomatons(user)==2,"Only enabled living same-scene targets");
  Check(idle.HostilityActivated&&!hostile.HostilityActivated,"Invert mixed states independently");
  foreach(var a in new[]{otherScene,disabled,inactive,dead,ghost,noData})Check(a.calls==0,"Excluded target untouched");
  Check(remote.ToggleSceneAutomatons(user)==2,"Repeat-use target count");
  Check(!idle.HostilityActivated&&hostile.HostilityActivated,"Second use restores state");
  Check(!remote.CanBeStoredInCardboardBox(user),"R not intercepted by nearby box");
  Check(!remote.ExecuteUse(user),"Use retains inventory quantity");
  Check(idle.HostilityActivated&&!hostile.HostilityActivated,"Nonconsuming use still operates remote");
  Check(ZeldaHealthHeartsUI.Instance.last.Contains("2"),"Use feedback count");
  Check(remote.ToggleSceneAutomatons(null)==0,"No user");user.IsDead=true;
  Check(remote.ToggleSceneAutomatons(user)==0,"Dead user");user.IsDead=false;user.IsGhostLike=true;
  Check(remote.ToggleSceneAutomatons(user)==0,"Ghost user");user.IsGhostLike=false;
  user.gameObject.scene=0;Check(remote.ToggleSceneAutomatons(user)==0,"Invalid gameplay scene");
  user.gameObject.scene=1;user.gameObject.loaded=false;Check(remote.ToggleSceneAutomatons(user)==0,"Unloaded gameplay scene");user.gameObject.loaded=true;
  user.gameObject.scene=-1;Check(remote.ToggleSceneAutomatons(user)==2,"Traveling player uses current gameplay scene");
  user.gameObject.scene=2;Check(remote.ToggleSceneAutomatons(user)==1&&otherScene.HostilityActivated,"Scene scope follows user, not temporary item instance");
  user.gameObject.scene=3;Check(!remote.ExecuteUse(user),"No targets does not consume item");
  Check(ZeldaHealthHeartsUI.Instance.last.Contains("没有"),"No targets notification");
  ZeldaHealthHeartsUI.Instance=null;Check(!remote.ExecuteUse(user),"No HUD is safe");
  var rows=Rows();Check(rows.Length==16,"16 pixel height");
  foreach(var row in rows){Check(row.Length==16,"16 pixel width");foreach(char c in row)Check(".DSHR".Contains(c.ToString()),"Only red/steel palette; no cyan window");}
  Check(Pixel('C').a==0,"Cyan color removed rather than recoloring the old display");
  Check(rows[6].Substring(6,4)=="SSSS"&&rows[7].Substring(6,4)=="SSSS","Old display and its dark frame replaced with steel casing");
  Check(Pixel('.').a==0&&Pixel('R').r>Pixel('R').g,"Transparent background and red switch");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([PuppetRemoteTests]::Run()) remote use/scope/reuse/pixel checks (Unity services stubbed)."

$prefab = Get-Content -Raw "$root/Assets/Resources/PickupItems/PuppetRemotePickupItem.prefab"
if (!$prefab.Contains('itemId: puppet_remote') -or !$prefab.Contains([PuppetRemotePickupItem]::DefaultDescription) -or !$prefab.Contains([PuppetRemotePickupItem]::DefaultItemName)) { throw 'Prefab identity or description mismatch' }
if ($prefab -notmatch 'uniqueInstanceId: ""' -or $prefab -notmatch 'persistentIdentity: \{fileID: 0\}' -or $prefab -notmatch 'maxStackSize: 1') { throw 'Unique item setup' }
foreach ($script in @('PuppetRemotePickupItem','PuppetRemotePickupItemVisual')) {
    $meta = Get-Content -Raw "$root/Assets/Scripts/Items/$script.cs.meta"
    $guid = [regex]::Match($meta, 'guid: (\w+)').Groups[1].Value
    if (!$prefab.Contains("guid: $guid")) { throw 'Script reference missing' }
}
$inventory = Get-Content -Raw "$root/Assets/Scripts/Items/PersistentInventory.cs"
if ($inventory -notmatch '(?s)bool consumed = runtimeItem.ExecuteUse\(user\);\s*if \(!consumed\)\s*\{\s*Destroy\(runtimeItem.gameObject\);\s*return false;') { throw 'Inventory nonconsuming item contract changed' }
Write-Output 'PASS: Resources discovery, prefab references, exact description, unique identity and nonconsuming inventory contract.'

Add-Type -AssemblyName System.Drawing
$bmp = [Drawing.Bitmap]::new(480,360);$g=[Drawing.Graphics]::FromImage($bmp)
$g.Clear([Drawing.Color]::FromArgb(25,32,43));$rows=[PuppetRemoteTests]::Rows()
for($y=0;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){
    $c=[PuppetRemoteTests]::Pixel($rows[$y][$x]);if($c.a -le 0){continue}
    $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb([int]($c.r*255),[int]($c.g*255),[int]($c.b*255)))
    $g.FillRectangle($brush,35+$x*16,35+$y*16,16,16)
    $g.FillRectangle($brush,345+$x*4,132+$y*4,4,4);$brush.Dispose()
}}
$font=[Drawing.Font]::new('Segoe UI',12)
$g.DrawString('Puppet remote / 16 x 16', $font,[Drawing.Brushes]::White,35,310)
$bmp.Save("$root/Docs/PuppetRemote-preview.png",[Drawing.Imaging.ImageFormat]::Png)
$font.Dispose();$g.Dispose();$bmp.Dispose()
