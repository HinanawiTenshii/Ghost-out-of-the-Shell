using UnityEngine;
using System.Collections.Generic;

public sealed partial class QuestJournalManager
{
    public const string KnightAmbushQuestId = "level2.knight_ambush";
    public const string KnightAmbushQuestTitle = "不必刺杀骑士团长";
    public const string KnightAmbushObjective = "制造一场足够规模的袭击";
    public const string KnightAmbushDescription = "骑士长由三名圣骑士护送。小队遭到严重袭击后，会护送骑士长撤往安全屋，并在入口驻守。可以利用加农炮或自动人偶制造袭击，再设法进入安全屋附身骑士长，利用他的权限接近试炼证明。骑士长必须存活。\n\n当前目标：制造一场足够规模的袭击。";
    public const string FortressProofQuestId = "level2.fortress_proof";
    public const string FortressProofQuestTitle = "失窃的证明";
    public const string FortressProofObjective = "进入骑士长房间并取得试炼证明";
    public const string FortressProofDescription = "要塞情报记载，要塞二楼守卫者骑士长的房间内存放着一份试炼证明。该区域需要极高权限，周围有严密守卫。进入房间后可直接取得证明。\n\n当前目标：进入守卫者骑士长的房间，取得试炼证明。";

    public const string CounterfeitQuestId = "level2_counterfeit_trial_proof";
    public const string CounterfeitQuestTitle = "假作真时";
    public const string CounterfeitMerchantTarget = "level2.counterfeit.gem_merchant";
    public const string CounterfeitCraftsmanTarget = "level2.counterfeit.craftsman";
    public const string CounterfeitPriceKnown = "金色宝石售价为30金币";
    public const string CounterfeitGemAcquired = "已取得金色宝石";
    public const string SummoningQuestId = "level2.summoning";
    public const string SummoningQuestTitle = "六把钥匙";
    public const string SummoningCircleTarget = "level2.summoning.circle";
    public const string SummoningQuestIntroduction = "召唤仪式需要六名指定角色的灵魂：大主教、穿金色服装的守卫士官、大厨、大工程师、诡异商人和隐居剑客。\n\n这些角色必须在法阵的矩形范围内死亡，才能由水晶吸收灵魂。六枚水晶全部点亮后，法阵中央会召唤域外来客。";
    private int summoningLitCrystals;
    private bool summoningPerformed;
    public int SummoningLitCrystals => summoningLitCrystals;
    private readonly HashSet<string> summoningTargetIds = new HashSet<string>();
    private readonly HashSet<string> summoningConsumedIds = new HashSet<string>();
    private bool summoningTargetsConfigured;

    public void RecordSummoningMarkerBindings(IReadOnlyList<CharacterPersistentIdentity> identities, IEnumerable<string> consumed)
    {
        summoningTargetsConfigured = true;
        summoningTargetIds.Clear(); summoningConsumedIds.Clear();
        if (identities != null) foreach (var identity in identities)
            if (identity != null) summoningTargetIds.Add(identity.StableId);
        if (consumed != null) foreach (string id in consumed) summoningConsumedIds.Add(id);
    }

    private void RestoreSummoningMarkerState(List<string> targets, List<string> consumed)
    {
        summoningTargetsConfigured = targets != null && targets.Count > 0;
        summoningTargetIds.Clear(); summoningConsumedIds.Clear();
        if (targets != null) summoningTargetIds.UnionWith(targets);
        if (consumed != null) summoningConsumedIds.UnionWith(consumed);
    }

    public bool IsTrackedSummoningTarget(string identity)
    {
        if (TrackedEntryId != SummoningQuestId || !ContainsEntry(SummoningQuestId) ||
            IsEntryFinished(SummoningQuestId) || string.IsNullOrEmpty(identity)) return false;
        EnsureSummoningTargetsConfigured();
        return summoningTargetIds.Contains(identity) && !summoningConsumedIds.Contains(identity);
    }

    private void EnsureSummoningTargetsConfigured()
    {
        if (!summoningTargetsConfigured)
        {
            // Legacy saves may resume on a floor without the ritual circle.
            // Resolve the original six identity assets once; live circle bindings
            // replace these defaults and are carried by subsequent saves.
            summoningTargetsConfigured = true;
            foreach (string name in new[] { "GrandPriest", "GoldenSoldier", "GreatChief", "MatserEngineer", "StrangeMerchant", "TheSwordsMan" })
            {
                var asset = Resources.Load<CharacterPersistentIdentity>("ItemIdentity/" + name);
                if (asset != null) summoningTargetIds.Add(asset.StableId);
            }
        }
    }

    // Milestones use the same saved journal evidence as the other item quests.
    // Do not poll the scene or search for individual coins while tracking this task.
    public void RecordCounterfeitGemAcquired()
    {
        counterfeitGemAcquired = true;
        if (!IsEntryCompleted(CounterfeitQuestId))
            ApplyEventChanges(new[] { new QuestJournalEventChange {
                entryId = CounterfeitQuestId, operation = QuestJournalEventOperation.AppendDetails,
                details = CounterfeitGemAcquired + "，返回赝品工匠处交付，制作赝品试炼证明。"
            } });
    }

    private void RefreshCounterfeitQuestInventory()
    {
        if (ContainsEntry(CounterfeitQuestId) && !IsEntryCompleted(CounterfeitQuestId) &&
            !EntryDetailsContain(CounterfeitQuestId, CounterfeitGemAcquired) &&
            PersistentInventory.Instance != null && PersistentInventory.Instance.Contains(GoldenGemPickupItem.DefaultItemId))
            RecordCounterfeitGemAcquired();
    }

    public void RecordSummoningQuestProgress(int litCrystals, bool performed)
    {
        int count = Mathf.Max(summoningLitCrystals, Mathf.Clamp(litCrystals, 0, SummoningCircle.CrystalCount));
        bool complete = summoningPerformed || performed;
        if (count == summoningLitCrystals && complete == summoningPerformed) return;
        summoningLitCrystals = count;
        summoningPerformed = complete;
        RefreshSummoningQuest();
    }

    private void RefreshSummoningQuest()
    {
        // Physical progress before reading the notice is retained, but never starts the task.
        if (!TryGetEntry(SummoningQuestId, out var entry) || IsEntryFinished(SummoningQuestId)) return;
        string details = SummoningQuestIntroduction + (summoningPerformed
            ? "\n\n六枚水晶已全部点亮，域外来客已被召唤至法阵中央。"
            : "\n\n当前目标：使六名指定角色在法阵范围内死亡，点亮全部水晶并完成召唤。\n已点亮水晶：" + summoningLitCrystals + "/6。");
        if (entry.Details != details)
            AddOrUpdateEntry(SummoningQuestId, SummoningQuestTitle, details, entry.SourceScene);
        if (summoningPerformed) CompleteEntry(SummoningQuestId);
    }
}
