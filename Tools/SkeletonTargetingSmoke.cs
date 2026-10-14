using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Native-Unity regression suite. Run only in the disposable validation project.
[InitializeOnLoad]
public static class SkeletonTargetingSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks, frame = -1;
    static IEnumerator suite;
    static SkeletonTargetingSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SkeletonTargetingSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SkeletonTargetingSmoke), false))
            {
                SessionState.SetBool(nameof(SkeletonTargetingSmoke), false);
                Time.timeScale = 0; suite = Verify();
            }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Debug.Log("SKELETON_TARGETING_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, Flags | BindingFlags.DeclaredOnly); if (f != null) return f; }
        throw new Exception("Missing field " + name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static object Call(object o, string name, params object[] args)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var m = t.GetMethod(name, Flags | BindingFlags.DeclaredOnly); if (m != null) return m.Invoke(o, args); }
        throw new Exception("Missing method " + name);
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static void Position(Component actor, Vector2 p)
    {
        actor.transform.position = p;
        var body = actor.GetComponent<Rigidbody2D>(); if (body) body.position = p;
        Physics2D.SyncTransforms(); ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
    }
    static ZeldaFourWayMover Actor(string name, Type dataType, Vector2 p)
    {
        var root = new GameObject(name); root.SetActive(false); root.transform.position = p;
        root.AddComponent(dataType);
        var mover = root.AddComponent<ZeldaFourWayMover>(); mover.enabled = false;
        root.SetActive(true); return mover;
    }
    static bool See(SkeletonCharacterAi ai, ZeldaFourWayMover target) => (bool)Call(ai, "CanSeeCharacter", target);
    static void Scan(SkeletonCharacterAi ai)
    { Call(ai, "BeginRecovery"); Call(ai, "UpdateTargetAndState", 0f); }
    // Pre-optimization full-scan visibility rules, independent of the new broad phase.
    static bool LegacySee(SkeletonCharacterAi ai, ZeldaFourWayMover target, EnergyCrystal leash)
    {
        if (!target || target.GetComponent<SkeletonZeldaCharacterData>() != null) return false;
        if (leash && !leash.Contains(target.transform.position)) return false;
        if (target.gameObject == ai.gameObject || !target.gameObject.activeInHierarchy ||
            ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject)) return false;
        var data = target.GetComponent<ZeldaCharacterData>();
        return data && !data.IsDead && data.CanBeDetectedByAi &&
            (bool)Call(ai, "CanSeeWorldPoint", (Vector2)target.transform.position, target.transform);
    }
    static IEnumerator Verify()
    {
        var body = Actor("Skeleton", typeof(SkeletonZeldaCharacterData), Vector2.zero);
        var ai = body.gameObject.AddComponent<SkeletonCharacterAi>();
        Set(ai, "visionRadius", 7f); Set(ai, "visionAngle", 360f);
        Set(ai, "peripheralVisionRadius", 1.7f); Set(ai, "peripheralVisionAngle", 240f);
        Set(ai, "enableAwarenessSound", false); Set(ai, "solidCollisionLayers", (LayerMask)0);
        int blocks = LayerMask.NameToLayer("Blocks"); if (blocks < 0) blocks = 8;
        Set(ai, "visionObstacleLayers", (LayerMask)(1 << blocks));

        var near = Actor("Near NPC (disabled player mover)", typeof(CivilianZeldaCharacterData), new Vector2(0, 2));
        var far = Actor("Far NPC", typeof(CivilianZeldaCharacterData), new Vector2(0, 4));
        var ally = Actor("Other skeleton", typeof(SkeletonZeldaCharacterData), new Vector2(0, .1f));
        var ghost = Actor("Ghost", typeof(GhostZeldaCharacterData), new Vector2(.1f, 0));
        var sleeper = Actor("Unloaded actor", typeof(CivilianZeldaCharacterData), new Vector2(.2f, 0));
        sleeper.gameObject.SetActive(false);
        for (int i = 0; i < 150; i++)
        {
            var dormant = Actor("Dormant " + i, typeof(CivilianZeldaCharacterData), new Vector2(100 + i, 100));
            dormant.gameObject.SetActive(false);
        }
        Scan(ai); Check(ai.CurrentTarget == near && ai.CurrentState == ZeldaAiState.Hostile, "nearest living ordinary NPC acquired immediately");
        Check(!See(ai, body) && !See(ai, ally) && !See(ai, ghost) && !See(ai, sleeper), "self, skeletons, ghosts and unloaded actors excluded");
        var buffer = (List<ZeldaFourWayMover>)Field(ai, "nearbyEnemies").GetValue(ai);
        Check(buffer.Count < 10 && ZeldaRuntimeRegistry.Movers.Count > 150, "broad phase excludes dormant and distant actors");
        var reused = buffer;
        Position(far, new Vector2(0, 1)); Call(ai, "UpdateTargetAndState", 0f);
        Check(ai.CurrentTarget == near, "retains visible target even when another is nearer");
        near.gameObject.SetActive(false); Call(ai, "UpdateTargetAndState", 0f);
        Check(ai.CurrentTarget == far, "unloaded current target replaced immediately");
        Check(ReferenceEquals(reused, Field(ai, "nearbyEnemies").GetValue(ai)), "query list reused across scans");

        far.gameObject.SetActive(false); Scan(ai);
        var reloaded = sleeper.gameObject.AddComponent<ZeldaCharacterAiBase>(); Set(reloaded, "enableAwarenessSound", false);
        // Populate the inactive snapshot, then activate in the SAME frame.
        Call(ai, "QueryNearbyVisionCandidates", new List<ZeldaFourWayMover>());
        sleeper.gameObject.SetActive(true); Scan(ai);
        Check(ai.CurrentTarget == sleeper, "streamed AI activation invalidates shared snapshot in same frame");
        sleeper.gameObject.SetActive(false);
        near.gameObject.SetActive(true); Position(near, new Vector2(0, 7)); Scan(ai);
        Check(ai.CurrentTarget == near, "exact main vision boundary included");
        Position(near, new Vector2(0, 7.01f)); Scan(ai); Check(ai.CurrentTarget == null, "outside main radius rejected");
        Check(CharacterPotionBuffs.TryApply(body.CharacterData, AlchemyPotionKind.Sight), "sight potion accepted");
        Position(near, new Vector2(0, 8.7f)); Scan(ai); Check(ai.CurrentTarget == near, "query includes 125 percent potion radius");
        Position(near, new Vector2(0, 8.76f)); Scan(ai); Check(ai.CurrentTarget == null, "potion range remains exact");
        var buffs = body.GetComponent<CharacterPotionBuffs>(); var saved = buffs.CaptureRuntimeState();
        saved.remaining[(int)AlchemyPotionKind.Sight] = 0; buffs.ApplyRuntimeState(saved);
        Set(ai, "visionRadius", 1f); Set(ai, "peripheralVisionRadius", 5f); Set(ai, "peripheralVisionAngle", 360f);
        Position(near, new Vector2(4.9f, 0)); Scan(ai); Check(ai.CurrentTarget == near, "larger peripheral radius included in broad phase");
        Set(ai, "visionRadius", 7f); Set(ai, "peripheralVisionRadius", 1.7f);
        Position(near, new Vector2(0, 3));
        CharacterPotionBuffs.TryApply(near.CharacterData, AlchemyPotionKind.Veil);
        Scan(ai); Check(ai.CurrentTarget == near, "skeleton still detects veil potion");
        var wall = new GameObject("LOS wall"); wall.layer = blocks; wall.transform.position = new Vector3(0, 1.5f);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(2, .3f); Physics2D.SyncTransforms();
        Scan(ai); Check(ai.CurrentTarget == null, "Blocks wall still occludes target");
        far.gameObject.SetActive(true); Position(far, new Vector2(4, 0)); Scan(ai);
        Check(ai.CurrentTarget == far, "blocked nearer target does not hide farther visible target");
        wall.SetActive(false); far.gameObject.SetActive(false);

        var crystal = new GameObject("Anchor").AddComponent<EnergyCrystal>();
        crystal.ActivitySize = new Vector2(8, 8); crystal.BindSkeleton(body.CharacterData as SkeletonZeldaCharacterData);
        Scan(ai); Check(ai.CurrentTarget == near, "bound target inside rectangle acquired");
        Position(near, new Vector2(0, 4.2f)); Call(ai, "UpdateTargetAndState", 0f);
        Check(ai.CurrentTarget == null && ai.CurrentState == ZeldaAiState.Recovery, "crossing leash immediately returns, not searches");
        Scan(ai); Check(ai.CurrentTarget == null, "new target outside leash rejected");
        crystal.enabled = false; Scan(ai); Check(ai.CurrentTarget == near, "disabled crystal removes restriction immediately");
        crystal.enabled = true;

        var otherScene = SceneManager.CreateScene("Other gameplay scene");
        var foreign = Actor("Other scene actor", typeof(CivilianZeldaCharacterData), new Vector2(.3f, .3f));
        SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        Check(!See(ai, foreign), "additively loaded scene actor excluded");
        foreign.gameObject.SetActive(false); near.gameObject.SetActive(false);
        // Seeded full-scan equivalence across sparse/dense, bound/unbound and walls.
        var random = new System.Random(7419); var candidates = new List<ZeldaFourWayMover>();
        for (int i = 0; i < 24; i++) candidates.Add(Actor("Random " + i,
            i % 6 == 0 ? typeof(SkeletonZeldaCharacterData) : typeof(CivilianZeldaCharacterData), Vector2.one * 30));
        for (int trial = 0; trial < 80; trial++)
        {
            crystal.enabled = trial % 2 == 0; wall.SetActive(trial % 3 == 0);
            foreach (var candidate in candidates)
            {
                Position(candidate, new Vector2((float)random.NextDouble() * 24 - 12, (float)random.NextDouble() * 24 - 12));
                candidate.gameObject.SetActive(random.Next(4) != 0);
            }
            ZeldaRuntimeRegistry.InvalidateSpatialSnapshot(); Physics2D.SyncTransforms();
            ZeldaFourWayMover expected = null; float nearest = float.MaxValue;
            foreach (var candidate in ZeldaRuntimeRegistry.Movers)
            {
                bool visible = LegacySee(ai, candidate, crystal.enabled ? crystal : null);
                Check(See(ai, candidate) == visible, "visibility parity " + trial + " / " + candidate.name);
                float distance = ((Vector2)candidate.transform.position - body.GetComponent<Rigidbody2D>().position).sqrMagnitude;
                if (visible && distance < nearest) { nearest = distance; expected = candidate; }
            }
            Scan(ai); Check(ai.CurrentTarget == expected, "nearest full-scan target parity " + trial);
        }
        foreach (var candidate in candidates) candidate.gameObject.SetActive(false);
        crystal.enabled = false; wall.SetActive(false); near.gameObject.SetActive(true); Position(near, new Vector2(0, 3));
        Scan(ai); Check(ai.CurrentTarget == near, "acquires after randomized cases");
        near.enabled = true; ZeldaRuntimeRegistry.ClaimControlledMover(near); Scan(ai);
        Check(ai.CurrentTarget == near, "player-controlled body remains valid");
        Check(ai.CaptureSaveState().sharedPlayerPhase == 0, "independent hostility remains outside shared timer");
        Position(near, new Vector2(100, 100)); Set(ai, "npcThreatLastSeenAt", Time.time - 10);
        Call(ai, "UpdateTargetAndState", .02f); Check(ai.CurrentState == ZeldaAiState.Search, "individual lost-target timeout still searches");
        Set(ai, "investigationElapsed", 12f); Call(ai, "UpdateTargetAndState", .02f);
        Check(ai.CurrentState == ZeldaAiState.Recovery, "individual search expiry still returns");
        yield return null;
    }
}
