using System;
using UnityEngine;

public class ZeldaCharacterData : ZeldaCharacterCommonData
{
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
        public int spentBasePossessionEnergy;
        public int crystalEnergyThirds;
        public int appliedHealthBonus;
        public float ghostFormRemaining;
        public float ghostFormDuration;
        public bool hasCharacterSkillState;
        // Legacy save fields only; these are not editable character configuration.
        public bool combatExpertiseSkill;
        public bool commandSkill;
        public float combatExpertiseRemaining;
        public bool combatExpertiseRegeneration;
    }

    private const string VisualObjectName = "Visual";

    private static Sprite commandIcon;
    private static readonly Sprite[] rankedCommandIcons = new Sprite[2];
    public static Sprite GetRankedCommandIcon(int rank)
    {
        rank = Mathf.Clamp(rank, 1, 2);
        if (rankedCommandIcons[rank - 1] == null) rankedCommandIcons[rank - 1] = CreateCommandIcon(rank);
        return rankedCommandIcons[rank - 1];
    }
    public static Sprite CommandIcon
    {
        get
        {
            if (commandIcon == null) commandIcon = CreateCommandIcon(0);
            return commandIcon;
        }
    }
    private static Sprite CreateCommandIcon(int rank)
    {
            string[] rows = {
                ".................", "........#........", "...#...###...#...",
                "...##.#####.##...", "...###########...", "....#########....",
                "....#########....", ".................", "..#############..",
                ".................", "...#....#....#...", "..###..###..###..",
                "...#....#....#...", ".................", ".................",
                ".................", "................."
            };
            int size = rank > 0 ? 22 : 17;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Royal Command Icon"; texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                bool crown = y < 17 && x < 17 && rows[y][x] == '#';
                bool numeral = rank > 0 && y >= 15 && y <= 21 && x >= 14 && x <= 21 &&
                    (y == 15 || y == 21 || (rank == 1 ? x == 18 : x == 16 || x == 19));
                texture.SetPixel(x, size-1-y, crown || numeral ? Color.white : Color.clear);
            }
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0,0,size,size), Vector2.one * 0.5f, size);
    }
    private float combatExpertiseRemaining;
    private bool combatExpertiseRegeneration;
    public const float CombatExpertiseDuration = 3f;
    public bool IsCombatExpertiseActive => !isDead && combatExpertiseRemaining > 0f;
    public float CombatExpertiseProgress => Mathf.Clamp01(combatExpertiseRemaining / CombatExpertiseDuration);

    private int currentHealth;
    private bool isDead;
    private float damageFeedbackTimer;
    private SpriteRenderer damageFeedbackRenderer;
    private Color damageFeedbackBaseColor;
    private Vector3 damageFeedbackBaseLocalPosition;
    private bool hasDamageFeedbackBaseVisual;
    private ZeldaCharacterStatsWindow attributeWindow;
    private int spentGrowthPossessionEnergy;
    private int spentBasePossessionEnergy;
    private bool soulDetonationDeath;
    private int crystalEnergyThirds;
    public int CrystalEnergyThirds => crystalEnergyThirds;
    public int BaseMaxPossessionEnergy => possessionEnergy + spentBasePossessionEnergy + GetControlledGrowthPossessionEnergy();
    public int MaxPossessionEnergy => BaseMaxPossessionEnergy + crystalEnergyThirds / 3;
    public void RestorePossessionEnergy(int amount)
    {
        if (IsDead || amount <= 0) return;
        int restored = Mathf.Min(amount, spentBasePossessionEnergy);
        possessionEnergy += restored;
        spentBasePossessionEnergy -= restored;
        int growthRestored = Mathf.Min(amount - restored, Mathf.Min(spentGrowthPossessionEnergy, GetControlledGrowthPossessionEnergy()));
        spentGrowthPossessionEnergy -= growthRestored;
        if (restored + growthRestored > 0) ValuesChanged?.Invoke();
    }
    public bool UseStabilityCrystal()
    {
        int missing = spentBasePossessionEnergy + Mathf.Min(spentGrowthPossessionEnergy, GetControlledGrowthPossessionEnergy());
        if (missing > 0)
        {
            int restored = Mathf.Min(2, spentBasePossessionEnergy);
            possessionEnergy += restored;
            spentBasePossessionEnergy -= restored;
            spentGrowthPossessionEnergy = Mathf.Max(0, spentGrowthPossessionEnergy - (2 - restored));
        }
        else if (crystalEnergyThirds < 3) crystalEnergyThirds++;
        else return false;
        ValuesChanged?.Invoke();
        return true;
    }
    public void ClearCurrentPossessionEnergy()
    {
        TrySpendPossessionEnergy(FinalPossessionEnergy);
        crystalEnergyThirds = 0;
        ValuesChanged?.Invoke();
    }
    private int appliedHealthBonus;
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
    private GhostFormRuntime ghostForm;
    public GhostFormRuntime GhostForm
    {
        get => ghostForm;
        internal set
        {
            if (ghostForm == value) return;
            ghostForm = value;
            // A form change affects presentation even when every numeric attribute stays the same.
            ValuesChanged?.Invoke();
        }
    }
    public bool IsGhostForm => GhostForm != null && GhostForm.Remaining > 0f;
    public bool IsGhostLike => this is GhostZeldaCharacterData || IsGhostForm;
    public virtual Color GhostFormEyeColor => new Color(0.05f, 0.12f, 0.22f, 1f);
    public float MoveSpeed => moveSpeed * (IsGhostForm ? 2f : 1f);
    public virtual bool CanAttack => false;
    // The separate HUD slot (key 0) belongs to the possessed body, not the player's equipped skills.
    private NativeCharacterSkillData nativeSkillData;
    private NativeCharacterSkillData NativeSkillData => nativeSkillData != null
        ? nativeSkillData : (nativeSkillData = GetComponent<NativeCharacterSkillData>());

    public virtual string CharacterSkillName => NativeSkillData != null ? NativeSkillData.SkillName : string.Empty;
    public virtual Sprite CharacterSkillIcon => NativeSkillData != null ? NativeSkillData.Icon : null;
    public virtual string NativeCharacterSkillId => NativeSkillData != null ? NativeSkillData.SkillId : null;
    public bool TryUseRoyalCommand()
    {
        var growth = PlayerGrowthAttributes.Instance;
        int level = growth != null ? growth.RoyalCommandLevel : 0;
        if ((NativeCharacterSkillId != "royal_command" && level == 0) || IsDead || IsGhostLike || !IsPlayerControlled() || DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        var user = GetComponent<ZeldaFourWayMover>();
        var skillScene = ZeldaRuntimeRegistry.GetGameplayScene(gameObject);
        var candidates = new System.Collections.Generic.List<ZeldaCharacterAiBase>();
        foreach (var ai in ZeldaRuntimeRegistry.AiCharacters)
            if (ai != null && ZeldaRuntimeRegistry.GetGameplayScene(ai.gameObject) == skillScene && ai.CanReceiveRoyalCommand(user)) candidates.Add(ai);
        candidates.Sort((a,b) => {
            int distance = (a.transform.position-transform.position).sqrMagnitude.CompareTo((b.transform.position-transform.position).sqrMagnitude);
            return distance != 0 ? distance : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });
        if (candidates.Count == 0 || !TrySpendPossessionEnergy(1)) return false;
        int targetLimit = level >= 2 ? 5 : 3;
        for (int i = 0; i < Mathf.Min(targetLimit, candidates.Count); i++) candidates[i].ReceiveRoyalCommand();
        return true;
    }
    private FearRoarArea activeFearRoar;
    public bool TryUseFearRoar(float shakeStrength = 0.12f, float shakeFrequency = 22f)
    {
        var growth = PlayerGrowthAttributes.Instance;
        int level = growth != null ? growth.FearRoarLevel : 0;
        if ((NativeCharacterSkillId != "fear_roar" && level == 0) || IsDead || IsGhostLike ||
            activeFearRoar != null || !IsPlayerControlled() || DocumentReader.IsInputBlocked ||
            ClockworkPuppetRuntime.BlocksCharacterInput || !TrySpendPossessionEnergy(3)) return false;
        activeFearRoar = new GameObject("Fear Roar Area").AddComponent<FearRoarArea>();
        activeFearRoar.Initialize(this, Mathf.Max(1, level));
        var camera = Camera.main;
        if (camera != null) camera.GetComponent<CameraFollowActiveZeldaMover>()?.PlayShake(1f, shakeStrength, shakeFrequency);
        return true;
    }
    public virtual bool TryUseCharacterSkill() => NativeSkillData != null && NativeSkillData.TryUse(this);
    public bool TryUseCombatExpertise()
    {
        bool learned = PlayerGrowthAttributes.Instance != null && PlayerGrowthAttributes.Instance.HasSkill("combat_expertise");
        if ((NativeCharacterSkillId != "combat_expertise" && !learned) || isDead || IsGhostLike || IsCombatExpertiseActive || !IsPlayerControlled() ||
            DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput) return false;
        if (!TrySpendPossessionEnergy(2)) return false;
        combatExpertiseRemaining = CombatExpertiseDuration;
        combatExpertiseRegeneration = PlayerGrowthAttributes.Instance != null && PlayerGrowthAttributes.Instance.HasSkill("combat_expertise_2");
        ValuesChanged?.Invoke();
        return true;
    }
    public virtual bool CanTakeAttackDamage => !IsGhostForm;
    public virtual int AttackPower => 0;
    public int FinalAttackPower => (AttackPower + GetControlledGrowthAttackPower()) * (IsCombatExpertiseActive ? 2 : 1);
    public int FinalSkillValue => skillValue + GetControlledGrowthSkillValue();
    public int FinalPossessionEnergy =>
        crystalEnergyThirds / 3 + possessionEnergy + Mathf.Max(
            0,
            GetControlledGrowthPossessionEnergy() - spentGrowthPossessionEnergy);
    public virtual GameObject AttackPrefab => null;
    public virtual float AttackDuration => 0f;
    public virtual Vector2 AttackSpawnOffset => Vector2.zero;
    public virtual Vector2 AttackSize => Vector2.zero;
    public virtual bool CanInteractWithDoors => !IsGhostForm;
    public virtual bool CanBeDetectedByAi => !IsGhostForm;
    public virtual bool DestroyAfterControlSwitch => false;
    public virtual bool ParticipatesInCharacterCollision => !IsGhostForm;
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
            spentGrowthPossessionEnergy = spentGrowthPossessionEnergy,
            spentBasePossessionEnergy = spentBasePossessionEnergy,
            crystalEnergyThirds = crystalEnergyThirds,
            appliedHealthBonus = appliedHealthBonus,
            ghostFormRemaining = IsGhostForm ? GhostForm.Remaining : 0f,
            ghostFormDuration = IsGhostForm ? GhostForm.TotalDuration : 0f,
            hasCharacterSkillState = true,
            combatExpertiseSkill = NativeCharacterSkillId == "combat_expertise",
            commandSkill = NativeCharacterSkillId == "royal_command",
            combatExpertiseRemaining = combatExpertiseRemaining,
            combatExpertiseRegeneration = combatExpertiseRegeneration
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        health = Mathf.Max(1, state.health);
        appliedHealthBonus = Mathf.Max(0, state.appliedHealthBonus);
        currentHealth = Mathf.Clamp(state.currentHealth, 0, health);
        skillValue = Mathf.Max(0, state.skillValue);
        permissionLevel = Mathf.Clamp(
            state.permissionLevel,
            MinimumPermissionLevel,
            MaximumPermissionLevel);
        possessionEnergy = Mathf.Max(0, state.possessionEnergy);
        possessionCost = Mathf.Max(0, state.possessionCost);
        spentGrowthPossessionEnergy = Mathf.Max(0, state.spentGrowthPossessionEnergy);
        spentBasePossessionEnergy = Mathf.Max(0, state.spentBasePossessionEnergy);
        crystalEnergyThirds = Mathf.Clamp(state.crystalEnergyThirds, 0, 3);
        isDead = currentHealth <= 0;
        // Legacy ownership flags remain in the save DTO only. Native ownership now
        // comes from the prefab's data component, never from mutable save flags.
        combatExpertiseRegeneration = state.combatExpertiseRegeneration;
        combatExpertiseRemaining = !isDead
            ? Mathf.Clamp(state.combatExpertiseRemaining, 0f, CombatExpertiseDuration) : 0f;
        GhostFormRuntime.Restore(this, state.ghostFormRemaining, state.ghostFormDuration);
        ValuesChanged?.Invoke();
    }

    private PixelCharacterWalkAnimator walkAnimator;

    public void ApplyCharacterVisualWithMovement(SpriteRenderer renderer, Vector2 facing, bool moving, bool attacking)
    {
        ApplyCharacterVisual(renderer, facing, moving, attacking);
        // Paladin has its own authored animation. Unknown future types opt in explicitly.
        if (!PixelCharacterWalkAnimator.Supports(this)) return;
        if (walkAnimator == null && moving && !attacking && !IsDead && renderer != null)
        {
            walkAnimator = GetComponent<PixelCharacterWalkAnimator>();
            if (walkAnimator == null) walkAnimator = gameObject.AddComponent<PixelCharacterWalkAnimator>();
            walkAnimator.Initialize(this);
        }
        if (walkAnimator != null) walkAnimator.Apply(renderer, moving && !IsDead, attacking);
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
        if (combatExpertiseRemaining > 0f)
        {
            int previousTicks = Mathf.FloorToInt(CombatExpertiseDuration - combatExpertiseRemaining);
            combatExpertiseRemaining = Mathf.Max(0f, combatExpertiseRemaining - Time.deltaTime);
            int ticks = Mathf.FloorToInt(CombatExpertiseDuration - combatExpertiseRemaining) - previousTicks;
            if (combatExpertiseRegeneration && !isDead && ticks > 0 && currentHealth < health)
            {
                currentHealth = Mathf.Min(health, currentHealth + ticks);
                ValuesChanged?.Invoke();
            }
            if (combatExpertiseRemaining <= 0f) ValuesChanged?.Invoke();
        }
    }

    public void ApplyPlayerHealthUpgrade()
    {
        if (GameSaveSystem.IsLoading) return;
        var stateManager = SceneTravelStateManager.Instance;
        if (stateManager != null && (stateManager.IsSceneTravelRestoreInProgress || stateManager.IsQuickRestartPending)) return;
        if (isDead || this is GhostZeldaCharacterData || !IsPlayerControlled()) return;
        var growth = PlayerGrowthAttributes.Instance;
        int bonus = growth != null ? growth.HealthBonus : 0;
        if (bonus <= appliedHealthBonus) return;
        int increase = bonus - appliedHealthBonus;
        health += increase;
        currentHealth += increase;
        appliedHealthBonus = bonus;
        ValuesChanged?.Invoke();
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
        spentBasePossessionEnergy += spentFromBase;
        int remaining = safeCost - spentFromBase;
        int spentFromGrowth = Mathf.Min(remaining, Mathf.Max(0, GetControlledGrowthPossessionEnergy() - spentGrowthPossessionEnergy));
        spentGrowthPossessionEnergy += spentFromGrowth;
        crystalEnergyThirds -= (remaining - spentFromGrowth) * 3;
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

    public void DieFromSoulDetonation()
    {
        soulDetonationDeath = true;
        currentHealth = 0;
        Die();
    }

    internal void DieFromDomino()
    {
        currentHealth = 0;
        Die();
    }

    public void TakeDamage(int damage, ZeldaCharacterData attacker)
    {
        ApplyDamage(damage, attacker, false);
    }

    internal void TakeDominoDamage(int damage, ZeldaCharacterData attacker)
    {
        ApplyDamage(damage, attacker, true);
    }

    private void ApplyDamage(int damage, ZeldaCharacterData attacker, bool shared)
    {
        if (damage <= 0 || currentHealth <= 0)
        {
            return;
        }

        ZeldaFourWayMover damagedMover = GetComponent<ZeldaFourWayMover>();
        CardboardBoxWearState wornBox = damagedMover != null
            ? damagedMover.ActiveCardboardBox
            : null;
        if (!shared && wornBox != null && wornBox.TryBlockAttack(attacker))
        {
            return;
        }

        ZeldaCharacterAiBase damagedAi = GetComponent<ZeldaCharacterAiBase>();
        int resolvedDamage = !shared && damagedAi != null
            ? damagedAi.ResolveIncomingAttackDamage(damage)
            : damage;

        currentHealth = Mathf.Max(0, currentHealth - resolvedDamage);
        ValuesChanged?.Invoke();
        PlayDamageSound();
        if (damagedMover != null)
        {
            damagedMover.InterruptPossessionByDamage();
        }
        CaptureDamageFeedbackBaseVisual();
        damageFeedbackTimer = damageFeedbackDuration;
        Debug.Log($"{name} took {resolvedDamage} damage. Current health: {currentHealth}/{health}", this);
        if (attacker != null)
        {
            BroadcastMessage("OnCharacterDamagedBy", attacker, SendMessageOptions.DontRequireReceiver);
        }

        if (!shared) DominoSkillRuntime.ShareDamage(this, resolvedDamage, attacker);
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
        DominoSkillRuntime.ShareDeath(this);
        PlayDeathSound();
        Debug.Log($"{name} died.", this);
        BroadcastMessage("OnCharacterDied", this, SendMessageOptions.DontRequireReceiver);
        if (!soulDetonationDeath) SpawnDeathExplosion();
        Destroy(gameObject);
    }

    protected virtual void OnValidate()
    {
        ValidateCommonData();

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
