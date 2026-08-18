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
        if (pixel == 'B')
        {
            return bodyColor;
        }

        if (pixel == 'H')
        {
            return useBodyHighlight ? bodyHighlightColor : bodyColor;
        }

        return pixel == 'F' ? fuseColor : Color.clear;
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            ".........FF..",
            "........FF...",
            ".......FF....",
            "......FFF....",
            ".....BHHB....",
            "...BBHHBBBB..",
            "..BBHBBBBBBB.",
            ".BBHBBBBBBBBB",
            ".BBBBBBBBBBBB",
            ".BBBBBBBBBBBB",
            "..BBBBBBBBBB.",
            "...BBBBBBBB..",
            ".....BBBB...."
        };
    }
}
