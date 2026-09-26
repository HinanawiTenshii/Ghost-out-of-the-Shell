using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A restrained, same-colour phosphor halo for title-menu text and thin edges.
/// Uses the graphic's existing texture/material; the unmodified core is drawn last.
/// </summary>
[DisallowMultipleComponent]
public sealed class TitleScreenGlow : BaseMeshEffect
{
    private const int HaloCopies = 16;
    private readonly List<UIVertex> source = new List<UIVertex>();
    private readonly List<UIVertex> output = new List<UIVertex>();
    private static readonly Vector2[] Directions =
    {
        new Vector2(1f, 0f), new Vector2(-1f, 0f),
        new Vector2(0f, 1f), new Vector2(0f, -1f),
        new Vector2(0.7071068f, 0.7071068f),
        new Vector2(-0.7071068f, 0.7071068f),
        new Vector2(0.7071068f, -0.7071068f),
        new Vector2(-0.7071068f, -0.7071068f)
    };

    public static void Attach(Graphic target)
    {
        if (target != null && target.GetComponent<TitleScreenGlow>() == null)
            target.gameObject.AddComponent<TitleScreenGlow>();
    }

    public override void ModifyMesh(VertexHelper vertices)
    {
        if (!IsActive() || vertices.currentVertCount == 0) return;

        source.Clear();
        vertices.GetUIVertexStream(source);
        // Long/localised save names must not exceed uGUI's 16-bit vertex budget.
        if (source.Count > 64000 / (HaloCopies + 1))
        {
            source.Clear();
            return;
        }

        output.Clear();
        int count = source.Count * (HaloCopies + 1);
        if (output.Capacity < count) output.Capacity = count;

        // Distances are reference-canvas pixels, so the halo follows CanvasScaler.
        AppendRing(3f, 0.025f);
        AppendRing(1.25f, 0.055f);
        output.AddRange(source);
        vertices.Clear();
        vertices.AddUIVertexTriangleStream(output);
        source.Clear();
        output.Clear();
    }

    private void AppendRing(float radius, float opacity)
    {
        for (int direction = 0; direction < Directions.Length; direction++)
        {
            Vector2 offset = Directions[direction] * radius;
            for (int index = 0; index < source.Count; index++)
            {
                UIVertex vertex = source[index];
                Vector3 position = vertex.position;
                position.x += offset.x;
                position.y += offset.y;
                vertex.position = position;
                Color32 color = vertex.color;
                color.a = (byte)Mathf.RoundToInt(color.a * opacity);
                vertex.color = color;
                output.Add(vertex);
            }
        }
    }
}
