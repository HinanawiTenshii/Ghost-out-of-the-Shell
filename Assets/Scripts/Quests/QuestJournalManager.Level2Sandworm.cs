public sealed partial class QuestJournalManager
{
    // Started by the Floor1 notice; completed only by the proof recovered from
    // a dead sandworm. Existing journal evidence also covers obtaining it first.
    public const string Level2SandwormQuestId = "level2.sandworm";
    public const string Level2SandwormQuestTitle = "香料与獠牙";
    public const string Level2SandwormTargetName = "SandwormQuestTarget";
    public const string SandwormBaitQuestId = "level2_strange_merchant_synthesis_tools";
    public const string SandwormBaitTitle = "它不喜欢烟雾";
    public const string SandwormMortarQuestId = "level2.sandworm.mortar";
    public const string SandwormMortarAgreementId = "level2.master_engineer.mortar.accepted_actor";
    public const string SandwormToolsFound = "已取得合成工具";
    public const string SandwormDeviceFound = "已取得远程控制装置";
    public const string SandwormToolsTarget = "level2.sandworm.tools";
    public const string SandwormMerchantTarget = "level2.sandworm.merchant";
    public const string SandwormDeviceTarget = "level2.sandworm.device";
    public const string SandwormMortarTarget = "level2.sandworm.installation";
    public const string SandwormMortarTitle = "→→↑";
    public const string SandwormMortarDetails = "大工程师的远程控制装置可安装在城墙臼炮上，用于从沙漠呼叫炮击。炮击可将沙虫从地下炸出。\n\n当前目标：从工程师旁的保险柜取得远程控制装置（密码：1551），将其安装在城墙西南角城楼的臼炮上。";
    private bool sandwormMortarInstalled;

    public void RecordSandwormMortarInstallation()
    {
        // A physical installation fact is not an early child-quest trigger.
        sandwormMortarInstalled = true;
        FinishSandwormMortarIfAccepted();
    }

    private void FinishSandwormMortarIfAccepted()
    {
        if (!sandwormMortarInstalled || !ContainsEntry(SandwormMortarQuestId) ||
            !CanTriggerEntry(SandwormMortarQuestId) || IsEntryCompleted(SandwormMortarQuestId)) return;
        AddOrUpdateEntry(SandwormMortarQuestId, SandwormMortarTitle,
            SandwormMortarCompletedDetails, "Level2-Floor-1");
        CompleteEntry(SandwormMortarQuestId);
    }

    public void RefreshSandwormSupportInventory()
    {
        var inventory = PersistentInventory.Instance;
        if (inventory != null)
        {
            if (ContainsEntry(SandwormBaitQuestId) && CanTriggerEntry(SandwormBaitQuestId) &&
                !IsEntryCompleted(SandwormBaitQuestId) && inventory.GetQuantity(SynthesisToolsPickupItem.DefaultItemId) > 0)
                AppendEntryDetails(SandwormBaitQuestId, SandwormToolsFound + "，将其交给城中的诡异商人。");
            if (ContainsEntry(SandwormMortarQuestId) && CanTriggerEntry(SandwormMortarQuestId) &&
                !IsEntryCompleted(SandwormMortarQuestId) && inventory.GetQuantity(MortarControlDevicePickupItem.DefaultItemId) > 0)
                AppendEntryDetails(SandwormMortarQuestId, SandwormDeviceFound + "，前往城墙西南角城楼的臼炮安装。");
            if (inventory.GetQuantity(MortarRemotePickupItem.DefaultItemId) > 0) sandwormMortarInstalled = true;
        }
        FinishSandwormMortarIfAccepted();
    }

    private void RestoreSandwormSupportProgress()
    {
        // Older save versions have only the scene object's installation flag.
        foreach (var station in UnityEngine.Object.FindObjectsOfType<FortressMortarRemoteInstallation>(true))
            if (station.IsInstalled) { sandwormMortarInstalled = true; break; }
        // Earlier saves contain the engineer's agreement but no visible quest.
        if (ContainsEntry(Level2SandwormQuestId) && HasRememberedDialogueActor(SandwormMortarAgreementId) &&
            !ContainsEntry(SandwormMortarQuestId))
            AddOrUpdateEntry(SandwormMortarQuestId, SandwormMortarTitle, SandwormMortarDetails, "Level2-Floor-1");
        RefreshSandwormSupportInventory();
    }
}
