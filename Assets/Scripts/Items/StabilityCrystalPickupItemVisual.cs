using UnityEngine;

public sealed class StabilityCrystalPickupItemVisual : PickupItemVisualBase
{
    protected override string RuntimeSpriteName => "Runtime Stability Crystal";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(13, 29, 91, 255);
            case 'B': return new Color32(26, 58, 145, 255);
            case 'M': return new Color32(46, 91, 183, 255);
            case 'L': return new Color32(86, 151, 220, 255);
            case 'H': return new Color32(158, 214, 247, 255);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[]
    {
        // Isometric cube: diamond top, straight upright edges and two planar sides.
        "........H........",
        "......HHLLL......",
        "....HHLLLLMMM....",
        "..HHLLLLMMMMMMM..",
        "HHLLLLMMMMMMMMMMD",
        "HMLLMMMMMMMMMBBBD",
        "HMMMLLMMMMMBBBBBD",
        "HMMMMMLMMBBBBBBBD",
        "HMMMMMMMLBBBBBBBD",
        "HMMMLMMMLBBBDBBBD",
        "HMMMLMMMLBBBDBBDD",
        "HMMMMMMMLBBBDDDBD",
        "HMMMMMMMLBBBDBBDD",
        "LLMMMMMMLBBDDDDDD",
        "..LLMMMMLBDDDDD..",
        "....LLMMLDDDD....",
        "......LLLDD......",
        "........L........"
    };
}
