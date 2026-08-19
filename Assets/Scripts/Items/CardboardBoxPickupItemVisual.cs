using UnityEngine;

/// <summary>
/// Perspective, upside-down cubic cardboard box with its lower flaps spread out.
/// The sprite deliberately contains no printed markings so it reads as a plain box.
/// </summary>
public sealed class CardboardBoxPickupItemVisual : PickupItemVisualBase
{
    private const int GeometryScale = 3;
    private const int SpriteWidth = 48 * GeometryScale;
    private const int SpriteHeight = 40 * GeometryScale;
    private static readonly string[] BoxRows = CreateBoxRows();

    [SerializeField] private Color cardboardColor =
        new Color(0.72f, 0.55f, 0.32f, 1f);
    [SerializeField] private Color highlightColor =
        new Color(0.9f, 0.76f, 0.5f, 1f);
    [SerializeField] private Color shadowColor =
        new Color(0.48f, 0.32f, 0.18f, 1f);
    [SerializeField] private Color outlineColor =
        new Color(0.2f, 0.14f, 0.09f, 1f);

    protected override string RuntimeSpriteName => "Runtime Cardboard Box";
    protected override FilterMode RuntimeTextureFilterMode => FilterMode.Bilinear;
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

    private static string[] CreateBoxRows()
    {
        char[,] pixels = new char[SpriteWidth, SpriteHeight];
        for (int y = 0; y < SpriteHeight; y++)
        {
            for (int x = 0; x < SpriteWidth; x++)
            {
                pixels[x, y] = '.';
            }
        }

        // Geometry follows the reference: a square front face, a top and
        // right face receding toward the upper-right, and loose flaps at the
        // open bottom of the upside-down box.
        Vector2Int frontTopLeft = ScalePoint(8, 12);
        Vector2Int frontTopRight = ScalePoint(30, 12);
        Vector2Int frontBottomRight = ScalePoint(30, 31);
        Vector2Int frontBottomLeft = ScalePoint(8, 31);
        Vector2Int backTopLeft = ScalePoint(20, 3);
        Vector2Int backTopRight = ScalePoint(43, 3);
        Vector2Int backBottomRight = ScalePoint(43, 22);

        Vector2Int[] frontFlap =
        {
            frontBottomLeft,
            frontBottomRight,
            ScalePoint(27, 37),
            ScalePoint(3, 37)
        };
        Vector2Int[] rightFlap =
        {
            frontBottomRight,
            backBottomRight,
            ScalePoint(47, 24),
            ScalePoint(38, 34)
        };
        Vector2Int[] leftFlap =
        {
            frontBottomLeft,
            ScalePoint(3, 34),
            ScalePoint(8, 29)
        };
        Vector2Int[] topFace =
        {
            frontTopLeft,
            backTopLeft,
            backTopRight,
            frontTopRight
        };
        Vector2Int[] rightFace =
        {
            frontTopRight,
            backTopRight,
            backBottomRight,
            frontBottomRight
        };
        Vector2Int[] frontFace =
        {
            frontTopLeft,
            frontTopRight,
            frontBottomRight,
            frontBottomLeft
        };

        FillPolygon(pixels, leftFlap, 'S');
        FillPolygon(pixels, frontFlap, 'H');
        FillPolygon(pixels, rightFlap, 'C');
        FillPolygon(pixels, topFace, 'H');
        FillPolygon(pixels, rightFace, 'S');
        FillPolygon(pixels, frontFace, 'C');

        DrawPolygonOutline(pixels, leftFlap);
        DrawPolygonOutline(pixels, frontFlap);
        DrawPolygonOutline(pixels, rightFlap);
        DrawPolygonOutline(pixels, topFace);
        DrawPolygonOutline(pixels, rightFace);
        DrawPolygonOutline(pixels, frontFace);

        string[] rows = new string[SpriteHeight];
        for (int y = 0; y < SpriteHeight; y++)
        {
            char[] row = new char[SpriteWidth];
            for (int x = 0; x < SpriteWidth; x++)
            {
                row[x] = pixels[x, y];
            }
            rows[y] = new string(row);
        }
        return rows;
    }

    private static Vector2Int ScalePoint(int x, int y)
    {
        return new Vector2Int(x * GeometryScale, y * GeometryScale);
    }

    private static void FillPolygon(
        char[,] pixels,
        Vector2Int[] points,
        char fill)
    {
        for (int y = 0; y < SpriteHeight; y++)
        {
            for (int x = 0; x < SpriteWidth; x++)
            {
                if (IsInsidePolygon(x + 0.5f, y + 0.5f, points))
                {
                    pixels[x, y] = fill;
                }
            }
        }
    }

    private static bool IsInsidePolygon(
        float x,
        float y,
        Vector2Int[] points)
    {
        bool inside = false;
        int previous = points.Length - 1;
        for (int current = 0; current < points.Length; current++)
        {
            Vector2Int a = points[current];
            Vector2Int b = points[previous];
            bool crosses = (a.y > y) != (b.y > y) &&
                x < (float)(b.x - a.x) * (y - a.y) /
                (b.y - a.y) + a.x;
            if (crosses)
            {
                inside = !inside;
            }
            previous = current;
        }
        return inside;
    }

    private static void DrawPolygonOutline(
        char[,] pixels,
        Vector2Int[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            DrawLine(pixels, points[i], points[(i + 1) % points.Length]);
        }
    }

    private static void DrawLine(
        char[,] pixels,
        Vector2Int start,
        Vector2Int end)
    {
        int x = start.x;
        int y = start.y;
        int dx = Mathf.Abs(end.x - start.x);
        int dy = Mathf.Abs(end.y - start.y);
        int stepX = start.x < end.x ? 1 : -1;
        int stepY = start.y < end.y ? 1 : -1;
        int error = dx - dy;

        while (true)
        {
            if (x >= 0 && x < SpriteWidth && y >= 0 && y < SpriteHeight)
            {
                int outlineRadius = Mathf.Max(0, GeometryScale / 2);
                for (int offsetY = -outlineRadius;
                     offsetY <= outlineRadius;
                     offsetY++)
                {
                    for (int offsetX = -outlineRadius;
                         offsetX <= outlineRadius;
                         offsetX++)
                    {
                        int outlineX = x + offsetX;
                        int outlineY = y + offsetY;
                        if (outlineX >= 0 && outlineX < SpriteWidth &&
                            outlineY >= 0 && outlineY < SpriteHeight)
                        {
                            pixels[outlineX, outlineY] = 'O';
                        }
                    }
                }
            }
            if (x == end.x && y == end.y)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError > -dy)
            {
                error -= dy;
                x += stepX;
            }
            if (doubledError < dx)
            {
                error += dx;
                y += stepY;
            }
        }
    }

    public Sprite CreateStandaloneSprite(out Texture2D texture)
    {
        int height = BoxRows.Length;
        int width = BoxRows[0].Length;
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Worn Cardboard Box",
            filterMode = FilterMode.Bilinear,
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
