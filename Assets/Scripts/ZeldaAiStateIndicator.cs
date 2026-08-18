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

        if (!componentVisible || state == ZeldaAiState.Idle || state == ZeldaAiState.Search ||
            state == ZeldaAiState.Recovery ||
            (state == ZeldaAiState.Hostile && !showHostileIndicator))
        {
            spriteRenderer.enabled = false;
            return;
        }

        spriteRenderer.enabled = true;
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
