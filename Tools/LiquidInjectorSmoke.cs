using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in the isolated full-script Unity smoke project; never saves authored scenes.
[InitializeOnLoad]
public static class LiquidInjectorSmoke
{
    const string Pending="LiquidInjectorSmoke.Pending";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static IEnumerator test; static int checks,lastFrame=-1;
    static LiquidInjectorSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try{
            if(SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);test=Test();}
            if(test!=null&&!test.MoveNext()){test=null;Debug.Log("LIQUID_INJECTOR_PASS checks="+checks);EditorApplication.Exit(0);}
        }catch(Exception e){test=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string label){checks++;if(!ok)throw new Exception(label);}
    static T Get<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,Flags).SetValue(o,value);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags).Invoke(o,args);
    static TemporaryCharacterEffectArea Area(string name,Vector2 pos,Color tint)
    {
        var root=new GameObject(name);root.transform.position=pos;
        var box=root.AddComponent<BoxCollider2D>();box.isTrigger=true;box.size=Vector2.one*2;
        var area=root.AddComponent<TemporaryCharacterEffectArea>();Set(area,"animateBubbles",false);
        var settings=area.CaptureEffectSettings();settings.bubbleColor=tint;area.ApplyEffectSettings(settings);return area;
    }
    static void Ready(LiquidInjector injector,ZeldaFourWayMover mover){mover.EndItemAttack();Set(injector,"nextInteractionTime",0f);}
    static void Health(ZeldaCharacterData data,int current){var s=data.CaptureRuntimeState();s.health=8;s.currentHealth=current;data.ApplyRuntimeState(s);}
    static void Step(TemporaryCharacterEffectArea area,int count){for(int i=0;i<count;i++)Call(area,"TickEffects",.02f);}
    static IEnumerator Test()
    {
        while(DocumentReader.IsInputBlocked)yield return null;
        var scene=SceneManager.CreateScene("Injector Test");SceneManager.SetActiveScene(scene);Time.timeScale=1f;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/LiquidInjector.prefab");
        Check(prefab!=null,"Production prefab imports");
        var root=UnityEngine.Object.Instantiate(prefab);root.name="Injector";
        var injector=root.GetComponent<LiquidInjector>();var visual=root.GetComponent<LiquidInjectorVisual>();
        Check(injector!=null&&visual!=null&&root.GetComponent<CameraVisionStreamingExempt>()!=null,"Prefab wiring");
        Check(!root.GetComponent<BoxCollider2D>().isTrigger&&root.GetComponent<BoxCollider2D>().size.x>1f,"Solid body footprint");
        Check(!injector.ConditionSatisfied&&injector.CurrentEffectColor==new Color(.18f,.22f,.24f,1),"Default locked with dark-gray indicator");
        Check(Get<Font>(injector,"promptFont")!=null,"Project prompt font linked");
        var actor=new GameObject("Operator");actor.transform.position=new Vector3(0,-1.1f,0);
        actor.AddComponent<BoxCollider2D>().size=Vector2.one*.25f;
        var data=actor.AddComponent<ZeldaCharacterData>();var mover=actor.AddComponent<ZeldaFourWayMover>();
        Health(data,1);ZeldaRuntimeRegistry.ClaimControlledMover(mover);mover.ReceiveControl(Vector2.up);
        var a=Area("Area A",new Vector2(3,0),Color.green);var b=Area("Area B",new Vector2(6,0),Color.blue);
        var inactive=Area("Inactive Area",new Vector2(9,0),Color.red);inactive.gameObject.SetActive(false);
        var foreign=Area("Foreign Area",new Vector2(12,0),Color.magenta);
        var elsewhere=SceneManager.CreateScene("Other Injector Scene");SceneManager.MoveGameObjectToScene(foreign.gameObject,elsewhere);
        Set(injector,"linkedAreas",new[]{a,b,inactive,foreign,a,null});
        int events=0;bool reentered=false;
        Get<LiquidInjector.InjectionEvent>(injector,"onInjected").AddListener(who=>{events++;Check(who==data,"Callback carries operator");reentered|=injector.TryInject();});
        Check(!injector.TryInject()&&!mover.IsAttacking&&events==0,"Locked interaction has no mutation/gesture/event");
        injector.SetConditionSatisfied(true);
        Check(a.CaptureEffectSettings().healthPerTick==1&&events==0,"Unlock does not automatically inject");
        var settings=TemporaryCharacterEffectArea.EffectSettings.Healing;settings.healthPerTick=2;settings.tickInterval=.4f;settings.bubbleColor=Color.cyan;
        injector.ConfigureInjection(settings,Color.red);
        Check(a.CaptureEffectSettings().healthPerTick==1&&injector.CurrentEffectColor!=Color.red,"Configuration is pending until interaction");
        // Check E target arbitration, then invoke its callback. The arbiter also
        // requires native Input.GetKeyDown, which is not synthesized in batch mode.
        ZeldaInteractionArbiter.OfferInteraction(injector,mover,KeyCode.E,root.transform.position,_=>{});
        ZeldaInteractionArbiter.Submit(injector,mover,KeyCode.E,root.transform.position,injector.Inject);
        typeof(ZeldaInteractionArbiter).GetMethod("ResolveCurrentFrame",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,null);
        Check(ZeldaInteractionArbiter.IsSelected(injector,mover,KeyCode.E),"Injector participates in shared E target arbitration");
        injector.Inject();
        Check(events==1&&!reentered,"Interaction callback routes one injection and refuses reentrancy");
        Check(mover.IsUsingItemAttack,"Normal raised-hand item gesture reused");
        foreach(var zone in new[]{a,b,inactive})Check(zone.CaptureEffectSettings().healthPerTick==2&&zone.TickInterval==.4f,"Multiple areas receive effect");
        Check(Get<int>(a,"effectRevision")==2,"Duplicate target not applied twice");
        Check(!inactive.gameObject.activeSelf,"Inactive region not force-enabled");
        Check(foreign.CaptureEffectSettings().healthPerTick==1,"Cross-scene links ignored");
        Check(a.CaptureEffectSettings().bubbleColor==Color.green&&b.CaptureEffectSettings().bubbleColor==Color.blue,"Default preserves per-area bubble colors");
        Check(injector.CurrentEffectColor==Color.red&&!injector.TryInject(),"Successful use changes indicator and blocks immediate repeat");
        Ready(injector,mover);Time.timeScale=0f;Check(!injector.TryInject(),"Paused interaction blocked");Time.timeScale=1;
        actor.transform.position=Vector3.one*100;Check(!injector.TryInject(),"Distant actor blocked");actor.transform.position=new Vector3(0,-1.1f);
        var ghost=new GameObject("Ghost");ghost.transform.position=actor.transform.position;ghost.AddComponent<GhostZeldaCharacterData>();
        var ghostMover=ghost.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.ClaimControlledMover(ghostMover);
        Check(!injector.TryInject(),"Ghost cannot inject");ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        Health(data,0);Check(!injector.TryInject(),"Dead body cannot inject");Health(data,1);
        SceneManager.MoveGameObjectToScene(actor,elsewhere);Check(!injector.TryInject(),"Operator in foreign scene blocked");SceneManager.MoveGameObjectToScene(actor,scene);
        injector.enabled=false;Check(!injector.TryInject(),"Disabled injector blocked");injector.enabled=true;
        Set(injector,"linkedAreas",new[]{foreign,(TemporaryCharacterEffectArea)null});Check(!injector.TryInject()&&!mover.IsAttacking,"Empty valid target set does not gesture");
        Set(injector,"linkedAreas",new[]{a,b,inactive});injector.ConfigureInjection(settings,Color.green,true);
        Check(injector.TryInject(),"Explicit bubble recolor injection");
        Check(a.CaptureEffectSettings().bubbleColor==Color.cyan&&b.CaptureEffectSettings().bubbleColor==Color.cyan,"Recolor applies consistently");
        Ready(injector,mover);Time.timeScale=0f;actor.transform.position=a.transform.position;Health(data,1);
        Step(a,19);Check(data.CurrentHealth==1,"New interval waits full 0.4 seconds");Step(a,1);Check(data.CurrentHealth==3,"New amount is used by existing healing system");
        Step(a,19);settings.healthPerTick=1;settings.tickInterval=.3f;a.ApplyEffectSettings(settings);Step(a,1);
        Check(data.CurrentHealth==3,"Replacing effect resets leftover timer");Step(a,14);Check(data.CurrentHealth==4,"Current occupant receives changed effect without reentry");
        settings.restoreHealth=false;a.ApplyEffectSettings(settings);Step(a,30);Check(data.CurrentHealth==4,"No-healing configuration is supported");
        // Callback replacing an effect must not execute the old catch-up tick batch.
        settings.restoreHealth=true;a.ApplyEffectSettings(settings);Health(data,1);int ticks=0;
        Get<TemporaryCharacterEffectArea.CharacterEvent>(a,"onEffectTick").AddListener(_=>{ticks++;var off=settings;off.restoreHealth=false;a.ApplyEffectSettings(off);});
        Call(a,"TickEffects",1.2f);Check(ticks==1&&data.CurrentHealth==2,"Effect replacement during callbacks cancels old pending ticks");
        Get<TemporaryCharacterEffectArea.CharacterEvent>(a,"onEffectTick").RemoveAllListeners();
        var invalid=settings;invalid.healthPerTick=0;invalid.tickInterval=float.NaN;a.ApplyEffectSettings(invalid);
        Check(a.CaptureEffectSettings().healthPerTick==1&&a.TickInterval==.3f,"Invalid numeric configuration sanitized");
        // Real scene capture plus disk JSON round-trip and actual snapshot restore.
        var finalArea=settings;finalArea.restoreHealth=false;finalArea.healthPerTick=3;finalArea.tickInterval=.8f;finalArea.bubbleColor=Color.magenta;a.ApplyEffectSettings(finalArea);
        injector.SetIndicatorColor(new Color(.18f,.22f,.24f));injector.SetConditionSatisfied(false);
        var manager=SceneTravelStateManager.Instance;
        string json=manager.CaptureDiskWorld();
        Check(json.Contains("\"hasLiquidInjector\":true")&&json.Contains("\"hasTemporaryEffectArea\":true"),"Disk saves include both state types");
        var diskType=typeof(SceneTravelStateManager).GetNestedType("DiskWorld",BindingFlags.NonPublic);
        var disk=JsonUtility.FromJson(json,diskType);var scenes=Get<IList>(disk,"scenes");object savedScene=null;
        foreach(var entry in scenes)if(Get<string>(entry,"name")==scene.name)savedScene=entry;
        Check(savedScene!=null,"Disk scene round-trip");
        var snapshotType=typeof(SceneTravelStateManager).GetNestedType("SceneState",BindingFlags.NonPublic);
        var snapshot=Activator.CreateInstance(snapshotType,true);Set(snapshot,"sceneName",scene.name);
        var states=Get<IDictionary>(snapshot,"objects");
        foreach(var obj in Get<IList>(savedScene,"objects"))states.Add(Get<string>(obj,"path"),obj);
        injector.SetConditionSatisfied(true);injector.SetIndicatorColor(Color.yellow);a.ApplyEffectSettings(settings);
        int beforeEvents=events;Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);
        Check(!injector.ConditionSatisfied&&injector.CurrentEffectColor==new Color(.18f,.22f,.24f),"Unlock/indicator restore");
        Check(!a.CaptureEffectSettings().restoreHealth&&a.CaptureEffectSettings().healthPerTick==3&&a.TickInterval==.8f,"Independent area override restored, not overwritten by injector pending settings");
        Check(a.CaptureEffectSettings().bubbleColor==Color.magenta&&events==beforeEvents,"Restore preserves area color without firing injection events");
        Check(!inactive.gameObject.activeSelf,"Inactive target remains inactive after restore");
        // Old snapshots without flags leave new component defaults/configurations intact.
        foreach(DictionaryEntry entry in states){Set(entry.Value,"hasLiquidInjector",false);Set(entry.Value,"hasTemporaryEffectArea",false);}
        a.ApplyEffectSettings(settings);injector.SetIndicatorColor(Color.yellow);Call(manager,"RestoreSceneSnapshot",scene,snapshot,false);
        Check(a.CaptureEffectSettings().restoreHealth&&injector.CurrentEffectColor==Color.yellow,"Old save compatibility");
        // Geometry, color caching, transforms, resource disposal and real GPU preview.
        injector.SetIndicatorColor(new Color(.18f,.22f,.24f));Call(visual,"LateUpdate");
        var mesh=Get<Mesh>(visual,"mesh");var renderer=Get<GameObject>(visual,"generated").GetComponent<MeshRenderer>();
        Check(mesh.vertexCount<256&&root.GetComponentsInChildren<Renderer>().Length==1,"Single small mesh, no extra sprites/lights");
        Check(renderer.sortingOrder==5&&renderer.gameObject.layer==root.layer,"Ordinary world sorting/layer follows scene mask");
        int first=Get<int>(visual,"liquidStart");Check(mesh.colors[first]==injector.CurrentEffectColor,"Liquid window uses configured current color");
        var vertices=mesh.vertices;var triangles=mesh.triangles;
        for(int i=0;i<triangles.Length;i+=3)Check(Vector3.Cross(vertices[triangles[i+1]]-vertices[triangles[i]],vertices[triangles[i+2]]-vertices[triangles[i]]).z<0,"Triangles face overhead camera");
        injector.SetIndicatorColor(Color.red);Call(visual,"LateUpdate");Check(mesh==Get<Mesh>(visual,"mesh")&&mesh.colors[first]==Color.red,"Color changes reuse geometry");
        for(int i=0;i<vertices.Length;i++)Check(vertices[i]==mesh.vertices[i],"Recolor preserves geometry");
        root.transform.localScale=new Vector3(.7f,1.3f,1);root.transform.rotation=Quaternion.Euler(0,0,90);Call(visual,"LateUpdate");
        Check(mesh==Get<Mesh>(visual,"mesh"),"Transforms do not rebuild mesh");root.transform.localScale=Vector3.one;root.transform.rotation=Quaternion.identity;
        injector.SetIndicatorColor(new Color(.18f,.22f,.24f));Call(visual,"LateUpdate");
        actor.SetActive(false);ghost.SetActive(false);Call(injector,"ShowPrompt",false);
        var camera=new GameObject("Injector Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1;
        camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=new Color(.035f,.055f,.13f);camera.clearFlags=CameraClearFlags.SolidColor;
        var target=new RenderTexture(560,560,24);camera.targetTexture=target;camera.aspect=1;
        var image=new Texture2D(560,560,TextureFormat.RGBA32,false);camera.Render();RenderTexture.active=target;
        image.ReadPixels(new Rect(0,0,560,560),0,0);image.Apply();File.WriteAllBytes(Application.dataPath+"/../LiquidInjector-preview.png",image.EncodeToPNG());
        var material=renderer.sharedMaterial;visual.enabled=false;yield return null;yield return null;
        Check(mesh==null&&material==null,"Disable frees generated resources");visual.enabled=true;Call(visual,"LateUpdate");
        Check(Get<Mesh>(visual,"mesh")!=null&&root.GetComponentsInChildren<MeshFilter>().Length==1,"Reenable rebuilds exactly once");
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(camera.gameObject);
        Time.timeScale=1f;yield return null;
    }
}
