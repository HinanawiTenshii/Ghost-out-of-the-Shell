using System.Collections.Generic;
using UnityEngine;

/// <summary>Each recipe has a separate resource prefab and stable inventory ID.</summary>
public sealed class PotionPickupItem : PickupItemBase
{
    [SerializeField] private AlchemyPotionKind kind;
    [SerializeField, Min(.1f), Tooltip("药水受击碎裂时的圆形飞溅半径（世界单位）。")]
    private float splashRadius = 4f;
    private bool shattered;
    public AlchemyPotionKind Kind => kind;
    public float SplashRadius => Mathf.Max(.1f, splashRadius);
    protected override bool CanAttemptPickup => !shattered;
    public override bool TryPickUp() => !shattered && base.TryPickUp();
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => !shattered && CharacterPotionBuffs.TryApply(user, kind);

    protected override void Awake()
    {
        base.Awake();
        PotionSplashVisual.PrewarmAudio();
    }

    /// <summary>Attack contact, including zero-power hits; no damage/chain explosion is emitted.</summary>
    public void ReceiveAttack(int attackPower, ZeldaCharacterData attacker)
    {
        if (shattered || IsPickupBeingConsumed || !isActiveAndEnabled) return;
        shattered = true; // Death, inventory and save callbacks can reenter synchronously.
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
        Vector3 center = transform.position;
        float radius = SplashRadius;
        gameObject.SetActive(false);
        Destroy(gameObject);
        // Wakes nearby streamed-out bodies before the one-shot query, without revealing them.
        PotionSplashVisual.Spawn(scene, center, radius, AlchemyRecipeCatalog.GetColor(kind));

        var targets = new HashSet<ZeldaCharacterData>();
        var movers = new List<ZeldaFourWayMover>();
        ZeldaRuntimeRegistry.InvalidateSpatialSnapshot();
        ZeldaRuntimeRegistry.QueryNearbyMovers(center, radius, movers);
        foreach (var mover in movers)
            if (mover != null) AddTarget(mover.GetComponent<ZeldaCharacterData>(), targets, center, radius, scene);
        // Covers characters without a mover; center distance rejects huge vision trigger colliders.
        Physics2D.SyncTransforms();
        var colliders = new List<Collider2D>();
        Physics2D.OverlapCircle(center, radius, new ContactFilter2D { useTriggers = true }, colliders);
        foreach (var other in colliders)
            if (other != null) AddTarget(other.GetComponentInParent<ZeldaCharacterData>(), targets, center, radius, scene);
        // Snapshot first: poison can kill bodies and mutate registries/physics during callbacks.
        foreach (var target in targets) CharacterPotionBuffs.TryApplySplash(target, kind);
    }

    private static void AddTarget(ZeldaCharacterData target, HashSet<ZeldaCharacterData> targets,
        Vector2 center, float radius, UnityEngine.SceneManagement.Scene scene)
    {
        if (target == null || !target.gameObject.activeInHierarchy || target.IsDead || target.IsGhostLike ||
            ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != scene ||
            ((Vector2)target.transform.position - center).sqrMagnitude > radius * radius) return;
        targets.Add(target);
    }
}
