using UnityEngine;

/// <summary>
/// North-bank origin; extends along local -Y. Lever parity is the saved state,
/// so either lever toggles the bridge, including after a save/load or scene return.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Interaction/Retractable Bridge Mechanism")]
public sealed class RetractableBridgeMechanism : MonoBehaviour
{
    [Header("Controls (either lever toggles the bridge)")]
    [SerializeField] private LeverData firstLever;
    [SerializeField] private LeverData secondLever;
    [Header("Bridge (origin at the upper bank)")]
    [Min(.5f), SerializeField] private float width = 2f;
    [Min(.5f), SerializeField] private float length = 2.6f;
    [Min(.1f), SerializeField] private float moveDuration = 1.2f;
    [SerializeField] private int sortingOrder = -5;
    [SerializeField] private Color deckColor = new Color(.48f, .35f, .24f, 1f);
    [SerializeField] private Color metalColor = new Color(.46f, .5f, .5f, 1f);

    private BoxCollider2D waterBarrier;
    private bool requestedOpen, refresh = true, restorePending;
    private float progress, nextOccupancyCheck;
    private bool occupied;
    private GameObject visual;
    private Mesh mesh;
    private Material material;
    private const int MaxQuads = 64;
    private readonly Vector3[] vertices = new Vector3[MaxQuads * 4];
    private readonly Color[] colors = new Color[MaxQuads * 4];
    private readonly int[] triangles = new int[MaxQuads * 6];
    private readonly Collider2D[] occupants = new Collider2D[64];
    private int quadCount;

    public bool IsOpenRequested => requestedOpen;
    public bool IsFullyOpen => progress >= 1f;
    public bool IsFullyClosed => progress <= 0f;
    public bool IsWaitingForClearance => !requestedOpen && IsFullyOpen && occupied;

    private bool ReadLeverState() => (firstLever != null && firstLever.IsOn) ^
        (secondLever != null && secondLever != firstLever && secondLever.IsOn);

    private void OnEnable()
    {
        waterBarrier = GetComponent<BoxCollider2D>();
        requestedOpen = ReadLeverState();
        progress = requestedOpen ? 1f : 0f;
        refresh = true;
        restorePending = Application.IsPlaying(gameObject);
        if (firstLever != null) firstLever.SignalEmitted += OnLeverUsed;
        if (secondLever != null && secondLever != firstLever) secondLever.SignalEmitted += OnLeverUsed;
    }

    private void OnValidate()
    {
        width = Mathf.Max(.5f, width);
        length = Mathf.Max(.5f, length);
        moveDuration = Mathf.Max(.1f, moveDuration);
        refresh = true;
    }

    private void OnLeverUsed(LeverData source, bool value)
    {
        requestedOpen = ReadLeverState();
        nextOccupancyCheck = 0f;
        restorePending = false;
    }

    private void Update()
    {
        if (waterBarrier == null) waterBarrier = GetComponent<BoxCollider2D>();
        if (visual == null) CreateVisual();
        if (!Application.IsPlaying(gameObject))
        {
            progress = 0f;
            requestedOpen = false;
            if (refresh) ApplyState();
            return;
        }

        var travel = SceneTravelStateManager.Instance;
        if (GameSaveSystem.IsLoading || (travel != null && travel.IsSceneTravelRestoreInProgress))
        {
            restorePending = true;
            return;
        }

        // ApplyPersistentState deliberately does not emit lever signals. Restore
        // the endpoint directly instead of replaying an animation on every load.
        bool savedOpen = ReadLeverState();
        if (restorePending || savedOpen != requestedOpen)
        {
            restorePending = false;
            requestedOpen = savedOpen;
            occupied = HasCharacterOnBridge();
            progress = savedOpen || occupied ? 1f : 0f;
            refresh = true;
            nextOccupancyCheck = 0f;
        }
        Step(Time.deltaTime);
    }

    private void Step(float deltaTime)
    {
        if (!requestedOpen && IsFullyOpen)
        {
            // Check only while a retraction is pending, at most ten times/sec.
            if (Time.unscaledTime >= nextOccupancyCheck)
            {
                occupied = HasCharacterOnBridge();
                nextOccupancyCheck = Time.unscaledTime + .1f;
            }
            if (occupied) { if (refresh) ApplyState(); return; }
        }
        else occupied = false;
        float before = progress;
        progress = Mathf.MoveTowards(progress, requestedOpen ? 1f : 0f,
            Mathf.Max(0f, deltaTime) / moveDuration);
        if (refresh || before != progress) ApplyState();
    }

    private bool HasCharacterOnBridge()
    {
        Vector2 center = transform.TransformPoint(new Vector3(0f, -length * .5f, 0f));
        Vector3 scale = transform.lossyScale;
        Vector2 size = new Vector2(width * Mathf.Abs(scale.x) + .16f,
            length * Mathf.Abs(scale.y) + .16f);
        int count = Physics2D.OverlapBoxNonAlloc(center, size, transform.eulerAngles.z, occupants);
        // A full buffer is conservatively considered occupied, never close on a crowd.
        if (count == occupants.Length) return true;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = occupants[i];
            occupants[i] = null;
            if (hit != null && !hit.isTrigger && hit.GetComponentInParent<ZeldaCharacterData>() != null)
                return true;
        }
        return false;
    }

    private void ApplyState()
    {
        // The entire water gap remains solid until the deck reaches the far bank.
        bool blocking = !IsFullyOpen;
        bool collisionChanged = waterBarrier.enabled != blocking;
        waterBarrier.isTrigger = false;
        waterBarrier.offset = new Vector2(0f, -length * .5f);
        waterBarrier.size = new Vector2(width, length);
        waterBarrier.enabled = blocking;
        if (collisionChanged && Application.IsPlaying(gameObject))
        {
            Physics2D.SyncTransforms();
            var bounds = new Bounds(transform.TransformPoint(waterBarrier.offset),
                new Vector3(width * Mathf.Abs(transform.lossyScale.x), length * Mathf.Abs(transform.lossyScale.y), 1f));
            CameraCircularVision.NotifyBlockersChanged(bounds);
        }
        if (mesh != null) RebuildDeck();
        refresh = false;
    }

    private void CreateVisual()
    {
        visual = new GameObject("Bridge Deck (Generated)") { hideFlags = HideFlags.HideAndDontSave };
        visual.transform.SetParent(transform, false);
        visual.layer = gameObject.layer;
        mesh = new Mesh { name = "Retractable bridge flat vector deck", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        material.mainTexture = Texture2D.whiteTexture;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        for (int i = 0; i < MaxQuads; i++)
        {
            int v = i * 4, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        refresh = true;
    }

    private void RebuildDeck()
    {
        quadCount = 0;
        float half = width * .5f;
        float reach = length * Mathf.SmoothStep(0f, 1f, progress);
        Color outline = new Color(.16f, .20f, .23f, 1f);
        if (reach > .001f)
        {
            Rect(-half, -reach, width, reach, outline);
            Rect(-half + .1f, -reach, width - .2f, reach, deckColor);
            Rect(-half + .04f, -reach, .08f, reach, metalColor);
            Rect(half - .12f, -reach, .08f, reach, metalColor);
            // Reveal planks successively from the upper bank, rather than stretching their spacing.
            float plankSpacing = Mathf.Max(.32f, length / 48f);
            for (float y = plankSpacing; y < reach - .04f; y += plankSpacing)
                Rect(-half + .13f, -y, width - .26f, .025f, outline);
            Rect(-half, -reach, width, Mathf.Min(.09f, reach), metalColor);
        }
        // Small housing sits entirely on the north bank, never a false road over water.
        Rect(-half - .16f, 0f, width + .32f, .26f, outline);
        Rect(-half - .1f, .055f, width + .2f, .15f, metalColor);
        Rect(-half + .12f, .09f, width - .24f, .07f, outline);
        for (int i = quadCount * 4; i < vertices.Length; i++) { vertices[i] = Vector3.zero; colors[i] = Color.clear; }
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        visual.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;
    }

    private void Rect(float x, float y, float w, float h, Color color)
    {
        if (quadCount >= MaxQuads) return;
        int v = quadCount++ * 4;
        vertices[v] = new Vector3(x, y); vertices[v + 1] = new Vector3(x, y + h);
        vertices[v + 2] = new Vector3(x + w, y + h); vertices[v + 3] = new Vector3(x + w, y);
        for (int i = 0; i < 4; i++) colors[v + i] = color;
    }

    private void OnDisable()
    {
        if (firstLever != null) firstLever.SignalEmitted -= OnLeverUsed;
        if (secondLever != null && secondLever != firstLever) secondLever.SignalEmitted -= OnLeverUsed;
        Release(visual); Release(mesh); Release(material);
        visual = null; mesh = null; material = null;
    }

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
