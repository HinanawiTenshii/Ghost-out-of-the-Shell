using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Run in an isolated project only. Unlike the travel regression, this actually
// stops Play Mode between scenes, exercising Awake and the initial scene event.
[InitializeOnLoad]
public static class InventorySessionRestartSmoke
{
    const string Prefix = "InventorySessionRestartSmoke.";
    const string CacheKey = "CogitansExtensa.PersistentInventory.v1";
    static readonly string[] Scenes = {
        "Level2-Floor1", "Level2-Floor1", "Level2-Floor2", "Level2-Floor-1", "Level2-Desert",
        "Level1-Garden", "Level1-Floor1", "Level1-Floor2", "Level1-Floor3", "Level1-Floor-1"
    };
    static readonly Stack<IEnumerator> Steps = new Stack<IEnumerator>();
    static int frame = -1;
    static double deadline;

    static InventorySessionRestartSmoke()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredEditMode)
                SessionState.SetBool(Prefix + "editReady", true);
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void TraceInitialLoad()
    {
        if (!SessionState.GetBool(Prefix + "running", false)) return;
        SceneManager.sceneLoaded += (scene, mode) => Debug.Log("INVENTORY_SESSION_EVENT " + scene.name + " mode=" + mode);
    }

    public static void Run() => Begin(false);
    public static void RunBaseline() => Begin(true);
    static void Begin(bool baseline)
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("isolated project required");
        PlayerSettings.productName = "inventory-session-restart-smoke";
        EditorBuildSettings.scenes = Scenes.Distinct().Select(n => new EditorBuildSettingsScene(PathFor(n), true)).ToArray();
        SessionState.SetBool(Prefix + "running", true);
        SessionState.SetBool(Prefix + "baseline", baseline);
        SessionState.SetBool(Prefix + "editReady", true);
        SessionState.SetInt(Prefix + "stage", 0);
        SessionState.SetInt(Prefix + "checks", 0);
    }

    static string PathFor(string scene) => "Assets/Scenes/" + (scene.StartsWith("Level1") ? "Level1/" : "Level2/") + scene + ".unity";
    static void Check(bool ok, string label)
    {
        SessionState.SetInt(Prefix + "checks", SessionState.GetInt(Prefix + "checks", 0) + 1);
        if (!ok) throw new Exception(label);
    }

    static void Tick()
    {
        if (!SessionState.GetBool(Prefix + "running", false) || EditorApplication.isCompiling) return;
        try
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (!SessionState.GetBool(Prefix + "editReady", false)) return;
                SessionState.SetBool(Prefix + "editReady", false);
                int stage = SessionState.GetInt(Prefix + "stage", 0);
                if (stage >= Scenes.Length)
                {
                    Debug.Log("INVENTORY_SESSION_" + (SessionState.GetBool(Prefix + "baseline", false) ? "BASELINE" : "PASS") +
                        " checks=" + SessionState.GetInt(Prefix + "checks", 0));
                    SessionState.SetBool(Prefix + "running", false);
                    EditorApplication.Exit(0);
                    return;
                }
                EditorSceneManager.OpenScene(PathFor(Scenes[stage]));
                SessionState.SetBool(Prefix + "verify", true);
                EditorApplication.EnterPlaymode();
                return;
            }
            if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (SessionState.GetBool(Prefix + "verify", false))
            {
                SessionState.SetBool(Prefix + "verify", false);
                deadline = EditorApplication.timeSinceStartup + 120;
                Steps.Push(Verify(SessionState.GetInt(Prefix + "stage", 0)));
            }
            if (Steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("session timeout");
            while (Steps.Count > 0)
            {
                var step = Steps.Peek();
                if (!step.MoveNext()) { Steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { Steps.Push(nested); continue; }
                return;
            }
            SessionState.SetInt(Prefix + "stage", SessionState.GetInt(Prefix + "stage", 0) + 1);
            EditorApplication.ExitPlaymode();
        }
        catch (Exception e)
        {
            SessionState.SetBool(Prefix + "running", false);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static IEnumerator Ready(string scene)
    {
        int stable = 0;
        while (stable < 4)
        {
            bool ready = SceneManager.GetActiveScene().name == scene && PersistentInventory.Instance != null &&
                !DocumentReader.IsInputBlocked && !GameSaveSystem.IsLoading && !GameSaveSystem.IsPreparingSceneSave &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress &&
                !SceneTravelStateManager.GetOrCreate().IsQuickRestartPending;
            stable = ready ? stable + 1 : 0;
            yield return null;
        }
    }

    static void Fill()
    {
        PersistentInventory.Instance.ApplyRuntimeState(new PersistentInventory.RuntimeState {
            selectedSlotIndex = 1,
            slots = new[] {
                new PersistentInventory.RuntimeSlotState { itemId = "gold_coin", quantity = 17 },
                new PersistentInventory.RuntimeSlotState { itemId = "battery", quantity = 1,
                    itemInstanceId = "restart-battery", hasStoredCharge = true, storedCharge = 2 },
                new PersistentInventory.RuntimeSlotState { itemId = "glass_bottle", quantity = 3 },
                new PersistentInventory.RuntimeSlotState(), new PersistentInventory.RuntimeSlotState(),
                new PersistentInventory.RuntimeSlotState { itemId = "memo", quantity = 1 },
                new PersistentInventory.RuntimeSlotState { itemId = "gold_coin", quantity = 4 }
            }
        });
    }

    static IEnumerator Verify(int stage)
    {
        bool baseline = SessionState.GetBool(Prefix + "baseline", false);
        yield return Ready(Scenes[stage]);
        var bag = PersistentInventory.Instance;
        bool empty = bag.Slots.All(s => s.IsEmpty);
        Debug.Log("INVENTORY_SESSION_TRACE stage=" + stage + " scene=" + Scenes[stage] + " empty=" + empty);
        if (stage > 0 && !baseline)
        {
            Check(empty, "fresh Play session must not inherit inventory: " + Scenes[stage]);
            Check(bag.SelectedSlotIndex == 0, "fresh selection reset");
            Check(bag.SlotCount == 5, "fresh session must not inherit expanded capacity");
            var cached = JsonUtility.FromJson<PersistentInventory.RuntimeState>(PlayerPrefs.GetString(CacheKey, "{}"));
            Check(cached.slots != null && cached.slots.All(s => s.quantity == 0), "old automatic cache cleared");
            // Some secondary scenes deliberately have no authored player and
            // only receive one through travel; inventory still starts empty.
            if (ZeldaRuntimeRegistry.GetControlledMover() != null)
            {
                Check(GameSaveSystem.Instance.Save(0, false), "first save of fresh session");
                Check(GameSaveSystem.Instance.Read(0).player.inventory.slots.All(s => s.quantity == 0), "first save must not capture stale items");
            }
        }
        if (stage == 1 && !baseline)
        {
            Check(GameSaveSystem.Instance.Load(3), "manual save load after Play Mode restart");
            yield return Ready(Scenes[stage]);
            Check(JsonUtility.ToJson(PersistentInventory.Instance.CaptureRuntimeState()) ==
                SessionState.GetString(Prefix + "saved", ""), "manual save restores exact items, charges and selection");
        }
        if (stage == 2 && !baseline)
        {
            Check(GameSaveSystem.Instance.Load(3), "load another scene's manual save after fresh start");
            yield return Ready(Scenes[0]);
            Check(JsonUtility.ToJson(PersistentInventory.Instance.CaptureRuntimeState()) ==
                SessionState.GetString(Prefix + "saved", ""), "cross-scene manual save inventory restored");
        }
        Fill();
        if (stage == 0)
        {
            Check(GameSaveSystem.Instance.Save(3, false), "save nonempty inventory for explicit load regression");
            SessionState.SetString(Prefix + "saved", JsonUtility.ToJson(PersistentInventory.Instance.CaptureRuntimeState()));
        }
        Check(PlayerPrefs.GetString(CacheKey, "").Contains("restart-battery"), "seed old session cache before stopping Play Mode");
    }
}
