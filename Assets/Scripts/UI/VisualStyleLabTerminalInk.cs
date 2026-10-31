using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation-only vertex colors: gameplay remains the owner of Image.color/state.</summary>
[DisallowMultipleComponent]
public sealed class VisualStyleLabTerminalInk : BaseMeshEffect
{
    public enum Treatment { Border, Panel, MapWalls, MapSurfaces, Text }
    public Treatment Mode { get; set; }
    public bool SolidWindow { get; set; }
    private float reveal = 1;
    public void SetReveal(float value)
    {
        if (Mathf.Approximately(reveal, value)) return;
        reveal = value;
        graphic.SetVerticesDirty();
    }
    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;
        UIVertex v = default;
        float center = graphic.rectTransform.rect.center.x;
        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref v, i);
            Color c = v.color;
            switch (Mode)
            {
                case Treatment.Text:
                    // Replace the dark-blue phosphor core, not semantic/disabled/rich-text colours.
                    // This supplies legibility without the 24 halo copies used by title lettering.
                    if (c.r < .25f && c.g > .35f && c.b > .60f && c.b > c.g * 1.2f)
                        c = new Color(.48f, .80f, .90f, c.a);
                    break;
                case Treatment.Panel:
                    c = new Color(.012f, .035f, .065f, SolidWindow ? Mathf.Max(.97f, c.a) : c.a);
                    v.position.x = center + (v.position.x - center) * reveal;
                    c.a *= Mathf.Clamp01(reveal * 3);
                    break;
                case Treatment.MapWalls: c *= new Color(.40f, .58f, .70f, 1); break;
                case Treatment.MapSurfaces: c *= new Color(.78f, .82f, .86f, 1); break;
                default:
                    // Leave red/amber/quest colours alone; dim only the ordinary cyan frame.
                    if (c.b > c.r * 1.15f && c.g > c.r)
                        c *= new Color(.75f, .62f, .72f, 1);
                    break;
            }
            v.color = c;
            vh.SetUIVertex(v, i);
        }
    }
}
