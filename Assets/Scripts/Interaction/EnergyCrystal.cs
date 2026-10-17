using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Destructible, scene-authored resurrection anchor. Sleeping is not loss of power.</summary>
[DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Interaction/Energy Crystal")]
public sealed class EnergyCrystal : MonoBehaviour
{
    [Header("Binding / 骷髅绑定（数量不限）")]
    [SerializeField, Tooltip("每名骷髅只能绑定一个水晶。无需把骷髅放进水晶的子层级。")]
    private List<SkeletonZeldaCharacterData> skeletons = new List<SkeletonZeldaCharacterData>();
    // Preserve old prefab-instance radius overrides until a rectangular size is authored.
    [SerializeField, HideInInspector]
    private float activityRadius = 12f;
    [SerializeField, Tooltip("矩形的本地宽、高。旧数据为零时，沿用旧半径的两倍，避免场景范围突然缩小。")]
    private Vector2 activitySize;
    [SerializeField, Tooltip("矩形中心相对水晶的本地偏移，可让水晶位于房间一侧。")]
    private Vector2 activityOffset;
    [SerializeField] private AudioClip shatterSound;
    [SerializeField, Range(0, 1)] private float shatterVolume = .7f;
    private bool broken;
    private EnergyCrystalVisual visual;
    private static readonly HashSet<EnergyCrystal> Instances = new HashSet<EnergyCrystal>();
    public bool IsBroken => broken;
    public event Action<EnergyCrystal> StateChanged;
    public bool IsFunctional => !broken && enabled;
    public Vector2 ActivitySize
    {
        get => activitySize.x > 0 && activitySize.y > 0 ? activitySize : Vector2.one * Mathf.Max(1, activityRadius) * 2;
        set => activitySize = new Vector2(Mathf.Max(.2f, value.x), Mathf.Max(.2f, value.y));
    }
    public Vector2 ActivityOffset { get => activityOffset; set => activityOffset = value; }
    public IReadOnlyList<SkeletonZeldaCharacterData> Skeletons => skeletons;
    public Vector3 CrystalCenter => Visual != null ? Visual.CrystalCenter : transform.position;
    private EnergyCrystalVisual Visual => visual != null ? visual : (visual = GetComponent<EnergyCrystalVisual>());
    [Serializable] public struct RuntimeState { public bool broken; }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => Instances.Clear();
    private void Awake() { Instances.Add(this); RefreshBindings(); }
    private void OnEnable()
    {
        Instances.Add(this); RefreshBindings();
        if (Visual != null) Visual.SetBroken(broken);
    }
    private void OnValidate()
    {
        activityRadius = Mathf.Max(1, activityRadius);
        if (activitySize != Vector2.zero) ActivitySize = activitySize;
    }
    private Vector2 ToAreaPoint(Vector2 position) => (Vector2)transform.InverseTransformPoint(
        new Vector3(position.x, position.y, transform.position.z)) - activityOffset;
    private Vector2 HalfSize(float inset)
    {
        Vector2 half = ActivitySize * .5f;
        Vector3 scale = transform.lossyScale;
        // Insets are world distances, consistent with the existing AI navigation skin.
        return new Vector2(Mathf.Max(.01f, half.x - inset / Mathf.Max(.001f, Mathf.Abs(scale.x))),
            Mathf.Max(.01f, half.y - inset / Mathf.Max(.001f, Mathf.Abs(scale.y))));
    }
    public bool Contains(Vector2 position, float inset = 0f)
    {
        Vector2 point = ToAreaPoint(position), half = HalfSize(inset);
        return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y;
    }
    public Vector2 ClampPosition(Vector2 position, float inset = 0f)
    {
        Vector2 point = ToAreaPoint(position), half = HalfSize(inset);
        return transform.TransformPoint(activityOffset + new Vector2(
            Mathf.Clamp(point.x, -half.x, half.x), Mathf.Clamp(point.y, -half.y, half.y)));
    }
    public float DistanceToAreaSquared(Vector2 position) => (position - ClampPosition(position)).sqrMagnitude;

    /// <summary>Clip the movement segment at its first rectangle edge, without bending it through obstacles.</summary>
    public Vector2 ConstrainDisplacement(Vector2 position, Vector2 displacement)
    {
        if (displacement.sqrMagnitude < .0000001f) return displacement;
        if (!Contains(position))
            return DistanceToAreaSquared(position + displacement) < DistanceToAreaSquared(position) ? displacement : Vector2.zero;
        if (Contains(position + displacement)) return displacement;
        Vector2 start = ToAreaPoint(position), delta = ToAreaPoint(position + displacement) - start;
        Vector2 half = HalfSize(0);
        float t = 1;
        if (delta.x > .000001f) t = Mathf.Min(t, (half.x - start.x) / delta.x);
        else if (delta.x < -.000001f) t = Mathf.Min(t, (-half.x - start.x) / delta.x);
        if (delta.y > .000001f) t = Mathf.Min(t, (half.y - start.y) / delta.y);
        else if (delta.y < -.000001f) t = Mathf.Min(t, (-half.y - start.y) / delta.y);
        return displacement * Mathf.Clamp01(t - .0001f);
    }
    public bool Powers(SkeletonZeldaCharacterData skeleton) => IsFunctional && skeleton != null &&
        skeleton.BoundCrystal == this && ZeldaRuntimeRegistry.GetGameplayScene(skeleton.gameObject) == gameObject.scene;

    public bool BindSkeleton(SkeletonZeldaCharacterData skeleton)
    {
        if (skeleton == null) return false;
        if (skeleton.BoundCrystal != null && skeleton.BoundCrystal != this) return false;
        if (!skeletons.Contains(skeleton)) skeletons.Add(skeleton);
        skeleton.SetEnergyCrystal(this);
        if (skeleton.gameObject.activeInHierarchy) WakeForSkeleton(skeleton);
        return true;
    }
    public void UnbindSkeleton(SkeletonZeldaCharacterData skeleton)
    {
        skeletons.Remove(skeleton);
        if (skeleton != null && skeleton.BoundCrystal == this) skeleton.SetEnergyCrystal(null);
    }
    public void RefreshBindings()
    {
        for (int i = 0; i < skeletons.Count; i++)
            if (skeletons[i] != null && (skeletons[i].BoundCrystal == null || skeletons[i].BoundCrystal == this))
                skeletons[i].SetEnergyCrystal(this);
    }
    internal static void ConnectExistingSkeleton(SkeletonZeldaCharacterData skeleton)
    {
        if (skeleton.BoundCrystal != null) { skeleton.BoundCrystal.WakeForSkeleton(skeleton); return; }
        foreach (var crystal in Instances)
            if (crystal != null && crystal.skeletons.Contains(skeleton)) { crystal.BindSkeleton(skeleton); return; }
    }
    internal void WakeForSkeleton(SkeletonZeldaCharacterData skeleton)
    {
        if (Powers(skeleton) && !gameObject.activeInHierarchy)
            CameraVisionObjectStreaming.RestoreForGameplay(gameObject);
    }
    // Queried only for streaming-managed crystal objects; no global searches or per-frame leases.
    public bool HasActiveBoundSkeletons
    {
        get
        {
            if (!IsFunctional) return false;
            foreach (var skeleton in skeletons)
                if (Powers(skeleton) && skeleton.gameObject.activeInHierarchy) return true;
            return false;
        }
    }
    public void ReceiveAttack(int damage, ZeldaCharacterData attacker = null)
    {
        if (damage <= 0 || broken) return;
        broken = true; // Latch before effects or linked death callbacks.
        StateChanged?.Invoke(this);
        // Detach the currently visible link particles before hiding the intact visuals.
        // The gameplay latch above already prevents resurrection/repeated damage callbacks.
        if (Visual != null) { Visual.PlayShatter(shatterSound, shatterVolume); Visual.SetBroken(true); }
        foreach (var skeleton in skeletons)
            if (skeleton != null && skeleton.BoundCrystal == this) skeleton.OnCrystalPowerLost();
    }
    public RuntimeState CaptureRuntimeState() => new RuntimeState { broken = broken };
    public void ApplyRuntimeState(RuntimeState state)
    {
        broken = state.broken;
        StateChanged?.Invoke(this);
        RefreshBindings();
        if (Visual != null) Visual.SetBroken(broken);
        if (broken) foreach (var skeleton in skeletons)
            if (skeleton != null && skeleton.BoundCrystal == this) skeleton.OnCrystalPowerLost();
    }
    private void OnDestroy()
    {
        Instances.Remove(this);
        foreach (var skeleton in skeletons)
            if (skeleton != null && skeleton.BoundCrystal == this) skeleton.SetEnergyCrystal(null);
    }
}
