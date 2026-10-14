$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$alarm=Get-Content -Raw "$root/Assets/Scripts/Interaction/AlarmDevice.cs"
$stream=Get-Content -Raw "$root/Assets/Scripts/Camera/CameraVisionObjectStreaming.cs"
$ai=Get-Content -Raw "$root/Assets/Scripts/AI/ZeldaCharacterAiBase.cs"
function Extract($source,$signature){
 $start=$source.IndexOf($signature);if($start -lt 0){throw "Missing $signature"}
 $brace=$source.IndexOf('{',$start);$end=$brace+1;$depth=1
 while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
 $source.Substring($start,$end-$start)
}
$code=@'
using System;using System.Collections.Generic;
public struct Vector2 {public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}}
public static class Time {public static float time;}
public static class Mathf {public static int Min(int a,int b)=>Math.Min(a,b);public static float Max(float a,float b)=>Math.Max(a,b);}
public class Transform {public GameObject gameObject;public Transform parent;public Vector2 position;}
public class GameObject {
 public int scene=1;public bool activeSelf=true;public int activations;public Transform transform;
 public Dictionary<Type,Component> components=new Dictionary<Type,Component>();
 public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
 public GameObject(){transform=new Transform{gameObject=this};}
 public void SetActive(bool v){activeSelf=v;if(v)activations++;}
 public T Add<T>() where T:Component,new(){var c=new T{gameObject=this};components[typeof(T)]=c;return c;}
}
public class Component {
 public GameObject gameObject;public Transform transform=>gameObject.transform;public bool enabled=true;
 public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
 public T GetComponent<T>() where T:Component=>gameObject.components.TryGetValue(typeof(T),out var c)?(T)c:null;
}
public class ZeldaCharacterData:Component {public bool IsDead,IsGhostForm;}
public class ZeldaFourWayMover:Component {}
public class ZeldaCharacterAiBase:Component {
 public bool IsPossessionLocked;public int calls;public Vector2 destination;
 public void RespondToAlarm(Vector2 p){calls++;destination=p;}
}
public static class BatterySocket {
 public static readonly Dictionary<Component,bool> supplies=new Dictionary<Component,bool>();
 public static bool HasConnectedSocket(Component c)=>supplies.ContainsKey(c);
 public static bool IsPowered(Component c)=>!supplies.ContainsKey(c)||supplies[c];
}
public class CameraVisionObjectStreaming {
 public sealed class ManagedObject {public bool suspendedByManager,queuedForActivation;public float outsideTimer=10;}
 public Dictionary<GameObject,ManagedObject> managedLookup=new Dictionary<GameObject,ManagedObject>();
 public static List<CameraVisionObjectStreaming> managers=new List<CameraVisionObjectStreaming>();
 public static T[] FindObjectsOfType<T>(){var values=new List<T>();foreach(var m in managers)values.Add((T)(object)m);return values.ToArray();}
 public void Suspend(GameObject go){managedLookup[go]=new ManagedObject{suspendedByManager=true,queuedForActivation=true};go.SetActive(false);}
'@
$code+=Extract $stream '    public static void RestoreForGameplay('
$code+='} public class AlarmDevice:Component {'
$code+=[regex]::Match($alarm,'public const int MaximumResponders = \d+;').Value
$code+='public ZeldaCharacterAiBase[] responders=new ZeldaCharacterAiBase[10];public float interactionDistance=1.4f,activationCooldown=1.2f;private float nextActivationTime,flashUntil;private readonly HashSet<ZeldaCharacterAiBase> dispatched=new HashSet<ZeldaCharacterAiBase>();'
$code+=[regex]::Match($alarm,'public bool HasPower =>[^;]+;').Value
$code+=Extract $alarm '    public bool TryActivate('
$code+=(Extract $alarm '    private void OnValidate()').Replace('private void','public void')
$code+=@'
}
public static class AlarmTests {
 static int n;static void Check(bool b,string s){n++;if(!b)throw new Exception(s);}
 static ZeldaCharacterAiBase Npc(){var go=new GameObject();go.Add<ZeldaCharacterData>();go.Add<ZeldaFourWayMover>().enabled=false;return go.Add<ZeldaCharacterAiBase>();}
 static void Next(){Time.time+=2;}
 public static int Run(){
  var alarm=new GameObject().Add<AlarmDevice>();alarm.transform.position=new Vector2(4,6);
  var npc=Npc();alarm.responders=new[]{npc};
  Check(!alarm.TryActivate()&&npc.calls==0,"Unbound alarm cannot use legacy no-socket power default");
  BatterySocket.supplies[alarm]=false;Check(!alarm.TryActivate(),"Empty/depleted socket cannot trigger");
  BatterySocket.supplies[alarm]=true;Check(alarm.TryActivate()&&npc.calls==1,"Powered alarm dispatches");
  Check(npc.destination.x==4&&npc.destination.y==6,"Dispatch uses alarm world position");
  Check(!alarm.TryActivate()&&npc.calls==1,"Cooldown prevents repeated dispatch in one interaction");
  Next();alarm.responders=new[]{npc,npc,null};alarm.TryActivate();Check(npc.calls==2,"Duplicate/null bindings safely filtered");
  Next();alarm.responders=new ZeldaCharacterAiBase[15];for(int i=0;i<15;i++)alarm.responders[i]=Npc();
  alarm.TryActivate();for(int i=0;i<15;i++)Check(alarm.responders[i].calls==(i<10?1:0),"Runtime caps list to ten even before validation");
  alarm.OnValidate();Check(alarm.responders.Length==10,"Inspector validation caps ten");
  var dead=Npc();dead.GetComponent<ZeldaCharacterData>().IsDead=true;
  var ghost=Npc();ghost.GetComponent<ZeldaCharacterData>().IsGhostForm=true;
  var player=Npc();player.GetComponent<ZeldaFourWayMover>().enabled=true;
  var otherScene=Npc();otherScene.gameObject.scene=2;
  var locked=Npc();locked.IsPossessionLocked=true;
  var noAi=Npc();noAi.enabled=false;
  var hidden=Npc();hidden.gameObject.SetActive(false);
  alarm.responders=new[]{dead,ghost,player,otherScene,locked,noAi,hidden};Next();alarm.TryActivate();
  foreach(var a in alarm.responders)Check(a.calls==0,"Invalid/protected binding ignored");
  Check(!hidden.gameObject.activeSelf,"Explicitly hidden objects not force-enabled");
  var camera=new CameraVisionObjectStreaming();CameraVisionObjectStreaming.managers.Add(camera);
  var asleep=Npc();camera.Suspend(asleep.gameObject);alarm.responders=new[]{asleep};Next();alarm.TryActivate();
  Check(asleep.isActiveAndEnabled&&asleep.calls==1,"Streaming-disabled NPC wakes and receives dispatch in same call");
  var entry=camera.managedLookup[asleep.gameObject];
  Check(!entry.suspendedByManager&&!entry.queuedForActivation&&entry.outsideTimer==0,"Wake clears stale suspension and activation queue state");
  var parent=new GameObject();asleep.transform.parent=parent.transform;camera.Suspend(parent);camera.Suspend(asleep.gameObject);
  Next();alarm.TryActivate();Check(parent.activeInHierarchy&&asleep.calls==2,"Streaming-owned ancestors restored too");
  int wakeCount=asleep.gameObject.activations;Next();alarm.TryActivate();
  Check(asleep.calls==3&&asleep.gameObject.activations==wakeCount,"Already active actors are not re-enabled/reset");
  parent.SetActive(false);Next();alarm.TryActivate();Check(!parent.activeSelf&&asleep.calls==3,"Non-streaming hidden parent never force-activated");
  parent.SetActive(true);BatterySocket.supplies[alarm]=false;Next();Check(!alarm.TryActivate()&&asleep.calls==3,"Removing battery immediately blocks trigger");
  alarm.responders=null;alarm.OnValidate();Check(alarm.responders.Length==0,"Null serialized list is safe");
  return n;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output ("PASS: "+[AlarmTests]::Run()+" production alarm and streaming wake checks (Unity lifecycle/navigation stubbed).")
if(!(Extract $ai '    private void FinishStun()').Contains('RespondToAlarm(alarmPosition);')){throw 'Queued alarm must resume after stun'}
if(!(Extract $ai '    public virtual SaveState CaptureSaveState()').Contains('alarmResponse = alarmResponseActive')){throw 'Alarm state missing from saves'}
if(!(Extract $ai '    public virtual void ApplySaveState(').Contains('state.incidentInvestigation')){throw 'Alarm travel/search timing must survive load'}
$restore=Get-Content -Raw "$root/Assets/Scripts/SceneManagement/SceneTravelStateManager.cs"
if(!$restore.Contains('ai.RestoreAlarmResponseState(state.savedAi);')){throw 'Scene-return dispatch restore missing'}
$recovery=Extract $ai '    private void CompleteRecovery()'
if($recovery.Contains('SetActive(false)') -or !$recovery.Contains('alarmResponseActive = pendingAlarmResponse = false;')){throw 'Home completion must release streaming without forcing disable'}
$prefab=Get-Content -Raw "$root/Assets/Prefabs/Decorations/AlarmDevice.prefab"
foreach($name in @('Interaction/AlarmDevice','Interaction/AlarmDeviceVisual','Camera/CameraVisionStreamingExempt')){
 $meta=Get-Content -Raw "$root/Assets/Scripts/$name.cs.meta"
 $guid=[regex]::Match($meta,'guid: ([a-f0-9]{32})').Groups[1].Value
 if(!$prefab.Contains($guid)){throw "Missing prefab script $name"}
}
Write-Output 'PASS: save/scene-return/stun hooks and prefab script references.'
