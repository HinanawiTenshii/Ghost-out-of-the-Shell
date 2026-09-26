$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$menu=Get-Content -Raw "$taskRoot/Assets/Scripts/UI/TabJournalMenuController.cs"
$map=Get-Content -Raw "$taskRoot/Assets/Scripts/Map/RuntimeMiniMapGraphic.cs"
function Extract($source,$signature) {
    $start=$source.IndexOf($signature); if($start -lt 0){throw "Missing $signature"}
    $begin=$source.IndexOf('{',$start); $end=$begin+1; $depth=1
    while($depth){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    return $source.Substring($start,$end-$start)
}
$code=@'
using System;
using System.Collections.Generic;
public struct Vector2 {
 public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}
 public static Vector2 zero=>new Vector2();
 public static Vector2 operator +(Vector2 a,Vector2 b)=>new Vector2(a.x+b.x,a.y+b.y);
}
public struct Scene {
 public string name;public bool isLoaded;
 public bool IsValid()=>name!=null;
 public static bool operator ==(Scene a,Scene b)=>a.name==b.name;
 public static bool operator !=(Scene a,Scene b)=>a.name!=b.name;
 public override bool Equals(object o)=>o is Scene && this==(Scene)o;
 public override int GetHashCode()=>name==null?0:name.GetHashCode();
}
public static class SceneManager {public static Scene active;public static Scene GetActiveScene()=>active;}
public class GameObject {public Scene scene;public bool activeSelf;public int changes;public void SetActive(bool value){activeSelf=value;changes++;}}
public class Transform {public string name;public GameObject gameObject=new GameObject();public Vector2 position;public Transform child;public Transform Find(string n)=>child!=null&&child.name==n?child:null;}
public struct RuntimeMiniMapQuestTargetData {public string questId;public Vector2 position;}
public class RuntimeMiniMapSceneData {public RuntimeMiniMapQuestTargetData[] QuestTargets;}
public class QuestJournalManager {public string TrackedEntryId;public HashSet<string> completed=new HashSet<string>();public bool IsEntryCompleted(string id)=>completed.Contains(id);}
public class RuntimeMiniMapGraphic {
 public static Transform[] objects=new Transform[0];
 public static T[] FindObjectsOfType<T>(bool inactive)=>objects as T[];
'@
$code += Extract $map 'public static bool TryGetSceneQuestTarget('
$code += @'
}
public class MenuHarness {
 public QuestJournalManager questJournal;
 public Dictionary<string,GameObject> mapSceneQuestMarkers=new Dictionary<string,GameObject>(StringComparer.OrdinalIgnoreCase);
 public static Dictionary<string,RuntimeMiniMapSceneData> snapshots=new Dictionary<string,RuntimeMiniMapSceneData>(StringComparer.OrdinalIgnoreCase);
 private static RuntimeMiniMapSceneData GetMiniMapSnapshot(string n){RuntimeMiniMapSceneData s;return snapshots.TryGetValue(n,out s)?s:null;}
 public void Refresh()=>RefreshMapSceneQuestMarkers();
'@
$code += Extract $menu 'private void RefreshMapSceneQuestMarkers()'
$code += Extract $menu 'private static bool SceneHasQuestTarget('
$code += @'
}
public static class Checks {
 static int count;static void Check(bool ok,string message){count++;if(!ok)throw new Exception(message);}
 public static int Run(){
  Scene a=new Scene{name="Current",isLoaded=true},b=new Scene{name="Other",isLoaded=true};SceneManager.active=a;
  var work=new Transform{name="WorkTable",position=new Vector2(3,4)};work.gameObject.scene=a;
  var wrong=new Transform{name="FinalTarget"};wrong.gameObject.scene=b;
  RuntimeMiniMapGraphic.objects=new[]{work,wrong};Vector2 p;
  Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(a,"level1.craft_super_bomb",out p)&&p.x==3,"Live target including inactive objects");
  Check(!RuntimeMiniMapGraphic.TryGetSceneQuestTarget(a,"level1.escape_castle",out p),"Ignore other scene objects");
  Check(!RuntimeMiniMapGraphic.TryGetSceneQuestTarget(a,"unknown",out p),"Unknown target");
  Check(!RuntimeMiniMapGraphic.TryGetSceneQuestTarget(new Scene(),"level1.craft_super_bomb",out p),"Invalid scene");
  var gate=new Transform{name="MainGate",child=new Transform{name="Door",position=new Vector2(7,8)}};gate.gameObject.scene=a;
  var key=new Transform{name="GoldenKey",position=new Vector2(1,2)};key.gameObject.scene=a;
  RuntimeMiniMapGraphic.objects=new[]{work,gate,key};
  Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(a,"level1.find_castle_gate_key",out p)&&p.x==7&&p.y==8,"Gate child position preserved");
  Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(a,"level0.obtain_gate_key",out p)&&Math.Abs(p.y-2.72f)<.001,"Key offset preserved");
  var menu=new MenuHarness();foreach(string name in new[]{"current","Remote","Empty","Missing"})menu.mapSceneQuestMarkers.Add(name,new GameObject());
  MenuHarness.snapshots["Remote"]=new RuntimeMiniMapSceneData{QuestTargets=new[]{new RuntimeMiniMapQuestTargetData{questId="level1.escape_castle"}}};
  MenuHarness.snapshots["Empty"]=new RuntimeMiniMapSceneData();
  // A stale current-scene snapshot must never override the live scene.
  MenuHarness.snapshots["current"]=MenuHarness.snapshots["Remote"];
  menu.Refresh();Check(!menu.mapSceneQuestMarkers["current"].activeSelf,"Null journal safely hidden");
  menu.questJournal=new QuestJournalManager{TrackedEntryId="level1.craft_super_bomb"};menu.Refresh();
  Check(menu.mapSceneQuestMarkers["current"].activeSelf,"Current region badge");
  Check(!menu.mapSceneQuestMarkers["Remote"].activeSelf,"No unrelated region badge");
  int changes=menu.mapSceneQuestMarkers["current"].changes;menu.Refresh();Check(changes==menu.mapSceneQuestMarkers["current"].changes,"No redundant activation");
  menu.questJournal.TrackedEntryId="level1.escape_castle";menu.Refresh();
  Check(!menu.mapSceneQuestMarkers["current"].activeSelf,"Tracking change removes previous marker and ignores stale snapshot");
  Check(menu.mapSceneQuestMarkers["Remote"].activeSelf,"Unloaded region snapshot badge");
  Check(!menu.mapSceneQuestMarkers["Empty"].activeSelf&&!menu.mapSceneQuestMarkers["Missing"].activeSelf,"Missing or empty snapshot safe");
  menu.questJournal.completed.Add("level1.escape_castle");menu.Refresh();Check(!menu.mapSceneQuestMarkers["Remote"].activeSelf,"Completion hides marker");
  menu.questJournal.completed.Clear();menu.Refresh();Check(menu.mapSceneQuestMarkers["Remote"].activeSelf,"Restored task state updates");
  menu.questJournal.TrackedEntryId="";menu.Refresh();Check(!menu.mapSceneQuestMarkers["Remote"].activeSelf,"Untracking clears marker");
  menu.questJournal.TrackedEntryId="no_marker_task";menu.Refresh();Check(!menu.mapSceneQuestMarkers["current"].activeSelf&&!menu.mapSceneQuestMarkers["Remote"].activeSelf,"Tasks without map targets stay unmarked");
  menu.mapSceneQuestMarkers.Clear();menu.Refresh();Check(true,"Empty menu safe");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "Passed $([Checks]::Run()) production-method behavior checks."
foreach($method in @('private void RefreshMiniMap()','private void HandleTrackedQuestChanged()')) {
 if(!(Extract $menu $method).Contains('RefreshMapSceneQuestMarkers();')){throw "Missing badge update: $method"}
}
if(!(Extract $menu 'private void RefreshQuestJournal()').Contains('HandleTrackedQuestChanged();')){throw 'Journal completion update missing'}
if(!$menu.Contains('markerObject.GetComponent<MapSceneQuestMarkerGraphic>().raycastTarget = false;')){throw 'Badge must not consume button clicks'}
$graphic=Get-Content -Raw "$taskRoot/Assets/Scripts/UI/MapSceneQuestMarkerGraphic.cs"
if(!$graphic.Contains('RuntimeMiniMapGraphic.AddQuestTargetMarker(')){throw 'Badge must share map marker rendering'}
Write-Output 'Passed event wiring, click-through and shared visual checks.'
