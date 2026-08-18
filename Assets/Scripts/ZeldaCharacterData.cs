using System;
using UnityEngine;

public class ZeldaCharacterData : MonoBehaviour
{
    public const int MinimumPermissionLevel = -2;
    public const int MaximumPermissionLevel = 9;

    [Serializable]
    public struct RuntimeState
    {
        public string characterType;
        public int health;
        public int currentHealth;
        public int skillValue;
        public int permissionLevel;
        public int possessionEnergy;
        public int possessionCost;
        public int spentGrowthPossessionEnergy;
    }

    private const string VisualObjectName = "Visual";

    [SerializeField] private int health = 6;
    [SerializeField] private int skillValue;
    [SerializeField, Range(MinimumPermissionLevel, MaximumPermissionLevel)]
    private int permissionLevel;
    [SerializeField] private int possessionEnergy = 3;
    [SerializeField] private int possessionCost = 1;
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float deathExplosionDuration = 0.35f;
    [SerializeField] private float deathExplosionScale = 1.9f;
    [SerializeField] private Color deathExplosionTint = new Color(1f, 0.65f, 0.12f, 1f);
    [SerializeField] private float damageFeedbackDuration = 0.18f;
    [SerializeField] private Color damageTint = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float damageShakeAmount = 0.04f;
    [SerializeField] private float damageShakeSpeed = 80f;
    [Header("Attack Audio")]
    [SerializeField] private AudioClip attackSound;
    [SerializeField, Range(0f, 1f)] private float attackSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float attackSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] private float attackSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] private float attackSoundMaxDistance = 16f;
    [Header("Damage Audio")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField, Range(0f, 1f)] private float damageSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float damageSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] private float damageSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] private float damageSoundMaxDistance = 16f;
    [Header("Death Audio")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float deathSoundVolume = 1f;
    [SerializeField, Range(0.1f, 3f)] private float deathSoundPitch = 1f;
    [SerializeField, Range(0f, 1f)] private float deathSoundSpatialBlend = 0.75f;
    [SerializeField, Min(0.01f)] private float deathSoundMaxDistance = 16f;
    [Header("World Attribute Window")]
    [SerializeField] private Vector2 attributeWindowOffset = Vector2.zero;
    [SerializeField, Min(0.1f)] private float attributeWindowScale = 2.2f;

    private int currentHealth;
    private bool isDead;
    private float damageFeedbackTimer;
    private SpriteRenderer damageFeedbackRenderer;
    private Color damageFeedbackBaseColor;
    private Vector3 damageFeedbackBaseLocalPosition;
    private bool hasDamageFeedbackBaseVisual;
    private ZeldaCharacterStatsWindow attributeWindow;
    private int spentGrowthPossessionEnergy;
    private AudioSource attackAudioSource;
    private AudioSource damageAudioSource;

    public int Health => health;
    public int CurrentHealth => currentHealth;
    public int SkillValue => skillValue;
    public int 技巧值 => skillValue;
    public int PermissionLevel => permissionLevel;
    public int PossessionEnergy => possessionEnergy;
    public int PossessionCost => possessionCost;
    public bool IsDead => isDead;
    public float MoveSpeed => moveSpeed;
    public virtual bool CanAttack => false;
    public virtual bool CanTakeAttackDamage => true;
    public virtual int AttackPower => 0;
    public int FinalAttackPower => AttackPower + GetControlledGrowthAttackPower();
    public int FinalSkillValue => skillValue + GetControlledGrowthSkillValue();
    public int FinalPossessionEnergy =>
        possessionEnergy + Mathf.Max(
            0,
            GetControlledGrowthPossessionEnergy() - spentGrowthPossessionEnergy);
    public virtual GameObject AttackPrefab => null;
    public virtual float AttackDuration => 0f;
    public virtual Vector2 AttackSpawnOffset => Vector2.zero;
    public virtual Vector2 AttackSize => Vector2.zero;
    public virtual bool CanInteractWithDoors => true;
    public virtual bool CanBeDetectedByAi => true;
    public virtual bool DestroyAfterControlSwitch => false;
    public virtual bool ParticipatesInCharacterCollision => true;
    public virtual ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Box;
    public virtual Color AttackVisualTint => new Color(1f, 0.85f, 0.2f, 0.65f);
    public bool IsShowingDamageFeedback => damageFeedbackTimer > 0f;
    public event Action ValuesChanged;

    /// <summary>Plays this character's configured attack sound.</summary>
    public void PlayAttackSound()
    {
        if (attackSound == null || attackSoundVolume <= 0f)
        {
            return;
        }

        EnsureAttackAudioSource();
        attackAudioSource.pitch = Mathf.Clamp(attackSoundPitch, 0.1f, 3f);
        attackAudioSource.spatialBlend = Mathf.Clamp01(attackSoundSpatialBlend);
        attackAudioSource.maxDistance = Mathf.Max(0.01f, attackSoundMaxDistance);
        attackAudioSource.PlayOneShot(attackSound, Mathf.Clamp01(attackSoundVolume));
    }

    private void PlayDamageSound()
    {
        if (damageSound == null || damageSoundVolume <= 0f)
        {
            return;
        }

        EnsureDamageAudioSource();
        ConfigureAudioSource(
            damageAudioSource,
            damageSoundPitch,
            damageSoundSpatialBlend,
            damageSoundMaxDistance);
        damageAudioSource.PlayOneShot(damageSound, Mathf.Clamp01(damageSoundVolume));
    }

    private void PlayDeathSound()
    {
        if (deathSound == null || deathSoundVolume <= 0f)
        {
            return;
        }

        GameObject soundObject = new GameObject(name + " Death Sound");
        soundObject.transform.position = transform.position;
        AudioSource source = soundObject.AddComponent<AudioSource>();
        InitializeAudioSource(source);
        ConfigureAudioSource(
            source,
            deathSoundPitch,
            deathSoundSpatialBlend,
            deathSoundMaxDistance);
        source.clip = deathSound;
        source.volume = Mathf.Clamp01(deathSoundVolume);
        source.Play();

        float playbackDuration = deathSound.length /
            Mathf.Max(0.1f, Mathf.Abs(source.pitch));
        Destroy(soundObject, playbackDuration + 0.1f);
    }

    public RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            characterType = GetType().AssemblyQualifiedName,
            health = health,
            currentHealth = currentHealth,
            skillValue = skillValue,
            permissionLevel = permissionLevel,
            possessionEnergy = possessionEnergy,
            possessionCost = possessionCost,
            spentGrowthPossessionEnergy = spentGrowthPossessionEnergy
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        health = Mathf.Max(1, state.health);
        currentHealth = Mathf.Clamp(state.currentHealth, 0, health);
        skillValue = Mathf.Max(0, state.skillValue);
        permissionLevel = Mathf.Clamp(
            state.permissionLevel,
            MinimumPermissionLevel,
            MaximumPermissionLevel);
        possessionEnergy = Mathf.Max(0, state.possessionEnergy);
        possessionCost = Mathf.Max(0, state.possessionCost);
        spentGrowthPossessionEnergy = Mathf.Max(0, state.spentGrowthPossessionEnergy);
        isDead = currentHealth <= 0;
        ValuesChanged?.Invoke();
    }

    public virtual void ApplyCharacterVisual(SpriteRenderer spriteRenderer, Vector2 facingDirection, bool isMoving, bool isAttacking)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        Color baseColor = isMoving ? Color.white : new Color(0.86f, 1f, 0.86f, 1f);
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    protected virtual void Awake()
    {
        currentHealth = health;
        EnsureAttributeWindow();
    }

    protected virtual void Update()
    {
        UpdateDamageFeedback(Time.deltaTime);
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    public bool TrySpendPossessionEnergy(int cost)
    {
        int safeCost = Mathf.Max(0, cost);
        if (FinalPossessionEnergy < safeCost)
        {
            return false;
        }

        int spentFromBase = Mathf.Min(possessionEnergy, safeCost);
        possessionEnergy -= spentFromBase;
        spentGrowthPossessionEnergy += safeCost - spentFromBase;
        ValuesChanged?.Invoke();
        return true;
    }

    private bool IsPlayerControlled()
    {
        ZeldaFourWayMover mover = GetComponent<ZeldaFourWayMover>();
        return mover != null && mover.isActiveAndEnabled;
    }

    private int GetControlledGrowthAttackPower()
    {
        PlayerGrowthAttributes growth = PlayerGrowthAttributes.Instance;
        return IsPlayerControlled() && growth != null ? growth.AttackPower : 0;
    }

    private int GetControlledGrowthSkillValue()
    {
        PlayerGrowthAttributes growth = PlayerGrowthAttributes.Instance;
        return IsPlayerControlled() && growth != null ? growth.SkillValue : 0;
    }

    private int GetControlledGrowthPossessionEnergy()
    {
        PlayerGrowthAttributes growth = PlayerGrowthAttributes.Instance;
        return IsPlayerControlled() && growth != null ? growth.PossessionEnergy : 0;
    }

    /// <summary>
    /// Removes this character after a successful possession while reusing the
    /// normal death notification, explosion and cleanup sequence.
    /// </summary>
    public void DestroyAfterSuccessfulPossession()
    {
        Die();
    }

    public void TakeDamage(int damage, ZeldaCharacterData attacker)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - damage);
        ValuesChanged?.Invoke();
        PlayDamageSound();
        ZeldaFourWayMover damagedMover = GetComponent<ZeldaFourWayMover>();
        if (damagedMover != null)
        {
            damagedMover.InterruptPossessionByDamage();
        }
        CaptureDamageFeedbackBaseVisual();
        damageFeedbackTimer = damageFeedbackDuration;
        Debug.Log($"{name} took {damage} damage. Current health: {currentHealth}/{health}", this);
        if (attacker != null)
        {
            BroadcastMessage("OnCharacterDamagedBy", attacker, SendMessageOptions.DontRequireReceiver);
        }

        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void UpdateDamageFeedback(float deltaTime)
    {
        if (damageFeedbackTimer <= 0f)
        {
            return;
        }

        damageFeedbackTimer = Mathf.Max(0f, damageFeedbackTimer - deltaTime);

        if (damageFeedbackRenderer != null)
        {
            damageFeedbackRenderer.color = GetDamageFeedbackTint(damageFeedbackBaseColor);
            damageFeedbackRenderer.transform.localPosition = damageFeedbackBaseLocalPosition + GetDamageShakeOffset();
        }

        if (damageFeedbackTimer <= 0f)
        {
            RestoreDamageFeedbackBaseVisual();
        }
    }

    public Color GetDamageFeedbackTint(Color baseColor)
    {
        if (!IsShowingDamageFeedback)
        {
            return baseColor;
        }

        return Color.Lerp(baseColor, damageTint, 0.75f);
    }

    public Vector3 GetDamageShakeOffset()
    {
        if (!IsShowingDamageFeedback || damageShakeAmount <= 0f)
        {
            return Vector3.zero;
        }

        float shake = Mathf.Sin(Time.time * damageShakeSpeed) * damageShakeAmount;
        return new Vector3(shake, 0f, 0f);
    }

    private void CaptureDamageFeedbackBaseVisual()
    {
        damageFeedbackRenderer = EnsureDamageFeedbackRenderer();
        if (damageFeedbackRenderer == null)
        {
            hasDamageFeedbackBaseVisual = false;
            return;
        }

        if (hasDamageFeedbackBaseVisual)
        {
            return;
        }

        damageFeedbackBaseColor = damageFeedbackRenderer.color;
        damageFeedbackBaseLocalPosition = damageFeedbackRenderer.transform.localPosition;
        hasDamageFeedbackBaseVisual = true;
    }

    private void RestoreDamageFeedbackBaseVisual()
    {
        if (!hasDamageFeedbackBaseVisual || damageFeedbackRenderer == null)
        {
            return;
        }

        damageFeedbackRenderer.color = damageFeedbackBaseColor;
        damageFeedbackRenderer.transform.localPosition = damageFeedbackBaseLocalPosition;
        hasDamageFeedbackBaseVisual = false;
    }

    private SpriteRenderer EnsureDamageFeedbackRenderer()
    {
        Transform visualTransform = transform.Find(VisualObjectName);
        if (visualTransform != null)
        {
            SpriteRenderer visualRenderer = visualTransform.GetComponent<SpriteRenderer>();
            if (visualRenderer != null)
            {
                return visualRenderer;
            }
        }

        SpriteRenderer rootRenderer = GetComponent<SpriteRenderer>();
        if (rootRenderer == null)
        {
            return GetComponentInChildren<SpriteRenderer>(true);
        }

        if (visualTransform == null)
        {
            GameObject visualObject = new GameObject(VisualObjectName);
            visualObject.transform.SetParent(transform, false);
            visualTransform = visualObject.transform;
        }

        SpriteRenderer childRenderer = visualTransform.GetComponent<SpriteRenderer>();
        if (childRenderer == null)
        {
            childRenderer = visualTransform.gameObject.AddComponent<SpriteRenderer>();
        }

        childRenderer.sortingLayerID = rootRenderer.sortingLayerID;
        childRenderer.sortingOrder = rootRenderer.sortingOrder;
        childRenderer.sprite = rootRenderer.sprite;
        childRenderer.color = rootRenderer.color;

        rootRenderer.enabled = false;
        return childRenderer;
    }

    protected virtual void Die()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        PlayDeathSound();
        Debug.Log($"{name} died.", this);
        BroadcastMessage("OnCharacterDied", this, SendMessageOptions.DontRequireReceiver);
        SpawnDeathExplosion();
        Destroy(gameObject);
    }

    protected virtual void OnValidate()
    {
        health = Mathf.Max(1, health);
        skillValue = Mathf.Max(0, skillValue);
        permissionLevel = Mathf.Clamp(
            permissionLevel,
            MinimumPermissionLevel,
            MaximumPermissionLevel);
        possessionEnergy = Mathf.Max(0, possessionEnergy);
        possessionCost = Mathf.Max(0, possessionCost);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        deathExplosionDuration = Mathf.Max(0f, deathExplosionDuration);
        deathExplosionScale = Mathf.Max(0f, deathExplosionScale);
        damageFeedbackDuration = Mathf.Max(0f, damageFeedbackDuration);
        damageShakeAmount = Mathf.Max(0f, damageShakeAmount);
        damageShakeSpeed = Mathf.Max(0f, damageShakeSpeed);
        attackSoundVolume = Mathf.Clamp01(attackSoundVolume);
        attackSoundPitch = Mathf.Clamp(attackSoundPitch, 0.1f, 3f);
        attackSoundSpatialBlend = Mathf.Clamp01(attackSoundSpatialBlend);
        attackSoundMaxDistance = Mathf.Max(0.01f, attackSoundMaxDistance);
        damageSoundVolume = Mathf.Clamp01(damageSoundVolume);
        damageSoundPitch = Mathf.Clamp(damageSoundPitch, 0.1f, 3f);
        damageSoundSpatialBlend = Mathf.Clamp01(damageSoundSpatialBlend);
        damageSoundMaxDistance = Mathf.Max(0.01f, damageSoundMaxDistance);
        deathSoundVolume = Mathf.Clamp01(deathSoundVolume);
        deathSoundPitch = Mathf.Clamp(deathSoundPitch, 0.1f, 3f);
        deathSoundSpatialBlend = Mathf.Clamp01(deathSoundSpatialBlend);
        deathSoundMaxDistance = Mathf.Max(0.01f, deathSoundMaxDistance);
        attributeWindowScale = Mathf.Max(0.1f, attributeWindowScale);

        if (Application.isPlaying && attributeWindow != null)
        {
            attributeWindow.Configure(this, attributeWindowOffset, attributeWindowScale);
        }
    }

    private void EnsureAttributeWindow()
    {
        attributeWindow = GetComponentInChildren<ZeldaCharacterStatsWindow>(true);
        if (attributeWindow == null)
        {
            GameObject windowObject = new GameObject("Character Attribute Window");
            windowObject.transform.SetParent(transform, false);
            attributeWindow = windowObject.AddComponent<ZeldaCharacterStatsWindow>();
        }

        attributeWindow.Configure(this, attributeWindowOffset, attributeWindowScale);
    }

    private void EnsureAttackAudioSource()
    {
        if (attackAudioSource != null)
        {
            return;
        }

        attackAudioSource = gameObject.AddComponent<AudioSource>();
        InitializeAudioSource(attackAudioSource);
    }

    private void EnsureDamageAudioSource()
    {
        if (damageAudioSource != null)
        {
            return;
        }

        damageAudioSource = gameObject.AddComponent<AudioSource>();
        InitializeAudioSource(damageAudioSource);
    }

    private static void InitializeAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.dopplerLevel = 0f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = 1f;
    }

    private static void ConfigureAudioSource(
        AudioSource source,
        float pitch,
        float spatialBlend,
        float maxDistance)
    {
        source.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.maxDistance = Mathf.Max(0.01f, maxDistance);
    }

    private void SpawnDeathExplosion()
    {
        GameObject explosionObject = new GameObject($"{name} Death Explosion");
        explosionObject.transform.position = transform.position;

        explosionObject.AddComponent<SpriteRenderer>();
        ZeldaDeathExplosionVisual explosion = explosionObject.AddComponent<ZeldaDeathExplosionVisual>();
        explosion.Configure(deathExplosionDuration, deathExplosionScale, deathExplosionTint);
    }
}
