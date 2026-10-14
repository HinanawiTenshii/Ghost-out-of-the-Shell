using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

// Copy into an isolated project's Assets/Editor and invoke -executeMethod
// SandwormApproachProbe.Run. This diagnostic deliberately reports, not asserts,
// approach outcomes so the same cases can be compared before and after a fix.
// SandwormApproachProbe.Verify runs the strict post-fix regression suite instead.
[InitializeOnLoad]
public static class SandwormApproachProbe
{
    const string Prefab = "Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab";
    const string PendingKey = "SandwormApproachProbe.Pending";
    const string VerifyKey = "SandwormApproachProbe.Verify";
    const float Dt = .025f;
    static readonly List<Object> owned = new List<Object>();
    static IEnumerator pending;
    static int lastFrame = -1, failedCases, checks;
    static bool strictMode;

    static SandwormApproachProbe() { EditorApplication.update += Tick; }
    static T Own<T>(T item) where T : Object { owned.Add(item); return item; }
    static string V(Vector2 point) { return point.ToString("F3"); }

    public static void Run()
    {
        SessionState.SetBool(VerifyKey, false);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    public static void Verify()
    {
        SessionState.SetBool(VerifyKey, true);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == lastFrame) return;
        lastFrame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(PendingKey, false))
            {
                SessionState.SetBool(PendingKey, false);
                strictMode = SessionState.GetBool(VerifyKey, false);
                checks = 0;
                pending = strictMode ? Regression() : Probe();
            }
            if (pending == null || pending.MoveNext()) return;
            pending = null;
            Cleanup();
            Debug.Log(strictMode ? "SANDWORM_APPROACH_VERIFY_PASS cases=24 checks=" + checks :
                "SANDWORM_APPROACH_PROBE_DONE cases=12 failures=" + failedCases);
            EditorApplication.Exit(0);
        }
        catch (Exception error)
        {
            pending = null;
            Cleanup();
            Debug.LogException(error);
            EditorApplication.Exit(1);
        }
    }

    static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new Exception("Sandworm approach regression: " + label);
    }

    static SandwormEruptionAttack CreateWorm(float lockDistance, bool faceUp = false)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Check(prefab != null, "fixture prefab exists");
        var root = Own(Object.Instantiate(prefab));
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        var movement = root.GetComponent<SandwormBossPrototype>();
        // Match BossTest's arena. The newer 1.3x body-length landing span no
        // longer fits both sides of the old prefab-sized center fixture.
        var arena = new SerializedObject(movement);
        arena.FindProperty("wanderSize").vector2Value = new Vector2(30f, 20f);
        arena.ApplyModifiedPropertiesWithoutUndo();
        movement.Head.position = Vector3.zero;
        movement.Head.rotation = Quaternion.Euler(0f, 0f, faceUp ? 90f : 0f);
        movement.ResetPose();
        var attack = root.GetComponent<SandwormEruptionAttack>();
        var serialized = new SerializedObject(attack);
        serialized.FindProperty("targetLockDistance").floatValue = lockDistance;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return attack;
    }

    // Reflection keeps the non-asserting Run entry usable against pre-fix source,
    // where the endpoint-specific arrival helpers have not been introduced yet.
    static bool DirectedQuery(SandwormBossPrototype movement, string name, Vector2 point)
    {
        var method = typeof(SandwormBossPrototype).GetMethod(name, new[] { typeof(Vector2) });
        Check(method != null, "directed endpoint helper exists: " + name);
        return (bool)method.Invoke(movement, new object[] { point });
    }

    static bool Arrived(SandwormBossPrototype movement, Vector2 point)
    {
        return DirectedQuery(movement, "HasArrivedAtDirectedPoint", point);
    }

    static bool Approaching(SandwormBossPrototype movement, Vector2 point)
    {
        return DirectedQuery(movement, "IsApproachingDirectedPoint", point);
    }

    static void VerifyDirectedArrival()
    {
        var attack = CreateWorm(4f);
        var movement = attack.GetComponent<SandwormBossPrototype>();
        Vector2 near = new Vector2(.02f, .02f);
        Check(movement.TrySetEruptionDestination(near), "near-snap destination accepted");
        Check(movement.CurrentDestination == near && movement.HeadPosition == near,
            "near-snap updates both directed destination and head position");
        Check(Arrived(movement, near) && !Approaching(movement, near), "near-snap is arrived, not approaching");
        Check(!Arrived(movement, near + Vector2.right), "arrival does not belong to another endpoint");
        movement.Head.position += Vector3.right * 2f;
        Check(!Arrived(movement, near), "moving head invalidates stale arrived flag");
        Check(!Approaching(movement, near), "stale arrival without a route is not approaching");
        Vector2 next = movement.HeadPosition + new Vector2(.02f, .02f);
        Check(movement.TrySetEruptionDestination(next) && Arrived(movement, next), "near-snap replaces previous goal");
        Check(movement.CurrentDestination == next && !Arrived(movement, near), "old near-snap destination is forgotten");
        movement.ResetPose();
        Check(!movement.ArrivedAtDirectedDestination && !Arrived(movement, next), "ResetPose clears directed arrival");
        Check(!Approaching(movement, next), "ResetPose clears active directed route");
        Check(movement.TrySetEruptionDestination(next), "reinitialized movement accepts same nearby point");
        Check(Arrived(movement, next), "reinitialization cannot resurrect an older destination");
        Object.DestroyImmediate(attack.gameObject);

        attack = CreateWorm(4f, true);
        movement = attack.GetComponent<SandwormBossPrototype>();
        Vector2 down = new Vector2(0f, -4f);
        Check(movement.TrySetEruptionDestination(down), "up-facing head routes to directly lower endpoint");
        Check(Approaching(movement, down) && !Approaching(movement, down + Vector2.right),
            "route ownership matches its actual endpoint");
        for (int frame = 0; frame < 400 && !Arrived(movement, down); frame++)
        {
            Vector2 head = movement.HeadPosition;
            Vector2 tail = movement.Tail.position;
            movement.Advance(Dt);
            Check(Vector2.Distance(head, movement.HeadPosition) <= movement.WorldMoveSpeed * Dt + .005f,
                "reverse alignment does not teleport head");
            Check(Vector2.Distance(tail, movement.Tail.position) <= movement.WorldMoveSpeed * Dt + .2f,
                "reverse alignment preserves continuous tail history");
        }
        Check(Arrived(movement, down), "reverse alignment arrives within ten seconds");
        Check(Vector2.Angle(movement.Head.right, Vector2.up) < 1f, "reverse alignment ends facing up");
        Object.DestroyImmediate(attack.gameObject);

        var blocker = Own(new GameObject("Approach endpoint Blocks fixture", typeof(BoxCollider2D)));
        blocker.layer = LayerMask.NameToLayer("Blocks");
        Check(blocker.layer >= 0, "Blocks layer exists");
        blocker.transform.position = new Vector3(8f, 6f, 0f);
        blocker.GetComponent<BoxCollider2D>().size = Vector2.one;
        Physics2D.SyncTransforms();
        attack = CreateWorm(4f);
        movement = attack.GetComponent<SandwormBossPrototype>();
        Check(!movement.TrySetEruptionDestination(blocker.transform.position), "blocked endpoint remains rejected");
        Check(!attack.TryLockAttack(blocker.transform.position), "blocked endpoint cannot start damage warning");
        Object.DestroyImmediate(attack.gameObject);
        Object.DestroyImmediate(blocker);

        var wall = Own(new GameObject("Approach full-width Blocks fixture", typeof(BoxCollider2D)));
        wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = new Vector3(0f, -5f, 0f);
        wall.GetComponent<BoxCollider2D>().size = new Vector2(100f, 1f);
        Physics2D.SyncTransforms();
        attack = CreateWorm(4f, true);
        movement = attack.GetComponent<SandwormBossPrototype>();
        Vector2 acrossWall = new Vector2(0f, -10f);
        Check(movement.IsDestinationAllowed(acrossWall), "wall fixture target itself has clearance");
        Check(!movement.TrySetEruptionDestination(acrossWall), "tight/reverse fallback cannot pass through Blocks wall");
        Object.DestroyImmediate(attack.gameObject);
        Object.DestroyImmediate(wall);
        Physics2D.SyncTransforms();
        Debug.Log("SANDWORM_APPROACH_VERIFY directed-arrival/reversal/Blocks passed");
    }

    static IEnumerator Regression()
    {
        Time.timeScale = 0f;
        VerifyDirectedArrival();
        string[] labels = { "right", "left", "up", "down", "diagonal", "tiny" };
        Vector2[] offsets = {
            new Vector2(4f, 0f), new Vector2(-4f, 0f), new Vector2(0f, 4f),
            new Vector2(0f, -4f), new Vector2(3f, 3f), new Vector2(.02f, .02f)
        };
        foreach (float lockDistance in new[] { 4f, 8f })
        foreach (float warmup in new[] { .3f, .8f })
        for (int which = 0; which < offsets.Length; which++)
        {
            Random.InitState(7491);
            string name = "lock" + lockDistance + "_" + warmup.ToString("F1") + "_" + labels[which];
            var playerRoot = Own(new GameObject("Approach regression controlled player"));
            playerRoot.transform.position = new Vector3(8f, 6f, 0f);
            var player = playerRoot.AddComponent<ZeldaFourWayMover>();
            ZeldaRuntimeRegistry.ClaimControlledMover(player);
            var attack = CreateWorm(lockDistance);
            var movement = attack.GetComponent<SandwormBossPrototype>();
            bool firstPulse = false;
            attack.SegmentPulse += (point, power, index, landing) => {
                if (landing || index != 0) return;
                Check(Vector2.Distance(movement.HeadPosition, attack.LockedAttackPoint) < .01f,
                    name + ": real head is at locked point before first damage");
                Check(point == attack.LockedAttackPoint, name + ": first damage uses locked endpoint");
                firstPulse = true;
            };
            Check(attack.TryTrackPlayerAttack(), name + ": distant tracking starts");
            Check(!attack.IsTargetLocked, name + ": fixture remains unlocked before warmup");
            for (int frame = 0; frame < Mathf.RoundToInt(warmup / Dt); frame++) movement.Advance(Dt);
            player.transform.position = movement.HeadPosition + offsets[which];
            Vector2 retargetHead = movement.HeadPosition, retargetTail = movement.Tail.position;
            attack.AdvanceAttack(Dt);
            Check(Vector2.Distance(retargetHead, movement.HeadPosition) <= .16f,
                name + ": retarget cannot snap farther than nearby alignment tolerance");
            Check(Vector2.Distance(retargetTail, movement.Tail.position) <= .2f,
                name + ": retarget preserves tail pose");
            int stationary = 0, wrongGoalStreak = 0, longestWrongGoal = 0, correctGoalStreak = 0;
            float elapsed = Dt, maxHeadStep = 0f, maxTailStep = 0f;
            for (int frame = 0; frame < 400 && attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching; frame++)
            {
                Vector2 head = movement.HeadPosition, tail = movement.Tail.position;
                movement.Advance(Dt);
                bool readyArrival = attack.IsTargetLocked && Arrived(movement, attack.LockedAttackPoint) &&
                    Object.FindObjectsOfType<SandwormEruptionSite>().Length >= 2;
                attack.AdvanceAttack(Dt);
                if (readyArrival)
                    Check(attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing && firstPulse,
                        name + ": arrival hands off and emits head pulse in the same frame, with no warning pause");
                elapsed += Dt;
                float headStep = Vector2.Distance(head, movement.HeadPosition);
                float tailStep = Vector2.Distance(tail, movement.Tail.position);
                maxHeadStep = Mathf.Max(maxHeadStep, headStep);
                maxTailStep = Mathf.Max(maxTailStep, tailStep);
                Check(headStep <= movement.WorldMoveSpeed * Dt + .16f, name + ": head motion remains continuous");
                Check(tailStep <= movement.WorldMoveSpeed * Dt + .2f, name + ": tail motion remains continuous");
                bool stopped = headStep < .00001f;
                if (stopped) stationary++;
                bool wrongGoal = Vector2.Distance(movement.HeadPosition, attack.LockedAttackPoint) > .01f;
                wrongGoalStreak = stopped && wrongGoal ? wrongGoalStreak + 1 : 0;
                correctGoalStreak = stopped && !wrongGoal && attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching
                    ? correctGoalStreak + 1 : 0;
                longestWrongGoal = Mathf.Max(longestWrongGoal, wrongGoalStreak);
                Check(wrongGoalStreak * Dt <= .25f, name + ": cannot wait at stale goal longer than .25 seconds");
                Check(correctGoalStreak <= 1, name + ": no stationary pause at correct emergence point");
                if (attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching)
                    Check(attack.EmergencePulses == 0 && attack.LandingPulses == 0,
                        name + ": no damage before actual arrival and passage");
            }
            LogState(name, "strict-result", elapsed, attack, movement, player,
                stationary, longestWrongGoal, maxHeadStep, maxTailStep);
            Check(attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing && elapsed <= 10.05f,
                name + ": viable retarget reaches passage within ten seconds");
            Check(firstPulse && attack.EmergencePulses == 1, name + ": passage begins with exactly one head pulse");
            Check(Vector2.Distance(movement.HeadPosition, attack.LockedAttackPoint) < .01f,
                name + ": passage starts at locked emergence point");
            attack.CancelAttack();
            Object.DestroyImmediate(attack.gameObject);
            Object.DestroyImmediate(playerRoot);
            yield return null;
        }
    }

    static void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        Time.timeScale = 1f;
    }

    static void LogState(string name, string stage, float time, SandwormEruptionAttack attack,
        SandwormBossPrototype movement, ZeldaFourWayMover player, int stationary, int longest,
        float maxHeadStep, float maxTailStep)
    {
        Debug.Log("SANDWORM_APPROACH_PROBE case=" + name + " stage=" + stage +
            " time=" + time.ToString("F3") + " phase=" + attack.Phase +
            " locked=" + attack.IsTargetLocked + " head=" + V(movement.HeadPosition) +
            " target=" + V(player.transform.position) + " lockedPoint=" + V(attack.LockedAttackPoint) +
            " goal=" + V(movement.CurrentDestination) + " hasGoal=" + movement.HasDestination +
            " arrived=" + movement.ArrivedAtDirectedDestination +
            " goalError=" + Vector2.Distance(movement.CurrentDestination, attack.LockedAttackPoint).ToString("F4") +
            " headError=" + Vector2.Distance(movement.HeadPosition, attack.LockedAttackPoint).ToString("F4") +
            " stationaryFrames=" + stationary + " longestStationary=" + longest +
            " maxHeadStep=" + maxHeadStep.ToString("F4") + " maxTailStep=" + maxTailStep.ToString("F4"));
    }

    static IEnumerator Probe()
    {
        Time.timeScale = 0f;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if (prefab == null) throw new Exception("Missing sandworm fixture prefab: " + Prefab);
        string[] labels = { "right", "left", "up", "down", "diagonal", "tiny" };
        Vector2[] offsets = {
            new Vector2(4f, 0f), new Vector2(-4f, 0f), new Vector2(0f, 4f),
            new Vector2(0f, -4f), new Vector2(3f, 3f), new Vector2(.02f, .02f)
        };
        float[] warmups = { .3f, .8f };
        failedCases = 0;
        foreach (float warmup in warmups)
        {
            for (int which = 0; which < offsets.Length; which++)
            {
                Random.InitState(7491);
                var playerRoot = Own(new GameObject("Approach probe controlled player"));
                playerRoot.transform.position = new Vector3(8f, 6f, 0f);
                var player = playerRoot.AddComponent<ZeldaFourWayMover>();
                ZeldaRuntimeRegistry.ClaimControlledMover(player);
                var root = Own(Object.Instantiate(prefab));
                root.transform.position = Vector3.zero;
                root.transform.rotation = Quaternion.identity;
                var movement = root.GetComponent<SandwormBossPrototype>();
                var attack = root.GetComponent<SandwormEruptionAttack>();
                movement.Head.position = Vector3.zero;
                movement.Head.rotation = Quaternion.identity;
                movement.ResetPose();
                // Keep the requested fixture threshold independent of prefab overrides.
                var serialized = new SerializedObject(attack);
                serialized.FindProperty("targetLockDistance").floatValue = 8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                bool started = attack.TryTrackPlayerAttack();
                string name = warmup.ToString("F1") + "_" + labels[which];
                Debug.Log("SANDWORM_APPROACH_PROBE case=" + name + " started=" + started +
                    " lockDistance=" + attack.TargetLockDistance + " speed=" + movement.WorldMoveSpeed);

                // Accumulate movement before observing the target again, so the target
                // relocation happens while still tracking rather than after proximity lock.
                for (int frame = 0; frame < Mathf.RoundToInt(warmup / Dt); frame++) movement.Advance(Dt);
                player.transform.position = movement.HeadPosition + offsets[which];
                attack.AdvanceAttack(Dt);
                int stationary = 0, streak = 0, longest = 0;
                float elapsed = Dt, maxHeadStep = 0f, maxTailStep = 0f;
                LogState(name, "retarget", elapsed, attack, movement, player, stationary, longest, maxHeadStep, maxTailStep);
                for (int frame = 0; frame < 820 && attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching; frame++)
                {
                    Vector2 head = movement.HeadPosition;
                    Vector2 tail = movement.Tail.position;
                    bool locked = attack.IsTargetLocked;
                    movement.Advance(Dt);
                    attack.AdvanceAttack(Dt);
                    elapsed += Dt;
                    float headStep = Vector2.Distance(head, movement.HeadPosition);
                    float tailStep = Vector2.Distance(tail, movement.Tail.position);
                    maxHeadStep = Mathf.Max(maxHeadStep, headStep);
                    maxTailStep = Mathf.Max(maxTailStep, tailStep);
                    if (headStep < .00001f && attack.Phase == SandwormEruptionAttack.AttackPhase.Approaching)
                    { stationary++; streak++; longest = Mathf.Max(longest, streak); }
                    else streak = 0;
                    if (locked != attack.IsTargetLocked || frame % 40 == 39)
                        LogState(name, "progress", elapsed, attack, movement, player, stationary, longest, maxHeadStep, maxTailStep);
                }
                bool traversing = attack.Phase == SandwormEruptionAttack.AttackPhase.Traversing;
                if (!started || !traversing) failedCases++;
                LogState(name, traversing ? "traversing" : "failed_or_cancelled", elapsed,
                    attack, movement, player, stationary, longest, maxHeadStep, maxTailStep);
                attack.CancelAttack();
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(playerRoot);
                yield return null;
            }
        }
    }
}
