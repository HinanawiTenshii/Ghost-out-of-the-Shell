using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Copy to Assets/Editor in the disposable .codex-temp project and execute Run.
// Uses an empty scene and reflection, not a captured gameplay world. The service
// Update is disabled; nextAutoRetry is held at infinity whenever Tick is invoked.
// Never calls SaveCore, LoadCovered, or CaptureDiskWorld. Only the explicit fresh
// history fixture directory below receives writes from the production writer.
[InitializeOnLoad]
public static class AutoSaveSchedulerSmoke
{
    private const string ProductPrefix = "autosave-scheduler-smoke-";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static int checks;
    private static double deadline;
    static AutoSaveSchedulerSmoke() { EditorApplication.update += Tick; }

    public static void Run()
    {
        checks = 0; deadline = 0;
        RequireIsolation();
        PlayerSettings.productName = ProductPrefix + Guid.NewGuid().ToString("N");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(AutoSaveSchedulerSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(AutoSaveSchedulerSmoke), false)) return;
        if (deadline == 0) deadline = EditorApplication.timeSinceStartup + 60;
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Scheduler fixture bootstrap timed out");
            if (GameSaveSystem.Instance == null || RetroSceneLoadReveal.IsPreparingGameplay ||
                RetroSceneLoadReveal.IsBlockingInput || DocumentReader.IsInputBlocked) return;
            SessionState.SetBool(nameof(AutoSaveSchedulerSmoke), false);
            RunChecks();
            Debug.Log("AUTO_SAVE_SCHEDULER_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            SessionState.SetBool(nameof(AutoSaveSchedulerSmoke), false);
            Debug.LogException(ex);
            EditorApplication.Exit(1);
        }
    }

    public static void RunChecks()
    {
        RequireIsolation();
        Check(Application.isPlaying, "scheduler checks run in play mode");
        Check(Application.productName.StartsWith(ProductPrefix, StringComparison.Ordinal) &&
            Application.persistentDataPath.Replace('\\', '/').Contains(Application.productName),
            "only a fresh disposable product may bootstrap the save service");
        var saves = GameSaveSystem.Instance;
        Check(saves != null, "production save service exists");
        bool wasEnabled = saves.enabled;
        float timeScale = Time.timeScale;
        Scene original = SceneManager.GetActiveScene();
        object oldProfile = Get(saves, "activeProfile"), oldVisits = Get(saves, "visits"), oldLevel = Get(saves, "sessionLevelId");
        var scene = SceneManager.CreateScene("AutoSaveSchedulerFixture-" + Guid.NewGuid().ToString("N"));
        var otherScene = SceneManager.CreateScene("AutoSaveSchedulerOther-" + Guid.NewGuid().ToString("N"));
        var travel = SceneTravelStateManager.GetOrCreate();
        bool oldRestore = travel.IsSceneTravelRestoreInProgress, oldQuick = travel.IsQuickRestartPending;
        try
        {
            saves.enabled = false;
            saves.StopAllCoroutines();
            Set(saves, "preparingSceneSave", false);
            Set(saves, "loading", false); Set(saves, "writing", false); Set(saves, "restoreFailed", false);
            Set(travel, "sceneTravelRestoreInProgress", false); Set(travel, "quickRestartPending", false);
            SceneManager.SetActiveScene(scene);
            Time.timeScale = 1f;
            var player = Actor("Scheduler Player", scene, false);
            ZeldaRuntimeRegistry.ClaimControlledMover(player);
            Call(saves, "ResetAutoSaveSession");
            Set(saves, "nextAutoRetry", float.PositiveInfinity);
            Check((bool)Call(saves, "IsAutoSaveGameplayReady"), "empty isolated gameplay fixture is ready");
            Check(Safe(saves, player), "healthy idle player is safe");

            var ghost = Actor("Scheduler Ghost", scene, true);
            Check(!Safe(saves, ghost), "actual ghost body is never a safe automatic checkpoint");
            ghost.enabled = false;
            ZeldaRuntimeRegistry.ClaimControlledMover(player);
            Set(player, "attackTimer", .5f);
            Check(!Safe(saves, player), "attack in progress blocks automatic checkpoint");
            Set(player, "attackTimer", 0f);
            Set(player.CharacterData, "currentHealth", 1);
            Check(!Safe(saves, player), "last health point is unsafe for a multi-health body");
            Set(player.CharacterData, "currentHealth", 8);

            // No BeginTemporaryHostility call: this fixture deliberately has no
            // shared warning/incident. Only the independent AI state targets us.
            var npc = Actor("Independent Hostile NPC", scene, false);
            npc.enabled = false;
            var ai = npc.gameObject.AddComponent<ZeldaCharacterAiBase>();
            Set(ai, "enableAwarenessSound", false);
            Set(ai, "currentState", ZeldaAiState.Hostile);
            Set(ai, "targetMover", player);
            ZeldaRuntimeRegistry.ClaimControlledMover(player);
            Check(!SharedPlayerHostilityTimer.TryGetForScene(scene, out var timer) ||
                timer.Phase == SharedPlayerHostilityTimer.IncidentPhase.Idle, "independent AI fixture has no shared incident");
            foreach (var state in new[] { ZeldaAiState.Hostile, ZeldaAiState.Alert, ZeldaAiState.Suspicious, ZeldaAiState.Search })
            {
                Set(ai, "currentState", state);
                Check(!Safe(saves, player), "independent AI " + state + " targeting player blocks");
            }
            SceneManager.MoveGameObjectToScene(npc.gameObject, otherScene);
            Check(Safe(saves, player), "hostile AI in another scene cannot block this scene");
            SceneManager.MoveGameObjectToScene(npc.gameObject, scene);
            ai.enabled = false;
            Check(Safe(saves, player), "disabled AI cannot block checkpoint");
            AutoSaveTransientActivity.Register(player);
            try { Check(!Safe(saves, player), "registered transient activity blocks otherwise-safe checkpoint"); }
            finally { AutoSaveTransientActivity.Unregister(player); }

            Call(saves, "ResetAutoSaveSession");
            Set(saves, "autoPlayClock", 20f);
            for (int i = 0; i < 100; i++) GameSaveSystem.RequestAutoSave("fixture transaction " + i);
            Check(Get<bool>(saves, "autoProgressDirty") && Get<bool>(saves, "autoSaveRequested"), "many transaction requests become one pending checkpoint");
            Check(!Get<bool>(saves, "importantAutoSave"), "ordinary requests remain ordinary");
            Set(saves, "autoPlayClock", 21f);
            GameSaveSystem.RequestAutoSave("important quest fixture", true);
            Set(saves, "autoPlayClock", 22f);
            GameSaveSystem.RequestAutoSave("later inventory fixture");
            Check(Get<bool>(saves, "importantAutoSave"), "important priority survives later ordinary coalescing");
            Check(Get<float>(saves, "lastAutoRequest") == 22f, "coalescing extends settling to final transaction");
            Call(saves, "AutoSaveCommitted");
            Check(!Get<bool>(saves, "autoProgressDirty") && !Get<bool>(saves, "autoSaveRequested") &&
                !Get<bool>(saves, "importantAutoSave"), "one successful commit clears the coalesced request");
            Check(Get<float>(saves, "lastAutoSaveClock") == 22f, "commit starts a new cooldown interval");

            foreach (string flag in new[] { "loading", "writing", "restoreFailed" })
            {
                Call(saves, "ResetAutoSaveSession");
                Set(saves, flag, true);
                GameSaveSystem.RequestAutoSave("restore replay fixture", true);
                GameSaveSystem.MarkAutoSaveProgress();
                Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "autoProgressDirty"), flag + " ignores restored progress notifications");
                Set(saves, flag, false);
            }
            foreach (string flag in new[] { "quickRestartPending", "sceneTravelRestoreInProgress" })
            {
                Call(saves, "ResetAutoSaveSession"); Set(travel, flag, true);
                GameSaveSystem.RequestAutoSave("travel restore fixture", true);
                GameSaveSystem.MarkAutoSaveProgress();
                Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "autoProgressDirty"), flag + " cannot enqueue snapshot");
                Set(travel, flag, false);
            }
            GameSaveSystem.RequestAutoSave("pending before restore", true);
            Set(saves, "autoPlayClock", 200f); Set(saves, "safeSince", 0f);
            Call(saves, "ResetAutoSaveSession");
            Check(!Get<bool>(saves, "autoSaveRequested") && !Get<bool>(saves, "importantAutoSave") &&
                !Get<bool>(saves, "autoProgressDirty") && Get<float>(saves, "safeSince") < 0f &&
                Get<float>(saves, "autoPlayClock") == 0f, "restore reset discards pending work and requires a fresh safe period");

            // Only predicate evaluation can pass the safety gate here; Tick is
            // always held behind retry=infinity, so no world/disk capture occurs.
            Set(saves, "autoSaveSafeSeconds", 5f);
            Set(saves, "autoPlayClock", 40f); Set(saves, "safeSince", 36f);
            Check(!(bool)Call(saves, "CanCommitAutomaticSave"), "four safe seconds are insufficient");
            Set(saves, "safeSince", 35f);
            Check((bool)Call(saves, "CanCommitAutomaticSave"), "full five safe seconds permit a checkpoint");
            PrepareTick(saves, player);
            Set(saves, "autoPlayClock", 179f); Set(saves, "autoProgressDirty", true);
            Call(saves, "TickAutoSave");
            Check(!Get<bool>(saves, "autoSaveRequested"), "dirty progress alone never requests a checkpoint");
            Set(saves, "autoPlayClock", 180f);
            Call(saves, "TickAutoSave");
            Check(!Get<bool>(saves, "autoSaveRequested"), "former periodic interval no longer queues a checkpoint");
            foreach (float elapsed in new[] { 360f, 3600f, 86400f })
            {
                Set(saves, "autoPlayClock", elapsed);
                player.transform.position += Vector3.right * 4;
                GameSaveSystem.MarkAutoSaveProgress();
                Call(saves, "TickAutoSave");
                Check(!Get<bool>(saves, "autoSaveRequested"), "movement/progress after " + elapsed + " seconds does not auto-save");
                Check(Get<float>(saves, "nextSafetyPoll") == 0, "no event means no recurring safety registry queries");
            }
            Call(saves, "ResetAutoSaveSession"); PrepareTick(saves, player);
            Set(saves, "autoPlayClock", 1000f);
            Call(saves, "TickAutoSave");
            Check(!Get<bool>(saves, "autoSaveRequested"), "standing idle does not rotate history periodically");
            GameSaveSystem.RequestAutoSave("unsafe queue fixture", true);
            Set(saves, "safeSince", 0f); Set(player, "attackTimer", .5f);
            Call(saves, "TickAutoSave");
            Check(Get<bool>(saves, "autoSaveRequested") && Get<float>(saves, "safeSince") < 0f, "combat defers pending request and resets safe period without dropping it");
            Set(player, "attackTimer", 0f);
            float clock = Get<float>(saves, "autoPlayClock");
            Time.timeScale = 0f; Call(saves, "TickAutoSave");
            Check(Get<float>(saves, "autoPlayClock") == clock, "pause does not consume event cooldown clock");
            Time.timeScale = 1f;

            VerifyEntryUnchanged(saves, scene.name);
            VerifyHistory();
        }
        finally
        {
            Call(saves, "ResetAutoSaveSession");
            Set(saves, "activeProfile", oldProfile); Set(saves, "visits", oldVisits); Set(saves, "sessionLevelId", oldLevel);
            Set(saves, "loading", false); Set(saves, "writing", false); Set(saves, "restoreFailed", false);
            Set(travel, "sceneTravelRestoreInProgress", oldRestore); Set(travel, "quickRestartPending", oldQuick);
            for (int i = objects.Count - 1; i >= 0; i--) if (objects[i] != null) Object.DestroyImmediate(objects[i]);
            objects.Clear();
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            SceneManager.UnloadSceneAsync(scene); SceneManager.UnloadSceneAsync(otherScene);
            Time.timeScale = timeScale;
            saves.enabled = wasEnabled;
        }
    }

    private static void PrepareTick(GameSaveSystem saves, ZeldaFourWayMover player)
    {
        Set(saves, "nextAutoRetry", float.PositiveInfinity);
        Set(saves, "nextSafetyPoll", 0f);
        Set(saves, "autoObservedMover", player);
        Set(saves, "autoObservedPosition", (Vector2)player.transform.position);
        Set(saves, "autoObservedHealth", player.CharacterData.CurrentHealth);
        Set(saves, "autoObservedShieldVersion", player.CharacterData.HolyShieldBlockVersion);
    }

    private static ZeldaFourWayMover Actor(string name, Scene scene, bool ghost)
    {
        var go = new GameObject(name);
        objects.Add(go);
        SceneManager.MoveGameObjectToScene(go, scene);
        var data = (ZeldaCharacterData)go.AddComponent(ghost ? typeof(GhostZeldaCharacterData) : typeof(CivilianZeldaCharacterData));
        Set(data, "health", 8); Set(data, "currentHealth", 8); Set(data, "isDead", false);
        return go.AddComponent<ZeldaFourWayMover>();
    }

    private static void VerifyEntryUnchanged(GameSaveSystem saves, string scene)
    {
        var entry = new GameSaveSystem.LevelVisit { id = GameSaveSystem.LevelIdForScene(scene), entryScene = scene,
            checkpointId = Guid.NewGuid().ToString("N"), capturedUtcTicks = 123, initialCollectibleCount = 7 };
        var profile = new GameSaveSystem.ProfileData { id = "slot-1", campaignId = Guid.NewGuid().ToString("N"),
            entries = new List<GameSaveSystem.LevelVisit> { entry } };
        var visits = new List<GameSaveSystem.LevelVisit> { entry };
        Set(saves, "activeProfile", profile); Set(saves, "visits", visits);
        // Even with no matching session marker, ordinary auto capture must not
        // reinterpret current progress as a new level-entry checkpoint.
        Set(saves, "sessionLevelId", null);
        string original = JsonUtility.ToJson(entry);
        for (int i = 0; i < 3; i++)
            Call(saves, "EnsureLevelVisit", new GameSaveSystem.SaveData { scene = scene, collectibleCount = 100 + i }, false);
        Check(profile.entries.Count == 1 && visits.Count == 1 && JsonUtility.ToJson(profile.entries[0]) == original &&
            JsonUtility.ToJson(visits[0]) == original, "repeated ordinary autos never replace early-level entry identity or initial collectible state");
    }

    private static void VerifyHistory()
    {
        string root = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), ".codex-temp"));
        string folder = Path.Combine(root, "scheduler-history-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        string campaign = Guid.NewGuid().ToString("N");
        string[] names = { "auto.json", "auto-previous-1.json", "auto-previous-2.json" };
        try
        {
            for (int moment = 0; moment < 4; moment++)
            {
                var data = new GameSaveSystem.SaveData { profileId = "slot-1", campaignId = campaign,
                    savedUtcTicks = 100 + moment, savedAt = "original-time-" + moment, name = "snapshot-" + moment,
                    scene = "SyntheticFixture", levelName = "Fixture", collectibleCount = moment, world = "{}",
                    player = new SceneTravelStateManager.DiskPlayer() };
                StaticCall("CommitAutomaticSaveInDirectory", folder, data);
            }
            Check(Directory.GetFiles(folder, "*.json").Length == 3, "four successful checkpoints retain exactly three automatic files");
            for (int index = 0; index < names.Length; index++)
            {
                var data = (GameSaveSystem.SaveData)StaticCall("ReadData", Path.Combine(folder, names[index]));
                Check(data != null && data.collectibleCount == 3 - index && data.campaignId == campaign &&
                    data.savedAt == "original-time-" + (3 - index), "automatic history " + index + " preserves chronological payload and timestamp");
            }
            Check(!GameSaveSystem.IsManualSlot(7) && !GameSaveSystem.IsManualSlot(8), "automatic history cannot be overwritten or deleted as manual slots");
        }
        finally
        {
            if (!folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new IOException("Invalid synthetic history folder");
            foreach (string name in names)
            {
                string path = Path.Combine(folder, name);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
            }
            if (!Directory.GetFileSystemEntries(folder).Any()) Directory.Delete(folder);
        }
    }

    private static bool Safe(GameSaveSystem saves, ZeldaFourWayMover mover) => (bool)Call(saves, "IsAutomaticSaveSafe", mover);
    private static void RequireIsolation() => Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "disposable project only");
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception("AutoSaveScheduler: " + label); }
    private static FieldInfo Field(Type type, string name)
    {
        for (Type current = type; current != null; current = current.BaseType)
        {
            var field = current.GetField(name, Flags | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
    private static object Get(object target, string name) => Field(target.GetType(), name).GetValue(target);
    private static T Get<T>(object target, string name) => (T)Get(target, name);
    private static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    private static object StaticCall(string name, params object[] args) => typeof(GameSaveSystem).GetMethod(name, Flags).Invoke(null, args);
}
