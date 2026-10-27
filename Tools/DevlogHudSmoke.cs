using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run against a disposable project; never rewrite the authored demo scene.
[InitializeOnLoad]
public static class DevlogHudSmoke
{
    const string Key = nameof(DevlogHudSmoke);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static DevlogHudSmoke()
    {
        Application.logMessageReceived += Log;
        EditorApplication.update += Tick;
    }
    public static void RunBefore() => Begin(true);
    public static void Run() => Begin(false);
    static void Begin(bool baseline)
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("Isolated project only");
        PlayerSettings.productName = "devlog-hud-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/DevlogDemo.unity");
        SessionState.SetBool(Key, true);
        SessionState.SetBool(Key + ".baseline", baseline);
        SessionState.SetString(Key + ".error", "");
        SessionState.SetInt(Key + ".asserts", 0);
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string stack, LogType type)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (type == LogType.Assert)
        {
            SessionState.SetInt(Key + ".asserts", SessionState.GetInt(Key + ".asserts", 0) + 1);
            SessionState.SetString(Key + ".error", message + "\n" + stack);
        }
        else if (type == LogType.Error || type == LogType.Exception)
            SessionState.SetString(Key + ".error", message + "\n" + stack);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (suite == null) { suite = Verify(); deadline = EditorApplication.timeSinceStartup + 150; }
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("HUD test timeout");
            if (suite.MoveNext()) return;
            SessionState.SetBool(Key, false);
            Debug.Log("DEVLOG_HUD_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            SessionState.SetBool(Key, false);
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
    static void Check(bool condition, string label)
    {
        checks++; if (!condition) throw new Exception(label);
    }
    static T Field<T>(ZeldaHealthHeartsUI hud, string name) =>
        (T)typeof(ZeldaHealthHeartsUI).GetField(name, Flags).GetValue(hud);
    static bool Ready() => ZeldaRuntimeRegistry.GetControlledMover() != null &&
        !RetroSceneLoadReveal.IsBlockingInput && !RetroSceneLoadReveal.IsPreparingGameplay && !GameSaveSystem.IsPreparingSceneSave;
    static void VerifyHud(string sceneName)
    {
        string error = SessionState.GetString(Key + ".error", "");
        Check(string.IsNullOrEmpty(error), "No Awake/scene-load assertions or errors: " + error);
        var hud = ZeldaHealthHeartsUI.Instance;
        Check(hud != null && Object.FindObjectsOfType<ZeldaHealthHeartsUI>().Length == 1, sceneName + ": exactly one HUD");
        Check(hud.transform.parent == null && hud.gameObject.scene.name == "DontDestroyOnLoad", sceneName + ": standalone persistent HUD");
        var map = Field<RuntimeMiniMapGraphic>(hud, "taskMiniMapGraphic");
        Check(map != null && map.gameObject.activeInHierarchy && map.isActiveAndEnabled, sceneName + ": minimap enabled");
        Check(Field<RectTransform>(hud, "taskPointerLabelRect").gameObject.activeInHierarchy, sceneName + ": map caption enabled");
        var canvas = hud.GetComponent<Canvas>();
        Canvas.ForceUpdateCanvases();
        Check(canvas.enabled && canvas.worldCamera != null && canvas.worldCamera.isActiveAndEnabled, sceneName + ": HUD camera bound");
        Check(map.rectTransform.rect.width > 0 && map.rectTransform.lossyScale.x > 0, sceneName + ": map has visible geometry");
        Check(Field<Scene>(hud, "taskMiniMapScene") == SceneManager.GetActiveScene(), sceneName + ": map rebuilt for current scene");
        var markers = (RuntimeMiniMapLayerGraphic)typeof(RuntimeMiniMapGraphic).GetField("markerLayer", Flags).GetValue(map);
        Check(markers != null && markers.isActiveAndEnabled, sceneName + ": marker mesh is enabled");
        using (var mesh = new VertexHelper())
        {
            typeof(RuntimeMiniMapGraphic).GetMethod("PopulateMarkers", Flags).Invoke(map, new object[] { mesh });
            Check(mesh.currentVertCount > 0, sceneName + ": camera marker produces visible vertices");
        }
        Debug.Log("DEVLOG_HUD_CHECKED " + sceneName);
    }
    static IEnumerator Verify()
    {
        while (!Ready()) yield return null;
        for (int i = 0; i < 10; i++) yield return null;
        if (SessionState.GetBool(Key + ".baseline", false))
        {
            var hud = ZeldaHealthHeartsUI.Instance;
            var map = Field<RuntimeMiniMapGraphic>(hud, "taskMiniMapGraphic");
            Debug.Log("DEVLOG_HUD_BASELINE asserts=" + SessionState.GetInt(Key + ".asserts", 0) +
                " minimapActive=" + map.gameObject.activeInHierarchy + " error=" + SessionState.GetString(Key + ".error", ""));
            yield break;
        }
        VerifyHud("DevlogDemo direct play");
        var original = ZeldaHealthHeartsUI.Instance;
        Check(Field<Text>(original, "taskPointerLabelText").text == "开发日志演示", "Demo caption");
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        mover.transform.position += new Vector3(1, 0, 0);
        for (int i = 0; i < 4; i++) yield return null;
        VerifyHud("DevlogDemo movement");
        foreach (string name in new[] { "DevlogDemo", "Level0", "Level2-Floor-1", "DevlogDemo" })
        {
            string folder = name.StartsWith("Level2-") ? "Level2/" : "";
            var op = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/" + folder + name + ".unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!op.isDone || !Ready()) yield return null;
            for (int i = 0; i < 10; i++) yield return null;
            Check(ZeldaHealthHeartsUI.Instance == original, name + ": HUD survives scene change");
            VerifyHud(name);
        }
        var pause = Object.FindObjectOfType<PauseMenuController>();
        pause.OpenMenu(); Check(PauseMenuController.IsPaused, "Pause opens");
        pause.CloseMenu(); Check(!PauseMenuController.IsPaused, "Pause closes");
        var tab = Object.FindObjectOfType<TabJournalMenuController>();
        tab.OpenMenu(); Check(TabJournalMenuController.IsOpen, "Map menu opens");
        tab.CloseMenu(); Check(!TabJournalMenuController.IsOpen, "Map menu closes");
        VerifyHud("After menus");
    }
}
