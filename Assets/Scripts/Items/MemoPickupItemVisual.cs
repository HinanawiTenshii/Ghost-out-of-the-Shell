using UnityEngine;

/// <summary>A memory matrix, visually related to the D.A.T.A. collectible.</summary>
public sealed class MemoPickupItemVisual : PickupItemVisualBase
{
    private static Sprite icon, sphereIcon;
    private GrowthCollectible.IconAppearance appearance;
    private Transform orbit, glow;
    private SpriteRenderer sourceRenderer;
    private SpriteRenderer[] accentRenderers;

    protected override bool UsesEmbeddedColors => true;
    protected override string RuntimeSpriteName => "M.E.M.O. Sphere";
    protected override FilterMode RuntimeTextureFilterMode => FilterMode.Bilinear;

    public override Sprite InventoryIcon
    {
        get
        {
            if (icon != null) return icon;
            return icon = CreateIcon(true);
        }
    }

    // Body only: animated UI supplies the same separate orbit and halo as D.A.T.A.
    public static Sprite SphereIcon => sphereIcon != null ? sphereIcon : (sphereIcon = CreateIcon(false));

    private static Sprite CreateIcon(bool includeOrbit)
    {
        const int size = 128;
        var style = includeOrbit ? GrowthCollectible.GetIconAppearance() : default;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = includeOrbit ? "M.E.M.O. Inventory Icon" : "M.E.M.O. Sphere Icon",
            filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f);
            Color color = SpherePixel(p, 0.027f) != '.' ? Color.white : Color.clear;
            for (int i = 0; includeOrbit && i < style.pointCount && color.a == 0f; i++)
            {
                float angle = i * Mathf.PI * 2f / style.pointCount;
                if ((p - new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * style.radius).sqrMagnitude <
                    style.pointSize * style.pointSize * 0.25f)
                    color = Color.white;
            }
            texture.SetPixel(x, y, color);
        }
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        sprite.name = texture.name;
        return sprite;
    }

    protected override void Awake()
    {
        base.Awake();
        sourceRenderer = GetComponent<SpriteRenderer>();
        appearance = GrowthCollectible.GetIconAppearance();
        orbit = new GameObject("Memory Orbit").transform;
        orbit.SetParent(transform, false);
        accentRenderers = new SpriteRenderer[appearance.pointCount + 1];
        for (int i = 0; i < appearance.pointCount; i++)
        {
            float angle = i * Mathf.PI * 2f / appearance.pointCount;
            var dot = MakeAccent("Memory Point", orbit, appearance.circle, Color.white, sourceRenderer.sortingOrder);
            dot.transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * appearance.radius;
            dot.transform.localScale = Vector3.one * appearance.pointSize;
            accentRenderers[i] = dot;
        }
        var halo = MakeAccent("Memory Glow", transform, appearance.glow,
            new Color(1f, 1f, 1f, appearance.glowOpacity), sourceRenderer.sortingOrder - 1);
        glow = halo.transform;
        accentRenderers[appearance.pointCount] = halo;
        UpdateAccents();
    }

    private SpriteRenderer MakeAccent(string label, Transform parent, Sprite sprite, Color color, int order)
    {
        var go = new GameObject(label);
        go.layer = gameObject.layer;
        go.transform.SetParent(parent, false);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = order;
        return renderer;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        UpdateAccents();
    }

    private void UpdateAccents()
    {
        if (orbit == null) return;
        orbit.Rotate(0f, 0f, Time.unscaledDeltaTime * appearance.rotationSpeed);
        glow.localScale = Vector3.one * (appearance.glowSize *
            (1f + Mathf.Sin(Time.unscaledTime * appearance.pulseSpeed) * appearance.pulseAmount));
        foreach (var renderer in accentRenderers)
        {
            renderer.enabled = sourceRenderer.enabled;
            renderer.maskInteraction = sourceRenderer.maskInteraction;
        }
    }

    protected override string[] GetPixelRows()
    {
        const int size = 128;
        var rows = new string[size];
        for (int y = 0; y < size; y++)
        {
            var row = new char[size];
            for (int x = 0; x < size; x++)
                row[x] = SpherePixel(new Vector2((x + 0.5f) / size * 1.3f - 0.65f, 0.65f - (y + 0.5f) / size * 1.3f), 0.027f);
            rows[y] = new string(row);
        }
        return rows;
    }

    protected override Color GetPixelColor(char pixel) => pixel == '#' || pixel == 'r' ? Color.white : Color.clear;

    private static char SpherePixel(Vector2 p, float width)
    {
        // A round silhouette with curved meridian/equator, not flat cube faces.
        // Keep the central + clear of the spherical wireframe.
        if (OnLine(p, new Vector2(-0.13f, 0f), new Vector2(0.13f, 0f), width * 1.4f) ||
            OnLine(p, new Vector2(0f, -0.13f), new Vector2(0f, 0.13f), width * 1.4f)) return '#';
        if (Mathf.Abs(p.magnitude - 0.55f) <= width) return '#';
        Vector2 tilted = new Vector2(p.x * 0.951057f - p.y * 0.309017f,
            p.x * 0.309017f + p.y * 0.951057f);
        if (p.sqrMagnitude > 0.205f * 0.205f &&
            (OnEllipse(tilted, new Vector2(0.23f, 0.55f), width * 0.8f) ||
             OnEllipse(tilted, new Vector2(0.55f, 0.18f), width * 0.8f))) return 'r';
        return '.';
    }

    private static bool OnEllipse(Vector2 p, Vector2 radius, float width)
    {
        Vector2 gradient = new Vector2(p.x / (radius.x * radius.x), p.y / (radius.y * radius.y));
        return Mathf.Abs(Vector2.Dot(p, gradient) - 1f) <= width * 2f * gradient.magnitude;
    }

    private static bool OnLine(Vector2 p, Vector2 a, Vector2 b, float width)
    {
        Vector2 ab = b - a;
        return (p - a - ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude)).sqrMagnitude <= width * width;
    }
}
