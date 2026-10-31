using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-400)]
public sealed partial class GameSaveSystem : MonoBehaviour
{
    [Serializable] public sealed class LevelVisit
    {
        public string id, name, entryScene, initialWorld, checkpointId;
        public int initialCollectibleCount;
        public long capturedUtcTicks;
        public List<SaveGameCatalog.SceneTemplate> initialScenes = new List<SaveGameCatalog.SceneTemplate>();
    }
    [Serializable] public sealed class SaveData
    {
        public int version = 4;
        public string profileId, campaignId;
        public long savedUtcTicks;
        public string name, savedAt, scene, levelName, world;
        public int collectibleCount;
        public List<LevelVisit> visitedLevels;
        public SceneTravelStateManager.DiskPlayer player;
        public QuestJournalManager.SaveState journal;
        public string[] discoveredItems;
        public List<MapPointOfInterestRecord> mapPoints;
        public List<LevelOutcome> outcomes;
        public List<LevelVisit> levelEntries;
    }
    [Serializable] private sealed class Envelope { public string payload, checksum; }

    public const int SlotCount = 9;
    public static GameSaveSystem Instance { get; private set; }
    public static bool IsLoading => Instance != null && Instance.loading;
    public static bool IsPreparingSceneSave => Instance != null && Instance.preparingSceneSave;
    private bool preparingSceneSave;
    public static string SaveDirectory => Path.Combine(Application.persistentDataPath, "Saves");
    public string LastMessage { get; private set; }
    public event Action Changed;
    private List<LevelVisit> visits = new List<LevelVisit>();
    private SaveGameCatalog catalog;
    private bool loading, writing;
    private SaveData pendingLoad;
    private SaveData pendingEntranceSnapshot;
    private bool arenaLayoutLoad;
    private int sceneGeneration;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance == null) new GameObject("Game Save System").AddComponent<GameSaveSystem>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
        RemoveObsoleteProgress();
        SceneManager.sceneLoaded += SceneLoaded;
        SceneTravelStateManager.GetOrCreate().QuickRestartCompleted += RestoreCompleted;
    }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        if (SceneTravelStateManager.Instance != null)
            SceneTravelStateManager.Instance.QuickRestartCompleted -= RestoreCompleted;
        if (Instance == this) Instance = null;
    }
    private void Update()
    {
        TickAutoSave();
        TickAutoSaveCompletionNotification();
        if (loading || DocumentReader.IsInputBlocked || Time.timeScale <= 0f) return;
        if (IsTitle(SceneManager.GetActiveScene().name) || ZeldaRuntimeRegistry.GetControlledMover() == null) return;
        if (Input.GetKeyDown(KeyCode.F1)) Save(1);
        else if (Input.GetKeyDown(KeyCode.F2)) Load(1);
    }
    private void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Single) return;
        int generation = ++sceneGeneration;
        if (!loading)
        {
            preparingSceneSave = true;
            StartCoroutine(AutoSaveAfterLoad(scene, generation));
        }
    }
    private IEnumerator AutoSaveAfterLoad(Scene scene, int generation)
    {
        try
        {
        yield return null;
        yield return null;
        while (SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress ||
            SceneTravelStateManager.GetOrCreate().IsQuickRestartPending)
        {
            if (generation != sceneGeneration) yield break;
            yield return null;
        }
        if (generation != sceneGeneration || scene != SceneManager.GetActiveScene() || loading) yield break;
        // Edition starting growth belongs in the immutable Level2 entrance too.
        ExhibitionEdition.PrepareLevelEntry(scene.name);
        // Capture an immutable, pristine logical-level entrance under the cover.
        // Floor travel does not replace it, and waits for safety before rotating auto.
        // Authored entrances may intentionally begin as a ghost (Tutorial/Level0).
        if (ZeldaRuntimeRegistry.GetControlledMover() != null && !IsTitle(scene.name) && !RuntimeUiVisibility.IsPauseOnlyScene &&
            !DevlogDemoSceneSettings.SuppressesAutomaticSaves(scene))
        {
            if (!sessionBound || !string.Equals(sessionLevelId, LevelIdForScene(scene.name), StringComparison.OrdinalIgnoreCase))
            {
                if (!SaveCore(0, false, true)) RequestAutoSave("重试入口存档", true);
            }
            else RequestAutoSave("场景切换完成", true);
        }
        }
        finally
        {
            if (generation == sceneGeneration) preparingSceneSave = false;
        }
    }
    private static bool IsTitle(string scene) => scene.Replace(" ", "").Equals("TitleScreen", StringComparison.OrdinalIgnoreCase);
    private void BeginNewGame(int profileSlot)
    {
        pendingEntranceSnapshot = null;
        ResetAutoSaveSession();
        activeProfile = NewProfile(profileSlot);
        sessionBound = true;
        restoreFailed = false;
        visits.Clear();
        outcomes.Clear();
        sessionLevelId = null;
    }

    private void EnsureLevelVisit(SaveData data, bool captureEntry)
    {
        string scene = data.scene;
        LevelSceneGroup.TryGetLevelForScene(scene, out var group);
        string id = LevelIdForScene(scene);
        // A new logical-level entrance replaces the old entry. Floor travel,
        // ordinary saves and explicit save restoration never capture an entry.
        if (!captureEntry || string.Equals(sessionLevelId, id, StringComparison.OrdinalIgnoreCase)) return;
        var visit = new LevelVisit { id = id, name = group != null ? group.DisplayName : id,
            entryScene = scene, initialCollectibleCount = data.collectibleCount, capturedUtcTicks = DateTime.UtcNow.Ticks };
        if (catalog != null)
        foreach (var template in catalog.scenes)
        {
            bool included = group != null ? group.ContainsScene(template.name) :
                string.Equals(LevelIdForScene(template.name), id, StringComparison.OrdinalIgnoreCase);
            if (included) visit.initialScenes.Add(SaveSceneTemplateStore.Reference(template));
        }
        if (captureEntry)
        {
            visit.checkpointId = Guid.NewGuid().ToString("N");
            // Commit the immutable entry BEFORE any slot/index can reference it.
            data.visitedLevels = CreateCompactVisits();
            data.visitedLevels.RemoveAll(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
            data.visitedLevels.Add(visit);
            data.levelEntries = CopyEntries(activeProfile.entries);
            MergeEntries(data.levelEntries, new List<LevelVisit> { visit });
            WriteData(CheckpointPath(visit.checkpointId), data);
        }
        visits.RemoveAll(v => string.Equals(v.id, id, StringComparison.OrdinalIgnoreCase));
        visits.Add(visit);
        MergeEntries(activeProfile.entries, new List<LevelVisit> { visit });
        sessionLevelId = id;
    }

    public bool Save(int slot, bool notify = true)
    {
        if (slot == 0 && !CanCommitAutomaticSave())
        {
            RequestAutoSave("请求自动保存", true);
            return false;
        }
        using (SceneLoadMetrics.Measure("Save.Total")) return SaveCore(slot, notify);
    }

    private bool SaveCore(int slot, bool notify, bool captureEntry = false)
    {
        if ((slot != 0 && slot != 1 && !IsManualSlot(slot)) || loading || writing || restoreFailed ||
            (preparingSceneSave && !captureEntry)) return false;
        var manager = SceneTravelStateManager.GetOrCreate();
        if (manager.IsSceneTravelRestoreInProgress || manager.IsQuickRestartPending ||
            ZeldaRuntimeRegistry.GetControlledMover() == null || IsTitle(SceneManager.GetActiveScene().name))
        { Message("当前无法存档"); return false; }
        writing = true;
        try
        {
            // Direct editor scene play uses an empty slot only; it must never
            // silently overwrite one of the player's three campaigns.
            if (!sessionBound)
            {
                var profiles = ListProfiles();
                int emptySlot = -1;
                for (int i = 0; i < ProfileCount; i++)
                    if (!profiles.Exists(p => p.id == ProfileId(i))) { emptySlot = i; break; }
                if (emptySlot < 0) { Message("三个进度栏已满，请从标题界面选择栏位开始新游戏。"); return false; }
                BeginNewGame(emptySlot);
            }
            // Retry the captured entrance, never recapture a later gameplay
            // position as the logical-level start after a temporary IO failure.
            if (pendingEntranceSnapshot != null)
            {
                EnsureLevelVisit(pendingEntranceSnapshot, true);
                pendingEntranceSnapshot = null;
            }
            var world = manager.CaptureDiskWorld();
            var player = manager.CaptureDiskPlayer();
            var scene = SceneManager.GetActiveScene().name;
            LevelSceneGroup.TryGetLevelForScene(scene, out var group);
            var now = DateTime.Now;
            var data = new SaveData {
                profileId = activeProfile.id, campaignId = activeProfile.campaignId, savedUtcTicks = DateTime.UtcNow.Ticks,
                name = (slot == 0 ? "A" : slot == 1 ? "Q" : "") + now.ToString("yyyy-MM-dd-HH-mm"),
                savedAt = now.ToString("yyyy-MM-dd HH:mm:ss"), scene = scene,
                levelName = group != null ? group.DisplayName : scene,
                world = world, player = player, collectibleCount = player.growth.collectibleCount,
                journal = QuestJournalManager.GetOrCreate().CaptureSaveState(),
                discoveredItems = ItemDescriptionWindow.CaptureDiscoveryHistory(),
                mapPoints = MapPointOfInterestManager.GetOrCreate().CaptureSaveState(), outcomes = outcomes,
                levelEntries = CopyEntries(activeProfile.entries)
            };
            if (captureEntry && !string.Equals(sessionLevelId, LevelIdForScene(scene), StringComparison.OrdinalIgnoreCase))
                pendingEntranceSnapshot = data;
            EnsureLevelVisit(data, captureEntry);
            pendingEntranceSnapshot = null;
            data.visitedLevels = CreateCompactVisits();
            data.levelEntries = CopyEntries(activeProfile.entries);
            if (slot == 0) CommitAutomaticSave(data);
            else WriteData(SlotPath(slot), data);
            visits = data.visitedLevels;
            CommitProfile(data, slot);
            AutoSaveCommitted();
            // Every automatic entry/event save reports success, even when its
            // caller suppresses manual-save messages. Never notify on request.
            autoSaveCompletionPending = slot == 0;
            if (slot != 0 && notify) Message("游戏已保存");
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) { autoSaveCompletionPending = false; Debug.LogException(ex); Message("存档失败：" + ex.Message); return false; }
        finally { writing = false; }
    }

    private List<LevelVisit> CreateCompactVisits()
    {
        var result = new List<LevelVisit>(visits.Count);
        foreach (var visit in visits)
        {
            var copy = new LevelVisit { id = visit.id, name = visit.name, entryScene = visit.entryScene,
                checkpointId = visit.checkpointId, capturedUtcTicks = visit.capturedUtcTicks,
                initialWorld = SceneTravelStateManager.CompactLegacyDiskWorld(visit.initialWorld), initialCollectibleCount = visit.initialCollectibleCount };
            if (visit.initialScenes != null)
                foreach (var template in visit.initialScenes) copy.initialScenes.Add(SaveSceneTemplateStore.Reference(template));
            result.Add(copy);
        }
        return result;
    }

    public SaveData Read(int slot)
    {
        if (slot < 0 || slot >= SlotCount || activeProfile == null) return null;
        var data = ReadData(SlotPath(slot));
        return BelongsTo(data, activeProfile) ? data : null;
    }
    private static SaveData ReadData(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var data = ReadChecked<SaveData>(path);
            if (data == null || data.version != 4 || data.player == null || string.IsNullOrEmpty(data.world)) return null;
            if (data.visitedLevels != null)
                foreach (var visit in data.visitedLevels)
                    if (visit.initialScenes != null)
                        foreach (var template in visit.initialScenes)
                            if (!string.IsNullOrEmpty(template.templateKey) && !SaveSceneTemplateStore.Validate(template.templateKey)) return null;
            return data;
        }
        catch (Exception ex) { Debug.LogWarning("Cannot read save " + path + ": " + ex.Message); return null; }
    }
    public bool DeleteManualSave(int slot)
    {
        if (!IsManualSlot(slot) || loading || writing || activeProfile == null) return false;
        try
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
            string temporary = path + ".tmp";
            if (File.Exists(temporary)) File.Delete(temporary);
            RepairProfileIndex(activeProfile);
            Message("存档已删除");
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            Message("删除存档失败：" + ex.Message);
            return false;
        }
    }
    public bool Load(int slot)
    {
        if (loading || writing) return false;
        var data = Read(slot);
        if (data == null) { Message("该栏位没有可读取的存档"); return false; }
        if (!Application.CanStreamedLevelBeLoaded(data.scene)) { Message("存档场景未包含在构建中"); return false; }
        var target = activeProfile;
        return RetroSceneLoadReveal.BeginTransition(data.scene, () => LoadCovered(data, target, slot));
    }

    /// <summary>Use the normal covered loader, replacing only arena poses/doors.
    /// Capture the live inventory/player and all non-arena progress at the actual reload boundary.</summary>
    public bool ReloadArenaLayout(ArenaChallengeController arena, bool returning)
    {
        if (loading || writing || arena == null) return false;
        string scene = arena.gameObject.scene.name;
        return RetroSceneLoadReveal.BeginLayoutTransition(scene, () =>
        {
            try
            {
                var manager = SceneTravelStateManager.GetOrCreate();
                var arenaState = arena.CaptureRuntimeState();
                string world = manager.MergeArenaLayout(manager.CaptureDiskWorld(), arenaState.layoutSnapshot);
                var data = new SaveData {
                    scene = scene, world = world, player = manager.CaptureDiskPlayer(),
                    visitedLevels = CreateCompactVisits(), journal = QuestJournalManager.GetOrCreate().CaptureSaveState(),
                    discoveredItems = ItemDescriptionWindow.CaptureDiscoveryHistory(),
                    mapPoints = MapPointOfInterestManager.GetOrCreate().CaptureSaveState(), outcomes = outcomes,
                    levelEntries = CopyEntries(activeProfile.entries)
                };
                arenaLayoutLoad = true;
                if (!LoadCovered(data)) { arenaLayoutLoad = false; arena.CancelLayoutLoad(); }
            }
            catch (Exception ex)
            {
                arenaLayoutLoad = false; arena.CancelLayoutLoad();
                Debug.LogException(ex); Message("竞技场布局载入失败，请重试。");
            }
        });
    }

    private bool LoadCovered(SaveData data, ProfileData target = null, int sourceSlot = -1, string sourceCheckpointId = null)
    {
        BeginRestoreResumeReference(sourceSlot, sourceCheckpointId);
        pendingEntranceSnapshot = null;
        ResetAutoSaveSession();
        loading = true;
        pendingLoad = data;
        pendingProfile = target ?? activeProfile;
        try
        {
            Time.timeScale = 1f;
            SceneTravelStateManager.GetOrCreate().RestoreDiskWorld(data.world, data.scene, data.player, catalog);
            return true;
        }
        catch (Exception ex)
        {
            loading = false; pendingLoad = null; pendingProfile = null; restoreFailed = true;
            Debug.LogException(ex); Message("读档失败：" + ex.Message); return false;
        }
    }
    private void RestoreCompleted()
    {
        if (!loading || pendingLoad == null) return;
        bool restoredArenaLayout = arenaLayoutLoad;
        if (!SceneTravelStateManager.GetOrCreate().LastQuickRestartRestoreSucceeded)
        {
            pendingLoad = null; pendingProfile = null; loading = false; arenaLayoutLoad = false; restoreFailed = true;
            Message("存档与当前场景不兼容，恢复失败；原存档文件保持不变。");
            return;
        }
        try
        {
            var data = pendingLoad;
            activeProfile = pendingProfile ?? activeProfile;
            MergeEntries(activeProfile.entries, data.levelEntries);
            sessionLevelId = LevelIdForScene(data.scene);
            sessionBound = true;
            restoreFailed = false;
            visits = data.visitedLevels ?? new List<LevelVisit>();
            outcomes = data.outcomes ?? new List<LevelOutcome>();
            QuestJournalManager.GetOrCreate().ApplySaveState(data.journal);
            ItemDescriptionWindow.ApplyDiscoveryHistory(data.discoveredItems);
            MapPointOfInterestManager.GetOrCreate().ApplySaveState(data.mapPoints);
            var mover = ZeldaRuntimeRegistry.GetControlledMover();
            if (mover != null) mover.RestoreSavedMovement(data.player.facing, data.player.stamina);
            if (mover != null && !string.IsNullOrEmpty(data.player.wornBoxId))
            {
                foreach (var template in SaveGameCatalog.LoadPickupTemplates())
                {
                    var item = template as CardboardBoxPickupItem;
                    if (item == null) continue;
                    if (item.ItemId != data.player.wornBoxId) continue;
                    var worn = mover.gameObject.AddComponent<CardboardBoxWearState>();
                    worn.Configure(item, mover);
                    worn.RestoreSavedFields(data.player.wornBoxFields);
                    break;
                }
            }
            if (!arenaLayoutLoad)
            {
                CommitRestoredResumeReference(data);
                Message("游戏已读取");
            }
        }
        catch (Exception ex) { restoreFailed = true; Debug.LogException(ex); Message("存档恢复失败，已停止自动保存。"); }
        finally { pendingLoad = null; pendingProfile = null; loading = false; arenaLayoutLoad = false; Changed?.Invoke(); }
        ArenaChallengeController.FinalizeSceneRestoration();
        // Reading a save is not new progress: never evict rollback history just
        // because the same slot is loaded repeatedly. Arena layout changes are progress.
        ResetAutoSaveSession();
        if (restoredArenaLayout && !restoreFailed) RequestAutoSave("竞技场布局已就绪", true);
    }
    internal void CancelPendingSceneLoad()
    {
        arenaLayoutLoad = false;
        if (!loading) return;
        pendingLoad = null; pendingProfile = null; loading = false; restoreFailed = true;
        Message("场景载入失败，原存档文件保持不变。");
    }
    private void Message(string message)
    {
        LastMessage = message;
        Changed?.Invoke();
        var hud = FindObjectOfType<ZeldaHealthHeartsUI>();
        if (hud != null) hud.ShowJournalStyleNotificationPopup(message);
    }
    private string SlotPath(int slot) => ProfileSlotPath(activeProfile.id, slot);
    private static string Hash(string data)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }
}
