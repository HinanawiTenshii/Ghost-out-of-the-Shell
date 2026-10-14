using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Isolated diagnostic only: instrument that project's AI copies with
// SquadPerformanceCounters before running either side of the comparison.
[InitializeOnLoad]
public static class SquadPerformanceSmoke
{
    [Serializable] public class Result
    {
        public string label;
        public long updates, gridPlans, cellChecks, bodyCasts;
        public double updateMs;
        public float[] positions;
    }
    static readonly List<SquadPatrolCharacterAi> actors = new List<SquadPatrolCharacterAi>();
    static readonly List<BoxCollider2D> walls = new List<BoxCollider2D>();
    static int frame = -1, frames;
    static string label;
    const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static SquadPerformanceSmoke() { EditorApplication.update += Tick; }
    public static void RunBefore() { Run("before"); }
    public static void RunAfter() { Run("after"); }
    static void Run(string mode)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetString("SquadPerformance", mode);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            string pending = SessionState.GetString("SquadPerformance", "");
            if (pending != "")
            {
                SessionState.SetString("SquadPerformance", ""); label = pending;
                Time.captureFramerate = 60; Time.timeScale = 1f;
                for (int i = 0; i < 4; i++)
                {
                    float y = i * 30;
                    actors.AddRange((SquadPatrolCharacterAi[])typeof(SquadPatrolSmoke).GetMethod("MakeSquad", S).Invoke(null, new object[] { 4, new Vector2(0, y) }));
                    if (i >= 2)
                    {
                        walls.Add((BoxCollider2D)typeof(SquadPatrolSmoke).GetMethod("Wall", S).Invoke(null, new object[] { "Lower", new Vector2(6, y - .65f), new Vector2(6, .8f) }));
                        walls.Add((BoxCollider2D)typeof(SquadPatrolSmoke).GetMethod("Wall", S).Invoke(null, new object[] { "Upper", new Vector2(6, y + 1.1f), new Vector2(6, .8f) }));
                    }
                }
                frames = -40;
            }
            if (label == null) return;
            frames++;
            if (frames == 0) SquadPerformanceCounters.Reset();
            if (frames <= 0) return;
            foreach (var actor in actors)
                foreach (var wall in walls)
                    if (actor.GetComponent<BoxCollider2D>().Distance(wall).isOverlapped)
                        throw new Exception("Wall overlap " + actor.name);
            if (frames < 600) return;
            var result = new Result { label = label, updates = SquadPerformanceCounters.updates,
                updateMs = SquadPerformanceCounters.ticks * 1000.0 / Stopwatch.Frequency,
                gridPlans = SquadPerformanceCounters.gridPlans, cellChecks = SquadPerformanceCounters.cellChecks,
                bodyCasts = SquadPerformanceCounters.bodyCasts, positions = new float[actors.Count * 2] };
            for (int i = 0; i < actors.Count; i++)
            {
                var p = actors[i].GetComponent<Rigidbody2D>().position;
                result.positions[i * 2] = p.x; result.positions[i * 2 + 1] = p.y;
                if (p.x < 9.2f) throw new Exception("Follower failed to exit test corridor: " + p);
            }
            string json = JsonUtility.ToJson(result, true);
            File.WriteAllText(Application.dataPath + "/../squad-performance-" + label + ".json", json);
            Debug.Log("SQUAD_PERFORMANCE_PASS " + JsonUtility.ToJson(result));
            Time.captureFramerate = 0;
            typeof(SquadPatrolSmoke).GetMethod("Clear", S).Invoke(null, null);
            label = null; EditorApplication.Exit(0);
        }
        catch (Exception e) { label = null; Time.captureFramerate = 0; Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
