using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Isolated Unity project only: never opens or saves authored scenes.
[InitializeOnLoad]
public static class MiniMapIncidentTimerSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test;
    static int checks, frame = -1;
    static MiniMapIncidentTimerSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool("MiniMapIncidentTimerSmoke", true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("MiniMapIncidentTimerSmoke", false))
            { SessionState.SetBool("MiniMapIncidentTimerSmoke", false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Clear(); Debug.Log("MINIMAP_INCIDENT_TIMER_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Clear() { for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]); owned.Clear(); }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    static void SetAi(ZeldaCharacterAiBase ai, string field, object value) => typeof(ZeldaCharacterAiBase).GetField(field, Flags).SetValue(ai, value);
    static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = Own(new GameObject(name, typeof(RectTransform))).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    static Image Image(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var image = Rect(name, parent, position, size).gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false; return image;
    }
    static MiniMapIncidentTimerGraphic Timer(Transform parent, Graphic map)
    {
        var rect = Rect("Mini Map Incident Timer", parent, Vector2.zero, Vector2.zero);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.one * 6; rect.offsetMax = Vector2.one * -6;
        var timer = rect.gameObject.AddComponent<MiniMapIncidentTimerGraphic>(); timer.Bind(map); return timer;
    }
    static ZeldaFourWayMover Actor(string name, Vector2 position)
    {
        var go = Own(new GameObject(name, typeof(CivilianZeldaCharacterData), typeof(ZeldaFourWayMover)));
        go.transform.position = position; go.GetComponent<Rigidbody2D>().position = position;
        return go.GetComponent<ZeldaFourWayMover>();
    }
    static void Mesh(MiniMapIncidentTimerGraphic graphic, int coloredEdges)
    {
        using (var vh = new VertexHelper())
        {
            typeof(MiniMapIncidentTimerGraphic).GetMethod("OnPopulateMesh", Flags | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { vh });
            Check(vh.currentVertCount == (graphic.HasIncident ? 16 + 4 * coloredEdges : 0), "one compact mesh with expected remaining edges");
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                var vertex = new UIVertex(); vh.PopulateUIVertex(ref vertex, i);
                var rect = graphic.rectTransform.rect;
                Check(vertex.position.x >= rect.xMin - .001f && vertex.position.x <= rect.xMax + .001f &&
                    vertex.position.y >= rect.yMin - .001f && vertex.position.y <= rect.yMax + .001f, "timer vertices stay in inset bounds");
            }
            if (coloredEdges == 3)
            {
                var v = new UIVertex(); vh.PopulateUIVertex(ref v, 16);
                Check(v.position.x > 0 && v.position.y > 0, "at 75% remaining, top has erased and next edge begins at top-right");
            }
            if (coloredEdges == 2)
            {
                var v = new UIVertex(); vh.PopulateUIVertex(ref v, 16);
                Check(v.position.x > 0 && v.position.y < 0, "at 50%, top/right have erased and next edge begins at bottom-right");
            }
        }
    }
    static IEnumerator Verify()
    {
        var scene = SceneManager.CreateScene("MiniMap Incident Test"); SceneManager.SetActiveScene(scene); Time.timeScale = 1;
        var canvasRect = Rect("Timer isolated canvas", null, Vector2.zero, new Vector2(1280, 720));
        canvasRect.gameObject.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var slot = Rect("Map slot", canvasRect, Vector2.zero, Vector2.one * 104);
        var map = Image("Map", slot, Vector2.zero, Vector2.one * 96, Color.black);
        var graphic = Timer(slot, map);
        Check(!SharedPlayerHostilityTimer.TryGetForScene(scene, out _) && !graphic.HasIncident, "HUD read does not create an incident singleton");
        Check(!graphic.raycastTarget && graphic.transform.GetSiblingIndex() > map.transform.GetSiblingIndex(), "timer overlays map without intercepting input");
        Mesh(graphic, 0);

        var player = Actor("Controlled actor", new Vector2(30, 0)); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        var mover = Actor("Guard", Vector2.zero); mover.enabled = false;
        var ai = mover.gameObject.AddComponent<ZeldaCharacterAiBase>();
        SetAi(ai, "enableAwarenessSound", false); SetAi(ai, "visionRadius", 3f);
        SetAi(ai, "visionObstacleLayers", (LayerMask)0); SetAi(ai, "solidCollisionLayers", (LayerMask)0); SetAi(ai, "facingDirection", Vector2.up);
        ai.OnCharacterDamagedBy(player.CharacterData);
        var clock = SharedPlayerHostilityTimer.ForScene(scene); owned.Add(clock.gameObject);
        graphic.RefreshFromScene();
        Check(graphic.DisplayedPhase == SharedPlayerHostilityTimer.IncidentPhase.Hostile && Near(graphic.Remaining01, 1) &&
              graphic.color == MiniMapIncidentTimerGraphic.HostileColor, "hostility begins with full red ring"); Mesh(graphic, 4);
        clock.Advance(2.25f); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .75f), "red ring follows shared clock exactly"); Mesh(graphic, 3);
        clock.Advance(2.25f); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .5f), "red half time"); Mesh(graphic, 2);
        Time.timeScale = 0; clock.Advance(5); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .5f), "pause freezes ring with gameplay clock"); Time.timeScale = 1;
        player.transform.position = new Vector2(0, 1); player.GetComponent<Rigidbody2D>().position = player.transform.position; Physics2D.SyncTransforms();
        clock.Advance(.01f); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, 1), "being seen refills red ring immediately");
        player.transform.position = new Vector2(30, 0); player.GetComponent<Rigidbody2D>().position = player.transform.position; Physics2D.SyncTransforms();
        clock.Advance(9); graphic.RefreshFromScene();
        Check(graphic.DisplayedPhase == SharedPlayerHostilityTimer.IncidentPhase.Search && Near(graphic.Remaining01, 1) &&
              graphic.color == MiniMapIncidentTimerGraphic.SearchColor, "search switches to full yellow ring");
        clock.Advance(6); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .5f), "yellow ring uses search duration"); Mesh(graphic, 2);
        map.gameObject.SetActive(false); graphic.RefreshFromScene(); Check(!graphic.HasIncident, "hidden minimap also hides countdown");
        map.gameObject.SetActive(true); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .5f), "showing map resumes existing fraction, not new timer");
        var otherScene = SceneManager.CreateScene("Empty Other Floor"); SceneManager.SetActiveScene(otherScene);
        graphic.RefreshFromScene(); Check(!graphic.HasIncident && !SharedPlayerHostilityTimer.TryGetForScene(otherScene, out _), "scene switch hides old incident without creating another clock");
        SceneManager.SetActiveScene(scene); graphic.RefreshFromScene(); Check(Near(graphic.Remaining01, .5f), "return to scene reads its own countdown");
        clock.Advance(6); graphic.RefreshFromScene(); Check(!graphic.HasIncident, "search end removes ring completely"); Mesh(graphic, 0);

        ai.OnCharacterDamagedBy(player.CharacterData); clock.Advance(3); Time.timeScale = 0;
        var hudObject = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaHealthHeartsUI.prefab")));
        var hud = hudObject.GetComponent<ZeldaHealthHeartsUI>(); hud.enabled = false;
        var rings = hud.GetComponentsInChildren<MiniMapIncidentTimerGraphic>(true);
        Check(rings.Length == 1, "production HUD creates exactly one timer");
        var production = rings[0]; var mapRect = production.transform.parent.Find("Task Mini Map Viewport/Task Real-Time Mini Map");
        mapRect.gameObject.SetActive(true); production.SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Idle, 0);
        yield return null; yield return null;
        Check(!hud.enabled && production.DisplayedPhase == clock.Phase && Near(production.Remaining01, clock.Remaining / clock.HostileDuration),
            "timer keeps refreshing when event-driven parent HUD script sleeps");
        Check(production.rectTransform.offsetMin == Vector2.one * 6 && production.rectTransform.offsetMax == Vector2.one * -6,
            "timer sits inside the original cyan frame");
        hudObject.SetActive(false); canvasRect.gameObject.SetActive(false);
        yield return null;
        Preview();
    }

    static void Preview()
    {
        const int width = 1100, height = 350;
        var canvasRect = Rect("Timer visual preview", null, Vector2.zero, new Vector2(width, height));
        var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
        var graphics = new MiniMapIncidentTimerGraphic[4];
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        string[] labels = { "敌对 · 100%", "敌对 · 50%", "搜索 · 75%", "搜索 · 25%" };
        for (int i = 0; i < 4; i++)
        {
            float x = -402 + 268 * i;
            var slot = Rect("Map frame", canvasRect, new Vector2(x, 16), Vector2.one * 104); slot.localScale = Vector3.one * 2;
            var map = Image("Map background", slot, Vector2.zero, Vector2.one * 104, Color.black);
            Image("Top", slot, new Vector2(0, 51), new Vector2(104, 1), ZeldaUiPalette.Primary);
            Image("Bottom", slot, new Vector2(0, -51), new Vector2(104, 1), ZeldaUiPalette.Primary);
            Image("Left", slot, new Vector2(-51, 0), new Vector2(1, 104), ZeldaUiPalette.Primary);
            Image("Right", slot, new Vector2(51, 0), new Vector2(1, 104), ZeldaUiPalette.Primary);
            Image("Wall a", slot, new Vector2(-22, 6), new Vector2(2, 56), Color.white);
            Image("Wall b", slot, new Vector2(-3, 34), new Vector2(40, 2), Color.white);
            Image("Wall c", slot, new Vector2(28, -14), new Vector2(2, 38), Color.white);
            Image("Wall d", slot, new Vector2(9, -32), new Vector2(40, 2), Color.white);
            var marker = Image("Player", slot, Vector2.zero, Vector2.one * 3, ZeldaUiPalette.Primary);
            marker.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            graphics[i] = Timer(slot, map);
            var label = Rect(labels[i], canvasRect, new Vector2(x, -125), new Vector2(240, 40)).gameObject.AddComponent<Text>();
            label.font = font; label.text = labels[i]; label.fontSize = 20; label.alignment = TextAnchor.MiddleCenter; label.color = ZeldaUiPalette.Primary;
        }
        graphics[0].SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Hostile, 1);
        graphics[1].SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Hostile, .5f);
        graphics[2].SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Search, .75f);
        graphics[3].SetDisplay(SharedPlayerHostilityTimer.IncidentPhase.Search, .25f);
        var camera = Own(new GameObject("Preview camera", typeof(Camera))).GetComponent<Camera>();
        camera.enabled = false; camera.orthographic = true; camera.orthographicSize = height * .5f;
        camera.transform.position = new Vector3(0, 0, -100); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color32(12, 20, 45, 255); canvas.worldCamera = camera;
        var rt = new RenderTexture(width, height, 24); camera.targetTexture = rt;
        Canvas.ForceUpdateCanvases(); camera.Render(); var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        int red = 0, yellow = 0;
        foreach (Color32 pixel in image.GetPixels32())
        {
            if (pixel.r > 220 && pixel.g < 100 && pixel.b < 100) red++;
            if (pixel.r > 220 && pixel.g > 170 && pixel.b < 100) yellow++;
        }
        Check(red > 3000 && yellow > 2000, "real GPU preview renders both countdown colors");
        File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "MiniMapIncidentTimer-preview.png"), image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null; rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
    }
}
