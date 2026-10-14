using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

[InitializeOnLoad]
public static class SandwormEruptionSmoke
{
    const string Prefab = "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    static readonly List<Object> owned = new List<Object>();
    static int checks, lastFrame = -1;
    static IEnumerator pending;
    static SandwormEruptionSmoke() { EditorApplication.update += Tick; }
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception("Sandworm eruption: " + label); }
    static void Cleanup()
    {
        for (int i=owned.Count-1;i>=0;i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear(); Time.timeScale = 1;
    }
    static SandwormEruptionAttack Worm()
    {
        var root = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab)));
        root.transform.position = Vector3.zero;
        var move = root.GetComponent<SandwormBossPrototype>(); move.ResetPose();
        return root.GetComponent<SandwormEruptionAttack>();
    }
    static bool Lock(SandwormEruptionAttack attack)
    {
        Random.InitState(7491);
        return attack.TryLockAttack(new Vector2(8,6));
    }
    static void Step(SandwormEruptionAttack attack, float delta)
    {
        attack.GetComponent<SandwormBossPrototype>().Advance(delta);
        attack.AdvanceAttack(delta);
    }
    static void ConfigureFloat(Object component, string property, float value)
    {
        var serialized = new SerializedObject(component);
        var field = serialized.FindProperty(property);
        Check(field != null, "configurable property exists: " + property);
        field.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static SandwormEruptionSite[] Sites() { return Object.FindObjectsOfType<SandwormEruptionSite>(); }
    static ZeldaFourWayMover Player(Vector2 position)
    {
        var root = Own(new GameObject("Controlled player fixture")); root.transform.position = position;
        var mover = root.AddComponent<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        return mover;
    }
    static void VerifyTracking()
    {
        var player = Player(new Vector2(8,6));
        var tracked = Worm();
        var move = tracked.GetComponent<SandwormBossPrototype>();
        var health = player.GetComponent<ZeldaCharacterData>();
        int initialHealth = health.CurrentHealth;
        Random.InitState(7491);
        ConfigureFloat(tracked,"targetLockDistance",8f);
        Check(Mathf.Abs(tracked.TargetLockDistance-8f)<.001f, "configured lock distance is eight world units despite scaled worm");
        Check(tracked.TryTrackPlayerAttack() && !tracked.IsTargetLocked, "distant player starts a tracking approach");
        var sites = Sites();
        Check(sites.Length==1, "only emergence warning exists before target lock");
        var emergence = sites[0];
        for (int frame=0;frame<8;frame++)
        {
            player.transform.position += new Vector3(.03f,.02f,0);
            tracked.AdvanceAttack(.017f);
            Check(!tracked.IsTargetLocked && tracked.LockedAttackPoint==(Vector2)player.transform.position,
                "distant target point follows moving player every frame");
            Check((Vector2)emergence.transform.position==(Vector2)player.transform.position,
                "warning transform follows even between throttled route replans");
            Check(tracked.EmergencePulses==0 && tracked.LandingPulses==0 && health.CurrentHealth==initialHealth,
                "tracking cannot cause damage before target lock");
        }
        for (int frame=0;frame<800 && !tracked.IsTargetLocked && tracked.Phase==SandwormEruptionAttack.AttackPhase.Approaching;frame++)
        {
            Step(tracked,.025f);
            Check(tracked.EmergencePulses==0 && tracked.LandingPulses==0, "approach stays harmless until locked warning completes");
        }
        Check(tracked.IsTargetLocked && Vector2.Distance(move.HeadPosition,player.transform.position)<=tracked.TargetLockDistance+.001f,
            "moving worm locks only after reaching configured proximity");
        Vector2 locked = tracked.LockedAttackPoint;
        player.transform.position += Vector3.left*8;
        for (int frame=0;frame<800 && tracked.Phase==SandwormEruptionAttack.AttackPhase.Approaching && Sites().Length<2;frame++)
            Step(tracked,.025f);
        sites = Sites();
        Check(sites.Length==2, "valid locked plan reserves the fixed landing dust site");
        foreach(var fixedSite in sites)
            Check(fixedSite.HasWarning==((Vector2)fixedSite.transform.position==tracked.LockedAttackPoint),
                "only emergence has a warning; landing remains invisible until impact");
        Vector2 landing = tracked.LandingPoint;
        Check(Mathf.Abs(landing.x-locked.x)>=tracked.StraightBodyLength*1.15f-.001f && landing.y==locked.y,
            "tracked attack preserves full-body horizontal span after locking");
        var sitePositions = new Vector3[] { sites[0].transform.position, sites[1].transform.position };
        for (int frame=0;frame<800 && tracked.Phase==SandwormEruptionAttack.AttackPhase.Approaching;frame++)
        {
            Step(tracked,.025f);
            Check(tracked.LockedAttackPoint==locked && tracked.LandingPoint==landing, "player dodge after lock cannot retarget either endpoint");
            Check(sites[0].transform.position==sitePositions[0] && sites[1].transform.position==sitePositions[1],
                "emergence warning and hidden landing dust positions stay frozen after lock");
        }
        Check(tracked.Phase==SandwormEruptionAttack.AttackPhase.Traversing, "tracked plan reaches flight at final locked endpoint");
        tracked.CancelAttack(); tracked.gameObject.SetActive(false);
        Check(!CameraVisionStreamingRegion.HasActiveRegions, "tracked attack cancellation releases both warning leases");

        // Change the inspector threshold at runtime while the replan cooldown is still running.
        player.transform.position = new Vector3(8,6,0);
        var threshold = Worm(); ConfigureFloat(threshold,"targetLockDistance",5f);
        Check(threshold.TryTrackPlayerAttack() && !threshold.IsTargetLocked, "smaller configured threshold postpones lock");
        player.transform.position = new Vector3(6,4,0);
        threshold.AdvanceAttack(.02f);
        Check(!threshold.IsTargetLocked && threshold.LockedAttackPoint==(Vector2)player.transform.position,
            "point within eight units remains tracked outside smaller configured distance");
        ConfigureFloat(threshold,"targetLockDistance",8f);
        threshold.AdvanceAttack(.001f);
        Check(threshold.IsTargetLocked && threshold.LockedAttackPoint==new Vector2(6,4),
            "runtime threshold change locks immediately without waiting for the replan interval");
        Check(threshold.EmergencePulses==0, "locking does not emit damage before the head reaches the actual target");
        threshold.CancelAttack(); threshold.gameObject.SetActive(false);

        player.transform.position = new Vector3(8,6,0);
        var switched = Worm(); ConfigureFloat(switched,"targetLockDistance",.5f);
        Check(switched.TryTrackPlayerAttack(), "control-switch fixture starts tracking");
        var replacement = Player(new Vector2(-8,6));
        switched.AdvanceAttack(.016f);
        Check(!switched.IsTargetLocked && switched.LockedAttackPoint==(Vector2)replacement.transform.position &&
            (Vector2)Sites()[0].transform.position==(Vector2)replacement.transform.position,
            "tracking follows newly controlled character immediately");
        player.gameObject.SetActive(false); replacement.gameObject.SetActive(false);
        switched.AdvanceAttack(.016f);
        Check(switched.Phase==SandwormEruptionAttack.AttackPhase.Roaming && !switched.IsTargetLocked &&
            !CameraVisionStreamingRegion.HasActiveRegions && Sites().Length==0,
            "lost controlled target cancels approach and cleans up warnings and leases");
        switched.gameObject.SetActive(false);

        player.gameObject.SetActive(true); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        player.transform.position = new Vector3(8,6,0);
        var invalid = Worm(); ConfigureFloat(invalid,"targetLockDistance",.5f);
        Check(invalid.TryTrackPlayerAttack(), "invalid-retarget fixture starts from a valid distant point");
        var blocker = Own(new GameObject("Tracked target blocker",typeof(BoxCollider2D)));
        blocker.layer = LayerMask.NameToLayer("Blocks"); blocker.transform.position = new Vector3(6,4,0);
        blocker.GetComponent<BoxCollider2D>().size = Vector2.one;
        player.transform.position = blocker.transform.position;
        ConfigureFloat(invalid,"targetLockDistance",50f); Physics2D.SyncTransforms();
        VerifyInvalidRetarget(invalid, player, "blocked nearby");
        Check(!invalid.TryTrackPlayerAttack(), "blocked player cannot start a new tracking attack");
        invalid.gameObject.SetActive(false);
        Object.DestroyImmediate(blocker);

        // Use a fresh valid route: blocked-target cancellation must not silently skip the outside-area case.
        player.transform.position = new Vector3(8,6,0); Physics2D.SyncTransforms();
        var outside = Worm(); ConfigureFloat(outside,"targetLockDistance",.5f);
        Check(outside.TryTrackPlayerAttack(), "out-of-area fixture starts from a valid distant point");
        player.transform.position = new Vector3(1000,1000,0);
        ConfigureFloat(outside,"targetLockDistance",2000f);
        VerifyInvalidRetarget(outside, player, "out-of-area");
        Check(!outside.TryTrackPlayerAttack(), "out-of-area player cannot start a new tracking attack");
        outside.gameObject.SetActive(false); player.gameObject.SetActive(false);
    }
    static void VerifyInvalidRetarget(SandwormEruptionAttack attack, ZeldaFourWayMover player, string label)
    {
        Vector2 target = player.transform.position;
        var health = player.GetComponent<ZeldaCharacterData>();
        int initialHealth = health.CurrentHealth;
        // This is deliberately shorter than the 20-second approach timeout: once the
        // stale route ends or is blocked, the worm must cancel instead of standing still.
        for (int frame=0;frame<200 && attack.Phase==SandwormEruptionAttack.AttackPhase.Approaching;frame++)
        {
            Step(attack,.05f);
            Check(!attack.IsTargetLocked && attack.EmergencePulses==0 && attack.LandingPulses==0 && health.CurrentHealth==initialHealth,
                label+" retarget never locks or damages, even within configured lock distance");
            Check(attack.LockedAttackPoint==target, label+" retarget never reuses the stale route endpoint as its attack point");
            var activeSites = Sites();
            if (attack.Phase==SandwormEruptionAttack.AttackPhase.Approaching)
                Check(activeSites.Length==1 && (Vector2)activeSites[0].transform.position==target,
                    label+" approach has only the latest tracking warning and no landing warning");
            else
                Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && activeSites.Length==0,
                    label+" cancellation immediately removes all warnings");
        }
        Check(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && !CameraVisionStreamingRegion.HasActiveRegions,
            label+" approach cancels promptly when its old route ends instead of waiting for timeout");
    }
    static void VerifyDust()
    {
        var site = SandwormEruptionSite.Create(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Vector2.zero,2f);
        Own(site.gameObject);
        int renderers = site.GetComponentsInChildren<Renderer>().Length;
        Check(renderers<=64, "dust effect uses a bounded renderer pool");
        site.Pulse(true); site.AdvanceVisuals(.1f);
        Check(site.ActiveFragmentCount>=30, "first section raises substantial billowing dust and earth clods");
        int count = site.ActiveFragmentCount; site.AdvanceVisuals(0f);
        Check(site.ActiveFragmentCount==count, "paused visuals preserve the dust pool");
        for (int i=0;i<20;i++) site.Pulse(false);
        site.AdvanceVisuals(.01f);
        Check(site.ActiveFragmentCount<=60 && site.GetComponentsInChildren<Renderer>().Length==renderers,
            "successive body sections reuse fragments without allocating more renderers");
        site.Finish(); site.AdvanceVisuals(.9f);
        Check(!site.IsFinished && site.ActiveFragmentCount>0, "dust keeps drifting after the ground opening fades");
        Check(Mathf.Abs(site.Radius-2f)<.001f, "billowing dust never changes the exact warning damage radius");
        site.AdvanceVisuals(3f);
        Check(site.IsFinished && site.ActiveFragmentCount==0, "site completes only once lingering dust has dissipated");
        site.Release();
        Check(!CameraVisionStreamingRegion.HasActiveRegions, "dust-only site releases its streaming lease");
    }
    static ZeldaCharacterData Target(Vector2 position, bool extraCollider)
    {
        var root = Own(new GameObject("Pulse target")); root.SetActive(false); root.transform.position = position;
        var data = root.AddComponent<ZeldaCharacterData>();
        var state = data.CaptureRuntimeState(); state.health = 200; state.currentHealth = 200; data.ApplyRuntimeState(state);
        root.AddComponent<BoxCollider2D>().size = Vector2.one*.2f;
        if (extraCollider)
        {
            var child = new GameObject("Second collider"); child.transform.SetParent(root.transform,false);
            child.AddComponent<CircleCollider2D>().radius = .1f;
        }
        root.SetActive(true);
        // Awake initializes health, so restore after activation as well.
        data.ApplyRuntimeState(state);
        return data;
    }
    public static void Run()
    {
        SessionState.SetBool("SandwormEruptionSmoke.Pending", true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("SandwormEruptionSmoke.Pending",false))
            { SessionState.SetBool("SandwormEruptionSmoke.Pending",false); pending = Verify(); }
            if (pending == null || pending.MoveNext()) return;
            pending = null; Cleanup(); Debug.Log("SANDWORM_ERUPTION_PASS checks="+checks); EditorApplication.Exit(0);
        }
        catch(Exception error) { pending = null; Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        var attack = Worm();
        var move = attack.GetComponent<SandwormBossPrototype>();
        var data = attack.GetComponent<SandwormZeldaCharacterData>();
        int initialWormHealth = data.CurrentHealth;
        Check(attack != null && Lock(attack), "plans fixed target, horizontal landing and safe buried departure");
        Check(CameraTemporaryFraming.GetMultiplier(attack.gameObject.scene)==1f, "approach warning does not widen camera or vision yet");
        Check(attack.LockedAttackPoint.y == attack.LandingPoint.y, "endpoints are on the same horizontal line");
        Check(Mathf.Abs(attack.LandingPoint.x-attack.LockedAttackPoint.x)>=attack.StraightBodyLength*1.15f-.001f,"horizontal span exceeds full straight body including head/tail by at least 15 percent");
        Debug.Log("SANDWORM_ERUPTION_SPAN straightBody="+attack.StraightBodyLength+" horizontal="+Mathf.Abs(attack.LandingPoint.x-attack.LockedAttackPoint.x));
        Vector2 locked = attack.LockedAttackPoint;
        var nearExit = Target(locked, true);
        var nearLanding = Target(attack.LandingPoint, true);
        var corner = Target(locked + Vector2.one * (attack.DamageRadius*.9f), false);
        var outside = Target(locked + Vector2.up * (attack.DamageRadius+.3f), false);
        int outCount=0, inCount=0, outPower=0, inPower=0;
        attack.SegmentPulse += (point,power,index,landing) => {
            if(landing) { inCount++; inPower+=power; Check(point == attack.LandingPoint, "landing pulse location"); }
            else { outCount++; outPower+=power; Check(point == locked, "emergence pulse location"); }
        };
        foreach(var site in Object.FindObjectsOfType<SandwormEruptionSite>())
            Check(Mathf.Abs(site.Radius-attack.DamageRadius)<.001f, "warning radius equals actual circular query radius");
        bool sawMixed = false, sawFlight = false, sawWholeBodyAboveGround = false, sawPreplanned = false;
        int movingFadeFrames = 0;
        float testTime = 0f;
        while(attack.Phase != SandwormEruptionAttack.AttackPhase.Roaming && testTime < 30)
        {
            bool wasSettling = attack.Phase == SandwormEruptionAttack.AttackPhase.Settling;
            Vector2 previousHead = move.HeadPosition;
            Vector2 previousHeading = move.Head.right;
            Vector2 previousTail = move.Tail.position;
            Step(attack,.037f); testTime += .037f;
            float expectedFraming = attack.Phase==SandwormEruptionAttack.AttackPhase.Traversing ? 1.3f : 1f;
            Check(Mathf.Abs(CameraTemporaryFraming.GetMultiplier(attack.gameObject.scene)-expectedFraming)<.0001f,
                "framing lasts only from head emergence through tail burial, not settling dust");
            if (wasSettling)
            {
                movingFadeFrames++;
                Check(Vector2.Distance(previousHead,move.HeadPosition) > move.WorldMoveSpeed*.037f*.95f,
                    "head continues moving every frame while landing dust fades, with no settling pause");
                Check(Vector2.Angle(previousHeading,move.Head.right)<12f,"handoff does not reset head direction");
                Check(Vector2.Distance(previousTail,move.Tail.position)<move.WorldMoveSpeed*.037f*1.2f,
                    "exact curved history prevents tail snapping on handoff");
            }
            Check(attack.LockedAttackPoint == locked, "locked point never moves");
            foreach(var collider in attack.GetComponentsInChildren<Collider2D>()) Check(!collider.enabled,"all worm colliders stay disabled");
            if(attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing)
            {
                sawFlight = true;
                sawPreplanned |= attack.LandingPulses==0 && move.HasDestination;
                int surface=0, buried=0;
                foreach(var visual in attack.GetComponentsInChildren<SandwormSegmentVisual>())
                {
                    if(visual.HasUndergroundOverlay) buried++; else
                    { surface++; Check(visual.transform.position.y >= locked.y-.001f, "all exposed sections are above the ground line"); }
                }
                sawMixed |= surface>0 && buried>0;
                sawWholeBodyAboveGround |= surface==8;
            }
        }
        Check(testTime < 30 && sawFlight && sawMixed, "full sequential flight returns to roaming");
        Check(sawWholeBodyAboveGround,"longer leap has a visible fully-emerged body phase before landing");
        Check(sawPreplanned && movingFadeFrames>5,"roaming continuation is ready while airborne and never waits for dust fade");
        Check(outCount==8 && inCount==8 && outPower==45 && inPower==40, "exactly eight emergence and eight landing pulses, powers 10+7x5 and 8x5");
        Check(nearExit.CurrentHealth==155, "compound-collider emergence target damaged once per section");
        Check(nearLanding.CurrentHealth==160, "compound-collider landing target damaged once per section");
        Check(corner.CurrentHealth==200 && outside.CurrentHealth==200, "outside-circle and square-corner targets are not hit");
        Check(data.CurrentHealth==initialWormHealth, "does not damage itself");
        Check(attack.GetComponent<SortingGroup>().enabled && attack.GetComponent<SandwormStateController>().IsUnderground,"underground sorting restored");
        Check(!CameraVisionStreamingRegion.HasActiveRegions,"warning retention leases released");
        Vector2 before = move.HeadPosition; move.Advance(.1f);
        Check(Vector2.Distance(before,move.HeadPosition)>.1f,"roaming resumes from landing departure, not original position");
        attack.gameObject.SetActive(false);

        // Blocked endpoints must fail before any warning, damage or movement takeover.
        var obstacle = Own(new GameObject("Blocks test",typeof(BoxCollider2D)));
        obstacle.layer=LayerMask.NameToLayer("Blocks"); obstacle.transform.position=new Vector3(8,6,0);
        obstacle.GetComponent<BoxCollider2D>().size=Vector2.one*3;
        var blocked = Worm(); Physics2D.SyncTransforms();
        Check(!Lock(blocked) && blocked.Phase==SandwormEruptionAttack.AttackPhase.Roaming,"blocked target rejected");
        Object.DestroyImmediate(obstacle); blocked.gameObject.SetActive(false);

        var auto = Worm();
        var player = Player(new Vector2(8,6)).gameObject;
        ConfigureFloat(auto,"targetLockDistance",.5f);
        auto.AdvanceAttack(.01f);
        var autoMove = auto.GetComponent<SandwormBossPrototype>();
        int atApproach=-1;
        Random.InitState(1189);
        for(int frame=0;frame<2400;frame++)
        {
            Step(auto,.1f);
            if(autoMove.CompletedRoamingLegs<4) Check(auto.Phase==SandwormEruptionAttack.AttackPhase.Roaming,"no attack before four completed legs");
            if(auto.Phase!=SandwormEruptionAttack.AttackPhase.Roaming) { atApproach=autoMove.CompletedRoamingLegs; break; }
        }
        Check(atApproach>=4,"automatically approaches controlled player after four trips");
        Debug.Log("SANDWORM_ERUPTION_AUTO_TRACK completedLegs="+atApproach);
        Check(!auto.IsTargetLocked,"automatic approach tracks until it reaches the configured distance");
        player.transform.position += Vector3.left*.1f;
        auto.AdvanceAttack(.001f);
        Check(auto.LockedAttackPoint==(Vector2)player.transform.position,"automatic attack warning follows player before proximity lock");
        auto.enabled=false;
        Check(auto.Phase==SandwormEruptionAttack.AttackPhase.Roaming && !CameraVisionStreamingRegion.HasActiveRegions,"disable cancels warning and streaming lease");
        foreach(var collider in auto.GetComponentsInChildren<Collider2D>()) Check(!collider.enabled,"cancel keeps underground collision off");

        player.SetActive(false);
        VerifyTracking();
        VerifyDust();
        var interrupted = Worm(); Check(Lock(interrupted),"second attack can be planned");
        for(int frame=0; frame<300 && interrupted.Phase==SandwormEruptionAttack.AttackPhase.Approaching; frame++) Step(interrupted,.025f);
        Check(interrupted.Phase==SandwormEruptionAttack.AttackPhase.Traversing,"interrupt fixture reaches flight");
        interrupted.AdvanceAttack(.5f);
        Check(CameraTemporaryFraming.GetMultiplier(interrupted.gameObject.scene)==1.3f,"airborne attack holds camera framing");
        int heldPulses=interrupted.EmergencePulses; interrupted.AdvanceAttack(0f);
        Check(interrupted.EmergencePulses==heldPulses,"paused timeline cannot emit damage pulses");
        interrupted.enabled=false;
        Check(CameraTemporaryFraming.GetMultiplier(interrupted.gameObject.scene)==1f,"interrupted attack immediately releases camera framing");
        Check(interrupted.GetComponent<SortingGroup>().enabled && !CameraVisionStreamingRegion.HasActiveRegions,"mid-flight cancellation restores sorting and releases sites");
        foreach(var visual in interrupted.GetComponentsInChildren<SandwormSegmentVisual>()) Check(visual.HasUndergroundOverlay,"mid-flight cancellation removes all exposed sections");
        interrupted.gameObject.SetActive(false);

        var hitch = Worm(); Check(Lock(hitch),"hitch fixture plans attack");
        for(int frame=0; frame<300 && hitch.Phase==SandwormEruptionAttack.AttackPhase.Approaching; frame++) Step(hitch,.025f);
        Check(hitch.Phase==SandwormEruptionAttack.AttackPhase.Traversing,"hitch fixture reaches flight");
        hitch.AdvanceAttack(100f);
        Check(hitch.EmergencePulses==8 && hitch.LandingPulses==8,"large delta still processes every crossing exactly once");
        for (int frame=0;frame<40 && hitch.Phase!=SandwormEruptionAttack.AttackPhase.Roaming;frame++) Step(hitch,.1f);
        Check(hitch.Phase==SandwormEruptionAttack.AttackPhase.Roaming,"large delta completes back underground");
        yield return null;
    }

    public static void Preview()
    {
        try
        {
            var attack=Worm(); Check(Lock(attack),"preview has a valid attack");
            var camera=Own(new GameObject("Fixed eruption preview camera")).AddComponent<Camera>();
            camera.transform.position=new Vector3(0,2,-50); camera.orthographic=true; camera.orthographicSize=25;
            camera.aspect=1.5f; camera.backgroundColor=new Color(.045f,.065f,.16f); camera.clearFlags=CameraClearFlags.SolidColor;
            camera.allowHDR=false; camera.allowMSAA=false;
            var rt=Own(new RenderTexture(1200,800,24)); camera.targetTexture=rt;
            var image=Own(new Texture2D(1200,800,TextureFormat.RGBA32,false));
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","sandworm-eruption-frames")); Directory.CreateDirectory(folder);
            int frame=0, completed=0;
            while(frame<420)
            {
                Step(attack,1f/30f);
                SortingGroup.UpdateAllSortingGroups(); camera.Render(); RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,1200,800),0,0); image.Apply(); RenderTexture.active=null;
                File.WriteAllBytes(Path.Combine(folder,frame.ToString("D3")+".png"),image.EncodeToPNG());
                if(frame%20==0) Debug.Log("ERUPTION_PREVIEW frame="+frame+" phase="+attack.Phase+" exit="+attack.EmergencePulses+" landing="+attack.LandingPulses);
                frame++;
                if(attack.Phase==SandwormEruptionAttack.AttackPhase.Roaming && ++completed>24) break;
            }
            Check(attack.EmergencePulses==8 && attack.LandingPulses==8,"preview completed sequential traversal");
            camera.targetTexture=null; Cleanup(); Debug.Log("SANDWORM_ERUPTION_PREVIEW_PASS frames="+frame); EditorApplication.Exit(0);
        }
        catch(Exception error) { Cleanup(); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
