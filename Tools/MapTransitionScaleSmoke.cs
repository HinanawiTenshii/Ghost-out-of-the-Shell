using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Renders the real map meshes from the authored snapshot, without loading gameplay.
public static class MapTransitionScaleSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool condition, string label) { checks++; if (!condition) throw new Exception(label); }
    static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated only");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CheckSharedTransitionBadge();
            var canvas = new GameObject("Map preview", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1400, 1000);
            var test = CreateMap(canvas.transform, Vector2.zero, Vector2.one * 500, 1f, 1f);
            var data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
            data.Configure("Test", "Test", new[] { new[] { new Vector2(-50,-50), new Vector2(50,50) } },
                new[] { new RuntimeMiniMapTransitionData { position = Vector2.zero, preservesSceneState = true },
                    new RuntimeMiniMapTransitionData { position = new Vector2(20, 0), preservesSceneState = false } }, null);
            test.RebuildFromSnapshot(data);
            using (var vh = new VertexHelper())
            {
                int iconVertices;
                using (var glyph = new VertexHelper())
                {
                    RuntimeMiniMapPointIconGraphic.DrawIcon(glyph, MapPointIconShape.SceneTransition, Vector2.zero, 1f, Color.white);
                    iconVertices = glyph.currentVertCount;
                }
                Call(test, "PopulateMarkers", vh);
                Check(vh.currentVertCount == iconVertices * 2, "40% overview uses the shared compact portal glyph");
                float compactInk = Ink(vh);
                var v = new UIVertex(); bool opaqueBlue = false;
                for (int i = 0; i < vh.currentVertCount; i++) { vh.PopulateUIVertex(ref v, i); opaqueBlue |= v.color.a == 255 && v.color.b > v.color.r + 50; }
                Check(opaqueBlue, "small portal glyph retains opaque readable blue");
                test.ResetView(); Call(test, "PopulateMarkers", vh);
                Check(vh.currentVertCount == iconVertices * 2, "all zoom levels reuse cached portal geometry");
                Check(compactInk < Ink(vh), "overview footprint shrinks with shared POI zoom floor");
                test.SetCameraMarkerVisible(true); Call(test, "PopulateMarkers", vh);
                Check(vh.currentVertCount == iconVertices * 2 + 4, "player marker retained");
                test.SetTrackedQuestTarget(true, Vector2.one * 100, true); Call(test, "PopulateMarkers", vh);
                Check(vh.currentVertCount == iconVertices * 2 + 16, "quest marker retained with same geometry");
            }
            float previous = 0;
            test.ZoomBy(-100);
            for (int i = 0; i < 17; i++)
            {
                float size = test.PointIconScale;
                Check(size >= previous && size <= 1f, "zoom sizing monotonic and capped " + i);
                CheckSameSizeAsPoi(test);
                previous = size; test.ZoomBy(.25f);
            }
            test.SetIconScale(.45f); test.SetZoomToMaximum();
            float hud = test.PointIconScale;
            Check(Mathf.Approximately(hud, .5f), "HUD exits retain the same readable minimum as POIs");
            CheckSameSizeAsPoi(test);
            test.rectTransform.sizeDelta = Vector2.one * 96;
            CheckSameSizeAsPoi(test);
            Object.DestroyImmediate(test.transform.parent.gameObject); Object.DestroyImmediate(data);
            var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor1");
            Check(snapshot != null && snapshot.Transitions.Length > 10, "actual dense Floor1 snapshot loaded");
            for (int column = 0; column < 2; column++)
            {
                float x = column == 0 ? -345 : 345;
                Label(canvas.transform, new Vector2(x, 459), column == 0 ? "线框图标 · 40% 总览" : "实心图标 · 40% 总览");
                var map = CreateMap(canvas.transform, new Vector2(x, 136), new Vector2(650, 580), 1, 1);
                map.RebuildFromSnapshot(snapshot);
                if (column == 1) CheckSameSizeAsPoi(map);
                Check(map.Transitions.Count == snapshot.Transitions.Length, "no exit positions hidden or merged");
                if (column == 0) Legacy(map, 1);
                Label(canvas.transform, new Vector2(x + 115, -310), column == 0 ? "线框小地图" : "实心小地图");
                var mini = CreateMap(canvas.transform, new Vector2(x - 165, -310), Vector2.one * 96, 5, .45f);
                mini.transform.parent.localScale = Vector3.one * 2.4f;
                mini.RebuildFromSnapshot(snapshot); mini.CenterOnWorldPosition(new Vector2(-45, -20));
                if (column == 1) CheckSameSizeAsPoi(mini);
                if (column == 0) Legacy(mini, .45f);
            }
            Render(canvas);
            Debug.Log("MAP_TRANSITION_SCALE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static float Ink(VertexHelper vh)
    {
        var vertices = new List<UIVertex>(); vh.GetUIVertexStream(vertices); float area = 0;
        for (int i = 0; i < vertices.Count; i += 3)
        {
            Vector3 a = vertices[i+1].position - vertices[i].position, b = vertices[i+2].position - vertices[i].position;
            area += Mathf.Abs(a.x*b.y - a.y*b.x) * .5f * vertices[i].color.a / 255f;
        }
        return area;
    }
    static void CheckSharedTransitionBadge()
    {
        using (var exit = new VertexHelper())
        using (var lever = new VertexHelper())
        {
            var tint = new Color32(65,175,235,255);
            RuntimeMiniMapPointIconGraphic.DrawIcon(exit, MapPointIconShape.SceneTransition, Vector2.zero, 1, tint);
            RuntimeMiniMapPointIconGraphic.DrawIcon(lever, MapPointIconShape.Lever, Vector2.zero, 1, tint);
            Check(exit.currentVertCount == 34, "compact doorway uses only 34 cached vertices");
            // Two five-vertex fans: matching rim, background, opacity and inset.
            for (int i=0;i<10;i++)
            {
                var a = new UIVertex(); var b = new UIVertex();
                exit.PopulateUIVertex(ref a,i); lever.PopulateUIVertex(ref b,i);
                Check(a.position == b.position && a.color.Equals(b.color), "shared diamond badge vertex " + i);
            }
            for (int i=10;i<exit.currentVertCount;i++)
            {
                var v = new UIVertex(); exit.PopulateUIVertex(ref v,i);
                Check(Mathf.Abs(v.position.x)+Mathf.Abs(v.position.y)<1.25f, "glyph does not touch diamond rim " + i);
            }
        }
    }
    static void CheckSameSizeAsPoi(RuntimeMiniMapGraphic map)
    {
        using (var actual = new VertexHelper())
        using (var expected = new VertexHelper())
        {
            Call(map, "PopulateMarkers", actual);
            map.TryWorldToLocal(map.Transitions[0].position, out var center);
            RuntimeMiniMapPointIconGraphic.DrawIcon(expected, MapPointIconShape.Lever,
                center, 6.25f * map.PointIconScale, Color.white);
            for (int i=0;i<10;i++)
            {
                var a = new UIVertex(); var b = new UIVertex();
                actual.PopulateUIVertex(ref a,i); expected.PopulateUIVertex(ref b,i);
                Check((a.position-b.position).sqrMagnitude < .0001f,
                    "exit badge matches actual POI size at zoom " + map.Zoom + " vertex " + i);
            }
        }
    }
    static RuntimeMiniMapGraphic CreateMap(Transform parent, Vector2 position, Vector2 size, float zoom, float icons)
    {
        var viewport = new GameObject("Map viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
        viewport.SetParent(parent, false); viewport.sizeDelta = size; viewport.anchoredPosition = position;
        viewport.GetComponent<Image>().color = Color.black;
        var map = new GameObject("Actual map", typeof(RectTransform), typeof(CanvasRenderer), typeof(RuntimeMiniMapGraphic)).GetComponent<RuntimeMiniMapGraphic>();
        map.transform.SetParent(viewport, false); map.rectTransform.sizeDelta = size;
        map.SetIconScale(icons); map.SetCameraMarkerVisible(false); map.ZoomBy(zoom - map.Zoom); return map;
    }
    static void Legacy(RuntimeMiniMapGraphic map, float icons)
    {
        ((RuntimeMiniMapLayerGraphic)Get(map, "markerLayer")).enabled = false;
        var legacy = new GameObject("Previous transition display", typeof(RectTransform), typeof(CanvasRenderer), typeof(MapTransitionLegacyPreview)).GetComponent<MapTransitionLegacyPreview>();
        legacy.transform.SetParent(map.transform.parent, false); legacy.rectTransform.sizeDelta = map.rectTransform.sizeDelta;
        legacy.Map = map; legacy.Icons = icons;
    }
    static void Label(Transform parent, Vector2 position, string text)
    {
        var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
        label.transform.SetParent(parent, false); label.rectTransform.anchoredPosition = position; label.rectTransform.sizeDelta = new Vector2(620, 40);
        label.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = 22; label.text = text; label.alignment = TextAnchor.MiddleCenter; label.color = ZeldaUiPalette.Primary;
    }
    static void Render(Canvas canvas)
    {
        var camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 500; camera.transform.position = new Vector3(0, 0, -100);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(10, 19, 37, 255); canvas.worldCamera = camera;
        var rt = new RenderTexture(1400, 1000, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var pixels = new Texture2D(1400, 1000, TextureFormat.RGB24, false); pixels.ReadPixels(new Rect(0, 0, 1400, 1000), 0, 0); pixels.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "MapTransitionScale-preview.png"), pixels.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(pixels);
    }
}

public sealed class MapTransitionLegacyPreview : MaskableGraphic
{
    public RuntimeMiniMapGraphic Map;
    public float Icons;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); if (Map == null) return;
        Map.TryWorldToLocal(Vector2.zero, out var origin); Map.TryWorldToLocal(Vector2.right, out var unit);
        float scale = Icons * Mathf.Min(Mathf.Clamp(Map.Zoom/2.5f,.5f,1f),
            Mathf.Clamp(Vector2.Distance(origin,unit)*4f/(28f*Icons),.45f,1f));
        bool compact = scale / Icons < .7f;
        Vector2[] lines = compact ? new[] {
            new Vector2(-8,-12), new Vector2(-8,12), new Vector2(-8,12), new Vector2(5,12),
            new Vector2(-8,-12), new Vector2(5,-12), new Vector2(-3,0), new Vector2(12,0),
            new Vector2(-3,0), new Vector2(3,6), new Vector2(-3,0), new Vector2(3,-6)
        } : new[] {
            new Vector2(-9,-14), new Vector2(-9,14), new Vector2(-9,14), new Vector2(5,14),
            new Vector2(5,14), new Vector2(5,5), new Vector2(5,-5), new Vector2(5,-14),
            new Vector2(5,-14), new Vector2(-9,-14), new Vector2(13,5), new Vector2(-1,5),
            new Vector2(13,-5), new Vector2(-1,-5), new Vector2(13,5), new Vector2(13,-5),
            new Vector2(-1,9), new Vector2(-8,0), new Vector2(-8,0), new Vector2(-1,-9),
            new Vector2(-1,9), new Vector2(-1,5), new Vector2(-1,-9), new Vector2(-1,-5)
        };
        var draw = typeof(RuntimeMiniMapGraphic).GetMethod("AddLine", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var point in Map.Transitions)
            if (Map.TryWorldToLocal(point.position, out var position))
            {
                Color tint = point.preservesSceneState ? (Color)new Color32(65,175,235,255) : ZeldaUiPalette.Ghost;
                tint.a *= Mathf.Lerp(.85f,1f,Mathf.InverseLerp(.45f,.85f,scale/Icons));
                for (int i = 0; i < lines.Length; i += 2)
                    draw.Invoke(null, new object[] { vh, position + lines[i]*scale, position + lines[i+1]*scale,
                        Mathf.Max(.75f, (compact ? 1.8f : 2.4f)*scale), tint });
            }
    }
}
