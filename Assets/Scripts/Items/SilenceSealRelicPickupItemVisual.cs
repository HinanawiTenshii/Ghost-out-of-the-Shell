using UnityEngine;

/// <summary>Gilt silver seal with a stitched mouth and dark red vow ribbons.</summary>
public sealed class SilenceSealRelicPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Silence Seal Relic";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(43, 46, 54, 255);
            case 'B': return new Color32(139, 94, 49, 255);
            case 'G': return new Color32(218, 171, 83, 255);
            case 'L': return new Color32(249, 220, 146, 255);
            case 'S': return new Color32(133, 151, 157, 255);
            case 'H': return new Color32(202, 216, 211, 255);
            case 'W': return new Color32(237, 239, 218, 255);
            case 'R': return new Color32(114, 40, 51, 255);
            case 'V': return new Color32(171, 61, 64, 255);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows() => new[]
    {
        "....................",
        "........DDDD........",
        ".......DGLGBD.......",
        ".......DG..BD.......",
        ".......DGGGBD.......",
        "......DDGGGGDD......",
        ".....DGLLLLGGBD.....",
        "....DGLHHHHHSGGD....",
        "...DGLHWWWWWHSGGD...",
        "...DGHWWSSSSHHSGD...",
        "..DGLHWSHHHHSWSGBD..",
        "..DGHWSHHHHHHSWSBD..",
        "..DGHWHDHDHDHHWSBD..",
        "..DGHWDDDDDDDDWSBD..",
        "..DGHWHDHDHDHHWSBD..",
        "..DGBHWSHHHHSWSBBD..",
        "...DGBHWSSSSWSBBD...",
        "...DGBSHHHHHHSBBD...",
        "....DGBBSSSSBBBD....",
        ".....DGBBBBBBBD.....",
        ".....DRVDDDDVRD.....",
        "....DRVRD..DRVVD....",
        "....DRVRD..DRVRD....",
        "...DRVRD....DRVRD...",
        "...DR.DD....DD.RD...",
        "....D..........D....",
    };
}
