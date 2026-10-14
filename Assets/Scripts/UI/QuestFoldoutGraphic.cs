using UnityEngine;
using UnityEngine.UI;

/// <summary>Solid, font-independent triangle; the surrounding Button supplies tint feedback.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class QuestFoldoutGraphic : MaskableGraphic
{
    public bool expanded = true;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        Vector2 c = rect.center;
        float radius = Mathf.Min(rect.width, rect.height) * .25f;
        Vector2 a = expanded ? new Vector2(-radius, radius * .65f) : new Vector2(-radius * .65f, radius);
        Vector2 b = expanded ? new Vector2(radius, radius * .65f) : new Vector2(radius, 0);
        Vector2 d = expanded ? new Vector2(0, -radius) : new Vector2(-radius * .65f, -radius);
        vh.AddVert(c + a, color, Vector2.zero);
        vh.AddVert(c + b, color, Vector2.zero);
        vh.AddVert(c + d, color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
    }
}
