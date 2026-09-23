using UnityEngine;

/// <summary>Calm standing water using the same clipped geometric crests as SpriteWaterFlowParticles, without current.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Sprite Still Water Surface")]
public sealed class SpriteStillWaterSurface : MonoBehaviour
{
    [SerializeField, Tooltip("指定水面 SpriteRenderer；留空查找自身或子对象。喷泉请选 Water，不要选石边。")]
    private SpriteRenderer sourceRenderer;
    [SerializeField] private SpriteWaterFlowParticles.StillWaterSettings settings =
        new SpriteWaterFlowParticles.StillWaterSettings();

    private SpriteWaterFlowParticles runtimeEffect;
    private SpriteRenderer configuredRenderer;
    private bool dirty = true;

    public float SurfaceWorldArea => runtimeEffect != null ? runtimeEffect.SurfaceWorldArea : 0f;
    public int TargetRippleCount => runtimeEffect != null ? runtimeEffect.TargetParticleCount : 0;

    private void OnEnable() => dirty = true;

    private void Update()
    {
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null) sourceRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (sourceRenderer == null)
        {
            if (runtimeEffect != null) runtimeEffect.gameObject.SetActive(false);
            dirty = true;
            return;
        }
        if (runtimeEffect != null && !dirty && configuredRenderer == sourceRenderer) return;

        if (runtimeEffect == null)
        {
            // An owned child avoids modifying/stealing any authored flow or particle component.
            var child = new GameObject("Still Water Surface (Runtime)") { hideFlags = HideFlags.HideAndDontSave };
            child.SetActive(false);
            child.transform.SetParent(transform, false);
            runtimeEffect = child.AddComponent<SpriteWaterFlowParticles>();
        }
        runtimeEffect.ConfigureStillWater(sourceRenderer, settings);
        configuredRenderer = sourceRenderer;
        runtimeEffect.gameObject.SetActive(true);
        dirty = false;
    }

    [ContextMenu("Refresh Water Surface / 刷新静态水面")]
    public void RefreshWaterSurface() => dirty = true;

    private void OnValidate() => dirty = true;

    private void OnDisable()
    {
        if (runtimeEffect != null) runtimeEffect.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (runtimeEffect != null) Destroy(runtimeEffect.gameObject);
    }
}
