using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-400)]
public sealed class GameSaveSystem : MonoBehaviour
{
    [Serializable] public sealed class LevelVisit
    {
        public string id, name, entryScene, initialWorld;
        public int initialCollectibleCount;
        public List<SaveGameCatalog.SceneTemplate> initialScenes = new List<SaveGameCatalog.SceneTemplate>();
    }
    [Serializable] public sealed class SaveData
    {
        public int version = 1;
        public string name, savedAt, scene, levelName, world;
        public int collectibleCount;
        public List<LevelVisit> visitedLevels;
        public SceneTravelStateManager.DiskPlayer player;
        public QuestJournalManager.SaveState journal;
        public string[] discoveredItems;
        public List<MapPointOfInterestRecord> mapPoints;
    }
    [Serializable] private sealed class Envelope { public string payload, checksum; }

    public const int SlotCount = 7;
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
        // Save captures the world once and also records first-visit state.
        // Finish while fully covered, before player input resumes.
        if (ZeldaRuntimeRegistry.GetControlledMover() != null && !IsTitle(scene.name)) Save(0, false);
        }
        finally
        {
            if (generation == sceneGeneration) preparingSceneSave = false;
        }
    }
    private static bool IsTitle(string scene) => scene.Replace(" ", "").Equals("TitleScreen", StringComparison.OrdinalIgnoreCase);
    public void BeginNewGame() { visits.Clear(); }

    private void EnsureLevelVisit(string scene, string world, int collected)
    {
        LevelSceneGroup.TryGetLevelForScene(scene, out var group);
        string id = group != null ? group.LevelId : scene.StartsWith("Level1", StringComparison.OrdinalIgnoreCase) ? "level1" : scene;
        if (visits.Exists(v => v.id == id)) return;
        var visit = new LevelVisit { id = id, name = group != null ? group.DisplayName : id,
            entryScene = scene, initialWorld = world, initialCollectibleCount = collected };
        if (catalog != null)
        foreach (var template in catalog.scenes)
        {
            bool included = group != null ? group.ContainsScene(template.name) :
                id == "level1" ? template.name.StartsWith("Level1", StringComparison.OrdinalIgnoreCase) : template.name == scene;
            if (included) visit.initialScenes.Add(template.CreateSaveSnapshot());
        }
        visits.Add(visit);
    }

    public bool Save(int slot, bool notify = true)
    {
        if (slot < 0 || slot >= SlotCount || loading || writing) return false;
        var manager = SceneTravelStateManager.GetOrCreate();
        if (manager.IsSceneTravelRestoreInProgress || manager.IsQuickRestartPending ||
            ZeldaRuntimeRegistry.GetControlledMover() == null || IsTitle(SceneManager.GetActiveScene().name))
        { Message("当前无法存档"); return false; }
        writing = true;
        try
        {
            var world = manager.CaptureDiskWorld();
            var player = manager.CaptureDiskPlayer();
            var scene = SceneManager.GetActiveScene().name;
            EnsureLevelVisit(scene, world, player.growth.collectibleCount);
            LevelSceneGroup.TryGetLevelForScene(scene, out var group);
            var now = DateTime.Now;
            var data = new SaveData {
                name = (slot == 0 ? "A" : slot == 1 ? "Q" : "") + now.ToString("yyyy-MM-dd-HH-mm"),
                savedAt = now.ToString("yyyy-MM-dd HH:mm:ss"), scene = scene,
                levelName = group != null ? group.DisplayName : scene,
                world = world, player = player, collectibleCount = player.growth.collectibleCount,
                visitedLevels = visits, journal = QuestJournalManager.GetOrCreate().CaptureSaveState(),
                discoveredItems = ItemDescriptionWindow.CaptureDiscoveryHistory(),
                mapPoints = MapPointOfInterestManager.GetOrCreate().CaptureSaveState()
            };
            var json = JsonUtility.ToJson(data);
            var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = json, checksum = Hash(json) }));
            Directory.CreateDirectory(SaveDirectory);
            var target = SlotPath(slot);
            var temporary = target + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (File.Exists(target)) File.Replace(temporary, target, null);
            else File.Move(temporary, target);
            if (notify) Message("游戏已保存");
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) { Debug.LogException(ex); Message("存档失败：" + ex.Message); return false; }
        finally { writing = false; }
    }

    public SaveData Read(int slot)
    {
        if (slot < 0 || slot >= SlotCount || !File.Exists(SlotPath(slot))) return null;
        try
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(SlotPath(slot), Encoding.UTF8));
            if (envelope == null || string.IsNullOrEmpty(envelope.payload) || envelope.checksum != Hash(envelope.payload)) return null;
            var data = JsonUtility.FromJson<SaveData>(envelope.payload);
            return data != null && data.version == 1 && data.player != null && !string.IsNullOrEmpty(data.world) ? data : null;
        }
        catch (Exception ex) { Debug.LogWarning("Cannot read save slot " + slot + ": " + ex.Message); return null; }
    }
    public bool DeleteManualSave(int slot)
    {
        if (slot < 2 || slot >= SlotCount || loading || writing) return false;
        try
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
            string temporary = path + ".tmp";
            if (File.Exists(temporary)) File.Delete(temporary);
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
        return RetroSceneLoadReveal.BeginTransition(data.scene, () => LoadCovered(data));
    }

    private bool LoadCovered(SaveData data)
    {
        loading = true;
        pendingLoad = data;
        try
        {
            SceneTravelStateManager.GetOrCreate().RestoreDiskWorld(data.world, data.scene, data.player, catalog);
            Time.timeScale = 1f;
            return true;
        }
        catch (Exception ex)
        {
            loading = false; pendingLoad = null;
            Debug.LogException(ex); Message("读档失败：" + ex.Message); return false;
        }
    }
    private void RestoreCompleted()
    {
        if (!loading || pendingLoad == null) return;
        if (!SceneTravelStateManager.GetOrCreate().LastQuickRestartRestoreSucceeded)
        {
            pendingLoad = null; loading = false;
            Message("存档与当前场景不兼容，恢复失败；原存档文件保持不变。");
            return;
        }
        try
        {
            var data = pendingLoad;
            visits = data.visitedLevels ?? new List<LevelVisit>();
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
                    SavedScalarFields.Apply(worn, data.player.wornBoxFields);
                    break;
                }
            }
            Message("游戏已读取");
        }
        finally { pendingLoad = null; loading = false; Changed?.Invoke(); }
        // Loading a slot is also a scene transition; update auto only after restoration.
        Save(0, false);
    }
    private void Message(string message)
    {
        LastMessage = message;
        var hud = FindObjectOfType<ZeldaHealthHeartsUI>();
        if (hud != null) hud.ShowJournalStyleNotificationPopup(message);
    }
    private static string SlotPath(int slot) => Path.Combine(SaveDirectory,
        slot == 0 ? "auto.json" : slot == 1 ? "quick.json" : "manual-" + (slot - 1) + ".json");
    private static string Hash(string data)
    {
        using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }
}
