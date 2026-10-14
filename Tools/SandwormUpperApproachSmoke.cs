using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SandwormUpperApproachSmoke
{
    static IEnumerator tests;
    static int frame = -1, failures, checks;
    static bool strict;
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static SandwormUpperApproachSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("UpperApproach.Strict", false); Start(); }
    public static void VerifyFix() { SessionState.SetBool("UpperApproach.Strict", true); Start(); }
    static void Start() { SessionState.SetBool("UpperApproach.Pending", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("UpperApproach.Pending", false))
            { SessionState.SetBool("UpperApproach.Pending", false); strict = SessionState.GetBool("UpperApproach.Strict", false); tests = Verify(); }
            if (tests == null || tests.MoveNext()) return;
            tests = null; Cleanup(); Debug.Log("SANDWORM_UPPER_APPROACH_DONE failures=" + failures + " checks=" + checks + " strict=" + strict); EditorApplication.Exit(0);
        }
        catch (Exception e) { tests = null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup() { foreach (var go in owned) if (go != null) Object.DestroyImmediate(go); owned.Clear(); }
    static void Set(object obj, string key, object value) => obj.GetType().GetField(key, Flags).SetValue(obj, value);
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static IEnumerator Verify()
    {
        SceneManager.SetActiveScene(SceneManager.CreateScene("Upper approach diagnostic")); Time.timeScale = 0;
        foreach (float y in strict ? new[] { 0f, -14f, 16f, -24f, -26f } : new[] { -24f, -26f })
        foreach (float dx in new[] { 0f, .05f, 3f, -8f })
        foreach (float angle in new[] { -90f, -50f, 90f })
        {
            UnityEngine.Random.InitState(7491);
            var playerRoot = Own(new GameObject("Target")); playerRoot.transform.position = new Vector2(0, y);
            var player = playerRoot.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(player);
            player.GetComponent<Collider2D>().enabled = false;
            var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
            go.transform.position = Vector3.zero;
            var move = go.GetComponent<SandwormBossPrototype>(); Set(move, "wanderSize", new Vector2(30, 20));
            move.Head.position = new Vector2(dx, y + 9f); move.Head.rotation = Quaternion.Euler(0, 0, angle); move.ResetPose();
            var attack = go.GetComponent<SandwormEruptionAttack>();
            var watch = System.Diagnostics.Stopwatch.StartNew(); bool started = attack.TryTrackPlayerAttack();
            double startMs = watch.Elapsed.TotalMilliseconds, worst = 0; int updates = 0, stops = 0;
            Vector2 last = move.HeadPosition; string samples = "";
            while (updates < 1000 && attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching)
            {
                watch.Restart(); move.Advance(.025f); attack.AdvanceAttack(.025f);
                worst = Math.Max(worst, watch.Elapsed.TotalMilliseconds); updates++;
                if (strict)
                    Check(Vector2.Distance(last, move.HeadPosition) <= move.WorldMoveSpeed * .025f + .01f, "approach head never teleports");
                if ((last - move.HeadPosition).sqrMagnitude < .0000001f) stops++;
                last = move.HeadPosition;
                if (updates % 200 == 0) samples += " t=" + updates * .025f + " head=" + last + " goal=" + move.CurrentDestination + " locked=" + attack.IsTargetLocked;
            }
            bool pass = started && attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing;
            if (!pass) failures++;
            if (strict)
            {
                Check(pass && updates < 400 && stops == 0, "upper approach reaches exact emergence without stalls");
                Check(Vector2.Distance(move.HeadPosition, attack.LockedAttackPoint) < .01f, "damage starts at actual head position");
                Check(Vector2.Angle(move.Head.right, Vector2.up) < 2f, "upward breach tangent after real smooth turn");
                var path = (List<Vector2>)typeof(SandwormEruptionAttack).GetField("departure", Flags).GetValue(attack);
                Check(move.IsBuriedPathClear(path), "boundary-aware escape stays inside arena and clear of Blocks");
                Check((path[1] - path[0]).normalized.y < -.85f, "burial begins downward, not with a flipped pose");
                if (dx == 0 && angle == -90f)
                {
                    for (int n = 0; n < 2000 && attack.Phase != SandwormEruptionAttack.AttackPhase.Roaming; n++)
                    { move.Advance(.025f); attack.AdvanceAttack(.025f); }
                    Check(attack.Phase == SandwormEruptionAttack.AttackPhase.Roaming && attack.LandingPulses == 8,
                        "entire edge flight completes with eight burial pulses");
                    Vector2 before = move.HeadPosition; move.Advance(.025f);
                    Check(Vector2.Distance(move.HeadPosition, before) > .05f, "preplanned roaming continues without a landing pause");
                }
            }
            Debug.Log("UPPER_CASE y=" + y + " dx=" + dx + " angle=" + angle + " pass=" + pass + " start=" + started +
                " phase=" + attack.Phase + " seconds=" + updates*.025f + " stop=" + stops + " startMs=" + startMs.ToString("F2") +
                " worstMs=" + worst.ToString("F2") + samples);
            Cleanup(); yield return null;
        }
        if (!strict) yield break;

        // Truly impossible landings fail cheaply instead of being resubmitted
        // every tick. Moving to a different location immediately releases it.
        var target = Own(new GameObject("Unreachable target")); var actor = target.AddComponent<ZeldaFourWayMover>();
        actor.GetComponent<Collider2D>().enabled = false; ZeldaRuntimeRegistry.ClaimControlledMover(actor);
        var worm = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        worm.transform.position = Vector3.zero;
        var motion = worm.GetComponent<SandwormBossPrototype>(); Set(motion, "wanderSize", new Vector2(5, 5));
        motion.Head.position = new Vector2(0, 3); motion.Head.rotation = Quaternion.Euler(0, 0, -90); motion.ResetPose();
        var attackScript = worm.GetComponent<SandwormEruptionAttack>(); attackScript.TryTrackPlayerAttack();
        for (int n = 0; n < 40 && attackScript.Phase == SandwormEruptionAttack.AttackPhase.Approaching; n++)
        { motion.Advance(.025f); attackScript.AdvanceAttack(.025f); }
        Check(attackScript.Phase == SandwormEruptionAttack.AttackPhase.Roaming, "infeasible landing exits bounded planning");
        for (int n = 0; n < 60; n++) Check(!attackScript.TryTrackPlayerAttack(), "same rejected position obeys backoff");
        Set(motion, "wanderSize", new Vector2(30, 20)); target.transform.position = new Vector2(5, 0);
        Check(attackScript.TryTrackPlayerAttack(), "a changed viable target is not suppressed by old failure");
        Cleanup(); yield return null;

        // A fast path must not relax collision clearance or fake a viable exit.
        worm = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        worm.transform.position = Vector3.zero; motion = worm.GetComponent<SandwormBossPrototype>();
        Set(motion, "wanderSize", new Vector2(30, 20)); motion.ResetPose();
        var obstacle = Own(new GameObject("Solid Blocks barrier", typeof(BoxCollider2D))); obstacle.layer = LayerMask.NameToLayer("Blocks");
        obstacle.transform.position = new Vector2(0, -20); obstacle.GetComponent<BoxCollider2D>().size = new Vector2(100, 1);
        Physics2D.SyncTransforms();
        var departure = new List<Vector2>();
        Check(!motion.TryPlanBuriedDeparture(new Vector2(0, -26), 24f, departure), "blocked boundary exit cannot cross wall");
        obstacle.SetActive(false); Physics2D.SyncTransforms();
        Check(motion.TryPlanBuriedDeparture(new Vector2(0, -26), 24f, departure) && motion.IsBuriedPathClear(departure),
            "removing a dynamic wall permits valid exit without a stale negative cache");
        Cleanup(); yield return null;
    }
}
