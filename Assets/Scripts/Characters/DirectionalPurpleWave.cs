using UnityEngine;

/// <summary>
/// A continuous, hand-fed pixel energy wave. The terminal burst is visual only;
/// the original growing contact region and once-per-target damage are preserved.
/// </summary>
[DefaultExecutionOrder(110)]
public sealed class DirectionalPurpleWave : MonoBehaviour
{
    private const float TravelFraction = .85f;
    private PixelEnergyCanvas canvas;
    private BoxCollider2D hitbox;
    private float duration, elapsed, length, width;
    private int lastFrame = -1;
    private bool burstStarted;
    private Vector3 terminal;
    private Color tint;
    private ZeldaCharacterData attackOwner;
    private bool hasOwner;
    private OuterRealmVisitorZeldaCharacterData visitor;
    private Vector3 farBoundary;
    private Transform releaseAudio;

    public void Configure(float seconds, Color color, SpriteRenderer source, BoxCollider2D contact,
        ZeldaCharacterData owner = null)
    {
        canvas?.Dispose();
        duration = Mathf.Max(.05f, seconds); elapsed = 0; tint = color; hitbox = contact;
        attackOwner = owner; hasOwner = owner != null;
        visitor = owner as OuterRealmVisitorZeldaCharacterData;
        lastFrame = -1; burstStarted = false;
        Vector3 forward = transform.up;
        Vector3 near = transform.TransformPoint(new Vector3(0, -.5f, 0));
        Vector3 far = transform.TransformPoint(new Vector3(0, .5f, 0));
        farBoundary = far;
        if (visitor != null) visitor.SetCastProgress(forward, 0, this);
        Vector3 hand = visitor != null
            ? visitor.GetAttackHandPosition(forward) : near;
        length = Mathf.Max(.0625f, Vector3.Dot(far - hand, forward));
        width = Mathf.Max(.125f, transform.TransformVector(Vector3.right).magnitude);
        // The far end is on the original range boundary. Only the transverse
        // visual offset moves to palm height, like the other held attack visuals.
        terminal = hand + forward * length;
        canvas = new PixelEnergyCanvas(transform, "Hand-fed Pixel Energy Wave",
            Mathf.Clamp(Mathf.CeilToInt(width * 16) + 8, 12, 96),
            Mathf.Clamp(Mathf.CeilToInt(length * 16), 1, 256), new Vector2(.5f, 0), tint);
        canvas.Renderer.transform.position = hand;
        canvas.Renderer.transform.rotation = transform.rotation;
        canvas.SetWorldSize(canvas.Width / 16f, length);
        canvas.CopySorting(source);
        source.enabled = false;
        Draw(0);
        PurpleEnergyWaveAudio.PlayRelease(transform, hand, duration * TravelFraction);
        var audio = GetComponentInChildren<PurpleEnergyWaveAudio>();
        releaseAudio = audio != null ? audio.transform : null;
    }

    private void Update()
    {
        // Ordinary death destroys the actor immediately. Keep the distinction
        // between an intentionally ownerless effect and a destroyed caster so
        // its continuous release cannot survive and emit a delayed terminal cue.
        if (hasOwner && attackOwner == null)
        {
            if (hitbox != null) hitbox.enabled = false;
            Destroy(gameObject); return;
        }
        if (hitbox == null || !hitbox.enabled || canvas == null) return;
        elapsed += Time.deltaTime;
        Draw(Mathf.Clamp01(elapsed / duration));
    }

    private void Draw(float progress)
    {
        if (visitor != null) visitor.SetCastProgress(transform.up, progress, this);
        RefreshHandAnchor();
        float front = Mathf.Clamp01(progress / TravelFraction);
        float reach = Mathf.Max(.01f, front);
        hitbox.offset = new Vector2(0, -.5f + reach * .5f);
        hitbox.size = new Vector2(Mathf.Lerp(.25f, 1f, front), reach);

        if (front >= 1 && !burstStarted)
        {
            burstStarted = true;
            var root = new GameObject("Pixel Energy Wave Terminal Burst") { layer = gameObject.layer };
            // Belongs to the attack's scene, but may finish after the .6 s hitbox
            // expires. It has no collider and cannot cause additional damage.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            root.transform.SetPositionAndRotation(terminal, transform.rotation);
            root.AddComponent<DirectionalPurpleWaveBurst>().Configure(tint, width, canvas.Renderer);
            PurpleEnergyWaveAudio.PlayBurst(gameObject.scene, terminal);
        }

        // Animate on a fixed pixel-frame cadence, not through stretched polygons.
        // Two small textures per attack; no per-frame managed allocations/materials.
        int frame = Mathf.FloorToInt(elapsed * 30);
        float opacity = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.85f, 1, progress));
        canvas.Renderer.color = new Color(1, 1, 1, opacity * tint.a);
        if (frame == lastFrame) return;
        lastFrame = frame;
        canvas.Clear();
        int lead = Mathf.Clamp(Mathf.CeilToInt(front * canvas.Height), 1, canvas.Height);
        for (int y = 0; y < lead; y++)
        {
            float along = y / (float)Mathf.Max(1, canvas.Height - 1);
            int ripple = PixelEnergyCanvas.Hash(y / 3, frame / 2) % 3 - 1;
            int center = canvas.Width / 2 + ripple;
            int half = Mathf.Max(2, Mathf.RoundToInt(Mathf.Lerp(.11f, width * .38f, along) * 16));
            half += PixelEnergyCanvas.Hash(y / 2, frame) % 2;
            // Filled all the way back to the palm, with outward-travelling
            // bright bands and stepped, restless violet edges.
            for (int x = -half; x <= half; x++)
            {
                int edge = half - Mathf.Abs(x);
                int stream = (y - frame * 3 + 512) % 11;
                int shade = edge == 0 ? 1 : edge == 1 ? 2 : 3;
                if (Mathf.Abs(x) <= 1 || (stream < 2 && edge > 1)) shade = 4;
                canvas.Put(center + x, y, shade);
            }
            if (y > 3 && (y + frame) % 7 < 2)
            {
                int side = (y / 7 + frame / 3) % 2 == 0 ? -1 : 1;
                canvas.Put(center + side * (half + 2), y, 2);
            }
        }
        canvas.Upload();
    }

    // Follow both the authored reach frames and the visual rig's late bobbing.
    // Only the visible wave changes length; the damage rectangle is untouched.
    private void LateUpdate()
    {
        if (hitbox != null && hitbox.enabled) RefreshHandAnchor();
    }

    private void RefreshHandAnchor()
    {
        if (canvas == null || visitor == null) return;
        Vector3 hand = visitor.GetAttackHandPosition(transform.up);
        length = Mathf.Max(.0625f, Vector3.Dot(farBoundary - hand, transform.up));
        canvas.Renderer.transform.position = hand;
        canvas.SetWorldSize(canvas.Width / 16f, length);
        if (!burstStarted) terminal = hand + transform.up * length;
        if (releaseAudio != null) releaseAudio.position = hand;
    }

    private void OnDisable() { if (visitor != null) visitor.EndCastVisual(this); }
    private void OnDestroy()
    {
        if (visitor != null) visitor.EndCastVisual(this);
        canvas?.Dispose(); canvas = null;
    }
}

/// <summary>A short, detached pixel breakup at the range boundary, never a projectile.</summary>
internal sealed class DirectionalPurpleWaveBurst : MonoBehaviour
{
    private const float Lifetime = .3f;
    private PixelEnergyCanvas canvas;
    private float elapsed;
    private int lastFrame = -1;
    private float alpha;

    public void Configure(Color tint, float width, SpriteRenderer source)
    {
        canvas = new PixelEnergyCanvas(transform, "Pixel Wave Breakup", 28, 28, Vector2.one * .5f, tint);
        float size = Mathf.Max(.5f, width * 1.4f);
        canvas.SetWorldSize(size, size);
        canvas.CopySorting(source);
        alpha = tint.a;
        Draw(0);
        Destroy(gameObject, Lifetime);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        Draw(Mathf.Clamp01(elapsed / Lifetime));
    }

    private void Draw(float progress)
    {
        if (canvas == null) return;
        canvas.Renderer.color = new Color(1, 1, 1, alpha * (1 - progress * progress));
        int frame = Mathf.Min(8, Mathf.FloorToInt(progress * 9));
        if (frame == lastFrame) return;
        lastFrame = frame; canvas.Clear();
        float radius = Mathf.Lerp(3, 10, Mathf.Sqrt(progress));
        for (int y = -13; y <= 13; y++)
        for (int x = -13; x <= 13; x++)
        {
            int noise = PixelEnergyCanvas.Hash((x + 14) / 2, (y + 14) / 2);
            float distance = Mathf.Sqrt(x * x + y * y);
            float edge = radius + (noise % 3 - 1) - distance;
            if (edge < 0 || (progress > .3f && edge > Mathf.Lerp(6, 1, progress))) continue;
            if (progress > .5f && (noise + frame) % 5 == 0) continue;
            int shade = edge < 1 ? 1 : edge < 2 ? 2 : edge < 3 ? 3 : 4;
            canvas.Put(x + 14, y + 14, shade);
        }
        // Eight tiny pixel flecks spread and dissolve after the central flash.
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4 + .18f;
            float distance = 4 + progress * 9;
            int x = 14 + Mathf.RoundToInt(Mathf.Cos(angle) * distance);
            int y = 14 + Mathf.RoundToInt(Mathf.Sin(angle) * distance);
            canvas.Put(x, y, 3);
            if (frame < 6) canvas.Put(x + 1, y, 2);
        }
        canvas.Upload();
    }

    private void OnDestroy() { canvas?.Dispose(); canvas = null; }
}

/// <summary>Small, reusable raster buffer with a discrete four-shade energy palette.</summary>
internal sealed class PixelEnergyCanvas
{
    public readonly int Width, Height;
    public readonly SpriteRenderer Renderer;
    private readonly Texture2D texture;
    private readonly Sprite sprite;
    private readonly Color32[] pixels, palette;

    public PixelEnergyCanvas(Transform parent, string name, int width, int height, Vector2 pivot, Color tint)
    {
        Width = width; Height = height;
        pixels = new Color32[width * height];
        tint.a = 1;
        palette = new Color32[] { Color.clear, Color.Lerp(tint, new Color(.1f, .025f, .2f), .55f),
            tint, Color.Lerp(tint, new Color(.91f, .69f, 1), .65f), new Color(.98f, .9f, 1) };
        texture = new Texture2D(width, height, TextureFormat.RGBA32, false) {
            name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        // Fixed full rectangle: the animation may fill pixels absent on frame 0.
        texture.SetPixels32(pixels); texture.Apply(false, false);
        sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 16, 0, SpriteMeshType.FullRect);
        sprite.name = name; sprite.hideFlags = HideFlags.HideAndDontSave;
        var root = new GameObject(name) { layer = parent.gameObject.layer };
        root.transform.SetParent(parent, false);
        Renderer = root.AddComponent<SpriteRenderer>(); Renderer.sprite = sprite;
    }

    public void CopySorting(SpriteRenderer source)
    {
        Renderer.sortingLayerID = source.sortingLayerID;
        Renderer.sortingOrder = source.sortingOrder;
        Renderer.maskInteraction = source.maskInteraction;
    }

    public void SetWorldSize(float width, float height)
    {
        Vector3 parentScale = Renderer.transform.parent.lossyScale;
        Renderer.transform.localScale = new Vector3(
            width * 16 / Width / Mathf.Max(.0001f, Mathf.Abs(parentScale.x)),
            height * 16 / Height / Mathf.Max(.0001f, Mathf.Abs(parentScale.y)), 1);
    }

    public void Clear() => System.Array.Clear(pixels, 0, pixels.Length);
    public void Put(int x, int y, int shade)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height) pixels[y * Width + x] = palette[shade];
    }
    public void Upload() { texture.SetPixels32(pixels); texture.Apply(false, false); }
    public static int Hash(int x, int y) => unchecked(((x * 73856093) ^ (y * 19349663)) & 0x7fffffff);
    public void Dispose()
    {
        if (Renderer != null) CrystalMesh.Release(Renderer.gameObject);
        CrystalMesh.Release(sprite); CrystalMesh.Release(texture);
    }
}
