using UnityEngine;

/// <summary>Faceted transparent potion glass, gilt neck/base and a jewel stopper; no potion contents/effect.</summary>
public sealed class GlassBottlePickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Ornate Glass Potion Bottle";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel) => BottleColor(pixel);
    protected override string[] GetPixelRows() => BottleRows;
    public static readonly string[] BottleRows = {
        "................", "......GGGG......", ".....GHJJHG.....", "......GBBG......",
        "......GLLG......", "......GLLG......", ".....GGGGGG.....", "....DTLiiLTD....",
        "...DTLiiiiLTD...", "..DTLiiiiiiLTD..", "..DTLiiiiiiLTD..", "..DTLiiiiiiLTD..",
        "..DTiiiiiiiiTD..", "..DGiiiGGiiiGD..", "..DTGiiJJiiGTD..", "...DTGiGGiGTD...",
        "...DTiiiiiiTD...", "....DTTTTTTD....", "....BGGGGGGB....", ".....BBBBBB....."
    };
    public static Color BottleColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color(.19f,.37f,.46f,1);
            case 'T': return new Color(.37f,.74f,.79f,.92f);
            case 'L': return new Color(.87f,1f,1f,.95f);
            case 'i': return new Color(.40f,.72f,.79f,.27f);
            case 'B': return new Color(.43f,.25f,.09f,1);
            case 'G': return new Color(.88f,.61f,.18f,1);
            case 'H': return new Color(1,.88f,.48f,1);
            case 'J': return new Color(.71f,.24f,.76f,1);
            default: return Color.clear;
        }
    }
    public static Sprite CreateFlightSprite(out Texture2D texture)
    {
        texture = new Texture2D(16, 20, TextureFormat.RGBA32, false)
        { name = "Thrown Potion Bottle", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 20; y++) for (int x = 0; x < 16; x++)
            texture.SetPixel(x, 19-y, BottleColor(BottleRows[y][x]));
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0,0,16,20), Vector2.one * .5f, 20);
    }
}
