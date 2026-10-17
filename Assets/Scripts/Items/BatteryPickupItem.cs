using UnityEngine;

public sealed class BatteryPickupItem : PickupItemBase, IConsumableExplosive
{
    public const string DefaultItemId = "battery";
    public const string DefaultItemName = "魔力电池";
    public const string DefaultDescription = "封存着魔力的便携式能源，装入电池槽后可以驱动与其相连的机关。靠近电池槽按E安装或取回，无法直接使用。注意，电池内部装载的魔力极度不稳定，受到任何冲击都会发生爆炸。";
    public const int ExplosionAttackPower = 4;
    public const int DefaultCapacity = 5;
    private const string ChargePrefix = "battery-charge-v1:";
    [SerializeField, Min(1), InspectorName("电量上限")] private int maximumCharge = DefaultCapacity;
    [SerializeField, Min(0), InspectorName("剩余电量")] private int remainingCharge = DefaultCapacity;
    public int MaximumCharge => Mathf.Max(1, maximumCharge);
    public int RemainingCharge => Mathf.Clamp(remainingCharge, 0, MaximumCharge);
    public override bool HasInventoryCharge => true;
    public override float InventoryCharge => RemainingCharge;
    public override string InventoryState => EncodeCharge(RemainingCharge, MaximumCharge);

    public struct ChargeState { public int remaining, capacity; }

    // Versioned integer state is authoritative; shared float charge is only a compatibility mirror.
    public static string EncodeCharge(int remaining, int capacity)
    {
        capacity = Mathf.Max(1, capacity);
        return ChargePrefix + capacity.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":"
            + Mathf.Clamp(remaining, 0, capacity).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static ChargeState ReadCharge(string state, bool hasCharge, float charge, int capacity = DefaultCapacity)
    {
        capacity = Mathf.Max(1, capacity);
        if (!string.IsNullOrEmpty(state) && state.StartsWith(ChargePrefix, System.StringComparison.Ordinal))
        {
            string[] parts = state.Substring(ChargePrefix.Length).Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int savedCapacity)
                && int.TryParse(parts[1], out int savedRemaining) && savedCapacity > 0)
                return new ChargeState { capacity = savedCapacity, remaining = Mathf.Clamp(savedRemaining, 0, savedCapacity) };
        }
        // Missing old-save metadata means full; an explicitly saved zero stays empty.
        return new ChargeState { capacity = capacity, remaining = hasCharge
            ? Mathf.Clamp(Mathf.RoundToInt(float.IsNaN(charge) || float.IsInfinity(charge) ? 0f : charge), 0, capacity)
            : capacity };
    }

    public override void ApplyInventoryCharge(bool hasCharge, float charge)
    {
        remainingCharge = ReadCharge(null, hasCharge, charge, MaximumCharge).remaining;
    }

    public override void ApplyInventoryState(string state)
    {
        ChargeState value = ReadCharge(state, true, RemainingCharge, MaximumCharge);
        maximumCharge = value.capacity;
        remainingCharge = value.remaining;
    }

    private bool detonationStarted;
    protected override bool CanAttemptPickup => !detonationStarted;
    public override bool CanUseFromInventory => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;

    public override bool TryPickUp() => !detonationStarted && base.TryPickUp();

    /// <summary>Removes the unstable battery without triggering its ground blast.</summary>
    public bool TryConsumeExplosive(out int attackPower)
    {
        attackPower = 0;
        if (detonationStarted || !isActiveAndEnabled) return false;
        detonationStarted = true;
        attackPower = ExplosionAttackPower;
        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    /// <summary>Any positive damage detonates once, including other explosions.</summary>
    public void ReceiveAttack(int damage, ZeldaCharacterData attacker)
    {
        if (damage <= 0 || detonationStarted || !isActiveAndEnabled) return;
        detonationStarted = true;
        PlacedBomb explosion = CreateDetonation(this, attacker);
        if (explosion == null) { detonationStarted = false; return; }
        // Disable pickup/collision immediately; Destroy is deferred until end of frame.
        gameObject.SetActive(false);
        explosion.DetonateImmediately();
        Destroy(gameObject);
    }

    // Both loose and installed batteries use the NORMAL bomb resource, so radius,
    // particles, sound and investigation remain identical if its settings change.
    internal static PlacedBomb CreateDetonation(Component source, ZeldaCharacterData attacker)
    {
        BombPickupItem template = Resources.Load<BombPickupItem>("PickupItems/Bombs/BombPickupItem");
        if (template == null)
        {
            Debug.LogError("魔力电池爆炸失败：缺少普通炸弹预制体。", source);
            return null;
        }
        PlacedBomb explosion = template.CreateConfiguredPlacedBomb(
            source.transform.position, attacker, ExplosionAttackPower);
        explosion.ExplosionSource = source.transform;
        // Attribute damage to the attacker, but keep effects in the battery's scene.
        var scene = source.gameObject.scene;
        if (scene.IsValid() && scene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(explosion.gameObject, scene);
        return explosion;
    }
}
