#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class QuestEdgeMenuSmoke
{
    const BindingFlags F=BindingFlags.Static|BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    const string Key="QuestEdgeMenuSmoke";
    static IEnumerator suite;static int frame=-1,checks;static double deadline;
    static QuestEdgeMenuSmoke(){EditorApplication.update+=Tick;}
    static object Field(object o,string n)=>o.GetType().GetField(n,F).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Isolated clone only");
        PlayerSettings.productName="quest-edge-"+Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(suite==null){suite=Verify();deadline=EditorApplication.timeSinceStartup+180;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Timeout");
            if(suite.MoveNext())return;
            SessionState.SetBool(Key,false);Debug.Log("QUEST_EDGE_MENU_RUNTIME_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        var profile=Object.FindObjectOfType<LevelSceneVisualProfile>();profile.gameObject.AddComponent<DevlogDemoSceneSettings>();
        float wait=Time.realtimeSinceStartup+3;
        while(Time.realtimeSinceStartup<wait||RetroSceneLoadReveal.IsBlockingInput||RetroSceneLoadReveal.IsPreparingGameplay)yield return null;
        var journal=QuestJournalManager.GetOrCreate();var menu=Object.FindObjectOfType<TabJournalMenuController>();
        menu.OpenMenu();
        wait=Time.realtimeSinceStartup+3;
        while(Time.realtimeSinceStartup<wait)yield return null;
        Canvas.ForceUpdateCanvases();
        var map=(RuntimeMiniMapGraphic)Field(menu,"miniMapGraphic");
        string id="test.quest-edge";
        journal.AddOrUpdateEntry(id,"边界检查","仅测试使用","Level2-Floor1");
        var targets=(List<RuntimeMiniMapQuestTargetData>)Field(map,"questTargets");
        var target=new RuntimeMiniMapQuestTargetData{questId=id,position=new Vector2(100000,50000)};targets.Add(target);
        journal.TrackEntry(id);Call(menu,"HandleTrackedQuestChanged");
        Check((bool)Field(map,"clampQuestTargetToBorder"),"Changing tracked quest enables full-map edge anchoring");
        Check((bool)Field(map,"showTrackedQuestTarget"),"Tracked destination is rendered");
        foreach(float delta in new[]{-100f,2f,100f})
        foreach(float pan in new[]{0f,.5f,1f})
        {
            map.ZoomBy(delta);map.SetHorizontalPan01(pan);map.SetVerticalPan01(1-pan);CheckEdge(map);
        }
        // Moving destinations use the same event path as captain refresh, no
        // additional scanning or faster target polling is introduced.
        target.position=new Vector2(-100000,-100000);targets[targets.Count-1]=target;
        Call(menu,"HandleTrackedQuestChanged");CheckEdge(map);
        Check((Vector2)Field(map,"trackedQuestTargetPosition")==target.position,"Moving target refresh uses new coordinates");
        map.ResetView();map.ZoomBy(2f);
        map.SetTrackedQuestTargets(new[]{Vector2.left*100000,Vector2.right*100000},true);CheckEdge(map,2);
        typeof(VisualStyleLabSmoke).GetMethod("Capture",F).Invoke(null,new object[]{"Quest-Edge-FullMap"});
        // Actual remote-scene snapshot and task ID (not synthetic coordinates).
        journal.AddOrUpdateEntry(QuestJournalManager.FortressProofQuestId,"失窃的证明","检查远端目标","Level2-Floor2");
        journal.TrackEntry(QuestJournalManager.FortressProofQuestId);
        Call(menu,"SelectMapScene","Level2-Floor2");
        Check((bool)Field(map,"clampQuestTargetToBorder"),"Selecting remote floor retains edge mode");
        Check((bool)Field(map,"showTrackedQuestTarget"),"Remote saved map retains destination");
        menu.CloseMenu();yield return null;menu.OpenMenu();yield return null;
        Check((bool)Field(map,"clampQuestTargetToBorder"),"Reopening journal retains edge mode");
        menu.CloseMenu();
        wait=Time.realtimeSinceStartup+2;
        while(Time.realtimeSinceStartup<wait)yield return null;
        var hud=Object.FindObjectOfType<ZeldaHealthHeartsUI>();var small=(RuntimeMiniMapGraphic)Field(hud,"taskMiniMapGraphic");
        small.SetTrackedQuestTarget(true,Vector2.right*100000,true);CheckEdge(small);
        typeof(VisualStyleLabSmoke).GetMethod("Capture",F).Invoke(null,new object[]{"Quest-Edge-Hud"});
    }
    static void CheckEdge(RuntimeMiniMapGraphic map,int count=1)
    {
        using(var vh=new VertexHelper())
        {
            Call(map,"PopulateMarkers",vh);Check(vh.currentVertCount>=count*22,"Complete quest glyphs exist");
            var rect=map.GetPixelAdjustedRect();
            for(int marker=0;marker<count;marker++)
            {
                Vector2 min=Vector2.positiveInfinity,max=Vector2.negativeInfinity;
                for(int i=vh.currentVertCount-22*(marker+1);i<vh.currentVertCount-22*marker;i++)
                {var v=new UIVertex();vh.PopulateUIVertex(ref v,i);min=Vector2.Min(min,v.position);max=Vector2.Max(max,v.position);}
                Check(min.x>=rect.xMin&&max.x<=rect.xMax&&min.y>=rect.yMin&&max.y<=rect.yMax,"All vertices inside actual UI viewport");
                float gap=Mathf.Min(Mathf.Min(min.x-rect.xMin,rect.xMax-max.x),Mathf.Min(min.y-rect.yMin,rect.yMax-max.y));
                Check(Mathf.Abs(gap-1f)<.02f,"Glyph touches live inner frame edge: "+gap);
            }
        }
    }
}
#endif
