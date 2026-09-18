using UnityEngine;
using UnityEngine.Events;

/// <summary>Two sliding leaves, meeting at the invisible root's local origin.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("Interaction/Double Sliding Door")]
public sealed class DoubleSlidingDoor : MonoBehaviour
{
    [Header("Door Leaves (Length X / Thickness Y)")]
    [SerializeField] private SpriteRenderer negativeLeaf;
    [SerializeField] private SpriteRenderer positiveLeaf;
    [SerializeField] private Vector2 negativeSize = new Vector2(1.5f, 0.25f);
    [SerializeField] private Vector2 positiveSize = new Vector2(1.5f, 0.25f);
    [SerializeField] private Color negativeColor = new Color(0.65f, 0.72f, 0.75f, 1f);
    [SerializeField] private Color positiveColor = new Color(0.65f, 0.72f, 0.75f, 1f);
    [SerializeField] private bool solidLeaves = true;

    [Header("Sliding")]
    [Tooltip("Initial state. Can also be changed during play to drive the door condition.")]
    [SerializeField] private bool open;
    [Min(0.01f), SerializeField] private float moveDuration = 0.8f;
    [Tooltip("Travel of EACH leaf. Zero automatically uses its own length, fully clearing the original doorway.")]
    [Min(0f), SerializeField] private float slideDistance;
    [Min(0f), SerializeField] private float extraTravel = 0.05f;

    [Header("Completion Events")]
    [SerializeField] private UnityEvent onOpened = new UnityEvent();
    [SerializeField] private UnityEvent onClosed = new UnityEvent();

    private float progress;
    private bool initialized;
    private bool refreshRequired = true;
    private BoxCollider2D negativeCollider;
    private BoxCollider2D positiveCollider;
    // Only created by the bridge interlock; ordinary sliding doors are unchanged.
    private BoxCollider2D safetyBarrier;
    private float safetyCloseDuration;
    public bool IsOpenRequested => open;
    public bool IsFullyOpen => progress >= 1f;
    public bool IsFullyClosed => progress <= 0f;
    public bool IsMoving => Application.isPlaying && !Mathf.Approximately(progress, open ? 1f : 0f);

    /// <summary>Map-only leaf rectangles at an endpoint, independent of live animation.</summary>
    public void AppendMapEndpointPaths(bool opened, System.Collections.Generic.List<Vector2[]> paths)
    {
        AppendMapLeaf(negativeSize, -1f, opened, paths);
        AppendMapLeaf(positiveSize, 1f, opened, paths);
    }

    private void AppendMapLeaf(Vector2 size, float sign, bool opened, System.Collections.Generic.List<Vector2[]> paths)
    {
        size = ClampSize(size);
        float travel = (slideDistance > 0f ? slideDistance : size.x) + Mathf.Max(0f, extraTravel);
        Vector2 center = new Vector2(sign * (size.x * 0.5f + (opened ? travel : 0f)), 0f);
        paths.Add(RuntimeMiniMapSceneData.CreateBoxPath(transform.localToWorldMatrix, center, size));
    }

    [ContextMenu("Open Door")]
    public void Open() => SetOpen(true);
    [ContextMenu("Close Door")]
    public void Close() => SetOpen(false);
    public void ToggleFromExternal() => SetOpen(!open);
    public void SetConditionSatisfied(bool satisfied) => SetOpen(satisfied);
    public void SetOpen(bool value)
    {
        open = value;
        if (value) safetyCloseDuration = 0f;
        refreshRequired = true;
    }

    /// <summary>Immediately block the closed doorway, then animate the leaves shut.</summary>
    public void CloseWithSafetyBarrier(float duration)
    {
        if (safetyBarrier != null && safetyBarrier.enabled && !open && safetyCloseDuration > 0f)
            return; // Repeated bridge synchronization must not restart the animation.

        if (safetyBarrier == null)
        {
            GameObject barrierObject = new GameObject("Bridge Passage Safety Collider");
            // Keep a real scene physics object. NotEditable also lets the save
            // system skip this generated helper without hiding/removing it from the scene.
            barrierObject.hideFlags = HideFlags.NotEditable;
            barrierObject.layer = negativeLeaf != null ? negativeLeaf.gameObject.layer : gameObject.layer;
            barrierObject.transform.SetParent(transform, false);
            safetyBarrier = barrierObject.AddComponent<BoxCollider2D>();
            safetyBarrier.enabled = false;
        }

        Vector2 negative = ClampSize(negativeSize);
        Vector2 positive = ClampSize(positiveSize);
        safetyBarrier.isTrigger = false;
        safetyBarrier.offset = new Vector2((positive.x - negative.x) * 0.5f, 0f);
        safetyBarrier.size = new Vector2(negative.x + positive.x, Mathf.Max(negative.y, positive.y));
        safetyBarrier.enabled = true;
        // Character motion uses physics casts; publish the barrier this frame,
        // even when Physics2D.autoSyncTransforms is disabled.
        Physics2D.SyncTransforms();
        CameraCircularVision.NotifyBlockersChanged(GetLeafBounds());

        safetyCloseDuration = Mathf.Max(0.01f, duration);
        SetOpen(false);
    }

    /// <summary>Synchronize visuals and solid leaves immediately for an interlock or initialization.</summary>
    public void SetOpenImmediately(bool value)
    {
        SetOpen(value);
        progress = value ? 1f : 0f;
        initialized = true;
        Update();
    }

    private void OnEnable()
    {
        CacheColliders();
        if (!initialized || !Application.isPlaying)
        {
            progress = open ? 1f : 0f;
            initialized = true;
        }
        refreshRequired = true;
    }

    private void OnValidate()
    {
        negativeSize = ClampSize(negativeSize);
        positiveSize = ClampSize(positiveSize);
        moveDuration = Mathf.Max(0.01f, moveDuration);
        slideDistance = Mathf.Max(0f, slideDistance);
        extraTravel = Mathf.Max(0f, extraTravel);
        // OnValidate may run on an import thread; apply Unity component changes in Update.
        refreshRequired = true;
    }

    private static Vector2 ClampSize(Vector2 size)
    {
        float length = Mathf.Max(0.01f, size.x);
        return new Vector2(length, Mathf.Clamp(size.y, 0.01f, length));
    }

    private void CacheColliders()
    {
        negativeCollider = negativeLeaf != null ? negativeLeaf.GetComponent<BoxCollider2D>() : null;
        positiveCollider = positiveLeaf != null ? positiveLeaf.GetComponent<BoxCollider2D>() : null;
    }

    private void Update()
    {
        float target = open ? 1f : 0f;
        float previous = progress;
        float duration = !open && safetyCloseDuration > 0f ? safetyCloseDuration : moveDuration;
        progress = Application.isPlaying
            ? Mathf.MoveTowards(progress, target, Time.deltaTime / Mathf.Max(0.01f, duration))
            : target;
        if (!refreshRequired && Mathf.Approximately(previous, progress)) return;

        Bounds affected = GetLeafBounds();
        if (refreshRequired) CacheColliders();
        float eased = Mathf.SmoothStep(0f, 1f, progress);
        ApplyLeaf(negativeLeaf, negativeCollider, negativeSize, negativeColor, -1f, eased);
        ApplyLeaf(positiveLeaf, positiveCollider, positiveSize, positiveColor, 1f, eased);
        // Keep blocking while closed/rotating and through the opening animation.
        // Release only when the selected passage has fully cleared.
        if (open && IsFullyOpen && safetyBarrier != null && safetyBarrier.enabled)
        {
            safetyBarrier.enabled = false;
            Physics2D.SyncTransforms();
        }
        refreshRequired = false;
        if (Application.isPlaying)
        {
            affected.Encapsulate(GetLeafBounds());
            CameraCircularVision.NotifyBlockersChanged(affected);
            if (!Mathf.Approximately(previous, progress) && Mathf.Approximately(progress, target))
            {
                if (open) onOpened.Invoke(); else onClosed.Invoke();
            }
        }
    }

    private void ApplyLeaf(SpriteRenderer leaf, BoxCollider2D leafCollider, Vector2 size,
        Color tint, float sign, float amount)
    {
        if (leaf == null || leaf.sprite == null) return;
        size = ClampSize(size);
        Vector2 spriteSize = leaf.sprite.bounds.size;
        float travel = (slideDistance > 0f ? slideDistance : size.x) + Mathf.Max(0f, extraTravel);
        leaf.transform.localRotation = Quaternion.identity;
        leaf.transform.localScale = new Vector3(size.x / Mathf.Max(0.001f, spriteSize.x),
            size.y / Mathf.Max(0.001f, spriteSize.y), 1f);
        leaf.transform.localPosition = new Vector3(sign * (size.x * 0.5f + travel * amount), 0f, 0f);
        leaf.color = tint;
        if (leafCollider != null)
        {
            leafCollider.enabled = solidLeaves;
            leafCollider.isTrigger = false;
            leafCollider.offset = Vector2.zero;
            leafCollider.size = spriteSize;
        }
    }

    private Bounds GetLeafBounds()
    {
        Bounds result = new Bounds(transform.position, Vector3.zero);
        if (negativeLeaf != null) result.Encapsulate(negativeLeaf.bounds);
        if (positiveLeaf != null) result.Encapsulate(positiveLeaf.bounds);
        if (safetyBarrier != null && safetyBarrier.enabled) result.Encapsulate(safetyBarrier.bounds);
        return result;
    }

    private void OnDestroy()
    {
        if (safetyBarrier == null) return;
        if (Application.isPlaying) Destroy(safetyBarrier.gameObject);
        else DestroyImmediate(safetyBarrier.gameObject);
    }

    private void OnDisable()
    {
        if (Application.isPlaying) CameraCircularVision.NotifyBlockersChanged(GetLeafBounds());
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        float halfHeight = Mathf.Max(negativeSize.y, positiveSize.y) * 0.5f + 0.15f;
        Gizmos.DrawLine(new Vector3(0f, -halfHeight, 0f), new Vector3(0f, halfHeight, 0f));
        Gizmos.matrix = previous;
    }
}
