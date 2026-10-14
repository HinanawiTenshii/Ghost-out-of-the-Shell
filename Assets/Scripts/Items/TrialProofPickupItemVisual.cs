using UnityEngine;

/// <summary>A pale-metal pendant framing a faceted golden gem; shared world/inventory pixel art.</summary>
public sealed class TrialProofPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Trial Proof";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(51, 63, 70, 255);
            case 'S': return new Color32(134, 152, 157, 255);
            case 'H': return new Color32(201, 215, 212, 255);
            case 'B': return new Color32(164, 105, 42, 255);
            case 'G': return new Color32(222, 164, 57, 255);
            case 'Y': return new Color32(249, 200, 83, 255);
            case 'L': return new Color32(255, 229, 148, 255);
            case 'W': return new Color32(255, 244, 207, 255);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows() => new[]
    {
        "....................",
        "........DDDD........",
        ".......DHHSSD.......",
        ".......DH..SD.......",
        ".......DHHSSD.......",
        "........DSSD........",
        ".......DHGGSD.......",
        "......DHGLYGSD......",
        ".....DHGLWYYGSD.....",
        "....DHGLWYYYGGSD....",
        "....DHGLWYYGGGSD....",
        "..DDHHGLYYGGGBSSDD..",
        "..DHSHGLYGGGBBSSSD..",
        "..DDSSGLGGGBBBSSDD..",
        "....DSHGGGGBBBSD....",
        "....DSHGGGBBBSSD....",
        ".....DSHGGBBSSD.....",
        "......DSHGBSSD......",
        ".......DSGGSD.......",
        "........DSSD........",
        "........DGSD........",
        ".........DD.........",
        "....................",
        "....................",
    };
}
