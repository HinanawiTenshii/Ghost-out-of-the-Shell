using UnityEngine;

/// <summary>Hollow red energy outline, a white-hot control rune and translucent fringe; no physical casing.</summary>
public sealed class ClockworkPuppetRemotePickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Projected Magic Puppet Remote";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch(pixel)
        {
            case 'W': return new Color(1f,.94f,.89f,1f);
            case 'H': return new Color(1f,.43f,.37f,.96f);
            case 'R': return new Color(1f,.10f,.055f,.86f);
            case 'g': return new Color(1f,.15f,.07f,.25f);
            case 'f': return new Color(1f,.12f,.06f,.10f);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[]
    {
        ".........gWg....",
        "..........H.....",
        "..g.......Rg....",
        "....gHHHHHHg....",
        "...gHfffffRHg...",
        "...HffffffffR...",
        ".g.RfffgWgffR.g.",
        "...RffgWHWgfH...",
        "...HfffgWgffR...",
        "...RffffffffR...",
        "...RffHHHHffH...",
        "...HffggggffR...",
        "...gRffffffRg...",
        "....gRRHHRRg....",
        "..g........g....",
        ".......g........"
    };
}
