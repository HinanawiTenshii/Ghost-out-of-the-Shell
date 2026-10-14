using UnityEngine;

/// <summary>Large red casing, steel caps and a simple plus mark, matching the mechanical props.</summary>
public sealed class BatteryPickupItemVisual : PickupItemVisualBase
{
    private static readonly Sprite[] chargeSprites = new Sprite[8];
    public override Sprite InventoryIcon
    {
        get
        {
            var battery = GetComponent<BatteryPickupItem>();
            return GetChargeSprite(battery != null ? battery.RemainingCharge : BatteryPickupItem.DefaultCapacity,
                battery != null ? battery.MaximumCharge : BatteryPickupItem.DefaultCapacity);
        }
    }
    public override Sprite GetInventoryIcon(PersistentInventory.Slot slot)
    {
        var charge = BatteryPickupItem.ReadCharge(slot.ItemState, slot.HasStoredCharge, slot.StoredCharge);
        return GetChargeSprite(charge.remaining, charge.capacity);
    }
    protected override void LateUpdate()
    {
        GetComponent<SpriteRenderer>().sprite = InventoryIcon;
        base.LateUpdate(); // Sync the pickup breathing renderer after selecting its charge frame.
    }

    private static Sprite GetChargeSprite(int remaining, int capacity)
    {
        int height = Mathf.Clamp(Mathf.CeilToInt(7f * remaining / Mathf.Max(1, capacity)), 0, 7);
        if (chargeSprites[height] != null) return chargeSprites[height];
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Battery Charge " + height, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        string[] rows = FullRows;
        for (int row = 0; row < 16; row++)
            for (int x = 0; x < 16; x++)
            {
                char pixel = rows[row][x];
                if (row >= 5 && row <= 11 && 11 - row >= height && (pixel == 'R' || pixel == 'L' || pixel == 'B')) pixel = 'S';
                texture.SetPixel(x, 15 - row, PixelColor(pixel));
            }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .5f), 16);
        sprite.name = texture.name; sprite.hideFlags = HideFlags.HideAndDontSave;
        return chargeSprites[height] = sprite;
    }
    protected override void OnEnable()
    {
        // Old scene overrides must not send the pickup to the post-mask camera.
        if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;
        base.OnEnable();
        Transform breathingVisual = transform.Find("Pickup Breathing Visual");
        if (breathingVisual != null) breathingVisual.gameObject.layer = gameObject.layer;
    }

    protected override string RuntimeSpriteName => "Red Steel Battery";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel) => PixelColor(pixel);
    public static Color PixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color(.17f, .21f, .23f);
            case 'E': return new Color(.34f, .40f, .43f); // Brighter right metal edge, separate from socket recesses.
            case 'S': return new Color(.46f, .51f, .53f);
            case 'H': return new Color(.78f, .83f, .83f);
            case 'R': return new Color(.80f, .10f, .07f);
            case 'L': return new Color(1f, .28f, .17f);
            case 'B': return new Color(.62f, .16f, .11f);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => FullRows;
    private static readonly string[] FullRows = {
        "................", "......HHHH......", "......SSSS......",
        "....HHHHHHHH....", "...HSSSSSSSSE...", "...HLRRRRRRBE...",
        "...HLRRRHRRBE...", "...HLRRHHHRBE...", "...HLRRRHRRBE...",
        "...HLRRRHRRBE...", "...HLRRRRRRBE...", "...HLRRRRRRBE...",
        "...HSSSSSSSSE...", "....DDDDDDDD....", "................", "................"
    };
}
