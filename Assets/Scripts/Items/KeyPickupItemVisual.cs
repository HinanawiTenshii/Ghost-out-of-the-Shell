using System.Collections.Generic;
using UnityEngine;

/// <summary>Steel key with a coloured enamel bow; the saved tint identifies its door.</summary>
public sealed class KeyPickupItemVisual : PickupItemVisualBase
{
    // Broad, readable bow, bevelled shaft and two square teeth. Only the enamel
    // (C/H/S) changes colour; metal and the open centre retain their own palette.
    private static readonly string[] KeyRows = {
        "........................",
        "...DDDDD................",
        "..DLHHHMD...............",
        ".DLHCCCSMD..............",
        ".DHCDDDCSD..............",
        ".DHC...CSDDDDDDDDDDDDDD.",
        ".DHC...CSDLLLLLLLLLLLLMD",
        ".DSCDDDCSDMMMMMMMMMMMMMD",
        ".DMSCSSSDDDDDDDDDMD.DMD.",
        "..DMSSSD........DMD.DMD.",
        "...DDDD.........DMD.DMD.",
        "................DDD.DDD.",
        "........................",
        "........................"
    };

    // The prefab services all inventory slots. Never recolour its DisplayColor
    // while drawing one slot, otherwise other keys inherit that slot's colour.
    private readonly Dictionary<Color32, Sprite> colorSprites = new Dictionary<Color32, Sprite>();
    private SpriteRenderer sourceRenderer;
    private Color appliedColor;

    protected override bool UsesEmbeddedColors => true;
    protected override string RuntimeSpriteName => "Runtime Steel Enamel Key";
    protected override string[] GetPixelRows() => KeyRows;
    protected override Color GetPixelColor(char pixel) => PixelColor(pixel, DisplayColor);

    public override Sprite InventoryIcon => GetColorSprite(DisplayColor);

    public override Sprite GetInventoryIcon(PersistentInventory.Slot slot)
    {
        return GetColorSprite(slot != null && slot.HasVisualColor ? slot.VisualColor : DisplayColor);
    }

    // This visual owns colour-keyed sprites instead of the base's single sprite.
    protected override void EnsureVisualSprite() { }

    protected override void Awake()
    {
        base.Awake();
        RefreshWorldSprite();
        base.LateUpdate(); // The breathing copy must show the same sprite immediately.
    }

    public override void SetDisplayColor(Color color)
    {
        base.SetDisplayColor(color); // Keep the existing scene/save/inventory colour field.
        RefreshWorldSprite();
        base.LateUpdate();
    }

    protected override void LateUpdate()
    {
        // Also support editing Item Color during play without rebuilding each frame.
        if (appliedColor != DisplayColor) RefreshWorldSprite();
        base.LateUpdate();
    }

    private void RefreshWorldSprite()
    {
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null) return;
        sourceRenderer.sprite = InventoryIcon;
        sourceRenderer.color = Color.white;
        appliedColor = DisplayColor;
    }

    private Sprite GetColorSprite(Color color)
    {
        Color32 key = color;
        if (colorSprites.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;
        int width = KeyRows[0].Length, height = KeyRows.Length;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
            name = RuntimeSpriteName, filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < height; row++)
        for (int x = 0; x < width; x++)
            texture.SetPixel(x, height - 1 - row, PixelColor(KeyRows[row][x], key));
        texture.Apply(false, true);
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), Vector2.one * .5f, width);
        sprite.name = RuntimeSpriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        colorSprites[key] = sprite;
        return sprite;
    }

    private static Color PixelColor(char pixel, Color enamel)
    {
        Color result;
        switch (pixel)
        {
            case 'D': result = new Color32(48, 58, 65, 255); break;
            case 'M': result = new Color32(136, 155, 163, 255); break;
            case 'L': result = new Color32(218, 230, 225, 255); break;
            case 'C': result = enamel; break;
            case 'H': result = Color.Lerp(enamel, Color.white, .35f); break;
            case 'S': result = Color.Lerp(enamel, Color.black, .28f); break;
            default: return Color.clear;
        }
        result.a = enamel.a;
        return result;
    }

    protected override void OnDestroy()
    {
        foreach (Sprite sprite in colorSprites.Values)
        {
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
        }
        colorSprites.Clear();
        base.OnDestroy();
    }
}
