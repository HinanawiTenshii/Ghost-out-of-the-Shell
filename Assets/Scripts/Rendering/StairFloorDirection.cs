using UnityEngine;

/// <summary>Per-sprite stair ascent orientation; shared material and other renderer properties stay intact.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("Rendering/Stair Floor Direction")]
public sealed class StairFloorDirection : MonoBehaviour
{
    public enum StairDirection
    {
        [InspectorName("横向（左右上楼）")] Horizontal = 0,
        [InspectorName("纵向（上下上楼）")] Vertical = 1
    }

    [SerializeField, Tooltip("按世界坐标选择上楼轴：横向为左右，纵向为上下。台阶边沿与上楼轴垂直。")]
    private StairDirection direction = StairDirection.Vertical;
    [SerializeField, Tooltip("默认向右 / 向上；勾选后向左 / 向下，并翻转台阶亮暗排列。")]
    private bool reverse;

    private static readonly int OverrideId = Shader.PropertyToID("_StairDirectionOverride");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private SpriteRenderer targetRenderer;
    private MaterialPropertyBlock properties;
    private Material lastMaterial;
    private bool dirty = true;

    public StairDirection Direction
    {
        get => direction;
        set
        {
            direction = value == StairDirection.Horizontal ? StairDirection.Horizontal : StairDirection.Vertical;
            dirty = true;
            if (isActiveAndEnabled) RefreshDirection();
        }
    }

    public bool Reverse
    {
        get => reverse;
        set
        {
            reverse = value;
            dirty = true;
            if (isActiveAndEnabled) RefreshDirection();
        }
    }

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        Material material = targetRenderer != null ? targetRenderer.sharedMaterial : null;
        float ascent = material != null && material.HasProperty(DirectionId) ? material.GetFloat(DirectionId) : 0f;
        direction = ascent > 1.5f ? StairDirection.Horizontal : StairDirection.Vertical;
        reverse = ascent > 0.5f && ascent <= 2.5f;
        dirty = true;
    }

    private void OnEnable() => dirty = true;
    // Inspector and Undo changes are applied on the main thread, not during deserialization.
    private void OnValidate() => dirty = true;

    private void Update()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer != null && (dirty || lastMaterial != targetRenderer.sharedMaterial)) RefreshDirection();
    }

    [ContextMenu("Refresh Direction / 刷新楼梯方向")]
    public void RefreshDirection()
    {
        if (!isActiveAndEnabled) return;
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(properties);
        float ascent = direction == StairDirection.Horizontal ? (reverse ? 2f : 3f) : (reverse ? 1f : 0f);
        properties.SetFloat(OverrideId, ascent);
        targetRenderer.SetPropertyBlock(properties);
        lastMaterial = targetRenderer.sharedMaterial;
        dirty = false;
    }

    private void OnDisable()
    {
        if (targetRenderer == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(properties);
        // Restore the live shared-material default without clearing anyone else's parameters.
        properties.SetFloat(OverrideId, -1f);
        targetRenderer.SetPropertyBlock(properties);
        dirty = true;
    }
}
