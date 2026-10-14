using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct InventoryItemCost
{
    [InspectorName("物品预制体")] public PickupItemBase item;
    [Min(1), InspectorName("数量")] public int quantity;
}

public sealed partial class PersistentInventory
{
    /// <summary>On-demand, unlimited recipe/quest transaction. A null output is a hand-in.</summary>
    public bool CanExchangeItems(InventoryItemCost[] costs, PickupItemBase output, out string reason)
        => PlanRecipeExchange(costs, output, out _, out _, out reason);

    private bool PlanRecipeExchange(InventoryItemCost[] costs, PickupItemBase output,
        out int[] remaining, out int destination, out string reason)
    {
        remaining = null; destination = -1; reason = null;
        if (itemExchangeInProgress || potionUseInProgress)
        { reason = "正在处理物品，请稍后再试。"; return false; }
        if (costs == null || costs.Length == 0)
        { reason = "交易材料未配置。"; return false; }
        var totals = new Dictionary<string, int>();
        foreach (var cost in costs)
        {
            if (cost.item == null || string.IsNullOrEmpty(cost.item.ItemId) || cost.quantity < 1)
            { reason = "交易材料未正确配置。"; return false; }
            totals.TryGetValue(cost.item.ItemId, out int old);
            if (old > int.MaxValue - cost.quantity) { reason = "材料数量无效。"; return false; }
            totals[cost.item.ItemId] = old + cost.quantity;
        }
        foreach (var cost in costs)
            if (!Contains(cost.item.ItemId, totals[cost.item.ItemId]))
            { reason = cost.item.ItemName + "不足。"; return false; }
        if (output != null && string.IsNullOrEmpty(output.ItemId))
        { reason = "商品未正确配置。"; return false; }

        remaining = new int[slots.Length];
        for (int i = 0; i < slots.Length; i++) remaining[i] = slots[i].IsEmpty ? 0 : slots[i].Quantity;
        foreach (var cost in totals)
        {
            int needed = cost.Value;
            for (int i = slots.Length - 1; i >= 0 && needed > 0; i--)
            {
                if (slots[i].ItemId != cost.Key) continue;
                int take = Mathf.Min(needed, remaining[i]); remaining[i] -= take; needed -= take;
            }
        }
        if (output == null) return true;
        // Capacity after all materials have been paid, including slots freed by payment.
        for (int i = 0; i < slots.Length; i++)
            if (remaining[i] > 0 && CanMergeShopItem(slots[i], output, remaining[i]))
            { destination = i; return true; }
        for (int i = 0; i < slots.Length; i++)
            if (remaining[i] == 0) { destination = i; return true; }
        reason = "物品栏空间不足。"; return false;
    }

    public bool TryExchangeItems(InventoryItemCost[] costs, PickupItemBase output, Action onCommitted = null)
    {
        if (!PlanRecipeExchange(costs, output, out var remaining, out int destination, out _)) return false;
        // Read authored template data before touching any inventory slot. No world-stock object is spawned/removed.
        string id = output != null ? output.ItemId : null;
        string itemName = output != null ? output.ItemName : null;
        string description = output != null ? output.ItemDescription : null;
        string state = output != null ? output.InventoryState : null;
        bool hasColor = output != null && output.ItemVisual != null;
        Color color = hasColor ? output.ItemVisual.DisplayColor : Color.white;
        bool hasCharge = output != null && output.HasInventoryCharge;
        float charge = hasCharge ? output.InventoryCharge : 0;
        string identity = output == null || IsFungibleShopItem(output) ? string.Empty : Guid.NewGuid().ToString("N");
        itemExchangeInProgress = true;
        try
        {
            for (int i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Quantity == remaining[i]) continue;
                if (remaining[i] == 0) slot.Clear();
                else slot.Set(slot.ItemId, remaining[i], slot.ItemInstanceId, slot.ItemName, slot.ItemDescription,
                    slot.HasVisualColor, slot.VisualColor, slot.HasStoredCharge, slot.StoredCharge, slot.ItemState);
            }
            if (output != null)
            {
                var slot = slots[destination];
                if (slot.IsEmpty) slot.Set(id, 1, identity, itemName, description, hasColor, color, hasCharge, charge, state);
                else slot.Set(slot.ItemId, slot.Quantity + 1, slot.ItemInstanceId, slot.ItemName, slot.ItemDescription,
                    slot.HasVisualColor, slot.VisualColor, slot.HasStoredCharge, slot.StoredCharge, slot.ItemState);
            }
            onCommitted?.Invoke();
            NotifyChanged();
            if (destination >= 0) SelectSlot(destination);
            return true;
        }
        finally { itemExchangeInProgress = false; }
    }
}
