using UnityEngine;

/// <summary>Coarse steel barrel, brass fittings and brown wooden grip.</summary>
public sealed class DisposableHandgunPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Disposable Hand Cannon";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
        => PixelColor(pixel);

    public static Color PixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color(.20f, .23f, .25f);
            case 'S': return new Color(.49f, .56f, .59f);
            case 'H': return new Color(.80f, .87f, .88f);
            case 'G': return new Color(.89f, .60f, .18f);
            case 'W': return new Color(.55f, .27f, .10f);
            case 'L': return new Color(.76f, .40f, .16f);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[]
    {
        "................",
        "................",
        ".....D..........",
        "....GGHHHHHHHHD.",
        "...GGGSSSSSSSSD.",
        "...GSSSSSSSSSSD.",
        "..WWWGDDDDDDDDD.",
        "..WLLWG..G......",
        "..WLLWGGGG......",
        ".WLLWW..........",
        ".WLLW...........",
        ".WWW............",
        ".GGG............",
        "................",
        "................",
        "................"
    };
}
