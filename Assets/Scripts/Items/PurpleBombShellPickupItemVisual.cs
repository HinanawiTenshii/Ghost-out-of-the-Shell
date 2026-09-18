using UnityEngine;

/// <summary>The reinforced super-bomb casing, before its collar/fuse is installed.</summary>
public sealed class PurpleBombShellPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color shellColor =
        new Color(0.54f, 0.25f, 0.82f, 1f);
    [SerializeField] private Color shellShadowColor =
        new Color(0.351f, 0.1625f, 0.533f, 1f);
    [SerializeField] private Color shellHighlightColor =
        new Color(0.72f, 0.43f, 0.94f, 1f);

    protected override string RuntimeSpriteName =>
        "Runtime Pixel Purple Bomb Shell";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'P': return shellColor;
            case 'D': return shellShadowColor;
            case 'H': return shellHighlightColor;
            case 'O': return Color.Lerp(shellShadowColor, Color.black, 0.55f);
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows() => ShellRows;

    private static readonly string[] ShellRows = CreateShellRows();

    private static string[] CreateShellRows()
    {
        // Keep the finished bomb's shoulders, belt, buckle and lower facets.
        // Move the casing up two pixels to centre the standalone material.
        var rows = new string[16];
        string[] bomb = BombPickupItemVisual.GetBombRows(true);
        for (int row = 0; row < rows.Length; row++)
        {
            rows[row] = row + 2 < bomb.Length
                ? bomb[row + 2].Replace('F', '.').Replace('K', '.').Replace('B', 'P')
                : "................";
        }
        // Empty socket, not a lit/ready-to-detonate bomb.
        rows[2] = "......HOOH......";
        return rows;
    }
}
