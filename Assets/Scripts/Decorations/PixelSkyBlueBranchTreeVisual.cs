using UnityEngine;

/// <summary>
/// An asymmetric, translucent geometric tree with a vertical blue gradient. The legacy class name is
/// retained so existing prefab and scene references keep working.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelSkyBlueBranchTreeVisual : MonoBehaviour
{
    [SerializeField] private int sortingOrder = 1;
    [ColorUsage(false)]
    [Tooltip("Tint multiplied over the blue gradient. White retains the original gradient.")]
    [SerializeField] private Color branchColor = Color.white;
    [Range(0f, 1f)]
    [Tooltip("Uniform transparency, including branch junctions.")]
    [SerializeField] private float opacity = 0.65f;

    // One connected silhouette, triangulated without overlapping faces.
    // Coordinates retain the original 3 x 3 footprint and bottom-center pivot.
    private static readonly Vector2[] Outline =
    {
        new Vector2(0.12f, 0.01f),
        new Vector2(0.035f, 0.89f),
        new Vector2(0.59f, 1.61f),
        new Vector2(1.08f, 1.9f),
        new Vector2(1.28f, 2.36f),
        new Vector2(1.23f, 2.38f),
        new Vector2(1f, 1.96f),
        new Vector2(0.6f, 1.73f),
        new Vector2(0.65f, 2.25f),
        new Vector2(1.13f, 2.68f),
        new Vector2(1.09f, 2.72f),
        new Vector2(0.62f, 2.34f),
        new Vector2(0.57f, 2.97f),
        new Vector2(0.505f, 2.965f),
        new Vector2(0.535f, 2.25f),
        new Vector2(0.43f, 1.665f),
        new Vector2(-0.07f, 1.06f),
        new Vector2(-0.1f, 1.55f),
        new Vector2(-0.39f, 2.17f),
        new Vector2(-0.44f, 2.15f),
        new Vector2(-0.215f, 1.53f),
        new Vector2(-0.21f, 1.075f),
        new Vector2(-0.65f, 1.45f),
        new Vector2(-0.83f, 1.92f),
        new Vector2(-0.72f, 2.52f),
        new Vector2(-0.78f, 2.53f),
        new Vector2(-0.915f, 1.99f),
        new Vector2(-1.41f, 2.23f),
        new Vector2(-1.435f, 2.18f),
        new Vector2(-0.945f, 1.91f),
        new Vector2(-0.83f, 1.5f),
        new Vector2(-1.18f, 1.62f),
        new Vector2(-1.425f, 1.84f),
        new Vector2(-1.46f, 1.8f),
        new Vector2(-1.245f, 1.535f),
        new Vector2(-0.71f, 1.32f),
        new Vector2(-0.175f, 0.91f),
        new Vector2(-0.12f, 0.01f),
    };

    private static readonly ushort[] Triangles =
    {
        37, 0, 1, 3, 4, 5, 3, 5, 6, 2, 3, 6,
        2, 6, 7, 1, 2, 7, 8, 9, 10, 8, 10, 11,
        7, 8, 11, 7, 11, 12, 7, 12, 13, 7, 13, 14,
        7, 14, 15, 1, 7, 15, 1, 15, 16, 37, 1, 16,
        37, 16, 17, 17, 18, 19, 17, 19, 20, 17, 20, 21,
        23, 24, 25, 23, 25, 26, 22, 23, 26, 26, 27, 28,
        26, 28, 29, 22, 26, 29, 22, 29, 30, 21, 22, 30,
        21, 30, 31, 31, 32, 33, 31, 33, 34, 31, 34, 35,
        21, 31, 35, 21, 35, 36, 17, 21, 36, 17, 36, 37,
    };

    private const int GradientSize = 64;
    private static readonly Color GradientBottom = new Color(0.10f, 0.35f, 0.56f, 1f);
    private static readonly Color GradientTop = new Color(0.53f, 0.82f, 0.92f, 1f);
    private static Sprite treeSprite;
    private static Texture2D treeTexture;
    private static int activeUsers;
    private SpriteRenderer treeRenderer;
    private bool ownsSharedVisual;
    private bool visualDirty;

    private void OnEnable()
    {
        if (!ownsSharedVisual)
        {
            activeUsers++;
            ownsSharedVisual = true;
        }
        treeRenderer = GetComponent<SpriteRenderer>();
        // Scene loading / OnEnable can run outside Unity's player loop.
        // OverrideGeometry is only safe later, during Update.
        visualDirty = true;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
    }

    private void OnValidate()
    {
        opacity = Mathf.Clamp01(opacity);
        // OnValidate can run during deserialization; create Unity objects later.
        visualDirty = true;
    }

    private void Update()
    {
        if (visualDirty)
            ApplyVisual();
    }

    private void ApplyVisual()
    {
        EnsureSprite();
        if (treeRenderer == null)
            treeRenderer = GetComponent<SpriteRenderer>();

        Color tint = branchColor;
        tint.a = opacity;
        treeRenderer.sprite = treeSprite;
        treeRenderer.color = tint;
        treeRenderer.sortingOrder = sortingOrder;
        visualDirty = false;
    }

    private static void EnsureSprite()
    {
        if (treeSprite != null)
            return;

        // A small gradient texture supplies color, while the mesh defines clean
        // geometric edges. This method is called only from Update, never OnEnable.
        treeTexture = new Texture2D(GradientSize, GradientSize, TextureFormat.RGBA32, false)
        {
            name = "Geometric Sky Blue Branch Tree Gradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[GradientSize * GradientSize];
        for (int y = 0; y < GradientSize; y++)
        {
            Color color = Color.Lerp(GradientBottom, GradientTop, y / (GradientSize - 1f));
            for (int x = 0; x < GradientSize; x++)
                pixels[y * GradientSize + x] = color;
        }
        treeTexture.SetPixels(pixels);
        treeTexture.Apply(false, false);

        Sprite generatedSprite = Sprite.Create(treeTexture,
            new Rect(0f, 0f, GradientSize, GradientSize),
            new Vector2(0.5f, 0f), GradientSize / 3f, 0, SpriteMeshType.Tight);
        generatedSprite.name = "Geometric Sky Blue Branch Tree";
        // FullRect sprites reject geometry overrides in Unity 2021.3.
        // OverrideGeometry takes pixel coordinates relative to Sprite.rect,
        // NOT the pivot-relative world-unit coordinates returned by vertices.
        Vector2[] rectVertices = new Vector2[Outline.Length];
        for (int i = 0; i < Outline.Length; i++)
            rectVertices[i] = (Outline[i] + new Vector2(1.5f, 0f)) * (GradientSize / 3f);
        generatedSprite.OverrideGeometry(rectVertices, Triangles);
        generatedSprite.hideFlags = HideFlags.HideAndDontSave;
        // Publish only after the geometry is ready; later instances share it.
        treeSprite = generatedSprite;
        treeTexture.Apply(false, true);
    }

    private void OnDisable()
    {
        ReleaseVisual();
    }

    private void OnDestroy()
    {
        ReleaseVisual();
    }

    private void ReleaseVisual()
    {
        if (!ownsSharedVisual)
            return;

        if (treeRenderer != null && treeRenderer.sprite == treeSprite)
            treeRenderer.sprite = null;

        ownsSharedVisual = false;
        activeUsers--;
        if (activeUsers > 0)
            return;

        DestroyGenerated(treeSprite);
        DestroyGenerated(treeTexture);
        treeSprite = null;
        treeTexture = null;
        activeUsers = 0;
    }

    private static void DestroyGenerated(Object resource)
    {
        if (resource == null)
            return;
        if (Application.isPlaying)
            Destroy(resource);
        else
            DestroyImmediate(resource);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, 1.5f, 0f), new Vector3(3f, 3f, 0f));
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
