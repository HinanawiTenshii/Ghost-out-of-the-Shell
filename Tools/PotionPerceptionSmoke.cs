using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run in the isolated full-script Unity validation project, never an authored scene.
[InitializeOnLoad]
public static class PotionPerceptionSmoke
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
    const string Pending="PotionPerceptionSmoke.Pending";
    static IEnumerator test; static int checks,lastFrame=-1;
    static PotionPerceptionSmoke(){EditorApplication.update+=Tick;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Tick()
    {
        if(!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);test=Test();}
            if(test!=null&&!test.MoveNext()){test=null;Debug.Log("POTION_PERCEPTION_PASS checks="+checks);EditorApplication.Exit(0);}
        }
        catch(Exception e){test=null;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool ok,string message){checks++;if(!ok)throw new Exception(message);}
    static FieldInfo Field(object obj,string name)
    {for(var t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,Flags);if(f!=null)return f;}throw new Exception(name);}
    static T Get<T>(object obj,string name)=>(T)Field(obj,name).GetValue(obj);
    static void Set(object obj,string name,object value)=>Field(obj,name).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)
    {for(var t=obj.GetType();t!=null;t=t.BaseType){var m=t.GetMethod(name,Flags|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(obj,args);}throw new Exception(name);}
    static float Property(object obj,string name)=>(float)typeof(ZeldaCharacterAiBase).GetProperty(name,Flags).GetValue(obj);
    static ZeldaCharacterData Actor(string name,Vector2 position,bool controlled=false,Type aiType=null)
    {
        var root=new GameObject(name);root.SetActive(false);root.transform.position=position;
        var data=root.AddComponent<ZeldaCharacterData>();
        var mover=root.AddComponent<ZeldaFourWayMover>();mover.enabled=controlled;
        root.AddComponent<BoxCollider2D>().size=Vector2.one*.3f;
        if(aiType!=null)root.AddComponent(aiType);
        root.SetActive(true);
        var state=data.CaptureRuntimeState();state.health=10;state.currentHealth=10;data.ApplyRuntimeState(state);
        if(controlled)ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        return data;
    }
    static bool See(ZeldaCharacterAiBase ai,ZeldaFourWayMover mover)=>(bool)Call(ai,"CanSeePlayer",mover);
    static void Expire(ZeldaCharacterData actor,AlchemyPotionKind kind)
    {var buff=actor.GetComponent<CharacterPotionBuffs>();var state=buff.CaptureRuntimeState();state.remaining[(int)kind]=0;buff.ApplyRuntimeState(state);}
    static IEnumerator Test()
    {
        var scene=SceneManager.CreateScene("Potion Perception Test");SceneManager.SetActiveScene(scene);Time.timeScale=0;
        var player=Actor("Potion player",new Vector2(0,3),true);
        var mover=player.GetComponent<ZeldaFourWayMover>();
        var guardData=Actor("Guard",Vector2.zero,false,typeof(ZeldaCharacterAiBase));
        var guard=guardData.GetComponent<ZeldaCharacterAiBase>();
        Set(guard,"facingDirection",Vector2.down);Physics2D.SyncTransforms();
        Check(!See(guard,mover),"Ordinary NPC has a rear blind spot");
        CharacterPotionBuffs.TryApply(guardData,AlchemyPotionKind.Sight);
        Check(See(guard,mover),"NPC Sight sees behind itself");
        Check(Mathf.Abs(Property(guard,"EffectiveVisionRadius")-5f)<.001f,"NPC Sight radius is exactly 125 percent");
        mover.transform.position=new Vector3(0,4.9f,0);Physics2D.SyncTransforms();Check(See(guard,mover),"NPC Sight reaches expanded radius");
        mover.transform.position=new Vector3(0,5.1f,0);Physics2D.SyncTransforms();Check(!See(guard,mover),"NPC Sight does not exceed 125 percent");
        guardData.GetComponent<ZeldaFourWayMover>().enabled=true;
        Check(Property(guard,"EffectiveVisionAngle")==90f&&Property(guard,"EffectiveVisionRadius")==4f,"AI omnidirectional modifier removed immediately when body is player controlled");
        guardData.GetComponent<ZeldaFourWayMover>().enabled=false;guard.enabled=true;ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        // The isolated smoke project intentionally uses Unity's default TagManager.
        // Provide an explicit fallback wall layer/mask instead of assuming project tags.
        int wallLayer=LayerMask.NameToLayer("Blocks");if(wallLayer<0)wallLayer=8;
        var wall=new GameObject("Blocks wall");wall.layer=wallLayer;wall.transform.position=new Vector3(0,1.5f,0);wall.AddComponent<BoxCollider2D>().size=new Vector2(10,.2f);
        mover.transform.position=new Vector3(0,3,0);Physics2D.SyncTransforms();
        Check(!See(guard,mover),"AI Sight never sees through walls");
        wall.SetActive(false);Expire(guardData,AlchemyPotionKind.Sight);Set(guard,"facingDirection",Vector2.up);Physics2D.SyncTransforms();
        Check(See(guard,mover),"Unbuffed guard sees ordinary target in front");
        CharacterPotionBuffs.TryApply(player,AlchemyPotionKind.Veil);
        Check(!See(guard,mover),"Veil prevents ordinary visual detection");
        Set(guard,"targetMover",mover);Set(guard,"currentState",ZeldaAiState.Hostile);Set(guard,"hostileUntilTargetDeath",true);
        Vector2 remembered=new Vector2(.6f,1f);Set(guard,"lastKnownTargetPosition",remembered);
        Call(guard,"UpdateTargetAndState",.02f);
        Check(guard.CurrentState==ZeldaAiState.Search,"Veil makes existing hostility search, not idle");
        Check(Get<Vector2>(guard,"investigationIncidentPosition")==remembered,"Search goes to last observed position rather than live invisible position");
        var automatonData=Actor("Sentinel",new Vector2(2,3),false,typeof(AutomatonCharacterAi));
        var automaton=automatonData.GetComponent<AutomatonCharacterAi>();
        Check(See(automaton,mover),"Mechanical sentinel retains special Veil detection");
        var skeletonData=Actor("Skeleton AI",new Vector2(0,5),false,typeof(SkeletonCharacterAi));
        var skeleton=skeletonData.GetComponent<SkeletonCharacterAi>();Set(skeleton,"facingDirection",Vector2.down);
        Check(See(skeleton,mover),"Skeleton retains special Veil detection");
        CharacterPotionBuffs.NotifyAction(player);Check(See(guard,mover),"Attack/item action immediately removes Veil detection suppression");

        CharacterPotionBuffs.TryApply(player,AlchemyPotionKind.Sight);
        player.transform.position=Vector3.zero;guard.transform.position=new Vector3(0,3,0);wall.SetActive(true);
        foreach(var existing in UnityEngine.Object.FindObjectsOfType<Camera>())existing.tag="Untagged";
        var cameraRoot=new GameObject("Potion test camera");cameraRoot.tag="MainCamera";cameraRoot.transform.position=new Vector3(0,0,-10);
        var camera=cameraRoot.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6;
        var vision=cameraRoot.AddComponent<CameraCircularVision>();Set(vision,"blockLayers",(LayerMask)(1<<wallLayer));Physics2D.SyncTransforms();Call(vision,"UpdateVision",true);
        Check(Mathf.Abs(vision.VisionRadius-player.GetPlayerVisionRadius(scene.path)*1.25f)<.001f,"Player Sight receives only 25 percent radius boost");
        Check(!vision.IsWorldPositionVisible(guard.transform.position,guard.transform),"Sight does not alter discovery/map visibility APIs");
        var helper=player.GetComponent<PotionPerceptionEffects>();Call(helper,"LateUpdate");
        var mesh=Get<Mesh>(helper,"outlineMesh");
        Check(mesh!=null&&mesh.vertexCount>=16,"Player Sight creates unfilled wall-hidden target contours");
        var outline=Get<GameObject>(helper,"outlineRoot");
        Check(outline.layer==LayerMask.NameToLayer("UI")&&outline.GetComponent<MeshRenderer>().sortingOrder==30010,"Contours use existing above-wall UI composite, below HUD");
        Check(outline.GetComponentsInChildren<Collider2D>().Length==0,"Visual contours create no physical colliders");
        int physicsLayer=guard.gameObject.layer;Check(physicsLayer==0,"Target physics/render layer remains unchanged");
        helper.enabled=false;yield return null;
        Check(mesh==null&&outline==null,"Disabling effect releases generated mesh and root");helper.enabled=true;
        Expire(player,AlchemyPotionKind.Sight);Call(helper,"LateUpdate");Call(vision,"UpdateVision",true);
        Check(Get<Mesh>(helper,"outlineMesh")==null&&Mathf.Abs(vision.VisionRadius-player.GetPlayerVisionRadius(scene.path))<.001f,"Expiry removes contours and restores original radius");

        wall.SetActive(false);guard.transform.position=new Vector3(0,2,0);Set(guard,"currentState",ZeldaAiState.Idle);
        var hostileData=Actor("Already hostile",new Vector2(4,0),false,typeof(ZeldaCharacterAiBase));var hostile=hostileData.GetComponent<ZeldaCharacterAiBase>();
        Set(hostile,"currentState",ZeldaAiState.Hostile);Set(hostile,"targetMover",mover);
        Set(skeleton,"currentState",ZeldaAiState.Idle);Set(automaton,"currentState",ZeldaAiState.Idle);
        Call(helper,"EmitClamor");
        Check(guard.CurrentState==ZeldaAiState.Search,"Clamor sends ordinary idle listener to investigate");
        Check(Get<Vector2>(guard,"investigationIncidentPosition")==Vector2.zero,"Clamor originates at drinker's current position");
        Check(hostile.CurrentState==ZeldaAiState.Hostile&&Get<ZeldaFourWayMover>(hostile,"targetMover")==mover,"Clamor preserves active hostile state and target");
        Check(skeleton.CurrentState==ZeldaAiState.Idle&&automaton.CurrentState==ZeldaAiState.Idle,"Clamor leaves special skeleton/sentinel AI untouched");
        CharacterPotionBuffs.TryApply(player,AlchemyPotionKind.Clamor);Set(helper,"nextNoiseTime",Time.time-1f);Call(helper,"UpdateClamor");
        Check(Get<float>(helper,"nextNoiseTime")<Time.time,"Paused Clamor does not emit or advance cadence");
        CharacterPotionBuffs.TryApply(guardData,AlchemyPotionKind.Might);
        var roarRoot=new GameObject("Roar test");roarRoot.AddComponent<FearRoarArea>().Initialize(player,1);
        Check(!guard.IsStunned,"Might blocks fear roar only");
        guard.StunForDuration(1f);Check(guard.IsStunned,"Might does not block unrelated stun");
        UnityEngine.Object.Destroy(roarRoot);helper.enabled=false;yield return null;
        Check(Get<GameObject>(helper,"retentionRoot")==null,"Noise streaming retention is released on disable");
        Time.timeScale=1f;
    }
}
