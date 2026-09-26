$ErrorActionPreference='Stop'
$taskRoot=Split-Path $PSScriptRoot -Parent
$manager=Get-Content -Raw "$taskRoot/Assets/Scripts/Quests/QuestJournalManager.cs"
$sceneData=Get-Content -Raw "$taskRoot/Assets/Scripts/Quests/QuestJournalSceneData.cs"
$code=@'
#pragma warning disable 0649
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace UnityEngine {
 public class Tooltip:Attribute {public Tooltip(string s){}}
 public class TextArea:Attribute {public TextArea(int a,int b){}}
 public class SerializeField:Attribute {}
 public class DefaultExecutionOrder:Attribute {public DefaultExecutionOrder(int a){}}
 public enum RuntimeInitializeLoadType{BeforeSceneLoad}
 public class RuntimeInitializeOnLoadMethod:Attribute {public RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType a){}}
 public class GameObject {public string name;public Scene scene;public GameObject(string name=""){this.name=name;}public T AddComponent<T>() where T:new()=>new T();}
 public class MonoBehaviour {
  public GameObject gameObject=new GameObject();
  protected static void Destroy(object o){}protected static void DontDestroyOnLoad(object o){}
  protected static T[] FindObjectsOfType<T>(bool inactive)=>new T[0];
 }
}
namespace UnityEngine.SceneManagement {
 public struct Scene {
  public string name;public bool isLoaded;public bool IsValid()=>name!=null;
  public static bool operator ==(Scene a,Scene b)=>a.name==b.name;
  public static bool operator !=(Scene a,Scene b)=>a.name!=b.name;
  public override bool Equals(object o)=>o is Scene&&this==(Scene)o;
  public override int GetHashCode()=>name==null?0:name.GetHashCode();
 }
 public enum LoadSceneMode{Single}
 public static class SceneManager {public static Scene GetActiveScene()=>new Scene{name="Test",isLoaded=true};public static event Action<Scene,LoadSceneMode> sceneLoaded {add{} remove{}}}
}
public class ZeldaFourWayMover {public bool isActiveAndEnabled;public T GetComponent<T>() where T:class=>null;}
public class BehemothZeldaCharacterData {}
public static class ZeldaRuntimeRegistry {public static ZeldaFourWayMover GetControlledMover()=>null;}
public class SpecificItemSubmissionStation {public GameObject gameObject=new GameObject();public string name;public bool IsComplete;}
'@
$code += [regex]::Replace($manager,'(?m)^using .*;\r?\n','')
$code += [regex]::Replace($sceneData,'(?m)^using .*;\r?\n','')
$code += @'
public static class LevelObjectiveChecks {
 static int count;
 static void Check(bool value,string message){count++;if(!value)throw new Exception(message);}
 static QuestJournalManager.Entry Entry(QuestJournalManager m,string id){QuestJournalManager.Entry e;if(!m.TryGetEntry(id,out e))throw new Exception("Missing "+id);return e;}
 public static int Run(){
  var m=new QuestJournalManager();var data=new QuestJournalSceneData();
  data.gameObject.scene=new Scene{name="TestLevel",isLoaded=true};
  var definitions=new[]{
   new QuestJournalEntryDefinition{entryId="main.one",title="One"},
   new QuestJournalEntryDefinition{entryId="main.two",title="Two"},
   new QuestJournalEntryDefinition{entryId="side.triggered",title="Side",addOnlyWhenTriggered=true}
  };
  typeof(QuestJournalSceneData).GetField("initialEntries",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(data,definitions);
  Check(data.LoadInitialEntriesWithoutNotification(m,"TestLevel"),"Initial entries loaded");
  Check(Entry(m,"main.one").IsLevelObjective&&Entry(m,"main.two").IsLevelObjective,"All automatic initial tasks tagged, not just last tracked");
  Check(!m.ContainsEntry("side.triggered"),"Triggered tasks remain hidden initially");
  m.AddOrUpdateEntry("side.triggered","Side","details","TestLevel");
  Check(!Entry(m,"side.triggered").IsLevelObjective,"Triggered task remains ordinary");
  Check(m.TrackedEntryId=="side.triggered"&&Entry(m,"main.one").IsLevelObjective,"Tracking a side task preserves objective identity");
  m.AddOrUpdateEntry("main.one","Renamed","Updated","Elsewhere");
  Check(Entry(m,"main.one").IsLevelObjective,"Updates preserve objective flag");
  m.CompleteEntry("main.one");
  Check(m.IsEntryCompleted("main.one")&&Entry(m,"main.one").IsLevelObjective,"Completion preserves objective identity");
  var state=m.CaptureSaveState();var restored=new QuestJournalManager();restored.ApplySaveState(state);
  Check(Entry(restored,"main.one").IsLevelObjective&&Entry(restored,"main.two").IsLevelObjective,"Multiple objective flags survive save roundtrip");
  Check(!Entry(restored,"side.triggered").IsLevelObjective,"Ordinary task survives save roundtrip");
  Check(restored.IsEntryCompleted("main.one")&&restored.TrackedEntryId==m.TrackedEntryId,"Completion and tracking unchanged");
  var legacy=new QuestJournalManager.SaveState();
  legacy.entries.Add(new QuestJournalManager.SavedEntry{id="old.main",title="Old main"});
  legacy.entries.Add(new QuestJournalManager.SavedEntry{id="old.side",title="Old side"});
  legacy.initialTracked="old.main";legacy.tracked="old.side";
  restored.ApplySaveState(legacy);
  Check(Entry(restored,"old.main").IsLevelObjective,"Old save initialTracked fallback");
  Check(!Entry(restored,"old.side").IsLevelObjective,"Old save active side task not mistaken for objective");
  restored.ApplySaveState(new QuestJournalManager.SaveState{entries=new List<QuestJournalManager.SavedEntry>{new QuestJournalManager.SavedEntry{id="ordinary"}}});
  Check(!Entry(restored,"ordinary").IsLevelObjective,"Missing old initial ID is safe");
  restored.RemoveEntry("ordinary");Check(restored.Entries.Count==0,"Removal still works");
  restored.ApplySaveState(state);restored.ResetAllEntries();Check(restored.Entries.Count==0,"New session clears objective records");
  var existing=new QuestJournalManager();existing.AddOrUpdateEntry("main.one","Found first","","TestLevel");
  data.LoadInitialEntriesWithoutNotification(existing,"TestLevel");Check(Entry(existing,"main.one").IsLevelObjective,"Initial declaration upgrades an existing entry");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code
Write-Output "PASS: $([LevelObjectiveChecks]::Run()) checks using production manager and scene loader with Unity stubs."
$menu=Get-Content -Raw "$taskRoot/Assets/Scripts/UI/TabJournalMenuController.cs"
foreach($snippet in @('bool levelObjective = entry.IsLevelObjective;', 'CreateLevelObjectiveCorners(rowRect);', 'corner + " Horizontal", ZeldaUiPalette.Primary', 'corner + " Vertical", ZeldaUiPalette.Primary', 'image.raycastTarget = false;')) {
 if(!$menu.Contains($snippet)){throw "Missing UI integration: $snippet"}
}
foreach($removed in @('LevelObjectiveAccent', 'LevelObjectiveText', 'CreateLevelObjectiveBadge', 'text.text = "关卡任务";')) {
 if($menu.Contains($removed)){throw "Obsolete gold/text badge remains: $removed"}
}
Write-Output 'PASS: shared-palette noninteractive corners; gold and extra text removed.'
