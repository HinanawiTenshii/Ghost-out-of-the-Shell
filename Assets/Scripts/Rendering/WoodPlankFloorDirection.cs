using UnityEngine;

/// <summary>Per-sprite plank orientation without cloning or changing the shared floor material.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("Rendering/Wood Plank Floor Direction")]
public sealed class WoodPlankFloorDirection : MonoBehaviour
{
    public enum BoardDirection
    {
        [InspectorName("横向")] Horizontal = 0,
        [InspectorName("纵向")] Vertical = 1
    }

    [SerializeField, Tooltip("仅影响本对象；方向以世界坐标为准。禁用组件则使用材质默认方向。")]
    private BoardDirection direction;

    private static readonly int OverrideId = Shader.PropertyToID("_DirectionOverride");
    private static readonly int DirectionId = Shader.PropertyToID("_Direction");
    private SpriteRenderer targetRenderer;
    private MaterialPropertyBlock properties;
    private Material lastMaterial;
    private bool dirty = true;

    public BoardDirection Direction
    {
        get => direction;
        set
        {
            direction = value == BoardDirection.Vertical ? BoardDirection.Vertical : BoardDirection.Horizontal;
            dirty = true;
            if (isActiveAndEnabled) RefreshDirection();
        }
    }

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        Material material = targetRenderer != null ? targetRenderer.sharedMaterial : null;
        direction = material != null && material.HasProperty(DirectionId) && material.GetFloat(DirectionId) > 0.5f
            ? BoardDirection.Vertical : BoardDirection.Horizontal;
        dirty = true;
    }

    private void OnEnable() => dirty = true;
    // OnValidate can run during deserialization; defer rendering calls to the main-thread update.
    private void OnValidate() => dirty = true;

    private void Update()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer != null && (dirty || lastMaterial != targetRenderer.sharedMaterial)) RefreshDirection();
    }

    [ContextMenu("Refresh Direction / 刷新排列方向")]
    public void RefreshDirection()
    {
        if (!isActiveAndEnabled) return;
        if (targetRenderer == null) targetRenderer = GetComponent<SpriteRenderer>();
        if (targetRenderer == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        // Read first to preserve any unrelated per-renderer parameters.
        targetRenderer.GetPropertyBlock(properties);
        properties.SetFloat(OverrideId, direction == BoardDirection.Vertical ? 1f : 0f);
        targetRenderer.SetPropertyBlock(properties);
        lastMaterial = targetRenderer.sharedMaterial;
        dirty = false;
    }

    private void OnDisable()
    {
        if (targetRenderer == null) return;
        if (properties == null) properties = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(properties);
        // Sentinel restores the live material default, including later edits to that material.
        properties.SetFloat(OverrideId, -1f);
        targetRenderer.SetPropertyBlock(properties);
        dirty = true;
    }
}
