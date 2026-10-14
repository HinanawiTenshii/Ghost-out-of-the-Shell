using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SceneScaleBaselineSmoke
{
    [Serializable] public class PathResult { public bool success; public Vector2[] points; }
    [Serializable] public class Result { public PathResult[] paths; public double milliseconds; }
    public const string Output = "Assets/Editor/SceneScaleBaseline.json";
    public const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static SceneScaleBaselineSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool("SceneScaleBaseline", true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool("SceneScaleBaseline", false)) return;
        SessionState.SetBool("SceneScaleBaseline", false);
        try
        {
            var result = CapturePaths(); File.WriteAllText(Output, JsonUtility.ToJson(result));
            Debug.Log("SCENE_SCALE_BASELINE_PASS paths=" + result.paths.Length + " ms=" + result.milliseconds);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    public static FieldInfo Field(object target, string name)
    {
        for (Type t=target.GetType();t!=null;t=t.BaseType)
        { var f=t.GetField(name,Flags); if(f!=null)return f; }
        throw new Exception("Missing field " + name);
    }
    public static Result CapturePaths()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/Bodyguard-level2.prefab");
        var actors = new List<GameObject>();
        var walls = new List<GameObject>();
        var results = new List<PathResult>();
        for (int i=0;i<2;i++) actors.Add(Object.Instantiate(prefab,new Vector3(-6,i*2,0),Quaternion.identity));
        for (int i=0;i<4;i++)
        {
            var wall = new GameObject("Navigation fixture wall"); wall.layer=LayerMask.NameToLayer("Blocks");
            var box=wall.AddComponent<BoxCollider2D>(); box.size = new Vector2(.7f,5f);
            wall.transform.position=new Vector3(i*3-3, i%2==0 ? 1.7f : -1.7f, 0); walls.Add(wall);
        }
        var clock=System.Diagnostics.Stopwatch.StartNew();
        for (int i=0;i<24;i++)
        {
            var ai=actors[i%2].GetComponent<ZeldaCharacterAiBase>();
            Field(ai,"solidCollisionLayers").SetValue(ai,(LayerMask)LayerMask.GetMask("Blocks"));
            // Open/close/move a wall between searches; no cached occupancy may leak.
            walls[0].GetComponent<BoxCollider2D>().enabled=i%3!=0;
            walls[1].transform.position=new Vector3(0, i%4==0 ? 8 : -1.7f,0);
            Physics2D.SyncTransforms();
            Vector2 goal = new Vector2(6, (i%5-2)*1.1f);
            bool ok=(bool)typeof(ZeldaCharacterAiBase).GetMethod("TryBuildGridPath",Flags).Invoke(ai,
                new object[] { (Vector2)ai.transform.position,goal,i%2==0?.5f:.25f,.16f,i%3==0?121:45,5,0f,i%3==0,true });
            results.Add(new PathResult { success=ok,points=((List<Vector2>)Field(ai,"gridPath").GetValue(ai)).ToArray() });
        }
        clock.Stop();
        foreach(var go in actors)Object.DestroyImmediate(go);
        foreach(var go in walls)Object.DestroyImmediate(go);
        return new Result { paths=results.ToArray(), milliseconds=clock.Elapsed.TotalMilliseconds };
    }
}
