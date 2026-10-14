using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class Level2MapSurfacesSmoke
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
    static readonly string[] Names = { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2" };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static Level2MapSurfacesSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Field(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static RuntimeMiniMapGraphic Graphic() => new GameObject("Map Smoke", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
    static List<RuntimeMiniMapSurfaceData> Surfaces(RuntimeMiniMapGraphic g) => (List<RuntimeMiniMapSurfaceData>)Field(g, "surfaces");
    static List<float> Opacities(RuntimeMiniMapGraphic g) => (List<float>)Field(g, "surfaceOpacities");
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "level2-map-surfaces-smoke";
        foreach (string name in Names)
        {
            var s = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + name + ".unity");
            var g = Graphic(); g.RebuildFromScene(s);
            string path = "Assets/Resources/MiniMaps/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(path);
            var previousWalls = data.BlockPaths.Select(p => p.points).ToArray();
            g.CopyCurrentGeometryTo(data, name, data.DisplayName); EditorUtility.SetDirty(data);
            Check(previousWalls.All(p => data.BlockPaths.Any(n => n.points.SequenceEqual(p))), name + ": previous walls preserved");
            VisibleNonBlockingMapSmoke.CheckScene(s, data);
            Check(data.Surfaces.Count(p => p.kind == MiniMapSurfaceGroup.SurfaceKind.Road) >= 7, name + ": roads");
            Check(data.Surfaces.Count(p => p.kind == MiniMapSurfaceGroup.SurfaceKind.Water) >= 8, name + ": water");
            Check(data.UsesCurrentSchema, name + ": new schema");
            foreach (var p in data.Surfaces) Check(p.points.Length == 5 && p.points[0] == p.points[4], "closed footprint");
            var copy = Graphic(); copy.RebuildFromSnapshot(data);
            Check(Surfaces(copy).Count == Surfaces(g).Count, name + ": unloaded map parity");
            for (int i = 0; i < Surfaces(copy).Count; i++) Check(Surfaces(copy)[i].points.SequenceEqual(Surfaces(g)[i].points), name + ": same geometry");
            RenderPreview(copy, name);
            Object.DestroyImmediate(g.gameObject);
            Debug.Log("LEVEL2_MAP_GEOMETRY " + name + " roads=" + data.Surfaces.Count(p => p.kind == MiniMapSurfaceGroup.SurfaceKind.Road) + " water=" + data.Surfaces.Count(p => p.kind == MiniMapSurfaceGroup.SurfaceKind.Water));
        }
        AssetDatabase.SaveAssets();
        VisibleNonBlockingMapSmoke.CheckFixtures();
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        SessionState.SetInt("MapSurfaceChecks", checks); SessionState.SetBool("MapSurfaceRun", true);
        EditorApplication.EnterPlaymode();
    }
    static void RenderPreview(RuntimeMiniMapGraphic g, string name)
    {
        var cameraRoot = new GameObject("Map Preview Camera"); var cam = cameraRoot.AddComponent<Camera>();
        cam.transform.position = new Vector3(0, 0, -10); cam.orthographic = true; cam.orthographicSize = 4.5f;
        cam.cullingMask = 1 << 31; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color32(5, 12, 24, 255);
        var rt = new RenderTexture(1200, 900, 24); cam.targetTexture = rt;
        var root = new GameObject("Map Preview Canvas", typeof(RectTransform), typeof(Canvas)); root.layer = 31;
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = cam;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(1200, 900); root.transform.localScale = Vector3.one * .01f;
        g.transform.SetParent(root.transform, false); g.rectTransform.sizeDelta = new Vector2(1200, 900); g.SetCameraMarkerVisible(false); g.ZoomBy(-100);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases(); cam.Render();
        RenderTexture.active = rt; var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0,0,1200,900),0,0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + name + "-map.png"), image.EncodeToPNG());
        RenderTexture.active = null; cam.targetTexture = null;
        Object.DestroyImmediate(root); Object.DestroyImmediate(cameraRoot); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("MapSurfaceRun", false))
            {
                SessionState.SetBool("MapSurfaceRun", false); checks = SessionState.GetInt("MapSurfaceChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 90; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_MAP_SURFACES_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        for (int i = 0; i < 30 || RetroSceneLoadReveal.IsBlockingInput; i++) yield return null;
        var optimizationSuite = MapUiOptimizationSmoke.VerifyFloor2();
        while (optimizationSuite.MoveNext()) yield return optimizationSuite.Current;
        var floor2Hud = ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeMiniMapGraphic>();
        var floor2Menu = Object.FindObjectOfType<TabJournalMenuController>(); floor2Menu.OpenMenu();
        var floor2Full = (RuntimeMiniMapGraphic)Field(floor2Menu, "miniMapGraphic");
        var floor2Data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
        floor2Hud.CopyCurrentGeometryTo(floor2Data, "Level2-Floor2", "test");
        VisibleNonBlockingMapSmoke.CheckScene(SceneManager.GetActiveScene(), floor2Data);
        var floor2MenuData = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
        floor2Full.CopyCurrentGeometryTo(floor2MenuData, "Level2-Floor2", "test");
        Check(floor2MenuData.BlockPaths.Length == floor2Data.BlockPaths.Length, "Floor2 HUD / menu wall parity");
        VisibleNonBlockingMapSmoke.CheckScene(SceneManager.GetActiveScene(), floor2MenuData);
        Object.Destroy(floor2Data); Object.Destroy(floor2MenuData); floor2Menu.CloseMenu();
        SceneManager.LoadScene("Level2-Floor-1");
        yield return null;
        for (int i = 0; i < 30 || RetroSceneLoadReveal.IsBlockingInput; i++) yield return null;
        var hud = ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeMiniMapGraphic>();
        MapUiOptimizationSmoke.VerifySceneTravel();
        var menu = Object.FindObjectOfType<TabJournalMenuController>(); menu.OpenMenu();
        var full = (RuntimeMiniMapGraphic)Field(menu, "miniMapGraphic");
        Check(Surfaces(hud).Count == Surfaces(full).Count && Surfaces(hud).Count > 40, "HUD and active menu share terrain");
        foreach (string name in Names)
        {
            Call(menu, "SelectMapScene", name);
            Check(Surfaces(full).Count > 10, "menu other-floor map: " + name);
        }
        Call(menu, "SelectMapScene", "Level2-Floor-1"); menu.CloseMenu();
        var fader = Object.FindObjectsOfType<WaterwayGateTargetFade>(true).First(f => f.name == "3BWater1");
        int index = Surfaces(hud).FindIndex(s => s.visibilityId == SceneTravelStateManager.GetMapObjectId(fader.transform));
        Check(index >= 0, "gate-controlled surface identified");
        fader.SetVisibleImmediately(false); Call(hud, "RefreshSurfaceVisibility", true); Call(full, "RefreshSurfaceVisibility", true);
        Check(Opacities(hud)[index] == 0 && Opacities(full)[index] == 0, "drained water disappears in both views");
        SceneTravelStateManager.GetOrCreate().CaptureDiskWorld();
        var snapshot = Graphic(); snapshot.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1"));
        int savedIndex = Surfaces(snapshot).FindIndex(s => s.visibilityId == Surfaces(hud)[index].visibilityId);
        Check(Opacities(snapshot)[savedIndex] == 0, "unloaded map respects saved water state");
        fader.SetVisibleImmediately(true); Call(hud, "RefreshSurfaceVisibility", true);
        Check(Opacities(hud)[index] == 1, "refilled water returns");
        var road = Object.FindObjectsOfType<MiniMapSurfaceGroup>().First(g => g.Kind == MiniMapSurfaceGroup.SurfaceKind.Road);
        var tile = road.GetComponentInChildren<SpriteRenderer>(); tile.gameObject.SetActive(false);
        Call(hud, "RefreshSurfaceVisibility", true);
        Check(Surfaces(hud).Where(s => s.kind == MiniMapSurfaceGroup.SurfaceKind.Road).All(s => s.initiallyVisible), "streaming never erases roads");
        tile.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
        var layer = (RuntimeMiniMapLayerGraphic)Field(hud, "surfaceLayer");
        var wall = (RuntimeMiniMapLayerGraphic)Field(hud, "wallLayer");
        Check(layer.transform.GetSiblingIndex() < wall.transform.GetSiblingIndex(), "terrain behind walls");
        int surfaceBuilds = layer.MeshBuildCount, wallBuilds = wall.MeshBuildCount;
        for (int i = 0; i < 25; i++) { hud.SetCameraPosition(new Vector2(i, i)); hud.PanByPixels(new Vector2(.1f, .1f)); Canvas.ForceUpdateCanvases(); }
        Check(layer.MeshBuildCount == surfaceBuilds && wall.MeshBuildCount == wallBuilds, "moving markers and panning reuse cached terrain/walls");
        hud.ZoomBy(-.2f); Canvas.ForceUpdateCanvases();
        Check(layer.MeshBuildCount > surfaceBuilds, "zoom rebuilds surface once");
        var legacy = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
        legacy.Configure("old", "old", new List<Vector2[]>(), new List<RuntimeMiniMapTransitionData>(), null);
        snapshot.RebuildFromSnapshot(legacy); Check(Surfaces(snapshot).Count == 0, "old no-terrain maps remain valid");
        Object.Destroy(snapshot.gameObject); Object.Destroy(legacy);
    }
}
