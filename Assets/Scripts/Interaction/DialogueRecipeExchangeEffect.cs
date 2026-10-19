using UnityEngine;

/// <summary>Unlimited, template-based purchase with one or more kinds of payment.</summary>
[AddComponentMenu("Interaction/Dialogue Recipe Exchange (对话无限兑换商品)")]
public sealed class DialogueRecipeExchangeEffect : DialogueOptionEffect
{
    [SerializeField, InspectorName("获得商品预制体"), Tooltip("无限供应，直接按此预制体创建物品栏数据，不消耗场景库存。")]
    private PickupItemBase merchandise;
    [SerializeField, InspectorName("消耗材料")] private InventoryItemCost[] costs;
    [SerializeField, InspectorName("解锁任务（可选）"), Tooltip("可选：仅在此任务完成后允许交易。")]
    private DialogueQuestProgression requiredQuest;
    private bool purchasing;
    public PickupItemBase Merchandise => merchandise;
    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || purchasing || merchandise == null)
        { reason = "商品暂时无法购买。"; return false; }
        if (requiredQuest != null && (!requiredQuest.CanTrigger || !requiredQuest.IsCompleted))
        { reason = "需要先完成委托。"; return false; }
        if (PersistentInventory.Instance == null) { reason = "物品栏不可用。"; return false; }
        return PersistentInventory.Instance.CanExchangeItems(costs, merchandise, out reason);
    }
    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        purchasing = true;
        try { return PersistentInventory.Instance.TryExchangeItems(costs, merchandise); }
        finally { purchasing = false; }
    }
}
