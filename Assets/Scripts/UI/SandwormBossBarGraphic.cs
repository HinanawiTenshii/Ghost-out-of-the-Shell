using UnityEngine;
using UnityEngine.UI;

/// <summary>One batched geometric HUD graphic: [ brackets ], dark track, pale damage trail and real health.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class SandwormBossBarGraphic : MaskableGraphic
{
    private float expansion, health, delayedHealth;
    private static readonly Color Track = new Color32(13, 27, 57, 255);
    private static readonly Color Fill = new Color32(38, 201, 213, 255);
    private static readonly Color Trail = new Color32(177, 239, 245, 255);
    public float Expansion => expansion;
    public float HealthFraction => health;
    public float DelayedFraction => delayedHealth;

    public void SetDisplay(float open, float current, float delayed)
    {
        open = Mathf.Clamp01(open); current = Mathf.Clamp01(current); delayed = Mathf.Clamp(delayed, current, 1f);
        if (expansion == open && health == current && delayedHealth == delayed) return;
        expansion = open; health = current; delayedHealth = delayed; SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear(); Rect rect = GetPixelAdjustedRect();
        float cx = rect.center.x, cy = rect.center.y;
        float half = Mathf.Lerp(12f, rect.width * .5f, expansion);
        float h = Mathf.Min(24f, rect.height), y = cy - h * .5f;
        const float line = 3f, cap = 10f;
        Color bracket = ZeldaUiPalette.Primary * color;
        Quad(mesh, new Rect(cx - half, y, line, h), bracket);
        Quad(mesh, new Rect(cx - half, y, cap, line), bracket);
        Quad(mesh, new Rect(cx - half, y + h - line, cap, line), bracket);
        Quad(mesh, new Rect(cx + half - line, y, line, h), bracket);
        Quad(mesh, new Rect(cx + half - cap, y, cap, line), bracket);
        Quad(mesh, new Rect(cx + half - cap, y + h - line, cap, line), bracket);
        float width = Mathf.Max(0f, rect.width - 20f) * expansion;
        Rect track = new Rect(cx - width * .5f, cy - 7f, width, 14f);
        Quad(mesh, track, Track * color);
        Rect delayed = track; delayed.width *= delayedHealth; Quad(mesh, delayed, Trail * color);
        Rect actual = track; actual.width *= health; Quad(mesh, actual, Fill * color);
    }

    private static void Quad(VertexHelper mesh, Rect rect, Color tint)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        int index = mesh.currentVertCount;
        mesh.AddVert(new Vector3(rect.xMin, rect.yMin), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMin, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMax), tint, Vector2.zero);
        mesh.AddVert(new Vector3(rect.xMax, rect.yMin), tint, Vector2.zero);
        mesh.AddTriangle(index, index + 1, index + 2); mesh.AddTriangle(index + 2, index + 3, index);
    }
}
