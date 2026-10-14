using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SquadOptimizationSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite; static int frame = -1, checks;
    static SquadOptimizationSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SquadOptimizationSmoke), true); EditorApplication.EnterPlaymode();
    }
    public static void RunDynamic()
    {
        SessionState.SetBool("SquadDynamicOnly", true); Run();
    }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static SquadPatrolCharacterAi[] Team(int followers, Vector2 origin) =>
        (SquadPatrolCharacterAi[])typeof(SquadPatrolSmoke).GetMethod("MakeSquad", S).Invoke(null, new object[] { followers, origin });
    static void Clear() => typeof(SquadPatrolSmoke).GetMethod("Clear", S).Invoke(null, null);
    static Vector2 Position(SquadPatrolCharacterAi ai) => ai.GetComponent<Rigidbody2D>().position;
    static void Move(SquadPatrolCharacterAi ai, Vector2 p) { ai.GetComponent<Rigidbody2D>().position = p; ai.transform.position = p; }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SquadOptimizationSmoke), false))
            {
                SessionState.SetBool(nameof(SquadOptimizationSmoke), false); Time.captureFramerate = 60;
                suite = SessionState.GetBool("SquadDynamicOnly", false) ? VerifyDynamic() : Verify();
                SessionState.SetBool("SquadDynamicOnly", false);
            }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Clear(); Time.captureFramerate = 0;
            Debug.Log("SQUAD_OPTIMIZATION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Time.captureFramerate = 0; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        var existing = (IEnumerator)typeof(FlexiblePatrolSmoke).GetMethod("Verify", S).Invoke(null, null);
        while (existing.MoveNext()) yield return existing.Current;
        var team = Team(2, new Vector2(0, 400));
        Check(team[0].CurrentLeader == team[0], "captain exists");
        object group = Get(team[0], "group");
        var expected = new List<Vector2> { Position(team[0]) };
        for (int step = 0; step < 800; step++)
        {
            Vector2 p = new Vector2(step * .085f, 400 + Mathf.Sin(step * .024f) * 2f);
            Move(team[0], p);
            if (Vector2.Distance(expected[0], p) >= .12f)
            { expected.Insert(0, p); if (expected.Count > 256) expected.RemoveAt(256); }
            Set(group, "frame", -1); group.GetType().GetMethod("Refresh", F).Invoke(group, null);
            foreach (float d in new[] { 0f, .05f, .2f, 1.5f, 4.6f, 18f, 35f })
            {
                Vector2 from = p, wanted = p; float remaining = d; bool found = false;
                foreach (Vector2 point in expected)
                {
                    float length = Vector2.Distance(from, point);
                    if (length >= remaining && length > .001f)
                    { wanted = Vector2.Lerp(from, point, remaining / length); found = true; break; }
                    remaining -= length; from = point;
                }
                if (!found) wanted = from - (Vector2)Get(group, "heading") * remaining;
                Vector2 actual = (Vector2)group.GetType().GetMethod("TrailPoint", F).Invoke(group, new object[] { d });
                Check(Vector2.Distance(actual, wanted) < .0001f, "ring trail preserves original path including wraparound");
            }
        }
        Clear(); yield return null;
        var dynamicTest = VerifyDynamic();
        while (dynamicTest.MoveNext()) yield return dynamicTest.Current;
    }
    static IEnumerator VerifyDynamic()
    {
        var team = Team(1, new Vector2(0, 500));
        float until = Time.time + 1f;
        while (Time.time < until) yield return null;
        // Close between captain and follower. A wall ahead of both actors may
        // simply be avoided by the captain, leaving the follower's trail clear.
        Vector2 doorway = (Position(team[0]) + Position(team[1])) * .5f;
        Check(Mathf.Abs(Position(team[0]).x - Position(team[1]).x) > .7f, "doorway fixture has body clearance");
        var wall = (BoxCollider2D)typeof(SquadPatrolSmoke).GetMethod("Wall", S).Invoke(null,
            new object[] { "Closing obstacle", doorway, new Vector2(.25f, 4) });
        Physics2D.SyncTransforms();
        float closedAt = Time.time;
        var previousCenters = new Vector2[team.Length];
        for (int i = 0; i < team.Length; i++) previousCenters[i] = team[i].GetComponent<BoxCollider2D>().bounds.center;
        bool detected = false; float maxOverlap = 0f; until = Time.time + 3f;
        var clearField = typeof(SquadPatrolCharacterAi).GetField("followSegmentClear", F);
        while (Time.time < until)
        {
            detected |= clearField != null && !(bool)clearField.GetValue(team[1]);
            for (int i = 0; i < team.Length; i++)
            {
                var actor = team[i];
                var body = actor.GetComponent<BoxCollider2D>();
                var distance = body.Distance(wall);
                maxOverlap = Mathf.Max(maxOverlap, -distance.distance);
                Check(!distance.isOverlapped, "no obstacle penetration: " + actor.name +
                    " after=" + (Time.time - closedAt) + " distance=" + distance.distance + " position=" + Position(actor));
                Vector2 center = body.bounds.center, previous = previousCenters[i];
                float wallX = wall.bounds.center.x;
                if ((previous.x - wallX) * (center.x - wallX) < 0f)
                {
                    float crossingY = Mathf.Lerp(previous.y, center.y, (wallX - previous.x) / (center.x - previous.x));
                    Check(crossingY < wall.bounds.min.y - body.bounds.extents.y ||
                          crossingY > wall.bounds.max.y + body.bounds.extents.y, "never tunnels through newly closed obstacle");
                }
                previousCenters[i] = center;
            }
            yield return null;
        }
        Debug.Log("DYNAMIC_OBSTACLE maxContactOverlap=" + maxOverlap + " follower=" + Position(team[1]));
        if (clearField != null) Check(detected, "follower refreshes blocked segment");
        float before = Position(team[1]).x;
        Object.DestroyImmediate(wall.gameObject);
        until = Time.time + 3f;
        while (Time.time < until) yield return null;
        Check(Position(team[1]).x > before + 1f, "follower resumes progress after obstacle opens");
        Clear();
    }
}
