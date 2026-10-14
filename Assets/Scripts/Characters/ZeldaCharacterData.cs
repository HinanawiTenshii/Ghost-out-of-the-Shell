using System;
using UnityEngine;

public class ZeldaCharacterData : ZeldaCharacterCommonData
{
    [Serializable]
    public struct RuntimeState
    {
        public string characterType;
        public string characterSpecificState;
        public string individualAccessId;
        public string persistentCharacterId;
        public string summonedPrefabName;
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
        public CharacterPotionBuffs.RuntimeState potionBuffs;
        public HolyShieldRuntime.State holyShield;
    }

    private const string VisualObjectName = "Visual";
    [Header("Persistent Identity / 跨场景角色绑定")]
    [SerializeField, Tooltip("为特定角色分配独立的身份资产，并在召唤法阵等机关中引用同一资产。不要给不同角色复用同一身份。")]
    private CharacterPersistentIdentity persistentIdentity;
    private string restoredPersistentCharacterId;
    public CharacterPersistentIdentity PersistentIdentity => persistentIdentity;
    internal void ClearPersistentIdentityForSummon()
    {
        persistentIdentity = null;
        restoredPersistentCharacterId = null;
    }
    public string PersistentCharacterId => !string.IsNullOrEmpty(restoredPersistentCharacterId)
        ? restoredPersistentCharacterId : persistentIdentity != null ? persistentIdentity.StableId : string.Empty;
    private string individualAccessId;
    // Runtime identity follows the body through possession, travel and save restoration.
    public string IndividualAccessId => string.IsNullOrEmpty(individualAccessId)
        ? (individualAccessId = Guid.NewGuid().ToString("N")) : individualAccessId;

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
    public Sprite CombatExpertiseBuffIcon => combatExpertiseRegeneration
        ? ZeldaHealthHeartsUI.GetRankedCombatExpertiseIcon(2)
        : NativeCharacterSkillId == "combat_expertise" &&
          (PlayerGrowthAttributes.Instance == null || !PlayerGrowthAttributes.Instance.HasSkill("combat_expertise"))
            ? CharacterSkillIcon : ZeldaHealthHeartsUI.GetRankedCombatExpertiseIcon(1);

    private int currentHealth;
    private bool isDead;
    private HolyShieldRuntime.State holyShield;
    public int HolyShieldCharges => !isDead ? holyShield.charges : 0;
    public int HolyShieldCapacity => holyShield.capacity;
    public bool HolyShieldPreservesCharges => holyShield.preserveCharges;
    public int HolyShieldBlockVersion { get; private set; }

    internal void RefreshHolyShield(int capacity, bool preserveCharges)
    {
        holyShield = HolyShieldRuntime.Sanitize(new HolyShieldRuntime.State {
            charges = capacity, capacity = capacity, preserveCharges = preserveCharges });
        ValuesChanged?.Invoke();
    }
    private float damageFeedbackTimer;
    private SpriteRenderer damageFeedbackRenderer;
    private Color damageFeedbackBaseColor;
    private Vector3 damageFeedbackBaseLocalPosition;
    private bool hasDamageFeedbackBaseVisual;
    private ZeldaCharacterStatsWindow attributeWindow;
    private int spentGrowthPossessionEnergy;
    private int spentBasePossessionEnergy;
    private bool soulDetonationDeath;
    private bool silentGhostPossessionDeath;
    private int controlTransferDamage;
    private int crystalEnergyThirds;
    public int CrystalEnergyThirds => crystalEnergyThirds;
    public int BaseMaxPossessionEnergy => possessionEnergy + spentBasePossessionEnergy + GetControlledGrowthPossessionEnergy();
    public int MaxPossessionEnergy => BaseMaxPossessionEnergy + crystalEnergyThirds / 3 + GetPotionEnergyMaximum();
    // The crystal's partial thirds still occupy one display slot. Potion reserve
    // slots follow it, but never contribute to the fill of ordinary energy slots.
    public int PossessionEnergyDisplaySlotCount => Mathf.Max(0, BaseMaxPossessionEnergy) +
        (crystalEnergyThirds > 0 ? 1 : 0) + GetPotionEnergyMaximum();
    public bool IsCrystalPossessionEnergyDisplaySlot(int index) =>
        crystalEnergyThirds > 0 && index == Mathf.Max(0, BaseMaxPossessionEnergy);
    public float GetPossessionEnergyDisplayFill(int index)
    {
        if (index < 0 || index >= PossessionEnergyDisplaySlotCount) return 0f;
        int baseCount = Mathf.Max(0, BaseMaxPossessionEnergy);
        if (index < baseCount)
        {
            int baseCurrent = possessionEnergy + Mathf.Max(0,
                GetControlledGrowthPossessionEnergy() - spentGrowthPossessionEnergy);
            return index < baseCurrent ? 1f : 0f;
        }
        if (IsCrystalPossessionEnergyDisplaySlot(index)) return Mathf.Clamp01(crystalEnergyThirds / 3f);
        int potionIndex = index - baseCount - (crystalEnergyThirds > 0 ? 1 : 0);
        return potionIndex < GetPotionEnergyCurrent() ? 1f : 0f;
    }
    public void RestorePossessionEnergy(int amount)
    {
        if (IsDead || amount <= 0) return;
        int restored = Mathf.Min(amount, spentBasePossessionEnergy);
        possessionEnergy += restored;
        spentBasePossessionEnergy -= restored;
        int growthRestored = Mathf.Min(amount - restored, Mathf.Min(spentGrowthPossessionEnergy, GetControlledGrowthPossessionEnergy()));
        spentGrowthPossessionEnergy -= growthRestored;
        int potionAmount = Mathf.Min(amount - restored - growthRestored,
            GetPotionEnergyMaximum() - GetPotionEnergyCurrent());
        var bodyBuffs = GetComponent<CharacterPotionBuffs>();
        int bodyRestored = bodyBuffs != null ? bodyBuffs.RestoreBodySpirit(potionAmount) : 0;
        int playerRestored = IsPlayerControlled() && PlayerGrowthAttributes.Instance != null
            ? PlayerGrowthAttributes.Instance.RestorePotionEnergy(potionAmount) : 0;
        int potionRestored = Mathf.Max(bodyRestored, playerRestored);
        if (restored + growthRestored + potionRestored > 0) ValuesChanged?.Invoke();
    }
    public bool UseStabilityCrystal()
    {
        int missing = MaxPossessionEnergy - FinalPossessionEnergy;
        if (missing > 0)
        {
            RestorePossessionEnergy(2);
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
    private static AudioClip defaultDamageSound;
    private static AudioClip defaultDeathSound;

    public int Health => Mathf.Max(1, health + CharacterPotionBuffs.HealthModifier(this));
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
    public float MoveSpeed => moveSpeed * (IsGhostForm ? 2f : 1f) *
        CharacterPotionBuffs.SpeedMultiplier(this) * SpeedBoostArea.GetSpeedMultiplier(this);
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
    public int FinalAttackPower => CharacterPotionBuffs.Has(this, AlchemyPotionKind.Weakness) ? 1 :
        (AttackPower + GetControlledGrowthAttackPower() + CharacterPotionBuffs.AttackBonus(this)) * (IsCombatExpertiseActive ? 2 : 1);
    public int FinalSkillValue => skillValue + GetControlledGrowthSkillValue();
    public int FinalPossessionEnergy =>
        GetPotionEnergyCurrent() + crystalEnergyThirds / 3 + possessionEnergy + Mathf.Max(
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
    // Bound protectors receive direct evidence even for anonymous gunfire, before death.
    public event Action<ZeldaCharacterData> AttackedBy;
    public static event Action<ZeldaCharacterData> AnyCharacterDied;

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
        if (damageSoundVolume <= 0f)
        {
            return;
        }

        AudioClip clip = damageSound;
        if (clip == null)
        {
            if (defaultDamageSound == null)
                defaultDamageSound = Resources.Load<AudioClip>("Audio/CharacterDamage");
            clip = defaultDamageSound;
        }
        if (clip == null) return;

        EnsureDamageAudioSource();
        ConfigureAudioSource(
            damageAudioSource,
            damageSoundPitch,
            damageSoundSpatialBlend,
            damageSoundMaxDistance);
        damageAudioSource.PlayOneShot(clip, Mathf.Clamp01(damageSoundVolume));
    }

    protected virtual void PlayDeathSound()
    {
        if (deathSoundVolume <= 0f)
        {
            return;
        }

        AudioClip clip = deathSound;
        if (clip == null)
        {
            if (defaultDeathSound == null)
                defaultDeathSound = Resources.Load<AudioClip>("Audio/CharacterDeath");
            clip = defaultDeathSound;
        }
        if (clip == null) return;

        GameObject soundObject = new GameObject(name + " Death Sound");
        soundObject.transform.position = transform.position;
        // The voice survives body destruction, but belongs to the current level
        // (including when the dying player is stored in DontDestroyOnLoad).
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            soundObject, ZeldaRuntimeRegistry.GetGameplayScene(gameObject));
        soundObject.AddComponent<CameraVisionStreamingExempt>();
        AudioSource source = soundObject.AddComponent<AudioSource>();
        InitializeAudioSource(source);
        ConfigureAudioSource(
            source,
            deathSoundPitch,
            deathSoundSpatialBlend,
            deathSoundMaxDistance);
        source.clip = clip;
        source.volume = Mathf.Clamp01(deathSoundVolume);
        source.Play();

        float playbackDuration = clip.length /
            Mathf.Max(0.1f, Mathf.Abs(source.pitch));
        Destroy(soundObject, playbackDuration + 0.1f);
    }

    public virtual RuntimeState CaptureRuntimeState()
    {
        return new RuntimeState
        {
            characterType = GetType().AssemblyQualifiedName,
            individualAccessId = IndividualAccessId,
            persistentCharacterId = PersistentCharacterId,
            summonedPrefabName = GetComponent<SummonedCharacterOrigin>()?.PrefabName,
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
            combatExpertiseRegeneration = combatExpertiseRegeneration,
            holyShield = holyShield,
            potionBuffs = GetComponent<CharacterPotionBuffs>() != null
                ? GetComponent<CharacterPotionBuffs>().CaptureRuntimeState() : default
        };
    }

    public virtual void ApplyRuntimeState(RuntimeState state)
    {
        if (!string.IsNullOrEmpty(state.individualAccessId)) individualAccessId = state.individualAccessId;
        restoredPersistentCharacterId = state.persistentCharacterId;
        if (!string.IsNullOrEmpty(state.summonedPrefabName))
        {
            var origin = GetComponent<SummonedCharacterOrigin>();
            if (origin == null) origin = gameObject.AddComponent<SummonedCharacterOrigin>();
            origin.Initialize(state.summonedPrefabName);
            ClearPersistentIdentityForSummon();
        }
        health = Mathf.Max(1, state.health);
        appliedHealthBonus = Mathf.Max(0, state.appliedHealthBonus);
        isDead = state.currentHealth <= 0;
        var potionBuffs = GetComponent<CharacterPotionBuffs>();
        if (potionBuffs == null && state.potionBuffs.remaining != null)
            potionBuffs = gameObject.AddComponent<CharacterPotionBuffs>();
        if (potionBuffs != null) potionBuffs.ApplyRuntimeState(state.potionBuffs);
        currentHealth = Mathf.Clamp(state.currentHealth, 0, Health);
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
        holyShield = !isDead ? HolyShieldRuntime.Sanitize(state.holyShield) : default;
        HolyShieldBlockVersion = 0;
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
            if (combatExpertiseRegeneration && !isDead && ticks > 0 && currentHealth < Health)
            {
                currentHealth = Mathf.Min(Health, currentHealth + ticks);
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
        currentHealth = Mathf.Min(Health, currentHealth + increase);
        appliedHealthBonus = bonus;
        ValuesChanged?.Invoke();
    }

    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null);
    }

    /// <summary>Restores living physical characters, without damage/hostility or resurrection side effects.</summary>
    public int RestoreHealth(int amount)
    {
        if (amount <= 0 || isDead || IsGhostLike || currentHealth <= 0 || currentHealth >= Health) return 0;
        int restored = Mathf.Min(amount, Health - currentHealth);
        currentHealth += restored;
        ValuesChanged?.Invoke();
        return restored;
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
        remaining -= spentFromGrowth;
        int spentFromCrystal = Mathf.Min(remaining, crystalEnergyThirds / 3);
        crystalEnergyThirds -= spentFromCrystal * 3;
        remaining -= spentFromCrystal;
        // Concurrent body/player Spirit sources share at most two usable slots, never +4.
        if (remaining > 0) GetComponent<CharacterPotionBuffs>()?.SpendBodySpirit(remaining);
        if (remaining > 0 && IsPlayerControlled() && PlayerGrowthAttributes.Instance != null)
            PlayerGrowthAttributes.Instance.SpendPotionEnergy(remaining);
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

    private int GetPotionEnergyMaximum() => Mathf.Max(
        GetComponent<CharacterPotionBuffs>()?.BodySpiritMaximum ?? 0,
        IsPlayerControlled() && PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.PotionEnergyMaximum : 0);
    private int GetPotionEnergyCurrent() => Mathf.Max(
        GetComponent<CharacterPotionBuffs>()?.BodySpiritCurrent ?? 0,
        IsPlayerControlled() && PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.PotionEnergyCurrent : 0);

    internal void RefreshPotionDerivedAttributes()
    {
        if (!isDead) currentHealth = Mathf.Min(currentHealth, Health);
        ValuesChanged?.Invoke();
    }

    public void KillFromPotion()
    {
        if (isDead) return;
        controlTransferDamage = 0;
        Die();
    }

    /// <summary>
    /// Removes this character after a successful possession while reusing the
    /// normal death notification, explosion and cleanup sequence.
    /// </summary>
    public void DestroyAfterSuccessfulPossession()
    {
        if (isDead) return;
        // Transferring consciousness out of a ghost is not an audible death.
        silentGhostPossessionDeath = IsGhostLike;
        controlTransferDamage = Mathf.Max(0, currentHealth);
        Die();
    }

    public void DieFromSoulDetonation()
    {
        if (isDead) return;
        controlTransferDamage = Mathf.Max(0, currentHealth);
        soulDetonationDeath = true;
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

    /// <summary>A distant gun hit reveals a firing location, not the shooter's identity.</summary>
    public void TakeHandgunDamage(int damage, Vector2 firingPosition, ZeldaCharacterData shooter = null)
    {
        int healthBeforeHit = currentHealth;
        int shieldBeforeHit = HolyShieldBlockVersion;
        // Keep normal mitigation, feedback and domino propagation. A null attacker
        // deliberately prevents retaliation/witness evidence, including on lethal hits.
        ApplyDamage(damage, null, false, shooter);
        if (!isDead && (currentHealth < healthBeforeHit || HolyShieldBlockVersion != shieldBeforeHit))
        {
            var ai = GetComponent<ZeldaCharacterAiBase>();
            if (ai != null) ai.InvestigateHandgunShot(firingPosition);
        }
    }

    private void ApplyDamage(int damage, ZeldaCharacterData attacker, bool shared, ZeldaCharacterData evidenceSource = null)
    {
        if (ArenaChallengeController.LimitDamage(this, damage) <= 0) return;
        if (isDead || damage <= 0 || currentHealth <= 0 || CharacterPotionBuffs.IsInvulnerable(this))
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

        if (resolvedDamage > 0 && HolyShieldRuntime.TryAbsorb(ref holyShield))
        {
            HolyShieldBlockVersion++;
            ValuesChanged?.Invoke();
            if (damagedMover != null) damagedMover.InterruptPossessionByDamage();
            // The hit remains an offence even though no HP was lost. It must not
            // trigger Echo, death, or transfer damage that the shield fully absorbed.
            NotifyAttackEvidence(attacker, evidenceSource, shared);
            return;
        }

        resolvedDamage = ArenaChallengeController.LimitDamage(this, resolvedDamage);
        int echoHealth = 0;
        bool echoed = resolvedDamage >= currentHealth && CharacterPotionBuffs.TryPreventLethalDamage(this, out echoHealth);
        currentHealth = echoed ? echoHealth : Mathf.Max(0, currentHealth - resolvedDamage);
        ArenaChallengeController.HealthChanged(this);
        ValuesChanged?.Invoke();
        // A fatal hit is communicated by the death cue, not two simultaneous sounds.
        if (currentHealth > 0) PlayDamageSound();
        if (damagedMover != null)
        {
            damagedMover.InterruptPossessionByDamage();
        }
        CaptureDamageFeedbackBaseVisual();
        damageFeedbackTimer = damageFeedbackDuration;
        Debug.Log($"{name} took {resolvedDamage} damage. Current health: {currentHealth}/{health}", this);
        NotifyAttackEvidence(attacker, evidenceSource, shared);

        if (!shared) DominoSkillRuntime.ShareDamage(this, resolvedDamage, attacker);
        if (currentHealth == 0)
        {
            Die();
        }
    }

    private void NotifyAttackEvidence(ZeldaCharacterData attacker, ZeldaCharacterData evidenceSource, bool shared)
    {
        if (!shared && (attacker != null || evidenceSource != null))
            AttackedBy?.Invoke(attacker != null ? attacker : evidenceSource);
        if (attacker != null)
        {
            BroadcastMessage("OnCharacterDamagedBy", attacker, SendMessageOptions.DontRequireReceiver);
            // Dispatch before death/linked deaths remove the victim from the AI registry.
            // Witnesses must not depend on a surviving victim's next AI update.
            if (!shared) ZeldaCharacterAiBase.NotifyPlayerAttackWitnesses(attacker, this);
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

    // Articulated characters own several renderers and must not shake a path/
    // collider transform. They still use the shared damage timer, tint and audio.
    protected virtual bool UsesSingleRendererDamageFeedback => true;

    private void CaptureDamageFeedbackBaseVisual()
    {
        if (!UsesSingleRendererDamageFeedback) return;
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
        // A transfer intentionally abandons the registered body; ordinary lethal
        // effects (including poison/direct death APIs) obey the nonlethal arena rule.
        if ((controlTransferDamage == 0 || soulDetonationDeath) && ArenaChallengeController.PreventDeath(this))
        {
            currentHealth = 1;
            soulDetonationDeath = false;
            ArenaChallengeController.HealthChanged(this);
            ValuesChanged?.Invoke();
            return;
        }
        if (isDead)
        {
            return;
        }

        isDead = true;
        currentHealth = 0;
        holyShield = default;
        // Ordinary hits already shared their resolved damage. Only a control-transfer
        // death has no incoming hit, so share its captured remaining HP exactly once.
        // Mark dead first to guard callbacks, but keep the link until the snapshot is taken.
        if (controlTransferDamage > 0) DominoSkillRuntime.ShareDamage(this, controlTransferDamage, this);
        DominoSkillRuntime.RemoveMark(this);
        if (damageAudioSource != null) damageAudioSource.Stop();
        if (!silentGhostPossessionDeath) PlayDeathSound();
        Debug.Log($"{name} died.", this);
        AnyCharacterDied?.Invoke(this);
        BroadcastMessage("OnCharacterDied", this, SendMessageOptions.DontRequireReceiver);
        // Keep death/possession/quest notifications, but replace eligible remains
        // with the ritual's purple fragments instead of a second death effect.
        if (SummoningCircle.TryAbsorbDeath(this))
        {
            Destroy(gameObject);
            return;
        }
        if (TryHandleDeathRemains()) return;
        if (!soulDetonationDeath) SpawnDeathExplosion();
        Destroy(gameObject);
    }

    // Default remains unchanged; special creatures can retain a dead, inert body.
    protected virtual bool TryHandleDeathRemains()
    {
        return false;
    }

    protected void RestoreAfterDeath()
    {
        isDead = false;
        currentHealth = Health;
        controlTransferDamage = 0;
        soulDetonationDeath = false;
        silentGhostPossessionDeath = false;
        damageFeedbackTimer = 0f;
        ValuesChanged?.Invoke();
    }

    protected virtual void OnValidate()
    {
        ValidateCommonData();

        if (Application.isPlaying && attributeWindow != null)
        {
            attributeWindow.Configure(this, attributeWindowOffset, attributeWindowScale);
        }
    }

    // Articulated creatures may move their visible body independently of the
    // stationary root that owns their stats and roaming-area configuration.
    public virtual Transform AttributeWindowAnchor => transform;

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
        if (deathExplosionDuration <= 0f || deathExplosionScale <= 0f) return;
        // Prefer the actual body, not the disabled root sprite or child HUD/weapon sprites.
        Transform visual = transform.Find(VisualObjectName);
        SpriteRenderer bodyRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (bodyRenderer == null || bodyRenderer.sprite == null) bodyRenderer = damageFeedbackRenderer;
        if (bodyRenderer == null || bodyRenderer.sprite == null) bodyRenderer = GetComponent<SpriteRenderer>();

        Vector3 center = transform.position;
        float effectScale;
        if (bodyRenderer != null && bodyRenderer.sprite != null)
        {
            Vector2 localSize = bodyRenderer.drawMode == SpriteDrawMode.Simple
                ? (Vector2)bodyRenderer.sprite.bounds.size : bodyRenderer.size;
            effectScale = CalculateDeathExplosionScale(localSize, bodyRenderer.transform.lossyScale, deathExplosionScale);
            center = bodyRenderer.bounds.center;
        }
        else
        {
            Collider2D bodyCollider = GetComponent<Collider2D>();
            effectScale = bodyCollider != null
                ? CalculateDeathExplosionScale(bodyCollider.bounds.size, Vector3.one, deathExplosionScale)
                : CalculateDeathExplosionScale(Vector2.one, transform.lossyScale, deathExplosionScale);
            if (bodyCollider != null) center = bodyCollider.bounds.center;
        }
        GameObject explosionObject = new GameObject($"{name} Death Explosion");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
            explosionObject, ZeldaRuntimeRegistry.GetGameplayScene(gameObject));
        explosionObject.AddComponent<CameraVisionStreamingExempt>();
        explosionObject.transform.position = center;

        explosionObject.AddComponent<SpriteRenderer>();
        ZeldaDeathExplosionVisual explosion = explosionObject.AddComponent<ZeldaDeathExplosionVisual>();
        explosion.Configure(deathExplosionDuration, effectScale, deathExplosionTint,
            bodyRenderer != null ? bodyRenderer.sortingLayerID : 0,
            bodyRenderer != null ? bodyRenderer.sortingOrder + 2 : 4);
    }

    private static float CalculateDeathExplosionScale(Vector2 localSize, Vector3 worldScale, float effectMultiplier)
    {
        // Sprite dimensions already account for pixels-per-unit; include parent/instance scale.
        // A one-world-unit body preserves the original authored effect scale. No camera/player input.
        float diameter = Mathf.Max(Mathf.Abs(localSize.x * worldScale.x), Mathf.Abs(localSize.y * worldScale.y));
        return diameter * Mathf.Max(0f, effectMultiplier);
    }
}
