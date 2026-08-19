using UnityEngine;

/// <summary>Expanding pixel ring used to visualize an AI investigation radius.</summary>
public sealed class ZeldaInvestigationPulse : MonoBehaviour
{
    private const int TextureSize = 64;
    private static Sprite ringSprite;

    private SpriteRenderer spriteRenderer;
    private float maximumRadius;
    private float duration;
    private float elapsed;
    private Color baseColor;

    public void Configure(
        float radius,
        float fadeDuration,
        Color color,
        int sortingLayerId,
        int sortingOrder)
    {
        EnsureRingSprite();
        maximumRadius = Mathf.Max(0f, radius);
        duration = Mathf.Max(0.05f, fadeDuration);
        baseColor = color;

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = ringSprite;
        spriteRenderer.color = baseColor;
        spriteRenderer.sortingLayerID = sortingLayerId;
        spriteRenderer.sortingOrder = sortingOrder;
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float easedProgress = 1f - (1f - progress) * (1f - progress);
        float diameter = maximumRadius * 2f * easedProgress;
        transform.localScale = new Vector3(diameter, diameter, 1f);
        spriteRenderer.color = new Color(
            baseColor.r,
            baseColor.g,
            baseColor.b,
            baseColor.a * (1f - progress));

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private static void EnsureRingSprite()
    {
        if (ringSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.name = "Runtime Investigation Ring";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Vector2 center = new Vector2((TextureSize - 1) * 0.5f, (TextureSize - 1) * 0.5f);
        float outerRadius = TextureSize * 0.48f;
        float innerRadius = outerRadius - 1.5f;
        float outerSquared = outerRadius * outerRadius;
        float innerSquared = innerRadius * innerRadius;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                float distanceSquared = ((Vector2)new Vector2(x, y) - center).sqrMagnitude;
                bool isRing = distanceSquared <= outerSquared && distanceSquared >= innerSquared;
                texture.SetPixel(x, y, isRing ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, true);
        ringSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        ringSprite.name = "Runtime Investigation Ring";
    }
}
