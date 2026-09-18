using UnityEngine;

/// <summary>Chunky bent fuse, matching the finished bomb's tan cord.</summary>
public sealed class FusePickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color cordColor =
        new Color(0.42f, 0.3f, 0.2f, 1f);
    [SerializeField] private Color cordHighlightColor =
        new Color(0.78f, 0.64f, 0.43f, 1f);
    [SerializeField] private Color emberColor =
        new Color(1f, 0.42f, 0.12f, 1f);

    protected override string RuntimeSpriteName => "Runtime Pixel Fuse";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'C': return cordColor;
            case 'H': return cordHighlightColor;
            case 'E': return emberColor;
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            "................",
            "................",
            "...........EE...",
            "..........HHE...",
            ".........HHC....",
            "........HHC.....",
            ".......HHC......",
            "......HHC.......",
            ".....HHC........",
            "....HHC.........",
            "...HHC..........",
            "...HCC..........",
            "...HHHHHHH......",
            "....CCCCCC......",
            "................",
            "................"
        };
    }
}
