using UnityEngine;

public sealed partial class PersistentInventory
{
    /// <summary>Tests capacity AFTER paying, including a coin slot emptied by the purchase.</summary>
    public bool CanPurchaseItem(PickupItemBase item, int coinCost, out string reason)
    {
        return PlanShopPurchase(item, coinCost, out _, out reason);
    }

    private bool PlanShopPurchase(PickupItemBase item, int coinCost, out int destination, out string reason)
    {
        destination = -1;
        reason = null;
        if (itemExchangeInProgress || potionUseInProgress)
        { reason = "正在处理物品，请稍后再试。"; return false; }
        if (item == null || !item.IsAvailableForShop || string.IsNullOrEmpty(item.ItemId))
        { reason = "商品暂时无法购买。"; return false; }
        if (coinCost < 1) { reason = "商品价格未正确配置。"; return false; }
        if (!Contains(GoldCoinPickupItem.DefaultItemId, coinCost))
        { reason = "金币不足。"; return false; }

        if (!IsFungibleShopItem(item) && !string.IsNullOrEmpty(item.ConfiguredUniqueInstanceId) &&
            ContainsItemInstance(item.ConfiguredUniqueInstanceId))
        { reason = "已持有此物品。"; return false; }

        // Match existing pickups: hand cannons 3, crystals 10, supplies 3,
        // coins 20, bombs as authored; unique/charged items keep their identity.
        for (int i = 0; i < slots.Length; i++)
            if (CanMergeShopItem(slots[i], item))
            { destination = i; return true; }
        for (int i = 0; i < slots.Length; i++)
            if (slots[i].IsEmpty) { destination = i; return true; }

        int remaining = coinCost;
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = slots[i];
            if (slot.IsEmpty || slot.ItemId != GoldCoinPickupItem.DefaultItemId) continue;
            if (slot.Quantity <= remaining) destination = i;
            remaining -= Mathf.Min(remaining, slot.Quantity);
        }
        if (destination >= 0) return true;
        reason = "物品栏空间不足。";
        return false;
    }

    /// <summary>
    /// Commit payment, one purchased item, and its world-stock removal before
    /// notifying observers. Failed checks leave both coins and inventory untouched.
    /// </summary>
    public bool TryPurchaseItem(PickupItemBase item, int coinCost)
    {
        if (!PlanShopPurchase(item, coinCost, out int destination, out _)) return false;
        // Capture the purchased instance, not just its prefab: batteries, bottles,
        // keys and future items may carry scene-specific state.
        bool hasColor = item.ItemVisual != null;
        Color color = hasColor ? item.ItemVisual.DisplayColor : Color.white;
        string state = item.InventoryState;
        string identity = IsFungibleShopItem(item) ? string.Empty : item.UniqueInstanceId;
        string itemName = item.ItemName, description = item.ItemDescription;
        itemExchangeInProgress = true;
        try
        {
            int remaining = coinCost;
            for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
            {
                var slot = slots[i];
                if (slot.IsEmpty || slot.ItemId != GoldCoinPickupItem.DefaultItemId) continue;
                int removed = Mathf.Min(remaining, slot.Quantity);
                remaining -= removed;
                if (removed == slot.Quantity) slot.Clear();
                else slot.Set(slot.ItemId, slot.Quantity - removed, string.Empty, slot.ItemName, slot.ItemDescription,
                    slot.HasVisualColor, slot.VisualColor, slot.HasStoredCharge, slot.StoredCharge, slot.ItemState);
            }
            var output = slots[destination];
            if (output.IsEmpty) output.Set(item.ItemId, 1, identity, itemName, description,
                hasColor, color, item.HasInventoryCharge, item.InventoryCharge, state);
            else output.Set(output.ItemId, output.Quantity + 1, string.Empty, output.ItemName, output.ItemDescription,
                output.HasVisualColor, output.VisualColor, output.HasStoredCharge, output.StoredCharge, output.ItemState);
            item.CompleteShopPurchase(this);
            NotifyChanged();
            SelectSlot(destination);
            return true;
        }
        finally { itemExchangeInProgress = false; }
    }

    private static bool IsFungibleShopItem(PickupItemBase item)
    {
        return item is BombPickupItem || item is CardboardBoxPickupItem ||
            item.ItemId == DisposableHandgunPickupItem.DefaultItemId || item.ItemId == "stability_crystal" ||
            item.ItemId == GoldCoinPickupItem.DefaultItemId || AlchemyRecipeCatalog.IsStackableSupply(item.ItemId);
    }

    private static bool CanMergeShopItem(Slot slot, PickupItemBase item, int remainingQuantity = -1)
    {
        if (slot.IsEmpty || slot.ItemId != item.ItemId || !IsFungibleShopItem(item)) return false;
        int quantity = remainingQuantity >= 0 ? remainingQuantity : slot.Quantity;
        int limit = Mathf.Max(1, item.MaxStackSize);
        if (item.ItemId == DisposableHandgunPickupItem.DefaultItemId) return quantity < 3;
        if (item.ItemId == "stability_crystal") return quantity < 10;
        if (item.ItemId == GoldCoinPickupItem.DefaultItemId) return quantity < GoldCoinPickupItem.StackLimit;
        bool supply = AlchemyRecipeCatalog.IsStackableSupply(item.ItemId);
        if (supply) limit = 3;
        if (quantity >= limit || (!supply && !string.IsNullOrEmpty(slot.ItemInstanceId))) return false;
        // Normal bomb pickups use TryAddItem and do not store visual tint. A
        // purchased bomb must still join that same stack, including in a full bag.
        if (item is BombPickupItem) return true;
        return (supply ? SameAlchemyState(item.ItemId, slot.ItemState, item.InventoryState) : slot.ItemState == item.InventoryState) &&
            slot.HasStoredCharge == item.HasInventoryCharge &&
            (!slot.HasStoredCharge || Mathf.Approximately(slot.StoredCharge, item.InventoryCharge)) &&
            (slot.HasVisualColor ? slot.VisualColor : Color.white) == (item.ItemVisual != null ? item.ItemVisual.DisplayColor : Color.white);
    }
}
