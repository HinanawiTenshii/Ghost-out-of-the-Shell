using UnityEngine;

/// <summary>A single-shot, consumable hand cannon. Inventory removes one on successful firing.</summary>
public sealed class DisposableHandgunPickupItem : PickupItemBase
{
    protected override int DefaultRequiredUseSkill => CharacterSkillRequirements.Handgun;
    // Its firing visual already owns the raised-hand pose; do not start another gesture.
    public override bool UsesSimpleUseGesture => false;
    public const string DefaultItemId = "disposable_handgun";
    public const string DefaultItemName = "一次性手铳";
    public const string DefaultDescription = "装填了一发子弹的一次性手铳，在发射一发子弹后就会被废弃。";
    [SerializeField, Min(0.1f), Tooltip("子弹速度（世界单位/秒）")]
    private float bulletSpeed = 10f;
    private bool fired;

    public override bool TryPickUp()
    {
        if (IsPickupBlockedForControlledCharacter()) return false;
        var inventory = PersistentInventory.Instance;
        if (inventory == null || !inventory.TryAddItem(ItemId, 1, 3)) return false;
        OnPickedUp(inventory);
        NotifyPickedUp();
        Destroy(gameObject);
        return true;
    }

    protected override void Awake()
    {
        base.Awake();
        DisposableHandgunAudio.Preload();
    }

    // R always fires instead of silently putting the weapon into a nearby box.
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        if (fired || user == null || user.IsDead || user.IsGhostLike) return false;
        var mover = user.GetComponent<ZeldaFourWayMover>();
        if (mover == null || !mover.isActiveAndEnabled) return false;
        if (!DisposableHandgunAttackVisual.TryFire(user, mover, bulletSpeed)) return false;
        fired = true;
        return true;
    }
}
