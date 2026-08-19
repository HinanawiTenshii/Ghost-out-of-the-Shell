using UnityEngine;

/// <summary>Irregular pixel-art powder mound using the sky-blue blast palette.</summary>
public sealed class SkyBlueGunpowderPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color powderColor =
        new Color(0.33f, 0.81f, 1f, 1f);
    [SerializeField] private Color powderShadowColor =
        new Color(0.16f, 0.46f, 0.7f, 1f);
    [SerializeField] private Color powderHighlightColor =
        new Color(0.66f, 0.93f, 1f, 1f);

    protected override string RuntimeSpriteName =>
        "Runtime Pixel Sky Blue Gunpowder";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'B': return powderColor;
            case 'D': return powderShadowColor;
            case 'H': return powderHighlightColor;
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            "......DD.......",
            ".....DBBD......",
            "....DBBHBD.....",
            "...DBBHBBD.....",
            "..DBBHHBHBBD...",
            ".DBBHBHBBBBBD..",
            "DBBBBBBBBBBBBD.",
            "DBBHBHBBBBBBBD.",
            ".DDDDDDDDDDDD..",
            "...DDDDDDDD...."
        };
    }
}
