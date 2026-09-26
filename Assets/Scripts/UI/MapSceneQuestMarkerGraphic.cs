using UnityEngine;
using UnityEngine.UI;

/// <summary>Region-list badge using exactly the same geometry and colors as the map target.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class MapSceneQuestMarkerGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = rectTransform.rect;
        RuntimeMiniMapGraphic.AddQuestTargetMarker(
            vh, rect.center, Mathf.Min(rect.width, rect.height) / 28f);
    }
}
