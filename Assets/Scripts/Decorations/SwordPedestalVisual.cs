using System.Collections.Generic;
using UnityEngine;

/// <summary>Low overhead-view stone slab with a half-embedded pixel sword. Story controls its state.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Sword Pedestal")]
public sealed class SwordPedestalVisual : MonoBehaviour
{
    [Header("State / 剑台状态")]
    [SerializeField, Tooltip("显示或隐藏宝剑；不自动发放装备或推进剧情。")]
    private bool hasSword = true;
    [Header("Flat Colors / 纯色色块")]
    [SerializeField] private Color stoneColor = new Color(.39f, .43f, .48f);
    [SerializeField] private Color stoneTopColor = new Color(.59f, .63f, .67f);
    [SerializeField] private Color stoneShadowColor = new Color(.24f, .28f, .34f);
    [SerializeField] private Color bladeColor = HeroSwordPixelArt.GetDefaultColor(HeroSwordPixelArt.Ink.Blade);
    [SerializeField] private Color bladeShadowColor = HeroSwordPixelArt.GetDefaultColor(HeroSwordPixelArt.Ink.BladeShadow);
    [SerializeField] private Color hiltColor = HeroSwordPixelArt.GetDefaultColor(HeroSwordPixelArt.Ink.Hilt);
    [SerializeField] private Color gripColor = HeroSwordPixelArt.GetDefaultColor(HeroSwordPixelArt.Ink.Grip);
    [Header("Rendering / 渲染")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 5;
    [SerializeField] private Material sourceMaterial;

    private GameObject visual, swordVisual;
    private Mesh stoneMesh, swordMesh;
    private Material material;
    private bool dirty = true;
    public bool HasSword => hasSword;

    /// <summary>Switch without rebuilding geometry or changing the pedestal's collision.</summary>
    public void SetSwordPresent(bool present)
    {
        hasSword = present;
        if (swordVisual != null) swordVisual.SetActive(present);
    }

    private void OnEnable()
    {
        if (visual == null || dirty) Build();
        else { visual.SetActive(true); SetSwordPresent(hasSword); }
    }
    // Editor changes are applied outside serialization callbacks.
    private void OnValidate() => dirty = true;
    private void Update() { if (dirty || visual == null) Build(); }

    private void Build()
    {
        Release();
        dirty = false;
        var collider = GetComponent<BoxCollider2D>();
        collider.isTrigger = false;
        collider.autoTiling = false;
        // Ground footprint; the top face is drawn 0.125 units above it.
        collider.size = new Vector2(1.18f, .75f);
        collider.offset = new Vector2(0f, .3125f);
        collider.edgeRadius = 0f;
        Shader shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
        material.name = "Sword Pedestal Flat Colors";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = Color.white;

        var stone = new Geometry();
        // Broad top and a thin front lip establish the game's overhead view.
        // The two simple cut-corner faces overlap; no stairs, frame or ornament.
        stone.Polygon(stoneColor, -.50f,-.0625f, .50f,-.0625f, .625f,.0625f, .625f,.5625f,
            .50f,.6875f, -.50f,.6875f, -.625f,.5625f, -.625f,.0625f);
        stone.Polygon(stoneTopColor, -.50f,.0625f, .50f,.0625f, .625f,.1875f, .625f,.6875f,
            .50f,.8125f, -.50f,.8125f, -.625f,.6875f, -.625f,.1875f);
        // The insertion point stays at the centre of the larger top face.
        stone.Polygon(stoneShadowColor, -.125f,.40625f, .10f,.40625f, .125f,.46875f, -.10f,.46875f);
        stoneMesh = stone.Create("Sword Pedestal Stone");

        var sword = new Geometry();
        // 16-PPU pixel grid, matching the characters. A 12-pixel blade has its
        // lower six pixels (including the point) inside the stone, so omit them.
        // Solid rectangular runs keep a pixel silhouette without extra textures.
        for (int i = 0; i < HeroSwordPixelArt.BlockCount; i++)
        {
            var block = HeroSwordPixelArt.GetBlock(i);
            int bottom = Mathf.Max(block.Y, HeroSwordPixelArt.InsertionRow);
            int top = block.Y + block.Height;
            if (bottom >= top) continue;
            Color color = block.Color == HeroSwordPixelArt.Ink.Blade ? bladeColor
                : block.Color == HeroSwordPixelArt.Ink.BladeShadow ? bladeShadowColor
                : block.Color == HeroSwordPixelArt.Ink.Hilt ? hiltColor : gripColor;
            sword.Pixels(color, block.X, bottom, block.Width, top - bottom);
        }
        swordMesh = sword.Create("Sword Pedestal Pixel Blade");
        visual = new GameObject("Sword Pedestal Geometry (Generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        CreateRenderer(visual, stoneMesh, sortingOrder);
        swordVisual = new GameObject("Sword (Generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        swordVisual.transform.SetParent(visual.transform, false);
        CreateRenderer(swordVisual, swordMesh, sortingOrder + 1);
        SetSwordPresent(hasSword);
    }

    private void CreateRenderer(GameObject root, Mesh mesh, int order)
    {
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // Convex polygons only; generated once, never allocated when toggling the sword.
    private sealed class Geometry
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color32> colors = new List<Color32>();
        readonly List<int> triangles = new List<int>();
        public void Polygon(Color color, params float[] xy)
        {
            int start = vertices.Count;
            for (int i = 0; i < xy.Length; i += 2) { vertices.Add(new Vector3(xy[i], xy[i + 1], 0)); colors.Add(color); }
            for (int i = 1; i < xy.Length / 2 - 1; i++) { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
        }
        public void Pixels(Color color, int x, int y, int width, int height)
        {
            const float pixel = 1f / 16f;
            Polygon(color, x * pixel, y * pixel, (x + width) * pixel, y * pixel,
                (x + width) * pixel, (y + height) * pixel, x * pixel, (y + height) * pixel);
        }
        public Mesh Create(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) Release();
        else if (visual != null) visual.SetActive(false);
    }
    private void OnDestroy() => Release();
    private void Release()
    {
        if (visual != null) visual.SetActive(false);
        Dispose(visual); Dispose(stoneMesh); Dispose(swordMesh); Dispose(material);
        visual = swordVisual = null; stoneMesh = swordMesh = null; material = null;
    }
    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
