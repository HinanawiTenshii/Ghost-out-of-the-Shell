using UnityEngine;

/// <summary>The existing symmetrical gilt bottle, now visibly filled with the recipe's liquid.</summary>
public sealed class PotionPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Brewed Alchemy Potion";
    protected override bool UsesEmbeddedColors => true;
    protected override string[] GetPixelRows() => GlassBottlePickupItemVisual.BottleRows;
    protected override Color GetPixelColor(char pixel)
    {
        var potion = GetComponent<PotionPickupItem>();
        Color liquid = AlchemyRecipeCatalog.GetColor(potion != null ? potion.Kind : AlchemyPotionKind.Vitality);
        if (pixel == 'i') return liquid;
        if (pixel == 'J') return Color.Lerp(liquid, Color.white, .32f);
        return GlassBottlePickupItemVisual.BottleColor(pixel);
    }
}
