using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Full production Unity scripts/physics. Fixtures and PNG stay in the isolated validation project.
[InitializeOnLoad]
public static class CannonSmoke
{
    const BindingFlags F=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static IEnumerator suite; static int checks, frame=-1;
    static CannonSmoke() { EditorApplication.update+=Tick; }
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
        SessionState.SetBool("CannonSmoke",true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying || frame==Time.frameCount) return; frame=Time.frameCount;
        try {
            if(SessionState.GetBool("CannonSmoke",false)) { SessionState.SetBool("CannonSmoke",false); suite=Verify(); }
            if(suite==null || suite.MoveNext()) return;
            suite=null; Debug.Log("CANNON_PASS checks="+checks); EditorApplication.Exit(0);
        } catch(Exception e) { suite=null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok,string label) { checks++; if(!ok) throw new Exception(label); }
    static FieldInfo Field(object o,string name) { for(var t=o.GetType();t!=null;t=t.BaseType) { var f=t.GetField(name,F); if(f!=null)return f; } throw new Exception(name); }
    static void Set(object o,string n,object v)=>Field(o,n).SetValue(o,v);
    static T Get<T>(object o,string n)=>(T)Field(o,n).GetValue(o);
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,F).Invoke(o,args);
    static ZeldaCharacterData Body(string name,Vector2 position)
    {
        var root=new GameObject(name); root.transform.position=position;
        root.AddComponent<BoxCollider2D>().size=Vector2.one*.12f;
        var actor=root.AddComponent<ZeldaCharacterData>();
        var state=actor.CaptureRuntimeState();state.health=30;state.currentHealth=30;state.skillValue=5;actor.ApplyRuntimeState(state);
        return actor;
    }
    static void ClearShots()
    {
        foreach(var item in Object.FindObjectsOfType<FortressCannonball>()) Object.DestroyImmediate(item.gameObject);
        foreach(var item in Object.FindObjectsOfType<ZeldaAttackHitbox>()) Object.DestroyImmediate(item.gameObject);
        foreach(var item in Object.FindObjectsOfType<BombExplosionVisual>()) Object.DestroyImmediate(item.gameObject);
        foreach(var item in Object.FindObjectsOfType<ZeldaInvestigationPulse>()) Object.DestroyImmediate(item.gameObject);
    }
    static FortressCannonball Fire(FortressCannon cannon,ZeldaCharacterData owner,Vector2 origin,float speed=12)
    {
        Check(FortressCannonball.Fire(cannon,owner,origin,Vector2.up,speed),"projectile spawned");
        var shot=Object.FindObjectOfType<FortressCannonball>();shot.enabled=false;return shot;
    }
    static IEnumerator Verify()
    {
        while(DocumentReader.IsInputBlocked) yield return null;
        Physics2D.simulationMode=SimulationMode2D.Script;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/Fortress-Cannon.prefab");
        Check(prefab,"prefab imports");
        var root=Object.Instantiate(prefab);var cannon=root.GetComponent<FortressCannon>();
        Check(cannon && root.GetComponent<CameraVisionStreamingExempt>(),"interactive cannon and streaming exemption");
        Check(root.GetComponent<BoxCollider2D>().size==new Vector2(2.4f,3f),"one solid cannon footprint");
        var mesh=root.GetComponentInChildren<MeshFilter>().sharedMesh;
        Check(mesh.name.Contains("Cannon") && mesh.vertexCount<300,"compact shared geometry");
        Check((int)GeometricFortressDecoration.FurnitureShape.Mortar==6 && (int)GeometricFortressDecoration.FurnitureShape.Ballista==1,"old shapes keep enum indices");
        RenderPreview(root);
        foreach(float angle in new[]{0f,45f,90f,180f,270f})
        foreach(var scale in new[]{Vector3.one,new Vector3(1.5f,.7f,1),new Vector3(-1,1.2f,1)})
        {
            root.transform.rotation=Quaternion.Euler(0,0,angle); root.transform.localScale=scale;
            Vector2 rear=root.transform.TransformPoint(0,-2,0);
            Check(cannon.IsAtRearAndFacing(rear,(Vector2)cannon.RearWorldPosition-rear),"rear follows rotation and scale");
            Check(!cannon.IsAtRearAndFacing(root.transform.TransformPoint(0,2,0),Vector2.down),"front cannot operate");
            Check(Vector2.Dot(cannon.ShotDirection,root.transform.TransformVector(Vector3.up).normalized)>.999,"muzzle direction follows transform");
        }
        root.transform.rotation=Quaternion.identity;root.transform.localScale=Vector3.one;
        var owner=Body("Operator",new Vector2(0,-2));var mover=owner.gameObject.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);mover.RestoreSavedMovement(Vector2.up,mover.CurrentStamina);
        Set(owner,"skillValue",4); Check(!cannon.TryFire() && !cannon.IsCoolingDown && !mover.IsAttacking,"skill four: no shot, gesture or cooldown");
        Set(owner,"skillValue",5); Check(cannon.TryFire(),"skill five can operate from rear");
        Check(cannon.IsCoolingDown && Mathf.Abs(cannon.RemainingCooldown-6)<.001f,"six second cooldown");
        var fired=Object.FindObjectOfType<FortressCannonball>();
        Check(fired && fired.transform.position==cannon.MuzzleWorldPosition && !Get<bool>(fired,"finished"),"fires from muzzle without hitting own carriage");
        Check(!cannon.TryFire(),"cannot refire during cooldown");
        ClearShots();mover.EndItemAttack();Set(cannon,"nextFireTime",Time.time-.01f);
        mover.RestoreSavedMovement(Vector2.down,mover.CurrentStamina);Check(!cannon.TryFire(),"must face cannon tail");
        mover.RestoreSavedMovement(Vector2.up,mover.CurrentStamina);Check(cannon.TryFire(),"ready after six seconds");ClearShots();mover.EndItemAttack();
        var origin=new Vector2(100,100);
        var nearby=Body("No proximity fuse",origin+Vector2.right*.6f);
        var shot=Fire(cannon,owner,origin);Check(!Get<bool>(shot,"finished"),"nearby character alone does not detonate cannonball");
        Object.DestroyImmediate(nearby.gameObject);ClearShots();
        var wall=new GameObject("Thin wall");wall.transform.position=origin+Vector2.up;
        wall.AddComponent<BoxCollider2D>().size=new Vector2(2,.02f);
        shot=Fire(cannon,owner,origin,300);Call(shot,"FixedUpdate");
        Check(Get<bool>(shot,"finished") && shot.transform.position.y<101,"swept contact catches thin wall at high speed");
        int blasts=Object.FindObjectsOfType<ZeldaAttackHitbox>().Length;Call(shot,"Explode");Call(shot,"FixedUpdate");
        Check(blasts==1 && Object.FindObjectsOfType<ZeldaAttackHitbox>().Length==1,"impact explodes once");
        Object.DestroyImmediate(wall);ClearShots();
        shot=Fire(cannon,owner,origin);Set(shot,"elapsed",4.97f);Call(shot,"FixedUpdate");Check(!Get<bool>(shot,"finished"),"not exploded before five seconds");
        Call(shot,"FixedUpdate");Check(Get<bool>(shot,"finished"),"explodes at five seconds");ClearShots();
        var victim=Body("Blast victim",origin+Vector2.right*2.8f);victim.gameObject.AddComponent<CircleCollider2D>().radius=.08f;
        var corner=Body("Outside circular blast",origin+new Vector2(2.7f,2.7f));
        var listener=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Engineer-level2-Idle.prefab"),origin+Vector2.left*5,Quaternion.identity);
        var ai=listener.GetComponent<ZeldaCharacterAiBase>();Set(ai,"enableAwarenessSound",false);
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        shot=Fire(cannon,owner,origin);Call(shot,"Explode");
        var blast=Object.FindObjectOfType<ZeldaAttackHitbox>();var circle=blast.GetComponent<CircleCollider2D>();
        Check(circle && circle.isTrigger && Mathf.Abs(circle.radius*blast.transform.localScale.x-3)<.001f && !blast.GetComponent<BoxCollider2D>().enabled,"true radius-three damage trigger");
        Physics2D.SyncTransforms();Physics2D.Simulate(.02f);Physics2D.Simulate(.02f);
        Check(victim.CurrentHealth==23,"compound target receives exactly seven damage once");
        Check(corner.CurrentHealth==30,"square corner outside circle is unharmed");
        Check(ai.CurrentState==ZeldaAiState.Search && Get<Vector2>(ai,"investigationIncidentPosition")==origin,"same bomb investigation at impact point");
        var pulse=Object.FindObjectOfType<ZeldaInvestigationPulse>();
        var bomb=Resources.Load<GameObject>("PickupItems/Bombs/BombPickupItem").GetComponent<BombPickupItem>();
        Check(pulse && Get<float>(pulse,"maximumRadius")==bomb.InvestigationRadius,"investigation range follows normal bomb prefab");
        ClearShots(); Object.DestroyImmediate(listener); Object.DestroyImmediate(victim.gameObject); Object.DestroyImmediate(corner.gameObject);
        // Shared predicate remains scene scoped and ignores ordinary interaction zones, not physical walls.
        var zone=new GameObject("Trigger zone");var zoneCollider=zone.AddComponent<BoxCollider2D>();zoneCollider.isTrigger=true;
        Check(!FortressBallistaBolt.CanProjectileHit(zoneCollider,root.transform,root.scene),"ordinary trigger ignored");
        zoneCollider.isTrigger=false;Check(FortressBallistaBolt.CanProjectileHit(zoneCollider,root.transform,root.scene),"solid props collide");
        Object.DestroyImmediate(zone);
        // Regression: the existing bolt still retains its character-only proximity fuse and 1.6 box blast.
        var ballista=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Fortress/Fortress-Ballista.prefab"));
        var fuseTarget=Body("Ballista fuse target",origin+Vector2.right*.6f);
        Check(FortressBallistaBolt.Fire(ballista.GetComponent<FortressBallista>(),owner,origin,Vector2.up,12),"ballista still fires");
        var bolt=Object.FindObjectOfType<FortressBallistaBolt>();Check(Get<bool>(bolt,"finished"),"existing bolt proximity retained");
        blast=Object.FindObjectOfType<ZeldaAttackHitbox>();Check(blast.GetComponent<BoxCollider2D>().enabled && blast.transform.localScale.x==1.6f,"ballista blast unchanged");
        yield return null;
    }
    static void RenderPreview(GameObject root)
    {
        var camera=new GameObject("Cannon preview").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=1.9f;
        camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.09f,.17f);
        var rt=new RenderTexture(640,640,24);camera.targetTexture=rt;camera.Render();
        var previous=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(640,640,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,640,640),0,0);texture.Apply();
        int red=0,gray=0;foreach(var c in texture.GetPixels()){if(c.r>.4f && c.r>c.g*2)red++;if(c.r>.4f && Mathf.Abs(c.r-c.b)<.15f)gray++;}
        Check(red>3000 && gray>10000,"rendered red carriage and gray barrel");
        System.IO.File.WriteAllBytes("Cannon-preview.png",texture.EncodeToPNG());
        RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(camera.gameObject);
    }
}
