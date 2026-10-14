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

// Actual saved Level1/Level2 scenes and their existing E-transition components.
[InitializeOnLoad]
public static class InventorySceneTravelSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string PrefsKey = "CogitansExtensa.PersistentInventory.v1";
    static readonly string[] L2 = { "Level2-Floor1", "Level2-Floor2", "Level2-Floor1", "Level2-Floor-1", "Level2-Floor1", "Level2-Desert", "Level2-Floor1" };
    static readonly string[] L1 = { "Level1-Garden", "Level1-Floor1", "Level1-Floor2", "Level1-Floor3", "Level1-Floor2", "Level1-Floor1", "Level1-Floor-1", "Level1-Floor1", "Level1-Garden" };
    [Serializable] class PrefsData { public PersistentInventory.RuntimeSlotState[] slots; }
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static bool baseline;
    static double deadline;
    static PersistentInventory.RuntimeState expected;
    static string expectedJson;
    static InventorySceneTravelSmoke() { EditorApplication.update += Tick; }
    static string PathFor(string scene) => "Assets/Scenes/" + (scene.StartsWith("Level1") ? "Level1/" : "Level2/") + scene + ".unity";
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run() => Begin(false);
    public static void RunBaseline() => Begin(true);
    static void Begin(bool old)
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "inventory-scene-travel-smoke";
        EditorBuildSettings.scenes = L2.Concat(L1).Distinct().Select(n => new EditorBuildSettingsScene(PathFor(n), true)).ToArray();
        EditorSceneManager.OpenScene(PathFor(L2[0]));
        SessionState.SetBool("InventoryTravelBaseline", old); SessionState.SetBool(nameof(InventorySceneTravelSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(InventorySceneTravelSmoke), false))
            {
                SessionState.SetBool(nameof(InventorySceneTravelSmoke), false);
                baseline = SessionState.GetBool("InventoryTravelBaseline", false);
                deadline = EditorApplication.timeSinceStartup + 540; steps.Push(Verify());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek(); if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; } return;
            }
            Debug.Log("INVENTORY_TRAVEL_" + (baseline ? "BASELINE" : "PASS") + " checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string scene)
    {
        int stable = 0;
        while (stable < 4)
        {
            bool ready = SceneManager.GetActiveScene().name == scene && !DocumentReader.IsInputBlocked &&
                !GameSaveSystem.IsLoading && !GameSaveSystem.IsPreparingSceneSave &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress &&
                !SceneTravelStateManager.GetOrCreate().IsQuickRestartPending && PersistentInventory.Instance != null;
            stable = ready ? stable + 1 : 0; yield return null;
        }
    }
    static void Fill()
    {
        expected = new PersistentInventory.RuntimeState { selectedSlotIndex = 3, slots = new[] {
            new PersistentInventory.RuntimeSlotState { itemId = "gold_coin", quantity = 20 },
            new PersistentInventory.RuntimeSlotState { itemId = "battery", quantity = 1, itemInstanceId = "travel-test-battery", hasStoredCharge = true, storedCharge = 2,
                itemState = "{\"remaining\":2,\"capacity\":5}", hasVisualColor = true, visualColor = new Color(.8f,.15f,.1f,1) },
            new PersistentInventory.RuntimeSlotState { itemId = "glass_bottle", quantity = 3 },
            new PersistentInventory.RuntimeSlotState { itemId = "bomb", quantity = 2 },
            new PersistentInventory.RuntimeSlotState { itemId = "mortar_control_device", quantity = 1, itemInstanceId = "travel-test-controller", itemName = "远程控制装置", itemDescription = "跨场景测试" }
        } };
        PersistentInventory.Instance.ApplyRuntimeState(expected);
        expected = PersistentInventory.Instance.CaptureRuntimeState(); expectedJson = JsonUtility.ToJson(expected);
    }
    static bool CachedCorrect()
    {
        var saved = JsonUtility.FromJson<PrefsData>(PlayerPrefs.GetString(PrefsKey, "{}"));
        return saved != null && JsonUtility.ToJson(saved) == JsonUtility.ToJson(new PrefsData { slots = expected.slots });
    }
    static void AssertInventory(PersistentInventory original, string label)
    {
        var inventory = PersistentInventory.Instance;
        bool same = original != null && inventory == original;
        bool contents = inventory != null && JsonUtility.ToJson(inventory.CaptureRuntimeState()) == expectedJson;
        Debug.Log("INVENTORY_TRACE " + label + " sameInstance=" + same + " contents=" + contents + " prefs=" + CachedCorrect());
        if (baseline) return;
        Check(same, label + ": original inventory survives");
        Check(contents, label + ": full slots, quantities, IDs, charge, color, metadata and selection retained");
        Check(CachedCorrect(), label + ": scene duplicate did not overwrite saved cache");
        Check(inventory.transform.parent == null && inventory.gameObject.scene.name == "DontDestroyOnLoad", label + ": root persistent inventory");
        Check(Object.FindObjectsOfType<PersistentInventory>(true).Length == 1, label + ": only one inventory");
        var hud = ZeldaHealthHeartsUI.Instance;
        if (hud != null)
        {
            Check((PersistentInventory)typeof(ZeldaHealthHeartsUI).GetField("inventory", F).GetValue(hud) == inventory, label + ": HUD bound to live inventory");
            var icons = (Image[])typeof(ZeldaHealthHeartsUI).GetField("inventoryItemIcons", F).GetValue(hud);
            Check(icons.All(i => i != null && i.gameObject.activeSelf && i.sprite != null), label + ": five item icons visible");
        }
    }
    static IEnumerator Route(string[] route)
    {
        Fill(); var original = PersistentInventory.Instance;
        for (int i = 1; i < route.Length; i++)
        {
            var point = Object.FindObjectsOfType<PersistentSceneTransitionPoint>(true).First(p => p.TargetSceneName == route[i]);
            typeof(PersistentSceneTransitionPoint).GetMethod("BeginTransition", F).Invoke(point, null);
            yield return Ready(route[i]); AssertInventory(original, route[i-1] + " -> " + route[i]);
            if (baseline) { Fill(); original = PersistentInventory.Instance; }
        }
        if (!baseline)
        {
            Check(GameSaveSystem.Instance.Save(3, false), "save inventory after actual scene trips");
            Check(JsonUtility.ToJson(GameSaveSystem.Instance.Read(3).player.inventory) == expectedJson, "disk inventory snapshot complete");
            PersistentInventory.Instance.ClearAll();
            Check(GameSaveSystem.Instance.Load(3), "load inventory after actual scene trips"); yield return Ready(route[route.Length-1]);
            AssertInventory(original, route[0] + " disk restore");
        }
    }
    static IEnumerator Verify()
    {
        yield return Ready(L2[0]);
        if (!baseline)
        {
            Check(PersistentInventory.Instance.Slots.All(s => s.IsEmpty), "fresh session ignores old automatic inventory cache");
            Check(PersistentInventory.Instance.SelectedSlotIndex == 0, "fresh session starts at first slot");
        }
        yield return Route(baseline ? L2.Take(3).ToArray() : L2);
        RetroSceneLoadReveal.BeginTransition(L1[0], () => {
            SceneTravelStateManager.GetOrCreate().ResetAllSceneStates(); RetroSceneLoadReveal.LoadSceneAsync(L1[0]); });
        yield return Ready(L1[0]);
        if (!baseline) Check(PersistentInventory.Instance.Slots.All(s => s.IsEmpty), "fresh/nonpersistent level start still clears items intentionally");
        yield return Route(baseline ? new[] { "Level1-Garden", "Level1-Floor1", "Level1-Garden" } : L1);
        if (!baseline)
        {
            var bag = PersistentInventory.Instance; bag.PreserveForNextSceneLoad();
            // A non-gameplay additive notification must not consume the next single-load preservation flag.
            typeof(PersistentInventory).GetMethod("OnSceneLoaded", F).Invoke(bag, new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Additive });
            Check((bool)typeof(PersistentInventory).GetField("preserveOnNextSceneLoad", F).GetValue(bag), "additive load cannot consume pending inventory preservation");
            AssertInventory(bag, "additive notification");
        }
    }
}
