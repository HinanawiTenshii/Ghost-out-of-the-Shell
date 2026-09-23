using UnityEngine;

/// <summary>Chunky red/steel remote, matching the automaton armor without a display window.</summary>
public sealed class PuppetRemotePickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Pixel Puppet Remote";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color(0.18f, 0.21f, 0.22f, 1f);
            case 'S': return new Color(0.46f, 0.5f, 0.5f, 1f);
            case 'H': return new Color(0.68f, 0.72f, 0.73f, 1f);
            case 'R': return new Color(0.82f, 0.035f, 0.025f, 1f);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows() => new[]
    {
        "................",
        ".........HH.....",
        ".........SS.....",
        ".........SS.....",
        "....HHHHHHHH....",
        "...HSSSSSSSSD...",
        "...HRRSSSSRRD...",
        "...HRRSSSSRRD...",
        "...HSSSSSSSSD...",
        "...HSSRRRRSSD...",
        "...HSSRRRRSSD...",
        "...HSSDDDDSSD...",
        "...HRRSSSSRRD...",
        "...HSSSSSSSSD...",
        "....DDDDDDDD....",
        "................"
    };
}
