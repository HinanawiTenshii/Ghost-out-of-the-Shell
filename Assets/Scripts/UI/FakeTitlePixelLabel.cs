using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Keep native-size glyphs at every resolution. Standard dynamic Text rerasterizes
/// at Canvas.scaleFactor, which distorts a 12px pixel font at fractional scales.
/// The RectTransform enlarges the fixed grid; the material samples atlas texels.
/// </summary>
[AddComponentMenu("UI/Fake Title Pixel Label")]
public sealed class FakeTitlePixelLabel : Text
{
    private readonly UIVertex[] quad = new UIVertex[4];

    private TextGenerationSettings NativeSettings(Vector2 extents)
    {
        var settings = GetGenerationSettings(extents);
        settings.scaleFactor = 1f;
        settings.resizeTextForBestFit = false;
        return settings;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (font == null) return;
        m_DisableFontTextureRebuiltCallback = true;
        try
        {
            cachedTextGenerator.PopulateWithErrors(text, NativeSettings(rectTransform.rect.size), gameObject);
            var vertices = cachedTextGenerator.verts;
            if (vertices.Count == 0) return;
            Vector3 first = vertices[0].position;
            var offset = new Vector3(Mathf.Round(first.x) - first.x, Mathf.Round(first.y) - first.y, 0f);
            for (int i = 0; i < vertices.Count; i++)
            {
                int corner = i & 3;
                quad[corner] = vertices[i];
                quad[corner].position += offset;
                if (corner == 3) mesh.AddUIVertexQuad(quad);
            }
        }
        finally { m_DisableFontTextureRebuiltCallback = false; }
    }

    public override float preferredWidth => cachedTextGeneratorForLayout.GetPreferredWidth(text, NativeSettings(Vector2.zero));
    public override float preferredHeight => cachedTextGeneratorForLayout.GetPreferredHeight(text, NativeSettings(new Vector2(rectTransform.rect.width, 0f)));
}
