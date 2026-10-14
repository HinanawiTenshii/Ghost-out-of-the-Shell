using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run in the isolated Unity smoke project, not the user's open scene.
[InitializeOnLoad]
public static class SandwormBossHudSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Pending = "SandwormBossHudSmoke.Pending";
    static readonly List<GameObject> owned = new List<GameObject>();
    static IEnumerator test; static int frame = -1, checks;
    static SandwormBossHudSmoke() { EditorApplication.update += Tick; }
    public static void Run() { SessionState.SetBool(Pending, true); EditorApplication.EnterPlaymode(); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Pending, false)) { SessionState.SetBool(Pending, false); test = Verify(); }
            if (test == null || test.MoveNext()) return;
            test = null; Cleanup(); Debug.Log("SANDWORM_BOSS_HUD_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { test = null; Cleanup(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static bool Near(float a, float b) => Mathf.Abs(a - b) < .001f;
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    static void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        foreach (var effect in Object.FindObjectsOfType<SandwormDeathSequence>()) Object.DestroyImmediate(effect.gameObject);
    }
    static Font PixelFont => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf");
    static SandwormZeldaCharacterData Worm()
    {
        var go = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-BossPrototype.prefab")));
        go.transform.position = Vector3.zero;
        var motion = go.GetComponent<SandwormBossPrototype>(); motion.ResetPose(); motion.SetMovementEnabled(false);
        var data = go.GetComponent<SandwormZeldaCharacterData>();
        var state = data.CaptureRuntimeState(); state.health = state.currentHealth = 100; data.ApplyRuntimeState(state);
        return data;
    }
    static ZeldaFourWayMover Player()
    {
        var go = Own(new GameObject("HUD controlled actor")); go.SetActive(false);
        go.AddComponent<CivilianZeldaCharacterData>(); var mover = go.AddComponent<ZeldaFourWayMover>();
        go.SetActive(true); ZeldaRuntimeRegistry.ClaimControlledMover(mover); return mover;
    }
    static Camera MainCamera(Vector2 position)
    {
        var camera = Own(new GameObject("HUD main camera", typeof(Camera))).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 3f; camera.aspect = 16f / 9f;
        camera.transform.position = new Vector3(position.x, position.y, -30); return camera;
    }
    static Text Label(Transform parent, string name, string content, float y, int fontSize, float height)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>(); text.text = content; text.font = PixelFont; text.fontSize = fontSize;
        text.color = ZeldaUiPalette.Primary; text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow;
        var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 1f);
        rect.anchoredPosition = new Vector2(0, y); rect.sizeDelta = new Vector2(200, height); return text;
    }
    static RuntimeSandwormBossBar Hud()
    {
        var canvas = Own(new GameObject("Boss HUD test canvas", typeof(RectTransform), typeof(Canvas)));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        ((RectTransform)canvas.transform).sizeDelta = new Vector2(1280, 720);
        Label(canvas.transform, "Area name", "区域等级", -8, 13, 20);
        Label(canvas.transform, "Area number", "0", -36, 56, 66);
        var status = Label(canvas.transform, "Area status", "合法区域", -106, 13, 20);
        var go = new GameObject("Boss bar", typeof(RectTransform), typeof(RuntimeSandwormBossBar));
        go.transform.SetParent(canvas.transform, false); var bar = go.GetComponent<RuntimeSandwormBossBar>();
        bar.Build(status.rectTransform, PixelFont); return bar;
    }
    static void Age(RuntimeSandwormBossBar bar, float seconds) => bar.Advance(seconds);
    static void MeshCheck(RuntimeSandwormBossBar bar)
    {
        using (var mesh = new VertexHelper())
        {
            var graphic = bar.BarGraphic;
            Check(graphic.GetComponent<CanvasRenderer>() != null, "custom HUD mesh has required CanvasRenderer");
            typeof(SandwormBossBarGraphic).GetMethod("OnPopulateMesh", Flags | BindingFlags.DeclaredOnly).Invoke(graphic, new object[] { mesh });
            Check(mesh.currentVertCount <= 36, "single compact mesh, at most nine rectangular quads");
            var a = new UIVertex(); var b = new UIVertex();
            mesh.PopulateUIVertex(ref a, mesh.currentVertCount - 4); mesh.PopulateUIVertex(ref b, mesh.currentVertCount - 2);
            float fullWidth = graphic.rectTransform.rect.width - 20;
            Check(Near(a.position.x, -fullWidth * .5f), "real health remains anchored at left edge");
            Check(Near(b.position.x - a.position.x, fullWidth * bar.HealthFraction), "real width follows health percentage");
            Check(!graphic.raycastTarget && !bar.GetComponent<CanvasGroup>().blocksRaycasts, "boss HUD does not intercept input");
        }
    }
    static IEnumerator Verify()
    {
        var scene = SceneManager.CreateScene("Boss HUD gameplay"); SceneManager.SetActiveScene(scene); Time.timeScale = 0;
        var worm = Worm(); var motion = worm.GetComponent<SandwormBossPrototype>(); var player = Player();
        var camera = MainCamera(new Vector2(200, 200)); var bar = Hud();
        bar.Advance(0); Check(!bar.IsEncounterVisible, "hidden before screen encounter");
        camera.transform.position = motion.Tail.position + Vector3.down * 3.5f + Vector3.back * 30;
        player.transform.position = (Vector2)camera.transform.position;
        typeof(ZeldaCharacterCommonData).GetField("playerVisionRadius", Flags).SetValue(player.GetComponent<ZeldaCharacterData>(), .5f);
        var vision = camera.gameObject.AddComponent<CameraCircularVision>(); vision.RefreshSceneCameraSettings();
        bar.Advance(0); Check(!bar.IsEncounterVisible, "masked on-screen body cannot reveal boss HUD");
        typeof(ZeldaCharacterCommonData).GetField("playerVisionRadius", Flags).SetValue(player.GetComponent<ZeldaCharacterData>(), 2f);
        vision.RefreshSceneCameraSettings();
        var wall = Own(new GameObject("Vision barrier", typeof(BoxCollider2D))); wall.layer = LayerMask.NameToLayer("Blocks");
        wall.transform.position = motion.Tail.position + Vector3.down * 3;
        wall.GetComponent<BoxCollider2D>().size = new Vector2(80, .1f);
        Physics2D.SyncTransforms(); CameraCircularVision.NotifyBlockersChanged(); vision.RefreshSceneCameraSettings();
        bar.Advance(0); Check(!bar.IsEncounterVisible, "Blocks occlusion cannot reveal boss HUD");
        wall.SetActive(false); Physics2D.SyncTransforms(); CameraCircularVision.NotifyBlockersChanged(); vision.RefreshSceneCameraSettings();
        var head = motion.Head.GetComponent<SpriteRenderer>();
        Check(!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), head.bounds), "head outside camera in tail-only fixture");
        bar.Advance(0); Check(bar.IsEncounterVisible && bar.Target == worm, "first visible tail portion triggers encounter");
        var title = bar.GetComponentInChildren<Text>(true);
        Check(title.text == "沙虫" && title.font == PixelFont && title.enabled && !bar.BarGraphic.enabled, "pixel name flashes first, no early brackets");
        Age(bar, .11f); Check(!title.enabled, "name flash off"); Age(bar, .1f); Check(title.enabled, "name flash on");
        Age(bar, .4f); Check(title.enabled && !bar.BarGraphic.enabled, "name settles before bracket reveal");
        Age(bar, .13f); Check(bar.BarGraphic.enabled && Near(bar.BarGraphic.Expansion, 0), "closely spaced initial brackets");
        Age(bar, .3f); Check(Near(bar.BarGraphic.Expansion, .5f) && Near(bar.BarGraphic.HealthFraction, 0), "brackets unfold before any health filling");
        Age(bar, .2f); Check(Near(bar.BarGraphic.Expansion, 1) && Near(bar.BarGraphic.HealthFraction, 0), "fully expanded empty background before fill");
        Age(bar, .275f); Check(Near(bar.BarGraphic.HealthFraction, .5f), "left-to-right progressive health filling");
        Age(bar, .3f); Check(bar.IntroComplete && Near(bar.BarGraphic.HealthFraction, 1), "intro completes at actual full health");
        Check(Near(((RectTransform)bar.transform).anchoredPosition.y, -136), "health bar below permission UI with ten-pixel gap");
        camera.transform.position += Vector3.right * 200;
        bar.Advance(0); Check(bar.IsEncounterVisible && bar.IntroComplete, "leaving screen cannot reset encounter");
        worm.gameObject.SetActive(false); bar.Advance(0);
        Check(bar.IsEncounterVisible && bar.Target == worm, "distance streaming disable preserves discovered HUD");
        worm.gameObject.SetActive(true);
        worm.TakeDamage(25); Check(Near(bar.HealthFraction, .75f) && Near(bar.DelayedFraction, 1), "damage updates immediately with retained ghost health");
        Age(bar, .3f); Check(Near(bar.DelayedFraction, 1), "ghost holds during damage delay");
        worm.TakeDamage(25); Age(bar, .54f); Check(Near(bar.HealthFraction, .5f) && Near(bar.DelayedFraction, 1), "successive hit restarts delay");
        Age(bar, .335f); Check(Near(bar.DelayedFraction, .75f), "ghost eases down after quiet interval"); MeshCheck(bar);
        Age(bar, .34f); Check(Near(bar.DelayedFraction, .5f), "ghost catches actual health, leaving dark track");
        worm.RestoreHealth(10); Check(Near(bar.HealthFraction, .6f) && bar.DelayedFraction >= .6f, "healing never leaves ghost below health");
        bar.enabled = false; worm.TakeDamage(10); bar.enabled = true; bar.Advance(0);
        Check(Near(bar.HealthFraction, .5f), "resume reconciles health missed while HUD disabled");
        var canvasRect = (RectTransform)bar.transform.parent; canvasRect.sizeDelta = new Vector2(640, 720); bar.Advance(0);
        Check(bar.BarGraphic.rectTransform.rect.width < 300, "narrow canvas scales boss track safely");
        canvasRect.sizeDelta = new Vector2(1280, 720);
        var second = SceneManager.CreateScene("Boss HUD other scene"); SceneManager.SetActiveScene(second); bar.Advance(0);
        Check(!bar.IsEncounterVisible, "scene transition clears visit-local encounter");
        SceneManager.SetActiveScene(scene); camera.transform.position = motion.Tail.position + Vector3.back * 30;
        player.transform.position = motion.Tail.position; vision.RefreshSceneCameraSettings(); bar.Advance(0);
        Check(bar.IsEncounterVisible && !bar.IntroComplete && Near(bar.HealthFraction, .5f), "returning visit rediscovers living boss at current health");
        worm.TakeDamage(10); Age(bar, 2); Check(Near(bar.BarGraphic.HealthFraction, .4f), "damage during intro cannot fabricate full health");
        worm.TakeDamage(1000); Check(Near(bar.HealthFraction, 0) && bar.DelayedFraction > 0, "lethal health event retained before source destruction");
        yield return null;
        Check(worm == null && bar.IsEncounterVisible, "death visuals/source destruction do not prematurely hide ghost trail");
        Age(bar, .56f); Check(bar.DelayedFraction > 0, "lethal ghost also respects hold");
        for (int i = 0; i < 65; i++) Age(bar, .01f);
        Check(Near(bar.DelayedFraction, 0) && bar.IsEncounterVisible, "empty dark track remains briefly after lethal trail finishes");
        Age(bar, 1); Check(!bar.IsEncounterVisible, "defeated boss HUD fades away");
        Cleanup(); yield return null;

        // The production HUD creates the component without editing any scenes/prefabs.
        player = Player(); worm = Worm(); camera = MainCamera(worm.GetComponent<SandwormBossPrototype>().HeadPosition);
        var hud = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaHealthHeartsUI.prefab")));
        yield return null;
        var bars = hud.GetComponentsInChildren<RuntimeSandwormBossBar>(true);
        Check(bars.Length == 1, "production persistent HUD auto-installs exactly one boss bar"); bar = bars[0];
        Check(bar.GetComponentInChildren<Text>(true).font == PixelFont, "production font passed through");
        bar.Advance(0); Check(bar.IsEncounterVisible, "production HUD discovers screen-visible boss");
        hud.GetComponent<ZeldaHealthHeartsUI>().enabled = false;
        Time.timeScale = 1; Time.captureFramerate = 60;
        for (int i = 0; i < 115; i++) yield return null;
        Check(bar.IntroComplete, "boss animation continues when idle base HUD disables its own Update");
        Time.timeScale = 0; Time.captureFramerate = 0; Cleanup(); yield return null;

        // Actual shader/font rendering: half-open, filling, damaged/trailing, settled.
        worm = Worm(); player = Player(); camera = MainCamera(worm.GetComponent<SandwormBossPrototype>().HeadPosition); bar = Hud();
        bar.Advance(0); bar.enabled = false;
        var preview = Own(new GameObject("HUD preview camera", typeof(Camera))).GetComponent<Camera>();
        preview.enabled = false; preview.orthographic = true; preview.orthographicSize = 112;
        preview.transform.position = new Vector3(0, 252, -500); preview.clearFlags = CameraClearFlags.SolidColor;
        preview.backgroundColor = new Color32(15, 23, 53, 255);
        const int width = 720, height = 280;
        var rt = new RenderTexture(width, height, 24); preview.targetTexture = rt;
        var image = new Texture2D(width, height * 4, TextureFormat.RGB24, false);
        float[] advances = { 1.04f, .475f, .4f, 1.25f };
        for (int row = 0; row < advances.Length; row++)
        {
            Age(bar, advances[row]);
            if (row == 2) { worm.TakeDamage(40); bar.Advance(0); }
            Canvas.ForceUpdateCanvases(); yield return null;
            preview.Render(); RenderTexture.active = rt;
            image.ReadPixels(new Rect(0, 0, width, height), 0, (3 - row) * height); RenderTexture.active = null;
        }
        image.Apply(); int cyan = 0, magenta = 0;
        foreach (var p in image.GetPixels32()) { if (p.g > 170 && p.b > 170) cyan++; if (p.r > 180 && p.b > 180 && p.g < 40) magenta++; }
        Check(cyan > 1000 && magenta == 0, "actual HUD graphics render without missing shaders");
        var pixels = image.GetPixels32(); int cyanBar = 0, paleTrail = 0;
        // Bottom row's bar band is below the name; counting elsewhere could
        // accidentally pass on the title/permission text with a missing mesh.
        for (int y = 40; y < 65; y++) for (int x = 100; x < 620; x++)
        {
            Color32 p = pixels[y * width + x];
            if (p.g > 150 && p.b > 150) cyanBar++;
            Color32 trailPixel = pixels[(height + y) * width + x];
            if (trailPixel.r > 140 && trailPixel.g > 210 && trailPixel.b > 210) paleTrail++;
        }
        Check(cyanBar > 1000 && paleTrail > 500, "GPU-rendered real and pale ghost bars are both visibly present below name");
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "../Sandworm-boss-hud-preview.png"), image.EncodeToPNG());
        preview.targetTexture = null; Object.DestroyImmediate(rt); Object.DestroyImmediate(image);
        Cleanup(); yield return null;
    }
}
