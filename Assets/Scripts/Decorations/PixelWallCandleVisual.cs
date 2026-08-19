using UnityEngine;

public enum WallCandleDirection
{
    Up,
    Right,
    Down,
    Left
}

/// <summary>
/// Generates a cold-toned pixel wall candle and a warm animated flame at runtime.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelWallCandleVisual : MonoBehaviour
{
    private const int TextureSize = 24;
    private const float EditorBoundsSize = 1.5f;
    private const string FlameObjectName = "Flame";
    private const string OuterGlowObjectName = "Outer Glow";
    private const string InnerGlowObjectName = "Inner Glow";

    [SerializeField] private WallCandleDirection mountingDirection = WallCandleDirection.Up;
    [SerializeField, Min(0.04f)] private float flameFrameDuration = 0.14f;
    [SerializeField] private int baseSortingOrder = 2;
    [SerializeField, Min(0.1f)] private float glowPulseSpeed = 2.2f;
    [SerializeField, Range(0f, 0.25f)] private float glowPulseAmount = 0.08f;
    [SerializeField] private bool useGoldMount;

    private static readonly Sprite[,] BodySprites = new Sprite[2, 4];
    private static readonly Sprite[,] FlameSprites = new Sprite[4, 2];
    private static Sprite outerGlowSprite;
    private static Sprite innerGlowSprite;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer flameRenderer;
    private SpriteRenderer outerGlowRenderer;
    private SpriteRenderer innerGlowRenderer;
    private float flameTimer;
    private int flameFrame;

    public WallCandleDirection MountingDirection => mountingDirection;

    private void Awake()
    {
        bodyRenderer = GetComponent<SpriteRenderer>();
        flameRenderer = GetOrCreateFlameRenderer();
        outerGlowRenderer = GetOrCreateChildRenderer(OuterGlowObjectName);
        innerGlowRenderer = GetOrCreateChildRenderer(InnerGlowObjectName);
        EnsureSprites();
        ApplyVisual();
    }

    private void Update()
    {
        float glowScale = 1f + Mathf.Sin(Time.time * glowPulseSpeed) * glowPulseAmount;
        outerGlowRenderer.transform.localScale = Vector3.one * glowScale;
        innerGlowRenderer.transform.localScale = Vector3.one * glowScale;

        flameTimer += Time.deltaTime;
        if (flameTimer < flameFrameDuration)
        {
            return;
        }

        flameTimer -= flameFrameDuration;
        flameFrame = 1 - flameFrame;
        flameRenderer.sprite = FlameSprites[(int)mountingDirection, flameFrame];
    }

    public void SetMountingDirection(WallCandleDirection direction)
    {
        mountingDirection = direction;
        if (Application.isPlaying && bodyRenderer != null)
        {
            ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        EnsureSprites();
        int directionIndex = (int)mountingDirection;
        bodyRenderer.sprite = BodySprites[useGoldMount ? 1 : 0, directionIndex];
        bodyRenderer.color = Color.white;
        bodyRenderer.sortingOrder = baseSortingOrder;
        flameRenderer.sprite = FlameSprites[directionIndex, flameFrame];
        flameRenderer.color = Color.white;
        flameRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        flameRenderer.sortingOrder = baseSortingOrder + 1;

        Vector2Int candleAnchor = RotatePoint(12, 10, mountingDirection);
        Vector3 glowPosition = PixelToLocalPosition(candleAnchor.x, candleAnchor.y + 6);
        outerGlowRenderer.transform.localPosition = glowPosition;
        innerGlowRenderer.transform.localPosition = glowPosition;
        outerGlowRenderer.sprite = outerGlowSprite;
        innerGlowRenderer.sprite = innerGlowSprite;
        outerGlowRenderer.color = new Color(1f, 0.48f, 0.08f, 0.28f);
        innerGlowRenderer.color = new Color(1f, 0.82f, 0.24f, 0.14f);
        outerGlowRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        innerGlowRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
        outerGlowRenderer.sortingOrder = baseSortingOrder - 2;
        innerGlowRenderer.sortingOrder = baseSortingOrder - 1;
    }

    private SpriteRenderer GetOrCreateFlameRenderer()
    {
        return GetOrCreateChildRenderer(FlameObjectName);
    }

    private SpriteRenderer GetOrCreateChildRenderer(string objectName)
    {
        Transform child = transform.Find(objectName);
        if (child == null)
        {
            GameObject childObject = new GameObject(objectName);
            childObject.transform.SetParent(transform, false);
            child = childObject.transform;
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        return renderer != null ? renderer : child.gameObject.AddComponent<SpriteRenderer>();
    }

    private void EnsureSprites()
    {
        int bodyVariant = useGoldMount ? 1 : 0;
        if (BodySprites[bodyVariant, 0] == null)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                BodySprites[bodyVariant, direction] =
                    CreateBodySprite((WallCandleDirection)direction, useGoldMount);
            }
        }

        if (FlameSprites[0, 0] == null)
        {
            for (int direction = 0; direction < 4; direction++)
            {
                FlameSprites[direction, 0] = CreateFlameSprite((WallCandleDirection)direction, false);
                FlameSprites[direction, 1] = CreateFlameSprite((WallCandleDirection)direction, true);
            }
        }

        if (outerGlowSprite == null || innerGlowSprite == null)
        {
            outerGlowSprite = CreateGlowSprite(7f, "Wall Candle Outer Glow");
            innerGlowSprite = CreateGlowSprite(4f, "Wall Candle Inner Glow");
        }
    }

    private static Sprite CreateGlowSprite(float radius, string spriteName)
    {
        const int glowSize = 16;
        Texture2D texture = new Texture2D(glowSize, glowSize, TextureFormat.RGBA32, false);
        texture.name = spriteName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((glowSize - 1) * 0.5f, (glowSize - 1) * 0.5f);
        for (int y = 0; y < glowSize; y++)
        for (int x = 0; x < glowSize; x++)
        {
            bool inside = Vector2.Distance(new Vector2(x, y), center) <= radius;
            texture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
        }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, glowSize, glowSize), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = spriteName;
        return sprite;
    }

    private static Vector3 PixelToLocalPosition(int x, int y)
    {
        return new Vector3((x + 0.5f - TextureSize * 0.5f) / 16f, (y + 0.5f - TextureSize * 0.5f) / 16f, 0f);
    }

    private static Sprite CreateBodySprite(WallCandleDirection direction, bool goldMount)
    {
        Texture2D texture = NewTexture("Wall Candle " + direction);
        Color plateDark = goldMount
            ? new Color(0.34f, 0.19f, 0.04f, 1f)
            : new Color(0.08f, 0.14f, 0.23f, 1f);
        Color plateMid = goldMount
            ? new Color(0.62f, 0.39f, 0.07f, 1f)
            : new Color(0.16f, 0.28f, 0.42f, 1f);
        Color metalLight = goldMount
            ? new Color(0.96f, 0.73f, 0.18f, 1f)
            : new Color(0.34f, 0.52f, 0.65f, 1f);
        Color wallPlateBase = goldMount
            ? new Color(0.70f, 0.45f, 0.08f, 1f)
            : new Color(0.24f, 0.40f, 0.55f, 1f);
        Color wallPlateHighlight = goldMount
            ? new Color(1f, 0.82f, 0.28f, 1f)
            : new Color(0.42f, 0.61f, 0.72f, 1f);
        Color waxDark = new Color(0.30f, 0.43f, 0.56f, 1f);
        Color waxLight = new Color(0.58f, 0.72f, 0.80f, 1f);

        // Only the wall plate and projecting bracket rotate with the mounting direction.
        FillRotatedRect(texture, 8, 3, 8, 2, wallPlateBase, direction);
        FillRotatedRect(texture, 9, 4, 6, 1, wallPlateHighlight, direction);
        FillRotatedRect(texture, 10, 5, 4, 2, plateMid, direction);
        FillRotatedRect(texture, 11, 7, 2, 2, plateDark, direction);
        FillRotatedRect(texture, 12, 7, 1, 2, metalLight, direction);

        // The tray and candle remain upright and are repositioned onto the bracket end.
        Vector2Int candleAnchor = RotatePoint(12, 10, direction);
        DrawDirectionalConnection(texture, candleAnchor, direction, plateDark, plateMid, metalLight);
        FillRect(texture, candleAnchor.x - 4, candleAnchor.y - 2, 8, 2, plateMid);
        FillRect(texture, candleAnchor.x - 3, candleAnchor.y - 1, 6, 1, metalLight);
        SetPixel(texture, candleAnchor.x - 4, candleAnchor.y - 1, plateDark);
        SetPixel(texture, candleAnchor.x + 3, candleAnchor.y - 1, plateDark);
        FillRect(texture, candleAnchor.x - 2, candleAnchor.y, 4, 4, waxDark);
        FillRect(texture, candleAnchor.x, candleAnchor.y, 2, 4, waxLight);
        FillRect(texture, candleAnchor.x - 1, candleAnchor.y + 4, 2, 1, plateDark);
        return FinishSprite(texture, "Wall Candle Body " + direction);
    }

    private static void DrawDirectionalConnection(
        Texture2D texture,
        Vector2Int anchor,
        WallCandleDirection direction,
        Color dark,
        Color mid,
        Color light)
    {
        switch (direction)
        {
            case WallCandleDirection.Up:
                // Central tongue with two small diagonal braces.
                FillRect(texture, anchor.x - 1, anchor.y - 5, 2, 3, dark);
                SetPixel(texture, anchor.x - 2, anchor.y - 3, mid);
                SetPixel(texture, anchor.x + 1, anchor.y - 3, mid);
                SetPixel(texture, anchor.x, anchor.y - 4, light);
                break;

            case WallCandleDirection.Right:
                // Side hinge block approached from the right, with twin rivets.
                FillRect(texture, anchor.x + 3, anchor.y - 2, 3, 3, dark);
                FillRect(texture, anchor.x + 3, anchor.y - 1, 2, 1, mid);
                SetPixel(texture, anchor.x + 4, anchor.y - 2, light);
                SetPixel(texture, anchor.x + 4, anchor.y, light);
                break;

            case WallCandleDirection.Down:
                // Raised rear guard and a centered locking pin.
                SetPixel(texture, anchor.x - 4, anchor.y, dark);
                SetPixel(texture, anchor.x + 3, anchor.y, dark);
                FillRect(texture, anchor.x - 4, anchor.y - 1, 2, 1, mid);
                FillRect(texture, anchor.x + 2, anchor.y - 1, 2, 1, mid);
                FillRect(texture, anchor.x - 1, anchor.y - 4, 2, 2, light);
                break;

            case WallCandleDirection.Left:
                // Mirrored clamp with a bright locking edge and one large bolt.
                FillRect(texture, anchor.x - 6, anchor.y - 2, 3, 3, dark);
                FillRect(texture, anchor.x - 5, anchor.y - 1, 2, 1, mid);
                FillRect(texture, anchor.x - 4, anchor.y - 2, 1, 3, light);
                SetPixel(texture, anchor.x - 5, anchor.y - 1, light);
                break;
        }
    }

    private static Sprite CreateFlameSprite(WallCandleDirection direction, bool alternate)
    {
        Texture2D texture = NewTexture("Wall Candle Flame " + direction);
        Color flameOuter = new Color(1f, 0.36f, 0.05f, 0.96f);
        Color flameMid = new Color(1f, 0.72f, 0.08f, 1f);
        Color flameCore = new Color(1f, 0.96f, 0.58f, 1f);
        Vector2Int candleAnchor = RotatePoint(12, 10, direction);
        int centerX = candleAnchor.x + (alternate ? 1 : 0);
        int baseY = candleAnchor.y + 4;
        SetPixel(texture, centerX, baseY, flameOuter);
        FillRect(texture, centerX - 1, baseY + 1, 3, 1, flameOuter);
        FillRect(texture, centerX - 2, baseY + 2, 5, 1, flameOuter);
        FillRect(texture, centerX - 1, baseY + 3, 3, 1, flameMid);
        SetPixel(texture, centerX, baseY + 4, flameOuter);
        SetPixel(texture, centerX, baseY + 1, flameCore);
        FillRect(texture, centerX - 1, baseY + 2, 3, 1, flameMid);
        SetPixel(texture, centerX, baseY + 2, flameCore);
        return FinishSprite(texture, "Wall Candle Flame " + direction + " " + (alternate ? "B" : "A"));
    }

    private static Texture2D NewTexture(string textureName)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = textureName;
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color[] pixels = new Color[TextureSize * TextureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;
        texture.SetPixels(pixels);
        return texture;
    }

    private static void FillRotatedRect(Texture2D texture, int x, int y, int width, int height, Color color, WallCandleDirection direction)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++) SetRotatedPixel(texture, px, py, color, direction);
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++) SetPixel(texture, px, py, color);
    }

    private static void SetPixel(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < TextureSize && y >= 0 && y < TextureSize)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private static Vector2Int RotatePoint(int x, int y, WallCandleDirection direction)
    {
        switch (direction)
        {
            case WallCandleDirection.Right: return new Vector2Int(TextureSize - 1 - y, x);
            case WallCandleDirection.Down: return new Vector2Int(TextureSize - 1 - x, TextureSize - 1 - y);
            case WallCandleDirection.Left: return new Vector2Int(y, TextureSize - 1 - x);
            default: return new Vector2Int(x, y);
        }
    }

    private static void SetRotatedPixel(Texture2D texture, int x, int y, Color color, WallCandleDirection direction)
    {
        Vector2Int target = RotatePoint(x, y, direction);
        SetPixel(texture, target.x, target.y, color);
    }

    private static Sprite FinishSprite(Texture2D texture, string spriteName)
    {
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), 16f);
        sprite.name = spriteName;
        return sprite;
    }

    private void OnValidate()
    {
        flameFrameDuration = Mathf.Max(0.04f, flameFrameDuration);
        glowPulseSpeed = Mathf.Max(0.1f, glowPulseSpeed);
        glowPulseAmount = Mathf.Clamp(glowPulseAmount, 0f, 0.25f);
        if (Application.isPlaying && bodyRenderer != null)
        {
            ApplyVisual();
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(EditorBoundsSize, EditorBoundsSize, 0f));
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
