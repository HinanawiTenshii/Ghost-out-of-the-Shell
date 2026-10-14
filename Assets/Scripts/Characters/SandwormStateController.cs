using UnityEngine;
using UnityEngine.Rendering;

public enum SandwormState
{
    [InspectorName("地下 / Underground")] Underground = 0,
    [InspectorName("地上 / Above Ground")] AboveGround = 1
}

/// <summary>Presentation/collision state only. Neither state adds attacks or changes roaming.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SortingGroup))]
[AddComponentMenu("Characters/Sandworm State Controller")]
public sealed class SandwormStateController : MonoBehaviour
{
    [SerializeField, Tooltip("默认地下：关闭部位碰撞，叠加 RGB(15,33,60)、50% 不透明度蒙版。")]
    private SandwormState state = SandwormState.Underground;

    public static readonly Color32 UndergroundMaskColor = new Color32(15, 33, 60, 255);
    public const float UndergroundMaskOpacity = .5f;
    // Floors use -10; the lowest ordinary decorations use -7. Grouping preserves
    // the authored head-to-tail order (8..1) within this one underground band.
    public const int UndergroundSortingOrder = -9;
    public SandwormState State => state;
    public bool IsUnderground => isActiveAndEnabled && state == SandwormState.Underground;
    private bool segmentedPassage;
    private int[] originalOrders;

    public bool BeginSegmentedPassage(bool allowAboveGround = false)
    {
        if ((!IsUnderground && !(allowAboveGround && isActiveAndEnabled)) || segmentedPassage) return false;
        RefreshParts();
        originalOrders = new int[visuals.Length];
        for (int i = 0; i < visuals.Length; i++) originalOrders[i] = visuals[i].GetComponent<SpriteRenderer>().sortingOrder;
        segmentedPassage = true;
        ApplyState();
        return true;
    }

    public void SetSegmentExposed(SandwormSegmentVisual visual, bool exposed, bool enableExposedCollision = false)
    {
        if (!segmentedPassage || visual == null) return;
        int index = System.Array.IndexOf(visuals, visual);
        if (index < 0) return;
        visual.SetUndergroundOverlay(!exposed);
        visual.GetComponent<SpriteRenderer>().sortingOrder = exposed ? originalOrders[index] : UndergroundSortingOrder;
        if (enableExposedCollision && colliders != null)
            foreach (var collider in colliders)
                if (collider != null && collider.transform == visual.transform) collider.enabled = exposed;
    }

    public void EndSegmentedPassage()
    {
        RestoreSegmentOrders();
        segmentedPassage = false;
        ApplyState();
    }

    private void RestoreSegmentOrders()
    {
        if (originalOrders == null || visuals == null) return;
        for (int i = 0; i < Mathf.Min(visuals.Length, originalOrders.Length); i++)
            if (visuals[i] != null) visuals[i].GetComponent<SpriteRenderer>().sortingOrder = originalOrders[i];
        originalOrders = null;
    }

    private SandwormSegmentVisual[] visuals;
    private Collider2D[] colliders;
    private SortingGroup undergroundSortingGroup;
    private bool dirty = true;

    private void OnEnable() { RefreshParts(); ApplyState(); }
    private void OnValidate()
    {
        if (state != SandwormState.AboveGround) state = SandwormState.Underground;
        dirty = true; // Asset creation/renderer changes must stay out of serialization callbacks.
    }
    private void OnTransformChildrenChanged() { dirty = true; }
    private void Update()
    {
        if (!dirty) return;
        RefreshParts();
        ApplyState();
    }

    public void SetState(SandwormState next)
    {
        state = next == SandwormState.AboveGround ? next : SandwormState.Underground;
        if (visuals == null || colliders == null || dirty) RefreshParts();
        ApplyState();
    }

    [ContextMenu("Enter Underground / 切换为地下")]
    public void EnterUnderground() { SetState(SandwormState.Underground); }

    [ContextMenu("Enter Above Ground / 切换为地上")]
    public void EnterAboveGround() { SetState(SandwormState.AboveGround); }

    private void RefreshParts()
    {
        undergroundSortingGroup = GetComponent<SortingGroup>();
        visuals = GetComponentsInChildren<SandwormSegmentVisual>(true);
        // Only this character's authored body colliders, not future attack triggers or UI.
        var parts = new System.Collections.Generic.List<Collider2D>(visuals.Length);
        foreach (var visual in visuals)
            if (visual != null) parts.AddRange(visual.GetComponents<Collider2D>());
        colliders = parts.ToArray();
    }

    private void ApplyState()
    {
        bool underground = IsUnderground;
        if (undergroundSortingGroup != null)
        {
            undergroundSortingGroup.sortingLayerID = 0; // Existing Default sorting layer, not the physics/vision layer.
            undergroundSortingGroup.sortingOrder = UndergroundSortingOrder;
            undergroundSortingGroup.enabled = underground && !segmentedPassage;
        }
        if (colliders != null)
            foreach (var collider in colliders)
                if (collider != null) collider.enabled = !underground && !segmentedPassage;
        if (visuals != null && !segmentedPassage)
            foreach (var visual in visuals)
                if (visual != null) visual.SetUndergroundOverlay(underground);
        dirty = false;
    }

    private void OnDisable()
    {
        RestoreSegmentOrders();
        segmentedPassage = false;
        // Disabling/removing just this component restores the unmodified character.
        // A disabled whole GameObject still remains inactive in physics/rendering.
        if (undergroundSortingGroup != null) undergroundSortingGroup.enabled = false;
        if (colliders != null)
            foreach (var collider in colliders)
                if (collider != null) collider.enabled = true;
        if (visuals != null)
            foreach (var visual in visuals)
                if (visual != null) visual.SetUndergroundOverlay(false);
        dirty = true;
    }
}
