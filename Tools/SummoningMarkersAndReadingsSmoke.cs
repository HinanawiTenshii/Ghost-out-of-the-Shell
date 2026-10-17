using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SummoningMarkersAndReadingsSmoke
{
    const string Key=nameof(SummoningMarkersAndReadingsSmoke);
    const string Quest=QuestJournalManager.SummoningQuestId;
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator suite; static int frame=-1,checks; static double deadline;
    static SummoningMarkersAndReadingsSmoke(){EditorApplication.update+=Tick;}
    static FieldInfo Field(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,F|BindingFlags.DeclaredOnly);if(f!=null)return f;}throw new Exception(n);}
    static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
    static T Get<T>(object o,string n)=>(T)Field(o,n).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;Debug.Log("MARKER_READING_CHECK "+label);}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("isolated only");
        PlayerSettings.productName="summoning-reading-smoke";
        var fixture=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        int docs=0;
        foreach(string path in new[]{"Assets/Scenes/Level0.unity","Assets/Scenes/Level1/Level1-Floor-1.unity","Assets/Scenes/Level1/Level1-Floor1.unity",
            "Assets/Scenes/Level1/Level1-Floor2.unity","Assets/Scenes/Level1/Level1-Floor3.unity","Assets/Scenes/Level1/Level1-Garden.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
            foreach(var doc in Object.FindObjectsOfType<DocumentReader>(true).Where(d=>d.gameObject.scene==scene).ToArray())
            {
                Check(Get<bool>(doc,"keepAuthoredTextOnLoad"),"all level0/1 documents updated "+scene.name+"/"+doc.name);
                Check(doc.DocumentText.Length>60,"authored document is not placeholder "+doc.name);
                var clone=Object.Instantiate(doc.gameObject);clone.name=scene.name+"_"+doc.name;
                clone.transform.SetParent(null);SceneManager.MoveGameObjectToScene(clone,fixture);clone.transform.position=Vector3.zero;
                docs++;
            }
            EditorSceneManager.CloseScene(scene,true);
        }
        Check(docs==8,"full document inventory contains eight readings");
        var floor1=EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity",OpenSceneMode.Additive);
        var source=Object.FindObjectsOfType<SummoningCircle>(true).Single(c=>c.JournalQuestId==Quest);
        var ids=source.BoundCharacters.ToArray();Check(ids.Length==6&&ids.All(i=>i!=null)&&ids.Select(i=>i.StableId).Distinct().Count()==6,"actual ritual binds six distinct identities");
        var circle=Object.Instantiate(source.gameObject);circle.name="Test ritual";circle.transform.SetParent(null);SceneManager.MoveGameObjectToScene(circle,fixture);
        circle.transform.position=new Vector3(100,100,0);
        EditorSceneManager.CloseScene(floor1,true);
        var found=new HashSet<string>();
        foreach(string name in new[]{"Level2-Floor1","Level2-Floor-1","Level2-Floor2"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Scenes/Level2/"+name+".unity",OpenSceneMode.Additive);
            foreach(var actor in Object.FindObjectsOfType<ZeldaCharacterData>(true).Where(a=>a.gameObject.scene==scene))
                if(ids.Any(i=>i.StableId==actor.PersistentCharacterId))found.Add(actor.PersistentCharacterId);
            EditorSceneManager.CloseScene(scene,true);
        }
        Check(found.Count==6,"all six actual scene actors carry their bound identity");
        SceneManager.SetActiveScene(fixture);
        for(int i=0;i<6;i++)
        {
            var go=new GameObject("Offering "+i);go.SetActive(false);var actor=go.AddComponent<CivilianZeldaCharacterData>();
            Set(actor,"persistentIdentity",ids[i]);go.transform.position=new Vector3(i*2,3,0);go.SetActive(true);
        }
        EditorSceneManager.SaveScene(fixture,"Assets/SummoningMarkersAndReadingsFixture.unity");
        SessionState.SetInt(Key+".checks",checks);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;
        try{
            if(SessionState.GetBool(Key,false)){SessionState.SetBool(Key,false);checks=SessionState.GetInt(Key+".checks",0);suite=Verify();deadline=EditorApplication.timeSinceStartup+120;}
            if(suite==null)return;if(EditorApplication.timeSinceStartup>deadline)throw new Exception("test timeout");
            if(suite.MoveNext())return;suite=null;Debug.Log("SUMMONING_MARKERS_READINGS_PASS checks="+checks);EditorApplication.Exit(0);
        }catch(Exception e){suite=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        while(DocumentReader.IsInputBlocked||GameSaveSystem.IsPreparingSceneSave)yield return null;
        var journal=QuestJournalManager.GetOrCreate();journal.ResetAllEntries();
        var actors=Object.FindObjectsOfType<CivilianZeldaCharacterData>().Where(a=>a.name.StartsWith("Offering ")).OrderBy(a=>a.name).ToArray();
        var circle=Object.FindObjectOfType<SummoningCircle>();SummoningCircle.ReportLoadedQuestProgress();
        foreach(var actor in actors){var marker=actor.GetComponent<SummoningQuestCharacterMarker>();Check(marker!=null,"identity creates marker component");Call(marker,"LateUpdate");Check(Get<GameObject>(marker,"visual")==null,"unaccepted quest shows no marker");}
        journal.AddOrUpdateEntry(Quest,QuestJournalManager.SummoningQuestTitle,QuestJournalManager.SummoningQuestIntroduction);journal.TrackEntry(Quest);yield return null;
        foreach(var actor in actors){var marker=actor.GetComponent<SummoningQuestCharacterMarker>();Call(marker,"LateUpdate");var visual=Get<GameObject>(marker,"visual");
            Check(visual!=null&&visual.activeSelf,"tracked offering marked "+actor.name);
            Check(visual.GetComponentsInChildren<SpriteRenderer>().All(r=>r.sortingOrder<30000)&&visual.GetComponentsInChildren<Collider2D>().Length==0,"world glyph below vision mask, no collision");
            actor.transform.position+=Vector3.right;Call(marker,"LateUpdate");Check(Vector3.Distance(visual.transform.position,actor.AttributeWindowAnchor.position+Vector3.up*1.25f)<.001f,"marker follows actor");}
        foreach(string scene in new[]{"Level2-Floor1","Level2-Floor-1","Level2-Floor2"}){
            var map=new GameObject("Map",typeof(RectTransform)).AddComponent<RuntimeMiniMapGraphic>();map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/"+scene));
            var points=new List<Vector2>();map.GetQuestTargets(Quest,points);Check(points.Count==(scene=="Level2-Floor1"?1:0),"no character markers added to map "+scene);Object.Destroy(map.gameObject);}
        var target=actors[0];var targetMarker=target.GetComponent<SummoningQuestCharacterMarker>();
        target.gameObject.SetActive(false);Check(!Get<GameObject>(targetMarker,"visual").activeSelf,"distance unloading hides marker");target.gameObject.SetActive(true);yield return null;
        Check(Get<GameObject>(targetMarker,"visual").activeSelf,"reloading restores marker");
        journal.AddOrUpdateEntry("other.quest","其他任务","test");journal.TrackEntry("other.quest");yield return null;
        Check(actors.All(a=>!Get<GameObject>(a.GetComponent<SummoningQuestCharacterMarker>(),"visual").activeSelf),"tracking different task hides all six");journal.TrackEntry(Quest);yield return null;
        var state=journal.CaptureSaveState();journal.ResetAllEntries();journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state)));
        Check(actors.All(a=>journal.IsTrackedSummoningTarget(a.PersistentCharacterId)),"six identities persist through save");
        var otherScene=SceneManager.CreateScene("Marker travel destination");SceneManager.MoveGameObjectToScene(target.gameObject,otherScene);Call(targetMarker,"LateUpdate");
        Check(Get<GameObject>(targetMarker,"visual").scene==otherScene,"visual follows owner across scenes");
        var fresh=new GameObject("Restored actor").AddComponent<CivilianZeldaCharacterData>();fresh.ApplyRuntimeState(target.CaptureRuntimeState());
        Check(fresh.GetComponent<SummoningQuestCharacterMarker>()!=null,"restored identity creates marker even without prefab identity");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState{litMask=1,consumedIds=new[]{target.PersistentCharacterId}});yield return null;
        Check(!Get<GameObject>(targetMarker,"visual").activeSelf&&!journal.IsTrackedSummoningTarget(target.PersistentCharacterId),"consumed identity no longer marked");
        state=journal.CaptureSaveState();journal.ResetAllEntries();journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state)));
        Check(!journal.IsTrackedSummoningTarget(target.PersistentCharacterId),"consumed identity exclusion persists");
        journal.CompleteEntry(Quest);yield return null;Check(actors.All(a=>!Get<GameObject>(a.GetComponent<SummoningQuestCharacterMarker>(),"visual").activeSelf),"completion hides all markers");
        state.summoningTargetIds=null;state.summoningConsumedIds=null;state.completed=new List<string>();state.tracked=Quest;
        journal.ApplySaveState(state);Check(actors.All(a=>journal.IsTrackedSummoningTarget(a.PersistentCharacterId)),"old saves on another floor resolve six original identities");

        journal.ResetAllEntries();var reader=new GameObject("Reader").AddComponent<CivilianZeldaCharacterData>();var mover=reader.gameObject.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        yield return null;yield return null;Physics2D.SyncTransforms();
        foreach(var doc in Object.FindObjectsOfType<DocumentReader>().OrderBy(d=>d.name)){
            while(DocumentReader.IsInputBlocked)yield return null;
            Call(doc,"OpenDocument");Check(DocumentReader.IsDocumentOpen,"actual reading opens "+doc.name);yield return null;Canvas.ForceUpdateCanvases();
            var text=Get<Text>(doc,"contentText");var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
            var clipped=new TextGenerator();clipped.Populate(text.text,settings);settings.verticalOverflow=VerticalWrapMode.Overflow;var full=new TextGenerator();full.Populate(text.text,settings);
            Check(clipped.characterCountVisible==full.characterCountVisible,"all glyphs fit "+doc.name+" "+clipped.characterCountVisible+"/"+full.characterCountVisible);
            Capture(Get<Canvas>(doc,"readingCanvas"),doc.name);
            var fields=SavedScalarFields.Capture(doc,true);fields.Single(v=>v.name=="documentText").text="old prose";fields.RemoveAll(v=>v.name=="keepAuthoredTextOnLoad");
            string expected=doc.DocumentText;Call(doc,"RestoreSavedFields",fields);Check(doc.DocumentText==expected,"old save preserves new reading "+doc.name);
            Call(doc,"CloseDocument");yield return null;yield return null;
        }
    }
    static void Capture(Canvas canvas,string name){
        var camera=new GameObject("Preview").AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=360;camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(12,20,35,255);camera.cullingMask=1<<30;
        foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        var rt=new RenderTexture(1280,720,24);camera.targetTexture=rt;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;
        Canvas.ForceUpdateCanvases();camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../Reading-"+name+".png"),image.EncodeToPNG());
        RenderTexture.active=old;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.worldCamera=null;Object.Destroy(camera.gameObject);Object.Destroy(rt);Object.Destroy(image);
    }
}
