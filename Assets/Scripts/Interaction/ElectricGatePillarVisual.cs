using System.Collections.Generic;
using UnityEngine;

/// <summary>Simple overhead rectangles, with a network-driven status lamp and one soft halo.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ElectricGatePillar))]
public sealed class ElectricGatePillarVisual : MonoBehaviour
{
    [SerializeField] private Sprite square;
    [SerializeField, Range(0f, 3f), InspectorName("指示灯光晕强度")] private float indicatorGlowIntensity = 1.15f;
    [SerializeField, Min(.5f), InspectorName("指示灯呼吸周期")] private float indicatorBreathPeriod = 2.8f;
    private GameObject generated;
    private bool previousPanel;
    private ElectricGatePillar.PanelSide previousSide;
    private ElectricGatePillar pillar;
    private SpriteRenderer conductor, cap;
    private MeshRenderer glowRenderer;
    private Mesh glowMesh;
    private Material glowMaterial;
    private Mesh permissionMesh;
    private MeshRenderer permissionRenderer;
    private Material permissionMaterial;
    private int displayedPermission = int.MinValue;
    // Compact 3 x 5 display cells match the pillar's simple geometric construction.
    private static readonly string[] DigitPixels = {
        "111101101101111", "010110010010111", "111001111100111", "111001111001111",
        "101101111001001", "111100111001111", "111100111101111", "111001010010010",
        "111101111101111", "111101111001111"
    };
    private bool dirty;
    private static readonly int GlowColorId = Shader.PropertyToID("_Color");
    private static readonly int GlowIntensityId = Shader.PropertyToID("_Intensity");
    private void OnEnable() { pillar = GetComponent<ElectricGatePillar>(); Rebuild(); }
    private void OnValidate()
    {
        indicatorGlowIntensity = Mathf.Clamp(indicatorGlowIntensity, 0f, 3f);
        indicatorBreathPeriod = Mathf.Max(.5f, indicatorBreathPeriod);
        dirty = true;
    }
    private void LateUpdate()
    {
        if (pillar != null && (dirty || generated == null || previousPanel != pillar.HasPanel || previousSide != pillar.Side)) Rebuild();
        ApplySignal();
        UpdatePermissionLabel();
    }
    private void Rebuild()
    {
        Clear();
        if (pillar == null || square == null) return;
        dirty = false;
        previousPanel = pillar.HasPanel; previousSide = pillar.Side;
        generated = new GameObject("Electric Pillar Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
        generated.transform.SetParent(transform, false);
        Rect("Base", Vector2.zero, new Vector2(1, 1), new Color(.29f,.34f,.36f), 2);
        Rect("Steel top", new Vector2(0,.035f), new Vector2(.84f,.84f), new Color(.68f,.73f,.74f), 3);
        Rect("Inset", Vector2.zero, new Vector2(.6f,.6f), new Color(.4f,.46f,.49f), 4);
        conductor = Rect("Status conductor", Vector2.zero, new Vector2(.34f,.56f), IndicatorColor(ElectricGateRules.SignalState.Off), 5);
        cap = Rect("Cap light", new Vector2(0,.2f), new Vector2(.34f,.07f), new Color(.26f,.3f,.32f), 6);
        Rect("Cap steel", new Vector2(0,-.2f), new Vector2(.34f,.07f), new Color(.83f,.88f,.88f), 6);
        BuildHalo();
        BuildPermissionLabel();
        ApplySignal();
        UpdatePermissionLabel();
        if (!pillar.HasPanel) return;
        Vector2 d = pillar.PanelDirection;
        bool vertical = Mathf.Abs(d.y) > .5f;
        Rect("Control panel", d * .49f, vertical ? new Vector2(.52f,.24f) : new Vector2(.24f,.52f), new Color(.28f,.33f,.36f), 7);
        Rect("Panel screen", d * .49f, vertical ? new Vector2(.32f,.12f) : new Vector2(.12f,.32f), new Color(.95f,.18f,.12f), 8);
        Rect("Panel indicator", d * .49f, new Vector2(.055f,.055f), new Color(1,.87f,.78f), 9);
    }
    private SpriteRenderer Rect(string label, Vector2 pos, Vector2 size, Color color, int order)
    {
        var part = new GameObject(label, typeof(SpriteRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
        part.transform.SetParent(generated.transform, false); part.transform.localPosition = pos;
        part.transform.localScale = new Vector3(size.x / square.bounds.size.x, size.y / square.bounds.size.y, 1);
        var renderer = part.GetComponent<SpriteRenderer>(); renderer.sprite = square; renderer.color = color; renderer.sortingOrder = order;
        return renderer;
    }
    public static Color IndicatorColor(ElectricGateRules.SignalState signal)
    {
        if (signal == ElectricGateRules.SignalState.Off) return new Color(.16f,.20f,.23f,1f);
        return signal == ElectricGateRules.SignalState.Passage ? new Color(.12f,.72f,1f,1f) : new Color(.85f,.12f,.09f,1f);
    }
    public static float BreathMultiplier(float time, float period)
    {
        return 1f + .24f * Mathf.Sin(time * Mathf.PI * 2f / Mathf.Max(.5f, period));
    }
    public static Color PermissionDigitColor(ElectricGateRules.SignalState signal)
    {
        // Keep the signal hue, but make the number paler. No extra glow: the small
        // digits must stay readable on top of the existing breathing light.
        return Color.Lerp(IndicatorColor(signal), Color.white, .48f);
    }
    private void BuildPermissionLabel()
    {
        permissionMesh = new Mesh { name = "Electric Pillar Permission Digits", hideFlags = HideFlags.HideAndDontSave };
        permissionMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        var label = new GameObject("Permission Level", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
        label.transform.SetParent(generated.transform, false);
        label.GetComponent<MeshFilter>().sharedMesh = permissionMesh;
        permissionRenderer = label.GetComponent<MeshRenderer>();
        permissionRenderer.sharedMaterial = permissionMaterial;
        permissionRenderer.sortingLayerID = 0;
        permissionRenderer.sortingOrder = 7;
        permissionRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        permissionRenderer.receiveShadows = false;
        permissionRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        permissionRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        permissionRenderer.enabled = false;
    }
    private void UpdatePermissionLabel()
    {
        if (pillar == null || permissionRenderer == null || permissionMaterial == null) return;
        bool show = pillar.Mode == ElectricGatePillar.PassageMode.Permission;
        permissionRenderer.enabled = show;
        if (!show) return;
        int level = Mathf.Clamp(pillar.MinimumPermission, -2, 9);
        // Inspector edits, saved state and panel commits all use the same source.
        // Changing colour each frame does not rebuild geometry or allocate text.
        if (displayedPermission != level)
        {
            WritePermissionDigits(level);
            displayedPermission = level;
        }
        permissionMaterial.color = PermissionDigitColor(pillar.Signal);
    }
    private void WritePermissionDigits(int level)
    {
        string text = level.ToString(System.Globalization.CultureInfo.InvariantCulture);
        int columns = text.Length * 4 - 1;
        float cell = Mathf.Min(.06f, .28f / columns);
        var vertices = new List<Vector3>(80);
        var triangles = new List<int>(120);
        for (int digit = 0; digit < text.Length; digit++)
        {
            string pixels = text[digit] == '-' ? "000000111000000" : DigitPixels[text[digit] - '0'];
            for (int row = 0; row < 5; row++) for (int column = 0; column < 3; column++)
            {
                if (pixels[row * 3 + column] != '1') continue;
                float x = (digit * 4 + column - columns * .5f) * cell;
                float y = (1.5f - row) * cell;
                int start = vertices.Count;
                vertices.Add(new Vector3(x, y, 0)); vertices.Add(new Vector3(x, y + cell, 0));
                vertices.Add(new Vector3(x + cell, y + cell, 0)); vertices.Add(new Vector3(x + cell, y, 0));
                triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
                triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
            }
        }
        permissionMesh.Clear();
        permissionMesh.SetVertices(vertices);
        permissionMesh.SetTriangles(triangles, 0);
        var colors = new Color32[vertices.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
        permissionMesh.colors32 = colors;
        permissionMesh.uv = new Vector2[vertices.Count];
        permissionMesh.RecalculateBounds();
    }
    private void ApplySignal()
    {
        if (pillar == null || conductor == null || cap == null) return;
        var signal = pillar.Signal;
        bool powered = signal != ElectricGateRules.SignalState.Off;
        Color color = IndicatorColor(signal);
        conductor.color = color;
        cap.color = !powered ? new Color(.26f,.30f,.32f) : signal == ElectricGateRules.SignalState.Passage
            ? new Color(.62f,.91f,1f) : new Color(1,.42f,.32f);
        if (glowRenderer == null || glowMaterial == null) return;
        glowRenderer.enabled = Application.isPlaying && powered && indicatorGlowIntensity > 0f;
        if (!glowRenderer.enabled) return;
        glowMaterial.SetColor(GlowColorId, color);
        // Shared scaled time keeps both ends in phase and freezes breathing when paused.
        glowMaterial.SetFloat(GlowIntensityId, indicatorGlowIntensity * BreathMultiplier(Time.time, indicatorBreathPeriod));
    }
    private void BuildHalo()
    {
        Shader shader = Resources.Load<Shader>("WarmLightGlow");
        if (shader == null || !shader.isSupported) return;
        glowMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        glowMaterial.SetFloat("_RadialFalloff", 1f);
        // Explicit UVs avoid depending on the atlas coordinates of the built-in square.
        glowMesh = new Mesh { name = "Electric Pillar Indicator Halo", hideFlags = HideFlags.HideAndDontSave };
        glowMesh.vertices = new[] { new Vector3(-.4f,-.52f,0), new Vector3(-.4f,.52f,0),
            new Vector3(.4f,.52f,0), new Vector3(.4f,-.52f,0) };
        glowMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        glowMesh.colors32 = new Color32[] { Color.white, Color.white, Color.white, Color.white };
        glowMesh.triangles = new[] { 0,1,2,0,2,3 }; glowMesh.RecalculateBounds();
        var halo = new GameObject("Status Light Halo", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.HideAndDontSave, layer = 0 };
        halo.transform.SetParent(generated.transform, false);
        halo.GetComponent<MeshFilter>().sharedMesh = glowMesh;
        glowRenderer = halo.GetComponent<MeshRenderer>(); glowRenderer.sharedMaterial = glowMaterial;
        glowRenderer.sortingLayerID = 0; glowRenderer.sortingOrder = 6;
        glowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        glowRenderer.receiveShadows = false; glowRenderer.enabled = false;
    }
    private void Clear()
    {
        if (generated != null) generated.SetActive(false);
        Release(generated); Release(glowMesh); Release(glowMaterial);
        Release(permissionMesh); Release(permissionMaterial);
        permissionMesh = null; permissionMaterial = null; permissionRenderer = null; displayedPermission = int.MinValue;
        generated = null; glowMesh = null; glowMaterial = null; glowRenderer = null; conductor = null; cap = null;
    }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
    private void OnDisable() => Clear();
    private void OnDestroy() => Clear();
}
