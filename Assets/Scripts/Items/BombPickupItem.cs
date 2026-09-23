using UnityEngine;

/// <summary>A stackable pickup that places a timed bomb when used.</summary>
public sealed class BombPickupItem : PickupItemBase
{
    private bool detonationStarted;

    [Header("Bomb Fuse")]
    [SerializeField, Min(0.05f)] private float fuseDuration = 2f;
    [SerializeField, Min(0.02f)] private float flashInterval = 0.16f;
    [SerializeField] private Color bombColor = new Color(0.16f, 0.18f, 0.22f, 1f);
    [SerializeField] private bool usePlacedBombHighlight;
    [SerializeField] private Color placedBombHighlightColor =
        new Color(0.32f, 0.35f, 0.42f, 1f);
    [SerializeField] private Color flashColor = Color.red;
    [SerializeField, Min(0.05f)] private float placedBombSize = 0.55f;

    [Header("Explosion Hitbox")]
    [SerializeField, Min(0)] private int explosionAttackPower = 3;
    [SerializeField] private Vector2 explosionSize = new Vector2(2.4f, 2.4f);
    [SerializeField, Min(0.02f)] private float explosionHitboxDuration = 0.18f;
    [SerializeField] private Color explosionHitboxColor =
        new Color(1f, 0.2f, 0.08f, 0.5f);

    [Header("Explosion Audio")]
    [Tooltip("留空使用项目默认复古炸弹爆炸声；音量为 0 时静音。")]
    [SerializeField] private AudioClip explosionSound;
    [SerializeField, Range(0f, 1f)] private float explosionSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float explosionSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] private float explosionSoundSpatialBlend = 0.85f;
    [SerializeField, Min(0.01f)] private float explosionSoundMaxDistance = 24f;

    [Header("AI Investigation Wave")]
    [SerializeField, Min(0f)] private float investigationRadius = 5f;
    [SerializeField, Min(0.05f)] private float investigationPulseDuration = 0.7f;
    [SerializeField] private Color investigationPulseColor = Color.white;
    [SerializeField] private int effectSortingOrder = 10;

    [Header("Placed Bomb Aura")]
    [SerializeField] private bool emitPlacedBombParticles;
    [SerializeField] private Color placedBombParticleColor =
        new Color(0.62f, 0.24f, 0.9f, 0.85f);
    [SerializeField, Min(0f)] private float placedBombParticlesPerSecond = 18f;
    [SerializeField, Min(0.01f)] private float placedBombParticleLifetime = 0.9f;
    [SerializeField, Min(0f)] private float placedBombParticleRadius = 0.18f;
    [SerializeField, Min(0f)] private float placedBombParticleSpeed = 0.55f;
    [SerializeField, Min(0.001f)] private float placedBombParticleSize = 0.09f;

    public override bool TryPickUp()
    {
        if (IsPickupBlockedForControlledCharacter())
        {
            return false;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null ||
            !inventory.TryAddItem(ItemId, 1, MaxStackSize))
        {
            return false;
        }

        OnPickedUp(inventory);
        NotifyPickedUp();
        Destroy(gameObject);
        return true;
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        CreateConfiguredPlacedBomb(transform.position, user);
        return true;
    }

    /// <summary>Detonates a bomb pickup immediately when an attack reaches it.</summary>
    public void DetonateFromAttack(ZeldaCharacterData triggeringCharacter)
    {
        if (detonationStarted)
        {
            return;
        }

        detonationStarted = true;
        PlacedBomb placedBomb = CreateConfiguredPlacedBomb(
            transform.position,
            triggeringCharacter);
        placedBomb.DetonateImmediately();
        Destroy(gameObject);
    }

    public override void OnReleasedFromCardboardBox(
        ZeldaCharacterAiBase observingAi)
    {
        ZeldaCharacterData triggeringCharacter = observingAi != null
            ? observingAi.GetComponent<ZeldaCharacterData>()
            : null;
        DetonateFromAttack(triggeringCharacter);
    }

    public PlacedBomb CreateConfiguredPlacedBomb(
        Vector3 worldPosition,
        ZeldaCharacterData user)
    {
        GameObject placedObject = new GameObject("Placed Bomb");
        placedObject.transform.position = worldPosition;
        var placementScene = ZeldaRuntimeRegistry.GetGameplayScene(user != null ? user.gameObject : gameObject);
        // Save restoration may invoke this method on a prefab asset: in that
        // case the new object already belongs to the active destination scene.
        if (placementScene.IsValid() && placementScene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(placedObject, placementScene);
        PlacedBomb placedBomb = placedObject.AddComponent<PlacedBomb>();
        placedBomb.SaveSourceItemId = ItemId;
        placedBomb.Configure(
            fuseDuration,
            flashInterval,
            bombColor,
            usePlacedBombHighlight,
            placedBombHighlightColor,
            flashColor,
            placedBombSize,
            explosionAttackPower,
            explosionSize,
            explosionHitboxDuration,
            explosionHitboxColor,
            explosionSound,
            explosionSoundVolume,
            explosionSoundPitch,
            explosionSoundSpatialBlend,
            explosionSoundMaxDistance,
            investigationRadius,
            investigationPulseDuration,
            investigationPulseColor,
            effectSortingOrder,
            user,
            emitPlacedBombParticles,
            placedBombParticleColor,
            placedBombParticlesPerSecond,
            placedBombParticleLifetime,
            placedBombParticleRadius,
            placedBombParticleSpeed,
            placedBombParticleSize);
        return placedBomb;
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        fuseDuration = Mathf.Max(0.05f, fuseDuration);
        flashInterval = Mathf.Max(0.02f, flashInterval);
        placedBombSize = Mathf.Max(0.05f, placedBombSize);
        explosionAttackPower = Mathf.Max(0, explosionAttackPower);
        explosionSize.x = Mathf.Max(0.05f, explosionSize.x);
        explosionSize.y = Mathf.Max(0.05f, explosionSize.y);
        explosionHitboxDuration = Mathf.Max(0.02f, explosionHitboxDuration);
        explosionSoundVolume = Mathf.Clamp01(explosionSoundVolume);
        explosionSoundPitch = Mathf.Clamp(explosionSoundPitch, 0.1f, 3f);
        explosionSoundSpatialBlend = Mathf.Clamp01(explosionSoundSpatialBlend);
        explosionSoundMaxDistance = Mathf.Max(0.01f, explosionSoundMaxDistance);
        investigationRadius = Mathf.Max(0f, investigationRadius);
        investigationPulseDuration = Mathf.Max(0.05f, investigationPulseDuration);
        placedBombParticlesPerSecond = Mathf.Max(0f, placedBombParticlesPerSecond);
        placedBombParticleLifetime = Mathf.Max(0.01f, placedBombParticleLifetime);
        placedBombParticleRadius = Mathf.Max(0f, placedBombParticleRadius);
        placedBombParticleSpeed = Mathf.Max(0f, placedBombParticleSpeed);
        placedBombParticleSize = Mathf.Max(0.001f, placedBombParticleSize);
    }
#endif
}
