using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MapUiOptimizationSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
    static int checks;
    static Canvas persistent;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception("Map/UI regression: " + message); }
    static object Field(object owner, string name) => owner.GetType().GetField(name, F).GetValue(owner);
    static void Configure(Camera cam) => typeof(CRTScreenEffect).GetMethod("ConfigureRegisteredCanvases", F).Invoke(null, new object[] { cam, false });
    static void Render()
    {
        Canvas.ForceUpdateCanvases();
        foreach (var cam in Object.FindObjectsOfType<Camera>().Where(c => c.isActiveAndEnabled).OrderBy(c => c.depth)) cam.Render();
    }
    public static IEnumerator VerifyFloor2()
    {
        var map = ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeMiniMapGraphic>();
        var wall = (RuntimeMiniMapLayerGraphic)Field(map, "wallLayer");
        var terrain = (RuntimeMiniMapLayerGraphic)Field(map, "surfaceLayer");
        Render(); yield return null; Render();
        int w = wall.MeshBuildCount, t = terrain.MeshBuildCount;
        var camera = Camera.main;
        Vector3 original = camera.transform.position;
        for (int i = 0; i < 40; i++)
        {
            camera.transform.position = original + new Vector3(i * .07f, i * .02f);
            map.PanByPixels(new Vector2(.4f, .2f));
            Render();
            yield return null;
        }
        Check(wall.MeshBuildCount == w && terrain.MeshBuildCount == t, "real camera renders and panning reuse static meshes");
        camera.transform.position = original;
        map.ZoomBy(-.5f); Render();
        Check(wall.MeshBuildCount > w && terrain.MeshBuildCount > t, "zoom still regenerates both layers");
        w = wall.MeshBuildCount; t = terrain.MeshBuildCount;
        Vector2 previous = map.rectTransform.offsetMax;
        map.rectTransform.offsetMax = previous + new Vector2(40, -30); Render();
        Check(wall.MeshBuildCount > w && terrain.MeshBuildCount > t, "actual viewport resize regenerates both layers");
        map.rectTransform.offsetMax = previous; map.ResetView(); Render();
        w = wall.MeshBuildCount; map.gameObject.SetActive(false); map.gameObject.SetActive(true); Render();
        Check(wall.MeshBuildCount > w && wall.isActiveAndEnabled && terrain.isActiveAndEnabled, "map re-enables without blank cached layers");

        var uiCam = CRTScreenEffect.FindActiveUiCamera();
        var hiddenObject = new GameObject("Deferred Canvas Test", typeof(RectTransform), typeof(Canvas));
        var hidden = hiddenObject.GetComponent<Canvas>(); hidden.renderMode = RenderMode.ScreenSpaceOverlay;
        hiddenObject.SetActive(false); CRTScreenEffect.RegisterCanvas(hidden, true); Configure(uiCam);
        Check(hidden.renderMode == RenderMode.ScreenSpaceOverlay, "inactive canvas configuration deferred");
        hiddenObject.SetActive(true); Configure(uiCam);
        Check(hidden.renderMode == RenderMode.ScreenSpaceCamera && hidden.worldCamera == uiCam, "first activation binds before rendering: mode=" + hidden.renderMode + " active=" + hidden.isActiveAndEnabled + " expected=" + uiCam + " actual=" + hidden.worldCamera);
        hidden.enabled = false; Configure(uiCam);
        var child = new GameObject("New Hidden Child", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        child.transform.SetParent(hidden.transform, false); child.layer = 0;
        hidden.enabled = true; Configure(uiCam);
        Check(child.layer == LayerMask.NameToLayer("UI"), "Canvas.enabled activation includes children built while hidden");
        Check(hidden.GetComponent<RuntimeUiGlowScope>() != null, "popup glow opt-in preserved");
        CRTScreenEffect.RegisterCanvas(hidden, false);
        Check(!hidden.GetComponent<RuntimeUiGlowScope>().enabled, "HUD/no-glow opt-out preserved");

        var bypassObject = new GameObject("Overlay Bypass Test", typeof(RectTransform), typeof(Canvas), typeof(CRTCanvasBypass));
        var bypass = bypassObject.GetComponent<Canvas>(); bypass.renderMode = RenderMode.ScreenSpaceCamera; bypass.worldCamera = uiCam;
        CRTScreenEffect.RegisterCanvas(bypass); Configure(uiCam);
        Check(bypass.renderMode == RenderMode.ScreenSpaceOverlay && bypass.worldCamera == null, "scene reveal bypass remains overlay");
        var worldObject = new GameObject("World Canvas Test", typeof(RectTransform), typeof(Canvas));
        var world = worldObject.GetComponent<Canvas>(); world.renderMode = RenderMode.WorldSpace;
        CRTScreenEffect.RegisterCanvas(world); Configure(uiCam);
        Check(world.renderMode == RenderMode.WorldSpace, "world-space labels unchanged");

        var pause = Object.FindObjectOfType<PauseMenuController>(); var menu = Object.FindObjectOfType<TabJournalMenuController>();
        float oldScale = Time.timeScale; pause.OpenMenu(); Render();
        Check(PauseMenuController.IsPaused && Time.timeScale == 0 && Cursor.visible, "pause interaction preserved");
        Check(pause.GetComponent<Canvas>().sortingOrder == short.MaxValue && pause.GetComponent<Canvas>().worldCamera == uiCam, "pause priority bound on open");
        pause.CloseMenu(); Check(Time.timeScale == oldScale, "pause resumes");
        menu.OpenMenu(); Render();
        Check(TabJournalMenuController.IsOpen && menu.GetComponent<Canvas>().worldCamera == uiCam && menu.GetComponent<RuntimeUiGlowScope>() != null, "Tab map still has CRT camera and glow");
        menu.CloseMenu();
        Object.Destroy(bypassObject); Object.Destroy(worldObject); Object.Destroy(hiddenObject);

        persistent = new GameObject("Persistent Canvas Regression", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        persistent.renderMode = RenderMode.ScreenSpaceOverlay;
        Object.DontDestroyOnLoad(persistent.gameObject); CRTScreenEffect.RegisterCanvas(persistent);
        Debug.Log("MAP_UI_OPTIMIZATION_FLOOR2_PASS checks=" + checks);
    }
    public static void VerifySceneTravel()
    {
        Render();
        Check(persistent != null && persistent.worldCamera == CRTScreenEffect.FindActiveUiCamera(), "persistent canvas rebinds after scene travel");
        Object.Destroy(persistent.gameObject);
        Debug.Log("MAP_UI_OPTIMIZATION_PASS checks=" + checks);
    }
}
