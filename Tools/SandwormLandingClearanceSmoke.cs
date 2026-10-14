using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class SandwormLandingClearanceSmoke
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Pending="SandwormLandingClearanceSmoke.Pending";
    static readonly List<GameObject> owned=new List<GameObject>();
    static IEnumerator test; static int frame=-1,checks;
    static SandwormLandingClearanceSmoke() { EditorApplication.update+=Tick; }
    public static void Run() { SessionState.SetBool(Pending,true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount) return; frame=Time.frameCount;
        try
        {
            if(SessionState.GetBool(Pending,false)) { SessionState.SetBool(Pending,false); test=Verify(); }
            if(test==null || test.MoveNext()) return;
            test=null; Cleanup(); Debug.Log("SANDWORM_LANDING_CLEARANCE_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception e) { test=null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool b,string label) { checks++; if(!b) throw new Exception(label); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup() { foreach(var go in owned) if(go!=null) Object.DestroyImmediate(go); owned.Clear(); }
    static object Call(object obj,string name,params object[] args) => obj.GetType().GetMethod(name,Flags).Invoke(obj,args);
    static void SetPhase(SandwormEruptionAttack attack,SandwormEruptionAttack.AttackPhase phase) =>
        typeof(SandwormEruptionAttack).GetField("<Phase>k__BackingField",Flags).SetValue(attack,phase);
    static SandwormEruptionAttack Worm(bool exposed=true)
    {
        var go=Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        go.transform.position=Vector3.zero; go.GetComponent<SandwormBossPrototype>().ResetPose();
        var attack=go.GetComponent<SandwormEruptionAttack>(); Call(attack,"Initialize");
        if(exposed) { go.GetComponent<SandwormStateController>().EnterAboveGround(); SetPhase(attack,SandwormEruptionAttack.AttackPhase.Stunned); }
        return attack;
    }
    static ZeldaCharacterData Actor(Vector2 at,bool ai=false)
    {
        var go=Own(new GameObject(ai?"Landing NPC":"Landing controlled actor")); go.SetActive(false); go.transform.position=at;
        var data=go.AddComponent<CivilianZeldaCharacterData>();
        var box=go.AddComponent<BoxCollider2D>(); box.size=Vector2.one*.45f;
        var mover=go.AddComponent<ZeldaFourWayMover>(); mover.enabled=!ai;
        if(ai) go.AddComponent<ZeldaCharacterAiBase>();
        go.SetActive(true); Physics2D.SyncTransforms(); return data;
    }
    static bool Overlaps(SandwormEruptionAttack worm,ZeldaCharacterData actor)
    {
        Physics2D.SyncTransforms(); var body=actor.GetComponent<Collider2D>();
        foreach(var part in worm.GetComponentsInChildren<SandwormSegmentVisual>())
        {
            var collider=part.GetComponent<Collider2D>(); if(!collider.enabled) continue;
            var distance=body.Distance(collider); if(distance.isOverlapped && distance.distance<-.001f) return true;
        }
        return false;
    }
    static void Clear(SandwormEruptionAttack worm) => Call(worm,"ClearGroundedCharacterOverlaps");
    static void CheckWindow(ZeldaCharacterStatsWindow window,ZeldaCharacterData data,Camera camera,Vector2 offset)
    {
        Call(window,"UpdateScreenPosition");
        var panel=(RectTransform)typeof(ZeldaCharacterStatsWindow).GetField("panelRect",Flags).GetValue(window);
        var canvas=(Canvas)typeof(ZeldaCharacterStatsWindow).GetField("overlayCanvas",Flags).GetValue(window);
        var expected=camera.WorldToScreenPoint(data.AttributeWindowAnchor.position+data.transform.TransformVector(offset));
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform,expected,null,out Vector2 local);
        Check(Vector2.Distance(panel.anchoredPosition,local)<.01f,"window follows live anchor with root-relative offset");
    }
    static IEnumerator Verify()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Sandworm landing and stats isolated")); Time.timeScale=0;
        var worm=Worm(); var motion=worm.GetComponent<SandwormBossPrototype>();
        Vector2 center=motion.BodySegments[2].position;
        var upper=Actor(center+Vector2.up*.6f); var lower=Actor(center-Vector2.up*.6f,true);
        var far=Actor(center+Vector2.up*10); Vector3 farPosition=far.transform.position;
        Check(Overlaps(worm,upper)&&Overlaps(worm,lower),"both sides overlap before landing resolution");
        int upperHp=upper.CurrentHealth,lowerHp=lower.CurrentHealth;
        Clear(worm);
        Check(!Overlaps(worm,upper)&&!Overlaps(worm,lower),"player and NPC leave all compound body colliders");
        Check(upper.transform.position.y>center.y && lower.transform.position.y<center.y,"opposite sides push outward");
        Check(Mathf.Abs(upper.transform.position.x-center.x)<.01f && Mathf.Abs(lower.transform.position.x-center.x)<.01f,"prefer lateral, not lengthwise, escape");
        Check(upper.CurrentHealth==upperHp && lower.CurrentHealth==lowerHp,"separation does not add damage");
        Check(far.transform.position==farPosition,"nonoverlapping actors stay put");
        var settled=upper.transform.position; Clear(worm); Check(upper.transform.position==settled,"separation does not repeat once clear");
        Cleanup(); yield return null;

        worm=Worm(); center=worm.GetComponent<SandwormBossPrototype>().BodySegments[2].position;
        upper=Actor(center+Vector2.up*.5f);
        var wall=Own(new GameObject("Blocks above landing")); wall.layer=LayerMask.NameToLayer("Blocks");
        wall.transform.position=center+Vector2.up*2.2f; wall.AddComponent<BoxCollider2D>().size=new Vector2(15,.4f);
        Physics2D.SyncTransforms(); Clear(worm);
        Check(!Overlaps(worm,upper)&&upper.transform.position.y<center.y,"blocked preferred side chooses opposite exit without crossing wall");
        Check(!upper.GetComponent<Collider2D>().Distance(wall.GetComponent<Collider2D>()).isOverlapped,"no wall penetration");
        Cleanup(); yield return null;

        worm=Worm(); center=worm.GetComponent<SandwormBossPrototype>().BodySegments[2].position;
        var inOtherScene=Actor(center);
        SceneManager.MoveGameObjectToScene(inOtherScene.gameObject,SceneManager.CreateScene("Other landing scene"));
        Clear(worm); Check((Vector2)inOtherScene.transform.position==center,"different loaded scenes isolated");
        Cleanup(); yield return null;

        // Real explosive fall, then continued settling: catches only-once or too-early fixes.
        worm=Worm(false); motion=worm.GetComponent<SandwormBossPrototype>(); UnityEngine.Random.InitState(7491);
        Check(worm.TryLockAttack(new Vector2(8,6)),"fall fixture plans");
        var bomb=Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem").GetComponent<BombPickupItem>();
        Own(bomb.CreateConfiguredPlacedBomb(worm.LockedAttackPoint,null).gameObject);
        for(int n=0;n<3000 && worm.Phase!=SandwormEruptionAttack.AttackPhase.Falling;n++)
        { motion.Advance(.025f); worm.AdvanceAttack(.025f); }
        Check(worm.Phase==SandwormEruptionAttack.AttackPhase.Falling,"real swallow reaches falling");
        upper=Actor(worm.FallGroundPoint+Vector2.up*.5f); lower=Actor(worm.FallGroundPoint-Vector2.up*.5f,true);
        upperHp=upper.CurrentHealth; lowerHp=lower.CurrentHealth;
        for(int n=0;n<160 && (worm.Phase==SandwormEruptionAttack.AttackPhase.Falling || worm.StunRemaining>4f);n++)
        {
            motion.Advance(.025f); worm.AdvanceAttack(.025f);
            if(worm.Phase==SandwormEruptionAttack.AttackPhase.Stunned)
                Check(!Overlaps(worm,upper)&&!Overlaps(worm,lower),"whole landing/settling sequence remains free of trapped actors");
        }
        Check(worm.Phase==SandwormEruptionAttack.AttackPhase.Stunned,"landing retains configured stun");
        Check(upper.CurrentHealth==upperHp && lower.CurrentHealth==lowerHp,"landing push neither injures nor kills actors");
        Cleanup(); yield return null;

        worm=Worm(false); motion=worm.GetComponent<SandwormBossPrototype>(); motion.SetMovementEnabled(false);
        var data=worm.GetComponent<SandwormZeldaCharacterData>();
        var window=data.GetComponentInChildren<ZeldaCharacterStatsWindow>(true);
        var camera=Own(new GameObject("MainCamera")).AddComponent<Camera>(); camera.tag="MainCamera";
        camera.orthographic=true; camera.orthographicSize=4;
        motion.Head.position=new Vector3(20,10,0); camera.transform.position=new Vector3(20,10,-10);
        Vector2 offset=new Vector2(.25f,.5f); window.Configure(data,offset,2.2f); Canvas.ForceUpdateCanvases();
        Check(data.AttributeWindowAnchor==motion.Head,"boss window anchored to moving head");
        Check((bool)Call(window,"IsCharacterInsideCameraVision"),"head visible even when stationary root is offscreen");
        CheckWindow(window,data,camera,offset);
        motion.Head.position+=new Vector3(1,.5f); motion.Head.rotation=Quaternion.Euler(0,0,90);
        CheckWindow(window,data,camera,offset);
        camera.transform.position=new Vector3(0,0,-10);
        Check(!(bool)Call(window,"IsCharacterInsideCameraVision"),"visible old root cannot keep offscreen head window visible");
        var ordinary=Actor(new Vector2(1,2)); Check(ordinary.AttributeWindowAnchor==ordinary.transform,"ordinary character anchor unchanged");
        Cleanup(); yield return null;
    }
}
