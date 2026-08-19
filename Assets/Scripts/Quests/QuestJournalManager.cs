using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[Serializable]
public struct QuestJournalEntryDefinition
{
    [Tooltip("Stable identifier used when updating or removing this entry.")]
    public string entryId;
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
    public string title;
    [TextArea(4, 12)] public string details;
}

/// <summary>
/// Stores the current quest journal independently from individual scenes.
/// Persistent transitions keep it, while fresh-session transitions clear it.
/// </summary>
[DefaultExecutionOrder(-490)]
public sealed class QuestJournalManager : MonoBehaviour
{
    public sealed class Entry
    {
        public string Id { get; private set; }
        public string Title { get; private set; }
        public string Details { get; private set; }
        public string SourceScene { get; private set; }

        internal Entry(string id, string title, string details, string sourceScene)
        {
            Update(id, title, details, sourceScene);
        }

        internal void Update(string id, string title, string details, string sourceScene)
        {
            Id = id;
            Title = title;
            Details = details;
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
    }

    public bool AddOrUpdateEntry(
        string entryId,
        string title,
        string details,
        string sourceScene = "")
    {
        string normalizedId = NormalizeId(entryId);
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
        // A newly received objective becomes the active tracked objective.
        TrackedEntryId = normalizedId;
        JournalChanged?.Invoke();
        JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Added);
        TrackedQuestChanged?.Invoke();
        if (pendingCompletionEvidence.Remove(normalizedId))
        {
            CompleteEntry(normalizedId);
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
            if (string.Equals(TrackedEntryId, normalizedId, StringComparison.Ordinal))
            {
                TrackedEntryId = entries.Count > 0
                    ? entries[entries.Count - 1].Id
                    : string.Empty;
            }
            JournalChanged?.Invoke();
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
        if (!ContainsEntry(normalizedId) || !completedEntryIds.Add(normalizedId))
            return false;

        if (string.Equals(TrackedEntryId, normalizedId, StringComparison.Ordinal))
        {
            TrackedEntryId = ContainsEntry(InitialTrackedEntryId) &&
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
        if (string.IsNullOrWhiteSpace(entryId))
            return false;

        string normalizedId = entryId.Trim();
        return ContainsEntry(normalizedId)
            ? CompleteEntry(normalizedId)
            : pendingCompletionEvidence.Add(normalizedId);
    }

    public void EvaluateKnownCompletionConditions()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
            return;

        if (scene.name == "Level1-Floor1")
        {
            DoorHingeInteraction[] doors = FindObjectsOfType<DoorHingeInteraction>(true);
            int cageDoorCount = 0;
            bool allUnlocked = true;
            for (int index = 0; index < doors.Length; index++)
            {
                DoorHingeInteraction door = doors[index];
                if (door == null || door.gameObject.scene != scene ||
                    !door.name.StartsWith("CageDoor", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                cageDoorCount++;
                allUnlocked &= !door.IsLocked;
            }

            if (cageDoorCount >= 2 && allUnlocked)
            {
                RecordQuestCompletion("level1.open_monster_cage");
            }
        }

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

    private string FindLatestIncompleteEntryId(string excludedId = "")
    {
        for (int index = entries.Count - 1; index >= 0; index--)
        {
            string id = entries[index].Id;
            if (!string.Equals(id, excludedId, StringComparison.Ordinal) &&
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
        entries.Clear();
        initializedScenes.Clear();
        completedEntryIds.Clear();
        pendingDetailEvidence.Clear();
        pendingUpdateEvidence.Clear();
        pendingCompletionEvidence.Clear();
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
        string sourceScene)
    {
        string normalizedId = NormalizeId(entryId);
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
        entries.Add(new Entry(normalizedId, normalizedTitle, mergedDetails, sourceScene));
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
                return "逃脱者";
            case "level0.obtain_gate_key":
                return "出行审查";
            case "level1.escape_castle":
                return "逃脱者（续集）";
            case "level1.craft_super_bomb":
                return "爆破手";
            case "level1.open_monster_cage":
                return "笼中困兽";
            case "level1.find_castle_gate_key":
                return "老套，但有用";
            default:
                return fallback;
        }
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
