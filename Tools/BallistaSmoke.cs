using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run only in an isolated test project: actual Unity physics, projectile, hitbox and visual.
// Character data, input arbitration and streaming controller use the accompanying small stubs.
[InitializeOnLoad]
public static class BallistaSmoke
{
    const string Pending="BallistaSmoke.Pending";
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static int checks;
    static BallistaSmoke(){EditorApplication.update+=Resume;}
    public static void Run(){SessionState.SetBool(Pending,true);EditorApplication.EnterPlaymode();}
    static void Resume()
    {
        if(!EditorApplication.isPlaying || !SessionState.GetBool(Pending,false)) return;
        SessionState.SetBool(Pending,false);
        try {Test();Debug.Log("BALLISTA_SMOKE_PASS checks="+checks);EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Check(bool v,string label){checks++;if(!v)throw new Exception(label);}
    static void Set(object o,string f,object v)=>o.GetType().GetField(f,Private).SetValue(o,v);
    static T Get<T>(object o,string f)=>(T)o.GetType().GetField(f,Private).GetValue(o);
    static void Call(object o,string m)=>o.GetType().GetMethod(m,Private).Invoke(o,null);
    static FortressBallistaBolt Bolt()
    {
        foreach(var b in UnityEngine.Object.FindObjectsOfType<FortressBallistaBolt>())
            if(!Get<bool>(b,"finished"))return b;
        return null;
    }
    static void ClearShots()
    {
        foreach(var b in UnityEngine.Object.FindObjectsOfType<FortressBallistaBolt>())UnityEngine.Object.DestroyImmediate(b.gameObject);
        foreach(var b in UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>())UnityEngine.Object.DestroyImmediate(b.gameObject);
        foreach(var b in UnityEngine.Object.FindObjectsOfType<BombExplosionVisual>())UnityEngine.Object.DestroyImmediate(b.gameObject);
    }
    static GameObject Box(string name,Vector2 position,Vector2 size,bool trigger=false)
    {
        var go=new GameObject(name);go.transform.position=position;var box=go.AddComponent<BoxCollider2D>();box.size=size;box.isTrigger=trigger;return go;
    }
    static void Test()
    {
        Physics2D.simulationMode=SimulationMode2D.Script;
        var go=new GameObject("Test Ballista");
        var geometry=go.AddComponent<GeometricFortressDecoration>();
        Set(geometry,"shape",GeometricFortressDecoration.FurnitureShape.Ballista);Set(geometry,"size",new Vector2(2.2f,2f));Call(geometry,"Rebuild");
        var ballista=go.AddComponent<FortressBallista>();
        var actor=Box("Operator",new Vector2(0,-1.5f),Vector2.one*.25f);
        var data=actor.AddComponent<ZeldaCharacterData>();var mover=actor.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.Controlled=mover;
        Check(ballista.IsAtRearAndFacing(actor.transform.position,Vector2.up),"Rear position facing handle allowed");
        Check(!ballista.IsAtRearAndFacing(actor.transform.position,Vector2.down),"Facing away rejected");
        Check(!ballista.IsAtRearAndFacing(new Vector2(1.5f,-1.5f),Vector2.up),"Side of ballista rejected");
        Check(!ballista.IsAtRearAndFacing(new Vector2(0,1.5f),Vector2.down),"Front rejected");
        Check(!ballista.IsAtRearAndFacing(new Vector2(0,-4),Vector2.up),"Out of reach rejected");
        foreach(float angle in new[]{0f,45f,90f,180f,270f})
        foreach(Vector3 scale in new[]{Vector3.one,new Vector3(1.6f,.7f,1),new Vector3(-1.3f,1.8f,1)})
        {
            go.transform.rotation=Quaternion.Euler(0,0,angle);go.transform.localScale=scale;
            Vector3 rear=go.transform.TransformPoint(0,-1.5f,0);
            Vector2 facing=((Vector2)ballista.RearWorldPosition-(Vector2)rear).normalized;
            Check(ballista.IsAtRearAndFacing(rear,facing),"Operating region follows rotation/nonuniform or mirrored scale");
            Check(Vector2.Dot(ballista.ShotDirection,go.transform.TransformVector(Vector3.up).normalized)>.999f,"Shot follows authored arrow");
        }
        go.transform.rotation=Quaternion.Euler(0,0,90);go.transform.localScale=Vector3.one;
        Check(Vector2.Dot(ballista.ShotDirection,Vector2.left)>.999f,"Reference screenshot fires left");
        go.transform.rotation=Quaternion.identity;
        Set(ballista,"nextFireTime",-1f);
        data.IsGhostLike=true;Check(!ballista.TryFire(),"Ghost cannot operate");data.IsGhostLike=false;
        DocumentReader.IsInputBlocked=true;Check(!ballista.TryFire(),"UI blocks firing");DocumentReader.IsInputBlocked=false;
        Time.timeScale=0;Check(!ballista.TryFire(),"Pause blocks firing");Time.timeScale=1;
        Check(ballista.TryFire(),"Rear E action launches a bolt");
        var bolt=Bolt();Check(bolt!=null,"Bolt created outside own collision");bolt.enabled=false;
        Check(Mathf.Abs(Get<float>(ballista,"nextFireTime")-Time.time-3f)<.001f,"Cooldown exactly three seconds");
        Check(!ballista.TryFire(),"Repeated input during cooldown rejected");
        Set(ballista,"recoilRemaining",.2f);Call(ballista,"LateUpdate");
        Check(go.transform.position==Vector3.zero && go.GetComponent<BoxCollider2D>().offset==Vector2.zero,"Recoil never moves launcher collision");
        Check(geometry.GeneratedRoot.localPosition.sqrMagnitude>0,"Generated art recoils");
        ballista.enabled=false;Check(geometry.GeneratedRoot.localPosition==Vector3.zero,"Disabling clears visual recoil");ballista.enabled=true;
        ClearShots();Set(ballista,"nextFireTime",Time.time-.001f);Check(ballista.TryFire(),"Can fire after cooldown");ClearShots();

        // A fast arrow cannot tunnel through a thin wall; the first of two walls wins.
        var wall=Box("Thin wall",new Vector2(0,2.5f),new Vector2(2,.01f));
        var farther=Box("Farther wall",new Vector2(0,4),new Vector2(2,.01f));
        Check(FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,300),"Fast bolt spawned");
        bolt=Bolt();bolt.enabled=false;Call(bolt,"FixedUpdate");
        Check(Get<bool>(bolt,"finished") && bolt.transform.position.y<2.5f && bolt.transform.position.y>2.3f,"Swept nearest thin-wall contact");
        int blasts=UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>().Length;
        Call(bolt,"FixedUpdate");Call(bolt,"Explode");
        Check(UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==blasts && blasts==1,"Contact produces exactly one explosion");
        var blast=UnityEngine.Object.FindObjectOfType<ZeldaAttackHitbox>();
        Check(Get<int>(blast,"attackPower")==8 && blast.transform.localScale==new Vector3(1.6f,1.6f,1),"Power eight, smaller-than-bomb area");
        Check(!blast.GetComponent<SpriteRenderer>().enabled,"Only gray-white explosion visual shown");
        var effect=UnityEngine.Object.FindObjectOfType<BombExplosionVisual>();
        Color tint=Get<Color>(effect,"blastColor");Check(Mathf.Abs(tint.r-tint.b)<.03f && tint.r>.7f,"Neutral gray-white explosion");
        ClearShots();UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(farther);

        // Trigger-only gameplay zones are not solid; damageable triggers still stop bolts.
        var zone=Box("Permission trigger",new Vector2(0,2),new Vector2(2,.2f),true);
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,12);bolt=Bolt();bolt.enabled=false;
        for(int i=0;i<9;i++)Call(bolt,"FixedUpdate");
        Check(!Get<bool>(bolt,"finished") && bolt.transform.position.y>3,"Nonphysical zones do not consume arrows");
        ClearShots();UnityEngine.Object.DestroyImmediate(zone);
        var door=Box("Glass trigger",new Vector2(0,2),new Vector2(2,.02f),true);door.AddComponent<DoorData>();
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,50);bolt=Bolt();bolt.enabled=false;Call(bolt,"FixedUpdate");
        Check(Get<bool>(bolt,"finished"),"Damageable trigger is hit");
        Physics2D.SyncTransforms();Physics2D.Simulate(.02f);
        Check(door.GetComponent<DoorData>().Damage==8,"Production hitbox damages glass");
        ClearShots();UnityEngine.Object.DestroyImmediate(door);

        var blocked=Box("Wall at muzzle",new Vector2(0,1.05f),new Vector2(1,.05f));
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,12);
        Check(Bolt()==null && UnityEngine.Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==1,"Muzzle overlap detonates immediately");
        ClearShots();UnityEngine.Object.DestroyImmediate(blocked);

        // At four seconds there is a blast even without a hit. Shooter changes do not cancel it.
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,12);bolt=Bolt();bolt.enabled=false;
        data.IsDead=true;ZeldaRuntimeRegistry.Controlled=null;
        for(int i=0;i<199;i++)Call(bolt,"FixedUpdate");
        Check(!Get<bool>(bolt,"finished"),"No premature four-second expiry");
        for(int i=0;i<3;i++)Call(bolt,"FixedUpdate");
        Check(Get<bool>(bolt,"finished") && Mathf.Abs(bolt.transform.position.y-49)<.015f,"Four-second timeout explodes at traveled position");
        ClearShots();data.IsDead=false;ZeldaRuntimeRegistry.Controlled=mover;

        var target=Box("Target",new Vector2(0,3),Vector2.one*.2f);var targetData=target.AddComponent<ZeldaCharacterData>();
        target.AddComponent<CircleCollider2D>().radius=.10f; // Two colliders must not cause double damage.
        var nearby=Box("Nearby",new Vector2(.6f,3),Vector2.one*.2f);var nearbyData=nearby.AddComponent<ZeldaCharacterData>();
        var outside=Box("Outside",new Vector2(1.8f,3),Vector2.one*.2f);var outsideData=outside.AddComponent<ZeldaCharacterData>();
        target.SetActive(false);CameraVisionObjectStreaming.Suspended.Add(target);
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,Vector2.up,12);bolt=Bolt();bolt.enabled=false;
        Check(target.activeSelf && CameraVisionObjectStreaming.Refreshes>0,"Retention wakes a streamed-out target before query");
        for(int i=0;i<20 && !Get<bool>(bolt,"finished");i++)Call(bolt,"FixedUpdate");
        Check(Get<bool>(bolt,"finished"),"Character contact detonates");
        Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Physics2D.Simulate(.02f);
        Check(targetData.Damage==8 && nearbyData.Damage==8 && outsideData.Damage==0,"Production hitbox damages each nearby target once, not outside area");
        Check(targetData.LastOwner==data,"Damage attributed to firing character");
        ClearShots();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(nearby);UnityEngine.Object.DestroyImmediate(outside);

        BallistaProximitySmoke.Test(ballista,data);

        // Capture the actual geometry in the same left-facing orientation as the reference.
        go.transform.rotation=Quaternion.Euler(0,0,90);
        FortressBallistaBolt.Fire(ballista,data,ballista.MuzzleWorldPosition,ballista.ShotDirection,12);bolt=Bolt();bolt.enabled=false;
        for(int i=0;i<4;i++)Call(bolt,"FixedUpdate");
        var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.6f;
        camera.transform.position=new Vector3(-.55f,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.09f,.13f);
        var rt=new RenderTexture(720,480,24);camera.targetTexture=rt;camera.aspect=1.5f;camera.Render();RenderTexture.active=rt;
        var image=new Texture2D(720,480,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,720,480),0,0);image.Apply();
        System.IO.File.WriteAllBytes(Application.dataPath+"/../ballista-preview.png",image.EncodeToPNG());
    }
}
