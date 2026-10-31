using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

/// <summary>
/// Shared amethyst hexagon quest badge for maps, region badges and world hints.
/// Native flat geometry stays legible under the game's CRT treatment. The navy
/// silhouette separates it from white walls without a large opaque badge.
/// </summary>
public static class QuestFlagGlyph
{
    public const float Size = 28f;
    public const float HalfSize = Size * .5f;
    public const float WidthRatio = .88f;
    public const float HalfWidth = HalfSize * WidthRatio;
    public const float WorldSize = .65f;
    private static readonly Color32 Outline = new Color32(15, 33, 60, 255);
    private static readonly Color32 Rim = new Color32(234, 212, 255, 255);
    private static readonly Color32 Interior = new Color32(41, 33, 68, 255);
    private static readonly Color32 Core = new Color32(188, 131, 255, 255);
    private static Mesh sharedWorldMesh;
    private static Material sharedWorldMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (sharedWorldMesh != null) Object.Destroy(sharedWorldMesh);
        if (sharedWorldMaterial != null) Object.Destroy(sharedWorldMaterial);
        sharedWorldMesh = null;
        sharedWorldMaterial = null;
    }

    public static void AddTo(VertexHelper vh, Vector2 center, float scale)
    {
        if (scale <= 0f) return;
        // Pale-violet hexagonal rim, dark interior and a solid amethyst diamond.
        // Retain the same 28-unit footprint so existing border placement, world
        // anchors and saved scene UI layouts need no migration.
        AddHexagon(vh, center, scale, 14f, Outline);
        AddHexagon(vh, center, scale, 13f, Rim);
        AddHexagon(vh, center, scale, 10.5f, Interior);
        AddDiamond(vh, center, scale, 6.2f, Core);
    }

    public static GameObject CreateWorldVisual(UnityEngine.SceneManagement.Scene scene)
    {
        EnsureWorldResources();
        var root = new GameObject("Quest Journal Interaction Marker", typeof(MeshFilter), typeof(MeshRenderer))
        { hideFlags = HideFlags.DontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.localScale = Vector3.one * (WorldSize / Size);
        root.GetComponent<MeshFilter>().sharedMesh = sharedWorldMesh;
        var renderer = root.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = sharedWorldMaterial;
        renderer.sortingLayerID = 0;
        // Vision mask is order 30000; the later UI camera remains above this.
        renderer.sortingOrder = 29999;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return root;
    }

    private static void EnsureWorldResources()
    {
        if (sharedWorldMesh == null)
        {
            sharedWorldMesh = new Mesh { name = "Shared Quest Amethyst Hexagon", hideFlags = HideFlags.HideAndDontSave };
            using (var vh = new VertexHelper())
            {
                AddTo(vh, Vector2.zero, 1f);
                vh.FillMesh(sharedWorldMesh);
            }
        }
        if (sharedWorldMaterial == null)
            sharedWorldMaterial = new Material(Shader.Find("Sprites/Default")) {
                name = "Quest Amethyst Flat UI Colors", mainTexture = Texture2D.whiteTexture,
                hideFlags = HideFlags.HideAndDontSave
            };
    }

    private static void AddDiamond(VertexHelper vh, Vector2 c, float s, float radius, Color32 color)
    {
        int first = vh.currentVertCount;
        Vertex(vh, c, s, 0f, radius, color);
        Vertex(vh, c, s, radius, 0f, color);
        Vertex(vh, c, s, 0f, -radius, color);
        Vertex(vh, c, s, -radius, 0f, color);
        vh.AddTriangle(first, first + 1, first + 2);
        vh.AddTriangle(first, first + 2, first + 3);
    }

    private static void AddHexagon(VertexHelper vh, Vector2 c, float s, float radius, Color32 color)
    {
        int first = vh.currentVertCount;
        float width = radius * WidthRatio;
        Vertex(vh, c, s, 0f, radius, color);
        Vertex(vh, c, s, width, radius * .5f, color);
        Vertex(vh, c, s, width, -radius * .5f, color);
        Vertex(vh, c, s, 0f, -radius, color);
        Vertex(vh, c, s, -width, -radius * .5f, color);
        Vertex(vh, c, s, -width, radius * .5f, color);
        for (int i = 1; i < 5; i++) vh.AddTriangle(first, first + i, first + i + 1);
    }

    private static void Vertex(VertexHelper vh, Vector2 c, float s, float x, float y, Color32 color)
    {
        var vertex = UIVertex.simpleVert;
        vertex.position = c + new Vector2(x, y) * s;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
