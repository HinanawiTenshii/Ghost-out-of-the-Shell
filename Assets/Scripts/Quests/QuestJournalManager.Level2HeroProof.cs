using UnityEngine;
using UnityEngine.SceneManagement;

public sealed partial class QuestJournalManager
{
    public const string Level2HeroProofId = "level2.hero_proof";
    public const string Level2CityTargetId = "level2.hero_proof.city";
    public const string Level2TrialTargetId = "level2.hero_proof.trial";
    private const string HeroProofTitle = "英雄之证";
    private const string HeroProofIntroduction = "勇者已前往边境城市的试炼之地提升能力。需要先抵达边境城市，再进入试炼之地寻找勇者。";
    private int level2HeroProofStage;
    private string pendingLevel2TrialScene;

    /// <summary>0: not started; 1: reach city; 2: enter trials; 3: completed.</summary>
    public int Level2HeroProofStage => level2HeroProofStage;

    // Maps cache both authored destinations independently of the current save.
    // Resolving an ID each display update is allocation-free and never scans a scene.
    public static string ResolveQuestMapTargetId(string questId)
    {
        if (questId == ArenaInjectionQuestId)
        {
            var journal = Instance;
            return journal != null && journal.ContainsEntry(questId) && journal.CanTriggerEntry(questId) &&
                !journal.IsEntryCompleted(questId) ? questId : null;
        }
        if (questId == ArenaQuestId)
        {
            var journal = Instance;
            if (journal == null || !journal.ContainsEntry(questId) || journal.IsEntryCompleted(questId)) return null;
            if (journal.ArenaStage == ArenaQuestStage.Preparing) return ArenaEntranceTarget;
            if (journal.ArenaStage == ArenaQuestStage.Fighting || journal.ArenaStage == ArenaQuestStage.RoundWon) return ArenaCombatTarget;
            return ArenaRegistrarTarget;
        }
        if (questId == CounterfeitQuestId || questId == SummoningQuestId)
        {
            var journal = Instance;
            if (journal == null || !journal.ContainsEntry(questId) || journal.IsEntryCompleted(questId)) return null;
            if (questId == SummoningQuestId) return SummoningCircleTarget;
            return journal.EntryDetailsContain(questId, CounterfeitGemAcquired) ? CounterfeitCraftsmanTarget : CounterfeitMerchantTarget;
        }
        if (questId == SandwormBaitQuestId || questId == SandwormMortarQuestId)
        {
            var journal = Instance;
            if (journal == null || !journal.ContainsEntry(questId) || !journal.CanTriggerEntry(questId) || journal.IsEntryCompleted(questId)) return null;
            if (questId == SandwormBaitQuestId)
                return journal.EntryDetailsContain(questId, SandwormToolsFound) ? SandwormMerchantTarget : SandwormToolsTarget;
            return journal.EntryDetailsContain(questId, SandwormDeviceFound) ? SandwormMortarTarget : SandwormDeviceTarget;
        }
        if (questId != Level2HeroProofId) return questId;
        switch (Instance != null ? Instance.Level2HeroProofStage : 0)
        {
            case 1: return Level2CityTargetId;
            case 2: return Level2TrialTargetId;
            default: return null;
        }
    }

    private static int GetLevel2SceneStage(string sceneName)
    {
        switch (sceneName)
        {
            case "Level2-Desert": return 1;
            case "Level2-Floor1":
            case "Level2-Floor2":
            case "Level2-Floor-1": return 2;
            default: return 0;
        }
    }

    private void RestoreLevel2HeroProofStage()
    {
        // Old saves do not contain the stage field. Entering a city floor already
        // proves arrival; revisiting the desert must not erase that evidence.
        if (IsEntryCompleted(Level2HeroProofId)) level2HeroProofStage = 3;
        if (level2HeroProofStage == 0)
            foreach (string scene in initializedScenes)
                level2HeroProofStage = Mathf.Max(level2HeroProofStage, GetLevel2SceneStage(scene));
        RefreshLevel2HeroProof(SceneManager.GetActiveScene().name, false);
    }

    private void RefreshLevel2HeroProof(string sceneName, bool notify = true)
    {
        int desired = GetLevel2SceneStage(sceneName);
        if (!string.IsNullOrEmpty(pendingLevel2TrialScene) && sceneName == pendingLevel2TrialScene)
        {
            desired = 3;
            pendingLevel2TrialScene = null;
        }
        int stage = Mathf.Max(level2HeroProofStage, desired);
        if (stage == 0) return;
        string details = HeroProofIntroduction + "\n\n" + (stage == 1
            ? "当前目标：穿过沙漠，抵达边境城市。"
            : stage == 2
                ? "已抵达边境城市，需要找到开启试炼大门的方法。\n当前目标：打开试炼大门，通过场景入口进入试炼之地。"
                : "已打开试炼大门并进入试炼之地。");
        bool added = !TryGetEntry(Level2HeroProofId, out Entry entry);
        bool changed = added || stage != level2HeroProofStage || !entry.IsLevelObjective || entry.Details != details;
        level2HeroProofStage = stage;
        bool trackingChanged = false;
        if (added)
        {
            entry = new Entry(Level2HeroProofId, HeroProofTitle, details, "Level2-Desert", true);
            entries.Add(entry);
            // A restored completed quest must not steal tracking from the next level's main quest.
            if (stage < 3 && (string.IsNullOrWhiteSpace(TrackedEntryId) ||
                !ContainsEntry(TrackedEntryId) || IsEntryCompleted(TrackedEntryId)))
            {
                TrackedEntryId = Level2HeroProofId;
                trackingChanged = true;
            }
        }
        else
        {
            entry.IsLevelObjective = true;
            entry.Update(Level2HeroProofId, HeroProofTitle, details, "Level2-Desert");
        }
        if (stage < 3 && string.IsNullOrWhiteSpace(InitialTrackedEntryId)) InitialTrackedEntryId = Level2HeroProofId;
        if (stage == 3)
        {
            changed |= completedEntryIds.Add(Level2HeroProofId);
            if (TrackedEntryId == Level2HeroProofId)
            {
                TrackedEntryId = FindLatestIncompleteEntryId(Level2HeroProofId);
                trackingChanged = true;
            }
        }
        if (!changed || !notify) return;
        JournalChanged?.Invoke();
        if (trackingChanged) TrackedQuestChanged?.Invoke();
        JournalEntriesChanged?.Invoke(stage == 3 ? QuestJournalChangeKind.Completed :
            added ? QuestJournalChangeKind.InitialSceneLoad : QuestJournalChangeKind.Updated);
    }

    // The ordinary SceneTransitionPoint clears the previous level's state. Queue
    // this after that reset; only successful arrival completes this main quest.
    internal void ExpectLevel2TrialArrival(string sceneName) => pendingLevel2TrialScene = sceneName;
    internal void CancelLevel2TrialArrival() => pendingLevel2TrialScene = null;
}
