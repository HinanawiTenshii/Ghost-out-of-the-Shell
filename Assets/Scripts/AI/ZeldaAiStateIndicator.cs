using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ZeldaAiStateIndicator : MonoBehaviour
{
    private const int TextureWidth = 7;
    private const int TextureHeight = 10;

    private readonly Color clear = new Color(1f, 1f, 1f, 0f);
    private readonly Color suspicionBase = Color.white;
    private readonly Color suspicionFill = new Color(1f, 0.86f, 0.12f, 1f);
    private readonly Color warningBase = new Color(1f, 0.86f, 0.12f, 1f);
    private readonly Color warningFill = new Color(1f, 0.12f, 0.08f, 1f);

    private SpriteRenderer spriteRenderer;
    private Texture2D indicatorTexture;
    private Sprite indicatorSprite;
    private bool componentVisible = true;
    private static Sprite stunnedSprite;
    private bool showingStun;
    private float stunBlinkTime;

    private void Update()
    {
        if (!showingStun || !componentVisible || spriteRenderer == null) return;
        stunBlinkTime = Mathf.Repeat(stunBlinkTime + Time.deltaTime, 0.8f);
        float alpha = Mathf.Lerp(0.3f, 1f, (Mathf.Cos(stunBlinkTime * Mathf.PI * 2f / 0.8f) + 1f) * 0.5f);
        spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
    }

    private void Awake()
    {
        EnsureVisual();
    }

    public void SetComponentVisible(bool isVisible)
    {
        componentVisible = isVisible;
        if (spriteRenderer != null && !isVisible)
        {
            spriteRenderer.enabled = false;
        }
    }

    public void SetState(
        ZeldaAiState state,
        float suspicionProgress,
        float warningProgress,
        bool showHostileIndicator)
    {
        EnsureVisual();

        bool nextShowingStun = state == ZeldaAiState.Stunned;
        if (nextShowingStun != showingStun)
        {
            stunBlinkTime = 0f;
            spriteRenderer.color = Color.white;
        }
        showingStun = nextShowingStun;
        if (!componentVisible || state == ZeldaAiState.Idle || state == ZeldaAiState.Search ||
            state == ZeldaAiState.Recovery ||
            (state == ZeldaAiState.Hostile && !showHostileIndicator))
        {
            spriteRenderer.enabled = false;
            return;
        }

        spriteRenderer.enabled = true;
        if (state == ZeldaAiState.Stunned)
        {
            spriteRenderer.sprite = GetStunnedSprite();
            return;
        }
        spriteRenderer.sprite = indicatorSprite;
        bool questionMark = state == ZeldaAiState.Suspicious;
        float fillProgress = questionMark ? suspicionProgress : warningProgress;
        Color baseColor = questionMark ? suspicionBase : warningBase;
        Color fillColor = state == ZeldaAiState.Hostile ? warningFill :
            (questionMark ? suspicionFill : warningFill);

        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                bool isIconPixel = questionMark ? IsQuestionMarkPixel(x, y) : IsExclamationMarkPixel(x, y);
                if (!isIconPixel)
                {
                    indicatorTexture.SetPixel(x, y, clear);
                    continue;
                }

                float pixelHeight = (y + 1f) / TextureHeight;
                Color pixelColor = state == ZeldaAiState.Hostile || pixelHeight <= fillProgress
                    ? fillColor
                    : baseColor;
                indicatorTexture.SetPixel(x, y, pixelColor);
            }
        }

        indicatorTexture.Apply(false);
    }

    private static Sprite GetStunnedSprite()
    {
        if (stunnedSprite != null) return stunnedSprite;
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "AI Stunned Double Stars";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        Vector2[] upper = StarVertices(new Vector2(22f, 42f), 21f);
        Vector2[] lower = StarVertices(new Vector2(44f, 21f), 19f);
        Color gold = new Color32(213, 201, 140, 255);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            int coverage = 0;
            for (int sy = 0; sy < 2; sy++) for (int sx = 0; sx < 2; sx++)
            {
                var point = new Vector2(x + (sx + 0.5f) / 2f, y + (sy + 0.5f) / 2f);
                if (InsideStar(point, upper) || InsideStar(point, lower)) coverage++;
            }
            pixels[y * size + x] = new Color(gold.r, gold.g, gold.b, coverage / 4f);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        // Lower the double-star artwork within the shared state-indicator anchor.
        stunnedSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size / 0.75f);
        return stunnedSprite;
    }

    private static Vector2[] StarVertices(Vector2 center, float radius)
    {
        var vertices = new Vector2[10];
        for (int i = 0; i < vertices.Length; i++)
        {
            float angle = (90f + i * 36f) * Mathf.Deg2Rad;
            vertices[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (i % 2 == 0 ? 1f : 0.42f);
        }
        return vertices;
    }

    private static bool InsideStar(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
        {
            Vector2 a = vertices[i], b = vertices[j];
            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
        }
        return inside;
    }

    private void EnsureVisual()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 20;
        }

        if (indicatorTexture != null)
        {
            return;
        }

        indicatorTexture = new Texture2D(TextureWidth, TextureHeight);
        indicatorTexture.name = "Zelda AI State Indicator";
        indicatorTexture.filterMode = FilterMode.Point;
        indicatorTexture.wrapMode = TextureWrapMode.Clamp;
        indicatorSprite = Sprite.Create(
            indicatorTexture,
            new Rect(0f, 0f, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0f),
            10f);
        spriteRenderer.sprite = indicatorSprite;
    }

    private static bool IsQuestionMarkPixel(int x, int y)
    {
        return (y == 8 && x >= 2 && x <= 4) ||
               (y == 7 && (x == 1 || x == 5)) ||
               (y == 6 && x == 5) ||
               (y == 5 && x == 4) ||
               (y == 4 && x == 3) ||
               (y == 3 && x == 3) ||
               (y == 1 && x == 3);
    }

    private static bool IsExclamationMarkPixel(int x, int y)
    {
        return (x == 3 && y >= 3 && y <= 8) || (x == 3 && y == 1);
    }

    private void OnDestroy()
    {
        if (indicatorSprite != null)
        {
            Destroy(indicatorSprite);
        }

        if (indicatorTexture != null)
        {
            Destroy(indicatorTexture);
        }
    }
}
