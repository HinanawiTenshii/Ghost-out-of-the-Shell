using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class EnergyCrystalSmoke
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static int checks;
    static EnergyCrystal crystal;
    static SkeletonZeldaCharacterData unbound, bound, disrupted;
    static double finishAt;
    static EnergyCrystalSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt("EnergyCrystalSmoke", 1); EditorApplication.EnterPlaymode();
    }
    public static void Preview()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var c=Crystal(new Vector2(-.95f,-.15f));
            var s=Skeleton(new Vector2(1.05f,-.25f));
            s.GetComponent<SpriteRenderer>().sprite=s.GetPreviewSprite();
            c.BindSkeleton(s);Call(c.GetComponent<EnergyCrystalVisual>(),"WriteLinks",.7f);
            var camera=new GameObject("Preview Camera").AddComponent<Camera>();
            camera.orthographic=true;camera.orthographicSize=1.55f;camera.transform.position=new Vector3(0,.12f,-10);
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(19,27,39,255);
            var rt=new RenderTexture(960,600,24);camera.targetTexture=rt;camera.Render();
            RenderTexture.active=rt;
            var texture=new Texture2D(960,600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,600),0,0);texture.Apply();
            var output=System.IO.Path.GetFullPath(Application.dataPath+"/../../../Docs/EnergyCrystal-preview.png");
            System.IO.File.WriteAllBytes(output,texture.EncodeToPNG());
            Debug.Log("ENERGY_CRYSTAL_PREVIEW "+output);
            RenderTexture.active=null;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(texture);
            EditorApplication.Exit(0);
        }
        catch(Exception e) { Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static FieldInfo Field(Type type, string name)
    {
        while(type != null) { var f=type.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f;type=type.BaseType; }
        throw new Exception("Missing field "+name);
    }
    static object Get(object obj,string name)=>Field(obj.GetType(),name).GetValue(obj);
    static void Set(object obj,string name,object value)=>Field(obj.GetType(),name).SetValue(obj,value);
    static object Call(object obj,string name,params object[] args)
    {
        var type=obj is Type ? (Type)obj : obj.GetType();
        while(type!=null)
        {
            var method=type.GetMethod(name,Flags|BindingFlags.DeclaredOnly);
            if(method!=null)return method.Invoke(obj is Type ? null : obj,args);
            type=type.BaseType;
        }
        throw new Exception("Missing method "+name);
    }
    static void Check(bool ok,string label) { checks++; if(!ok)throw new Exception(label); }
    static void Position(GameObject obj,Vector2 p)
    { obj.transform.position=p;var rb=obj.GetComponent<Rigidbody2D>();if(rb)rb.position=p;Physics2D.SyncTransforms(); }
    static GameObject Actor(string name,Type data,Type ai,Vector2 position)
    {
        var go=new GameObject(name);go.AddComponent(data);go.AddComponent<ZeldaFourWayMover>();
        if(ai!=null) { go.GetComponent<ZeldaFourWayMover>().enabled=false;var component=go.AddComponent(ai);
            Set(component,"enableAwarenessSound",false);Set(component,"visionRadius",3f);
            Set(component,"visionObstacleLayers",(LayerMask)0);Set(component,"solidCollisionLayers",(LayerMask)0); }
        Position(go,position);return go;
    }
    static SkeletonZeldaCharacterData Skeleton(Vector2 p)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/Skeleton-level2.prefab");
        Check(source!=null,"skeleton asset resolves");
        var go=Object.Instantiate(source);Position(go,p);
        var ai=go.GetComponent<SkeletonCharacterAi>();Set(ai,"enableAwarenessSound",false);
        return go.GetComponent<SkeletonZeldaCharacterData>();
    }
    static EnergyCrystal Crystal(Vector2 p)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/EnergyCrystal.prefab");
        Check(source!=null,"crystal prefab loads");
        var go=Object.Instantiate(source);Position(go,p);return go.GetComponent<EnergyCrystal>();
    }
    static void Tick()
    {
        int phase=SessionState.GetInt("EnergyCrystalSmoke",0);
        if(!EditorApplication.isPlaying || phase==0)return;
        try
        {
            if(phase==1)
            {
                SessionState.SetInt("EnergyCrystalSmoke",2);
                VerifyTimers(); ClearScene(); VerifyCrystal();
                finishAt=EditorApplication.timeSinceStartup+2.8;
            }
            else if(EditorApplication.timeSinceStartup>finishAt)
            {
                Check(unbound==null,"unbound bones fade and remove actor");
                Check(disrupted==null,"breaking crystal during reassembly permanently removes remains");
                Check(bound!=null&&!bound.IsDead,"bound skeleton reassembles and revives");
                Check(bound.GetComponent<SkeletonCharacterAi>().enabled,"revived AI enabled");
                Check(!bound.GetComponent<ZeldaFourWayMover>().enabled,"revival never takes player control");
                Check(bound.GetComponent<BoxCollider2D>().enabled,"revived collider enabled");
                Check(bound.CanReassemble,"binding survives death/reassembly");
                SessionState.SetInt("EnergyCrystalSmoke",0);
                Debug.Log("ENERGY_CRYSTAL_PASS checks="+checks);EditorApplication.Exit(0);
            }
        }
        catch(Exception e) { SessionState.SetInt("EnergyCrystalSmoke",0);Debug.LogException(e);EditorApplication.Exit(1); }
    }
    static void ClearScene()
    {
        foreach(var go in SceneManager.GetActiveScene().GetRootGameObjects())Object.DestroyImmediate(go);
    }
    static void VerifyTimers()
    {
        var player=Actor("Player",typeof(CivilianZeldaCharacterData),null,new Vector2(500,500)).GetComponent<ZeldaFourWayMover>();
        var a=Actor("Skeleton",typeof(SkeletonZeldaCharacterData),typeof(SkeletonCharacterAi),Vector2.zero).GetComponent<SkeletonCharacterAi>();
        var b=Actor("Automaton",typeof(AutomatonZeldaCharacterData),typeof(AutomatonCharacterAi),new Vector2(50,0)).GetComponent<AutomatonCharacterAi>();
        b.ActivateHostility();
        var guard=Actor("Guard",typeof(CivilianZeldaCharacterData),typeof(ZeldaCharacterAiBase),new Vector2(100,0)).GetComponent<ZeldaCharacterAiBase>();
        var timer=SharedPlayerHostilityTimer.ForScene(SceneManager.GetActiveScene());
        Call(a,"BeginTemporaryHostility",player,false);Call(b,"BeginTemporaryHostility",player,false);
        Check(a.CurrentState==ZeldaAiState.Hostile && b.CurrentState==ZeldaAiState.Hostile,"creatures acquire player independently");
        Check(timer.Phase==SharedPlayerHostilityTimer.IncidentPhase.Idle,"creatures do not start global incident");
        Check(a.CaptureSaveState().sharedPlayerPhase==0 && b.CaptureSaveState().sharedPlayerPhase==0,"no global timer in creature snapshots");
        Set(a,"npcThreatLastSeenAt",Time.time-10);Set(b,"npcThreatLastSeenAt",Time.time-4);
        Call(a,"UpdateTargetAndState",.1f);Call(b,"UpdateTargetAndState",.1f);
        Check(a.CurrentState==ZeldaAiState.Search&&b.CurrentState==ZeldaAiState.Hostile,"staggered individual hostile timers");
        Set(a,"investigationElapsed",11.99f);Call(a,"UpdateTargetAndState",.02f);
        Check(a.CurrentState==ZeldaAiState.Recovery&&b.CurrentState==ZeldaAiState.Hostile,"individual search expiry isolated");
        Set(b,"npcThreatLastSeenAt",Time.time-10);Call(b,"UpdateTargetAndState",.1f);
        Check(b.CurrentState==ZeldaAiState.Search,"activated automaton permits Search");
        Set(b,"investigationElapsed",11.99f);Call(b,"UpdateTargetAndState",.02f);
        Check(b.CurrentState==ZeldaAiState.Recovery&&b.HostilityActivated,"automaton returns without losing activation");
        a.OnCharacterDamagedBy(player.CharacterData);Set(a,"npcThreatLastSeenAt",Time.time-10);Call(a,"UpdateTargetAndState",.1f);
        Check(a.CurrentState==ZeldaAiState.Search,"unseen player retaliation also expires for skeleton");
        guard.OnCharacterDamagedBy(a.GetComponent<ZeldaCharacterData>());
        Check(guard.CurrentTarget==a.GetComponent<ZeldaFourWayMover>()&&guard.CurrentState==ZeldaAiState.Hostile,"unseen skeleton attack provokes NPC");
        Check(timer.Phase==SharedPlayerHostilityTimer.IncidentPhase.Idle,"NPC retaliating against skeleton does not trigger global");
        Set(guard,"npcThreatLastSeenAt",Time.time-10);Call(guard,"UpdateTargetAndState",.1f);
        Check(guard.CurrentState==ZeldaAiState.Search,"NPC-vs-skeleton uses individual hostile timeout");
        Set(guard,"investigationElapsed",11.99f);Call(guard,"UpdateTargetAndState",.02f);
        Check(guard.CurrentState==ZeldaAiState.Recovery,"NPC-vs-skeleton search expires individually");
        guard.OnCharacterDamagedBy(b.GetComponent<ZeldaCharacterData>());
        Check(guard.CurrentTarget==b.GetComponent<ZeldaFourWayMover>()&&timer.Phase==SharedPlayerHostilityTimer.IncidentPhase.Idle,"automaton retaliation isolated");
        guard.OnCharacterDamagedBy(player.CharacterData);
        Check(timer.Phase==SharedPlayerHostilityTimer.IncidentPhase.Hostile,"normal player damage still starts shared clock");
        Position(a.gameObject,new Vector2(500,499));
        Set(a,"facingDirection",Vector2.up);
        timer.Advance(2);
        Check(Mathf.Approximately(timer.Remaining,7),"creature seeing player cannot refresh global clock");
        var observer=Actor("Occupied guard",typeof(CivilianZeldaCharacterData),typeof(ZeldaCharacterAiBase),new Vector2(500,499)).GetComponent<ZeldaCharacterAiBase>();
        observer.OnCharacterDamagedBy(b.GetComponent<ZeldaCharacterData>());
        Set(observer,"facingDirection",Vector2.up);
        Check(!(bool)Call(observer,"CanObserveSharedPlayer",player),"guard occupied with creature excluded from observers");
        timer.Advance(1);Check(Mathf.Approximately(timer.Remaining,6),"creature combat cannot refresh an existing player clock");
        Call(observer,"BeginRecovery");timer.Advance(.1f);
        Check(Mathf.Approximately(timer.Remaining,9),"ordinary free observer retains normal global refresh");
        var saved=a.CaptureSaveState();a.ApplySaveState(saved);
        Check(a.CaptureSaveState().sharedPlayerPhase==0,"creature restore does not join shared clock");
    }
    static void VerifyCrystal()
    {
        crystal=Crystal(new Vector2(30,30));VerifyRectangle(crystal);crystal.ActivitySize=new Vector2(10,6);
        Check(crystal.IsFunctional&&crystal.GetComponent<BoxCollider2D>()&&!crystal.GetComponent<BoxCollider2D>().isTrigger,"crystal starts intact/solid");
        Check(Resources.Load<AudioClip>("Audio/EnergyCrystalShatter")!=null,"dedicated shatter audio imports");
        bound=Skeleton(new Vector2(31,30));
        Check(!bound.CanReassemble,"new skeleton has no implicit revival");
        Check(crystal.BindSkeleton(bound)&&bound.CanReassemble,"binding enables revival");
        Check(crystal.BindSkeleton(bound)&&crystal.Skeletons.Count==1,"duplicate binding deduplicated");
        var other=Crystal(new Vector2(60,30));Check(!other.BindSkeleton(bound),"second crystal cannot steal binding");
        var ai=bound.GetComponent<SkeletonCharacterAi>();var mover=bound.GetComponent<ZeldaFourWayMover>();
        var target=Actor("Leash target",typeof(CivilianZeldaCharacterData),null,new Vector2(32,30)).GetComponent<ZeldaFourWayMover>();
        Call(ai,"BeginTemporaryHostility",target,false);Position(target.gameObject,new Vector2(38,30));Call(ai,"UpdateTargetAndState",.1f);
        Check(ai.CurrentState==ZeldaAiState.Recovery&&ai.CurrentTarget==null,"target outside radius immediately returns, not Search");
        Position(bound.gameObject,new Vector2(34.5f,30));
        Vector2 step=(Vector2)Call(ai,"ConstrainAiDisplacement",new Vector2(2,0));
        Check(step.x<=.501f&&step.x>0,"exact movement segment clipped to rectangle edge");
        Check(!(bool)Call(ai,"IsSearchWaypointClear",new Vector2(38,30)),"search excludes outside waypoints");
        Vector2 goal=(Vector2)Call(ai,"ConstrainAiDestination",new Vector2(80,30));Check(crystal.Contains(goal),"navigation destination constrained");
        mover.enabled=true;step=(Vector2)Call(ai,"ConstrainAiDisplacement",new Vector2(2,0));
        Check(step.x==2,"player-controlled skeleton unconstrained");mover.enabled=false;
        Position(bound.gameObject,new Vector2(31,30));Object.DestroyImmediate(target.gameObject);
        var visual=crystal.GetComponent<EnergyCrystalVisual>();
        Call(visual,"WriteLinks",0f);
        var links=crystal.transform.Find(EnergyCrystalVisual.LinkMeshName);
        Check(links&&links.GetComponent<MeshFilter>().sharedMesh.vertexCount>0,"DNA beads have geometry");
        Check(links.GetComponent<MeshRenderer>().sortingOrder<0,"link renders below characters");
        var linkMaterial=links.GetComponent<MeshRenderer>().sharedMaterial;
        Check(linkMaterial.GetFloat("_Intensity")>1&&linkMaterial.GetFloat("_CoreFraction")>.4f,"stronger link glow and bright core");
        Check(linkMaterial!=crystal.transform.Find("Crystal Soft Glow").GetComponent<MeshRenderer>().sharedMaterial,"link enhancement isolated from pedestal glow");
        Check((float)Get(visual,"linkParticleScale")>1.6f,"larger world-sized beads");
        var colors=links.GetComponent<MeshFilter>().sharedMesh.colors;
        foreach(var c in colors)Check(c.g>c.r&&c.g>c.b,"link vertices remain green");
        var before=visual.CrystalCenter;Call(visual,"UpdateFrame",.7f);Check(before!=visual.CrystalCenter,"floating center animates");
        for(int i=0;i<11;i++)
        {
            var s=Skeleton(new Vector2(32+i*.05f,30));crystal.BindSkeleton(s);s.gameObject.SetActive(false);
        }
        Check(crystal.Skeletons.Count==12,"more than ten skeletons supported");
        Check(crystal.HasActiveBoundSkeletons,"active bound member retains crystal");
        bound.gameObject.SetActive(false);Check(!crystal.HasActiveBoundSkeletons,"all asleep releases crystal retention");
        bound.gameObject.SetActive(true);Check(crystal.HasActiveBoundSkeletons,"one member wakes retention again");
        VerifyStreaming(crystal,bound);
        // A dead skeleton's pieces scatter with no collision, then either assemble or fade.
        unbound=Skeleton(new Vector2(80,80));disrupted=Skeleton(new Vector2(61,30));other.BindSkeleton(disrupted);
        foreach(var s in new[]{bound,unbound,disrupted})
        {
            Set(s,"scatterDuration",.1f);Set(s,"revivalDelay",.15f);Set(s,"assembleDuration",.1f);Set(s,"remainsFadeDuration",.25f);
            s.TakeDamage(1000,null);
            Check(s.IsDead&&!s.GetComponent<BoxCollider2D>().enabled,"death disables body collider");
            Check(s.transform.Find("Skeleton Bone Remains").GetComponentsInChildren<Collider2D>().Length==0,"fragments have no colliders");
        }
        Set(disrupted,"elapsed",.28f);
        other.ReceiveAttack(1);
        Check(other.IsBroken&&!disrupted.CanReassemble&&(bool)Get(disrupted,"fadingRemains"),"crystal break cancels ongoing assembly immediately");
        Check(!other.transform.Find("Floating Green Crystal").gameObject.activeSelf,"broken gem hidden, pedestal remains");
        int effects=Object.FindObjectsOfType<EnergyCrystalShatterVisual>().Length;other.ReceiveAttack(8);
        Check(Object.FindObjectsOfType<EnergyCrystalShatterVisual>().Length==effects,"repeated hits cannot repeat shatter");
        var saved=other.CaptureRuntimeState();other.ApplyRuntimeState(new EnergyCrystal.RuntimeState());other.ApplyRuntimeState(saved);
        Check(other.IsBroken&&!other.IsFunctional,"broken state round-trips without effects");
        Check(Object.FindObjectsOfType<EnergyCrystalShatterVisual>().Length==effects,"loading broken state silent");
        // Damageable projectile lookup and common area/melee routing.
        var hit=Crystal(new Vector2(90,90));var hitbox=new GameObject("Crystal Test Hitbox").AddComponent<ZeldaAttackHitbox>();
        Set(hitbox,"attackPower",1);Call(hitbox,"TryDamage",hit.GetComponent<Collider2D>());
        Check(hit.IsBroken,"common melee/explosion hitbox shatters crystal");Object.DestroyImmediate(hitbox.gameObject);
        Check((Object)Call(typeof(DisposableHandgunBullet),"FindDamageTarget",hit.GetComponent<Collider2D>())==hit,"bullet recognizes crystal");
        Check((Object)Call(typeof(ThrownGlassBottle),"FindDamageTarget",hit.GetComponent<Collider2D>())==hit,"thrown bottle recognizes crystal");
    }
    static void VerifyStreaming(EnergyCrystal c,SkeletonZeldaCharacterData s)
    {
        var camera=new GameObject("Streaming Test Camera").AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=2;
        Position(camera.gameObject,new Vector2(-200,-200));
        var manager=camera.gameObject.AddComponent<CameraVisionObjectStreaming>();
        Set(manager,"discoveryTransformsPerFrame",10000);Set(manager,"visibilityChecksPerFrame",10000);
        Set(manager,"unloadDelay",0f);Set(manager,"visibilityCheckInterval",.02f);
        Call(manager,"ProcessDiscoveryScan");
        var entries=(IDictionary)Get(manager,"managedLookup");Check(entries.Contains(c.gameObject),"streaming discovers crystal root");
        var entry=entries[c.gameObject];
        Check(((EnergyCrystal[])Get(entry,"energyCrystals")).Length==1,"streaming caches crystal dependency");
        var bounds=(Bounds)Call(typeof(CameraVisionObjectStreaming),"CalculateLocalBounds",c.gameObject);
        Check(bounds.size.x<3&&bounds.size.y<3,"long DNA links excluded from loading bounds");
        Call(manager,"RefreshStreamingViewport");
        Call(manager,"UpdateSpatialIndex");
        Set(entry,"lastVisibilityCheckTime",Time.unscaledTime-1);
        Call(manager,"UpdateStreaming");
        Check(c.gameObject.activeSelf,"offscreen crystal is retained while member active");
        s.gameObject.SetActive(false);
        Set(entry,"lastVisibilityCheckTime",Time.unscaledTime-1);
        Call(manager,"UpdateStreaming");
        Check(!c.gameObject.activeSelf&&(bool)Get(entry,"suspendedByManager"),"all sleeping members permit actual offscreen unload");
        s.gameObject.SetActive(true);
        Check(c.gameObject.activeSelf,"member reactivation restores dependency after actual unload");
        c.gameObject.SetActive(false);Set(entry,"suspendedByManager",true);
        s.gameObject.SetActive(false);s.gameObject.SetActive(true);
        Check(c.gameObject.activeInHierarchy&&!(bool)Get(entry,"suspendedByManager"),"streamed skeleton wakes streamed crystal immediately");
        Check((bool)Get(manager,"restoreObjectsWhenDisabled"),"streaming default restoration unchanged");
        Object.DestroyImmediate(camera.gameObject);
    }
    static void VerifyRectangle(EnergyCrystal c)
    {
        Check(c.ActivitySize==new Vector2(24,24),"default old radius becomes diameter-sized rectangle");
        Set(c,"activityRadius",28.198818f);
        Check(Vector2.Distance(c.ActivitySize,Vector2.one*56.397636f)<.0001f,"saved scene radius override still inherited before editing rectangle");
        c.ActivitySize=new Vector2(6,4);c.ActivityOffset=new Vector2(4,-3);
        foreach(float rotation in new[]{0f,35f,90f}) foreach(float sign in new[]{1f,-1f})
        {
            c.transform.rotation=Quaternion.Euler(0,0,rotation);
            c.transform.localScale=new Vector3(sign*1.5f,.65f,1);
            Func<Vector2,Vector2> world=p=>c.transform.TransformPoint(p+c.ActivityOffset);
            foreach(float x in new[]{-2.99f,0,2.99f}) foreach(float y in new[]{-1.99f,0,1.99f})
                Check(c.Contains(world(new Vector2(x,y))),"rectangle includes all corners under rotation/negative/nonuniform scaling");
            foreach(Vector2 p in new[]{new Vector2(3.1f,0),new Vector2(-3.1f,0),new Vector2(0,2.1f),new Vector2(0,-2.1f)})
                Check(!c.Contains(world(p)),"each edge rejects outside points");
            Vector2 clamped=c.transform.InverseTransformPoint(c.ClampPosition(world(new Vector2(8,9))));
            Check(Vector2.Distance(clamped-c.ActivityOffset,new Vector2(3,2))<.0001f,"clamp respects offset and transform");
            Vector2 inset=c.transform.InverseTransformPoint(c.ClampPosition(world(new Vector2(8,9)),.2f));
            Check(Vector2.Distance(inset-c.ActivityOffset,new Vector2(3-.2f/1.5f,2-.2f/.65f))<.0001f,"world-space navigation inset respects nonuniform scale");
            Vector2 start=world(new Vector2(2.9f,0)),delta=c.transform.TransformVector(new Vector3(2,3));
            Vector2 clipped=c.ConstrainDisplacement(start,delta);
            Check(c.Contains(start+clipped)&&clipped.magnitude>0&&clipped.magnitude<delta.magnitude,"diagonal segment clipped inside edge");
            Check(Mathf.Abs(delta.x*clipped.y-delta.y*clipped.x)<.0001f,"clipping preserves straight movement instead of bending around blockers");
            Check(c.ConstrainDisplacement(world(new Vector2(5,0)),(Vector2)c.transform.TransformVector(Vector3.left)).sqrMagnitude>0,"released outside body can move toward rectangle");
            Check(c.ConstrainDisplacement(world(new Vector2(5,0)),(Vector2)c.transform.TransformVector(Vector3.right))==Vector2.zero,"released outside body cannot move farther away");
        }
        c.transform.rotation=Quaternion.identity;c.transform.localScale=Vector3.one;c.ActivityOffset=Vector2.zero;
        c.ActivitySize=new Vector2(10,6);
        Check(c.Contains((Vector2)c.transform.position+new Vector2(4.9f,2.9f)),"corner formerly outside circle is now inside rectangular range");
    }
}
