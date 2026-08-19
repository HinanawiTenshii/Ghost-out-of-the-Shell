using System.Collections.Generic;
using UnityEngine;

/// <summary>Short configurable blast and collision-free white debris used by placed bombs.</summary>
public sealed class BombExplosionVisual : MonoBehaviour
{
    private sealed class Particle
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 Velocity;
        public float Spin;
    }

    private static Sprite circleSprite;
    private static Sprite particleSprite;

    private readonly List<Particle> particles = new List<Particle>();
    private SpriteRenderer outerBlast;
    private SpriteRenderer innerBlast;
    private float duration;
    private float elapsed;
    private Vector2 blastSize;
    private Color blastColor;

    public void Configure(
        Vector2 size,
        float visualDuration,
        Color color,
        int sortingLayerId,
        int sortingOrder,
        int whiteParticleCount = 9)
    {
        EnsureSprites();
        duration = Mathf.Max(0.08f, visualDuration);
        blastSize = new Vector2(Mathf.Max(0.05f, size.x), Mathf.Max(0.05f, size.y));
        blastColor = new Color(color.r, color.g, color.b, Mathf.Max(0.65f, color.a));

        outerBlast = CreateBlastRenderer(
            "Outer Blast",
            new Color(
                blastColor.r * 0.72f,
                blastColor.g * 0.72f,
                blastColor.b * 0.72f,
                blastColor.a),
            sortingLayerId,
            sortingOrder);
        innerBlast = CreateBlastRenderer(
            "Inner Blast",
            Color.Lerp(blastColor, Color.white, 0.25f),
            sortingLayerId,
            sortingOrder + 1);

        int count = Mathf.Clamp(whiteParticleCount, 4, 20);
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.18f, 0.18f);
            float speed = Random.Range(1.8f, 3.4f);
            GameObject particleObject = new GameObject("White Explosion Particle");
            particleObject.transform.SetParent(transform, false);
            SpriteRenderer renderer = particleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = particleSprite;
            renderer.color = Color.white;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + 2;
            float scale = Random.Range(0.075f, 0.14f);
            particleObject.transform.localScale = Vector3.one * scale;
            particles.Add(new Particle
            {
                Transform = particleObject.transform,
                Renderer = renderer,
                Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                Spin = Random.Range(-480f, 480f)
            });
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(elapsed / duration);
        float burst = 1f - Mathf.Pow(1f - progress, 3f);

        SetBlastState(outerBlast, blastSize * (0.2f + burst * 0.8f), 1f - progress);
        SetBlastState(innerBlast, blastSize * (0.12f + burst * 0.46f), 1f - progress);

        float particleFade = 1f - progress;
        for (int i = 0; i < particles.Count; i++)
        {
            Particle particle = particles[i];
            particle.Transform.position += (Vector3)(particle.Velocity * Time.deltaTime);
            particle.Velocity *= Mathf.Pow(0.12f, Time.deltaTime);
            particle.Transform.Rotate(0f, 0f, particle.Spin * Time.deltaTime);
            particle.Renderer.color = new Color(1f, 1f, 1f, particleFade);
        }

        if (progress >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private SpriteRenderer CreateBlastRenderer(
        string objectName,
        Color color,
        int sortingLayerId,
        int sortingOrder)
    {
        GameObject blastObject = new GameObject(objectName);
        blastObject.transform.SetParent(transform, false);
        SpriteRenderer renderer = blastObject.AddComponent<SpriteRenderer>();
        renderer.sprite = circleSprite;
        renderer.color = color;
        renderer.sortingLayerID = sortingLayerId;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private static void SetBlastState(SpriteRenderer renderer, Vector2 size, float alpha)
    {
        renderer.transform.localScale = new Vector3(size.x, size.y, 1f);
        Color color = renderer.color;
        color.a = alpha;
        renderer.color = color;
    }

    private static void EnsureSprites()
    {
        if (circleSprite != null)
        {
            return;
        }

        circleSprite = CreateCircleSprite(32);
        Texture2D particleTexture = new Texture2D(3, 3, TextureFormat.RGBA32, false);
        particleTexture.name = "Runtime Bomb Particle";
        particleTexture.filterMode = FilterMode.Point;
        particleTexture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                particleTexture.SetPixel(x, y, Color.white);
            }
        }
        particleTexture.Apply(false, true);
        particleSprite = Sprite.Create(
            particleTexture,
            new Rect(0f, 0f, 3f, 3f),
            new Vector2(0.5f, 0.5f),
            3f);
        particleSprite.name = "Runtime Bomb White Particle";
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Bomb Explosion";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float radius = size * 0.48f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalized = Vector2.Distance(new Vector2(x, y), center) / radius;
                float alpha = Mathf.Clamp01((1f - normalized) * 4f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size);
        sprite.name = "Runtime Bomb Explosion";
        return sprite;
    }
}
