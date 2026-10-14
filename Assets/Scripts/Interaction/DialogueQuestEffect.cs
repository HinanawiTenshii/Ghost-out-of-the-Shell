using UnityEngine;

[AddComponentMenu("Interaction/Dialogue Quest Action (对话接取或交付任务)")]
public sealed class DialogueQuestEffect : DialogueOptionEffect
{
    public enum Operation { Accept, Deliver }
    [SerializeField, InspectorName("对应任务")] private DialogueQuestProgression progression;
    [SerializeField, InspectorName("功能（Accept 接取 / Deliver 交付）")] private Operation operation;
    [SerializeField, InspectorName("交付物品")] private InventoryItemCost[] handInCosts;
    [SerializeField, InspectorName("交付奖励（可选）"), Tooltip("交付材料与获得一件奖励会一次性结算；没有足够空间时不会扣除材料。")]
    private PickupItemBase handInReward;
    [SerializeField, InspectorName("交付前必须接取任务")] private bool requireAcceptedBeforeDelivery;
    [SerializeField, InspectorName("缺少物品时隐藏"), Tooltip("缺少提交物品时完全隐藏选项；不启用时显示不可选择的灰色选项。")]
    private bool hideWithoutItems;
    private bool executing;
    public override bool IsVisible(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (progression != null && !progression.CanTrigger) return false;
        if (!hideWithoutItems || operation != Operation.Deliver) return true;
        return PersistentInventory.Instance != null && PersistentInventory.Instance.CanExchangeItems(handInCosts, null, out _);
    }
    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || executing || progression == null || !progression.IsConfigured)
        { reason = "任务暂时不可用。"; return false; }
        if (progression.IsCompleted) { reason = "已经交付过了。"; return false; }
        if (!progression.CanTrigger) { reason = "需要先接取父任务。"; return false; }
        if (operation == Operation.Accept)
        {
            if (progression.IsAccepted) { reason = "已接下这个委托。"; return false; }
            return true;
        }
        if (requireAcceptedBeforeDelivery && !progression.IsAccepted)
        { reason = "需要先接下这个委托。"; return false; }
        if (PersistentInventory.Instance == null) { reason = "物品栏不可用。"; return false; }
        return PersistentInventory.Instance.CanExchangeItems(handInCosts, handInReward, out reason);
    }
    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        executing = true;
        try
        {
            return operation == Operation.Accept ? progression.AcceptQuest() :
                PersistentInventory.Instance.TryExchangeItems(handInCosts, handInReward, progression.CompleteQuest);
        }
        finally { executing = false; }
    }
}
