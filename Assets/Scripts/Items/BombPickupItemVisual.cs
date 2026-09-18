using UnityEngine;

public sealed class BombPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color bodyColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    [SerializeField] private bool useBodyHighlight;
    [SerializeField] private Color bodyHighlightColor =
        new Color(0.32f, 0.35f, 0.42f, 1f);
    [SerializeField] private Color fuseColor = new Color(0.78f, 0.64f, 0.43f, 1f);

    protected override string RuntimeSpriteName => "Runtime Pixel Bomb";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        return SamplePixel(pixel, bodyColor, useBodyHighlight, bodyHighlightColor, fuseColor);
    }

    protected override string[] GetPixelRows()
    {
        var item = GetComponent<PickupItemBase>();
        return GetBombRows(item != null && item.ItemId == "purple_bomb");
    }

    // Shared by inventory, world pickups and armed bombs: a single silhouette
    // and palette, not three independently maintained copies of the artwork.
    public static Color SamplePixel(char pixel, Color body, bool useHighlight, Color highlight, Color fuse)
    {
        switch (pixel)
        {
            case 'B': return body;
            case 'D': return Color.Lerp(body, Color.black, 0.35f);
            case 'H': return useHighlight ? highlight : body;
            case 'K': return new Color(0.46f, 0.49f, 0.50f, 1f);
            case 'F': return fuse;
            default: return Color.clear;
        }
    }

    public static string[] GetBombRows(bool reinforced) => reinforced ? SuperRows : BombRows;

    private static readonly string[] BombRows = {
        "................",
        "..........FF....",
        "........FFF.....",
        "........F.......",
        "......KKKK......",
        ".....DDDDDD.....",
        "....HHBBBBBD....",
        "...HHHBBBBBDD...",
        "..BHHBBBBBBBDD..",
        "..BBBBBBBBBBDD..",
        "..BBBBBBBBBBDD..",
        "..BBBBBBBBBBDD..",
        "...BBBBBBBBDD...",
        "....DDDDDDDD....",
        "......DDDD......",
        "................"
    };

    private static readonly string[] SuperRows = {
        "................",
        "..........FF....",
        "........FFF.....",
        "........F.......",
        "......KKKK......",
        "....DDDDDDDD....",
        "...HHBBBBBBDD...",
        "..HHHBBBBBBBDD..",
        "..BBBBBBBBBBDD..",
        "..DDDDHHDDDDDD..",
        "..DDDDHHDDDDDD..",
        "..BBBBBBBBBBDD..",
        "...BBBBBBBBDD...",
        "....DDDDDDDD....",
        "......DDDD......",
        "................"
    };
}
