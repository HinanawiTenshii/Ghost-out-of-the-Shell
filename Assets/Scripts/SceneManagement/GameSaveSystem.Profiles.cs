using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class GameSaveSystem
{
    public const int ProfileCount = 3;
    public const string NewGameScene = "TutorialLevel";

    [Serializable] public sealed class ProfileData
    {
        public int version = 2, lastSlot;
        public string id, campaignId, name, scene, levelName, savedAt;
        public long updatedUtcTicks;
        // A successful load selects a continuation branch without rewriting or
        // rotating its snapshot. Missing fields in legacy v2 profiles mean none.
        public long resumeUtcTicks, resumeSavedUtcTicks;
        public int resumeSlot;
        public string resumeCheckpointId;
        public List<LevelVisit> entries = new List<LevelVisit>();
    }
    [Serializable] public sealed class LevelOutcome
    {
        public string levelId;
        public LevelRunStatistics.State statistics;
        public List<string> completedQuests = new List<string>();
        public bool[] challenges;
    }

    private ProfileData activeProfile, pendingProfile;
    private bool sessionBound, restoreFailed;
    private string sessionLevelId;
    private List<LevelOutcome> outcomes = new List<LevelOutcome>();
    public string ActiveProfileId => activeProfile != null ? activeProfile.id : null;
    public string ActiveProfileName => activeProfile != null ? activeProfile.name : "未选择进度";
    public static string ProfilesDirectory => Path.Combine(SaveDirectory, "profiles");
    public static string CheckpointsDirectory => Path.Combine(SaveDirectory, "checkpoints");

    public static string ProfileId(int index)
    {
        if (index < 0 || index >= ProfileCount) throw new ArgumentOutOfRangeException(nameof(index));
        return "slot-" + (index + 1);
    }
    private static bool IsProfileId(string id) => id == "slot-1" || id == "slot-2" || id == "slot-3";
    private static bool ValidId(string id) => Guid.TryParseExact(id, "N", out _);
    private static string ProfileFolder(string id)
    {
        if (!IsProfileId(id)) throw new InvalidDataException("无效的进度栏。");
        return Path.Combine(ProfilesDirectory, id);
    }
    // Keep the original quick/manual numbers stable. Automatic history never
    // borrows a manual filename, including during repair and profile deletion.
    private static readonly int[] KnownSaveSlots = { 0, 1, 2, 3, 4, 5, 6, 7, 8 };
    public static bool IsManualSlot(int slot) => slot >= 2 && slot <= 6;
    public static bool IsAutomaticSlot(int slot) => slot == 0 || slot == 7 || slot == 8;
    public static string SlotLabel(int slot)
    {
        if (slot == 0) return "自动存档 · 最新";
        if (slot == 7) return "自动存档 · 上一次";
        if (slot == 8) return "自动存档 · 更早";
        if (slot == 1) return "快速存档";
        if (IsManualSlot(slot)) return "手动存档 " + (slot - 1);
        throw new ArgumentOutOfRangeException(nameof(slot));
    }
    private static string SlotName(int slot)
    {
        if (slot == 0) return "auto.json";
        if (slot == 7) return "auto-previous-1.json";
        if (slot == 8) return "auto-previous-2.json";
        if (slot == 1) return "quick.json";
        if (IsManualSlot(slot)) return "manual-" + (slot - 1) + ".json";
        throw new ArgumentOutOfRangeException(nameof(slot));
    }
    private static string ProfileSlotPath(string id, int slot) => Path.Combine(ProfileFolder(id), SlotName(slot));
    private static string CheckpointPath(string id)
    {
        if (!ValidId(id)) throw new InvalidDataException("无效的关卡入口标识。");
        return Path.Combine(CheckpointsDirectory, id + ".json");
    }
    private static bool BelongsTo(SaveData data, ProfileData profile) => data != null && profile != null &&
        data.version == 4 && data.profileId == profile.id && data.campaignId == profile.campaignId;

    private static void WriteData(string path, object data)
    {
        string json = JsonUtility.ToJson(data);
        var bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new Envelope { payload = json, checksum = Hash(json) }));
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, null);
        else File.Move(temporary, path);
    }
    private static T ReadChecked<T>(string path) where T : class
    {
        try
        {
            var envelope = JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8));
            return envelope != null && !string.IsNullOrEmpty(envelope.payload) && envelope.checksum == Hash(envelope.payload)
                ? JsonUtility.FromJson<T>(envelope.payload) : null;
        }
        catch (Exception ex) { Debug.LogWarning("无法读取 " + path + ": " + ex.Message); return null; }
    }
    private static ProfileData NewProfile(int index) => new ProfileData {
        id = ProfileId(index), campaignId = Guid.NewGuid().ToString("N"), name = "进度 " + (index + 1)
    };

    private static LevelVisit CopyEntry(LevelVisit visit) => new LevelVisit {
        id = visit.id, name = visit.name, entryScene = visit.entryScene, checkpointId = visit.checkpointId,
        capturedUtcTicks = visit.capturedUtcTicks, initialCollectibleCount = visit.initialCollectibleCount
    };
    private static List<LevelVisit> CopyEntries(List<LevelVisit> source)
    {
        var result = new List<LevelVisit>();
        foreach (var entry in source ?? new List<LevelVisit>()) if (entry != null) result.Add(CopyEntry(entry));
        return result;
    }
    // Entry availability belongs to the campaign, independently of the loaded
    // gameplay moment. Loading an older slot must not replace a newer entry.
    private static void MergeEntries(List<LevelVisit> target, List<LevelVisit> source)
    {
        if (source == null) return;
        foreach (var entry in source)
        {
            if (entry == null || string.IsNullOrEmpty(entry.id) || !ValidId(entry.checkpointId)) continue;
            int index = target.FindIndex(v => string.Equals(v.id, entry.id, StringComparison.OrdinalIgnoreCase));
            if (index < 0) target.Add(CopyEntry(entry));
            else if (entry.capturedUtcTicks > target[index].capturedUtcTicks) target[index] = CopyEntry(entry);
        }
    }
    private static void UpdateProfile(ProfileData profile, SaveData data, int slot)
    {
        if (profile.campaignId != data.campaignId)
        {
            profile.entries = new List<LevelVisit>();
            ClearResumeReference(profile);
        }
        profile.campaignId = data.campaignId;
        profile.entries = profile.entries ?? new List<LevelVisit>();
        MergeEntries(profile.entries, data.levelEntries);
        profile.lastSlot = slot; profile.scene = data.scene; profile.levelName = data.levelName;
        profile.savedAt = data.savedAt; profile.updatedUtcTicks = data.savedUtcTicks;
    }
    private void CommitProfile(SaveData data, int slot)
    {
        UpdateProfile(activeProfile, data, slot);
        ClearResumeReference(activeProfile);
        WriteData(Path.Combine(ProfileFolder(activeProfile.id), "profile.json"), activeProfile);
        // A newly committed campaign invalidates ALL old manual/quick saves in
        // that slot. The campaign ID also protects reads if file deletion fails.
        foreach (int i in KnownSaveSlots)
        {
            if (i == slot) continue;
            string path = ProfileSlotPath(activeProfile.id, i);
            if (!File.Exists(path)) continue;
            var other = ReadChecked<SaveData>(path);
            if (BelongsTo(other, activeProfile)) continue;
            try { File.Delete(path); if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); }
            catch (Exception ex) { Debug.LogWarning("无法清理已覆盖存档：" + ex.Message); }
        }
    }

    private static SaveData FindLatest(ProfileData profile, out int slot, bool anyCampaign = false)
    {
        SaveData latest = null; slot = -1;
        foreach (int i in KnownSaveSlots)
        {
            var data = ReadData(ProfileSlotPath(profile.id, i));
            if (data == null || data.profileId != profile.id || !ValidId(data.campaignId) ||
                (!anyCampaign && !BelongsTo(data, profile))) continue;
            if (latest != null && data.savedUtcTicks <= latest.savedUtcTicks) continue;
            latest = data; slot = i;
        }
        return latest;
    }
    private static bool RepairProfileIndex(ProfileData profile)
    {
        var latest = FindLatest(profile, out int slot, true);
        if (latest == null) return false;
        UpdateProfile(profile, latest, slot);
        foreach (int i in KnownSaveSlots)
        {
            var data = ReadData(ProfileSlotPath(profile.id, i));
            if (BelongsTo(data, profile)) MergeEntries(profile.entries, data.levelEntries);
        }
        WriteData(Path.Combine(ProfileFolder(profile.id), "profile.json"), profile);
        return true;
    }
    public List<ProfileData> ListProfiles()
    {
        var result = new List<ProfileData>();
        try
        {
            for (int index = 0; index < ProfileCount; index++)
            {
                string id = ProfileId(index);
                if (!Directory.Exists(ProfileFolder(id))) continue;
                string path = Path.Combine(ProfileFolder(id), "profile.json");
                var profile = File.Exists(path) ? ReadChecked<ProfileData>(path) : null;
                if (profile == null || profile.version != 2 || profile.id != id || !ValidId(profile.campaignId))
                {
                    profile = new ProfileData { id = id, name = "进度 " + (index + 1) };
                    if (!RepairProfileIndex(profile)) continue;
                }
                profile.entries = profile.entries ?? new List<LevelVisit>();
                bool stale = Array.IndexOf(KnownSaveSlots, profile.lastSlot) < 0 ||
                    !File.Exists(ProfileSlotPath(id, profile.lastSlot));
                foreach (int slot in KnownSaveSlots)
                {
                    if (stale) break;
                    string slotPath = ProfileSlotPath(id, slot);
                    stale = File.Exists(slotPath) && File.GetLastWriteTimeUtc(slotPath) > File.GetLastWriteTimeUtc(path);
                }
                if (stale && !RepairProfileIndex(profile)) continue;
                result.Add(profile);
            }
        }
        catch (Exception ex) { Debug.LogException(ex); Message("进度列表读取失败：" + ex.Message); }
        return result;
    }
    public bool SelectProfile(string id)
    {
        if (loading || writing || !IsTitle(SceneManager.GetActiveScene().name)) return false;
        var profile = ListProfiles().Find(p => p.id == id);
        if (profile == null) { Message("该进度栏为空或无法读取。"); return false; }
        activeProfile = profile; sessionBound = false;
        Changed?.Invoke(); return true;
    }
    public bool DeleteProfile(string id)
    {
        if (!IsProfileId(id) || loading || writing || preparingSceneSave ||
            RetroSceneLoadReveal.IsBlockingInput || !IsTitle(SceneManager.GetActiveScene().name)) return false;
        writing = true;
        try
        {
            string folder = ProfileFolder(id);
            if (!Directory.Exists(folder)) { Message("该进度栏为空。"); return false; }
            ValidateProfileDirectory(folder);
            string removed = Path.Combine(ProfilesDirectory, "deleted-" + id + "-" + Guid.NewGuid().ToString("N"));
            // Detach the whole slot in one operation, so a failed/interrupted
            // file cleanup cannot reconstruct a partially deleted campaign.
            ValidateProfileDirectory(removed);
            Directory.Move(folder, removed);
            if (activeProfile != null && activeProfile.id == id)
            {
                activeProfile = null; sessionBound = false; sessionLevelId = null;
                visits.Clear(); outcomes.Clear(); restoreFailed = false;
            }
            try { CleanupDeletedProfile(removed, id); }
            catch (Exception ex) { Debug.LogWarning("已删除进度的文件将在下次启动时继续清理：" + ex.Message); }
            LastMessage = null;
            Changed?.Invoke();
            return true;
        }
        catch (Exception ex) { Debug.LogException(ex); Message("删除进度失败，请重试。"); return false; }
        finally { writing = false; }
    }

    private static void ValidateProfileDirectory(string directory)
    {
        string root = Path.GetFullPath(ProfilesDirectory) + Path.DirectorySeparatorChar;
        if (!Path.GetFullPath(directory).StartsWith(root, StringComparison.OrdinalIgnoreCase) ||
            (File.GetAttributes(ProfilesDirectory) & FileAttributes.ReparsePoint) != 0 ||
            (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0))
            throw new IOException("无效的进度目录。");
    }

    private static void CleanupDeletedProfile(string directory, string id)
    {
        ValidateProfileDirectory(directory);
        var campaigns = new HashSet<string>();
        var checkpoints = new HashSet<string>();
        Action<List<LevelVisit>> collect = entries =>
        {
            if (entries == null) return;
            foreach (var entry in entries)
                if (entry != null && ValidId(entry.checkpointId)) checkpoints.Add(entry.checkpointId);
        };
        string manifest = Path.Combine(directory, "profile.json");
        var profile = File.Exists(manifest) ? ReadChecked<ProfileData>(manifest) : null;
        if (profile != null && profile.id == id && ValidId(profile.campaignId))
        { campaigns.Add(profile.campaignId); collect(profile.entries); }
        foreach (int slot in KnownSaveSlots)
        {
            string path = Path.Combine(directory, SlotName(slot));
            var data = File.Exists(path) ? ReadChecked<SaveData>(path) : null;
            if (data == null || data.profileId != id || !ValidId(data.campaignId)) continue;
            campaigns.Add(data.campaignId); collect(data.levelEntries); collect(data.visitedLevels);
        }
        if (Directory.Exists(CheckpointsDirectory))
        {
            if ((File.GetAttributes(CheckpointsDirectory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("无效的关卡入口目录。");
            // Include superseded entrances from this campaign. A replacement
            // campaign in the same slot must survive deferred cleanup.
            foreach (string path in Directory.GetFiles(CheckpointsDirectory, "*.json"))
            {
                string checkpoint = Path.GetFileNameWithoutExtension(path);
                if (!ValidId(checkpoint)) continue;
                var data = ReadChecked<SaveData>(path);
                if (data != null && data.profileId == id && campaigns.Contains(data.campaignId)) checkpoints.Add(checkpoint);
                else if (data != null) checkpoints.Remove(checkpoint);
            }
            foreach (string checkpoint in checkpoints) DeleteSaveFile(CheckpointPath(checkpoint));
        }
        foreach (int slot in KnownSaveSlots) DeleteSaveFile(Path.Combine(directory, SlotName(slot)));
        DeleteSaveFile(manifest);
        if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
    }

    private static string DeletedProfileId(string directoryName)
    {
        for (int index = 0; index < ProfileCount; index++)
        {
            string id = ProfileId(index), prefix = "deleted-" + id + "-";
            if (directoryName.StartsWith(prefix, StringComparison.Ordinal) && ValidId(directoryName.Substring(prefix.Length))) return id;
        }
        return null;
    }
    public bool ContinueLatest()
    {
        if (loading || writing || !IsTitle(SceneManager.GetActiveScene().name)) return false;
        ProfileData target = null; SaveData latest = null;
        int selectedSlot = -1;
        string selectedCheckpointId = null;
        long selectedOrder = long.MinValue;
        foreach (var profile in ListProfiles())
        {
            var data = ResolveContinueTarget(profile, out int slot, out string checkpointId, out long order);
            if (data == null || !Application.CanStreamedLevelBeLoaded(data.scene) ||
                (latest != null && order <= selectedOrder)) continue;
            target = profile; latest = data;
            selectedSlot = slot; selectedCheckpointId = checkpointId; selectedOrder = order;
        }
        if (latest == null) { Message("没有可继续的游戏进度。"); return false; }
        var selected = target;
        return RetroSceneLoadReveal.BeginTransition(latest.scene,
            () => LoadCovered(latest, selected, selectedSlot, selectedCheckpointId));
    }
    public bool HasContinuableProgress => ListProfiles().Exists(profile =>
    {
        var data = ResolveContinueTarget(profile, out _, out _, out _);
        return data != null && Application.CanStreamedLevelBeLoaded(data.scene);
    });
    // Kept for gameplay callers; the title's Continue button uses ContinueLatest.
    public bool ContinueProfile()
    {
        if (activeProfile == null || loading || writing) return false;
        var latest = ResolveContinueTarget(activeProfile, out int slot, out string checkpointId, out _);
        if (latest == null) { Message("该进度没有可读取的存档。"); return false; }
        if (!Application.CanStreamedLevelBeLoaded(latest.scene)) { Message("存档场景未包含在构建中"); return false; }
        var target = activeProfile;
        return RetroSceneLoadReveal.BeginTransition(latest.scene, () => LoadCovered(latest, target, slot, checkpointId));
    }
    public List<LevelVisit> ReplayEntries => activeProfile != null ? CopyEntries(activeProfile.entries) : new List<LevelVisit>();
    public static bool HasCheckpoint(LevelVisit entry) => entry != null && ValidId(entry.checkpointId) && File.Exists(CheckpointPath(entry.checkpointId));
    public static string LevelIdForScene(string scene)
    {
        if (LevelSceneGroup.TryGetLevelForScene(scene, out var group)) return group.LevelId;
        if (scene.StartsWith("Level1", StringComparison.OrdinalIgnoreCase)) return "Level1";
        if (scene.StartsWith("Level2", StringComparison.OrdinalIgnoreCase)) return "level2";
        return scene;
    }
    public static string LevelLabel(string scene)
    {
        if (scene == "TutorialLevel") return "教学关卡";
        if (scene == "Level0") return "Level 0-监牢";
        if (scene != null && scene.StartsWith("Level1", StringComparison.OrdinalIgnoreCase)) return "Level 1-城堡";
        if (scene != null && scene.StartsWith("Level2", StringComparison.OrdinalIgnoreCase)) return "Level 2-边境城市";
        return scene == "LastScene" ? "尾声" : scene;
    }

    private LevelVisit CurrentEntry => activeProfile?.entries.Find(v => string.Equals(v.id,
        LevelIdForScene(SceneManager.GetActiveScene().name), StringComparison.OrdinalIgnoreCase));
    public bool CanRestartCurrentLevel => sessionBound && !loading && !writing && !restoreFailed && HasCheckpoint(CurrentEntry);
    public bool RestartCurrentLevel()
    {
        if (!CanRestartCurrentLevel) { Message("本关没有可用的入口记录。"); return false; }
        return RestoreEntry(CurrentEntry);
    }
    public bool ReplayLevel(string levelId)
    {
        if (activeProfile == null || loading || writing || !IsTitle(SceneManager.GetActiveScene().name)) return false;
        return RestoreEntry(ReplayEntries.Find(v => string.Equals(v.id, levelId, StringComparison.OrdinalIgnoreCase)));
    }
    private bool RestoreEntry(LevelVisit entry)
    {
        if (!HasCheckpoint(entry)) { Message("该关卡没有可用的入口存档。"); return false; }
        var data = ReadData(CheckpointPath(entry.checkpointId));
        if (!BelongsTo(data, activeProfile) || data.visitedLevels == null ||
            !data.visitedLevels.Exists(v => v.id == entry.id && v.checkpointId == entry.checkpointId) ||
            !Application.CanStreamedLevelBeLoaded(data.scene))
        { Message("关卡入口记录损坏或与当前版本不兼容。"); return false; }
        var target = activeProfile;
        return RetroSceneLoadReveal.BeginTransition(data.scene, () => LoadCovered(data, target, -1, entry.checkpointId));
    }

    public bool StartNewGame(int profileSlot)
    {
        if (profileSlot < 0 || profileSlot >= ProfileCount || loading || writing ||
            !IsTitle(SceneManager.GetActiveScene().name) || !Application.CanStreamedLevelBeLoaded(NewGameScene)) return false;
        return RetroSceneLoadReveal.BeginTransition(NewGameScene, () =>
        {
            var previous = activeProfile;
            try
            {
                loading = true;
                SceneTravelStateManager.GetOrCreate().ResetAllSceneStates();
                PersistentInventory.Instance?.ResetForProfile();
                PlayerGrowthAttributes.Instance?.ApplyRuntimeState(default);
                BeginNewGame(profileSlot);
                Time.timeScale = 1f;
                RetroSceneLoadReveal.LoadSceneAsync(NewGameScene);
            }
            catch (Exception ex)
            {
                activeProfile = previous; sessionBound = false; restoreFailed = true;
                Debug.LogException(ex); Message("新游戏启动失败：" + ex.Message);
            }
            finally { loading = false; }
        });
    }

    public void RecordLevelCompletion(LevelSettlementScreen.Report report)
    {
        if (!sessionBound || report == null) return;
        string id = LevelIdForScene(SceneManager.GetActiveScene().name);
        var outcome = new LevelOutcome { levelId = id, statistics = report.statistics, challenges = report.challenges };
        foreach (var quest in report.quests) if (quest.complete) outcome.completedQuests.Add(quest.title);
        outcomes.RemoveAll(v => string.Equals(v.levelId, id, StringComparison.OrdinalIgnoreCase));
        outcomes.Add(outcome);
    }

    // Only known obsolete save files are removed. Never recursively traverse a
    // user folder or a reparse point; shared scene templates are not progress.
    private void RemoveObsoleteProgress()
    {
        try
        {
            foreach (int slot in KnownSaveSlots) DeleteSaveFile(Path.Combine(SaveDirectory, SlotName(slot)));
            if (!Directory.Exists(ProfilesDirectory)) return;
            foreach (string directory in Directory.GetDirectories(ProfilesDirectory))
            {
                string id = Path.GetFileName(directory);
                string deletedId = DeletedProfileId(id);
                if (deletedId != null)
                {
                    try { CleanupDeletedProfile(directory, deletedId); }
                    catch (Exception ex) { Debug.LogWarning("已删除进度的文件清理未完成：" + ex.Message); }
                    continue;
                }
                if (IsProfileId(id) || (!id.StartsWith("legacy-slot-", StringComparison.Ordinal) && !ValidId(id))) continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                string root = Path.GetFullPath(ProfilesDirectory) + Path.DirectorySeparatorChar;
                if (!Path.GetFullPath(directory).StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (int slot in KnownSaveSlots) DeleteSaveFile(Path.Combine(directory, SlotName(slot)));
                DeleteSaveFile(Path.Combine(directory, "profile.json"));
                if (Directory.GetFileSystemEntries(directory).Length == 0) Directory.Delete(directory);
            }
        }
        catch (Exception ex) { Debug.LogWarning("旧版进度清理未完成：" + ex.Message); }
    }
    private static void DeleteSaveFile(string path)
    {
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp");
    }
}
