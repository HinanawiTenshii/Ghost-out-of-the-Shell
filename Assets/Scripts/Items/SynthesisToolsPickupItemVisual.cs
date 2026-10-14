using UnityEngine;

/// <summary>An open red tool roll with a steel wrench, small mallet and brass clasp.</summary>
public sealed class SynthesisToolsPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Synthesis Tools";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(51, 63, 70, 255);
            case 'S': return new Color32(134, 152, 157, 255);
            case 'H': return new Color32(201, 215, 212, 255);
            case 'r': return new Color32(113, 47, 47, 255);
            case 'R': return new Color32(194, 69, 59, 255);
            case 'W': return new Color32(144, 101, 65, 255);
            case 'G': return new Color32(207, 163, 83, 255);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            "....................",
            "....................",
            "....SS..SS..HHHHHH..",
            "....SH..HS..SSSSSS..",
            "....SHSSHS....WW....",
            ".....SHHS.....WW....",
            "..DDDDSSDDDDDDWWDD..",
            ".DHHHHSSHHHHHHWWHHD.",
            ".DHrrrSSrrrrrrWWrHD.",
            ".DSrrrSSrrrrrrWWrSD.",
            ".DSrrrSSrrrrrrWWrSD.",
            ".DSrrrSSrrrrrrGGrSD.",
            ".DSrrrGGrrrrrrGGrSD.",
            ".DSrrrGGrrrrrrWWrSD.",
            ".DSrrrRRrrrrrrWWrSD.",
            ".DSrrrRRrrrrrrrrrSD.",
            ".DSSSSSSSSSSSSSSSSD.",
            "..DDDDDDHGGHDDDDDD..",
            "........DGGD........",
            "...................."
        };
    }
}
