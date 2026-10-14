using System.Collections.Generic;
using UnityEngine;

/// <summary>A scene-stock purchase, queried only on dialogue presentation or selection.</summary>
[AddComponentMenu("Interaction/Dialogue Shop Purchase (对话购买商品)")]
public sealed class DialogueShopPurchaseEffect : DialogueOptionEffect
{
    [SerializeField, InspectorName("商品预制体"), Tooltip("按物品 ID 匹配场景库存，并从预制体读取商品名称。每个购买选项绑定一个独立组件。")]
    private PickupItemBase merchandise;
    [SerializeField, InspectorName("场景库存分组"), Tooltip("仅出售此分组下的对应物品；被距离加载暂时卸载的库存仍计入。")]
    private Transform stockRoot;
    [SerializeField, Min(1), InspectorName("单件金币价格")]
    private int coinCost = 3;

    private readonly List<PickupItemBase> stock = new List<PickupItemBase>();
    private bool purchasing;

    public int ReadStockCount() { return ReadStock(out _); }

    public override string GetChoiceText(CharacterDialogue speaker, ZeldaFourWayMover player, string authoredText)
    {
        if (merchandise == null) return authoredText;
        return "购买" + merchandise.ItemName + "X1（消耗" + coinCost + "金币，剩余" + ReadStockCount() + "）";
    }

    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string unavailableReason)
    {
        if (!isActiveAndEnabled || purchasing || merchandise == null || stockRoot == null || stockRoot.gameObject.scene != gameObject.scene)
        { unavailableReason = "商品暂时无法购买。"; return false; }
        if (ReadStock(out var item) == 0) { unavailableReason = "商品已售罄。"; return false; }
        var inventory = PersistentInventory.Instance;
        if (inventory == null) { unavailableReason = "物品栏不可用。"; return false; }
        return inventory.CanPurchaseItem(item, coinCost, out unavailableReason);
    }

    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        ReadStock(out var item);
        if (item == null) return false;
        purchasing = true;
        try
        {
            return PersistentInventory.Instance.TryPurchaseItem(item, coinCost);
        }
        finally { purchasing = false; }
    }

    private int ReadStock(out PickupItemBase first)
    {
        first = null;
        if (merchandise == null || stockRoot == null || stockRoot.gameObject.scene != gameObject.scene) return 0;
        stockRoot.GetComponentsInChildren(true, stock);
        CameraVisionObjectStreaming[] streamers = null;
        int count = 0;
        foreach (var item in stock)
        {
            if (item == null || !item.enabled || !item.IsAvailableForShop || item.ItemId != merchandise.ItemId) continue;
            if (!item.gameObject.activeInHierarchy)
            {
                // Resolve only on demand and only when inactive stock is present.
                if (streamers == null) streamers = FindObjectsOfType<CameraVisionObjectStreaming>();
                bool available = true;
                for (Transform current = item.transform; current != null; current = current.parent)
                {
                    if (current.gameObject.activeSelf) continue;
                    bool streamedOut = false;
                    foreach (var streamer in streamers)
                        if (streamer.IsSuspendedByStreaming(current.gameObject)) { streamedOut = true; break; }
                    // Deliberately hidden stock (including restored collected items) is NOT for sale.
                    if (!streamedOut) { available = false; break; }
                }
                if (!available) continue;
            }
            if (first == null) first = item;
            count += item.ItemId == GoldCoinPickupItem.DefaultItemId ? item.PickupQuantity : 1;
        }
        return count;
    }
}
