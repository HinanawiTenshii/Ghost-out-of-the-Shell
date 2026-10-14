using System.Collections.Generic;
using UnityEngine;

/// <summary>2x movement zone for all characters, authored like PermissionArea.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
[AddComponentMenu("Interaction/Speed Boost Area (加速区域)")]
public sealed class SpeedBoostArea : MonoBehaviour
{
    public const float SpeedMultiplier = 2f;
    [SerializeField] private Vector2 areaSize = new Vector2(4f, 3f);
    [SerializeField] private Vector2 areaOffset;
    [SerializeField] private Color editorColor = new Color(.2f, 1f, .55f, .22f);

    private static readonly HashSet<SpeedBoostArea> activeAreas = new HashSet<SpeedBoostArea>();
    private BoxCollider2D areaCollider;
    public Vector2 AreaSize => areaSize;
    public Bounds AreaBounds => GetAreaCollider().bounds;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => activeAreas.Clear();

    private void OnEnable()
    {
        SynchronizeCollider();
        if (Application.isPlaying) activeAreas.Add(this);
    }
    private void OnDisable() => activeAreas.Remove(this);
    private void OnDestroy() => activeAreas.Remove(this);

    public bool Contains(Vector2 worldPosition)
    {
        BoxCollider2D box = GetAreaCollider();
        return isActiveAndEnabled && box != null && box.enabled && box.OverlapPoint(worldPosition);
    }

    public static float GetSpeedMultiplier(Component actor)
    {
        // AI intentionally disables the player-input mover. Eligibility depends
        // on the body being active, never on who controls it or its AI state.
        if (actor == null || !actor.gameObject.activeInHierarchy) return 1f;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(actor.gameObject);
        foreach (SpeedBoostArea area in activeAreas)
            if (area != null && area.gameObject.scene == scene && area.Contains(actor.transform.position))
                return SpeedMultiplier;
        return 1f;
    }

    private BoxCollider2D GetAreaCollider()
    {
        if (areaCollider == null) areaCollider = GetComponent<BoxCollider2D>();
        return areaCollider;
    }
    private void SynchronizeCollider()
    {
        BoxCollider2D box = GetAreaCollider();
        if (box == null) return;
        box.isTrigger = true;
        box.size = areaSize;
        box.offset = areaOffset;
    }
    private void Reset()
    {
        areaSize = new Vector2(4f, 3f); areaOffset = Vector2.zero;
        SynchronizeCollider();
    }
    private void OnValidate()
    {
        areaSize.x = Mathf.Max(.1f, areaSize.x); areaSize.y = Mathf.Max(.1f, areaSize.y);
        SynchronizeCollider();
    }

    private void OnDrawGizmos() => DrawAreaGizmo(false);
    private void OnDrawGizmosSelected() => DrawAreaGizmo(true);
    private void DrawAreaGizmo(bool selected)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Color fill = editorColor;
        fill.a = selected ? Mathf.Max(.2f, editorColor.a) : Mathf.Min(.08f, editorColor.a);
        Gizmos.color = fill; Gizmos.DrawCube(areaOffset, areaSize);
        Color border = editorColor; border.a = selected ? 1f : .45f;
        Gizmos.color = border; Gizmos.DrawWireCube(areaOffset, areaSize);
        Gizmos.matrix = previousMatrix; Gizmos.color = previousColor;
#if UNITY_EDITOR
        if (selected)
        {
            Color oldHandleColor = UnityEditor.Handles.color;
            UnityEditor.Handles.color = border;
            UnityEditor.Handles.Label(transform.TransformPoint(areaOffset + new Vector2(0, areaSize.y * .5f + .25f)),
                "加速区域 · 角色速度 ×2");
            UnityEditor.Handles.color = oldHandleColor;
        }
#endif
    }
}
