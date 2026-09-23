using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class ZeldaDeathExplosionVisual : MonoBehaviour
{
    [SerializeField] private float duration = 0.35f;
    [SerializeField] private float maxScale = 1.4f;
    [SerializeField] private Color explosionTint = new Color(1f, 0.65f, 0.12f, 1f);

    private const int FragmentCount = 8;
    // Extend existing prefab timings without changing their serialized settings.
    private const float DurationMultiplier = 1.35f;
    private struct Fragment
    {
        public SpriteRenderer Renderer;
        public Vector2 Direction, Size;
        public float Reach, Turn, Delay, End, Brightness;
    }
    private readonly Fragment[] fragments = new Fragment[FragmentCount];
    private static Sprite explosionSprite;
    private static Sprite fragmentSprite;
    private SpriteRenderer spriteRenderer;
    private float age;

    private void Awake()
    {
        CreateExplosionSprite();
        GetComponent<SpriteRenderer>().enabled = false;
        spriteRenderer = CreateRenderer("Impact Flash");
        spriteRenderer.sprite = explosionSprite;
        for (int i = 0; i < FragmentCount; i++)
            fragments[i].Renderer = CreateRenderer("Pixel Fracture");
    }

    private SpriteRenderer CreateRenderer(string objectName)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = fragmentSprite;
        renderer.enabled = false;
        return renderer;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = duration <= 0f ? 1f : Mathf.Clamp01(age / duration);

        ApplyFrame(progress);

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    public void Configure(float effectDuration, float effectMaxScale, Color tint,
        int sortingLayerId = 0, int sortingOrder = 4)
    {
        duration = Mathf.Max(0f, effectDuration) * DurationMultiplier;
        maxScale = Mathf.Max(0f, effectMaxScale);
        explosionTint = tint;
        age = 0f;
        if (duration <= 0f || maxScale <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        spriteRenderer.sortingLayerID = sortingLayerId;
        spriteRenderer.sortingOrder = sortingOrder + 1;
        float rotation = Random.Range(0f, 360f);
        for (int i = 0; i < FragmentCount; i++)
        {
            // Uneven sectors keep the burst balanced without identical spokes.
            float angle = (rotation + i * 360f / FragmentCount + Random.Range(-19f, 19f)) * Mathf.Deg2Rad;
            Fragment piece = fragments[i];
            piece.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            piece.Reach = Random.Range(0.46f, 0.78f);
            piece.Size = new Vector2(Random.Range(0.09f, 0.17f), Random.Range(0.045f, 0.075f));
            piece.Turn = Random.Range(-38f, 38f);
            piece.Delay = Random.Range(0f, 0.035f);
            piece.End = Random.Range(0.76f, 1f);
            piece.Brightness = Random.Range(0.06f, 0.4f);
            piece.Renderer.sortingLayerID = sortingLayerId;
            piece.Renderer.sortingOrder = sortingOrder;
            fragments[i] = piece;
        }
        ApplyFrame(0f);
    }

    private void ApplyFrame(float progress)
    {
        // Hold the cross briefly before fading; keep it distinct from a circular bomb blast.
        float flashProgress = Mathf.Clamp01(progress * DurationMultiplier);
        float flash = EvaluateFlashAlpha(flashProgress);
        spriteRenderer.enabled = flash > 0f && maxScale > 0f;
        Color flashColor = Color.Lerp(explosionTint, Color.white, 0.88f);
        flashColor.a = explosionTint.a * flash;
        spriteRenderer.color = flashColor;
        float flashSize = maxScale * EvaluateFlashSize(flashProgress);
        spriteRenderer.transform.localScale = Vector3.one * flashSize;
        for (int i = 0; i < FragmentCount; i++)
        {
            Fragment piece = fragments[i];
            float local = Mathf.Clamp01((progress - piece.Delay) / (piece.End - piece.Delay));
            float fade = EvaluateFragmentAlpha(local);
            piece.Renderer.enabled = progress >= piece.Delay && fade > 0f && maxScale > 0f;
            float travel = EvaluateTravel(local);
            Vector2 side = new Vector2(-piece.Direction.y, piece.Direction.x);
            Vector2 position = piece.Direction * (0.07f + piece.Reach * travel)
                + side * (Mathf.Sin(local * Mathf.PI) * piece.Turn * 0.0012f);
            Transform pieceTransform = piece.Renderer.transform;
            pieceTransform.localPosition = (Vector3)(position * maxScale);
            float angle = Mathf.Atan2(piece.Direction.y, piece.Direction.x) * Mathf.Rad2Deg;
            pieceTransform.localRotation = Quaternion.Euler(0f, 0f, angle + piece.Turn * travel);
            // Chunky short streaks relax to chips, with no continuous trails.
            float stretch = Mathf.Lerp(1.9f, 0.75f, travel);
            float shrink = Mathf.Lerp(1f, 0.55f, local * local);
            pieceTransform.localScale = new Vector3(piece.Size.x * stretch * maxScale, piece.Size.y * shrink * maxScale, 1f);
            Color color = Color.Lerp(explosionTint, Color.white, piece.Brightness + (1f - travel) * 0.4f);
            color.a = explosionTint.a * fade;
            piece.Renderer.color = color;
        }
    }

    // Analytic curves avoid frame-rate-dependent drag/integration in short effects.
    private static float EvaluateTravel(float progress)
    {
        float p = Mathf.Clamp01(progress);
        return (1f - Mathf.Exp(-4.5f * p)) / (1f - Mathf.Exp(-4.5f));
    }

    private static float EvaluateFlashAlpha(float progress)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.16f, 0.48f, progress));
    }

    private static float EvaluateFlashSize(float progress)
    {
        // A small seed grows across most of the visible lifetime instead of jumping outwards.
        // Keep the final footprint and alpha timing unchanged; only reshape the growth curve.
        return Mathf.SmoothStep(0.06f, 0.7f, Mathf.Clamp01(progress / 0.36f));
    }

    private static float EvaluateFragmentAlpha(float progress)
    {
        return 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 1f, progress));
    }

    private static void CreateExplosionSprite()
    {
        if (explosionSprite != null)
        {
            return;
        }

        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Character Pixel Fracture";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        FillRect(texture, 0, 0, size, size, clear);
        FillRect(texture, 7, 1, 2, 14, white);
        FillRect(texture, 1, 7, 14, 2, white);
        FillRect(texture, 4, 4, 8, 8, white);

        texture.Apply(false, true);
        explosionSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            16f, 0, SpriteMeshType.FullRect);
        fragmentSprite = Sprite.Create(texture, new Rect(7, 7, 2, 2), new Vector2(0.5f, 0.5f),
            2f, 0, SpriteMeshType.FullRect);
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
