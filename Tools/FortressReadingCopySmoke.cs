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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FortressReadingCopySmoke
{
    const string Key = nameof(FortressReadingCopySmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame=-1, checks;
    static double deadline;
    static FortressReadingCopySmoke() { EditorApplication.update += Tick; }
    static object Call(object obj,string method,params object[] args) => obj.GetType().GetMethod(method,F).Invoke(obj,args);
    static T Get<T>(object obj,string field) => (T)obj.GetType().GetField(field,F).GetValue(obj);
    static void Set(object obj,string field,object value) => obj.GetType().GetField(field,F).SetValue(obj,value);
    static void Check(bool ok,string label) { if(!ok)throw new Exception(label); checks++; Debug.Log("READING_COPY_CHECK " + label); }
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated only");
        PlayerSettings.productName="fortress-reading-copy-smoke";
        var source=EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var docs=Object.FindObjectsOfType<DocumentReader>(true).Where(d=>d.name=="要塞情报"||d.name=="巡逻队信息").ToArray();
        Check(docs.Length==2,"exactly two target documents");
        var fixture=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        foreach(var doc in docs)
        {
            Check(Get<bool>(doc,"keepAuthoredTextOnLoad"),"updated prose survives old scene snapshot");
            string details=Get<string>(doc,"journalEntryDetails");
            Check(details==(doc.name=="要塞情报"?QuestJournalManager.FortressProofDescription:QuestJournalManager.KnightAmbushDescription),
                "journal description unchanged " + doc.name);
            var clone=Object.Instantiate(doc.gameObject); clone.name=doc.name; clone.transform.position=Vector3.zero;
            SceneManager.MoveGameObjectToScene(clone,fixture);
        }
        EditorSceneManager.CloseScene(source,true);
        EditorSceneManager.SaveScene(fixture,"Assets/FortressReadingCopyFixture.unity");
        SessionState.SetInt(Key+".checks",checks); SessionState.SetBool(Key,true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return; frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(Key,false)) { SessionState.SetBool(Key,false); checks=SessionState.GetInt(Key+".checks",0); suite=Verify(); deadline=EditorApplication.timeSinceStartup+90; }
            if(suite==null)return;
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Reading test timeout");
            if(suite.MoveNext())return;
            suite=null; Debug.Log("FORTRESS_READING_COPY_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { suite=null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while(DocumentReader.IsInputBlocked||GameSaveSystem.IsPreparingSceneSave)yield return null;
        var journal=QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var actor=new GameObject("Reader").AddComponent<CivilianZeldaCharacterData>();
        var mover=actor.gameObject.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var docs=Object.FindObjectsOfType<DocumentReader>();
        var parent=docs.Single(d=>d.name=="要塞情报"); var child=docs.Single(d=>d.name=="巡逻队信息");
        yield return null; yield return null; Physics2D.SyncTransforms();
        Call(child,"OpenDocument");
        Check(DocumentReader.IsDocumentOpen&&!journal.ContainsEntry(QuestJournalManager.KnightAmbushQuestId),"child reading still requires parent quest");
        Call(child,"CloseDocument"); yield return null; yield return null;
        foreach(var doc in new[]{parent,child})
        {
            Call(doc,"OpenDocument"); Check(DocumentReader.IsDocumentOpen,"real reading window opens");
            yield return null; Canvas.ForceUpdateCanvases();
            var text=Get<Text>(doc,"contentText");
            Check(text.text==doc.DocumentText,"window shows new authored prose");
            Check(text.preferredHeight<=text.rectTransform.rect.height+.1f,"full prose fits: " + doc.name + " height="+text.preferredHeight+" available="+text.rectTransform.rect.height);
            var settings=text.GetGenerationSettings(text.rectTransform.rect.size);
            var clipped=new TextGenerator(); clipped.Populate(text.text,settings);
            settings.verticalOverflow=VerticalWrapMode.Overflow;
            var full=new TextGenerator(); full.Populate(text.text,settings);
            Check(clipped.characterCountVisible==full.characterCountVisible,
                "all actual glyphs visible " + doc.name + ": " + clipped.characterCountVisible + "/" + full.characterCountVisible);
            Capture(Get<Canvas>(doc,"readingCanvas"),doc.name=="要塞情报"?"FortressIntel-preview.png":"KnightPatrolNotice-preview.png");
            string id=Get<string>(doc,"journalEntryId"); Check(journal.ContainsEntry(id),"reading triggers same quest");
            var fields=SavedScalarFields.Capture(doc,true);
            fields.RemoveAll(v=>v.name=="keepAuthoredTextOnLoad");
            fields.Single(v=>v.name=="documentText").text="old saved reading text";
            string prose=doc.DocumentText;
            Call(doc,"RestoreSavedFields",fields);
            Check(doc.DocumentText==prose&&Get<bool>(doc,"journalEntryWasAdded"),"legacy snapshot keeps new prose and already-read flag");
            Call(doc,"CloseDocument"); yield return null; yield return null;
        }
        Check(journal.GetParentEntryId(QuestJournalManager.KnightAmbushQuestId)==QuestJournalManager.FortressProofQuestId,"parent-child relationship unchanged");
        var plain=new GameObject("Unrelated document"); plain.SetActive(false); var plainDoc=plain.AddComponent<DocumentReader>();
        Set(plainDoc,"documentText","authored unrelated text"); var plainFields=SavedScalarFields.Capture(plainDoc,true);
        plainFields.Single(v=>v.name=="documentText").text="saved unrelated text";
        Call(plainDoc,"RestoreSavedFields",plainFields);
        Check(plainDoc.DocumentText=="saved unrelated text","unrelated document save behavior unchanged");
    }
    static void Capture(Canvas canvas,string filename)
    {
        var camera=new GameObject("Reading preview camera").AddComponent<Camera>(); camera.enabled=false;
        camera.orthographic=true; camera.orthographicSize=360; camera.transform.position=new Vector3(0,0,-100);
        camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color32(12,20,35,255); camera.cullingMask=1<<30;
        foreach(Transform t in canvas.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
        var rt=new RenderTexture(1280,720,24); camera.targetTexture=rt;
        canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=10;
        Canvas.ForceUpdateCanvases(); camera.Render(); var old=RenderTexture.active; RenderTexture.active=rt;
        var image=new Texture2D(1280,720,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"../"+filename),image.EncodeToPNG());
        RenderTexture.active=old; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null;
        Object.Destroy(camera.gameObject); Object.Destroy(rt); Object.Destroy(image);
    }
}
