using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public struct QuestJournalEntryDefinition
{
    [Tooltip("Stable identifier used when updating or removing this entry.")]
    public string entryId;
    [Tooltip("Optional parent quest ID. This entry cannot trigger before its parent is in the journal.")]
    public string parentEntryId;
    public string title;
    [TextArea(4, 12)] public string details;
    [Tooltip("When enabled, this entry stays hidden until AddOrUpdateEntry is invoked by a scene event.")]
    public bool addOnlyWhenTriggered;
}

public enum QuestJournalChangeKind
{
    Added,
    Removed,
    Updated,
    Completed,
    Reset,
    InitialSceneLoad
}

public enum QuestJournalEventOperation
{
    AddOrUpdate,
    UpdateExisting,
    Remove,
    AppendDetails,
    Complete
}

[Serializable]
public struct QuestJournalEventChange
{
    [Tooltip("How this scene event changes the journal.")]
    public QuestJournalEventOperation operation;
    [Tooltip("Stable ID of the journal entry. Updating and removing require this ID to match an existing entry.")]
    public string entryId;
    [Tooltip("Optional parent quest ID. Leave empty for independent quests or an already configured binding.")]
    public string parentEntryId;
    public string title;
    [TextArea(4, 12)] public string details;
}

/// <summary>
/// Stores the current quest journal independently from individual scenes.
/// Persistent transitions keep it, while fresh-session transitions clear it.
/// </summary>
[DefaultExecutionOrder(-490)]
public sealed partial class QuestJournalManager : MonoBehaviour
{
    public const string Level0EscapeTitle = "只有死人才能离开";
    public const string Level0GateKeyTitle = "在法的门前";
    public const string Level0EscapeDescription = "原本的肉体已失去控制，目前只能以游魂形态行动。需要附身其他角色，寻找离开监狱的方法。\n\n当前目标：设法从大门逃出监狱。";
    public const string Level0GateKeyDescription = "监狱规定，离开前必须向典狱长报备，经批准后才能领取大门钥匙。钥匙目前存放在典狱长的房间内。\n\n当前目标：进入典狱长的房间，取得监狱大门钥匙。";
    public const string Level1EscapeTitle = "假如再也见不到你";
    public const string Level1BombTitle = "数到三，不许数到五";
    public const string Level1MonsterTitle = "最后的破坏者";
    public const string Level1GateKeyTitle = "制钥师的答案";
    public const string Level1EscapeDescription = "已离开监狱，目前被困在城堡内。城堡大门处于关闭状态，需要找到开启或破坏大门的方法。\n\n当前目标：从大门离开城堡。";
    public const string Level1BombDescription = "信件记载了超级炸弹的制作方法。需要收集[引信]、[超级炸弹外壳]和[魔力火药]，并在信件旁的工作台完成合成。超级炸弹可用于破坏城堡大门。\n\n当前目标：收集三种材料，在工作台合成超级炸弹。";
    public const string Level1MonsterDescription = "城堡内的巨兽曾破坏过城墙，可能具备破坏大门的能力。它被钥匙锁与密码锁束缚，需要[怪物牢笼钥匙]和[牢笼密码]才能解除。\n\n当前目标：找到钥匙和密码，解除束缚并附身巨兽。";
    public const string Level1GateKeyDescription = "城堡大门已上锁，需要对应的[大门钥匙]才能正常开启。\n\n当前目标：找到城堡大门钥匙。";

    [Serializable] public sealed class SavedEntry
    {
        public string id, title, details, scene;
        public bool isLevelObjective;
    }
    [Serializable] public sealed class SavedEvidence
    {
        public string id;
        public List<string> details;
    }
    [Serializable] public sealed class SaveState
    {
        public List<SavedEntry> entries = new List<SavedEntry>();
        public List<string> initialized, completed, pendingCompleted;
        public List<SavedEvidence> evidence = new List<SavedEvidence>();
        public List<QuestJournalEventChange> updates = new List<QuestJournalEventChange>();
        public string tracked, initialTracked;
        public int level2HeroProofStage;
        public List<SavedDialogueActor> dialogueActors = new List<SavedDialogueActor>();
        public List<ParentLink> parents = new List<ParentLink>();
        public List<string> collapsed = new List<string>();
        public bool sandwormMortarInstalled;
        public int summoningLitCrystals;
        public bool summoningPerformed;
        public ArenaQuestStage arenaQuestStage;
        public int arenaQuestWins, arenaQuestRounds;
    }

    public SaveState CaptureSaveState()
    {
        var state = new SaveState {
            initialized = new List<string>(initializedScenes),
            completed = new List<string>(completedEntryIds),
            pendingCompleted = new List<string>(pendingCompletionEvidence),
            tracked = TrackedEntryId, initialTracked = InitialTrackedEntryId,
            level2HeroProofStage = Level2HeroProofStage
        };
        foreach (var e in entries)
            state.entries.Add(new SavedEntry { id = e.Id, title = e.Title, details = e.Details, scene = e.SourceScene, isLevelObjective = e.IsLevelObjective });
        foreach (var e in pendingDetailEvidence)
            state.evidence.Add(new SavedEvidence { id = e.Key, details = new List<string>(e.Value) });
        foreach (var e in pendingUpdateEvidence) state.updates.Add(e.Value);
        foreach (var actor in lastDialogueActors)
            state.dialogueActors.Add(new SavedDialogueActor { dialogueId = actor.Key, characterId = actor.Value });
        foreach (var link in parentEntries)
            state.parents.Add(new ParentLink { entryId = link.Key, parentEntryId = link.Value });
        state.collapsed.AddRange(collapsedEntries);
        state.sandwormMortarInstalled = sandwormMortarInstalled;
        state.summoningLitCrystals = summoningLitCrystals;
        state.summoningPerformed = summoningPerformed;
        state.arenaQuestStage = arenaQuestStage;
        state.arenaQuestWins = arenaQuestWins;
        state.arenaQuestRounds = arenaQuestRounds;
        return state;
    }

    public void ApplySaveState(SaveState state)
    {
        if (state == null) return;
        entries.Clear(); initializedScenes.Clear(); completedEntryIds.Clear();
        pendingCompletionEvidence.Clear(); pendingDetailEvidence.Clear(); pendingUpdateEvidence.Clear();
        RestoreDialogueActors(state.dialogueActors);
        RestoreHierarchy(state);
        sandwormMortarInstalled = state.sandwormMortarInstalled;
        summoningLitCrystals = Mathf.Clamp(state.summoningLitCrystals, 0, SummoningCircle.CrystalCount);
        summoningPerformed = state.summoningPerformed;
        arenaQuestStage = (ArenaQuestStage)Mathf.Clamp((int)state.arenaQuestStage, 0, (int)ArenaQuestStage.Claimed);
        arenaQuestRounds = Mathf.Max(0, state.arenaQuestRounds);
        arenaQuestWins = Mathf.Clamp(state.arenaQuestWins, 0, arenaQuestRounds);
        // Older saves predate the explicit flag but already record the initial
        // level objective separately from the currently tracked side quest.
        foreach (var e in state.entries)
            entries.Add(new Entry(e.id, e.title, e.details, e.scene,
                e.isLevelObjective || (!string.IsNullOrWhiteSpace(state.initialTracked) &&
                string.Equals(e.id, state.initialTracked, StringComparison.Ordinal))));
        if (state.initialized != null) initializedScenes.UnionWith(state.initialized);
        if (state.completed != null) completedEntryIds.UnionWith(state.completed);
        if (state.pendingCompleted != null) pendingCompletionEvidence.UnionWith(state.pendingCompleted);
        foreach (var e in state.evidence) pendingDetailEvidence[e.id] = new List<string>(e.details);
        foreach (var e in state.updates) pendingUpdateEvidence[e.entryId] = e;
        TrackedEntryId = state.tracked; InitialTrackedEntryId = state.initialTracked;
        level2HeroProofStage = Mathf.Clamp(state.level2HeroProofStage, 0, 3);
        pendingLevel2TrialScene = null;
        RestoreLevel2HeroProofStage();
        RestoreSandwormSupportProgress();
        RefreshCounterfeitQuestInventory();
        RefreshSummoningQuest();
        RefreshArenaQuest();
        if (!string.IsNullOrEmpty(TrackedEntryId) && !CanTriggerEntry(TrackedEntryId))
            TrackedEntryId = FindLatestIncompleteEntryId();
        JournalChanged?.Invoke(); TrackedQuestChanged?.Invoke();
    }

    public sealed class Entry
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Details { get; private set; }
        public string SourceScene { get; private set; }
        public bool IsLevelObjective { get; internal set; }

        internal Entry(string id, string title, string details, string sourceScene, bool isLevelObjective = false)
        {
            IsLevelObjective = isLevelObjective;
            Update(id, title, details, sourceScene);
        }

        internal void Update(string id, string title, string details, string sourceScene)
        {
            Id = id;
            // Old saves and scene snapshots can still contain the former title.
            Title = id == Level2SandwormQuestId ? Level2SandwormQuestTitle :
                id == SandwormBaitQuestId ? SandwormBaitTitle :
                id == SandwormMortarQuestId ? SandwormMortarTitle :
                id == CounterfeitQuestId ? CounterfeitQuestTitle :
                id == SummoningQuestId ? SummoningQuestTitle :
                id == ArenaQuestId ? ArenaQuestTitle :
                id == ArenaInjectionQuestId ? ArenaInjectionQuestTitle : GetCanonicalQuestTitle(id, title);
            Details = UpgradeLegacyQuestDetails(id, details);
            SourceScene = sourceScene;
        }
    }

    private readonly List<Entry> entries = new List<Entry>();
    private readonly HashSet<string> initializedScenes = new HashSet<string>();
    private readonly HashSet<string> completedEntryIds = new HashSet<string>();
    private readonly Dictionary<string, List<string>> pendingDetailEvidence =
        new Dictionary<string, List<string>>();
    private readonly Dictionary<string, QuestJournalEventChange> pendingUpdateEvidence =
        new Dictionary<string, QuestJournalEventChange>();
    private readonly HashSet<string> pendingCompletionEvidence =
        new HashSet<string>();

    public static QuestJournalManager Instance { get; private set; }
    public IReadOnlyList<Entry> Entries => entries;
    public string TrackedEntryId { get; private set; } = string.Empty;
    public string InitialTrackedEntryId { get; private set; } = string.Empty;
    public event Action TrackedQuestChanged;
    public event Action JournalChanged;
    public event Action<QuestJournalChangeKind> JournalEntriesChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureCreatedBeforeSceneLoad()
    {
        GetOrCreate();
    }

    public static QuestJournalManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject managerObject = new GameObject("Quest Journal Manager");
        return managerObject.AddComponent<QuestJournalManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        LoadInitialEntriesForScene(scene);
        RefreshLevel2HeroProof(scene.name);
    }

    public bool AddOrUpdateEntry(
        string entryId,
        string title,
        string details,
        string sourceScene = "",
        string parentEntryId = null)
    {
        string normalizedId = NormalizeId(entryId);
        if (!CanTriggerEntry(normalizedId, parentEntryId)) return false;
        string normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? "未命名任务"
            : title.Trim();
        normalizedTitle = GetCanonicalQuestTitle(normalizedId, normalizedTitle);
        string normalizedDetails = details ?? string.Empty;
        string normalizedScene = sourceScene ?? string.Empty;

        if (pendingUpdateEvidence.TryGetValue(
                normalizedId,
                out QuestJournalEventChange pendingUpdate))
        {
            if (!string.IsNullOrWhiteSpace(pendingUpdate.title))
            {
                normalizedTitle = pendingUpdate.title.Trim();
            }
            if (!string.IsNullOrWhiteSpace(pendingUpdate.details))
            {
                normalizedDetails = pendingUpdate.details;
            }
            pendingUpdateEvidence.Remove(normalizedId);
        }
        normalizedTitle = GetCanonicalQuestTitle(normalizedId, normalizedTitle);
        normalizedDetails = MergePendingDetailEvidence(
            normalizedId,
            normalizedDetails);

        for (int index = 0; index < entries.Count; index++)
        {
            if (!string.Equals(entries[index].Id, normalizedId, StringComparison.Ordinal))
            {
                continue;
            }

            entries[index].Update(
                normalizedId,
                normalizedTitle,
                normalizedDetails,
                normalizedScene);
            JournalChanged?.Invoke();
            JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Updated);
            if (pendingCompletionEvidence.Remove(normalizedId))
            {
                CompleteEntry(normalizedId);
            }
            return false;
        }

        entries.Add(new Entry(
            normalizedId,
            normalizedTitle,
            normalizedDetails,
            normalizedScene));
        // A new child should be discoverable even if its parent was folded earlier.
        string parentId = GetParentEntryId(normalizedId);
        while (!string.IsNullOrEmpty(parentId))
        {
            collapsedEntries.Remove(parentId);
            parentId = GetParentEntryId(parentId);
        }
        // A newly received objective becomes the active tracked objective.
        TrackedEntryId = normalizedId;
        JournalChanged?.Invoke();
        JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Added);
        TrackedQuestChanged?.Invoke();
        if (pendingCompletionEvidence.Remove(normalizedId))
        {
            CompleteEntry(normalizedId);
        }
        if (normalizedId == Level2SandwormQuestId) RestoreSandwormSupportProgress();
        else if (normalizedId == SandwormBaitQuestId || normalizedId == SandwormMortarQuestId)
            RefreshSandwormSupportInventory();
        else if (normalizedId == CounterfeitQuestId) RefreshCounterfeitQuestInventory();
        else if (normalizedId == SummoningQuestId)
        {
            SummoningCircle.ReportLoadedQuestProgress();
            RefreshSummoningQuest();
        }
        return true;
    }

    public bool RemoveEntry(string entryId)
    {
        string normalizedId = NormalizeId(entryId);
        for (int index = 0; index < entries.Count; index++)
        {
            if (!string.Equals(entries[index].Id, normalizedId, StringComparison.Ordinal))
            {
                continue;
            }

            entries.RemoveAt(index);
            bool trackingChanged = string.Equals(TrackedEntryId, normalizedId, StringComparison.Ordinal) ||
                (!string.IsNullOrEmpty(TrackedEntryId) && !CanTriggerEntry(TrackedEntryId));
            if (trackingChanged)
            {
                TrackedEntryId = FindLatestIncompleteEntryId(normalizedId);
            }
            JournalChanged?.Invoke();
            if (trackingChanged) TrackedQuestChanged?.Invoke();
            JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Removed);
            return true;
        }

        return false;
    }

    public bool ContainsEntry(string entryId)
    {
        string normalizedId = NormalizeId(entryId);
        for (int index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, normalizedId, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool TryGetEntry(string entryId, out Entry result)
    {
        string normalizedId = NormalizeId(entryId);
        for (int index = 0; index < entries.Count; index++)
        {
            if (string.Equals(entries[index].Id, normalizedId, StringComparison.Ordinal))
            {
                result = entries[index];
                return true;
            }
        }

        result = null;
        return false;
    }

    public bool EntryDetailsContain(string entryId, string text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
            TryGetEntry(entryId, out Entry entry) &&
            !string.IsNullOrEmpty(entry.Details) &&
            entry.Details.Contains(text.Trim());
    }

    public bool TrackEntry(string entryId)
    {
        string normalizedId = NormalizeId(entryId);
        if (!ContainsEntry(normalizedId) ||
            !CanTriggerEntry(normalizedId) ||
            IsEntryCompleted(normalizedId) ||
            string.Equals(TrackedEntryId, normalizedId, StringComparison.Ordinal))
        {
            return false;
        }

        TrackedEntryId = normalizedId;
        JournalChanged?.Invoke();
        TrackedQuestChanged?.Invoke();
        return true;
    }

    /// <summary>Applies inspector-authored changes from documents, pickups or arbitrary scene events.</summary>
    public bool ApplyEventChanges(
        QuestJournalEventChange[] changes,
        string sourceScene = "")
    {
        if (changes == null || changes.Length == 0)
            return false;

        bool changed = false;
        for (int index = 0; index < changes.Length; index++)
        {
            QuestJournalEventChange change = changes[index];
            if (!CanTriggerEntry(change.entryId, change.parentEntryId)) continue;
            switch (change.operation)
            {
                case QuestJournalEventOperation.Remove:
                    changed |= RemoveEntry(change.entryId);
                    break;

                case QuestJournalEventOperation.UpdateExisting:
                    if (ContainsEntry(change.entryId))
                    {
                        AddOrUpdateEntry(
                            change.entryId,
                            change.title,
                            change.details,
                            sourceScene);
                        changed = true;
                    }
                    else
                    {
                        changed |= RecordPendingUpdate(change);
                    }
                    break;

                case QuestJournalEventOperation.AppendDetails:
                    changed |= ContainsEntry(change.entryId)
                        ? AppendEntryDetails(change.entryId, change.details)
                        : RecordPendingDetail(change.entryId, change.details);
                    break;

                case QuestJournalEventOperation.Complete:
                    changed |= RecordQuestCompletion(change.entryId);
                    break;

                default:
                    AddOrUpdateEntry(
                        change.entryId,
                        change.title,
                        change.details,
                        sourceScene);
                    changed = true;
                    break;
            }
        }

        return changed;
    }

    public bool IsEntryCompleted(string entryId)
    {
        return !string.IsNullOrWhiteSpace(entryId) &&
            completedEntryIds.Contains(entryId.Trim());
    }

    public bool CompleteEntry(string entryId)
    {
        string normalizedId = NormalizeId(entryId);
        if (!CanTriggerEntry(normalizedId) || !ContainsEntry(normalizedId) || !completedEntryIds.Add(normalizedId))
            return false;

        if (string.Equals(TrackedEntryId, normalizedId, StringComparison.Ordinal))
        {
            string parentId = GetParentEntryId(normalizedId);
            TrackedEntryId = ContainsEntry(parentId) && !IsEntryCompleted(parentId) ? parentId :
                ContainsEntry(InitialTrackedEntryId) &&
                             !IsEntryCompleted(InitialTrackedEntryId)
                ? InitialTrackedEntryId
                : FindLatestIncompleteEntryId(normalizedId);
        }

        JournalChanged?.Invoke();
        TrackedQuestChanged?.Invoke();
        JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Completed);
        return true;
    }

    public bool RecordQuestCompletion(string entryId)
    {
        return RecordQuestCompletion(entryId, false);
    }

    /// <summary>
    /// Explicit opt-in for actions that may precede even the parent quest.
    /// Evidence is saved without revealing/accepting the child; its normal
    /// trigger still has to pass CanTriggerEntry before consuming the evidence.
    /// </summary>
    public bool RecordQuestCompletion(string entryId, bool rememberBeforeParent)
    {
        if (string.IsNullOrWhiteSpace(entryId))
            return false;

        string normalizedId = entryId.Trim();
        if (!CanTriggerEntry(normalizedId))
            return rememberBeforeParent && pendingCompletionEvidence.Add(normalizedId);
        return ContainsEntry(normalizedId)
            ? CompleteEntry(normalizedId)
            : pendingCompletionEvidence.Add(normalizedId);
    }

    public void EvaluateKnownCompletionConditions()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        RecordControlledBehemoth(ZeldaRuntimeRegistry.GetControlledMover());

        if (scene.name == "Level1-Floor-1")
        {
            SpecificItemSubmissionStation[] stations =
                FindObjectsOfType<SpecificItemSubmissionStation>(true);
            for (int index = 0; index < stations.Length; index++)
            {
                SpecificItemSubmissionStation station = stations[index];
                if (station != null && station.gameObject.scene == scene &&
                    station.name == "WorkTable" && station.IsComplete)
                {
                    RecordQuestCompletion("level1.craft_super_bomb");
                    break;
                }
            }
        }

    }

    public void RecordControlledBehemoth(ZeldaFourWayMover mover)
    {
        if (mover == null || !mover.isActiveAndEnabled ||
            mover.GetComponent<BehemothZeldaCharacterData>() == null ||
            !SceneManager.GetActiveScene().name.StartsWith("Level1", StringComparison.OrdinalIgnoreCase)) return;

        // Possession alone completes the quest. Evidence is retained even if
        // the document introducing the quest has not been read yet.
        RecordQuestCompletion("level1.open_monster_cage");
    }

    private string FindLatestIncompleteEntryId(string excludedId = "")
    {
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            string id = entries[index].Id;
            if (!string.Equals(id, excludedId, StringComparison.Ordinal) &&
                CanTriggerEntry(id) &&
                !IsEntryCompleted(id))
            {
                return id;
            }
        }
        return string.Empty;
    }

    public bool AppendEntryDetails(string entryId, string detailsToAppend)
    {
        string normalizedId = NormalizeId(entryId);
        if (!CanTriggerEntry(normalizedId)) return false;
        string addition = string.IsNullOrWhiteSpace(detailsToAppend)
            ? string.Empty
            : detailsToAppend.Trim();
        if (string.IsNullOrEmpty(addition))
            return false;

        for (int index = 0; index < entries.Count; index++)
        {
            Entry entry = entries[index];
            if (!string.Equals(entry.Id, normalizedId, StringComparison.Ordinal))
                continue;

            string currentDetails = entry.Details ?? string.Empty;
            string[] existingLines = currentDetails.Split('\n');
            for (int lineIndex = 0; lineIndex < existingLines.Length; lineIndex++)
            {
                if (string.Equals(existingLines[lineIndex].Trim(), addition, StringComparison.Ordinal))
                    return false;
            }

            string updatedDetails = string.IsNullOrWhiteSpace(currentDetails)
                ? addition
                : currentDetails.TrimEnd() + "\n" + addition;
            entry.Update(entry.Id, entry.Title, updatedDetails, entry.SourceScene);
            JournalChanged?.Invoke();
            JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Updated);
            return true;
        }

        return false;
    }

    public void ResetAllEntries()
    {
        lastDialogueActors.Clear();
        level2HeroProofStage = 0;
        pendingLevel2TrialScene = null;
        entries.Clear();
        initializedScenes.Clear();
        completedEntryIds.Clear();
        pendingDetailEvidence.Clear();
        pendingUpdateEvidence.Clear();
        pendingCompletionEvidence.Clear();
        parentEntries.Clear(); collapsedEntries.Clear();
        sandwormMortarInstalled = false;
        summoningLitCrystals = 0; summoningPerformed = false;
        arenaQuestStage = ArenaQuestStage.Register; arenaQuestWins = 0; arenaQuestRounds = 0;
        TrackedEntryId = string.Empty;
        InitialTrackedEntryId = string.Empty;
        TrackedQuestChanged?.Invoke();
        JournalChanged?.Invoke();
        JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Reset);
    }

    private void LoadInitialEntriesForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded || !initializedScenes.Add(scene.name))
        {
            return;
        }

        QuestJournalSceneData[] configurations =
            FindObjectsOfType<QuestJournalSceneData>(true);
        bool changed = false;
        for (int configIndex = 0; configIndex < configurations.Length; configIndex++)
        {
            QuestJournalSceneData configuration = configurations[configIndex];
            if (configuration == null || configuration.gameObject.scene != scene)
            {
                continue;
            }

            changed |= configuration.LoadInitialEntriesWithoutNotification(this, scene.name);
        }

        if (changed)
        {
            if (string.IsNullOrWhiteSpace(InitialTrackedEntryId))
            {
                InitialTrackedEntryId = TrackedEntryId;
            }
            JournalChanged?.Invoke();
            JournalEntriesChanged?.Invoke(QuestJournalChangeKind.InitialSceneLoad);
        }
    }

    internal bool AddOrUpdateWithoutNotification(
        string entryId,
        string title,
        string details,
        string sourceScene,
        string parentEntryId = null)
    {
        string normalizedId = NormalizeId(entryId);
        if (!CanTriggerEntry(normalizedId, parentEntryId)) return false;
        string normalizedTitle = string.IsNullOrWhiteSpace(title)
            ? "未命名任务"
            : title.Trim();
        normalizedTitle = GetCanonicalQuestTitle(normalizedId, normalizedTitle);
        string effectiveDetails = details ?? string.Empty;
        if (pendingUpdateEvidence.TryGetValue(
                normalizedId,
                out QuestJournalEventChange pendingUpdate))
        {
            if (!string.IsNullOrWhiteSpace(pendingUpdate.title))
            {
                normalizedTitle = pendingUpdate.title.Trim();
            }
            if (!string.IsNullOrWhiteSpace(pendingUpdate.details))
            {
                effectiveDetails = pendingUpdate.details;
            }
            pendingUpdateEvidence.Remove(normalizedId);
        }
        normalizedTitle = GetCanonicalQuestTitle(normalizedId, normalizedTitle);

        for (int index = 0; index < entries.Count; index++)
        {
            if (!string.Equals(entries[index].Id, normalizedId, StringComparison.Ordinal))
            {
                continue;
            }

            string existingDetails = MergePendingDetailEvidence(
                normalizedId,
                effectiveDetails);
            entries[index].IsLevelObjective = true;
            entries[index].Update(
                normalizedId,
                normalizedTitle,
                existingDetails,
                sourceScene);
            if (pendingCompletionEvidence.Remove(normalizedId))
            {
                completedEntryIds.Add(normalizedId);
            }
            return true;
        }

        string mergedDetails = MergePendingDetailEvidence(
            normalizedId,
            effectiveDetails);
        entries.Add(new Entry(normalizedId, normalizedTitle, mergedDetails, sourceScene, true));
        if (pendingCompletionEvidence.Remove(normalizedId))
        {
            completedEntryIds.Add(normalizedId);
        }
        // Scene-authored entries are loaded in declaration order, leaving the
        // final newly-added entry tracked when the initial batch completes.
        TrackedEntryId = normalizedId;
        return true;
    }

    private static string NormalizeId(string entryId)
    {
        return string.IsNullOrWhiteSpace(entryId)
            ? "quest-" + Guid.NewGuid().ToString("N")
            : entryId.Trim();
    }

    private static string GetCanonicalQuestTitle(string entryId, string fallback)
    {
        switch (entryId)
        {
            case "level0.escape_prison":
                return Level0EscapeTitle;
            case "level0.obtain_gate_key":
                return Level0GateKeyTitle;
            case "level1.escape_castle":
                return Level1EscapeTitle;
            case "level1.craft_super_bomb":
                return Level1BombTitle;
            case "level1.open_monster_cage":
                return Level1MonsterTitle;
            case "level1.find_castle_gate_key":
                return Level1GateKeyTitle;
            default:
                return fallback;
        }
    }

    private static string UpgradeLegacyQuestDetails(string entryId, string details)
    {
        details = UpgradeAuthoredQuestDescriptions(entryId, details);
        string oldDescription, updatedDescription;
        switch (entryId)
        {
            case "level0.escape_prison":
                // The previous homage copy incorrectly implied the body had died.
                const string previousEscapeDescription = "高墙与铁门拦住了囚犯，却未必关得住一个失去肉身的灵魂。换一张面孔，借一双脚步，也许就能从守卫眼前走向外面的世界。\n\n死亡并没有带来自由，但或许能成为越狱的开始。\n\n当前目标：设法从大门逃出监狱。";
                oldDescription = details != null && details.StartsWith(previousEscapeDescription, StringComparison.Ordinal)
                    ? previousEscapeDescription : "设法从大门离开监狱";
                updatedDescription = Level0EscapeDescription;
                break;
            case "level0.obtain_gate_key":
                oldDescription = "大门钥匙目前位于典狱长的房间内，需要设法进入该房间以获得大门钥匙";
                updatedDescription = Level0GateKeyDescription;
                break;
            case "level1.escape_castle":
                oldDescription = "设法从大门逃出城堡";
                updatedDescription = Level1EscapeDescription;
                break;
            case "level1.craft_super_bomb":
                oldDescription = "信件中提到的超级炸弹似乎可以破坏城堡大门，需要找到[引线][超级炸弹外壳]与[魔法火药]后在信件旁边的工作台上合成超级炸弹";
                updatedDescription = Level1BombDescription;
                break;
            case "level1.open_monster_cage":
                oldDescription = "笼中被束缚的怪物似乎具备破坏城堡大门的能力，但是其被一对锁所束缚，需要找到[牢笼密码]与[怪物牢笼钥匙]打开其束缚才能设法去控制它";
                updatedDescription = Level1MonsterDescription;
                break;
            case "level1.find_castle_gate_key":
                oldDescription = "城堡大门被锁住了，需要找到对应的钥匙才能打开";
                updatedDescription = Level1GateKeyDescription;
                break;
            default: return details;
        }
        if (string.IsNullOrWhiteSpace(details)) return updatedDescription;
        // Upgrade only the known legacy copy. Preserve appended discoveries and
        // custom/stage-specific descriptions instead of replacing the whole log.
        if (!details.StartsWith(oldDescription, StringComparison.Ordinal)) return details;
        string suffix = details.Substring(oldDescription.Length);
        if (suffix.StartsWith("。", StringComparison.Ordinal)) suffix = suffix.Substring(1);
        return suffix.Length == 0 || suffix[0] == '\r' || suffix[0] == '\n'
            ? updatedDescription + suffix : details;
    }

    private bool RecordPendingDetail(string entryId, string details)
    {
        if (string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(details))
            return false;

        string normalizedId = entryId.Trim();
        string normalizedDetails = details.Trim();
        if (!pendingDetailEvidence.TryGetValue(normalizedId, out List<string> detailsList))
        {
            detailsList = new List<string>();
            pendingDetailEvidence.Add(normalizedId, detailsList);
        }
        if (detailsList.Contains(normalizedDetails))
            return false;

        detailsList.Add(normalizedDetails);
        return true;
    }

    private bool RecordPendingUpdate(QuestJournalEventChange change)
    {
        if (string.IsNullOrWhiteSpace(change.entryId))
            return false;

        pendingUpdateEvidence[change.entryId.Trim()] = change;
        return true;
    }

    private string MergePendingDetailEvidence(string entryId, string details)
    {
        if (!pendingDetailEvidence.TryGetValue(entryId, out List<string> additions))
            return details;

        string merged = details ?? string.Empty;
        for (int index = 0; index < additions.Count; index++)
        {
            string addition = additions[index];
            if (string.IsNullOrWhiteSpace(addition) || merged.Contains(addition))
                continue;

            merged = string.IsNullOrWhiteSpace(merged)
                ? addition
                : merged.TrimEnd() + "\n" + addition;
        }
        pendingDetailEvidence.Remove(entryId);
        return merged;
    }
}
