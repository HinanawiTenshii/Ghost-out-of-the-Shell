using UnityEngine;

public sealed partial class QuestJournalManager
{
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

    // Milestones use the same saved journal evidence as the other item quests.
    // Do not poll the scene or search for individual coins while tracking this task.
    public void RecordCounterfeitGemAcquired()
    {
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
        if (!TryGetEntry(SummoningQuestId, out var entry) || IsEntryCompleted(SummoningQuestId)) return;
        string details = SummoningQuestIntroduction + (summoningPerformed
            ? "\n\n六枚水晶已全部点亮，域外来客已被召唤至法阵中央。"
            : "\n\n当前目标：使六名指定角色在法阵范围内死亡，点亮全部水晶并完成召唤。\n已点亮水晶：" + summoningLitCrystals + "/6。");
        if (entry.Details != details)
            AddOrUpdateEntry(SummoningQuestId, SummoningQuestTitle, details, entry.SourceScene);
        if (summoningPerformed) CompleteEntry(SummoningQuestId);
    }
}
