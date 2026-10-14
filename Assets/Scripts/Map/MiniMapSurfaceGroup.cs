using UnityEngine;

/// <summary>Opt-in map underlay. Put on the Roads/River parent, not individual tiles.
/// Has no Update or physics behaviour; both map views capture its sprite footprints once.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Map/Mini Map Surface Group")]
public sealed class MiniMapSurfaceGroup : MonoBehaviour
{
    public enum SurfaceKind { Road, Water, WaterBank }
    [SerializeField, Tooltip("Map color for this group's sprites. Nested groups may override the parent, e.g. banks inside a water group.")]
    private SurfaceKind kind;
    public SurfaceKind Kind => kind;
}
