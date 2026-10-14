using System;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

// Run only in the isolated Unity smoke project, never loads or saves user scenes.
public static class SandwormRoamingSmoke
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab");
            if (!prefab) throw new Exception("Missing prefab");
            for (int seed = 0; seed < 8; seed++)
            {
                UnityEngine.Random.InitState(seed + 1024);
                var root = Object.Instantiate(prefab);
                var worm = root.GetComponent<SandwormBossPrototype>();
                worm.ResetPose();
                float minStep = float.MaxValue, maxAngle = 0, minLeg = float.MaxValue;
                double maxFrameMs = 0;
                int stalls = 0, transitions = 0;
                int centralFrames = 0, leftTurns = 0, rightTurns = 0;
                Vector2 minimumPosition = Vector2.zero, maximumPosition = Vector2.zero;
                Vector2 goal = Vector2.zero;
                bool hadGoal = false;
                var time = Stopwatch.StartNew();
                for (int i = 0; i < 15000; i++)
                {
                    Vector2 before = worm.HeadPosition;
                    Vector2 forward = worm.Head.TransformVector(Vector3.right).normalized;
                    long tick = Stopwatch.GetTimestamp();
                    worm.Advance(.02f);
                    maxFrameMs = Math.Max(maxFrameMs, (Stopwatch.GetTimestamp() - tick) * 1000.0 / Stopwatch.Frequency);
                    float distance = Vector2.Distance(before, worm.HeadPosition);
                    minStep = Mathf.Min(minStep, distance);
                    if (distance < .058f) stalls++;
                    maxAngle = Mathf.Max(maxAngle, Vector2.Angle(forward, worm.Head.TransformVector(Vector3.right)));
                    float turn = Vector2.SignedAngle(forward, worm.Head.TransformVector(Vector3.right));
                    if (turn > .03f) leftTurns++;
                    if (turn < -.03f) rightTurns++;
                    Vector2 position = worm.HeadPosition;
                    minimumPosition = Vector2.Min(minimumPosition, position);
                    maximumPosition = Vector2.Max(maximumPosition, position);
                    if (Mathf.Abs(position.x) < 18f && Mathf.Abs(position.y) < 13.5f) centralFrames++;
                    if (worm.HasDestination && (!hadGoal || goal != worm.CurrentDestination))
                    {
                        if (hadGoal) { minLeg = Mathf.Min(minLeg, Vector2.Distance(goal, worm.CurrentDestination)); transitions++; }
                        goal = worm.CurrentDestination; hadGoal = true;
                    }
                }
                Debug.Log($"SANDWORM_ROAM seed={seed} transitions={transitions} stalls={stalls} minStep={minStep} minLeg={minLeg} maxAngle={maxAngle} wallMs={time.ElapsedMilliseconds} maxFrameMs={maxFrameMs:F2} position={worm.HeadPosition}");
                Vector2 span = maximumPosition - minimumPosition;
                Debug.Log($"SANDWORM_VARIETY seed={seed} span={span} centralFrames={centralFrames} leftTurns={leftTurns} rightTurns={rightTurns}");
                if (stalls != 0 || transitions < 12 || minLeg < 13.499f || maxAngle > 3.1f)
                    throw new Exception("Continuous long-leg roaming regression at seed " + seed);
                Check(span.x > 35f && span.y > 22f && centralFrames > 100 && leftTurns > 100 && rightTurns > 100,
                    "roaming must cover a broad area, cross the interior and turn both ways, rather than settle into an orbit");
                Object.DestroyImmediate(root);
            }
            TestConstraints(prefab);
            Debug.Log("SANDWORM_ROAM_PASS");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Sandworm roaming: " + message);
    }

    static void TestConstraints(GameObject prefab)
    {
        var root = Object.Instantiate(prefab);
        var worm = root.GetComponent<SandwormBossPrototype>();
        worm.ResetPose();
        typeof(SandwormBossPrototype).GetField("minDestinationDistance", Fields).SetValue(worm, 1000f);
        Check(!worm.TryPickDestination(), "impossible minimum is never relaxed to a short trip");
        Vector2 origin = worm.HeadPosition;
        for (int i = 0; i < 100; i++) worm.Advance(.02f);
        Check(worm.HeadPosition == origin, "impossible route waits safely, without snapping or tight circling");
        Object.DestroyImmediate(root);

        var wall = new GameObject("Isolated live Blocks test");
        wall.layer = LayerMask.NameToLayer("Blocks");
        var box = wall.AddComponent<BoxCollider2D>();
        box.size = new Vector2(3, 3);
        wall.transform.position = new Vector3(7, 4);
        Physics2D.SyncTransforms();
        root = Object.Instantiate(prefab);
        worm = root.GetComponent<SandwormBossPrototype>();
        worm.ResetPose();
        UnityEngine.Random.InitState(400);
        float moved = 0f;
        for (int i = 0; i < 3000; i++)
        {
            Vector2 before = worm.HeadPosition;
            worm.Advance(.02f);
            moved += Vector2.Distance(before, worm.HeadPosition);
            Check(worm.IsDestinationAllowed(worm.HeadPosition), "entire moving curve respects Blocks clearance");
            Check(Vector2.Distance(box.ClosestPoint(worm.HeadPosition), worm.HeadPosition) >= 2.849f,
                "curved path, not just the goal or chord, stays clear of wall");
        }
        Check(moved > 120f, "wall fixture still makes useful long-distance progress");
        Vector2 frozen = worm.HeadPosition;
        worm.SetMovementEnabled(false); worm.Advance(.5f);
        Check(worm.HeadPosition == frozen, "pause preserves route and pose");
        worm.SetMovementEnabled(true); worm.Advance(0);
        Check(worm.HeadPosition == frozen, "zero delta cannot advance movement or planning");
        wall.transform.position = worm.HeadPosition;
        Physics2D.SyncTransforms();
        worm.Advance(.02f);
        Check(worm.HeadPosition == frozen, "newly moved wall invalidates old route without teleporting");
        Object.DestroyImmediate(wall);
        bool resumed = false;
        for (int i = 0; i < 500 && !resumed; i++)
        {
            worm.Advance(.02f);
            resumed = Vector2.Distance(worm.HeadPosition, frozen) > .05f;
        }
        Check(resumed, "removing obstruction allows bounded retry to resume");
        Check(worm.GetComponentsInChildren<Collider2D>(true).Length == 8, "roaming preserves eight authored part colliders");
        Object.DestroyImmediate(root);
        Debug.Log("SANDWORM_ROAM_CONSTRAINTS_PASS");
    }
}
