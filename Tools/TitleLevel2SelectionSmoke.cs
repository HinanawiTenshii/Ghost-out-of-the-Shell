using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Execute only in the isolated validation project; never saves user scenes.
[InitializeOnLoad]
public static class TitleLevel2SelectionSmoke
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static TitleLevel2SelectionSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Flags).GetValue(target);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "title-level2-selection-smoke";
        Check(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/Level2/Level2-Desert.unity"), "desert in enabled build scenes");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/TitleScreenUI.prefab");
        Check(Get<string>(prefab.GetComponent<TitleScreenController>(), "level2SceneName") == "Level2-Desert", "prefab target serialized explicitly");
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScreen.unity");
        SessionState.SetBool(nameof(TitleLevel2SelectionSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.frameCount == frame) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(TitleLevel2SelectionSmoke), false))
            {
                SessionState.SetBool(nameof(TitleLevel2SelectionSmoke), false);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("menu transition timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("TITLE_LEVEL2_SELECTION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        var title = Object.FindObjectOfType<TitleScreenController>();
        Check(title != null, "authored title scene creates menu");
        var main = Get<RectTransform>(title, "mainOptionsRoot");
        var levels = Get<RectTransform>(title, "levelSelectOptionsRoot");
        Check(main.gameObject.activeSelf && !levels.gameObject.activeSelf, "main menu initially shown");
        main.Find("开始游戏").GetComponent<Button>().onClick.Invoke();
        Check(!main.gameObject.activeSelf && levels.gameObject.activeSelf, "start opens level selection");
        var buttons = levels.GetComponentsInChildren<Button>();
        Check(buttons.Length == 6 && buttons.Count(b => b.interactable) == 5 && !buttons[4].interactable, "four levels plus back enabled; coming-soon disabled");
        string[] labels = { "教学关卡", "Level 0-监牢", "Level 1-城堡", "Level 2-边境城市", "未完待续", "返回" };
        for (int i = 0; i < labels.Length; i++)
        {
            var b = buttons[i];
            Check(b.name == labels[i] && b.GetComponentInChildren<Text>().text == labels[i], "correct label and order " + i);
            Check((b.GetComponent<PauseMenuButtonHover>() != null) == b.interactable && b.GetComponentInChildren<TitleScreenGlow>() != null, "existing feedback and glow " + i);
            var rect = (RectTransform)b.transform;
            Check(Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y * .5f <= levels.sizeDelta.y * .5f, "button fits options panel " + i);
            if (i > 0)
            {
                var previous = (RectTransform)buttons[i-1].transform; var current = (RectTransform)b.transform;
                Check(previous.anchoredPosition.y - current.anchoredPosition.y >= current.sizeDelta.y, "buttons do not overlap " + i);
            }
        }
        var level2 = buttons[3]; var graphic = level2.GetComponent<Image>(); var color = graphic.color;
        ExecuteEvents.Execute(level2.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
        yield return null; yield return null;
        Check(graphic.color != color, "level2 hover changes background");
        buttons[4].onClick.Invoke();
        Check(levels.gameObject.activeSelf && SceneManager.GetActiveScene().name == "TitleScreen", "coming-soon has no action");
        buttons[5].onClick.Invoke();
        Check(main.gameObject.activeSelf && !levels.gameObject.activeSelf, "back returns to main menu");
        title.StartGame();
        // Simulate returning from another play session before selecting a fresh level.
        var inv = PersistentInventory.Instance;
        if (inv == null) inv = new GameObject("Old Inventory Fixture").AddComponent<PersistentInventory>();
        inv.ApplyRuntimeState(new PersistentInventory.RuntimeState {
            selectedSlotIndex = 6, slots = new PersistentInventory.RuntimeSlotState[7]
        });
        inv.SetSlot(6, GoldCoinPickupItem.DefaultItemId, 20);
        QuestJournalManager.GetOrCreate().AddOrUpdateEntry("menu.old-side-quest", "Old Quest", "Fixture");
        level2.onClick.Invoke();
        int stable = 0;
        while (stable < 4)
        {
            bool ready = SceneManager.GetActiveScene().name == "Level2-Desert" &&
                !RetroSceneLoadReveal.IsBlockingInput && !DocumentReader.IsInputBlocked && !GameSaveSystem.IsLoading &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress;
            stable = ready ? stable + 1 : 0; yield return null;
        }
        Check(Object.FindObjectOfType<TitleScreenController>() == null, "title canvas unloaded after transition");
        Check(ZeldaRuntimeRegistry.GetControlledMover() != null, "desert starts with controlled character");
        Check(PersistentInventory.Instance != null && PersistentInventory.Instance.SlotCount == 5 &&
            PersistentInventory.Instance.Slots.All(s => s.IsEmpty), "fresh level does not inherit old items or expanded capacity");
        Check(GameSaveSystem.Instance != null && ZeldaHealthHeartsUI.Instance != null, "save and gameplay HUD initialized");
        var journal = QuestJournalManager.GetOrCreate();
        Check(!journal.ContainsEntry("menu.old-side-quest"), "old journal state cleared");
        Check(journal.Level2HeroProofStage == 1 && journal.TrackedEntryId == QuestJournalManager.Level2HeroProofId, "hero proof starts at reach-city stage and is tracked");
        Check(LevelSceneGroup.TryGetLevelForScene("Level2-Desert", out var group) && group.LevelId == "level2", "desert retains level2 group membership");
    }
}
