using System;
using UnityEngine;

/// <summary>Ornate empty potion bottle. R throws; Q uses the normal recoverable drop path.</summary>
public sealed class GlassBottlePickupItem : PickupItemBase
{
    public const string DefaultItemId = "glass_bottle";
    [SerializeField, Min(.1f), Tooltip("投掷速度（世界单位/秒）")] private float throwSpeed = 6f;
    [SerializeField, Min(0), Tooltip("碎裂声音吸引角色调查的半径，0 表示不吸引。")]
    private float investigationRadius = 5f;
    private bool thrown;
    [Serializable] private sealed class BottleState { public int version = 1; public float speed, radius; }
    public float InvestigationRadius => investigationRadius;
    public override string InventoryState => JsonUtility.ToJson(new BottleState { speed = throwSpeed, radius = investigationRadius });
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    public override void ApplyInventoryState(string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return;
        var saved = JsonUtility.FromJson<BottleState>(state);
        if (saved == null || saved.version != 1) return;
        throwSpeed = SafeValue(saved.speed, 6f, .1f);
        investigationRadius = SafeValue(saved.radius, 5f, 0f);
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        if (thrown || user == null || user.IsDead || user.IsGhostLike) return false;
        var mover = user.GetComponent<ZeldaFourWayMover>();
        if (mover == null || !mover.isActiveAndEnabled) return false;
        if (!ThrownGlassBottle.Throw(user, mover.FacingDirection, SafeValue(throwSpeed, 6f, .1f),
            SafeValue(investigationRadius, 5f, 0f), gameObject)) return false;
        thrown = true;
        return true;
    }
    private static float SafeValue(float value, float fallback, float minimum) =>
        float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(minimum, value);

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        throwSpeed = SafeValue(throwSpeed, 6f, .1f);
        investigationRadius = SafeValue(investigationRadius, 5f, 0);
    }
#endif
}
