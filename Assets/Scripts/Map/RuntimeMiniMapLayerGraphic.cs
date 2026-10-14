using UnityEngine;
using UnityEngine.UI;

// Runtime-owned child; it never handles input or creates a separate Canvas.
public sealed class RuntimeMiniMapLayerGraphic : MaskableGraphic
{
    public RuntimeMiniMapGraphic Owner { get; set; }
    public bool DrawWalls { get; set; }
    public bool DrawSurfaces { get; set; }
    public int MeshBuildCount { get; private set; }
    private Rect lastLocalRect;
    private bool hasLocalRect;

    protected override void OnRectTransformDimensionsChange()
    {
        // Camera-space/pixel-perfect canvases send this notification when the
        // camera moves, even if our LOCAL rectangle is unchanged. uGUI's base
        // implementation dirties the mesh unconditionally, bypassing the map's
        // geometry-version cache. Real resizes still follow the normal path;
        // zoom, water/bridge changes and marker animation dirty explicitly.
        Rect current = rectTransform.rect;
        if (hasLocalRect && current.Equals(lastLocalRect)) return;
        lastLocalRect = current;
        hasLocalRect = true;
        base.OnRectTransformDimensionsChange();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (Owner == null || !Owner.isActiveAndEnabled) return;
        MeshBuildCount++;
        if (DrawSurfaces) Owner.PopulateSurfaces(vh);
        else if (DrawWalls) Owner.PopulateWalls(vh); else Owner.PopulateMarkers(vh);
    }
}
