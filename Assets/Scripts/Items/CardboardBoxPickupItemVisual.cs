using UnityEngine;

/// <summary>
/// Pixelated version of the original upside-down perspective box and spread lower flaps.
/// The sprite deliberately contains no printed markings so it reads as a plain box.
/// </summary>
public sealed class CardboardBoxPickupItemVisual : PickupItemVisualBase
{
    // Original 48x40 perspective geometry snapped onto a 24x20 pixel grid.
    // Preserve the broad right face and the separately spread bottom flaps.
    private static readonly string[] BoxRows = {
        "........................",
        "........................",
        "..........OOOOOOOOOOOOO.",
        "........OOHHHHHHHHHHOOO.",
        ".......OHHHHHHHHHHOOSSO.",
        ".....OOHHHHHHHHHOOSSSSO.",
        "....OOOOOOOOOOOOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSSSO.",
        "....OCCCCCCCCCCOSSSSOOCO",
        "....OCCCCCCCCCCOSSSOCCO.",
        "....OCCCCCCCCCCOSSOCCO..",
        "....OCCCCCCCCCCOOOCCCO..",
        "...OOOOOOOOOOOOOOCCCO...",
        "..OOHHHHHHHHHHHO.OOO....",
        "...OHHHHHHHHHHO.........",
        "..OOOOOOOOOOOOO........."
    };

    [SerializeField] private Color cardboardColor =
        new Color(0.72f, 0.55f, 0.32f, 1f);
    [SerializeField] private Color highlightColor =
        new Color(0.9f, 0.76f, 0.5f, 1f);
    [SerializeField] private Color shadowColor =
        new Color(0.48f, 0.32f, 0.18f, 1f);
    [SerializeField] private Color outlineColor =
        new Color(0.2f, 0.14f, 0.09f, 1f);

    protected override string RuntimeSpriteName => "Runtime Cardboard Box";
    protected override FilterMode RuntimeTextureFilterMode => FilterMode.Point;
    protected override bool UsesEmbeddedColors => true;
    protected override string[] GetPixelRows() => BoxRows;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'C': return cardboardColor;
            case 'H': return highlightColor;
            case 'S': return shadowColor;
            case 'O': return outlineColor;
            default: return Color.clear;
        }
    }

    public Sprite CreateStandaloneSprite(out Texture2D texture)
    {
        int height = BoxRows.Length;
        int width = BoxRows[0].Length;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Worn Cardboard Box",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                texture.SetPixel(x, y, GetPixelColor(BoxRows[row][x]));
            }
        }

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.34f),
            Mathf.Max(width, height));
        sprite.name = "Worn Cardboard Box";
        return sprite;
    }
}
