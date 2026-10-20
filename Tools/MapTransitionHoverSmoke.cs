using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Real menu, shared lever pointer handlers, raycasts, panning and lifecycle.
[InitializeOnLoad]
public static class MapTransitionHoverSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator steps;
    static int checks, frame = -1;
    static double deadline;
    static MapTransitionHoverSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static object Get(object o, string field) => o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, F).Invoke(o, args);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "map-transition-hover-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), "Assets/MapTransitionHoverFixture.unity");
        SessionState.SetBool(nameof(MapTransitionHoverSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(MapTransitionHoverSmoke), false))
            { SessionState.SetBool(nameof(MapTransitionHoverSmoke), false); deadline = EditorApplication.timeSinceStartup + 90; steps = Verify(); }
            if (steps == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            if (steps.MoveNext()) return;
            Debug.Log("MAP_TRANSITION_HOVER_PASS checks=" + checks); steps = null; EditorApplication.Exit(0);
        }
        catch (Exception e) { steps = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        var group = new GameObject("Test map group").AddComponent<LevelSceneGroup>();
        var entries = new List<LevelSceneGroup.SceneEntry>();
        foreach (string scene in new[] { SceneManager.GetActiveScene().name, "Level2-Floor1", "Level2-Floor2" })
        {
            var entry = new LevelSceneGroup.SceneEntry(); Set(entry, "sceneName", scene); entries.Add(entry);
        }
        Set(group, "scenes", entries); group.Register();
        var root = new GameObject("Menu", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        root.SetActive(false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var menu = root.AddComponent<TabJournalMenuController>();
        Set(menu, "menuFont", AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/ZLabsPixel_12px_M_CN.ttf"));
        root.SetActive(true);
        yield return null;
        while (DocumentReader.IsInputBlocked) yield return null;
        // Use the normal lifecycle; no gameplay scene or save data is needed.
        menu.OpenMenu(); Check(TabJournalMenuController.IsOpen, "real menu opens");
        var map = (RuntimeMiniMapGraphic)Get(menu, "miniMapGraphic");
        var data = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>();
        data.Configure(SceneManager.GetActiveScene().name, "Hover test", new[] { new[] { new Vector2(-50,-50), new Vector2(50,50) } },
            new[] { new RuntimeMiniMapTransitionData { position = Vector2.zero, preservesSceneState = true, targetSceneName = "Level2-Floor2" },
                new RuntimeMiniMapTransitionData { position = new Vector2(20,0), targetSceneName = "Trial" } }, null);
        map.RebuildFromSnapshot(data);
        Call(menu, "RebuildMiniMapTransitionHovers", SceneManager.GetActiveScene());
        Call(menu, "UpdateMiniMapTransitionPositions");
        yield return null;
        var layer = (RectTransform)Get(menu, "mapTransitionHoverLayer");
        var tooltip = (GameObject)Get(menu, "mapPointTooltipRoot");
        var text = (Text)Get(menu, "mapPointTooltipText");
        var tooltipRect = (RectTransform)Get(menu, "mapPointTooltipRect");
        var targets = layer.GetComponentsInChildren<MapPointOfInterestUiInteraction>(true);
        Check(targets.Length == 2, "persistent and one-way exits both have hover targets");
        Check(layer.GetComponentsInChildren<Text>(true).Length == 0, "no automatic destination Text objects");
        var pointer = new PointerEventData(EventSystem.current);
        foreach (float zoom in new[] { 1f, 2.5f, 5f })
        {
            map.ZoomBy(zoom - map.Zoom); Call(menu, "UpdateMiniMapTransitionPositions"); Canvas.ForceUpdateCanvases();
            Check(!tooltip.activeSelf, "no automatic tooltip at zoom " + zoom);
            var rect = (RectTransform)targets[0].transform;
            Check(rect.sizeDelta == Vector2.one * 25f * map.PointIconScale,
                "transition hitbox follows ordinary POI size at zoom " + zoom);
            pointer.position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.position);
            var results = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, results);
            Check(results.Count > 0 && results[0].gameObject == targets[0].gameObject, "small exit is the top raycast target at " + zoom);
            targets[0].OnPointerEnter(pointer);
            Check(tooltip.activeSelf && text.text.StartsWith("前往") && text.text != "前往Level2-Floor2", "hover resolves destination display name");
            Check(tooltipRect.sizeDelta.y == 32 && text.alignment == TextAnchor.MiddleCenter, "compact lever-style window");
            Check(tooltip.GetComponentsInChildren<Graphic>().Length > 1, "tooltip reuses framed UI");
            foreach (var graphic in tooltip.GetComponentsInChildren<Graphic>()) Check(!graphic.raycastTarget, "tooltip does not steal hover");
            targets[0].OnPointerClick(pointer); Check(tooltip.activeSelf, "click leaves hovered info visible without selecting a POI");
            targets[0].OnPointerExit(pointer); Check(!tooltip.activeSelf, "pointer exit hides info");
        }
        map.ResetView(); Call(menu, "UpdateMiniMapTransitionPositions");
        targets[0].OnPointerEnter(pointer);
        Vector2 before = ((RectTransform)targets[0].transform).anchoredPosition;
        pointer.delta = new Vector2(15,10);
        ExecuteEvents.ExecuteHierarchy(targets[0].gameObject, pointer, ExecuteEvents.dragHandler);
        Call(menu, "UpdateMiniMapTransitionPositions");
        Check(((RectTransform)targets[0].transform).anchoredPosition != before, "exit target allows viewport dragging and follows pan");
        pointer.scrollDelta = Vector2.up;
        float oldZoom = map.Zoom;
        ExecuteEvents.ExecuteHierarchy(targets[0].gameObject, pointer, ExecuteEvents.scrollHandler);
        Check(map.Zoom > oldZoom, "exit target forwards scroll to map");
        map.PanByPixels(Vector2.one * 99999); Call(menu, "UpdateMiniMapTransitionPositions");
        Check(!targets[0].gameObject.activeSelf && !tooltip.activeSelf, "offscreen exit disables and clears tooltip");
        map.ResetView(); Call(menu, "UpdateMiniMapTransitionPositions");
        targets[0].OnPointerEnter(pointer); targets[1].OnPointerEnter(pointer); targets[0].OnPointerExit(pointer);
        Check(tooltip.activeSelf && text.text == "前往Trial", "late exit from previous point cannot hide current point");

        var lever = new GameObject("Test Lever").AddComponent<MapPointOfInterest>();
        Set(lever, "pointTitle", "拉杆"); Set(lever, "iconShape", MapPointIconShape.Lever);
        lever.transform.position = new Vector3(-10,0,0); lever.ForceDiscover();
        // Rebuild only the POI layer, keeping transition hover intact.
        Call(menu, "RebuildMiniMapPoints"); Call(menu, "UpdateMiniMapPointPositions");
        Check(tooltip.activeSelf && text.text == "前往Trial", "POI refresh preserves exit tooltip");
        var pointLayer = (RectTransform)Get(menu, "mapPointLayer");
        MapPointOfInterestUiInteraction leverUi = null;
        foreach (var candidate in pointLayer.GetComponentsInChildren<MapPointOfInterestUiInteraction>())
            if (candidate.name.EndsWith(lever.PointId)) leverUi = candidate;
        Check(leverUi != null, "lever hover target remains present");
        Call(menu, "UpdateMiniMapTransitionPositions");
        Check(((RectTransform)leverUi.transform).sizeDelta == ((RectTransform)targets[0].transform).sizeDelta,
            "exit and real lever use identical hitbox dimensions");
        leverUi.OnPointerEnter(pointer); targets[1].OnPointerExit(pointer);
        Check(tooltip.activeSelf && text.text == "拉杆", "lever still shows its existing info, old transition exit is inert");
        Check(tooltipRect.sizeDelta == new Vector2(76,32), "lever dimensions unchanged");
        targets[0].OnPointerEnter(pointer); leverUi.OnPointerExit(pointer);
        Check(tooltip.activeSelf && text.text.StartsWith("前往"), "transition takes over shared tooltip from lever");
        Call(menu, "SetSelectedTab", 1); Check(!tooltip.activeSelf, "changing tab clears tooltip");
        Call(menu, "SetSelectedTab", 0);
        Call(menu, "SelectMapScene", "Level2-Floor1"); yield return null;
        Check(!tooltip.activeSelf && map.Transitions.Count > 10, "real region selection keeps every exit but no automatic info");
        map.ZoomBy(1f-map.Zoom); Call(menu, "UpdateMiniMapTransitionPositions");
        targets = layer.GetComponentsInChildren<MapPointOfInterestUiInteraction>();
        Check(targets.Length > 10, "actual Floor1 overview has selectable exits");
        targets[0].OnPointerEnter(pointer); Check(tooltip.activeSelf, "real snapshot exit hover works");
        menu.CloseMenu(); Check(!tooltip.activeSelf && Get(menu,"hoveredMapPointId") == null, "closing menu clears hovered identity and info");
        while (DocumentReader.IsInputBlocked) yield return null;
        menu.OpenMenu(); Check(!tooltip.activeSelf, "reopening has no stale destination popup");
        menu.CloseMenu(); Object.Destroy(data); Object.Destroy(root);
    }
}
