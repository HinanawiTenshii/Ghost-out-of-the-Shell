using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SkeletonPatrolSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame=-1,checks;
    static SkeletonPatrolSmoke() { EditorApplication.update+=Tick; }
    static void Check(bool ok,string label) { checks++;if(!ok)throw new Exception(label); }
    static FieldInfo Field(object o,string name)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        { var f=t.GetField(name,F|BindingFlags.DeclaredOnly);if(f!=null)return f; }
        throw new Exception("Missing field "+name);
    }
    static void Set(object o,string name,object value)=>Field(o,name).SetValue(o,value);
    static T Get<T>(object o,string name)=>(T)Field(o,name).GetValue(o);
    static object Call(object o,string name,params object[] args)
    {
        for(Type t=o.GetType();t!=null;t=t.BaseType)
        { var m=t.GetMethod(name,F|BindingFlags.DeclaredOnly);if(m!=null)return m.Invoke(o,args); }
        throw new Exception("Missing method "+name);
    }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Skeleton-level2-Patrol.prefab");
        if(prefab==null)throw new Exception("patrol skeleton asset missing");
        var editor=Editor.CreateEditor(prefab.GetComponent<SkeletonCharacterAi>());
        if(!(editor is ZeldaPatrolCharacterAiEditor))throw new Exception("shared patrol point inspector missing");
        Object.DestroyImmediate(editor);
        SessionState.SetBool(nameof(SkeletonPatrolSmoke),true);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||frame==Time.frameCount)return;
        frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(nameof(SkeletonPatrolSmoke),false))
            { SessionState.SetBool(nameof(SkeletonPatrolSmoke),false);Time.captureFramerate=50;suite=Verify(); }
            if(suite==null||suite.MoveNext())return;
            suite=null;Time.captureFramerate=0;
            Debug.Log("SKELETON_PATROL_PASS checks="+checks);EditorApplication.Exit(0);
        }
        catch(Exception e){suite=null;Time.captureFramerate=0;Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static SkeletonCharacterAi Spawn(string name,Vector2 position)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/"+name+".prefab");
        var go=Object.Instantiate(prefab,position,Quaternion.identity);
        var ai=go.GetComponent<SkeletonCharacterAi>();Set(ai,"enableAwarenessSound",false);
        Set(ai,"visionObstacleLayers",(LayerMask)0);Set(ai,"solidCollisionLayers",(LayerMask)0);
        for(int i=1;i<=4;i++)Set(ai,"waitAtPoint"+i+"Duration",0f);
        return ai;
    }
    static void Position(Component o,Vector2 p)
    { o.transform.position=p;var rb=o.GetComponent<Rigidbody2D>();if(rb)rb.position=p;Physics2D.SyncTransforms(); }
    static IEnumerator Verify()
    {
        var idle=Spawn("Skeleton-level2",new Vector2(-10,0));
        var ai=Spawn("Skeleton-level2-Patrol",Vector2.zero);
        var data=ai.GetComponent<SkeletonZeldaCharacterData>();
        Check(!idle.HasIdlePatrol&&ai.HasIdlePatrol,"original stays idle; new prefab patrols");
        Check(ai.GetComponents<ZeldaCharacterAiBase>().Length==1,"single AI controls body, no competing movement scripts");
        Check(ai is ZeldaPatrolCharacterAi&&ai.IsUniversalThreat&&!ai.SharesHostilityWithOtherNpcs,"route reuse preserves skeleton faction behavior");
        Check(ai.GetComponent<ZeldaFourWayMover>().enabled==false,"NPC prefab is not player controlled");
        Check(data.AttackPower==idle.GetComponent<SkeletonZeldaCharacterData>().AttackPower,"same unarmed stats as current skeleton");
        Check(data.AttackVisualShape==ZeldaAttackVisualShape.BoneFist,"unarmed bone-fist attack retained");
        Check(!data.CanReassemble,"patrol skeleton still needs crystal to revive");
        Call(idle,"TickIdle",.02f);Check(Get<Vector2>(idle,"moveDirection")==Vector2.zero,"idle skeleton has no route movement");
        var points=Get<Vector2[]>(ai,"patrolPoints");
        Check(points.Length==4&&points[0]==Vector2.zero&&points[1]==new Vector2(2,0)&&points[2]==new Vector2(2,2)&&points[3]==new Vector2(0,2),"default four-point square relative to spawn");
        var origin=(Vector2)ai.transform.position;
        for(int i=0;i<30;i++)yield return null;
        Check(Vector2.Distance(ai.transform.position,origin)>.25f,"native Update/FixedUpdate actually moves patrol skeleton");
        Check(Vector2.Distance(idle.transform.position,new Vector2(-10,0))<.01f,"original skeleton physically remains still");

        Set(ai,"legacyPatrolPointCount",2);Call(ai,"RebuildPatrolPoints");Set(ai,"currentPatrolPointIndex",1);Set(ai,"waitTimer",0f);
        Check(Get<Vector2[]>(ai,"patrolPoints").Length==2,"supports reduced two-point routes");
        var route=new ZeldaPatrolCharacterAi.PatrolWaypoint[6];
        for(int i=0;i<route.Length;i++)route[i]=new ZeldaPatrolCharacterAi.PatrolWaypoint{offset=new Vector2(i*.4f,i%2),waitDuration=i*.1f};
        Set(ai,"useCustomPatrolRoute",true);Set(ai,"customPatrolPoints",route);Call(ai,"RebuildPatrolPoints");
        Check(Get<Vector2[]>(ai,"patrolPoints").Length==6,"supports more than four editable points");
        Position(ai,route[1].offset);Set(ai,"currentPatrolPointIndex",1);Set(ai,"waitTimer",0f);Call(ai,"TickIdle",.02f);
        Check(Get<int>(ai,"currentPatrolPointIndex")==2&&Mathf.Approximately(Get<float>(ai,"waitTimer"),.1f),"arrival advances index and uses per-point wait");
        var remembered=Get<Vector2[]>(ai,"patrolPoints")[4];ai.gameObject.SetActive(false);ai.gameObject.SetActive(true);
        Check(Get<Vector2[]>(ai,"patrolPoints")[4]==remembered&&Get<int>(ai,"currentPatrolPointIndex")==2,"streaming reactivation preserves route and progress");
        Set(ai,"useCustomPatrolRoute",false);Set(ai,"legacyPatrolPointCount",4);Call(ai,"RebuildPatrolPoints");Set(ai,"currentPatrolPointIndex",1);Set(ai,"waitTimer",0f);

        var victim=new GameObject("Visible non-skeleton",typeof(CivilianZeldaCharacterData),typeof(ZeldaFourWayMover));
        victim.GetComponent<ZeldaFourWayMover>().enabled=false;Position(ai,Vector2.zero);Position(victim.transform,new Vector2(1,0));
        Physics2D.SyncTransforms();Call(ai,"UpdateTargetAndState",.02f);
        Check(ai.CurrentState==ZeldaAiState.Hostile&&ai.CurrentTarget==victim.GetComponent<ZeldaFourWayMover>(),"patrol interrupted by ordinary NPC in sight");
        Check(!(bool)Call(ai,"CanSeeCharacter",idle.GetComponent<ZeldaFourWayMover>()),"does not target other skeletons");
        Check(ai.CaptureSaveState().sharedPlayerPhase==0,"no shared hostility enrollment");
        Position(victim.transform,new Vector2(100,100));Set(ai,"npcThreatLastSeenAt",Time.time-10);Call(ai,"UpdateTargetAndState",.02f);
        Check(ai.CurrentState==ZeldaAiState.Search,"lost target uses independent hostile timeout");
        Set(ai,"investigationElapsed",12f);Call(ai,"UpdateTargetAndState",.02f);
        Check(ai.CurrentState==ZeldaAiState.Recovery,"independent search returns home");
        Position(ai,Vector2.zero);Call(ai,"TickRecovery",.02f);Call(ai,"TickIdle",.02f);
        Check(ai.CurrentState==ZeldaAiState.Idle&&Get<int>(ai,"currentPatrolPointIndex")==1,"return resumes original patrol route");
        Object.DestroyImmediate(victim);

        var crystalPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/EnergyCrystal.prefab");
        var crystal=Object.Instantiate(crystalPrefab,new Vector3(-.7f,-.7f),Quaternion.identity).GetComponent<EnergyCrystal>();
        crystal.ActivityOffset=new Vector2(.7f,.7f);crystal.ActivitySize=new Vector2(3,3);crystal.BindSkeleton(data);
        Check(data.CanReassemble,"patrol skeleton binds to crystal normally");
        Set(ai,"currentPatrolPointIndex",1);Set(ai,"waitTimer",0f);
        var clipped=crystal.ClampPosition(new Vector2(2,0),.1f);Position(ai,clipped);Call(ai,"TickIdle",.02f);
        Check(Get<int>(ai,"currentPatrolPointIndex")==2,"outside authored point advances at clipped crystal boundary, no deadlock");
        var displacement=(Vector2)Call(ai,"ConstrainAiDisplacement",new Vector2(4,4));
        Check(crystal.Contains((Vector2)ai.transform.position+displacement),"patrol cannot walk outside rectangle");
        ai.GetComponent<ZeldaFourWayMover>().enabled=true;
        Check((Vector2)Call(ai,"ConstrainAiDisplacement",new Vector2(4,4))==new Vector2(4,4),"possessed patrol skeleton still ignores leash");
        ai.GetComponent<ZeldaFourWayMover>().enabled=false;
        var group=new GameObject("Patrol loading group").AddComponent<PatrolStreamingGroup>();
        Set(group,"members",new ZeldaPatrolCharacterAi[]{ai,idle});group.RefreshMembers();
        Check(((IList)Get<object>(group,"RuntimeMembers")).Count==1,"loading group accepts patrol skeleton but excludes idle original");
        Object.DestroyImmediate(group.gameObject);

        Position(ai,new Vector2(.5f,0));Set(data,"scatterDuration",.1f);Set(data,"revivalDelay",.1f);Set(data,"assembleDuration",.1f);
        data.TakeDamage(1000,null);Check(data.IsDead&&!ai.enabled,"death suspends patrol during reassembly");
        for(int i=0;i<140;i++)yield return null;
        Check(!data.IsDead&&ai.enabled&&ai.HasIdlePatrol,"bound skeleton revives with patrol enabled");
        Check(Get<Vector2[]>(ai,"patrolPoints")[0]==Vector2.zero,"revival preserves scene-load patrol origin, not death point");
        Check(!ai.GetComponent<ZeldaFourWayMover>().enabled,"revival does not steal player control");
        Check(crystal.Contains(ai.transform.position),"revived patrol stays in crystal rectangle");
        Check(ai.CurrentState==ZeldaAiState.Idle,"revived skeleton returns and resumes idle patrol");
        var p=(Vector2)ai.transform.position;for(int i=0;i<20;i++)yield return null;
        Check(Vector2.Distance(p,ai.transform.position)>.1f,"patrol continues after resurrection");
        Object.DestroyImmediate(crystal.gameObject);
        Check(!data.CanReassemble,"losing crystal also removes patrol skeleton resurrection");
    }
}
