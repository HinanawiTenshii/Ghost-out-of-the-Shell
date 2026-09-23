using UnityEngine;

/// <summary>
/// Coarse-pixel bacteriophage-shaped clockwork puppet. Its segmented neck and tail
/// display remaining magic, while four articulated fibres grip mechanisms.
/// </summary>
public sealed class ClockworkPuppetPickupItemVisual : PickupItemVisualBase
{
    private const int PixelWidth = 24;
    private const int PixelHeight = 28;
    private const int EnergyFirstRow = 11;
    private const int EnergySegmentCount = 6;
    internal static readonly string[] PixelRows = BuildPixelRows();
    private static readonly string[][] WalkRows = { BuildPixelRows(1), BuildPixelRows(2) };

    protected override string RuntimeSpriteName => "Runtime Clockwork Bacteriophage Puppet";
    protected override bool UsesEmbeddedColors => true;
    protected override string[] GetPixelRows() => PixelRows;
    protected override Color GetPixelColor(char pixel) => GetLayerColor(pixel);

    private static Color GetLayerColor(char pixel)
    {
        switch (pixel)
        {
            // Bomb-style broad planes: dark shell, steel body and one highlight.
            // Red is reserved for the functional six-segment energy column.
            case 'D': return new Color(0.11f, 0.13f, 0.17f, 1f);
            case 'P': return new Color(0.30f, 0.34f, 0.40f, 1f);
            case 'M': return new Color(0.46f, 0.49f, 0.50f, 1f);
            case 'H': return new Color(0.69f, 0.73f, 0.76f, 1f);
            case 'Y': return new Color(0.82f, 0.23f, 0.24f, 1f);
            case 'R': return new Color(0.52f, 0.12f, 0.16f, 1f);
            default: return Color.clear;
        }
    }

    private static string[] BuildPixelRows(int walkPose = 0)
    {
        char[,] pixels = new char[PixelHeight, PixelWidth];
        for (int y = 0; y < PixelHeight; y++)
            for (int x = 0; x < PixelWidth; x++)
                pixels[y, x] = '.';

        // Faceted capsule, without nested outlines, rivets or fine texture.
        int[] left = {9,8,7,6,6,6,6,7,8,9};
        for (int y = 0; y < left.Length; y++)
            DrawHeadSpan(pixels, y, left[y], PixelWidth - 1 - left[y]);

        DrawFilledSpan(pixels, 10, 9, 14, 'D', 'M');
        for (int segment = 0; segment < EnergySegmentCount; segment++)
        {
            DrawFilledSpan(pixels, EnergyFirstRow + segment, 10, 13,
                'P', segment % 2 == 0 ? 'Y' : 'R');
        }

        // Base plate and central injection spike.
        DrawFilledSpan(pixels, 17, 8, 15, 'D', 'M');
        DrawFilledSpan(pixels, 18, 6, 17, 'D', 'H');
        DrawFilledSpan(pixels, 19, 9, 14, 'D', 'P');
        for (int y = 20; y <= 24; y++)
        {
            Put(pixels, 11, y, 'M');
            Put(pixels, 12, y, 'P');
        }

        // Four articulated bacteriophage fibres/claws. Two broad outer feet
        // stabilise the body while the shorter inner pair grips a lever stem.
        // Rows increase downward: each knee rises above its base-plate root
        // before the long lower segment bends outward and down to the foot.
        // Alternate diagonal pairs; roots stay attached to the fixed base plate.
        int liftA = walkPose == 1 ? 2 : 0;
        int liftB = walkPose == 2 ? 2 : 0;
        DrawFibre(pixels, 7, 18, 4, 15, 1, 26, true, liftA);
        DrawFibre(pixels, 9, 19, 8, 16, 6, 26, false, liftB);
        DrawFibre(pixels, 15, 18, 18, 15, 21, 26, false, liftB);
        DrawFibre(pixels, 13, 19, 14, 16, 16, 26, true, liftA);

        string[] rows = new string[PixelHeight];
        for (int y = 0; y < PixelHeight; y++)
        {
            char[] row = new char[PixelWidth];
            for (int x = 0; x < PixelWidth; x++) row[x] = pixels[y, x];
            rows[y] = new string(row);
        }
        return rows;
    }

    private static void DrawHeadSpan(char[,] pixels, int y, int left, int right)
    {
        for (int x = left; x <= right; x++)
        {
            char layer;
            if (x == left || x == right || y == 0 || y == 9) layer = 'D';
            else if (x <= 9 && y <= 4) layer = 'H';
            else if (x < 13) layer = 'M';
            else layer = 'P';
            Put(pixels, x, y, layer);
        }
    }

    private static void DrawFilledSpan(char[,] pixels, int y, int left, int right, char edge, char fill)
    {
        for (int x = left; x <= right; x++)
            Put(pixels, x, y, x == left || x == right ? edge : fill);
    }

    private static void DrawFibre(
        char[,] pixels, int startX, int startY, int jointX, int jointY,
        int endX, int endY, bool highlighted, int lift)
    {
        jointY -= lift / 2;
        endY -= lift;
        // Two-pixel rails give readable joints at pickup scale, not hairline claws.
        DrawLine(pixels, startX, startY, jointX, jointY, 'P', 0);
        DrawLine(pixels, jointX, jointY, endX, endY, 'P', 0);
        char inner = highlighted ? 'H' : 'M';
        DrawLine(pixels, startX + 1, startY, jointX + 1, jointY, inner, 0);
        DrawLine(pixels, jointX + 1, jointY, endX + 1, endY, inner, 0);
        for (int x = endX; x <= endX + 1; x++) Put(pixels, x, endY + 1, 'M');
    }

    private static void DrawLine(char[,] pixels, int x0, int y0, int x1, int y1, char value, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            for (int oy = -thickness; oy <= thickness; oy++)
                for (int ox = -thickness; ox <= thickness; ox++)
                    Put(pixels, x0 + ox, y0 + oy, value);
            if (x0 == x1 && y0 == y1) break;
            int twiceError = error * 2;
            if (twiceError >= dy) { error += dy; x0 += sx; }
            if (twiceError <= dx) { error += dx; y0 += sy; }
        }
    }

    private static void Put(char[,] pixels, int x, int y, char value)
    {
        if (x >= 0 && x < PixelWidth && y >= 0 && y < PixelHeight)
            pixels[y, x] = value;
    }

    public static Sprite CreateRuntimeSprite(out Texture2D texture)
    {
        texture = new Texture2D(PixelWidth, PixelHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime Deployed Clockwork Bacteriophage Puppet",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int row = 0; row < PixelHeight; row++)
        {
            int y = PixelHeight - 1 - row;
            for (int x = 0; x < PixelWidth; x++)
                texture.SetPixel(x, y, GetLayerColor(PixelRows[row][x]));
        }
        // Keep the deployed texture readable because its tail segments update
        // only when the visible energy step changes.
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, PixelWidth, PixelHeight),
            new Vector2(0.5f, 0.04f),
            PixelHeight);
        sprite.name = "Runtime Deployed Clockwork Bacteriophage Puppet";
        return sprite;
    }

    public static void ApplyRuntimeEnergy(Texture2D texture, float normalizedEnergy)
    {
        if (texture == null) return;
        int activeSegments = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Clamp01(normalizedEnergy) * EnergySegmentCount),
            0,
            EnergySegmentCount);
        for (int row = EnergyFirstRow; row < EnergyFirstRow + EnergySegmentCount; row++)
        {
            int segment = row - EnergyFirstRow;
            bool active = segment < activeSegments;
            int textureY = PixelHeight - 1 - row;
            for (int x = 0; x < PixelWidth; x++)
            {
                char pixel = PixelRows[row][x];
                if (pixel != 'Y' && pixel != 'R') continue;
                Color color;
                if (active)
                {
                    color = GetLayerColor(pixel);
                }
                else
                {
                    color = GetLayerColor(segment % 2 == 0 ? 'P' : 'D');
                }
                texture.SetPixel(x, textureY, color);
            }
        }
        texture.Apply(false, false);
    }

    /// <summary>0 = rest, 1/2 = alternating steps. Reuses the deployed texture.</summary>
    public static void ApplyRuntimePose(Texture2D texture, float normalizedEnergy, int pose)
    {
        if (texture == null) return;
        string[] rows = pose == 1 || pose == 2 ? WalkRows[pose - 1] : PixelRows;
        for (int row = 0; row < PixelHeight; row++)
            for (int x = 0; x < PixelWidth; x++)
                texture.SetPixel(x, PixelHeight - 1 - row, GetLayerColor(rows[row][x]));
        // Reapply charge after painting the pose; uploads once and cannot refill the neck.
        ApplyRuntimeEnergy(texture, normalizedEnergy);
    }
}
