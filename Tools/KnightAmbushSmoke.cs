using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class KnightAmbushSmoke
{
    const string Key = nameof(KnightAmbushSmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static KnightAmbushSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("KNIGHT_CHECK " + label); }
    static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F|BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object obj,string name,object value) => Field(obj,name).SetValue(obj,value);
    static T Get<T>(object obj,string name) => (T)Field(obj,name).GetValue(obj);
    static object Call(object obj,string name,params object[] args)
    {
        for(Type t=obj.GetType();t!=null;t=t.BaseType)
        { var m=t.GetMethod(name,F|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(obj,args); }
        throw new Exception(name);
    }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated only");
        PlayerSettings.productName="knight-ambush-smoke";
        var original=EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var authored=Object.FindObjectOfType<KnightCaptainAmbush>();
        Check(authored!=null&&authored.captain!=null&&authored.guards.Length==3&&authored.guards.All(g=>g!=null),"authored captain + three escorts");
        Check(authored.captain.GetComponent<SquadRegenerationAura>()!=null,"advanced squad paladin has aura");
        Check(authored.safeHouses.Length==2&&authored.safeHouses.All(h=>h.guards.Length==3&&h.entrance!=null&&h.permission!=null),"two safehouses, actual doors and three posts each");
        Check(authored.safeHouses[0].guards.Select(g=>g.facing).Distinct().Count()==3,"south house covers west/east/south entrances");
        Check(authored.safeHouses[1].guards.All(g=>g.facing==Vector2.down),"north house three guards face south");
        Check(authored.safeHouses.All(h=>h.permission.PermissionLevel==1),"normal permission unchanged before incident");
        var notice=Object.FindObjectsOfType<DocumentReader>(true).Single(d=>d.name=="巡逻队信息");
        Check(Get<string>(notice,"journalEntryId")==QuestJournalManager.KnightAmbushQuestId&&
            Get<string>(notice,"journalParentEntryId")==QuestJournalManager.FortressProofQuestId,"existing readable triggers gated child");
        var docFields=SavedScalarFields.Capture(notice,true);
        foreach(var field in docFields)
        {
            if(field.name=="journalEntryId")field.text="";
            if(field.name=="documentText")field.text="old patrol information";
            if(field.name=="addJournalEntryOnOpen")field.boolean=false;
            if(field.name=="journalEntryWasAdded")field.boolean=true;
        }
        Call(notice,"RestoreSavedFields",docFields);
        Check(Get<string>(notice,"journalEntryId")==QuestJournalManager.KnightAmbushQuestId&&Get<bool>(notice,"addJournalEntryOnOpen")&&
            !Get<bool>(notice,"journalEntryWasAdded")&&notice.DocumentText.Contains("应急预案"),"older read-document save cannot erase newly authored child quest");
        var snapshot=Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor2");
        Check(snapshot.QuestTargets.Count(t=>t.questId==QuestJournalManager.KnightAmbushQuestId)==1,"baked menu map has captain target");
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(original,QuestJournalManager.KnightAmbushQuestId,out var target)&&
            Vector2.Distance(target,authored.captain.transform.position)<.01f,"editor rebake resolves captain");

        // Preserve actual city collision geometry and door positions, but omit other
        // NPCs/UI/save startup. This is not a modified or saved gameplay scene.
        var colliders=Object.FindObjectsOfType<Collider2D>(true);
        var doors=Object.FindObjectsOfType<DoorHingeInteraction>(true);
        var fixture=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        fixture.name="KnightAmbushFixture";
        SceneManager.SetActiveScene(fixture);
        foreach(var source in colliders)
        {
            if(!source.enabled||source.isTrigger||!source.gameObject.activeInHierarchy||
                source.GetComponentInParent<ZeldaCharacterData>()!=null||source.GetComponentInParent<DoorHingeInteraction>()!=null)continue;
            if(!(source is BoxCollider2D)&&!(source is PolygonCollider2D))continue;
            var go=new GameObject("Obstacle "+source.name);go.layer=source.gameObject.layer;
            go.transform.position=source.transform.position;go.transform.rotation=source.transform.rotation;go.transform.localScale=source.transform.lossyScale;
            if(source is BoxCollider2D box){var c=go.AddComponent<BoxCollider2D>();c.size=box.size;c.offset=box.offset;}
            else if(source is PolygonCollider2D poly){var c=go.AddComponent<PolygonCollider2D>();c.offset=poly.offset;c.pathCount=poly.pathCount;for(int p=0;p<poly.pathCount;p++)c.SetPath(p,poly.GetPath(p));}
        }
        var doorClones=new Dictionary<DoorHingeInteraction,DoorHingeInteraction>();
        foreach(var door in doors){var copy=Object.Instantiate(door.gameObject);copy.transform.position=door.transform.position;copy.transform.rotation=door.transform.rotation;copy.transform.localScale=door.transform.lossyScale;doorClones[door]=copy.GetComponent<DoorHingeInteraction>();}
        var team=Object.Instantiate(authored.captain.transform.parent.gameObject);team.name="Fixture Squad";
        team.transform.position=authored.captain.transform.parent.position;
        var director=Object.Instantiate(authored.gameObject).GetComponent<KnightCaptainAmbush>();
        director.captain=team.transform.Find(authored.captain.name).GetComponent<SquadPatrolCharacterAi>();
        director.guards=authored.guards.Select(g=>team.transform.Find(g.name).GetComponent<SquadPatrolCharacterAi>()).ToArray();
        for(int i=0;i<director.safeHouses.Length;i++)
        {
            var source=authored.safeHouses[i];var area=Object.Instantiate(source.permission.gameObject).GetComponent<PermissionArea>();
            area.transform.position=source.permission.transform.position;area.transform.rotation=source.permission.transform.rotation;area.transform.localScale=source.permission.transform.lossyScale;
            director.safeHouses[i].permission=area;director.safeHouses[i].entrance=doorClones[source.entrance];
        }
        foreach(var ai in team.GetComponentsInChildren<SquadPatrolCharacterAi>())Set(ai,"enableAwarenessSound",false);
        EditorSceneManager.CloseScene(original,true);
        EditorSceneManager.SaveScene(fixture,"Assets/KnightAmbushSmokeFixture.unity");
        SessionState.SetInt(Key+".checks",checks);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);checks=SessionState.GetInt(Key+".checks",0);suite=Verify();deadline=EditorApplication.timeSinceStartup+240;}
            if(suite==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Knight test timeout");
            if(suite.MoveNext())return;
            suite=null;Debug.Log("KNIGHT_AMBUSH_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){suite=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        Time.timeScale=0;yield return null;yield return null;
        var director=Object.FindObjectOfType<KnightCaptainAmbush>();
        var captain=director.captain;var actors=new[]{captain}.Concat(director.guards).ToArray();
        var data=actors.Select(a=>a.GetComponent<ZeldaCharacterData>()).ToArray();
        var aura=captain.GetComponent<SquadRegenerationAura>();
        var originalData=data.Select(d=>d.CaptureRuntimeState()).ToArray();
        var baseline=director.CaptureRuntimeState();
        var journal=QuestJournalManager.GetOrCreate();journal.ResetAllEntries();
        string id=QuestJournalManager.KnightAmbushQuestId;
        Check(!journal.CanTriggerEntry(id),"parent acceptance gates child");
        journal.RecordQuestCompletion(id,true);
        Check(!journal.ContainsEntry(id),"early possession evidence does not reveal child");
        journal.AddOrUpdateEntry(QuestJournalManager.FortressProofQuestId,"失窃的证明","测试");
        journal.AddOrUpdateEntry(id,QuestJournalManager.KnightAmbushQuestTitle,QuestJournalManager.KnightAmbushDescription);
        Check(journal.IsEntryCompleted(id),"early possession evidence completes on later reading");
        var journalState=JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries();journal.ApplySaveState(journalState);
        Check(journal.IsEntryCompleted(id)&&journal.GetParentEntryId(id)==QuestJournalManager.FortressProofQuestId,"quest evidence and parent survive save roundtrip");
        var texts=new List<string>();var completed=new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState",F).Invoke(null,new object[]{id,journal,texts,completed});
        Check(texts.Count==1&&texts[0]=="制造一场足够规模的袭击","only requested HUD hint");

        foreach(var ai in actors)Check(ai.ResolveIncomingAttackDamage(7)==7,"squad unaware cannon hit not tripled "+ai.name);
        var ordinaryGo=new GameObject("Ordinary NPC regression");ordinaryGo.SetActive(false);ordinaryGo.transform.position=new Vector3(500,500,0);
        ordinaryGo.AddComponent<CivilianZeldaCharacterData>();ordinaryGo.AddComponent<ZeldaFourWayMover>().enabled=false;
        var ordinary=ordinaryGo.AddComponent<ZeldaPatrolCharacterAi>();ordinaryGo.SetActive(true);
        Check(ordinary.ResolveIncomingAttackDamage(2)==6,"non-squad surprise damage unchanged");Object.DestroyImmediate(ordinaryGo);
        data[1].TakeDamage(2,null);int hp=data[1].CurrentHealth;
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Patrol,"single minor hit does not evacuate");
        Set(aura,"elapsed",.49f);Call(aura,"Update");Check(data[1].CurrentHealth==hp,"aura does not tick early");
        Set(aura,"elapsed",.5f);Call(aura,"Update");Check(data[1].CurrentHealth==hp+1,"aura heals other member one HP per .5 seconds");
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Patrol,"healing does not count as assault");
        data[0].TakeDamage(1,null);int ownHp=data[0].CurrentHealth;
        Set(aura,"elapsed",.5f);Call(aura,"Update");Check(data[0].CurrentHealth==ownHp,"aura does not heal captain");
        director.ApplyRuntimeState(baseline);Call(director,"Update");
        for(int i=0;i<data.Length;i++)data[i].ApplyRuntimeState(originalData[i]);
        data[1].TakeDamage(6,null);data[1].TakeDamage(6,null);
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Patrol,"twelve damage to only one surviving guard is insufficient");
        director.ApplyRuntimeState(baseline);Call(director,"Update");data[1].ApplyRuntimeState(originalData[1]);
        data[1].TakeDamage(6,null);var expired=director.CaptureRuntimeState();expired.hits[0].age=13;
        director.ApplyRuntimeState(expired);Call(director,"Update");data[2].TakeDamage(6,null);
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Patrol,"expired damage does not aggregate into next attack window");
        director.ApplyRuntimeState(baseline);Call(director,"Update");
        for(int i=0;i<data.Length;i++)data[i].ApplyRuntimeState(originalData[i]);
        var shielded=data[1].CaptureRuntimeState();shielded.holyShield=new HolyShieldRuntime.State{charges=3,capacity=3};data[1].ApplyRuntimeState(shielded);
        data[1].TakeDamage(10,null);
        Check(director.CaptureRuntimeState().hits.Count==0,"absorbed shield hit contributes no HP loss");
        data[1].ApplyRuntimeState(originalData[1]);
        data[1].TakeDamage(6,null);data[2].TakeDamage(6,null);
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat,"two injured escorts and twelve actual damage trigger evacuation");
        var incident=director.CaptureRuntimeState();
        Check(incident.routes.Length==4&&incident.routes.All(r=>r.points.Length>0),"independent saved routes for whole squad");
        Check(director.safeHouses[director.SelectedHouse].permission.PermissionLevel==5,"selected safehouse permission rises to five");
        Check(director.safeHouses[1-director.SelectedHouse].permission.PermissionLevel==1,"unused house retains normal permission");
        foreach(var ai in actors)
        {
            var prop=typeof(SquadPatrolCharacterAi).GetProperty("AiMovementSpeedMultiplier",F);
            Check(Mathf.Approximately((float)prop.GetValue(ai),1.2f),"retreat uses 1.2x base speed "+ai.name);
            Check(ai.RequiresVisionStreamingRetention&&!ai.IsInPossessableState,"retreat retained and not possessable "+ai.name);
        }
        var manager=SceneTravelStateManager.GetOrCreate();
        string disk=manager.CaptureDiskWorld();
        Check(disk.Contains("knightAmbushState")&&disk.Contains("\"house\":"+director.SelectedHouse),"disk-world snapshot contains director state");
        Check(System.Text.RegularExpressions.Regex.Matches(disk,"\"hasKnightAmbush\":true").Count==1,
            "explicit save discriminator separates controller from ordinary objects");
        bool gotSavedMarker=manager.TryGetSavedKnightMarker(director.gameObject.scene.name,out var savedMarker);
        Debug.Log("KNIGHT_SAVED_MARKER scene="+director.gameObject.scene.name+" active="+SceneManager.GetActiveScene().name+" found="+gotSavedMarker+" saved="+savedMarker.ToString("F6")+" live="+director.CachedMarker.ToString("F6"));
        Check(gotSavedMarker&&Vector2.Distance(savedMarker,director.CachedMarker)<.001f,
            "unloaded scene can read last sampled captain position from snapshot");
        Check(manager.TryGetSavedKnightMarker(director.gameObject.scene.name,out savedMarker),"subsequent saved marker lookup uses index");
        var worldState=Call(manager,"CreateSceneSnapshot",SceneManager.GetActiveScene());
        var detached=director.CaptureRuntimeState();detached.routes[0].next=999;
        Check(director.CaptureRuntimeState().routes[0].next!=999,"checkpoint detached from live route progress");
        director.ApplyRuntimeState(baseline);Call(director,"Update");
        Check(director.safeHouses.All(h=>h.permission.PermissionLevel==1),"rewinding pre-assault snapshot restores permissions");
        director.ApplyRuntimeState(incident);Call(director,"Update");
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat,"restored retreat resumes");
        director.ApplyRuntimeState(baseline);Call(director,"Update");
        Check((bool)Call(manager,"RestoreSceneSnapshot",SceneManager.GetActiveScene(),worldState,false),"scene-travel restore applies full snapshot");
        Call(director,"Update");
        Check(director.SelectedHouse==incident.house&&director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat&&
            director.CaptureRuntimeState().routes[0].points.SequenceEqual(incident.routes[0].points),"scene-travel restore retains selected house and absolute path");

        // Actual navigation, not teleporting to waypoints: retain real doors/walls.
        Time.timeScale=4;float start=Time.time;
        while(director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat&&Time.time-start<150)
        {
            if(Time.frameCount%400==0)Debug.Log("KNIGHT_ROUTE "+(Time.time-start)+" "+string.Join(" | ",actors.Select(a=>a.name+" "+a.transform.position+" target="+director.NextDestination(a)+" next="+director.CaptureRuntimeState().routes[Array.IndexOf(actors,a)].next)));
            yield return null;
        }
        Time.timeScale=0;
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Sheltered,"whole squad navigated actual city geometry into safehouse/posts");
        for(int i=0;i<actors.Length;i++)
        {
            Check(Vector2.Distance(actors[i].transform.position,director.PostPosition(actors[i]))<.5f,"assigned post reached "+actors[i].name);
            Check(!director.NeedsRetention(actors[i]),"idle post releases mission streaming retention");
        }
        Check(captain.IsInPossessableState,"isolated sheltered captain can be possessed");
        var postState=director.CaptureRuntimeState();
        Check(postState.phase==KnightCaptainAmbush.Phase.Sheltered,"shelter phase persisted");
        director.ApplyRuntimeState(postState);Call(director,"Update");
        Check(captain.IsInPossessableState,"restored sheltered captain remains possessable");

        // Directly test the second house's final route against its real doorway.
        var second=director.CaptureRuntimeState();second.house=1-director.SelectedHouse;second.phase=KnightCaptainAmbush.Phase.Retreat;
        for(int i=0;i<actors.Length;i++)
        {
            var house=director.safeHouses[second.house];var p=house.approach+new Vector2((i-1.5f)*.9f,-2.5f);
            actors[i].GetComponent<Rigidbody2D>().position=p;actors[i].transform.position=p;
            var path=new List<Vector2>{house.approach};path.AddRange(i==0?house.captainPath:house.guards[i-1].path);
            second.routes[i]=new KnightCaptainAmbush.Route{points=path.ToArray()};
        }
        Physics2D.SyncTransforms();director.ApplyRuntimeState(second);Call(director,"Update");
        Time.timeScale=2;start=Time.time;
        while(director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat&&Time.time-start<40)yield return null;
        Time.timeScale=0;
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Sheltered,"second safehouse all three posts reached");

        // Marker position changes only on the throttled sample, both map readers share it.
        Set(director,"nextMarkerTime",Time.unscaledTime+1);var old=director.CachedMarker;
        captain.transform.position+=(Vector3)Vector2.right;
        Call(director,"Update");Check(director.CachedMarker==old,"marker not read every frame");
        Set(director,"nextMarkerTime",0f);Call(director,"Update");
        Check(Vector2.Distance(director.CachedMarker,captain.transform.position)<.01f,"marker refresh samples captain");
        Check(Mathf.Abs(Get<float>(director,"nextMarkerTime")-Time.unscaledTime-.7f)<.01f,"next marker refresh is .7 seconds");
        var ui=new GameObject("Map",typeof(RectTransform)).AddComponent<RuntimeMiniMapGraphic>();
        Set(ui,"geometrySceneName",director.gameObject.scene.name);var targets=new List<Vector2>();ui.GetQuestTargets(id,targets);
        Check(targets.Count==1&&targets[0]==director.CachedMarker&&ui.TryGetQuestTarget(id,out var m)&&m==targets[0],"minimap/menu share throttled target without geometry rebake");

        journal.ResetAllEntries();
        captain.GetComponent<ZeldaFourWayMover>().enabled=true;ZeldaRuntimeRegistry.ClaimControlledMover(captain.GetComponent<ZeldaFourWayMover>());
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Possessed&&!director.HasOrders(captain),"possession hands control to player without movement orders");
        journal.AddOrUpdateEntry(QuestJournalManager.FortressProofQuestId,"失窃的证明","测试");journal.AddOrUpdateEntry(id,QuestJournalManager.KnightAmbushQuestTitle,QuestJournalManager.KnightAmbushDescription);
        Check(journal.IsEntryCompleted(id),"exact captain possession completes child through saved evidence");
        // Reopen only the test director to exercise the alternative casualty trigger.
        captain.GetComponent<ZeldaFourWayMover>().enabled=false;
        director.ApplyRuntimeState(baseline);Call(director,"Update");
        data[3].TakeDamage(999,null);
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.Retreat,"escort death independently triggers retreat");
        data[0].DieFromSoulDetonation();hp=data[1].CurrentHealth;
        Set(aura,"elapsed",.5f);Call(aura,"Update");Check(data[1].CurrentHealth==hp,"dead commander cannot heal squad");
        Check(director.CurrentPhase==KnightCaptainAmbush.Phase.CaptainDead&&!director.HasOrders(actors[1]),"captain death releases mission orders without resurrection");
    }
}
