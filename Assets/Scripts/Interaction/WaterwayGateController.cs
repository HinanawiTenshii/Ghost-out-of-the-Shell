using System.Collections.Generic;
using UnityEngine;

/// <summary>Single-gate breaker switch. Gate state is authoritative, including external changes.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Interaction/Waterway Gate Controller")]
public sealed class WaterwayGateController : MonoBehaviour
{
    [Header("Binding / 绑定水闸")]
    [SerializeField, Tooltip("将场景中的 WaterwayGate 拖入；窗口自动显示其编号。")]
    private WaterwayGate linkedGate;
    [Header("Interaction / 交互")]
    [SerializeField, Min(0.1f)] private float interactionDistance = 1.4f;
    [Header("Visual / 外观")]
    [SerializeField] private Font displayFont;
    [SerializeField] private Material surfaceMaterial;
    [SerializeField] private int sortingOrder = 12;
    [SerializeField, Min(0.05f)] private float switchDuration = 0.16f;

    private readonly List<Mesh> meshes = new List<Mesh>();
    private GameObject generated, promptObject;
    private Transform handle;
    private Mesh handleMesh;
    private Panel[] handlePanels;
    private Vector3[] handleVertices;
    private float lastHandlePose = float.NaN;
    private TextMesh identifierText, promptText;
    private Material ownedMaterial, promptMaterial;
    private bool dirty = true;
    private float pose;

    public WaterwayGate LinkedGate => linkedGate;
    public string DisplayedIdentifier => linkedGate != null ? linkedGate.Identifier : "--";
    public bool IsOpenRequested => linkedGate != null && linkedGate.IsOpenRequested;
    public bool IsTransitionLocked => WaterwayGateTargetFade.IsGateTransitioning(linkedGate);
    public bool HasBatteryPower => BatterySocket.IsPowered(this);

    public void BindGate(WaterwayGate gate)
    {
        linkedGate = gate;
        RefreshState();
    }

    // Also usable by scripted scene mechanisms; no duplicated local switch state.
    public void SetOpen(bool value)
    {
        if (linkedGate == null || IsTransitionLocked || !HasBatteryPower) return;
        linkedGate.SetOpen(value);
        RefreshState();
    }

    public void ToggleFromExternal()
    {
        if (linkedGate == null) return;
        SetOpen(!linkedGate.IsOpenRequested);
    }

    private void OnEnable()
    {
        pose = IsOpenRequested ? 1f : 0f;
        dirty = true;
    }

    private void OnValidate()
    {
        interactionDistance = Mathf.Max(0.1f, interactionDistance);
        switchDuration = Mathf.Max(0.05f, switchDuration);
        dirty = true; // Unity may validate off the main thread.
    }

    private void Update()
    {
        if (dirty || generated == null) Rebuild();
        RefreshState();
        if (!Application.isPlaying) return;

        ZeldaFourWayMover mover = InputBlocked ? null : GetNearbyCharacter();
        if (linkedGate == null || IsTransitionLocked || !HasBatteryPower || mover == null)
        {
            SetPromptVisible(false);
            return;
        }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, SetPromptVisible);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, HandleInteraction);
    }

    private bool InputBlocked => DocumentReader.IsInputBlocked
        || ClockworkPuppetRuntime.BlocksCharacterInput || Time.timeScale <= 0f;

    private ZeldaFourWayMover GetNearbyCharacter()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled) return null;
        ZeldaCharacterData data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead && !data.IsGhostForm
            && ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject).handle == gameObject.scene.handle
            && Vector2.Distance(mover.transform.position, transform.position) <= interactionDistance
            ? mover : null;
    }

    private void HandleInteraction()
    {
        // Recheck after arbitration: possession, menus, range or binding may have changed.
        if (!isActiveAndEnabled || InputBlocked || IsTransitionLocked || !HasBatteryPower || linkedGate == null || GetNearbyCharacter() == null) return;
        ToggleFromExternal();
    }

    private void RefreshState()
    {
        float target = IsOpenRequested ? 1f : 0f;
        pose = Application.isPlaying
            ? Mathf.MoveTowards(pose, target, Time.deltaTime / Mathf.Max(0.05f, switchDuration))
            : target;
        if (handle != null)
        {
            // Fixed axle at the slot centre, not a sliding switch assembly.
            handle.localRotation = Quaternion.identity;
            handle.localPosition = new Vector3(0f, -0.14f, -0.01f);
            UpdateHandleGeometry(pose);
        }
        if (identifierText != null)
        {
            string label = DisplayedIdentifier;
            if (identifierText.text != label) identifierText.text = label;
            identifierText.color = linkedGate == null ? new Color(0.50f, 0.55f, 0.57f)
                : IsOpenRequested ? new Color(0.35f, 1f, 0.70f) : new Color(1f, 0.67f, 0.30f);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        ZeldaFourWayMover mover = visible && linkedGate != null && !InputBlocked && !IsTransitionLocked && HasBatteryPower ? GetNearbyCharacter() : null;
        if (mover == null)
        {
            if (promptObject != null) promptObject.SetActive(false);
            return;
        }
        if (displayFont == null) return;
        if (promptObject == null)
        {
            promptObject = new GameObject("Gate Controller Interaction Prompt", typeof(TextMesh))
                { hideFlags = HideFlags.HideAndDontSave };
            promptObject.transform.SetParent(transform, false);
            promptText = promptObject.GetComponent<TextMesh>();
            promptText.font = displayFont;
            promptText.fontSize = 72;
            promptText.characterSize = 0.035f;
            promptText.anchor = TextAnchor.MiddleCenter;
            promptText.alignment = TextAlignment.Center;
            promptText.color = ZeldaUiPalette.Primary;
            promptMaterial = new Material(displayFont.material) { hideFlags = HideFlags.HideAndDontSave };
            MeshRenderer renderer = promptObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = promptMaterial;
            renderer.sortingOrder = short.MaxValue - 2;
            ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptText.text = "按[E]" + (IsOpenRequested ? "关闭水闸 " : "开启水闸 ") + DisplayedIdentifier;
        displayFont.RequestCharactersInTexture(promptText.text, 72, FontStyle.Normal);
        promptMaterial.mainTexture = displayFont.material.mainTexture;
        promptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, 0.9f));
        promptObject.transform.rotation = Quaternion.identity;
        promptObject.SetActive(true);
    }

    private void Rebuild()
    {
        // Include existing scene instances, not just newly placed prefabs.
        // Render the case, handle and identifier beneath the vision mask.
        if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;
        ReleaseVisual();
        dirty = false;
        Material material = surfaceMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        generated = new GameObject("Generated Gate Controller")
            { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generated.transform.SetParent(transform, false);
        DrawParts("Metal Case", BuildCase(), material, sortingOrder);
        handle = DrawParts("Breaker Handle", BuildHandle(), material, sortingOrder + 1);
        handleMesh = handle.GetComponent<MeshFilter>().sharedMesh;
        handleMesh.MarkDynamic();
        handlePanels = new Panel[6];
        handleVertices = new Vector3[24];
        if (displayFont != null)
        {
            var label = new GameObject("Linked Gate Identifier", typeof(TextMesh))
                { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            label.transform.SetParent(generated.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.34f, -0.02f);
            identifierText = label.GetComponent<TextMesh>();
            identifierText.font = displayFont;
            identifierText.fontSize = 48;
            identifierText.characterSize = 0.08f;
            identifierText.anchor = TextAnchor.MiddleCenter;
            identifierText.alignment = TextAlignment.Center;
            MeshRenderer renderer = label.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = displayFont.material;
            renderer.sortingOrder = sortingOrder + 2;
        }
        RefreshState();
    }

    private struct Panel
    {
        public float X, Y, Width, Height;
        public Color Color;
        public Panel(float x, float y, float width, float height, Color color)
        { X = x; Y = y; Width = width; Height = height; Color = color; }
    }

    private static List<Panel> BuildCase()
    {
        Color edge = new Color(0.23f, 0.28f, 0.30f);
        Color steel = new Color(0.60f, 0.66f, 0.67f);
        Color light = new Color(0.80f, 0.84f, 0.83f);
        Color inset = new Color(0.08f, 0.13f, 0.17f);
        return new List<Panel> {
            new Panel(0f, 0f, 0.86f, 1.24f, edge),
            new Panel(0f, 0.02f, 0.74f, 1.12f, steel),
            new Panel(0f, 0.53f, 0.74f, 0.10f, light),
            new Panel(0f, -0.49f, 0.74f, 0.09f, new Color(0.39f, 0.45f, 0.47f)),
            new Panel(0f, 0.34f, 0.60f, 0.34f, edge),
            new Panel(0f, 0.34f, 0.50f, 0.24f, inset),
            new Panel(0f, -0.16f, 0.45f, 0.53f, edge),
            new Panel(0f, -0.14f, 0.31f, 0.39f, inset),
            new Panel(0.28f, -0.30f, 0.07f, 0.10f, new Color(0.95f, 0.58f, 0.15f)),
            new Panel(0.28f, 0.04f, 0.07f, 0.10f, new Color(0.15f, 0.82f, 0.54f)),
            new Panel(-0.29f, -0.46f, 0.06f, 0.06f, light),
            new Panel(0.29f, -0.46f, 0.06f, 0.06f, light),
            new Panel(0f, -0.14f, 0.22f, 0.13f, edge),
            new Panel(0f, -0.14f, 0.18f, 0.085f, light)
        };
    }

    private static List<Panel> BuildHandle()
    {
        var parts = new Panel[6];
        FillHandlePanels(parts, 0f);
        return new List<Panel>(parts);
    }

    private static Vector3 GetHandleTip(float amount)
    {
        // Orthographic projection of a rigid 0.22-unit arm rotating around X.
        // Mid-throw points toward the viewer: shortest stem, most grip depth.
        float angle = Mathf.Lerp(-55f, 55f, Mathf.SmoothStep(0f, 1f, amount)) * Mathf.Deg2Rad;
        return new Vector3(0f, 0.22f * Mathf.Sin(angle), -0.22f * Mathf.Cos(angle));
    }

    private static void FillHandlePanels(Panel[] parts, float amount)
    {
        Vector3 tip = GetHandleTip(amount);
        float front = -tip.z / 0.22f;
        float gripHeight = 0.10f + 0.06f * front;
        parts[0] = new Panel(0f, tip.y * 0.5f, 0.095f, Mathf.Abs(tip.y) + 0.025f, new Color(0.22f, 0.26f, 0.28f));
        parts[1] = new Panel(-0.012f, tip.y * 0.5f, 0.045f, Mathf.Abs(tip.y) + 0.015f, new Color(0.83f, 0.86f, 0.85f));
        parts[2] = new Panel(0f, tip.y, 0.38f, gripHeight, new Color(0.45f, 0.07f, 0.055f));
        parts[3] = new Panel(0f, tip.y + 0.008f, 0.32f, gripHeight + 0.02f, new Color(0.78f, 0.12f, 0.08f));
        parts[4] = new Panel(0f, tip.y + gripHeight * 0.35f, 0.29f, 0.03f, new Color(1f, 0.31f, 0.16f));
        parts[5] = new Panel(0f, tip.y - gripHeight * 0.34f, 0.29f, 0.025f, new Color(0.61f, 0.07f, 0.045f));
    }

    private void UpdateHandleGeometry(float amount)
    {
        if (handleMesh == null || handlePanels == null || Mathf.Approximately(lastHandlePose, amount)) return;
        lastHandlePose = amount;
        FillHandlePanels(handlePanels, amount);
        for (int i = 0; i < handlePanels.Length; i++)
        {
            Panel p = handlePanels[i];
            int first = i * 4;
            float w = p.Width * 0.5f, h = p.Height * 0.5f, z = -first * 0.0001f;
            handleVertices[first] = new Vector3(p.X-w, p.Y-h, z);
            handleVertices[first+1] = new Vector3(p.X-w, p.Y+h, z);
            handleVertices[first+2] = new Vector3(p.X+w, p.Y+h, z);
            handleVertices[first+3] = new Vector3(p.X+w, p.Y-h, z);
        }
        handleMesh.vertices = handleVertices;
        handleMesh.RecalculateBounds();
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
        var mesh = new Mesh { name = "Gate Controller " + name, hideFlags = HideFlags.HideAndDontSave };
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
        if (promptObject != null) promptObject.SetActive(false);
    }

    private void OnDestroy()
    {
        ReleaseVisual();
        Release(promptObject);
        Release(promptMaterial);
    }

    private void ReleaseVisual()
    {
        if (generated != null) generated.SetActive(false);
        Release(generated); generated = null; handle = null; identifierText = null;
        handleMesh = null; handlePanels = null; handleVertices = null; lastHandlePose = float.NaN;
        foreach (Mesh mesh in meshes) Release(mesh);
        meshes.Clear();
        Release(ownedMaterial); ownedMaterial = null;
    }

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
