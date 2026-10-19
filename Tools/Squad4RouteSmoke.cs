using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run only in a disposable copy; neither the user's scene nor saves are written.
[InitializeOnLoad]
public static class Squad4RouteSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static Squad4RouteSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string text) { checks++; if (!ok) throw new Exception(text); }
    static object Get(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) return field.GetValue(obj); }
        throw new Exception(name);
    }
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
            var root = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "Squad4");
            var team = root.GetComponentsInChildren<SquadPatrolCharacterAi>(true);
            Check(team.Length == 5, "original roster preserved");
            var leader = team.Single(a => ((SquadPatrolCharacterAi[])Get(a, "members")).Length == 4);
            var waypoints = (ZeldaPatrolCharacterAi.PatrolWaypoint[])Get(leader, "customPatrolPoints");
            Check(waypoints.Length == 38, "authored road-following route");
            Check(waypoints.Count(p => p.waitDuration > 0) == 7 && Mathf.Approximately(waypoints.Sum(p => p.waitDuration), 7), "no extra stop time");
            var preview = typeof(ZeldaPatrolCharacterAi).GetMethod("PreviewPoint", F);
            var points = Enumerable.Range(0, waypoints.Length).Select(i => (Vector2)(Vector3)preview.Invoke(leader, new object[] { i })).ToArray();
            Physics2D.SyncTransforms();
            var body = leader.GetComponent<BoxCollider2D>();
            Vector2 offset = body.bounds.center - leader.transform.position;
            // Include extra clearance beyond the actual body, not just a thin line cast.
            Vector2 size = (Vector2)body.bounds.size + Vector2.one * .2f;
            int doors = 0;
            for (int i = 0; i < points.Length; i++)
            {
                Vector2 delta = points[(i + 1) % points.Length] - points[i];
                Check(delta.magnitude <= 20.01f, "short local destination at " + i);
                foreach (var hit in Physics2D.BoxCastAll(points[i] + offset, size, 0, delta.normalized, delta.magnitude))
                {
                    var c = hit.collider;
                    if (c.isTrigger || c.GetComponentInParent<ZeldaCharacterData>() != null) continue;
                    if (c.GetComponentInParent<DoorHingeInteraction>() != null) { doors++; continue; }
                    Check(false, "route leg " + i + " intersects " + c.name + " at " + hit.point);
                }
                Check(true, "body clearance leg " + i);
            }
            Check(doors == 2, "original two fortress door passages retained");
            Debug.Log("SQUAD4_GEOMETRY_PASS checks=" + checks);
            // Keep the actual scene-authored squad, but not any world services, save
            // managers or nearby characters that could interfere with this fixture.
            root.SetParent(null, true);
            var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(root.gameObject, fixture);
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(fixture);
            SessionState.SetBool(nameof(Squad4RouteSmoke), true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Squad4RouteSmoke), false))
            { SessionState.SetBool(nameof(Squad4RouteSmoke), false); suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null;
            Debug.Log("SQUAD4_ROUTE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        var root = Object.FindObjectsOfType<Transform>(true).Single(t => t.name == "Squad4");
        var team = root.GetComponentsInChildren<SquadPatrolCharacterAi>(true);
        var leader = team.Single(a => ((SquadPatrolCharacterAi[])Get(a, "members")).Length == 4);
        yield return null;
        var expected = ((Vector2[])Get(leader, "patrolPoints")).ToArray();
        Check(expected.Length == 38, "runtime imports entire edited route");
        for (int cycle = 0; cycle < 6; cycle++)
        {
            var before = team.Select(a => a.GetComponent<Rigidbody2D>().position).ToArray();
            int index = (int)Get(leader, "currentPatrolPointIndex");
            foreach (var ai in team) ai.gameObject.SetActive(false);
            yield return null;
            // Resume followers first, as a budgeted visibility loader can do.
            foreach (var ai in team.Where(a => a != leader)) ai.gameObject.SetActive(true);
            yield return null;
            foreach (var ai in team.Where(a => a != leader)) Check(ai.CurrentLeader == leader, "streamed captain is not replaced");
            leader.gameObject.SetActive(true);
            Check((int)Get(leader, "currentPatrolPointIndex") == index, "reload preserves next destination");
            var actual = (Vector2[])Get(leader, "patrolPoints");
            for (int j = 0; j < expected.Length; j++) Check(Vector2.Distance(actual[j], expected[j]) < .0001f, "route never shifts to moving reload origin");
            for (int j = 0; j < team.Length; j++) Check(Vector2.Distance(before[j], team[j].GetComponent<Rigidbody2D>().position) < .15f, "reload does not teleport actor");
            yield return null;
        }
    }
}
