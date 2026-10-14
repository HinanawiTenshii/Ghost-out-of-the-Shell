using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SquadPatrolSmoke
{
    static IEnumerator test; static int frame = -1, checks;
    static readonly List<GameObject> objects = new List<GameObject>();
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static SquadPatrolSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Patrol/Civilian-level2.prefab");
        var conversion=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
        var before=new SerializedObject(conversion.GetComponent<ZeldaCharacterAiBase>());
        before.FindProperty("visionRadius").floatValue=12.34f;before.ApplyModifiedPropertiesWithoutUndo();
        Selection.activeGameObject=conversion;
        SquadPatrolCharacterAiEditor.ConvertSelected();
        var converted=conversion.GetComponent<SquadPatrolCharacterAi>();
        Check(converted!=null&&conversion.GetComponents<ZeldaCharacterAiBase>().Length==1,"editor conversion replaces rather than duplicates AI");
        var after=new SerializedObject(converted);
        Check(Mathf.Approximately(after.FindProperty("visionRadius").floatValue,12.34f),"editor conversion preserves configured vision");
        Check(after.FindProperty("civilianPermissionRules").boolValue,"editor conversion preserves civilian rules");
        Object.DestroyImmediate(conversion);
        SessionState.SetBool("SquadPatrolSmoke", true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("SquadPatrolSmoke", false))
            { SessionState.SetBool("SquadPatrolSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("SQUAD_PATROL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static GameObject Own(GameObject go) { objects.Add(go); return go; }
    static void Clear() { for(int i=objects.Count-1;i>=0;i--) if(objects[i]) Object.DestroyImmediate(objects[i]); objects.Clear(); }
    static FieldInfo Field(object target,string name)
    {
        for(Type type=target.GetType();type!=null;type=type.BaseType)
        { var f=type.GetField(name,Flags); if(f!=null)return f; }
        throw new Exception(name);
    }
    static void Set(object target,string name,object value) => Field(target,name).SetValue(target,value);
    static T Get<T>(object target,string name) => (T)Field(target,name).GetValue(target);
    static Vector2 Position(SquadPatrolCharacterAi ai) => ai.GetComponent<Rigidbody2D>().position;
    static IEnumerator Wait(float seconds) { float end=Time.time+seconds; while(Time.time<end)yield return null; }
    static SquadPatrolCharacterAi[] MakeSquad(int followers, Vector2 origin)
    {
        var container=Own(new GameObject("Squad test")); container.SetActive(false);
        var squad=new SquadPatrolCharacterAi[followers+1];
        for(int i=0;i<squad.Length;i++)
        {
            var go=new GameObject("Member "+i); go.transform.SetParent(container.transform);
            Vector2 offset=i==0?Vector2.zero:SquadPatrolCharacterAi.FormationOffset(followers,i-1)*1.15f;
            go.transform.position=origin+new Vector2(offset.y,-offset.x);
            var data=go.AddComponent<CivilianZeldaCharacterData>(); Set(data,"moveSpeed",3.5f);
            var mover=go.AddComponent<ZeldaFourWayMover>(); mover.enabled=false; Set(mover,"initialFacingDirection",Enum.ToObject(Field(mover,"initialFacingDirection").FieldType,3));
            var ai=go.AddComponent<SquadPatrolCharacterAi>(); squad[i]=ai;
            go.GetComponent<BoxCollider2D>().size=new Vector2(.34f,.5f);
            go.GetComponent<BoxCollider2D>().offset=new Vector2(0,.23f);
            Set(ai,"patrolPoint2Offset",new Vector2(i==0?20:2,0));
            Set(ai,"patrolPoint3Offset",new Vector2(i==0?20:2,4));
            Set(ai,"patrolPoint4Offset",new Vector2(0,4));
            Set(ai,"waitAtPoint2Duration",2.4f);
            Set(ai,"enableAwarenessSound",false);
        }
        var members=new SquadPatrolCharacterAi[followers];
        Array.Copy(squad,1,members,0,followers); Set(squad[0],"members",members);
        container.SetActive(true); return squad;
    }
    static BoxCollider2D Wall(string name,Vector2 center,Vector2 size)
    {
        var go=Own(new GameObject(name));go.layer=LayerMask.NameToLayer("Blocks");go.transform.position=center;
        var wall=go.AddComponent<BoxCollider2D>();wall.size=size;return wall;
    }
    static IEnumerator Verify()
    {
        Time.timeScale=1;
        Check(SquadPatrolCharacterAi.FormationOffset(1,0)==new Vector2(0,-1),"one follower line");
        Check(SquadPatrolCharacterAi.FormationOffset(2,0)==new Vector2(-.8f,-1)&&
            SquadPatrolCharacterAi.FormationOffset(2,1)==new Vector2(.8f,-1),"two follower triangle");
        Check(SquadPatrolCharacterAi.FormationOffset(3,2)==new Vector2(0,-2),"three follower diamond");
        Check(SquadPatrolCharacterAi.FormationOffset(4,2)==new Vector2(-.8f,-2)&&
            SquadPatrolCharacterAi.FormationOffset(4,3)==new Vector2(.8f,-2),"four follower two rows");
        var team=MakeSquad(4,Vector2.zero);
        var wait=Wait(1.5f);while(wait.MoveNext())yield return null;
        foreach(var member in team)Check(member.CurrentLeader==team[0],"authored captain");
        foreach(var member in team)Check(Position(member).x>.25f,"open space followers move with leader: "+member.name+" "+Position(member));
        Check(!team[1].IsUsingSingleFile,"open space formation");
        for(int i=1;i<team.Length;i++)Check(Vector2.Distance(Position(team[i]),team[i].CurrentFormationTarget)<1f,"formed slot "+i);
        var originalRoute=(Vector2[])Get<Vector2[]>(team[0],"patrolPoints").Clone();
        int routeIndex=Get<int>(team[0],"currentPatrolPointIndex");
        // Distance unloading must not appoint a replacement.
        team[0].gameObject.SetActive(false);yield return null;yield return null;
        Check(team[1].CurrentLeader==team[0],"inactive streamed captain retained");
        team[0].gameObject.SetActive(true);yield return null;
        // Use actual character death rather than only toggling the AI.
        team[0].GetComponent<ZeldaCharacterData>().TakeDamage(999);
        yield return null;yield return null;
        Check(team[1].CurrentLeader==team[1],"first bound member inherits captain");
        for(int i=0;i<4;i++)Check(Get<Vector2[]>(team[1],"patrolPoints")[i]==originalRoute[i],"absolute original route preserved "+i);
        Check(Get<int>(team[1],"currentPatrolPointIndex")==routeIndex,"waypoint progress inherited");
        Check(Mathf.Approximately((float)typeof(ZeldaPatrolCharacterAi).GetMethod("GetWaitDuration",Flags).Invoke(team[1],new object[]{1}),2.4f),"wait settings inherited");
        team[1].GetComponent<ZeldaFourWayMover>().enabled=true;
        team[1].enabled=false;yield return null;yield return null;
        Check(team[2].CurrentLeader==team[2],"possession appoints next array member");
        for(int i=0;i<4;i++)Check(Get<Vector2[]>(team[2],"patrolPoints")[i]==originalRoute[i],"second successor retains original route");
        team[1].GetComponent<ZeldaFourWayMover>().enabled=false;team[1].enabled=true;
        yield return null;yield return null;
        Check(team[1].CurrentLeader==team[2],"released former captain rejoins without taking command");
        Clear();yield return null;

        var narrow=MakeSquad(4,new Vector2(0,20));
        var lower=Wall("lower",new Vector2(6,19.35f),new Vector2(6,.8f));
        var upper=Wall("upper",new Vector2(6,21.1f),new Vector2(6,.8f));
        bool compressed=false;float end=Time.time+10;
        while(Time.time<end)
        {
            foreach(var member in narrow)
            {
                compressed |= member.IsUsingSingleFile;
                var body=member.GetComponent<BoxCollider2D>();
                Check(!body.Distance(lower).isOverlapped&&!body.Distance(upper).isOverlapped,"no wall penetration");
            }
            yield return null;
        }
        Check(compressed,"narrow corridor selects single file");
        foreach(var member in narrow)Check(Position(member).x>9.2f,"whole team exits corridor: "+member.name+" "+Position(member));
        wait=Wait(2);while(wait.MoveNext())yield return null;
        Check(!narrow[1].IsUsingSingleFile,"formation expands after corridor");
        Clear();yield return null;
        var waiting=MakeSquad(2,new Vector2(0,40));
        yield return null;
        // Advance the real route into a stop, then transfer in mid-countdown.
        var route=Get<Vector2[]>(waiting[0],"patrolPoints");
        waiting[0].GetComponent<Rigidbody2D>().position=route[1];
        waiting[0].transform.position=route[1];
        wait=Wait(.2f);while(wait.MoveNext())yield return null;
        float remaining=Get<float>(waiting[0],"waitTimer");
        Check(remaining>1,"captain is actually waiting at a route point");
        waiting[0].GetComponent<ZeldaCharacterData>().TakeDamage(999);
        yield return null;yield return null;
        Check(waiting[1].CurrentLeader==waiting[1],"waiting captain replaced");
        float inherited=Get<float>(waiting[1],"waitTimer");
        Check(inherited>0&&inherited<=remaining+.05f&&inherited>remaining-.3f,"remaining stop duration inherited rather than reset");
    }
}
