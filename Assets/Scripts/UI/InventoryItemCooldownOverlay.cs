using UnityEngine;
using UnityEngine.UI;

/// <summary>Lightweight slot overlay: no inventory redraws or allocations while seconds are unchanged.</summary>
public sealed class InventoryItemCooldownOverlay : MonoBehaviour
{
    private int slotIndex, lastSecond = -1;
    private Image shade, progress;
    private Text seconds;
    public void Configure(int index, Font font)
    {
        slotIndex = index;
        shade = Child<Image>("Cooldown shade");
        shade.color = new Color(.015f, .035f, .08f, .78f);
        progress = Child<Image>("Cooldown bar");
        progress.color = ZeldaUiPalette.Ghost;
        var bar = progress.rectTransform;
        bar.anchorMax = new Vector2(1, 0); bar.offsetMin = Vector2.zero; bar.offsetMax = new Vector2(0, 2);
        seconds = Child<Text>("Cooldown seconds");
        seconds.font = font; seconds.fontSize = 15; seconds.alignment = TextAnchor.MiddleCenter;
        seconds.color = ZeldaUiPalette.Ghost;
        seconds.horizontalOverflow = HorizontalWrapMode.Overflow;
        seconds.verticalOverflow = VerticalWrapMode.Overflow;
        Refresh();
    }
    private T Child<T>(string label) where T : Graphic
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
        go.transform.SetParent(transform, false);
        var graphic = go.GetComponent<T>(); graphic.raycastTarget = false;
        var rect = graphic.rectTransform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return graphic;
    }
    private void LateUpdate() => Refresh();
    private void OnEnable() { if (shade != null) Refresh(); }
    private void Refresh()
    {
        var slot = PersistentInventory.Instance?.GetSlot(slotIndex);
        float remaining = slot != null && !slot.IsEmpty && slot.ItemId == MortarRemotePickupItem.DefaultItemId && slot.HasStoredCharge
            ? MortarRemotePickupItem.SanitizeCooldown(slot.StoredCharge) : 0f;
        bool visible = remaining > 0f;
        if (shade.gameObject.activeSelf != visible)
        { shade.gameObject.SetActive(visible); progress.gameObject.SetActive(visible); seconds.gameObject.SetActive(visible); }
        if (!visible) { lastSecond = -1; return; }
        float ratio = remaining / MortarRemotePickupItem.Cooldown;
        shade.rectTransform.anchorMax = new Vector2(1, ratio);
        progress.rectTransform.anchorMax = new Vector2(ratio, 0);
        int next = Mathf.CeilToInt(remaining);
        if (lastSecond != next) { lastSecond = next; seconds.text = next.ToString(); }
    }
}
