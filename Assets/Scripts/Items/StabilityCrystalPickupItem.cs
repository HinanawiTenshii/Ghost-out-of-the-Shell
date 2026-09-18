using UnityEngine;

/// <summary>Restores normal E.G.O, or supplies up to three temporary thirds.</summary>
public sealed class StabilityCrystalPickupItem : PickupItemBase
{
    [SerializeField] private Vector2 burstSize = new Vector2(2.4f, 2.4f);
    private bool burstTriggered;
    public override bool TryPickUp()
    {
        if (IsPickupBlockedForControlledCharacter()) return false;
        var inventory = PersistentInventory.Instance;
        if (inventory == null || !inventory.TryAddItem(ItemId, 1, 10)) return false;
        OnPickedUp(inventory);
        NotifyPickedUp();
        Destroy(gameObject);
        return true;
    }

    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => true;
    public override void OnReleasedFromCardboardBox(ZeldaCharacterAiBase observingAi)
    {
        BurstFromAttack();
    }

    public void BurstFromAttack()
    {
        if (burstTriggered) return;
        burstTriggered = true;
        Vector2 size = new Vector2(Mathf.Max(0.1f, burstSize.x), Mathf.Max(0.1f, burstSize.y));
        Vector3 position = transform.position;
        var effect = new GameObject("Stability Crystal Burst").AddComponent<BombExplosionVisual>();
        effect.transform.position = position;
        var visual = GetComponent<SpriteRenderer>();
        effect.Configure(size, 0.5f, new Color(0.08f, 0.2f, 0.65f, 1f),
            visual != null ? visual.sortingLayerID : 0, 12);

        // One-shot effects, not an attack hitbox: no damage, breakage or chain detonation.
        var affected = new System.Collections.Generic.HashSet<ZeldaCharacterData>();
        var controlled = ZeldaRuntimeRegistry.GetControlledMover();
        foreach (var collider in Physics2D.OverlapBoxAll(position, size, 0f))
        {
            var data = collider.GetComponentInParent<ZeldaCharacterData>();
            if (data == null || data.IsDead || !affected.Add(data)) continue;
            var mover = data.GetComponent<ZeldaFourWayMover>();
            if (mover != null && mover == controlled) data.RestorePossessionEnergy(1);
            else
            {
                var ai = data.GetComponent<ZeldaCharacterAiBase>();
                if (ai != null) ai.Stun();
            }
        }
        Destroy(gameObject);
    }
    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        return user != null && !user.IsDead && user.UseStabilityCrystal();
    }
}
