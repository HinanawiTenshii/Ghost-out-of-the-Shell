using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class TemporaryEffectAreaSmoke
{
    const string Pending="TemporaryEffectAreaSmoke.Pending";
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    static IEnumerator test; static int checks,lastFrame=-1;
    static TemporaryEffectAreaSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||lastFrame==Time.frameCount)return; lastFrame=Time.frameCount;
        try{
            if(SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);test=Test();}
            if(test!=null&&!test.MoveNext()){test=null;Debug.Log("TEMPORARY_EFFECT_AREA_PASS checks="+checks);EditorApplication.Exit(0);}
        }catch(Exception e){test=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static FieldInfo Field(object target,string name){for(var type=target.GetType();type!=null;type=type.BaseType){var f=type.GetField(name,Flags);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object target,string name)=>(T)Field(target,name).GetValue(target);
    static void Set(object target,string name,object value)=>Field(target,name).SetValue(target,value);
    static object Call(object target,string name,params object[] args)=>target.GetType().GetMethod(name,Flags).Invoke(target,args);
    static void Health(ZeldaCharacterData character,int value){var state=character.CaptureRuntimeState();state.health=6;state.currentHealth=value;character.ApplyRuntimeState(state);}
    static ZeldaCharacterData Actor(string name,Vector2 position,bool ghost=false)
    {
        var root=new GameObject(name);root.transform.position=position;
        root.AddComponent<BoxCollider2D>().size=Vector2.one*.25f;
        var data=ghost?(ZeldaCharacterData)root.AddComponent<GhostZeldaCharacterData>():root.AddComponent<ZeldaCharacterData>();
        Health(data,1);return data;
    }
    static void Step(TemporaryCharacterEffectArea area,int count){for(int i=0;i<count;i++)Call(area,"TickEffects",.02f);}
    static IEnumerator Test()
    {
        while(DocumentReader.IsInputBlocked)yield return null;
        var scene=SceneManager.CreateScene("Temporary Area Smoke");SceneManager.SetActiveScene(scene);Time.timeScale=0f;
        var region=new GameObject("Healing Trigger"); var box=region.AddComponent<BoxCollider2D>();box.size=new Vector2(3,2);box.isTrigger=true;
        var area=region.AddComponent<TemporaryCharacterEffectArea>();
        Check(region.GetComponent<CameraVisionStreamingExempt>()!=null,"Functional zone remains available to active offscreen NPCs without waking inactive actors");
        var actor=Actor("Player",Vector2.zero);var mover=actor.gameObject.AddComponent<ZeldaFourWayMover>();ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var source=new GameObject("Data Source").AddComponent<ActiveZeldaCharacterDataSource>();
        var hudRoot=new GameObject("Healing HUD");hudRoot.SetActive(false);var hud=hudRoot.AddComponent<ZeldaHealthHeartsUI>();
        Set(hud,"permissionLabelFont",Resources.GetBuiltinResource<Font>("Arial.ttf"));hudRoot.SetActive(true);source.Refresh();
        var npc=Actor("NPC",new Vector2(.7f,0));
        var second=new GameObject("Second body collider");second.transform.SetParent(npc.transform,false);second.AddComponent<BoxCollider2D>().size=Vector2.one*.2f;
        var ghost=Actor("Ghost",new Vector2(-.6f,0),true);var dead=Actor("Dead",new Vector2(0,.5f));Health(dead,0);
        var foreign=Actor("Foreign scene",new Vector2(0,-.5f));var otherScene=SceneManager.CreateScene("Other Area Scene");SceneManager.MoveGameObjectToScene(foreign.gameObject,otherScene);
        int entered=0,exited=0,ticks=0;Get<TemporaryCharacterEffectArea.CharacterEvent>(area,"onCharacterEntered").AddListener(_=>entered++);
        Get<TemporaryCharacterEffectArea.CharacterEvent>(area,"onCharacterExited").AddListener(_=>exited++);
        Get<TemporaryCharacterEffectArea.CharacterEvent>(area,"onEffectTick").AddListener(_=>ticks++);
        Step(area,14);Check(actor.CurrentHealth==1&&npc.CurrentHealth==1,"No healing before 0.3s");
        Check(area.OccupantCount==2&&entered==2,"Deduplicate colliders, exclude ghost/dead/other-scene actors");
        Set(hud,"healthFlashTimer",0f);Set(hud,"healthShakeTimer",0f);
        Step(area,1);Check(actor.CurrentHealth==2&&npc.CurrentHealth==2&&ticks==2,"Exactly one health at 0.3 seconds per character");
        Check(Get<float>(hud,"healthFlashTimer")>.2f&&Get<float>(hud,"healthShakeTimer")==0f,"Real HUD flashes on healing without damage shake");
        Check(Get<float>(actor,"damageFeedbackTimer")==0f,"Recovery does not simulate damage feedback");
        Step(area,60);Check(actor.CurrentHealth==6&&npc.CurrentHealth==6,"Repeated recovery reaches maximum");
        Set(hud,"healthFlashTimer",0f);Step(area,30);
        Check(actor.CurrentHealth==6&&Get<float>(hud,"healthFlashTimer")==0f,"Full health neither overflows nor repeatedly flashes");
        Check(actor.RestoreHealth(int.MaxValue)==0&&ghost.RestoreHealth(1)==0&&dead.RestoreHealth(1)==0,"Full, ghost and dead restoration rejected");
        Health(actor,1);Check(actor.RestoreHealth(int.MaxValue)==5&&actor.CurrentHealth==6,"Large heal safely caps without integer overflow");
        Health(actor,1);Check(actor.RestoreHealth(-1)==0&&actor.RestoreHealth(0)==0,"Invalid heal is inert");
        actor.transform.position=Vector2.right*20;Step(area,1);int prior=actor.CurrentHealth;Step(area,30);
        Check(actor.CurrentHealth==prior&&area.OccupantCount==1&&exited==1,"Teleport/exit immediately removes temporary effect");
        actor.transform.position=Vector2.zero;Step(area,14);Check(actor.CurrentHealth==1,"Reentry starts a fresh duration");Step(area,1);Check(actor.CurrentHealth==2,"Reentry heals at fresh 0.3s");
        actor.gameObject.SetActive(false);Step(area,1);Check(area.OccupantCount==1,"Unloaded character is removed even without trigger exit");
        actor.gameObject.SetActive(true);Health(actor,1);area.enabled=false;Check(area.OccupantCount==0,"Disabled area removes all effects");
        area.enabled=true;Step(area,14);Check(actor.CurrentHealth==1,"Reenabled area does not carry old tick progress");Step(area,1);
        Check(actor.CurrentHealth==2,"Area resumes on reenable");
        box.enabled=false;Step(area,1);Check(area.OccupantCount==0,"Disabled collider removes occupants");box.enabled=true;
        box.isTrigger=false;Step(area,15);Check(actor.CurrentHealth==2&&!box.isTrigger,"Solid colliders are not silently converted or used");box.isTrigger=true;
        Get<TemporaryCharacterEffectArea.CharacterEvent>(area,"onCharacterEntered").AddListener(_=>area.enabled=false);
        Step(area,1);Check(!area.enabled&&area.OccupantCount==0,"Extension event may disable its own area safely");
        Get<TemporaryCharacterEffectArea.CharacterEvent>(area,"onCharacterEntered").RemoveAllListeners();area.enabled=true;
        Set(area,"restoreHealth",false);Health(actor,1);Step(area,15);Check(actor.CurrentHealth==1&&ticks>0,"Recovery can be disabled while retaining extension hooks");Set(area,"restoreHealth",true);
        Call(area,"FixedUpdate");Check(actor.CurrentHealth==1,"Game pause prevents actual FixedUpdate healing");
        var saved=JsonUtility.ToJson(actor.CaptureRuntimeState());Health(actor,5);actor.ApplyRuntimeState(JsonUtility.FromJson<ZeldaCharacterData.RuntimeState>(saved));Check(actor.CurrentHealth==1,"Health remains compatible with existing save state");
        // Non-box geometry: do not heal characters merely inside the collider AABB.
        var roundRoot=new GameObject("Round healing trigger");roundRoot.transform.position=new Vector3(10,10,0);
        var circle=roundRoot.AddComponent<CircleCollider2D>();circle.radius=1;circle.isTrigger=true;var round=roundRoot.AddComponent<TemporaryCharacterEffectArea>();
        actor.transform.position=new Vector3(10.9f,10.9f,0);Health(actor,1);Step(round,15);Check(actor.CurrentHealth==1,"Circle corners outside shape do not heal");
        actor.transform.position=roundRoot.transform.position;Step(round,15);Check(actor.CurrentHealth==2,"Circular trigger interior heals");
        round.enabled=false;
        var childRoot=new GameObject("Child trigger");childRoot.transform.SetParent(region.transform,false);childRoot.transform.localPosition=new Vector3(5,0,0);
        var childBox=childRoot.AddComponent<BoxCollider2D>();childBox.isTrigger=true;childBox.size=Vector2.one;
        Set(area,"areaCollider",childBox);Health(actor,1);actor.transform.position=new Vector3(5,0,0);Step(area,15);
        Check(actor.CurrentHealth==2&&area.AreaCollider==childBox,"Explicit child trigger is supported without changing the parent's collider");
        Set(area,"areaCollider",box);UnityEngine.Object.Destroy(childRoot);
        // Use the normal Update path once to create the area bubbles, then test deterministic frames.
        actor.transform.position=Vector3.one*100;npc.transform.position=Vector3.one*100;
        ghost.gameObject.SetActive(false);dead.gameObject.SetActive(false);foreign.gameObject.SetActive(false);
        hudRoot.SetActive(false);Time.timeScale=1f;yield return null;yield return null;Time.timeScale=0f;
        var fx=Get<object>(area,"bubbles");Check(fx!=null,"Attaching one component creates cauldron-style bubbles automatically");
        Action<float> bubbleStep=dt=>Call(fx,"Tick",box,dt,new Color(.18f,.85f,.49f),new Vector2(.06875f,.10625f),new Vector2(.8f,1.35f),.3f,.22f,0,-2);
        bubbleStep(.1f);Check(Get<Mesh>(fx,"mesh").vertexCount>0,"Region emits growing bubbles");
        Check(Get<MeshRenderer>(fx,"renderer").sortingOrder==-2,"Normal world sorting below characters and vision mask");
        var bubbles=Get<IList>(fx,"bubbles");Check(bubbles.Count<=8,"At most eight active bubbles");
        var ringVertices=Get<List<Vector3>>(fx,"vertices");
        region.transform.rotation=Quaternion.Euler(0,0,32);region.transform.localScale=new Vector3(1.6f,.7f,1);
        Physics2D.SyncTransforms();
        ringVertices.Clear();Call(fx,"Disc",new Vector3(0,0,-.006f),.1f,Color.white);
        foreach(var vertex in ringVertices)Check(Mathf.Abs(Vector3.Distance(region.transform.TransformPoint(vertex),new Vector3(0,0,-.006f))-.1f)<.0001f,"Bubble size stays in world units after nonuniform rotation/scale");
        Check(!(bool)Call(fx,"HasRoom",(Vector2)box.bounds.max,.2f),"No bubble protrusion at rotated rectangle corner");
        region.transform.rotation=Quaternion.identity;region.transform.localScale=Vector3.one;Physics2D.SyncTransforms();
        var polygonRoot=new GameObject("Concave trigger");var polygon=polygonRoot.AddComponent<PolygonCollider2D>();polygon.isTrigger=true;
        polygon.points=new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,0),new Vector2(0,0),new Vector2(0,1),new Vector2(-1,1)};
        Physics2D.SyncTransforms();Set(fx,"source",polygon);
        Check((bool)Call(fx,"HasRoom",new Vector2(-.5f,.5f),.1f),"Bubble fits concave polygon interior");
        Check(!(bool)Call(fx,"HasRoom",new Vector2(.5f,.5f),.1f)&&!(bool)Call(fx,"HasRoom",new Vector2(-.02f,.1f),.1f),"Concave cutout and nearby edges reject protruding bubbles");
        Set(fx,"source",box);UnityEngine.Object.Destroy(polygonRoot);
        var camera=new GameObject("Bubble Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.3f;
        camera.transform.position=new Vector3(0,0,-10);camera.backgroundColor=new Color(.04f,.15f,.12f);camera.clearFlags=CameraClearFlags.SolidColor;
        var target=new RenderTexture(720,480,24);camera.targetTexture=target;camera.aspect=1.5f;
        var image=new Texture2D(720,480,TextureFormat.RGBA32,false);string frames=Application.dataPath+"/../effect-area-frames";Directory.CreateDirectory(frames);
        Call(fx,"Clear");bool sawPop=false,sawGrow=false;
        for(int i=0;i<60;i++)
        {
            bubbleStep(.05f);bubbles=Get<IList>(fx,"bubbles");
            foreach(var b in bubbles){float age=Get<float>(b,"age"),growth=Get<float>(b,"growTime");sawPop|=age>=growth;sawGrow|=age<growth;}
            Check(bubbles.Count<=8&&Get<Mesh>(fx,"mesh").vertexCount<=400,"Continuous animation keeps mesh and bubble budget");
            camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,720,480),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(frames,"area-"+i.ToString("000")+".png"),image.EncodeToPNG());
            if(i==35)File.WriteAllBytes(Application.dataPath+"/../TemporaryEffectArea-preview.png",image.EncodeToPNG());
        }
        Check(sawGrow&&sawPop,"Cauldron growth and five-way popping both occur");
        var oldMesh=Get<Mesh>(fx,"mesh");var oldMaterial=Get<Material>(fx,"material");int oldVertices=oldMesh.vertexCount;
        Call(area,"Update");Check(oldMesh.vertexCount==oldVertices,"Pause freezes bubble geometry");
        box.enabled=false;Call(area,"Update");Check(oldMesh.vertexCount==0,"Disabling trigger clears bubble visual");box.enabled=true;
        area.enabled=false;yield return null;yield return null;
        Check(oldMesh==null&&oldMaterial==null,"Disable releases bubble mesh/material");
        camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(camera.gameObject);
        UnityEngine.Object.Destroy(region);UnityEngine.Object.Destroy(roundRoot);UnityEngine.Object.Destroy(actor.gameObject);UnityEngine.Object.Destroy(npc.gameObject);
        UnityEngine.Object.Destroy(ghost.gameObject);UnityEngine.Object.Destroy(dead.gameObject);UnityEngine.Object.Destroy(foreign.gameObject);UnityEngine.Object.Destroy(hudRoot);UnityEngine.Object.Destroy(source.gameObject);
        Time.timeScale=1f;yield return null;
    }
}
