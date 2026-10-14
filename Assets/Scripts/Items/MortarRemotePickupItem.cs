using UnityEngine;

/// <summary>The saved charge field stores remaining cooldown, including when dropped.</summary>
public sealed class MortarRemotePickupItem : PickupItemBase
{
    public const string DefaultItemId = "mortar_remote";
    public const float Cooldown = 5f;
    public const string AllowedScene = "Level2-Desert";
    [SerializeField, Min(0f)] private float remainingCooldown;
    [SerializeField, Min(.1f), Tooltip("呼叫到炮弹开始下落的延迟（秒）。")]
    private float callDelay = .8f;
    public float CallDelay => Mathf.Max(.1f, callDelay);
    public override bool HasInventoryCharge => true;
    public override float InventoryCharge => remainingCooldown;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    public override void ApplyInventoryCharge(bool hasCharge, float charge) =>
        remainingCooldown = SanitizeCooldown(hasCharge ? charge : 0f);
    public static float SanitizeCooldown(float value) =>
        float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp(value, 0f, Cooldown);
    protected override void Update()
    {
        if (!GameSaveSystem.IsLoading) remainingCooldown = Mathf.Max(0f, remainingCooldown - Time.deltaTime);
        base.Update();
    }
    // Inventory owns reusable-use success and cooldown atomically; this fallback
    // deliberately does not consume the remote or spawn an untracked extra shot.
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
