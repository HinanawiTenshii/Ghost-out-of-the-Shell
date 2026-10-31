using UnityEngine;

/// <summary>Shared 16-PPU sword design: point down in the stand, point forward when attacking.</summary>
public static class HeroSwordPixelArt
{
    public enum Ink { Blade, BladeShadow, Hilt, Grip }
    public const int PixelsPerUnit = 16;
    public const int InsertionRow = 7;
    public const int Width = 6;
    public const int Height = 17;

    public readonly struct PixelBlock
    {
        public readonly int X, Y, Width, Height;
        public readonly Ink Color;
        public PixelBlock(int x, int y, int width, int height, Ink color)
        { X = x; Y = y; Width = width; Height = height; Color = color; }
    }

    // Coordinates match the sword stand. The lower six blade rows are embedded there.
    // The one-pixel tip is only exposed in the complete attack weapon.
    private static readonly PixelBlock[] Blocks = {
        new PixelBlock(-1, 1, 1, 1, Ink.Blade),
        new PixelBlock(-1, 2, 1, 11, Ink.Blade),
        new PixelBlock(0, 2, 1, 11, Ink.BladeShadow),
        new PixelBlock(-3, 13, 6, 1, Ink.Hilt),
        new PixelBlock(-3, 12, 1, 1, Ink.Hilt),
        new PixelBlock(2, 12, 1, 1, Ink.Hilt),
        new PixelBlock(-1, 14, 2, 3, Ink.Grip),
        new PixelBlock(-2, 17, 4, 1, Ink.Hilt)
    };
    public static int BlockCount => Blocks.Length;
    public static PixelBlock GetBlock(int index) => Blocks[index];
    private static Sprite attackSprite;

    public static Color GetDefaultColor(Ink ink)
    {
        switch (ink)
        {
            case Ink.Blade: return new Color(.87f, .93f, .96f);
            case Ink.BladeShadow: return new Color(.48f, .64f, .74f);
            case Ink.Hilt: return new Color(.79f, .61f, .29f);
            default: return new Color(.19f, .39f, .56f);
        }
    }

    public static Sprite GetAttackSprite()
    {
        if (attackSprite != null) return attackSprite;
        var pixels = new Color32[Width * Height];
        foreach (var block in Blocks)
        {
            Color32 color = GetDefaultColor(block.Color);
            for (int y = block.Y; y < block.Y + block.Height; y++)
            for (int x = block.X; x < block.X + block.Width; x++)
                // Rotate the sword 180 degrees: its point now faces local +Y.
                pixels[(17 - y) * Width + (2 - x)] = color;
        }
        var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false) {
            name = "Hero Pedestal Sword Attack", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels32(pixels); texture.Apply();
        attackSprite = Sprite.Create(texture, new Rect(0, 0, Width, Height),
            new Vector2(.5f, .18f), PixelsPerUnit);
        attackSprite.name = texture.name;
        attackSprite.hideFlags = HideFlags.HideAndDontSave;
        return attackSprite;
    }
}
