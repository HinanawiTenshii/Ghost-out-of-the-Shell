using UnityEngine;
using UnityEngine.UI;

/// <summary>Eight short phosphor corner strokes, rather than another glowing full rectangle.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class VisualStyleLabTerminalChrome : MaskableGraphic
{
    public bool Compact;
    public static VisualStyleLabTerminalChrome Create(RectTransform parent, bool compact)
    {
        var go = new GameObject("Lab8 Terminal corners", typeof(RectTransform), typeof(CanvasRenderer));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var chrome = go.AddComponent<VisualStyleLabTerminalChrome>();
        chrome.Compact = compact;
        chrome.color = new Color(.40f, .88f, .94f, .95f);
        chrome.raycastTarget = false;
        TitleScreenGlow.Attach(chrome);
        chrome.GetComponent<TitleScreenGlow>().ConfigureHalo(.65f, .65f);
        return chrome;
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = rectTransform.rect;
        float inset = Compact ? 1 : 2;
        float length = Mathf.Min(Compact ? 6 : 19, Mathf.Min(r.width, r.height) * .17f);
        float thickness = Compact ? 1.25f : 2;
        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++)
        {
            float px = x == 0 ? r.xMin + inset : r.xMax - inset;
            float py = y == 0 ? r.yMin + inset : r.yMax - inset;
            float sx = x == 0 ? 1 : -1, sy = y == 0 ? 1 : -1;
            Quad(vh, px, py, px + sx * length, py + sy * thickness);
            Quad(vh, px, py, px + sx * thickness, py + sy * length);
        }
    }
    private void Quad(VertexHelper vh, float x1, float y1, float x2, float y2)
    {
        int start = vh.currentVertCount;
        vh.AddVert(new Vector3(Mathf.Min(x1,x2), Mathf.Min(y1,y2)), color, Vector2.zero);
        vh.AddVert(new Vector3(Mathf.Min(x1,x2), Mathf.Max(y1,y2)), color, Vector2.zero);
        vh.AddVert(new Vector3(Mathf.Max(x1,x2), Mathf.Max(y1,y2)), color, Vector2.zero);
        vh.AddVert(new Vector3(Mathf.Max(x1,x2), Mathf.Min(y1,y2)), color, Vector2.zero);
        vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
    }
}
