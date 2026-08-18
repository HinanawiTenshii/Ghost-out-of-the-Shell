using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ZeldaPossessionProgressBar : MonoBehaviour
{
    private const int TextureWidth = 14;
    private const int TextureHeight = 7;
    private const int ProgressBarHeight = 3;
    private const int ProgressSortingOrder = 32760;

    private static readonly Color EmptyColor = Color.white;
    private static readonly Color FillColor = ZeldaUiPalette.Ghost;
    private static readonly Color FailureColor = new Color(1f, 0.08f, 0.06f, 1f);

    private SpriteRenderer spriteRenderer;
    private Texture2D progressTexture;
    private Sprite progressSprite;
    private bool failureActive;
    private float failureTimer;
    private float failureDuration;
    private int failureFlashCount;

    public bool IsFailureActive => failureActive;

    private void Awake()
    {
        EnsureVisual();
        Hide();
    }

    private void Update()
    {
        if (!failureActive)
        {
            return;
        }

        failureTimer += Time.deltaTime;
        if (failureTimer >= failureDuration)
        {
            failureActive = false;
            spriteRenderer.enabled = false;
            return;
        }

        float normalizedTime = failureDuration > 0f ? failureTimer / failureDuration : 1f;
        int flashPhase = Mathf.FloorToInt(normalizedTime * failureFlashCount * 2f);
        spriteRenderer.enabled = flashPhase % 2 == 0;
    }

    public void SetProgress(float progress)
    {
        SetProgress(progress, FillColor);
    }

    public void SetProgress(float progress, Color fillColor)
    {
        EnsureVisual();
        failureActive = false;
        float clampedProgress = Mathf.Clamp01(progress);
        int filledColumns = Mathf.FloorToInt(clampedProgress * TextureWidth);

        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                if (y < ProgressBarHeight)
                {
                    progressTexture.SetPixel(
                        x,
                        y,
                        x < filledColumns ? fillColor : EmptyColor);
                }
                else
                {
                    progressTexture.SetPixel(x, y, new Color(1f, 1f, 1f, 0f));
                }
            }
        }

        progressTexture.Apply(false);
        spriteRenderer.enabled = true;
    }

    public void ShowFailure(float duration, int flashCount)
    {
        EnsureVisual();
        failureDuration = Mathf.Max(0.1f, duration);
        failureFlashCount = Mathf.Max(1, flashCount);
        failureTimer = 0f;
        failureActive = true;

        for (int y = 0; y < TextureHeight; y++)
        {
            for (int x = 0; x < TextureWidth; x++)
            {
                bool isCrossPixel = Mathf.Abs(x - (y + 4)) <= 1 ||
                    Mathf.Abs(x - (10 - y)) <= 1;
                progressTexture.SetPixel(x, y, isCrossPixel ? FailureColor : new Color(1f, 1f, 1f, 0f));
            }
        }

        progressTexture.Apply(false);
        spriteRenderer.enabled = true;
    }

    public void HideProgress()
    {
        if (!failureActive && spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    public void Hide()
    {
        failureActive = false;
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }
    }

    private void EnsureVisual()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = ProgressSortingOrder;
        }

        if (progressTexture != null)
        {
            return;
        }

        progressTexture = new Texture2D(TextureWidth, TextureHeight);
        progressTexture.name = "Zelda Possession Progress";
        progressTexture.filterMode = FilterMode.Point;
        progressTexture.wrapMode = TextureWrapMode.Clamp;
        progressSprite = Sprite.Create(
            progressTexture,
            new Rect(0f, 0f, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0f),
            14f);
        spriteRenderer.sprite = progressSprite;
    }

    private void OnDestroy()
    {
        if (progressSprite != null)
        {
            Destroy(progressSprite);
        }

        if (progressTexture != null)
        {
            Destroy(progressTexture);
        }
    }
}
