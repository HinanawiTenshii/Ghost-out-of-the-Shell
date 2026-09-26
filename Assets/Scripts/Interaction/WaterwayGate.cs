using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Top-down sluice gate: a steel shutter lifts into a fixed stone/metal frame.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Interaction/Waterway Gate")]
public sealed class WaterwayGate : MonoBehaviour
{
    public enum Letter { A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z }

    [Header("Identifier / 中央编号")]
    [Range(0, 9), SerializeField] private int number = 1;
    [SerializeField] private Letter letter = Letter.A;
    [SerializeField] private Font numberFont;
    [Header("Geometry / 几何尺寸")]
    [Min(1.6f), SerializeField] private float passageWidth = 3.4f;
    [SerializeField] private BoxCollider2D leftPost;
    [SerializeField] private BoxCollider2D rightPost;
    [SerializeField] private Material surfaceMaterial;
    [SerializeField] private int sortingOrder = 10;
    [Header("Mechanism / 机关控制")]
    [Tooltip("Initial state; use SetOpen or SetConditionSatisfied for runtime control.")]
    [SerializeField] private bool open;
    [Min(0.05f), SerializeField] private float moveDuration = 0.65f;
    [Header("Completion Events / 动画完成事件")]
    [SerializeField] private UnityEvent onOpened = new UnityEvent();
    [SerializeField] private UnityEvent onClosed = new UnityEvent();

    private const float Depth = 0.72f;
    private const float PostWidth = 0.42f;
    private const float PostDepth = 1.12f;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private GameObject generated;
    private Transform shutter;
    private Transform sign;
    private Mesh shutterMesh;
    private List<Panel> shutterPanels;
    private Vector3[] shutterVertices;
    private float lastLift = float.NaN;
    private BoxCollider2D barrier;
    private Material ownedMaterial;
    private float progress;
    private bool initialized;
    private bool dirty = true;

    public string Identifier => FormatIdentifier(number, letter);
    public bool IsOpenRequested => open;
    public bool IsFullyOpen => progress >= 1f;
    public bool IsFullyClosed => progress <= 0f;
    public bool IsMoving => !Mathf.Approximately(progress, open ? 1f : 0f);

    public static string FormatIdentifier(int digit, Letter suffix)
    {
        return Mathf.Clamp(digit, 0, 9).ToString(System.Globalization.CultureInfo.InvariantCulture)
            + (char)('A' + Mathf.Clamp((int)suffix, 0, 25));
    }

    [ContextMenu("Open Gate / 开启")]
    public void Open() => SetOpen(true);
    [ContextMenu("Close Gate / 关闭")]
    public void Close() => SetOpen(false);
    public void ToggleFromExternal() => SetOpen(!open);
    public void SetConditionSatisfied(bool satisfied) => SetOpen(satisfied);

    public void SetOpen(bool value)
    {
        open = value;
        // Close the physical passage before the first frame of closing animation.
        // Opening only releases it once the shutter has completely lifted.
        ApplyCollision();
        if (!Application.isPlaying) SetOpenImmediately(value);
    }

    /// <summary>Initialization/restore hook; does not fire completion events.</summary>
    public void SetOpenImmediately(bool value)
    {
        open = value;
        progress = value ? 1f : 0f;
        initialized = true;
        ApplyPose();
        ApplyCollision();
    }

    private void OnEnable()
    {
        barrier = GetComponent<BoxCollider2D>();
        if (!initialized || !Application.isPlaying)
        {
            progress = open ? 1f : 0f;
            initialized = true;
        }
        dirty = true;
        ApplyCollision();
    }

    private void OnValidate()
    {
        number = Mathf.Clamp(number, 0, 9);
        letter = (Letter)Mathf.Clamp((int)letter, 0, 25);
        passageWidth = Mathf.Max(1.6f, passageWidth);
        moveDuration = Mathf.Max(0.05f, moveDuration);
        // Import-thread safe: rebuild graphics and colliders on the main thread.
        dirty = true;
    }

    private void Update()
    {
        if (dirty || generated == null) Rebuild();
        float previous = progress;
        float target = open ? 1f : 0f;
        progress = Application.isPlaying
            ? Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.05f, moveDuration))
            : target;
        ApplyPose();
        ApplyCollision();
        if (Application.isPlaying && !Mathf.Approximately(previous, progress)
            && Mathf.Approximately(progress, target))
        {
            if (open) onOpened.Invoke(); else onClosed.Invoke();
        }
    }

    private void ApplyPose()
    {
        float amount = Mathf.SmoothStep(0f, 1f, progress);
        float lift = (Depth + 0.02f) * amount;
        if (shutter != null)
        {
            // The rigid panel translates into the housing. Only its hidden portion
            // is clipped; ribs and trim keep their original dimensions/spacing.
            shutter.localScale = new Vector3(1f, 1f, 1f);
            shutter.localPosition = new Vector3(0f, lift, 0f);
        }
        UpdateShutterClip(lift);
        // Identifier stays on the fixed housing and remains readable when open.
        if (sign != null) sign.localPosition = new Vector3(0f, 0.44f, 0f);
    }

    private static Panel ClipShutterPanel(Panel part, float lift)
    {
        float bottom = Mathf.Max(part.Y - part.Height * 0.5f, -Depth * 0.5f - lift);
        float top = Mathf.Min(part.Y + part.Height * 0.5f, Depth * 0.5f - lift);
        float height = Mathf.Max(0f, top - bottom);
        return new Panel(part.X, top - height * 0.5f, part.Width, height, part.Color);
    }

    private void UpdateShutterClip(float lift)
    {
        if (shutterMesh == null || shutterVertices == null || Mathf.Approximately(lastLift, lift)) return;
        lastLift = lift;
        for (int i = 0; i < shutterPanels.Count; i++)
        {
            Panel part = ClipShutterPanel(shutterPanels[i], lift);
            int first = i * 4;
            float x = part.X, y = part.Y, w = part.Width * 0.5f, h = part.Height * 0.5f;
            float z = -first * 0.0001f;
            shutterVertices[first] = new Vector3(x-w, y-h, z);
            shutterVertices[first+1] = new Vector3(x-w, y+h, z);
            shutterVertices[first+2] = new Vector3(x+w, y+h, z);
            shutterVertices[first+3] = new Vector3(x+w, y-h, z);
        }
        shutterMesh.vertices = shutterVertices;
        shutterMesh.RecalculateBounds();
    }

    private void ApplyCollision()
    {
        if (barrier == null) barrier = GetComponent<BoxCollider2D>();
        if (barrier == null) return;
        bool blocked = !open || progress < 1f;
        bool changed = barrier.enabled != blocked;
        barrier.enabled = blocked;
        if (changed && Application.isPlaying)
        {
            Physics2D.SyncTransforms();
            CameraCircularVision.NotifyBlockersChanged();
        }
    }

    private void Rebuild()
    {
        ReleaseVisual();
        dirty = false;
        float width = Mathf.Max(1.6f, passageWidth);
        barrier = GetComponent<BoxCollider2D>();
        barrier.size = new Vector2(width, Depth);
        barrier.offset = Vector2.zero;
        barrier.isTrigger = false;
        ConfigurePost(leftPost, -(width + PostWidth) * 0.5f);
        ConfigurePost(rightPost, (width + PostWidth) * 0.5f);

        Material material = surfaceMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        generated = new GameObject("Generated Waterway Gate")
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generated.transform.SetParent(transform, false);
        // Housing renders above the moving leaf, covering the entry slot.
        DrawParts("Frame", BuildFrame(width), material, sortingOrder + 2);
        shutterPanels = BuildShutter(width);
        shutter = DrawParts("Shutter", shutterPanels, material, sortingOrder + 1);
        shutterMesh = shutter.GetComponent<MeshFilter>().sharedMesh;
        shutterMesh.MarkDynamic();
        shutterVertices = new Vector3[shutterPanels.Count * 4];
        sign = DrawParts("Number Plate", new List<Panel> {
            new Panel(0f, 0f, 0.84f, 0.24f, new Color(0.24f, 0.27f, 0.28f)),
            new Panel(0f, 0f, 0.70f, 0.18f, new Color(0.11f, 0.15f, 0.18f))
        }, material, sortingOrder + 3);
        if (numberFont != null)
        {
            var textObject = new GameObject("Identifier", typeof(TextMesh));
            textObject.hideFlags = HideFlags.HideAndDontSave;
            textObject.layer = gameObject.layer;
            textObject.transform.SetParent(sign, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            TextMesh text = textObject.GetComponent<TextMesh>();
            text.font = numberFont;
            text.fontSize = 48;
            text.characterSize = 0.065f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.88f, 0.93f, 0.90f, 1f);
            text.text = Identifier;
            MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = numberFont.material;
            renderer.sortingOrder = sortingOrder + 4;
        }
        ApplyPose();
        ApplyCollision();
        if (Application.isPlaying) CameraCircularVision.NotifyBlockersChanged();
    }

    private static void ConfigurePost(BoxCollider2D post, float x)
    {
        if (post == null) return;
        post.transform.localPosition = new Vector3(x, 0f, 0f);
        post.size = new Vector2(PostWidth, PostDepth);
        post.offset = Vector2.zero;
        post.isTrigger = false;
    }

    private struct Panel
    {
        public float X, Y, Width, Height;
        public Color Color;
        public Panel(float x, float y, float width, float height, Color color)
        { X = x; Y = y; Width = width; Height = height; Color = color; }
    }

    private static List<Panel> BuildFrame(float width)
    {
        Color shadow = new Color(0.29f, 0.32f, 0.33f);
        Color stone = new Color(0.62f, 0.65f, 0.65f);
        Color light = new Color(0.78f, 0.80f, 0.78f);
        var parts = new List<Panel> {
            new Panel(0, 0.43f, width, 0.26f, shadow),
            new Panel(0, 0.46f, width, 0.18f, stone),
            new Panel(0, 0.53f, width, 0.05f, light),
            new Panel(0, 0.32f, width - 0.08f, 0.04f, new Color(0.12f, 0.17f, 0.19f))
        };
        foreach (float side in new[] { -1f, 1f })
        {
            float x = side * (width + PostWidth) * 0.5f;
            parts.Add(new Panel(x, 0, PostWidth, PostDepth, shadow));
            parts.Add(new Panel(x, 0.035f, 0.32f, 0.97f, stone));
            parts.Add(new Panel(x, 0.43f, 0.32f, 0.12f, light));
            parts.Add(new Panel(x, -0.40f, 0.32f, 0.08f, light));
            parts.Add(new Panel(x, 0.15f, 0.18f, 0.26f, shadow));
            parts.Add(new Panel(x, 0.15f, 0.10f, 0.18f, new Color(0.74f, 0.10f, 0.065f)));
        }
        return parts;
    }

    private static List<Panel> BuildShutter(float width)
    {
        Color dark = new Color(0.25f, 0.29f, 0.31f);
        Color metal = new Color(0.50f, 0.57f, 0.59f);
        // Plain plate and narrow rim; keep the rigid slide/clipping animation.
        return new List<Panel> {
            new Panel(0, 0, width, Depth, dark),
            new Panel(0, 0, width - 0.12f, Depth - 0.12f, metal)
        };
    }

    private Transform DrawParts(string name, List<Panel> parts, Material material, int order)
    {
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        foreach (Panel part in parts)
        {
            int first = vertices.Count;
            float x = part.X, y = part.Y, w = part.Width * 0.5f, h = part.Height * 0.5f;
            float z = -first * 0.0001f;
            vertices.Add(new Vector3(x-w, y-h, z)); vertices.Add(new Vector3(x-w, y+h, z));
            vertices.Add(new Vector3(x+w, y+h, z)); vertices.Add(new Vector3(x+w, y-h, z));
            for (int i = 0; i < 4; i++) colors.Add(part.Color);
            triangles.AddRange(new[] { first, first+1, first+2, first, first+2, first+3 });
        }
        var mesh = new Mesh { name = "Waterway Gate " + name, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds(); meshes.Add(mesh);
        var partObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        partObject.transform.SetParent(generated.transform, false);
        partObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = partObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return partObject.transform;
    }

    private void OnDisable()
    {
        if (generated != null) generated.SetActive(false);
        if (Application.isPlaying) CameraCircularVision.NotifyBlockersChanged();
    }
    private void OnDestroy() { ReleaseVisual(); }
    private void ReleaseVisual()
    {
        if (generated != null) generated.SetActive(false);
        Release(generated); generated = null; shutter = null; sign = null;
        shutterMesh = null; shutterPanels = null; shutterVertices = null; lastLift = float.NaN;
        foreach (Mesh mesh in meshes) Release(mesh);
        meshes.Clear(); Release(ownedMaterial); ownedMaterial = null;
    }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
