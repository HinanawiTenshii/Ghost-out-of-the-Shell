using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(BoxCollider2D))]
public class PermissionArea : MonoBehaviour
{
    public const int DefaultPermissionLevel = 0;

    [SerializeField, Min(0)] private int permissionLevel = DefaultPermissionLevel;
    [SerializeField] private Vector2 areaSize = new Vector2(4f, 3f);
    [SerializeField] private Vector2 areaOffset;
    [SerializeField] private Color editorColor = new Color(0.25f, 0.75f, 1f, 0.22f);

    private BoxCollider2D areaCollider;

    public int PermissionLevel => Mathf.Max(DefaultPermissionLevel, permissionLevel);
    public Vector2 AreaSize => areaSize;
    public Bounds AreaBounds => GetAreaCollider().bounds;

    private void Awake()
    {
        if (Application.isPlaying)
        {
            ZeldaRuntimeRegistry.Register(this);
        }
        SynchronizeCollider();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            ZeldaRuntimeRegistry.Register(this);
        }
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
        {
            ZeldaRuntimeRegistry.Unregister(this);
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying)
        {
            ZeldaRuntimeRegistry.Unregister(this);
        }
    }

    private void Reset()
    {
        areaSize = new Vector2(4f, 3f);
        areaOffset = Vector2.zero;
        SynchronizeCollider();
    }

    public bool Contains(Vector2 worldPosition)
    {
        return GetAreaCollider().OverlapPoint(worldPosition);
    }

    public bool CanCharacterAccess(ZeldaCharacterData characterData)
    {
        return characterData != null && characterData.PermissionLevel >= permissionLevel;
    }

    public bool CanCharacterAccessArea(ZeldaCharacterData characterData)
    {
        return CanCharacterAccess(characterData) && Contains(characterData.transform.position);
    }

    private BoxCollider2D GetAreaCollider()
    {
        if (areaCollider == null)
        {
            areaCollider = GetComponent<BoxCollider2D>();
        }

        return areaCollider;
    }

    private void SynchronizeCollider()
    {
        BoxCollider2D boxCollider = GetAreaCollider();
        boxCollider.isTrigger = true;
        boxCollider.size = areaSize;
        boxCollider.offset = areaOffset;
    }

    private void OnValidate()
    {
        permissionLevel = Mathf.Max(DefaultPermissionLevel, permissionLevel);
        areaSize.x = Mathf.Max(0.1f, areaSize.x);
        areaSize.y = Mathf.Max(0.1f, areaSize.y);
        SynchronizeCollider();
    }

    private void OnDrawGizmos()
    {
        DrawAreaGizmo(false);
    }

    private void OnDrawGizmosSelected()
    {
        DrawAreaGizmo(true);
    }

    private void DrawAreaGizmo(bool selected)
    {
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;

        Color fillColor = editorColor;
        fillColor.a = selected ? Mathf.Max(0.2f, editorColor.a) : Mathf.Min(0.08f, editorColor.a);
        Gizmos.color = fillColor;
        Gizmos.DrawCube(areaOffset, areaSize);

        Color borderColor = editorColor;
        borderColor.a = selected ? 1f : 0.45f;
        Gizmos.color = borderColor;
        Gizmos.DrawWireCube(areaOffset, areaSize);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;

#if UNITY_EDITOR
        if (selected)
        {
            Vector3 labelPosition = transform.TransformPoint(
                areaOffset + new Vector2(0f, areaSize.y * 0.5f + 0.25f));
            UnityEditor.Handles.color = borderColor;
            UnityEditor.Handles.Label(labelPosition, $"Permission Level: {permissionLevel}");
        }
#endif
    }
}
