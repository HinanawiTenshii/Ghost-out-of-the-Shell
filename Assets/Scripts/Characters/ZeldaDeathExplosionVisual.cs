using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ZeldaDeathExplosionVisual : MonoBehaviour
{
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float maxScale = 1.4f;
    [SerializeField] private Color explosionTint = new Color(1f, 0.65f, 0.12f, 1f);

    private static Sprite explosionSprite;
    private SpriteRenderer spriteRenderer;
    private float age;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        CreateExplosionSprite();

        spriteRenderer.sprite = explosionSprite;
        spriteRenderer.color = explosionTint;
        spriteRenderer.sortingOrder = 4;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(age / duration);

        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, maxScale, progress);

        Color color = explosionTint;
        color.a = Mathf.Lerp(explosionTint.a, 0f, progress);
        spriteRenderer.color = color;

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    public void Configure(float effectDuration, float effectMaxScale, Color tint)
    {
        duration = effectDuration;
        maxScale = effectMaxScale;
        explosionTint = tint;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = explosionTint;
        }
    }

    private static void CreateExplosionSprite()
    {
        if (explosionSprite != null)
        {
            return;
        }

        const int size = 16;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        FillRect(texture, 0, 0, size, size, clear);
        FillRect(texture, 7, 1, 2, 14, white);
        FillRect(texture, 1, 7, 14, 2, white);
        FillRect(texture, 4, 4, 8, 8, white);

        texture.SetPixel(3, 3, white);
        texture.SetPixel(12, 3, white);
        texture.SetPixel(3, 12, white);
        texture.SetPixel(12, 12, white);

        texture.Apply();
        explosionSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
