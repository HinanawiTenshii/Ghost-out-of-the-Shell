#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class LevelVisualRolloutSmoke
{
    const string Key="LevelVisualRolloutSmoke";
    static IEnumerator suite;static int checks, frame=-1;static double deadline;
    static LevelVisualRolloutSmoke(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;}
    public static void Run()
    {
        if(!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"))throw new Exception("Clone only");
        PlayerSettings.productName="level-style-test-"+Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene(LevelVisualRollout.Scenes[0]);SessionState.SetBool(Key,true);SessionState.SetString(Key+"Error","");
        EditorApplication.EnterPlaymode();
    }
    static void Log(string text,string stack,LogType type)
    {if(SessionState.GetBool(Key,false)&&(type==LogType.Error||type==LogType.Exception||type==LogType.Assert))SessionState.SetString(Key+"Error",text+"\n"+stack);}
    static void Check(bool yes,string what){checks++;if(!yes)throw new Exception(what);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(suite==null){suite=Verify();deadline=EditorApplication.timeSinceStartup+900;}
            if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Runtime tests timed out");
            var error=SessionState.GetString(Key+"Error","");if(error.Length>0)throw new Exception(error);
            if(suite.MoveNext())return;
            SessionState.SetBool(Key,false);Debug.Log("LEVEL_VISUAL_RUNTIME_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Verify()
    {
        foreach(var path in LevelVisualRollout.Scenes)
        {
            if(SceneManager.GetActiveScene().path!=path)EditorSceneManager.LoadSceneInPlayMode(path,new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var scene=SceneManager.GetActiveScene();
            var profile=Object.FindObjectOfType<LevelSceneVisualProfile>();Check(profile!=null,"Profile "+path);
            profile.gameObject.AddComponent<DevlogDemoSceneSettings>(); // Test-only: no automatic saves.
            float wait=Time.realtimeSinceStartup+3;
            while(Time.realtimeSinceStartup<wait||RetroSceneLoadReveal.IsPreparingGameplay||RetroSceneLoadReveal.IsBlockingInput)yield return null;
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Component>(true)).Where(c=>c!=null).ToArray();
            Check(all.OfType<LevelSceneVisualProfile>().Count()==1,"Exactly one profile "+path);
            Check(Object.FindObjectsOfType<VisualStyleLabTerminalScope>().Length>0,"HUD themed after travel "+path);
            Check(all.OfType<SpriteRenderer>().Any(s=>s.sharedMaterial&&s.sharedMaterial.shader.name=="Game/Terminal Floor Detail"),"Floor material "+path);
            foreach(var s in all.OfType<SpriteRenderer>().Where(s=>s.sharedMaterial&&s.sharedMaterial.shader.name=="Game/Terminal Floor Detail"))
                Check(!ShaderUtil.ShaderHasError(s.sharedMaterial.shader)&&s.sharedMaterial.GetFloat("_Contrast")<=.2f,"Restrained supported floor texture");
            foreach(var ai in all.OfType<ZeldaCharacterAiBase>().Where(a=>a.isActiveAndEnabled))
                Check(ai.GetComponent<VisualStyleLabVisionStyle>()!=null,"Active AI retains display style "+ai.name);
            var activeActors=ZeldaRuntimeRegistry.Movers.Where(m=>m&&m.gameObject.scene==scene&&m.GetComponent<ZeldaCharacterData>()!=null&&
                !m.GetComponent<ZeldaCharacterData>().IsGhostLike&&!(m.GetComponent<ZeldaCharacterData>() is BehemothZeldaCharacterData)).ToArray();
            foreach(var actor in activeActors)Check(actor.GetComponent<LevelContactShadow>()!=null,"Physical actor contact "+actor.name);
            var spawn=Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem");
            Check(spawn!=null,"Runtime pickup resource");
            var item=Object.Instantiate(spawn);Check(item.GetComponent<LevelContactShadow>()!=null,"Dropped/spawned pickup styled on Awake");Object.Destroy(item);
            yield return null;
            var current=ZeldaRuntimeRegistry.GetControlledMover();
            var candidate=activeActors.Where(m=>m.gameObject.activeInHierarchy).OrderBy(m=>current?(m.transform.position-current.transform.position).sqrMagnitude:0).FirstOrDefault();
            if(candidate)
            {
                if(current&&current!=candidate){current.enabled=false;current.gameObject.SetActive(false);}
                candidate.ReceiveControl(Vector2.down);ZeldaRuntimeRegistry.ClaimControlledMover(candidate);
                ActiveZeldaCharacterDataSource.Instance.Refresh();
                Camera.main.GetComponent<CameraFollowActiveZeldaMover>().PrepareLoadedSceneView();
                Camera.main.GetComponent<CameraVisionObjectStreaming>().PrepareSoulTransferView();
                float settled=Time.realtimeSinceStartup+.6f;while(Time.realtimeSinceStartup<settled)yield return null;
                var shadow=candidate.transform.Find("Terminal Contact Shadow");
                Check(shadow&&shadow.GetComponent<MeshRenderer>().enabled,"Possessed body shadow remains visible");
                var contact=candidate.GetComponent<LevelContactShadow>();contact.enabled=false;
                Check(!shadow.GetComponent<MeshRenderer>().enabled,"Disabled contact cleans up");
                contact.enabled=true;
                settled=Time.realtimeSinceStartup+.3f;while(Time.realtimeSinceStartup<settled)yield return null;
                Check(shadow.GetComponent<MeshRenderer>().enabled,"Contact returns after enable");
                var victim=all.OfType<ZeldaCharacterAiBase>().FirstOrDefault(a=>a.gameObject!=candidate.gameObject&&a.gameObject.activeInHierarchy);
                if(victim)
                {
                    victim.gameObject.SetActive(false);yield return null;victim.gameObject.SetActive(true);yield return null;
                    Check(victim.GetComponent<VisualStyleLabVisionStyle>()!=null,"Vision survives streaming reactivation");
                }
            }
            foreach(var signal in all.OfType<VisualStyleLabMechanismSignal>())
                Check(signal.GetComponent<Collider2D>()!=null,"Indicator is on original mechanism, not gameplay replacement");
            foreach (var step in LevelPropFloorSmoke.VerifyScene(scene)) yield return step;
            var capture=typeof(VisualStyleLabSmoke).GetMethod("Capture",BindingFlags.NonPublic|BindingFlags.Static);
            capture.Invoke(null,new object[]{"Rollout-"+scene.name});
            Debug.Log("LEVEL_VISUAL_SCENE_PASS "+scene.name+" activeActors="+activeActors.Length+" checks="+checks);
        }
    }
}
#endif
