using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class SceneLoadingMapSmoke
{
    static SceneLoadingMapSmoke() { EditorApplication.update += CheckBakedPath; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        RuntimeMiniMapSceneDataGenerator.GenerateSnapshots(true);
        SessionState.SetBool(nameof(SceneLoadingMapSmoke), true);
        Level2MapSurfacesSmoke.Run();
    }
    static void CheckBakedPath()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(SceneLoadingMapSmoke), false)) return;
        var caches = (IDictionary)typeof(RuntimeMiniMapGraphic).GetField("sceneGeometry", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        if (caches.Count == 0) return;
        SessionState.SetBool(nameof(SceneLoadingMapSmoke), false);
        foreach (object value in caches.Values)
        {
            if (!(bool)value.GetType().GetField("generated").GetValue(value)) { Debug.Log("BAKED_SCENE_MAP_PASS"); return; }
        }
        Debug.LogError("No current scene used baked map geometry"); EditorApplication.Exit(1);
    }
}
