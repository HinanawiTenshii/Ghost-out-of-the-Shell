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
using Object = UnityEngine.Object;

// Isolated validation only. Does not save scenes or alter original scene geometry.
[InitializeOnLoad]
public static class DesertMapGroupSmoke
{
    static readonly string[] Names = { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2", "Level2-Desert" };
    const string Desert = "Level2-Desert";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks, frame = -1;
    static IEnumerator suite;
    static double deadline;
    static DesertMapGroupSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static object Field(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    static List<RuntimeMiniMapSurfaceData> Surfaces(RuntimeMiniMapGraphic g) => (List<RuntimeMiniMapSurfaceData>)Field(g, "surfaces");
    static RuntimeMiniMapGraphic Graphic() => new GameObject("Desert Map Capture", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();

    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
            Check(EditorBuildSettings.scenes.Count(s => s.enabled && s.path.EndsWith("/" + Desert + ".unity")) == 1, "Desert in build exactly once");
            foreach (string name in Names)
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + name + ".unity");
                var groups = InScene<LevelSceneGroup>(scene);
                Check(groups.Length == 1, name + ": single group");
                var def = groups[0].CreateDefinition(); groups[0].Register();
                Check(def.LevelId == "level2" && def.SceneNames.SequenceEqual(Names), name + ": identical four-scene membership");
                Check(def.EntrySceneName == Names[0] && def.SaveSchemaVersion == 1, name + ": original entry/schema unchanged");
                foreach (string member in Names)
                {
                    Check(LevelSceneGroup.AreScenesInSameLevel(name, member), "same level: " + member);
                    Check(def.GetSceneSaveKey(member) == "levels/level2/scenes/" + member, "stable save key: " + member);
                }
                var menuNames = (List<string>)typeof(TabJournalMenuController).GetMethod("GetMapSceneNames", F).Invoke(null, new object[] { name });
                Check(menuNames.SequenceEqual(Names), name + ": map menu lists all four scenes after registration");
            }
            var desert = SceneManager.GetActiveScene();
            Check(InScene<Component>(desert).All(c => c != null), "Desert has no missing component scripts");
            var road = InScene<MiniMapSurfaceGroup>(desert).Single();
            Check(road.name == "Roads" && road.Kind == MiniMapSurfaceGroup.SurfaceKind.Road, "Only explicit road parent mapped");
            var sprites = road.GetComponentsInChildren<SpriteRenderer>(true);
            Check(sprites.Length == 5, "Five existing road segments preserved");
            var live = Graphic(); live.RebuildFromScene(desert);
            Check(Surfaces(live).Count == sprites.Length, "Live minimap includes all roads and no decoration");
            foreach (var sprite in sprites)
            {
                var expected = RuntimeMiniMapSceneData.CreateBoxPath(sprite.transform.localToWorldMatrix, sprite.sprite.bounds.center, sprite.sprite.bounds.size);
                Check(Surfaces(live).Any(s => s.points.SequenceEqual(expected)), "World-space road footprint: " + sprite.name);
            }
            var menu = InScene<TabJournalMenuController>(desert).Single();
            Check(menu.AreaDisplayName == "Level2 · 沙漠", "Desert has distinct menu/HUD label");
            const string mapPath = "Assets/Resources/MiniMaps/Level2-Desert.asset";
            var data = AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(mapPath);
            if (!data) { data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>(); AssetDatabase.CreateAsset(data, mapPath); }
            live.CopyCurrentGeometryTo(data, Desert, menu.AreaDisplayName); EditorUtility.SetDirty(data);
            var unloaded = Graphic(); unloaded.RebuildFromSnapshot(data);
            Check(data.UsesCurrentSchema && data.DisplayName == menu.AreaDisplayName, "Current snapshot schema/label");
            Check(Surfaces(unloaded).Count == 5, "Unloaded scene map has roads");
            for (int i = 0; i < 5; i++) Check(Surfaces(live)[i].points.SequenceEqual(Surfaces(unloaded)[i].points), "Live and snapshot geometry match");
            RenderPreview(unloaded);
            Object.DestroyImmediate(live.gameObject);

            // Update only these level templates, preserving all other catalog entries.
            var catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
            foreach (var name in Names)
            {
                string path = "Assets/Scenes/Level2/" + name + ".unity";
                var entry = SaveGameCatalog.SceneTemplate.FromSceneText(name, path, File.ReadAllText(path));
                int index = catalog.scenes.FindIndex(s => s.name == name);
                if (index < 0) catalog.scenes.Add(entry); else catalog.scenes[index] = entry;
                Check(entry.CreateSaveSnapshot().authoredState.Contains("sceneSaveId: Level2-Desert"), name + ": save template retains desert membership");
            }
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            File.Copy("Assets/Resources/SaveGameCatalog.asset", "Desert-catalog-generated.asset", true);
            PlayerSettings.productName = "desert-map-group-smoke";
            SessionState.SetInt("DesertMapChecks", checks); SessionState.SetBool("DesertMapRun", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("DesertMapRun", false))
            {
                SessionState.SetBool("DesertMapRun", false); checks = SessionState.GetInt("DesertMapChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 90; suite = VerifyRuntime();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("runtime validation timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("DESERT_MAP_GROUP_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static IEnumerator VerifyRuntime()
    {
        for (int i = 0; i < 25 || RetroSceneLoadReveal.IsBlockingInput; i++) yield return null;
        var hud = ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeMiniMapGraphic>(true);
        Check(hud && Surfaces(hud).Count == 5, "Actual HUD shows all desert roads");
        var menu = Object.FindObjectOfType<TabJournalMenuController>(); menu.OpenMenu();
        Check(TabJournalMenuController.IsOpen, "Tab menu opens");
        Call(menu, "SelectMapScene", Desert);
        var full = (RuntimeMiniMapGraphic)Field(menu, "miniMapGraphic");
        Check(full && Surfaces(full).Count == 5, "Actual menu map matches HUD roads");
        foreach (string name in Names) { Call(menu, "SelectMapScene", name); Check(Surfaces(full).Count > 0, "Map selection works: " + name); }
        menu.CloseMenu();
        var road = Object.FindObjectOfType<MiniMapSurfaceGroup>();
        var tile = road.GetComponentInChildren<SpriteRenderer>(); tile.gameObject.SetActive(false);
        Call(hud, "RefreshSurfaceVisibility", true);
        Check(Surfaces(hud).Count == 5 && ((List<float>)Field(hud, "surfaceOpacities")).All(a => a == 1), "Streaming does not erase roads");
        tile.gameObject.SetActive(true);
    }

    static void RenderPreview(RuntimeMiniMapGraphic graphic)
    {
        var camera = new GameObject("Map Preview Camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 4;
        camera.cullingMask = 1 << 31; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(5, 12, 24, 255);
        var rt = new RenderTexture(800, 800, 24); camera.targetTexture = rt;
        var root = new GameObject("Map Preview", typeof(RectTransform), typeof(Canvas));
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        root.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 800); root.transform.localScale = Vector3.one * .01f;
        graphic.transform.SetParent(root.transform, false); graphic.rectTransform.sizeDelta = new Vector2(800, 800);
        graphic.SetCameraMarkerVisible(false); graphic.ZoomBy(-100);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
        Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(800, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 800, 800), 0, 0); image.Apply();
        Check(image.GetPixels().Count(c => c.b > .2f) > 1000, "Actual map GPU render contains visible geometry");
        File.WriteAllBytes("Level2-Desert-map.png", image.EncodeToPNG());
        RenderTexture.active = null; camera.targetTexture = null;
        Object.DestroyImmediate(image); Object.DestroyImmediate(rt); Object.DestroyImmediate(root); Object.DestroyImmediate(camera.gameObject);
    }
}
