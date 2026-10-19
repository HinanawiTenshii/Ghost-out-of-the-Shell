using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Run in the disposable Unity project only. Never writes the player's saves.
[InitializeOnLoad]
public static class LastSceneUiSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Stack<IEnumerator> routines;
    static int frame = -1, checks;
    static double deadline;
    static LastSceneUiSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "last-scene-ui-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/LastScene.unity");
        SessionState.SetBool(nameof(LastSceneUiSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(LastSceneUiSmoke), false))
            {
                SessionState.SetBool(nameof(LastSceneUiSmoke), false);
                routines = new Stack<IEnumerator>(); routines.Push(Verify());
                deadline = EditorApplication.timeSinceStartup + 360;
            }
            if (routines == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("LastScene UI timeout");
            while (routines.Count > 0)
            {
                var step = routines.Peek();
                if (!step.MoveNext()) { routines.Pop(); continue; }
                if (step.Current is IEnumerator child) { routines.Push(child); continue; }
                return;
            }
            routines = null; Debug.Log("LAST_SCENE_UI_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { routines = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, F).SetValue(target, value);
    static IEnumerator Ready(string scene)
    {
        yield return null; yield return null;
        while (SceneManager.GetActiveScene().name != scene || RetroSceneLoadReveal.IsPreparingGameplay ||
            RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsLoading || GameSaveSystem.IsPreparingSceneSave)
            yield return null;
        yield return null;
    }
    static bool Hidden(Canvas canvas)
    {
        var group = canvas.GetComponent<CanvasGroup>();
        return group != null && group.alpha == 0 && !group.interactable && !group.blocksRaycasts;
    }
    static Canvas NewCanvas(string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Object.DontDestroyOnLoad(go);
        return go.GetComponent<Canvas>();
    }
    static IEnumerator CheckPause(string label)
    {
        var pause = Object.FindObjectOfType<PauseMenuController>();
        var tab = pause.GetComponent<TabJournalMenuController>();
        Check(pause != null && tab != null, label + " shared pause/tab prefab exists");
        tab.OpenMenu();
        Check(!TabJournalMenuController.IsOpen && Time.timeScale > 0, label + " Tab cannot invisibly pause");
        pause.OpenMenu(); yield return null;
        Check(PauseMenuController.IsPaused && Time.timeScale == 0 && Cursor.visible, label + " pause and pointer work");
        Check(!Hidden(pause.GetComponent<Canvas>()) && pause.transform.Find("Pause Menu Root").gameObject.activeInHierarchy,
            label + " pause UI visible");
        foreach (bool saving in new[] { true, false })
        {
            Call(pause, "OpenSaveSlots", saving); yield return null;
            var panel = Object.FindObjectOfType<SaveSlotPanel>();
            Check(SaveSlotPanel.IsOpen && panel != null && !Hidden(panel.GetComponentInParent<Canvas>()), label + " save/load UI accessible " + saving);
            Call(panel, "Close"); yield return null;
            Check(pause.transform.Find("Pause Menu Root").gameObject.activeInHierarchy, label + " slots return to pause");
        }
        pause.CloseMenu(); yield return null;
        Check(!PauseMenuController.IsPaused && Time.timeScale > 0 && !Cursor.visible, label + " resume works");
    }
    static IEnumerator Verify()
    {
        yield return Ready("LastScene");
        Check(RuntimeUiVisibility.IsPauseOnlyScene, "direct editor entry applies policy without save flag");
        yield return CheckPause("direct LastScene");

        // A late-created HUD must not require a scene reload to become hidden.
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/ZeldaHealthHeartsUI.prefab"));
        yield return null;
        var hud = ZeldaHealthHeartsUI.Instance;
        Check(hud != null && Hidden(hud.GetComponent<Canvas>()), "late-created persistent HUD hidden");
        var dynamic = NewCanvas("Late UI"); CRTScreenEffect.RegisterCanvas(dynamic);
        Check(Hidden(dynamic), "new registered windows are hidden and cannot intercept clicks");
        Object.Destroy(dynamic.gameObject);

        RetroSceneLoadReveal.LoadSceneAsync("Level2-Floor1");
        yield return Ready("Level2-Floor1");
        Check(!RuntimeUiVisibility.IsPauseOnlyScene && !Hidden(hud.GetComponent<Canvas>()), "normal gameplay restores same persistent HUD");
        var tab = Object.FindObjectOfType<TabJournalMenuController>();
        tab.OpenMenu(); Check(TabJournalMenuController.IsOpen, "normal Tab still opens"); tab.CloseMenu(); yield return null;
        var saves = GameSaveSystem.Instance;
        Check(saves.Save(1, false), "old-format progress fixture saved");
        var baseline = NewCanvas("Persistent State Baseline");
        var baselineGroup = baseline.gameObject.AddComponent<CanvasGroup>();
        baselineGroup.alpha = .65f; baselineGroup.interactable = false; baselineGroup.blocksRaycasts = true;
        CRTScreenEffect.RegisterCanvas(baseline);
        var visibility = Object.FindObjectOfType<RuntimeUiVisibility>();
        Set(visibility, "hidden", true); Call(visibility, "LateUpdate");
        var exit = Object.FindObjectsOfType<SceneTransitionPoint>(true).First(p => p.TargetSceneName == "LastScene");
        Call(exit, "LoadTargetScene");
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        Check(LevelSettlementScreen.IsOpen, "authored transition still opens settlement first");
        Object.FindObjectOfType<LevelSettlementScreen>().NextLevel();
        yield return Ready("LastScene");
        Check(Hidden(hud.GetComponent<Canvas>()) && Hidden(baseline), "transition hides persistent HUD and extra canvases");
        var curtain = Object.FindObjectOfType<RetroSceneLoadReveal>().GetComponentInChildren<Canvas>(true);
        Check(!Hidden(curtain), "transition curtain is exempt");
        yield return CheckPause("transition with prior F12 hide");
        // Use the actual disk loader: no new UI flag exists in this save.
        Set(visibility, "hidden", false);
        Check(saves.Load(1), "load existing-format gameplay progress from LastScene");
        yield return Ready("Level2-Floor1");
        Check(!Hidden(hud.GetComponent<Canvas>()), "disk load restores HUD");
        Check(Mathf.Approximately(baselineGroup.alpha, .65f) && !baselineGroup.interactable && baselineGroup.blocksRaycasts,
            "exact original group state restored");

        // Also cover retained characters and old disk progress already in LastScene.
        SceneTravelStateManager.GetOrCreate().TravelToScene("LastScene", Vector2.zero);
        yield return Ready("LastScene");
        Check(Hidden(hud.GetComponent<Canvas>()), "persistent scene travel also hides HUD");
        Check(saves.Save(2, false), "LastScene legacy-shaped save");
        Check(saves.Load(2), "reload LastScene save without any new UI fields");
        yield return Ready("LastScene");
        Check(Hidden(hud.GetComponent<Canvas>()), "old LastScene disk progress remains hidden");
        yield return CheckPause("LastScene disk reload");
        Object.Destroy(baseline.gameObject);
        var finalPause = Object.FindObjectOfType<PauseMenuController>();
        finalPause.OpenMenu(); finalPause.ReturnToTitleScreen();
        yield return Ready("TitleScreen");
        Check(!RuntimeUiVisibility.IsPauseOnlyScene && Object.FindObjectOfType<TitleScreenController>() != null,
            "pause return to title still works");
        Debug.Log("LAST_SCENE_UI_CASES direct/transition/settlement/persistent-travel/legacy-save/pause/slots/title");
    }
}
